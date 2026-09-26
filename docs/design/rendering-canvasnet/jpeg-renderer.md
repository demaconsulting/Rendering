## JpegRenderer Unit Design

Part of the Rendering.CanvasNet system.

### JpegRenderer Overview

`JpegRenderer` is the concrete raster renderer for lossy JPEG output. It is a thin public wrapper
that delegates to a singleton private `JpegRendererCore` subclass of `CanvasNetRasterRenderer`.
The wrapper supplies the JPEG metadata and a fixed encoding quality of `90`; all drawing behavior
comes from the shared rasterizer.

### JpegRenderer Data Model

| Member | Value |
| --- | --- |
| `EncodingQuality` | `90` |
| `Core` | singleton `JpegRendererCore` instance |
| `MediaType` | `image/jpeg` |
| `DefaultExtension` | `.jpg` |
| `FileExtensions` | `.jpg`, `.jpeg` |

### JpegRenderer Interactions

A `RendererRegistry` can resolve `JpegRenderer` by the `image/jpeg` media type or by either
advertised file extension. Because JPEG has no transparency channel, it relies on the inherited
rasterizer's theme background initialization before encoding.

### JpegRenderer Key Methods

`JpegRenderer` declares no drawing logic of its own. It delegates to the shared rasterizer through
small wrapper methods and a private core save step:

- `Render(LayoutTree, RenderOptions, Stream)` forwards directly to `Core.Render(...)`.
- `JpegRendererCore.Save(Surface, Stream)` calls `JpegCodec.Save(surface, output, 90)` after null
  checks, causing the inherited drawing path to be encoded as JPEG at the fixed public quality.
- `MediaType`, `DefaultExtension`, and `FileExtensions` forward to the core's advertised JPEG
  identifiers used by `RendererRegistry` for lookup.

The inherited algorithm, preconditions, and postconditions are documented in
*CanvasNetRasterRenderer Unit Design*.

### JpegRenderer Error Handling

`JpegRenderer` contains no error-detection or error-handling logic of its own. All argument
validation for layout/options, minimum-size clamping, drawing, and surface disposal happen in the
inherited `CanvasNetRasterRenderer` methods. Any exceptions raised by `JpegCodec.Save` (for
example, a disposed or non-writable stream) propagate unchanged to the caller.

### JpegRenderer Dependencies

- **`CanvasNetRasterRenderer` (base unit)** — provides all rasterization and drawing logic.
- **CanvasNet (OTS)** — used through `Surface` and `JpegCodec`; all low-level drawing API calls are
  made by the base class.
- **`DemaConsulting.Rendering.Abstractions`** — provides the `IRenderer` contract that
  `JpegRenderer` implements.

### JpegRenderer Callers

- **`RendererRegistry`** — resolves this renderer by the `image/jpeg` media type or by the `.jpg`
  or `.jpeg` extension.
- **Applications and tools that reference `DemaConsulting.Rendering.CanvasNet`** — either
  instantiate `JpegRenderer` directly (`new JpegRenderer().Render(...)`) or resolve it through the
  registry and invoke it through the `IRenderer` contract.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-CanvasNet-JpegRenderer-EmitsJpeg | JPEG codec, quality, media type, and extension members |
