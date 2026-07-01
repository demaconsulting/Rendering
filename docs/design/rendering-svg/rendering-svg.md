# DemaConsulting.Rendering.Svg

## Overview

`DemaConsulting.Rendering.Svg` is the SVG renderer system of the DEMA Consulting rendering
library. It provides a single public class, `SvgRenderer`, that translates a placed
`LayoutTree` into a self-contained SVG 1.1 document written to an output stream. The
renderer has zero external runtime dependencies beyond the .NET base class library and the
in-house rendering packages, so its output can be embedded anywhere without additional
assets.

## SvgRenderer

### Purpose

`SvgRenderer` implements the `IRenderer` interface to produce SVG 1.1 diagram output from a
`LayoutTree` intermediate representation. Each call to `Render` builds a complete SVG
document in a `StringBuilder` and writes it to the supplied stream in UTF-8 encoding. The
renderer is pure and stateless; no fields are mutated between calls, so a single instance
may be reused concurrently.

### Data Model

`SvgRenderer` has no instance state. All inputs are supplied through `Render` parameters.

- `LayoutTree` — read-only input; canvas dimensions and the list of placed nodes
- `RenderOptions` — read-only input; `Theme` for visual parameters, `Scale` for sizing
- `Stream output` — write-only output; receives UTF-8 SVG bytes; caller owns lifetime

### Font Family

All text elements use `font-family="Noto Sans, sans-serif"`. The `Noto Sans` family is
specified first so that browsers and renderers with Noto Sans installed use it; `sans-serif`
is the CSS generic fallback. Naming Noto Sans first keeps the SVG output visually aligned
with the PNG renderer, which embeds the same font for pixel-identical rasterization.

### Font Weight and Style Per Node Type

Each node type uses a fixed font weight and style as SVG attributes:

| Node Type | `font-weight` | `font-style` |
| --- | --- | --- |
| `LayoutBox` label | `bold` | (default) |
| `LayoutBoxCompartment` title | `bold` | `italic` |
| `LayoutBoxCompartment` rows | (default) | (default) |
| `LayoutLine` midpoint label | (default) | (default) |
| `LayoutLabel` | Per `Weight` field | Per `Style` field |
| `LayoutPort` label | (default) | (default) |
| `LayoutBadge` label | (default) | (default) |
| `LayoutBand` label | (default) | (default) |
| `LayoutLifeline` label | `bold` | (default) |
| `LayoutGrid` header cells | `bold` | (default) |
| `LayoutGrid` body cells | (default) | (default) |

### LayoutLabel Font Styling Fields

`LayoutLabel` carries three explicit font styling fields:

- `Weight` (`FontWeight`) — `Regular` maps to `font-weight="normal"`; `Bold` maps to
  `font-weight="bold"`.
- `Style` (`FontStyle`) — `Normal` maps to `font-style="normal"`; `Italic` maps to
  `font-style="italic"`.
- `FontSize` (double) — Font size in logical pixels, used as `font-size` instead of the
  theme body size.

### Text Length Shrink-to-Fit

`LayoutBox` labels and `LayoutLabel` nodes are constrained to their available width only
when the text would otherwise overflow it. `FitTextLength` estimates each label's natural
width (character count multiplied by font size and an average glyph-width factor) and
compares it to the available width — `box.Width - 2 * theme.LabelPadding` for box labels,
or `MaxWidth` for a `LayoutLabel`. Only when the estimate exceeds the available width does
the renderer emit `textLength="{availableWidth * scale}"` together with
`lengthAdjust="spacingAndGlyphs"`, which instructs SVG viewers to compress the glyph
spacing so the text shrinks into the available area without overflow.

When the text already fits (or the available width is non-positive), no `textLength` or
`lengthAdjust` attribute is emitted, so short labels render at their natural width and are
never stretched to fill the box.

### Key Methods

**`Render(LayoutTree layout, RenderOptions options, Stream output)`**

Entry point. Validates arguments, computes canvas size clamped to a minimum of 1x1, writes
the SVG root element with `xmlns`, `width`, `height`, and `viewBox` attributes, then calls
`WriteEndMarkerDefs` followed by recursive `RenderNode` calls for every top-level node.
Encodes the completed `StringBuilder` as UTF-8 and writes all bytes to `output` in a single
`Write` call.

**`WriteEndMarkerDefs(StringBuilder sb, Theme theme)`**

Writes the SVG `<defs>` block containing the named line-end marker elements, including
`line-end-open-chevron` (open chevron drawn as a `<polyline>` with no closing base edge),
`line-end-hollow-triangle`, `line-end-filled-arrow`, `line-end-hollow-diamond`,
`line-end-filled-diamond`, `line-end-circle`, `line-end-bar`, and
`line-end-hollow-triangle-crossbar`. All markers use `theme.StrokeColor` and
`theme.StrokeWidth`.

Every marker coordinate is derived from the single-source notation geometry in
`NotationMetrics` (for example `EndMarkerLength`, `EndMarkerWidth`, `EndMarkerRefX`,
`DiamondLength`, `CircleRadius`, and the `TriangleVertices()`/`DiamondVertices()` helpers
that return `MarkerVertex` values). Because the SVG and PNG renderers read from the same
`NotationMetrics` source in `DemaConsulting.Rendering.Abstractions`, their end markers are
geometrically identical. `line-end-open-chevron` is the only open marker — it omits the
closing base edge so the chevron renders as two strokes meeting at the apex; the hollow
triangle and both diamonds remain closed.

**`RenderNode(StringBuilder sb, LayoutNode node, Theme theme, double scale)`**

Dispatches by concrete node type to the appropriate typed render method. All nine
`LayoutNode` subtypes are handled: `LayoutBox` → `RenderBox`, `LayoutLine` → `RenderLine`,
`LayoutLabel` → `RenderLabel`, `LayoutPort` → `RenderPort`, `LayoutBadge` → `RenderBadge`,
`LayoutBand` → `RenderBand`, `LayoutLifeline` → `RenderLifeline`,
`LayoutActivation` → `RenderActivation`, `LayoutGrid` → `RenderGrid`. Unknown subtypes are
silently skipped for forward compatibility.

**`RenderBox(StringBuilder sb, LayoutBox box, Theme theme, double scale)`**

Selects the fill color from `theme.DepthFillColors[box.Depth % count]` and delegates to
`RenderBoxOutline`, `RenderFolderOutline`, or `RenderNoteOutline` depending on `box.Shape`.
The rectangle outline adds `rx`/`ry` attributes when `BoxShape.RoundedRectangle` and
`LineCornerRadius > 0`. Calls `RenderBoxTitle` to write the bold `<text>` title (with
`textLength` from `FitTextLength`) when `box.Label` is non-null, then `RenderBoxCompartments`
for any compartments, then recursively calls `RenderNode` for all `box.Children`.

**`RenderBoxCompartments(StringBuilder sb, LayoutBox box, Theme theme, double scale)`**

Writes a `<line>` divider across the full box width at the top of each compartment, followed
by an optional `font-weight="bold" font-style="italic"` `<text>` title row and zero or more
left-aligned regular-weight body-font `<text>` rows.

**`RenderLine(StringBuilder sb, LayoutLine line, Theme theme, double scale)`**

Calls `BuildLinePath` to produce the path `d` attribute, then writes a `<path>` element with
`fill="none"`. Adds `marker-start` or `marker-end` attributes for the non-None
`EndMarkerStyle` values, referencing the `SourceEnd` and `TargetEnd` line-end markers. Adds
`stroke-dasharray` for `Dashed` and `Dotted` line styles. Calls `RenderLineLabel` to write
an optional midpoint `<text>` element when `MidpointLabel` is non-null.

**`BuildLinePath(...)`**

Builds the SVG path `d` string. When `cornerRadius` is zero, emits plain `M`/`L` commands.
When positive, each interior waypoint is replaced with a shortened `L` command to the arc
start point, followed by an `A` (elliptical arc) command whose sweep direction is determined
from the cross product of the incoming and outgoing unit direction vectors. The radius is
clamped to half the shorter adjacent segment to prevent overshoot. At the first and last
bends the radius is additionally clamped so the rounded corner completes at least the
marker's along-line length (`NotationMetrics.AlongLineLength` for the `SourceEnd`/`TargetEnd`
styles) before the endpoint, so the curve never intrudes into the end-marker zone.

**`RenderLabel(StringBuilder sb, LayoutLabel label, Theme theme, double scale)`**

Writes a `<text>` element with `text-anchor` from `label.Align`, `font-size` from
`label.FontSize`, and `font-weight`/`font-style` from `label.Weight` and `label.Style`. When
`label.MaxWidth > 0`, adds `textLength` and `lengthAdjust="spacingAndGlyphs"`.

**`RenderPort` / `RenderBadge` / `RenderBand` / `RenderLifeline` / `RenderActivation` /
`RenderGrid`**

Each typed method writes the SVG elements appropriate to its node kind: an 8x8 `<rect>` for
ports; `<circle>`, `<polygon>`, or `<line>` for the badge shapes; a `<rect>` with a rotated
or horizontal `<text>` for bands; a header `<rect>`, bold `<text>`, and dashed `<line>` stem
for lifelines; a white `<rect>` border for activations; and per-cell `<rect>` plus `<text>`
elements for grids. Fills come from `theme.DepthFillColors`, and header cells add
`font-weight="bold"`.

### Error Handling

`Render` throws `ArgumentNullException` when `layout`, `options`, or `output` is null. No
other exceptions are expected under normal operation. XML special characters in labels are
escaped via `EscapeXml` (replaces `&`, `<`, `>` with XML entities) to prevent malformed SVG
output.

### Dependencies

- `DemaConsulting.Rendering` — provides `LayoutTree` and all nine `LayoutNode` subtypes (the
  Model IR / LayoutGraph)
- `DemaConsulting.Rendering.Abstractions` — provides `IRenderer`, `RenderOptions`, `Theme`,
  `Themes`, `NotationMetrics`, `BoxMetrics`, and `MarkerVertex`

### Callers

- Any consumer of the rendering library that selects vector output constructs an
  `SvgRenderer` and calls `IRenderer.Render` with a placed `LayoutTree` and `RenderOptions`.

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Svg-SvgRenderer-ImplementsIRenderer | `IRenderer` implementation; `MediaType`/`DefaultExtension` |
| Rendering-Svg-SvgRenderer-RenderDocument | `Render` writes the SVG root element |
| Rendering-Svg-SvgRenderer-RenderBox | `RenderBox` writes the `<rect>` outline and compartments |
| Rendering-Svg-SvgRenderer-RenderLabel | `RenderLabel` writes `<text>` with styling and escaping |
| Rendering-Svg-SvgRenderer-RenderLine | `RenderLine` writes `<path>` with corners, dashes, labels |
| Rendering-Svg-SvgRenderer-RenderNodeKinds | Port, badge, band, lifeline, activation, and grid render methods |
| Rendering-Svg-SvgRenderer-EndMarkers | `WriteEndMarkerDefs` and marker references in `RenderLine` |
