# WebpRenderer Unit Design

Part of the Rendering.Skia system.

## WebpRenderer Overview

`WebpRenderer` is the concrete raster renderer for WEBP output. It derives from `SkiaRasterRenderer`
and supplies only the WEBP format-selection metadata and encoding quality; all drawing behaviour comes
from the shared rasterizer.

## WebpRenderer Data Model

| Member | Value |
| --- | --- |
| `EncodedFormat` | `SKEncodedImageFormat.Webp` |
| `EncodingQuality` | `90` |
| `MediaType` | `image/webp` |
| `DefaultExtension` | `.webp` |
| `FileExtensions` | `.webp` |

## WebpRenderer Interactions

A `RendererRegistry` can resolve `WebpRenderer` by the `image/webp` media type or by the `.webp` file
extension. After resolution, callers invoke the inherited `Render` method and receive a WEBP container
written to their output stream.

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Skia-WebpRenderer-EmitsWebp | WEBP format, quality, media type, and extension overrides |
