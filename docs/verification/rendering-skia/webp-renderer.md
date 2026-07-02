# WebpRenderer Unit Verification

Part of the Rendering.Skia Verification.

This document describes the verification design for the `WebpRenderer` unit of the
`DemaConsulting.Rendering.Skia` system. It maps every WebpRenderer unit requirement to at least one
named test scenario so a reviewer can confirm coverage without reading the test code. The verification
strategy, test environment, and acceptance criteria are described in the
system verification document; the test project is
`DemaConsulting.Rendering.Skia.Tests` (`SkiaFormatRendererTests.cs`).

## WebpRenderer Unit Scenarios

### WEBP output has the expected container header and metadata

Test `WebpRenderer_Render_ProducesWebpContainerHeader` renders a sample layout and asserts that output
is a RIFF/WEBP container and that the renderer reports the `image/webp` media type and `.webp` file
extension.

**Covers**: `Rendering-Skia-WebpRenderer-EmitsWebp`.

## Requirements Coverage

- **`Rendering-Skia-WebpRenderer-EmitsWebp`**: WebpRenderer_Render_ProducesWebpContainerHeader
