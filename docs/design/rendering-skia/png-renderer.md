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

### PngRenderer Key Methods

`PngRenderer` declares no methods of its own. It inherits `Render(LayoutTree, RenderOptions, Stream)`
from `SkiaRasterRenderer` (see _SkiaRasterRenderer Unit Design_ for the algorithm, preconditions, and
post-conditions) and overrides only the following members that steer the inherited encode step:

- `EncodedFormat` returns `SKEncodedImageFormat.Png` so the base class encodes the bitmap as PNG.
- `EncodingQuality` is not overridden; the inherited default of `100` is used (PNG is lossless, so
  the quality argument does not affect the output).
- `MediaType`, `DefaultExtension`, and `FileExtensions` return the advertised PNG identifiers used
  by `RendererRegistry` for lookup.

### PngRenderer Error Handling

`PngRenderer` contains no error-detection or error-handling logic of its own. All argument
validation (`ArgumentNullException` for null `LayoutTree`, `RenderOptions`, or output `Stream`),
minimum-size clamping, and disposal of SkiaSharp resources happen in the inherited
`SkiaRasterRenderer.Render` method — see _SkiaRasterRenderer Unit Design_ under "Error Handling".
Any exceptions raised by SkiaSharp's PNG encoder (for example if the underlying native asset
package is missing) propagate unchanged to the caller.

### PngRenderer Dependencies

- **`SkiaRasterRenderer` (base unit)** — provides all rasterization, drawing, and encoding logic.
- **SkiaSharp (OTS)** — used only through the `SKEncodedImageFormat.Png` enum value returned by
  `EncodedFormat`; all direct SkiaSharp API calls are made by the base class.
- **`DemaConsulting.Rendering.Abstractions`** — provides the `IRenderer` contract that
  `PngRenderer` (through its base class) implements.

### PngRenderer Callers

- **`RendererRegistry`** — resolves this renderer by the `image/png` media type or by the `.png`
  extension.
- **Applications and tools that reference `DemaConsulting.Rendering.Skia`** — either instantiate
  `PngRenderer` directly (`new PngRenderer().Render(...)`) or resolve it through the registry and
  invoke it through the `IRenderer` contract. The XML documentation example on `PngRenderer` shows
  the direct-instantiation usage.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Skia-PngRenderer-EmitsPng | PNG format, media type, and extension overrides |
