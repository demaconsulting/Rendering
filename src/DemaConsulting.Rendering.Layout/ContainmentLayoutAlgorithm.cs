// <copyright file="ContainmentLayoutAlgorithm.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout;

/// <summary>
/// The bundled containment layout algorithm: arranges the input graph's top-level nodes by packing them
/// into rows within a width budget, then routes each edge around the packed boxes with the selected
/// <see cref="CoreOptions.EdgeRouting"/> style. This is a second reference <see cref="ILayoutAlgorithm"/>
/// implementation alongside the layered algorithm; it composes the reusable
/// <see cref="ContainmentLayout"/> packer and the <see cref="ConnectorRouter"/> orchestration.
/// </summary>
/// <remarks>
///     <para>
///     Where the bundled <see cref="LayeredLayoutAlgorithm"/> arranges nodes by their connectivity into
///     Sugiyama layers, the containment algorithm arranges them by their <em>reading order</em>: each
///     node becomes a leaf box, the boxes are packed left to right and wrapped into rows within a
///     heuristic content width, and the graph's edges are then routed around the packed boxes. It suits
///     views whose elements group as peers inside a container rather than flowing along a directed
///     spine.
///     </para>
///     <para>
///     The algorithm is deterministic and order-preserving: given the same graph and options it always
///     produces the same geometry, and the placed boxes appear in the same order as
///     <see cref="LayoutGraph.Nodes"/>. It reads only the top-level nodes and edges — a node's nested
///     <see cref="LayoutGraphNode.Children"/> are treated as opaque and are not laid out at this level.
///     The content-width budget is derived from a roughly four-by-three canvas heuristic (the square root
///     of the total box area, widened to at least the widest box) so a wide set of boxes wraps into a
///     balanced block rather than one long row.
///     </para>
///     <para>
///     Edges whose <see cref="LayoutGraphEdge.Source"/> or <see cref="LayoutGraphEdge.Target"/> is not a
///     top-level node of the graph are skipped, mirroring how the layered algorithm drops out-of-graph
///     endpoints. Every routed connector carries its edge's <see cref="LayoutGraphEdge.TargetEnd"/>,
///     <see cref="LayoutGraphEdge.LineStyle"/>, and <see cref="LayoutGraphEdge.Label"/>.
///     </para>
/// </remarks>
/// <example>
///     <code>
///     // Build a small graph of peer boxes joined by a couple of edges.
///     var graph = new LayoutGraph();
///     var a = graph.AddNode("a", 80, 40);
///     var b = graph.AddNode("b", 80, 40);
///     var c = graph.AddNode("c", 80, 40);
///     a.Label = "A";
///     b.Label = "B";
///     c.Label = "C";
///     var e1 = graph.AddEdge("e1", a, b);
///     e1.TargetEnd = EndMarkerStyle.FilledArrow;
///     graph.AddEdge("e2", a, c);
///
///     // Pack the nodes into rows and route the edges around them.
///     var tree = new ContainmentLayoutAlgorithm().Apply(graph, new LayoutOptions());
///
///     // Hand the placed tree to a renderer (for example the SVG renderer).
///     // new SvgRenderer().Render(tree, new RenderOptions(Themes.Light), stream);
///     </code>
/// </example>
public sealed class ContainmentLayoutAlgorithm : ILayoutAlgorithm
{
    /// <summary>
    /// The stable algorithm identifier <c>"containment"</c> under which this algorithm is selected and
    /// registered. Pass it to <see cref="LayoutOptions.ForAlgorithm(string)"/> or
    /// <see cref="CoreOptions.Algorithm"/> instead of hardcoding the literal string.
    /// </summary>
    public const string AlgorithmId = "containment";

    /// <summary>
    /// Target width-to-height ratio of the packed content block. A value of <c>4/3</c> biases the
    /// derived content width toward a landscape canvas so a wide set of boxes wraps into a balanced
    /// block rather than a single long row.
    /// </summary>
    private const double CanvasAspectRatio = 4.0 / 3.0;

    /// <summary>
    /// Lower bound applied to the derived content width so the packer always receives a positive width,
    /// even for a graph with no nodes.
    /// </summary>
    private const double MinContentWidth = 1.0;

    /// <summary>
    /// Gap, in logical pixels, kept between packed boxes on both axes. It is sized to leave room for a
    /// connector to pass cleanly between two packed boxes: the orthogonal router steps off a box edge by
    /// an approach stub of roughly the routing clearance plus a small margin, so a gap wider than that
    /// stub lets an edge route around an intervening box instead of being forced to cross it.
    /// </summary>
    private const double NodeSpacing = 24.0;

    /// <inheritdoc/>
    public string Id => AlgorithmId;

    /// <inheritdoc/>
    public LayoutTree Apply(LayoutGraph graph, LayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(options);

        var graphNodes = graph.Nodes;
        var count = graphNodes.Count;

        // Convert each top-level node into a leaf box, remembering its index so edges can be mapped to
        // the packed box that ends up representing the node. Nested children are not laid out here.
        var boxes = new LayoutBox[count];
        var indexOf = new Dictionary<LayoutGraphNode, int>(count);
        for (var i = 0; i < count; i++)
        {
            var node = graphNodes[i];
            indexOf[node] = i;
            boxes[i] = new LayoutBox(
                0,
                0,
                node.Width,
                node.Height,
                node.Label,
                Depth: 0,
                node.Shape,
                node.Compartments,
                Children: [],
                Keyword: node.Keyword,
                RoundedCornerRadius: node.RoundedCornerRadius,
                FolderTabWidth: node.FolderTabWidth,
                FolderTabHeight: node.FolderTabHeight);
        }

        // Pack the leaf boxes into rows within a width budget derived from a roughly 4:3 canvas, keeping
        // a connector-aware gap between boxes so edges can route around intervening ones.
        var containmentOptions = new ContainmentOptions(
            ComputeContentWidth(boxes),
            HorizontalGap: NodeSpacing,
            VerticalGap: NodeSpacing);
        var result = ContainmentLayout.Pack(boxes, containmentOptions);
        var packedBoxes = result.Children;

        // Build one connection per edge whose endpoints are both top-level nodes, carrying the edge's
        // styling; edges referencing out-of-graph nodes are skipped.
        var connections = new List<Connection>(graph.Edges.Count);
        foreach (var edge in graph.Edges)
        {
            if (indexOf.TryGetValue(edge.Source, out var s) &&
                indexOf.TryGetValue(edge.Target, out var t))
            {
                connections.Add(new Connection(
                    packedBoxes[s],
                    packedBoxes[t],
                    edge.TargetEnd,
                    edge.LineStyle,
                    edge.Label));
            }
        }

        // Route the connections around the packed boxes using the per-scope routing style.
        var routeOptions = new ConnectorRouteOptions(ResolveEdgeRouting(graph, options));
        var routedLines = ConnectorRouter.Route(packedBoxes, connections, routeOptions);

        var nodes = new List<LayoutNode>(packedBoxes.Count + routedLines.Count);
        nodes.AddRange(packedBoxes);
        nodes.AddRange(routedLines);

        return new LayoutTree(result.Width, result.Height, nodes);
    }

    /// <summary>
    /// Resolves the edge-routing style for this layout: an explicit <see cref="CoreOptions.EdgeRouting"/>
    /// on the graph takes precedence, then one on the options, falling back to the property default when
    /// neither declares one. Mirrors <see cref="LayeredLayoutAlgorithm"/>'s graph-then-options-then-default
    /// resolution for <see cref="CoreOptions.Direction"/>, so a graph-level override is honored whether
    /// this algorithm is invoked directly or as a scope's leaf algorithm inside
    /// <see cref="HierarchicalLayoutAlgorithm"/>.
    /// </summary>
    /// <param name="graph">The graph whose explicit edge-routing declaration takes precedence.</param>
    /// <param name="options">The options consulted when the graph declares no edge-routing style.</param>
    /// <returns>The edge-routing style to route connections with.</returns>
    private static EdgeRouting ResolveEdgeRouting(LayoutGraph graph, LayoutOptions options)
    {
        if (graph.TryGet(CoreOptions.EdgeRouting, out var fromGraph))
        {
            return fromGraph;
        }

        return options.TryGet(CoreOptions.EdgeRouting, out var fromOptions)
            ? fromOptions
            : CoreOptions.EdgeRouting.DefaultValue;
    }

    /// <summary>
    /// Derives the packer's content-width budget from the boxes: the square root of their total area
    /// biased toward a landscape canvas, widened to at least the widest box, and floored to a positive
    /// value so the packer always receives a usable width.
    /// </summary>
    private static double ComputeContentWidth(IReadOnlyList<LayoutBox> boxes)
    {
        var totalArea = 0.0;
        var widest = 0.0;
        foreach (var box in boxes)
        {
            totalArea += box.Width * box.Height;
            widest = Math.Max(widest, box.Width);
        }

        var target = Math.Sqrt(totalArea * CanvasAspectRatio);
        return Math.Max(Math.Max(widest, target), MinContentWidth);
    }
}
