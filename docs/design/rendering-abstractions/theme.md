## Theme Unit Design

Part of the Rendering Abstractions system.

### Theme Purpose

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

### Theme Error Handling

`Theme` is a `sealed record` whose values are supplied through its primary constructor; the compiler
enforces that every property is initialised at construction time and no runtime validation is
performed by the type itself. `ConnectorApproachZone(double)` and the `BackgroundColor` accessor are
pure arithmetic and indexed reads over the record's own state, so they cannot fail for a
well-formed `Theme`. Callers that supply their own themes are expected to populate
`DepthFillColors` with at least one entry (indexed as `[0]` by `BackgroundColor`); an empty
collection would surface as `IndexOutOfRangeException` from the underlying list access. The built-in
`Themes.Light`, `Themes.Dark`, and `Themes.Print` instances satisfy this precondition by
construction. No logging is performed.

### Theme Dependencies

- **.NET base class library** — `System.Collections.Generic.IReadOnlyList<string>` for
  `DepthFillColors`. No other runtime dependency.
- No dependency on other units, OTS runtime components, or Shared Packages.

### Theme Callers

- **Rendering Contracts Unit** (same system) — `RenderOptions.Theme` carries a `Theme` reference into
  the render invocation.
- **NotationMetrics Unit** (same system) — `NotationMetrics.RoundedRectRadius(Theme)` reads
  `Theme.LineCornerRadius`.
- **BoxMetrics Unit** (same system) — `BoxMetrics.FolderTabHeight(Theme)` and
  `BoxMetrics.TitleAreaHeight(Theme, bool, bool)` read `FontSizeBody`, `FontSizeTitle`, and
  `LabelPadding`.
- **Rendering.Svg and Rendering.Skia renderer systems** — read the theme's colors, stroke, font
  sizes, and padding directly, and call `ConnectorApproachZone` when reserving connector approach
  space.
- **Rendering.Layout engines** — read `ConnectorStub`, `BendRadius`, and `CleanLegMargin` when
  reserving space for connector routing.

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
