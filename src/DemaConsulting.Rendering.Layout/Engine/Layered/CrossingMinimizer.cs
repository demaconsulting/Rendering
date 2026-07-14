// <copyright file="CrossingMinimizer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>
using static DemaConsulting.Rendering.Layout.Engine.Layered.LayeredLayoutMetrics;

namespace DemaConsulting.Rendering.Layout.Engine.Layered;

/// <summary>
/// Pipeline stage that reduces edge crossings via Barycenter ordering over the augmented graph.
/// </summary>
internal sealed class CrossingMinimizer : ILayoutStage
{
    /// <inheritdoc/>
    public void Apply(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        graph.Groups = GroupByLayerAug(graph.AugNodes);
        OrderLayersAug(graph.Groups, graph.AugNodes.Count, graph.AugEdges);
    }

    /// <summary>
    /// Minimizes crossings across every nesting level of an assembled merge region with hierarchical
    /// port-order propagation, mirroring ELK's per-level sweep-with-recursion, and sets each level's
    /// <see cref="LayeredGraph.Groups"/> to the resolved ordering.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Each level is minimized over its own <see cref="LayeredGraph"/> so its layers stay local to
    ///     the container's own box; the levels are coupled only through their hierarchy-crossing dummies,
    ///     which pair by <see cref="BoundaryPort.Port"/> identity — the <see cref="HierarchyCrossingFace.External"/>
    ///     crossing in a parent level and the <see cref="HierarchyCrossingFace.Internal"/> crossing in the
    ///     matching child level stand for the same boundary port.
    ///     </para>
    ///     <para>
    ///     The recursion is a genuine two-direction hierarchical sweep. <em>Up-sweep</em>: every child
    ///     subtree is resolved first (innermost first), and the child's resolved incoming-crossing order
    ///     seeds this level's matching outgoing crossings, so a child order propagates upward whenever
    ///     this level's own external pressure is neutral. <em>Down-sweep</em>: this level's resolved
    ///     outgoing-crossing order — decided under outer-scope crossing pressure — is then fed back down
    ///     as a fixed order for each child's incoming crossings and the child is re-minimized, so an
    ///     ordinary (non-boundary) interior node genuinely reorders under outer-scope pressure rather
    ///     than only bottom-up. The flat <see cref="Apply"/> entry point is unchanged and still governs
    ///     every single-level graph.
    ///     </para>
    /// </remarks>
    /// <param name="level">The nesting level whose subtree is minimized.</param>
    /// <param name="levels">The lookup from each level to its assembled layered graph and crossing bookkeeping.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="level"/> or <paramref name="levels"/> is <see langword="null"/>.</exception>
    internal void MinimizeCrossingsRecursive(
        MergeRegionLevel level,
        IReadOnlyDictionary<MergeRegionLevel, LevelLayeredGraph> levels)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(levels);

        // Up-sweep: resolve every child subtree first, then read each child's resolved incoming-crossing
        // (Internal face) order as this level's preferred order for the matching outgoing crossings.
        var childPreference = new Dictionary<LayoutGraphPort, int>();
        var rank = 0;
        foreach (var (_, _, child) in level.Children)
        {
            MinimizeCrossingsRecursive(child, levels);
            foreach (var port in ReadCrossingOrder(levels[child], HierarchyCrossingFace.Internal))
            {
                childPreference[port] = rank++;
            }
        }

        // Order this level, seeding its outgoing crossings with the children's preferred order so a
        // resolved child order propagates upward when this level's own external pressure does not decide.
        var levelGraph = levels[level];
        OrderWithSeed(levelGraph, HierarchyCrossingFace.External, childPreference);

        // Down-sweep: feed this level's resolved outgoing-crossing order back down into each child as a
        // fixed order for that child's incoming crossings, re-minimizing the child subtree so an ordinary
        // interior node reorders under outer-scope pressure.
        var externalOrder = ReadCrossingRanks(levelGraph, HierarchyCrossingFace.External);
        foreach (var (_, _, child) in level.Children)
        {
            PropagateDown(child, levels, externalOrder);
        }
    }

    /// <summary>
    /// Re-minimizes <paramref name="level"/>'s subtree with its incoming crossings pinned to
    /// <paramref name="pinnedIncoming"/> (the parent's resolved outgoing order), then propagates the
    /// resulting outgoing order further down into every nested child.
    /// </summary>
    /// <param name="level">The child level being re-minimized under outer-scope pressure.</param>
    /// <param name="levels">The lookup from each level to its assembled layered graph.</param>
    /// <param name="pinnedIncoming">The fixed cross-axis rank per boundary port for this level's incoming crossings.</param>
    private void PropagateDown(
        MergeRegionLevel level,
        IReadOnlyDictionary<MergeRegionLevel, LevelLayeredGraph> levels,
        IReadOnlyDictionary<LayoutGraphPort, int> pinnedIncoming)
    {
        var levelGraph = levels[level];
        OrderWithPinnedIncoming(levelGraph, pinnedIncoming);

        var externalOrder = ReadCrossingRanks(levelGraph, HierarchyCrossingFace.External);
        foreach (var (_, _, child) in level.Children)
        {
            PropagateDown(child, levels, externalOrder);
        }
    }

    /// <summary>
    /// Groups <paramref name="levelGraph"/> by layer, seeds the cross-axis order of the crossings on
    /// <paramref name="face"/> from <paramref name="seed"/> (a tie-break honored only when this level's
    /// own barycenter pressure is neutral), then runs the standard barycenter sweeps.
    /// </summary>
    /// <param name="levelGraph">The level graph to order.</param>
    /// <param name="face">The crossing face whose initial order is seeded.</param>
    /// <param name="seed">The preferred cross-axis rank per boundary port; ports absent from it are left in place.</param>
    private static void OrderWithSeed(
        LevelLayeredGraph levelGraph,
        HierarchyCrossingFace face,
        IReadOnlyDictionary<LayoutGraphPort, int> seed)
    {
        var graph = levelGraph.Graph;
        graph.Groups = GroupByLayerAug(graph.AugNodes);
        SeedCrossingOrder(graph.Groups, levelGraph.Crossings, face, seed);
        OrderLayersAug(graph.Groups, graph.AugNodes.Count, graph.AugEdges);
    }

    /// <summary>
    /// Groups <paramref name="levelGraph"/> by layer, pins the cross-axis order of its incoming crossings
    /// to <paramref name="pinnedIncoming"/>, then propagates that fixed order forward through the interior
    /// layers only, so the pinned crossings are never reordered by a reverse sweep.
    /// </summary>
    /// <param name="levelGraph">The level graph to re-order under a fixed incoming order.</param>
    /// <param name="pinnedIncoming">The fixed cross-axis rank per boundary port for the incoming crossings.</param>
    private static void OrderWithPinnedIncoming(
        LevelLayeredGraph levelGraph,
        IReadOnlyDictionary<LayoutGraphPort, int> pinnedIncoming)
    {
        var graph = levelGraph.Graph;
        graph.Groups = GroupByLayerAug(graph.AugNodes);
        SeedCrossingOrder(graph.Groups, levelGraph.Crossings, HierarchyCrossingFace.Internal, pinnedIncoming);
        ForwardPropagate(graph.Groups, graph.AugNodes.Count, graph.AugEdges);
    }

    /// <summary>
    /// Returns the boundary ports of the crossings on <paramref name="face"/> in their resolved
    /// cross-axis order (their position within their layer in <paramref name="levelGraph"/>'s groups).
    /// </summary>
    /// <param name="levelGraph">The already-ordered level graph.</param>
    /// <param name="face">The crossing face to read.</param>
    /// <returns>The boundary ports in cross-axis order.</returns>
    private static IReadOnlyList<LayoutGraphPort> ReadCrossingOrder(
        LevelLayeredGraph levelGraph,
        HierarchyCrossingFace face)
    {
        var positions = PositionsInLayer(levelGraph.Graph.Groups);
        return levelGraph.Crossings
            .Where(crossing => crossing.Face == face)
            .OrderBy(crossing => positions.TryGetValue(crossing.NodeIndex, out var p) ? p : int.MaxValue)
            .Select(crossing => crossing.Boundary.Port)
            .ToList();
    }

    /// <summary>
    /// Returns the crossings on <paramref name="face"/> as a lookup from boundary port to resolved
    /// cross-axis rank (0-based, in resolved order).
    /// </summary>
    /// <param name="levelGraph">The already-ordered level graph.</param>
    /// <param name="face">The crossing face to read.</param>
    /// <returns>A lookup from boundary port to cross-axis rank.</returns>
    private static IReadOnlyDictionary<LayoutGraphPort, int> ReadCrossingRanks(
        LevelLayeredGraph levelGraph,
        HierarchyCrossingFace face)
    {
        var order = ReadCrossingOrder(levelGraph, face);
        var ranks = new Dictionary<LayoutGraphPort, int>();
        for (var i = 0; i < order.Count; i++)
        {
            ranks[order[i]] = i;
        }

        return ranks;
    }

    /// <summary>
    /// Reorders, in place, the crossings on <paramref name="face"/> within each layer they occupy so
    /// they appear in ascending <paramref name="seed"/> rank at the slot positions they already hold,
    /// leaving every non-crossing node fixed.
    /// </summary>
    /// <param name="groups">The per-layer ordered index lists to seed.</param>
    /// <param name="crossings">The level's crossing metadata.</param>
    /// <param name="face">The crossing face to reorder.</param>
    /// <param name="seed">The preferred cross-axis rank per boundary port.</param>
    private static void SeedCrossingOrder(
        List<List<int>> groups,
        IReadOnlyList<LevelCrossing> crossings,
        HierarchyCrossingFace face,
        IReadOnlyDictionary<LayoutGraphPort, int> seed)
    {
        // Map each seeded crossing node index to its desired rank.
        var desiredRank = new Dictionary<int, int>();
        foreach (var crossing in crossings)
        {
            if (crossing.Face == face && seed.TryGetValue(crossing.Boundary.Port, out var r))
            {
                desiredRank[crossing.NodeIndex] = r;
            }
        }

        if (desiredRank.Count == 0)
        {
            return;
        }

        foreach (var layer in groups)
        {
            // The slot positions this layer's seeded crossings occupy, and the crossing node indices.
            var slots = new List<int>();
            var seededNodes = new List<int>();
            for (var i = 0; i < layer.Count; i++)
            {
                if (desiredRank.ContainsKey(layer[i]))
                {
                    slots.Add(i);
                    seededNodes.Add(layer[i]);
                }
            }

            if (seededNodes.Count <= 1)
            {
                continue;
            }

            // Sort the crossing nodes by desired rank and write them back into the same slot positions.
            seededNodes.Sort((a, b) => desiredRank[a].CompareTo(desiredRank[b]));
            for (var i = 0; i < slots.Count; i++)
            {
                layer[slots[i]] = seededNodes[i];
            }
        }
    }

    /// <summary>Returns a lookup from augmented-node index to its position within its layer group.</summary>
    /// <param name="groups">The per-layer ordered index lists.</param>
    /// <returns>A lookup from node index to its cross-axis position within its layer.</returns>
    private static Dictionary<int, int> PositionsInLayer(List<List<int>> groups)
    {
        var positions = new Dictionary<int, int>();
        foreach (var layer in groups)
        {
            for (var i = 0; i < layer.Count; i++)
            {
                positions[layer[i]] = i;
            }
        }

        return positions;
    }

    /// <summary>
    /// Orders every interior layer (1..) by the barycenter of its left neighbors, propagating the fixed
    /// order of layer 0 rightward without any reverse sweep, so pinned layer-0 crossings stay fixed.
    /// </summary>
    /// <param name="groups">The per-layer ordered index lists, ordered in place.</param>
    /// <param name="numAug">The number of augmented nodes.</param>
    /// <param name="augEdges">The augmented sub-edges.</param>
    private static void ForwardPropagate(List<List<int>> groups, int numAug, List<AugEdge> augEdges)
    {
        var leftNeighbors = new List<int>[numAug];
        for (var i = 0; i < numAug; i++)
        {
            leftNeighbors[i] = [];
        }

        foreach (var ae in augEdges)
        {
            leftNeighbors[ae.Target].Add(ae.Source);
        }

        var isolated = BuildIsolatedFlags(numAug, augEdges);

        // Two forward passes give the interior layers a stable order relative to the pinned layer 0.
        for (var pass = 0; pass < 2; pass++)
        {
            for (var l = 1; l < groups.Count; l++)
            {
                SortByBarycenter(groups[l], groups[l - 1], leftNeighbors, isolated);
            }
        }
    }

    /// <summary>
    /// Builds a per-augmented-node flag reporting whether the node has zero incident
    /// <see cref="AugEdge"/>s in <em>either</em> direction — i.e. it is genuinely disconnected from the
    /// rest of the graph, not merely a leaf or root relative to one sweep direction.
    /// </summary>
    /// <remarks>
    /// <see cref="SortByBarycenter"/> is called once per sweep direction with only that direction's
    /// neighbor lists (<c>leftNeighbors</c> or <c>rightNeighbors</c>). A perfectly ordinary node with no
    /// outgoing edges (a "sink" partway through the layering, not just in the final layer) has an empty
    /// <c>rightNeighbors</c> entry and would otherwise be mistaken for an isolated node during an
    /// upward/right-facing sweep, even though it has real incoming edges from the left. This flag is
    /// computed once from the full, direction-agnostic <paramref name="augEdges"/> list — a node counts
    /// as isolated here only when it never appears as a <see cref="AugEdge.Source"/> or
    /// <see cref="AugEdge.Target"/> at all — so <see cref="SortByBarycenter"/> can tell "no neighbors this
    /// direction" apart from "no neighbors, ever" and only cluster the latter at the trailing end of the
    /// layer.
    /// </remarks>
    /// <param name="numAug">The number of augmented nodes.</param>
    /// <param name="augEdges">The augmented sub-edges.</param>
    /// <returns>An array indexed by augmented-node index; <see langword="true"/> for genuinely isolated nodes.</returns>
    private static bool[] BuildIsolatedFlags(int numAug, List<AugEdge> augEdges)
    {
        var isolated = new bool[numAug];
        Array.Fill(isolated, true);
        foreach (var ae in augEdges)
        {
            isolated[ae.Source] = false;
            isolated[ae.Target] = false;
        }

        return isolated;
    }

    /// <summary>Groups augmented-node indices by layer.</summary>
    private static List<List<int>> GroupByLayerAug(List<AugNode> augNodes)
    {
        var maxLayer = augNodes.Max(a => a.Layer);
        var groups = new List<List<int>>(maxLayer + 1);
        for (var l = 0; l <= maxLayer; l++)
        {
            groups.Add([]);
        }

        for (var i = 0; i < augNodes.Count; i++)
        {
            groups[augNodes[i].Layer].Add(i);
        }

        return groups;
    }

    /// <summary>
    /// Runs <see cref="BarycentricSweeps"/> Barycenter sweeps over the augmented graph
    /// (real nodes and dummies) to reduce edge crossings.
    /// </summary>
    private static void OrderLayersAug(List<List<int>> groups, int numAug, List<AugEdge> augEdges)
    {
        var leftNeighbors = new List<int>[numAug];
        var rightNeighbors = new List<int>[numAug];
        for (var i = 0; i < numAug; i++)
        {
            leftNeighbors[i] = [];
            rightNeighbors[i] = [];
        }

        foreach (var ae in augEdges)
        {
            rightNeighbors[ae.Source].Add(ae.Target);
            leftNeighbors[ae.Target].Add(ae.Source);
        }

        var isolated = BuildIsolatedFlags(numAug, augEdges);

        for (var sweep = 0; sweep < BarycentricSweeps; sweep++)
        {
            if (sweep % 2 == 0)
            {
                for (var l = 1; l < groups.Count; l++)
                {
                    SortByBarycenter(groups[l], groups[l - 1], leftNeighbors, isolated);
                }
            }
            else
            {
                for (var l = groups.Count - 2; l >= 0; l--)
                {
                    SortByBarycenter(groups[l], groups[l + 1], rightNeighbors, isolated);
                }
            }
        }
    }

    /// <summary>
    /// Sorts <paramref name="layer"/> by the average position of each node's neighbors in
    /// <paramref name="adjacentLayer"/>; nodes with no neighbors at all in the graph are clustered at the
    /// trailing end of the layer (after every neighbor-bearing node) rather than kept at their current
    /// index. Nodes that simply have no neighbor on <em>this</em> side (e.g. a leaf during a
    /// right-facing sweep) keep the prior stable-index fallback so their ordering is unaffected.
    /// </summary>
    /// <remarks>
    /// A node with no same-layer-adjacent neighbors in <paramref name="neighbors"/> falls into one of two
    /// distinct cases, distinguished by <paramref name="isolated"/>:
    ///     <list type="bullet">
    ///         <item>
    ///         It is genuinely isolated — zero incident edges anywhere in the graph. A dummy bend-point
    ///         node always has at least one incident edge by construction, so only a real node can be
    ///         isolated this way. Falling back to the node's current index (<c>i</c>) for such a node lets
    ///         it land anywhere in the layer, including directly beside an unrelated dummy chain whose
    ///         barycenter key happens to sort near that index; because dummies carry zero height, this
    ///         later shows up in <see cref="BrandesKopfPlacer"/>'s coordinate assignment as a single
    ///         inflated gap (the isolated node inherits the dummy's compaction floor). Using
    ///         <see cref="double.PositiveInfinity"/> instead pushes every isolated node to the end of the
    ///         layer's barycenter order, past every neighbor-bearing node (dummy or real), so isolated
    ///         nodes always end up contiguous with each other at one end of the layer — turning scattered
    ///         inflated gaps into at most one gap, which <see cref="BrandesKopfPlacer"/> then squeezes down
    ///         to <see cref="NodeSpacing"/>.
    ///         </item>
    ///         <item>
    ///         It has real edges, just none of them run to <paramref name="adjacentLayer"/> on this
    ///         particular sweep (e.g. a node with no outgoing edges evaluated during a right-facing sweep,
    ///         whose only edges are incoming from the left). Such a node is not isolated and must keep the
    ///         original stable-index fallback (<c>key = i</c>) exactly as before this change, otherwise an
    ///         ordinary leaf/root node would be misclassified as isolated and reordered on every sweep that
    ///         looks the "wrong" way, changing layouts for graphs that contain no isolated node at all.
    ///         </item>
    ///     </list>
    /// The final <c>Original</c> tie-break still applies among nodes sharing the same key (including
    /// isolated nodes, which all share <see cref="double.PositiveInfinity"/>), so relative order among
    /// them is preserved exactly as it was before this change.
    /// </remarks>
    /// <param name="layer">The layer whose node order is updated in place.</param>
    /// <param name="adjacentLayer">The neighboring layer used as the ordering reference for this sweep.</param>
    /// <param name="neighbors">Per-node neighbor lists in the direction of this sweep (left or right).</param>
    /// <param name="isolated">
    /// Per-node flag reporting whether the node has zero incident edges in either direction at all, as
    /// produced by <see cref="BuildIsolatedFlags"/>.
    /// </param>
    private static void SortByBarycenter(
        List<int> layer,
        List<int> adjacentLayer,
        List<int>[] neighbors,
        bool[] isolated)
    {
        var position = new Dictionary<int, int>();
        for (var i = 0; i < adjacentLayer.Count; i++)
        {
            position[adjacentLayer[i]] = i;
        }

        var keyed = new List<(int Node, double Key, int Original)>(layer.Count);
        for (var i = 0; i < layer.Count; i++)
        {
            var node = layer[i];
            var ns = neighbors[node].Where(position.ContainsKey).ToList();
            double key;
            if (ns.Count > 0)
            {
                key = ns.Average(x => position[x]);
            }
            else if (isolated[node])
            {
                key = double.PositiveInfinity;
            }
            else
            {
                key = i;
            }

            keyed.Add((node, key, i));
        }

        keyed.Sort((a, b) =>
        {
            var c = a.Key.CompareTo(b.Key);
            return c != 0 ? c : a.Original.CompareTo(b.Original);
        });

        for (var i = 0; i < layer.Count; i++)
        {
            layer[i] = keyed[i].Node;
        }
    }
}
