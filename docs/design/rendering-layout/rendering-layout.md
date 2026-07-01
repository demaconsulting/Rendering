## Rendering.Layout Design

This document provides the design for the Rendering.Layout software system (package
`DemaConsulting.Rendering.Layout`). It covers the reusable geometric layout engines, the composable
ELK-style layered pipeline assembled from them, and the public layout algorithm that renderers
consume.

### Overview

Rendering.Layout is the placement engine of the rendering stack. A caller supplies an abstract
`LayoutGraph` (nodes with sizes and directed edges) plus `LayoutOptions`; the system returns a
placed `LayoutTree` of boxes and orthogonal connectors that a renderer can draw without any layout
knowledge of its own.

The system is organized around an ELK-inspired, staged layered layout. A `LayeredGraph` carries the
mutable state; an ordered sequence of small, single-purpose stages transforms that state from raw
nodes and edges into fully placed boxes and routed connectors. Two facades sit in front of the
pipeline: `InterconnectionLayoutEngine` adapts it to the internal geometric result contract, and the
public `LayeredLayoutAlgorithm` adapts it to the `ILayoutAlgorithm` contract used by renderers. Two
further reusable engines — `ChannelRouter` (single-connector orthogonal routing) and
`ContainmentPacker` (row packing of child elements) — serve callers that need geometric primitives
outside the layered pipeline.

Every engine computes from plain geometric inputs (sizes, edges, anchors) and is deterministic:
identical input yields identical geometry on every invocation. The layered pipeline was produced by
a behavior-preserving extraction from a previous monolithic engine and reproduces its geometry byte
for byte.

### Software Structure

The system decomposes into one engine subsystem plus two top-level units, as shown below. Test
projects and classes are excluded from the design because they validate the product code rather than
form part of it.

```text
Rendering.Layout (System)
├── Engine (Subsystem)
│   ├── ChannelRouter (Unit)
│   ├── ContainmentPacker (Unit)
│   ├── InterconnectionLayoutEngine (Unit)
│   └── Rect (Unit)
├── LayeredPipeline (Unit)
└── LayeredLayoutAlgorithm (Unit)
```

`Rect` is the shared axis-aligned rectangle value type used across the engine subsystem. The
`LayeredPipeline` unit is documented as a single cohesive unit: it is the whole `Engine/Layered`
stage collection (the shared graph state, the stage interface, the pipeline builder, the enums, the
metrics, and each ordered stage), grouped because the stages are only meaningful as an assembled
pipeline and are verified together.

### Folder Layout

The source folders mirror the software structure above:

```text
src/DemaConsulting.Rendering.Layout/
├── Engine/
│   ├── ChannelRouter.cs                  - Single orthogonal connector router
│   ├── ContainmentPacker.cs              - Shelf (row) bin-packing engine
│   ├── InterconnectionLayoutEngine.cs    - facade over the layered pipeline
│   ├── Rect.cs                           - Shared axis-aligned rectangle type
│   └── Layered/
│       ├── ILayoutStage.cs               - Common stage interface
│       ├── LayeredGraph.cs               - Mutable shared pipeline state
│       ├── LayeredLayoutPipeline.cs      - Fluent builder and stage runner
│       ├── LayeredLayoutMetrics.cs       - Shared spacing and clearance constants
│       ├── LayoutDirection.cs            - Flow-direction enum
│       ├── HierarchyHandling.cs          - Nested-node handling enum
│       ├── CycleBreaker.cs               - Reverses cycle-causing edges
│       ├── LayerAssigner.cs              - Longest-path layer assignment
│       ├── LongEdgeSplitter.cs           - Inserts dummy nodes for long edges
│       ├── CrossingMinimizer.cs          - Orders nodes within each layer
│       ├── BrandesKopfPlacer.cs          - Assigns absolute coordinates
│       ├── PortDistributor.cs            - Distributes ports along box faces
│       ├── OrthogonalRouter.cs           - Emits orthogonal bend points
│       ├── LongEdgeJoiner.cs             - Joins sub-edges into one polyline
│       ├── AxisTransform.cs              - Maps abstract axes to screen axes
│       └── ComponentPacker.cs            - Lays out and packs components
└── LayeredLayoutAlgorithm.cs             - Public ILayoutAlgorithm entry point
```

Each software item in the structure above has corresponding artifacts in parallel directory trees:

- Requirements: `docs/reqstream/rendering-layout/rendering-layout.yaml`
- Design docs: `docs/design/rendering-layout/rendering-layout.md`
- Verification design: `docs/verification/rendering-layout/rendering-layout.md`
- Source code: `src/DemaConsulting.Rendering.Layout/...`
- Tests: `test/DemaConsulting.Rendering.Layout.Tests/...`
- Review-sets: defined in `.reviewmark.yaml`

## Engine Subsystem

The Engine subsystem holds the reusable geometric components. Each is independent of any semantic
model and operates purely on sizes, edges, anchors, and rectangles. The shared `Rect` value type is
an axis-aligned rectangle in logical pixels (`X`, `Y`, `Width`, `Height`) returned by the placement
engines.

### ChannelRouter

#### ChannelRouter Purpose

`ChannelRouter` routes a single orthogonal connector between a source anchor and a target anchor,
steering around obstacle rectangles and keeping a requested clearance. It is the engine through
which all single-connector routing quality flows.

#### ChannelRouter Data Model

`ChannelRouter` is a static class with no instance state. Inputs are the source and target `Point2D`
anchors, a list of obstacle `Rect`, a clearance distance, optional source and target `PortSide`
values, and an optional list of `CostBand` records. The result is a `RouteResult` record carrying
the ordered `Waypoints` and a `Crossed` flag.

#### ChannelRouter Methods

`RouteWithStatus(source, target, obstacles, clearance, sourceSide?, targetSide?, costBands?)`
computes the route and reports whether it had to cross an obstacle. The algorithm is:

1. **Perpendicular stubs.** When a side is supplied, the anchor is stepped off its edge by a short
   stub so the connector leaves and enters boxes at right angles. Each stub length is capped to half
   the gap to the opposing anchor so two facing stubs across a narrow gap meet at the midline
   instead of overshooting.
2. **Grid construction.** Candidate grid lines are built from the two endpoint coordinates plus each
   obstacle's near and far edges offset outward by the current clearance.
3. **Clearance-retry ladder.** An A\*-style search runs over the grid at successively smaller
   clearances — full, half, quarter, then zero. Segments passing within the current clearance of an
   obstacle are rejected; the largest clearance yielding an obstacle-free path is used.
4. **Crossing fallback.** Only when no obstacle-free path exists at any clearance (for example an
   enclosed target) does the router fall back to a best-effort L-shape and set `Crossed = true`.
5. **Finalize.** The original anchors are re-attached outside their stubs and the path is
   simplified — collinear interior points are removed while U-turns are preserved so a perpendicular
   stub is never collapsed.

A turn penalty biases the search toward routes with fewer bends. When cost bands are supplied, each
segment's length is scaled by the cheapest band covering its midpoint, so a discounted highway band
attracts wires into shared corridors while a null band list leaves cost neutral. The thin `Route`
wrapper returns only the `Waypoints` for callers that do not need the crossing status.

#### ChannelRouter Error Handling

Null `source`, `target`, or `obstacles` arguments throw `ArgumentNullException`. Degenerate geometry
never throws: when no clean route exists the router returns a crossing route with `Crossed = true`
rather than failing, leaving the decision to surface a warning to the caller.

#### ChannelRouter Interactions

`ChannelRouter` depends only on the `Point2D` and `Rect` geometric value types and the `PortSide`
enumeration for perpendicular-stub direction. It is a leaf engine invoked directly by callers that
route individual connectors; the `Crossed` flag feeds their layout-warning handling.

### ContainmentPacker

#### ContainmentPacker Purpose

`ContainmentPacker` arranges a sequence of variable-size items into rows within a width budget. It
places items left to right, wraps to a new row when the next item would exceed the maximum content
width, and sizes the enclosing region to fit all items plus uniform outer padding. It is used to
pack child elements inside a containing box in a compact, ordered grid.

#### ContainmentPacker Data Model

`ContainmentPacker` is a static class with no instance state. Inputs are a list of `PackItem`
records (each a `Width` and `Height`), a `maxContentWidth`, a `horizontalGap`, a `verticalGap`, and
a `padding`. The result is a `PackResult` record carrying the region `Width`, `Height`, and the
ordered list of `PackedRect` rectangles, one per input item in input order, each positioned relative
to the region origin `(0, 0)`.

#### ContainmentPacker Methods

`Pack(items, maxContentWidth, horizontalGap, verticalGap, padding)` computes the packing as a single
left-to-right shelf (row) pass:

1. **Degenerate case.** An empty item list returns a region of `2 * padding` on each axis with no
   rectangles.
2. **Row filling.** A horizontal cursor starts at the left padding offset. Each item is placed at
   the current cursor and the cursor advances past the item plus `horizontalGap`. The row height
   tracks the tallest item placed so far.
3. **Wrapping.** Before placing an item that is not first in its row, the packer checks whether its
   right edge would exceed `padding + maxContentWidth`. If so, it drops to a new row (advancing the
   row top by the row height plus `verticalGap`), resets the cursor, and places the item there.
   Because the first-in-row item is exempt from the check, an item wider than the content width is
   placed alone on its own row and the region width grows to contain it.
4. **Region sizing.** The total width is the widest row's right edge plus padding; the total height
   is the last row's bottom plus padding.

Input order is preserved, and the left-to-right, no-backtracking placement is what guarantees that
no two rectangles overlap and that every rectangle stays within the reported region.

#### ContainmentPacker Error Handling

A null `items` argument throws `ArgumentNullException`. An empty item list returns a padding-only
region. No other input causes a throw; an oversized item is handled by the first-in-row exemption
rather than by an error.

#### ContainmentPacker Interactions

`ContainmentPacker` depends only on the `PackItem`, `PackedRect`, and `PackResult` value types
declared alongside it. It is a leaf engine invoked by callers that pack child elements inside a
containing box, using the returned rectangles to position children and the region size to size the
container.

### InterconnectionLayoutEngine

#### InterconnectionLayoutEngine Purpose

`InterconnectionLayoutEngine` places directed graphs and routes all connector lines using a full
Sugiyama-style pipeline. It is a thin facade that assembles and runs the reusable
`LayeredLayoutPipeline` (see *Layered Pipeline*) with its default stage sequence, the Right layout
direction, and flat hierarchy handling. Its `Place` API and `LayerResult` output are the stable
internal contract; the facade produces byte-for-byte identical geometry to the previous monolithic
implementation, proven by an equivalence test against a legacy oracle.

#### InterconnectionLayoutEngine Data Model

`InterconnectionLayoutEngine` is a static class with no instance state. Input is an
`IReadOnlyList<LayerNode>` (width and height per node) and an `IReadOnlyList<LayerEdge>` (directed
edges by index). The result is a `LayerResult` record carrying one `Rect` per real node in input
order, the bounding-box totals, a `NodeLayers` list of longest-path layer indices, a
`ConnectorWaypoints` list of orthogonal waypoints, and the `AcyclicEdges` set that is index-aligned
with `ConnectorWaypoints`.

#### InterconnectionLayoutEngine Methods

`Place(nodes, edges)` builds a `LayeredGraph` from the inputs, assembles a `LayeredLayoutPipeline`
with the default stages, and runs it. It then reads the placed coordinates, column extents, layer
assignments, and waypoints from the graph state and assembles the `LayerResult`. Because the
pipeline drops self-loops, de-duplicates identical directed pairs, and reverses back edges,
`ConnectorWaypoints` holds one polyline per acyclic edge; consumers key a `(source, target)` lookup
on `AcyclicEdges` (reversing the polyline for a reversed back edge) to recover each input edge's
route.

#### InterconnectionLayoutEngine Error Handling

Null `nodes` or `edges` arguments throw `ArgumentNullException`. An empty `nodes` list returns a
minimal-size `LayerResult` with empty lists without performing any computation. Out-of-range edge
indices and self-loops are ignored by the pipeline stages.

#### InterconnectionLayoutEngine Interactions

`InterconnectionLayoutEngine` depends on `LayeredLayoutPipeline` and `LayeredGraph` (the staged
pipeline it assembles and runs), the `Rect` value type, and the `Point2D` point type used for
waypoints. It is called by the public `LayeredLayoutAlgorithm` and by the interconnection view
strategy to obtain a placement result.

## Layered Pipeline

### Layered Pipeline Overview

The layered pipeline is a reusable, composable layered-layout engine that reproduces ELK's layered
(Sugiyama-style) algorithm. It replaces a single monolithic placement method with an ordered
sequence of small, single-purpose stages. Each stage reads the state produced by earlier stages and
writes the state it owns, so the pipeline can be extended, reordered, and unit-tested one stage at a
time. It was produced by a behavior-preserving extraction: for every input it produces exactly the
same rectangles, totals, layer assignments, and connector waypoints as the previous implementation,
verified byte for byte by the pipeline-equivalence tests.

### Layered Pipeline Data Model

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

### Layered Pipeline Assembly

A pipeline is assembled through the fluent `LayeredLayoutPipeline.PipelineBuilder` returned by
`Builder()`. The builder exposes `Direction`, `Hierarchy`, `AddStage`, `AddDefaultStages`, and
`Build`. `AddStage` rejects a null stage with `ArgumentNullException`, and `Build` fails fast with
`NotSupportedException` when recursive hierarchy handling is requested. `Run(graph)` first calls
`AxisTransform.NormalizeInputAxes` to normalize the input node axes for the requested direction, then
applies every stage in order. `Run` rejects a null graph with `ArgumentNullException`.

### Layered Pipeline Stages

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
   source-side and target-side port that lies within the corresponding node face.
7. **OrthogonalRouter.** Assigns routing slots per corridor and emits orthogonal bend points, adding
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

### Layered Pipeline Interactions

All types are internal and consume only the geometric value types of the Layout system (`Point2D`,
`Rect`) plus the internal `LayerNode`, `LayerEdge`, `AugNode`, and `AugEdge` records; no stage
references any semantic model. The pipeline is assembled and run by `InterconnectionLayoutEngine`
and, transitively, by the public `LayeredLayoutAlgorithm`.

## LayeredLayoutAlgorithm

### LayeredLayoutAlgorithm Purpose

`LayeredLayoutAlgorithm` is the public `ILayoutAlgorithm` implementation and the system's product
boundary. It arranges an input `LayoutGraph` into Sugiyama layers, routes edges orthogonally, and
produces a placed `LayoutTree` of boxes and connectors. It wraps the reusable layered pipeline via
`InterconnectionLayoutEngine`.

### LayeredLayoutAlgorithm Data Model

The class is stateless and sealed. It exposes the `AlgorithmId` constant (`"layered"`) and returns it
from the `Id` property, the stable identifier under which the algorithm is selected and registered.
Its single behavior is `Apply(LayoutGraph graph, LayoutOptions options)`, which returns a
`LayoutTree` carrying the total width and height and a flat list of `LayoutNode` items (`LayoutBox`
per node followed by `LayoutLine` per edge).

### LayeredLayoutAlgorithm Methods

`Apply(graph, options)` rejects null `graph` or `options` with `ArgumentNullException`, then:

1. **Index mapping.** Assigns each `LayoutGraphNode` a positional index and builds the
   `LayerNode` array from node sizes.
2. **Edge mapping.** Maps each edge to an index pair, dropping any edge that references a node
   outside this graph.
3. **Placement.** Calls `InterconnectionLayoutEngine.Place` to obtain the `LayerResult`.
4. **Box emission.** Emits one `LayoutBox` per input node, in input order, at the placed rectangle,
   carrying the node label.
5. **Route resolution.** Builds a `(source, target)` to polyline lookup from the engine's acyclic
   edge set, then emits one `LayoutLine` per input edge. `ResolveRoute` returns the forward polyline
   when present, reverses the polyline of a reversed back edge, and otherwise falls back to a
   straight segment between the two node centers (for a self-loop or duplicate edge the engine
   dropped) so the connector is still drawn.

An empty graph yields an empty `LayoutTree` because `InterconnectionLayoutEngine.Place` returns a
minimal-size empty result, which produces an empty canvas.

### LayeredLayoutAlgorithm Interactions

`LayeredLayoutAlgorithm` depends on the `ILayoutAlgorithm`, `LayoutGraph`, `LayoutTree`, and related
model types from `DemaConsulting.Rendering.Abstractions` and on `InterconnectionLayoutEngine` from
the Engine subsystem. It is the entry point resolved by renderers through the layout registry.
