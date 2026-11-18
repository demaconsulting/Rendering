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
/// This is the reference <see cref="LayoutAlgorithmBase"/> implementation; it wraps the reusable layered
/// pipeline under <c>Engine/Layered/</c>. It honors <see cref="CoreOptions.Direction"/> so the layers
/// progress right, left, down, or up (a downward flow lays action-flow and state-transition diagrams
/// out top-to-bottom), <see cref="CoreOptions.NodeSpacing"/> so the minimum gap between nodes stacked
/// in the same layer can be widened or narrowed from the engine's default, <see cref="CoreOptions.LayerSpacing"/>
/// so the corridor between adjacent layers is at least that wide (a floor: the connector routing and end
/// labels may still need more), and
/// <see cref="CoreOptions.MergeParallelEdges"/> so parallel input edges either collapse to one
/// rendered connector (the default) or each keep their own independently-routed line. An edge whose
/// <see cref="LayoutGraphEdge.Source"/> or <see cref="LayoutGraphEdge.Target"/> is a
/// <see cref="LayoutGraphPort"/> emits a <see cref="LayoutPort"/> anchored at the routed connector
/// endpoint, and each node's <see cref="LayoutBox.ContentInsetLeft"/>/Right/Top/Bottom reserve space
/// for its ports' labels, measured via the shared <see cref="PortLabelWidthEstimator"/> (in
/// <c>Rendering.Abstractions</c>, also consumed by <c>Rendering.Svg</c>'s renderer so layout-time and
/// render-time measurements never disagree) at <see cref="CoreOptions.AssumedFontSize"/>. An edge
/// marked layout-only by the hierarchical engine (an identity-keyed marker, see
/// <c>HierarchicalLayoutAlgorithm.MarkLayoutOnly</c>) takes part in layering, ordering,
/// and spacing exactly like a real edge but emits no connector, port, or anchor reservation, and never
/// displaces a real parallel edge as the retained representative when parallel edges are merged.
/// </summary>
public sealed class LayeredLayoutAlgorithm : LayoutAlgorithmBase
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

    /// <summary>
    /// Clearance, in logical pixels, reserved beside an end label on the straight end run on top of the
    /// label's own extent: the offset <see cref="ConnectorLabelPlacer.PlaceEndLabels"/> keeps from the end
    /// point or marker, plus a little slack so rounding in the width estimate never pushes a label into
    /// the bend.
    /// </summary>
    private const double EndLabelClearance = 6.0;

    /// <inheritdoc/>
    public override string Id => AlgorithmId;

    /// <inheritdoc/>
    protected internal override LayoutTree ApplyCore(LayoutGraph graph, LayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(options);

        var graphNodes = graph.Nodes;
        var count = graphNodes.Count;

        // Map each input node to a positional index the layered engine works in terms of, and each
        // named port to the index of the node that owns it (a port has no independent box; it always
        // anchors to its owning node's placed rectangle).
        var assumedFontSize = ResolveAssumedFontSize(graph, options);

        var indexOf = new Dictionary<LayoutGraphNode, int>(count);
        var portOwnerIndex = new Dictionary<LayoutGraphPort, int>();
        var engineNodes = new LayerNode[count];
        var orderRanks = options.Get(HierarchicalLayoutAlgorithm.OrderRanks);
        for (var i = 0; i < count; i++)
        {
            var node = graphNodes[i];
            indexOf[node] = i;
            var orderRank = orderRanks != null && orderRanks.TryGetValue(node, out var ranked) ? ranked : double.NaN;
            // TitleReserveTop excludes a band from PortDistributor's left/right-face port placement
            // (see PortDistributor.TitleReserveFor) to keep a *named* port's rendered label clear of
            // the title. A node with no declared LayoutGraphPorts only has plain, unlabeled edge
            // anchors on its faces — nothing rendered there could ever collide with the title text —
            // so the reserve must stay 0.0 for it; otherwise every titled node's single/plain
            // connector anchors would be shifted off-center for no real collision risk (e.g. a simple
            // A -> B -> C pipeline with no named ports at all).
            engineNodes[i] = new LayerNode(
                node.Width,
                node.Height,
                node.Shape,
                node.RoundedCornerRadius,
                node.FolderTabWidth,
                node.FolderTabHeight,
                node.Label,
                RealWidth: node.Width,
                RealHeight: node.Height,
                TitleReserveTop: node.HasPorts
                    ? LayeredLayoutMetrics.ResolveTitleReserveTop(
                        HasBodyTitle(node),
                        node.Keyword != null,
                        assumedFontSize)
                    : 0.0,
                OrderRank: orderRank);

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
        var verticalFlow = ResolveDirection(graph, options) is LayoutFlowDirection.Down or LayoutFlowDirection.Up;
        var engineEdges = new List<LayerEdge>(graph.Edges.Count);
        var edgeSourcePorts = new List<LayoutGraphPort?>(graph.Edges.Count);
        var edgeTargetPorts = new List<LayoutGraphPort?>(graph.Edges.Count);
        var edgeByEngineIndex = new List<LayoutGraphEdge>(graph.Edges.Count);
        // Real edges are added first and layout-only edges last (relative order preserved within each
        // group): the cycle breaker keeps the FIRST edge of each parallel group as the retained
        // representative, so a layout-only edge must never precede a real edge on the same node pair or
        // it would win the de-duplication and hide the real connector when MergeParallelEdges is true.
        var layoutOnlyEdges = new List<(LayerEdge Edge, LayoutGraphPort? SourcePort, LayoutGraphPort? TargetPort, LayoutGraphEdge Source)>();
        foreach (var edge in graph.Edges)
        {
            if (!TryResolveEndpoint(edge.Source, indexOf, portOwnerIndex, out var s, out var sourcePort) ||
                !TryResolveEndpoint(edge.Target, indexOf, portOwnerIndex, out var t, out var targetPort))
            {
                continue;
            }

            if (HierarchicalLayoutAlgorithm.IsLayoutOnlyEdge(edge))
            {
                layoutOnlyEdges.Add((new LayerEdge(s, t), sourcePort, targetPort, edge));
                continue;
            }

            engineEdges.Add(new LayerEdge(
                s,
                t,
                EndLabelAlong(edge.SourceEndLabel, verticalFlow, assumedFontSize, EndMarkerStyle.None, sourcePort)
                    + OutwardPortLabelAlong(sourcePort, verticalFlow, assumedFontSize, EndMarkerStyle.None),
                EndLabelAlong(edge.TargetEndLabel, verticalFlow, assumedFontSize, edge.TargetEnd, targetPort)
                    + OutwardPortLabelAlong(targetPort, verticalFlow, assumedFontSize, EndMarkerStyle.None),
                ResolveEndApproach(edge.TargetEnd)));
            edgeSourcePorts.Add(sourcePort);
            edgeTargetPorts.Add(targetPort);
            edgeByEngineIndex.Add(edge);
        }

        foreach (var (layoutOnlyEdge, sourcePort, targetPort, original) in layoutOnlyEdges)
        {
            engineEdges.Add(layoutOnlyEdge);
            edgeSourcePorts.Add(sourcePort);
            edgeTargetPorts.Add(targetPort);
            edgeByEngineIndex.Add(original);
        }

        var direction = ToEngineDirection(ResolveDirection(graph, options));
        var nodeSpacing = ResolveNodeSpacing(graph, options);
        var layerSpacing = ResolveLayerSpacing(graph, options);
        var mergeParallelEdges = ResolveMergeParallelEdges(graph, options);
        var backEdgeEntryApproach = ResolveBackEdgeEntryApproach(edgeByEngineIndex);

        // Count how many raw input edges share each node pair, so the final connector emission loop
        // can tell whether an emitted line is a genuine single edge or the survivor of 2+ collapsed
        // parallel edges. Keyed by the *undirected* pair (not the raw declared direction) because
        // CycleBreaker's own merge/de-duplication normalizes direction (reversing cycle-causing back edges)
        // before deciding which edges collapse together, so two edges declared as A->B and B->A can
        // still collapse into a single survivor; grouping here must match that or this count silently
        // misses direction-mixed parallel groups. Only needed when MergeParallelEdges is true (when it
        // is false, every edge is emitted independently and always keeps its own label). Independent
        // of node sizes, so it is computed once and unaffected by the Fix 5 auto-grow re-pass below.
        var pairCounts = new Dictionary<(int Low, int High), int>();
        if (mergeParallelEdges)
        {
            for (var i = 0; i < engineEdges.Count; i++)
            {
                // A layout-only edge never renders, so it never counts as a collapsed parallel edge.
                if (HierarchicalLayoutAlgorithm.IsLayoutOnlyEdge(edgeByEngineIndex[i]))
                {
                    continue;
                }

                var edge = engineEdges[i];
                var key = (Math.Min(edge.Source, edge.Target), Math.Max(edge.Source, edge.Target));
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
            Dictionary<int, Dictionary<PortSide, (int Total, bool AnyLabeled, double MaxLabelWidth)>> FaceAnchors,
            Dictionary<(int Node, PortSide Side), double> EndLabelPitch) RunLayerPass(
            LayerNode[] nodesForPass)
        {
            var passResult = InterconnectionLayoutEngine.Place(
                nodesForPass, engineEdges, direction, nodeSpacing, mergeParallelEdges, backEdgeEntryApproach, layerSpacing: layerSpacing);

            // Build an engine-edge-index -> (waypoints, reversed) lookup from the acyclic edge set the
            // engine routed. When MergeParallelEdges is true, CycleBreaker has already collapsed
            // parallel edges (including ones declared in opposite directions between the same node
            // pair, which it normalizes via its own back-edge reversal before comparing) into a single
            // acyclic edge per node pair, so only the surviving engine edge resolves here. The
            // emission loop below treats "no entry in this dictionary" as the ground truth for "this
            // edge was collapsed away", rather than re-deriving its own (necessarily direction-blind)
            // duplicate rule, so the two stages can never disagree about which edges survived.
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

                // A layout-only edge (see HierarchicalLayoutAlgorithm.IsLayoutOnlyEdge) stays in the
                // engine edge list so it still drives layering, ordering, and gap sizing, but it is
                // never emitted: no connector, port, or anchor reservation is produced for it.
                if (HierarchicalLayoutAlgorithm.IsLayoutOnlyEdge(edgeByEngineIndex[i]))
                {
                    continue;
                }

                // A self-loop (s == t) is always dropped by CycleBreaker regardless of
                // MergeParallelEdges, yet must still be emitted (via ResolveRoute's centre-to-centre
                // fallback below) so the connector remains visible. A non-self-loop edge with no
                // surviving route was genuinely collapsed into another parallel edge — possibly one
                // declared in the opposite direction — by CycleBreaker's merge/de-duplication, so it is skipped
                // here rather than falling back to a spurious straight line through both node centers.
                if (mergeParallelEdges && s != t && !routes.ContainsKey(i))
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
                // An outward label is drawn outside the box, so it reserves no content inset inside it.
                if (emission.SourcePort != null)
                {
                    RecordPort(emission.Source, emission.Waypoints[0], IsOutward(emission.SourcePort) ? null : emission.SourcePort.ExternalLabel);
                }

                if (emission.TargetPort != null)
                {
                    RecordPort(emission.Target, emission.Waypoints[^1], IsOutward(emission.TargetPort) ? null : emission.TargetPort.ExternalLabel);
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

            // Aggregate, per node and side, the widest end-label stack any anchor on that face carries —
            // the pitch a face with 2+ anchors must keep between adjacent connectors so one connector's
            // labels never sit on its neighbor.
            var endLabelPitchLocal = new Dictionary<(int Node, PortSide Side), double>();
            void RecordEndLabelPitch(int nodeIndex, Point2D anchor, LineEndLabel? label, LayoutGraphPort? port)
            {
                var pitch = EndLabelPitchOf(label, assumedFontSize, ResolveSide(anchor, passResult.Rects[nodeIndex]))
                    + OutwardPortLabelPitch(port, assumedFontSize, ResolveSide(anchor, passResult.Rects[nodeIndex]));
                if (pitch <= 0.0)
                {
                    return;
                }

                var key = (nodeIndex, ResolveSide(anchor, passResult.Rects[nodeIndex]));
                endLabelPitchLocal[key] = endLabelPitchLocal.TryGetValue(key, out var existing) ? Math.Max(existing, pitch) : pitch;
            }

            // A diamond's connector ends are spread over the usable extent of the face (the face less
            // ConnectorClearance at each end), so adjacent end markers must also be a marker's painted
            // width plus a gap apart; record that as the face pitch the same way end-label pitches are.
            // Only a diamond node does this — rectangles spread over the whole face and keep their output.
            void RecordMarkerPitch(int nodeIndex, Point2D anchor, EndMarkerStyle style)
            {
                if (graphNodes[nodeIndex].Shape != BoxShape.Diamond)
                {
                    return;
                }

                var pitch = LayeredLayoutMetrics.MarkerFacePitch(style);
                if (pitch <= 0.0)
                {
                    return;
                }

                var key = (nodeIndex, ResolveSide(anchor, passResult.Rects[nodeIndex]));
                endLabelPitchLocal[key] = endLabelPitchLocal.TryGetValue(key, out var existing) ? Math.Max(existing, pitch) : pitch;
            }

            foreach (var emission in emissionsLocal)
            {
                RecordAnchor(emission.Source, emission.Waypoints[0], emission.Edge.Label);
                RecordAnchor(emission.Target, emission.Waypoints[^1], emission.Edge.Label);
                RecordEndLabelPitch(emission.Source, emission.Waypoints[0], emission.Edge.SourceEndLabel, emission.SourcePort);
                RecordEndLabelPitch(emission.Target, emission.Waypoints[^1], emission.Edge.TargetEndLabel, emission.TargetPort);
                RecordMarkerPitch(emission.Target, emission.Waypoints[^1], emission.Edge.TargetEnd);
            }

            return (passResult, routes, emissionsLocal, sidePortsLocal, faceAnchorsLocal, endLabelPitchLocal);
        }

        var (result, routesByEngineIndex, emissions, sidePorts, faceAnchors, endLabelPitch) = RunLayerPass(engineNodes);

        // PortDistributor spreads a shaped node's ports over the face less ConnectorClearance at each end,
        // so a diamond face with 2+ anchors needs that trim on top of Total * pitch to honor its pitch
        // (a rectangle spreads over the whole face and needs none).
        double ShapedFaceTrim(int nodeIndex, int total) =>
            total >= 2 && graphNodes[nodeIndex].Shape == BoxShape.Diamond
                ? 2.0 * LayeredLayoutMetrics.ConnectorClearance
                : 0.0;
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
                ResolveContentInsets(bySide, assumedFontSize, hasTitle: HasBodyTitleContent(graphNodes[i]));

            // PortDistributor excludes engineNodes[i].TitleReserveTop from left/right-face port
            // placement for every titled node that actually has a left/right anchor (see the
            // layered pipeline's title-vs-side-port reservation), regardless of whether that anchor
            // resolves to a named LayoutGraphPort. insetTop above only widens for *named* ports
            // (sidePorts only tracks those), so an anonymous edge endpoint on a titled node's
            // left/right face would otherwise have its usable band shrunk by the exclusion without
            // minHeight ever growing to compensate; take whichever of the two is larger so every
            // downstream minHeight floor below reflects the band PortDistributor will actually
            // exclude. Gated on faceAnchors (unconditional anchor counts, unlike sidePorts) actually
            // having a Left/Right entry, since TitleReserveTop is computed per titled node
            // regardless of whether it has any side port at all — applying it unconditionally would
            // over-reserve (and needlessly grow) every titled leaf, side ports or not.
            var hasLeftOrRightAnchor = faceAnchors.TryGetValue(i, out var byFaceForReserve)
                && (byFaceForReserve.ContainsKey(PortSide.Left) || byFaceForReserve.ContainsKey(PortSide.Right));
            var effectiveTopReserve = Math.Max(insetTop, hasLeftOrRightAnchor ? engineNodes[i].TitleReserveTop : 0.0);

            var minWidth = PortLabelWidthEstimator.MeasureWidth(graphNodes[i].Label ?? string.Empty, assumedFontSize)
                + (PortLabelClearance * 2) + insetLeft + insetRight;
            var minHeight = assumedFontSize + (PortLabelClearance * 2) + effectiveTopReserve + insetBottom;

            // SvgRenderer.EmitPortLabel draws a Left/Right port's ExternalLabel at
            // (port.CentreY + FontSizeBody) — a full line's worth of clearance below the port glyph's
            // own centre (matching the Top/Bottom convention's own CentreY + offset + FontSizeBody
            // formula) — with dominant-baseline="middle", so the glyph itself extends roughly another
            // half line-height of descent below that anchor y. The label's actual bottom edge therefore
            // sits at CentreY + 1.5 * FontSizeBody, not the symmetric top/bottom clearance the row floor
            // above assumes. Without compensating for that downward shift, the clearance above sits
            // unused above the port row while the shifted-down label text (plus its own glyph descent)
            // can run past the box's bottom edge — exactly what a single labeled port on a titled box's
            // face produces (the title reserve leaves only one row for the port, so there is no slack
            // elsewhere to absorb the shift). A flat addition of assumedFontSize is not enough on its
            // own, though: PortDistributor.DistributePorts centers the port within its face's available
            // band (nodeTop + nodeHeight / 2 for the single-anchor case), so only HALF of any height
            // added here ends up below the port's centre — the other half is spent growing the band
            // upward, where the label never reaches. Add 2 * assumedFontSize (not 1x) as extra bottom
            // margin so the required assumedFontSize of clearance actually materializes below the
            // label's bottom edge, gated on the face actually carrying a named, labeled port: bySide
            // (from sidePorts, already resolved above) is the same dictionary ResolveContentInsets uses
            // and only records named LayoutGraphPort labels (unlike faceAnchors.AnyLabeled, which tracks
            // the *edge's own* mid-line label instead of a port's ExternalLabel and would miss this case
            // entirely) — an anonymous edge endpoint draws no label near the box face and needs no extra
            // room.
            var hasLabeledLeftOrRightAnchor = bySide != null
                && ((bySide.TryGetValue(PortSide.Left, out var leftLabels) && leftLabels.Exists(l => !string.IsNullOrEmpty(l)))
                    || (bySide.TryGetValue(PortSide.Right, out var rightLabels) && rightLabels.Exists(l => !string.IsNullOrEmpty(l))));
            if (hasLabeledLeftOrRightAnchor)
            {
                minHeight += assumedFontSize * 2.0;
            }

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
            //
            // "Labeled" here must include a named port's ExternalLabel, not just an edge's own
            // mid-line Label: faceInfo.AnyLabeled is populated solely from emission.Edge.Label (see
            // RecordAnchor), so a face whose anchors carry only port ExternalLabels (no edge labels
            // at all, e.g. two named ports on one face with plain edges) would otherwise never
            // trigger this floor and could render its stacked port labels crowded/overlapping — the
            // exact same faceAnchors-vs-sidePorts distinction as hasLabeledLeftOrRightAnchor above.
            // bySide (from sidePorts) is reused here for the same reason.
            //
            // Any face with 2+ anchors — labeled or not — still needs enough physical room to keep
            // adjacent port glyphs and their outgoing connector routing from visually crowding each
            // other; a box is allowed to say "I have enough ports on me that I need to spread them
            // out a bit" even when none of those ports carry any text. Only the label-driven terms
            // below (labelHeight/labelWidth scaling, and the downward-shift compensation) are gated
            // on actual labels being present, since they have no meaning otherwise; the baseline
            // per-anchor connector-clearance floor applies unconditionally whenever Total >= 2.
            if (faceAnchors.TryGetValue(i, out var byFace))
            {
                var labelHeight = ConnectorLabelPlacer.EstimateLabelHeight(assumedFontSize);
                foreach (var (side, faceInfo) in byFace)
                {
                    List<string>? sideLabels = null;
                    bySide?.TryGetValue(side, out sideLabels);
                    var sidePortLabeled = sideLabels != null && sideLabels.Exists(l => !string.IsNullOrEmpty(l));
                    var anyLabeled = faceInfo.AnyLabeled || sidePortLabeled;

                    if (faceInfo.Total < 2)
                    {
                        continue;
                    }

                    if (side is PortSide.Left or PortSide.Right)
                    {
                        // PortDistributor now centers each of faceInfo.Total ports within its own
                        // equal-width slice of the face (see PortDistributor.DistributePorts), so
                        // adjacent-port spacing equals nodeHeight / Total (not / (Total - 1) as the
                        // old endpoint-inclusive linear formula gave) — require that per-slice height
                        // to be at least the label's own line height, with no separate outer-edge
                        // buffer term needed since the outermost slice's own half-height already gives
                        // proportional corner clearance. When no label is present, fall back to a
                        // per-anchor connector-clearance floor so unlabeled ports still get enough
                        // absolute room to avoid crowding (scaled by Total, unlike the historical flat
                        // 2 * ConnectorClearance floor, which never bound in practice for the labeled
                        // case anyway and was too weak on its own for several unlabeled ports).
                        var labelBasedHeight = anyLabeled ? labelHeight * faceInfo.Total : 0.0;
                        if (endLabelPitch.TryGetValue((i, side), out var endPitchHeight))
                        {
                            // End labels (role name above, multiplicity below) stack beside each
                            // connector, so every anchor needs its full stack of rows.
                            labelBasedHeight = Math.Max(labelBasedHeight, (endPitchHeight * faceInfo.Total) + ShapedFaceTrim(i, faceInfo.Total));
                        }

                        var clearanceBasedHeight = 2.0 * LayeredLayoutMetrics.ConnectorClearance * faceInfo.Total;
                        var minHeightCandidate = Math.Max(labelBasedHeight, clearanceBasedHeight)
                            + effectiveTopReserve + insetBottom;

                        // Same downward-label-shift compensation as the single-port minHeight floor
                        // above (SvgRenderer.EmitPortLabel draws a Left/Right port's ExternalLabel at
                        // CentreY + FontSizeBody with dominant-baseline="middle", so the label's actual
                        // bottom edge sits at CentreY + 1.5 * FontSizeBody, not centered on the port row)
                        // — without it, this multi-port candidate can win the Math.Max below yet still
                        // leave no slack for the bottom-most labeled port's downward-shifted text, which
                        // then runs past the box's bottom edge exactly as the single-port case would
                        // without this term. PortDistributor.DistributePorts centers each of
                        // faceInfo.Total ports within its own equal-height slice of the added band, so a
                        // flat addition here is only fully absorbed by the bottom-most slice's own height
                        // when there is exactly one slice (Total == 1); for Total >= 2 a flat addend is
                        // instead divided across every slice's height (each slice gains only addend /
                        // Total), silently eroding the intended margin as Total grows. Scale by
                        // faceInfo.Total so every slice's height still gains the full compensation term
                        // the single-port case relies on, regardless of how many ports share the face.
                        // As with the single-port floor, each slice only receives HALF of any height
                        // added here (the port is centered within its own slice, so the other half grows
                        // the slice upward, where the label never reaches) — multiply by 2.0 so the
                        // required assumedFontSize of clearance actually materializes below every
                        // bottom-most labeled port's label.
                        if (sidePortLabeled)
                        {
                            minHeightCandidate += assumedFontSize * 2.0 * faceInfo.Total;
                        }

                        minHeight = Math.Max(minHeight, minHeightCandidate);
                    }
                    else
                    {
                        var sidePortMaxLabelWidth = 0.0;
                        if (sidePortLabeled)
                        {
                            foreach (var label in sideLabels!)
                            {
                                if (string.IsNullOrEmpty(label))
                                {
                                    continue;
                                }

                                sidePortMaxLabelWidth = Math.Max(
                                    sidePortMaxLabelWidth,
                                    ConnectorLabelPlacer.EstimateLabelWidth(label, assumedFontSize));
                            }
                        }

                        // Same equal-area-slice reasoning as the Left/Right branch above, but along
                        // the width axis: each port's slice must be at least as wide as the widest
                        // same-side label so a Top/Bottom label (rendered centered on its port) never
                        // overlaps its neighbor's label or overflows past the box's own edge. When no
                        // label is present, fall back to a per-anchor connector-clearance floor (scaled
                        // by Total, matching the Left/Right branch's clearanceBasedHeight) so unlabeled
                        // Top/Bottom ports still get enough absolute room to avoid crowding — a flat,
                        // unscaled floor here would reproduce the same bunching bug the Left/Right
                        // branch was fixed for, just rotated 90 degrees.
                        var maxLabelWidth = Math.Max(faceInfo.MaxLabelWidth, sidePortMaxLabelWidth);
                        var labelBasedWidth = anyLabeled ? maxLabelWidth * faceInfo.Total : 0.0;
                        if (endLabelPitch.TryGetValue((i, side), out var endPitchWidth))
                        {
                            // End labels sit left and right of each vertical connector, so every anchor
                            // needs both label widths side by side.
                            labelBasedWidth = Math.Max(labelBasedWidth, (endPitchWidth * faceInfo.Total) + ShapedFaceTrim(i, faceInfo.Total));
                        }

                        var clearanceBasedWidth = 2.0 * LayeredLayoutMetrics.ConnectorClearance * faceInfo.Total;
                        var minWidthCandidate = Math.Max(labelBasedWidth, clearanceBasedWidth);
                        minWidth = Math.Max(minWidth, minWidthCandidate);
                    }
                }
            }

            var desiredWidth = Math.Max(engineNodes[i].Width, minWidth);
            var desiredHeight = Math.Max(engineNodes[i].Height, minHeight);

            // The widest label stack on any face of this node is the spacing PortAligner must keep between
            // its ports, so aligning a port with the far end of its edge never packs two connectors'
            // labels together. Zero (no change, no second pass) when the node has no end or outward labels.
            var minPitch = 0.0;
            foreach (var ((pitchNode, _), pitch) in endLabelPitch)
            {
                if (pitchNode == i)
                {
                    minPitch = Math.Max(minPitch, pitch);
                }
            }

            var needsPitch = minPitch > engineNodes[i].MinPortPitch + 1e-9;
            if (!needsPitch && desiredWidth <= engineNodes[i].Width && desiredHeight <= engineNodes[i].Height)
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
                MinPortPitch = Math.Max(minPitch, grownNodes[i].MinPortPitch),
            };
        }

        if (grownNodes != null)
        {
            engineNodes = grownNodes;
            (result, routesByEngineIndex, emissions, sidePorts, faceAnchors, endLabelPitch) = RunLayerPass(engineNodes);
        }

        var nodes = new List<LayoutNode>(count + (emissions.Count * 2));

        // Emit one placed box per input node, preserving input order, with content insets reserved
        // for that node's port labels (zero on any side with no ports).
        for (var i = 0; i < count; i++)
        {
            var rect = result.Rects[i];
            sidePorts.TryGetValue(i, out var bySide);
            var (insetLeft, insetRight, insetTop, insetBottom) =
                ResolveContentInsets(bySide, assumedFontSize, hasTitle: HasBodyTitleContent(graphNodes[i]));

            // A box whose ContentInsetTop was reserved specifically to keep a *left/right* port's
            // label out of the title's own row (see ResolveContentInsets' left/right fallback branch)
            // uses that inset as the title's own top-pinned band, excluded from port placement by
            // PortDistributor's title-vs-side-port reservation (see
            // LayeredLayoutMetrics.ResolveTitleReserveTop). BoxMetrics.TitleCursorTop's CenterTitle
            // branch instead treats ContentInsetTop as space reserved by something else *above* the
            // title, then centers the title in the box's *remaining* height — exactly the band where
            // those left/right ports live. Centering would therefore pull the title back down into
            // the ports it was just excluded from. A *top*-port box's ContentInsetTop means the
            // opposite (space reserved above the title, by the top port itself), where centering
            // below it is correct — see ResolveContentInsets, where the two reservations are mutually
            // exclusive. Keep the title top-pinned (CenterTitle: false) only for the left/right case,
            // and reserve centering for every other leaf (no ports, or top/bottom ports only).
            var hasNamedLeftOrRightPort = bySide != null
                && !bySide.ContainsKey(PortSide.Top)
                && (bySide.ContainsKey(PortSide.Left) || bySide.ContainsKey(PortSide.Right));

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
                ContentInsetBottom: insetBottom,
                CenterTitle: graphNodes[i].Compartments.Count == 0 && !hasNamedLeftOrRightPort,
                Appearance: graphNodes[i].Appearance));
        }

        // Emit one connector (and, for a port endpoint, one LayoutPort) per emitted edge. When 2+ raw
        // edges collapsed into this pair, the midpoint label is omitted entirely (never "first
        // survivor wins") because it would misleadingly attribute only one of the merged edges'
        // meanings to the single rendered line.
        foreach (var emission in emissions)
        {
            var collapsed = mergeParallelEdges &&
                pairCounts.TryGetValue(
                    (Math.Min(emission.Source, emission.Target), Math.Max(emission.Source, emission.Target)),
                    out var pairCount) &&
                pairCount > 1;

            nodes.Add(new LayoutLine(
                emission.Waypoints,
                EndMarkerStyle.None,
                emission.Edge.TargetEnd,
                emission.Edge.LineStyle,
                collapsed ? null : emission.Edge.Label,
                collapsed ? null : emission.Edge.SourceEndLabel,
                collapsed ? null : emission.Edge.TargetEndLabel));

            if (emission.SourcePort != null)
            {
                var anchor = emission.Waypoints[0];
                nodes.Add(new LayoutPort(
                    anchor.X,
                    anchor.Y,
                    ResolveSide(anchor, result.Rects[emission.Source]),
                    emission.SourcePort.ExternalLabel,
                    MaxLabelWidth: IsOutward(emission.SourcePort) ? double.PositiveInfinity : ResolveMaxLabelWidth(result.Rects[emission.Source]),
                    SourcePort: emission.SourcePort,
                    Appearance: emission.SourcePort.Appearance));
            }

            if (emission.TargetPort != null)
            {
                var anchor = emission.Waypoints[^1];
                nodes.Add(new LayoutPort(
                    anchor.X,
                    anchor.Y,
                    ResolveSide(anchor, result.Rects[emission.Target]),
                    emission.TargetPort.ExternalLabel,
                    MaxLabelWidth: IsOutward(emission.TargetPort) ? double.PositiveInfinity : ResolveMaxLabelWidth(result.Rects[emission.Target]),
                    SourcePort: emission.TargetPort,
                    Appearance: emission.TargetPort.Appearance));
            }
        }

        // The canvas is the drawn content plus one Padding on every side: the final canvas fit measures the
        // boxes, connectors, resolved labels and ports, so the reflection of a Left or Up flow (which leaves
        // the content one padding off-center), a label reaching into the padding band, and a connector
        // detour all end with the same margin.
        return CanvasFitter.Fit(new LayoutTree(result.TotalWidth, result.TotalHeight, nodes), assumedFontSize, LayeredLayoutMetrics.Padding);
    }

    /// <summary>
    /// Resolves an edge endpoint (a node or one of its named ports) to the placed-rect index of the
    /// node that ultimately anchors it.
    /// </summary>
    /// <summary>
    /// Returns the space, in logical pixels along the flow axis, that the end labels of one connector end
    /// need on the straight run beside the end (label extent plus the clearance the placer keeps), plus
    /// the end marker for a target end; zero when the end carries no label. Passed to the layered
    /// pipeline through <see cref="LayerEdge.SourceLabelAlong"/>/<see cref="LayerEdge.TargetLabelAlong"/>.
    /// </summary>
    /// <param name="label">The end labels of the connector end, or <see langword="null"/>.</param>
    /// <param name="verticalFlow">Whether the flow runs down or up (labels then extend by their height along it).</param>
    /// <param name="fontSize">Body font size, in logical pixels.</param>
    /// <param name="end">The end marker drawn at this end, which the labels must clear.</param>
    /// <param name="port">
    /// The graph port the end attaches to, or <see langword="null"/>; a marker end on a port is drawn
    /// retracted by the port glyph's half size, which the labels must clear as well.
    /// </param>
    /// <returns>The space to reserve, in logical pixels.</returns>
    private static double EndLabelAlong(LineEndLabel? label, bool verticalFlow, double fontSize, EndMarkerStyle end, LayoutGraphPort? port = null)
    {
        var extent = 0.0;
        foreach (var text in new[] { label?.Primary, label?.Secondary })
        {
            if (text is null || text.Length == 0)
            {
                continue;
            }

            extent = Math.Max(
                extent,
                verticalFlow
                    ? ConnectorLabelPlacer.EstimateLabelHeight(fontSize)
                    : ConnectorLabelPlacer.EstimateEndLabelWidth(text, fontSize));
        }

        var retraction = port is not null && end != EndMarkerStyle.None
            ? NotationMetrics.PortGlyphHalfSizeFor(port.Appearance)
            : 0.0;
        return extent <= 0.0
            ? 0.0
            : extent + EndLabelClearance + retraction + (NotationMetrics.AlongLineLength(end) * NotationMetrics.MarkerScale(Themes.Light));
    }

    /// <summary>
    /// Returns the pitch one connector's end labels need across the face they sit on: the stacked
    /// heights of its slots on a left or right face (labels sit above and below the horizontal run), or
    /// the side-by-side widths on a top or bottom face (labels sit left and right of the vertical run).
    /// </summary>
    /// <param name="label">The end labels of the connector end, or <see langword="null"/>.</param>
    /// <param name="fontSize">Body font size, in logical pixels.</param>
    /// <param name="side">The box face the connector end sits on.</param>
    /// <returns>The pitch, in logical pixels; zero when the end carries no label.</returns>
    private static double EndLabelPitchOf(LineEndLabel? label, double fontSize, PortSide side)
    {
        var pitch = 0.0;
        foreach (var text in new[] { label?.Primary, label?.Secondary })
        {
            if (text is null || text.Length == 0)
            {
                continue;
            }

            pitch += side is PortSide.Left or PortSide.Right
                ? ConnectorLabelPlacer.EstimateLabelHeight(fontSize)
                : ConnectorLabelPlacer.EstimateEndLabelWidth(text, fontSize);
        }

        // Slack for the clearance the placer keeps between a label and the line it sits beside (on both
        // sides of the line), so adjacent connectors' labels never touch.
        return pitch > 0.0 ? pitch + EndLabelClearance : 0.0;
    }

    /// <summary>Returns whether a graph port's external label is drawn outward (outside its box).</summary>
    /// <param name="port">The graph port.</param>
    /// <returns><see langword="true"/> when the label is outward and present.</returns>
    private static bool IsOutward(LayoutGraphPort port) =>
        port.Appearance?.LabelPlacement == PortLabelPlacement.Outward && !string.IsNullOrEmpty(port.ExternalLabel);

    /// <summary>
    /// Returns the space, in logical pixels along the flow axis, that a port's outward label needs beside
    /// the connector at that end (the glyph, the label padding, and the label's own extent, plus the
    /// placer's clearance); zero for a port without an outward label. Added to the end-label reservation so
    /// the label lies on the straight run before the first bend.
    /// </summary>
    /// <param name="port">The graph port at the connector end, or <see langword="null"/>.</param>
    /// <param name="verticalFlow">Whether the flow runs down or up.</param>
    /// <param name="fontSize">Body font size, in logical pixels.</param>
    /// <param name="end">Unused marker style kept for symmetry with <see cref="EndLabelAlong"/>.</param>
    /// <returns>The space to reserve, in logical pixels.</returns>
    private static double OutwardPortLabelAlong(LayoutGraphPort? port, bool verticalFlow, double fontSize, EndMarkerStyle end)
    {
        if (port is null || !IsOutward(port))
        {
            return 0.0;
        }

        var glyph = NotationMetrics.PortGlyphHalfSizeFor(port.Appearance);
        var extent = verticalFlow
            ? fontSize * 1.3
            : PortLabelWidthEstimator.MeasureWidth(port.ExternalLabel!, fontSize);
        return glyph + Themes.Light.LabelPadding + extent + EndLabelClearance
            + (NotationMetrics.AlongLineLength(end) * NotationMetrics.MarkerScale(Themes.Light));
    }

    /// <summary>
    /// Returns the pitch a port's outward label needs across the face: one label height on a left or
    /// right face (the label sits above the connector), the label width on a top or bottom face (the label
    /// sits beside the connector); zero for a port without an outward label.
    /// </summary>
    /// <param name="port">The graph port at the connector end, or <see langword="null"/>.</param>
    /// <param name="fontSize">Body font size, in logical pixels.</param>
    /// <param name="side">The box face the connector end sits on.</param>
    /// <returns>The pitch, in logical pixels.</returns>
    private static double OutwardPortLabelPitch(LayoutGraphPort? port, double fontSize, PortSide side)
    {
        if (port is null || !IsOutward(port))
        {
            return 0.0;
        }

        return (side is PortSide.Left or PortSide.Right
            ? (fontSize * 1.3) + NotationMetrics.PortLabelConnectorGap(Themes.Light)
            : PortLabelWidthEstimator.MeasureWidth(port.ExternalLabel!, fontSize) + NotationMetrics.PortLabelConnectorGap(Themes.Light))
            + EndLabelClearance;
    }

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
        // between the two node centers so the connector is still drawn.
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
            // margin) for a box that has both a title and a top/bottom port to avoid overlap. A
            // boundary port (one with both an InternalLabel and an ExternalLabel) additionally offsets
            // its InternalLabel by NotationMetrics.EndMarkerLength beyond a plain port's offset (see
            // SvgRenderer.EmitPortLabel's remarks), pushing the label that much further toward the
            // title; this function only sees the aggregated per-side label text (not whether any of
            // it came from a boundary port), so the clearance is unconditionally widened by that same
            // amount to safely cover the worst case.
            var titleClearance = (2.0 * assumedFontSize) + (2.0 * PortLabelClearance) + NotationMetrics.EndMarkerLength;
            if (top > 0.0)
            {
                top = Math.Max(top, titleClearance);
            }

            if (bottom > 0.0)
            {
                bottom = Math.Max(bottom, titleClearance);
            }

            // Deliberately no left/right-port fallback here: a box with only left/right ports and a
            // title needs its *rendered* ContentInsetTop to stay 0 so the title still renders
            // top-pinned at the box's own edge (see the box-emission site's CenterTitle gating) —
            // the growth-floor need for this case is handled separately via
            // engineNodes[i].TitleReserveTop/effectiveTopReserve in the Fix-5 growth loop, which does
            // not depend on this function's return value.
        }

        return (left, right, top, bottom);
    }

    /// <summary>
    /// Resolves the minimum straight entry approach reserved for a reversed (back) edge's final
    /// sub-edge from the end decorations the graph's edges actually carry. A reversed edge ends in a
    /// wrap-around corridor whose final approach was historically only
    /// <see cref="LayeredLayoutMetrics.ConnectorClearance"/> wide — shorter than a rendered open
    /// chevron plus the rounded corner before it, so the renderer's corner clamp collapsed the bend
    /// into a sharp corner inside the decoration (a small hollow triangle at the arrowhead). The
    /// approach is therefore raised to the widest decoration's rendered along-line length plus the
    /// corner radius plus the clean-leg margin (the same formula the connector router uses for a
    /// forward edge's end leg). A graph whose edges carry no decoration keeps exactly
    /// <see cref="LayeredLayoutMetrics.ConnectorClearance"/>, so its output is unchanged.
    /// </summary>
    /// <param name="edges">The graph edges whose <see cref="LayoutGraphEdge.TargetEnd"/> decorations are inspected.</param>
    /// <returns>The back-edge entry approach, in logical pixels.</returns>
    internal static double ResolveBackEdgeEntryApproach(IEnumerable<LayoutGraphEdge> edges)
    {
        var approach = LayeredLayoutMetrics.ConnectorClearance;
        foreach (var edge in edges)
        {
            approach = Math.Max(approach, ResolveEndApproach(edge.TargetEnd));
        }

        return approach;
    }

    /// <summary>
    /// Returns the straight run a connector must keep into an end decoration so the rounded corner before
    /// it never intrudes into the decoration: its rendered along-line length plus the corner radius plus the
    /// clean-leg margin (the same formula the connector router uses for a forward edge's end leg).
    /// </summary>
    /// <param name="end">The decoration drawn at the connector end.</param>
    /// <returns>The required straight approach, in logical pixels.</returns>
    internal static double ResolveEndApproach(EndMarkerStyle end) =>
        NotationMetrics.RenderedAlongLineLength(end, Themes.Light)
            + Themes.Light.LineCornerRadius
            + Themes.Light.CleanLegMargin;

    /// <summary>
    /// Returns whether a node has a name label that occupies the body title band: it has a label and
    /// does not draw it in a folder tab (<see cref="FolderTitlePlacement.Tab"/>), whose name therefore
    /// reserves no body space.
    /// </summary>
    /// <param name="node">The input node.</param>
    /// <returns><see langword="true"/> when the node's name occupies the body title band.</returns>
    internal static bool HasBodyTitle(LayoutGraphNode node) =>
        node.Label != null && !BoxMetrics.IsNameInTab(node.Shape, node.Label, node.Appearance);

    /// <summary>
    /// Returns whether a node draws any title content in its body that a top/bottom port label must stay
    /// clear of: a body name (<see cref="HasBodyTitle"/>) or a keyword line, which the renderers still
    /// draw in the body for a folder whose name is drawn in the tab.
    /// </summary>
    /// <param name="node">The input node.</param>
    /// <returns><see langword="true"/> when the node has a body name or a keyword line.</returns>
    internal static bool HasBodyTitleContent(LayoutGraphNode node) =>
        HasBodyTitle(node) || node.Keyword != null;

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
    internal static double ResolveAssumedFontSize(LayoutGraph graph, LayoutOptions options)
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
    /// Resolves the minimum corridor width between adjacent layers: an explicit
    /// <see cref="CoreOptions.LayerSpacing"/> on the graph takes precedence, then one on the options,
    /// falling back to the property default when neither declares one.
    /// </summary>
    /// <param name="graph">The graph whose explicit layer-spacing declaration takes precedence.</param>
    /// <param name="options">The options consulted when the graph declares no layer spacing.</param>
    /// <returns>The minimum layer-to-layer corridor width, in logical pixels, to lay the graph out with.</returns>
    internal static double ResolveLayerSpacing(LayoutGraph graph, LayoutOptions options)
    {
        if (graph.TryGet(CoreOptions.LayerSpacing, out var fromGraph))
        {
            return fromGraph;
        }

        return options.TryGet(CoreOptions.LayerSpacing, out var fromOptions)
            ? fromOptions
            : CoreOptions.LayerSpacing.DefaultValue;
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
