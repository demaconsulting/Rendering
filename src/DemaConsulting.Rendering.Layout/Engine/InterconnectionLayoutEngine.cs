// <copyright file="InterconnectionLayoutEngine.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Layout.Engine.Layered;

using static DemaConsulting.Rendering.Layout.Engine.Layered.LayeredLayoutMetrics;

namespace DemaConsulting.Rendering.Layout.Engine;

/// <summary>
/// A node to be placed by the <see cref="InterconnectionLayoutEngine"/>, identified by its size.
/// </summary>
/// <param name="Width">
/// Width of the node's bounding box in logical pixels. For <see cref="LayoutDirection.Down"/>/
/// <see cref="LayoutDirection.Up"/> flow, <see cref="Layered.LayeredGraph.SwapNodeAxes"/> swaps this
/// with <see cref="Height"/> so the abstract along/cross axes line up with layer progression; see
/// <see cref="RealWidth"/> for the caller's true, never-swapped width.
/// </param>
/// <param name="Height">
/// Height of the node's bounding box in logical pixels. See the <see cref="Width"/> remarks for the
/// Down/Up axis-swap caveat; see <see cref="RealHeight"/> for the caller's true, never-swapped height.
/// </param>
/// <param name="Shape">
/// The box shape used for shape-aware port distribution and connector-endpoint projection. Defaults to
/// <see cref="BoxShape.Rectangle"/>, which keeps every existing 2-arg call site's full-face, zero-offset
/// behavior unchanged.
/// </param>
/// <param name="RoundedCornerRadius">
/// Corner radius hint for <see cref="BoxShape.RoundedRectangle"/> nodes, or <see langword="null"/> to
/// use the shape's default. Ignored for other shapes.
/// </param>
/// <param name="FolderTabWidth">
/// Folder-tab width hint for <see cref="BoxShape.Folder"/> nodes, or <see langword="null"/> to fall back
/// to the router's generic folder-tab width formula (which also consults <see cref="Label"/>). Ignored
/// for other shapes.
/// </param>
/// <param name="FolderTabHeight">
/// Folder-tab height hint for <see cref="BoxShape.Folder"/> nodes, or <see langword="null"/> to use the
/// shape's default tab height. Ignored for other shapes.
/// </param>
/// <param name="Label">
/// The node's label, threaded through so <see cref="BoxShape.Folder"/>'s label-length tab-width fallback
/// computes the same tab width the router and renderer use when no explicit <see cref="FolderTabWidth"/>
/// hint is set.
/// </param>
/// <param name="RealWidth">
/// The caller's true, never-swapped bounding-box width in logical pixels, needed because
/// <see cref="Width"/> may have been swapped with <see cref="Height"/> by
/// <see cref="Layered.LayeredGraph.SwapNodeAxes"/> for Down/Up flow. Shape geometry (for example
/// <see cref="BoxShape.Note"/>'s fold size, which combines both real dimensions) requires the real,
/// never-swapped values.
/// </param>
/// <param name="RealHeight">
/// The caller's true, never-swapped bounding-box height in logical pixels. See the
/// <see cref="RealWidth"/> remarks.
/// </param>
/// <param name="TitleReserveTop">
/// The vertical band, in logical pixels, that this node's title (keyword line, if any, then the name
/// line) occupies at the top of the box, or 0 when the node has no title. <see cref="Layered.PortDistributor"/>
/// excludes this band from left/right-face port placement (only when the requested flow direction is
/// <see cref="LayoutDirection.Right"/> or <see cref="LayoutDirection.Left"/>, the only directions for
/// which this abstract cross-axis band corresponds to the box's real top edge — see the layered
/// pipeline's title-vs-side-port reservation), so a left/right port can never land in the same row as
/// the box's own title.
/// </param>
internal readonly record struct LayerNode(
    double Width,
    double Height,
    BoxShape Shape = BoxShape.Rectangle,
    double? RoundedCornerRadius = null,
    double? FolderTabWidth = null,
    double? FolderTabHeight = null,
    string? Label = null,
    double RealWidth = 0.0,
    double RealHeight = 0.0,
    double TitleReserveTop = 0.0);

/// <summary>
/// A directed edge (from a source node to a target node, by index) used for layering.
/// </summary>
/// <param name="Source">Index of the source node.</param>
/// <param name="Target">Index of the target node.</param>
internal readonly record struct LayerEdge(int Source, int Target);

/// <summary>
/// The result of an interconnection layout pass.
/// </summary>
/// <param name="Rects">Placed rectangles, one per input node in the same order.</param>
/// <param name="TotalWidth">Total diagram width in logical pixels, including padding.</param>
/// <param name="TotalHeight">Total diagram height in logical pixels, including padding.</param>
/// <param name="NodeLayers">Assigned Sugiyama layer index for each node, in node order.</param>
/// <param name="ConnectorWaypoints">Orthogonal connector waypoints for each acyclic edge.</param>
internal sealed record LayerResult(
    IReadOnlyList<Rect> Rects,
    double TotalWidth,
    double TotalHeight,
    IReadOnlyList<int> NodeLayers,
    IReadOnlyList<IReadOnlyList<Point2D>> ConnectorWaypoints)
{
    /// <summary>
    /// Gets the acyclic edge set (by node index), index-aligned with <see cref="ConnectorWaypoints"/>.
    /// </summary>
    /// <remarks>
    /// The layered pipeline's cycle-breaking stage drops self-loops, de-duplicates identical directed
    /// pairs, and reverses back edges, so <see cref="ConnectorWaypoints"/> holds one polyline per
    /// <em>acyclic</em> edge rather than one per input edge. Consumers key a
    /// <c>(source, target) → polyline</c> lookup on this list (reversing the polyline for a reversed
    /// back edge) to recover the route for each of their own input edges.
    /// </remarks>
    public IReadOnlyList<LayerEdge> AcyclicEdges { get; init; } = [];

    /// <summary>
    /// Gets, parallel to <see cref="AcyclicEdges"/> (same index order), the 0-based index into the
    /// input <c>edges</c> list passed to <see cref="InterconnectionLayoutEngine.Place"/> that each
    /// acyclic edge originated from. Populated from <see cref="Layered.LayeredGraph.AcyclicOriginalIndex"/>.
    /// </summary>
    public IReadOnlyList<int> AcyclicOriginalIndex { get; init; } = [];
}

/// <summary>
/// Thin façade over the reusable layered layout pipeline (see
/// <see cref="DemaConsulting.Rendering.Layout.Engine.Layered.ComponentPacker"/>, which wraps the
/// default ELK-layered stage sequence). Assembles the default stage sequence and adapts its output to
/// the <see cref="LayerResult"/> contract consumed by the interconnection view strategy.
/// </summary>
/// <remarks>
/// All placement and routing logic lives in the individual pipeline stages under
/// <c>Layout/Engine/Layered/</c>. This type exists only to preserve the original public entry
/// point and result shape; for a single connected graph, it is behavior-preserving with respect to the
/// previous monolithic implementation (verified byte for byte by the pipeline-equivalence tests). Since
/// <see cref="Place"/> routes through <see cref="DemaConsulting.Rendering.Layout.Engine.Layered.ComponentPacker"/>
/// (see its class doc), a graph with 2+ disconnected components is now split, laid out per component,
/// and shelf-packed rather than stacked into a single column — this is a deliberate behavior change
/// from the historical single-column-for-everything output, not a regression.
/// </remarks>
internal static class InterconnectionLayoutEngine
{
    /// <summary>
    /// Computes a full Sugiyama layered placement and ELK-style slot routing for the given nodes
    /// and directed edges, returning box positions and orthogonal connector waypoints.
    /// </summary>
    /// <param name="nodes">Input nodes to place, in caller order.</param>
    /// <param name="edges">Directed edges between nodes (by index).</param>
    /// <param name="direction">
    /// The primary flow direction the layers progress along. Defaults to
    /// <see cref="LayoutDirection.Right"/>, which is byte-identical to the original engine; the other
    /// directions are realized by the pipeline's <see cref="AxisTransform"/> stage.
    /// </param>
    /// <param name="nodeSpacing">
    /// The minimum vertical gap enforced between adjacent nodes stacked in the same layer. Defaults to
    /// <see cref="Layered.LayeredLayoutMetrics.NodeSpacing"/>, which is byte-identical to the original
    /// engine's fixed constant.
    /// </param>
    /// <param name="mergeParallelEdges">
    /// Whether parallel edges (edges sharing the same directed node pair) are merged into a single
    /// acyclic edge, mirroring <see cref="Rendering.CoreOptions.MergeParallelEdges"/>. Defaults to
    /// <see langword="true"/>, which is byte-identical to the original engine's unconditional
    /// deduplication.
    /// </param>
    /// <returns>Placement result with rects, layer assignments, and connector waypoints.</returns>
    public static LayerResult Place(
        IReadOnlyList<LayerNode> nodes,
        IReadOnlyList<LayerEdge> edges,
        LayoutDirection direction = LayoutDirection.Right,
        double nodeSpacing = Layered.LayeredLayoutMetrics.NodeSpacing,
        bool mergeParallelEdges = true)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        var n = nodes.Count;
        if (n == 0)
        {
            return new LayerResult([], 2.0 * Padding, 2.0 * Padding, [], []);
        }

        var graph = new LayeredGraph(nodes, edges, direction) { NodeSpacing = nodeSpacing, MergeParallelEdges = mergeParallelEdges };
        ComponentPacker.WithDefaultStages(nodeSpacing).Apply(graph);

        var augX = graph.AugX;
        var augY = graph.AugY;
        var columnX = graph.ColumnX;
        var maxColWidth = graph.MaxColWidth;
        var waypoints = graph.Waypoints;

        // ComponentPacker's single-component fast path runs the stages directly on this graph, which
        // (like the original pipeline) bake in the canvas Padding as their own coordinate origin, so
        // AugX/AugY/Waypoints already start at (Padding, Padding). Its multi-component path instead
        // normalizes each packed component's content bounding box to touch the shelf origin (0, 0) —
        // by design, since ComponentPacker is a reusable stage that should not assume any particular
        // caller adds outer padding — so this façade must add that same (Padding, Padding) origin
        // itself whenever the packed path ran (indicated by ColumnX being left empty; see its remarks).
        if (columnX.Length == 0)
        {
            var shiftedX = new double[n];
            var shiftedY = new double[n];
            for (var i = 0; i < n; i++)
            {
                shiftedX[i] = augX[i] + Padding;
                shiftedY[i] = augY[i] + Padding;
            }

            augX = shiftedX;
            augY = shiftedY;

            var shiftedWaypoints = new IReadOnlyList<Point2D>[waypoints.Count];
            for (var k = 0; k < waypoints.Count; k++)
            {
                var original = waypoints[k];
                var translated = new Point2D[original.Count];
                for (var p = 0; p < original.Count; p++)
                {
                    translated[p] = new Point2D(original[p].X + Padding, original[p].Y + Padding);
                }

                shiftedWaypoints[k] = translated;
            }

            waypoints = shiftedWaypoints;
        }

        // Assemble result. After the pipeline's AxisTransform stage the augmented coordinates are in
        // screen space, so each box is placed at its screen top-left with its intrinsic (un-swapped)
        // width and height.
        var rects = new Rect[n];
        for (var i = 0; i < n; i++)
        {
            rects[i] = new Rect(augX[i], augY[i], nodes[i].Width, nodes[i].Height);
        }

        // The abstract stages compute two extents: the along-extent (layer progression, from the
        // column geometry) and the cross-extent (within-layer, from the placed screen coordinates). A
        // RIGHT/LEFT flow maps the along-axis to screen X and the cross-axis to screen Y; a DOWN/UP flow
        // transposes them. Computing both extents once and assigning them by direction keeps the
        // RIGHT path byte-identical while giving DOWN/UP correct screen dimensions.
        //
        // ColumnX/MaxColWidth are only populated when ComponentPacker's single-component fast path ran
        // the stages directly on this graph (the byte-identical case the equivalence tests cover); for a
        // multi-component (packed) graph, ComponentPacker deliberately leaves them empty, since the
        // shelf-packed components no longer share one aligned layer/column structure. In that case the
        // along-extent is instead computed the same bounding-box way the cross-extent already is below.
        var transposed = direction is LayoutDirection.Down or LayoutDirection.Up;
        double alongTotal;
        if (columnX.Length > 0)
        {
            var lastLayer = columnX.Length - 1;
            alongTotal = columnX[lastLayer] + maxColWidth[lastLayer] + Padding;
        }
        else
        {
            alongTotal = Padding;
            for (var i = 0; i < n; i++)
            {
                var alongFar = transposed
                    ? augY[i] + nodes[i].Height
                    : augX[i] + nodes[i].Width;
                alongTotal = Math.Max(alongTotal, alongFar + Padding);
            }
        }

        var crossTotal = Padding;
        for (var i = 0; i < n; i++)
        {
            var crossFar = transposed
                ? augX[i] + nodes[i].Width
                : augY[i] + nodes[i].Height;
            crossTotal = Math.Max(crossTotal, crossFar + Padding);
        }

        var totalWidth = transposed ? crossTotal : alongTotal;
        var totalHeight = transposed ? alongTotal : crossTotal;

        // The node-rect-only extents above assume every layer's routing corridor fits within the gap
        // the placement stages budgeted for it. That assumption does not always hold — for example a
        // reversed (back) edge's wrap-around approach (LayeredCorridorRouter.BackEdgeEntryApproach) can
        // route a bend point beyond the last node's far edge. Rather than trying to enumerate every
        // stage that can push a waypoint outside the node-derived bounds, widen the canvas directly from
        // the actual routed geometry: Math.Max only ever grows the extents, so a topology whose routing
        // stays within the node bounds (the common case) sees byte-identical totals to before.
        foreach (var wp in waypoints)
        {
            foreach (var p in wp)
            {
                totalWidth = Math.Max(totalWidth, p.X + Padding);
                totalHeight = Math.Max(totalHeight, p.Y + Padding);
            }
        }

        return new LayerResult(rects, totalWidth, totalHeight, graph.NodeLayers, waypoints)
        {
            AcyclicEdges = graph.Acyclic,
            AcyclicOriginalIndex = graph.AcyclicOriginalIndex,
        };
    }
}
