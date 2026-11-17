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

Internally, `ConnectorRouter` also resolves one shape-geometry object per endpoint box. That internal
abstraction exposes two face-level concerns per `PortSide`: the connectable extents (one or more
usable sub-ranges along the face, possibly empty) and the surface projection (the inward offset from
the bounding-box face to the real outline at a chosen along-face coordinate). The shipped
implementations are:

- **Rectangle** — one full-length extent per face; zero projection on every face.
- **RoundedRectangle** — one extent per face inset by the resolved corner radius at both ends; zero
  projection because the flat portion of the face still lies on the bounding box.
- **Note** — one full-length extent per face for now; zero projection.
- **Folder** — full-length left, right, and bottom extents; top extent only to the right of the
  raised tab, and a positive top-face projection equal to the resolved tab height so the anchor
  touches the recessed body top. `FolderTabWidth` and `FolderTabHeight` are resolved
  independently, so when only one hint is supplied the router computes the missing companion
  dimension from its generic fallback before extents and projection are evaluated.

### ConnectorRouter Methods

`Route(boxes, connection, options)` rejects null arguments (including a null `From` or `To`) with
`ArgumentNullException`, then:

1. **Anchor selection.** Computes the naturally-facing source and target faces from box separation as
   before (right/left when the horizontal separation dominates, otherwise bottom/top). For each
   endpoint, if that natural face reports a non-empty connectable extent it is used; otherwise the
   router falls back, in order, to the adjacent face that still points most toward the other box on
   the minor axis, then the other adjacent face, and finally the opposite face as a last resort.
   The along-face coordinate is chosen from the overlap-centre rule used previously, then clamped
   inward by `ConnectorRouteOptions.Clearance` whenever the face is long enough to keep that margin
   from both ends. Faces too short for that inset fall back to their own center instead of violating
   the margin. The result is then clamped into the chosen face's usable connectable extents (again
   applying the same clearance inset when an extent is long enough) and projected inward to the real
   outline. The chosen `PortSide` is retained so the route exits and enters perpendicular to the face.
2. **Obstacle set.** Builds a `Rect` per box, including the connection's own two endpoint boxes. The
   connector remains free to leave and enter the boxes it joins because the underlying
   `OrthogonalEdgeRouter` steps each anchor off its face by a perpendicular stub longer than any
   clearance level it tries before pathfinding, then reattaches the true anchor when assembling the
   final path; treating endpoints as ordinary obstacles closes the gap through which an unrelated,
   already-routed connector's soft obstacles could otherwise squeeze a later connector into its own
   target box's interior.
3. **Dispatch.** Routes through the router realizing `options.EdgeRouting`. Today `Orthogonal` maps to
   the internal `OrthogonalEdgeRouter.RouteWithStatus`, which is the implementation behind the enum
   value and remains internal to the Layout system. The dispatch is a single-arm switch structured so
   new styles slot in additively.
4. **Assembly.** Wraps the returned waypoints in a `LayoutLine` carrying the connection's `TargetEnd`,
   `LineStyle`, and `Label`, with `SourceEnd` left `None`.

The batch overload first computes those naive per-connection face coordinates, then groups any shared
box face claims (and any face that already carries an occupied anchor) and assigns the claims
(`DistributeSharedFaceAnchors`, see *Anchor separation and face overflow* below) across the **union of that
face's connectable extents** rather than across the full bounding-box span. It still orders the claims by
counterpart-box centre so the
visual left-to-right or top-to-bottom order of the connectors tracks the order of their counterparts.
Finally it routes the connectors sequentially and turns only each prior route's **interior** segments
into soft obstacles; the short endpoint-adjacent approach legs are intentionally omitted so several
connectors may still share a legitimate final corridor into the same box face without being lured into
redundant leave-and-return detours. The underlying router now discourages an interior overlap in
proportion to how far it extends, rather than treating every overlap as an equally cheap flat cost, so
parallel fan-out or fan-in connectors separate into visually distinct corridors instead of merging
along a shared trunk for an extended span. The single-connection overload is routed as a one-element
batch, so both overloads share every stage described below.

**Anchor separation and face overflow.** Connector end markers are about 15 px long and 10 px wide at the
default theme, so two anchors closer than `MinFixedAnchorSeparation` (16 px) on one face make neighboring
markers touch and the connectors hard to trace. `DistributeSharedFaceAnchors` therefore guarantees that
connectors landing on one face are at least 16 px apart, the anchors already occupied there (the end points
of connectors routed in a child scope, passed to the batch as fixed anchors) included, in up to
`MaxAnchorRedistributionPasses` (4) deterministic passes:

1. Each shared (or already occupied) face is placed inside its connectable extents inset by the connector
   clearance (12 px), the historical region. The part clear of the occupied anchors (the extents minus the
   open 16 px windows around them) is packed greedily to count how many anchors it holds 16 px apart.
2. If that region holds all the face's connectors, they are spread evenly along it (no occupied anchor:
   exactly the historical result) or each takes the position nearest its current anchor that is clear of
   every anchor already placed (occupied anchors present), so an anchor that already satisfies the rule does
   not move. Otherwise the wider *corner-margin* region is used: the extents inset by the marker-aware
   `PortCornerMargin` (8 px; `PortCornerMarginFor` of the target end, 10 px for a circled plus). A 50 px
   face therefore holds three anchors (at 8, 25 and 42) instead of two, and a centered interior anchor
   still leaves room for an external anchor at 9 or 41 (16 px away), instead of the old "most separated"
   fallback of 12 to 13 px. An anchor placed outside the clearance inset is flagged *slid*, so the
   clearance clamp and the later sliding stage keep it there.
3. If even the corner-margin region cannot hold them, the connectors nearest the middle of the face keep it
   and the overflow is peeled from the ends of the ordering (the claim whose counterpart sits farthest from
   the face's middle first) and moved to the adjacent face on the side the counterpart lies on: the lower end
   (counterpart above or left) to the top or left face, the upper end to the bottom or right face, falling
   back to the other adjacent face when the first is unusable. A connector end never returns to a face it
   has left, so two full faces cannot ping-pong. An overflowing claim from a right or bottom face takes the
   reverse connection order on its new face, so the connectors that wrap around the corner nest instead of
   crossing. The moved connectors take part in the next pass as ordinary claims of their new face.
4. On the last pass nothing moves: a face that is still over capacity (every adjacent face is full or
   unusable) is spread as evenly as its region allows, a documented best effort that never throws.

Later stages preserve the separation: the sliding stage keeps at least the smaller of 16 px and the current
distance from each neighbor, so it never repairs-or-worsens a face this stage already separated. The layered
pipeline is unaffected: its `PortDistributor` grows a node until every face holds its anchors 20 px apart.

**Port sliding (straight-lane alignment).** The anchors above are fixed before any obstacle is
considered, so a connector whose straight line is blocked used to step off its face, jog around the
obstacle and jog back right before its arrowhead although a free lane existed inside both faces. After
the first route (computed exactly as before) each connection is checked for being *defective*:
outside the cross-hierarchy profile, a route between two facing, opposite faces with two or more bends;
in the profile, also any route with three or more bends, a leg shorter than the comfort minimum, a tiny
interior jog (any interior segment shorter than twice the clearance, not only the first), a run along a
container wall, a crossing of another connector's end zone, or an
obstacle-crossing fallback. Only a defective connection is searched again, with the ports left to the
router: each end becomes a *port range* (`PortRange`) — the face, the face outline coordinate, the
permitted along-face intervals and a preferred position. The permitted intervals are the face's
connectable extents inset by `PortCornerMargin`, and a face too short for that inset (under twice the
margin) keeps its centre. The margin is derived, not chosen: both renderers draw end markers at the shared marker
scale (the stroke width,
`NotationMetrics.MarkerScale`), so a marker reaches `StrokeWidth × (half-width + StrokeWidth / 2)` to either
side of the line once its own outline stroke is included (6.375 px for an arrowhead, 7.125 px for a
diamond at the 1.5 stroke), and the box border adds half its stroke (0.75 px); rounded up that is 8 px.
A slid port therefore keeps the arrowhead's wings inside the face instead of overhanging a corner (the
earlier fixed 5 px, the unscaled half-width, left the wing flush with or past the corner). The margin is
computed by `PortCornerMarginFor(EndMarkerStyle style)`, which applies the same derivation to the wider of
the legacy widest arrowhead (the diamond) and, for `EndMarkerStyle.CircledPlus` only, the circle's
across half-width (`NotationMetrics.AcrossHalfWidth`): every legacy style, and the two dotted-triangle
styles, keep exactly 8 px (so no existing route moves), while the circled plus gets 10 px so its circle
never overhangs a corner. `PortCornerMargin` itself is `PortCornerMarginFor(EndMarkerStyle.None)` (8 px)
and is used at a connector's source end and by the straight-lane and clearance clamps; the slid-port
extents of the *target* end use `PortCornerMarginFor(connection.TargetEnd)`, because the end marker is
drawn at the target. The ports are
restricted so a
slid port stays between its neighbors on the same face (other batch connectors at their planned or
final ports, and fixed anchors), in the original order, and at least the smaller of the batch
separation (16 px) and its current distance away from each of them. The preferred position is the
plain anchor, and the router adds the overlap midpoint of the two ends' intervals as a candidate lane.
`OrthogonalEdgeRouter.RoutePorts` then searches from every candidate source port to every candidate
target port, charging a small cost per pixel of displacement (so the centered anchor wins every tie and a
port only moves to remove a bend). Outside the profile the search result replaces the first route only
when it has fewer bends, does not double back, does not overlap an earlier connector's lane for more
than a trivial stretch and was found at a clearance level no smaller than the first route's. The
accepted ports are written back to the batch's anchors (flagged *slid*, so later neighbors see their true
position and the clearance clamp no longer pulls them back inward) before the next connector is routed.

**Minimum end legs.** The search passes `RouteHints` so the first leg is at least the connector stub
(`Themes.Light.ConnectorStub`) and the final leg at least the end marker's RENDERED along-line length (the
notation length times the shared marker scale, `NotationMetrics.RenderedAlongLineLength`, the size at which
both renderers draw it) plus the bend radius plus the clean-leg margin (20 for an arrow, 26 for a diamond at
the 1.5 stroke), wherever the gap between the faces allows it. The legs are soft costs, never hard blocks, so a gap
too narrow for both still yields a route
that crosses no box (the legacy half-gap stub); the renderers' corner clamp keeps the decoration clean.
The required end leg is derived from `NotationMetrics.RenderedAlongLineLength` of the connection's
`TargetEnd` (and the peer end zone from its `AlongLineLength`), so the `HollowTriangleCrossbar`,
`HollowTriangleTwoDots`, `HollowTriangleFourDots` and `CircledPlus` end markers automatically receive a clean
straight final leg of at least their own rendered along-line length plus the bend radius, with no extra
router code.

**Folder tab resolution.** `FolderGeometry.ResolveFolderTabWidth` and `ResolveFolderTabHeight`, which
size the folder's non-connectable tab strip, delegate to `BoxMetrics.ResolveFolderTabWidth(box,
Themes.Light)` and `BoxMetrics.ResolveFolderTabHeight(box, Themes.Light)` instead of keeping a private
copy of the formula. A caller-supplied `FolderTabWidth`/`FolderTabHeight` still wins. Without one, a folder
that draws its name in the tab (`BoxShape.Folder`, a label and `Appearance.FolderTitle` equal to
`FolderTitlePlacement.Tab`) gets a tab as wide as the measured name plus padding (never narrower than
`FolderTabMinWidth`, never wider than the box), and every other folder keeps the historical
label-length estimate, so the router's cutout always matches the tab the renderers draw.

**Cross-hierarchy profile (`RoutingProfile`).** The hierarchical engine supplies a profile carrying each
connector's container wall keep-out strips. With it, a defective connector additionally (a) keeps its runs
and bends at least twice the clearance from the supplied walls (a wall is crossed only at right angles),
(b) keeps comfort legs of at least twice the clearance (soft preference for three times) off both ends,
(c) tries the adjacent faces toward the other box for each end (the face pair with the lowest cost wins,
with a small penalty against leaving the natural faces) so a face shielded by a container's title band is
not approached by a four-bend detour, (d) treats the stretch just outside every other connector's end
faces (twice the clearance deep; one clearance wide for an arrowhead end) as a hard obstacle so no
connector crosses another's arrowhead approach or first leg, and (e) pays a crossing penalty for every
right-angle crossing of an earlier connector, so avoidable crossings are avoided and forced ones happen at
the cheapest place, and (f) forbids any interior segment (between two bends) shorter than twice the
clearance and any reversal along an axis (`RouteHints.MinInteriorSegment`, a hard constraint of the
first search pass that is dropped only when no route satisfies it), so a Z never carries a few-pixel jog.
Connectors outside the profile (containment, direct callers) keep the behavior above.

**Straightness policy.** A connector between two facing faces is one straight segment whenever a lane
exists that (a) lies in both faces' usable spans (the faces inset by `PortCornerMargin`, so the arrowhead
stays inside each face), (b) is at least twice the clearance from every container border it runs along —
those of the endpoints' enclosing containers and of every unrelated container (the hierarchical engine
supplies walls for all of them) — and (c) clears every other box. When no such lane exists the connector
is a clean Z or U instead: legs and interior segments of at least twice the clearance, borders crossed only
at right angles, ports inside the faces' usable spans. A lane that is merely short of this (for instance
12 px under an unrelated container's border, or 5.5 px from a face corner) is refused rather than drawn
with a hugging run or an overhanging arrowhead. A face pair whose spans overlap by less than twice the
margin (for example only a few pixels) cannot carry a straight lane and takes the clean Z.

**Internal per-connection batch (`RouteBatch`).** The public batch overload delegates to an internal
entry point, `RouteBatch(connections, options, obstaclesFor, fixedAnchors, penalizeLongEndSegments, profile)`,
that is otherwise the same algorithm but (a) takes each connection's hard-obstacle list from the
`obstaclesFor` callback instead of one shared box list, (b) accepts *fixed anchors* — end points already
occupied on a box face by connectors routed elsewhere — and moves any batch anchor that would land within
a minimum separation of one to the nearest free position on the face's usable extent (an anchor that is
already clear is left untouched), (c) can optionally also turn the long runs of the first and last
segments of already-routed lines (beyond a short corridor at each end) into soft obstacles, and (d)
accepts the optional cross-hierarchy `RoutingProfile`. The
`HierarchicalLayoutAlgorithm` uses it to route connectors whose endpoints are boxes nested inside
containers (their coordinates are absolute like every box's): it expands the endpoints' ancestor
containers in that connector's obstacle list so the connector can cross their walls, passes the face
anchors taken by the endpoint's own container's connectors, enables the end-segment penalty, and supplies
the profile with the borders of those ancestor containers — and of every other container reachable from
the scope, so a run beside an unrelated container's border also keeps its distance — as wall strips (a
folder's top border follows the tab and the body). The public overloads' behavior for connectors that
need no search is unchanged (no
fixed anchors, shared obstacle list, interior segments only).

#### Diamond outline anchors

`DiamondGeometry` is the shape geometry of `BoxShape.Diamond`. Every face is connectable in full, because a
diamond has an outline point at every along-face coordinate, but the outline recedes linearly from the
bounding-box face towards the vertex at the face midpoint. `ProjectToSurface(side, along)` returns that
distance — `depth / faceLength × |along − faceLength / 2|`, so zero at the vertex and half the box depth at a
corner — and the existing anchor resolution (`ResolveAnchorPoint`, and the layered pipeline's
`PortDistributor`/`LongEdgeJoiner` through `ShapeAnchorSupport`) places the connector end on that outline and
leaves it along the face axis. A single connector on a face therefore lands exactly on the vertex, several
connectors spread over the face at distinct points of the outline, and the first run is measured from the
outline instead of from a bounding box the shape does not touch (Rendering#58). Nothing else in the router
changes: the sloped projection is a property of the geometry, not a new routing mode.

A shaped node spreads the ports of a face over its *usable extent* (the face less `ConnectorClearance` at each
end; `PortDistributor.BuildUsableExtents`), so the pitch between k ports is `(length − 2 × ConnectorClearance) / k`,
not `length / k` as for a plain rectangle. When several connectors end on a diamond face the layered algorithm
therefore grows the diamond (Diamond only, never a rectangle, folder, or note) so the pitch it hands to
`PortDistributor` is at least `LayeredLayoutMetrics.MarkerFacePitch(style)` — the marker's painted width
(`2 × AcrossHalfWidth × MarkerScale + MarkerStrokeWidth`) plus a 4 px `MarkerGap`, never less than `EdgeSpacing` —
by requiring a face length of `Total × pitch + 2 × ConnectorClearance` (see the layered layout algorithm design).
Adjacent end markers on one diamond face then keep a marker width plus 4 px apart; a single connector on a face
is unaffected and still lands on the vertex.

#### Shared-face final runs (Rendering#57)

The router already slides ports on a shared face to a straight run when the facing face spans overlap
(`IsDefective`/`SearchBetterRoute`), so Rendering#57 needed no change here; the same scenario through the
layered pipeline is handled by its `PortAligner` stage, and a regression test keeps the router path honest.

#### End labels

`Connection` has two optional appended members, `SourceEndLabel` and `TargetEndLabel`, which `RouteConnection`
copies onto the routed `LayoutLine`. The router does not place or reserve room for the labels (placement is the
renderers' job, through `ConnectorLabelPlacer.PlaceEndLabels`).

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
  invoked by `RouteWithStatus` for the `EdgeRouting.Orthogonal` style. See *OrthogonalEdgeRouter Unit
  Design*.
- **`NotationMetrics`** — the note-fold and folder-tab sizing constants (`NoteFoldFraction`,
  `NoteFoldMaxSize`, `FolderTabMaxWidthFraction`, `FolderTabMinWidth`, `FolderLabelCharWidthFactor`)
  consumed by `NoteGeometry` and `FolderGeometry` to compute each shape's connectable-extent cutouts
  (the note's diagonal fold corner and the folder's top-edge tab).
- **`Themes`** — `Themes.Light`'s `FontSizeBody` and `LabelPadding` feed the shared folder-tab width
  resolution (through `BoxMetrics`) when a box does not supply its own `FolderTabWidth` hint, approximating the
  tab width a themed folder label would need; its `ConnectorStub`, `LineCornerRadius` and `CleanLegMargin`
  set the minimum first and final legs (with `NotationMetrics.RenderedAlongLineLength` for the end marker).
- **`BoxMetrics`** — `BoxMetrics.ResolveFolderTabWidth` and `BoxMetrics.ResolveFolderTabHeight` (with
  `Themes.Light`) are what `FolderGeometry` delegates to for the folder-tab dimensions, so the routing
  cutout stays consistent with the renderers' own folder-tab geometry, including a tab sized to an in-tab
  name.

No OTS runtime component or shared package is consumed.

### ConnectorRouter Callers

`ConnectorRouter` is used by units that have already placed boxes and need to draw connectors among
them:

- **HierarchicalLayoutAlgorithm** — calls `ConnectorRouter.Route` at each hierarchical scope to route
  cross-container edges around sibling containers after the leaf algorithms have placed the
  containers themselves. See *HierarchicalLayoutAlgorithm Unit Design*.
- **External application code** — any caller that supplies its own placed `LayoutBox` list (for
  example from a containment or free-form placement produced outside the layered pipeline) and needs
  routed `LayoutLine` connectors to drop into a `LayoutTree`.
- **LayeredPipeline** — the `PortDistributor` and `LongEdgeJoiner` stages internally consume
  `ConnectorRouter`'s (internal, widened-accessibility) shape-geometry resolution
  (`ResolveShapeGeometry` and its supporting extent-math helpers) for same-scope (leaf) edges routed
  through `LayeredLayoutAlgorithm`'s own pipeline, so a shaped node gets the same connectable-extent
  restriction and inward surface projection whether it is routed by `ConnectorRouter` (cross-container
  edges) or by the layered pipeline (same-scope edges). See *Layered Pipeline Unit Design*'s
  "Layered Pipeline Dependencies" section for the reverse-direction documentation of this cross-unit
  dependency.

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
| Rendering-Layout-ConnectorRouter-ShapeAwareAnchors | ConnectorRouter behavior described above |
| Rendering-Layout-ConnectorRouter-SharedFaceDistribution | ConnectorRouter behavior described above |
| Rendering-Layout-ConnectorRouter-MinimumAnchorSeparation | Anchor separation and face overflow (steps 1-2) |
| Rendering-Layout-ConnectorRouter-FaceOverflowToAdjacentFace | Anchor separation and face overflow (steps 3-4) |
| Rendering-Layout-ConnectorRouter-AvoidsObstacles | ConnectorRouter behavior described above |
| Rendering-Layout-ConnectorRouter-ExcludesEndpoints | ConnectorRouter behavior described above |
| Rendering-Layout-ConnectorRouter-CarriesStyling | ConnectorRouter behavior described above |
| Rendering-Layout-ConnectorRouter-BatchOrder | ConnectorRouter behavior described above |
| Rendering-Layout-ConnectorRouter-Validation | ConnectorRouter behavior described above |
| Rendering-Layout-ConnectorRouter-AlignsPortsToLane | Port sliding (straight-lane alignment) |
| Rendering-Layout-ConnectorRouter-DecorationWithinFace | Port sliding (`PortCornerMargin` derivation) |
| Rendering-Layout-ConnectorRouter-SlidePreservesDistinctPorts | Port sliding (neighbor windows, overlap guard) |
| Rendering-Layout-ConnectorRouter-MinimumEndLegs | Minimum end legs |
| Rendering-Layout-ConnectorRouter-WallsAndFaceSelection | Cross-hierarchy profile; straightness policy |
| Rendering-Layout-ConnectorRouter-PeerEndZones | Cross-hierarchy profile (peer end zones) |
| Rendering-Layout-ConnectorRouter-FolderTabResolution | Folder tab resolution |
| Rendering-Layout-ConnectorRouter-PortCornerMargin | Port sliding (`PortCornerMarginFor`) |
| Rendering-Layout-ConnectorRouter-NewEndMarkerClearance | Minimum end legs |
