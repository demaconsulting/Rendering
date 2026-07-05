### OrthogonalEdgeRouter Unit Design

Part of the Rendering Layout system.

#### OrthogonalEdgeRouter Purpose

`OrthogonalEdgeRouter` routes a single orthogonal connector between a source anchor and a target anchor,
steering around obstacle rectangles and keeping a requested clearance. It is the engine through
which all single-connector routing quality flows.

#### OrthogonalEdgeRouter Data Model

`OrthogonalEdgeRouter` is a static class with no instance state. Inputs are the source and target
`Point2D` anchors, a list of obstacle `Rect`, a clearance distance, optional source and target
`PortSide` values, an optional list of `CostBand` records, and an optional list of soft-obstacle
`Rect` values (typically already-routed connector segments). The result is a `RouteResult` record
carrying the ordered `Waypoints` and a `Crossed` flag.

#### OrthogonalEdgeRouter Methods

`RouteWithStatus(source, target, obstacles, clearance, sourceSide?, targetSide?, costBands?,
softObstacles?)`
computes the route and reports whether it had to cross an obstacle. The algorithm is:

1. **Perpendicular stubs.** When a side is supplied, the anchor is stepped off its edge by a short
   stub so the connector leaves and enters boxes at right angles. Each stub length is capped to half
   the gap to the opposing anchor so two facing stubs across a narrow gap meet at the midline
   instead of overshooting.
2. **Grid construction.** Candidate grid lines are built from the two endpoint coordinates plus each
   obstacle's near and far edges offset outward by the current clearance; optional soft-obstacle
   edges are also added so the search has candidate lanes on either side of an already-routed line.
3. **Clearance-retry ladder.** An A\*-style search runs over the grid at successively smaller
   clearances — full, half, quarter, then zero. Segments passing within the current clearance of an
   obstacle are rejected; the largest clearance yielding an obstacle-free path is used.
4. **Crossing fallback.** Only when no obstacle-free path exists at any clearance (for example an
   enclosed target) does the router fall back to a best-effort L-shape and set `Crossed = true`.
5. **Finalize.** The original anchors are re-attached outside their stubs and the path is
   simplified — collinear interior points are removed while U-turns are preserved so a perpendicular
   stub is never collapsed. A final defensive cleanup removes any exact leave-and-return excursion
   that revisits one waypoint before continuing, so callers never receive a visibly redundant loop
   even if a future soft-obstacle pattern reintroduces one.

A turn penalty biases the search toward routes with fewer bends. When cost bands are supplied, each
segment's length is scaled by the cheapest band covering its midpoint, so a discounted highway band
attracts wires into shared corridors while a null band list leaves cost neutral. The thin `Route`
wrapper returns only the `Waypoints` for callers that do not need the crossing status. Soft obstacles
add a penalty rather than a hard block, which is what keeps a shared box face reachable even when one
connector's interior corridor has already been claimed by an earlier route. That penalty is
proportional to the length of the overlap between the candidate move and the soft obstacle, not a flat
per-move cost: a flat penalty was tried first and found insufficient, because on a sparse narrow-gap
grid a connector's entire multi-hundred-pixel corridor can collapse into a single grid move, so a flat
cost priced a long visual overlap identically to a trivial one and was always cheaper than the
roughly fixed cost of a lane-change detour. Scaling the penalty by overlap length keeps a short,
incidental overlap cheap while making an extended overlap cost substantially more than detouring to a
free lane, so parallel connectors separate into distinct corridors instead of merging along a shared
trunk. The redundant leave-and-
return regression arose when endpoint-adjacent approach legs were also contributed as soft obstacles:
those are exactly the segments that several connectors may legitimately share when converging on one
face, so penalizing them lured the search into a pointless excursion away from a usable approach point
and back again. `ConnectorRouter` now omits those endpoint-adjacent segments from the soft-obstacle
set, while the final revisit cleanup remains as a defensive last line of defense.

#### OrthogonalEdgeRouter Error Handling

Null `source`, `target`, or `obstacles` arguments throw `ArgumentNullException`. Degenerate geometry
never throws: when no clean route exists the router returns a crossing route with `Crossed = true`
rather than failing, leaving the decision to surface a warning to the caller.

#### OrthogonalEdgeRouter Dependencies

`OrthogonalEdgeRouter` depends on the following items:

- **Rendering model** (`DemaConsulting.Rendering`) — the `Point2D` value type for anchors, the
  `Rect` value type for obstacles, the `PortSide` enumeration for perpendicular stub direction, and
  the `CostBand` record for corridor cost biasing.
- **.NET base class library** — no other runtime dependency.

No OTS runtime component or shared package is consumed.

#### OrthogonalEdgeRouter Callers

`OrthogonalEdgeRouter` is a leaf engine invoked wherever a single connector must be routed
orthogonally through an obstacle field:

- **ConnectorRouter** — dispatches to `RouteWithStatus` for every connection routed under the
  `EdgeRouting.Orthogonal` style. See _ConnectorRouter Unit Design_.
- **LayeredPipeline** (`OrthogonalRouter` stage) — routes individual layered-pipeline edges through
  the same engine so pipeline routes and free-form routes share one implementation. See _Layered
  Pipeline Unit Design_.

The `Crossed` flag returned by `RouteWithStatus` feeds each caller's layout-warning handling.

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
| Rendering-Layout-OrthogonalEdgeRouter-NoWaypointRevisit | OrthogonalEdgeRouter behavior described above |
| Rendering-Layout-OrthogonalEdgeRouter-AvoidsExtendedSoftOverlap | OrthogonalEdgeRouter behavior described above |
