# InterconnectionLayoutEngine Unit Verification

Part of the Rendering Layout Verification.

This document maps the InterconnectionLayoutEngine unit requirements to named test scenarios.

## InterconnectionLayoutEngine Unit Scenarios

- **Layering** (`Rendering-Layout-InterconnectionEngine-Layering`):
  `Place_LinearChain_MonotonicLayerAssignment` asserts monotonic layer indices along a chain, and
  `Place_WorkstationTopology_CorrectLayersAndNoOverlap` verifies correct layers on a representative
  topology.
- **Non-overlapping** (`Rendering-Layout-InterconnectionEngine-NonOverlapping`):
  `Place_WorkstationTopology_CorrectLayersAndNoOverlap` asserts placed rectangles do not overlap.
- **Dummy nodes** (`Rendering-Layout-InterconnectionEngine-DummyNodes`):
  `Place_LongEdge_RectCountEqualsInputNodeCount` confirms dummy nodes are excluded from the returned
  rectangles, and `Place_LongEdge_RoutesViaDummyNodesWithinBounds` confirms long edges route through
  them within bounds.
- **Waypoints** (`Rendering-Layout-InterconnectionEngine-Waypoints`):
  `Place_SingleEdge_ProducesStraightTwoWaypointPath` confirms a span-one edge produces a straight
  two-waypoint path; `Place_LongEdge_RoutesViaDummyNodesWithinBounds` confirms a long edge's route.
- **Deterministic** (`Rendering-Layout-InterconnectionEngine-Deterministic`): the layering scenarios
  `Place_LinearChain_MonotonicLayerAssignment` and
  `Place_WorkstationTopology_CorrectLayersAndNoOverlap` assert fixed, reproducible geometry for fixed
  input.

## Requirements Coverage

- **`Rendering-Layout-InterconnectionEngine-Layering`**:
  Place_LinearChain_MonotonicLayerAssignment, Place_WorkstationTopology_CorrectLayersAndNoOverlap
- **`Rendering-Layout-InterconnectionEngine-NonOverlapping`**:
  Place_WorkstationTopology_CorrectLayersAndNoOverlap
- **`Rendering-Layout-InterconnectionEngine-DummyNodes`**:
  Place_LongEdge_RectCountEqualsInputNodeCount, Place_LongEdge_RoutesViaDummyNodesWithinBounds
- **`Rendering-Layout-InterconnectionEngine-Waypoints`**:
  Place_SingleEdge_ProducesStraightTwoWaypointPath, Place_LongEdge_RoutesViaDummyNodesWithinBounds
- **`Rendering-Layout-InterconnectionEngine-Deterministic`**:
  Place_LinearChain_MonotonicLayerAssignment, Place_WorkstationTopology_CorrectLayersAndNoOverlap
