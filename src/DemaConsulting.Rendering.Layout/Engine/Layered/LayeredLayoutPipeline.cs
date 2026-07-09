// <copyright file="LayeredLayoutPipeline.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Layout.Engine.Layered;

/// <summary>
/// An ordered sequence of <see cref="ILayoutStage"/> instances that, when run, transforms a
/// <see cref="LayeredGraph"/> from raw nodes and edges into a fully placed and routed layout.
/// </summary>
/// <remarks>
/// Pipelines are assembled with the fluent <see cref="PipelineBuilder"/> returned by
/// <see cref="Builder"/>. The default stage sequence reproduces ELK's layered algorithm in the
/// order used by the original interconnection engine.
/// </remarks>
internal sealed class LayeredLayoutPipeline
{
    private readonly IReadOnlyList<ILayoutStage> _stages;

    private LayeredLayoutPipeline(
        LayoutDirection direction,
        HierarchyHandling hierarchy,
        IReadOnlyList<ILayoutStage> stages)
    {
        Direction = direction;
        Hierarchy = hierarchy;
        _stages = stages;
    }

    /// <summary>Gets the layout flow direction this pipeline was built for.</summary>
    public LayoutDirection Direction { get; }

    /// <summary>Gets the hierarchy-handling mode this pipeline was built for.</summary>
    public HierarchyHandling Hierarchy { get; }

    /// <summary>Creates a new <see cref="PipelineBuilder"/>.</summary>
    /// <returns>A fresh builder with default direction and hierarchy.</returns>
    public static PipelineBuilder Builder() => new();

    /// <summary>Runs every stage, in order, against the supplied graph.</summary>
    /// <param name="graph">The graph to lay out; mutated in place.</param>
    public void Run(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        // Normalize the input node axes for the requested direction before any stage runs, so the
        // direction-agnostic stages space layers by the correct extent (a no-op for RIGHT/LEFT).
        AxisTransform.NormalizeInputAxes(graph);

        foreach (var stage in _stages)
        {
            stage.Apply(graph);
        }
    }

    /// <summary>
    /// Runs the recursive (hierarchy-aware) layered pipeline over an assembled merge region, laying out
    /// every nesting level and retaining each level's placed graph for the decomposition step.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     The same nine-stage sequence as <see cref="PipelineBuilder.AddDefaultStages"/> is applied
    ///     per level, innermost first: <see cref="CycleBreaker"/>, level-relative
    ///     <see cref="LayerAssigner"/>, <see cref="LongEdgeSplitter"/>, hierarchy-aware
    ///     <see cref="CrossingMinimizer"/>, <see cref="BrandesKopfPlacer"/>, <see cref="PortDistributor"/>,
    ///     <see cref="LayeredCorridorRouter"/>, <see cref="LongEdgeJoiner"/>, and <see cref="AxisTransform"/>.
    ///     Crossing minimization is the single stage coordinated across levels: each level's resolved
    ///     boundary order propagates between adjacent levels (see
    ///     <see cref="CrossingMinimizer.MinimizeCrossingsRecursive"/>). Every other stage runs
    ///     independently per level, keeping each container's interior laid out inside its own box.
    ///     </para>
    ///     <para>
    ///     The pipeline's own <see cref="Direction"/> is used for every level. The returned
    ///     <see cref="RecursiveLayoutResult"/> exposes each level's placed graph and each
    ///     hierarchy-crossing dummy's placed coordinate and originating <c>(BoundaryPort, Face)</c>.
    ///     </para>
    /// </remarks>
    /// <param name="region">The assembled merge region to lay out.</param>
    /// <returns>The per-level placed graphs and crossing-placement lookup for the decomposition step.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="region"/> is <see langword="null"/>.</exception>
    public RecursiveLayoutResult RunRecursive(AssembledMergeRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);

        var levels = MergeRegionGraphAssembler.BuildAllLevelGraphs(region, Direction);

        // Stages 1-3 (+ crossing tag): normalize axes and break cycles per level, assign level-relative
        // layers across the whole region, then split long edges and tag the hierarchy-crossing dummies.
        foreach (var graph in levels.Values.Select(levelGraph => levelGraph.Graph))
        {
            AxisTransform.NormalizeInputAxes(graph);
            new CycleBreaker().Apply(graph);
        }

        new LayerAssigner().AssignLayersRecursive(region.Root, levels);

        foreach (var levelGraph in levels.Values)
        {
            new LongEdgeSplitter().Apply(levelGraph.Graph);
            TagCrossings(levelGraph);
        }

        // Stage 4: hierarchy-aware crossing minimization with boundary-order propagation.
        new CrossingMinimizer().MinimizeCrossingsRecursive(region.Root, levels);

        // Stages 5-9: place, distribute ports, route, join long edges, and transform axes, per level.
        foreach (var graph in levels.Values.Select(levelGraph => levelGraph.Graph))
        {
            new BrandesKopfPlacer().Apply(graph);
            new PortDistributor().Apply(graph);
            new LayeredCorridorRouter().Apply(graph);
            new LongEdgeJoiner().Apply(graph);
            new AxisTransform().Apply(graph);
        }

        return new RecursiveLayoutResult(region, levels);
    }

    /// <summary>
    /// Tags each hierarchy-crossing dummy's augmented node with its originating boundary port and face,
    /// wiring <see cref="AugNode.Crossing"/> from scaffolding into a genuinely honored field.
    /// </summary>
    /// <param name="levelGraph">The level graph whose crossing dummies are tagged (after long-edge splitting).</param>
    private static void TagCrossings(LevelLayeredGraph levelGraph)
    {
        var aug = levelGraph.Graph.AugNodes;
        foreach (var crossing in levelGraph.Crossings)
        {
            aug[crossing.NodeIndex] = aug[crossing.NodeIndex] with
            {
                Crossing = new HierarchyCrossing(crossing.Boundary.Port, crossing.Face),
            };
        }
    }

    /// <summary>
    /// Fluent builder that assembles a <see cref="LayeredLayoutPipeline"/> from an ordered list
    /// of stages plus a direction and hierarchy-handling selection.
    /// </summary>
    internal sealed class PipelineBuilder
    {
        private readonly List<ILayoutStage> _stages = [];
        private LayoutDirection _direction = LayoutDirection.Right;
        private HierarchyHandling _hierarchy = HierarchyHandling.Flat;

        /// <summary>Sets the layout flow direction.</summary>
        /// <param name="direction">The desired direction.</param>
        /// <returns>This builder, for chaining.</returns>
        public PipelineBuilder Direction(LayoutDirection direction)
        {
            _direction = direction;
            return this;
        }

        /// <summary>Sets the hierarchy-handling mode.</summary>
        /// <param name="hierarchy">The desired hierarchy handling.</param>
        /// <returns>This builder, for chaining.</returns>
        public PipelineBuilder Hierarchy(HierarchyHandling hierarchy)
        {
            _hierarchy = hierarchy;
            return this;
        }

        /// <summary>Appends a single stage to the pipeline.</summary>
        /// <param name="stage">The stage to append.</param>
        /// <returns>This builder, for chaining.</returns>
        public PipelineBuilder AddStage(ILayoutStage stage)
        {
            ArgumentNullException.ThrowIfNull(stage);
            _stages.Add(stage);
            return this;
        }

        /// <summary>
        /// Appends the default ELK-layered stage sequence: cycle breaking, layer assignment,
        /// long-edge splitting, crossing minimization, Brandes-Kopf placement, port distribution,
        /// orthogonal routing, long-edge joining, and the final axis transform.
        /// </summary>
        /// <returns>This builder, for chaining.</returns>
        public PipelineBuilder AddDefaultStages()
        {
            _stages.Add(new CycleBreaker());
            _stages.Add(new LayerAssigner());
            _stages.Add(new LongEdgeSplitter());
            _stages.Add(new CrossingMinimizer());
            _stages.Add(new BrandesKopfPlacer());
            _stages.Add(new PortDistributor());
            _stages.Add(new LayeredCorridorRouter());
            _stages.Add(new LongEdgeJoiner());
            _stages.Add(new AxisTransform());
            return this;
        }

        /// <summary>
        /// Appends the ELK-layered stage sequence used for recursive (compound-graph) hierarchy
        /// handling. It is the same Sugiyama stage sequence as <see cref="AddDefaultStages"/>, because a
        /// hierarchy-crossing dummy participates in exactly the same layer-assignment,
        /// crossing-minimization, and placement stages as an ordinary node or long-edge dummy — ELK's
        /// own compound-graph design. The Recursive-specific behavior is not a distinct stage but the
        /// way <see cref="LayeredLayoutPipeline.RunRecursive"/> drives this sequence per nesting level and
        /// propagates each level's resolved boundary order between adjacent levels (see
        /// <c>MergeRegionGraphAssembler</c> for the crossing-dummy seeding and
        /// <see cref="CrossingMinimizer.MinimizeCrossingsRecursive"/> for the propagation). This method
        /// exists so a Recursive pipeline is assembled through its own explicit entry point, keeping the
        /// <see cref="AddDefaultStages"/> Flat path untouched.
        /// </summary>
        /// <returns>This builder, for chaining.</returns>
        public PipelineBuilder AddRecursiveStages()
        {
            // A hierarchy-crossing dummy is a zero-size node that flows through the standard stages
            // like any other node, so the recursive pass reuses the identical stage sequence rather
            // than introducing a parallel, divergent one that could drift from the Flat path.
            return AddDefaultStages();
        }

        /// <summary>Builds the configured pipeline.</summary>
        /// <returns>A new <see cref="LayeredLayoutPipeline"/>.</returns>
        /// <remarks>
        /// Both <see cref="HierarchyHandling.Flat"/> and <see cref="HierarchyHandling.Recursive"/> are
        /// supported: Flat runs the stage sequence over a single flat graph, while Recursive runs the
        /// same stage sequence over a graph pre-seeded with hierarchy-crossing dummies. The mode is
        /// retained on the built pipeline (<see cref="LayeredLayoutPipeline.Hierarchy"/>) so callers can
        /// assert which contract a pipeline was assembled for.
        /// </remarks>
        public LayeredLayoutPipeline Build()
        {
            return new LayeredLayoutPipeline(_direction, _hierarchy, _stages.ToArray());
        }
    }
}

/// <summary>
/// The result of <see cref="LayeredLayoutPipeline.RunRecursive"/>: every nesting level's placed layered
/// graph plus the means to recover each hierarchy-crossing dummy's placed coordinate and originating
/// boundary, so the decomposition step can project the combined pass back into per-scope geometry.
/// </summary>
/// <remarks>
///     <para>
///     Per-level placed coordinates are read directly from a level's <see cref="LayeredGraph"/>:
///     <see cref="LayeredGraph.AugX"/><c>[i]</c>/<see cref="LayeredGraph.AugY"/><c>[i]</c> is the placed
///     centre of augmented node <c>i</c>, where indices <c>0..N-1</c> align with the level's
///     <see cref="MergeRegionLevel.Nodes"/> in order, and each original edge's routed polyline is in
///     <see cref="LayeredGraph.Waypoints"/>.
///     </para>
///     <para>
///     A crossing dummy's placed coordinate and its <c>(BoundaryPort, Face)</c> are recovered together
///     from <see cref="CrossingPlacements"/>, which pairs each level's <see cref="LevelCrossing"/> with
///     the placed coordinate at its node index.
///     </para>
/// </remarks>
/// <param name="Region">The assembled merge region that was laid out.</param>
/// <param name="Levels">The lookup from each nesting level to its placed layered graph and crossing bookkeeping.</param>
internal sealed record RecursiveLayoutResult(
    AssembledMergeRegion Region,
    IReadOnlyDictionary<MergeRegionLevel, LevelLayeredGraph> Levels)
{
    /// <summary>
    /// Returns every hierarchy crossing placed at <paramref name="level"/>, each paired with its
    /// originating boundary port, boundary face, and placed centre coordinate.
    /// </summary>
    /// <param name="level">The nesting level whose crossing placements are read.</param>
    /// <returns>The boundary port, face, and placed centre of each crossing at the level.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="level"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<(BoundaryPort Boundary, HierarchyCrossingFace Face, Point2D Position)> CrossingPlacements(
        MergeRegionLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        var levelGraph = Levels[level];
        var graph = levelGraph.Graph;
        var result = new List<(BoundaryPort, HierarchyCrossingFace, Point2D)>(levelGraph.Crossings.Count);
        foreach (var crossing in levelGraph.Crossings)
        {
            var position = new Point2D(graph.AugX[crossing.NodeIndex], graph.AugY[crossing.NodeIndex]);
            result.Add((crossing.Boundary, crossing.Face, position));
        }

        return result;
    }
}
