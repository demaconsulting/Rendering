### OrthogonalEdgeRouter Unit Verification

Part of the Rendering Layout Verification.

This document maps the OrthogonalEdgeRouter unit requirements to named test scenarios.

#### OrthogonalEdgeRouter Unit Scenarios

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
