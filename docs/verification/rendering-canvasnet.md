# Rendering.CanvasNet Verification

This document describes the system-level verification design for the
`DemaConsulting.Rendering.CanvasNet` system and links to the per-unit verification documents for
its three units. It records the verification strategy, test environment, and acceptance criteria
shared by every unit, and maps the system-level requirement to representative named test scenarios.
The detailed per-requirement scenarios live in the unit documents:

- CanvasNetRasterRenderer Unit Verification
- PngRenderer Unit Verification
- JpegRenderer Unit Verification

## Verification Approach

The CanvasNet renderers are verified by unit tests that render small layout trees and assert on the
produced bytes or sampled pixels. Format is checked by the encoded file signature (PNG signature,
JPEG Start-Of-Image marker). Drawing behavior is checked by sampling individual pixels from the
managed `Surface` returned by `PngRenderer.RenderToSurface`, including fill colors, stroke colors,
and the theme background. The PNG pixel tests exercise the shared `CanvasNetRasterRenderer` base
that both public formats share, so they also establish the drawing correctness of the JPEG
renderer.

## Test Environment

- **Framework**: xUnit v4 running under the .NET SDK (`net8.0`, `net9.0`, `net10.0`).
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Mocking**: none required; renderers are pure and deterministic.
- **Isolation**: each test renders into its own `MemoryStream` or disposable in-memory `Surface`.
- **Test project**: `DemaConsulting.Rendering.CanvasNet.Tests` (`PngRendererTests.cs`,
  `PngRendererPortedTests.cs`, `PngEndMarkerTests.cs`, `CanvasNetPortAndContentInsetTests.cs`,
  `CanvasNetFormatRendererTests.cs`, `CanvasNetTypefacesTests.cs`).

## Acceptance Criteria

A verification run passes when every scenario in this system document and in the three unit
documents passes without error or unexpected exception. Any wrong encoded signature, media type,
file extension, pixel color, marker geometry, or unexpected exception constitutes a failure.

## Test Scenarios

The system requirement is satisfied through the unit scenarios documented in the per-unit
verification files; the representative system-level scenarios are:

- **`Rendering-CanvasNet-RenderRasterImage`**: Render_SingleBox_ProducesPngSignature,
  JpegRenderer_Render_ProducesJpegSignature
