## CanvasNetRasterRenderer Unit Verification

Part of the Rendering.CanvasNet Verification.

This document describes the verification design for the `CanvasNetRasterRenderer` unit of the
`DemaConsulting.Rendering.CanvasNet` system. It maps every CanvasNetRasterRenderer unit
requirement to at least one named test scenario so a reviewer can confirm coverage without reading
the test code.

### CanvasNetRasterRenderer Verification Approach

Because `CanvasNetRasterRenderer` is `abstract`, it is exercised indirectly through the concrete
PNG render path in `DemaConsulting.Rendering.CanvasNet.Tests` (`PngRendererPortedTests.cs`,
`PngEndMarkerTests.cs`, `CanvasNetPortAndContentInsetTests.cs`, `CanvasNetTypefacesTests.cs`, and
`CanvasNetNotationPrimitiveTests.cs`).
Each test renders a small placed `LayoutTree` into a `MemoryStream` or a disposable `Surface` and
either inspects specific pixel colors or asserts on geometric properties of the rendered output.
Using `PngRenderer.RenderToSurface` for pixel sampling is deliberate: it avoids any encode/decode
round trip and samples the exact pixels the shared rasterizer produced. No dependencies are mocked;
`RenderOptions`, `Theme`, `NotationMetrics`, `BoxMetrics`, and `ConnectorLabelPlacer` are supplied
as real instances (typically from `Themes.Light` / `Themes.Dark`).

Coverage is organized around five concerns:

1. Drawing of every supported `LayoutTree` node kind (boxes, lines, ports, badges, lifelines,
   activations, bands, labels, deeply nested boxes).
2. Theme-driven fill selection from `Theme.DepthFillColors` and `Theme.BackgroundColor`.
3. Connector end-marker geometry derived from `NotationMetrics`.
4. Robust handling of the degenerate empty-tree case (minimum one-by-one surface).
5. The SysML notation primitives (dotted-triangle and circled-plus markers, dashed outlines, text
   styles, centered titles, folder name in tab, corner icons, thick-bar and crossed-circle badges,
   lifeline headers, and obstacle-aware labels), checked by pixel-level ink assertions.

### CanvasNetRasterRenderer Test Environment

- **Framework**: xUnit v4.
- **Target frameworks**: `net8.0`, `net9.0`, `net10.0`, `net11.0`.
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

#### Theme colors drive fills

Tests `PngRenderer_Render_SingleBox_FillColorMatchesTheme`,
`PngRenderer_Render_SingleBox_DepthOneUsesSecondColor`, and
`PngRenderer_Render_SingleGrid_HeaderFillMatchesTheme` render boxes and grids and assert that fill
pixels equal the theme depth-palette color selected by nesting depth.

**Covers**: `Rendering-CanvasNet-CanvasNetRasterRenderer-ThemeColors`.

#### End markers match notation metrics

Tests `FilledArrow_AlongLength_MatchesNotationMetrics`,
`FilledArrow_BaseWidth_MatchesNotationMetrics`, `OpenChevron_HasFewerInkPixelsThanClosedTriangle`,
and `PngRenderer_Render_DrawArrowhead_OpenWithCrossbar_ProducesNonEmptyOutput` assert that
rendered end-marker geometry derives from the shared notation metrics, drawn at the shared marker scale
(`NotationMetrics.MarkerScale`, 1.5 at the default theme: the filled arrow measures about 15 px along and
10.5 px across), and that distinct marker styles produce distinguishable output. The dotted-triangle and
circled-plus tests below place their expected ink at the notation positions scaled by the same factor.

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

#### Dotted-triangle and circled-plus markers, including at a source end

`PngTwoDots_InkAtBothDotCenters_TriangleTipUnchanged` renders a line with the two-dots target marker and
a plain hollow-triangle line, and asserts there is ink at every `NotationMetrics.DotCenters` position in the
dotted render (and none in the plain one) and that the rightmost ink column, the triangle tip, is identical
in both. `PngFourDots_InkAtAllFourDotCenters` asserts ink at all four centers of the two-by-two grid.
`PngCircledPlus_RingAndArmsHaveInkAndInteriorIsEmpty` asserts ink at the top and bottom of the ring, on the
vertical arm, and where the circle's far edge meets the endpoint, and background pixels inside the circle
off the arms.

`PngCircledPlus_AtSourceEnd_SitsOnLineSideOfStartPoint` renders the circled plus at the source end and
asserts ink on the ring on the line side of the start point and no ink at all beyond the start point.
`PngTwoDots_AtSourceEnd_DotsFollowTriangleBaseAlongLine` renders the two-dots marker at the source end and
asserts ink at every dot center mirrored along the line. Together they verify the same outward placement
that the SVG renderer reaches with its mirrored start variants.

**Covers**: `Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkerDottedTriangles`,
`Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkerCircledPlus`,
`Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkerSourceEnd`.

#### End markers have the SVG marker's size

Theory `EndMarkerParity_TargetEnd_PngFootprintMatchesSvgGeometry` and theory
`EndMarkerParity_SourceEnd_PngFootprintMatchesMirroredSvgGeometry` (in the gallery test project, the only one
that references both renderers) render, for every `EndMarkerStyle` other than `None`, a horizontal line with the
marker at its target end and at its source end through both renderers. The SVG marker definition is read back
from the rendered document and its geometry (points, circles and bars, including half the marker stroke,
clipped to the marker box as a browser does) is converted to pixels with the line stroke width; the PNG
footprint is the bounding box of the pixels that differ from the same line without a marker. The test asserts
that the along-line and across-line extents agree within 1.75 px (anti-aliasing plus the clip). Fact
`EndMarkerParity_ThickerStrokeTheme_FootprintsStillMatch` repeats this for the two-dots marker under a theme
with stroke width 2.5, so a renderer that ignored the theme stroke width would fail. At the previous raster
scale (the notation size) a 10 px marker measured 10 px against 15 px in SVG and the test fails by 5 px.

**Covers**: `Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkerSvgParity`.

#### Rounded corner clamps to the rendered marker length

`PngLineCornerClamp_UsesRenderedMarkerLength` renders a three-point line whose final leg is two pixels longer than
the rendered along-line length of an open-chevron target marker, under a theme with corner radius 12. The corner
radius is clamped to 2, so the test asserts ink at the original bend. The same waypoints without markers are
rounded with the full available radius (half the leg), and the test asserts no ink at the bend, which shows that
the clamp rather than the geometry produces the sharper corner. Disabling the clamp makes the test fail.

**Covers**: `Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkerCornerClamp`.

#### Redefinition crossbar sits behind the triangle base

`PngCrossbar_BarSitsBehindBaseWithGap` renders the redefinition marker at a target end and asserts ink at the
top and bottom of the bar at `NotationMetrics.CrossbarAlong` back from the endpoint (scaled by the marker scale),
a clear gap between the triangle base and the bar above and below the line, and no ink behind the bar except
the line itself. `PngCrossbar_AtSourceEnd_BarFollowsBaseAlongLine` asserts the same placement mirrored at a source
end, with nothing beyond the start point but the triangle apex.

**Covers**: `Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkerCrossbar`.

#### Box and band outlines and box text styles

Theory `PngBoxOutline_Dashed_HasGapsAlongTopEdgeAndSolidHasNone` (rectangle, rounded rectangle, folder, and
note) counts ink-to-background transitions along the top edge (for a folder, the body's top edge beside the
tab) and asserts at least five gaps for a dashed outline and none for a solid one. `PngBand_Dashed_HasGapsAlongTopEdge`
does the same for a dashed and a solid band.

`PngBoxName_RegularHasLessInkThanBold_DefaultIsBold` renders the same name regular, bold, and with no
appearance, and asserts the regular name has less ink than the bold one and that the default render has
exactly the bold ink count. `PngBoxName_Italic_SlantsRelativeToRegular` compares the horizontal ink centroid of
the upper and lower halves of the name and asserts the italic name slants more than the regular one.

**Covers**: `Rendering-CanvasNet-CanvasNetRasterRenderer-BoxOutlineStyle`,
`Rendering-CanvasNet-CanvasNetRasterRenderer-BandDashed`,
`Rendering-CanvasNet-CanvasNetRasterRenderer-BoxNameStyle`.

The resolved keyword style is applied by the same font-selection code as the name style, but no raster
scenario measures a box keyword's weight or slant; the keyword style requirement is verified for the SVG
renderer only (`Rendering-Svg-SvgRenderer-BoxKeywordStyle`).

#### Compartment title, folder name in tab, and corner icon

`PngCompartmentTitle_Centered_InkCenteredInContentArea` asserts a centered title's ink centroid lies within
six pixels of the box center while a legacy title's centroid is in the left part of the box.
`PngFolderNameInTab_InkInTabAndNoneInBodyBand` asserts that with the tab placement there is ink inside the
tab and none in the body's title band, whereas the default placement inks the body band and leaves the tab
empty. `PngCornerIcon_SendHasLeftEdgeAcceptHasNotch` asserts the send icon's left edge is inked at its
mid-height, the accept icon's left edge is not (the notch), the notch tip is inked, and a box with no icon
has no ink there.

**Covers**: `Rendering-CanvasNet-CanvasNetRasterRenderer-CompartmentTitleStyle`,
`Rendering-CanvasNet-CanvasNetRasterRenderer-FolderTitleInTab`,
`Rendering-CanvasNet-CanvasNetRasterRenderer-BoxCornerIcon`.

#### Thick-bar and crossed-circle badges, lifeline headers, and label avoidance

`PngBadgeThickHorizontalBar_FillsSizeByThickness` asserts the default bar is inked across its size and
default thickness and not beyond, and that an explicit thickness widens it.
`PngBadgeThickVerticalBar_FillsThicknessBySize` asserts the same for the vertical bar.
`PngBadgeCrossedCircle_RingAndDiagonalArmsHaveInk` asserts ink on the ring, at the center, and along the
diagonals, with a background interior between the arms.

`PngLifeline_RoundedHeader_CutsCornerAndSharpDoesNot` asserts the square corner pixel is empty for a rounded
header and inked for a sharp one. `PngLifeline_Keyword_AddsTextRowAboveLabel` counts text bands inside the
header and asserts two with a keyword and one without. `PngLifeline_DashedHeaderAppearance_HasGapsAlongTopEdge`
asserts gaps along a dashed header's top edge.

`PngLineLabel_MidpointOnLeafBox_DoesNotEraseBoxBorder` renders a labeled line whose midpoint lies on a small
leaf box and asserts the box's top border is still inked, i.e. the label's opaque backplate was placed
clear of the box.

**Covers**: `Rendering-CanvasNet-CanvasNetRasterRenderer-BadgeThickBar`,
`Rendering-CanvasNet-CanvasNetRasterRenderer-BadgeCrossedCircle`,
`Rendering-CanvasNet-CanvasNetRasterRenderer-LifelineRoundedHeader`,
`Rendering-CanvasNet-CanvasNetRasterRenderer-LabelsAvoidBoxes`.

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
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-ThemeColors`**:
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
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkerDottedTriangles`**:
  PngTwoDots_InkAtBothDotCenters_TriangleTipUnchanged,
  PngFourDots_InkAtAllFourDotCenters
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkerCircledPlus`**:
  PngCircledPlus_RingAndArmsHaveInkAndInteriorIsEmpty
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkerSvgParity`**:
  EndMarkerParity_TargetEnd_PngFootprintMatchesSvgGeometry,
  EndMarkerParity_SourceEnd_PngFootprintMatchesMirroredSvgGeometry,
  EndMarkerParity_ThickerStrokeTheme_FootprintsStillMatch
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkerCrossbar`**:
  PngCrossbar_BarSitsBehindBaseWithGap, PngCrossbar_AtSourceEnd_BarFollowsBaseAlongLine
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkerCornerClamp`**:
  PngLineCornerClamp_UsesRenderedMarkerLength
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-EndMarkerSourceEnd`**:
  PngCircledPlus_AtSourceEnd_SitsOnLineSideOfStartPoint,
  PngTwoDots_AtSourceEnd_DotsFollowTriangleBaseAlongLine
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-BoxOutlineStyle`**:
  PngBoxOutline_Dashed_HasGapsAlongTopEdgeAndSolidHasNone
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-BoxNameStyle`**:
  PngBoxName_RegularHasLessInkThanBold_DefaultIsBold,
  PngBoxName_Italic_SlantsRelativeToRegular
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-CompartmentTitleStyle`**:
  PngCompartmentTitle_Centered_InkCenteredInContentArea
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-FolderTitleInTab`**:
  PngFolderNameInTab_InkInTabAndNoneInBodyBand
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-BoxCornerIcon`**:
  PngCornerIcon_SendHasLeftEdgeAcceptHasNotch
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-BadgeThickBar`**:
  PngBadgeThickHorizontalBar_FillsSizeByThickness,
  PngBadgeThickVerticalBar_FillsThicknessBySize
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-BadgeCrossedCircle`**:
  PngBadgeCrossedCircle_RingAndDiagonalArmsHaveInk
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-BandDashed`**:
  PngBand_Dashed_HasGapsAlongTopEdge
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-LifelineRoundedHeader`**:
  PngLifeline_RoundedHeader_CutsCornerAndSharpDoesNot,
  PngLifeline_Keyword_AddsTextRowAboveLabel,
  PngLifeline_DashedHeaderAppearance_HasGapsAlongTopEdge
- **`Rendering-CanvasNet-CanvasNetRasterRenderer-LabelsAvoidBoxes`**:
  PngLineLabel_MidpointOnLeafBox_DoesNotEraseBoxBorder
