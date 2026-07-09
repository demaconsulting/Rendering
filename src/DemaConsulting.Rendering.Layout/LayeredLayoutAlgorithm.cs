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
/// for its ports' labels, measured via the shared <see cref="PortLabelWidthEstimator"/> (in
/// <c>Rendering.Abstractions</c>, also consumed by <c>Rendering.Svg</c>'s renderer so layout-time and
/// render-time measurements never disagree) at <see cref="CoreOptions.AssumedFontSize"/>.
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
        var assumedFontSize = ResolveAssumedFontSize(graph, options);

        // Count how many raw input edges share each (source, target) pair, so the final connector
        // emission loop can tell whether an emitted line is a genuine single edge or the survivor of
        // 2+ collapsed parallel edges. Only needed when MergeParallelEdges is true (when it is false,
        // every edge is emitted independently and always keeps its own label). Independent of node
        // sizes, so it is computed once and unaffected by the Fix 5 auto-grow re-pass below.
        var pairCounts = new Dictionary<(int Source, int Target), int>();
        if (mergeParallelEdges)
        {
            foreach (var edge in engineEdges)
            {
                var key = (edge.Source, edge.Target);
                pairCounts[key] = pairCounts.TryGetValue(key, out var existing) ? existing + 1 : 1;
            }
        }

        // Runs one full engine placement + route-resolution + port-aggregation pass for the given
        // per-node sizes. Used twice when Fix 5's minimum-size floor grows a node: once with the
        // caller-supplied sizes (Pass 1), and (only if growth is needed) once more with the grown
        // sizes (Pass 2), so the packing/spacing stage always sees final sizes and never silently
        // overlaps a sibling positioned relative to a smaller Pass-1 footprint.
        (LayerResult Result,
            Dictionary<int, (IReadOnlyList<Point2D> Waypoints, bool Reversed)> RoutesByEngineIndex,
            List<(
                LayoutGraphEdge Edge,
                int Source,
                int Target,
                LayoutGraphPort? SourcePort,
                LayoutGraphPort? TargetPort,
                IReadOnlyList<Point2D> Waypoints)> Emissions,
            Dictionary<int, Dictionary<PortSide, List<string>>> SidePorts,
            Dictionary<int, Dictionary<PortSide, (int Total, bool AnyLabeled, double MaxLabelWidth)>> FaceAnchors) RunLayerPass(
            LayerNode[] nodesForPass)
        {
            var passResult = InterconnectionLayoutEngine.Place(
                nodesForPass, engineEdges, direction, nodeSpacing, mergeParallelEdges);

            // Build an engine-edge-index -> (waypoints, reversed) lookup from the acyclic edge set the
            // engine routed. When MergeParallelEdges is true, CycleBreaker has already collapsed
            // parallel edges into a single acyclic edge per node pair, so only the surviving engine
            // edge resolves here; the emission loop below independently skips the non-surviving
            // duplicates using the same first-occurrence rule, so the two decisions always agree.
            var routes = new Dictionary<int, (IReadOnlyList<Point2D> Waypoints, bool Reversed)>();
            for (var k = 0; k < passResult.AcyclicEdges.Count; k++)
            {
                var originalEngineIndex = passResult.AcyclicOriginalIndex[k];
                var acyclic = passResult.AcyclicEdges[k];
                var reversed = acyclic.Source != engineEdges[originalEngineIndex].Source;
                routes[originalEngineIndex] = (passResult.ConnectorWaypoints[k], reversed);
            }

            // Decide which edges are emitted (honoring MergeParallelEdges) and resolve each emitted
            // edge's normalized (source -> target) waypoints, so port sides/labels can be aggregated
            // per node before the boxes (which need the resulting content insets) are built.
            var emittedPairsLocal = new HashSet<(int Source, int Target)>();
            var emissionsLocal = new List<(
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

                if (mergeParallelEdges && !emittedPairsLocal.Add((s, t)))
                {
                    continue;
                }

                var waypoints = ResolveRoute(routes, i, s, t, passResult.Rects);
                emissionsLocal.Add((edgeByEngineIndex[i], s, t, edgeSourcePorts[i], edgeTargetPorts[i], waypoints));
            }

            // Aggregate, per node and side, the port labels driving that side's content inset.
            var sidePortsLocal = new Dictionary<int, Dictionary<PortSide, List<string>>>();
            void RecordPort(int nodeIndex, Point2D anchor, string? label)
            {
                var side = ResolveSide(anchor, passResult.Rects[nodeIndex]);
                if (!sidePortsLocal.TryGetValue(nodeIndex, out var bySide))
                {
                    bySide = new Dictionary<PortSide, List<string>>();
                    sidePortsLocal[nodeIndex] = bySide;
                }

                if (!bySide.TryGetValue(side, out var labels))
                {
                    labels = [];
                    bySide[side] = labels;
                }

                labels.Add(label ?? string.Empty);
            }

            foreach (var emission in emissionsLocal)
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

            // Aggregate, per node and side, the total anchor count, whether any anchored edge
            // carries a label, and (since label width — unlike the fixed label-height formula —
            // varies with text) the widest label text sharing that face — unconditionally for every
            // emitted edge endpoint, regardless of whether it resolves to a named LayoutGraphPort
            // (unlike sidePortsLocal above, which only tracks named ports' labels for content-inset
            // sizing). Used below to derive a minimum node-size floor so that, when 2+ labels share a
            // face, PortDistributor's even spacing between anchors on that face is never smaller than
            // a label's own bounding box in the axis that face spreads anchors along: height for
            // Left/Right faces (see ConnectorLabelPlacer.EstimateLabelHeight), width for Top/Bottom
            // faces (see ConnectorLabelPlacer.EstimateLabelWidth) — so ConnectorLabelPlacer's
            // first-pass (no-nudge) placement succeeds for every label instead of visually detaching a
            // label from its own line.
            var faceAnchorsLocal = new Dictionary<int, Dictionary<PortSide, (int Total, bool AnyLabeled, double MaxLabelWidth)>>();
            void RecordAnchor(int nodeIndex, Point2D anchor, string? label)
            {
                var labeled = label != null;
                var labelWidth = label != null ? ConnectorLabelPlacer.EstimateLabelWidth(label, assumedFontSize) : 0.0;
                var side = ResolveSide(anchor, passResult.Rects[nodeIndex]);
                if (!faceAnchorsLocal.TryGetValue(nodeIndex, out var bySide))
                {
                    bySide = new Dictionary<PortSide, (int Total, bool AnyLabeled, double MaxLabelWidth)>();
                    faceAnchorsLocal[nodeIndex] = bySide;
                }

                bySide.TryGetValue(side, out var existing);
                bySide[side] = (existing.Total + 1, existing.AnyLabeled || labeled, Math.Max(existing.MaxLabelWidth, labelWidth));
            }

            foreach (var emission in emissionsLocal)
            {
                RecordAnchor(emission.Source, emission.Waypoints[0], emission.Edge.Label);
                RecordAnchor(emission.Target, emission.Waypoints[^1], emission.Edge.Label);
            }

            return (passResult, routes, emissionsLocal, sidePortsLocal, faceAnchorsLocal);
        }

        var (result, routesByEngineIndex, emissions, sidePorts, faceAnchors) = RunLayerPass(engineNodes);

        // Fix 5: grow any node whose caller-supplied size is insufficient to fit its title plus its
        // (now-known) port-driven content insets simultaneously — never shrinking below the
        // caller-supplied size. When any node needs growth, engineNodes is rebuilt with the grown
        // sizes and the engine re-runs its full placement/packing/spacing pass (Pass 2), so a grown
        // node never silently overlaps a sibling positioned relative to a smaller Pass-1 footprint.
        // The common case (no node needs growth) skips Pass 2 entirely.
        LayerNode[]? grownNodes = null;
        for (var i = 0; i < count; i++)
        {
            sidePorts.TryGetValue(i, out var bySide);
            var (insetLeft, insetRight, insetTop, insetBottom) =
                ResolveContentInsets(bySide, assumedFontSize, hasTitle: graphNodes[i].Label != null);

            var minWidth = PortLabelWidthEstimator.MeasureWidth(graphNodes[i].Label ?? string.Empty, assumedFontSize)
                + (PortLabelClearance * 2) + insetLeft + insetRight;
            var minHeight = assumedFontSize + (PortLabelClearance * 2) + insetTop + insetBottom;

            // Port-label MaxLabelWidth floor (this fix): ResolveMaxLabelWidth halves the box's own placed
            // width to bound a Left/Right port label independently of ContentInsetLeft/Right, so a box that
            // only grew enough to fit the *inset* (insetLeft + insetRight + title) can still end up with
            // ResolveMaxLabelWidth < the label width it already reserved room for via the inset, needlessly
            // squeezing a label the box has physical space for. Since insetLeft/insetRight are already
            // (widest same-side label width + PortLabelClearance), requiring width >= 2 * inset makes
            // ResolveMaxLabelWidth (width/2 - PortLabelClearance) resolve to >= the widest label's own width,
            // so the label is never squeezed once the box has grown to satisfy this floor. Zero on any side
            // with no labeled port (inset is 0 there), and composes via Math.Max with every other floor below.
            minWidth = Math.Max(minWidth, 2.0 * insetLeft);
            minWidth = Math.Max(minWidth, 2.0 * insetRight);

            // Parallel-edge label-spacing floor (Regression 1 fix, now axis-aware): when 2+ connector
            // anchors share a face and at least one carries a label, ensure PortDistributor's even
            // spacing between those anchors is never smaller than a label's own bounding box in the
            // axis that face spreads anchors along, so ConnectorLabelPlacer's first-pass (no-nudge)
            // placement succeeds for every label instead of nudging it away from its own line.
            // PortDistributor spreads Left/Right-face anchors vertically (so the floor grows
            // minHeight, compared against the fixed-per-font label height) and Top/Bottom-face
            // anchors horizontally (so the floor grows minWidth, compared against the widest labeled
            // anchor's actual label width, since — unlike height — label width varies by text). Only
            // ever raises minHeight/minWidth (never lowers either), and is a no-op for every face with
            // 0-1 anchors or no labeled anchors.
            if (faceAnchors.TryGetValue(i, out var byFace))
            {
                var labelHeight = ConnectorLabelPlacer.EstimateLabelHeight(assumedFontSize);
                foreach (var (side, faceInfo) in byFace)
                {
                    if (faceInfo.Total < 2 || !faceInfo.AnyLabeled)
                    {
                        continue;
                    }

                    if (side is PortSide.Left or PortSide.Right)
                    {
                        var requiredUsable = labelHeight * (faceInfo.Total - 1);
                        var minHeightCandidate = requiredUsable + (2.0 * LayeredLayoutMetrics.ConnectorClearance);
                        minHeight = Math.Max(minHeight, minHeightCandidate);
                    }
                    else
                    {
                        var requiredUsable = faceInfo.MaxLabelWidth * (faceInfo.Total - 1);
                        var minWidthCandidate = requiredUsable + (2.0 * LayeredLayoutMetrics.ConnectorClearance);
                        minWidth = Math.Max(minWidth, minWidthCandidate);
                    }
                }
            }

            var desiredWidth = Math.Max(engineNodes[i].Width, minWidth);
            var desiredHeight = Math.Max(engineNodes[i].Height, minHeight);

            if (desiredWidth <= engineNodes[i].Width && desiredHeight <= engineNodes[i].Height)
            {
                continue;
            }

            grownNodes ??= (LayerNode[])engineNodes.Clone();
            grownNodes[i] = grownNodes[i] with
            {
                Width = desiredWidth,
                Height = desiredHeight,
                RealWidth = desiredWidth,
                RealHeight = desiredHeight,
            };
        }

        if (grownNodes != null)
        {
            engineNodes = grownNodes;
            (result, routesByEngineIndex, emissions, sidePorts, faceAnchors) = RunLayerPass(engineNodes);
        }

        var nodes = new List<LayoutNode>(count + (emissions.Count * 2));

        // Emit one placed box per input node, preserving input order, with content insets reserved
        // for that node's port labels (zero on any side with no ports).
        for (var i = 0; i < count; i++)
        {
            var rect = result.Rects[i];
            sidePorts.TryGetValue(i, out var bySide);
            var (insetLeft, insetRight, insetTop, insetBottom) =
                ResolveContentInsets(bySide, assumedFontSize, hasTitle: graphNodes[i].Label != null);

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

        // Emit one connector (and, for a port endpoint, one LayoutPort) per emitted edge. When 2+ raw
        // edges collapsed into this pair, the midpoint label is omitted entirely (never "first
        // survivor wins") because it would misleadingly attribute only one of the merged edges'
        // meanings to the single rendered line.
        foreach (var emission in emissions)
        {
            var collapsed = mergeParallelEdges &&
                pairCounts.TryGetValue((emission.Source, emission.Target), out var pairCount) &&
                pairCount > 1;

            nodes.Add(new LayoutLine(
                emission.Waypoints,
                EndMarkerStyle.None,
                emission.Edge.TargetEnd,
                emission.Edge.LineStyle,
                collapsed ? null : emission.Edge.Label));

            if (emission.SourcePort != null)
            {
                var anchor = emission.Waypoints[0];
                nodes.Add(new LayoutPort(
                    anchor.X,
                    anchor.Y,
                    ResolveSide(anchor, result.Rects[emission.Source]),
                    emission.SourcePort.ExternalLabel,
                    MaxLabelWidth: ResolveMaxLabelWidth(result.Rects[emission.Source]),
                    SourcePort: emission.SourcePort));
            }

            if (emission.TargetPort != null)
            {
                var anchor = emission.Waypoints[^1];
                nodes.Add(new LayoutPort(
                    anchor.X,
                    anchor.Y,
                    ResolveSide(anchor, result.Rects[emission.Target]),
                    emission.TargetPort.ExternalLabel,
                    MaxLabelWidth: ResolveMaxLabelWidth(result.Rects[emission.Target]),
                    SourcePort: emission.TargetPort));
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
    /// Computes the maximum width, in logical pixels, a port label attached to <paramref name="rect"/>
    /// should be allowed to occupy before a renderer squeezes it to fit — roughly half the box's inner
    /// width, so a long label on one side cannot visually overlap a label on the opposite side.
    /// </summary>
    private static double ResolveMaxLabelWidth(Rect rect) =>
        Math.Max(0.0, (rect.Width / 2.0) - PortLabelClearance);

    /// <summary>
    /// Computes the four <see cref="LayoutBox.ContentInsetLeft"/>/Right/Top/Bottom margins for a
    /// node from its aggregated per-side port labels: the left/right insets are the widest same-side
    /// label's measured width plus clearance, and the top/bottom insets are a flat
    /// <see cref="CoreOptions.AssumedFontSize"/>-derived height, per ROADMAP. Zero on any side with
    /// no ports. When <paramref name="hasTitle"/> is <see langword="true"/>, the top/bottom insets
    /// (when driven by a top/bottom port) are additionally widened enough that a rendered box title,
    /// which starts drawing immediately after the top inset, cannot visually overlap the top port's
    /// own label — a renderer draws a port's label at a position derived from the port's glyph and
    /// font size, independent of the box's total height, so only the inset itself (not a later
    /// auto-grow of the box's overall height) can create that clearance.
    /// </summary>
    private static (double Left, double Right, double Top, double Bottom) ResolveContentInsets(
        Dictionary<PortSide, List<string>>? bySide,
        double assumedFontSize,
        bool hasTitle = false)
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

            var widest = labels.Max(label => PortLabelWidthEstimator.MeasureWidth(label, assumedFontSize));
            return widest + PortLabelClearance;
        }

        var left = SideWidth(PortSide.Left);
        var right = SideWidth(PortSide.Right);
        var top = bySide.ContainsKey(PortSide.Top) ? assumedFontSize + PortLabelClearance : 0.0;
        var bottom = bySide.ContainsKey(PortSide.Bottom) ? assumedFontSize + PortLabelClearance : 0.0;

        if (hasTitle)
        {
            // A renderer draws a top/bottom port's label roughly (PortLabelClearance + 1.5 x
            // assumedFontSize) away from the box edge, regardless of the inset value; the box title
            // starts immediately after the inset, so the inset itself must be at least that deep (with
            // margin) for a box that has both a title and a top/bottom port to avoid overlap.
            var titleClearance = (2.0 * assumedFontSize) + (2.0 * PortLabelClearance);
            if (top > 0.0)
            {
                top = Math.Max(top, titleClearance);
            }

            if (bottom > 0.0)
            {
                bottom = Math.Max(bottom, titleClearance);
            }
        }

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
