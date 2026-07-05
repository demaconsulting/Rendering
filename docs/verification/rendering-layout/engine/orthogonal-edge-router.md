### OrthogonalEdgeRouter Unit Verification

Part of the Rendering Layout Verification.

This document maps the OrthogonalEdgeRouter unit requirements to named test scenarios.

#### Verification Approach

`OrthogonalEdgeRouter` is a stateless static engine, so verification is by direct xUnit unit tests
that call `Route` and `RouteWithStatus` on synthetic anchor / obstacle inputs. No mocks are used;
the tests observe the real grid construction, A\*-style search, clearance-retry ladder, and cost-
band biasing so orthogonality, obstacle avoidance, clearance, perpendicular ends, and crossing
status are all measured on production output.

#### Test Environment

- **Framework**: xUnit v3 running on the .NET SDK.
- **Runner**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Project**: `test/DemaConsulting.Rendering.Layout.Tests/Engine/OrthogonalEdgeRouterTests.cs`.
- **Dependencies**: no external services, files, or network access; every test constructs its own
  in-memory `Point2D` anchors and `Rect` obstacle lists.
- **Isolation**: each test builds its own inputs; the engine holds no state between calls.

#### Acceptance Criteria

A verification run passes when every named scenario below asserts without unexpected exception, and
the referenced tests cover each `Rendering-Layout-OrthogonalEdgeRouter-*` requirement. Any drift in
the orthogonality of returned waypoints, entry of a segment into an obstacle interior when a clean
route exists, failure to keep the requested clearance, non-perpendicular exit or entry at a
supplied side, incorrect `Crossed` flag, or lost cost-band bias constitutes a failure.

#### Test Scenarios

- **Orthogonal path** (`Rendering-Layout-OrthogonalEdgeRouter-Orthogonal`):
  `Route_NoObstacles_ProducesOrthogonalPath` asserts consecutive waypoints share an X or Y
  coordinate; `Route_AlignedEndpoints_ProducesStraightLine` confirms aligned anchors yield a
  straight two-point path.
- **Obstacle avoidance** (`Rendering-Layout-OrthogonalEdgeRouter-AvoidObstacles`):
  `Route_ObstacleBetween_RoutesAround` and `Route_MultipleObstacles_RemainsValid` verify the path
  never enters an obstacle interior; `RouteWithStatus_ObstacleBetween_RoutesAroundWithoutCrossing`
  confirms the crossing flag stays clear when a clean detour exists.
- **Clearance** (`Rendering-Layout-OrthogonalEdgeRouter-Clearance`):
  `RouteWithStatus_CleanRoute_KeepsClearanceFromObstacles` asserts routed segments keep the
  requested clearance from obstacles.
- **Perpendicular ends** (`Rendering-Layout-OrthogonalEdgeRouter-PerpendicularEnds`):
  `Route_WithSourceSide_LeavesPerpendicular` and `Route_WithTargetSide_EntersPerpendicular` confirm
  the connector leaves/enters an anchor perpendicular to the given box side.
- **Crossing status** (`Rendering-Layout-OrthogonalEdgeRouter-CrossingStatus`):
  `RouteWithStatus_NoBlockingObstacle_ReportsNotCrossed` reports a clean route, and
  `RouteWithStatus_TargetEnclosedByObstacle_ReportsCrossed` reports a forced crossing for an enclosed
  target.
- **Cost bands** (`Rendering-Layout-OrthogonalEdgeRouter-CostBands`):
  `RouteWithStatus_HighwayBand_PrefersBandedDetour` confirms the router prefers a discounted band
  over an equal-length alternative.
- **No redundant waypoint revisit** (`Rendering-Layout-OrthogonalEdgeRouter-NoWaypointRevisit`):
  `RouteWithStatus_SoftObstacleDetour_DoesNotRevisitWaypoint` confirms a soft-obstacle-driven detour
  does not publish a leave-and-return loop that revisits one waypoint before continuing.
- **No extended soft-obstacle overlap** (`Rendering-Layout-OrthogonalEdgeRouter-AvoidsExtendedSoftOverlap`):
  `RouteWithStatus_LongSoftObstacleOverlap_PrefersAlternateLane` confirms the router detours to an
  alternate lane instead of riding a soft obstacle that occupies the natural corridor for an extended
  span; `RouteWithStatus_ShortSoftObstacleOverlap_KeepsStraightRoute` confirms a short, incidental
  overlap still stays cheaper than the detour and is tolerated as before.

#### Requirements Coverage

- **`Rendering-Layout-OrthogonalEdgeRouter-Orthogonal`**:
  Route_NoObstacles_ProducesOrthogonalPath, Route_AlignedEndpoints_ProducesStraightLine
- **`Rendering-Layout-OrthogonalEdgeRouter-AvoidObstacles`**:
  Route_ObstacleBetween_RoutesAround, Route_MultipleObstacles_RemainsValid,
  RouteWithStatus_ObstacleBetween_RoutesAroundWithoutCrossing
- **`Rendering-Layout-OrthogonalEdgeRouter-Clearance`**:
  RouteWithStatus_CleanRoute_KeepsClearanceFromObstacles
- **`Rendering-Layout-OrthogonalEdgeRouter-PerpendicularEnds`**:
  Route_WithSourceSide_LeavesPerpendicular, Route_WithTargetSide_EntersPerpendicular
- **`Rendering-Layout-OrthogonalEdgeRouter-CrossingStatus`**:
  RouteWithStatus_NoBlockingObstacle_ReportsNotCrossed, RouteWithStatus_TargetEnclosedByObstacle_ReportsCrossed
- **`Rendering-Layout-OrthogonalEdgeRouter-CostBands`**:
  RouteWithStatus_HighwayBand_PrefersBandedDetour
- **`Rendering-Layout-OrthogonalEdgeRouter-NoWaypointRevisit`**:
  RouteWithStatus_SoftObstacleDetour_DoesNotRevisitWaypoint
- **`Rendering-Layout-OrthogonalEdgeRouter-AvoidsExtendedSoftOverlap`**:
  RouteWithStatus_LongSoftObstacleOverlap_PrefersAlternateLane, RouteWithStatus_ShortSoftObstacleOverlap_KeepsStraightRoute
