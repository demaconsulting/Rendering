## CanvasNetRasterRenderer Unit Verification

Part of the Rendering.CanvasNet Verification.

This document describes the verification design for the `CanvasNetRasterRenderer` unit of the
`DemaConsulting.Rendering.CanvasNet` system. It maps every CanvasNetRasterRenderer unit
requirement to at least one named test scenario so a reviewer can confirm coverage without reading
the test code.

### CanvasNetRasterRenderer Verification Approach

Because `CanvasNetRasterRenderer` is `abstract`, it is exercised indirectly through the concrete
PNG render path in `DemaConsulting.Rendering.CanvasNet.Tests` (`PngRendererPortedTests.cs`,
`PngEndMarkerTests.cs`, `CanvasNetPortAndContentInsetTests.cs`, and `CanvasNetTypefacesTests.cs`).
Each test renders a small placed `LayoutTree` into a `MemoryStream` or a disposable `Surface` and
either inspects specific pixel colors or asserts on geometric properties of the rendered output.
Using `PngRenderer.RenderToSurface` for pixel sampling is deliberate: it avoids any encode/decode
round trip and samples the exact pixels the shared rasterizer produced. No dependencies are mocked;
`RenderOptions`, `Theme`, `NotationMetrics`, `BoxMetrics`, and `ConnectorLabelPlacer` are supplied
as real instances (typically from `Themes.Light` / `Themes.Dark`).

Coverage is organized around four concerns:

1. Drawing of every supported `LayoutTree` node kind (boxes, lines, ports, badges, lifelines,
   activations, bands, labels, deeply nested boxes).
2. Theme-driven fill selection from `Theme.DepthFillColors` and `Theme.BackgroundColor`.
3. Connector end-marker geometry derived from `NotationMetrics`.
4. Robust handling of the degenerate empty-tree case (minimum one-by-one surface).

### CanvasNetRasterRenderer Test Environment

- **Framework**: xUnit v3.
- **Target frameworks**: `net8.0`, `net9.0`, `net10.0`.
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline (see
  *Rendering.CanvasNet Verification* for the system-level environment).
- **External services / files**: none. Rendering is deterministic and writes to a `MemoryStream`
  or in-memory `Surface`.
- **Runtime prerequisites**: none beyond `dotnet restore`; CanvasNet is a pure managed dependency
  with no platform-specific native assets.

### CanvasNetRasterRenderer Acceptance Criteria

A verification run passes when every scenario below executes without an unexpected exception, each
inspected pixel or geometric measurement equals its expected theme color or notation-metric value,
and encoded output remains valid where encoding is part of the scenario. Any wrong pixel color,
wrong marker geometry, stack overflow, or unexpected exception constitutes a failure.

### CanvasNetRasterRenderer Unit Scenarios

#### Draws all layout-tree node kinds

Tests `PngRenderer_Render_SingleBox_ProducesNonEmptyOutput`,
`PngRenderer_Render_BackgroundIsThemeBackground`,
`PngRenderer_Render_SingleLine_PixelOnLineIsStrokeColor`,
`PngRenderer_Render_SinglePort_CenterPixelIsStrokeColor`,
`PngRenderer_Render_SingleBadge_FilledCircle_CenterPixelIsStrokeColor`,
`PngRenderer_Render_SingleLifeline_StemPixelIsStrokeColor`,
`PngRenderer_Render_SingleActivation_CenterPixelIsWhite`,
`PngRenderer_Render_SingleBand_BorderIsStrokeColor`,
`PngRenderer_Render_DeeplyNestedBoxes_DoesNotStackOverflow`, and
`PngRenderer_Render_LabelWithXmlSpecialCharacters_ProducesValidPng` render layout trees containing
supported node kinds and assert that representative pixels take the expected color or that
rendering produces a valid PNG without overflowing the stack.

The background contract is that the surface is initialized from
`RenderOptions.Theme.BackgroundColor`. The `PngRenderer_Render_BackgroundIsThemeBackground` theory
renders an empty tree with both the light and dark themes and asserts the sampled pixel equals each
theme's background color; the dark theme, whose background is not white, proves the fill is
genuinely theme-driven rather than a hardcoded white.

**Covers**: `Rendering-CanvasNet-CanvasNetRasterRenderer-DrawsLayoutTree`.

#### Theme colours drive fills

Tests `PngRenderer_Render_SingleBox_FillColorMatchesTheme`,
`PngRenderer_Render_SingleBox_DepthOneUsesSecondColor`, and
`PngRenderer_Render_SingleGrid_HeaderFillMatchesTheme` render boxes and grids and assert that fill
pixels equal the theme depth-palette color selected by nesting depth.

**Covers**: `Rendering-CanvasNet-CanvasNetRasterRenderer-ThemeColours`.

#### End markers match notation metrics

Tests `FilledArrow_AlongLength_MatchesNotationMetrics`,
`FilledArrow_BaseWidth_MatchesNotationMetrics`, `OpenChevron_HasFewerInkPixelsThanClosedTriangle`,
and `PngRenderer_Render_DrawArrowhead_OpenWithCrossbar_ProducesNonEmptyOutput` assert that
rendered end-marker geometry derives from the shared notation metrics and that distinct marker
styles produce distinguishable output.

**Covers**: `Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkers`.

#### Empty tree renders as a valid image

Test `PngRenderer_Render_EmptyTree_WritesPngSignature` renders an empty layout tree and asserts
that a valid image with the PNG signature is produced, proving the minimum one-by-one surface path.

**Covers**: `Rendering-CanvasNet-CanvasNetRasterRenderer-EmptyTree`.

#### Port glyph/label render on every side, and a reserved inset shifts compartment content

Theory test `PngRenderer_RenderPort_AnySide_ProducesNonBackgroundPixels` (over `PortSide.Left`,
`Right`, `Top`, `Bottom`) renders a single port on each side of a box and asserts the port's
expected pixel region contains at least one non-background pixel, confirming the glyph and label
draw on every side. `PngRenderer_RenderBoxCompartments_ContentInsetLeft_ShiftsRowContentRight`
renders the same compartment-row scenario twice, once with a positive `ContentInsetLeft` and once
with zero, and asserts the inset case's row content is shifted right relative to the zero-inset
case, confirming the raster renderer reads the reserved margin exactly as the SVG renderer does.

**Covers**: `Rendering-CanvasNet-CanvasNetRasterRenderer-PortLabelPlacement`,
`Rendering-CanvasNet-CanvasNetRasterRenderer-ContentInsetLeft`.

#### Port label squeeze, title geometric centering, port outline, and label-aware surface growth

`PngRenderer_RenderPort_LongLabelWithMaxLabelWidth_SqueezesToFit` renders the same deliberately
long port label twice — once with a finite `MaxLabelWidth` and once unconstrained — and asserts the
constrained render's rightmost non-background pixel falls short of the unconstrained render's,
confirming the squeeze genuinely compresses the label rather than being a no-op.
`PngRenderer_RenderBoxTitle_AsymmetricContentInsets_StaysAtGeometricCenter` renders a box whose
`ContentInsetLeft` differs from `ContentInsetRight` and asserts the title's leftmost rendered pixel
is identical whether or not the asymmetric insets are present, confirming the title remains at the
box's full geometric center. `PngRenderer_RenderPort_Rect_HasStrokeDistinctFromFill` renders a
single port at a large scale and asserts a pixel sampled just inside the glyph's edge (within the
outline's stroke band) differs from the fill color sampled at the glyph's exact center and matches
`Theme.BackgroundColor`, confirming the port glyph remains visually distinguishable from a
solid-filled arrowhead marker that might land on or near the same box edge.
`PngRenderer_Render_ManyCollidingConnectorLabels_BitmapGrowsToFitAllLabels` renders three or more
parallel labeled connectors whose midpoint labels collide and get nudged, and asserts the allocated
surface dimensions grow enough that every label's rendered pixels remain within bounds rather than
being silently clipped.

**Covers**: `Rendering-CanvasNet-CanvasNetRasterRenderer-PortLabelSqueeze`,
`Rendering-CanvasNet-CanvasNetRasterRenderer-TitleCentersOnBoxWidth`,
`Rendering-CanvasNet-CanvasNetRasterRenderer-PortGlyphOutline`,
`Rendering-CanvasNet-CanvasNetRasterRenderer-CanvasGrowsForLabels`.

#### Shared typeface resolution is stable and distinct per variant

Test `CanvasNetTypefaces_Resolve_ReturnsStableDistinctTypefacesPerVariant` resolves each of the
four bold/italic combinations twice and asserts the same combination returns the same
`TrueTypeFont` instance both times (stability), while different combinations return different
instances (distinctness) — confirming every drawing call site in this unit measures and draws
against the exact same lazily loaded typeface objects.

**Covers**: `Rendering-CanvasNet-CanvasNetRasterRenderer-SharedTypefaces`.

### Requirements Coverage

- **`Rendering-CanvasNet-CanvasNetRasterRenderer-DrawsLayoutTree`**:
  PngRenderer_Render_SingleBox_ProducesNonEmptyOutput,
  PngRenderer_Render_BackgroundIsThemeBackground,
  PngRenderer_Render_SingleLine_PixelOnLineIsStrokeColor,
  PngRenderer_Render_SinglePort_CenterPixelIsStrokeColor,
  PngRenderer_Render_SingleBadge_FilledCircle_CenterPixelIsStrokeColor,
  PngRenderer_Render_SingleLifeline_StemPixelIsStrokeColor,
  PngRenderer_Render_SingleActivation_CenterPixelIsWhite,
  PngRenderer_Render_SingleBand_BorderIsStrokeColor,
  PngRenderer_Render_DeeplyNestedBoxes_DoesNotStackOverflow,
  PngRenderer_Render_LabelWithXmlSpecialCharacters_ProducesValidPng
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-ThemeColours`**:
  PngRenderer_Render_SingleBox_FillColorMatchesTheme,
  PngRenderer_Render_SingleBox_DepthOneUsesSecondColor,
  PngRenderer_Render_SingleGrid_HeaderFillMatchesTheme
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkers`**:
  FilledArrow_AlongLength_MatchesNotationMetrics,
  FilledArrow_BaseWidth_MatchesNotationMetrics,
  OpenChevron_HasFewerInkPixelsThanClosedTriangle,
  PngRenderer_Render_DrawArrowhead_OpenWithCrossbar_ProducesNonEmptyOutput
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-EmptyTree`**:
  PngRenderer_Render_EmptyTree_WritesPngSignature
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-PortLabelPlacement`**:
  PngRenderer_RenderPort_AnySide_ProducesNonBackgroundPixels
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-ContentInsetLeft`**:
  PngRenderer_RenderBoxCompartments_ContentInsetLeft_ShiftsRowContentRight
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-PortLabelSqueeze`**:
  PngRenderer_RenderPort_LongLabelWithMaxLabelWidth_SqueezesToFit
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-TitleCentersOnBoxWidth`**:
  PngRenderer_RenderBoxTitle_AsymmetricContentInsets_StaysAtGeometricCenter
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-PortGlyphOutline`**:
  PngRenderer_RenderPort_Rect_HasStrokeDistinctFromFill
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-CanvasGrowsForLabels`**:
  PngRenderer_Render_ManyCollidingConnectorLabels_BitmapGrowsToFitAllLabels
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-SharedTypefaces`**:
  CanvasNetTypefaces_Resolve_ReturnsStableDistinctTypefacesPerVariant
