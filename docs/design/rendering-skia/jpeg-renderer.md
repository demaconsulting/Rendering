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

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Skia-JpegRenderer-EmitsJpeg | JPEG format, quality, media type, and extension overrides |
