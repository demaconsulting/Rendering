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
///     The leaf algorithm is chosen <em>per scope</em> through <see cref="CoreOptions.Algorithm"/>: the
///     root scope inherits the algorithm from the supplied <see cref="LayoutOptions"/> (default
///     <c>layered</c>), and any container node may override it by setting
///     <see cref="CoreOptions.Algorithm"/> on itself; unset containers inherit their parent scope's
///     algorithm. This lets, for example, a <c>containment</c>-packed root hold a <c>layered</c>
///     container without either level knowing about the other.
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

        // The root scope inherits the algorithm from the options (its own default is "layered"), unless
        // the graph itself carries an explicit CoreOptions.Algorithm override.
        var algoId = ResolveScopeAlgorithm(graph, options.Get(CoreOptions.Algorithm));
        return LayoutScope(graph, algoId, options);
    }

    /// <summary>
    /// Lays out a single container scope with the resolved leaf algorithm, recursing into any nested
    /// containers first and composing their sub-layouts into this level's coordinate space.
    /// </summary>
    /// <param name="graph">The container scope to place (the root graph or a node's child subgraph).</param>
    /// <param name="algoId">Identifier of the leaf algorithm to place this scope with.</param>
    /// <param name="options">The shared options carried into every leaf-algorithm invocation.</param>
    /// <returns>The placed sub-tree for this scope, in local coordinates rooted at the origin.</returns>
    private LayoutTree LayoutScope(LayoutGraph graph, string algoId, LayoutOptions options)
    {
        var algo = _registry.Resolve(algoId);

        // Flat fast path: when no direct node is a container, delegate straight to the leaf algorithm so
        // the result is byte-for-byte identical to invoking that algorithm directly. This preserves the
        // flat-graph equivalence guarantee: no cloning, no post-processing, no mutation.
        var anyContainer = false;
        foreach (var node in graph.Nodes)
        {
            if (node.HasChildren)
            {
                anyContainer = true;
                break;
            }
        }

        if (!anyContainer)
        {
            return algo.Apply(graph, options);
        }

        // Hierarchical path (SeparateChildren): recurse into each container child, size it to fit, place
        // this level over a sized view, compose the sub-layouts, and route cross-container edges.
        var subLayouts = new Dictionary<LayoutGraphNode, LayoutTree>();
        var effectiveSize = new Dictionary<LayoutGraphNode, (double Width, double Height)>();
        LayoutContainerChildren(graph, algoId, options, subLayouts, effectiveSize);

        var (view, _) = BuildSizedView(graph, effectiveSize);

        // Place this level with the resolved leaf algorithm over the sized view. The leaf algorithm
        // emits one box per node in Nodes order, so placed boxes align with graph.Nodes by index.
        var placed = algo.Apply(view, options);
        var placedBoxes = placed.Nodes.OfType<LayoutBox>().ToList();
        var placedLines = placed.Nodes.OfType<LayoutLine>().ToList();

        var (composed, indexOf) = ComposeBoxes(graph, placedBoxes, subLayouts);

        var crossLines = RouteCrossContainerEdges(graph, composed, indexOf, options);

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
    /// <param name="algoId">The algorithm inherited by children that do not override it.</param>
    /// <param name="options">The shared options carried into each recursive layout.</param>
    /// <param name="subLayouts">Receives each container's placed sub-tree in local coordinates.</param>
    /// <param name="effectiveSize">Receives each container's size enclosing its children plus padding/title.</param>
    private void LayoutContainerChildren(
        LayoutGraph graph,
        string algoId,
        LayoutOptions options,
        Dictionary<LayoutGraphNode, LayoutTree> subLayouts,
        Dictionary<LayoutGraphNode, (double Width, double Height)> effectiveSize)
    {
        foreach (var node in graph.Nodes)
        {
            if (!node.HasChildren)
            {
                continue;
            }

            // A container inherits this scope's algorithm unless it overrides CoreOptions.Algorithm.
            var childAlgoId = ResolveScopeAlgorithm(node, algoId);
            var sub = LayoutScope(node.Children, childAlgoId, options);
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
    /// the same order (container nodes carrying their effective size, leaves their own size) and only
    /// the edges whose endpoints are both direct members of this scope.
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
    /// <param name="options">Options supplying the per-scope <see cref="CoreOptions.EdgeRouting"/> style.</param>
    /// <returns>One routed line per cross-container edge owned by this scope.</returns>
    private static List<LayoutLine> RouteCrossContainerEdges(
        LayoutGraph graph,
        LayoutBox[] composed,
        Dictionary<LayoutGraphNode, int> indexOf,
        LayoutOptions options)
    {
        // Map every descendant node to the direct member of this scope that contains it, so an edge that
        // references a deeply nested endpoint can be anchored to the top-level box that owns it.
        var descendantToDirect = new Dictionary<LayoutGraphNode, LayoutGraphNode>();
        foreach (var direct in graph.Nodes)
        {
            MapDescendants(direct, direct, descendantToDirect);
        }

        var routeOptions = new ConnectorRouteOptions(options.Get(CoreOptions.EdgeRouting));
        var boxesForRouting = (IReadOnlyList<LayoutBox>)composed;
        var crossLines = new List<LayoutLine>();
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
            crossLines.Add(ConnectorRouter.Route(
                boxesForRouting,
                new Connection(from, to, edge.TargetEnd, edge.LineStyle, edge.Label),
                routeOptions));
        }

        return crossLines;
    }

    /// <summary>
    /// Resolves the layout algorithm for a scope: the scope's explicit <see cref="CoreOptions.Algorithm"/>
    /// override when present, otherwise the <paramref name="inherited"/> algorithm from the parent scope.
    /// </summary>
    /// <param name="scope">The graph or node whose algorithm override is consulted.</param>
    /// <param name="inherited">The algorithm inherited when the scope carries no override.</param>
    /// <returns>The algorithm identifier to place the scope with.</returns>
    private static string ResolveScopeAlgorithm(PropertyHolder scope, string inherited) =>
        scope.TryGet(CoreOptions.Algorithm, out var value) ? value : inherited;

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
