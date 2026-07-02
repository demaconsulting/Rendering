# Rendering.Skia Design

## Overview

The `DemaConsulting.Rendering.Skia` system renders a placed `LayoutTree` to raster images using
[SkiaSharp](https://github.com/mono/SkiaSharp). A shared `SkiaRasterRenderer` base performs all drawing;
thin concrete renderers select the encoded output format.

The system draws a diagram once onto a SkiaSharp bitmap and then encodes it in a concrete image format.
All drawing logic - boxes, labels, connectors with end markers, ports, badges, bands, lifelines,
activations, and grids - lives in the abstract `SkiaRasterRenderer`. Each concrete renderer
(`PngRenderer`, `JpegRenderer`, `WebpRenderer`) supplies only the SkiaSharp encoded-image format,
encoding quality, media type, and file extensions. This keeps the multi-format surface a few lines per
format while guaranteeing that PNG, JPEG, and WEBP output share the same raster drawing path apart from
the final encode step.

An embedded Noto Sans font (SIL OFL 1.1) is used for all text so that raster output is byte-reproducible
across platforms regardless of installed fonts. SkiaSharp is MIT-licensed.

## Software Structure

```text
Rendering.Skia (System)
├── SkiaRasterRenderer (Unit)  - abstract base: rasterizes a LayoutTree to a bitmap and encodes it
├── PngRenderer (Unit)         - lossless PNG output
├── JpegRenderer (Unit)        - lossy JPEG output
└── WebpRenderer (Unit)        - WEBP output
```

- **SkiaRasterRenderer** - the shared SkiaSharp rasterizer that allocates the bitmap, initializes it
  with the render theme's background color, draws layout-tree nodes, and encodes the bitmap using the
  derived renderer's format. Detailed in SkiaRasterRenderer Unit Design.
- **PngRenderer** - the concrete renderer that emits lossless PNG output. Detailed in
  PngRenderer Unit Design.
- **JpegRenderer** - the concrete renderer that emits lossy JPEG output. Detailed in
  JpegRenderer Unit Design.
- **WebpRenderer** - the concrete renderer that emits WEBP output. Detailed in
  WebpRenderer Unit Design.

## System Interactions

The system consumes the `LayoutTree` and node records from the Rendering model, and the `IRenderer`,
`RenderOptions`, `Theme`, `NotationMetrics`, `BoxMetrics`, and `ConnectorLabelPlacer` helpers from the
Rendering.Abstractions system. The three concrete renderers have no interactions with each other; each
is a leaf that inherits all drawing behaviour from `SkiaRasterRenderer` and differs only in the encoded
output format it selects.

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Skia-RenderRasterImage | `SkiaRasterRenderer` rasterization plus the three format encoders |
