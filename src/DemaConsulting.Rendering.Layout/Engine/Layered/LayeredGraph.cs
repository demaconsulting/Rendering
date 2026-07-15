// <copyright file="LayeredGraph.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Layout.Engine.Layered;

/// <summary>
/// Identifies which face of a boundary container's box a hierarchy-crossing dummy represents.
/// </summary>
/// <remarks>
///     A boundary port carries up to two logical connection faces at the same physical anchor: the
///     <see cref="External"/> face is where an edge crossing in from a sibling scope approaches the
///     container from outside, and the <see cref="Internal"/> face is where a delegation edge into the
///     container's own child scope departs on the inside. Both faces resolve to a single shared anchor
///     on the container boundary; the enum records which logical half a given hierarchy-crossing dummy
///     stands in for, so the resolver can spread and reconcile the two halves consistently.
/// </remarks>
internal enum HierarchyCrossingFace
{
    /// <summary>The outward-facing half, approached by an edge crossing in from a sibling scope.</summary>
    External,

    /// <summary>The inward-facing half, departed by a delegation edge into the container's child scope.</summary>
    Internal,
}

/// <summary>
/// Describes a hierarchy-crossing dummy: the extra data an <see cref="AugNode"/> carries when it stands
/// in for a boundary port crossing a container boundary, rather than an ordinary long-edge dummy.
/// </summary>
/// <remarks>
///     Generalizes <c>LongEdgeSplitter</c>'s zero-size intermediate-layer dummy from "spans layers
///     within one scope" to "spans layers across nested scopes". A hierarchy-crossing dummy participates
///     in the same layer-assignment, crossing-minimization, and placement stages as an ordinary dummy
///     while additionally remembering the originating <see cref="Port"/> and which <see cref="Face"/> of
///     the container boundary it stands in for. The recursive layered pipeline
///     (<c>LayeredLayoutPipeline.RunRecursive</c>) is the producer: <c>MergeRegionGraphAssembler</c>
///     seeds one crossing dummy per boundary port and the pipeline tags the corresponding
///     <see cref="AugNode.Crossing"/> after long-edge splitting, then reads the dummies' placed
///     positions back to propagate a resolved boundary order between nesting levels. The ordering
///     primitive's unit tests exercise the same descriptor.
/// </remarks>
/// <param name="Port">The originating input-graph boundary port this dummy stands in for.</param>
/// <param name="Face">Which logical face (external/internal) of the boundary crossing this dummy is.</param>
internal readonly record struct HierarchyCrossing(LayoutGraphPort Port, HierarchyCrossingFace Face);

/// <summary>A node in the augmented Sugiyama graph (real part box, long-edge dummy, or hierarchy-crossing dummy).</summary>
/// <param name="Width">Width of the node's bounding box in logical pixels.</param>
/// <param name="Height">Height of the node's bounding box in logical pixels.</param>
/// <param name="Layer">Assigned Sugiyama layer index.</param>
/// <param name="IsDummy">Whether this node is a zero-size long-edge dummy.</param>
/// <param name="Crossing">
/// When non-<see langword="null"/>, marks this node as a hierarchy-crossing dummy standing in for a
/// boundary port, recording the originating port and boundary face. The recursive layered pipeline
/// (<c>LayeredLayoutPipeline.RunRecursive</c>) is the producer, tagging crossing dummies after long-edge
/// splitting; the ordinary flat pipeline never assigns it, so it is <see langword="null"/> for every
/// real node and every long-edge dummy on that path, keeping default construction and every existing
/// flat caller byte-identical.
/// </param>
/// <param name="PinnedCrossAxis">
/// When non-<see langword="null"/>, pins this node's cross-axis coordinate (Y for horizontal flow, X
/// for vertical flow) to an already-resolved value from an enclosing scope, rather than letting
/// <c>BrandesKopfPlacer</c> derive it from ordinary fork-centering/alignment. <c>MergeRegionGraphAssembler</c>
/// sets this on a child level's <see cref="HierarchyCrossing"/> dummy to the parent scope's resolved
/// boundary-port anchor, so the child's fan-out does not re-center independently of where the parent
/// already placed the port. Like <see cref="Crossing"/>, this is <see langword="null"/> by default for
/// every real node, every long-edge dummy, and every node produced by the flat (non-hierarchical)
/// pipeline, keeping default construction and every existing flat caller byte-identical.
/// </param>
internal sealed record AugNode(
    double Width,
    double Height,
    int Layer,
    bool IsDummy = false,
    HierarchyCrossing? Crossing = null,
    double? PinnedCrossAxis = null);

/// <summary>A sub-edge in the augmented graph after long-edge splitting.</summary>
/// <param name="Source">Index of the source augmented node.</param>
/// <param name="Target">Index of the target augmented node.</param>
/// <param name="OrigEdgeIndex">Index of the original (pre-split) edge this sub-edge belongs to.</param>
internal readonly record struct AugEdge(int Source, int Target, int OrigEdgeIndex);

/// <summary>
/// The mutable shared state threaded through every <see cref="ILayoutStage"/> of the layered
/// pipeline. Each stage reads the fields produced by earlier stages and writes the fields it owns.
/// </summary>
/// <remarks>
/// This object replaces the ad-hoc local variables that the monolithic interconnection engine
/// passed between its private phase methods, while preserving exactly the same intermediate values
/// (and therefore the same floating-point results).
/// </remarks>
internal sealed class LayeredGraph
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LayeredGraph"/> class.
    /// </summary>
    /// <param name="nodes">Input nodes to place, in caller order.</param>
    /// <param name="edges">Directed edges between nodes (by index).</param>
    /// <param name="direction">The requested layout flow direction.</param>
    public LayeredGraph(
        IReadOnlyList<LayerNode> nodes,
        IReadOnlyList<LayerEdge> edges,
        LayoutDirection direction)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        Nodes = nodes;
        Edges = edges;
        Direction = direction;
        N = nodes.Count;
    }

    /// <summary>Gets the number of real input nodes.</summary>
    public int N { get; }

    /// <summary>Gets the input nodes, in caller order.</summary>
    /// <remarks>
    /// The setter is private; the only in-place mutation is <see cref="SwapNodeAxes"/>, the seam
    /// used by <see cref="AxisTransform.NormalizeInputAxes"/> to feed the direction-agnostic stages
    /// node sizes whose along-extent matches the requested flow direction.
    /// </remarks>
    public IReadOnlyList<LayerNode> Nodes { get; private set; }

    /// <summary>Gets the directed input edges (by node index).</summary>
    public IReadOnlyList<LayerEdge> Edges { get; }

    /// <summary>Gets the requested layout flow direction.</summary>
    public LayoutDirection Direction { get; }

    /// <summary>
    /// Gets or sets whether <see cref="AxisTransform.NormalizeInputAxes"/> has already swapped this
    /// graph's node axes.
    /// </summary>
    /// <remarks>
    /// <see cref="AxisTransform.NormalizeInputAxes"/> is called at every composable entry point
    /// (<see cref="LayeredLayoutPipeline.Run"/>, <see cref="LayeredLayoutPipeline.RunRecursive"/>, and
    /// <see cref="ComponentPacker.Apply"/>) so each is self-contained regardless of call site. When
    /// <see cref="ComponentPacker"/> is composed as an inner stage of a <see cref="LayeredLayoutPipeline"/>
    /// that already normalized the same graph, a second unguarded swap would undo the first and hand the
    /// downstream stages the wrong along/cross extents. This flag makes the normalization idempotent so
    /// composing normalizing stages is always safe.
    /// </remarks>
    public bool InputAxesNormalized { get; set; }

    /// <summary>
    /// Gets or sets the minimum straight entry approach reserved for a reversed (back) edge's final
    /// sub-edge — the wrap-around corridor that ends at the true target where the consumer draws the
    /// end marker.
    /// </summary>
    /// <remarks>
    /// The default is <see cref="LayeredLayoutMetrics.ConnectorClearance"/>, which exactly reproduces
    /// the original engine: the router's first slot already starts one
    /// <see cref="LayeredLayoutMetrics.ConnectorClearance"/> past the source column, so the
    /// <c>Math.Max</c> clamp in <see cref="LayeredCorridorRouter"/> is a no-op at the default and forward
    /// geometry stays byte-identical. A consumer that draws a longer end decoration (for example the
    /// state-transition view's open chevron) raises this so the rounded corner never intrudes into the
    /// decoration.
    /// </remarks>
    public double BackEdgeEntryApproach { get; set; } = LayeredLayoutMetrics.ConnectorClearance;

    /// <summary>
    /// Gets or sets the minimum vertical gap enforced between adjacent nodes stacked in the same
    /// layer by the Brandes-Köpf compaction step, mirroring <see cref="Rendering.CoreOptions.NodeSpacing"/>.
    /// </summary>
    /// <remarks>
    /// The default is <see cref="LayeredLayoutMetrics.NodeSpacing"/>, which exactly reproduces the
    /// original engine's fixed constant so callers that never set this property see byte-identical
    /// output to before this property existed.
    /// </remarks>
    public double NodeSpacing { get; set; } = LayeredLayoutMetrics.NodeSpacing;

    /// <summary>
    /// Gets or sets whether <see cref="CycleBreaker"/> collapses parallel edges (multiple input
    /// edges sharing the same directed node pair after cycle-breaking) into a single retained edge,
    /// mirroring <see cref="Rendering.CoreOptions.MergeParallelEdges"/>.
    /// </summary>
    /// <remarks>
    /// The default, <see langword="true"/>, exactly reproduces the original engine's unconditional
    /// deduplication, so callers that never set this property see byte-identical output to before
    /// this property existed. Setting it to <see langword="false"/> retains every parallel edge
    /// instance (self-loops are still always dropped).
    /// </remarks>
    public bool MergeParallelEdges { get; set; } = true;

    /// <summary>Gets or sets the acyclic edge set after cycle breaking.</summary>
    public List<LayerEdge> Acyclic { get; set; } = [];

    /// <summary>
    /// Gets or sets, parallel to <see cref="Acyclic"/> (same index order), the 0-based index into the
    /// input <see cref="Edges"/> list that each surviving <see cref="Acyclic"/> entry originated from.
    /// </summary>
    /// <remarks>
    /// Lets a consumer recover, for every acyclic/routed edge, which original input edge it came
    /// from — needed when <see cref="MergeParallelEdges"/> is <see langword="false"/> so each of
    /// several parallel input edges can be matched back to its own independently-routed polyline.
    /// </remarks>
    public IReadOnlyList<int> AcyclicOriginalIndex { get; set; } = [];

    /// <summary>
    /// Gets or sets, parallel to <see cref="Acyclic"/> (same index order), whether each retained
    /// acyclic edge was produced by reversing a cycle-causing back edge.
    /// </summary>
    /// <remarks>
    /// <see cref="CycleBreaker"/> records this flag so later stages can recognize edges whose true
    /// direction was flipped for layering. <see cref="LayeredCorridorRouter"/> reads it to guarantee a
    /// minimum entry approach for the arrowhead that the consumer draws on the (un-reversed) target.
    /// </remarks>
    public bool[] AcyclicReversed { get; set; } = [];

    /// <summary>Gets or sets the assigned layer index for each real node, in node order.</summary>
    public int[] NodeLayers { get; set; } = [];

    /// <summary>Gets or sets the augmented nodes (real boxes followed by long-edge dummies).</summary>
    public List<AugNode> AugNodes { get; set; } = [];

    /// <summary>Gets or sets the augmented sub-edges produced by long-edge splitting.</summary>
    public List<AugEdge> AugEdges { get; set; } = [];

    /// <summary>Gets or sets the augmented-node indices grouped (and ordered) by layer.</summary>
    public List<List<int>> Groups { get; set; } = [];

    /// <summary>Gets or sets the X coordinate of each augmented node.</summary>
    public double[] AugX { get; set; } = [];

    /// <summary>Gets or sets the Y coordinate of each augmented node.</summary>
    public double[] AugY { get; set; } = [];

    /// <summary>Gets or sets the left X coordinate of each layer column.</summary>
    public double[] ColumnX { get; set; } = [];

    /// <summary>Gets or sets the maximum real-node width per layer column.</summary>
    public double[] MaxColWidth { get; set; } = [];

    /// <summary>Gets or sets the source-side (right face) port Y for each augmented sub-edge.</summary>
    public double[] AugPortYSrc { get; set; } = [];

    /// <summary>Gets or sets the target-side (left face) port Y for each augmented sub-edge.</summary>
    public double[] AugPortYTgt { get; set; } = [];

    /// <summary>Gets or sets the orthogonal bend points for each augmented sub-edge.</summary>
    public List<Point2D>[] AugBendPoints { get; set; } = [];

    /// <summary>Gets or sets the assembled orthogonal waypoints for each original (acyclic) edge.</summary>
    public IReadOnlyList<IReadOnlyList<Point2D>> Waypoints { get; set; } = [];

    /// <summary>
    /// Swaps each input node's <see cref="LayerNode.Width"/> and <see cref="LayerNode.Height"/>.
    /// </summary>
    /// <remarks>
    /// The direction-agnostic stages always treat a node's width as its along-axis (layer
    /// progression) extent and its height as its cross-axis (within-layer) extent. For a top-to-bottom
    /// (<see cref="LayoutDirection.Down"/>) or bottom-to-top (<see cref="LayoutDirection.Up"/>) flow,
    /// the along-axis must instead be the node height, so <see cref="AxisTransform.NormalizeInputAxes"/>
    /// calls this seam before the stages run. It is never invoked for the
    /// <see cref="LayoutDirection.Right"/>/<see cref="LayoutDirection.Left"/> paths, which keeps those
    /// outputs byte-identical. Every other <see cref="LayerNode"/> field (<see cref="LayerNode.Shape"/>,
    /// <see cref="LayerNode.RoundedCornerRadius"/>, <see cref="LayerNode.FolderTabWidth"/>,
    /// <see cref="LayerNode.FolderTabHeight"/>, <see cref="LayerNode.Label"/>,
    /// <see cref="LayerNode.RealWidth"/>, <see cref="LayerNode.RealHeight"/>) is preserved unchanged by
    /// the swap, since only the abstract along/cross axes need to reorient for Down/Up flow.
    /// </remarks>
    public void SwapNodeAxes()
    {
        var swapped = new LayerNode[Nodes.Count];
        for (var i = 0; i < Nodes.Count; i++)
        {
            // S2234: width and height are deliberately swapped so the stages space layers by height.
#pragma warning disable S2234
            swapped[i] = Nodes[i] with { Width = Nodes[i].Height, Height = Nodes[i].Width };
#pragma warning restore S2234
        }

        Nodes = swapped;
    }
}
