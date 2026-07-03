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

**Initialization.** SkiaSharp requires no explicit initialization: the platform-specific native
asset packages are loaded on first use by the SkiaSharp NuGet package. The embedded Noto Sans font
resource is loaded once per typeface at renderer startup by opening the manifest resource stream,
wrapping it in an `SKData` created via `SKData.Create(stream)`, and turning it into an `SKTypeface`;
the stream and the intermediate `SKData` are disposed with `using` declarations immediately after
the typeface has been created.

**Configuration.** Each concrete renderer configures SkiaSharp only through its overrides of
`EncodedFormat` (`SKEncodedImageFormat.Png`/`Jpeg`/`Webp`) and `EncodingQuality`. No SkiaSharp
global state is mutated; every render call constructs its own drawing objects.

**Resource lifecycle and disposal.** All SkiaSharp objects used by `SkiaRasterRenderer` are managed
wrappers around unmanaged Skia handles and implement `IDisposable`. They are therefore created and
disposed deterministically with C# `using` declarations or `using` blocks so that native memory,
GPU/CPU raster buffers, and encoded-byte buffers are released before `Render` returns:

- **`SKBitmap`** — allocated inside `Render` (`using var bitmap = new SKBitmap(w, h, …)`) and
  disposed at method exit; it holds the raster pixel buffer for the whole drawing pass.
- **`SKCanvas`** — created over the bitmap (`using var canvas = new SKCanvas(bitmap)`) and disposed
  at method exit so the canvas releases its reference to the bitmap before the bitmap itself is
  disposed.
- **`SKImage`** — created from the bitmap for encoding (`using var image = SKImage.FromBitmap(bitmap)`)
  and disposed at method exit.
- **`SKData`** — produced by `image.Encode(EncodedFormat, EncodingQuality)` and disposed after its
  bytes have been copied to the caller-owned output `Stream` via `data.SaveTo(output)`. The
  transient `SKData` used to wrap the embedded font resource stream is likewise disposed with a
  `using` declaration.
- **`SKPaint`, `SKPath`, `SKPathEffect`** — drawing primitives created per node (fills, strokes,
  text paints, dashed/cornered path effects, marker path shapes) are each wrapped in `using`
  declarations or `using` blocks so their unmanaged handles are freed before the drawing helper
  returns; none of them outlive a single `Render` call.
- **`SKTypeface`** — the embedded Noto Sans typefaces are the one long-lived Skia resource:
  they are loaded once at renderer initialization and cached for the lifetime of the process,
  because loading a typeface is expensive and the font bytes never change.
- **Manifest resource streams** — the `System.IO.Stream` returned by
  `Assembly.GetManifestResourceStream` for the embedded font is opened with a `using` declaration
  during typeface loading and disposed as soon as the `SKData` wrapping it has been consumed.
- **Caller-owned output `Stream`** — the `Stream` argument to `Render` is written to via
  `SKData.SaveTo(output)` but is neither flushed, closed, nor disposed by the renderer; ownership
  of that stream, and any decision to flush or dispose it, stays with the caller.

**Threading.** `SkiaRasterRenderer` treats each `Render` call as a self-contained operation: the
bitmap, canvas, paints, and encoder are all local to the call, so concurrent renders on different
threads with different renderer instances (or even the same instance) do not share mutable
SkiaSharp state.
