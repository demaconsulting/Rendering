# LayeredLayoutAlgorithm Unit Design

Part of the Rendering Layout system.

## LayeredLayoutAlgorithm Purpose

`LayeredLayoutAlgorithm` is the public `ILayoutAlgorithm` implementation and the system's product
boundary. It arranges an input `LayoutGraph` into Sugiyama layers, routes edges orthogonally, and
produces a placed `LayoutTree` of boxes and connectors. It wraps the reusable layered pipeline via
`InterconnectionLayoutEngine`.

## LayeredLayoutAlgorithm Data Model

The class is stateless and sealed. It exposes the `AlgorithmId` constant (`"layered"`) and returns it
from the `Id` property, the stable identifier under which the algorithm is selected and registered.
Its single behavior is `Apply(LayoutGraph graph, LayoutOptions options)`, which returns a
`LayoutTree` carrying the total width and height and a flat list of `LayoutNode` items (`LayoutBox`
per node followed by `LayoutLine` per edge).

## LayeredLayoutAlgorithm Methods

`Apply(graph, options)` rejects null `graph` or `options` with `ArgumentNullException`, then:

1. **Index mapping.** Assigns each `LayoutGraphNode` a positional index and builds the
   `LayerNode` array from node sizes.
2. **Edge mapping.** Maps each edge to an index pair, dropping any edge that references a node
   outside this graph.
3. **Direction resolution.** Resolves the requested flow direction from `CoreOptions.Direction`,
   taking an explicit value on the graph in preference to one on the options and falling back to the
   property default (`Right`), then maps the public `LayoutFlowDirection` to the engine's internal
   direction. This mirrors how the algorithm resolves its other well-known options.
4. **Placement.** Calls `InterconnectionLayoutEngine.Place` with the resolved direction to obtain the
   `LayerResult`. For a downward or upward flow the engine transposes the layout so the layers
   progress top-to-bottom (or bottom-to-top); `Right` is the default and is byte-identical to the
   original left-to-right placement.
5. **Box emission.** Emits one `LayoutBox` per input node, in input order, at the placed rectangle,
   carrying the node label.
6. **Route resolution.** Builds a `(source, target)` to polyline lookup from the engine's acyclic
   edge set, then emits one `LayoutLine` per input edge. `ResolveRoute` returns the forward polyline
   when present, reverses the polyline of a reversed back edge, and otherwise falls back to a
   straight segment between the two node centers (for a self-loop or duplicate edge the engine
   dropped) so the connector is still drawn.

An empty graph yields an empty `LayoutTree` because `InterconnectionLayoutEngine.Place` returns a
minimal-size empty result, which produces an empty canvas.

## LayeredLayoutAlgorithm Interactions

`LayeredLayoutAlgorithm` depends on the `ILayoutAlgorithm`, `LayoutGraph`, `LayoutTree`, and related
model types from `DemaConsulting.Rendering.Abstractions` and on `InterconnectionLayoutEngine` from
the Engine subsystem. It is the entry point resolved by renderers through the layout registry.

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-LayeredAlgorithm-Identity | LayeredLayoutAlgorithm behavior described above |
| Rendering-Layout-LayeredAlgorithm-PlacesAndRoutes | LayeredLayoutAlgorithm behavior described above |
| Rendering-Layout-LayeredAlgorithm-EmptyGraph | LayeredLayoutAlgorithm behavior described above |
| Rendering-Layout-LayeredAlgorithm-Direction | LayeredLayoutAlgorithm behavior described above |
| Rendering-Layout-LayeredAlgorithm-Validation | LayeredLayoutAlgorithm behavior described above |
