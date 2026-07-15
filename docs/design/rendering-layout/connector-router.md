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
box face claims and redistributes them across the **union of that face's connectable extents** rather
than across the full bounding-box span. It still orders the claims by counterpart-box centre so the
visual left-to-right or top-to-bottom order of the connectors tracks the order of their counterparts.
Finally it routes the connectors sequentially and turns only each prior route's **interior** segments
into soft obstacles; the short endpoint-adjacent approach legs are intentionally omitted so several
connectors may still share a legitimate final corridor into the same box face without being lured into
redundant leave-and-return detours. The underlying router now discourages an interior overlap in
proportion to how far it extends, rather than treating every overlap as an equally cheap flat cost, so
parallel fan-out or fan-in connectors separate into visually distinct corridors instead of merging
along a shared trunk for an extended span.

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
- **`NotationMetrics`** — the note-fold and folder-tab sizing constants (`NoteFoldFraction`,
  `NoteFoldMaxSize`, `FolderTabMaxWidthFraction`, `FolderTabMinWidth`, `FolderLabelCharWidthFactor`)
  consumed by `NoteGeometry` and `FolderGeometry` to compute each shape's connectable-extent cutouts
  (the note's diagonal fold corner and the folder's top-edge tab).
- **`Themes`** — `Themes.Light`'s `FontSizeBody` and `LabelPadding` feed `FolderGeometry`'s fallback
  folder-tab width formula when a box does not supply its own `FolderTabWidth` hint, approximating the
  tab width a themed folder label would need.
- **`BoxMetrics`** — `BoxMetrics.FolderTabHeight(Themes.Light)` is `FolderGeometry`'s fallback folder-tab
  height when a box does not supply its own `FolderTabHeight` hint, keeping the routing cutout consistent
  with the renderer's own default folder-tab geometry.

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
- **LayeredPipeline** — the `PortDistributor` and `LongEdgeJoiner` stages internally consume
  `ConnectorRouter`'s (internal, widened-accessibility) shape-geometry resolution
  (`ResolveShapeGeometry` and its supporting extent-math helpers) for same-scope (leaf) edges routed
  through `LayeredLayoutAlgorithm`'s own pipeline, so a shaped node gets the same connectable-extent
  restriction and inward surface projection whether it is routed by `ConnectorRouter` (cross-container
  edges) or by the layered pipeline (same-scope edges). See _Layered Pipeline Unit Design_'s
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
| Rendering-Layout-ConnectorRouter-AvoidsObstacles | ConnectorRouter behavior described above |
| Rendering-Layout-ConnectorRouter-ExcludesEndpoints | ConnectorRouter behavior described above |
| Rendering-Layout-ConnectorRouter-CarriesStyling | ConnectorRouter behavior described above |
| Rendering-Layout-ConnectorRouter-BatchOrder | ConnectorRouter behavior described above |
| Rendering-Layout-ConnectorRouter-Validation | ConnectorRouter behavior described above |
