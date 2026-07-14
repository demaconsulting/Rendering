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
        for (var i = 0; i < count; i++)
        {
            var node = graphNodes[i];
            indexOf[node] = i;
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
                    ? LayeredLayoutMetrics.ResolveTitleReserveTop(node.Label != null, node.Keyword != null, assumedFontSize)
                    : 0.0);

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
            // (port.CentreY + FontSizeBody / 2) — an asymmetric downward shift from the port glyph's
            // own centre, not the symmetric top/bottom clearance the row floor above assumes. Without
            // compensating for that shift, half of the clearance above sits unused above the port row
            // while the shifted-down label text (plus its own glyph descent) can run past the box's
            // bottom edge — exactly what a single labeled port on a titled box's face produces (the
            // title reserve leaves only one row for the port, so there is no slack elsewhere to absorb
            // the shift). Add the same downward-shift amount as extra bottom margin, gated on the face
            // actually carrying a named, labeled port: bySide (from sidePorts, already resolved above)
            // is the same dictionary ResolveContentInsets uses and only records named
            // LayoutGraphPort labels (unlike faceAnchors.AnyLabeled, which tracks the *edge's own*
            // mid-line label instead of a port's ExternalLabel and would miss this case entirely) — an
            // anonymous edge endpoint draws no label near the box face and needs no extra room.
            var hasLabeledLeftOrRightAnchor = bySide != null
                && ((bySide.TryGetValue(PortSide.Left, out var leftLabels) && leftLabels.Exists(l => !string.IsNullOrEmpty(l)))
                    || (bySide.TryGetValue(PortSide.Right, out var rightLabels) && rightLabels.Exists(l => !string.IsNullOrEmpty(l))));
            if (hasLabeledLeftOrRightAnchor)
            {
                minHeight += assumedFontSize / 2.0;
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
                        var clearanceBasedHeight = 2.0 * LayeredLayoutMetrics.ConnectorClearance * faceInfo.Total;
                        var minHeightCandidate = Math.Max(labelBasedHeight, clearanceBasedHeight)
                            + effectiveTopReserve + insetBottom;

                        // Same asymmetric-downward-label-shift compensation as the single-port
                        // minHeight floor above (SvgRenderer.EmitPortLabel draws a Left/Right port's
                        // ExternalLabel at CentreY + FontSizeBody/2, not centered on the port row) —
                        // without it, this multi-port candidate can win the Math.Max below yet still
                        // leave no slack for the bottom-most labeled port's downward-shifted text,
                        // which then runs past the box's bottom edge exactly as the single-port case
                        // would without this term. PortDistributor.DistributePorts centers each of
                        // faceInfo.Total ports within its own equal-height slice of the added band, so
                        // a flat addition here is only fully absorbed by the bottom-most slice's own
                        // height when there is exactly one slice (Total == 1); for Total >= 2 a flat
                        // addend is instead divided across every slice's height (each slice gains only
                        // addend / Total), silently eroding the intended margin as Total grows. Scale
                        // by faceInfo.Total so every slice's height still gains the full
                        // assumedFontSize / 2 the single-port case relies on, regardless of how many
                        // ports share the face.
                        if (sidePortLabeled)
                        {
                            minHeightCandidate += assumedFontSize / 2.0 * faceInfo.Total;
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
                        var clearanceBasedWidth = 2.0 * LayeredLayoutMetrics.ConnectorClearance * faceInfo.Total;
                        var minWidthCandidate = Math.Max(labelBasedWidth, clearanceBasedWidth);
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
                CenterTitle: graphNodes[i].Compartments.Count == 0 && !hasNamedLeftOrRightPort));
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
