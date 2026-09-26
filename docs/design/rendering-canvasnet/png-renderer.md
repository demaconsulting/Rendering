## PngRenderer Unit Design

Part of the Rendering.CanvasNet system.

### PngRenderer Overview

`PngRenderer` is the concrete raster renderer for lossless PNG output. It is a thin public wrapper
that delegates to a singleton private `PngRendererCore` subclass of `CanvasNetRasterRenderer`.
The wrapper supplies the PNG metadata and exposes an internal `RenderToSurface` seam for pixel
sampling in tests; all drawing behavior comes from the shared rasterizer.

### PngRenderer Data Model

| Member | Value |
| --- | --- |
| `Core` | singleton `PngRendererCore` instance |
| `MediaType` | `image/png` |
| `DefaultExtension` | `.png` |
| `FileExtensions` | `.png` |

### PngRenderer Interactions

A `RendererRegistry` can resolve `PngRenderer` by the `image/png` media type or by the `.png` file
extension. After resolution, callers invoke the wrapper's `Render` method and receive a PNG byte
stream written to their output stream. Internal tests can call `RenderToSurface` to inspect the
exact rendered pixels before any encode/decode round trip.

### PngRenderer Key Methods

`PngRenderer` declares no drawing logic of its own. It delegates to the shared rasterizer through
three small members:

- `Render(LayoutTree, RenderOptions, Stream)` forwards directly to `Core.Render(...)`.
- `RenderToSurface(LayoutTree, RenderOptions)` forwards to `PngRendererCore.RenderToSurface(...)`
  so tests can sample pixels from the managed `Surface`.
- `PngRendererCore.Save(Surface, Stream)` calls `PngCodec.Save(surface, output)` after null checks,
  causing the inherited drawing path to be encoded as PNG.

The inherited algorithm, preconditions, and postconditions are documented in
*CanvasNetRasterRenderer Unit Design*.

### PngRenderer Error Handling

`PngRenderer` contains no error-detection or error-handling logic of its own. All argument
validation for layout/options, minimum-size clamping, drawing, and surface disposal happen in the
inherited `CanvasNetRasterRenderer` methods. Any exceptions raised by `PngCodec.Save` (for example,
a disposed or non-writable stream) propagate unchanged to the caller.

### PngRenderer Dependencies

- **`CanvasNetRasterRenderer` (base unit)** — provides all rasterization and drawing logic.
- **CanvasNet (OTS)** — used through `Surface` and `PngCodec`; all low-level drawing API calls are
  made by the base class.
- **`DemaConsulting.Rendering.Abstractions`** — provides the `IRenderer` contract that
  `PngRenderer` implements.

### PngRenderer Callers

- **`RendererRegistry`** — resolves this renderer by the `image/png` media type or by the `.png`
  extension.
- **Applications and tools that reference `DemaConsulting.Rendering.CanvasNet`** — either
  instantiate `PngRenderer` directly (`new PngRenderer().Render(...)`) or resolve it through the
  registry and invoke it through the `IRenderer` contract.
- **`DemaConsulting.Rendering.CanvasNet.Tests`** — uses the internal `RenderToSurface` seam for
  exact pixel assertions.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-CanvasNet-PngRenderer-EmitsPng | PNG codec, media type, and extension members |
