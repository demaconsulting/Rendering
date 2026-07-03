## ConnectorRouter Unit Design

Part of the Rendering Layout system.

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

### ConnectorRouter Dependencies

`ConnectorRouter` depends on the following items:

- **Rendering model** (`DemaConsulting.Rendering`) — the `LayoutBox`, `LayoutLine`, `Point2D`, `Rect`,
  `PortSide`, `EndMarkerStyle`, `LineStyle`, and `EdgeRouting` value types used to describe placed
  boxes and produced route lines.
- **Engine subsystem** (`OrthogonalEdgeRouter` unit) — the internal orthogonal path-finding engine
  invoked by `RouteWithStatus` for the `EdgeRouting.Orthogonal` style. See _OrthogonalEdgeRouter Unit
  Design_.

No OTS runtime component or shared package is consumed.

### ConnectorRouter Callers

`ConnectorRouter` is used by units that have already placed boxes and need to draw connectors among
them:

- **HierarchicalLayoutAlgorithm** — calls `ConnectorRouter.Route` at each hierarchical scope to route
  cross-container edges around sibling containers after the leaf algorithms have placed the
  containers themselves. See _HierarchicalLayoutAlgorithm Unit Design_.
- **External application code** — any caller that supplies its own placed `LayoutBox` list (for
  example from a containment or free-form placement produced outside the layered pipeline) and needs
  routed `LayoutLine` connectors to drop into a `LayoutTree`.

### ConnectorRouter Interactions

`ConnectorRouter` consumes the `LayoutBox`, `LayoutLine`, `Point2D`, `Rect`, `PortSide`,
`EndMarkerStyle`, `LineStyle`, and `EdgeRouting` model types and the internal `OrthogonalEdgeRouter`
engine. It produces `LayoutLine` nodes that a caller drops into a `LayoutTree` alongside the placed
`LayoutBox` nodes for a renderer to draw. It is independent of the layered pipeline and can be used on
any set of placed boxes.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-ConnectorRouter-AnchorsFaceEachOther | ConnectorRouter behavior described above |
| Rendering-Layout-ConnectorRouter-AvoidsObstacles | ConnectorRouter behavior described above |
| Rendering-Layout-ConnectorRouter-ExcludesEndpoints | ConnectorRouter behavior described above |
| Rendering-Layout-ConnectorRouter-CarriesStyling | ConnectorRouter behavior described above |
| Rendering-Layout-ConnectorRouter-BatchOrder | ConnectorRouter behavior described above |
| Rendering-Layout-ConnectorRouter-Validation | ConnectorRouter behavior described above |
