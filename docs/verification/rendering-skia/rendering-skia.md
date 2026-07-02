# Rendering.Skia Verification

This document describes the system-level verification design for the `DemaConsulting.Rendering.Skia`
system and links to the per-unit verification documents for its four units. It records the verification
strategy, test environment, and acceptance criteria shared by every unit, and maps the system-level
requirement to representative named test scenarios. The detailed per-requirement scenarios live in the
unit documents:

- SkiaRasterRenderer Unit Verification
- PngRenderer Unit Verification
- JpegRenderer Unit Verification
- WebpRenderer Unit Verification

## Verification Strategy

The Skia renderers are verified by unit tests that render small layout trees and assert on the produced
bytes. Format is checked by the encoded file signature (PNG signature, JPEG Start-Of-Image marker, WEBP
RIFF container header). Drawing behaviour is checked by inspecting individual pixels of the decoded PNG
bitmap, including fill colours, stroke colours, and the theme background. The PNG pixel tests exercise
the shared `SkiaRasterRenderer` base that all three formats share, so they also establish the drawing
correctness of the JPEG and WEBP renderers.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK (net8.0, net9.0, net10.0).
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Mocking**: none required; renderers are pure and deterministic.
- **Isolation**: each test renders into its own `MemoryStream`.
- **Test project**: `DemaConsulting.Rendering.Skia.Tests` (`PngRendererTests.cs`,
  `PngRendererPortedTests.cs`, `PngEndMarkerTests.cs`, `SkiaFormatRendererTests.cs`).

## Acceptance Criteria

A verification run passes when every scenario in this system document and in the four unit documents
passes without error or unexpected exception. Any wrong encoded signature, media type, file extension,
pixel colour, marker geometry, or unexpected exception constitutes a failure.

## System Requirements Coverage

The system requirement is satisfied through the unit scenarios documented in the per-unit verification
files; the representative system-level scenarios are:

- **`Rendering-Skia-RenderRasterImage`**: Render_SingleBox_ProducesPngSignature,
  JpegRenderer_Render_ProducesJpegSignature, WebpRenderer_Render_ProducesWebpContainerHeader
