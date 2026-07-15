### InterconnectionLayoutEngine Unit Design

Part of the Rendering Layout system.

#### InterconnectionLayoutEngine Purpose

`InterconnectionLayoutEngine` places directed graphs and routes all connector lines using a full
Sugiyama-style pipeline. It is a thin facade that assembles and runs the reusable
`LayeredLayoutPipeline` (see *Layered Pipeline*) with its default stage sequence, the requested layout
direction (defaulting to Right), and flat hierarchy handling. Its `Place` API and `LayerResult` output
are the stable internal contract; for the default Right direction the facade produces byte-for-byte
identical geometry to the previous monolithic implementation, proven by an equivalence test against a
legacy oracle.

#### InterconnectionLayoutEngine Data Model

`InterconnectionLayoutEngine` is a static class with no instance state. Input is an
`IReadOnlyList<LayerNode>` (width and height per node) and an `IReadOnlyList<LayerEdge>` (directed
edges by index). The result is a `LayerResult` record carrying one `Rect` per real node in input
order, the bounding-box totals, a `NodeLayers` list of longest-path layer indices, a
`ConnectorWaypoints` list of orthogonal waypoints, and the `AcyclicEdges` set that is index-aligned
with `ConnectorWaypoints`.

#### InterconnectionLayoutEngine Methods

`Place(nodes, edges, direction, nodeSpacing)` builds a `LayeredGraph` from the inputs, wraps the
default stage sequence with `Engine.Layered.ComponentPacker.WithDefaultStages(nodeSpacing)` for the
requested `direction` (defaulting to Right), and runs it — so a disconnected input is automatically
split into its connected components, each laid out independently, and packed without overlap, with no
special action required by the caller (see the Layered Pipeline Unit Design document's `ComponentPacker`
section). The optional `nodeSpacing` argument (defaulting to the engine's original fixed constant)
is stored on the `LayeredGraph` and consumed by the Brandes-Köpf placement stage as the minimum gap
between same-layer nodes; omitting it reproduces the engine's original geometry exactly. It then reads
the placed coordinates, column extents, layer assignments, and waypoints from the graph state and
assembles the `LayerResult`. Because the pipeline drops self-loops, de-duplicates identical directed
pairs, and reverses back edges, `ConnectorWaypoints` holds one polyline per acyclic edge; consumers key
a `(source, target)` lookup on `AcyclicEdges` (reversing the polyline for a reversed back edge) to
recover each input edge's route.

The reported total dimensions are direction-aware. The pipeline's axis-transform stage places the
nodes along the requested direction, so a top-to-bottom (Down) or bottom-to-top (Up) flow transposes
the layout relative to the left-to-right (Right) and right-to-left (Left) flows. The engine computes
the along-axis extent from the column geometry and the cross-axis extent from the placed screen
coordinates, then assigns them to `TotalWidth`/`TotalHeight` per direction: for Right/Left the
along-axis is the width and for Down/Up it is the height. The Right path is unchanged and remains
byte-identical.

#### InterconnectionLayoutEngine Error Handling

Null `nodes` or `edges` arguments throw `ArgumentNullException`. An empty `nodes` list returns a
minimal-size `LayerResult` with empty lists without performing any computation. Out-of-range edge
indices and self-loops are ignored by the pipeline stages.

#### InterconnectionLayoutEngine Interactions

`InterconnectionLayoutEngine` depends on `LayeredLayoutPipeline` and `LayeredGraph` (the staged
pipeline it assembles and runs), the `Rect` value type, and the `Point2D` point type used for
waypoints. It is called by the public `LayeredLayoutAlgorithm` and by the interconnection view
strategy to obtain a placement result.

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-InterconnectionEngine-Layering | InterconnectionLayoutEngine behavior described above |
| Rendering-Layout-InterconnectionEngine-NonOverlapping | InterconnectionLayoutEngine behavior described above |
| Rendering-Layout-InterconnectionEngine-DummyNodes | InterconnectionLayoutEngine behavior described above |
| Rendering-Layout-InterconnectionEngine-Waypoints-AcyclicMapping | See above |
| Rendering-Layout-InterconnectionEngine-Waypoints-StraightSpanOne | See above |
| Rendering-Layout-InterconnectionEngine-Waypoints-LongEdgeRouting | See above |
| Rendering-Layout-InterconnectionEngine-Direction-RequestedFlow | See above |
| Rendering-Layout-InterconnectionEngine-Direction-TransposedTotals | See above |
| Rendering-Layout-InterconnectionEngine-Direction-DefaultsToRight | See above |
| Rendering-Layout-InterconnectionEngine-NodeSpacing | See above |
| Rendering-Layout-InterconnectionEngine-Deterministic | InterconnectionLayoutEngine behavior described above |
