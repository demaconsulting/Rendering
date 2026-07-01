# Rendering.Skia

The Rendering.Skia system renders a placed `LayoutTree` to raster images using
[SkiaSharp](https://github.com/mono/SkiaSharp). A shared `SkiaRasterRenderer` base performs all
drawing; thin concrete renderers select the encoded output format.

## Overview

The system draws a diagram once onto a SkiaSharp bitmap and then encodes it in a concrete image
format. All drawing logic — boxes, labels, connectors with end markers, ports, badges, bands,
lifelines, activations, and grids — lives in the abstract `SkiaRasterRenderer`. Each concrete
renderer (`PngRenderer`, `JpegRenderer`, `WebpRenderer`) supplies only the SkiaSharp encoded-image
format, encoding quality, media type, and file extensions. This keeps the multi-format surface a
few lines per format while guaranteeing that PNG, JPEG, and WEBP output is pixel-identical apart
from the final encode step.

An embedded Noto Sans font (SIL OFL 1.1) is used for all text so that raster output is
byte-reproducible across platforms regardless of installed fonts. SkiaSharp is MIT-licensed.

## Software Structure

```text
Rendering.Skia (System)
├── SkiaRasterRenderer (Unit)  — abstract base: rasterizes a LayoutTree to a bitmap and encodes it
├── PngRenderer (Unit)         — lossless PNG output
├── JpegRenderer (Unit)        — lossy JPEG output
└── WebpRenderer (Unit)        — WEBP output
```

## SkiaRasterRenderer

### Rasterization

`SkiaRasterRenderer` is an `abstract class` implementing `IRenderer`. Its sealed `Render` method
allocates an `SKBitmap` sized from `LayoutTree.Width`/`Height` scaled by `RenderOptions.Scale`
(minimum one by one pixel), clears it to white, draws every node, and then encodes the bitmap using
the format supplied by the derived class.

### Data Model

| Member | Type | Description |
| --- | --- | --- |
| `EncodedFormat` | `abstract SKEncodedImageFormat` | The SkiaSharp encode format supplied by the concrete renderer. |
| `EncodingQuality` | `virtual int` (default 100) | Quality passed to the encoder; used by lossy formats. |
| `MediaType` | `abstract string` | The MIME media type reported to callers and registries. |
| `DefaultExtension` | `abstract string` | The primary output file extension. |
| `FileExtensions` | `abstract IReadOnlyList<string>` | Every file extension the concrete renderer produces. |

### Methods

- **`Render(LayoutTree, RenderOptions, Stream)`** — validates its arguments, draws the diagram, and
  writes the encoded bytes to the stream. It does not close or flush the stream. Text is drawn with
  the embedded Noto Sans typefaces; box and grid fills are read from `Theme.DepthFillColors`;
  connector end markers are built from the shared `NotationMetrics` so they match the SVG renderer.

### Dependencies

Consumes the `LayoutTree` and node records from the Rendering model, and the `Theme`,
`RenderOptions`, `NotationMetrics`, `BoxMetrics`, and `ConnectorLabelPlacer` helpers from
Rendering.Abstractions. It depends on SkiaSharp (OTS).

## PngRenderer, JpegRenderer, WebpRenderer

Each concrete renderer is a `sealed class` deriving from `SkiaRasterRenderer` that overrides only
the format-selection members:

| Renderer | `EncodedFormat` | Quality | `MediaType` | Extensions |
| --- | --- | --- | --- | --- |
| `PngRenderer` | `Png` | 100 (lossless) | `image/png` | `.png` |
| `JpegRenderer` | `Jpeg` | 90 | `image/jpeg` | `.jpg`, `.jpeg` |
| `WebpRenderer` | `Webp` | 90 | `image/webp` | `.webp` |

Because every renderer advertises the file extensions it produces through `IRenderer.FileExtensions`,
a `RendererRegistry` can resolve the correct renderer directly from an output filename's extension
(for example `diagram.webp`), in addition to resolving by media type.

## Interactions

The three concrete renderers have no interactions with each other; each is a leaf that inherits all
behaviour from `SkiaRasterRenderer` and differs only in the encoded output format it selects.
