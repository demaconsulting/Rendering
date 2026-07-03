## SkiaSharp Integration Design

### Purpose

SkiaSharp is the raster-graphics library that backs the `DemaConsulting.Rendering.Skia` renderer
tier. Unlike the other OTS items in this document set, it is not build-time or documentation
tooling: it is a runtime NuGet dependency linked into the delivered `DemaConsulting.Rendering.Skia`
package, providing the bitmap canvas, drawing primitives, typeface loading, and image encoders that
`SkiaRasterRenderer` and its concrete formats (`PngRenderer`, `JpegRenderer`, `WebpRenderer`) use to
turn a placed `LayoutTree` into PNG, JPEG, and WEBP output.

### Features Used

- **Bitmap canvas** — `SKBitmap` and `SKCanvas` provide the in-memory raster surface that
  `SkiaRasterRenderer` draws layout boxes, connectors, and labels onto.
- **Shape and text drawing** — `SKPaint`, `SKPaintStyle`, and `SKColor` draw filled/stroked
  rectangles, lines, and other primitives; `SKTypeface` (loaded from embedded Noto Sans font
  resources) and `SKPaint` text APIs render node titles and labels.
- **Image encoding** — `SKBitmap`/`SKCanvas` output is encoded by each concrete renderer into its
  target container format: `PngRenderer` (PNG), `JpegRenderer` (JPEG), and `WebpRenderer` (WEBP).

### Integration Pattern

SkiaSharp is referenced as a NuGet package dependency (`SkiaSharp`, plus the platform-specific
`SkiaSharp.NativeAssets.Win32`/`.Linux.NoDependencies`/`.macOS` native asset packages selected by
MSBuild OS conditions) in `DemaConsulting.Rendering.Skia.csproj`. It is a compile-time and run-time
dependency of the shipped `DemaConsulting.Rendering.Skia` NuGet package, not a local .NET tool or a
build/lint-time utility. `SkiaRasterRenderer` is the abstract base class that wraps the SkiaSharp
APIs; `PngRenderer`, `JpegRenderer`, and `WebpRenderer` each configure it for their target image
format and implement the `DemaConsulting.Rendering.Abstractions.IRenderer` contract so callers can
render a `LayoutTree` without depending on SkiaSharp types directly.
