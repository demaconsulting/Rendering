# JpegRenderer Unit Verification

Part of the [Rendering.Skia Verification](rendering-skia.md).

This document describes the verification design for the `JpegRenderer` unit of the
`DemaConsulting.Rendering.Skia` system. It maps every JpegRenderer unit requirement to at least one
named test scenario so a reviewer can confirm coverage without reading the test code. The verification
strategy, test environment, and acceptance criteria are described in the
[system verification document](rendering-skia.md); the test project is
`DemaConsulting.Rendering.Skia.Tests` (`SkiaFormatRendererTests.cs`).

## JpegRenderer Unit Scenarios

### JPEG output has the expected signature and metadata

Test `JpegRenderer_Render_ProducesJpegSignature` renders a sample layout and asserts that output begins
with the JPEG Start-Of-Image marker and that the renderer reports the `image/jpeg` media type, the
`.jpg` default extension, and the `.jpeg` alternate extension.

**Covers**: `Rendering-Skia-JpegRenderer-EmitsJpeg`.

## Requirements Coverage

- **`Rendering-Skia-JpegRenderer-EmitsJpeg`**: JpegRenderer_Render_ProducesJpegSignature
