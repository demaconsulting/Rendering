### Layered Pipeline Unit Design

Part of the Rendering Layout system.

#### Layered Pipeline Overview

The layered pipeline is a reusable, composable layered-layout engine that reproduces ELK's layered
(Sugiyama-style) algorithm. It replaces a single monolithic placement method with an ordered
sequence of small, single-purpose stages. Each stage reads the state produced by earlier stages and
writes the state it owns, so the pipeline can be extended, reordered, and unit-tested one stage at a
time. It was produced by a behavior-preserving extraction: for every input it produces exactly the
same rectangles, totals, layer assignments, and connector waypoints as the previous implementation,
verified byte for byte by the pipeline-equivalence tests.

#### Layered Pipeline Data Model

`LayeredGraph` is the mutable shared state threaded through every stage. Construction takes the input
`LayerNode` list, the `LayerEdge` list, and a `LayoutDirection`, rejecting null nodes or edges with
`ArgumentNullException`. As the stages run, it accumulates the acyclic edge set and its reversal
flags, per-node layer indices, the augmented node and sub-edge lists (real boxes plus long-edge
dummies), the per-layer node groups, the augmented-node coordinate arrays, the column extents, the
per-sub-edge port positions, the per-sub-edge bend points, and finally the assembled per-edge
waypoints. The `BackEdgeEntryApproach` parameter (default `ConnectorClearance`) lets a
decoration-aware caller lengthen a reversed edge's final approach without disturbing default
geometry. The `SwapNodeAxes` seam swaps each node's width and height for the down/up directions so
the direction-agnostic stages space layers by the correct extent.

Each stage implements `ILayoutStage` (`void Apply(LayeredGraph graph)`) and mutates the graph in
place. Stages are stateless and may be shared across pipelines. `LayeredLayoutMetrics` holds the
shared spacing, clearance, and padding constants — intentionally identical to the constants of the
previous monolithic engine so the pipeline reproduces its output exactly. The `LayoutDirection` enum
selects Right, Down, Left, or Up flow; `HierarchyHandling` selects Flat (supported) or Recursive
(reserved).

#### Layered Pipeline Assembly

A pipeline is assembled through the fluent `LayeredLayoutPipeline.PipelineBuilder` returned by
`Builder()`. The builder exposes `Direction`, `Hierarchy`, `AddStage`, `AddDefaultStages`, and
`Build`. `AddStage` rejects a null stage with `ArgumentNullException`, and `Build` fails fast with
`NotSupportedException` when recursive hierarchy handling is requested. `Run(graph)` first calls
`AxisTransform.NormalizeInputAxes` to normalize the input node axes for the requested direction, then
applies every stage in order. `Run` rejects a null graph with `ArgumentNullException`.

#### Layered Pipeline Stages

The default stage sequence added by `AddDefaultStages` runs in this order:

1. **CycleBreaker.** Detects cycle-causing edges by depth-first back-edge detection and reverses
   them to produce an acyclic edge set, recording which retained edges were reversed. Self-loops and
   duplicate directed edges are dropped.
2. **LayerAssigner.** Assigns each node the longest-path layer index, so every edge runs from a
   strictly lower layer to a strictly higher layer and no same-layer connection is possible.
3. **LongEdgeSplitter.** For each edge spanning more than one layer, inserts one zero-size dummy node
   per intermediate layer so the connector routes through inter-layer corridors rather than through
   intervening boxes. A span-one edge gains no dummy node.
4. **CrossingMinimizer.** Orders the augmented nodes within each layer, using alternating sweep
   passes, to reduce edge crossings while keeping every augmented node in its layer group.
5. **BrandesKopfPlacer.** Assigns every augmented node absolute coordinates, with layer columns
   ordered left to right and symmetric forks centered between their targets.
6. **PortDistributor.** Distributes connector ports along each box face and assigns each sub-edge a
   source-side and target-side port that lies within the corresponding node face. The clearance inset
   is capped at half the face extent so a box too small to hold the full clearance on both edges
   degrades gracefully (ports collapse toward the centre) rather than inverting the clamp range; a
   box at least twice the clearance tall is unaffected, keeping its geometry byte-identical.
7. **LayeredCorridorRouter.** Assigns routing slots per corridor and emits orthogonal bend points, adding
   no bend points for a straight sub-edge. It reads `BackEdgeEntryApproach` to reserve a minimum
   final approach for a reversed edge; at the default this clamp is a no-op, keeping forward geometry
   byte-identical.
8. **LongEdgeJoiner.** Concatenates the bend points of a split edge's sub-edges into one polyline per
   original edge.
9. **AxisTransform.** Maps the abstract left-to-right along/cross coordinates onto screen coordinates
   for the requested direction. The Right direction is the identity; Down, Left, and Up are rotations
   or flips. It also normalizes the input node axes at the start of `Run`.

`ComponentPacker` is an optional composite stage added explicitly by callers that lay out potentially
disconnected graphs. It splits the graph into connected components, runs an inner stage sequence on
each, and packs the results without overlap in a deterministic order, translating each component's
boxes and waypoints together.

#### Layered Pipeline Dependencies

All pipeline types are internal and consume only the geometric value types of the Layout system
(`Point2D`, `Rect`) plus the internal `LayerNode`, `LayerEdge`, `AugNode`, and `AugEdge` records.
No stage depends on the semantic `LayoutGraph` model, any OTS runtime component, or any Shared
Package.

#### Layered Pipeline Callers

The pipeline is assembled and run directly by `InterconnectionLayoutEngine`, and the public
`LayeredLayoutAlgorithm` consumes it transitively through that engine when the layered algorithm is
selected.

#### Layered Pipeline Interactions

The stage sequence collaborates only through the shared `LayeredGraph` state, with
`InterconnectionLayoutEngine` adapting the final rectangles, dimensions, and waypoints to the
public layout result contract.

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-LayeredPipeline-StagedPipeline | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-BehaviorPreserving | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-FlatHierarchyOnly | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-Directions | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-OrthogonalConnectors | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-CycleBreaking | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-LayerAssignment | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-LongEdgeSplitting | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-CrossingMinimization | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-CoordinateAssignment | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-PortDistribution | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-OrthogonalRouting | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-BackEdgeApproach | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-LongEdgeJoining | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-ComponentPacking | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-SharedState | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-InputValidation | Layered pipeline behavior described above |
