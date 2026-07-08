// <copyright file="LayeredLayoutAlgorithm.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout.Engine;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout;

/// <summary>
/// The bundled ELK-style layered layout algorithm: arranges the input graph into Sugiyama layers and
/// routes edges orthogonally, producing a placed <see cref="LayoutTree"/> of boxes and connectors.
/// This is the reference <see cref="ILayoutAlgorithm"/> implementation; it wraps the reusable layered
/// pipeline under <c>Engine/Layered/</c>. It honors <see cref="CoreOptions.Direction"/> so the layers
/// progress right, left, down, or up (a downward flow lays action-flow and state-transition diagrams
/// out top-to-bottom), <see cref="CoreOptions.NodeSpacing"/> so the minimum gap between nodes stacked
/// in the same layer can be widened or narrowed from the engine's default, and
/// <see cref="CoreOptions.MergeParallelEdges"/> so parallel input edges either collapse to one
/// rendered connector (the default) or each keep their own independently-routed line. An edge whose
/// <see cref="LayoutGraphEdge.Source"/> or <see cref="LayoutGraphEdge.Target"/> is a
/// <see cref="LayoutGraphPort"/> emits a <see cref="LayoutPort"/> anchored at the routed connector
/// endpoint, and each node's <see cref="LayoutBox.ContentInsetLeft"/>/Right/Top/Bottom reserve space
/// for its ports' labels, measured via <see cref="CoreOptions.TextMeasurer"/> (or a dependency-free
/// heuristic fallback) at <see cref="CoreOptions.AssumedFontSize"/>.
/// </summary>
public sealed class LayeredLayoutAlgorithm : ILayoutAlgorithm
{
    /// <summary>
    /// The stable algorithm identifier <c>"layered"</c> under which this algorithm is selected and
    /// registered. Pass it to <see cref="LayoutOptions.ForAlgorithm(string)"/> or
    /// <see cref="CoreOptions.Algorithm"/> instead of hardcoding the literal string.
    /// </summary>
    public const string AlgorithmId = "layered";

    /// <summary>
    /// Clearance, in logical pixels, between a port's glyph/label and the reserved content-inset
    /// boundary it drives, and the flat padding added to <see cref="CoreOptions.AssumedFontSize"/> for
    /// a top/bottom content inset.
    /// </summary>
    private const double PortLabelClearance = 4.0;

    /// <summary>
    /// Tolerance, in logical pixels, used when matching a routed connector anchor point against a
    /// placed node's rectangle edges to classify which <see cref="PortSide"/> it lies on. Generous
    /// enough to absorb ordinary floating-point accumulation while routing, since the engine always
    /// anchors ports exactly on a rectangle face.
    /// </summary>
    private const double PortSideTolerance = 0.01;

    /// <summary>Shared default fallback text measurer, reused across calls to avoid re-allocating.</summary>
    private static readonly HeuristicTextMeasurer SharedHeuristicTextMeasurer = new();

    /// <inheritdoc/>
    public string Id => AlgorithmId;

    /// <inheritdoc/>
    public LayoutTree Apply(LayoutGraph graph, LayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(options);

        var graphNodes = graph.Nodes;
        var count = graphNodes.Count;

        // Map each input node to a positional index the layered engine works in terms of, and each
        // named port to the index of the node that owns it (a port has no independent box; it always
        // anchors to its owning node's placed rectangle).
        var indexOf = new Dictionary<LayoutGraphNode, int>(count);
        var portOwnerIndex = new Dictionary<LayoutGraphPort, int>();
        var engineNodes = new LayerNode[count];
        for (var i = 0; i < count; i++)
        {
            var node = graphNodes[i];
            indexOf[node] = i;
            engineNodes[i] = new LayerNode(
                node.Width,
                node.Height,
                node.Shape,
                node.RoundedCornerRadius,
                node.FolderTabWidth,
                node.FolderTabHeight,
                node.Label,
                RealWidth: node.Width,
                RealHeight: node.Height);

            if (!node.HasPorts)
            {
                continue;
            }

            foreach (var port in node.Ports.Ports)
            {
                portOwnerIndex[port] = i;
            }
        }

        // Map edges to index pairs (resolving a port endpoint to its owning node's index), dropping
        // any that reference nodes/ports outside this graph. Kept in lockstep with engineEdges so the
        // same position in both lists always describes the same input edge.
        var engineEdges = new List<LayerEdge>(graph.Edges.Count);
        var edgeSourcePorts = new List<LayoutGraphPort?>(graph.Edges.Count);
        var edgeTargetPorts = new List<LayoutGraphPort?>(graph.Edges.Count);
        var edgeByEngineIndex = new List<LayoutGraphEdge>(graph.Edges.Count);
        foreach (var edge in graph.Edges)
        {
            if (!TryResolveEndpoint(edge.Source, indexOf, portOwnerIndex, out var s, out var sourcePort) ||
                !TryResolveEndpoint(edge.Target, indexOf, portOwnerIndex, out var t, out var targetPort))
            {
                continue;
            }

            engineEdges.Add(new LayerEdge(s, t));
            edgeSourcePorts.Add(sourcePort);
            edgeTargetPorts.Add(targetPort);
            edgeByEngineIndex.Add(edge);
        }

        var direction = ToEngineDirection(ResolveDirection(graph, options));
        var nodeSpacing = ResolveNodeSpacing(graph, options);
        var mergeParallelEdges = ResolveMergeParallelEdges(graph, options);
        var textMeasurer = ResolveTextMeasurer(graph, options);
        var assumedFontSize = ResolveAssumedFontSize(graph, options);
        var result = InterconnectionLayoutEngine.Place(engineNodes, engineEdges, direction, nodeSpacing, mergeParallelEdges);

        // Build an engine-edge-index -> (waypoints, reversed) lookup from the acyclic edge set the
        // engine routed. When MergeParallelEdges is true, CycleBreaker has already collapsed parallel
        // edges into a single acyclic edge per node pair, so only the surviving engine edge resolves
        // here; the emission loop below independently skips the non-surviving duplicates using the
        // same first-occurrence rule, so the two decisions always agree.
        var routesByEngineIndex = new Dictionary<int, (IReadOnlyList<Point2D> Waypoints, bool Reversed)>();
        for (var k = 0; k < result.AcyclicEdges.Count; k++)
        {
            var originalEngineIndex = result.AcyclicOriginalIndex[k];
            var acyclic = result.AcyclicEdges[k];
            var reversed = acyclic.Source != engineEdges[originalEngineIndex].Source;
            routesByEngineIndex[originalEngineIndex] = (result.ConnectorWaypoints[k], reversed);
        }

        // First pass: decide which edges are emitted (honoring MergeParallelEdges) and resolve each
        // emitted edge's normalized (source -> target) waypoints, so port sides/labels can be
        // aggregated per node before the boxes (which need the resulting content insets) are built.
        var emittedPairs = new HashSet<(int Source, int Target)>();
        var emissions = new List<(
            LayoutGraphEdge Edge,
            int Source,
            int Target,
            LayoutGraphPort? SourcePort,
            LayoutGraphPort? TargetPort,
            IReadOnlyList<Point2D> Waypoints)>();

        for (var i = 0; i < engineEdges.Count; i++)
        {
            var s = engineEdges[i].Source;
            var t = engineEdges[i].Target;

            if (mergeParallelEdges && !emittedPairs.Add((s, t)))
            {
                continue;
            }

            var waypoints = ResolveRoute(routesByEngineIndex, i, s, t, result.Rects);
            emissions.Add((edgeByEngineIndex[i], s, t, edgeSourcePorts[i], edgeTargetPorts[i], waypoints));
        }

        // Aggregate, per node and side, the port labels driving that side's content inset.
        var sidePorts = new Dictionary<int, Dictionary<PortSide, List<string>>>();
        void RecordPort(int nodeIndex, Point2D anchor, string? label)
        {
            var side = ResolveSide(anchor, result.Rects[nodeIndex]);
            if (!sidePorts.TryGetValue(nodeIndex, out var bySide))
            {
                bySide = new Dictionary<PortSide, List<string>>();
                sidePorts[nodeIndex] = bySide;
            }

            if (!bySide.TryGetValue(side, out var labels))
            {
                labels = [];
                bySide[side] = labels;
            }

            labels.Add(label ?? string.Empty);
        }

        foreach (var emission in emissions)
        {
            if (emission.SourcePort != null)
            {
                RecordPort(emission.Source, emission.Waypoints[0], emission.SourcePort.ExternalLabel);
            }

            if (emission.TargetPort != null)
            {
                RecordPort(emission.Target, emission.Waypoints[^1], emission.TargetPort.ExternalLabel);
            }
        }

        var nodes = new List<LayoutNode>(count + (emissions.Count * 2));

        // Emit one placed box per input node, preserving input order, with content insets reserved
        // for that node's port labels (zero on any side with no ports).
        for (var i = 0; i < count; i++)
        {
            var rect = result.Rects[i];
            sidePorts.TryGetValue(i, out var bySide);
            var (insetLeft, insetRight, insetTop, insetBottom) =
                ResolveContentInsets(bySide, textMeasurer, assumedFontSize);

            nodes.Add(new LayoutBox(
                rect.X,
                rect.Y,
                rect.Width,
                rect.Height,
                graphNodes[i].Label,
                Depth: 0,
                graphNodes[i].Shape,
                graphNodes[i].Compartments,
                Children: [],
                Keyword: graphNodes[i].Keyword,
                RoundedCornerRadius: graphNodes[i].RoundedCornerRadius,
                FolderTabWidth: graphNodes[i].FolderTabWidth,
                FolderTabHeight: graphNodes[i].FolderTabHeight,
                ContentInsetLeft: insetLeft,
                ContentInsetRight: insetRight,
                ContentInsetTop: insetTop,
                ContentInsetBottom: insetBottom));
        }

        // Emit one connector (and, for a port endpoint, one LayoutPort) per emitted edge.
        foreach (var emission in emissions)
        {
            nodes.Add(new LayoutLine(
                emission.Waypoints,
                EndMarkerStyle.None,
                emission.Edge.TargetEnd,
                emission.Edge.LineStyle,
                emission.Edge.Label));

            if (emission.SourcePort != null)
            {
                var anchor = emission.Waypoints[0];
                nodes.Add(new LayoutPort(
                    anchor.X,
                    anchor.Y,
                    ResolveSide(anchor, result.Rects[emission.Source]),
                    emission.SourcePort.ExternalLabel));
            }

            if (emission.TargetPort != null)
            {
                var anchor = emission.Waypoints[^1];
                nodes.Add(new LayoutPort(
                    anchor.X,
                    anchor.Y,
                    ResolveSide(anchor, result.Rects[emission.Target]),
                    emission.TargetPort.ExternalLabel));
            }
        }

        return new LayoutTree(result.TotalWidth, result.TotalHeight, nodes);
    }

    /// <summary>
    /// Resolves an edge endpoint (a node or one of its named ports) to the placed-rect index of the
    /// node that ultimately anchors it.
    /// </summary>
    private static bool TryResolveEndpoint(
        ILayoutConnectable connectable,
        Dictionary<LayoutGraphNode, int> indexOf,
        Dictionary<LayoutGraphPort, int> portOwnerIndex,
        out int index,
        out LayoutGraphPort? port)
    {
        switch (connectable)
        {
            case LayoutGraphNode node when indexOf.TryGetValue(node, out index):
                port = null;
                return true;

            case LayoutGraphPort p when portOwnerIndex.TryGetValue(p, out index):
                port = p;
                return true;

            default:
                index = 0;
                port = null;
                return false;
        }
    }

    private static IReadOnlyList<Point2D> ResolveRoute(
        Dictionary<int, (IReadOnlyList<Point2D> Waypoints, bool Reversed)> routesByEngineIndex,
        int engineIndex,
        int source,
        int target,
        IReadOnlyList<Rect> rects)
    {
        if (routesByEngineIndex.TryGetValue(engineIndex, out var route))
        {
            if (!route.Reversed)
            {
                return route.Waypoints;
            }

            var flipped = new List<Point2D>(route.Waypoints);
            flipped.Reverse();
            return flipped;
        }

        // Self-loops and duplicate edges are dropped by the engine; fall back to a straight segment
        // between the two node centres so the connector is still drawn.
        return [Centre(rects[source]), Centre(rects[target])];
    }

    private static Point2D Centre(Rect rect) =>
        new(rect.X + (rect.Width / 2.0), rect.Y + (rect.Height / 2.0));

    /// <summary>
    /// Classifies which side of a placed node's rectangle a routed connector anchor point lies on, by
    /// comparing the anchor against the rectangle's four faces within <see cref="PortSideTolerance"/>.
    /// </summary>
    private static PortSide ResolveSide(Point2D anchor, Rect rect)
    {
        if (Math.Abs(anchor.X - rect.X) <= PortSideTolerance)
        {
            return PortSide.Left;
        }

        if (Math.Abs(anchor.X - (rect.X + rect.Width)) <= PortSideTolerance)
        {
            return PortSide.Right;
        }

        if (Math.Abs(anchor.Y - rect.Y) <= PortSideTolerance)
        {
            return PortSide.Top;
        }

        if (Math.Abs(anchor.Y - (rect.Y + rect.Height)) <= PortSideTolerance)
        {
            return PortSide.Bottom;
        }

        // Fall back to whichever face is nearest, so an anchor that (unexpectedly) does not land
        // exactly on a face still gets a deterministic, reasonable classification.
        var distLeft = Math.Abs(anchor.X - rect.X);
        var distRight = Math.Abs(anchor.X - (rect.X + rect.Width));
        var distTop = Math.Abs(anchor.Y - rect.Y);
        var distBottom = Math.Abs(anchor.Y - (rect.Y + rect.Height));

        var minSide = PortSide.Left;
        var min = distLeft;
        if (distRight < min)
        {
            minSide = PortSide.Right;
            min = distRight;
        }

        if (distTop < min)
        {
            minSide = PortSide.Top;
            min = distTop;
        }

        if (distBottom < min)
        {
            minSide = PortSide.Bottom;
        }

        return minSide;
    }

    /// <summary>
    /// Computes the four <see cref="LayoutBox.ContentInsetLeft"/>/Right/Top/Bottom margins for a
    /// node from its aggregated per-side port labels: the left/right insets are the widest same-side
    /// label's measured width plus clearance, and the top/bottom insets are a flat
    /// <see cref="CoreOptions.AssumedFontSize"/>-derived height, per ROADMAP. Zero on any side with
    /// no ports.
    /// </summary>
    private static (double Left, double Right, double Top, double Bottom) ResolveContentInsets(
        Dictionary<PortSide, List<string>>? bySide,
        ITextMeasurer textMeasurer,
        double assumedFontSize)
    {
        if (bySide == null)
        {
            return (0.0, 0.0, 0.0, 0.0);
        }

        double SideWidth(PortSide side)
        {
            if (!bySide.TryGetValue(side, out var labels) || labels.Count == 0)
            {
                return 0.0;
            }

            var widest = labels.Max(label => textMeasurer.MeasureWidth(label, assumedFontSize, bold: false, italic: false));
            return widest + PortLabelClearance;
        }

        var left = SideWidth(PortSide.Left);
        var right = SideWidth(PortSide.Right);
        var top = bySide.ContainsKey(PortSide.Top) ? assumedFontSize + PortLabelClearance : 0.0;
        var bottom = bySide.ContainsKey(PortSide.Bottom) ? assumedFontSize + PortLabelClearance : 0.0;
        return (left, right, top, bottom);
    }

    /// <summary>
    /// Resolves whether parallel edges are merged into a single rendered connector: an explicit
    /// <see cref="CoreOptions.MergeParallelEdges"/> on the graph takes precedence, then one on the
    /// options, falling back to the property default when neither declares one.
    /// </summary>
    /// <param name="graph">The graph whose explicit declaration takes precedence.</param>
    /// <param name="options">The options consulted when the graph declares no value.</param>
    /// <returns>Whether parallel edges collapse to a single connector.</returns>
    private static bool ResolveMergeParallelEdges(LayoutGraph graph, LayoutOptions options)
    {
        if (graph.TryGet(CoreOptions.MergeParallelEdges, out var fromGraph))
        {
            return fromGraph;
        }

        return options.TryGet(CoreOptions.MergeParallelEdges, out var fromOptions)
            ? fromOptions
            : CoreOptions.MergeParallelEdges.DefaultValue;
    }

    /// <summary>
    /// Resolves the <see cref="ITextMeasurer"/> used to measure port label widths: an explicit
    /// <see cref="CoreOptions.TextMeasurer"/> on the graph takes precedence, then one on the options,
    /// falling back to a shared <see cref="HeuristicTextMeasurer"/> instance when neither scope sets
    /// one.
    /// </summary>
    /// <param name="graph">The graph whose explicit declaration takes precedence.</param>
    /// <param name="options">The options consulted when the graph declares no value.</param>
    /// <returns>The text measurer to use.</returns>
    private static ITextMeasurer ResolveTextMeasurer(LayoutGraph graph, LayoutOptions options)
    {
        if (graph.TryGet(CoreOptions.TextMeasurer, out var fromGraph) && fromGraph != null)
        {
            return fromGraph;
        }

        if (options.TryGet(CoreOptions.TextMeasurer, out var fromOptions) && fromOptions != null)
        {
            return fromOptions;
        }

        return SharedHeuristicTextMeasurer;
    }

    /// <summary>
    /// Resolves the assumed font size used to measure port labels and compute the flat top/bottom
    /// content insets: an explicit <see cref="CoreOptions.AssumedFontSize"/> on the graph takes
    /// precedence, then one on the options, falling back to the property default when neither
    /// declares one.
    /// </summary>
    /// <param name="graph">The graph whose explicit declaration takes precedence.</param>
    /// <param name="options">The options consulted when the graph declares no value.</param>
    /// <returns>The assumed font size, in logical pixels.</returns>
    private static double ResolveAssumedFontSize(LayoutGraph graph, LayoutOptions options)
    {
        if (graph.TryGet(CoreOptions.AssumedFontSize, out var fromGraph))
        {
            return fromGraph;
        }

        return options.TryGet(CoreOptions.AssumedFontSize, out var fromOptions)
            ? fromOptions
            : CoreOptions.AssumedFontSize.DefaultValue;
    }

    /// <summary>
    /// Resolves the primary flow direction for this layout: an explicit
    /// <see cref="CoreOptions.Direction"/> on the graph takes precedence, then one on the options,
    /// falling back to the property default when neither declares one.
    /// </summary>
    /// <param name="graph">The graph whose explicit direction declaration takes precedence.</param>
    /// <param name="options">The options consulted when the graph declares no direction.</param>
    /// <returns>The flow direction to lay the graph out along.</returns>
    private static LayoutFlowDirection ResolveDirection(LayoutGraph graph, LayoutOptions options)
    {
        if (graph.TryGet(CoreOptions.Direction, out var fromGraph))
        {
            return fromGraph;
        }

        return options.TryGet(CoreOptions.Direction, out var fromOptions)
            ? fromOptions
            : CoreOptions.Direction.DefaultValue;
    }

    /// <summary>
    /// Resolves the minimum spacing between adjacent nodes stacked in the same layer: an explicit
    /// <see cref="CoreOptions.NodeSpacing"/> on the graph takes precedence, then one on the options,
    /// falling back to the property default when neither declares one.
    /// </summary>
    /// <param name="graph">The graph whose explicit node-spacing declaration takes precedence.</param>
    /// <param name="options">The options consulted when the graph declares no node spacing.</param>
    /// <returns>The minimum node-to-node gap, in logical pixels, to lay the graph out with.</returns>
    private static double ResolveNodeSpacing(LayoutGraph graph, LayoutOptions options)
    {
        if (graph.TryGet(CoreOptions.NodeSpacing, out var fromGraph))
        {
            return fromGraph;
        }

        return options.TryGet(CoreOptions.NodeSpacing, out var fromOptions)
            ? fromOptions
            : CoreOptions.NodeSpacing.DefaultValue;
    }

    /// <summary>
    /// Maps the public <see cref="LayoutFlowDirection"/> option to the engine's internal
    /// <see cref="LayoutDirection"/> the layered pipeline understands.
    /// </summary>
    /// <param name="direction">The public flow direction selected through the options.</param>
    /// <returns>The equivalent internal engine direction.</returns>
    private static LayoutDirection ToEngineDirection(LayoutFlowDirection direction) => direction switch
    {
        LayoutFlowDirection.Down => LayoutDirection.Down,
        LayoutFlowDirection.Left => LayoutDirection.Left,
        LayoutFlowDirection.Up => LayoutDirection.Up,
        _ => LayoutDirection.Right,
    };
}
