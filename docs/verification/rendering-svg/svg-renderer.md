## SvgRenderer Unit Verification

Part of the Rendering.Svg Verification.

This document describes the verification design for the `SvgRenderer` unit of the
`DemaConsulting.Rendering.Svg` system. It maps every SvgRenderer unit requirement to at least one
named test scenario so a reviewer can confirm coverage without reading the test code. The verification
strategy, test environment, and acceptance criteria are described in the
system verification document; the test project is
`DemaConsulting.Rendering.Svg.Tests`.

### Verification Approach

Unit verification uses xUnit v4 tests in `DemaConsulting.Rendering.Svg.Tests` that construct
concrete `LayoutTree`, `RenderOptions`, and `MemoryStream` instances and invoke
`SvgRenderer.Render` directly. Because `SvgRenderer` is pure and stateless, no mocking or
stubbing is used: the real `Themes.Light` theme, the real `NotationMetrics` and
`ConnectorLabelPlacer` helpers from `DemaConsulting.Rendering.Abstractions`, and the real
`LayoutTree` and `LayoutNode` records from `DemaConsulting.Rendering` are exercised end-to-end.
Each test decodes the emitted UTF-8 bytes and asserts on the SVG text, element presence,
attribute values, well-formed XML, and geometric parity with `NotationMetrics`.

### Test Environment

- **Framework**: xUnit v4 on the .NET SDK, run against the `net8.0`, `net9.0`, `net10.0`, and `net11.0`
  target frameworks.
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Dependencies**: none — no external services, databases, files, or network access are used;
  tests write to in-memory `MemoryStream` instances only.
- **Isolation**: each test constructs its own `SvgRenderer`, layout tree, render options, and
  output stream, so tests are independent and order-agnostic.

### Acceptance Criteria

A unit verification run passes when every named scenario listed below completes without
unexpected exception and every assertion holds. A failure is any missing SVG root element,
wrong renderer metadata (`MediaType`, `DefaultExtension`), wrong emitted SVG element or
attribute, malformed XML escaping, or marker geometry that does not match `NotationMetrics`. For the
notation primitives, a failure is also an on-demand marker definition that is missing, duplicated, or
written when unused; a source-end marker that does not reference its mirrored `line-start-…` variant; a
missing or spurious `stroke-dasharray`; a `font-weight`/`font-style` that does not match the resolved
style (including a default style whose markup differs from the legacy markup); or a title, tab name,
corner icon, badge, band, lifeline, or connector label positioned differently from the documented rule.
Requirements coverage is enforced by the mapping in the *Requirements Coverage* section below:
every `Rendering-Svg-SvgRenderer-*` requirement must map to at least one named scenario, and
every mapped scenario must exist in `DemaConsulting.Rendering.Svg.Tests` and pass.

### Test Scenarios

The named scenarios that satisfy each unit requirement are enumerated below under
*SvgRenderer Unit Scenarios*, grouped by rendering concern (renderer contract and metadata,
SVG document root and empty tree, box rectangle and compartments, label text and styling and
escaping, connector path and corners and dash pattern and label, additional node kinds, connector end
markers, and the SysML notation primitives: dotted-triangle and circled-plus markers with on-demand
definitions and mirrored start variants, box outline and text styles, folder name in tab, corner icons,
thick-bar and crossed-circle badges, dashed bands, lifeline headers, and obstacle-aware connector labels).
Every `Rendering-Svg-SvgRenderer-*` requirement is traced to at least
one named scenario in the *Requirements Coverage* table at the end of this document.

### SvgRenderer Unit Scenarios

#### Renderer contract and metadata

Test `SvgRenderer_Render_SingleBox_ProducesSvgDocument` renders a single box, asserts that SVG markup
is produced, and checks `MediaType` is `image/svg+xml` and `DefaultExtension` is `.svg`.

**Covers**: `Rendering-Svg-SvgRenderer-ImplementsIRenderer`,
`Rendering-Svg-SvgRenderer-MediaType`, `Rendering-Svg-SvgRenderer-DefaultExtension`.

#### SVG document root and empty tree

Test `SvgRenderer_Render_EmptyTree_ProducesSvgDocument` renders an empty `LayoutTree`, asserts the
output stream is non-empty, and checks that the decoded text contains `<svg` and `</svg>`.

**Covers**: `Rendering-Svg-SvgRenderer-RenderDocument`,
`Rendering-Svg-SvgRenderer-RenderEmptyTree`.

#### Box rectangle, rounded corners, and compartments

Tests `SvgRenderer_Render_SingleBox_ProducesRectElement`,
`SvgRenderer_Render_BoxRoundedRectangle_ProducesRxAttribute`, and
`SvgRenderer_Render_BoxWithCompartment_ProducesLineAndText` render boxes and assert that a `<rect>`
element, an `rx` rounded-corner attribute, and compartment divider/text content are present.

**Covers**: `Rendering-Svg-SvgRenderer-RenderBox`,
`Rendering-Svg-SvgRenderer-RenderBoxRoundedCorners`,
`Rendering-Svg-SvgRenderer-RenderBoxCompartments`.

#### Label text, styling, and escaping

Tests `SvgRenderer_Render_SingleLabel_ProducesTextElement`,
`SvgRenderer_Render_LabelWithBold_ProducesBoldAttribute`,
`SvgRenderer_Render_LabelWithItalic_ProducesItalicAttribute`, and
`SvgRenderer_Render_LabelWithXmlSpecialCharacters_ProducesWellFormedEscapedSvg` render labels and
assert on `<text>`, bold and italic attributes, escaped XML entities, and that the output remains
well-formed XML.

**Covers**: `Rendering-Svg-SvgRenderer-RenderLabel`,
`Rendering-Svg-SvgRenderer-RenderLabelBold`, `Rendering-Svg-SvgRenderer-RenderLabelItalic`,
`Rendering-Svg-SvgRenderer-RenderLabelEscaping`.

#### Connector path, corners, dash pattern, and label

Tests `SvgRenderer_Render_SingleLine_ProducesPathElement`,
`SvgRenderer_Render_SingleLine_WithCornerRadius_ProducesArcInPath`,
`SvgRenderer_Render_SingleLine_CollinearInteriorWaypoint_ProducesNoArcInPath`,
`SvgRenderer_Render_SingleLine_Dashed_ProducesDashArray`, and
`SvgRenderer_Render_LineWithMidpointLabel_ProducesTextElement` render connector lines and assert on
`<path>`, an arc command, the absence of an arc command at a collinear (non-corner) interior waypoint,
`stroke-dasharray`, and the connector label text.

The midpoint-label scenario asserts that a `<text>` element containing the label appears in the final
SVG. The design places connector labels in a final pass using `ConnectorLabelPlacer`; this scenario
does not assert the exact placed coordinates or collision-avoidance behavior.

**Covers**: `Rendering-Svg-SvgRenderer-RenderLine`,
`Rendering-Svg-SvgRenderer-RenderLineRoundedCorners`,
`Rendering-Svg-SvgRenderer-RenderLineDashed`,
`Rendering-Svg-SvgRenderer-RenderLineMidpointLabel`.

#### Additional node kinds

Tests `SvgRenderer_Render_SinglePort_ProducesRect`,
`SvgRenderer_Render_SingleBadge_FilledCircle_ProducesCircle`,
`SvgRenderer_Render_SingleBadge_Bullseye_ProducesConcentricCircles`,
`SvgRenderer_Render_SingleBadge_Diamond_ProducesPolygon`,
`SvgRenderer_Render_SingleBadge_HorizontalBar_ProducesLine`,
`SvgRenderer_Render_SingleBadge_VerticalBar_ProducesLine`,
`SvgRenderer_Render_SingleBand_ProducesRect`,
`SvgRenderer_Render_SingleLifeline_ProducesRectAndLine`,
`SvgRenderer_Render_SingleActivation_ProducesRect`, and
`SvgRenderer_Render_SingleGrid_ProducesRects` assert that ports, filled-circle badges, bands,
lifelines, activations, and grids emit their expected SVG element types.

**Covers**: `Rendering-Svg-SvgRenderer-RenderNodeKinds`,
`Rendering-Svg-SvgRenderer-RenderBadge`,
`Rendering-Svg-SvgRenderer-BadgeBullseye`,
`Rendering-Svg-SvgRenderer-BadgeDiamond`,
`Rendering-Svg-SvgRenderer-BadgeHorizontalBar`,
`Rendering-Svg-SvgRenderer-BadgeVerticalBar`, `Rendering-Svg-SvgRenderer-RenderBand`,
`Rendering-Svg-SvgRenderer-RenderLifeline`, `Rendering-Svg-SvgRenderer-RenderActivation`,
`Rendering-Svg-SvgRenderer-RenderGrid`.

#### Port label reads inward on every side, and a reserved inset shifts content

Theory tests `SvgRenderer_RenderPort_LeftRightLabel_ReadsInward` (Left/Right cases) and
`SvgRenderer_RenderPort_TopBottomLabel_ReadsInward` (Top/Bottom cases) each render a single port and
assert its label text element is positioned toward the box interior relative to the port glyph's
center: rightward for a left-side port, leftward for a right-side port, below a top-side port, and
above a bottom-side port — confirming the label never reads outward off the box on any of the four
sides. `SvgRenderer_RenderBoxCompartments_ContentInsetLeft_ShiftsRowTextRight` renders a box with a
positive `ContentInsetLeft` and asserts a compartment row's text starts further right than the
`Theme.LabelPadding`-only offset used when the inset is zero, confirming the renderer reads the
reserved margin rather than assuming a fixed offset.

**Covers**: `Rendering-Svg-SvgRenderer-RenderPortLabel`, `Rendering-Svg-SvgRenderer-ContentInset`.

#### Port label squeeze, title geometric centering, and label-aware canvas growth

`SvgRenderer_RenderPort_LongLabelWithMaxLabelWidth_AppliesTextLengthConstraint` renders a port with
a deliberately long `ExternalLabel` and a finite `MaxLabelWidth`, and asserts the emitted `<text>`
element carries a `textLength` attribute bounding it to that width (matching the squeeze mechanism
already used for box titles).
`SvgRenderer_RenderPort_LabelWidthEqualsMaxLabelWidth_NoTextLengthConstraint` sets a port's
`MaxLabelWidth` to exactly its label's natural width as measured by the shared
`PortLabelWidthEstimator.MeasureWidth` (mirroring how `LayeredLayoutAlgorithm` sizes
`MaxLabelWidth` from the same estimator) and asserts the emitted `<text>` element carries no
`textLength` attribute at all — confirming `RenderPort`'s accurate-estimator mode agrees with the
layout engine's own sizing decision instead of the coarser flat-multiplier heuristic disagreeing and
squeezing a label that already fits exactly.
`SvgRenderer_RenderBoxTitle_AsymmetricContentInsets_StaysAtGeometricCenter`
renders a box whose `ContentInsetLeft` differs from `ContentInsetRight` and asserts the title's
`<text x="...">` remains exactly at the box's full geometric center (`box.X + box.Width / 2.0`),
unaffected by the asymmetric insets — confirming an earlier inset-adjusted centering behavior (which
visibly shifted and squeezed titles even though no title/port-label collision was actually possible)
has been reverted. `SvgRenderer_RenderPort_Rect_HasStrokeDistinctFromFill` renders a single port and
asserts its glyph `<rect>` carries both a `fill` and a `stroke` attribute with distinct values (the
stroke matching `Theme.BackgroundColor`), confirming the port glyph remains visually distinguishable
from a solid-filled arrowhead marker that might land on/near the same box edge.
`SvgRenderer_Render_ManyCollidingConnectorLabels_AllLabelsWithinViewBox` renders 3+ parallel labeled
connectors whose midpoint labels collide and get nudged, then parses the rendered `viewBox` and every
label `<text>` element's approximate position/extent, asserting every label lies fully within the
final `viewBox` bounds — confirming the canvas grows to include labels nudged outside the
box+routing-geometry extent rather than clipping them invisibly.

**Covers**: `Rendering-Svg-SvgRenderer-PortLabelSqueeze`,
`Rendering-Svg-SvgRenderer-PortLabelAccurateFit`,
`Rendering-Svg-SvgRenderer-TitleCentersOnBoxWidth`,
`Rendering-Svg-SvgRenderer-PortGlyphOutline`,
`Rendering-Svg-SvgRenderer-CanvasGrowsForLabels`.

#### Connector end markers

Tests `OpenChevron_IsDefinedAsPolyline` and `OpenChevronLine_ReferencesOpenChevronMarker` assert that
the open-chevron marker definition contains `<polyline>` and no `<polygon>`, and that an open-chevron
line contains `marker-end="url(#line-end-open-chevron)"`.

Tests `HollowTriangle_IsDefinedAsClosedPolygon`,
`SvgRenderer_Render_SingleLine_WithHollowTriangleArrowhead_ProducesMarkerEnd`, and
`TriangleMarker_DimensionsDeriveFromNotationMetrics` assert that the hollow-triangle marker definition
contains `<polygon>` and no `<polyline>`, a hollow-triangle target line contains a `marker-end`
attribute, and triangle marker dimensions and points match `NotationMetrics`.

Tests `DiamondMarker_DimensionsDeriveFromNotationMetrics` and
`SvgRenderer_Render_SingleLine_WithDiamondArrowhead_ProducesDiamondMarker` assert that diamond marker
dimensions and points match `NotationMetrics` and that the rendered SVG contains the
`line-end-hollow-diamond` id.

Test `SvgRenderer_Render_SingleLine_WithOpenCrossbarArrowhead_ProducesOpenCrossbarMarker` asserts that
the rendered SVG contains the `line-end-hollow-triangle-crossbar` id. Test
`EndMarkerCrossbar_BarSitsBehindTriangleBase` reads the marker definition back and asserts the marker box
and tip anchor are the widened `NotationMetrics.Crossbar*` values, that the triangle base is at box x =
`CrossbarExtension` (apex at the box end), and that the bar is a vertical `<line>` spanning the full marker
height whose box x is below the base's by `CrossbarBaseGap`, i.e. behind the base with a gap.

Test `LineCornerClamp_UsesRenderedMarkerLength` renders a line whose final leg is 17 px before a 15 px
rendered chevron and asserts the final corner arc radius is 17 px minus the rendered marker length, not the 4 px
that a clamp
using the notation length would allow.

Tests `Marker_PaintedTip_LandsOnTipAnchor_AtBothEnds` and `Marker_PaintedTip_LandsOnTipAnchor_WithThickerStroke`
read the marker definition back for every end-marker style, at the line end and (mirrored) the line start, in
the Light theme and a 2.5-stroke theme, and compute the STROKED tip with an independent stroke-geometry helper
(the union of the stroked segments' rectangles, exact for bevel joins and butt caps, and the stroke
half-width beyond a circle radius). They assert the painted tip equals the marker's tip anchor, which is the
line endpoint, and is not clipped by the marker box (the regression test for issue #49: before the fix every
tip was 1 to 1.5 px past the endpoint). Test `Marker_PolygonsAndPolylines_UseBevelJoin` asserts every polygon
and polyline in every marker carries `stroke-linejoin="bevel"`.

Tests `LineEndingAtPort_TargetMarker_IsShortenedToPortEdge`, `LineStartingAtPort_SourceMarker_IsShortenedToPortEdge`
and `LineEnd_WithoutMarkerOrPort_IsNotShortened` render a line whose marker end sits on a `LayoutPort`
centre and assert the path ends (or starts) `PortHalfSize` short of it, at the port square's outer edge, while
a line end without a marker, or one not on a port, keeps its original endpoint.

Tests `SvgRenderer_Render_SingleLine_WithFilledArrow_ProducesFilledArrowMarker`,
`SvgRenderer_Render_SingleLine_WithFilledDiamond_ProducesFilledDiamondMarker`,
`SvgRenderer_Render_SingleLine_WithCircleEnd_ProducesCircleMarker`, and
`SvgRenderer_Render_SingleLine_WithBarEnd_ProducesBarMarker` assert that the connector path carries
the marker reference for the filled-arrow, filled-diamond, circle, and bar end-marker variants.

The diamond and crossbar assertions now check that the connector path element carries the marker
reference itself — `marker-start="url(#line-start-hollow-diamond)"` for the source end and
`marker-end="url(#line-end-hollow-triangle-crossbar)"` for the target end — rather than only that the
marker id appears somewhere in the document. This prevents a false pass if the marker is defined in
`<defs>` but no longer referenced by the connector. The source-end references use the mirrored
`line-start-…` variant (see *Dotted-triangle and circled-plus markers, on-demand definitions, and start
variants* below); the filled-diamond scenario likewise asserts `marker-start="url(#line-start-filled-diamond)"`.

**Covers**: `Rendering-Svg-SvgRenderer-EndMarkers`,
`Rendering-Svg-SvgRenderer-EndMarkersOpenChevronReference`,
`Rendering-Svg-SvgRenderer-HollowTriangleEndMarkers`,
`Rendering-Svg-SvgRenderer-HollowTriangleEndMarkerReference`,
`Rendering-Svg-SvgRenderer-TriangleEndMarkerMetrics`,
`Rendering-Svg-SvgRenderer-DiamondEndMarkers`,
`Rendering-Svg-SvgRenderer-DiamondEndMarkerReference`,
`Rendering-Svg-SvgRenderer-CrossbarEndMarkers`,
`Rendering-Svg-SvgRenderer-CornerClampRenderedMarker`,
`Rendering-Svg-SvgRenderer-EndMarkerFilledArrow`,
`Rendering-Svg-SvgRenderer-EndMarkerFilledDiamond`,
`Rendering-Svg-SvgRenderer-EndMarkerCircle`,
`Rendering-Svg-SvgRenderer-EndMarkerBar`,
`Rendering-Svg-SvgRenderer-EndMarkerTipOnEndpoint`,
`Rendering-Svg-SvgRenderer-EndMarkerPortEndpoint`.

#### Dotted-triangle and circled-plus markers, on-demand definitions, and start variants

Tests `EndMarkerTwoDots_DefinesTriangleAndTwoDotsFromNotationMetrics` and
`EndMarkerFourDots_HasTwoByTwoDotGrid` render a line with the two-dots and four-dots target marker. The
first asserts the `line-end-hollow-triangle-two-dots` definition contains a `<polygon>` and exactly two
`<circle>` elements, that its `markerWidth`/`markerHeight`/`refX`/`refY` equal the `NotationMetrics`
values, and that the line references it with `marker-end`. The second asserts four dot circles forming two
distinct columns and two distinct rows and the `FourDotsMarkerLength` marker width. Test
`EndMarkerCircledPlus_DefinesCircleAndTwoPlusArms` asserts the `line-end-circled-plus` definition has one
circle of radius `CircledPlusRadius` filled with the theme background, two `<line>` arms, and the
`CircledPlusMarkerBox`/`CircledPlusRefX`/`CircledPlusCenter` box and anchor.

Test `EndMarkerDefs_NewMarkersUnused_AreNotEmitted` renders a line that uses none of the new markers and
asserts that no `two-dots`, `four-dots`, `circled-plus`, or `line-start-` text appears, that exactly eight
`<marker>` elements are written, and that the `label-bg` filter is present. Test
`EndMarkerDefs_NewMarkerUsedTwice_IsDefinedOnce` renders two lines that use the circled plus and asserts a
single `line-end-circled-plus` definition.

Tests `EndMarkerSource_CircledPlus_UsesMirroredStartVariant`,
`EndMarkerSource_FourDots_UsesMirroredStartVariantOnly`, and
`EndMarkerSource_LegacyDiamond_UsesMirroredStartVariant` render lines with a source-end marker and assert
that `marker-start` references the `line-start-…` id, that the definition's `refX` is the mirrored anchor
(marker length minus the end-variant anchor), and that the glyph is wrapped in
`matrix(-1 0 0 1 <length> 0)`; the four-dots scenario also asserts the unused end variant is not written.
Test `EndMarkerBothEnds_NewMarkers_ReferenceStartAndEndVariants` renders one line with the circled plus at
the source and the two-dots marker at the target and asserts `marker-start` references
`line-start-circled-plus` while `marker-end` references `line-end-hollow-triangle-two-dots`. These
scenarios verify the deliberate fix of the earlier defect in which a `marker-start` marker reused the
end-variant geometry and was drawn pointing inward.

**Covers**: `Rendering-Svg-SvgRenderer-EndMarkerDottedTriangles`,
`Rendering-Svg-SvgRenderer-EndMarkerCircledPlus`,
`Rendering-Svg-SvgRenderer-EndMarkerDefsOnlyWhenUsed`,
`Rendering-Svg-SvgRenderer-EndMarkerStartVariants`.

#### Box outline, name, keyword, and compartment title styles

Theory `BoxOutline_DashedOrDotted_EmitsDashArrayForEveryShape` renders rectangle, rounded-rectangle,
folder, and note boxes with a dashed or dotted appearance and asserts `stroke-dasharray` of `6 3` or
`2 2`. `BoxOutline_DashedNote_DashesBodyAndFold` asserts a dashed note carries exactly two `6 3` dash
arrays (body and fold). `BoxOutline_SolidOrNoAppearance_EmitsNoDashArray` asserts a box with no
appearance, and one with a default appearance, emit no dash array.

`BoxName_Styles_EmitMatchingFontAttributes` asserts a regular name emits neither `font-weight` nor
`font-style`, an italic name emits only `font-style="italic"`, and a bold-italic name emits both.
`BoxName_DefaultAppearance_EmitsLegacyBoldMarkup` asserts a box without an appearance and one with a
default appearance emit the identical bold, upright name element. `BoxKeyword_DefaultItalicAndRegularOverride`
asserts the keyword is italic by default, upright with a regular keyword style, and bold with a bold
keyword style.

`CompartmentTitle_Centered_UsesMiddleAnchorAtContentCentre` renders a compartment with a centered italic
title in a box with asymmetric content insets and asserts `text-anchor="middle"`, an `x` equal to
`box.X + insetLeft + (box.Width - insetLeft - insetRight) / 2`, italic style, and no bold weight.
`CompartmentTitle_Default_IsLegacyBoldItalicStart` asserts the default title is `text-anchor="start"` with
`font-weight="bold" font-style="italic"`. `CompartmentSeparator_WithCenteredTitle_SpansFullBoxWidth` asserts
the divider line still runs from `box.X` to `box.X + box.Width`.

**Covers**: `Rendering-Svg-SvgRenderer-BoxOutlineStyle`, `Rendering-Svg-SvgRenderer-BoxNameStyle`,
`Rendering-Svg-SvgRenderer-BoxKeywordStyle`, `Rendering-Svg-SvgRenderer-CompartmentTitleStyle`.

#### Folder name in tab and corner icons

`FolderNameInTab_DrawsNameInsideTab` renders a folder with the tab title placement and asserts the name
appears exactly once, with its `x`/`y` inside the tab rectangle computed from
`BoxMetrics.ResolveFolderTabWidth`/`ResolveFolderTabHeight`, left-aligned, and not bold for a regular name
style. `FolderNameInTab_CompartmentsStartUnderTab` asserts the first compartment row sits higher than under
a body-titled folder. `FolderBodyTitle_Default_DrawsNameBelowTab` asserts the default folder name is below
the tab, centered, and bold. `FolderNameInTab_NonFolderShape_IsIgnored` asserts a rectangle with the tab
placement still centers its name in the body.

`CornerIcon_SendAndAccept_EmitPolygonsWithExpectedVertexCounts` asserts the send icon is a five-vertex
polygon, the accept icon a six-vertex polygon, a box without an icon has no polygon outside `<defs>`, and
the first vertex sits at the box origin plus `NotationMetrics.CornerIconInset`.
`CornerIcon_OnFolder_StartsBelowTab` asserts the icon's first vertex is below the folder tab.

**Covers**: `Rendering-Svg-SvgRenderer-FolderTitleInTab`, `Rendering-Svg-SvgRenderer-BoxCornerIcon`.

#### Thick-bar and crossed-circle badges, dashed bands, lifeline headers, and label avoidance

`BadgeThickHorizontalBar_EmitsFilledRectOfSizeAndDefaultThickness` renders a thick horizontal bar badge
and asserts a filled `<rect>` whose width is the badge size and whose height is the default
`ThickBarThickness`, centered on the badge. `BadgeThickVerticalBar_EmitsFilledRectWithCustomThickness`
asserts a vertical bar's height is the size and its width is an explicit `Thickness`.
`BadgeCrossedCircle_EmitsCircleAndTwoDiagonalsEndingOnCircle` asserts a background-filled circle and two
diagonal `<line>` arms whose endpoints are offset by `r × CrossedCircleArmFraction`.
`BandOutline_DashedAndSolid` asserts a dashed band carries `stroke-dasharray="6 3"` and a solid band
carries none.

`LifelineRoundedHeader_EmitsCornerRadiusAndKeepsDashedStem` asserts a rounded header carries `rx`/`ry`
equal to the theme radius (or an explicit `HeaderCornerRadius`) and the stem keeps its dash array.
`LifelineKeyword_DrawsGuillemetLineAboveLabel` asserts the keyword text is above the label and italic.
`LifelineHeaderAppearance_DashedOutlineAndRegularLabel` asserts a dashed header appearance adds a second
dash array (header plus stem) and a regular label has no `font-weight`. `LifelineDefault_MatchesLegacyMarkup`
asserts a lifeline with no new properties has no `rx`, a bold label, and a single dash array (the stem).

`LineLabel_MidpointOnLeafBox_IsPlacedClear` renders a horizontal labeled line whose midpoint lies on a
leaf box and asserts the label's estimated extent lies entirely left or right of the box.

**Covers**: `Rendering-Svg-SvgRenderer-BadgeThickBar`, `Rendering-Svg-SvgRenderer-BadgeCrossedCircle`,
`Rendering-Svg-SvgRenderer-BandDashed`, `Rendering-Svg-SvgRenderer-LifelineRoundedHeader`,
`Rendering-Svg-SvgRenderer-LabelsAvoidBoxes`.

#### Diamond box outline

`SvgRenderer_Diamond_PolygonPointsAreTheFourEdgeMidpoints` asserts the diamond is a polygon whose points are the four
box-edge midpoints, `SvgRenderer_Diamond_UsesDepthFillAndThemeStroke` asserts the depth fill and theme stroke,
`SvgRenderer_Diamond_DashedAppearance_AddsDashArray` asserts the dash array,
`SvgRenderer_Diamond_Scaled_ScalesThePolygonPoints` asserts the render scale, and
`SvgRenderer_Diamond_Label_IsCenteredOnTheDiamond` asserts a centered title.
`SvgRenderer_Rectangle_StillRendersAsRectNotPolygon` asserts rectangles keep their `<rect>` markup, and
`SysmlPortsParity_DiamondShape_PngOutlineMatchesSvgPolygon` asserts the PNG outline passes through every vertex and edge
midpoint of the SVG polygon.

**Covers**: `Rendering-Svg-SvgRenderer-DiamondShape`.

### Requirements Coverage

- **`Rendering-Svg-SvgRenderer-ImplementsIRenderer`**:
  `SvgRenderer_Render_SingleBox_ProducesSvgDocument`
- **`Rendering-Svg-SvgRenderer-MediaType`**: `SvgRenderer_Render_SingleBox_ProducesSvgDocument`
- **`Rendering-Svg-SvgRenderer-DefaultExtension`**:
  `SvgRenderer_Render_SingleBox_ProducesSvgDocument`
- **`Rendering-Svg-SvgRenderer-RenderDocument`**:
  `SvgRenderer_Render_EmptyTree_ProducesSvgDocument`
- **`Rendering-Svg-SvgRenderer-RenderEmptyTree`**:
  `SvgRenderer_Render_EmptyTree_ProducesSvgDocument`
- **`Rendering-Svg-SvgRenderer-RenderBox`**: `SvgRenderer_Render_SingleBox_ProducesRectElement`
- **`Rendering-Svg-SvgRenderer-RenderBoxRoundedCorners`**:
  `SvgRenderer_Render_BoxRoundedRectangle_ProducesRxAttribute`
- **`Rendering-Svg-SvgRenderer-RenderBoxCompartments`**:
  `SvgRenderer_Render_BoxWithCompartment_ProducesLineAndText`
- **`Rendering-Svg-SvgRenderer-RenderLabel`**:
  `SvgRenderer_Render_SingleLabel_ProducesTextElement`
- **`Rendering-Svg-SvgRenderer-RenderLabelBold`**:
  `SvgRenderer_Render_LabelWithBold_ProducesBoldAttribute`
- **`Rendering-Svg-SvgRenderer-RenderLabelItalic`**:
  `SvgRenderer_Render_LabelWithItalic_ProducesItalicAttribute`
- **`Rendering-Svg-SvgRenderer-RenderLabelEscaping`**:
  `SvgRenderer_Render_LabelWithXmlSpecialCharacters_ProducesWellFormedEscapedSvg`
- **`Rendering-Svg-SvgRenderer-RenderLine`**: `SvgRenderer_Render_SingleLine_ProducesPathElement`
- **`Rendering-Svg-SvgRenderer-RenderLineRoundedCorners`**:
  `SvgRenderer_Render_SingleLine_WithCornerRadius_ProducesArcInPath`,
  `SvgRenderer_Render_SingleLine_CollinearInteriorWaypoint_ProducesNoArcInPath`
- **`Rendering-Svg-SvgRenderer-RenderLineDashed`**:
  `SvgRenderer_Render_SingleLine_Dashed_ProducesDashArray`
- **`Rendering-Svg-SvgRenderer-RenderLineMidpointLabel`**:
  `SvgRenderer_Render_LineWithMidpointLabel_ProducesTextElement`
- **`Rendering-Svg-SvgRenderer-RenderNodeKinds`**: `SvgRenderer_Render_SinglePort_ProducesRect`
- **`Rendering-Svg-SvgRenderer-RenderPortLabel`**: `SvgRenderer_RenderPort_LeftRightLabel_ReadsInward`,
  `SvgRenderer_RenderPort_TopBottomLabel_ReadsInward`
- **`Rendering-Svg-SvgRenderer-PortLabelSqueeze`**:
  `SvgRenderer_RenderPort_LongLabelWithMaxLabelWidth_AppliesTextLengthConstraint`
- **`Rendering-Svg-SvgRenderer-PortLabelAccurateFit`**:
  `SvgRenderer_RenderPort_LabelWidthEqualsMaxLabelWidth_NoTextLengthConstraint`
- **`Rendering-Svg-SvgRenderer-ContentInset`**:
  `SvgRenderer_RenderBoxCompartments_ContentInsetLeft_ShiftsRowTextRight`
- **`Rendering-Svg-SvgRenderer-TitleCentersOnBoxWidth`**:
  `SvgRenderer_RenderBoxTitle_AsymmetricContentInsets_StaysAtGeometricCenter`
- **`Rendering-Svg-SvgRenderer-PortGlyphOutline`**:
  `SvgRenderer_RenderPort_Rect_HasStrokeDistinctFromFill`
- **`Rendering-Svg-SvgRenderer-CanvasGrowsForLabels`**:
  `SvgRenderer_Render_ManyCollidingConnectorLabels_AllLabelsWithinViewBox`
- **`Rendering-Svg-SvgRenderer-RenderBadge`**:
  `SvgRenderer_Render_SingleBadge_FilledCircle_ProducesCircle`
- **`Rendering-Svg-SvgRenderer-BadgeBullseye`**:
  `SvgRenderer_Render_SingleBadge_Bullseye_ProducesConcentricCircles`
- **`Rendering-Svg-SvgRenderer-BadgeDiamond`**:
  `SvgRenderer_Render_SingleBadge_Diamond_ProducesPolygon`
- **`Rendering-Svg-SvgRenderer-BadgeHorizontalBar`**:
  `SvgRenderer_Render_SingleBadge_HorizontalBar_ProducesLine`
- **`Rendering-Svg-SvgRenderer-BadgeVerticalBar`**:
  `SvgRenderer_Render_SingleBadge_VerticalBar_ProducesLine`
- **`Rendering-Svg-SvgRenderer-RenderBand`**: `SvgRenderer_Render_SingleBand_ProducesRect`
- **`Rendering-Svg-SvgRenderer-RenderLifeline`**:
  `SvgRenderer_Render_SingleLifeline_ProducesRectAndLine`
- **`Rendering-Svg-SvgRenderer-RenderActivation`**:
  `SvgRenderer_Render_SingleActivation_ProducesRect`
- **`Rendering-Svg-SvgRenderer-RenderGrid`**: `SvgRenderer_Render_SingleGrid_ProducesRects`
- **`Rendering-Svg-SvgRenderer-EndMarkers`**: `OpenChevron_IsDefinedAsPolyline`
- **`Rendering-Svg-SvgRenderer-EndMarkersOpenChevronReference`**:
  `OpenChevronLine_ReferencesOpenChevronMarker`
- **`Rendering-Svg-SvgRenderer-HollowTriangleEndMarkers`**:
  `HollowTriangle_IsDefinedAsClosedPolygon`
- **`Rendering-Svg-SvgRenderer-HollowTriangleEndMarkerReference`**:
  `SvgRenderer_Render_SingleLine_WithHollowTriangleArrowhead_ProducesMarkerEnd`
- **`Rendering-Svg-SvgRenderer-TriangleEndMarkerMetrics`**:
  `TriangleMarker_DimensionsDeriveFromNotationMetrics`
- **`Rendering-Svg-SvgRenderer-DiamondEndMarkers`**:
  `DiamondMarker_DimensionsDeriveFromNotationMetrics`
- **`Rendering-Svg-SvgRenderer-DiamondEndMarkerReference`**:
  `SvgRenderer_Render_SingleLine_WithDiamondArrowhead_ProducesDiamondMarker`
- **`Rendering-Svg-SvgRenderer-CrossbarEndMarkers`**:
  `SvgRenderer_Render_SingleLine_WithOpenCrossbarArrowhead_ProducesOpenCrossbarMarker`,
  `EndMarkerCrossbar_BarSitsBehindTriangleBase`
- **`Rendering-Svg-SvgRenderer-CornerClampRenderedMarker`**:
  `LineCornerClamp_UsesRenderedMarkerLength`
- **`Rendering-Svg-SvgRenderer-EndMarkerFilledArrow`**:
  `SvgRenderer_Render_SingleLine_WithFilledArrow_ProducesFilledArrowMarker`
- **`Rendering-Svg-SvgRenderer-EndMarkerFilledDiamond`**:
  `SvgRenderer_Render_SingleLine_WithFilledDiamond_ProducesFilledDiamondMarker`
- **`Rendering-Svg-SvgRenderer-EndMarkerCircle`**:
  `SvgRenderer_Render_SingleLine_WithCircleEnd_ProducesCircleMarker`
- **`Rendering-Svg-SvgRenderer-EndMarkerBar`**:
  `SvgRenderer_Render_SingleLine_WithBarEnd_ProducesBarMarker`
- **`Rendering-Svg-SvgRenderer-EndMarkerTipOnEndpoint`**:
  `Marker_PaintedTip_LandsOnTipAnchor_AtBothEnds`, `Marker_PaintedTip_LandsOnTipAnchor_WithThickerStroke`,
  `Marker_PolygonsAndPolylines_UseBevelJoin`
- **`Rendering-Svg-SvgRenderer-EndMarkerPortEndpoint`**:
  `LineEndingAtPort_TargetMarker_IsShortenedToPortEdge`, `LineStartingAtPort_SourceMarker_IsShortenedToPortEdge`,
  `LineEnd_WithoutMarkerOrPort_IsNotShortened`
- **`Rendering-Svg-SvgRenderer-EndMarkerDottedTriangles`**:
  `EndMarkerTwoDots_DefinesTriangleAndTwoDotsFromNotationMetrics`,
  `EndMarkerFourDots_HasTwoByTwoDotGrid`
- **`Rendering-Svg-SvgRenderer-EndMarkerCircledPlus`**:
  `EndMarkerCircledPlus_DefinesCircleAndTwoPlusArms`
- **`Rendering-Svg-SvgRenderer-EndMarkerDefsOnlyWhenUsed`**:
  `EndMarkerDefs_NewMarkersUnused_AreNotEmitted`, `EndMarkerDefs_NewMarkerUsedTwice_IsDefinedOnce`
- **`Rendering-Svg-SvgRenderer-EndMarkerStartVariants`**:
  `EndMarkerSource_CircledPlus_UsesMirroredStartVariant`,
  `EndMarkerSource_FourDots_UsesMirroredStartVariantOnly`,
  `EndMarkerSource_LegacyDiamond_UsesMirroredStartVariant`,
  `EndMarkerBothEnds_NewMarkers_ReferenceStartAndEndVariants`
- **`Rendering-Svg-SvgRenderer-BoxOutlineStyle`**:
  `BoxOutline_DashedOrDotted_EmitsDashArrayForEveryShape`,
  `BoxOutline_DashedNote_DashesBodyAndFold`, `BoxOutline_SolidOrNoAppearance_EmitsNoDashArray`
- **`Rendering-Svg-SvgRenderer-BoxNameStyle`**: `BoxName_Styles_EmitMatchingFontAttributes`,
  `BoxName_DefaultAppearance_EmitsLegacyBoldMarkup`
- **`Rendering-Svg-SvgRenderer-BoxKeywordStyle`**: `BoxKeyword_DefaultItalicAndRegularOverride`
- **`Rendering-Svg-SvgRenderer-CompartmentTitleStyle`**:
  `CompartmentTitle_Centered_UsesMiddleAnchorAtContentCentre`,
  `CompartmentTitle_Default_IsLegacyBoldItalicStart`,
  `CompartmentSeparator_WithCenteredTitle_SpansFullBoxWidth`
- **`Rendering-Svg-SvgRenderer-FolderTitleInTab`**: `FolderNameInTab_DrawsNameInsideTab`,
  `FolderNameInTab_CompartmentsStartUnderTab`, `FolderBodyTitle_Default_DrawsNameBelowTab`,
  `FolderNameInTab_NonFolderShape_IsIgnored`
- **`Rendering-Svg-SvgRenderer-BoxCornerIcon`**:
  `CornerIcon_SendAndAccept_EmitPolygonsWithExpectedVertexCounts`, `CornerIcon_OnFolder_StartsBelowTab`
- **`Rendering-Svg-SvgRenderer-BadgeThickBar`**:
  `BadgeThickHorizontalBar_EmitsFilledRectOfSizeAndDefaultThickness`,
  `BadgeThickVerticalBar_EmitsFilledRectWithCustomThickness`
- **`Rendering-Svg-SvgRenderer-BadgeCrossedCircle`**:
  `BadgeCrossedCircle_EmitsCircleAndTwoDiagonalsEndingOnCircle`
- **`Rendering-Svg-SvgRenderer-BandDashed`**: `BandOutline_DashedAndSolid`
- **`Rendering-Svg-SvgRenderer-LifelineRoundedHeader`**:
  `LifelineRoundedHeader_EmitsCornerRadiusAndKeepsDashedStem`,
  `LifelineKeyword_DrawsGuillemetLineAboveLabel`,
  `LifelineHeaderAppearance_DashedOutlineAndRegularLabel`, `LifelineDefault_MatchesLegacyMarkup`
- **`Rendering-Svg-SvgRenderer-LabelsAvoidBoxes`**: `LineLabel_MidpointOnLeafBox_IsPlacedClear`
- **`Rendering-Svg-SvgRenderer-DiamondShape`**:
  `SvgRenderer_Diamond_PolygonPointsAreTheFourEdgeMidpoints`, `SvgRenderer_Diamond_UsesDepthFillAndThemeStroke`,
  `SvgRenderer_Diamond_DashedAppearance_AddsDashArray`, `SvgRenderer_Diamond_Scaled_ScalesThePolygonPoints`,
  `SvgRenderer_Rectangle_StillRendersAsRectNotPolygon`, `SvgRenderer_Diamond_Label_IsCenteredOnTheDiamond`,
  `SysmlPortsParity_DiamondShape_PngOutlineMatchesSvgPolygon`
