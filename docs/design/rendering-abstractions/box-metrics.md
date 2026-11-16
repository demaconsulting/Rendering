## Box Metrics Unit Design

Part of the Rendering Abstractions system.

### Box Metrics Purpose

The Box Metrics unit provides the shared formulas that compute a box's title-area height, folder-tab
height and width, and lifeline header height from a `Theme` (and, where the box's appearance matters,
from the `LayoutBox` itself), so that the space the layout strategies reserve equals the space the
renderers draw.

### Box Metrics Data Model

- `BoxMetrics` (static class) — `FolderTabHeight(Theme)`, `TitleAreaHeight(Theme, bool, bool)`,
  `TitleAreaHeight(LayoutBox, Theme)`, `IsNameInTab(LayoutBox)`,
  `IsNameInTab(BoxShape, string?, BoxAppearance?)`, `ResolveFolderTabHeight(LayoutBox, Theme)`,
  `ResolveFolderTabWidth(LayoutBox, Theme)`, `LifelineHeaderHeight(Theme, bool)`, and the existing
  `ResolveTitleVsSidePortContentInsetTop` and `TitleCursorTop` helpers.

### Box Metrics Key Methods

`double FolderTabHeight(Theme theme)` — returns `theme.FontSizeBody + 2 * theme.LabelPadding`.

`double TitleAreaHeight(Theme theme, bool hasLabel, bool hasKeyword)` — returns the vertical space
reserved at the top of a box: zero when the box has neither a name nor a keyword; otherwise a leading
padding plus, conditionally, a keyword line and a name line, each followed by a padding.

`double TitleAreaHeight(LayoutBox box, Theme theme)` — the box-aware overload. It equals
`TitleAreaHeight(theme, box.Label != null, box.Keyword != null)` for every box except one whose name is
drawn in the folder tab (`IsNameInTab(box)`), which counts no name line: its body title band holds only
the keyword line, if any, and is zero without one. A box with no `Appearance` therefore always gets the
legacy result. `TitleCursorTop` uses this overload, so a centered title block of a name-in-tab folder
is positioned from the keyword-only height.

`bool IsNameInTab(LayoutBox box)` and `bool IsNameInTab(BoxShape shape, string? label, BoxAppearance?
appearance)` — return `true` only for a `Folder` shape that has a label and whose appearance requests
`FolderTitlePlacement.Tab`. The second overload lets layout code that has only an input graph node's
shape, label, and appearance (no `LayoutBox` yet) apply exactly the rule the renderers apply.

`double ResolveFolderTabHeight(LayoutBox box, Theme theme)` — returns the box's explicit
`FolderTabHeight` clamped to zero when it has one, otherwise `FolderTabHeight(theme)`.

`double ResolveFolderTabWidth(LayoutBox box, Theme theme)` — returns the box's explicit
`FolderTabWidth` clamped to zero when it has one. Without one, a box that draws its name in the tab
gets a tab sized to the measured name: `PortLabelWidthEstimator.MeasureWidth(label, FontSizeBody) +
2 * LabelPadding`, never narrower than `NotationMetrics.FolderTabMinWidth` and never wider than the box.
Any other folder keeps the historical estimate: the smaller of `box.Width *
FolderTabMaxWidthFraction` and the larger of `FolderTabMinWidth` and `label length * FontSizeBody *
FolderLabelCharWidthFactor + 2 * LabelPadding` (a four-character label length when there is no label).
These two resolvers centralize the tab rectangle that layout, the connector router, and both renderers
previously each derived.

`double LifelineHeaderHeight(Theme theme, bool hasKeyword)` — returns `LabelPadding + FontSizeBody +
LabelPadding` for the label line, plus `FontSizeBody + LabelPadding` for a keyword line above it. Callers
size a lifeline's `HeaderHeight` from it.

### Box Metrics Error Handling

Every helper that takes a `Theme` or a `LayoutBox` validates that argument with
`ArgumentNullException.ThrowIfNull` and propagates the resulting `ArgumentNullException` to the caller —
the same fail-fast contract used by the
sibling `NotationMetrics` unit. Callers therefore receive a clear, documented exception (rather than
an undocumented `NullReferenceException`) when a `null` theme or box is supplied. The
`IsNameInTab(BoxShape, string?, BoxAppearance?)` overload accepts `null` for its label and appearance
and returns `false`. Beyond those guards, the helpers are pure arithmetic over the fields of the supplied
`Theme` and `LayoutBox` (`ResolveFolderTabWidth` additionally calls `PortLabelWidthEstimator.MeasureWidth`):
they do not catch or transform
any exception raised by `Theme` property accessors, and no logging is performed. No other error
paths exist: the helpers never throw for boundary combinations of `hasLabel` and `hasKeyword`, and
the empty-title case (`hasLabel == false && hasKeyword == false`) is handled explicitly by
returning `0.0`.

### Box Metrics Dependencies

- **Theme Unit** (same system) — reads `Theme.FontSizeBody`, `Theme.FontSizeTitle`, and
  `Theme.LabelPadding`.
- **Notation Metrics Unit** and **Port Label Width Estimator Unit** (same system) —
  `ResolveFolderTabWidth` reads the folder-tab constants from `NotationMetrics` and measures the
  in-tab name with `PortLabelWidthEstimator.MeasureWidth`.
- **Rendering Model system (`DemaConsulting.Rendering`)** — reads `LayoutBox` (`Label`, `Keyword`,
  `Shape`, `Appearance`, `FolderTabWidth`, `FolderTabHeight`, `Width`) and `BoxAppearance.FolderTitle`.
- **.NET base class library** — only `System.Double` arithmetic. No third-party runtime packages
  are consumed.

### Box Metrics Callers

- **Rendering.Svg `SvgRenderer` unit** — calls `ResolveFolderTabHeight`/`ResolveFolderTabWidth` when
  drawing folder-shaped boxes, `TitleAreaHeight` when placing the title text and compartments of
  every box, and `LifelineHeaderHeight` when centering a lifeline header's keyword and label lines.
- **Rendering.CanvasNet raster renderers** — call the same helpers for the raster output so drawn
  geometry matches the SVG.
- **Rendering.Layout box layout strategies (`ContainmentPacker`, `LayeredPipeline`,
  `HierarchicalLayoutAlgorithm`) and `ConnectorRouter`** — call the helpers to reserve space for the
  folder tab and title area when computing box sizes and to share the folder-tab rectangle
  (`IsNameInTab` to decide whether the title band reserves a name line).

### Box Metrics Design Constraints

- `TitleAreaHeight` shall reserve no space when a box has neither a name label nor a keyword line.
- Both the layout strategies and the renderers shall compute box title and folder-tab heights from
  these formulas, so reserved space and drawn space always agree.
- `TitleAreaHeight(LayoutBox, Theme)` shall equal the flag-based overload for every box without a
  name-in-tab folder appearance, and `ResolveFolderTabWidth` shall return the historical estimate for
  every folder that does not draw its name in the tab, so default boxes are unchanged.
- The in-tab rule shall be defined once (`IsNameInTab`) and shared by layout and both renderers.

### Box Metrics Interactions

`BoxMetrics` reads `Theme.FontSizeBody`, `FontSizeTitle`, and `LabelPadding`, the `LayoutBox` fields
listed above, and, for the in-tab tab width, `NotationMetrics` and `PortLabelWidthEstimator`. It is
called by the renderers (SVG and PNG systems) and by the box layout strategies (*Rendering Layout*
system).

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Abstractions-BoxMetrics-FolderTabHeight | `BoxMetrics.FolderTabHeight` |
| Rendering-Abstractions-BoxMetrics-TitleAreaHeight | `BoxMetrics.TitleAreaHeight` |
| Rendering-Abstractions-BoxMetrics-RejectNullTheme | `FolderTabHeight`/`TitleAreaHeight` null-argument guard |
| Rendering-Abstractions-BoxMetrics-FolderTabResolution | `ResolveFolderTabHeight`, `ResolveFolderTabWidth` |
| Rendering-Abstractions-BoxMetrics-TitleAreaHeightForBox | `TitleAreaHeight(LayoutBox, Theme)`, `IsNameInTab` |
| Rendering-Abstractions-BoxMetrics-LifelineHeaderHeight | `BoxMetrics.LifelineHeaderHeight` |
