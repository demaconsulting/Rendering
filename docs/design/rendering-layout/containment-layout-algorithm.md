# ContainmentLayoutAlgorithm Unit Design

Part of the [Rendering Layout](rendering-layout.md) system.

## ContainmentLayoutAlgorithm Purpose

`ContainmentLayoutAlgorithm` is a second public `ILayoutAlgorithm` implementation alongside
`LayeredLayoutAlgorithm`. Where the layered algorithm arranges nodes by their connectivity into
Sugiyama layers, the containment algorithm arranges them by their reading order: it packs the graph's
top-level nodes into rows within a heuristic width budget and then routes each edge around the packed
boxes. It composes the two public building blocks of the system — the `ContainmentLayout` packer and
the `ConnectorRouter` orchestration — rather than the layered pipeline, and suits views whose elements
group as peers inside a container rather than flowing along a directed spine. It is additive: adding it
changes no existing output and leaves the layered algorithm untouched.

## ContainmentLayoutAlgorithm Data Model

The class is stateless and sealed. It exposes the `AlgorithmId` constant (`"containment"`) and returns
it from the `Id` property, the stable identifier under which the algorithm is selected and registered.
Two private constants govern the arrangement: `CanvasAspectRatio` (`4/3`) biases the derived content
width toward a landscape block, and `NodeSpacing` (`24.0` logical pixels) is the inter-box gap, sized
wider than the router's approach stub so a connector can pass cleanly between two packed boxes. Its
single behavior is `Apply(LayoutGraph graph, LayoutOptions options)`, which returns a `LayoutTree`
carrying the packed region size and a flat list of `LayoutNode` items (`LayoutBox` per top-level node
followed by `LayoutLine` per routed edge).

## ContainmentLayoutAlgorithm Methods

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

## ContainmentLayoutAlgorithm Error Handling

Null `graph` or `options` throw `ArgumentNullException`. Edges with an out-of-graph endpoint are
skipped rather than treated as errors. All other behavior is inherited from the composed
`ContainmentLayout` and `ConnectorRouter` units.

## ContainmentLayoutAlgorithm Interactions

`ContainmentLayoutAlgorithm` depends on the `ILayoutAlgorithm`, `LayoutGraph`, `LayoutTree`,
`CoreOptions`, and related model types, and composes the public `ContainmentLayout` and
`ConnectorRouter` units of this same system. It is resolvable by renderers and callers through the
layout registry under the `"containment"` identifier, selected via `CoreOptions.Algorithm`.

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-ContainmentAlgorithm-Identity | ContainmentLayoutAlgorithm behavior described above |
| Rendering-Layout-ContainmentAlgorithm-PacksNodes | ContainmentLayoutAlgorithm behavior described above |
| Rendering-Layout-ContainmentAlgorithm-RoutesEdges | ContainmentLayoutAlgorithm behavior described above |
| Rendering-Layout-ContainmentAlgorithm-RoutesAroundObstacle | ContainmentLayoutAlgorithm behavior described above |
| Rendering-Layout-ContainmentAlgorithm-EmptyGraph | ContainmentLayoutAlgorithm behavior described above |
| Rendering-Layout-ContainmentAlgorithm-SkipsOutOfGraphEdges | ContainmentLayoutAlgorithm behavior described above |
| Rendering-Layout-ContainmentAlgorithm-Validation | ContainmentLayoutAlgorithm behavior described above |
