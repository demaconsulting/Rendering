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
/// The first parent boundary port whose internal (delegation) edges cross into this scope, or
/// <see langword="null"/> for the outermost (root) level. Convenience accessor for the common
/// single-port case; <see cref="IncomingBoundaries"/> carries every incoming boundary when the owning
/// container exposes more than one boundary port.
/// </param>
/// <param name="IncomingBoundaries">
/// Every parent boundary port whose internal (delegation) edges cross into this scope — one per
/// boundary port owned by the container this level was descended into — or an empty list for the
/// outermost (root) level. A multi-port container contributes one <see cref="MergeRegionLevel"/> whose
/// incoming boundaries are all of that container's ports, so each port becomes its own inward
/// hierarchy crossing.
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
    IReadOnlyList<BoundaryPort> IncomingBoundaries,
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
/// Describes one hierarchy-crossing dummy node inside a level's assembled <see cref="LayeredGraph"/>:
/// which augmented-node index stands in for the crossing, the boundary port it originates from, and
/// which logical face of the container boundary it represents.
/// </summary>
/// <remarks>
///     The recursive pipeline uses this record for two purposes: to tag the corresponding
///     <see cref="AugNode.Crossing"/> after long-edge splitting (so the crossing dummy is a genuinely
///     honored hierarchy crossing rather than an ordinary node), and — by pairing an
///     <see cref="HierarchyCrossingFace.External"/> crossing in the parent level with the
///     <see cref="HierarchyCrossingFace.Internal"/> crossing in the child level that shares the same
///     <see cref="BoundaryPort.Port"/> — to propagate a resolved boundary order between adjacent
///     nesting levels.
/// </remarks>
/// <param name="NodeIndex">The augmented-node index of the crossing dummy within its level graph.</param>
/// <param name="Boundary">The boundary port this crossing dummy stands in for.</param>
/// <param name="Face">Which logical face (external/internal) of the boundary crossing this dummy is.</param>
internal readonly record struct LevelCrossing(
    int NodeIndex,
    BoundaryPort Boundary,
    HierarchyCrossingFace Face);

/// <summary>
/// The semantic role of one <see cref="LayerEdge"/> built into a level's assembled graph, so the
/// decomposition step can project each routed polyline back into the correct kind of per-scope
/// connector without re-deriving the crossing wiring from raw node indices.
/// </summary>
internal enum LevelEdgeKind
{
    /// <summary>An ordinary interior edge between two direct-member nodes of the level.</summary>
    Ordinary,

    /// <summary>An external approach edge feeding a boundary port's external-face crossing dummy.</summary>
    ExternalApproach,

    /// <summary>
    /// The synthetic dummy-to-container edge leading a boundary port's external-face crossing into its
    /// container box; carries no visible connector but its routed polyline lands on the container face,
    /// establishing the boundary port's single shared anchor.
    /// </summary>
    ContainerLink,

    /// <summary>An internal delegation edge from a boundary port's internal-face crossing into an interior target.</summary>
    InternalDelegation,
}

/// <summary>
/// The semantic role of one built <see cref="LayerEdge"/>, index-aligned with a level graph's input
/// <see cref="LayeredGraph.Edges"/> list, so the decomposition step can map each routed polyline
/// (recovered through <see cref="LayeredGraph.AcyclicOriginalIndex"/>) to the connector it should
/// produce and style it from its originating <see cref="LayoutGraphEdge"/>.
/// </summary>
/// <param name="Kind">Which kind of connector this edge represents.</param>
/// <param name="Edge">
/// The originating input <see cref="LayoutGraphEdge"/> the connector is styled from, or
/// <see langword="null"/> for a synthetic <see cref="LevelEdgeKind.ContainerLink"/>.
/// </param>
/// <param name="Boundary">
/// The boundary port this edge relates to for a crossing edge (external approach, container link, or
/// internal delegation), or <see langword="null"/> for an <see cref="LevelEdgeKind.Ordinary"/> edge.
/// </param>
internal readonly record struct LevelEdgeRole(
    LevelEdgeKind Kind,
    LayoutGraphEdge? Edge,
    BoundaryPort? Boundary);

/// <summary>
/// One nesting level's assembled <see cref="LayeredGraph"/> paired with the hierarchy-crossing dummies
/// seeded into it, so the recursive layered pipeline can both tag those dummies and recover each
/// crossing's placed coordinate and originating <c>(BoundaryPort, Face)</c> after placement.
/// </summary>
/// <param name="Graph">The per-level layered graph built by <see cref="MergeRegionGraphAssembler.BuildLevelGraph"/>.</param>
/// <param name="Crossings">The hierarchy-crossing dummies seeded into <paramref name="Graph"/>, in creation order.</param>
/// <param name="EdgeRoles">
/// The semantic role of each built edge, index-aligned with <see cref="LayeredGraph.Edges"/> of
/// <paramref name="Graph"/>, so the decomposition step can project routed polylines back into the
/// correct per-scope connectors.
/// </param>
internal sealed record LevelLayeredGraph(
    LayeredGraph Graph,
    IReadOnlyList<LevelCrossing> Crossings,
    IReadOnlyList<LevelEdgeRole> EdgeRoles);

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
        var root = BuildLevel(scope, boundaryPorts, incomingBoundaries: [], effectiveSize, allBoundaryPorts);
        return new AssembledMergeRegion(root, allBoundaryPorts);
    }

    /// <summary>
    /// Builds one <see cref="LevelLayeredGraph"/> per nesting level of <paramref name="region"/>, keyed
    /// by <see cref="MergeRegionLevel"/>, so the recursive pipeline can lay out every level and later
    /// recover each level's placed coordinates and hierarchy crossings.
    /// </summary>
    /// <param name="region">The assembled merge region whose every level is built.</param>
    /// <param name="direction">The flow direction each level's graph is laid out along.</param>
    /// <returns>A lookup from each level to its assembled layered graph and crossing bookkeeping.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="region"/> is <see langword="null"/>.</exception>
    public static IReadOnlyDictionary<MergeRegionLevel, LevelLayeredGraph> BuildAllLevelGraphs(
        AssembledMergeRegion region,
        LayoutDirection direction)
    {
        ArgumentNullException.ThrowIfNull(region);

        var result = new Dictionary<MergeRegionLevel, LevelLayeredGraph>();
        BuildAllLevelGraphs(region.Root, direction, result);
        return result;
    }

    /// <summary>
    /// Recursively populates <paramref name="sink"/> with the assembled layered graph of
    /// <paramref name="level"/> and every descendant level.
    /// </summary>
    /// <param name="level">The level to build a graph for.</param>
    /// <param name="direction">The flow direction each level's graph is laid out along.</param>
    /// <param name="sink">The accumulating level-to-graph lookup.</param>
    private static void BuildAllLevelGraphs(
        MergeRegionLevel level,
        LayoutDirection direction,
        Dictionary<MergeRegionLevel, LevelLayeredGraph> sink)
    {
        sink[level] = BuildLevelGraph(level, direction);
        foreach (var (_, _, child) in level.Children)
        {
            BuildAllLevelGraphs(child, direction, sink);
        }
    }

    /// <summary>
    /// Builds the per-level <see cref="LayeredGraph"/> for one <see cref="MergeRegionLevel"/>: real
    /// interior nodes translated to <see cref="LayerNode"/>s, ordinary interior edges to
    /// <see cref="LayerEdge"/>s, and every boundary-crossing edge replaced by a zero-size
    /// hierarchy-crossing dummy node with its own incident <see cref="LayerEdge"/>s.
    /// </summary>
    /// <remarks>
    ///     Package-private so the recursive <c>CrossingMinimizer</c>/<c>LayerAssigner</c> entry points
    ///     (later stages) can rebuild each nested level's graph on demand. Each boundary-crossing edge
    ///     is represented by a zero-size dummy node that the pipeline lays out like any other node. The
    ///     <see cref="HierarchyCrossing"/> descriptor itself lives on <see cref="AugNode"/> and is
    ///     populated by the pipeline's augmentation stages, so it is not carried on the input
    ///     <see cref="LayerNode"/> here.
    /// </remarks>
    /// <param name="level">The nesting level to build a layered graph for.</param>
    /// <param name="direction">The flow direction the level is laid out along; defaults to <see cref="LayoutDirection.Right"/>.</param>
    /// <returns>The per-level layered graph and its hierarchy-crossing bookkeeping, ready to feed the recursive pipeline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="level"/> is <see langword="null"/>.</exception>
    internal static LevelLayeredGraph BuildLevelGraph(MergeRegionLevel level, LayoutDirection direction = LayoutDirection.Right)
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
        var edgeRoles = new List<LevelEdgeRole>();

        // Ordinary interior edges connect two direct-member nodes of this level.
        foreach (var edge in level.Edges)
        {
            var source = ResolveMemberIndex(level, edge.Source);
            var target = ResolveMemberIndex(level, edge.Target);
            if (source >= 0 && target >= 0)
            {
                edges.Add(new LayerEdge(source, target));
                edgeRoles.Add(new LevelEdgeRole(LevelEdgeKind.Ordinary, edge, Boundary: null));
            }
        }

        var crossings = new List<LevelCrossing>();

        // The incoming crossings: each parent boundary port delegating inward becomes one zero-size
        // dummy (its internal face) feeding the interior delegation targets it reaches.
        foreach (var incoming in level.IncomingBoundaries)
        {
            AddIncomingCrossing(level, incoming, nodes, edges, edgeRoles, crossings);
        }

        // The outgoing crossings: each boundary-port container's external approach edges feed a zero-size
        // dummy (its external face) that then leads into the container's own box.
        foreach (var boundary in level.BoundaryPorts)
        {
            AddOutgoingCrossing(level, boundary, nodes, edges, edgeRoles, crossings);
        }

        return new LevelLayeredGraph(new LayeredGraph(nodes, edges, direction), crossings, edgeRoles);
    }

    /// <summary>
    /// Recursively builds one <see cref="MergeRegionLevel"/> for <paramref name="scope"/> and every
    /// boundary-port container nested within it, appending each level's boundary ports to
    /// <paramref name="allBoundaryPorts"/> in top-down order.
    /// </summary>
    /// <param name="scope">The container scope this level is drawn from.</param>
    /// <param name="boundaryPorts">The boundary ports discovered directly in <paramref name="scope"/>.</param>
    /// <param name="incomingBoundaries">The parent boundary ports delegating into this scope, or empty at the root.</param>
    /// <param name="effectiveSize">The effective size lookup threaded to every level.</param>
    /// <param name="allBoundaryPorts">The accumulating flattened boundary-port list.</param>
    /// <returns>The assembled level for <paramref name="scope"/>.</returns>
    private static MergeRegionLevel BuildLevel(
        LayoutGraph scope,
        IReadOnlyList<BoundaryPort> boundaryPorts,
        IReadOnlyList<BoundaryPort> incomingBoundaries,
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

        // Boundary-crossing edges reference either a boundary port discovered here or a parent boundary
        // port delegating in; they are represented as hierarchy crossings, so they are removed from the
        // level's ordinary edge set.
        var boundaryPortSet = new HashSet<LayoutGraphPort>();
        foreach (var boundary in boundaryPorts)
        {
            boundaryPortSet.Add(boundary.Port);
        }

        foreach (var incoming in incomingBoundaries)
        {
            boundaryPortSet.Add(incoming.Port);
        }

        var edges = scope.Edges
            .Where(edge => !ReferencesBoundaryPort(edge, boundaryPortSet))
            .ToList();

        // This level's own boundary ports precede its descendants' in the flattened lookup.
        allBoundaryPorts.AddRange(boundaryPorts);

        // Group boundary ports by their owning container so a multi-port container contributes exactly
        // one nested child level whose incoming boundaries are all of that container's ports, rather than
        // one duplicated child level per port.
        var containerOrder = new List<LayoutGraphNode>();
        var portsByContainer = new Dictionary<LayoutGraphNode, List<BoundaryPort>>();
        foreach (var boundary in boundaryPorts)
        {
            if (!portsByContainer.TryGetValue(boundary.Container, out var ports))
            {
                ports = [];
                portsByContainer[boundary.Container] = ports;
                containerOrder.Add(boundary.Container);
            }

            ports.Add(boundary);
        }

        // Recurse into every boundary-port container's full child scope, rediscovering that scope's own
        // boundary ports so a delegation chain of any depth is assembled level by level.
        var children = new List<(LayoutGraphNode Container, int NodeIndex, MergeRegionLevel Child)>();
        foreach (var container in containerOrder)
        {
            var containerPorts = portsByContainer[container];
            var childScope = container.Children;
            var childBoundaries = HierarchyMergeRegionBuilder.Collect(childScope);
            var childLevel = BuildLevel(childScope, childBoundaries, containerPorts, effectiveSize, allBoundaryPorts);
            children.Add((container, nodeIndex[container], childLevel));
        }

        return new MergeRegionLevel(
            scope,
            nodes,
            edges,
            nodeIndex,
            children,
            boundaryPorts,
            incomingBoundaries.Count > 0 ? incomingBoundaries[0] : null,
            incomingBoundaries,
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
    /// <param name="edgeRoles">The working edge-role list, appended to in place index-aligned with <paramref name="edges"/>.</param>
    /// <param name="crossings">The working crossing-metadata list, appended to in place.</param>
    private static void AddIncomingCrossing(
        MergeRegionLevel level,
        BoundaryPort incoming,
        List<LayerNode> nodes,
        List<LayerEdge> edges,
        List<LevelEdgeRole> edgeRoles,
        List<LevelCrossing> crossings)
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
                crossings.Add(new LevelCrossing(dummyIndex, incoming, HierarchyCrossingFace.Internal));
                wired = true;
            }

            edges.Add(new LayerEdge(dummyIndex, target));
            edgeRoles.Add(new LevelEdgeRole(LevelEdgeKind.InternalDelegation, edge, incoming));
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
    /// <param name="edgeRoles">The working edge-role list, appended to in place index-aligned with <paramref name="edges"/>.</param>
    /// <param name="crossings">The working crossing-metadata list, appended to in place.</param>
    private static void AddOutgoingCrossing(
        MergeRegionLevel level,
        BoundaryPort boundary,
        List<LayerNode> nodes,
        List<LayerEdge> edges,
        List<LevelEdgeRole> edgeRoles,
        List<LevelCrossing> crossings)
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
                edgeRoles.Add(new LevelEdgeRole(LevelEdgeKind.ContainerLink, Edge: null, boundary));
                crossings.Add(new LevelCrossing(dummyIndex, boundary, HierarchyCrossingFace.External));
                wired = true;
            }

            edges.Add(new LayerEdge(source, dummyIndex));
            edgeRoles.Add(new LevelEdgeRole(LevelEdgeKind.ExternalApproach, edge, boundary));
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
