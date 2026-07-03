## Connector Label Placer Unit Verification

Part of the Rendering Abstractions Verification.

This document describes the verification design for the connector-label-placer unit of the
`DemaConsulting.Rendering.Abstractions` system. It maps every connector-label-placer unit requirement
to at least one named test scenario so a reviewer can confirm coverage without reading the test code.
The verification strategy, test environment, and acceptance criteria are described in the
system verification document; the test project is
`DemaConsulting.Rendering.Abstractions.Tests` (`ConnectorLabelPlacerTests.cs`).

### Connector Label Placer Unit Scenarios

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

### Requirements Coverage

- **`Rendering-Abstractions-ConnectorLabelPlacer-OmitUnlabelled`**: Place_LineWithoutLabel_IsOmitted
- **`Rendering-Abstractions-ConnectorLabelPlacer-LongestSegment`**: Place_SingleLine_UsesLongestSegmentMidpoint
- **`Rendering-Abstractions-ConnectorLabelPlacer-AvoidOverlap`**: Place_CollidingLabels_AreSeparated
