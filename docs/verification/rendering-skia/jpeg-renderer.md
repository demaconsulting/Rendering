## JpegRenderer Unit Verification

Part of the Rendering.Skia Verification.

This document describes the verification design for the `JpegRenderer` unit of the
`DemaConsulting.Rendering.Skia` system. It maps every JpegRenderer unit requirement to at least one
named test scenario so a reviewer can confirm coverage without reading the test code.

### JpegRenderer Verification Approach

The `JpegRenderer` unit is verified with xUnit v3 unit tests in
`DemaConsulting.Rendering.Skia.Tests` (`SkiaFormatRendererTests.cs`) that render a small placed
`LayoutTree` into a `MemoryStream` and inspect the resulting bytes. The renderer is exercised as a
real `IRenderer` — no dependencies are mocked or stubbed — because it is a pure, deterministic
transformation from layout to encoded image bytes. Injected dependencies (the theme carried by
`RenderOptions`) are supplied by the test as concrete instances. Verification checks two things:
that the produced byte stream is a real JPEG (Start-Of-Image marker) and that the renderer
advertises the expected media type and file extensions for registry resolution.

### JpegRenderer Test Environment

- **Framework**: xUnit v3.
- **Target frameworks**: `net8.0`, `net9.0`, `net10.0`.
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline (see
  _Rendering.Skia Verification_ for the system-level environment).
- **External services / files**: none. Rendering is deterministic and writes to a `MemoryStream`.
- **Native assets**: the SkiaSharp platform-specific native asset packages provided by
  `DemaConsulting.Rendering.Skia`'s NuGet references must be available at test time; no other
  setup beyond `dotnet restore` is required.

### JpegRenderer Acceptance Criteria

A verification run passes when the JPEG scenarios below execute without an unexpected exception,
the encoded output begins with the JPEG Start-Of-Image marker, and the renderer reports the
`image/jpeg` media type plus the `.jpg` default and `.jpeg` alternate file extensions. Any wrong
byte signature, wrong metadata value, or unexpected exception constitutes a failure.

### JpegRenderer Unit Scenarios

#### JPEG output has the expected signature and metadata

Test `JpegRenderer_Render_ProducesJpegSignature` renders a sample layout and asserts that output begins
with the JPEG Start-Of-Image marker and that the renderer reports the `image/jpeg` media type, the
`.jpg` default extension, and the `.jpeg` alternate extension.

**Covers**: `Rendering-Skia-JpegRenderer-EmitsJpeg`.

### Requirements Coverage

- **`Rendering-Skia-JpegRenderer-EmitsJpeg`**: JpegRenderer_Render_ProducesJpegSignature
