// <copyright file="HierarchicalLayoutAlgorithm.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout;

/// <summary>
/// The bundled recursive hierarchical layout engine: it lays out a compound <see cref="LayoutGraph"/>
/// by placing each container node's children in their own coordinate space, sizing the container to
/// fit them, and composing the placed sub-layouts into a single absolute <see cref="LayoutTree"/>.
/// This is the ELK <c>RecursiveGraphLayoutEngine</c> analogue; it selects a bundled leaf algorithm
/// (for example <c>layered</c> or <c>containment</c>) per scope and delegates the actual box placement
/// to it.
/// </summary>
/// <remarks>
///     <para>
///     The engine walks the graph in <em>post-order</em>: each container's children are laid out first,
///     then the container is given an effective size that encloses that sub-layout (plus padding and an
///     optional title band), and finally the current level is placed by the selected leaf algorithm
///     using those effective sizes. The placed sub-layouts are translated from their local origin into
///     each container box's interior and attached as the box's <see cref="LayoutBox.Children"/>.
///     </para>
///     <para>
///     Every <see cref="CoreOptions"/> property — <see cref="CoreOptions.Algorithm"/>,
///     <see cref="CoreOptions.Direction"/>, <see cref="CoreOptions.EdgeRouting"/>,
///     <see cref="CoreOptions.HierarchyHandling"/>, <see cref="CoreOptions.NodeSpacing"/>, and
///     <see cref="CoreOptions.LayerSpacing"/> — cascades per scope through the same generalized
///     mechanism, built on <see cref="PropertyHolder.OverlayOnto"/>: each scope's own explicit
///     overrides are overlaid onto its parent scope's already-resolved effective options, so an
///     unset scope inherits its nearest ancestor's value and any scope may override it for itself and
///     its descendants. Two established, independently-tested conventions decide where a container's
///     own overrides live: <see cref="CoreOptions.Algorithm"/> is set on the container <em>node</em>
///     itself (for example <c>group.Set(CoreOptions.Algorithm, "layered")</c>), while every other
///     property is set on the container's <see cref="LayoutGraphNode.Children"/> graph (for example
///     <c>group.Children.Set(CoreOptions.Direction, LayoutFlowDirection.Down)</c>). This lets, for
///     example, a <c>containment</c>-packed root hold a <c>layered</c> container flowing in its own
///     direction without either level knowing about the other.
///     </para>
///     <para>
///     Hierarchy is handled in <see cref="HierarchyHandling.SeparateChildren"/> mode: each container is
///     laid out in isolation and sized to fit its children, so container placements are independent and
///     deterministic. Cross-container edges — edges whose endpoints live in different descendant
///     containers, added to their lowest common ancestor per the model's LCA convention — are routed at
///     the level that owns them, steering around the sibling containers between the two endpoints.
///     </para>
///     <para>
///     <strong>Flat-graph equivalence guarantee.</strong> When no direct node of a scope is a container
///     (a flat graph), the engine returns the selected leaf algorithm's output <em>unchanged</em>: it
///     delegates directly to the algorithm without cloning the graph or post-processing the tree, so the
///     result is byte-for-byte identical to invoking that algorithm itself. Hierarchical composition
///     only engages once a scope actually contains a container node.
///     </para>
///     <para>
///     The engine never mutates the caller's input graph. When a level must be re-sized to account for
///     nested content, it builds an internal <em>sized view</em> graph (same node order, container nodes
///     carrying their effective size, in-scope edges only) and lays that out, leaving the caller's graph
///     untouched. The engine is stateless and its <see cref="Apply"/> is safe to call concurrently on
///     distinct graphs.
///     </para>
///     <para>
///     <strong>Leaf-algorithm contract.</strong> This engine builds each scope's cascaded effective
///     <see cref="LayoutOptions"/> snapshot once and passes it to the selected leaf algorithm; it is
///     the leaf algorithm's own responsibility to resolve a property from that snapshot as the
///     ultimate source of a cascaded value (optionally preferring its own graph's override first, as
///     <see cref="LayeredLayoutAlgorithm"/> and <see cref="ContainmentLayoutAlgorithm"/> do). A leaf
///     algorithm that reads only its input graph and ignores the supplied options would silently break
///     cascading for that algorithm.
///     </para>
/// </remarks>
/// <example>
///     <code>
///     // A two-level graph: a labelled container laid out "layered", packed by a "containment" root.
///     var graph = new LayoutGraph();
///     var group = graph.AddNode("group", 10, 10);
///     group.Label = "Group";
///     group.Set(CoreOptions.Algorithm, "layered");        // this container lays its children out layered
///     var c1 = group.Children.AddNode("c1", 80, 40);
///     var c2 = group.Children.AddNode("c2", 80, 40);
///     group.Children.AddEdge("c1-c2", c1, c2);            // intra-container edge
///     graph.AddNode("outside", 80, 40);                   // a sibling leaf at the root
///
///     // Pack the root with the containment algorithm; containers recurse with their own algorithm.
///     var options = LayoutOptions.ForAlgorithm("containment");
///     var tree = new HierarchicalLayoutAlgorithm().Apply(graph, options);
///
///     // Hand the composed tree to a renderer (for example the SVG renderer).
///     // new SvgRenderer().Render(tree, new RenderOptions(Themes.Light), stream);
///     </code>
/// </example>
public sealed class HierarchicalLayoutAlgorithm : ILayoutAlgorithm
{
    /// <summary>
    /// The stable algorithm identifier <c>"hierarchical"</c> under which this algorithm is selected and
    /// registered. Pass it to <see cref="LayoutOptions.ForAlgorithm(string)"/> or
    /// <see cref="CoreOptions.Algorithm"/> instead of hardcoding the literal string.
    /// </summary>
    public const string AlgorithmId = "hierarchical";

    /// <summary>
    /// The leaf algorithm a scope is placed with when it resolves to this engine's own
    /// <see cref="AlgorithmId"/>. Because this engine is not a leaf and is never registered into the leaf
    /// registry, selecting <c>"hierarchical"</c> for a scope (including through the facade default) means
    /// "apply the hierarchical engine, placing this level with the default leaf algorithm" rather than a
    /// self-referential lookup that cannot resolve. That default leaf is <see cref="LayeredLayoutAlgorithm"/>.
    /// </summary>
    private const string DefaultLeafAlgorithmId = LayeredLayoutAlgorithm.AlgorithmId;

    /// <summary>
    /// Inset, in logical pixels, kept on every side between a container's border and the sub-layout of
    /// its children. Sizing each container to its children plus this padding keeps nested content from
    /// touching the container edge.
    /// </summary>
    private const double ContainerPadding = 12.0;

    /// <summary>
    /// Height, in logical pixels, of the title band reserved at the top of a container that carries a
    /// <see cref="LayoutGraphNode.Label"/>. The children are offset below this band so the label and the
    /// nested content never overlap; a container with no label reserves no band.
    /// </summary>
    private const double ContainerTitleHeight = 24.0;

    /// <summary>
    /// The leaf-algorithm provider used to resolve the per-scope layout algorithm by identifier. It is
    /// deliberately limited to leaf algorithms (for example <c>layered</c> and <c>containment</c>); this
    /// engine is not registered into it, so recursion always terminates in a bundled leaf algorithm.
    /// </summary>
    private readonly LayoutAlgorithmRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="HierarchicalLayoutAlgorithm"/> class backed by a
    /// default registry of the bundled leaf algorithms (<see cref="LayeredLayoutAlgorithm"/> and
    /// <see cref="ContainmentLayoutAlgorithm"/>).
    /// </summary>
    /// <remarks>
    ///     This convenience constructor lets callers use the engine without assembling a registry. It
    ///     registers only the leaf algorithms, never this engine itself, so a scope that selects an
    ///     unregistered algorithm surfaces a resolution error rather than recursing indefinitely.
    /// </remarks>
    public HierarchicalLayoutAlgorithm()
        : this(new LayoutAlgorithmRegistry()
            .Register(new LayeredLayoutAlgorithm())
            .Register(new ContainmentLayoutAlgorithm()))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HierarchicalLayoutAlgorithm"/> class that resolves
    /// per-scope leaf algorithms from the supplied <paramref name="registry"/>.
    /// </summary>
    /// <param name="registry">
    /// Provider of the leaf algorithms this engine delegates to. It should contain only leaf algorithms
    /// (not this hierarchical engine) so recursion terminates; every algorithm identifier referenced by
    /// the graph or options must be resolvable from it.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="registry"/> is <see langword="null"/>.</exception>
    public HierarchicalLayoutAlgorithm(LayoutAlgorithmRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc/>
    public string Id => AlgorithmId;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="graph"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when a scope selects a layout algorithm identifier that is not registered.
    /// </exception>
    public LayoutTree Apply(LayoutGraph graph, LayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(options);

        // The root scope's own explicit overrides (set directly on the graph) win over the supplied
        // options, giving the root the same "own overrides win" treatment every nested scope receives.
        var effective = graph.OverlayOnto(options);
        return LayoutScope(graph, effective);
    }

    /// <summary>
    /// Lays out a single container scope with the resolved leaf algorithm, recursing into any nested
    /// containers first and composing their sub-layouts into this level's coordinate space.
    /// </summary>
    /// <param name="graph">The container scope to place (the root graph or a node's child subgraph).</param>
    /// <param name="effective">
    /// This scope's cascaded effective options: the parent scope's resolved snapshot overlaid by this
    /// scope's own explicit overrides, per <see cref="PropertyHolder.OverlayOnto"/>.
    /// </param>
    /// <returns>The placed sub-tree for this scope, in local coordinates rooted at the origin.</returns>
    private LayoutTree LayoutScope(LayoutGraph graph, LayoutOptions effective)
    {
        var algo = _registry.Resolve(ResolveLeafAlgorithmId(effective));

        // Flat fast path: when no direct node is a container, delegate straight to the leaf algorithm so
        // the result is byte-for-byte identical to invoking that algorithm directly. This preserves the
        // flat-graph equivalence guarantee: no cloning, no post-processing, no mutation.
        var anyContainer = graph.Nodes.Any(node => node.HasChildren);

        if (!anyContainer)
        {
            return algo.Apply(graph, effective);
        }

        // Hierarchical path (SeparateChildren): recurse into each container child, size it to fit, place
        // this level over a sized view, compose the sub-layouts, and route cross-container edges.
        var subLayouts = new Dictionary<LayoutGraphNode, LayoutTree>();
        var effectiveSize = new Dictionary<LayoutGraphNode, (double Width, double Height)>();
        LayoutContainerChildren(graph, effective, subLayouts, effectiveSize);

        var (view, _) = BuildSizedView(graph, effectiveSize);

        // Place this level with the resolved leaf algorithm over the sized view. The leaf algorithm
        // emits one box per node in Nodes order, so placed boxes align with graph.Nodes by index.
        var placed = algo.Apply(view, effective);
        var placedBoxes = placed.Nodes.OfType<LayoutBox>().ToList();
        var placedLines = placed.Nodes.OfType<LayoutLine>().ToList();

        var (composed, indexOf) = ComposeBoxes(graph, placedBoxes, subLayouts);

        var crossLines = RouteCrossContainerEdges(graph, composed, indexOf, effective);

        // Assemble: composed boxes, then the leaf algorithm's routed lines, then the cross-container
        // lines. The canvas dimensions are the leaf algorithm's for this (sized) level.
        var nodes = new List<LayoutNode>(composed.Length + placedLines.Count + crossLines.Count);
        nodes.AddRange(composed);
        nodes.AddRange(placedLines);
        nodes.AddRange(crossLines);
        return new LayoutTree(placed.Width, placed.Height, nodes);
    }

    /// <summary>
    /// Recurses into every container child of <paramref name="graph"/>, laying out its subgraph and
    /// recording both the resulting sub-layout and the container's effective (child-fitting) size.
    /// </summary>
    /// <param name="graph">The scope whose container children are laid out.</param>
    /// <param name="parentEffective">This scope's own already-resolved cascaded effective options.</param>
    /// <param name="subLayouts">Receives each container's placed sub-tree in local coordinates.</param>
    /// <param name="effectiveSize">Receives each container's size enclosing its children plus padding/title.</param>
    private void LayoutContainerChildren(
        LayoutGraph graph,
        LayoutOptions parentEffective,
        Dictionary<LayoutGraphNode, LayoutTree> subLayouts,
        Dictionary<LayoutGraphNode, (double Width, double Height)> effectiveSize)
    {
        foreach (var node in graph.Nodes)
        {
            if (!node.HasChildren)
            {
                continue;
            }

            // Preserve both established, independently-tested conventions: CoreOptions.Algorithm
            // overrides live on the container node itself, while every other CoreOptions property
            // (Direction, EdgeRouting, and so on) lives on the node's Children graph. Overlaying in this
            // order lets either layer's own overrides win over the parent's resolved snapshot, and lets
            // the Children graph's overrides win over the node's own (though the two never collide, since
            // Algorithm is only ever set on the node and every other property only ever on Children).
            var nodeEffective = node.OverlayOnto(parentEffective);
            var childEffective = node.Children.OverlayOnto(nodeEffective);

            var sub = LayoutScope(node.Children, childEffective);
            subLayouts[node] = sub;

            // Size the container to enclose its sub-layout plus a padding inset on every side and, when
            // the container is labelled, a title band above the children.
            var titleHeight = node.Label is null ? 0.0 : ContainerTitleHeight;
            effectiveSize[node] = (
                sub.Width + (2 * ContainerPadding),
                sub.Height + (2 * ContainerPadding) + titleHeight);
        }
    }

    /// <summary>
    /// Builds an internal, side-effect-free sized view of <paramref name="graph"/>: the same nodes in
    /// the same order (container nodes carrying their effective size, leaves their own size), only
    /// the edges whose endpoints are both direct members of this scope. The scope's own cascaded
    /// options are carried separately as the caller's <c>effective</c> snapshot rather than propagated
    /// onto this view, since every leaf algorithm resolves such properties from the options it is
    /// invoked with.
    /// </summary>
    /// <param name="graph">The caller's scope, which is never mutated.</param>
    /// <param name="effectiveSize">Effective sizes for the container nodes of this scope.</param>
    /// <returns>The sized view graph and a map from each original node to its view counterpart.</returns>
    private static (LayoutGraph View, Dictionary<LayoutGraphNode, LayoutGraphNode> ViewOf) BuildSizedView(
        LayoutGraph graph,
        Dictionary<LayoutGraphNode, (double Width, double Height)> effectiveSize)
    {
        var view = new LayoutGraph();

        var viewOf = new Dictionary<LayoutGraphNode, LayoutGraphNode>(graph.Nodes.Count);
        foreach (var node in graph.Nodes)
        {
            var (width, height) = effectiveSize.TryGetValue(node, out var size)
                ? size
                : (node.Width, node.Height);
            var viewNode = view.AddNode(node.Id, width, height);
            viewNode.Label = node.Label;
            viewOf[node] = viewNode;
        }

        foreach (var edge in graph.Edges)
        {
            // Only edges whose both endpoints are direct members of this scope are placed by the leaf
            // algorithm here; cross-container edges are routed separately after composition.
            if (viewOf.TryGetValue(edge.Source, out var viewSource) &&
                viewOf.TryGetValue(edge.Target, out var viewTarget))
            {
                var viewEdge = view.AddEdge(edge.Id, viewSource, viewTarget);
                viewEdge.TargetEnd = edge.TargetEnd;
                viewEdge.LineStyle = edge.LineStyle;
                viewEdge.Label = edge.Label;
            }
        }

        return (view, viewOf);
    }

    /// <summary>
    /// Composes the placed boxes for this level: each container box receives its recursively laid-out
    /// children, translated from their local origin into the box's padded (and, when labelled,
    /// title-offset) interior.
    /// </summary>
    /// <param name="graph">The scope whose nodes align with <paramref name="placedBoxes"/> by index.</param>
    /// <param name="placedBoxes">The leaf algorithm's placed boxes, one per node in input order.</param>
    /// <param name="subLayouts">The recursively laid-out sub-tree for each container node.</param>
    /// <returns>The composed boxes and a map from each node to its positional index.</returns>
    private static (LayoutBox[] Composed, Dictionary<LayoutGraphNode, int> IndexOf) ComposeBoxes(
        LayoutGraph graph,
        List<LayoutBox> placedBoxes,
        Dictionary<LayoutGraphNode, LayoutTree> subLayouts)
    {
        var count = graph.Nodes.Count;
        var indexOf = new Dictionary<LayoutGraphNode, int>(count);
        var composed = new LayoutBox[count];
        for (var i = 0; i < count; i++)
        {
            var node = graph.Nodes[i];
            indexOf[node] = i;
            var box = placedBoxes[i];

            if (subLayouts.TryGetValue(node, out var sub))
            {
                // Offset the nested content to the container's padded interior, below any title band.
                var titleHeight = node.Label is null ? 0.0 : ContainerTitleHeight;
                var offsetX = box.X + ContainerPadding;
                var offsetY = box.Y + ContainerPadding + titleHeight;
                var children = new List<LayoutNode>(sub.Nodes.Count);
                foreach (var childNode in sub.Nodes)
                {
                    children.Add(Translate(childNode, offsetX, offsetY));
                }

                composed[i] = box with { Children = children };
            }
            else
            {
                composed[i] = box;
            }
        }

        return (composed, indexOf);
    }

    /// <summary>
    /// Routes the cross-container edges owned by this scope — edges whose endpoints resolve to different
    /// direct-member containers of this level — around the sibling boxes between them.
    /// </summary>
    /// <param name="graph">The scope whose edges are examined for cross-container routing.</param>
    /// <param name="composed">The composed top-level boxes of this scope, aligned with its nodes by index.</param>
    /// <param name="indexOf">Map from each direct-member node to its positional index in <paramref name="composed"/>.</param>
    /// <param name="effective">This scope's cascaded effective options, supplying <see cref="CoreOptions.EdgeRouting"/>.</param>
    /// <returns>One routed line per cross-container edge owned by this scope.</returns>
    private static List<LayoutLine> RouteCrossContainerEdges(
        LayoutGraph graph,
        LayoutBox[] composed,
        Dictionary<LayoutGraphNode, int> indexOf,
        LayoutOptions effective)
    {
        // Map every descendant node to the direct member of this scope that contains it, so an edge that
        // references a deeply nested endpoint can be anchored to the top-level box that owns it.
        var descendantToDirect = new Dictionary<LayoutGraphNode, LayoutGraphNode>();
        foreach (var direct in graph.Nodes)
        {
            MapDescendants(direct, direct, descendantToDirect);
        }

        var routeOptions = new ConnectorRouteOptions(effective.Get(CoreOptions.EdgeRouting));
        var boxesForRouting = (IReadOnlyList<LayoutBox>)composed;

        // Collect every cross-container edge owned by this scope into one list of Connections, then
        // route them all in a single batch call. Routing them independently (one ConnectorRouter.Route
        // call per edge) would let separate edges that converge on the same box face pick colliding
        // anchors, and separate edges on similar paths collapse onto the same corridor — the batch
        // overload spreads shared-face anchors and steers later connectors around earlier ones.
        var connections = new List<Connection>();
        foreach (var edge in graph.Edges)
        {
            // Skip edges whose endpoints are not both under this scope.
            if (!descendantToDirect.TryGetValue(edge.Source, out var sourceDirect) ||
                !descendantToDirect.TryGetValue(edge.Target, out var targetDirect))
            {
                continue;
            }

            // An edge whose endpoints are both direct members is already routed by the leaf algorithm.
            var bothDirect = ReferenceEquals(sourceDirect, edge.Source) &&
                             ReferenceEquals(targetDirect, edge.Target);
            if (bothDirect)
            {
                continue;
            }

            // An edge whose endpoints share one container belongs to that lower scope, not this one.
            if (ReferenceEquals(sourceDirect, targetDirect))
            {
                continue;
            }

            var from = composed[indexOf[sourceDirect]];
            var to = composed[indexOf[targetDirect]];
            connections.Add(new Connection(from, to, edge.TargetEnd, edge.LineStyle, edge.Label));
        }

        var crossLines = new List<LayoutLine>(ConnectorRouter.Route(boxesForRouting, connections, routeOptions));

        return crossLines;
    }

    /// <summary>
    /// Resolves the leaf-algorithm identifier a scope is placed with from its already-cascaded effective
    /// options.
    /// </summary>
    /// <param name="effective">
    /// This scope's cascaded effective options, produced by overlaying the scope's own explicit
    /// <see cref="CoreOptions.Algorithm"/> override (if any) onto its parent's resolved snapshot.
    /// </param>
    /// <returns>
    /// The leaf-algorithm identifier to place the scope with. When it resolves to this engine's own
    /// <see cref="AlgorithmId"/> (which is not a leaf), the <see cref="DefaultLeafAlgorithmId"/> is
    /// substituted so recursion terminates in a registered leaf instead of failing to resolve.
    /// </returns>
    private static string ResolveLeafAlgorithmId(LayoutOptions effective)
    {
        var resolved = effective.Get(CoreOptions.Algorithm);
        return resolved == AlgorithmId ? DefaultLeafAlgorithmId : resolved;
    }

    /// <summary>
    /// Records that <paramref name="node"/> and all of its descendants belong to the direct-member
    /// container <paramref name="direct"/>, so a cross-container edge referencing any descendant can be
    /// anchored to the top-level box representing that container.
    /// </summary>
    /// <param name="node">The node currently being mapped (initially the direct member itself).</param>
    /// <param name="direct">The direct member of the scope that owns this subtree.</param>
    /// <param name="map">The descendant-to-direct-member map being populated.</param>
    private static void MapDescendants(
        LayoutGraphNode node,
        LayoutGraphNode direct,
        Dictionary<LayoutGraphNode, LayoutGraphNode> map)
    {
        map[node] = direct;
        if (node.HasChildren)
        {
            foreach (var child in node.Children.Nodes)
            {
                MapDescendants(child, direct, map);
            }
        }
    }

    /// <summary>
    /// Translates a placed layout node by a fixed offset, recursively shifting a box's nested children
    /// and a line's waypoints so a locally-placed sub-layout can be composed into an absolute position.
    /// </summary>
    /// <param name="node">The placed node to translate; only boxes and lines are produced by the bundled leaf algorithms.</param>
    /// <param name="deltaX">Offset added to every X coordinate, in logical pixels.</param>
    /// <param name="deltaY">Offset added to every Y coordinate, in logical pixels.</param>
    /// <returns>A translated copy of <paramref name="node"/>, or the node unchanged for unknown subtypes.</returns>
    private static LayoutNode Translate(LayoutNode node, double deltaX, double deltaY)
    {
        switch (node)
        {
            case LayoutBox box:
                var children = new List<LayoutNode>(box.Children.Count);
                foreach (var child in box.Children)
                {
                    children.Add(Translate(child, deltaX, deltaY));
                }

                return box with { X = box.X + deltaX, Y = box.Y + deltaY, Children = children };

            case LayoutLine line:
                var waypoints = new List<Point2D>(line.Waypoints.Count);
                foreach (var point in line.Waypoints)
                {
                    waypoints.Add(new Point2D(point.X + deltaX, point.Y + deltaY));
                }

                return line with { Waypoints = waypoints };

            default:
                // Forward compatibility: an unknown node subtype is left untranslated rather than dropped.
                return node;
        }
    }
}
