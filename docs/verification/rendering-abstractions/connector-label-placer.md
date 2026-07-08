## Connector Label Placer Unit Verification

Part of the Rendering Abstractions Verification.

This document describes the verification design for the connector-label-placer unit of the
`DemaConsulting.Rendering.Abstractions` system. It maps every connector-label-placer unit requirement
to at least one named test scenario so a reviewer can confirm coverage without reading the test code.
The verification strategy, test environment, and acceptance criteria are described in the
system verification document; the test project is
`DemaConsulting.Rendering.Abstractions.Tests` (`ConnectorLabelPlacerTests.cs`).

### Verification Approach

The connector-label-placer unit is verified with in-process xUnit unit tests that call
`ConnectorLabelPlacer.Place` directly with hand-constructed `LayoutLine` inputs. No mocking or
stubbing is required because the class is a pure, static function of its inputs. The tests
construct `LayoutLine` instances with explicit `MidpointLabel` values and `Waypoints` sequences to
cover the omit-unlabelled, longest-segment-midpoint, and collision-avoidance requirements.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK on `net8.0`, `net9.0`, and `net10.0`.
- **Execution**: `dotnet test` invoked by `build.ps1` and by the CI pipeline.
- **Test project**: `DemaConsulting.Rendering.Abstractions.Tests` (`ConnectorLabelPlacerTests.cs`).
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
caller needs to compute the label's full bounding box for canvas-growth purposes.

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

### Requirements Coverage

- **`Rendering-Abstractions-ConnectorLabelPlacer-OmitUnlabelled`**: Place_LineWithoutLabel_IsOmitted
- **`Rendering-Abstractions-ConnectorLabelPlacer-LongestSegment`**: Place_SingleLine_UsesLongestSegmentMidpoint
- **`Rendering-Abstractions-ConnectorLabelPlacer-AvoidOverlap`**: Place_CollidingLabels_AreSeparated
- **`Rendering-Abstractions-ConnectorLabelPlacer-ExposesLabelExtent`**:
  Place_SingleLine_ExposesPositiveHalfWidthAndHalfHeight, Place_LongerLabel_HasLargerHalfWidth
- **`Rendering-Abstractions-ConnectorLabelPlacer-EstimateLabelHeight`**:
  EstimateLabelHeight_MatchesPlaceHalfHeightDoubled, EstimateLabelHeight_IsMonotonicInFontSize
- **`Rendering-Abstractions-ConnectorLabelPlacer-EstimateLabelWidth`**:
  EstimateLabelWidth_MatchesPlaceHalfWidthDoubled, EstimateLabelWidth_IsMonotonicInFontSize,
  EstimateLabelWidth_IsMonotonicInTextLength
