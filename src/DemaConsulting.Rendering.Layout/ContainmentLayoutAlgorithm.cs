// <copyright file="ContainmentLayoutAlgorithm.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout;

/// <summary>
/// The bundled containment layout algorithm: arranges the input graph's top-level nodes by packing them
/// into rows within a width budget, then routes each edge around the packed boxes with the selected
/// <see cref="CoreOptions.EdgeRouting"/> style. This is a second reference <see cref="LayoutAlgorithmBase"/>
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
///     var tree = new ContainmentLayoutAlgorithm().Apply(graph);
///
///     // Hand the placed tree to a renderer (for example the SVG renderer).
///     // new SvgRenderer().Render(tree, new RenderOptions(Themes.Light), stream);
///     </code>
/// </example>
public sealed class ContainmentLayoutAlgorithm : LayoutAlgorithmBase
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

    /// <summary>
    /// Minimum number of boxes before the column-count-based width candidate (see
    /// <see cref="ComputeContentWidth"/>) is considered at all. A handful of boxes with very different
    /// sizes (for example two boxes, one much taller than the other) legitimately want to stack in a
    /// single column rather than be forced side by side just because <c>ceil(sqrt(n))</c> is greater
    /// than one; the column estimate only pays for itself once there are enough boxes for multi-column
    /// packing to plausibly make sense.
    /// </summary>
    private const int MinBoxesForColumnEstimate = 6;

    /// <summary>
    /// Largest-to-smallest box-size ratio (width, and separately height) at or below which the
    /// column-count-based width candidate (see <see cref="ComputeContentWidth"/>) applies at full
    /// weight. The motivating scenario for that candidate is many boxes that are each individually
    /// wide-but-short and roughly uniform in size (for example a row of small labelled tiles); at or
    /// below this ratio, box sizes are considered uniform enough that the averaged width used by the
    /// estimate is representative of every box, so the candidate contributes its full computed width.
    /// Above this ratio the candidate's weight falls off smoothly toward zero, rather than being cut
    /// off abruptly, up to <see cref="ColumnEstimateZeroWeightSizeRatio"/> (see
    /// <see cref="ComputeColumnEstimateWeight"/>).
    /// </summary>
    private const double ColumnEstimateFullWeightSizeRatio = 2.0;

    /// <summary>
    /// Largest-to-smallest box-size ratio (width, and separately height) at or above which the
    /// column-count-based width candidate (see <see cref="ComputeContentWidth"/>) contributes nothing.
    /// The motivating scenario for suppressing the candidate entirely is a genuinely pathological mix —
    /// a couple of very differently sized boxes (for example one much taller than the other) among many
    /// small ones — where the averaged width is not representative of any single box and legitimately
    /// wants to stack in a single column instead of being forced into a wider grid. Ratios between
    /// <see cref="ColumnEstimateFullWeightSizeRatio"/> and this value scale the candidate's weight down
    /// linearly rather than snapping straight from full weight to none, since ordinary box sets — for
    /// example differently-named peer boxes whose label lengths alone routinely push the ratio just over
    /// 2 — should still benefit from most of the candidate's widening rather than losing all of it at an
    /// arbitrary cliff.
    /// </summary>
    private const double ColumnEstimateZeroWeightSizeRatio = 6.0;

    /// <inheritdoc/>
    public override string Id => AlgorithmId;

    /// <inheritdoc/>
    protected internal override LayoutTree ApplyCore(LayoutGraph graph, LayoutOptions options)
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
                FolderTabHeight: node.FolderTabHeight,
                CenterTitle: node.Compartments.Count == 0);
        }

        // Build the per-pair edge-count lookup the packer uses to widen the horizontal gap between two
        // same-row boxes carrying a fan of parallel connectors. Reuse the same indexOf map built for the
        // boxes: for every edge whose source and target are both top-level nodes, increment the count for
        // the unordered index pair (min, max). Edges referencing out-of-graph endpoints are ignored, the
        // same endpoints the connection-building loop below skips.
        var edgeCounts = new Dictionary<(int First, int Second), int>();
        foreach (var edge in graph.Edges)
        {
            if (edge.Source is LayoutGraphNode sourceNode && indexOf.TryGetValue(sourceNode, out var s) &&
                edge.Target is LayoutGraphNode targetNode && indexOf.TryGetValue(targetNode, out var t) &&
                s != t)
            {
                var key = s < t ? (s, t) : (t, s);
                edgeCounts.TryGetValue(key, out var existing);
                edgeCounts[key] = existing + 1;
            }
        }

        // Pack the leaf boxes into rows within a width budget derived from a roughly 4:3 canvas, keeping
        // a connector-aware gap between boxes so edges can route around intervening ones. The edge-count
        // lookup additionally widens the horizontal gap between two same-row boxes joined by a fan of
        // parallel connectors so those connectors get distinct routing lanes.
        var containmentOptions = new ContainmentOptions(
            ComputeContentWidth(boxes),
            HorizontalGap: NodeSpacing,
            VerticalGap: NodeSpacing,
            EdgeCounts: edgeCounts);
        var result = ContainmentLayout.Pack(boxes, containmentOptions);
        var packedBoxes = result.Children;

        // Build one connection per edge whose endpoints are both top-level nodes, carrying the edge's
        // styling; edges referencing out-of-graph nodes are skipped.
        var connections = new List<Connection>(graph.Edges.Count);
        foreach (var edge in graph.Edges)
        {
            if (edge.Source is LayoutGraphNode sourceNode && indexOf.TryGetValue(sourceNode, out var s) &&
                edge.Target is LayoutGraphNode targetNode && indexOf.TryGetValue(targetNode, out var t))
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
    /// biased toward a landscape canvas, widened to at least the widest box and to a column-count-based
    /// estimate for many small boxes, and floored to a positive value so the packer always receives a
    /// usable width.
    /// </summary>
    /// <remarks>
    ///     The area-based <c>target</c> width alone under-estimates the right budget for a set of many
    ///     small, wide boxes: <c>sqrt(totalArea * aspect)</c> grows with the square root of the box
    ///     count, while a set of <em>n</em> equally-sized boxes actually wants roughly
    ///     <c>sqrt(n)</c> columns side by side — the same growth rate, but scaled by each box's own
    ///     width rather than by the square root of its area, which for a wide, short box (for example
    ///     160 by 40) is a materially larger number. Without this second candidate, many small wide
    ///     boxes could be packed into an unrealistically narrow single- or two-column budget instead of
    ///     the balanced, roughly-square block of columns the 4:3 landscape bias intends. This candidate
    ///     is strictly additive — combined via <c>Math.Max</c>, so it only ever widens, never narrows,
    ///     the chosen budget. It is only considered when there are at least
    ///     <see cref="MinBoxesForColumnEstimate"/> boxes, and its contribution is scaled by
    ///     <see cref="ComputeColumnEstimateWeight"/> — full weight when box widths and heights are
    ///     reasonably uniform, falling off smoothly (rather than an abrupt cliff) as they diverge, and
    ///     zero once they diverge enough that the averaged width is no longer representative of any
    ///     single box (a few large outlier boxes mixed with many small ones, where the candidate's
    ///     underlying premise breaks down). Because the risk is asymmetric — applying the estimate too
    ///     liberally costs at most some extra whitespace, since <c>Math.Max</c> can only widen the
    ///     result, while suppressing it entirely for an ordinary, moderately-varied box set collapses a
    ///     balanced grid into a single degenerate column — the falloff favors keeping most of the
    ///     estimate's contribution well past the point a hard cutoff would have discarded it outright.
    /// </remarks>
    internal static double ComputeContentWidth(IReadOnlyList<LayoutBox> boxes)
    {
        var totalArea = 0.0;
        var totalWidth = 0.0;
        var widest = 0.0;
        var minWidth = double.PositiveInfinity;
        var minHeight = double.PositiveInfinity;
        var maxHeight = 0.0;
        foreach (var box in boxes)
        {
            totalArea += box.Width * box.Height;
            totalWidth += box.Width;
            widest = Math.Max(widest, box.Width);
            minWidth = Math.Min(minWidth, box.Width);
            minHeight = Math.Min(minHeight, box.Height);
            maxHeight = Math.Max(maxHeight, box.Height);
        }

        var target = Math.Sqrt(totalArea * CanvasAspectRatio);

        // A column-count estimate (columns = ceil(sqrt(n))) sized from each box's own average width,
        // rather than the total area, so a set of many small but wide boxes still wraps into a
        // balanced grid of columns instead of one narrow column. Only considered when there are enough
        // boxes for multi-column packing to plausibly make sense; its contribution is then scaled by
        // ComputeColumnEstimateWeight rather than gated all-or-nothing, so ordinary size variance (for
        // example label-length-driven width differences) does not throw away the whole estimate.
        var columnBasedWidth = 0.0;
        if (boxes.Count >= MinBoxesForColumnEstimate)
        {
            var widthRatio = ComputeSizeRatio(widest, minWidth);
            var heightRatio = ComputeSizeRatio(maxHeight, minHeight);
            var weight = ComputeColumnEstimateWeight(Math.Max(widthRatio, heightRatio));
            if (weight > 0.0)
            {
                var columns = (int)Math.Ceiling(Math.Sqrt(boxes.Count));
                var averageWidth = totalWidth / boxes.Count;
                var rawColumnBasedWidth = (columns * averageWidth) + ((columns - 1) * NodeSpacing);
                columnBasedWidth = weight * rawColumnBasedWidth;
            }
        }

        return Math.Max(Math.Max(Math.Max(widest, target), columnBasedWidth), MinContentWidth);
    }

    /// <summary>
    /// Computes the largest-to-smallest ratio of a box dimension (width, or separately height), treating
    /// a zero-or-negative smallest value as a special case rather than dividing by it: a
    /// <paramref name="smallest"/> of zero alongside a positive <paramref name="largest"/> is maximally
    /// non-uniform (returns <see cref="double.PositiveInfinity"/>, so <see cref="ComputeColumnEstimateWeight"/>
    /// yields zero weight), while both being zero is treated as perfectly uniform (returns
    /// <c>1.0</c>) rather than the indeterminate <c>0/0</c>.
    /// </summary>
    /// <param name="largest">The largest observed value for the dimension.</param>
    /// <param name="smallest">The smallest observed value for the dimension.</param>
    /// <returns>The largest-to-smallest ratio, or a substitute value for the degenerate zero cases.</returns>
    private static double ComputeSizeRatio(double largest, double smallest)
    {
        if (smallest <= 0.0)
        {
            return largest <= 0.0 ? 1.0 : double.PositiveInfinity;
        }

        return largest / smallest;
    }

    /// <summary>
    /// Scales the column-count-based width candidate's contribution by how uniform the packed boxes are
    /// in size, replacing a hard on/off cutoff with a graduated falloff. Returns <c>1.0</c> (full weight)
    /// at or below <see cref="ColumnEstimateFullWeightSizeRatio"/>, <c>0.0</c> (no contribution) at or
    /// above <see cref="ColumnEstimateZeroWeightSizeRatio"/>, and linearly interpolates between those two
    /// bounds for a ratio in between. A single hard cutoff at <see cref="ColumnEstimateFullWeightSizeRatio"/>
    /// (the candidate's original behavior) disabled the estimate entirely for box sets only marginally
    /// over the threshold — for example ordinary, differently-labelled peer boxes whose width variance
    /// alone often exceeds a 2x ratio — collapsing what should be a balanced multi-column grid into a
    /// single degenerate column. Because the candidate can only ever widen the final budget (it is
    /// combined via <c>Math.Max</c> in <see cref="ComputeContentWidth"/>), over-applying it costs at most
    /// some extra whitespace, while under-applying it has no bound on how degenerate the resulting
    /// layout gets — so the falloff is deliberately generous, keeping substantial weight well past the
    /// old cutoff.
    /// </summary>
    /// <param name="sizeRatio">
    /// The largest-to-smallest size ratio to weight, typically the greater of the width ratio and the
    /// height ratio so either axis being non-uniform reduces the candidate's contribution.
    /// </param>
    /// <returns>A weight in the closed range <c>[0.0, 1.0]</c> to scale the column-count-based candidate by.</returns>
    internal static double ComputeColumnEstimateWeight(double sizeRatio)
    {
        if (sizeRatio <= ColumnEstimateFullWeightSizeRatio)
        {
            return 1.0;
        }

        if (sizeRatio >= ColumnEstimateZeroWeightSizeRatio)
        {
            return 0.0;
        }

        return (ColumnEstimateZeroWeightSizeRatio - sizeRatio) /
            (ColumnEstimateZeroWeightSizeRatio - ColumnEstimateFullWeightSizeRatio);
    }
}
