### OrthogonalEdgeRouter Unit Design

Part of the Rendering Layout system.

#### OrthogonalEdgeRouter Purpose

`OrthogonalEdgeRouter` routes a single orthogonal connector between a source anchor and a target anchor,
steering around obstacle rectangles and keeping a requested clearance. It is the engine through
which all single-connector routing quality flows.

#### OrthogonalEdgeRouter Data Model

`OrthogonalEdgeRouter` is a static class with no instance state. Inputs are the source and target
`Point2D` anchors, a list of obstacle `Rect`, a clearance distance, optional source and target
`PortSide` values, an optional list of `CostBand` records, an optional list of soft-obstacle
`Rect` values (typically already-routed connector segments) and optional `RouteHints`. The result is a
`RouteResult` record carrying the ordered `Waypoints`, a `Crossed` flag, and the search `Cost` and
`ClearanceUsed` of the route it found (so a caller can compare alternative routes produced by the same
cost function). The supporting internal types are:

- `RouteHints` — optional quality hints: soft minimum and comfort end legs for each end
  (`SourceLeg`/`TargetLeg`, `SourceComfort`/`TargetComfort`), a list of `RouterWall` keep-out strips and a
  `CrossingPenalty`, and a hard `MinInteriorSegment` (shortest segment between two bends, no reversal).
  A route computed without hints (or with all-zero hints) is exactly the legacy route.
- `RouterWall(IsVertical, Coordinate, Lo, Hi, OutwardIsPositive)` — one container border: a direction-selective
  obstacle that blocks runs parallel to the wall (and bends) within twice the current clearance
  but never a move across it.
- `PortRange(Side, Face, Preferred, Intervals, Extra, FixedPoint)` — where along one face the connector
  may attach (see `RoutePorts` below); `PortRange.Single` wraps one fixed anchor.
- `RoutingWorkBudget` / `RoutingWorkLimits` / `RoutingTier` — the deterministic work account of one layout
  scope: the soft, hard and weight limits, the work spent so far, the effort tier (`Full`, `Reduced`,
  `Minimal`) that follows from them, and whether the scope was reduced by its own size or took the reduction
  over from the oversize scope that holds it (see *Work budget* below). It lives in `Engine/RoutingWorkBudget.cs`.

#### OrthogonalEdgeRouter Methods

`RouteWithStatus(source, target, obstacles, clearance, sourceSide?, targetSide?, costBands?,
softObstacles?, hints?, budget?)`
computes the route and reports whether it had to cross an obstacle. `RoutePorts(sourceRange, targetRange,
obstacles, clearance, costBands?, softObstacles?, hints?, budget?)` is the same search with the ports left to the
router: every grid line along each face that lies inside the range's permitted intervals (plus the
preferred position) is a candidate port, the search starts from every candidate source stub end and
finishes at any candidate target stub end, and each candidate carries a small extra cost per pixel of
displacement from the preferred position (slightly above one per pixel, so a port only moves when that
removes bends, and the preferred, centered anchor wins every tie). Both share one implementation. The
algorithm is:

1. **Perpendicular stubs.** When a side is supplied, the anchor is stepped off its edge by a short
   stub so the connector leaves and enters boxes at right angles. Each stub length is capped to half
   the gap to the opposing anchor so two facing stubs across a narrow gap meet at the midline
   instead of overshooting. Two ends on the same side do not face each other: when they are level
   along the face (for example two boxes with aligned left edges joined left to left) the cap would
   collapse both stubs to zero and leave a straight segment lying on the faces, so such an end keeps
   its full stub and the connector wraps around.
2. **Grid construction.** Candidate grid lines are built from the two endpoint coordinates plus each
   obstacle's near and far edges offset outward by the current clearance; optional soft-obstacle
   edges are also added so the search has candidate lanes on either side of an already-routed line.
3. **Clearance-retry ladder.** An A\*-style search runs over the grid at successively smaller
   clearances — full, half, quarter, then zero. Segments passing within the current clearance of an
   obstacle are rejected; the largest clearance yielding an obstacle-free path is used. The search
   frontier is held in `MinHeap`, a four-ary minimum-priority queue owned by the engine rather than
   `System.Collections.Generic.PriorityQueue`, which is unavailable on the `netstandard2.0` target.
   Owning it keeps a single implementation on every target framework, so states that tie on priority
   are expanded in the same order everywhere and a diagram routes identically wherever it runs.
4. **Crossing fallback.** Only when no obstacle-free path exists at any clearance (for example an
   enclosed target) does the router fall back to a best-effort L-shape and set `Crossed = true`.
5. **Finalize.** The original anchors are re-attached outside their stubs and the path is
   simplified — collinear interior points are removed while U-turns are preserved so a perpendicular
   stub is never collapsed. A final defensive cleanup removes any exact leave-and-return excursion
   that revisits one waypoint before continuing, so callers never receive a visibly redundant loop
   even if a future soft-obstacle pattern reintroduces one.

A turn penalty biases the search toward routes with fewer bends. **Hints** add, per clearance level, the
following soft or directional terms (none of which changes a route when the hint is absent):

- *End-leg zones.* A bend (any move lateral to an end's face normal) whose distance from that face is below
  the end's minimum leg is charged 100, below its comfort leg 30; grid lines are added at the zone
  boundaries so a bend can sit exactly one leg from the face. The zones are soft, so a gap too narrow to
  hold them still yields the legacy half-gap stub route.
- *Wall keep-out.* A move parallel to a wall within twice the clearance of it (strictly) that overlaps the
  wall's extent is blocked; a move across a wall, or touching the connector's own stub-end nodes, never is.
  The margin therefore narrows with the clearance ladder (24, 12, 6, 0) so a narrow channel is still
  routable. Grid lines are added at the wall coordinate plus and minus the margin and at the centre of each
  corridor between two walls facing each other, and a move parallel to nearby walls earns a cost discount of
  at most 5 % the farther it stays from the nearest one (up to five clearances), so equally good lanes resolve
  to the middle of a corridor instead of hugging a border.
- *Crossing penalty.* Each right-angle crossing of a soft obstacle (an earlier connector's segment, detected
  from its thin aspect ratio) adds the hint's penalty on top of the usual soft-obstacle cost.
- *Minimum interior segment* (`MinInteriorSegment`). A route's interior segments — those between two bends —
  must be at least the hint's length, so a Z never carries a few-pixel jog. The search state widens from
  (grid node, axis) to (grid node, axis, run, sign): `sign` is the direction of the last move along its axis
  and `run` is the grid index where the run being drawn began while it is still shorter than the minimum
  (otherwise -1). A turn is forbidden while a short run is pending, and a move that reverses `sign` along its
  own axis is forbidden — without the sign a path could "satisfy" the minimum by going 24 px left and
  coming back right. The two end legs are exempt, but only where they really are the end legs: a run that
  continues the step-off stub (first run along the start stub's axis, last run along the goal stub's axis)
  is part of the end leg, while a run across the stub axis is an interior segment and is measured (a goal
  reached in the middle of a short run across its stub is not accepted). The requirement is a hard
  constraint of a first pass that tries every clearance level; only when that pass finds no route is it
  dropped and the unconstrained passes run (so the hint can improve a route but never lose it or turn it
  into a crossing fallback). With a zero or absent hint the state's two extra components are constant, a
  single pass runs, and the route is byte-identical to the legacy one.

A multi-port search ends at a *done* state per goal that carries the goal's extra cost, so the cheapest
total wins; a search over a single goal without extra cost ends at the first dequeued goal node exactly as
before. The thin `Route` wrapper returns only the `Waypoints` for callers that do not need the crossing
status. When cost bands are supplied, each
segment's length is scaled by the cheapest band covering its midpoint, so a discounted highway band
attracts wires into shared corridors while a null band list leaves cost neutral. Soft obstacles
add a penalty rather than a hard block, which is what keeps a shared box face reachable even when one
connector's interior corridor has already been claimed by an earlier route. That penalty is
proportional to the length of the overlap between the candidate move and the soft obstacle, not a flat
per-move cost: a flat penalty was tried first and found insufficient, because on a sparse narrow-gap
grid a connector's entire multi-hundred-pixel corridor can collapse into a single grid move, so a flat
cost priced a long visual overlap identically to a trivial one and was always cheaper than the
roughly fixed cost of a lane-change detour. Scaling the penalty by overlap length keeps a short,
incidental overlap cheap while making an extended overlap cost substantially more than detouring to a
free lane, so parallel connectors separate into distinct corridors instead of merging along a shared
trunk. The redundant leave-and-return
regression arose when endpoint-adjacent approach legs were also contributed as soft obstacles:
those are exactly the segments that several connectors may legitimately share when converging on one
face, so penalizing them lured the search into a pointless excursion away from a usable approach point
and back again. `ConnectorRouter` now omits those endpoint-adjacent segments from the soft-obstacle
set, while the final revisit cleanup remains as a defensive last line of defense.

**Search implementation (Rendering#75).** The search is an exact, allocation-free re-implementation of the
state-keyed A\* described above, built so that its cost is proportional to the work the search really
needs rather than to the size of the grid times the number of obstacles. It never changes which route is
found — the five- and ten-connector scenes of `RoutingPerformanceTests` and every gallery image are
byte-identical to those of the dictionary-based search it replaced — because every cost is the same sum of
the same terms in the same order:

- *Per-edge cost memo.* The obstacle, wall, soft-obstacle, crossing and cost-band terms of a move depend on
  the grid edge crossed, not on the direction it is crossed in nor on the search state; only the turn
  penalty depends on the state. Each undirected grid edge therefore caches its blocked flag and its cost
  components (length term, soft cost, hint cost) separately — never as a pre-summed value, which would
  change the floating-point sum — and the search adds them to the path cost in the original order.
- *Rasterized obstacles.* Instead of every edge testing every obstacle, each hard obstacle, soft obstacle and
  existing line marks only the edges around it, found by binary search on the sorted grid lines; each
  candidate edge is tested with the original strict-inequality expressions, so the result is the same.
- *Dense search state.* A state is an integer — grid node times six direction slots (start, horizontal and
  vertical in each sense, done) — indexing flat arrays for cost, predecessor and expanded flag; only the
  rare state that carries a pending short run (a minimum interior segment) uses a dictionary. The arrays
  and the memo are thread-local scratch validated by a per-search stamp, so starting a search costs
  nothing proportional to the grid and nothing is allocated per search. The scratch of a thread is kept for
  reuse only while its tables take at most 32 MB (`RetainedByteLimit`, computed from the real array lengths at
  about 210 bytes per grid node, so about 160 000 nodes; the OMG Annex A whole-model view's largest grid is
  64 152 nodes, about 13 MB, so every benchmark and gallery search reuses it); a larger grid, or a scratch whose state
  tables grew past the limit during a search, is released when the search ends, so a thread never holds hundreds of
  megabytes for the life of the process. The tables hold no result (every entry a search reads is one it wrote under
  its own stamp), so dropping them changes no route. A queue entry whose state was
  already expanded at its best cost is skipped, since expanding it again could only find every neighbor
  already at the cost it would offer.
- *Connectivity sweep.* A search whose goal is walled in used to expand every state reachable from its
  start before failing. A breadth-first sweep over the free grid edges, spreading from the goals and
  advanced a few nodes per expansion alongside the search, ends such a search as soon as the goals' side
  of the grid is exhausted (a handful of nodes when a goal is enclosed) and never disagrees with it: every
  move the search may make is a free grid edge, and a free edge is free in both directions.

**Work budget (Rendering#75).** Even an exact search grows faster than linearly in the number of connectors of a scope:
the grid grows with every obstacle and every routed line, and so does the cost of each of the thousands of
searches a scope runs. `RoutingWorkBudget` is therefore an optional parameter of `RouteWithStatus` and
`RoutePorts`. It counts work in units that depend only on the input — one per four grid nodes built, one per
state expanded, one per node the connectivity sweep visits — never time, so every run on every platform
stops at the same state. A budget with room to spare changes no route. The work is charged to the budget while the
search runs, in chunks of 64 expansions (`ChargeChunkSize`), so the account is current whenever another part of the
layout reads it and the hard limit is checked at every chunk; the grid build is charged no more than the work the
budget has left, and a search that starts with no work left returns at once. The work spent therefore exceeds the
hard limit by less than one chunk, `63 * (1 + 4)` units (an expansion plus its four connectivity-sweep steps). The
budget may also carry a `CancellationToken`, checked at the start of a search and at every chunk, that stops the
routing with an `OperationCanceledException`; it never changes a tier or a route (see the *HierarchicalLayoutAlgorithm*
design). When the budget reaches its hard
limit, a search stops at once and the connector takes the obstacle-aware elbow fallback with
`Crossed = true`; every later connector of the scope takes it without a search; the budget counts such
routes. When the budget is at the reduced tier and no hints are given (the plain route), the route is first
searched inside a *window* — the box around the two anchors and their stub ends, grown by a quarter of its
larger extent and at least 120 pixels — where only the obstacles reaching into the window and the soft
obstacles overlapping it are kept and four wall rectangles close off the rest of the canvas, so the grid
holds the neighborhood of the connector rather than the whole canvas; a connector the window cannot join is
then searched over the whole canvas. The window is used only at the reduced tier, so the full effort is
never windowed.

*Soft lanes at the reduced tier.* Every soft obstacle (an already-routed line) normally adds grid lines on either
side of itself, so a search has an alternative lane beside every line already drawn. Profiling a whole SysML model
view (87 routed connectors in one scope) showed that these lines, not the hard obstacles, made the windowed
grids: a window held about 50 hard obstacles (a grid of about 5 000 nodes) but about 126 soft rectangles, so the
average window grid had about 107 000 nodes and one search cost the square of the number of lines already routed,
a whole scope the cube. At the reduced tier the router therefore (`SoftLanes.Reduced`) (a) adds lines only for the
128 soft rectangles whose centers are nearest the middle of the two step-off points (a stable ordering by
`|dx| + |dy|`, so ties keep the input order and the choice depends on the input alone) and (b) adds lines only
perpendicular to a rectangle's long side — a tall rectangle contributes x lines, a wide one y lines — because a
lane beside a line is only useful to a route running along it. The cost of the search still weighs every soft
obstacle, only the lanes are limited; the lines of the hard obstacles and of the two ends are always present, so
whether a route exists never depends on the soft lanes. The choice is a value (`SoftLanes`) handed down the
search, with no static state; the window gets it from the soft obstacles overlapping the window, the
whole-canvas retry from all of them. The full effort gives the search every lane, as before. Measured on the
whole-model view, together with the single routed pass and the inherited reduction described in the
*HierarchicalLayoutAlgorithm* design, the grid nodes the whole-model view searched fell from 319 million to 42
million and its connector crossings from 1 893 to 1 777; the quality curve of the cap is 128: 1 777 crossings,
96: 1 876, 64: 1 983, 32: 1 980, 16: 2 029, which is why the cap is 128.

*Rejected by measurement.* Clipping the grid to a window around the terminals for the full-effort (hinted)
searches did not reduce the cost (the search reaches every part of the narrow canvas anyway, and windows that
fail to hold the route cost extra searches), and neither did breaking ties toward larger path cost (the
searches contain no ties). Both were dropped for the hinted search; the window is kept only where it was
measured to help, the plain route of an oversize scope. For the reduced tier, a weighted heuristic (1.5 to 3
times the Manhattan distance) saved at most 40 % of the windowed work and added 58 % more bends; trying only
two of the four clearance levels per window made more connectors fall through to the whole-canvas search
(53 to 315) and so cost more; adding no soft lanes at all gave 18 % more crossings (2 239 against 1 893);
and a change of the .NET JIT settings (tiered compilation, PGO, ReadyToRun) made no consistent difference.

#### OrthogonalEdgeRouter Error Handling

Null `source`, `target`, or `obstacles` arguments throw `ArgumentNullException`. Degenerate geometry
never throws: when no clean route exists the router returns a crossing route with `Crossed = true`
rather than failing, leaving the decision to surface a warning to the caller. A search stopped by the work
budget is not an error either: it returns the same crossing elbow route, and the budget records that it did.

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
  `EdgeRouting.Orthogonal` style. See *ConnectorRouter Unit Design*.
- **LayeredPipeline** (`OrthogonalRouter` stage) — routes individual layered-pipeline edges through
  the same engine so pipeline routes and free-form routes share one implementation. See *Layered
  Pipeline Unit Design*.

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
| Rendering-Layout-OrthogonalEdgeRouter-AvoidsConnectorCrossing | OrthogonalEdgeRouter behavior described above |
| Rendering-Layout-OrthogonalEdgeRouter-MinimumEndLegs | End-leg zones (hints); `RouteResult.Cost` / `ClearanceUsed` |
| Rendering-Layout-OrthogonalEdgeRouter-PortRanges | `RoutePorts` multi-port search |
| Rendering-Layout-OrthogonalEdgeRouter-WallKeepOut | Wall keep-out (hints) |
| Rendering-Layout-OrthogonalEdgeRouter-CrossingCost | Crossing penalty (hints) |
| Rendering-Layout-OrthogonalEdgeRouter-MinimumInteriorSegment | Minimum interior segment (hints) |
| Rendering-Layout-OrthogonalEdgeRouter-BoundedWork | Work budget (`RoutingWorkBudget`) |
| Rendering-Layout-OrthogonalEdgeRouter-ReducedEffortWindow | Work budget: windowed plain route at the reduced tier |
| Rendering-Layout-OrthogonalEdgeRouter-ReducedSoftLanes | Work budget: reduced-tier soft lanes (`SoftLanes`) |
| Rendering-Layout-OrthogonalEdgeRouter-BoundedMemory | Search scratch retention (`RetainedByteLimit`) |
