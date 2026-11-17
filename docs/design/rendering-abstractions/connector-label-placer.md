## Connector Label Placer Unit Design

Part of the Rendering Abstractions system.

### Connector Label Placer Purpose

The Connector Label Placer unit computes non-overlapping screen positions for connector midpoint
labels. Each labeled line prefers the midpoint of its longest segment; when two labels would collide,
the placer falls back to a shorter segment or nudges the label perpendicular to its segment until it no
longer overlaps an already-placed label. Lines are processed in the supplied order so the result is
deterministic, and both renderers share this logic so their label layouts match.

### Connector Label Placer Data Model

- `ConnectorLabelPlacer` (static class) — `Place(IEnumerable<LayoutLine>, double)`,
  `Place(IEnumerable<LayoutLine>, double, IEnumerable<LayoutBox>)`, `EstimateLabelHeight(double)`,
  `EstimateLabelWidth(string, double)`.
- `LabelPlacement` (public `readonly record struct`) — `(double X, double Y, double HalfWidth,
  double HalfHeight)`. Carries the chosen label centre plus that label's estimated half-width/
  half-height, so a caller can compute the label's full rendered bounding box without
  re-estimating it. Exposes an explicit 2-argument `Deconstruct(out double x, out double y)`
  alongside its synthesized 4-argument member-wise `Deconstruct`, so existing
  `var (x, y) = result[line];` call sites keep compiling unchanged (C# resolves `Deconstruct`
  overloads by arity) while new callers can deconstruct or member-access all four values.

### Connector Label Placer Key Methods

`IReadOnlyDictionary<LayoutLine, LabelPlacement> Place(IEnumerable<LayoutLine> lines, double
fontSize)` — returns a chosen label placement (centre plus half-extent) for every line that has a
`MidpointLabel`. Lines without a label, and lines with no waypoints, are omitted. The method
estimates each label box from `fontSize`, places the label at the longest clear segment midpoint,
and nudges perpendicular to avoid overlap. Exposing each placement's half-width/half-height (not
just its centre) lets a caller compute the label's full bounding box after every nudge/fallback
has been applied, which is what a renderer needs to grow its canvas/bitmap bounds to include every
placed label — the final canvas size was previously computed from box+routing geometry before this
method ran, so a nudged label could land completely outside those bounds and render invisibly
clipped; a renderer must instead finalize its canvas size only after calling `Place`.

`IReadOnlyDictionary<LayoutLine, LabelPlacement> Place(IEnumerable<LayoutLine> lines, double fontSize,
IEnumerable<LayoutBox> obstacles)` — the box-aware overload, which additionally keeps each label clear
of the supplied obstacle boxes (a renderer passes every box in the tree). The two-argument overload
delegates to it with an empty obstacle set, so with no obstacles the results are identical for lines
without an end decoration (a decorated line's own end-decoration footprints always apply, see below). An obstacle
is classified once as a *leaf* box (its children, if any, are only ports) or a *container* box (it has
nested nodes other than ports). A candidate label rectangle (the estimated label box, including the
`Gap` clearance) is rejected when it overlaps an already-placed label, or when it:

- overlaps a leaf box at all, because a label there would paint its opaque background over the box; or
- overlaps a container box without lying fully inside it (straddles its border), because a label
  legitimately sits inside a container. A label fully inside a container is allowed.

`Place(lines, fontSize, obstacles, markerScale)` is the overload both renderers call, passing
`NotationMetrics.MarkerScale(theme)`; the three-argument overload uses the marker scale of the default light
theme. The `markerScale` sizes the end-decoration footprints (below), so the clearance tracks the size at
which both renderers actually draw every marker instead of a hard-coded stroke width.

Each end decoration of the line being placed has a *footprint*: the box from the line endpoint back along
the first (source) or last (target) segment by the decoration's rendered along-line length
(`NotationMetrics.AlongLineLength` times `markerScale`), widened across the line by the decoration's
rendered half width (`NotationMetrics.AcrossHalfWidth` times `markerScale`). A footprint is a leaf-box
obstacle for the midpoint and slide candidates of its own line only: a label is never placed over its own
line's arrowhead, even at the midpoint of a short run. (Another line's arrowhead is not avoided.)

When obstacles or decoration footprints are present, candidates are tried in this order, and the first
clear one is taken:

1. The segment midpoints, longest segment first.
2. Sliding along each segment (longest first) in steps of the label's half-height, alternating either
   side of the midpoint (toward the end of the segment first at each step distance). The slide keeps
   the whole label inside the segment and keeps it clear of the footprint of any end decoration at either
   end of the line (the same rendered along-line length as above). The reserve applies only to the first
   segment (start) and last segment (end) of the line. When the half-label steps all miss a narrow clear
   window (a short run barely long enough for the label and the decoration), the exact offset nearest the
   midpoint that keeps the whole label inside that window is tried as well.
3. Perpendicular nudges of the longest segment, up to four steps either side, which consider only the
   boxes and labels. When the midpoint was blocked only by a decoration footprint (the label there is
   clear of every box and label) no nudge is attempted: a label moved off its own line reads as belonging
   to a neighboring connector, so on a run too short for both the label and its arrowhead the placer
   falls back to the obstacle-free midpoint instead.

If no candidate clears every obstacle, the obstacle-free placement of the two-argument overload is used
unchanged, so a label is never lost and the no-overlap guarantee between labels is preserved. The search
is bounded and deterministic: the result depends only on the supplied order of lines and obstacles.

`double EstimateLabelHeight(double fontSize)` — returns the full (not half) estimated label
bounding-box height (`fontSize * HeightFactor + 2 * Gap`, i.e. twice the `halfHeight` `Place`
computes internally for every label), so another layout stage can size a node's spacing against the
exact same label-height formula this placer uses when testing for overlap, without duplicating its
internal `HeightFactor`/`Gap` constants. `LayeredLayoutAlgorithm` calls this to widen its auto-grow
minimum-size floor so 2+ labeled parallel connector anchors sharing a node's `Left`/`Right` face
(where anchors are spread vertically) are spaced far enough apart for `Place`'s first-pass
(no-nudge) placement to succeed for every label.

`double EstimateLabelWidth(string text, double fontSize)` — returns the full (not half) estimated
label bounding-box width for the given label text (`text.Length * fontSize * CharWidthFactor + 2 *
Gap`, i.e. twice the `halfWidth` `Place` computes internally for that label), so another layout
stage can size itself against the exact same label-width formula this placer uses when testing for
overlap, without duplicating its internal `CharWidthFactor`/`Gap` constants. Unlike
`EstimateLabelHeight`, this varies with the supplied label text since rendered text width is
text-dependent while the label box's height is not. `LayeredLayoutAlgorithm` calls this to widen its
auto-grow minimum-size floor so 2+ labeled parallel connector anchors sharing a node's `Top`/`Bottom`
face (where anchors are spread horizontally) are spaced far enough apart for `Place`'s first-pass
(no-nudge) placement to succeed for every label.

#### End labels

`PlaceEndLabels(lines, fontSize, obstacles, markerScale, occupied, ports)` places the role name and multiplicity
labels of each connector end (`LayoutLine.SourceEndLabel` / `TargetEndLabel`) and returns one
`EndLabelPlacement` (line, end, slot, text, centre, half-extents) per non-empty slot. It lives in a partial
class file of its own and does not touch `Place`, so midpoint placements are byte-identical with or without end
labels; the renderers pass the midpoint placements (and, for ports, their glyph and label rectangles) in
`occupied` so end labels yield to them, and every port of the tree in `ports`.

For each label, in the fixed order lines, source end before target end, primary slot before secondary slot (which makes
the result deterministic), the placer takes the end segment, the unit direction into the line, and the label box
(`EstimateLabelWidth` by `EstimateLabelHeight`; the width comes from `EstimateEndLabelWidth`, which measures the text
with `PortLabelWidthEstimator` plus the label gap on both sides, the same estimate the layered layout uses to reserve
room for stacked end labels). The nominal position starts beyond the end marker (`AlongLineLength × markerScale`) plus a
small offset; the primary slot sits above a horizontal run or to the right of a vertical one, the secondary slot on the
opposite side, offset across the line by half the label plus `EndLabelLineClearance` (2 px, which with the box's own
2 px gap keeps the text 4 px from its connector). A marker end that sits on a port centre is painted by both renderers
retracted along the end segment by the port glyph's half size, so the placer takes `ports`, asks
`NotationMetrics.PortEndRetraction` for that retraction (the same rule the renderers shorten lines by), and measures
both the first label offset and the end decoration's footprint from the retracted tip; with `ports` null nothing
retracts. Every `occupied` rectangle and every end decoration footprint is inflated by
`EndLabelClearanceMargin` (2 px) for the collision test, which with the box gap guarantees at least 4 px between label
text and a port glyph, port label, midpoint label, or marker; two end labels keep the two box gaps (4 px) between
their texts. Candidates are the nominal position, then slides along the segment in half-label steps, then the same on
the opposite side. A candidate is rejected when it overlaps a leaf box, straddles a container border, overlaps the
(inflated) footprint of the line's own end decorations, overlaps an (inflated) `occupied` rectangle or an earlier end
label, or runs past the end of the segment. When nothing clears, the nominal position is used: a label is never
dropped and the method never throws for a crowded diagram.

### Connector Label Placer Error Handling

`Place` validates its `lines` argument with `ArgumentNullException.ThrowIfNull` and propagates the
resulting `ArgumentNullException` to the caller; the box-aware overload additionally validates
`obstacles` the same way. All other input shapes are treated as normal cases
rather than errors: a line whose `MidpointLabel` is `null` is omitted from the result, a line whose
`Waypoints` collection is empty is likewise omitted, and a line with a single waypoint yields the
degenerate midpoint of that waypoint. The overlap-avoidance search is bounded — after exhausting the
segment fallback and a fixed number of perpendicular nudges, the label is dropped just beneath every
already-placed label, guaranteeing no overlap even when the bounded nudges above are exhausted (for
example where many connectors cross at a single point). The method has no side effects on its inputs
and performs no logging.

### Connector Label Placer Dependencies

- **Rendering Model system (`DemaConsulting.Rendering`)** — reads `LayoutLine.MidpointLabel`,
  `Waypoints`, `SourceEnd`, and `TargetEnd`, and (box-aware overload) `LayoutBox` bounds and children
  from the layout tree types.
- **Notation Metrics Unit** (same system) — the box-aware overload calls
  `NotationMetrics.AlongLineLength`, `NotationMetrics.AcrossHalfWidth` and `NotationMetrics.MarkerScale` to
  size the end-decoration footprints.
- **.NET base class library** — `System.Collections.Generic.IEnumerable<T>`,
  `IReadOnlyDictionary<TKey, TValue>`, and standard geometry arithmetic. No third-party runtime
  packages are consumed.

### Connector Label Placer Callers

- **Rendering.Svg `SvgRenderer` unit** — calls the box-aware `Place` once per render pass, passing
  every box in the tree and `NotationMetrics.MarkerScale(theme)`, to compute the label positions for the connector
  labels it writes as `<text>`
  elements.
- **Rendering.CanvasNet raster renderers (`CanvasNetRasterRenderer`, `PngRenderer`,
  `JpegRenderer`)** — call the box-aware `Place` for the same purpose so that the SVG and raster outputs
  agree on label positions.
- **Rendering.Layout `LayeredLayoutAlgorithm` unit** — calls `EstimateLabelHeight` when computing its
  auto-grow minimum-size floor for a `Left`/`Right` face, and `EstimateLabelWidth` for a `Top`/`Bottom`
  face, so a node with 2+ labeled parallel connector anchors on the same face grows enough along that
  face's spread axis that `Place`'s own label-extent formula never collides with itself.

### Connector Label Placer Design Constraints

- A line without a `MidpointLabel` shall be omitted from the result.
- A label shall be placed at the midpoint of its line's longest segment unless doing so would overlap
  an already-placed label, in which case it shall be moved to a shorter segment or nudged aside.
- Placement shall be deterministic for a given input order so the SVG and PNG renderers agree.
- The box-aware overload shall place exactly as the two-argument overload when given no obstacles, and
  shall fall back to the obstacle-free placement when no candidate clears every obstacle, so a label is
  never lost.
- A label shall never be placed overlapping a leaf box; it may lie fully inside a container box but
  shall not straddle a container's border (unless no candidate clears and the fallback applies).
- A label slid along a segment shall stay inside the segment and clear of the end-decoration footprint.
- A label's midpoint and slides shall stay clear of its own line's end-decoration footprints, sized by the
  marker scale the caller supplies; no marker scale is hard-coded in the placer beyond the default-theme
  value used by the overloads that take none.
- A label that cannot clear a decoration on a run too short for both stays on its own line.

### Connector Label Placer Interactions

`ConnectorLabelPlacer` reads `LayoutLine.MidpointLabel` and `Waypoints` from the rendering model and
is called by the renderers (SVG and PNG systems) before drawing connector labels.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Abstractions-ConnectorLabelPlacer-OmitUnlabelled | `ConnectorLabelPlacer.Place` filtering unlabelled lines |
| Rendering-Abstractions-ConnectorLabelPlacer-LongestSegment | `ConnectorLabelPlacer.Place` segment choice |
| Rendering-Abstractions-ConnectorLabelPlacer-AvoidOverlap | `ConnectorLabelPlacer.Place` overlap avoidance |
| Rendering-Abstractions-ConnectorLabelPlacer-ExposesLabelExtent | `LabelPlacement.HalfWidth`/`HalfHeight` in `Place` |
| Rendering-Abstractions-ConnectorLabelPlacer-EstimateLabelHeight | `ConnectorLabelPlacer.EstimateLabelHeight` |
| Rendering-Abstractions-ConnectorLabelPlacer-EstimateLabelWidth | `ConnectorLabelPlacer.EstimateLabelWidth` |
| Rendering-Abstractions-ConnectorLabelPlacer-AvoidBoxes | Box-aware `Place` leaf/container rule and fallback |
| Rendering-Abstractions-ConnectorLabelPlacer-AvoidEndDecoration | Box-aware `Place` footprints |
| Rendering-Abstractions-ConnectorLabelPlacer-RejectNullObstacleArguments | Box-aware `Place` guards |
