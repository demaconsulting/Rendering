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
  all `LayoutLine` nodes, resolves every connector label's placement via
  `ConnectorLabelPlacer.Place` *before* allocating the raster surface, grows the required width and
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
  does.
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

### CanvasNetRasterRenderer Design Constraints

- The rasterizer shall enforce a minimum surface size of one by one pixels before allocating the
  `Surface`.
- The rasterizer shall use the render theme as the single source of truth for the surface
  background, hollow-marker occlusion fill, and midpoint-label backplate fill.
- Box and grid fills shall be selected from `Theme.DepthFillColors` by layout depth.
- Connector end-marker geometry shall derive from `NotationMetrics`, matching the SVG renderer's
  marker dimensions.
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
| Rendering-CanvasNet-CanvasNetRasterRenderer-DrawsLayoutTree | `RenderToSurface`, node drawing, background, markers, and labels |
| Rendering-CanvasNet-CanvasNetRasterRenderer-ThemeColours | Box and grid fill selection from `Theme.DepthFillColors` |
| Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkers | End-marker drawing helpers that use `NotationMetrics` |
| Rendering-CanvasNet-CanvasNetRasterRenderer-EmptyTree | Minimum surface width and height enforcement in `RenderToSurface` |
| Rendering-CanvasNet-CanvasNetRasterRenderer-PortAndContentInset | Port label placement and `ContentInsetLeft`-aware compartment/title starts |
| Rendering-CanvasNet-CanvasNetRasterRenderer-CanvasGrowsForLabels | `RenderToSurface` grows the surface to fit every placed label |
| Rendering-CanvasNet-CanvasNetRasterRenderer-TitleCentersOnBoxWidth | `RenderBoxTitle` centers on full box width |
| Rendering-CanvasNet-CanvasNetRasterRenderer-PortGlyphOutline | `RenderPort` outlines the port glyph in `theme.BackgroundColor` |
| Rendering-CanvasNet-CanvasNetRasterRenderer-PortLabelSqueeze | `DrawPortLabel` bounds label width to `port.MaxLabelWidth` |
| Rendering-CanvasNet-CanvasNetRasterRenderer-SharedTypefaces | `CanvasNetTypefaces.Resolve` and its lazily loaded typeface fields |
