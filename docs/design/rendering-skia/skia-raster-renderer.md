## SkiaRasterRenderer Unit Design

Part of the Rendering.Skia system.

### SkiaRasterRenderer Overview

`SkiaRasterRenderer` is the abstract `IRenderer` implementation that provides the common SkiaSharp
rasterization path for every raster format. Concrete renderers supply the encoded image format, quality,
media type, and file extensions; the base class owns argument validation, bitmap allocation, canvas
initialization, node drawing, connector-label finalization, and stream encoding.

### SkiaRasterRenderer Data Model

| Member | Type | Description |
| --- | --- | --- |
| `EncodedFormat` | `abstract SKEncodedImageFormat` | The SkiaSharp encode format supplied by the concrete renderer. |
| `EncodingQuality` | `virtual int` (default 100) | Quality passed to the encoder; used by lossy formats. |
| `MediaType` | `abstract string` | The MIME media type reported to callers and registries. |
| `DefaultExtension` | `abstract string` | The primary output file extension. |
| `FileExtensions` | `abstract IReadOnlyList<string>` | Every file extension the concrete renderer produces. |

### SkiaRasterRenderer Methods

- **`Render(LayoutTree, RenderOptions, Stream)`** - validates its arguments, allocates an `SKBitmap`
  sized from `LayoutTree.Width`/`LayoutTree.Height` scaled by `RenderOptions.Scale` (minimum one by one
  pixel), clears the bitmap to `RenderOptions.Theme.BackgroundColor`, draws every node, draws connector
  labels in a final pass, and writes the encoded bytes to the stream without closing or flushing it.
- **Node drawing helpers** - draw boxes, labels, lines, ports, badges, bands, lifelines, activations,
  and grids using the render theme, the embedded Noto Sans typefaces, shared notation metrics, and
  shared box metrics.
- **Connector helpers** - build connector end markers from `NotationMetrics` so raster markers match
  the SVG renderer. Hollow marker interiors and midpoint-label backplates use `Theme.BackgroundColor`
  so they occlude connector strokes with the same background used for the bitmap.

### SkiaRasterRenderer Design Constraints

- The rasterizer shall enforce a minimum bitmap size of one by one pixels before allocating the
  `SKBitmap`.
- The rasterizer shall use the render theme as the single source of truth for the canvas background,
  hollow-marker occlusion fill, and midpoint-label backplate fill.
- Box and grid fills shall be selected from `Theme.DepthFillColors` by layout depth.
- Connector end-marker geometry shall derive from `NotationMetrics`, matching the SVG renderer's
  marker dimensions.
- The output stream remains owned by the caller; the renderer writes encoded bytes but does not close
  or flush the stream.

### SkiaRasterRenderer Interactions

`SkiaRasterRenderer` consumes model nodes from the Rendering system and `RenderOptions`, `Theme`,
`NotationMetrics`, `BoxMetrics`, and `ConnectorLabelPlacer` from the Rendering.Abstractions system. It
is inherited by `PngRenderer`, `JpegRenderer`, and `WebpRenderer`, which provide only format-selection
members.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Skia-SkiaRasterRenderer-DrawsLayoutTree | `Render`, node drawing, background, markers, and labels |
| Rendering-Skia-SkiaRasterRenderer-ThemeColours | Box and grid fill selection from `Theme.DepthFillColors` |
| Rendering-Skia-SkiaRasterRenderer-EndMarkers | End-marker drawing helpers that use `NotationMetrics` |
| Rendering-Skia-SkiaRasterRenderer-EmptyTree | Minimum bitmap width and height enforcement in `Render` |
