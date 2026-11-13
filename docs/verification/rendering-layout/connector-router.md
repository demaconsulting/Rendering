## ConnectorRouter Unit Verification

Part of the Rendering Layout Verification.

This document maps the connector-router unit requirements to named test scenarios.

### Verification Approach

`ConnectorRouter` is a stateless static class, so verification is by direct xUnit unit tests that
call `ConnectorRouter.Route` on synthetic `LayoutBox` inputs. No mocks or fakes are used: the tests
exercise the real anchor-selection, obstacle-set construction, and dispatch code paths, letting the
integration with the internal `OrthogonalEdgeRouter` engine run end-to-end so anchor geometry,
obstacle avoidance, and produced `LayoutLine` styling are all observed on real outputs.

### Test Environment

- **Framework**: xUnit v4 running on the .NET SDK.
- **Runner**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Project**: `test/DemaConsulting.Rendering.Layout.Tests/ConnectorRouterTests.cs`.
- **Dependencies**: no external services, files, or network access; every test constructs its own
  in-memory `LayoutBox` list and `Connection` records.
- **Isolation**: each test builds its own inputs; the class under test holds no static state, so
  tests are order-independent.

### Acceptance Criteria

A verification run passes when every named scenario below asserts without unexpected exception, and
the referenced tests cover each `Rendering-Layout-ConnectorRouter-*` requirement. Any wrong anchor
face, waypoint that enters an obstacle's strict interior, incorrect line styling passthrough,
out-of-order batch result, or non-argument-null exception for invalid input constitutes a failure.

### Test Scenarios

- **Anchors face each other** (`Rendering-Layout-ConnectorRouter-AnchorsFaceEachOther`):
  `Route_TargetToTheRight_AnchorsFaceEachOther` and `Route_TargetBelow_AnchorsFaceEachOther` confirm
  the route starts and ends on the box faces that point at the opposing box.
- **Shape-aware anchors** (`Rendering-Layout-ConnectorRouter-ShapeAwareAnchors`):
  `Route_FolderTopFace_TabExcludedFromConnectableExtent` confirms a folder top-face anchor never lands
  on the tab strip, `Route_FolderTopFace_WidthOnlyHint_UsesFallbackTabHeight` confirms a width-only
  folder hint still uses the generic tab-height fallback,
  `Route_FolderTopFace_HeightOnlyHint_UsesFallbackTabWidth` confirms a height-only folder hint still
  uses the generic tab-width fallback,
  `Route_FolderTopFace_ProjectsAnchorToRecessedBodyTop` confirms the final anchor is projected down
  to the folder body's recessed top edge, and
  `Route_FaceSelectionFallback_EmptyNaturalFaceUsesAdjacentFace` confirms anchor selection falls back
  to the next-best usable face when the natural face has no usable extent.
- **Shared-face distribution across usable extents**
  (`Rendering-Layout-ConnectorRouter-SharedFaceDistribution`):
  `Route_SharedFolderTopFace_DistributesAcrossReducedExtent` confirms a batch of connectors sharing a
  folder's top face is spread only across the usable extent to the right of the tab.
- **Obstacle avoidance** (`Rendering-Layout-ConnectorRouter-AvoidsObstacles`):
  `Route_ObstacleBetweenEndpoints_RoutesAroundInterior` confirms the route is orthogonal and never
  enters an intervening box's interior.
- **Endpoint boxes as ordinary obstacles** (`Rendering-Layout-ConnectorRouter-ExcludesEndpoints`):
  `Route_EndpointBoxes_ReachBoundaryAnchorsViaApproachStub` confirms the connector still reaches the
  endpoints' boundary anchors even though both boxes are treated as obstacles, and
  `Route_NineParallelEdgesIntoCompartmentBox_DoNotCrossTargetInterior` confirms a batch of parallel
  connectors converging on one box face never cuts through that box's (or the source box's) interior,
  reproducing a previously reported defect.
- **Styling carried** (`Rendering-Layout-ConnectorRouter-CarriesStyling`):
  `Route_Connection_CarriesRequestedStyling` confirms the requested target marker, line style, and
  label flow onto the produced line while the source end stays unmarked.
- **Batch order** (`Rendering-Layout-ConnectorRouter-BatchOrder`):
  `Route_MultipleConnections_ReturnsOneLinePerConnectionInOrder` confirms one line per connection in
  input order.
- **Validation** (`Rendering-Layout-ConnectorRouter-Validation`): `Route_NullBoxes_Throws`,
  `Route_NullConnections_Throws`, `Route_NullOptions_Throws`, and `Route_NullConnection_Throws` confirm
  null arguments are rejected with an argument-null error.
- **Port sliding to a straight lane** (`Rendering-Layout-ConnectorRouter-AlignsPortsToLane`):
  `Route_LaneWithinBothFaceSpans_ProducesSingleStraightSegment` (a theory over all four facing
  orientations) builds two 50 px faces with a blocker whose far edge plus the clearance leaves a free lane
  inside both spans while the face centers are blocked, and expects one two-waypoint straight segment that
  keeps the blocker's clearance. `Route_LaneWithinOnlyOneFaceSpan_UsesAtMostTwoBends` expects at most two
  bends (it was four with fixed anchors) when the lane fits only the tall source face.
  `Route_SlideMargin_PortKeepsDecorationInsideFace` (theory) confirms a lane at least the marker's reach
  (8 px) from the corner is used (straight at the blocker edge + 12) while a lane 5.5 px or 2 px from the
  corner is refused (the jog stays), and that both ports keep the reach from the corners in every case.
  `Route_SingleOverload_MatchesBatchForSlidRoute` confirms the single and batch overloads return identical
  waypoints. `Route_RoundedFaces_SlideStaysWithinConnectableExtent` confirms a slid port never enters a
  rounded corner's arc zone.
  `ConnectorRouter_Route_SmallSharedSpanOnLongFace_SlidesPortIntoSpanAndGoesStraight` and
  `ConnectorRouter_Route_SharedSpanNearCornerOfLongFace_UsesClearanceInset` pin the two sides of the
  clearance-inset rule: a small shared span (a 20 px face, usable span 8..12) on a long face yields one
  straight connector inside that span with both ports clear of the corners, while a shared
  span wide enough for a straight connector keeps the overlap-centered anchor at the clearance inset.
  `ConnectorRouter_Route_ShortFaceForMargin_UsesFaceCenter` confirms a face too short for the inset keeps its
  own centre, and `ConnectorRouter_Route_FaceShorterThanTwoMargins_KeepsCentre` confirms a 10 px face
  beside a long one keeps its centered anchor (the jog is accepted rather than an overhanging arrowhead).
- **Decoration stays inside the face** (`Rendering-Layout-ConnectorRouter-DecorationWithinFace`):
  `Route_SlideMargin_PortKeepsDecorationInsideFace`,
  `ConnectorRouter_Route_SmallSharedSpanOnLongFace_SlidesPortIntoSpanAndGoesStraight` and
  `ConnectorRouter_Route_FaceShorterThanTwoMargins_KeepsCentre` assert every port is at least the marker
  reach (`StrokeWidth × (EndMarkerHalfWidth + StrokeWidth / 2) + StrokeWidth / 2` = 7.125 px, computed in the
  test from the theme and notation metrics independently of the router's 8 px constant) from both ends of
  its face, and `RouteBatch_Profile_NarrowOverlapSweep_NoTinyJogsAndPortsClearOfCorners` repeats the check
  over 230 one-pixel offsets of two facing boxes.
- **Slide keeps ports distinct** (`Rendering-Layout-ConnectorRouter-SlidePreservesDistinctPorts`):
  `Route_SharedFace_SlideKeepsDistinctOrderedPorts` (three connectors on one face, each straightened onto
  its target row, ports strictly ordered and at least 16 px apart),
  `Route_SharedFace_SlideRefusedWhenItWouldCrowdNeighbor` (a lane inside a neighbor's separation is not
  used; the original port stays) and `Route_FiveConnectorsNarrowGap_InteriorCorridorsDoNotOverlapAlongLength`
  (a slid connector never shares an earlier connector's lane over a non-trivial stretch).
- **Minimum end legs** (`Rendering-Layout-ConnectorRouter-MinimumEndLegs`):
  `Route_LaneOutsideBothFaceSpans_KeepsJogWithMinimumLegs` (theory over a filled arrow and a filled diamond)
  confirms an unavoidable jog keeps a first leg of at least 8 and a final leg of at least 15 / 19, and no box
  is crossed; `Route_NarrowGapJog_StillReturnsRouteAndNeverCrossesBoxes` confirms a 20 px gap still yields
  a clean route with the legacy half-gap legs.
- **Cross-hierarchy profile** (`Rendering-Layout-ConnectorRouter-WallsAndFaceSelection`,
  `Rendering-Layout-ConnectorRouter-PeerEndZones`): `RouteBatch_ProfileOn_FirstAndLastLegAtLeastComfort`
  (legs of at least 24 on a wide gap), `RouteBatch_WallsSupplied_NoRunWithinWallMargin_AndCrossesPerpendicularly`
  (every vertical run at least 24 px from both supplied walls),
  `RouteBatch_ShieldedTopFace_ChoosesAdjacentFaceWithOneBend` (a top face shielded by a title band and tab
  is not approached; the adjacent face gives one bend),
  `RouteBatch_UnrelatedWall_StraightLaneInsideKeepOutIsRefused` (a theory: a wall for an unrelated
  container refuses a lane 12 px under its border — 90 px faces stay straight on the 24 px lane, 50 px
  faces take a clean Z whose long run is at least 24 px from the border, ports clear of the corners, every
  interior segment at least 24 px),
  `RouteBatch_Profile_NarrowOverlapSweep_NoTinyJogsAndPortsClearOfCorners` (230 offsets of two facing
  boxes: no interior segment under 24 px, no reversal, ports at least the marker reach from the corners) and
  `RouteBatch_PeerEndZone_LaterConnectorNeverCrossesWithinZoneOfEarlierArrowhead` (any crossing of an
  earlier connector keeps 24 px from its arrowhead tip).

### Additional Regression Coverage

The following tests guard against a reported defect but are not linked to a requirement here, because
the actual behavior change they exercise (the length-proportional soft-obstacle cost) is owned by, and
already covered under, `OrthogonalEdgeRouter`'s own
`Rendering-Layout-OrthogonalEdgeRouter-AvoidsExtendedSoftOverlap` requirement (see
_OrthogonalEdgeRouter Unit Verification_); these end-to-end tests add confidence at the batch-API
level rather than introducing a new `ConnectorRouter`-level responsibility:

- `Route_ThreeConnectorsNarrowGap_InteriorCorridorsDoNotOverlapAlongLength`,
  `Route_FiveConnectorsNarrowGap_InteriorCorridorsDoNotOverlapAlongLength`, and
  `Route_ThreeSourceBoxesNarrowGap_InteriorCorridorsDoNotOverlapAlongLength` reproduce the reported
  DictionaryMark defect (a narrow vertical gap between a small box and a much wider one) and confirm
  that no two distinct connectors' interior segments ride the same grid line for an extended span.

### Requirements Coverage

- **`Rendering-Layout-ConnectorRouter-AnchorsFaceEachOther`**:
  Route_TargetToTheRight_AnchorsFaceEachOther, Route_TargetBelow_AnchorsFaceEachOther
- **`Rendering-Layout-ConnectorRouter-ShapeAwareAnchors`**:
  Route_FolderTopFace_TabExcludedFromConnectableExtent,
  Route_FolderTopFace_WidthOnlyHint_UsesFallbackTabHeight,
  Route_FolderTopFace_HeightOnlyHint_UsesFallbackTabWidth,
  Route_FolderTopFace_ProjectsAnchorToRecessedBodyTop,
  Route_FaceSelectionFallback_EmptyNaturalFaceUsesAdjacentFace
- **`Rendering-Layout-ConnectorRouter-SharedFaceDistribution`**:
  Route_SharedFolderTopFace_DistributesAcrossReducedExtent
- **`Rendering-Layout-ConnectorRouter-AvoidsObstacles`**:
  Route_ObstacleBetweenEndpoints_RoutesAroundInterior
- **`Rendering-Layout-ConnectorRouter-ExcludesEndpoints`**:
  Route_EndpointBoxes_ReachBoundaryAnchorsViaApproachStub,
  Route_NineParallelEdgesIntoCompartmentBox_DoNotCrossTargetInterior
- **`Rendering-Layout-ConnectorRouter-CarriesStyling`**:
  Route_Connection_CarriesRequestedStyling
- **`Rendering-Layout-ConnectorRouter-BatchOrder`**:
  Route_MultipleConnections_ReturnsOneLinePerConnectionInOrder
- **`Rendering-Layout-ConnectorRouter-Validation`**:
  Route_NullBoxes_Throws, Route_NullConnections_Throws, Route_NullOptions_Throws, Route_NullConnection_Throws
- **`Rendering-Layout-ConnectorRouter-AlignsPortsToLane`**:
  Route_LaneWithinBothFaceSpans_ProducesSingleStraightSegment,
  Route_LaneWithinOnlyOneFaceSpan_UsesAtMostTwoBends,
  Route_SlideMargin_PortKeepsDecorationInsideFace,
  Route_SingleOverload_MatchesBatchForSlidRoute,
  Route_RoundedFaces_SlideStaysWithinConnectableExtent,
  ConnectorRouter_Route_SmallSharedSpanOnLongFace_SlidesPortIntoSpanAndGoesStraight,
  ConnectorRouter_Route_SharedSpanNearCornerOfLongFace_UsesClearanceInset,
  ConnectorRouter_Route_ShortFaceForMargin_UsesFaceCenter
- **`Rendering-Layout-ConnectorRouter-DecorationWithinFace`**:
  Route_SlideMargin_PortKeepsDecorationInsideFace,
  ConnectorRouter_Route_SmallSharedSpanOnLongFace_SlidesPortIntoSpanAndGoesStraight,
  ConnectorRouter_Route_FaceShorterThanTwoMargins_KeepsCentre,
  RouteBatch_Profile_NarrowOverlapSweep_NoTinyJogsAndPortsClearOfCorners
- **`Rendering-Layout-ConnectorRouter-SlidePreservesDistinctPorts`**:
  Route_SharedFace_SlideKeepsDistinctOrderedPorts, Route_SharedFace_SlideRefusedWhenItWouldCrowdNeighbor,
  Route_FiveConnectorsNarrowGap_InteriorCorridorsDoNotOverlapAlongLength
- **`Rendering-Layout-ConnectorRouter-MinimumEndLegs`**:
  Route_LaneOutsideBothFaceSpans_KeepsJogWithMinimumLegs,
  Route_NarrowGapJog_StillReturnsRouteAndNeverCrossesBoxes
- **`Rendering-Layout-ConnectorRouter-WallsAndFaceSelection`**:
  RouteBatch_ProfileOn_FirstAndLastLegAtLeastComfort,
  RouteBatch_WallsSupplied_NoRunWithinWallMargin_AndCrossesPerpendicularly,
  RouteBatch_UnrelatedWall_StraightLaneInsideKeepOutIsRefused,
  RouteBatch_Profile_NarrowOverlapSweep_NoTinyJogsAndPortsClearOfCorners,
  RouteBatch_ShieldedTopFace_ChoosesAdjacentFaceWithOneBend
- **`Rendering-Layout-ConnectorRouter-PeerEndZones`**:
  RouteBatch_PeerEndZone_LaterConnectorNeverCrossesWithinZoneOfEarlierArrowhead

The internal per-connection batch entry point used by the hierarchical engine is verified without a
dedicated requirement (the public contract is unchanged): RouteBatch_EndpointAncestorOmittedFromObstacles_RouteCrossesAncestorWall
(a nested endpoint's route crosses its enclosing container's wall when that container is omitted from the
connection's obstacles), RouteBatch_PerConnectionObstacles_AreHonoredIndependently (a blocker present in
one connection's obstacle set is avoided there while another connection stays straight),
Route_BatchOverload_MatchesInternalBatchWithSharedObstacles (the public batch overload yields the same
routes as the internal entry point with a shared list), RouteBatch_FixedAnchorOnFace_MovesBatchAnchorClearOfIt
(an anchor on an occupied face position moves to a free spot), and
TryResolveFaceAnchor_PointOnFace_ResolvesFaceAndAlong.
