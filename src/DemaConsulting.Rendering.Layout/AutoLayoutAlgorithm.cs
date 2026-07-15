// <copyright file="AutoLayoutAlgorithm.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout.Engine;

namespace DemaConsulting.Rendering.Layout;

/// <summary>
/// The bundled auto-routing meta-algorithm: splits the input graph into its connected top-level
/// components, routes each component to whichever bundled leaf algorithm best suits its shape, lays
/// each component out independently, and packs the resulting placed sub-trees into one combined
/// <see cref="LayoutTree"/> with <see cref="LayoutTreePacker"/>.
/// </summary>
/// <remarks>
///     <para>
///     <strong>Routing rule.</strong> Every top-level node is assigned to a connected component using
///     the graph's top-level edges (mirroring <see cref="LayeredLayoutAlgorithm"/>'s and
///     <see cref="HierarchicalLayoutAlgorithm"/>'s own node/port endpoint resolution — replicated here
///     rather than shared, since each caller's resolution serves a slightly different purpose and the
///     three implementations are independently small). Each component is then routed by shape:
///     </para>
///     <list type="bullet">
///         <item><description>
///         A component containing any node with <see cref="LayoutGraphNode.HasChildren"/> is routed to
///         <see cref="HierarchicalLayoutAlgorithm"/> (which recurses further into any nesting on its
///         own), regardless of the component's size — a single isolated container node still needs the
///         hierarchical engine to lay out its children.
///         </description></item>
///         <item><description>
///         A component with two or more nodes and no children anywhere in it (including a single node
///         carrying only a self-loop edge) is routed to <see cref="LayeredLayoutAlgorithm"/>, since it
///         has genuine connectivity for the layered engine's Sugiyama layering to exploit.
///         </description></item>
///         <item><description>
///         A truly childless, edgeless singleton node — no children, no incident edges at all — carries
///         no connectivity or nesting information a layered or hierarchical layout could use, so every
///         such singleton across the whole graph is instead gathered into one shared bucket routed
///         through <see cref="ContainmentLayoutAlgorithm"/>, which packs unrelated peer boxes into a
///         balanced block.
///         </description></item>
///     </list>
///     <para>
///     <strong>Fast path: nothing to split.</strong> When the routing above produces exactly one group
///     overall (either a single non-singleton component, or every top-level node is a childless,
///     edgeless singleton), the graph is not split at all: it is delegated directly, unchanged, to that
///     one leaf algorithm's <see cref="LayoutAlgorithmBase.ApplyCore"/>, so the result is byte-for-byte
///     identical to invoking that algorithm directly. This mirrors
///     <see cref="HierarchicalLayoutAlgorithm"/>'s own flat-graph equivalence guarantee, and is the
///     common case: a single fully (or mostly) connected diagram never pays any splitting or copying
///     cost.
///     </para>
///     <para>
///     <strong>Sub-graph construction copies, it cannot reuse, node instances.</strong> When more than
///     one group is produced, each group is laid out on its own freshly-built <see cref="LayoutGraph"/>
///     rather than a shared view of the original: the public <see cref="LayoutGraph"/>/
///     <see cref="LayoutGraphNode"/> API offers no way to insert an existing node instance into a
///     different graph's node list, and <see cref="LayoutGraphNode.Children"/> has no setter, so an
///     original node cannot simply be attached, by reference, to a new parent graph. Every node in a
///     split-off component is therefore copied field-by-field — <see cref="LayoutGraphNode.Label"/>,
///     <see cref="LayoutGraphNode.Shape"/>, <see cref="LayoutGraphNode.Keyword"/>,
///     <see cref="LayoutGraphNode.Compartments"/>, <see cref="LayoutGraphNode.TitleHeight"/>,
///     <see cref="LayoutGraphNode.RoundedCornerRadius"/>, <see cref="LayoutGraphNode.FolderTabWidth"/>,
///     <see cref="LayoutGraphNode.FolderTabHeight"/>, its named ports, and (recursively) its entire
///     nested <see cref="LayoutGraphNode.Children"/> subgraph — with edges re-added afterward once every
///     node and port in the component has a copy, so both direct and cross-container edge endpoints
///     resolve correctly regardless of nesting depth.
///     </para>
///     <para>
///     <strong>Known, disclosed limitation.</strong> A node's or edge's own arbitrary
///     <see cref="PropertyHolder"/> option overrides (set with <c>node.Set(property, value)</c>, for
///     example a per-node <see cref="CoreOptions.Algorithm"/> override on a container) are
///     <em>not</em> copied onto a split component's nodes: <see cref="PropertyHolder"/> exposes no
///     generic API to enumerate or copy an arbitrary set of overrides onto a different instance, and
///     adding one purely to support this rarely-hit multi-component path was judged out of scope. The
///     graph-level overrides that matter most in practice are unaffected: the original graph's own
///     cascaded <see cref="LayoutOptions"/> (direction, spacing, edge routing, and so on) are captured
///     once, before splitting, via <see cref="PropertyHolder.OverlayOnto"/> and passed as the fallback
///     options to every split component's leaf algorithm, so a graph-level override still applies to
///     every piece exactly as it would have applied to the whole. Only a node- or edge-level override,
///     specifically on a node that ends up copied into a split component, would be silently dropped —
///     an edge case a caller can avoid today by preferring graph-level (or per-scope
///     <see cref="LayoutGraphNode.Children"/>-level) overrides over per-node ones when also relying on
///     <c>"auto"</c> routing.
///     </para>
/// </remarks>
/// <example>
///     <code>
///     // A connected cluster plus two unrelated singleton boxes: the cluster routes to "layered", and
///     // the two singletons are packed together via "containment".
///     var graph = new LayoutGraph();
///     var a = graph.AddNode("a", 80, 40);
///     var b = graph.AddNode("b", 80, 40);
///     graph.AddEdge("a-b", a, b);
///     graph.AddNode("solo1", 80, 40);
///     graph.AddNode("solo2", 80, 40);
///
///     var tree = new AutoLayoutAlgorithm().Apply(graph);
///
///     // Hand the composed tree to a renderer (for example the SVG renderer).
///     // new SvgRenderer().Render(tree, new RenderOptions(Themes.Light), stream);
///     </code>
/// </example>
public sealed class AutoLayoutAlgorithm : LayoutAlgorithmBase
{
    /// <summary>
    /// The stable algorithm identifier <c>"auto"</c> under which this algorithm is selected and
    /// registered. Pass it to <see cref="LayoutOptions.ForAlgorithm(string)"/> or
    /// <see cref="CoreOptions.Algorithm"/> instead of hardcoding the literal string.
    /// </summary>
    public const string AlgorithmId = "auto";

    /// <summary>
    /// Gap, in logical pixels, kept between packed components by <see cref="LayoutTreePacker"/>. Matches
    /// <see cref="ContainmentLayoutAlgorithm"/>'s own inter-box spacing so a graph mixing routed
    /// components and the singleton bucket reads as one consistently-spaced canvas.
    /// </summary>
    private const double ComponentSpacing = 24.0;

    /// <summary>
    /// Target width-to-height multiplier <see cref="LayoutTreePacker"/> aims for when arranging
    /// components onto shelves. Matches <see cref="ContainmentLayoutAlgorithm"/>'s own landscape bias.
    /// </summary>
    private const double ComponentAspectRatio = 4.0 / 3.0;

    private readonly HierarchicalLayoutAlgorithm _hierarchical = new();
    private readonly LayeredLayoutAlgorithm _layered = new();
    private readonly ContainmentLayoutAlgorithm _containment = new();

    /// <inheritdoc/>
    public override string Id => AlgorithmId;

    /// <inheritdoc/>
    protected internal override LayoutTree ApplyCore(LayoutGraph graph, LayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(options);

        var nodes = graph.Nodes;
        var count = nodes.Count;
        if (count == 0)
        {
            // No top-level nodes: nothing to route or split. Hierarchical is a pure pass-through to its
            // default leaf (layered) when no container is present, so this is equivalent to routing an
            // empty graph through any bundled algorithm.
            return _hierarchical.ApplyCore(graph, options);
        }

        var indexOf = new Dictionary<LayoutGraphNode, int>(count);
        var portOwnerIndex = new Dictionary<LayoutGraphPort, int>();
        for (var i = 0; i < count; i++)
        {
            var node = nodes[i];
            indexOf[node] = i;
            if (!node.HasPorts)
            {
                continue;
            }

            foreach (var port in node.Ports.Ports)
            {
                portOwnerIndex[port] = i;
            }
        }

        // Union-find over top-level nodes, connected by every top-level edge whose endpoints both
        // resolve to a top-level node or one of its own ports (an edge referencing a node outside this
        // scope, or a deeply-nested descendant referenced directly rather than through its top-level
        // container, is not considered for connectivity — mirroring the "drop unresolvable endpoints"
        // convention every bundled leaf algorithm already follows).
        var parent = new int[count];
        for (var i = 0; i < count; i++)
        {
            parent[i] = i;
        }

        var selfLoop = new bool[count];
        foreach (var edge in graph.Edges)
        {
            if (!TryResolveOwner(edge.Source, indexOf, portOwnerIndex, out var s) ||
                !TryResolveOwner(edge.Target, indexOf, portOwnerIndex, out var t))
            {
                continue;
            }

            if (s == t)
            {
                selfLoop[s] = true;
            }
            else
            {
                Union(parent, s, t);
            }
        }

        // Group node indices by their component root, preserving first-appearance order so the packed
        // output is deterministic.
        var componentOrder = new List<int>();
        var componentMembers = new Dictionary<int, List<int>>();
        for (var i = 0; i < count; i++)
        {
            var root = Find(parent, i);
            if (!componentMembers.TryGetValue(root, out var members))
            {
                members = [];
                componentMembers[root] = members;
                componentOrder.Add(root);
            }

            members.Add(i);
        }

        // Classify each component: any node with children routes the whole component to hierarchical;
        // otherwise two-or-more members (or a lone node with a self-loop) route to layered; a lone,
        // childless, edgeless node is deferred into the shared singleton bucket.
        var routedGroups = new List<(List<int> Members, LayoutAlgorithmBase Algorithm)>();
        var singletons = new List<int>();
        foreach (var root in componentOrder)
        {
            var members = componentMembers[root];
            var anyChildren = false;
            foreach (var member in members)
            {
                if (nodes[member].HasChildren)
                {
                    anyChildren = true;
                    break;
                }
            }

            if (anyChildren)
            {
                routedGroups.Add((members, _hierarchical));
            }
            else if (members.Count > 1 || selfLoop[members[0]])
            {
                routedGroups.Add((members, _layered));
            }
            else
            {
                singletons.Add(members[0]);
            }
        }

        // Fast path: exactly one group overall means nothing needs to be split — delegate straight to
        // that group's algorithm on the original, unmodified graph.
        if (routedGroups.Count == 1 && singletons.Count == 0)
        {
            return routedGroups[0].Algorithm.ApplyCore(graph, options);
        }

        if (routedGroups.Count == 0 && singletons.Count == count)
        {
            return _containment.ApplyCore(graph, options);
        }

        // Genuine multi-group case: capture the graph's own cascaded options once (so a graph-level
        // override still applies to every split-off piece), split each group into its own freshly-built
        // sub-graph, lay each out independently, and pack the results into one combined tree.
        //
        // The captured options must not keep carrying this graph's own CoreOptions.Algorithm value
        // (typically "auto" itself, since that is how a caller selected this algorithm in the first
        // place): each split-off group's leaf/hierarchical algorithm was already chosen by the routing
        // rule above, but HierarchicalLayoutAlgorithm re-reads CoreOptions.Algorithm from its own
        // effective options to resolve ITS OWN top scope's leaf algorithm, and "auto" is never a
        // registered leaf identifier there. Resetting it to the layered default (the same default
        // HierarchicalLayoutAlgorithm itself falls back to when nothing declares an override) restores
        // the cascade to exactly what an ordinary caller not using "auto" would see.
        var effective = graph.OverlayOnto(options);
        effective.Set(CoreOptions.Algorithm, LayeredLayoutAlgorithm.AlgorithmId);

        var trees = new List<LayoutTree>(routedGroups.Count + (singletons.Count > 0 ? 1 : 0));
        foreach (var (members, algorithm) in routedGroups)
        {
            var subGraph = BuildComponentGraph(graph, members);
            trees.Add(algorithm.ApplyCore(subGraph, effective));
        }

        if (singletons.Count > 0)
        {
            var subGraph = BuildComponentGraph(graph, singletons);
            trees.Add(_containment.ApplyCore(subGraph, effective));
        }

        return LayoutTreePacker.Pack(trees, ComponentSpacing, ComponentAspectRatio);
    }

    /// <summary>
    /// Resolves a connectable edge endpoint to the top-level node index it belongs to: itself for a
    /// top-level node, or its owning node's index for one of that node's own ports.
    /// </summary>
    private static bool TryResolveOwner(
        ILayoutConnectable connectable,
        Dictionary<LayoutGraphNode, int> indexOf,
        Dictionary<LayoutGraphPort, int> portOwnerIndex,
        out int index)
    {
        switch (connectable)
        {
            case LayoutGraphNode node when indexOf.TryGetValue(node, out index):
                return true;

            case LayoutGraphPort port when portOwnerIndex.TryGetValue(port, out index):
                return true;

            default:
                index = 0;
                return false;
        }
    }

    /// <summary>Finds the representative root of <paramref name="x"/>'s set, with path halving.</summary>
    private static int Find(int[] parent, int x)
    {
        while (parent[x] != x)
        {
            parent[x] = parent[parent[x]];
            x = parent[x];
        }

        return x;
    }

    /// <summary>Merges the sets containing <paramref name="a"/> and <paramref name="b"/>.</summary>
    private static void Union(int[] parent, int a, int b)
    {
        var rootA = Find(parent, a);
        var rootB = Find(parent, b);
        if (rootA != rootB)
        {
            parent[rootA] = rootB;
        }
    }

    /// <summary>
    /// Builds a new, self-contained <see cref="LayoutGraph"/> holding a deep copy of every node listed
    /// in <paramref name="memberIndices"/> (recursively including nested children and ports), plus a
    /// copy of every edge from <paramref name="original"/> (at every nesting depth reachable from those
    /// members) whose endpoints both resolve inside the copy.
    /// </summary>
    /// <param name="original">The graph the component's nodes are copied out of.</param>
    /// <param name="memberIndices">Top-level node indices, into <paramref name="original"/>'s
    /// <see cref="LayoutGraph.Nodes"/>, belonging to this component.</param>
    /// <returns>The newly-built sub-graph for the component.</returns>
    private static LayoutGraph BuildComponentGraph(LayoutGraph original, List<int> memberIndices)
    {
        var target = new LayoutGraph();
        var nodeMap = new Dictionary<LayoutGraphNode, LayoutGraphNode>();
        var portMap = new Dictionary<LayoutGraphPort, LayoutGraphPort>();
        var pendingEdges = new List<(LayoutGraphEdge Edge, LayoutGraph Container)>();

        var originalNodes = original.Nodes;
        foreach (var index in memberIndices)
        {
            CopyNode(originalNodes[index], target, nodeMap, portMap, pendingEdges);
        }

        // Root-level edges belong to the copy's own root scope, mirroring the original's own scoping.
        foreach (var edge in original.Edges)
        {
            pendingEdges.Add((edge, target));
        }

        // Every node and port in the component (at every nesting depth) now has a copy, so cross-scope
        // edge endpoints resolve regardless of how deep either endpoint is nested.
        foreach (var (edge, container) in pendingEdges)
        {
            if (!TryMapConnectable(edge.Source, nodeMap, portMap, out var mappedSource) ||
                !TryMapConnectable(edge.Target, nodeMap, portMap, out var mappedTarget))
            {
                // Endpoint outside this component (or otherwise unresolvable) — dropped, mirroring the
                // "skip out-of-scope endpoints" convention every bundled leaf algorithm already follows.
                continue;
            }

            var copy = container.AddEdge(edge.Id, mappedSource, mappedTarget);
            copy.TargetEnd = edge.TargetEnd;
            copy.LineStyle = edge.LineStyle;
            copy.Label = edge.Label;
        }

        return target;
    }

    /// <summary>
    /// Deep-copies a single node (its own fields, its ports, and — recursively — its nested
    /// <see cref="LayoutGraphNode.Children"/> subgraph) into <paramref name="target"/>, recording the
    /// copy in <paramref name="nodeMap"/>/<paramref name="portMap"/> and queuing every nested-scope
    /// edge it owns into <paramref name="pendingEdges"/> for resolution once the whole component has
    /// been copied.
    /// </summary>
    private static void CopyNode(
        LayoutGraphNode source,
        LayoutGraph target,
        Dictionary<LayoutGraphNode, LayoutGraphNode> nodeMap,
        Dictionary<LayoutGraphPort, LayoutGraphPort> portMap,
        List<(LayoutGraphEdge Edge, LayoutGraph Container)> pendingEdges)
    {
        var copy = target.AddNode(source.Id, source.Width, source.Height);
        copy.Label = source.Label;
        copy.Shape = source.Shape;
        copy.Keyword = source.Keyword;
        copy.Compartments = source.Compartments;
        copy.TitleHeight = source.TitleHeight;
        copy.RoundedCornerRadius = source.RoundedCornerRadius;
        copy.FolderTabWidth = source.FolderTabWidth;
        copy.FolderTabHeight = source.FolderTabHeight;
        nodeMap[source] = copy;

        if (source.HasPorts)
        {
            foreach (var port in source.Ports.Ports)
            {
                var portCopy = copy.Ports.AddPort(port.Id);
                portCopy.ExternalLabel = port.ExternalLabel;
                portCopy.InternalLabel = port.InternalLabel;
                portMap[port] = portCopy;
            }
        }

        if (!source.HasChildren)
        {
            return;
        }

        foreach (var child in source.Children.Nodes)
        {
            CopyNode(child, copy.Children, nodeMap, portMap, pendingEdges);
        }

        foreach (var edge in source.Children.Edges)
        {
            pendingEdges.Add((edge, copy.Children));
        }
    }

    /// <summary>Resolves a copied edge endpoint through <paramref name="nodeMap"/>/<paramref name="portMap"/>.</summary>
    private static bool TryMapConnectable(
        ILayoutConnectable connectable,
        Dictionary<LayoutGraphNode, LayoutGraphNode> nodeMap,
        Dictionary<LayoutGraphPort, LayoutGraphPort> portMap,
        out ILayoutConnectable mapped)
    {
        switch (connectable)
        {
            case LayoutGraphNode node when nodeMap.TryGetValue(node, out var mappedNode):
                mapped = mappedNode;
                return true;

            case LayoutGraphPort port when portMap.TryGetValue(port, out var mappedPort):
                mapped = mappedPort;
                return true;

            default:
                mapped = null!;
                return false;
        }
    }
}
