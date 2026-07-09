// <copyright file="MergeRegionGraphAssembler.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Layout.Engine.Layered;

/// <summary>
/// One nesting level's contribution to an assembled merge region: the direct-member nodes and ordinary
/// (non-boundary-crossing) edges of a single container scope, plus the nested child levels contributed
/// by every boundary-port container discovered directly in that scope.
/// </summary>
/// <remarks>
///     <para>
///     A level mirrors the full leaf-level flattening decision: <see cref="Nodes"/> is the scope's
///     <em>entire</em> direct-member node set — boundary-port containers and ordinary interior nodes
///     alike — never a filtered "boundary skeleton" subset, so an ordinary interior node always
///     participates in the combined pass. <see cref="Edges"/> is the scope's own edges with the
///     boundary-crossing edges removed; a boundary-crossing edge is one that references either a
///     boundary port discovered in this scope or the parent boundary port delegating into it, and is
///     represented instead as a <see cref="HierarchyCrossing"/> dummy by <see cref="MergeRegionGraphAssembler.BuildLevelGraph"/>.
///     </para>
///     <para>
///     Each level keeps its own local <see cref="NodeIndex"/> because a container's interior is
///     spatially nested inside the container's own box, not sequential with the outer scope's layer
///     progression. <see cref="Children"/> is depth-unbounded: it holds one entry per boundary-port
///     container discovered in this scope, each carrying its own recursively-assembled
///     <see cref="MergeRegionLevel"/>, to whatever depth the discovery layer reports.
///     </para>
/// </remarks>
/// <param name="Scope">The container scope this level's nodes and edges were drawn from.</param>
/// <param name="Nodes">
/// This level's direct-member nodes, in graph (insertion) order; the complete interior node set, not a
/// boundary-port subset.
/// </param>
/// <param name="Edges">
/// This level's own edges excluding boundary-crossing edges (which become <see cref="HierarchyCrossing"/>
/// dummy edges), in graph order.
/// </param>
/// <param name="NodeIndex">Maps each node in <see cref="Nodes"/> to its index into that list, local to this level.</param>
/// <param name="Children">
/// The nested levels, one per boundary-port container discovered directly in this scope; index-aligned
/// with <see cref="BoundaryPorts"/>.
/// </param>
/// <param name="BoundaryPorts">
/// The boundary ports discovered directly in this scope, in discovery order; index-aligned with
/// <see cref="Children"/> (<c>BoundaryPorts[i].Container</c> is <c>Children[i].Container</c>).
/// </param>
/// <param name="IncomingBoundary">
/// The parent boundary port whose internal (delegation) edges cross into this scope, or
/// <see langword="null"/> for the outermost (root) level.
/// </param>
/// <param name="EffectiveSize">
/// The effective bounding-box size lookup used when translating this level's nodes into
/// <see cref="LayerNode"/>s; nodes absent from the lookup fall back to their own
/// <see cref="LayoutGraphNode.Width"/>/<see cref="LayoutGraphNode.Height"/>.
/// </param>
internal sealed record MergeRegionLevel(
    LayoutGraph Scope,
    IReadOnlyList<LayoutGraphNode> Nodes,
    IReadOnlyList<LayoutGraphEdge> Edges,
    IReadOnlyDictionary<LayoutGraphNode, int> NodeIndex,
    IReadOnlyList<(LayoutGraphNode Container, int NodeIndex, MergeRegionLevel Child)> Children,
    IReadOnlyList<BoundaryPort> BoundaryPorts,
    BoundaryPort? IncomingBoundary,
    IReadOnlyDictionary<LayoutGraphNode, (double Width, double Height)> EffectiveSize);

/// <summary>
/// The fully assembled combined graph spanning every nesting level of one merge region: the outermost
/// <see cref="Root"/> level and the flattened union of every boundary port discovered at any depth.
/// </summary>
/// <param name="Root">The outermost nesting level; its <see cref="MergeRegionLevel.Children"/> reach every deeper level.</param>
/// <param name="AllBoundaryPorts">
/// Every boundary port discovered at any nesting level of the region, in top-down discovery order, for
/// the decomposition step's lookup.
/// </param>
internal sealed record AssembledMergeRegion(
    MergeRegionLevel Root,
    IReadOnlyList<BoundaryPort> AllBoundaryPorts);

/// <summary>
/// Assembles the combined, hierarchy-aware node/edge model for one merge region from the boundary ports
/// <see cref="HierarchyMergeRegionBuilder.Collect"/> discovered in a scope, so the recursive layered
/// pipeline can lay every nesting level out in one combined pass rather than by post-hoc reconciliation.
/// </summary>
/// <remarks>
///     <para>
///     The assembler realizes the two settled architecture decisions. <em>Full leaf-level flattening</em>:
///     a boundary-port container's entire interior — every node and edge of its child scope, boundary-
///     related or not — is admitted as a nested <see cref="MergeRegionLevel"/>, so an ordinary interior
///     node participates in the same combined pass as the outer scope. <em>Arbitrary chain depth</em>:
///     <see cref="Assemble"/> recurses into every boundary-port container's child scope, rediscovering
///     that scope's own boundary ports with <see cref="HierarchyMergeRegionBuilder.Collect"/>, to
///     whatever depth the discovery layer reports, never capping the delegation chain.
///     </para>
///     <para>
///     Discovery is delegated verbatim to <see cref="HierarchyMergeRegionBuilder"/>; this type only
///     <em>assembles</em> the combined graph from that discovery, keeping each nesting level's local
///     node indexing distinct because a container's interior is nested inside its own box.
///     </para>
/// </remarks>
internal static class MergeRegionGraphAssembler
{
    /// <summary>
    /// Assembles the combined, hierarchy-aware node/edge model for the merge region rooted at
    /// <paramref name="scope"/>, given the boundary ports discovered in that scope.
    /// </summary>
    /// <remarks>
    ///     Recurses into every boundary-port container's full child scope (full leaf-level flattening),
    ///     to arbitrary depth, building one <see cref="MergeRegionLevel"/> per nesting level rather than
    ///     a single flat node list. The deeper levels' boundary ports are rediscovered with
    ///     <see cref="HierarchyMergeRegionBuilder.Collect"/> as the recursion descends.
    /// </remarks>
    /// <param name="scope">The outermost container scope of the merge region.</param>
    /// <param name="boundaryPorts">The boundary ports already discovered directly in <paramref name="scope"/>.</param>
    /// <param name="effectiveSize">
    /// The effective bounding-box size lookup for the region's nodes; a node absent from the lookup
    /// falls back to its own <see cref="LayoutGraphNode.Width"/>/<see cref="LayoutGraphNode.Height"/>.
    /// </param>
    /// <returns>The assembled merge region spanning every nesting level.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="scope"/>, <paramref name="boundaryPorts"/>, or
    /// <paramref name="effectiveSize"/> is <see langword="null"/>.
    /// </exception>
    public static AssembledMergeRegion Assemble(
        LayoutGraph scope,
        IReadOnlyList<BoundaryPort> boundaryPorts,
        IReadOnlyDictionary<LayoutGraphNode, (double Width, double Height)> effectiveSize)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(boundaryPorts);
        ArgumentNullException.ThrowIfNull(effectiveSize);

        // Accumulate every boundary port discovered at any depth for the decomposition step's lookup,
        // in top-down discovery order (this level's ports precede its descendants').
        var allBoundaryPorts = new List<BoundaryPort>();
        var root = BuildLevel(scope, boundaryPorts, incomingBoundary: null, effectiveSize, allBoundaryPorts);
        return new AssembledMergeRegion(root, allBoundaryPorts);
    }

    /// <summary>
    /// Builds the per-level <see cref="LayeredGraph"/> for one <see cref="MergeRegionLevel"/>: real
    /// interior nodes translated to <see cref="LayerNode"/>s, ordinary interior edges to
    /// <see cref="LayerEdge"/>s, and every boundary-crossing edge replaced by a zero-size
    /// hierarchy-crossing dummy node with its own incident <see cref="LayerEdge"/>s.
    /// </summary>
    /// <remarks>
    ///     Package-private so the recursive <c>CrossingMinimizer</c>/<c>LayerAssigner</c> entry points
    ///     (later stages) can rebuild each nested level's graph on demand. The crossing dummy wiring is
    ///     the input-graph precedent set by <see cref="BoundaryPortResolver.OrderCrossings"/>: a boundary
    ///     port becomes a zero-size dummy that the pipeline lays out like any other node. The
    ///     <see cref="HierarchyCrossing"/> descriptor itself lives on <see cref="AugNode"/> and is
    ///     populated by the pipeline's augmentation stages, so it is not carried on the input
    ///     <see cref="LayerNode"/> here.
    /// </remarks>
    /// <param name="level">The nesting level to build a layered graph for.</param>
    /// <param name="direction">The flow direction the level is laid out along; defaults to <see cref="LayoutDirection.Right"/>.</param>
    /// <returns>The per-level layered graph, ready to feed the recursive pipeline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="level"/> is <see langword="null"/>.</exception>
    internal static LayeredGraph BuildLevelGraph(MergeRegionLevel level, LayoutDirection direction = LayoutDirection.Right)
    {
        ArgumentNullException.ThrowIfNull(level);

        // Real interior nodes occupy indices 0..N-1, preserving this level's local node order.
        var nodes = new List<LayerNode>(level.Nodes.Count);
        foreach (var node in level.Nodes)
        {
            var (width, height) = ResolveSize(level, node);
            nodes.Add(new LayerNode(
                width,
                height,
                node.Shape,
                node.RoundedCornerRadius,
                node.FolderTabWidth,
                node.FolderTabHeight,
                node.Label,
                RealWidth: width,
                RealHeight: height));
        }

        var edges = new List<LayerEdge>();

        // Ordinary interior edges connect two direct-member nodes of this level.
        foreach (var edge in level.Edges)
        {
            var source = ResolveMemberIndex(level, edge.Source);
            var target = ResolveMemberIndex(level, edge.Target);
            if (source >= 0 && target >= 0)
            {
                edges.Add(new LayerEdge(source, target));
            }
        }

        // The incoming crossing: the parent boundary port delegates inward, so a single zero-size dummy
        // (its internal face) feeds each interior delegation target.
        if (level.IncomingBoundary is { } incoming)
        {
            AddIncomingCrossing(level, incoming, nodes, edges);
        }

        // The outgoing crossings: each boundary-port container's external approach edges feed a zero-size
        // dummy (its external face) that then leads into the container's own box.
        foreach (var boundary in level.BoundaryPorts)
        {
            AddOutgoingCrossing(level, boundary, nodes, edges);
        }

        return new LayeredGraph(nodes, edges, direction);
    }

    /// <summary>
    /// Recursively builds one <see cref="MergeRegionLevel"/> for <paramref name="scope"/> and every
    /// boundary-port container nested within it, appending each level's boundary ports to
    /// <paramref name="allBoundaryPorts"/> in top-down order.
    /// </summary>
    /// <param name="scope">The container scope this level is drawn from.</param>
    /// <param name="boundaryPorts">The boundary ports discovered directly in <paramref name="scope"/>.</param>
    /// <param name="incomingBoundary">The parent boundary port delegating into this scope, or <see langword="null"/> at the root.</param>
    /// <param name="effectiveSize">The effective size lookup threaded to every level.</param>
    /// <param name="allBoundaryPorts">The accumulating flattened boundary-port list.</param>
    /// <returns>The assembled level for <paramref name="scope"/>.</returns>
    private static MergeRegionLevel BuildLevel(
        LayoutGraph scope,
        IReadOnlyList<BoundaryPort> boundaryPorts,
        BoundaryPort? incomingBoundary,
        IReadOnlyDictionary<LayoutGraphNode, (double Width, double Height)> effectiveSize,
        List<BoundaryPort> allBoundaryPorts)
    {
        // Full leaf-level flattening: the level keeps the scope's entire direct-member node set.
        var nodes = scope.Nodes.ToList();
        var nodeIndex = new Dictionary<LayoutGraphNode, int>();
        for (var i = 0; i < nodes.Count; i++)
        {
            nodeIndex[nodes[i]] = i;
        }

        // Boundary-crossing edges reference either a boundary port discovered here or the parent
        // boundary port delegating in; they are represented as hierarchy crossings, so they are removed
        // from the level's ordinary edge set.
        var boundaryPortSet = new HashSet<LayoutGraphPort>();
        foreach (var boundary in boundaryPorts)
        {
            boundaryPortSet.Add(boundary.Port);
        }

        if (incomingBoundary is { } incoming)
        {
            boundaryPortSet.Add(incoming.Port);
        }

        var edges = scope.Edges
            .Where(edge => !ReferencesBoundaryPort(edge, boundaryPortSet))
            .ToList();

        // This level's own boundary ports precede its descendants' in the flattened lookup.
        allBoundaryPorts.AddRange(boundaryPorts);

        // Recurse into every boundary-port container's full child scope, rediscovering that scope's own
        // boundary ports so a delegation chain of any depth is assembled level by level.
        var children = new List<(LayoutGraphNode Container, int NodeIndex, MergeRegionLevel Child)>();
        foreach (var boundary in boundaryPorts)
        {
            var container = boundary.Container;
            var childScope = container.Children;
            var childBoundaries = HierarchyMergeRegionBuilder.Collect(childScope);
            var childLevel = BuildLevel(childScope, childBoundaries, boundary, effectiveSize, allBoundaryPorts);
            children.Add((container, nodeIndex[container], childLevel));
        }

        return new MergeRegionLevel(
            scope,
            nodes,
            edges,
            nodeIndex,
            children,
            boundaryPorts,
            incomingBoundary,
            effectiveSize);
    }

    /// <summary>
    /// Appends a single zero-size hierarchy-crossing dummy for <paramref name="incoming"/>'s internal
    /// face and wires it to each interior delegation target reachable in <paramref name="level"/>.
    /// </summary>
    /// <param name="level">The level whose interior receives the delegation.</param>
    /// <param name="incoming">The parent boundary port delegating into the level.</param>
    /// <param name="nodes">The working layer-node list, appended to in place.</param>
    /// <param name="edges">The working layer-edge list, appended to in place.</param>
    private static void AddIncomingCrossing(
        MergeRegionLevel level,
        BoundaryPort incoming,
        List<LayerNode> nodes,
        List<LayerEdge> edges)
    {
        var dummyIndex = nodes.Count;
        var wired = false;
        foreach (var edge in incoming.InternalEdges)
        {
            var targetEndpoint = OtherEndpoint(edge, incoming.Port);
            var target = ResolveMemberIndex(level, targetEndpoint);
            if (target < 0)
            {
                continue;
            }

            if (!wired)
            {
                nodes.Add(new LayerNode(0.0, 0.0, RealWidth: 0.0, RealHeight: 0.0));
                wired = true;
            }

            edges.Add(new LayerEdge(dummyIndex, target));
        }
    }

    /// <summary>
    /// Appends a single zero-size hierarchy-crossing dummy for <paramref name="boundary"/>'s external
    /// face, wires each in-scope external approach edge to it, and leads the dummy into the container box.
    /// </summary>
    /// <param name="level">The level owning the boundary-port container.</param>
    /// <param name="boundary">The boundary port whose external approach is being wired.</param>
    /// <param name="nodes">The working layer-node list, appended to in place.</param>
    /// <param name="edges">The working layer-edge list, appended to in place.</param>
    private static void AddOutgoingCrossing(
        MergeRegionLevel level,
        BoundaryPort boundary,
        List<LayerNode> nodes,
        List<LayerEdge> edges)
    {
        if (!level.NodeIndex.TryGetValue(boundary.Container, out var containerIndex))
        {
            return;
        }

        var dummyIndex = nodes.Count;
        var wired = false;
        foreach (var edge in boundary.ExternalEdges)
        {
            var sourceEndpoint = OtherEndpoint(edge, boundary.Port);
            var source = ResolveMemberIndex(level, sourceEndpoint);
            if (source < 0)
            {
                // The approach originates outside this scope (it is the parent's incoming crossing),
                // so it is represented at that outer level rather than duplicated here.
                continue;
            }

            if (!wired)
            {
                nodes.Add(new LayerNode(0.0, 0.0, RealWidth: 0.0, RealHeight: 0.0));
                edges.Add(new LayerEdge(dummyIndex, containerIndex));
                wired = true;
            }

            edges.Add(new LayerEdge(source, dummyIndex));
        }
    }

    /// <summary>
    /// Resolves the effective bounding-box size of <paramref name="node"/> for
    /// <paramref name="level"/>, falling back to the node's own dimensions when it is absent from the
    /// level's effective-size lookup.
    /// </summary>
    /// <param name="level">The level whose effective-size lookup is consulted.</param>
    /// <param name="node">The node whose size is required.</param>
    /// <returns>The effective width and height to lay the node out with.</returns>
    private static (double Width, double Height) ResolveSize(MergeRegionLevel level, LayoutGraphNode node)
    {
        return level.EffectiveSize.TryGetValue(node, out var size)
            ? size
            : (node.Width, node.Height);
    }

    /// <summary>
    /// Resolves <paramref name="endpoint"/> to the index of the direct-member node of
    /// <paramref name="level"/> it belongs to: the node itself when it is a member, or the member node
    /// that owns the endpoint port; otherwise <c>-1</c> when the endpoint lies outside this level.
    /// </summary>
    /// <param name="level">The level whose direct members are searched.</param>
    /// <param name="endpoint">The edge endpoint (a node or a port) to resolve.</param>
    /// <returns>The direct-member node index, or <c>-1</c> when the endpoint is not a member of this level.</returns>
    private static int ResolveMemberIndex(MergeRegionLevel level, ILayoutConnectable endpoint)
    {
        switch (endpoint)
        {
            case LayoutGraphNode node when level.NodeIndex.TryGetValue(node, out var nodeIndex):
                return nodeIndex;

            case LayoutGraphPort port:
                for (var i = 0; i < level.Nodes.Count; i++)
                {
                    var owner = level.Nodes[i];
                    if (owner.HasPorts && owner.Ports.Ports.Any(p => ReferenceEquals(p, port)))
                    {
                        return i;
                    }
                }

                return -1;

            default:
                return -1;
        }
    }

    /// <summary>
    /// Returns whether <paramref name="edge"/> references any port in
    /// <paramref name="boundaryPortSet"/> at either endpoint, marking it a boundary-crossing edge.
    /// </summary>
    /// <param name="edge">The edge to test.</param>
    /// <param name="boundaryPortSet">The set of boundary ports relevant to the current scope.</param>
    /// <returns><see langword="true"/> when either endpoint is a boundary port; otherwise <see langword="false"/>.</returns>
    private static bool ReferencesBoundaryPort(LayoutGraphEdge edge, HashSet<LayoutGraphPort> boundaryPortSet)
    {
        return (edge.Source is LayoutGraphPort source && boundaryPortSet.Contains(source))
            || (edge.Target is LayoutGraphPort target && boundaryPortSet.Contains(target));
    }

    /// <summary>
    /// Returns the endpoint of <paramref name="edge"/> that is not <paramref name="port"/>: the source
    /// when the port is the target, otherwise the target.
    /// </summary>
    /// <param name="edge">The boundary-crossing edge.</param>
    /// <param name="port">The boundary port whose opposite endpoint is required.</param>
    /// <returns>The edge endpoint opposite <paramref name="port"/>.</returns>
    private static ILayoutConnectable OtherEndpoint(LayoutGraphEdge edge, LayoutGraphPort port)
    {
        return ReferenceEquals(edge.Target, port) ? edge.Source : edge.Target;
    }
}
