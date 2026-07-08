## SvgRenderer Unit Design

Part of the Rendering.Svg system.

### Purpose

`SvgRenderer` has a single responsibility: translate a placed `LayoutTree` into a self-contained
SVG 1.1 document written as UTF-8 bytes to a caller-supplied `Stream`. It realizes the
`IRenderer` contract from `DemaConsulting.Rendering.Abstractions` for the vector output path.
It does not perform layout, mutate the model, choose a theme, own the output stream, or persist
the emitted bytes; those responsibilities remain with the caller and with upstream systems.

### SvgRenderer Overview

`SvgRenderer` implements the `IRenderer` interface to produce SVG 1.1 diagram output from a
`LayoutTree` intermediate representation. Each call to `Render` builds a complete SVG document in a
`StringBuilder` and writes it to the supplied stream in UTF-8 encoding. The renderer is pure and
stateless; no fields are mutated between calls, so a single instance may be reused concurrently.

### SvgRenderer Data Model

`SvgRenderer` has no instance state. All inputs are supplied through `Render` parameters.

- `LayoutTree` — read-only input; canvas dimensions and the list of placed nodes.
- `RenderOptions` — read-only input; `Theme` for visual parameters and `Scale` for sizing.
- `Stream output` — write-only output; receives UTF-8 SVG bytes; caller owns lifetime.

### Font Family

All text elements use `font-family="Noto Sans, sans-serif"`. The `Noto Sans` family is specified
first so that browsers and renderers with Noto Sans installed use it; `sans-serif` is the CSS generic
fallback. Naming Noto Sans first keeps the SVG output visually aligned with the PNG renderer, which
embeds the same font for pixel-identical rasterization.

### Font Weight and Style Per Node Type

Each node type uses a fixed font weight and style as SVG attributes:

| Node Type | `font-weight` | `font-style` |
| --- | --- | --- |
| `LayoutBox` label | `bold` | (default) |
| `LayoutBoxCompartment` title | `bold` | `italic` |
| `LayoutBoxCompartment` rows | (default) | (default) |
| `LayoutLine` connector label | (default) | (default) |
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
- `FontSize` (double) — font size in logical pixels, used as `font-size` instead of the theme body
  size.

### Text Length Shrink-to-Fit

`LayoutBox` labels and `LayoutLabel` nodes are constrained to their available width only when the text
would otherwise overflow it. `FitTextLength` estimates each label's natural width (character count
multiplied by font size and an average glyph-width factor) and compares it to the available width:
`box.Width - 2 * theme.LabelPadding` for box labels, or `MaxWidth` for a `LayoutLabel`. Only when the
estimate exceeds the available width does the renderer emit `textLength="{availableWidth * scale}"`
together with `lengthAdjust="spacingAndGlyphs"`, which instructs SVG viewers to compress the glyph
spacing so the text shrinks into the available area without overflow.

When the text already fits, or the available width is non-positive, no `textLength` or `lengthAdjust`
attribute is emitted, so short labels render at their natural width and are never stretched to fill
the box.

### Key Methods

**`Render(LayoutTree layout, RenderOptions options, Stream output)`**

Entry point. Validates arguments, then — before rendering or sizing anything — collects every
`LayoutLine` and asks `ConnectorLabelPlacer.Place` for collision-aware label positions, including
each label's final nudged/fallback position and its estimated half-width/half-height. Only *after*
every label's position is known does `Render` compute the final canvas size: it starts from the
box+routing-geometry extent (clamped to a minimum of 1x1) and grows `width`/`height` further to
include the full bounding-box extent of every placed connector label, so a label nudged during
collision avoidance can never fall outside the rendered canvas and be invisibly clipped. It then
writes the SVG root element with `xmlns`, `width`, `height`, and `viewBox` attributes sized to that
final (label-aware) extent, calls `WriteEndMarkerDefs`, recursively renders every top-level node
(connector paths are drawn during this pass, but connector labels are deferred), renders each
non-null connector label in a final pass at the positions already computed, closes the SVG root,
encodes the completed `StringBuilder` as UTF-8, and writes all bytes to `output` in a single
`Write` call.

**`WriteEndMarkerDefs(StringBuilder sb, Theme theme)`**

Writes the SVG `<defs>` block containing the named line-end marker elements, including
`line-end-open-chevron`, `line-end-hollow-triangle`, `line-end-filled-arrow`,
`line-end-hollow-diamond`, `line-end-filled-diamond`, `line-end-circle`, `line-end-bar`, and
`line-end-hollow-triangle-crossbar`. All markers use `theme.StrokeColor` and `theme.StrokeWidth`.
The `<defs>` block also defines the `label-bg` filter used behind connector labels.

Every marker coordinate is derived from the single-source notation geometry in `NotationMetrics`, for
example `EndMarkerLength`, `EndMarkerWidth`, `EndMarkerRefX`, `DiamondLength`, `CircleRadius`, and
the `TriangleVertices()` / `DiamondVertices()` helpers that return `MarkerVertex` values. Because the
SVG and PNG renderers read from the same `NotationMetrics` source in
`DemaConsulting.Rendering.Abstractions`, their end markers are geometrically identical.
`line-end-open-chevron` is the only open marker: it omits the closing base edge so the chevron renders
as two strokes meeting at the apex; the hollow triangle and both diamonds remain closed.

**`RenderNode(StringBuilder sb, LayoutNode node, Theme theme, double scale)`**

Dispatches by concrete node type to the appropriate typed render method. All nine `LayoutNode`
subtypes are handled: `LayoutBox` → `RenderBox`, `LayoutLine` → `RenderLine`, `LayoutLabel` →
`RenderLabel`, `LayoutPort` → `RenderPort`, `LayoutBadge` → `RenderBadge`, `LayoutBand` →
`RenderBand`, `LayoutLifeline` → `RenderLifeline`, `LayoutActivation` → `RenderActivation`, and
`LayoutGrid` → `RenderGrid`. Unknown subtypes are silently skipped for forward compatibility.

**`RenderBox(StringBuilder sb, LayoutBox box, Theme theme, double scale)`**

Selects the fill color from `theme.DepthFillColors[box.Depth % count]` and delegates to
`RenderBoxOutline`, `RenderFolderOutline`, or `RenderNoteOutline` depending on `box.Shape`. The
rectangle outline adds `rx` / `ry` attributes when `BoxShape.RoundedRectangle` and `LineCornerRadius`
is greater than zero. Calls `RenderBoxTitle` to write the bold `<text>` title with `textLength` from
`FitTextLength` when `box.Label` is non-null, then `RenderBoxCompartments` for any compartments, then
recursively calls `RenderNode` for all `box.Children`.

**`RenderBoxTitle(StringBuilder sb, LayoutBox box, Theme theme, double scale)`**

Writes the keyword line (when `box.Keyword` is non-null) and the bold name `<text>` label, both
horizontally centered on the box's full geometric width (`centerX = box.X + box.Width / 2.0`),
regardless of any asymmetric `box.ContentInsetLeft`/`ContentInsetRight` the box declares. The
title occupies its own row above the title area (`ResolveTitleAreaTop`), while left/right port
labels are drawn at the box's vertical center (`RenderPort`) — a different row — so the title never
needs to dodge sideways around a side-port label's reserved inset; an earlier revision centered the
title on the inset-adjusted content area instead, which visibly shifted and squeezed titles on
boxes with an asymmetric left/right inset even though no title/port-label collision was actually
possible. `box.ContentInsetTop` still legitimately widens the title's vertical (Y) start position
when a top-side port shares the title's row, which is unaffected by this centering behavior.

**`RenderBoxCompartments(StringBuilder sb, LayoutBox box, Theme theme, double scale)`**

Writes a `<line>` divider across the full box width at the top of each compartment, followed by an
optional `font-weight="bold" font-style="italic"` `<text>` title row and zero or more left-aligned
regular-weight body-font `<text>` rows. Title and row text both start at
`box.X + Theme.LabelPadding + box.ContentInsetLeft` (instead of the fixed `box.X + Theme.LabelPadding`
offset used before ports existed) and available width is reduced by `box.ContentInsetRight` as well,
so a non-zero reserved port-label margin on either side pushes compartment content inward rather than
overlapping a rendered port label.

**`RenderLine(StringBuilder sb, LayoutLine line, Theme theme, double scale)`**

Calls `BuildLinePath` to produce the path `d` attribute, then writes a `<path>` element with
`fill="none"`. Adds `marker-start` or `marker-end` attributes for the non-None `EndMarkerStyle`
values, referencing the `SourceEnd` and `TargetEnd` line-end markers. Adds `stroke-dasharray` for
`Dashed` and `Dotted` line styles. `RenderLine` intentionally does not draw `MidpointLabel`; connector
labels are drawn in the final pass of `Render` so later wires cannot cover earlier labels.

**`BuildLinePath(...)`**

Builds the SVG path `d` string. When `cornerRadius` is zero, emits plain `M` / `L` commands. When
positive, each interior waypoint is replaced with a shortened `L` command to the arc start point,
followed by an `A` (elliptical arc) command whose sweep direction is determined from the cross product
of the incoming and outgoing unit direction vectors. The radius is clamped to half the shorter
adjacent segment to prevent overshoot. At the first and last bends the radius is additionally clamped
so the rounded corner completes at least the marker's along-line length
(`NotationMetrics.AlongLineLength` for the `SourceEnd` / `TargetEnd` styles) before the endpoint, so
the curve never intrudes into the end-marker zone.

**`RenderLineLabel(StringBuilder sb, LayoutLine line, Theme theme, double scale, double midX, double midY)`**

Writes a connector label's `<text>` element at the position supplied by `ConnectorLabelPlacer.Place`.
The text is centered with `text-anchor="middle"` and `dominant-baseline="middle"`, uses the theme body
font size and stroke color, applies `filter="url(#label-bg)"` so the label has an auto-sizing white
background, and escapes XML-special characters in `line.MidpointLabel`.

**`RenderLabel(StringBuilder sb, LayoutLabel label, Theme theme, double scale)`**

Writes a `<text>` element with `text-anchor` from `label.Align`, `font-size` from `label.FontSize`, and
`font-weight` / `font-style` from `label.Weight` and `label.Style`. When `label.MaxWidth > 0`, adds
`textLength` and `lengthAdjust="spacingAndGlyphs"`.

**`RenderPort` / `RenderBadge` / `RenderBand` / `RenderLifeline` / `RenderActivation` /
`RenderGrid`**

`RenderPort` writes the 8x8 port glyph `<rect>`, filled with `theme.StrokeColor` and outlined with a
1.0px (logical, pre-scale) `theme.BackgroundColor` stroke so the glyph remains visually distinct
from a solid-filled connector arrowhead marker that may land on/near the same box edge (both would
otherwise be plain solid-filled shapes with no border of their own, and would visually merge into
an indistinguishable blob). When `LayoutPort.Label` is non-null, a
companion `<text>` element positioned immediately next to the glyph and reading inward toward the
box interior: to the right of the glyph for a left-side port, to the left of the glyph for a
right-side port, below the glyph for a top-side port, and above the glyph for a bottom-side port.
Side classification reuses the same geometric anchor comparison the layout engine used to decide
where the port's `LayoutPort` node was placed. The label's rendered width is bounded to
`port.MaxLabelWidth` via `FitTextLength` (the same squeeze mechanism `RenderBoxTitle`/`RenderLabel`
already apply), adding a `textLength`/`lengthAdjust` attribute when the label would otherwise
exceed that bound; `MaxLabelWidth` defaults to positive infinity (no squeeze) so a port with no
computed bound renders exactly as before. This prevents an excessively long port label from
visually overlapping the opposite port's label region; no further text wrapping, truncation, or
ellipsis is applied.

Each remaining typed method writes the SVG elements appropriate to its node kind:
`<circle>`, `<polygon>`, or `<line>` for the badge shapes; a `<rect>` with a rotated or horizontal
`<text>` for bands; a header `<rect>`, bold `<text>`, and dashed `<line>` stem for lifelines; a white
`<rect>` border for activations; and per-cell `<rect>` plus `<text>` elements for grids. Fills come
from `theme.DepthFillColors`, and header cells add `font-weight="bold"`.

### Error Handling

`Render` throws `ArgumentNullException` when `layout`, `options`, or `output` is null. No other
exceptions are expected under normal operation. XML special characters in labels are escaped via
`EscapeXml` to prevent malformed SVG output.

### Dependencies

- `DemaConsulting.Rendering` — provides `LayoutTree` and all nine `LayoutNode` subtypes.
- `DemaConsulting.Rendering.Abstractions` — provides `IRenderer`, `RenderOptions`, `Theme`, `Themes`,
  `NotationMetrics`, `BoxMetrics`, `MarkerVertex`, and `ConnectorLabelPlacer`.

### Callers

Any consumer of the rendering library that selects vector output constructs an `SvgRenderer` and calls
`IRenderer.Render` with a placed `LayoutTree` and `RenderOptions`.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Svg-SvgRenderer-ImplementsIRenderer | `SvgRenderer : IRenderer` |
| Rendering-Svg-SvgRenderer-MediaType | `MediaType` property |
| Rendering-Svg-SvgRenderer-DefaultExtension | `DefaultExtension` property |
| Rendering-Svg-SvgRenderer-RenderDocument | `Render` writes the SVG root element |
| Rendering-Svg-SvgRenderer-RenderEmptyTree | `Render` clamps canvas size and emits an empty SVG root |
| Rendering-Svg-SvgRenderer-RenderBox | `RenderBox` writes the box rectangle outline |
| Rendering-Svg-SvgRenderer-RenderBoxRoundedCorners | `RenderBoxOutline` writes rounded-corner attributes |
| Rendering-Svg-SvgRenderer-RenderBoxCompartments | `RenderBoxCompartments` writes dividers and row text |
| Rendering-Svg-SvgRenderer-RenderLabel | `RenderLabel` writes a text element |
| Rendering-Svg-SvgRenderer-RenderLabelBold | `RenderLabel` maps `FontWeight.Bold` |
| Rendering-Svg-SvgRenderer-RenderLabelItalic | `RenderLabel` maps `FontStyle.Italic` |
| Rendering-Svg-SvgRenderer-RenderLabelEscaping | `EscapeXml` protects label text |
| Rendering-Svg-SvgRenderer-RenderLine | `RenderLine` writes the connector path |
| Rendering-Svg-SvgRenderer-RenderLineRoundedCorners | `BuildLinePath` emits arc commands |
| Rendering-Svg-SvgRenderer-RenderLineDashed | `RenderLine` writes `stroke-dasharray` |
| Rendering-Svg-SvgRenderer-RenderLineMidpointLabel | `ConnectorLabelPlacer` plus `RenderLineLabel` |
| Rendering-Svg-SvgRenderer-RenderNodeKinds | `RenderPort` writes the port rectangle |
| Rendering-Svg-SvgRenderer-RenderBadge | `RenderBadge` writes filled-circle badges |
| Rendering-Svg-SvgRenderer-BadgeBullseye | `RenderBadge` writes bullseye badges as concentric circles |
| Rendering-Svg-SvgRenderer-BadgeDiamond | `RenderBadge` writes diamond badges as polygons |
| Rendering-Svg-SvgRenderer-BadgeHorizontalBar | `RenderBadge` writes horizontal-bar badges as lines |
| Rendering-Svg-SvgRenderer-BadgeVerticalBar | `RenderBadge` writes vertical-bar badges as lines |
| Rendering-Svg-SvgRenderer-RenderBand | `RenderBand` writes the band rectangle |
| Rendering-Svg-SvgRenderer-RenderLifeline | `RenderLifeline` writes the header and stem |
| Rendering-Svg-SvgRenderer-RenderActivation | `RenderActivation` writes the activation rectangle |
| Rendering-Svg-SvgRenderer-RenderGrid | `RenderGrid` writes cell rectangles |
| Rendering-Svg-SvgRenderer-EndMarkers | `WriteEndMarkerDefs` defines the open chevron polyline |
| Rendering-Svg-SvgRenderer-EndMarkersOpenChevronReference | `RenderLine` references `line-end-open-chevron` |
| Rendering-Svg-SvgRenderer-HollowTriangleEndMarkers | `WriteEndMarkerDefs` defines the hollow triangle polygon |
| Rendering-Svg-SvgRenderer-HollowTriangleEndMarkerReference | `RenderLine` writes a hollow-triangle `marker-end` |
| Rendering-Svg-SvgRenderer-TriangleEndMarkerMetrics | `WriteEndMarkerDefs` reads triangle `NotationMetrics` |
| Rendering-Svg-SvgRenderer-DiamondEndMarkers | `WriteEndMarkerDefs` reads diamond `NotationMetrics` |
| Rendering-Svg-SvgRenderer-DiamondEndMarkerReference | `RenderLine` writes the hollow-diamond marker reference |
| Rendering-Svg-SvgRenderer-CrossbarEndMarkers | `RenderLine` writes the crossbar marker reference |
| Rendering-Svg-SvgRenderer-EndMarkerFilledArrow | `RenderLine` writes the filled-arrow marker reference |
| Rendering-Svg-SvgRenderer-EndMarkerFilledDiamond | `RenderLine` writes the filled-diamond marker reference |
| Rendering-Svg-SvgRenderer-EndMarkerCircle | `RenderLine` writes the circle marker reference |
| Rendering-Svg-SvgRenderer-EndMarkerBar | `RenderLine` writes the bar marker reference |
| Rendering-Svg-SvgRenderer-RenderPortLabel | `RenderPort` writes an inward-reading label `<text>` |
| Rendering-Svg-SvgRenderer-ContentInset | `RenderBoxCompartments` starts content at `box.ContentInsetLeft` |
| Rendering-Svg-SvgRenderer-CanvasGrowsForLabels | `Render` grows `width`/`height` to fit every placed label |
| Rendering-Svg-SvgRenderer-TitleCentersOnBoxWidth | `RenderBoxTitle` centers on full box width |
| Rendering-Svg-SvgRenderer-PortGlyphOutline | `RenderPort` outlines the port glyph in `theme.BackgroundColor` |
| Rendering-Svg-SvgRenderer-PortLabelSqueeze | `RenderPort` applies `FitTextLength` bounded by `MaxLabelWidth` |
