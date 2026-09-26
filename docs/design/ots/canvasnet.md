## CanvasNet Integration Design

### Purpose

CanvasNet is the raster-graphics library that backs the `DemaConsulting.Rendering.CanvasNet`
renderer tier. Unlike the build and documentation OTS items in this document set, it is a runtime
NuGet dependency linked into the delivered `DemaConsulting.Rendering.CanvasNet` package, providing
the managed raster surface, drawing primitives, path geometry, typeface loading, and image codecs
that `CanvasNetRasterRenderer`, `PngRenderer`, and `JpegRenderer` use to turn a placed
`LayoutTree` into PNG and JPEG output.

CanvasNet is authored by DemaConsulting as a **pure native-managed .NET library with no P/Invoke
layer and no native binary dependency**. Its only package dependency is
`System.Numerics.Tensors`, which it uses as a SIMD helper.

**License**: MIT (`DemaConsulting.CanvasNet`, referenced as a NuGet package; see the `Dependencies`
section of *Rendering.CanvasNet Design* for the same license statement at the system level).

**Selection rationale**: CanvasNet was selected to replace the prior SkiaSharp-based raster backend
specifically because it is a native-managed .NET library with no P/Invoke layer, whereas SkiaSharp
requires a native binary per platform/architecture that was the source of cross-platform DLL
compatibility problems this migration set out to resolve. CanvasNet is authored by this same
organization (DemaConsulting), which allows the rendering libraries to depend on a raster backend
whose roadmap and codec support can be directly influenced rather than depending on a third-party
native binding. The trade-off accepted in choosing CanvasNet is a currently narrower codec surface
(PNG and JPEG only - no WEBP encoder, unlike SkiaSharp) and a pre-1.0, beta-stage package with the
corresponding API-stability caveats that implies; see *Rendering.CanvasNet Design* (Design
Constraints) for how the WEBP-format gap is scoped as an intentional trade-off of this replacement
rather than an oversight.

**Maturity**: CanvasNet is currently pre-1.0 (a beta-series prerelease). Its public API surface may
still change in a way that a stable 1.0+ release would not, so upgrades should be reviewed against
CanvasNet's release notes rather than assumed to be purely additive.

### Features Used

- **Managed raster surface** — `Surface` provides the in-memory raster pixel store that
  `CanvasNetRasterRenderer` clears, draws into, and finally passes to the concrete codec.
- **Drawing canvas and geometry** — `Canvas`, `StrokeStyle`, `PathBuilder`, `Geometry.Path`, and
  `CornerRoundEffect` draw filled and stroked rectangles, rounded outlines, connector paths, and
  end-marker shapes.
- **Text rendering** — `TextRenderer` measures and draws node titles and labels using embedded Noto
  Sans `TrueTypeFont` instances loaded from assembly resource streams.
- **Image encoding** — `PngCodec` and `JpegCodec` encode the rendered `Surface` into PNG and JPEG
  containers.

### Integration Pattern

CanvasNet is referenced as a NuGet package dependency (`DemaConsulting.CanvasNet`) in
`DemaConsulting.Rendering.CanvasNet.csproj`. It is a compile-time and runtime dependency of the
shipped `DemaConsulting.Rendering.CanvasNet` NuGet package, not a local .NET tool or a build/lint
utility. `CanvasNetRasterRenderer` is the abstract base class that wraps the CanvasNet APIs;
`PngRenderer` and `JpegRenderer` each configure it for their target image format and implement the
`DemaConsulting.Rendering.Abstractions.IRenderer` contract so callers can render a `LayoutTree`
without depending on CanvasNet types directly.

**Initialization.** CanvasNet requires no explicit initialization and no platform-conditioned native
asset loading. The embedded Noto Sans font resource is loaded on first use by opening the manifest
resource stream and passing it directly to `TrueTypeFont.Load(stream)`; the stream is disposed
immediately after the typeface has been created.

**Configuration.** Each concrete renderer configures CanvasNet only through its metadata overrides
and codec save call (`PngCodec.Save` or `JpegCodec.Save`). No CanvasNet global state is mutated;
every render call constructs its own drawing objects over a fresh `Surface`.

**Resource lifecycle and disposal.** CanvasNet's object model is predominantly ordinary managed
objects. The only object in the renderer path that requires deterministic disposal is `Surface`,
which owns the raster buffer and implements `IDisposable`; it is allocated inside `Render` or
`RenderToSurface` and disposed by the caller or by the wrapping `using` declaration. `Canvas`,
`Path`, `PathBuilder`, `StrokeStyle`, and `TrueTypeFont` are plain managed objects and do not form
a cascading disposal chain.

**Caller-owned output stream.** The `Stream` argument to `Render` is written to by the selected
codec but is neither flushed, closed, nor disposed by the renderer; ownership of that stream stays
with the caller.

**Threading.** `CanvasNetRasterRenderer` treats each `Render` call as a self-contained operation:
the surface, canvas, paths, and codec call are all local to the call, so concurrent renders on
different threads with different renderer instances (or even the same instance) do not share
mutable renderer state.
