# Rendering.Skia Verification Design

This document describes the verification strategy for the Rendering.Skia system and its units.

## Verification Strategy

The Skia renderers are verified by unit tests that render small layout trees and assert on the
produced bytes. Format is checked by the encoded file signature (PNG signature, JPEG Start-Of-Image
marker, WEBP RIFF container header). Drawing behaviour is checked by inspecting individual pixels of
the decoded PNG bitmap (fill colours, stroke colours, background), which exercises the shared
`SkiaRasterRenderer` base that all three formats share. Because the base is shared, the PNG pixel
tests also establish the drawing correctness of the JPEG and WEBP renderers.

Tests reside in the `DemaConsulting.Rendering.Skia.Tests` project.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK (net8.0, net9.0, net10.0)
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; renderers are pure and deterministic
- **Isolation**: Each test renders into its own `MemoryStream`

## Unit-Level Test Scenarios

### Rendering-Skia-SkiaRasterRenderer-DrawsLayoutTree: Draws all node kinds

**Tests**: `PngRenderer_Render_SingleBox_ProducesNonEmptyOutput`,
`PngRenderer_Render_BackgroundIsWhite`, `PngRenderer_Render_SingleLine_PixelOnLineIsStrokeColor`,
`PngRenderer_Render_SinglePort_CenterPixelIsStrokeColor`,
`PngRenderer_Render_SingleBadge_FilledCircle_CenterPixelIsStrokeColor`,
`PngRenderer_Render_SingleLifeline_StemPixelIsStrokeColor`,
`PngRenderer_Render_SingleActivation_CenterPixelIsWhite`,
`PngRenderer_Render_DeeplyNestedBoxes_DoesNotStackOverflow`,
`PngRenderer_Render_LabelWithXmlSpecialCharacters_ProducesValidPng`

Render layout trees containing each node kind and assert that representative pixels take the
expected colour, that nesting does not overflow the stack, and that special label text still
produces a valid image on the white background.

### Rendering-Skia-SkiaRasterRenderer-ThemeColours: Fills derive from theme

**Tests**: `PngRenderer_Render_SingleBox_FillColorMatchesTheme`,
`PngRenderer_Render_SingleBox_DepthOneUsesSecondColor`,
`PngRenderer_Render_SingleGrid_HeaderFillMatchesTheme`

Render boxes and grids and assert that fill pixels equal the theme depth-palette colour selected by
nesting depth.

### Rendering-Skia-SkiaRasterRenderer-EndMarkers: Markers match notation metrics

**Tests**: `FilledArrow_AlongLength_MatchesNotationMetrics`,
`FilledArrow_BaseWidth_MatchesNotationMetrics`,
`OpenChevron_HasFewerInkPixelsThanClosedTriangle`,
`PngRenderer_Render_DrawArrowhead_OpenWithCrossbar_ProducesNonEmptyOutput`

Assert that rendered end-marker geometry derives from the shared notation metrics and that distinct
marker styles produce distinguishable output.

### Rendering-Skia-SkiaRasterRenderer-EmptyTree: Empty tree renders

**Test**: `PngRenderer_Render_EmptyTree_WritesPngSignature`

Render an empty layout tree and assert that a valid image (minimum one by one pixel) is produced.

### Rendering-Skia-PngRenderer-EmitsPng: PNG output

**Tests**: `Render_SingleBox_ProducesPngSignature`, `PngRenderer_Render_EmptyTree_WritesPngSignature`,
`PngRenderer_FileExtensions_ContainsDefault`

Assert that output begins with the PNG signature and that the renderer reports the `image/png` media
type and a `.png` extension that is included in its advertised extensions.

### Rendering-Skia-JpegRenderer-EmitsJpeg: JPEG output

**Test**: `JpegRenderer_Render_ProducesJpegSignature`

Assert that output begins with the JPEG Start-Of-Image marker and that the renderer reports the
`image/jpeg` media type and the `.jpg`/`.jpeg` extensions.

### Rendering-Skia-WebpRenderer-EmitsWebp: WEBP output

**Test**: `WebpRenderer_Render_ProducesWebpContainerHeader`

Assert that output is a RIFF/WEBP container and that the renderer reports the `image/webp` media
type and the `.webp` extension.

## Requirements Coverage

| Requirement ID | Test Scenario(s) |
| --- | --- |
| Rendering-Skia-RenderRasterImage | PNG, JPEG, and WEBP signature tests |
| Rendering-Skia-SkiaRasterRenderer-DrawsLayoutTree | Node-kind pixel tests |
| Rendering-Skia-SkiaRasterRenderer-ThemeColours | Theme fill-colour tests |
| Rendering-Skia-SkiaRasterRenderer-EndMarkers | End-marker geometry tests |
| Rendering-Skia-SkiaRasterRenderer-EmptyTree | Empty-tree signature test |
| Rendering-Skia-PngRenderer-EmitsPng | PNG signature and extension tests |
| Rendering-Skia-JpegRenderer-EmitsJpeg | JPEG signature test |
| Rendering-Skia-WebpRenderer-EmitsWebp | WEBP container-header test |
