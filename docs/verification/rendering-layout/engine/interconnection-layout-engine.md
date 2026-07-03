### InterconnectionLayoutEngine Unit Verification

Part of the Rendering Layout Verification.

This document maps the InterconnectionLayoutEngine unit requirements to named test scenarios.

#### Verification Approach

`InterconnectionLayoutEngine` is verified by direct xUnit unit tests that call
`Place(nodes, edges, direction)` on synthetic `IReadOnlyList<LayerNode>` /
`IReadOnlyList<LayerEdge>` inputs. The tests run the real underlying `LayeredPipeline` end-to-end
(no stage is mocked) so layering, non-overlap, dummy-node handling, waypoint emission, direction
handling, validation, and determinism are all measured on production output.

#### Test Environment

- **Framework**: xUnit v3 running on the .NET SDK.
- **Runner**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Project**: `test/DemaConsulting.Rendering.Layout.Tests/Engine/InterconnectionLayoutEngineTests.cs`.
- **Dependencies**: no external services, files, or network access; every test constructs its own
  in-memory `LayoutGraph` and options.
- **Isolation**: each test builds its own inputs; the engine and pipeline are stateless between
  calls.

#### Acceptance Criteria

A verification run passes when every named scenario below asserts without unexpected exception, and
the referenced tests cover each `Rendering-Layout-InterconnectionEngine-*` requirement. Any drift
in monotonic layer indices along a chain, overlapping placed rectangles, dummy nodes leaking into
the returned rectangle list, missing or non-orthogonal edge waypoints, direction handling
transposing incorrectly, or non-deterministic geometry for identical input constitutes a failure.

#### Test Scenarios

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
- **Waypoints / acyclic mapping** (`Rendering-Layout-InterconnectionEngine-Waypoints-AcyclicMapping`):
  `Place_CyclicGraph_ReversesBackEdgeAndProducesWaypoint` confirms cycle breaking reverses a back
  edge, reports an acyclic edge set, and returns one waypoint list index-aligned with that retained
  edge set.
- **Waypoints / straight span-one** (`Rendering-Layout-InterconnectionEngine-Waypoints-StraightSpanOne`):
  `Place_SingleEdge_ProducesStraightTwoWaypointPath` confirms a span-one edge produces a straight
  two-waypoint path.
- **Waypoints / long-edge routing** (`Rendering-Layout-InterconnectionEngine-Waypoints-LongEdgeRouting`):
  `Place_LongEdge_RoutesViaDummyNodesWithinBounds` confirms a long edge routes through dummy nodes
  and stays within the layout bounds.
- **Direction / requested flow** (`Rendering-Layout-InterconnectionEngine-Direction-RequestedFlow`):
  `Place_DownDirection_TransposesTotalsRelativeToRight` confirms a downward flow stacks the chain in
  increasing Y rather than the default rightward orientation.
- **Direction / transposed totals** (`Rendering-Layout-InterconnectionEngine-Direction-TransposedTotals`):
  `Place_DownDirection_TransposesTotalsRelativeToRight` confirms the downward flow reports
  transposed total dimensions relative to the default rightward flow.
- **Direction / defaults to right** (`Rendering-Layout-InterconnectionEngine-Direction-DefaultsToRight`):
  `Place_DefaultDirection_MatchesRightFlow` confirms omitting the optional direction argument
  produces the same geometry and totals as explicitly requesting `LayoutDirection.Right`.
- **Deterministic** (`Rendering-Layout-InterconnectionEngine-Deterministic`):
  `Place_RepeatedInvocation_ProducesIdenticalGeometry` confirms identical input produces identical
  rects, totals, retained acyclic edges, layer indices, and connector waypoints on repeated
  invocation.
- **Validation** (supporting the documented error contract):
  `Place_NullNodes_ThrowsArgumentNullException` and `Place_NullEdges_ThrowsArgumentNullException`
  confirm null inputs are rejected before any pipeline stage executes.

#### Requirements Coverage

- **`Rendering-Layout-InterconnectionEngine-Layering`**:
  Place_LinearChain_MonotonicLayerAssignment, Place_WorkstationTopology_CorrectLayersAndNoOverlap
- **`Rendering-Layout-InterconnectionEngine-NonOverlapping`**:
  Place_WorkstationTopology_CorrectLayersAndNoOverlap
- **`Rendering-Layout-InterconnectionEngine-DummyNodes`**:
  Place_LongEdge_RectCountEqualsInputNodeCount, Place_LongEdge_RoutesViaDummyNodesWithinBounds
- **`Rendering-Layout-InterconnectionEngine-Waypoints-AcyclicMapping`**:
  Place_CyclicGraph_ReversesBackEdgeAndProducesWaypoint
- **`Rendering-Layout-InterconnectionEngine-Waypoints-StraightSpanOne`**:
  Place_SingleEdge_ProducesStraightTwoWaypointPath
- **`Rendering-Layout-InterconnectionEngine-Waypoints-LongEdgeRouting`**:
  Place_LongEdge_RoutesViaDummyNodesWithinBounds
- **`Rendering-Layout-InterconnectionEngine-Direction-RequestedFlow`**:
  Place_DownDirection_TransposesTotalsRelativeToRight
- **`Rendering-Layout-InterconnectionEngine-Direction-TransposedTotals`**:
  Place_DownDirection_TransposesTotalsRelativeToRight
- **`Rendering-Layout-InterconnectionEngine-Direction-DefaultsToRight`**:
  Place_DefaultDirection_MatchesRightFlow
- **`Rendering-Layout-InterconnectionEngine-Deterministic`**:
  Place_RepeatedInvocation_ProducesIdenticalGeometry
