## SkiaRasterRenderer Unit Verification

Part of the Rendering.Skia Verification.

This document describes the verification design for the `SkiaRasterRenderer` unit of the
`DemaConsulting.Rendering.Skia` system. It maps every SkiaRasterRenderer unit requirement to at least
one named test scenario so a reviewer can confirm coverage without reading the test code.

### SkiaRasterRenderer Verification Approach

Because `SkiaRasterRenderer` is `abstract`, it is exercised indirectly through its concrete
`PngRenderer` subclass in `DemaConsulting.Rendering.Skia.Tests` (`PngRendererPortedTests.cs` and
`PngEndMarkerTests.cs`). Each test renders a small placed `LayoutTree` into a `MemoryStream` and
either decodes the resulting PNG to inspect specific pixel colours or asserts on geometric
properties of the encoded output. Using PNG as the transport is deliberate: PNG is lossless so
individual pixels can be compared to theme colours without lossy-encoding error. No dependencies
are mocked; `RenderOptions`, `Theme`, `NotationMetrics`, `BoxMetrics`, and `ConnectorLabelPlacer`
are supplied as real instances (typically from `Themes.Light` / `Themes.Dark`).

Coverage is organized around four concerns:

1. Drawing of every supported `LayoutTree` node kind (boxes, lines, ports, badges, lifelines,
   activations, bands, labels, deeply nested boxes).
2. Theme-driven fill selection from `Theme.DepthFillColors` and `Theme.BackgroundColor`.
3. Connector end-marker geometry derived from `NotationMetrics`.
4. Robust handling of the degenerate empty-tree case (minimum one-by-one bitmap).

### SkiaRasterRenderer Test Environment

- **Framework**: xUnit v3.
- **Target frameworks**: `net8.0`, `net9.0`, `net10.0`.
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline (see
  _Rendering.Skia Verification_ for the system-level environment).
- **External services / files**: none. Rendering is deterministic and writes to a `MemoryStream`;
  pixel inspection uses `SKBitmap.Decode` on the in-memory PNG bytes.
- **Native assets**: the SkiaSharp platform-specific native asset packages provided by
  `DemaConsulting.Rendering.Skia`'s NuGet references must be available at test time; no other
  setup beyond `dotnet restore` is required.

### SkiaRasterRenderer Acceptance Criteria

A verification run passes when every scenario below executes without an unexpected exception,
each inspected pixel or geometric measurement equals its expected theme colour or notation-metric
value, and the encoded byte stream is a valid PNG. Any wrong pixel colour, wrong marker geometry,
stack overflow, or unexpected exception constitutes a failure.

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
