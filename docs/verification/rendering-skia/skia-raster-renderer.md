## SkiaRasterRenderer Unit Verification

Part of the Rendering.Skia Verification.

This document describes the verification design for the `SkiaRasterRenderer` unit of the
`DemaConsulting.Rendering.Skia` system. It maps every SkiaRasterRenderer unit requirement to at least
one named test scenario so a reviewer can confirm coverage without reading the test code. The
verification strategy, test environment, and acceptance criteria are described in the
system verification document; the test project is
`DemaConsulting.Rendering.Skia.Tests` (`PngRendererPortedTests.cs`, `PngEndMarkerTests.cs`).

### SkiaRasterRenderer Unit Scenarios

#### Draws all layout-tree node kinds

Tests `PngRenderer_Render_SingleBox_ProducesNonEmptyOutput`, `PngRenderer_Render_BackgroundIsThemeBackground`,
`PngRenderer_Render_SingleLine_PixelOnLineIsStrokeColor`,
`PngRenderer_Render_SinglePort_CenterPixelIsStrokeColor`,
`PngRenderer_Render_SingleBadge_FilledCircle_CenterPixelIsStrokeColor`,
`PngRenderer_Render_SingleLifeline_StemPixelIsStrokeColor`,
`PngRenderer_Render_SingleActivation_CenterPixelIsWhite`,
`PngRenderer_Render_SingleBand_BorderIsStrokeColor`,
`PngRenderer_Render_DeeplyNestedBoxes_DoesNotStackOverflow`, and
`PngRenderer_Render_LabelWithXmlSpecialCharacters_ProducesValidPng` render layout trees containing the
supported node kinds and assert that representative pixels take the expected colour or that rendering
produces a valid PNG without overflowing the stack.

The background contract is that the bitmap is initialized from `RenderOptions.Theme.BackgroundColor`.
The `PngRenderer_Render_BackgroundIsThemeBackground` theory renders an empty tree with both the light
and dark themes and asserts the top-left pixel equals each theme's background color; the dark theme,
whose background is not white, proves the fill is genuinely theme-driven rather than a hardcoded white.

**Covers**: `Rendering-Skia-SkiaRasterRenderer-DrawsLayoutTree`.

#### Theme colours drive fills

Tests `PngRenderer_Render_SingleBox_FillColorMatchesTheme`,
`PngRenderer_Render_SingleBox_DepthOneUsesSecondColor`, and
`PngRenderer_Render_SingleGrid_HeaderFillMatchesTheme` render boxes and grids and assert that fill
pixels equal the theme depth-palette colour selected by nesting depth.

**Covers**: `Rendering-Skia-SkiaRasterRenderer-ThemeColours`.

#### End markers match notation metrics

Tests `FilledArrow_AlongLength_MatchesNotationMetrics`, `FilledArrow_BaseWidth_MatchesNotationMetrics`,
`OpenChevron_HasFewerInkPixelsThanClosedTriangle`, and
`PngRenderer_Render_DrawArrowhead_OpenWithCrossbar_ProducesNonEmptyOutput` assert that rendered
end-marker geometry derives from the shared notation metrics and that distinct marker styles produce
distinguishable output.

**Covers**: `Rendering-Skia-SkiaRasterRenderer-EndMarkers`.

#### Empty tree renders as a valid image

Test `PngRenderer_Render_EmptyTree_WritesPngSignature` renders an empty layout tree and asserts that a
valid image with the PNG signature is produced, proving the minimum one by one pixel bitmap path.

**Covers**: `Rendering-Skia-SkiaRasterRenderer-EmptyTree`.

### Requirements Coverage

- **`Rendering-Skia-SkiaRasterRenderer-DrawsLayoutTree`**:
  PngRenderer_Render_SingleBox_ProducesNonEmptyOutput, PngRenderer_Render_BackgroundIsThemeBackground,
  PngRenderer_Render_SingleLine_PixelOnLineIsStrokeColor,
  PngRenderer_Render_SinglePort_CenterPixelIsStrokeColor,
  PngRenderer_Render_SingleBadge_FilledCircle_CenterPixelIsStrokeColor,
  PngRenderer_Render_SingleLifeline_StemPixelIsStrokeColor,
  PngRenderer_Render_SingleActivation_CenterPixelIsWhite,
  PngRenderer_Render_SingleBand_BorderIsStrokeColor,
  PngRenderer_Render_DeeplyNestedBoxes_DoesNotStackOverflow,
  PngRenderer_Render_LabelWithXmlSpecialCharacters_ProducesValidPng
- **`Rendering-Skia-SkiaRasterRenderer-ThemeColours`**: PngRenderer_Render_SingleBox_FillColorMatchesTheme,
  PngRenderer_Render_SingleBox_DepthOneUsesSecondColor, PngRenderer_Render_SingleGrid_HeaderFillMatchesTheme
- **`Rendering-Skia-SkiaRasterRenderer-EndMarkers`**: FilledArrow_AlongLength_MatchesNotationMetrics,
  FilledArrow_BaseWidth_MatchesNotationMetrics, OpenChevron_HasFewerInkPixelsThanClosedTriangle,
  PngRenderer_Render_DrawArrowhead_OpenWithCrossbar_ProducesNonEmptyOutput
- **`Rendering-Skia-SkiaRasterRenderer-EmptyTree`**: PngRenderer_Render_EmptyTree_WritesPngSignature
