## Notation Metrics Unit Design

Part of the Rendering Abstractions system.

### Notation Metrics Purpose

The Notation Metrics unit is the single home for all intrinsic, theme-independent notation geometry
shared by the SVG and PNG renderers: end-marker (arrowhead) shapes and sizes, the port square,
folder-tab and note-fold proportions, rounded-rectangle corner scaling, badge fractions, the
label-background inset, and the geometry of the SysML v2 notation primitives (dotted-triangle and
circled-plus end markers, thick-bar and crossed-circle badges, and send/accept corner icons). Every
value is either a documented primitive constant or a documented
derivation of those primitives, so a geometry literal never appears more than once in the rendering
path. `MarkerVertex` expresses one vertex in tip-relative units.

### Notation Metrics Data Model

- `NotationMetrics` (static class) — the end-marker, port, folder, note, badge, label, dotted-triangle,
  circled-plus, thick-bar, crossed-circle, and corner-icon constants plus the `AlongLineLength`,
  `RenderedAlongLineLength`, `AcrossHalfWidth`, `DotCenters`, `TriangleFamilyBox`, `TriangleVertices`,
  `DiamondVertices`, `CornerIconVertices`, and `RoundedRectRadius` helpers.
- `MarkerVertex` (readonly record struct) — `Along` (distance back from the tip) and `Across`
  (perpendicular offset).

The notation-primitive constants added for the SysML v2 end markers, badges, and corner icons are:

- Dots: `DotRadius` (1.25), `DotBaseGap` (1.5), `DotColumnSpacing` (3.5), and `DotRowOffset` (2.25).
- Dot extensions behind the triangle base: `TwoDotsExtension` = `DotBaseGap` + 2 x `DotRadius` (4.0);
  `FourDotsExtension` = `TwoDotsExtension` + `DotColumnSpacing` (7.5).
- Dotted marker box lengths: `TwoDotsMarkerLength` = `EndMarkerLength` + `TwoDotsExtension` (14.0);
  `FourDotsMarkerLength` = `EndMarkerLength` + `FourDotsExtension` (17.5).
- Dotted tip anchors: `TwoDotsRefX` = `EndMarkerRefX` + `TwoDotsExtension` (13.0); `FourDotsRefX` =
  `EndMarkerRefX` + `FourDotsExtension` (16.5).
- Circled plus: `CircledPlusRadius` (5.0), `CircledPlusMarkerBox` (12.0), `CircledPlusCenter` = box / 2
  (6.0), `CircledPlusRefX` = center + radius (11.0), and `CircledPlusArmHalfLength` = radius (5.0).
- Badges: `ThickBarThickness` (4.0) and `CrossedCircleArmFraction` = cos 45 degrees (about 0.7071).
- Corner icon: `CornerIconWidth` (12.0), `CornerIconHeight` (9.0), `CornerIconInset` (6.0),
  `CornerIconNotchDepth` (3.0), and `CornerIconPointWidth` (4.0).

#### Dotted-triangle and circled-plus marker geometry

Marker geometry is expressed in tip-relative units: `Along` is the distance measured back from the line
endpoint (the tip) into the line, and `Across` is the perpendicular offset from the line. The triangle
of every hollow-triangle-family marker is the unchanged `TriangleVertices` result (base corners at
`Along` = `EndMarkerRefX` = 9.0, `Across` = ±3.5; apex at `Along` = -1.0). The dots sit just behind the
triangle base, straddling the line.

| Style | Dot centers (`Along`, `Across`) | Marker box length / tip anchor (`refX`) |
| --- | --- | --- |
| `HollowTriangle` | none | 10.0 / 9.0 |
| `HollowTriangleTwoDots` | (11.75, -2.25), (11.75, +2.25) | 14.0 / 13.0 |
| `HollowTriangleFourDots` | (11.75, -2.25), (11.75, +2.25), (15.25, -2.25), (15.25, +2.25) | 17.5 / 16.5 |
| `CircledPlus` | none; circle centered 6.0 from the box origin, radius 5.0 | 12.0 / 11.0 |

The first dot column is at `Along` = `EndMarkerRefX` + `DotBaseGap` + `DotRadius` (9.0 + 1.5 + 1.25 =
11.75); the second column of the four-dot marker is a further `DotColumnSpacing` (3.5) back, at 15.25.
Each dot has radius 1.25 and its centre is 2.25 either side of the line, so a dot spans `Across` from
1.0 to 3.5: clear of the line stroke and exactly as wide as the triangle (`EndMarkerHalfWidth` = 3.5).
The marker box is widened behind the base by the dot extension and the tip anchor is shifted by the
same amount, so the triangle apex and base land at exactly the same place on the line as for a plain
`HollowTriangle`; only the box around it grows. For `CircledPlus` the far edge of the circle (box
position 6.0 + 5.0 = 11.0) sits on the line endpoint, the plus arms span the circle diameter
(each arm extends `CircledPlusArmHalfLength` = 5.0 either side of the circle center), and the circle
is background-filled by the renderers so the line shaft is hidden inside it.

### Notation Metrics Key Methods

`double AlongLineLength(EndMarkerStyle style)` — returns the along-line length consumed by an
end-marker decoration; zero for `EndMarkerStyle.None`. The legacy styles return their historical marker
box lengths unchanged; the appended styles return `TwoDotsMarkerLength` (`HollowTriangleTwoDots`),
`FourDotsMarkerLength` (`HollowTriangleFourDots`), and `CircledPlusMarkerBox` (`CircledPlus`), so the
connector router's end-leg and end-zone clearance and the renderers' corner clamp automatically
reserve space for the new decorations.

`double RenderedAlongLineLength(EndMarkerStyle style, Theme theme)` — returns `AlongLineLength(style)`
multiplied by `theme.StrokeWidth`: the length at which the SVG renderer draws the decoration, because
its `<marker>` elements scale their content by the stroke width (the SVG default
`markerUnits="strokeWidth"`), whereas the raster renderers draw at the notation size. A layout that
must keep a decoration clear of the corner that precedes it uses this conservative length.

`double AcrossHalfWidth(EndMarkerStyle style)` — returns the perpendicular half-extent of a marker
glyph measured from the line center: `EndMarkerHalfWidth` for the triangle family (including the two
dotted variants), `DiamondHalfWidth` for the diamonds, `CircleRadius` for the circle, `BarHalf` for the
bar, `CircledPlusRadius` for the circled plus, and zero for `None`.

`IReadOnlyList<MarkerVertex> DotCenters(EndMarkerStyle style)` — returns the tip-relative centers of
the dots trailing a dotted hollow triangle: two (one either side of the line) for
`HollowTriangleTwoDots`, four (a 2x2 grid) for `HollowTriangleFourDots`, and an empty list for every
other style. Placement is described in the marker geometry table above.

`(double Length, double RefX) TriangleFamilyBox(EndMarkerStyle style)` — returns the marker box length
and tip anchor of the hollow-triangle family: (`TwoDotsMarkerLength`, `TwoDotsRefX`) and
(`FourDotsMarkerLength`, `FourDotsRefX`) for the dotted variants, and (`EndMarkerLength`,
`EndMarkerRefX`) for every other style.

`IReadOnlyList<MarkerVertex> CornerIconVertices(BoxCornerIcon icon)` — returns the icon-local polygon
of a send or accept corner icon, with `Along` increasing to the right from the icon's left edge (0 to
`CornerIconWidth`) and `Across` increasing downward from its top (0 to `CornerIconHeight`). With
`ShoulderX` = `CornerIconWidth` - `CornerIconPointWidth` (8.0) and a mid height of 4.5, `Send` is the
convex pentagon (0, 0), (`ShoulderX`, 0), (`CornerIconWidth`, mid), (`ShoulderX`, `CornerIconHeight`),
(0, `CornerIconHeight`), pointing right; `Accept` is the same pentagon followed by one more vertex,
(`CornerIconNotchDepth`, mid), which cuts a triangular notch into the left edge. `BoxCornerIcon.None`
and any other value return an empty list.

`IReadOnlyList<MarkerVertex> TriangleVertices()` — returns the three triangle vertices in tip-relative
units, shared by the open chevron, hollow triangle, and filled arrow markers. The apex overshoots the
line endpoint by `EndMarkerTipOvershoot`.

`IReadOnlyList<MarkerVertex> DiamondVertices()` — returns the four diamond vertices in tip-relative
units, shared by the hollow and filled diamonds. The far point lands exactly on the line endpoint.

`double RoundedRectRadius(Theme theme)` — returns the theme corner radius scaled by
`RoundedRectCornerFactor`.

### Notation Metrics Error Handling

The constants and static helpers on `NotationMetrics` are pure functions. `RoundedRectRadius(Theme)`
and `RenderedAlongLineLength(EndMarkerStyle, Theme)` validate their `theme` argument with
`ArgumentNullException.ThrowIfNull` and propagate the resulting `ArgumentNullException` to the
caller. `AlongLineLength(EndMarkerStyle)` returns `0.0` for
`EndMarkerStyle.None` and for any other value returns the documented marker box length; it does not
throw for out-of-range enum values, treating them the same as `None`. `AcrossHalfWidth(EndMarkerStyle)`
likewise returns `0.0` for `None` and for out-of-range values. `DotCenters(EndMarkerStyle)` and
`CornerIconVertices(BoxCornerIcon)` return an empty list for a style or icon that carries no dots or
polygon, and `TriangleFamilyBox(EndMarkerStyle)` returns the plain triangle box for any
non-dotted style; none of them throw. The `TriangleVertices` and
`DiamondVertices` helpers take no arguments, cannot fail, and return the same immutable
`IReadOnlyList<MarkerVertex>` on every call. `MarkerVertex` is a readonly record struct with two
`double` fields and has no failure modes. No logging is performed.

### Notation Metrics Dependencies

- **Theme Unit** (same system) — `RoundedRectRadius(Theme)` reads `Theme.LineCornerRadius` and
  `RenderedAlongLineLength(EndMarkerStyle, Theme)` reads `Theme.StrokeWidth`; no other helper on
  `NotationMetrics` depends on `Theme`.
- **Rendering Model system (`DemaConsulting.Rendering`)** — the `EndMarkerStyle` enum is defined in
  the rendering model and is the parameter type for `AlongLineLength`, `RenderedAlongLineLength`,
  `AcrossHalfWidth`, `DotCenters`, and `TriangleFamilyBox`; the `BoxCornerIcon` enum is the parameter
  type for `CornerIconVertices`.
- **.NET base class library** — `System.Collections.Generic.IReadOnlyList<T>` for the vertex lists.
  No third-party runtime packages are consumed.

### Notation Metrics Callers

- **Rendering.Svg `SvgRenderer` unit** — reads the marker constants and calls the vertex, dot-centre,
  marker-box, and corner-icon helpers to emit the `<marker>` elements and box decorations.
- **Rendering.CanvasNet renderers** — read the same constants and helpers to draw identical decorations on
  the raster surface.
- **Rendering.Layout edge routers (`OrthogonalEdgeRouter`, `InterconnectionLayoutEngine`)** — call
  `AlongLineLength(EndMarkerStyle)` to reserve clean approach length for the end marker before the
  final endpoint. `LayeredLayoutAlgorithm` calls `RenderedAlongLineLength(EndMarkerStyle, Theme)` to
  derive the back-edge entry approach so the final approach clears the stroke-width-scaled SVG marker,
  and `ConnectorRouter` calls `AcrossHalfWidth(EndMarkerStyle)` for the marker's perpendicular
  extent.
- **Rendering.Abstractions `ConnectorLabelPlacer` unit** — calls `AlongLineLength` to keep a slid
  label clear of an end decoration.

### Notation Metrics Design Constraints

- The canonical marker values shall be the historical SVG marker dimensions (triangle 10x7 refX 9,
  diamond 14x8 refX 13, circle radius 4, bar 4x12); every renderer shall derive its markers from these
  constants so the two renderers draw the identical shape.
- Each derived constant shall be documented as a derivation of a named primitive so no geometry literal
  is duplicated.
- The `AlongLineLength` value of every pre-existing `EndMarkerStyle` shall be unchanged by the
  notation-primitive additions; only the three appended styles add arms.
- A dotted hollow triangle shall keep its triangle at exactly the position of a plain `HollowTriangle`
  (the tip anchor shifts by the dot extension) and its dots shall lie inside the marker's across-line
  extent (`EndMarkerHalfWidth`) and its marker box.
- The corner-icon proportions are centralized here so a correction to the icon shape is a change to
  constants and `CornerIconVertices` only.

### Notation Metrics Interactions

`NotationMetrics.RoundedRectRadius` reads `Theme.LineCornerRadius`. The end-marker helpers are called
by both renderers (SVG and PNG systems) and by the layout strategies (*Rendering Layout* system) that
reserve a clean approach using `AlongLineLength`.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Abstractions-NotationMetrics-TriangleDimensions | `NotationMetrics.TriangleVertices` and triangle values |
| Rendering-Abstractions-NotationMetrics-TriangleApexOvershoot | `NotationMetrics.TriangleVertices` apex overshoot |
| Rendering-Abstractions-NotationMetrics-DiamondDimensions | `NotationMetrics.DiamondVertices` and diamond constants |
| Rendering-Abstractions-NotationMetrics-DiamondFarPointOnEndpoint | `NotationMetrics.DiamondVertices` far point |
| Rendering-Abstractions-NotationMetrics-CircleBarGeometry | `NotationMetrics` circle and bar constants |
| Rendering-Abstractions-NotationMetrics-Crossbar | `NotationMetrics.CrossbarX` |
| Rendering-Abstractions-NotationMetrics-AlongLineLength | `NotationMetrics.AlongLineLength` |
| Rendering-Abstractions-NotationMetrics-PortSquare | `NotationMetrics` port square constants |
| Rendering-Abstractions-NotationMetrics-FolderTab | `NotationMetrics` folder-tab constants |
| Rendering-Abstractions-NotationMetrics-NoteFold | `NotationMetrics` note-fold constants |
| Rendering-Abstractions-NotationMetrics-RoundedRectCorner | `NotationMetrics.RoundedRectRadius` |
| Rendering-Abstractions-NotationMetrics-RejectNullTheme | `NotationMetrics.RoundedRectRadius` null-theme guard |
| Rendering-Abstractions-NotationMetrics-Badge | `NotationMetrics` badge constants |
| Rendering-Abstractions-NotationMetrics-LabelBackground | `NotationMetrics` label-background constants |
| Rendering-Abstractions-NotationMetrics-DottedTriangleMarkers | Dot constants, `DotCenters`, `TriangleFamilyBox` |
| Rendering-Abstractions-NotationMetrics-CircledPlusMarker | `NotationMetrics` circled-plus constants |
| Rendering-Abstractions-NotationMetrics-RenderedAlongLineLength | `NotationMetrics.RenderedAlongLineLength` |
| Rendering-Abstractions-NotationMetrics-AcrossHalfWidth | `NotationMetrics.AcrossHalfWidth` |
| Rendering-Abstractions-NotationMetrics-ThickBarBadge | `NotationMetrics.ThickBarThickness` |
| Rendering-Abstractions-NotationMetrics-CrossedCircleBadge | `NotationMetrics.CrossedCircleArmFraction` |
| Rendering-Abstractions-NotationMetrics-CornerIcon | Corner-icon constants, `CornerIconVertices` |
