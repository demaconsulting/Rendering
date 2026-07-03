## JpegRenderer Unit Design

Part of the Rendering.Skia system.

### JpegRenderer Overview

`JpegRenderer` is the concrete raster renderer for lossy JPEG output. It derives from
`SkiaRasterRenderer` and supplies only the JPEG format-selection metadata and lossy encoding quality;
all drawing behaviour comes from the shared rasterizer.

### JpegRenderer Data Model

| Member | Value |
| --- | --- |
| `EncodedFormat` | `SKEncodedImageFormat.Jpeg` |
| `EncodingQuality` | `90` |
| `MediaType` | `image/jpeg` |
| `DefaultExtension` | `.jpg` |
| `FileExtensions` | `.jpg`, `.jpeg` |

### JpegRenderer Interactions

A `RendererRegistry` can resolve `JpegRenderer` by the `image/jpeg` media type or by either advertised
file extension. Because JPEG has no transparency channel, it relies on the inherited rasterizer's theme
background initialization before encoding.

### JpegRenderer Key Methods

`JpegRenderer` declares no methods of its own. It inherits `Render(LayoutTree, RenderOptions, Stream)`
from `SkiaRasterRenderer` (see _SkiaRasterRenderer Unit Design_ for the algorithm, preconditions, and
post-conditions) and overrides only the following members that steer the inherited encode step:

- `EncodedFormat` returns `SKEncodedImageFormat.Jpeg` so the base class encodes the bitmap as JPEG.
- `EncodingQuality` returns `90`, the JPEG quality setting passed to SkiaSharp's encoder.
- `MediaType`, `DefaultExtension`, and `FileExtensions` return the advertised JPEG identifiers used
  by `RendererRegistry` for lookup.

### JpegRenderer Error Handling

`JpegRenderer` contains no error-detection or error-handling logic of its own. All argument
validation (`ArgumentNullException` for null `LayoutTree`, `RenderOptions`, or output `Stream`),
minimum-size clamping, and disposal of SkiaSharp resources happen in the inherited
`SkiaRasterRenderer.Render` method — see _SkiaRasterRenderer Unit Design_ under "Error Handling".
Any exceptions raised by SkiaSharp's JPEG encoder (for example if the underlying native asset
package is missing) propagate unchanged to the caller.

### JpegRenderer Dependencies

- **`SkiaRasterRenderer` (base unit)** — provides all rasterization, drawing, and encoding logic.
- **SkiaSharp (OTS)** — used only through the `SKEncodedImageFormat.Jpeg` enum value returned by
  `EncodedFormat`; all direct SkiaSharp API calls are made by the base class.
- **`DemaConsulting.Rendering.Abstractions`** — provides the `IRenderer` contract that
  `JpegRenderer` (through its base class) implements.

### JpegRenderer Callers

- **`RendererRegistry`** — resolves this renderer by the `image/jpeg` media type or by the `.jpg`
  or `.jpeg` extension.
- **Applications and tools that reference `DemaConsulting.Rendering.Skia`** — either instantiate
  `JpegRenderer` directly (`new JpegRenderer().Render(...)`) or resolve it through the registry and
  invoke it through the `IRenderer` contract.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Skia-JpegRenderer-EmitsJpeg | JPEG format, quality, media type, and extension overrides |
