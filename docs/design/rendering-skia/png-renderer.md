## PngRenderer Unit Design

Part of the Rendering.Skia system.

### PngRenderer Overview

`PngRenderer` is the concrete raster renderer for lossless PNG output. It derives from
`SkiaRasterRenderer` and supplies only the PNG format-selection metadata; all drawing behaviour comes
from the shared rasterizer.

### PngRenderer Data Model

| Member | Value |
| --- | --- |
| `EncodedFormat` | `SKEncodedImageFormat.Png` |
| `EncodingQuality` | inherited default `100` |
| `MediaType` | `image/png` |
| `DefaultExtension` | `.png` |
| `FileExtensions` | `.png` |

### PngRenderer Interactions

A `RendererRegistry` can resolve `PngRenderer` by the `image/png` media type or by the `.png` file
extension. After resolution, callers invoke the inherited `Render` method and receive a PNG byte stream
written to their output stream.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Skia-PngRenderer-EmitsPng | PNG format, media type, and extension overrides |
