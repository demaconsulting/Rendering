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
geometry. The `NodeSpacing` parameter (default `LayeredLayoutMetrics.NodeSpacing`) is the minimum gap
`BrandesKopfPlacer` enforces between same-layer nodes during horizontal compaction; a caller-supplied
value replaces the pipeline's original fixed constant without disturbing default geometry when left
at its default. The `SwapNodeAxes` seam swaps each node's width and height for the down/up directions so
the direction-agnostic stages space layers by the correct extent; it preserves every other
`LayerNode` field (`Shape`, `RoundedCornerRadius`, `FolderTabWidth`, `FolderTabHeight`, `Label`,
`RealWidth`, `RealHeight`) unchanged, since only the abstract along/cross axes need to reorient.

`LayerNode` carries shape metadata alongside its abstract `Width`/`Height`: `Shape` (defaulting to
`BoxShape.Rectangle`, which keeps every 2-arg call site's byte-identical full-face behavior),
`RoundedCornerRadius`, `FolderTabWidth`, `FolderTabHeight`, and `Label` mirror the corresponding
`LayoutGraphNode`/`LayoutBox` properties, and `RealWidth`/`RealHeight` carry the node's true,
never-swapped bounding-box dimensions (needed because `Width`/`Height` may have been swapped by
`SwapNodeAxes`, but shape geometry such as a note's fold size combines both real dimensions
independently of the abstract axes).

Each stage implements `ILayoutStage` (`void Apply(LayeredGraph graph)`) and mutates the graph in
place. Stages are stateless and may be shared across pipelines. `LayeredLayoutMetrics` holds the
shared spacing, clearance, and padding constants — intentionally identical to the constants of the
previous monolithic engine so the pipeline reproduces its output exactly. The `LayoutDirection` enum
selects Right, Down, Left, or Up flow; `HierarchyHandling` selects `Flat` (a single flat pass) or the
ELK-style `Recursive` compound-graph mode. `Recursive` is no longer a fail-fast placeholder: it
assembles a genuinely runnable stage sequence (via `AddRecursiveStages`) that the hierarchical
algorithm drives in production (through `LayeredLayoutPipeline.RunRecursive`) to lay out a container
together with its nested descendants in one combined pass. To coordinate the container faces that share
a boundary (delegation) port, `AugNode` carries an optional `HierarchyCrossing` descriptor recording
which container face a crossing occupies; the descriptor now has a real producer —
`MergeRegionGraphAssembler` seeds one crossing per boundary face and `LayeredLayoutPipeline.TagCrossings`
sets it on the augmented node after long-edge splitting — yet still defaults to none, so every
flat-graph augmented node — and therefore the flat fast path — is constructed byte-identically to
before it existed.

#### Layered Pipeline Assembly

A pipeline is assembled through the fluent `LayeredLayoutPipeline.PipelineBuilder` returned by
`Builder()`. The builder exposes `Direction`, `Hierarchy`, `AddStage`, `AddDefaultStages`,
`AddRecursiveStages`, and `Build`. `AddStage` rejects a null stage with `ArgumentNullException`.
`AddDefaultStages` assembles the flat stage sequence; `AddRecursiveStages` assembles the recursive
stage sequence for `HierarchyHandling.Recursive`, so `Build` no longer rejects recursive hierarchy
handling with `NotSupportedException`. `Run(graph)` first calls `AxisTransform.NormalizeInputAxes` to
normalize the input node axes for the requested direction, then applies every stage in order. `Run`
rejects a null graph with `ArgumentNullException`. `RunRecursive(region)` is the recursive entry
point: it assembles every level graph of an `AssembledMergeRegion` (via `MergeRegionGraphAssembler`)
and drives the same ELK stage sequence per level — innermost first for crossing coordination —
substituting the recursive `LayerAssigner.AssignLayersRecursive` and
`CrossingMinimizer.MinimizeCrossingsRecursive` for their flat counterparts so a container's boundary
faces and its children's ordering are decided together across levels. It returns a
`RecursiveLayoutResult` carrying each level's placed graph and its boundary crossings.

#### Layered Pipeline Stages

The default stage sequence added by `AddDefaultStages` runs in this order:

1. **CycleBreaker.** Detects cycle-causing edges by depth-first back-edge detection and reverses
   them to produce an acyclic edge set, recording which retained edges were reversed. Self-loops are
   always dropped. Duplicate directed edges (after back-edge reversal) are dropped only when
   `LayeredGraph.MergeParallelEdges` is `true` (the default, exactly reproducing the original
   unconditional deduplication); when `false`, every parallel edge instance is retained in
   `graph.Acyclic`, and `graph.AcyclicOriginalIndex` records each retained acyclic edge's index back
   into the original input edge list so later stages and the public algorithm can still recover which
   original edge produced which routed polyline.
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
   box at least twice the clearance tall is unaffected, keeping its geometry byte-identical. For a
   non-`BoxShape.Rectangle` real node, the port band is further restricted to the shape's usable
   connectable extents on the resolved real face (proportionally distributed across multiple
   disjoint extents when the shape excludes a middle portion of the face), reusing `ConnectorRouter`'s
   shape-geometry resolution via `ShapeAnchorSupport`; a plain-`Rectangle` node keeps the original
   full-span formula untouched.
7. **LayeredCorridorRouter.** Assigns routing slots per corridor and emits orthogonal bend points, adding
   no bend points for a straight sub-edge. It reads `BackEdgeEntryApproach` to reserve a minimum
   final approach for a reversed edge; at the default this clamp is a no-op, keeping forward geometry
   byte-identical.
8. **LongEdgeJoiner.** Concatenates the bend points of a split edge's sub-edges into one polyline per
   original edge. For a non-`BoxShape.Rectangle` real endpoint, the assembled perpendicular endpoint
   coordinate is additionally projected inward by the shape's surface-projection offset at that
   endpoint's local face coordinate (for example a folder's tab height), so the connector touches the
   shape's real outline rather than the plain bounding-box edge; a plain-`Rectangle` endpoint skips
   geometry resolution entirely and keeps the original formula byte-identical.
9. **AxisTransform.** Maps the abstract left-to-right along/cross coordinates onto screen coordinates
   for the requested direction. The Right direction is the identity; Down, Left, and Up are rotations
   or flips. It also normalizes the input node axes at the start of `Run`.

`ComponentPacker` is an optional composite stage added explicitly by callers that lay out potentially
disconnected graphs. It splits the graph into connected components, runs an inner stage sequence on
each, and packs the results without overlap in a deterministic order, translating each component's
boxes and waypoints together. Each component is laid out through a freshly constructed child
`LayeredGraph`, which copies the parent graph's `BackEdgeEntryApproach` so a caller-customized
reversed-edge clearance is honored consistently whether the input graph is packed into one component
or several.

#### Layered Pipeline Boundary Ports

Several internal helpers in `Engine/Layered` implement the hierarchical algorithm's boundary
(delegation) ports through one combined recursive pass rather than a post-hoc reconciliation of
separately-placed scopes. `HierarchyMergeRegionBuilder` detects boundary ports structurally: a
container's port is a boundary port when an edge inside that container's own child scope references the
port — the inward delegation edge is the signal. Its `Collect` reports a single scope's boundary ports
(ignoring same-scope ports and leaf-node ports), and its `CollectRecursive` walks the whole hierarchy
so detection is general, transitive, and depth-unbounded.

`MergeRegionGraphAssembler` assembles a detected container and all of its nested descendants into an
`AssembledMergeRegion` of per-level graphs, flattening every interior node into its level and seeding
one zero-size crossing dummy per boundary face (an incoming `Internal`-face dummy that relays inward to
each internal delegation target, and an outgoing `External`-face dummy between the interior approachers
and the container node). It records each seeded crossing as a `LevelCrossing` so the boundary faces
carry their `HierarchyCrossing(port, face)` identity through the pipeline; a multi-port container yields
exactly one child level whose incoming boundaries are all of that container's ports.

`RunRecursive` then drives every level through the ELK stage sequence with recursive layer assignment
and crossing minimization: `LayerAssigner.AssignLayersRecursive` assigns level-local layers per level
and recurses into children, while `CrossingMinimizer.MinimizeCrossingsRecursive` performs a genuine
two-direction hierarchical sweep — an up-sweep that seeds a parent level's same-face crossing order from
its resolved child (`Internal`-face) order, and a down-sweep that pins the child's `Internal` crossings
to the parent's resolved `External` order and re-minimizes the child interior with a forward-only
propagation. `LongEdgeSplitter` carries each pre-seeded `HierarchyCrossing` tag through the
augmented-node rebuild so a crossing dummy stays a tagged terminal hop and is never split; for the flat
path no crossing is seeded, so the rebuild is byte-identical.

`MergeRegionDecomposer` projects the placed combined result back into per-scope geometry. It resolves
each boundary port to one shared physical anchor on the container face — the face given by
`BoundaryPortResolver.FaceForDirection`, keyed on the boundary port's reference identity so the port's
external and internal faces collapse onto one point — and takes every converging edge's waypoints
directly from the orthogonal corridor router's routed polylines (the external approach concatenates the
approach and container-link polylines; each internal delegation prepends the shared anchor to its
delegation polyline with at most one orthogonal corner). Boundary containers recurse to arbitrary depth;
leaf and non-boundary nodes rigid-shift into place. No endpoint is patched onto the anchor with a
hand-built diagonal, which is what keeps external fan-in and multi-level delegation chains orthogonal.
`BoundaryPortResolver` itself is now reduced to the single retained `FaceForDirection` helper (the
direction→face mapping the decomposer's anchor placement shares); its former reconciliation code and the
`OrderCrossings` ordering it once performed are superseded by the recursive crossing minimizer above.

#### Layered Pipeline Dependencies

All pipeline types are internal and consume only the geometric value types of the Layout system
(`Point2D`, `Rect`) plus the internal `LayerNode`, `LayerEdge`, `AugNode`, and `AugEdge` records.
No stage depends on the semantic `LayoutGraph` model, any OTS runtime component, or any Shared
Package. The one exception is the `PortDistributor` and `LongEdgeJoiner` stages' dependency, via the
small internal `ShapeAnchorSupport` helper, on `ConnectorRouter`'s internal shape-geometry types
(`IBoxShapeGeometry`, `ResolveShapeGeometry`, `BuildUsableExtents`, `TotalExtentLength`,
`CoordinateAtDistance`) for non-`BoxShape.Rectangle` nodes — a documented cross-unit dependency
(`LayeredPipeline` unit → `ConnectorRouter` unit) that lets a shaped node's ports and endpoints reuse
`ConnectorRouter`'s already-tested extent-restriction and surface-projection rules instead of
duplicating them. See *ConnectorRouter Unit Design*'s "Callers" section for the reverse-direction
documentation of this dependency.

#### Layered Pipeline Callers

The pipeline is assembled and run directly by `InterconnectionLayoutEngine`, and the public
`LayeredLayoutAlgorithm` consumes it transitively through that engine when the layered algorithm is
selected. The boundary-port helpers (`HierarchyMergeRegionBuilder`, `MergeRegionGraphAssembler`,
`RunRecursive`, and `MergeRegionDecomposer`) are consumed by `HierarchicalLayoutAlgorithm` to detect a
container's boundary ports, lay the container and its descendants out in one combined pass, and project
the result back into per-scope geometry.

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
| Rendering-Layout-LayeredPipeline-RecursiveCombinedPass | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-HierarchyCrossingDescriptor | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-BoundaryPortDetection | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-BoundaryPortResolution | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-Directions | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-OrthogonalConnectors | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-CycleBreaking | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-MergeParallelEdges | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-LayerAssignment | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-LongEdgeSplitting | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-CrossingMinimization | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-CoordinateAssignment | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-PortDistribution | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-OrthogonalRouting | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-BackEdgeApproach | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-LongEdgeJoining | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-ComponentPacking | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-PackedComponentsBackEdgeApproach | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-SharedState | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-InputValidation | Layered pipeline behavior described above |
| Rendering-Layout-LayeredPipeline-ShapeAwareAnchors | Layered pipeline behavior described above |
