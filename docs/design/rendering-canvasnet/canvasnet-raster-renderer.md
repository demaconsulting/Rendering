## CanvasNetRasterRenderer Unit Design

Part of the Rendering.CanvasNet system.

### CanvasNetRasterRenderer Purpose

`CanvasNetRasterRenderer` has a single responsibility: rasterize a placed `LayoutTree` onto a
managed CanvasNet `Surface` and encode that surface to a caller-supplied `Stream` in whatever image
format the derived renderer selects. It centralizes every drawing decision (background fill, node
drawing, connector end markers, label backplates, and typography) so that PNG and JPEG output
share identical pixel-level behavior and differ only in the final encode step.

### CanvasNetRasterRenderer Overview

`CanvasNetRasterRenderer` is the abstract `IRenderer` implementation that provides the common
CanvasNet rasterization path for every raster format. Concrete renderer cores supply only the media
metadata and the `Save(Surface, Stream)` implementation; the base class owns argument validation,
line-label placement, surface allocation, background initialization, node drawing, midpoint-label
finalization, and stream encoding orchestration. The internal `CanvasNetTypefaces` helper resolves
shared, lazily loaded embedded Noto Sans `TrueTypeFont` instances that every drawing call site in
this unit measures and draws against.

### CanvasNetRasterRenderer Data Model

| Member | Type | Description |
| --- | --- | --- |
| `MediaType` | `abstract string` | The MIME media type reported to callers and registries. |
| `DefaultExtension` | `abstract string` | The primary output file extension. |
| `FileExtensions` | `abstract IReadOnlyList<string>` | Every file extension the concrete renderer produces. |
| `PortGlyphStrokeWidth` | `const float` | Logical-pixel outline width drawn around a port glyph square. |
| `White` | `static readonly Rgba32` | Theme-independent fill used by activation bars and bullseye centers. |

The unit is otherwise stateless: render-time state lives in method-local variables and the returned
`Surface`.

### CanvasNetRasterRenderer Methods

- **`RenderToSurface(LayoutTree, RenderOptions)`** — validates its arguments, recursively collects
  all `LayoutLine` and `LayoutBox` nodes, resolves every connector label's placement via
  `ConnectorLabelPlacer.Place(lines, fontSize, boxes)` (the obstacle-aware overload, with every box as an
  obstacle) *before* allocating the raster surface, grows the required width and
  height to include the full bounding-box extent of every placed connector label, allocates a
  managed `Surface`, clears it to `RenderOptions.Theme.BackgroundColor`, draws every node through a
  CanvasNet `Canvas`, and finally draws connector labels in a dedicated pass at their finalized
  positions. Sizing the surface only after label placement is known prevents a nudged label from
  landing outside the bitmap and being invisibly clipped.
- **`Render(LayoutTree, RenderOptions, Stream)`** — validates the output stream, calls
  `RenderToSurface`, hands the resulting surface to the derived `Save` implementation, and disposes
  the surface without closing or flushing the caller-owned stream.
- **Box helpers** — draw rectangle, rounded-rectangle, folder, and note outlines; select fill
  colors from `Theme.DepthFillColors`; render keyword/title rows centered on the box's full
  geometric width; and render compartments starting at `box.X + Theme.LabelPadding +
  box.ContentInsetLeft` so reserved port-label margins shift content exactly as the SVG renderer
  does. The notation extensions are described under *Notation Primitives* below.
- **Line helpers** — render straight or clamped polyline paths with `PathBuilder`, optionally round
  corners with `CornerRoundEffect`, apply dashed/dotted `StrokeStyle` patterns, and draw
  notation-metric-derived end markers plus midpoint-label background plates in the theme background
  color.
- **Text helpers** — `CreateFont`, `MeasureText`, and `FitFontSize` resolve the shared embedded
  `TrueTypeFont` instances and proportionally shrink labels when a finite width constraint is
  present.
- **Node helpers** — render labels, ports, badges, bands, lifelines, activations, and grids using
  the shared theme and notation metrics. Port glyphs are filled with `Theme.StrokeColor`, outlined
  in `Theme.BackgroundColor`, and have inward/outward labels whose width is bounded by
  `LayoutPort.MaxLabelWidth` through the same proportional squeeze mechanism used for box titles.
- **Marker helpers** — `DrawEndMarker`, `TrianglePath`, `DiamondPath`, and `MarkerPoint` derive all
  marker geometry from `NotationMetrics`, keeping raster markers aligned with the SVG renderer.

### Notation Primitives

The SysML notation primitives are drawn with the same shared geometry the SVG renderer uses, so the two
formats agree. Each rule below is the raster counterpart of the SVG rule of the same name.

- **Dotted-triangle end markers** — `HollowTriangleTwoDots` and `HollowTriangleFourDots` draw the hollow
  triangle exactly as `HollowTriangle` does (filled with the theme background and stroked), then a filled
  circle of radius `NotationMetrics.DotRadius × scale` at every center returned by
  `NotationMetrics.DotCenters(style)`, mapped into canvas space with `MarkerPoint`: two dots on the line
  axis, or a two-by-two grid of four dots, trailing the triangle base along the line.
- **Circled-plus end marker** — `CircledPlus` draws a circle of radius `NotationMetrics.CircledPlusRadius`
  whose near edge sits on the line endpoint (center one radius back along the line from the endpoint),
  filled with the theme background and stroked, then two arms of half-length `CircledPlusArmHalfLength`
  along and across the line through the center.
- **Source-end markers** — `RenderLine` draws a source-end marker with the same `DrawEndMarker` helper as
  a target-end marker, using the outward direction from the second waypoint to the first, and
  `MarkerPoint` places the glyph relative to the endpoint along that direction. At a source end the glyph
  body therefore extends back along the line and lies on the line side of the start point. No separate
  mirrored variant is needed; the SVG renderer reaches the same placement with its mirrored
  `line-start-…` marker definitions.
- **Marker scale** — the PNG renderer draws every marker at the SVG marker's size: the marker geometry is
  scaled by `NotationMetrics.MarkerScale(theme)` (the theme stroke width, which SVG applies through the
  default `markerUnits="strokeWidth"`) times the render scale, and outlines are drawn
  `NotationMetrics.MarkerStrokeWidth(theme)` wide, as SVG strokes the marker content in marker units. The
  filled arrow and filled diamond are also outlined in their own color, as in SVG. Polygonal markers use a
  bevel join, as the SVG markers do, so the painted tip is the bevel corner and not a miter spike that would
  poke through the target box border. The final-corner clamp
  (`NeedsEndCornerClamp`, `BuildClampedLinePath`) reserves `NotationMetrics.RenderedAlongLineLength`, and
  the connector-label placer receives `MarkerScale(theme)`, so layout, labels and both formats agree. A
  parity test asserts, for every style at both ends, that the pixel footprint of the PNG marker equals the
  SVG marker geometry converted to pixels.
- **Tip on the endpoint** — `RenderLine` moves each glyph back along the line by
  `NotationMetrics.TipRetraction(style, theme)` (times the marker and render scales) before calling
  `DrawEndMarker`, so the stroked tip (the bevel corner, or the full half stroke width of a circle or bar) lands
  exactly on the endpoint, the box boundary, and no marker ink enters the target box interior. The SVG
  renderer applies the same retraction through `NotationMetrics.MarkerAnchor`; a cross-renderer parity test
  asserts the painted tip of every style, at both ends and in two stroke widths, is on the endpoint in both.
- **Port ends** — before rendering, `ShortenPortEnds` moves a marker end that coincides with a `LayoutPort`
  centre by `NotationMetrics.PortHalfSize` along its end segment, so the arrowhead tip lands on the port
  square's outer edge instead of being hidden under the glyph painted after the connector. It leaves the
  layout tree and label placement untouched, and mirrors the SVG renderer's rule exactly.
- **Redefinition crossbar** — `HollowTriangleCrossbar` draws the hollow triangle, then a bar at
  `NotationMetrics.CrossbarAlong` back from the endpoint spanning `±CrossbarHalfLength` across the line:
  behind the triangle base with a clear gap (SysML v2.0 section 8.2.3.6), like the SVG marker.
- **Dashed and dotted outlines** — `DashArrayFor` maps a `LineStyle` to a dash array scaled by the render
  scale: `6 3` for dashed and `2 2` for dotted; solid has no dash array. A box takes its style from
  `box.Appearance.OutlineStyle`, a band from `band.OutlineStyle`, and a lifeline header from
  `lifeline.HeaderAppearance.OutlineStyle`. The dedicated rectangle strokers (`StrokeRect`,
  `StrokeRoundRect`) do not dash, so a dashed outline is stroked as a path built by `BuildRoundRectPath`
  (straight edges plus `TangentArcTo` corners, starting at the top edge so the pattern begins with ink
  there). The folder and note outlines are already paths and take the dash pattern directly. A solid
  outline keeps using the rectangle strokers.
- **Text styles** — the box name, box keyword, compartment title, and lifeline name and keyword select
  their embedded typeface from the resolved style (`ResolvedNameStyle` default bold,
  `ResolvedKeywordStyle` default italic, `ResolvedTitleStyle` default bold italic) through
  `CreateFont(bold, italic)`, so defaults reproduce the previous fonts.
- **Centered compartment title** — a compartment whose `TitleAlign` is `Center` draws its title with
  centered text alignment at `box.X + box.ContentInsetLeft + (box.Width - box.ContentInsetLeft -
  box.ContentInsetRight) / 2`; otherwise the title is left-aligned at
  `box.X + Theme.LabelPadding + box.ContentInsetLeft`.
- **Folder name in tab** — when `BoxMetrics.IsNameInTab(box)` is true, the name is drawn left-aligned
  inside the tab at `box.X + Theme.LabelPadding`, vertically centered on the tab, with its font size
  shrunk by `FitFontSize` to fit the tab width less twice the label padding. The tab size comes from
  `BoxMetrics.ResolveFolderTabWidth` / `ResolveFolderTabHeight`, and the body title area height from
  `BoxMetrics.TitleAreaHeight(box, theme)`, so compartments start directly under the tab and no name is
  drawn in the body.
- **Corner icon** — `RenderBoxCornerIcon` builds a closed path from `NotationMetrics.CornerIconVertices`
  for `Send` (pentagon) or `Accept` (pentagon with a notched left edge), inset by
  `NotationMetrics.CornerIconInset` from the box's left edge and from the top of the title area (below the
  tab for a folder), fills it with the theme background, and strokes it.
- **Badges** — `ThickHorizontalBar` and `ThickVerticalBar` fill a rectangle of `Size` by `Thickness`
  (default `NotationMetrics.ThickBarThickness`) centered on the badge center with the stroke color;
  `CrossedCircle` fills a circle with the theme background, strokes it, and strokes two diagonal arms
  whose ends lie on the circle (`radius × NotationMetrics.CrossedCircleArmFraction` on each axis). A label
  of a thick vertical bar is offset by half the thickness instead of half the size.
- **Lifeline headers** — a header whose `HeaderShape` is `RoundedRectangle` is filled with `FillRoundRect`
  using `HeaderCornerRadius` (or `NotationMetrics.RoundedRectRadius(theme)`) and stroked rounded; a
  dashed header appearance strokes the path instead. A non-null `Keyword` adds a centered keyword text row
  above the label (guillemet-wrapped, in the keyword style); the two-line block height comes from
  `BoxMetrics.LifelineHeaderHeight(theme, hasKeyword: true)` and is centered in the header. A lifeline
  without these properties draws as before.
- **Obstacle-aware labels** — connector labels are placed so a label at a line midpoint on a leaf box
  moves clear of that box instead of painting its opaque backplate over the box border.

#### Diamond Box Outline

For `BoxShape.Diamond`, `RenderBoxOutline` fills and strokes a closed path built from
`NotationMetrics.DiamondBoxVertices` (starting at the top vertex so a dash pattern begins there), using the
box's depth fill, the theme stroke, and the box appearance's dash pattern. It is the same vertex helper the
SVG renderer uses, so the PNG and SVG diamonds are the same shape; a parity test in the gallery project reads
the SVG polygon back against PNG pixels.

#### Connection End Labels

`RenderToSurface` places end labels with `ConnectorLabelPlacer.PlaceEndLabels` after the midpoint labels (which
they yield to), passing the port glyph and label rectangles as occupied rectangles and every port as `ports` so a
marker retracted onto a port glyph is measured from where it is painted (the same `PortEndRetraction` rule as the
line shortening), grows the surface to include them, and draws each as text centered on the placement, with no
background rectangle (an end label sits beside its own line, and a background could only erase a neighboring
connector). The text is centered vertically on the placement (baseline about 0.35 em
below it), the same anchor the SVG renderer's `dominant-baseline="middle"` text uses, so the two formats agree.

#### Content outside the surface for a custom theme

After resolving labels with the actual theme, `RenderToSurface` calls `ConnectorLabelPlacer.MeasureLeadingOverhang` and,
when the overhang is non-zero, allocates the larger surface and translates the canvas by it before drawing, exactly as
the SVG renderer does; when it is zero nothing changes, so existing output is byte-identical.

#### Port Glyphs

`RenderPort` draws the legacy filled 8 × 8 square unchanged for a port without an appearance. An outlined glyph is
a background-filled rounded rectangle stroked with the theme stroke (a dashed outline through the same
rounded-rectangle path the dashed boxes use) with each `NotationMetrics.PortArrowSegments` segment stroked as a
polyline; a proxy port is a filled circle with a background ring. An outward label is drawn with the text
centered vertically on `NotationMetrics.PortOutwardLabelAnchor` (the anchor the SVG text uses) and aligned right
or left of it; the surface grows to include it, and marker end shortening uses the glyph's own half size. The
shared helpers make the two renderers identical; `SysmlPortsParityTests` in the gallery project reads the SVG
geometry back against PNG pixels for every direction, side, and outline style.

### CanvasNetRasterRenderer Design Constraints

- The rasterizer shall enforce a minimum surface size of one by one pixels before allocating the
  `Surface`.
- The rasterizer shall use the render theme as the single source of truth for the surface
  background, hollow-marker occlusion fill, and midpoint-label backplate fill.
- Box and grid fills shall be selected from `Theme.DepthFillColors` by layout depth.
- Connector end-marker geometry shall derive from `NotationMetrics`, matching the SVG renderer's
  marker dimensions.
- Notation primitives (dotted-triangle and circled-plus markers, corner icons, thick-bar and crossed-circle
  badges, folder tab sizing, title-area heights) shall derive their geometry from `NotationMetrics` and
  `BoxMetrics`, and a layout node without the corresponding appearance properties shall draw exactly as
  before.
- The output stream remains owned by the caller; the renderer writes encoded bytes but does not
  close or flush the stream.

### CanvasNetRasterRenderer Interactions

`CanvasNetRasterRenderer` consumes model nodes from the Rendering system and `RenderOptions`,
`Theme`, `NotationMetrics`, `BoxMetrics`, and `ConnectorLabelPlacer` from the
Rendering.Abstractions system. It is subclassed by the private `PngRendererCore` and
`JpegRendererCore` nested types, which provide only format-selection metadata and the final save
step.

### CanvasNetRasterRenderer Error Handling

- **Null arguments** — `RenderToSurface(layout, options)` and `Render(layout, options, output)` use
  `ArgumentNullException.ThrowIfNull` on their required inputs before doing any work, so a null
  `LayoutTree`, `RenderOptions`, or output `Stream` fails fast.
- **Degenerate layout sizes** — non-positive layout widths or heights are clamped to a minimum of
  one by one pixels before `Surface` allocation so callers always receive a valid encoded image,
  including for an empty tree.
- **CanvasNet or codec exceptions** — errors surfaced by CanvasNet drawing or by the concrete codec
  save method are not caught. They propagate unchanged to the caller so they remain visible in test
  output and diagnostics; the renderer records no logs of its own.
- **Output stream errors** — exceptions raised while the codec writes to the caller-owned stream
  propagate to the caller. The renderer never closes, flushes, or otherwise mutates that stream
  beyond writing the encoded bytes.
- **Resource cleanup on failure** — the render path disposes the allocated `Surface` with a `using`
  declaration even when an exception is thrown mid-render. Other CanvasNet helper objects
  (`Canvas`, `PathBuilder`, `Geometry.Path`, `StrokeStyle`, `TrueTypeFont`) are plain managed
  objects and require no explicit disposal.

### CanvasNetRasterRenderer Dependencies

- **`DemaConsulting.Rendering`** — consumes `LayoutTree`, `LayoutNode`, `LayoutBox`, `LayoutLine`,
  `LayoutLabel`, `LayoutPort`, and the other layout-node records.
- **`DemaConsulting.Rendering.Abstractions`** — consumes the `IRenderer` contract it implements as
  well as `RenderOptions`, `Theme`, `NotationMetrics`, `BoxMetrics`, and `ConnectorLabelPlacer`
  for drawing geometry and typography.
- **CanvasNet (OTS)** — uses `Surface`, `Canvas`, `Rgba32`, `StrokeStyle`, `PathBuilder`,
  `Geometry.Path`, `CornerRoundEffect`, `TextRenderer`, and `TrueTypeFont` for allocation,
  drawing, text measurement, and encoding orchestration; see *CanvasNet Integration Design* under
  `docs/design/ots/` for lifecycle details.
- **Embedded Noto Sans typefaces** — the regular, bold, italic, and bold-italic Noto Sans font
  resources embedded in the assembly are loaded once as `TrueTypeFont` instances by
  `CanvasNetTypefaces`.

### CanvasNetRasterRenderer Callers

- **`PngRenderer.PngRendererCore`, `JpegRenderer.JpegRendererCore`** — the two concrete raster
  renderer cores each derive from `CanvasNetRasterRenderer` and rely on its `Render`
  implementation; they contribute only format metadata and the codec call.
- **`RendererRegistry` / consumers of `IRenderer`** — external callers do not use
  `CanvasNetRasterRenderer` directly (it is `abstract`); they resolve one of the concrete public
  wrappers by media type or file extension and invoke `Render` through the `IRenderer` contract.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-CanvasNet-CanvasNetRasterRenderer-DrawsLayoutTree | `RenderToSurface` drawing, markers, labels |
| Rendering-CanvasNet-CanvasNetRasterRenderer-ThemeColors | Box and grid fill selection from `Theme.DepthFillColors` |
| Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkers | End-marker drawing helpers that use `NotationMetrics` |
| Rendering-CanvasNet-CanvasNetRasterRenderer-EmptyTree | Minimum surface size enforcement in `RenderToSurface` |
| Rendering-CanvasNet-CanvasNetRasterRenderer-PortLabelPlacement | Port glyph/label placement on every side |
| Rendering-CanvasNet-CanvasNetRasterRenderer-ContentInsetLeft | `ContentInsetLeft`-aware compartment/title start |
| Rendering-CanvasNet-CanvasNetRasterRenderer-CanvasGrowsForLabels | `RenderToSurface` grows to fit every placed label |
| Rendering-CanvasNet-CanvasNetRasterRenderer-TitleCentersOnBoxWidth | `RenderBoxTitle` centers on full box width |
| Rendering-CanvasNet-CanvasNetRasterRenderer-PortGlyphOutline | `RenderPort` outlines the glyph in background color |
| Rendering-CanvasNet-CanvasNetRasterRenderer-PortLabelSqueeze | `DrawPortLabel` bounds width to `port.MaxLabelWidth` |
| Rendering-CanvasNet-CanvasNetRasterRenderer-SharedTypefaces | `CanvasNetTypefaces.Resolve` lazily loaded fields |
| Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkerDottedTriangles | `DrawEndMarker` dotted-triangle cases |
| Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkerCircledPlus | `DrawEndMarker` circled-plus case |
| Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkerSourceEnd | `RenderLine` outward direction and `MarkerPoint` |
| Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkerTipOnEndpoint | `RenderLine` tip retraction by `TipRetraction` |
| Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkerPortEndpoint | `ShortenPortEnds` shortens marker ends on a port |
| Rendering-CanvasNet-CanvasNetRasterRenderer-BoxOutlineStyle | `DashArrayFor` and `BuildRoundRectPath` |
| Rendering-CanvasNet-CanvasNetRasterRenderer-BoxNameStyle | `RenderBoxTitle` resolved name style |
| Rendering-CanvasNet-CanvasNetRasterRenderer-CompartmentTitleStyle | `RenderBoxCompartments` title layout |
| Rendering-CanvasNet-CanvasNetRasterRenderer-FolderTitleInTab | `RenderBoxTitle` and `BoxMetrics.IsNameInTab` |
| Rendering-CanvasNet-CanvasNetRasterRenderer-BoxCornerIcon | `RenderBoxCornerIcon` |
| Rendering-CanvasNet-CanvasNetRasterRenderer-BadgeThickBar | `RenderBadge` thick-bar cases |
| Rendering-CanvasNet-CanvasNetRasterRenderer-BadgeCrossedCircle | `RenderBadge` crossed-circle case |
| Rendering-CanvasNet-CanvasNetRasterRenderer-BandDashed | `RenderBand` dashed stroke |
| Rendering-CanvasNet-CanvasNetRasterRenderer-LifelineRoundedHeader | `RenderLifeline` header and keyword |
| Rendering-CanvasNet-CanvasNetRasterRenderer-LabelsAvoidBoxes | `RenderToSurface` passes `CollectBoxes` to the placer |
| Rendering-CanvasNet-CanvasNetRasterRenderer-CustomThemeContainment | `RenderToSurface` overhang translate |
