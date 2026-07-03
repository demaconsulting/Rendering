## Theme Unit Design

Part of the Rendering Abstractions system.

### Theme Overview

The Theme unit defines the visual parameters for rendering — depth-indexed fill colors, stroke color
and width, corner radius, font sizes, padding, and connector-approach geometry — and provides three
ready-made themes (`Light`, `Dark`, `Print`). Font choice is intentionally not part of the theme; each
renderer hardcodes its own typeface for consistent output.

### Theme Data Model

- `Theme` (sealed record) — `DepthFillColors`, `StrokeColor`, `StrokeWidth`, `LineCornerRadius`,
  `FontSizeTitle`, `FontSizeBody`, `LabelPadding`, `ConnectorStub`, `BendRadius`, `CleanLegMargin`.
- `Themes` (static class) — the built-in `Light`, `Dark`, and `Print` themes.

### Theme Key Methods

`double ConnectorApproachZone(double connectorClearance)` — returns the clear distance a connector
needs off a box face before it can bend, computed as `ConnectorStub + BendRadius + connectorClearance`.

`string BackgroundColor` — the depth-0 fill color, used to occlude a connector line behind a hollow
enclosing end marker.

### Theme Design Constraints

- `ConnectorApproachZone` shall equal the sum of the perpendicular stub, the corner bend radius, and
  the caller-supplied clearance, so that space reserved by layout matches geometry drawn by renderers.
- The `Light` and `Dark` themes shall carry identical connector geometry; the `Print` theme shall use a
  tighter stub and a zero bend radius suited to monochrome output.

### Theme Interactions

`Theme` is carried by `RenderOptions` (Rendering Contracts unit) and read by `NotationMetrics` and
`BoxMetrics` when deriving box and marker geometry.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Abstractions-Theme-ApproachZone | `Theme.ConnectorApproachZone` |
| Rendering-Abstractions-Theme-BuiltInGeometry | `Themes.Light`, `Themes.Dark`, and `Themes.Print` |
