## Connector Label Placer Unit Verification

Part of the Rendering Abstractions Verification.

This document describes the verification design for the connector-label-placer unit of the
`DemaConsulting.Rendering.Abstractions` system. It maps every connector-label-placer unit requirement
to at least one named test scenario so a reviewer can confirm coverage without reading the test code.
The verification strategy, test environment, and acceptance criteria are described in the
system verification document; the test project is
`DemaConsulting.Rendering.Abstractions.Tests` (`ConnectorLabelPlacerTests.cs` and
`ConnectorLabelPlacerObstacleTests.cs`).

### Verification Approach

The connector-label-placer unit is verified with in-process xUnit unit tests that call
`ConnectorLabelPlacer.Place` directly with hand-constructed `LayoutLine` inputs. No mocking or
stubbing is required because the class is a pure, static function of its inputs. The tests
construct `LayoutLine` instances with explicit `MidpointLabel` values and `Waypoints` sequences to
cover the omit-unlabelled, longest-segment-midpoint, and collision-avoidance requirements. The
box-aware overload is verified in `ConnectorLabelPlacerObstacleTests.cs` with hand-built horizontal
and vertical lines and hand-built leaf and container `LayoutBox` obstacles, asserting the chosen label
rectangle against the obstacle rectangles (overlap and containment) and against the two-argument
overload's result.

### Test Environment

- **Framework**: xUnit v4 running under the .NET SDK on `net8.0`, `net9.0`, `net10.0`, and `net11.0`.
- **Execution**: `dotnet test` invoked by `build.ps1` and by the CI pipeline.
- **Test project**: `DemaConsulting.Rendering.Abstractions.Tests` (`ConnectorLabelPlacerTests.cs` and
  `ConnectorLabelPlacerObstacleTests.cs`).
- **External dependencies**: none; the tests use only in-memory geometry inputs.

### Acceptance Criteria

The unit is considered verified when every scenario listed below passes. Each test must produce the
documented label position (or omit the line from the result); any wrong coordinate, wrong number of
entries in the returned dictionary, or unexpected exception constitutes a failure.

### Test Scenarios

#### Unlabelled line is omitted

Test `Place_LineWithoutLabel_IsOmitted` places labels for a single line whose `MidpointLabel` is null
and asserts the result is empty.

**Covers**: `Rendering-Abstractions-ConnectorLabelPlacer-OmitUnlabelled`.

#### Label uses the longest segment midpoint

Test `Place_SingleLine_UsesLongestSegmentMidpoint` places a label for a line with a short vertical
stub followed by a long horizontal run and asserts the label lands at the midpoint of the long run
(100, 10).

**Covers**: `Rendering-Abstractions-ConnectorLabelPlacer-LongestSegment`.

#### Colliding labels are separated

Test `Place_CollidingLabels_AreSeparated` places labels for two lines whose longest-segment midpoints
coincide and asserts the first keeps the preferred midpoint (100, 0) while the second is nudged to a
different Y so the two do not overlap.

**Covers**: `Rendering-Abstractions-ConnectorLabelPlacer-AvoidOverlap`.

#### Placement exposes each label's extent

Test `Place_SingleLine_ExposesPositiveHalfWidthAndHalfHeight` asserts a placed label's `HalfWidth`
and `HalfHeight` are both positive, and `Place_LongerLabel_HasLargerHalfWidth` asserts a longer
`MidpointLabel` string yields a larger `HalfWidth` than a shorter one, confirming the extent is
genuinely derived from the label's estimated rendered size (not a fixed placeholder), which a
caller needs to compute the label's full bounding box for canvas-growth purposes. Test
`Place_TwoArgOverload_ReturnsLabelPlacementWithTwoVariableDeconstruction` pins the two-argument overload's
shipped result shape (`IReadOnlyDictionary<LayoutLine, LabelPlacement>`), asserts the exact centre and
half-extents, and confirms `var (x, y) = result[line]` (the two-variable `LabelPlacement` deconstruction)
yields the same centre coordinates.

**Covers**: `Rendering-Abstractions-ConnectorLabelPlacer-ExposesLabelExtent`.

#### EstimateLabelHeight matches Place's internal formula and grows with font size

Test `EstimateLabelHeight_MatchesPlaceHalfHeightDoubled` places a labeled line via `Place`, then
asserts `EstimateLabelHeight(fontSize)` equals exactly twice that placement's `HalfHeight`,
confirming the public helper returns the same full label-height formula `Place` uses internally
(not an independently-drifting approximation). Test `EstimateLabelHeight_IsMonotonicInFontSize`
asserts a larger `fontSize` yields a larger estimated height.

**Covers**: `Rendering-Abstractions-ConnectorLabelPlacer-EstimateLabelHeight`.

#### EstimateLabelWidth matches Place's internal formula and grows with font size and text length

Test `EstimateLabelWidth_MatchesPlaceHalfWidthDoubled` places a labeled line via `Place`, then
asserts `EstimateLabelWidth(text, fontSize)` equals exactly twice that placement's `HalfWidth`,
confirming the public helper returns the same full label-width formula `Place` uses internally
(not an independently-drifting approximation). Test `EstimateLabelWidth_IsMonotonicInFontSize`
asserts a larger `fontSize` yields a larger estimated width for the same text, and test
`EstimateLabelWidth_IsMonotonicInTextLength` asserts a longer label string yields a larger
estimated width for the same font size — confirming the estimate is genuinely text-dependent,
unlike `EstimateLabelHeight`, which is a pure function of font size alone.

**Covers**: `Rendering-Abstractions-ConnectorLabelPlacer-EstimateLabelWidth`.

#### Box-aware placement avoids leaf and container borders

Test `PlaceWithObstacles_NoObstacles_MatchesLegacyOverload` places three labeled lines (two with
coincident midpoints) with no obstacles and asserts every `LabelPlacement` equals the two-argument
overload's. Test `PlaceWithObstacles_LeafBoxOnMidpoint_SlidesLabelClear` puts a leaf box on a horizontal
line's midpoint and asserts the chosen label rectangle does not overlap the box, stays on the line (Y
0), and has moved off the midpoint X. Test `PlaceWithObstacles_LabelFullyInsideContainer_IsAllowed`
asserts a label that fits wholly inside a container box stays at the segment midpoint. Test
`PlaceWithObstacles_LabelStraddlingContainerBorder_IsRejected` puts a container border across the
midpoint and asserts the chosen rectangle is either fully inside the container or fully outside it.
Test `PlaceWithObstacles_EverythingBlocked_FallsBackToObstacleFreePlacement` covers the line with a leaf
box larger than every candidate and asserts the placement equals the obstacle-free result, so the label
is not lost. Test `PlaceWithObstacles_SameInput_IsDeterministic` places the same input twice and asserts
identical results.

**Covers**: `Rendering-Abstractions-ConnectorLabelPlacer-AvoidBoxes`.

#### Labels keep clear of the end decoration

Test `PlaceWithObstacles_SlideAvoidsEndMarkerZone` places a label on a vertical line that ends in an
`OpenChevron`, blocks the midpoint with a leaf box, and asserts the chosen rectangle overlaps neither the
box nor the footprint of the end marker (its rendered along-line length back from the endpoint). Test
`PlaceWithObstacles_SlideAvoidsEndMarkerZone_UsesSuppliedMarkerScale` repeats this with a thicker-stroke
theme, passing its marker scale, and asserts the footprint grows with the scale (a hard-coded 1.5 would
let the label sit inside it). Test `Place_LabelOnShortRun_StaysClearOfEndMarkerFootprint` (target and source
end) places a long label on an 80 px run ending in a filled arrow, with no obstacle boxes, and asserts the
label no longer sits on the arrowhead. Test `Place_LabelClearOfEndMarker_KeepsMidpoint` asserts a
label that already clears the decoration keeps the exact midpoint. Test
`Place_RunTooShortForLabelAndMarker_KeepsLabelOnItsLine` asserts that on a 40 px run too short for both, the
label keeps its midpoint on the line instead of being nudged to another row.

**Covers**: `Rendering-Abstractions-ConnectorLabelPlacer-AvoidEndDecoration`.

#### Box-aware overload rejects null arguments

Test `PlaceWithObstacles_NullArguments_Throw` calls the overload with a `null` obstacle sequence and with
a `null` line sequence and asserts each raises `ArgumentNullException`.

**Covers**: `Rendering-Abstractions-ConnectorLabelPlacer-RejectNullObstacleArguments`.

#### End labels sit near their end, beside the line, clear of everything

`ConnectorLabelPlacerEndLabelTests` drive `PlaceEndLabels` directly. `PlaceEndLabels_NoEndLabels_ReturnsEmpty` asserts
nothing is returned without labels. `PlaceEndLabels_HorizontalLine_PrimaryAboveSecondaryBelowNearEachEnd` and
`PlaceEndLabels_VerticalLine_PrimaryRightSecondaryLeft` assert the notation sides and that each label lies in the half
of the line next to its own end; `PlaceEndLabels_RightToLeftLine_PrimaryStillAbove` asserts a reversed line keeps the
primary label above. `PlaceEndLabels_LabelsStayClearOfTheBoxAtTheEnd` and `PlaceEndLabels_LabelsStayClearOfTheEndMarker`
assert the box and marker footprints are never overlapped, and
`PlaceEndLabels_LabelsDoNotOverlapEachOtherOrOccupiedRectangles` asserts eight labels on two stacked lines and a
supplied midpoint rectangle never overlap. `PlaceEndLabels_IsDeterministic` asserts repeatable output,
`PlaceEndLabels_LabelWiderThanTheRun_StillPlacedAtTheNominalPosition` asserts a label that cannot fit is still placed,
`PlaceEndLabels_NullOrEmptySlots_AreSkipped` asserts blank slots yield nothing, and `PlaceEndLabels_NullArguments_Throw`
asserts null arguments are rejected. `PlaceEndLabels_VerticalTargetEndOnPort_KeepsFourPixelsFromRetractedMarker` asserts
a marker end on a port is measured from its retracted tip and both slots keep 4 px from it,
`PlaceEndLabels_VerticalRun_SecondaryKeepsFourPixelsFromConnector` asserts both vertical slots keep 4 px from their own
connector, `PlaceEndLabels_OccupiedRectangle_KeepsFourPixelsOfClearance` asserts a rectangle touching the nominal slot
is kept 4 px clear, `PlaceEndLabels_StackedEndAndPortLabel_KeepFourPixelGap` asserts both slots keep 4 px from a port
glyph and port label, and `PlaceEndLabels_NoPorts_PositionsUnchanged` pins the nominal position when no ports are given.
The gallery tests `Gallery_SysmlPortsHorizontal_RendersSvgAndPngWithoutLabelOverlaps`,
`Gallery_SysmlPortsVertical_RendersSvgAndPngWithoutLabelOverlaps`, and
`Gallery_ConnectionEndLabels_RendersSvgAndPngWithoutLabelOverlaps` assert at least 3.5 px (4 px less estimate tolerance)
between every end label and port glyph, port label, connector segment, retracted end marker, and other end label.
`Place_MidpointPlacements_UnchangedByEndLabels` asserts a line's midpoint placement is identical with and without end
labels.

**Covers**: `Rendering-Abstractions-ConnectorLabelPlacer-EndLabelPlacement`,
`Rendering-Abstractions-ConnectorLabelPlacer-EndLabelsKeepMidpointPlacementsUnchanged`.

#### Shared label resolution

`PortOccupiedRects_OutwardLabeledPort_ReturnsGlyphAndPaddedLabel` asserts an outward-labeled port yields its glyph
rectangle plus its label rectangle padded by 5 px along the text, and a plain port only its glyph.
`ResolveLabels_PortsAndLabels_MatchesManualComposition` asserts the resolver returns exactly the midpoint placements,
port rectangles and end labels (placed around the midpoints and port rectangles) a manual composition of `Place`,
`PortOccupiedRects` and `PlaceEndLabels` gives, and `ResolveLabels_NullArguments_Throw` asserts null arguments are
rejected. The rendered-output tests `RenderedSvg_EndLabels_AreAtThePositionsTheLayoutPredicted` and
`RenderedSvg_OutwardPortLabelsAndEndLabels_AreInsideCanvas` (gallery project, `RenderedLabelContainmentTests`) assert
that the end-label text elements of a rendered SVG sit where the shared resolver predicts and that every port glyph,
port label and end label of the rendered output is inside the canvas.

**Covers**: `Rendering-Abstractions-ConnectorLabelPlacer-SharedLabelResolution`.

#### Leading overhang

`MeasureLeadingOverhang_ContentInside_ReturnsZero` asserts content inside the canvas yields no translation.
`MeasureLeadingOverhang_LabelsPastOrigin_ReturnsOverhangForTheTheme` asserts a midpoint label and an outward port label
past the left edge produce a positive overhang and that a larger body font overhangs further, and
`MeasureLeadingOverhang_NullArguments_Throw` asserts null arguments are rejected.

**Covers**: `Rendering-Abstractions-ConnectorLabelPlacer-LeadingOverhang`.

### Requirements Coverage

- **`Rendering-Abstractions-ConnectorLabelPlacer-OmitUnlabelled`**: Place_LineWithoutLabel_IsOmitted
- **`Rendering-Abstractions-ConnectorLabelPlacer-LongestSegment`**: Place_SingleLine_UsesLongestSegmentMidpoint
- **`Rendering-Abstractions-ConnectorLabelPlacer-AvoidOverlap`**: Place_CollidingLabels_AreSeparated
- **`Rendering-Abstractions-ConnectorLabelPlacer-ExposesLabelExtent`**:
  Place_SingleLine_ExposesPositiveHalfWidthAndHalfHeight, Place_LongerLabel_HasLargerHalfWidth,
  Place_TwoArgOverload_ReturnsLabelPlacementWithTwoVariableDeconstruction
- **`Rendering-Abstractions-ConnectorLabelPlacer-EstimateLabelHeight`**:
  EstimateLabelHeight_MatchesPlaceHalfHeightDoubled, EstimateLabelHeight_IsMonotonicInFontSize
- **`Rendering-Abstractions-ConnectorLabelPlacer-EstimateLabelWidth`**:
  EstimateLabelWidth_MatchesPlaceHalfWidthDoubled, EstimateLabelWidth_IsMonotonicInFontSize,
  EstimateLabelWidth_IsMonotonicInTextLength
- **`Rendering-Abstractions-ConnectorLabelPlacer-AvoidBoxes`**:
  PlaceWithObstacles_NoObstacles_MatchesLegacyOverload, PlaceWithObstacles_LeafBoxOnMidpoint_SlidesLabelClear,
  PlaceWithObstacles_LabelFullyInsideContainer_IsAllowed,
  PlaceWithObstacles_LabelStraddlingContainerBorder_IsRejected,
  PlaceWithObstacles_EverythingBlocked_FallsBackToObstacleFreePlacement,
  PlaceWithObstacles_SameInput_IsDeterministic
- **`Rendering-Abstractions-ConnectorLabelPlacer-AvoidEndDecoration`**:
  PlaceWithObstacles_SlideAvoidsEndMarkerZone, PlaceWithObstacles_SlideAvoidsEndMarkerZone_UsesSuppliedMarkerScale,
  Place_LabelOnShortRun_StaysClearOfEndMarkerFootprint, Place_LabelClearOfEndMarker_KeepsMidpoint,
  Place_RunTooShortForLabelAndMarker_KeepsLabelOnItsLine
- **`Rendering-Abstractions-ConnectorLabelPlacer-RejectNullObstacleArguments`**:
  PlaceWithObstacles_NullArguments_Throw
- **`Rendering-Abstractions-ConnectorLabelPlacer-EndLabelPlacement`**:
  PlaceEndLabels_NoEndLabels_ReturnsEmpty, PlaceEndLabels_HorizontalLine_PrimaryAboveSecondaryBelowNearEachEnd,
  PlaceEndLabels_VerticalLine_PrimaryRightSecondaryLeft, PlaceEndLabels_RightToLeftLine_PrimaryStillAbove,
  PlaceEndLabels_LabelsStayClearOfTheBoxAtTheEnd, PlaceEndLabels_LabelsStayClearOfTheEndMarker,
  PlaceEndLabels_LabelsDoNotOverlapEachOtherOrOccupiedRectangles, PlaceEndLabels_IsDeterministic,
  PlaceEndLabels_LabelWiderThanTheRun_StillPlacedAtTheNominalPosition, PlaceEndLabels_NullOrEmptySlots_AreSkipped,
  PlaceEndLabels_NullArguments_Throw,
  PlaceEndLabels_VerticalTargetEndOnPort_KeepsFourPixelsFromRetractedMarker,
  PlaceEndLabels_VerticalRun_SecondaryKeepsFourPixelsFromConnector,
  PlaceEndLabels_OccupiedRectangle_KeepsFourPixelsOfClearance,
  PlaceEndLabels_StackedEndAndPortLabel_KeepFourPixelGap, PlaceEndLabels_NoPorts_PositionsUnchanged
- **`Rendering-Abstractions-ConnectorLabelPlacer-EndLabelsKeepMidpointPlacementsUnchanged`**:
  Place_MidpointPlacements_UnchangedByEndLabels
- **`Rendering-Abstractions-ConnectorLabelPlacer-SharedLabelResolution`**:
  ResolveLabels_PortsAndLabels_MatchesManualComposition,
  PortOccupiedRects_OutwardLabeledPort_ReturnsGlyphAndPaddedLabel, ResolveLabels_NullArguments_Throw,
  RenderedSvg_EndLabels_AreAtThePositionsTheLayoutPredicted, RenderedSvg_OutwardPortLabelsAndEndLabels_AreInsideCanvas
- **`Rendering-Abstractions-ConnectorLabelPlacer-LeadingOverhang`**:
  MeasureLeadingOverhang_ContentInside_ReturnsZero, MeasureLeadingOverhang_LabelsPastOrigin_ReturnsOverhangForTheTheme,
  MeasureLeadingOverhang_NullArguments_Throw
