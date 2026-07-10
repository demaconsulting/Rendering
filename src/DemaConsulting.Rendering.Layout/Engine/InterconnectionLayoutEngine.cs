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
/// <see cref="DemaConsulting.Rendering.Layout.Engine.Layered.LayeredLayoutPipeline"/>).
/// Assembles the default ELK-layered stage sequence and adapts its output to the
/// <see cref="LayerResult"/> contract consumed by the interconnection view strategy.
/// </summary>
/// <remarks>
/// All placement and routing logic lives in the individual pipeline stages under
/// <c>Layout/Engine/Layered/</c>. This type exists only to preserve the original public entry
/// point and result shape; it is behavior-preserving with respect to the previous monolithic
/// implementation (verified byte for byte by the pipeline-equivalence tests).
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
        var pipeline = LayeredLayoutPipeline.Builder()
            .Direction(direction)
            .Hierarchy(Layered.HierarchyHandling.Flat)
            .AddDefaultStages()
            .Build();
        pipeline.Run(graph);

        var augX = graph.AugX;
        var augY = graph.AugY;
        var columnX = graph.ColumnX;
        var maxColWidth = graph.MaxColWidth;

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
        var lastLayer = columnX.Length - 1;
        var alongTotal = columnX[lastLayer] + maxColWidth[lastLayer] + Padding;
        var transposed = direction is LayoutDirection.Down or LayoutDirection.Up;
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

        return new LayerResult(rects, totalWidth, totalHeight, graph.NodeLayers, graph.Waypoints)
        {
            AcyclicEdges = graph.Acyclic,
            AcyclicOriginalIndex = graph.AcyclicOriginalIndex,
        };
    }
}
