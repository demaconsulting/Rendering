### OrthogonalEdgeRouter Unit Verification

Part of the Rendering Layout Verification.

This document maps the OrthogonalEdgeRouter unit requirements to named test scenarios.

#### Verification Approach

`OrthogonalEdgeRouter` is a stateless static engine, so verification is by direct xUnit unit tests
that call `Route` and `RouteWithStatus` on synthetic anchor / obstacle inputs. No mocks are used;
the tests observe the real grid construction, A\*-style search, clearance-retry ladder, and cost-band
biasing so orthogonality, obstacle avoidance, clearance, perpendicular ends, and crossing
status are all measured on production output.

#### Test Environment

- **Framework**: xUnit v4 running on the .NET SDK.
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
  the connector leaves/enters an anchor perpendicular to the given box side, and
  `RouteWithStatus_SameSideAlignedEnds_StepOffInsteadOfRunningAlongFaces` confirms two level ends on the same
  side step off and wrap around instead of running along the faces.
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
  `RouteWithStatus_SaturatedNarrowGapSoftObstacles_StaysWithinEnvelope` pins the envelope-departure
  safeguard directly: with several soft obstacles pre-seeded at ever-increasing offsets simulating the
  "stair-stepping" candidate lane a stack of already-routed parallel connectors produced before the fix,
  the route stays within the source/target envelope instead of landing a lane behind the source box's
  own footprint.
- **No connector-to-connector crossing** (`Rendering-Layout-OrthogonalEdgeRouter-AvoidsConnectorCrossing`):
  `RouteWithStatus_VerticalOrientedSoftObstacleAcrossOneLRoute_PrefersNonCrossingAlternative` pins the
  orientation-aware crossing cost directly: a single vertical-oriented soft obstacle is seeded so that
  it crosses one of two otherwise equally-cheap single-turn "L" routes between source and target,
  leaving the other entirely clear, and the router is asserted to take the non-crossing alternative.
  `Route_NineParallelEdgesIntoCompartmentBox_DoNotLoopBehindEitherBox` (in
  `ConnectorRouterTests.cs`) additionally asserts, via a new pairwise
  `AssertNoTransversalCrossingAcrossLines` check across every routed connector's interior
  segments, that a full batch of parallel connectors sharing a narrow inter-box gap does not cross one
  another. In this saturated 9-edge scenario a residual crossing between one connector's own
  endpoint-adjacent approach segment (excluded from the soft-obstacle set, matching
  `ConnectorRouter.AddLineObstacles`' own endpoint-adjacent exclusion) and a later connector's interior
  trunk can still occur — a routing-order limitation the interior-segment-only assertion does not
  cover, and one no per-move cost constant can resolve, since the earlier connector's own soft
  obstacles never include connectors routed after it. See
  `docs/gallery/parallel-edges-and-ports/README.md`'s side-by-side gallery entry for the documented
  visual disclosure of this residual case.
- **Minimum end legs, cost and clearance reporting** (`Rendering-Layout-OrthogonalEdgeRouter-MinimumEndLegs`):
  `RouteWithStatus_TargetLeg_PlacesBendAtLeastLegFromFace` forces a route under a block and confirms a 40 px
  target-leg hint moves the final bend from the stub end (20 px from the face) to at least 40 px while the
  legacy route keeps 20; `RouteWithStatus_ComfortLegZone_PlacesFirstBendAtLeastComfortFromFace` makes the
  far lane costly with a soft obstacle so the legacy route bends right after the source stub (20 px), and
  confirms the comfort zone moves the first bend to at least 36 px; `RouteWithStatus_LegsZero_MatchesLegacyRoute`
  confirms all-zero hints change neither the waypoints nor the cost; and
  `RouteWithStatus_ReportsCostAndClearanceUsed` confirms a free run costs its length (200) at the full
  clearance, a detour costs more, and a source 2 px from a box is routed at a smaller clearance level.
- **Port ranges** (`Rendering-Layout-OrthogonalEdgeRouter-PortRanges`):
  `RoutePorts_LaneWithinBothRanges_SlidesPortsToStraightLane` gives each end a 5..45 interval around a
  25 preferred port, with a block whose edge plus the clearance leaves the lane at 32, and confirms the route
  is one straight segment at y = 32 starting and ending on the chosen ports.
- **Wall keep-out** (`Rendering-Layout-OrthogonalEdgeRouter-WallKeepOut`):
  `RouteWithStatus_VerticalWall_BlocksParallelRunInsideMarginButAllowsHorizontalCrossing` uses two barriers
  whose gap lanes (x = 112 / 118) lie within 24 px of a wall at x = 100: without the hint the route threads
  the gap beside the wall; with it the route goes around the barrier, every vertical run keeps at least 24 px
  from the wall, and a horizontal run still crosses the wall.
- **Crossing cost** (`Rendering-Layout-OrthogonalEdgeRouter-CrossingCost`):
  `RouteWithStatus_CrossingPenalty_PrefersRouteWithFewerCrossings` seeds two vertical soft lines across a
  straight run: without the penalty the route stays straight through both, with a penalty of 100 it detours
  around them.
- **Minimum interior segment** (`Rendering-Layout-OrthogonalEdgeRouter-MinimumInteriorSegment`):
  `RoutePorts_MinInteriorSegment_FacesOverlapNarrowly_ReturnsZWithJogAtLeastMinimum` gives two facing
  faces whose preferred ports are 8.2 px apart and whose other usable spans do not overlap: the legacy
  route jogs by 8.2 px, the hinted route (minimum 24) is a Z with a middle run of at least 24 px.
  `RoutePorts_MinInteriorSegment_NoReversalToSatisfyMinimum` (a theory over four target spans) confirms
  no two consecutive segments run along one axis in opposite directions and every interior segment meets
  the minimum. `RouteWithStatus_MinInteriorSegmentInfeasible_FallsBackToUnconstrainedRoute` asks for an
  unreachable minimum (1000 px) and confirms the unconstrained route comes back unchanged, not crossed.
  `RouteWithStatus_LegsZero_MatchesLegacyRoute` confirms an all-zero hint is the legacy route.
- **Bounded work** (`Rendering-Layout-OrthogonalEdgeRouter-BoundedWork`):
  `RouteWithStatus_ExhaustedBudget_FallsBackToElbow` hands the router a budget already past its hard limit and
  confirms the elbow fallback comes back flagged `Crossed`, orthogonal, between the two ends, and counted by the
  budget. `RouteWithStatus_BudgetRunsOutDuringSearch_FallsBackIdenticallyOnEveryRun` routes across a field of twelve
  obstacles with a hard limit far below the work of the search, twice, and confirms both runs stop with the same
  elbow having spent the same work (the limit counts work, not time).
  `RouteWithStatus_GenerousBudget_RouteEqualsUnbudgetedRoute` confirms a budget that does not run out leaves the
  route exactly as without a budget and records a positive amount of work. The `RoutingWorkBudgetTests` unit tests
  check the tier thresholds (full below the soft limit, reduced at it, minimal at the hard limit), that a scope
  weight at the weight limit reduces the budget without spending any of it, the elbow-route counter, and the null
  argument check.
- **Reduced-effort window** (`Rendering-Layout-OrthogonalEdgeRouter-ReducedEffortWindow`):
  `RouteWithStatus_ReducedBudget_RoutesAroundObstaclesInWindow` routes two staggered obstacles at the reduced tier
  with a third obstacle 5000 px away, and confirms the route is orthogonal, not crossed, and clear of every
  obstacle. `RouteWithStatus_ReducedBudget_WindowTooSmall_SearchesWholeCanvas` puts a 2150 px wall between two
  nearby ends and confirms the route still goes round it (the window cannot, so the whole canvas is searched).
- **Reduced soft lanes** (`Rendering-Layout-OrthogonalEdgeRouter-ReducedSoftLanes`):
  `RouteWithStatus_ReducedBudget_HundredsOfSoftRects_RoutesValidlyForLessWork` routes across a wall among 300 thin soft
  rectangles (tall and wide) at the reduced tier, twice, and at the full tier, and confirms the reduced route is
  orthogonal, not crossed, clear of the wall, identical on both runs with the same work spent, and found for fewer work
  units than the full effort spends. `..._HundredsOfSoftRects_WindowTooSmall_SearchesWholeCanvas` repeats the 2150 px
  wall scenario with 200 soft rectangles near the ends and confirms the whole-canvas retry (which limits the soft lanes
  too) still routes round the wall. `RouteWithStatus_FullBudget_HundredsOfSoftRects_RouteEqualsUnbudgetedRoute` confirms
  the limits are the reduced tier's alone: a budget at the full tier gives exactly the route of no budget among 200 soft
  rectangles.
- **Exactness of the search implementation** is verified through the existing scenarios above (they run on the
  rewritten search) and through the geometry fingerprints of `RoutingPerformanceTests` (see the
  *HierarchicalLayoutAlgorithm Unit Verification*), which equal those the dictionary-based search produced; the
  gallery and the layout-quality benchmark were regenerated and are byte-identical.

#### Requirements Coverage

- **`Rendering-Layout-OrthogonalEdgeRouter-Orthogonal`**:
  Route_NoObstacles_ProducesOrthogonalPath, Route_AlignedEndpoints_ProducesStraightLine
- **`Rendering-Layout-OrthogonalEdgeRouter-AvoidObstacles`**:
  Route_ObstacleBetween_RoutesAround, Route_MultipleObstacles_RemainsValid,
  RouteWithStatus_ObstacleBetween_RoutesAroundWithoutCrossing
- **`Rendering-Layout-OrthogonalEdgeRouter-Clearance`**:
  RouteWithStatus_CleanRoute_KeepsClearanceFromObstacles
- **`Rendering-Layout-OrthogonalEdgeRouter-PerpendicularEnds`**:
  Route_WithSourceSide_LeavesPerpendicular, Route_WithTargetSide_EntersPerpendicular,
  RouteWithStatus_SameSideAlignedEnds_StepOffInsteadOfRunningAlongFaces
- **`Rendering-Layout-OrthogonalEdgeRouter-CrossingStatus`**:
  RouteWithStatus_NoBlockingObstacle_ReportsNotCrossed, RouteWithStatus_TargetEnclosedByObstacle_ReportsCrossed
- **`Rendering-Layout-OrthogonalEdgeRouter-CostBands`**:
  RouteWithStatus_HighwayBand_PrefersBandedDetour
- **`Rendering-Layout-OrthogonalEdgeRouter-NoWaypointRevisit`**:
  RouteWithStatus_SoftObstacleDetour_DoesNotRevisitWaypoint
- **`Rendering-Layout-OrthogonalEdgeRouter-AvoidsExtendedSoftOverlap`**:
  RouteWithStatus_LongSoftObstacleOverlap_PrefersAlternateLane,
  RouteWithStatus_ShortSoftObstacleOverlap_KeepsStraightRoute,
  RouteWithStatus_SaturatedNarrowGapSoftObstacles_StaysWithinEnvelope
- **`Rendering-Layout-OrthogonalEdgeRouter-AvoidsConnectorCrossing`**:
  RouteWithStatus_VerticalOrientedSoftObstacleAcrossOneLRoute_PrefersNonCrossingAlternative,
  Route_NineParallelEdgesIntoCompartmentBox_DoNotLoopBehindEitherBox
- **`Rendering-Layout-OrthogonalEdgeRouter-MinimumEndLegs`**:
  RouteWithStatus_TargetLeg_PlacesBendAtLeastLegFromFace,
  RouteWithStatus_ComfortLegZone_PlacesFirstBendAtLeastComfortFromFace,
  RouteWithStatus_LegsZero_MatchesLegacyRoute, RouteWithStatus_ReportsCostAndClearanceUsed
- **`Rendering-Layout-OrthogonalEdgeRouter-PortRanges`**:
  RoutePorts_LaneWithinBothRanges_SlidesPortsToStraightLane
- **`Rendering-Layout-OrthogonalEdgeRouter-WallKeepOut`**:
  RouteWithStatus_VerticalWall_BlocksParallelRunInsideMarginButAllowsHorizontalCrossing
- **`Rendering-Layout-OrthogonalEdgeRouter-CrossingCost`**:
  RouteWithStatus_CrossingPenalty_PrefersRouteWithFewerCrossings
- **`Rendering-Layout-OrthogonalEdgeRouter-MinimumInteriorSegment`**:
  RoutePorts_MinInteriorSegment_FacesOverlapNarrowly_ReturnsZWithJogAtLeastMinimum,
  RoutePorts_MinInteriorSegment_NoReversalToSatisfyMinimum,
  RouteWithStatus_MinInteriorSegmentInfeasible_FallsBackToUnconstrainedRoute,
  RouteWithStatus_LegsZero_MatchesLegacyRoute
- **`Rendering-Layout-OrthogonalEdgeRouter-BoundedWork`**:
  RouteWithStatus_ExhaustedBudget_FallsBackToElbow,
  RouteWithStatus_BudgetRunsOutDuringSearch_FallsBackIdenticallyOnEveryRun,
  RouteWithStatus_GenerousBudget_RouteEqualsUnbudgetedRoute, RoutingWorkBudget_New_IsFullEffort,
  RoutingWorkBudget_Charge_ReachesSoftThenHardLimit, RoutingWorkBudget_NoteScopeWeight_AtLimit_ReducesWithoutSpending,
  RoutingWorkBudget_RecordElbowRoute_CountsRoutes, RoutingWorkBudget_NullLimits_Throws
- **`Rendering-Layout-OrthogonalEdgeRouter-ReducedEffortWindow`**:
  RouteWithStatus_ReducedBudget_RoutesAroundObstaclesInWindow,
  RouteWithStatus_ReducedBudget_WindowTooSmall_SearchesWholeCanvas
- **`Rendering-Layout-OrthogonalEdgeRouter-ReducedSoftLanes`**:
  RouteWithStatus_ReducedBudget_HundredsOfSoftRects_RoutesValidlyForLessWork,
  RouteWithStatus_ReducedBudget_HundredsOfSoftRects_WindowTooSmall_SearchesWholeCanvas,
  RouteWithStatus_FullBudget_HundredsOfSoftRects_RouteEqualsUnbudgetedRoute
