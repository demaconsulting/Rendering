### OrthogonalEdgeRouter Unit Design

Part of the Rendering Layout system.

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

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-OrthogonalEdgeRouter-Orthogonal | OrthogonalEdgeRouter behavior described above |
| Rendering-Layout-OrthogonalEdgeRouter-AvoidObstacles | OrthogonalEdgeRouter behavior described above |
| Rendering-Layout-OrthogonalEdgeRouter-Clearance | OrthogonalEdgeRouter behavior described above |
| Rendering-Layout-OrthogonalEdgeRouter-PerpendicularEnds | OrthogonalEdgeRouter behavior described above |
| Rendering-Layout-OrthogonalEdgeRouter-CrossingStatus | OrthogonalEdgeRouter behavior described above |
| Rendering-Layout-OrthogonalEdgeRouter-CostBands | OrthogonalEdgeRouter behavior described above |
