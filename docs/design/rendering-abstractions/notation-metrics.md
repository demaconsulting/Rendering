# Notation Metrics Unit Design

Part of the Rendering Abstractions system.

## Notation Metrics Overview

The Notation Metrics unit is the single home for all intrinsic, theme-independent notation geometry
shared by the SVG and PNG renderers: end-marker (arrowhead) shapes and sizes, the port square,
folder-tab and note-fold proportions, rounded-rectangle corner scaling, badge fractions, and the
label-background inset. Every value is either a documented primitive constant or a documented
derivation of those primitives, so a geometry literal never appears more than once in the rendering
path. `MarkerVertex` expresses one vertex in tip-relative units.

## Notation Metrics Data Model

- `NotationMetrics` (static class) — the end-marker, port, folder, note, badge, and label constants
  plus the `AlongLineLength`, `TriangleVertices`, `DiamondVertices`, and `RoundedRectRadius` helpers.
- `MarkerVertex` (readonly record struct) — `Along` (distance back from the tip) and `Across`
  (perpendicular offset).

## Notation Metrics Key Methods

`double AlongLineLength(EndMarkerStyle style)` — returns the along-line length consumed by an
end-marker decoration; zero for `EndMarkerStyle.None`.

`IReadOnlyList<MarkerVertex> TriangleVertices()` — returns the three triangle vertices in tip-relative
units, shared by the open chevron, hollow triangle, and filled arrow markers. The apex overshoots the
line endpoint by `EndMarkerTipOvershoot`.

`IReadOnlyList<MarkerVertex> DiamondVertices()` — returns the four diamond vertices in tip-relative
units, shared by the hollow and filled diamonds. The far point lands exactly on the line endpoint.

`double RoundedRectRadius(Theme theme)` — returns the theme corner radius scaled by
`RoundedRectCornerFactor`.

## Notation Metrics Design Constraints

- The canonical marker values shall be the historical SVG marker dimensions (triangle 10x7 refX 9,
  diamond 14x8 refX 13, circle radius 4, bar 4x12); every renderer shall derive its markers from these
  constants so the two renderers draw the identical shape.
- Each derived constant shall be documented as a derivation of a named primitive so no geometry literal
  is duplicated.

## Notation Metrics Interactions

`NotationMetrics.RoundedRectRadius` reads `Theme.LineCornerRadius`. The end-marker helpers are called
by both renderers (SVG and PNG systems) and by the layout strategies (*Rendering Layout* system) that
reserve a clean approach using `AlongLineLength`.

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Abstractions-NotationMetrics-TriangleGeometry | `NotationMetrics.TriangleVertices` and triangle constants |
| Rendering-Abstractions-NotationMetrics-DiamondGeometry | `NotationMetrics.DiamondVertices` and diamond constants |
| Rendering-Abstractions-NotationMetrics-CircleBarGeometry | `NotationMetrics` circle and bar constants |
| Rendering-Abstractions-NotationMetrics-Crossbar | `NotationMetrics.CrossbarX` |
| Rendering-Abstractions-NotationMetrics-AlongLineLength | `NotationMetrics.AlongLineLength` |
| Rendering-Abstractions-NotationMetrics-BoxDecorations | `NotationMetrics` decoration constants |
