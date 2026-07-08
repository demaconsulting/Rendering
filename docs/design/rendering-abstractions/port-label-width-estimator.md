## Port Label Width Estimator Unit Design

Part of the Rendering Abstractions system.

### Port Label Width Estimator Purpose

The Port Label Width Estimator unit provides the single, shared formula for estimating a port
label's horizontal advance width, so that the layout engine's layout-time sizing decisions (content
insets, `MaxLabelWidth` floor) and the SVG renderer's render-time squeeze decision (whether a
`textLength` constraint is needed) can never disagree about what "natural width" means for the same
label text.

### Port Label Width Estimator Data Model

- `PortLabelWidthEstimator` (static class) — `MeasureWidth(string text, double fontSize)`.
- `NotoSansRelativeWidths` (private lookup table) — approximate relative advance width, in logical
  pixels at a nominal 100px font size, for each mapped character (space, digits, upper/lowercase
  Latin letters, and common punctuation/symbols), modeled on Noto Sans — the one font family the
  bundled renderers hardcode.
- `MedianWidth` (private constant, `55.0`) — fallback width for any character absent from the table.

### Port Label Width Estimator Key Methods

`double MeasureWidth(string text, double fontSize)` — sums each character's mapped (or
median-fallback) relative width from `NotoSansRelativeWidths`, then scales the total from the
table's nominal 100px basis to `fontSize` (i.e. `total * (fontSize / 100.0)`). An empty string
measures as zero.

### Port Label Width Estimator Error Handling

`MeasureWidth` validates its `text` argument with `ArgumentNullException.ThrowIfNull` and propagates
the resulting `ArgumentNullException` to the caller — the same fail-fast contract used by the
sibling `BoxMetrics`/`NotationMetrics` units. Beyond that guard, the method is pure arithmetic over
the input string's characters and never throws for any character value, including characters absent
from the mapped table (they use `MedianWidth` instead).

### Port Label Width Estimator Dependencies

- **.NET base class library** — only `System.Double`/`System.String` arithmetic and
  `System.Collections.Generic.Dictionary`. No third-party runtime packages are consumed.

### Port Label Width Estimator Callers

- **Rendering.Layout `LayeredLayoutAlgorithm` unit** — calls `MeasureWidth` at layout time to size a
  node's `ContentInsetLeft`/Right/Top/Bottom reserved margins for its ports' labels and to compute
  each port's `MaxLabelWidth` floor, at `CoreOptions.AssumedFontSize`.
- **Rendering.Svg `SvgRenderer` unit** — calls `MeasureWidth` (via `FitTextLength`'s
  `useAccurateEstimator: true` mode) at render time to decide whether a port label's rendered
  `<text>` element needs a `textLength`/`lengthAdjust` squeeze constraint, so a label whose
  `MaxLabelWidth` already covers its measured natural width never receives one.

### Port Label Width Estimator Design Constraints

- This is intentionally a "good enough to avoid colliding with box content" heuristic, not an exact
  font-metric measurement: no label anywhere in this codebase wraps, truncates, or measures exactly.
- Every consumer that needs to know a port label's natural rendered width shall call this shared
  estimator rather than maintaining an independent estimate, so layout-time and render-time
  measurements can never disagree for the same label.

### Port Label Width Estimator Interactions

`PortLabelWidthEstimator` has no dependencies on other Abstractions units. It is called by
`LayeredLayoutAlgorithm` (*Rendering Layout* system) during layout and by `SvgRenderer` (*Rendering
Svg* system) during rendering — the exact cross-system sharing pattern this promotion from
`Rendering.Layout` into `Rendering.Abstractions` was designed to enable.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Abstractions-PortLabelWidthEstimator-MeasureWidth | `PortLabelWidthEstimator.MeasureWidth` |
| Rendering-Abstractions-PortLabelWidthEstimator-EmptyText | `MeasureWidth` empty-string handling |
| Rendering-Abstractions-PortLabelWidthEstimator-UnknownCharacterFallback | `MeasureWidth` median-fallback lookup |
| Rendering-Abstractions-PortLabelWidthEstimator-RejectNullText | `MeasureWidth` null-argument guard |
