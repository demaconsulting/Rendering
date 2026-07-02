# PngRenderer Unit Verification

Part of the Rendering.Skia Verification.

This document describes the verification design for the `PngRenderer` unit of the
`DemaConsulting.Rendering.Skia` system. It maps every PngRenderer unit requirement to at least one
named test scenario so a reviewer can confirm coverage without reading the test code. The verification
strategy, test environment, and acceptance criteria are described in the
system verification document; the test project is
`DemaConsulting.Rendering.Skia.Tests` (`PngRendererTests.cs`, `PngRendererPortedTests.cs`,
`SkiaFormatRendererTests.cs`).

## PngRenderer Unit Scenarios

### PNG output has the expected signature and metadata

Tests `Render_SingleBox_ProducesPngSignature`, `PngRenderer_Render_EmptyTree_WritesPngSignature`, and
`PngRenderer_FileExtensions_ContainsDefault` render sample layouts and assert that output begins with
the PNG signature and that the renderer reports the `image/png` media type and a `.png` extension that
is included in its advertised extensions.

**Covers**: `Rendering-Skia-PngRenderer-EmitsPng`.

## Requirements Coverage

- **`Rendering-Skia-PngRenderer-EmitsPng`**: Render_SingleBox_ProducesPngSignature,
  PngRenderer_Render_EmptyTree_WritesPngSignature, PngRenderer_FileExtensions_ContainsDefault
