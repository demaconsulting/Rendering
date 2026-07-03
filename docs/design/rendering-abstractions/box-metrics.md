## Box Metrics Unit Design

Part of the Rendering Abstractions system.

### Box Metrics Purpose

The Box Metrics unit provides the shared formulas that compute a box's title-area height and
folder-tab height from a `Theme`, so that the space the layout strategies reserve equals the space the
renderers draw.

### Box Metrics Data Model

- `BoxMetrics` (static class) — `FolderTabHeight(Theme)` and `TitleAreaHeight(Theme, bool, bool)`.

### Box Metrics Key Methods

`double FolderTabHeight(Theme theme)` — returns `theme.FontSizeBody + 2 * theme.LabelPadding`.

`double TitleAreaHeight(Theme theme, bool hasLabel, bool hasKeyword)` — returns the vertical space
reserved at the top of a box: zero when the box has neither a name nor a keyword; otherwise a leading
padding plus, conditionally, a keyword line and a name line, each followed by a padding.

### Box Metrics Error Handling

Both helpers are pure arithmetic over the fields of the supplied `Theme`. `FolderTabHeight(Theme)`
and `TitleAreaHeight(Theme, bool, bool)` currently perform no explicit argument validation and, per
the C# language contract, dereferencing a `null` theme parameter will raise a
`NullReferenceException` at the point of the first property access. Callers are therefore expected
to pass a non-null theme (the built-in `Themes.Light`, `Themes.Dark`, and `Themes.Print`, or a
caller-constructed instance). The helpers do not catch or transform any exception raised by
`Theme` property accessors, and no logging is performed. No other error paths exist: the helpers
never throw for boundary combinations of `hasLabel` and `hasKeyword`, and the empty-title case
(`hasLabel == false && hasKeyword == false`) is handled explicitly by returning `0.0`.

### Box Metrics Dependencies

- **Theme Unit** (same system) — reads `Theme.FontSizeBody`, `Theme.FontSizeTitle`, and
  `Theme.LabelPadding`.
- **.NET base class library** — only `System.Double` arithmetic. No third-party runtime packages
  are consumed.

### Box Metrics Callers

- **Rendering.Svg `SvgRenderer` unit** — calls `FolderTabHeight` when drawing folder-shaped boxes
  and `TitleAreaHeight` when placing the title text of every box.
- **Rendering.Skia raster renderers** — call the same helpers for the raster output so drawn
  geometry matches the SVG.
- **Rendering.Layout box layout strategies (`ContainmentPacker`, `LayeredPipeline`,
  `HierarchicalLayoutAlgorithm`)** — call both helpers to reserve space for the folder tab and title
  area when computing box sizes.

### Box Metrics Design Constraints

- `TitleAreaHeight` shall reserve no space when a box has neither a name label nor a keyword line.
- Both the layout strategies and the renderers shall compute box title and folder-tab heights from
  these formulas, so reserved space and drawn space always agree.

### Box Metrics Interactions

`BoxMetrics` reads `Theme.FontSizeBody`, `FontSizeTitle`, and `LabelPadding`. It is called by the
renderers (SVG and PNG systems) and by the box layout strategies (*Rendering Layout* system).

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Abstractions-BoxMetrics-FolderTabHeight | `BoxMetrics.FolderTabHeight` |
| Rendering-Abstractions-BoxMetrics-TitleAreaHeight | `BoxMetrics.TitleAreaHeight` |
