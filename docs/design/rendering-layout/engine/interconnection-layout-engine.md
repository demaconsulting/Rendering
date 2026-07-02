# InterconnectionLayoutEngine Unit Design

Part of the [Rendering Layout](../rendering-layout.md) system.

## InterconnectionLayoutEngine Purpose

`InterconnectionLayoutEngine` places directed graphs and routes all connector lines using a full
Sugiyama-style pipeline. It is a thin facade that assembles and runs the reusable
`LayeredLayoutPipeline` (see *Layered Pipeline*) with its default stage sequence, the Right layout
direction, and flat hierarchy handling. Its `Place` API and `LayerResult` output are the stable
internal contract; the facade produces byte-for-byte identical geometry to the previous monolithic
implementation, proven by an equivalence test against a legacy oracle.

## InterconnectionLayoutEngine Data Model

`InterconnectionLayoutEngine` is a static class with no instance state. Input is an
`IReadOnlyList<LayerNode>` (width and height per node) and an `IReadOnlyList<LayerEdge>` (directed
edges by index). The result is a `LayerResult` record carrying one `Rect` per real node in input
order, the bounding-box totals, a `NodeLayers` list of longest-path layer indices, a
`ConnectorWaypoints` list of orthogonal waypoints, and the `AcyclicEdges` set that is index-aligned
with `ConnectorWaypoints`.

## InterconnectionLayoutEngine Methods

`Place(nodes, edges)` builds a `LayeredGraph` from the inputs, assembles a `LayeredLayoutPipeline`
with the default stages, and runs it. It then reads the placed coordinates, column extents, layer
assignments, and waypoints from the graph state and assembles the `LayerResult`. Because the
pipeline drops self-loops, de-duplicates identical directed pairs, and reverses back edges,
`ConnectorWaypoints` holds one polyline per acyclic edge; consumers key a `(source, target)` lookup
on `AcyclicEdges` (reversing the polyline for a reversed back edge) to recover each input edge's
route.

## InterconnectionLayoutEngine Error Handling

Null `nodes` or `edges` arguments throw `ArgumentNullException`. An empty `nodes` list returns a
minimal-size `LayerResult` with empty lists without performing any computation. Out-of-range edge
indices and self-loops are ignored by the pipeline stages.

## InterconnectionLayoutEngine Interactions

`InterconnectionLayoutEngine` depends on `LayeredLayoutPipeline` and `LayeredGraph` (the staged
pipeline it assembles and runs), the `Rect` value type, and the `Point2D` point type used for
waypoints. It is called by the public `LayeredLayoutAlgorithm` and by the interconnection view
strategy to obtain a placement result.

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-InterconnectionEngine-Layering | InterconnectionLayoutEngine behavior described above |
| Rendering-Layout-InterconnectionEngine-NonOverlapping | InterconnectionLayoutEngine behavior described above |
| Rendering-Layout-InterconnectionEngine-DummyNodes | InterconnectionLayoutEngine behavior described above |
| Rendering-Layout-InterconnectionEngine-Waypoints | InterconnectionLayoutEngine behavior described above |
| Rendering-Layout-InterconnectionEngine-Deterministic | InterconnectionLayoutEngine behavior described above |
