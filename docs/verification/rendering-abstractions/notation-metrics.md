## Notation Metrics Unit Verification

Part of the Rendering Abstractions Verification.

This document describes the verification design for the notation-metrics unit of the
`DemaConsulting.Rendering.Abstractions` system. It maps every notation-metrics unit requirement to at
least one named test scenario so a reviewer can confirm coverage without reading the test code. The
verification strategy, test environment, and acceptance criteria are described in the
system verification document; the test project is
`DemaConsulting.Rendering.Abstractions.Tests` (`NotationMetricsTests.cs` and
`NotationPrimitiveMetricsTests.cs`).

### Verification Approach

The notation-metrics unit is verified with in-process xUnit unit tests that read the public
constants and call the static helpers on `NotationMetrics` and the `MarkerVertex` record struct
directly. No mocking, dependency injection, or filesystem access is used. Where a helper takes a
`Theme` argument (`RoundedRectRadius(Theme)`), the tests pass one of the built-in `Themes` so the
inputs are deterministic. The tests both assert the canonical constant values (for example the
triangle 10x7 box) and reproduce the historical SVG marker points to prove the shared vertices
match the values shipped previously. The notation-primitive tests additionally assert each derived
dotted-triangle, circled-plus, and badge constant against its documented derivation, assert the dot
centers and corner-icon polygons structurally (counts, symmetry, columns, and containment within the
marker box or icon box), and assert that every pre-existing `AlongLineLength` arm is unchanged.

### Test Environment

- **Framework**: xUnit v4 running under the .NET SDK on `net8.0`, `net9.0`, `net10.0`, and `net11.0`.
- **Execution**: `dotnet test` invoked by `build.ps1` and by the CI pipeline.
- **Test project**: `DemaConsulting.Rendering.Abstractions.Tests` (`NotationMetricsTests.cs` and
  `NotationPrimitiveMetricsTests.cs`).
- **External dependencies**: none.

### Acceptance Criteria

The unit is considered verified when every scenario listed below passes. Each test must return the
documented constant, derived value, or vertex list; any drift in a canonical value, in a derived
proportion, or in the vertex geometry constitutes a failure.

### Test Scenarios

#### Triangle marker geometry is canonical

Tests `TriangleFamily_HasCanonicalValues` and `TriangleVertices_ReproduceSvgBoxPoints` assert the
triangle-family constants (10x7, apex on the tip anchor: refX 10, overshoot 0) and map the shared vertices
back to the SVG box points "0 0, 10 3.5, 0 7".

**Covers**: `Rendering-Abstractions-NotationMetrics-TriangleDimensions`.

#### Triangle apex lands on the endpoint

Test `TriangleVertices_Apex_LandsOnEndpoint` confirms the apex sits on the tip anchor (`Along` 0) and that
no vertex lies ahead of it.

**Covers**: `Rendering-Abstractions-NotationMetrics-TriangleApexOnEndpoint`.

#### The stroked tip is retracted onto the endpoint

Test `TipRetraction_IsHalfStrokeTimesTipFactor` asserts, at the default theme, that the retraction is
half the stroke width times sin(half apex angle) for the triangle family (0.2478 units) and the diamond
far vertex (0.4163 units), the full half stroke width (0.75) for the circle, circled plus and bar, and zero
for `None`. Test `TipRetraction_ScalesWithThemeStrokeWidth_AndRejectsNullTheme` asserts it doubles with a
double stroke width and rejects a `null` theme. Test `MarkerAnchor_KeepsRetractedAnchorInsideBox_AndFeedsRenderedLength`
asserts for six representative styles that the tip anchor lies inside the (possibly widened) box, the box is
no shorter than the glyph's own, and `RenderedAlongLineLength` is that box length times the marker scale.

**Covers**: `Rendering-Abstractions-NotationMetrics-TipRetraction`.

#### Diamond marker geometry is canonical

Tests `Diamond_HasCanonicalValues` and `DiamondVertices_ReproduceSvgBoxPoints` assert the diamond
constants (14x8, refX 13) and map the shared vertices back to the SVG box points "1 4, 7 0, 13 4, 7 8".

**Covers**: `Rendering-Abstractions-NotationMetrics-DiamondDimensions`.

#### Diamond far point lands on the endpoint

Test `DiamondVertices_FarPoint_LandsOnEndpoint` confirms one vertex sits exactly on the line
endpoint.

**Covers**: `Rendering-Abstractions-NotationMetrics-DiamondFarPointOnEndpoint`.

#### Circle and bar geometry is canonical

Test `CircleAndBar_HaveCanonicalValues` asserts the circle (radius 4, box 10, centre 5, refX 9) and
bar (4x12, half-along 2, half 6) constants match the historical markers.

**Covers**: `Rendering-Abstractions-NotationMetrics-CircleBarGeometry`.

#### Crossbar sits behind the triangle base

Test `Crossbar_SitsBehindTriangleBase` asserts the crossbar constants (gap 3.5, extension 4.5, box
length 14.5, tip anchor 13.5), that the bar's distance back from the tip exceeds the triangle base's by
the gap (so it is on the line side of the base), that the bar and its half stroke lie inside the widened
marker box, that its half length equals the base half width, that the tip anchor shifts by the same
extension as the box grows, and that `TriangleFamilyBox` returns (14.5, 13.5) for the style.

**Covers**: `Rendering-Abstractions-NotationMetrics-Crossbar`.

#### Along-line length matches the marker box

Test `AlongLineLength_MatchesMarkerBox` reads `AlongLineLength` for each `EndMarkerStyle` and asserts
each reports its documented length (0 for None, 10 for the triangle family and circle, 14 for
diamonds, 4 for bar, 14.5 for the crossbar triangle). Test
`AlongLineLength_NewStyles_MatchMarkerBoxesAndLegacyUnchanged` asserts the
three appended styles report `TwoDotsMarkerLength` (15), `FourDotsMarkerLength` (19), and
`CircledPlusMarkerBox` (12), and that every other legacy style still reports its historical value (the
crossbar triangle reports `CrossbarMarkerLength`).

**Covers**: `Rendering-Abstractions-NotationMetrics-AlongLineLength`.

#### Port square proportion is documented

Test `Port_SizeIsTwiceHalfSize` asserts the port square constant and its derivation.

**Covers**: `Rendering-Abstractions-NotationMetrics-PortSquare`.

#### Folder-tab proportion is documented

Test `FolderTab_HasDocumentedConstants` asserts the folder-tab constants.

**Covers**: `Rendering-Abstractions-NotationMetrics-FolderTab`.

#### Note-fold proportion is documented

Test `NoteFold_HasDocumentedConstants` asserts the note-fold constants.

**Covers**: `Rendering-Abstractions-NotationMetrics-NoteFold`.

#### Rounded-rectangle corner proportion is documented

Test `RoundedRectRadius_IsThemeRadiusTimesFactor` asserts the rounded-rectangle corner derivation.

**Covers**: `Rendering-Abstractions-NotationMetrics-RoundedRectCorner`.

#### Null theme is rejected

Test `RoundedRectRadius_NullTheme_ThrowsArgumentNullException` asserts a null `theme` argument is
rejected with an `ArgumentNullException`.

**Covers**: `Rendering-Abstractions-NotationMetrics-RejectNullTheme`.

#### Badge proportions are documented

Test `BadgeFractions_HaveDocumentedValues` asserts the badge fraction constants.

**Covers**: `Rendering-Abstractions-NotationMetrics-Badge`.

#### Label-background proportion is documented

Test `LabelBackground_ExtentMatchesInset` asserts the label-background extent derivation.

**Covers**: `Rendering-Abstractions-NotationMetrics-LabelBackground`.

#### Dotted-triangle markers keep the triangle anchored

Test `DottedTriangles_HaveCanonicalValues` asserts the dot extensions, marker box lengths, and tip
anchors equal their documented derivations (`TwoDotsExtension` = `DotBaseGap` + 2 x `DotRadius`,
`FourDotsExtension` adds `DotColumnSpacing`, each marker length adds the extension to
`EndMarkerLength`, each anchor adds it to `EndMarkerRefX`). Test `DotCenters_TwoDots_StraddleLineBehindBase`
asserts two dots at the same `Along` position, mirrored about the line, wholly behind the triangle base
and clear of the line stroke. Test `DotCenters_FourDots_FormTwoByTwoGrid` asserts four dots in two
columns `DotColumnSpacing` apart with two dots on each side of the line. Test
`DotCenters_NonDottedStyles_AreEmpty` asserts `HollowTriangle`, `CircledPlus`, and `None` yield no dots.
Test `DotCenters_StayInsideMarkerBox` asserts every dot lies within the marker box length and within
`EndMarkerHalfWidth` across the line. Test `TriangleFamilyBox_DottedVariants_KeepTriangleTipAnchored`
asserts the plain box is (`EndMarkerLength`, `EndMarkerRefX`), the dotted boxes are
(`TwoDotsMarkerLength`, `TwoDotsRefX`) and (`FourDotsMarkerLength`, `FourDotsRefX`), and the distance from
the tip anchor to the box end is the same as for the plain triangle.

**Covers**: `Rendering-Abstractions-NotationMetrics-DottedTriangleMarkers`.

#### Circled-plus geometry is canonical

Test `CircledPlus_HasCanonicalValues` asserts the center is half the marker box, the tip anchor is the
center plus the radius and lies within the box, and the plus-arm half length equals the circle radius.

**Covers**: `Rendering-Abstractions-NotationMetrics-CircledPlusMarker`.

#### Marker scale is the theme stroke width

Test `MarkerScale_IsThemeStrokeWidth_AndMarkerStrokeIsScaledStroke` asserts `MarkerScale` returns the theme
stroke width (2.0 for a custom theme, 1.5 for Light), that `MarkerStrokeWidth` is the stroke width times the
scale (4.0, 2.25), and that both reject a `null` theme with `ArgumentNullException`.

**Covers**: `Rendering-Abstractions-NotationMetrics-MarkerScale`.

#### Rendered along-line length scales by the marker scale

Test `RenderedAlongLineLength_ScalesNotationLengthByStrokeWidth` asserts the result is the marker box
length (`AlongLineLength` plus the tip retraction for `OpenChevron`, unchanged for `CircledPlus`, whose
box has slack behind its far point) x the Light theme's marker scale, and zero for
`None`. Test `RenderedAlongLineLength_NullTheme_Throws` asserts a `null` theme raises
`ArgumentNullException`.

**Covers**: `Rendering-Abstractions-NotationMetrics-RenderedAlongLineLength`.

#### Across-line half width is the widest point

Test `AcrossHalfWidth_EveryStyle_MatchesWidestPoint` asserts zero for `None`, `EndMarkerHalfWidth` for
the triangle family including both dotted variants, `DiamondHalfWidth` for both diamonds,
`CircleRadius` for the circle, `BarHalf` for the bar, and `CircledPlusRadius` for the circled plus.

**Covers**: `Rendering-Abstractions-NotationMetrics-AcrossHalfWidth`.

#### Thick-bar thickness is documented

Test `ThickBarAndCrossedCircle_HaveCanonicalValues` asserts `ThickBarThickness` is 4.0.

**Covers**: `Rendering-Abstractions-NotationMetrics-ThickBarBadge`.

#### Crossed-circle arm fraction lands the arms on the circle

Test `ThickBarAndCrossedCircle_HaveCanonicalValues` also asserts `CrossedCircleArmFraction` equals cos 45
degrees and that an arm with that fraction on both axes has unit length, so it ends exactly on the
circle.

**Covers**: `Rendering-Abstractions-NotationMetrics-CrossedCircleBadge`.

#### Corner-icon polygons are canonical

Test `CornerIconVertices_Send_IsConvexPentagonInsideIconBox` asserts the send icon has five vertices,
all within the 12x9 icon box, with its rightmost vertex at (`CornerIconWidth`, half the height) and its
leftmost edge at zero. Test `CornerIconVertices_Accept_HasNotchInLeftEdge` asserts the accept icon has
one more vertex than the send icon and that the extra final vertex is the notch at
(`CornerIconNotchDepth`, half the height). Test `CornerIconVertices_None_IsEmpty` asserts no icon yields
no vertices.

**Covers**: `Rendering-Abstractions-NotationMetrics-CornerIcon`.

#### Diamond box vertices are the edge midpoints

`NotationMetrics_DiamondBoxVertices_AreTheBoxEdgeMidpointsTopRightBottomLeft` asserts the four vertices of a box
in order, `NotationMetrics_DiamondBoxVertices_ZeroSizeBox_CollapsesToThePoint` asserts a zero-size box collapses
without throwing, and `BoxMetrics_TitleCursorTop_Diamond_MatchesARectangle` asserts a diamond's title cursor
equals a rectangle's.

**Covers**: `Rendering-Abstractions-NotationMetrics-DiamondBoxVertices`.

### Requirements Coverage

- **`Rendering-Abstractions-NotationMetrics-TriangleDimensions`**: TriangleFamily_HasCanonicalValues,
  TriangleVertices_ReproduceSvgBoxPoints
- **`Rendering-Abstractions-NotationMetrics-TriangleApexOnEndpoint`**: TriangleVertices_Apex_LandsOnEndpoint
- **`Rendering-Abstractions-NotationMetrics-TipRetraction`**: TipRetraction_IsHalfStrokeTimesTipFactor,
  TipRetraction_ScalesWithThemeStrokeWidth_AndRejectsNullTheme,
  MarkerAnchor_KeepsRetractedAnchorInsideBox_AndFeedsRenderedLength
- **`Rendering-Abstractions-NotationMetrics-DiamondDimensions`**: Diamond_HasCanonicalValues,
  DiamondVertices_ReproduceSvgBoxPoints
- **`Rendering-Abstractions-NotationMetrics-DiamondFarPointOnEndpoint`**: DiamondVertices_FarPoint_LandsOnEndpoint
- **`Rendering-Abstractions-NotationMetrics-CircleBarGeometry`**: CircleAndBar_HaveCanonicalValues
- **`Rendering-Abstractions-NotationMetrics-Crossbar`**: Crossbar_SitsBehindTriangleBase
- **`Rendering-Abstractions-NotationMetrics-AlongLineLength`**: AlongLineLength_MatchesMarkerBox,
  AlongLineLength_NewStyles_MatchMarkerBoxesAndLegacyUnchanged
- **`Rendering-Abstractions-NotationMetrics-PortSquare`**: Port_SizeIsTwiceHalfSize
- **`Rendering-Abstractions-NotationMetrics-FolderTab`**: FolderTab_HasDocumentedConstants
- **`Rendering-Abstractions-NotationMetrics-NoteFold`**: NoteFold_HasDocumentedConstants
- **`Rendering-Abstractions-NotationMetrics-RoundedRectCorner`**: RoundedRectRadius_IsThemeRadiusTimesFactor
- **`Rendering-Abstractions-NotationMetrics-RejectNullTheme`**: RoundedRectRadius_NullTheme_ThrowsArgumentNullException
- **`Rendering-Abstractions-NotationMetrics-Badge`**: BadgeFractions_HaveDocumentedValues
- **`Rendering-Abstractions-NotationMetrics-LabelBackground`**: LabelBackground_ExtentMatchesInset
- **`Rendering-Abstractions-NotationMetrics-DottedTriangleMarkers`**: DottedTriangles_HaveCanonicalValues,
  DotCenters_TwoDots_StraddleLineBehindBase, DotCenters_FourDots_FormTwoByTwoGrid,
  DotCenters_NonDottedStyles_AreEmpty, DotCenters_StayInsideMarkerBox,
  TriangleFamilyBox_DottedVariants_KeepTriangleTipAnchored
- **`Rendering-Abstractions-NotationMetrics-CircledPlusMarker`**: CircledPlus_HasCanonicalValues
- **`Rendering-Abstractions-NotationMetrics-MarkerScale`**:
  MarkerScale_IsThemeStrokeWidth_AndMarkerStrokeIsScaledStroke
- **`Rendering-Abstractions-NotationMetrics-RenderedAlongLineLength`**:
  RenderedAlongLineLength_ScalesNotationLengthByStrokeWidth, RenderedAlongLineLength_NullTheme_Throws
- **`Rendering-Abstractions-NotationMetrics-AcrossHalfWidth`**:
  AcrossHalfWidth_EveryStyle_MatchesWidestPoint
- **`Rendering-Abstractions-NotationMetrics-ThickBarBadge`**:
  ThickBarAndCrossedCircle_HaveCanonicalValues
- **`Rendering-Abstractions-NotationMetrics-CrossedCircleBadge`**:
  ThickBarAndCrossedCircle_HaveCanonicalValues
- **`Rendering-Abstractions-NotationMetrics-CornerIcon`**:
  CornerIconVertices_Send_IsConvexPentagonInsideIconBox, CornerIconVertices_Accept_HasNotchInLeftEdge,
  CornerIconVertices_None_IsEmpty
- **`Rendering-Abstractions-NotationMetrics-DiamondBoxVertices`**:
  NotationMetrics_DiamondBoxVertices_AreTheBoxEdgeMidpointsTopRightBottomLeft,
  NotationMetrics_DiamondBoxVertices_ZeroSizeBox_CollapsesToThePoint,
  BoxMetrics_TitleCursorTop_Diamond_MatchesARectangle
