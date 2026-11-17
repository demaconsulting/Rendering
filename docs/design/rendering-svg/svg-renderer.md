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

Each node type uses a default font weight and style as SVG attributes. The box name, box keyword, and
compartment title defaults below are the values of the `BoxAppearance` / `LayoutCompartment` resolved
styles; an explicit style replaces them (see *Text Style Attributes*).

| Node Type | `font-weight` | `font-style` |
| --- | --- | --- |
| `LayoutBox` label | `bold` | (default) |
| `LayoutBox` keyword | (default) | `italic` |
| `LayoutBoxCompartment` title | `bold` | `italic` |
| `LayoutBoxCompartment` rows | (default) | (default) |
| `LayoutLine` connector label | (default) | (default) |
| `LayoutLabel` | Per `Weight` field | Per `Style` field |
| `LayoutPort` label | (default) | (default) |
| `LayoutBadge` label | (default) | (default) |
| `LayoutBand` label | (default) | (default) |
| `LayoutLifeline` label | `bold` | (default) |
| `LayoutLifeline` keyword | (default) | `italic` |
| `LayoutGrid` header cells | `bold` | (default) |
| `LayoutGrid` body cells | (default) | (default) |

### Text Style Attributes

A `TextStyle` (a `FontWeight` and a `FontStyle`) is written by `TextStyleAttributes`, which emits only the
non-default attribute values: `font-weight="bold"` for `FontWeight.Bold` and `font-style="italic"` for
`FontStyle.Italic`. A regular upright style therefore emits no attribute at all. The box name uses
`BoxAppearance.ResolvedNameStyle` (default bold), the box keyword uses
`BoxAppearance.ResolvedKeywordStyle` (default italic), and a compartment title uses
`LayoutCompartment.ResolvedTitleStyle` (default bold italic). Because those defaults equal the previously
hard-coded markup, a box with no `Appearance` and a compartment with no `TitleStyle` produce the same text
elements, byte for byte, as earlier versions. A lifeline header uses the same name and keyword styles from
`LayoutLifeline.HeaderAppearance`.

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
would otherwise overflow it. `FitTextLength` accepts a `useAccurateEstimator` flag that selects between
two width-estimation modes: the default (`false`) coarse flat multiplier — character count multiplied
by font size and an average glyph-width factor — used unchanged for box titles (`RenderBoxTitle`) and
generic/connector labels (`RenderLabel`); and an accurate mode (`true`), used only by `RenderPort`, that
measures via the shared `Rendering.Abstractions.PortLabelWidthEstimator.MeasureWidth` — the same
per-character estimator `Rendering.Layout`'s `LayeredLayoutAlgorithm` uses to size a port's
`MaxLabelWidth` in the first place, so the layout engine's sizing decision and the renderer's squeeze
decision can never disagree for the same port label. Either way, `FitTextLength` compares the estimate
to the available width: `box.Width - 2 * theme.LabelPadding` for box labels, `MaxWidth` for a
`LayoutLabel`, or `port.MaxLabelWidth` for a port label. Only when the estimate exceeds the available
width does the renderer emit `textLength="{availableWidth * scale}"` together with
`lengthAdjust="spacingAndGlyphs"`, which instructs SVG viewers to compress the glyph spacing so the
text shrinks into the available area without overflow.

When the text already fits, or the available width is non-positive, no `textLength` or `lengthAdjust`
attribute is emitted, so short labels render at their natural width and are never stretched to fill
the box.

### Key Methods

**`Render(LayoutTree layout, RenderOptions options, Stream output)`**

Entry point. Validates arguments, then — before rendering or sizing anything — collects every
`LayoutLine` and every `LayoutBox` (including boxes nested in boxes or bands) and asks
`ConnectorLabelPlacer.Place(lines, fontSize, boxes)` for collision-aware label positions, including
each label's final nudged/fallback position and its estimated half-width/half-height. Only *after*
every label's position is known does `Render` compute the final canvas size: it starts from the
box+routing-geometry extent (clamped to a minimum of 1x1) and grows `width`/`height` further to
include the full bounding-box extent of every placed connector label, so a label nudged during
collision avoidance can never fall outside the rendered canvas and be invisibly clipped. It then
writes the SVG root element with `xmlns`, `width`, `height`, and `viewBox` attributes sized to that
final (label-aware) extent, calls `WriteEndMarkerDefs` with the on-demand markers the lines use
(`CollectLazyMarkers`), recursively renders every top-level node
(connector paths are drawn during this pass, but connector labels are deferred), renders each
non-null connector label in a final pass at the positions already computed, closes the SVG root,
encodes the completed `StringBuilder` as UTF-8, and writes all bytes to `output` in a single
`Write` call.

**`WriteEndMarkerDefs(StringBuilder sb, Theme theme, IReadOnlyList<(EndMarkerStyle, bool)> lazyMarkers)`**

Writes the SVG `<defs>` block containing the named line-end marker elements. The eight historical
end markers — `line-end-open-chevron`, `line-end-hollow-triangle`, `line-end-filled-arrow`,
`line-end-hollow-triangle-crossbar`, `line-end-hollow-diamond`, `line-end-filled-diamond`,
`line-end-circle`, and `line-end-bar` — are always written, in that fixed order, so a document that
uses none of the newer markers carries exactly these definitions (the crossbar definition has the
geometry of the current redefinition notation, see below). After them come, in a stable order
(end variants before start variants, then by style), only the on-demand definitions that a line in the
tree uses: the three newer end markers `line-end-hollow-triangle-two-dots`,
`line-end-hollow-triangle-four-dots`, and `line-end-circled-plus`, and the mirrored `line-start-…`
variant of every style used at a line's source end. Each on-demand definition is written exactly once,
however many lines use it. All markers use `theme.StrokeColor` and `theme.StrokeWidth`. The `<defs>`
block also defines the `label-bg` filter used behind connector labels.

Every marker is written by `WriteMarker` from a description produced by `DescribeMarker`: the marker box
(length and height), the tip anchor (`refX`, `refY`), and the SVG elements of the glyph. The glyph
coordinates are derived from the single-source notation geometry in `NotationMetrics`, for example
`EndMarkerLength`, `EndMarkerWidth`, `EndMarkerRefX`, `DiamondLength`, `CircleRadius`, and the
`TriangleVertices()` / `DiamondVertices()` helpers that return `MarkerVertex` values; the marker box length
and tip anchor come from `NotationMetrics.MarkerAnchor(style, theme)`, which retracts the anchor by
`TipRetraction` so the STROKED glyph tip, not its geometric vertex, lands exactly on the line endpoint (the
box is widened for the retraction, so the tip is never clipped). Polygons and polylines carry
`stroke-linejoin="bevel"` (as in the raster renderer), so the painted tip is the bevel corner rather than a
miter spike. Because the
SVG and PNG renderers read from the same `NotationMetrics` source in
`DemaConsulting.Rendering.Abstractions`, their end markers are geometrically identical.
`line-end-open-chevron` is the only open marker: it omits the closing base edge so the chevron renders
as two strokes meeting at the apex; the hollow triangle and both diamonds remain closed.

The newer glyphs are:

- **Dotted triangles** — `line-end-hollow-triangle-two-dots` and `line-end-hollow-triangle-four-dots`
  draw the hollow triangle (its box and tip anchor from `NotationMetrics.TriangleFamilyBox`, so the
  triangle lands exactly where a plain hollow triangle does) followed by filled dots of radius
  `DotRadius` at the centers returned by `NotationMetrics.DotCenters`: two dots on the line axis, or a
  two-by-two grid of four dots. Each dot is a filled `<circle>` with no stroke.
- **Redefinition crossbar** — `line-end-hollow-triangle-crossbar` draws the hollow triangle (its box and tip
  anchor from `NotationMetrics.TriangleFamilyBox`: 14.5 long, anchor 14.5, so the triangle lands exactly
  where a plain hollow triangle does) followed by a `<line>` bar at box x = `CrossbarExtension` -
  `CrossbarBaseGap` (1.0), spanning the full box height (the base height). The bar is therefore on the
  line side of the triangle base with a clear gap, as SysML v2.0 section 8.2.3.6 draws redefinition; the
  earlier markup put the bar at 0.7 of the marker length, against the apex.
- **Circled plus** — `line-end-circled-plus` draws a `<circle>` of radius `CircledPlusRadius` filled with
  the theme background and stroked, crossed by two `<line>` arms of half-length
  `CircledPlusArmHalfLength`, in a `CircledPlusMarkerBox` square with its anchor at `CircledPlusRefX`.

#### Mirrored start variants (marker-start orientation fix)

An SVG `marker-start` marker with `orient="auto"` has its x axis pointing *into* the line (along the
path direction), whereas a `marker-end` marker's x axis points out of the line end along the path
direction. Earlier versions referenced the same `line-end-…` definition from `marker-start`, so the glyph
of every source-end marker was drawn pointing inward and sitting outside the line start. This was a
pre-existing defect, and the start variants fix it deliberately: every `marker-start` reference now uses a
`line-start-<name>` definition, written on demand, that wraps the glyph in
`<g transform="matrix(-1 0 0 1 L 0)">` (L is the marker length) and mirrors the anchor (`refX` becomes
`L - refX`). The mirrored glyph points out of the line start exactly as the end variant points out of the
line end, so the source-end marker sits on the line side of the start point. Target-end references
(`marker-end`) are unchanged and keep using the always-defined `line-end-…` markers (or the on-demand
newer ones).

#### Marker scale: one size in SVG and PNG

SVG markers use the default `markerUnits="strokeWidth"`, so a marker's box is scaled by the line's stroke
width and its rendered along-line size is the marker box length times `theme.StrokeWidth`
(`NotationMetrics.RenderedAlongLineLength`); the marker's own outline (written as `theme.StrokeWidth` in
marker units) is scaled the same way. This scale, `NotationMetrics.MarkerScale(Theme)`, is the one size at
which both renderers draw every marker: the PNG renderer applies the same scale and the same outline width
(`NotationMetrics.MarkerStrokeWidth(Theme)`), so a PNG marker has the size and geometry of the SVG marker
(the SVG renderer is the reference; a parity test compares the pixel footprint of every PNG marker with the
SVG marker geometry converted to pixels, and a second one that the painted tip of every style lands on the
endpoint in both). Both renderers also use `RenderedAlongLineLength` to clamp the
final corner radius, so a rounded bend never begins inside a marker, and pass `MarkerScale` to
`ConnectorLabelPlacer` so a label keeps clear of the marker as it is drawn. An SVG marker is clipped to its
box; the box is widened so the clip is inert at the tip, and clips only the outer half of the outline at the
base.

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
is greater than zero. Every outline element (including both paths of a note and the folder path)
appends the `stroke-dasharray` fragment from `OutlineDash` (see *Dashed and Dotted Outlines*). Then
calls `RenderBoxCornerIcon` to write the optional send/accept icon polygon (see *Box Corner Icons*),
`RenderBoxTitle` to write the keyword and name `<text>` elements with `textLength` from
`FitTextLength` when `box.Label` is non-null, then `RenderBoxCompartments` for any compartments, then
recursively calls `RenderNode` for all `box.Children`.

#### Dashed and Dotted Outlines

`DashAttribute` maps a `LineStyle` to a `stroke-dasharray` attribute fragment: `6 3` for `Dashed`, `2 2`
for `Dotted`, and nothing for `Solid`. A box takes its style from `box.Appearance.OutlineStyle`; a box
without an appearance is solid. A band takes it from `band.OutlineStyle` and a lifeline header from
`lifeline.HeaderAppearance.OutlineStyle`. Because no attribute is written for solid outlines, existing
documents are unchanged. The values equal the connector line dash patterns, and the PNG renderer scales
the same pattern lengths.

#### Box Corner Icons

When `box.Appearance.CornerIcon` is `Send` or `Accept`, `RenderBoxCornerIcon` writes one `<polygon>` filled
with `theme.BackgroundColor` and stroked with `theme.StrokeColor` whose vertices come from
`NotationMetrics.CornerIconVertices`: a
pentagon with a pointed right end for `Send`, and the same pentagon with a triangular notch cut into its
left edge for `Accept`. The icon origin is inset by `NotationMetrics.CornerIconInset` from the left edge
of the box and from the top of the title area (`ResolveTitleAreaTop`), which for a folder is the top of the
body, below the tab. `BoxCornerIcon.None` writes nothing.

**`RenderBoxTitle(StringBuilder sb, LayoutBox box, Theme theme, double scale)`**

Writes the keyword line (when `box.Keyword` is non-null) and the name `<text>` label, both
horizontally centered on the box's full geometric width (`centerX = box.X + box.Width / 2.0`),
regardless of any asymmetric `box.ContentInsetLeft`/`ContentInsetRight` the box declares. The
keyword and name take their `font-weight`/`font-style` from the box appearance's resolved keyword and
name styles (see *Text Style Attributes*), so a box without an appearance keeps the italic keyword and
bold name.
The title occupies its own row above the title area (`ResolveTitleAreaTop`), while left/right port
labels are drawn at the box's vertical center (`RenderPort`) — a different row — so the title never
needs to dodge sideways around a side-port label's reserved inset; an earlier revision centered the
title on the inset-adjusted content area instead, which visibly shifted and squeezed titles on
boxes with an asymmetric left/right inset even though no title/port-label collision was actually
possible. `box.ContentInsetTop` still legitimately widens the title's vertical (Y) start position
when a top-side port shares the title's row, which is unaffected by this centering behavior.

When `BoxMetrics.IsNameInTab(box)` is true (a `Folder` box with a label whose appearance sets
`FolderTitle = Tab`), the name is instead drawn inside the folder tab: left-aligned
(`text-anchor="start"`) at `box.X + Theme.LabelPadding`, vertically centered on the tab
(`box.Y + tabHeight / 2`), and squeezed with the accurate `FitTextLength` estimator to the tab width
less `2 * Theme.LabelPadding`. The tab size comes from `BoxMetrics.ResolveFolderTabWidth` and
`ResolveFolderTabHeight`, which size the tab to the measured name when no explicit
`FolderTabWidth`/`FolderTabHeight` is supplied. No name is drawn in the body; a keyword line, if any, still
is. For any other shape the `Tab` setting is ignored and the name stays in the body. The body title area
height comes from `BoxMetrics.TitleAreaHeight(box, theme)`, which excludes the name row in this case, so
`RenderBoxCompartments` starts the first compartment directly under the tab (or the keyword line).

**`RenderBoxCompartments(StringBuilder sb, LayoutBox box, Theme theme, double scale)`**

Writes a `<line>` divider across the full box width at the top of each compartment, followed by an
optional `<text>` title row and zero or more left-aligned
regular-weight body-font `<text>` rows. The first compartment starts below the title area computed by
`BoxMetrics.TitleAreaHeight(box, theme)`. A title is bold italic and left-aligned by default
(`font-weight="bold" font-style="italic"`, `text-anchor="start"`), with its style taken from
`LayoutCompartment.ResolvedTitleStyle`. When `compartment.TitleAlign` is `TextAlign.Center`, the title is
instead written with `text-anchor="middle"` at the horizontal center of the content area,
`box.X + box.ContentInsetLeft + (box.Width - box.ContentInsetLeft - box.ContentInsetRight) / 2`, so it stays
clear of reserved port-label margins; the divider line still spans the full box width. Row text starts at
`box.X + Theme.LabelPadding + box.ContentInsetLeft` (instead of the fixed `box.X + Theme.LabelPadding`
offset used before ports existed) and available width is reduced by `box.ContentInsetRight` as well,
so a non-zero reserved port-label margin on either side pushes compartment content inward rather than
overlapping a rendered port label.

**`RenderLine(StringBuilder sb, LayoutLine line, Theme theme, double scale)`**

Calls `BuildLinePath` to produce the path `d` attribute, then writes a `<path>` element with
`fill="none"`. Adds `marker-start` or `marker-end` attributes for the non-None `EndMarkerStyle`
values through `MarkerAttribute`: the `SourceEnd` style references the mirrored
`line-start-<name>` marker via `marker-start`, and the `TargetEnd` style references the `line-end-<name>`
marker via `marker-end` (see *Mirrored start variants*). Adds `stroke-dasharray` for
`Dashed` and `Dotted` line styles. `RenderLine` intentionally does not draw `MidpointLabel`; connector
labels are drawn in the final pass of `Render` so later wires cannot cover earlier labels.

**`ShortenPortEnds(IReadOnlyList<LayoutNode> nodes)`**

Before the nodes are rendered, a line end that carries an end marker and coincides (within 0.5 px) with a
`LayoutPort` centre is moved `NotationMetrics.PortHalfSize` along its end segment, away from the port
(`RewriteForPorts` / `ShortenLineForPorts` / `RetreatFromPort`, recursing through boxes and bands). The
port square is painted after the connector and would otherwise hide the last half port size of the
marker; shortening the drawn end puts the painted tip on the port square's outer edge. The end segment
must be longer than the port half size; the layout tree, the connector label placement and an end
without a marker are left untouched. The CanvasNet renderer applies the identical rule.

**`BuildLinePath(...)`**

Builds the SVG path `d` string. When `cornerRadius` is zero, emits plain `M` / `L` commands. When
positive, each interior waypoint is first checked for collinearity: if the incoming (previous→current)
and outgoing (current→next) direction vectors are parallel and same-sense (a normalized cross product
within `DirectionTolerance` of zero and a positive dot product — not a reversal), the waypoint sits on
a straight run rather than a genuine corner, so it is emitted as a plain `L` command and no arc is
drawn there; rounding a non-corner waypoint would otherwise draw a spurious bump in an already-straight
run. Any waypoint that fails this collinearity check is replaced with a shortened `L` command to the
arc start point, followed by an `A` (elliptical arc) command whose sweep direction is determined from
the cross product of the incoming and outgoing unit direction vectors. The radius is clamped to half
the shorter adjacent segment to prevent overshoot. At the first and last bends the radius is
additionally clamped so the rounded corner completes at least the marker's along-line length
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
`port.MaxLabelWidth` via `FitTextLength(useAccurateEstimator: true)` — the accurate
`PortLabelWidthEstimator`-based mode described above, reconciled with the layout engine's own
`MaxLabelWidth` sizing so a label the layout engine already sized to fit exactly never receives a
`textLength` attribute — adding a `textLength`/`lengthAdjust` attribute only when the label's
accurately-measured width would still exceed that bound; `MaxLabelWidth` defaults to positive infinity
(no squeeze) so a port with no computed bound renders exactly as before. This prevents an excessively
long port label from
visually overlapping the opposite port's label region; no further text wrapping, truncation, or
ellipsis is applied.

Each remaining typed method writes the SVG elements appropriate to its node kind:
`<circle>`, `<polygon>`, `<rect>`, or `<line>` for the badge shapes; a `<rect>` with a rotated or horizontal
`<text>` for bands; a header `<rect>`, bold `<text>`, and dashed `<line>` stem for lifelines; a white
`<rect>` border for activations; and per-cell `<rect>` plus `<text>` elements for grids. Fills come
from `theme.DepthFillColors`, and header cells add `font-weight="bold"`.

#### Badge Shapes

In addition to the filled circle, bullseye, diamond, and thin bar badges, `RenderBadge` handles three
notation shapes:

- **`ThickHorizontalBar` / `ThickVerticalBar`** — a `<rect>` filled with `theme.StrokeColor`. `Size` is the
  full length along the bar's axis and `Thickness` is the extent across it, defaulting to
  `NotationMetrics.ThickBarThickness` (4) when null. The rectangle is centered on the badge center. For a
  thick vertical bar the optional label is placed to the right of half the thickness rather than half the
  size.
- **`CrossedCircle`** — a `<circle>` of radius `Size / 2` filled with `theme.BackgroundColor` and stroked,
  plus two diagonal `<line>` arms from the center offset `±r × NotationMetrics.CrossedCircleArmFraction`
  on both axes, so each arm ends on the circle.

#### Bands and Lifelines

A band's `<rect>` takes its `stroke-dasharray` from `band.OutlineStyle` (dashed swim lane). A lifeline
header `<rect>` takes its outline dash from `lifeline.HeaderAppearance.OutlineStyle` and adds `rx` / `ry`
when `lifeline.HeaderShape` is `BoxShape.RoundedRectangle`; the radius is `HeaderCornerRadius` (clamped to
zero) when supplied, otherwise `NotationMetrics.RoundedRectRadius(theme)`. When `lifeline.Keyword` is
non-null, a keyword `<text>` (keyword style from the header appearance, default italic, wrapped in
guillemet marks) is written above the label: the two-line block height comes from
`BoxMetrics.LifelineHeaderHeight(theme, hasKeyword: true)` and the block is centered vertically in the
header, with the label on the second line. The label uses the header appearance name style (default
bold). The dashed stem is unchanged. A lifeline with no keyword, header shape, or appearance produces the
same markup as before.

#### Obstacle-Aware Connector Label Placement

`Render` passes every `LayoutBox` to `ConnectorLabelPlacer.Place` as an obstacle. A label painted at a
line midpoint that lies on a leaf box would cover the box with its opaque background, so the placer
rejects candidate positions that overlap a leaf box (or straddle the border of a container box), trying
segment midpoints, slides along segments, and perpendicular nudges in turn, and falls back to the
obstacle-free placement when nothing clears. The SVG and PNG renderers use the same overload, so labels
land at the same positions in both formats.

#### Diamond Box Outline

For `BoxShape.Diamond`, `RenderBoxOutline` emits one `<polygon>` through `NotationMetrics.DiamondBoxVertices`
with the same depth fill, theme stroke, stroke width, and `OutlineDash` handling as every other box outline.
The title and compartments use the plain-rectangle metrics (a diamond has no tab or fold), so a label on a
centered-title diamond is centered on the box. Rectangle boxes keep their `<rect>` markup unchanged.

#### Connection End Labels

After the midpoint labels are placed, `Render` calls `ConnectorLabelPlacer.PlaceEndLabels` with the midpoint
placements as occupied rectangles, grows the canvas to include every end-label box, and in the final label pass
draws each placement as a centered `<text>` with the midpoint label's font, fill, and middle baseline but **no**
`label-bg` filter, because an end label sits beside its own line and a background could only erase a neighboring
connector (the text is XML-escaped). A line without end labels adds nothing, so existing output is unchanged.

#### Port Glyphs

`RenderPort` draws the legacy filled 8 × 8 `<rect>` (byte-identical markup) when the port has no appearance or
resolves to `PortGlyph.Filled`. An outlined glyph is a `<rect>` of `2 × PortGlyphHalfSize` with `rx`/`ry` of
`PortGlyphCornerRadius`, filled with the theme background, stroked with the theme stroke and (for a dashed
outline) the box dash array, followed by one `<polyline>` per `NotationMetrics.PortArrowSegments` segment. A
proxy port is a `<circle>` of `PortProxyRadius` filled with the stroke color. An inward label is offset by the
glyph's own half size; an outward label is a `<text>` at `NotationMetrics.PortOutwardLabelAnchor` with
`text-anchor` start or end and no width squeeze. The marker end shortening (`RetreatFromPort`) retreats by the
glyph's own half size, and the canvas grows to include every outward label; the port glyph and label rectangles
are also passed to the end-label placer as occupied rectangles (the outward-label rectangle padded by 5 px on
each side so an end label never sits flush against a port label).

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
| Rendering-Svg-SvgRenderer-TriangleEndMarkerMetrics | `WriteEndMarkerDefs` reads `NotationMetrics` and `MarkerAnchor` |
| Rendering-Svg-SvgRenderer-EndMarkerTipOnEndpoint | `DescribeMarker` anchor, `DescribeGlyph` bevel |
| Rendering-Svg-SvgRenderer-EndMarkerPortEndpoint | `ShortenPortEnds` shortens marker ends on a port |
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
| Rendering-Svg-SvgRenderer-PortLabelAccurateFit | `RenderPort` calls `FitTextLength(useAccurateEstimator: true)` |
| Rendering-Svg-SvgRenderer-EndMarkerDottedTriangles | `DescribeMarker` writes the triangle and its dots |
| Rendering-Svg-SvgRenderer-EndMarkerCircledPlus | `DescribeMarker` writes the circled-plus circle and arms |
| Rendering-Svg-SvgRenderer-EndMarkerDefsOnlyWhenUsed | `CollectLazyMarkers` limits new defs to those used |
| Rendering-Svg-SvgRenderer-EndMarkerStartVariants | `MarkerAttribute` and `WriteMarker` mirror start markers |
| Rendering-Svg-SvgRenderer-BoxOutlineStyle | `OutlineDash` / `DashAttribute` on every outline path |
| Rendering-Svg-SvgRenderer-BoxNameStyle | `RenderBoxTitle` with `TextStyleAttributes(ResolvedNameStyle)` |
| Rendering-Svg-SvgRenderer-BoxKeywordStyle | `RenderBoxTitle` with `TextStyleAttributes(ResolvedKeywordStyle)` |
| Rendering-Svg-SvgRenderer-CompartmentTitleStyle | `RenderBoxCompartments` title alignment and `ResolvedTitleStyle` |
| Rendering-Svg-SvgRenderer-FolderTitleInTab | `RenderBoxTitle` and `BoxMetrics.IsNameInTab` / `TitleAreaHeight` |
| Rendering-Svg-SvgRenderer-BoxCornerIcon | `RenderBoxCornerIcon` writes the `CornerIconVertices` polygon |
| Rendering-Svg-SvgRenderer-BadgeThickBar | `RenderBadge` writes thick bars as filled rects |
| Rendering-Svg-SvgRenderer-BadgeCrossedCircle | `RenderBadge` writes the crossed circle |
| Rendering-Svg-SvgRenderer-BandDashed | `RenderBand` appends `DashAttribute(band.OutlineStyle)` |
| Rendering-Svg-SvgRenderer-LifelineRoundedHeader | `RenderLifeline` rounded header, keyword line, appearance |
| Rendering-Svg-SvgRenderer-LabelsAvoidBoxes | `Render` passes `CollectBoxes` to `ConnectorLabelPlacer.Place` |
