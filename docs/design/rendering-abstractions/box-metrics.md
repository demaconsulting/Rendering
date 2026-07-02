# Box Metrics Unit Design

Part of the Rendering Abstractions system.

## Box Metrics Overview

The Box Metrics unit provides the shared formulas that compute a box's title-area height and
folder-tab height from a `Theme`, so that the space the layout strategies reserve equals the space the
renderers draw.

## Box Metrics Data Model

- `BoxMetrics` (static class) — `FolderTabHeight(Theme)` and `TitleAreaHeight(Theme, bool, bool)`.

## Box Metrics Key Methods

`double FolderTabHeight(Theme theme)` — returns `theme.FontSizeBody + 2 * theme.LabelPadding`.

`double TitleAreaHeight(Theme theme, bool hasLabel, bool hasKeyword)` — returns the vertical space
reserved at the top of a box: zero when the box has neither a name nor a keyword; otherwise a leading
padding plus, conditionally, a keyword line and a name line, each followed by a padding.

## Box Metrics Design Constraints

- `TitleAreaHeight` shall reserve no space when a box has neither a name label nor a keyword line.
- Both the layout strategies and the renderers shall compute box title and folder-tab heights from
  these formulas, so reserved space and drawn space always agree.

## Box Metrics Interactions

`BoxMetrics` reads `Theme.FontSizeBody`, `FontSizeTitle`, and `LabelPadding`. It is called by the
renderers (SVG and PNG systems) and by the box layout strategies (*Rendering Layout* system).

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Abstractions-BoxMetrics-FolderTabHeight | `BoxMetrics.FolderTabHeight` |
| Rendering-Abstractions-BoxMetrics-TitleAreaHeight | `BoxMetrics.TitleAreaHeight` |
