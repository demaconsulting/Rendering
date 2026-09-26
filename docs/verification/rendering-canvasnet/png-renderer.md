## PngRenderer Unit Verification

Part of the Rendering.CanvasNet Verification.

This document describes the verification design for the `PngRenderer` unit of the
`DemaConsulting.Rendering.CanvasNet` system. It maps every PngRenderer unit requirement to at least
one named test scenario so a reviewer can confirm coverage without reading the test code.

### PngRenderer Verification Approach

The `PngRenderer` unit is verified with xUnit v4 unit tests in
`DemaConsulting.Rendering.CanvasNet.Tests` (`PngRendererTests.cs`, `PngRendererPortedTests.cs`, and
`CanvasNetFormatRendererTests.cs`) that render placed `LayoutTree` instances into `MemoryStream`s
and inspect the resulting bytes or sample pixels from `RenderToSurface`. The renderer is used as a
real `IRenderer` — no dependencies are mocked or stubbed — because it is a pure, deterministic
transformation from layout to encoded image bytes. Verification checks that the output begins with
the PNG file signature and that the renderer's advertised `MediaType`, `DefaultExtension`, and
`FileExtensions` match the PNG contract. Because `PngRenderer` uses the whole drawing path from
`CanvasNetRasterRenderer`, the PNG pixel tests in the `PngRendererPortedTests` suite implicitly
cross-verify that the base rasterizer works correctly with the PNG encoder.

### PngRenderer Test Environment

- **Framework**: xUnit v4.
- **Target frameworks**: `net8.0`, `net9.0`, `net10.0`.
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline (see
  *Rendering.CanvasNet Verification* for the system-level environment).
- **External services / files**: none. Rendering is deterministic and writes to a `MemoryStream`
  or in-memory `Surface`.
- **Runtime prerequisites**: none beyond `dotnet restore`; CanvasNet is a pure managed dependency.

### PngRenderer Acceptance Criteria

A verification run passes when the PNG scenarios below execute without an unexpected exception, the
encoded output begins with the PNG signature, and the renderer reports the `image/png` media type
with `.png` in its advertised file extensions. Any wrong byte signature, wrong metadata value, or
unexpected exception constitutes a failure.

### PngRenderer Unit Scenarios

#### PNG output has the expected signature and metadata

Tests `Render_SingleBox_ProducesPngSignature`, `PngRenderer_Render_EmptyTree_WritesPngSignature`,
and `PngRenderer_FileExtensions_ContainsDefault` render sample layouts and assert that output
begins with the PNG signature and that the renderer reports the `image/png` media type and a
`.png` extension that is included in its advertised extensions.

**Covers**: `Rendering-CanvasNet-PngRenderer-EmitsPng`.

### Requirements Coverage

- **`Rendering-CanvasNet-PngRenderer-EmitsPng`**: Render_SingleBox_ProducesPngSignature,
  PngRenderer_Render_EmptyTree_WritesPngSignature, PngRenderer_FileExtensions_ContainsDefault
