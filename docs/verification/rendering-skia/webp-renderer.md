## WebpRenderer Unit Verification

Part of the Rendering.Skia Verification.

This document describes the verification design for the `WebpRenderer` unit of the
`DemaConsulting.Rendering.Skia` system. It maps every WebpRenderer unit requirement to at least one
named test scenario so a reviewer can confirm coverage without reading the test code.

### WebpRenderer Verification Approach

The `WebpRenderer` unit is verified with xUnit v3 unit tests in
`DemaConsulting.Rendering.Skia.Tests` (`SkiaFormatRendererTests.cs`) that render a placed
`LayoutTree` into a `MemoryStream` and inspect the resulting bytes. The renderer is used as a real
`IRenderer` — no dependencies are mocked or stubbed — because it is a pure, deterministic
transformation from layout to encoded image bytes. Verification checks that the output is a valid
RIFF/WEBP container and that the renderer's advertised `MediaType` and `FileExtensions` match the
WEBP contract.

### WebpRenderer Test Environment

- **Framework**: xUnit v3.
- **Target frameworks**: `net8.0`, `net9.0`, `net10.0`.
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline (see
  _Rendering.Skia Verification_ for the system-level environment).
- **External services / files**: none. Rendering is deterministic and writes to a `MemoryStream`.
- **Native assets**: the SkiaSharp platform-specific native asset packages provided by
  `DemaConsulting.Rendering.Skia`'s NuGet references must be available at test time; no other
  setup beyond `dotnet restore` is required.

### WebpRenderer Acceptance Criteria

A verification run passes when the WEBP scenarios below execute without an unexpected exception,
the encoded output is a valid RIFF/WEBP container header, and the renderer reports the
`image/webp` media type and `.webp` file extension. Any wrong container header, wrong metadata
value, or unexpected exception constitutes a failure.

### WebpRenderer Unit Scenarios

#### WEBP output has the expected container header and metadata

Test `WebpRenderer_Render_ProducesWebpContainerHeader` renders a sample layout and asserts that output
is a RIFF/WEBP container and that the renderer reports the `image/webp` media type and `.webp` file
extension.

**Covers**: `Rendering-Skia-WebpRenderer-EmitsWebp`.

### Requirements Coverage

- **`Rendering-Skia-WebpRenderer-EmitsWebp`**: WebpRenderer_Render_ProducesWebpContainerHeader
