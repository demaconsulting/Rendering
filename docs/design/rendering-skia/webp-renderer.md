## WebpRenderer Unit Design

Part of the Rendering.Skia system.

### WebpRenderer Overview

`WebpRenderer` is the concrete raster renderer for WEBP output. It derives from `SkiaRasterRenderer`
and supplies only the WEBP format-selection metadata and encoding quality; all drawing behaviour comes
from the shared rasterizer.

### WebpRenderer Data Model

| Member | Value |
| --- | --- |
| `EncodedFormat` | `SKEncodedImageFormat.Webp` |
| `EncodingQuality` | `90` |
| `MediaType` | `image/webp` |
| `DefaultExtension` | `.webp` |
| `FileExtensions` | `.webp` |

### WebpRenderer Interactions

A `RendererRegistry` can resolve `WebpRenderer` by the `image/webp` media type or by the `.webp` file
extension. After resolution, callers invoke the inherited `Render` method and receive a WEBP container
written to their output stream.

### WebpRenderer Key Methods

`WebpRenderer` declares no methods of its own. It inherits `Render(LayoutTree, RenderOptions, Stream)`
from `SkiaRasterRenderer` (see _SkiaRasterRenderer Unit Design_ for the algorithm, preconditions, and
post-conditions) and overrides only the following members that steer the inherited encode step:

- `EncodedFormat` returns `SKEncodedImageFormat.Webp` so the base class encodes the bitmap as WEBP.
- `EncodingQuality` returns `90`, the WEBP quality setting passed to SkiaSharp's encoder.
- `MediaType`, `DefaultExtension`, and `FileExtensions` return the advertised WEBP identifiers used
  by `RendererRegistry` for lookup.

### WebpRenderer Error Handling

`WebpRenderer` contains no error-detection or error-handling logic of its own. All argument
validation (`ArgumentNullException` for null `LayoutTree`, `RenderOptions`, or output `Stream`),
minimum-size clamping, and disposal of SkiaSharp resources happen in the inherited
`SkiaRasterRenderer.Render` method — see _SkiaRasterRenderer Unit Design_ under "Error Handling".
Any exceptions raised by SkiaSharp's WEBP encoder (for example if the underlying native asset
package is missing) propagate unchanged to the caller.

### WebpRenderer Dependencies

- **`SkiaRasterRenderer` (base unit)** — provides all rasterization, drawing, and encoding logic.
- **SkiaSharp (OTS)** — used only through the `SKEncodedImageFormat.Webp` enum value returned by
  `EncodedFormat`; all direct SkiaSharp API calls are made by the base class.
- **`DemaConsulting.Rendering.Abstractions`** — provides the `IRenderer` contract that
  `WebpRenderer` (through its base class) implements.

### WebpRenderer Callers

- **`RendererRegistry`** — resolves this renderer by the `image/webp` media type or by the `.webp`
  extension.
- **Applications and tools that reference `DemaConsulting.Rendering.Skia`** — either instantiate
  `WebpRenderer` directly (`new WebpRenderer().Render(...)`) or resolve it through the registry and
  invoke it through the `IRenderer` contract.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Skia-WebpRenderer-EmitsWebp | WEBP format, quality, media type, and extension overrides |
