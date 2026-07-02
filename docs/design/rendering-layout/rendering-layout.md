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
further reusable engines — `OrthogonalEdgeRouter` (single-connector orthogonal routing) and
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
│   ├── OrthogonalEdgeRouter (Unit)
│   ├── ContainmentPacker (Unit)
│   └── InterconnectionLayoutEngine (Unit)
├── LayeredPipeline (Unit)
├── LayeredLayoutAlgorithm (Unit)
├── ContainmentLayoutAlgorithm (Unit)
├── HierarchicalLayoutAlgorithm (Unit)
├── ConnectorRouter (Unit)
├── ContainmentLayout (Unit)
├── LayoutAlgorithms (Unit)
└── LayoutEngine (Unit)
```

The engine subsystem operates on the public `Rect` geometry value type, which lives in the
`DemaConsulting.Rendering` model alongside `Point2D` and `PortSide` rather than in this system. The
`LayeredPipeline` unit is documented as a single cohesive unit: it is the whole `Engine/Layered`
stage collection (the shared graph state, the stage interface, the pipeline builder, the enums, the
metrics, and each ordered stage), grouped because the stages are only meaningful as an assembled
pipeline and are verified together.

### Folder Layout

The source folders mirror the software structure above:

```text
src/DemaConsulting.Rendering.Layout/
├── Engine/
│   ├── OrthogonalEdgeRouter.cs           - Single orthogonal connector router
│   ├── ContainmentPacker.cs              - Shelf (row) bin-packing engine
│   ├── InterconnectionLayoutEngine.cs    - facade over the layered pipeline
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
│       ├── LayeredCorridorRouter.cs           - Emits orthogonal bend points
│       ├── LongEdgeJoiner.cs             - Joins sub-edges into one polyline
│       ├── AxisTransform.cs              - Maps abstract axes to screen axes
│       └── ComponentPacker.cs            - Lays out and packs components
├── LayeredLayoutAlgorithm.cs             - Public ILayoutAlgorithm entry point
├── ContainmentLayoutAlgorithm.cs         - Public ILayoutAlgorithm packing nodes and routing edges
├── HierarchicalLayoutAlgorithm.cs        - Public ILayoutAlgorithm recursive hierarchical engine
├── ConnectorRouter.cs                    - Public connector routing orchestration (Connection, ConnectorRouteOptions)
├── ContainmentLayout.cs                  - Public containment packing (ContainmentOptions, ContainmentResult)
├── LayoutAlgorithms.cs                   - Default layout-algorithm registry factory
└── LayoutEngine.cs                       - Public batteries-included layout facade
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
model and operates purely on sizes, edges, anchors, and rectangles. The `Rect` value type they
consume is the public axis-aligned rectangle in logical pixels (`X`, `Y`, `Width`, `Height`) defined
by the `DemaConsulting.Rendering` model and returned by the placement engines.

### OrthogonalEdgeRouter

#### OrthogonalEdgeRouter Purpose

`OrthogonalEdgeRouter` routes a single orthogonal connector between a source anchor and a target anchor,
steering around obstacle rectangles and keeping a requested clearance. It is the engine through
which all single-connector routing quality flows.

#### OrthogonalEdgeRouter Data Model

`OrthogonalEdgeRouter` is a static class with no instance state. Inputs are the source and target `Point2D`
anchors, a list of obstacle `Rect`, a clearance distance, optional source and target `PortSide`
values, and an optional list of `CostBand` records. The result is a `RouteResult` record carrying
the ordered `Waypoints` and a `Crossed` flag.

#### OrthogonalEdgeRouter Methods

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

#### OrthogonalEdgeRouter Error Handling

Null `source`, `target`, or `obstacles` arguments throw `ArgumentNullException`. Degenerate geometry
never throws: when no clean route exists the router returns a crossing route with `Crossed = true`
rather than failing, leaving the decision to surface a warning to the caller.

#### OrthogonalEdgeRouter Interactions

`OrthogonalEdgeRouter` depends only on the `Point2D` and `Rect` geometric value types and the `PortSide`
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

## ConnectorRouter

### ConnectorRouter Purpose

`ConnectorRouter` is the public routing-orchestration entry point for connecting boxes that some
caller has already placed. Given the placed boxes and a list of connections, it produces one routed
`LayoutLine` per connection, choosing boundary anchors, assembling per-connection obstacle sets, and
delegating the actual path to the router selected by the `EdgeRouting` style. It complements
`LayeredLayoutAlgorithm`: where the algorithm places and routes a whole graph, `ConnectorRouter`
routes connectors among boxes whose positions are already fixed (for example a containment or
free-form placement produced outside the layered pipeline).

### ConnectorRouter Data Model

The unit comprises three public types plus the `EdgeRouting` option:

- `Connection(From, To, TargetEnd, LineStyle, Label)` — an immutable record naming the source and
  target `LayoutBox` (by instance identity) and the styling to carry onto the routed line. It holds
  no geometry and no domain concept; anchors are derived from the boxes' placement at routing time.
- `ConnectorRouteOptions(EdgeRouting, Clearance)` — an immutable record selecting the routing style
  (default `EdgeRouting.Orthogonal`) and the obstacle clearance in logical pixels (default `12.0`,
  caller-overridable).
- `ConnectorRouter` — a stateless static class exposing a batch `Route(boxes, connections, options)`
  and a single-connection `Route(boxes, connection, options)` convenience overload.

`EdgeRouting` is the closed routing-style vocabulary defined in the `DemaConsulting.Rendering` model.
It mirrors ELK's `elk.edgeRouting` and today carries the single value `Orthogonal`. The style is also
exposed on the open property system as `CoreOptions.EdgeRouting` (id `rendering.edgerouting`, default
`Orthogonal`), so routing can be selected per scope alongside `CoreOptions.Algorithm`.

### ConnectorRouter Methods

`Route(boxes, connection, options)` rejects null arguments (including a null `From` or `To`) with
`ArgumentNullException`, then:

1. **Anchor selection.** Computes each box centre and, for each endpoint, chooses the midpoint of the
   box side whose outward normal best points at the opposing box centre (right/left when the
   horizontal separation dominates, otherwise bottom/top). The chosen `PortSide` is retained so the
   route exits and enters perpendicular to the face.
2. **Obstacle set.** Builds a `Rect` per box, excluding the connection's two endpoint boxes matched
   by reference identity, so the connector is free to leave and enter the boxes it joins.
3. **Dispatch.** Routes through the router realizing `options.EdgeRouting`. Today `Orthogonal` maps to
   the internal `OrthogonalEdgeRouter.RouteWithStatus`, which is the implementation behind the enum
   value and remains internal to the Layout system. The dispatch is a single-arm switch structured so
   new styles slot in additively.
4. **Assembly.** Wraps the returned waypoints in a `LayoutLine` carrying the connection's `TargetEnd`,
   `LineStyle`, and `Label`, with `SourceEnd` left `None`.

The batch overload applies the single-connection routine to each connection and returns one line per
connection in input order.

### ConnectorRouter Error Handling

Null `boxes`, `connections`, `connection`, `options`, or a connection's `From`/`To` throw
`ArgumentNullException`. An `EdgeRouting` value with no shipped router throws `NotSupportedException`;
this is unreachable today because `Orthogonal` is the only enum value, but guards future additions.

### ConnectorRouter Interactions

`ConnectorRouter` consumes the `LayoutBox`, `LayoutLine`, `Point2D`, `Rect`, `PortSide`,
`EndMarkerStyle`, `LineStyle`, and `EdgeRouting` model types and the internal `OrthogonalEdgeRouter`
engine. It produces `LayoutLine` nodes that a caller drops into a `LayoutTree` alongside the placed
`LayoutBox` nodes for a renderer to draw. It is independent of the layered pipeline and can be used on
any set of placed boxes.

## ContainmentLayout

### ContainmentLayout Purpose

`ContainmentLayout` is the public, model-speaking containment building block: it packs a set of
already-sized `LayoutBox` children into a single container region, arranging them into rows within a
width budget. It complements `ConnectorRouter` and `LayeredLayoutAlgorithm`: where the algorithm places
and routes a whole connected graph and `ConnectorRouter` joins already-placed boxes, `ContainmentLayout`
arranges peer boxes inside a container when their reading order, not their connectivity, drives the
layout (for example the members of a package or the contents of a folder). It is the single-level packing
primitive; multi-level folder/canvas assembly is composed from it by higher layers rather than provided
here.

### ContainmentLayout Data Model

The unit comprises the public static class plus two records:

- `ContainmentOptions(MaxContentWidth, HorizontalGap, VerticalGap, Padding)` — an immutable record
  selecting the row-wrap content width and the spacing. `MaxContentWidth` is required; `HorizontalGap`
  and `VerticalGap` default to `8.0` and `Padding` defaults to `12.0` logical pixels. The names and
  defaults mirror ELK's content-area, `spacing.nodeNode`, and `padding` vocabulary.
- `ContainmentResult(Width, Height, Children)` — an immutable record carrying the enclosing region size
  (including outer padding) and the input boxes repositioned to their packed, region-relative
  coordinates, in input order.
- `ContainmentLayout` — a stateless static class exposing `Pack(children, options)`.

### ContainmentLayout Methods

`Pack(children, options)` rejects null arguments — a null `children` list, null `options`, or any null
child element — with `ArgumentNullException`, then:

1. **Size mapping.** Maps each child onto a size-only `PackItem` built from its `Width` and `Height`,
   correlating the packer's output back to each child by index.
2. **Packing.** Calls the internal `ContainmentPacker.Pack` with the mapped items and the options'
   content width, gaps, and padding to obtain the packed rectangles and region size.
3. **Repositioning.** Produces one new `LayoutBox` per child via `with { X = ..., Y = ... }`, updating
   only the coordinates from the corresponding packed rectangle and carrying every other field (label,
   depth, shape, compartments, nested children, keyword) through unchanged.
4. **Assembly.** Returns a `ContainmentResult` with the region `Width`/`Height` and the repositioned
   children in input order.

The operation is deterministic and order-preserving, never overlaps two children, keeps every child
within the reported region (which includes the outer padding on every side), places a child wider than
the content width alone on its own row while widening the region to contain it, and returns a
padding-only region for an empty input.

### ContainmentLayout Error Handling

Null `children`, `options`, or a null child element throw `ArgumentNullException`. Packing behavior for
degenerate sizes (zero or negative dimensions) follows the underlying `ContainmentPacker`; the public
operation adds no further validation beyond null rejection.

### ContainmentLayout Interactions

`ContainmentLayout` consumes the `LayoutBox` model type and the internal `ContainmentPacker`,
`PackItem`, `PackedRect`, and `PackResult` engine types, which remain internal to the Layout system —
the public API speaks only `LayoutBox`. It produces `LayoutBox` children that a caller nests inside a
container box (offsetting by the container's placement) and drops into a `LayoutTree` for a renderer to
draw. It is independent of the layered pipeline and can be used on any set of sized boxes.

## ContainmentLayoutAlgorithm

### ContainmentLayoutAlgorithm Purpose

`ContainmentLayoutAlgorithm` is a second public `ILayoutAlgorithm` implementation alongside
`LayeredLayoutAlgorithm`. Where the layered algorithm arranges nodes by their connectivity into
Sugiyama layers, the containment algorithm arranges them by their reading order: it packs the graph's
top-level nodes into rows within a heuristic width budget and then routes each edge around the packed
boxes. It composes the two public building blocks of the system — the `ContainmentLayout` packer and
the `ConnectorRouter` orchestration — rather than the layered pipeline, and suits views whose elements
group as peers inside a container rather than flowing along a directed spine. It is additive: adding it
changes no existing output and leaves the layered algorithm untouched.

### ContainmentLayoutAlgorithm Data Model

The class is stateless and sealed. It exposes the `AlgorithmId` constant (`"containment"`) and returns
it from the `Id` property, the stable identifier under which the algorithm is selected and registered.
Two private constants govern the arrangement: `CanvasAspectRatio` (`4/3`) biases the derived content
width toward a landscape block, and `NodeSpacing` (`24.0` logical pixels) is the inter-box gap, sized
wider than the router's approach stub so a connector can pass cleanly between two packed boxes. Its
single behavior is `Apply(LayoutGraph graph, LayoutOptions options)`, which returns a `LayoutTree`
carrying the packed region size and a flat list of `LayoutNode` items (`LayoutBox` per top-level node
followed by `LayoutLine` per routed edge).

### ContainmentLayoutAlgorithm Methods

`Apply(graph, options)` rejects null `graph` or `options` with `ArgumentNullException`, then:

1. **Leaf-box conversion.** Converts each top-level `LayoutGraphNode` into a leaf `LayoutBox` at the
   origin (carrying the node's width, height, and label), recording each node's positional index. A
   node's nested `Children` are treated as opaque and are not laid out at this level (that is the
   recursive hierarchical engine's responsibility, not this flat algorithm's).
2. **Content-width heuristic.** Derives the packer's `MaxContentWidth` from the boxes: the square root
   of their total area scaled by `CanvasAspectRatio`, widened to at least the widest box, and floored
   to a small positive value so the packer always receives a usable width — even for an empty graph.
3. **Packing.** Calls `ContainmentLayout.Pack` with the leaf boxes and a `ContainmentOptions` using the
   derived width and the connector-aware `NodeSpacing` on both axes, obtaining the packed boxes and the
   enclosing region size.
4. **Connection building.** Builds one `Connection` per edge whose source and target are both top-level
   nodes — carrying the edge's `TargetEnd`, `LineStyle`, and `Label` — using the packed box that
   represents each endpoint. Edges referencing a node outside the graph's top-level nodes are skipped,
   mirroring the layered algorithm's handling of out-of-graph endpoints.
5. **Routing.** Routes the connections around the packed boxes via `ConnectorRouter.Route`, selecting
   the routing style from `CoreOptions.EdgeRouting` on the supplied options (default `Orthogonal`).
6. **Assembly.** Returns a `LayoutTree` with the region `Width`/`Height` and the packed boxes followed
   by the routed lines.

An empty graph yields an empty `LayoutTree` with a positive-size canvas, because the packer returns a
padding-only region for no children and no connections are routed.

### ContainmentLayoutAlgorithm Error Handling

Null `graph` or `options` throw `ArgumentNullException`. Edges with an out-of-graph endpoint are
skipped rather than treated as errors. All other behavior is inherited from the composed
`ContainmentLayout` and `ConnectorRouter` units.

### ContainmentLayoutAlgorithm Interactions

`ContainmentLayoutAlgorithm` depends on the `ILayoutAlgorithm`, `LayoutGraph`, `LayoutTree`,
`CoreOptions`, and related model types, and composes the public `ContainmentLayout` and
`ConnectorRouter` units of this same system. It is resolvable by renderers and callers through the
layout registry under the `"containment"` identifier, selected via `CoreOptions.Algorithm`.

## HierarchicalLayoutAlgorithm

### HierarchicalLayoutAlgorithm Purpose

`HierarchicalLayoutAlgorithm` is a third public `ILayoutAlgorithm` implementation: the recursive
hierarchical layout engine, analogous to ELK's `RecursiveGraphLayoutEngine`. Where the layered and
containment algorithms place a single flat scope, this engine lays out a *compound* graph — a graph
whose nodes may be containers of nested subgraphs — by recursively placing each container's children
and composing the sub-layouts into one absolute `LayoutTree`. It does not place boxes itself; it
selects a bundled *leaf* algorithm per scope and delegates the actual placement to it, then sizes each
container and composes the results. It is additive: it changes no existing output and is honored only
when a caller selects it by name.

### HierarchicalLayoutAlgorithm Data Model

The class is sealed and stateless with respect to any single layout. It exposes the `AlgorithmId`
constant (`"hierarchical"`) and returns it from `Id`. Two private constants govern container framing:
`ContainerPadding` (`12.0` logical pixels) is the inset kept on every side between a container border
and its children's sub-layout, and `ContainerTitleHeight` (`24.0` logical pixels) is the title band
reserved above the children of a container that carries a `Label` (a container with no label reserves
no band). The engine holds a single field, a `LayoutAlgorithmRegistry` used to resolve the per-scope
leaf algorithm by identifier. A default constructor builds a default registry containing the
bundled `LayeredLayoutAlgorithm` and `ContainmentLayoutAlgorithm`; an injecting constructor accepts a
caller-supplied registry (rejecting null). The engine is deliberately never registered into its own
registry, so recursion always terminates in a leaf algorithm.

### HierarchicalLayoutAlgorithm Methods

`Apply(graph, options)` rejects null `graph` or `options` with `ArgumentNullException`, resolves the
root scope's algorithm (the graph's explicit `CoreOptions.Algorithm` override if present, otherwise the
options' algorithm — default `"layered"`), and calls the recursive `LayoutScope`. `LayoutScope(graph,
algoId, options)` performs:

1. **Flat fast path (equivalence guarantee).** If no direct node of the scope is a container
   (`HasChildren` is false for all), the engine delegates straight to the resolved leaf algorithm and
   returns its `LayoutTree` unchanged — no cloning, no post-processing, no mutation. This guarantees a
   flat graph is placed byte-for-byte identically to invoking the leaf algorithm directly.
2. **Post-order recursion.** For each container child, the engine resolves the child's algorithm (the
   node's `CoreOptions.Algorithm` override, else the inherited scope algorithm), recursively lays out
   the child's subgraph, and records both the sub-layout and the container's effective size — the
   sub-layout size grown by `ContainerPadding` on every side plus a title band when the container is
   labelled.
3. **Sized view.** The engine builds an internal, side-effect-free *view* graph with the same nodes in
   the same order (container nodes carrying their effective size, leaves their own size, labels copied)
   and only the edges whose endpoints are both direct members of this scope. The caller's input graph
   is never mutated.
4. **Placement.** The resolved leaf algorithm places the sized view, emitting one box per node in input
   order followed by routed lines for the in-scope edges.
5. **Composition.** Each container's placed box receives its recursively laid-out children, translated
   from their local origin to the box's padded (and title-offset) interior via a recursive `Translate`
   that shifts nested boxes and line waypoints (local-to-absolute translation, following the
   `ComponentPacker` precedent).
6. **Cross-container (LCA) routing.** Edges whose endpoints resolve to different direct-member
   containers of this scope — mapped from any descendant endpoint up to its owning top-level box — are
   routed at this level with `ConnectorRouter.Route`, steering around the sibling boxes; the
   `EdgeRouting` style is read from the options. Edges already routed by the leaf algorithm (both
   endpoints direct) or belonging to a lower scope (both endpoints under one container) are skipped.
7. **Assembly.** The engine returns a `LayoutTree` with the leaf algorithm's canvas size for this level
   and the composed boxes followed by the leaf-routed lines and the cross-container lines.

### HierarchicalLayoutAlgorithm Design Constraints

- The flat-graph equivalence guarantee is load-bearing: the fast path must delegate directly to the
  leaf algorithm and must not clone the graph or transform the tree, so selecting the engine never
  changes existing output.
- The engine shall not mutate the caller's graph; re-sizing is expressed through the internal sized
  view rather than by altering node sizes in place.
- Hierarchy handling is `HierarchyHandling.SeparateChildren` (see the *Rendering Model* design): each
  container is laid out in isolation and sized to fit its children. The `CoreOptions.HierarchyHandling`
  option records this selection; only the separate-children mode is honored today.

### HierarchicalLayoutAlgorithm Error Handling

Null `graph`, `options`, or (injecting constructor) `registry` throw `ArgumentNullException`. A scope
that selects an algorithm identifier absent from the registry surfaces the registry's
`KeyNotFoundException`. Edges whose endpoints are not under the current scope are skipped rather than
treated as errors.

### HierarchicalLayoutAlgorithm Interactions

`HierarchicalLayoutAlgorithm` depends on the `ILayoutAlgorithm`, `LayoutAlgorithmRegistry`,
`LayoutGraph`, `LayoutGraphNode`, `LayoutTree`, `CoreOptions`, and related model types, and composes
the public `ConnectorRouter` unit for cross-container routing and the bundled leaf algorithms
(`LayeredLayoutAlgorithm`, `ContainmentLayoutAlgorithm`) for per-scope placement. It is resolvable by
renderers and callers through the layout registry under the `"hierarchical"` identifier, selected via
`CoreOptions.Algorithm`.

## LayoutAlgorithms and LayoutEngine

### DefaultLayout Purpose

`LayoutAlgorithms` and `LayoutEngine` form the batteries-included happy path: the smallest possible way
to lay out a graph with the algorithm it declares. `LayoutAlgorithms` is a factory for a
`LayoutAlgorithmRegistry` pre-populated with the three bundled algorithms; `LayoutEngine` is a thin
facade that resolves the declared algorithm and applies it. Together they turn "lay out my graph with
whatever algorithm it declares" into one call that correctly handles both flat and nested graphs, with
no registry assembly or engine choice required of the caller. Both units are additive: they compose the
existing algorithms and change no existing behavior.

### DefaultLayout Data Model

Both units are static and hold no per-call state. `LayoutAlgorithms.CreateDefaultRegistry()` builds a
fresh `LayoutAlgorithmRegistry` and registers `LayeredLayoutAlgorithm` (`"layered"`),
`ContainmentLayoutAlgorithm` (`"containment"`), and `HierarchicalLayoutAlgorithm` (`"hierarchical"`),
returning a new, independently mutable instance on each call. `LayoutEngine` exposes the
`DefaultAlgorithmId` constant (`"hierarchical"`) and holds one private static `LayoutAlgorithmRegistry`
built once from `CreateDefaultRegistry()`; because the bundled algorithms are stateless, that shared
registry is safe to read (resolve) concurrently.

### DefaultLayout Methods

`LayoutEngine.Layout(graph, options)` resolves against the shared default registry;
`LayoutEngine.Layout(graph, options, registry)` resolves against a caller-supplied registry. Both reject
null arguments with `ArgumentNullException`, then:

1. **Resolve the algorithm identifier.** The identifier is read from an explicit `CoreOptions.Algorithm`
   on the graph, else from an explicit `CoreOptions.Algorithm` on the options, else `DefaultAlgorithmId`
   (`"hierarchical"`). Resolution consults *explicit* settings only (via `TryGet`), so an unset graph and
   options fall through to the hierarchical default rather than the `CoreOptions.Algorithm` property
   default of `"layered"`. The graph takes precedence over the options because, in the ELK-style model,
   layout options are naturally attached to the graph being laid out.
2. **Resolve and apply.** The identifier is resolved from the registry and the resolved algorithm's
   `Apply(graph, options)` produces the placed `LayoutTree`.

Defaulting to the hierarchical engine is what lets the single facade serve both flat and nested graphs.
It is safe because of the hierarchical engine's flat-graph equivalence guarantee: for a graph with no
container nodes the engine returns output byte-for-byte identical to the selected leaf algorithm
(default `"layered"`) applied directly. A flat graph therefore lays out exactly as the layered algorithm
would, while a nested graph is composed correctly — with no decision required from the caller.

### DefaultLayout Design Constraints

- The factory shall live in the Layout package, not in Abstractions, because it references the concrete
  bundled algorithms; the `LayoutAlgorithmRegistry` it populates remains in Abstractions. This keeps the
  dependency direction intact (model &lt;- Abstractions &lt;- Layout).
- The facade shall default to the hierarchical engine, not the layered algorithm, so one entry point
  handles both flat and nested graphs; the flat-graph equivalence guarantee makes this behavior-
  preserving.
- The facade shall consult only explicit algorithm declarations when resolving, so an unset graph and
  options reach the hierarchical default rather than the layered property default.

### DefaultLayout Error Handling

Null `graph`, `options`, or (three-argument overload) `registry` throw `ArgumentNullException`. A
declared algorithm identifier absent from the resolving registry surfaces the registry's
`KeyNotFoundException`.

### DefaultLayout Interactions

`LayoutAlgorithms` depends on `LayoutAlgorithmRegistry` and the three bundled algorithm units.
`LayoutEngine` depends on `LayoutAlgorithms`, `LayoutAlgorithmRegistry`, `LayoutGraph`, `LayoutOptions`,
`LayoutTree`, and `CoreOptions`. Callers typically pair `LayoutEngine.Layout(...)` with an `IRenderer`
(for example `SvgRenderer`) to go from graph to rendered output in two calls.
