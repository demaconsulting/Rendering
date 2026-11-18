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
- **Project**: `test/DemaConsulting.Rendering.Layout.Tests/ConnectorRouterTests.cs`, plus
  `Engine/RouteQualityTests.cs` (the route-quality measure) and
  `NotationPrimitivePassThroughTests.cs` (folder-tab resolution, port corner margin, and new end-marker
  clearance) in the same project.
- **Dependencies**: no external services, files, or network access; every test constructs its own
  in-memory `LayoutBox` list and `Connection` records.
- **Isolation**: each test builds its own inputs; the class under test holds no static state, so
  tests are order-independent.

### Acceptance Criteria

A verification run passes when every named scenario below asserts without unexpected exception, and
the referenced tests cover each `Rendering-Layout-ConnectorRouter-*` requirement. Any wrong anchor
face, waypoint that enters an obstacle's strict interior, incorrect line styling passthrough,
out-of-order batch result, folder tab that disagrees with the shared box metrics, slid-port corner
margin other than 8 px for a legacy end marker (10 px for a circled plus), final leg shorter than a new
end marker plus the bend radius, or non-argument-null exception for invalid input constitutes a failure.

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
- **Minimum anchor separation** (`Rendering-Layout-ConnectorRouter-MinimumAnchorSeparation`):
  `RouteBatch_FixedAnchorOnShortFace_ExternalAnchorKeepsSeparation` routes one connector into a 130x50 box
  whose left face already carries an occupied anchor at its centre and asserts the end lands at least 16 px
  from it (it was 13 px: the clearance-inset region [12, 38] holds no such position, so the corner-margin
  region [8, 42] is used). The theory `Route_ManyConnectorsIntoShortFace_AnchorsAtLeast16Apart` (N = 2 to 8
  stacked sources into one 130x50 target, public `Route`) asserts on every face of the target the anchors are
  pairwise at least 16 px apart, every connector end is accounted for, and every route is orthogonal. The
  hierarchical `Apply_CrossHierarchyRepro1_AnchorsOnSharedFaceAtLeast16Apart` (the exact issue #53 graph: leaf H,
  layered folder P holding M -> E, root edge H -> E, all 130x50) and
  `Apply_ManyCrossHierarchyEdgesIntoOneBox_AnchorsAtLeast16Apart` (N = 3, 5 and 10 usages in a folder typing one
  box) assert the same through the cross-container batch, and the gallery fact
  `Gallery_ManyEdgesIntoShortBox_RendersSvgWithSeparatedAnchors` asserts it for the committed ten-edge example.
- **Face overflow to an adjacent face** (`Rendering-Layout-ConnectorRouter-FaceOverflowToAdjacentFace`):
  `Route_FaceOverCapacity_OverflowUsesAdjacentFace` routes eight connectors into a 130x50 box (its 50 px face holds
  three anchors 16 px apart), asserts the left face holds at most three, the rest wrap onto the top and bottom
  faces, and no route enters the target's interior; `Route_NineParallelEdgesIntoCompartmentBox_DoNotLoopBehindEitherBox`
  asserts the nine parallel edges (three on the short source face, six wrapped onto its bottom face) stay in the box
  pair's envelope, stay orthogonal, do not cross one another and keep 16 px between anchors on every face.
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
- **Folder tab resolution** (`Rendering-Layout-ConnectorRouter-FolderTabResolution`):
  `FolderGeometry_NameInTab_UsesMeasuredTabWidth` resolves the tab width of a wide folder with a long name
  twice through `ConnectorRouter.FolderGeometry`, once with the name drawn in the tab and once without, and
  confirms each equals `BoxMetrics.ResolveFolderTabWidth` for that box and that the two widths differ
  (the in-tab tab is sized to the measured name).
- **Port corner margin** (`Rendering-Layout-ConnectorRouter-PortCornerMargin`):
  `ConnectorRouter_PortCornerMarginFor_LegacyIsEightAndCircledPlusIsTen` asserts `PortCornerMarginFor`
  returns exactly 8 for every legacy style (none, open chevron, hollow triangle, crossbar triangle,
  filled arrow, hollow and filled diamond, circle, bar) and both dotted-triangle styles, and exactly 10
  for the circled plus.
- **New end-marker clearance** (`Rendering-Layout-ConnectorRouter-NewEndMarkerClearance`):
  `ConnectorRouter_NewEndMarker_ReservesCleanEndLeg` (theory over the open chevron, hollow triangle,
  crossbar triangle, hollow diamond, the two-dot and four-dot hollow triangles, and the circled plus) routes a
  connector needing a jog between two boxes and confirms the final leg is at least the style's RENDERED
  along-line length (notation length times the shared marker scale) plus the corner radius.

### Additional Regression Coverage

The following tests guard against a reported defect but are not linked to a requirement here, because
the actual behavior change they exercise (the length-proportional soft-obstacle cost) is owned by, and
already covered under, `OrthogonalEdgeRouter`'s own
`Rendering-Layout-OrthogonalEdgeRouter-AvoidsExtendedSoftOverlap` requirement (see
*OrthogonalEdgeRouter Unit Verification*); these end-to-end tests add confidence at the batch-API
level rather than introducing a new `ConnectorRouter`-level responsibility:

- `Route_ThreeConnectorsNarrowGap_InteriorCorridorsDoNotOverlapAlongLength`,
  `Route_FiveConnectorsNarrowGap_InteriorCorridorsDoNotOverlapAlongLength`, and
  `Route_ThreeSourceBoxesNarrowGap_InteriorCorridorsDoNotOverlapAlongLength` reproduce the reported
  DictionaryMark defect (a narrow vertical gap between a small box and a much wider one) and confirm
  that no two distinct connectors' interior segments ride the same grid line for an extended span.

#### Diamond outline anchors

The layered-algorithm tests `LayeredLayout_DiamondSourceTwoTargets_Down_FirstRunStartsOnOutlineAndIsClean`
(the Rendering#58 reproduction), `LayeredLayout_DiamondSingleEdge_AnchorsOnTheVertex`,
`LayeredLayout_DiamondSource_AnyDirection_StartsOnOutlinePerpendicular` (all four directions and several
diamond sizes), `LayeredLayout_DiamondTarget_TwoSources_EndsOnOutlineWithCleanFinalRun`, and
`LayeredLayout_DiamondDecisionThreeBranches_AnchorsDistinctAndOnOutline` assert that connector ends lie on the
diamond outline (normalized distance within 0.6 px), leave perpendicular to the face, and keep the connector
stub and marker run measured from the outline. `ConnectorRouter_Route_DiamondTarget_AnchorsOnOutline` and
`HierarchicalLayout_DiamondInsideContainer_CrossContainerEdgeStartsOnOutline` cover the router and the
cross-container path. `DiamondGeometry_ProjectToSurface_IsZeroAtVertexAndLinearToTheCorner` asserts the sloped
projection and the full connectable extent for square and non-square boxes, and
`ContainmentAndAutoLayout_DiamondNode_PassesShapeThrough` asserts the shape reaches the placed box.

Several connectors on one diamond face keep their markers apart:
`LayeredLayout_DiamondTarget_ThreeSources_MarkerHeadsKeepClearOfEachOther` (theory over four flow directions and the
filled arrow, hollow triangle, filled diamond, and bar markers) and
`LayeredLayout_DiamondTarget_TwoSources_MarkerHeadsKeepClear` (a 44 px diamond) assert that adjacent end points are at
least a marker's painted width plus 4 px apart;
`LayeredLayout_DiamondDecisionThreeBranches_SourceEndsWithoutMarker_KeepClearanceGrowthOnly` asserts a marker-less
source face only keeps the existing growth, `LayeredLayout_DiamondSingleEdge_KeepsRequestedSizeAndVertexAnchor` asserts
a single connector neither grows the diamond nor leaves the vertex, and
`LayeredLayout_RectangleTarget_ThreeSources_SizeAndAnchorsUnchanged` asserts the growth is diamond-only. The gallery
test `Gallery_Regression58_DiamondOutlineAnchors_MarkerHeadsKeepApart` repeats the check for the showcase scene in the
laid-out tree and in the rendered SVG's `marker-end` paths.

**Covers**: `Rendering-Layout-ConnectorRouter-DiamondOutlineAnchors`.

#### End labels reach the routed line

`ConnectorRouter_Connection_EndLabelsReachRoutedLine` routes a connection carrying both end labels and asserts
the routed line carries them.

**Covers**: `Rendering-Layout-ConnectorRouter-EndLabelPropagation`.

#### Existing-line crossing cost

`RouteBatch_ExistingInteriorLine_AvoidsCrossingWhenAlternativeExists` supplies an interior line through
`RoutingProfile.ExistingLinesFor` that a first route would cross and asserts the routed connector avoids it;
`RouteBatch_ExistingInteriorLine_NoAlternative_StillRoutes` closes every alternative and asserts the connector is
still routed (and crosses the line); `RouteBatch_NoExistingLines_ByteIdentical` asserts a batch without existing
lines routes byte-identically whether the provider is absent, null, or returns an empty list.

**Covers**: `Rendering-Layout-ConnectorRouter-ExistingLineCrossingCost`.

#### Tidy replacement rule

The `IsTidyReplacement_*` tests call the replacement rule directly on hand-built polylines: a modest detour that
removes a crossing is accepted, a long wrap, a replacement with many more bends, one that adds a hairpin and one
that runs nearly on top of another connector are rejected, and at equal crossings only a lower visual cost is
accepted.

**Covers**: `Rendering-Layout-ConnectorRouter-TidyReplacement`.

#### Route quality measure

`RouteQualityTests` (`Engine/RouteQualityTests.cs`) tests `RouteQuality` on hand-built polylines: path length,
bend count (collinear waypoints are not bends), hairpins, close parallel length (only squeezed runs with enough
overlap count, a gap of exactly 16 pixels does not) and its weighted variant, the summary over several connectors,
and, on a benchmark scene, that the engine's measure equals the test metrics' measure.

**Covers**: `Rendering-Layout-ConnectorRouter-RouteQuality`.

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
- **`Rendering-Layout-ConnectorRouter-MinimumAnchorSeparation`**:
  RouteBatch_FixedAnchorOnShortFace_ExternalAnchorKeepsSeparation,
  Route_ManyConnectorsIntoShortFace_AnchorsAtLeast16Apart,
  Apply_CrossHierarchyRepro1_AnchorsOnSharedFaceAtLeast16Apart,
  Apply_ManyCrossHierarchyEdgesIntoOneBox_AnchorsAtLeast16Apart,
  Gallery_ManyEdgesIntoShortBox_RendersSvgWithSeparatedAnchors
- **`Rendering-Layout-ConnectorRouter-FaceOverflowToAdjacentFace`**:
  Route_FaceOverCapacity_OverflowUsesAdjacentFace,
  Route_NineParallelEdgesIntoCompartmentBox_DoNotLoopBehindEitherBox
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
- **`Rendering-Layout-ConnectorRouter-FolderTabResolution`**:
  FolderGeometry_NameInTab_UsesMeasuredTabWidth
- **`Rendering-Layout-ConnectorRouter-PortCornerMargin`**:
  ConnectorRouter_PortCornerMarginFor_LegacyIsEightAndCircledPlusIsTen
- **`Rendering-Layout-ConnectorRouter-NewEndMarkerClearance`**:
  ConnectorRouter_NewEndMarker_ReservesCleanEndLeg

The internal per-connection batch entry point used by the hierarchical engine is verified without a dedicated
requirement (the public contract is unchanged): RouteBatch_EndpointAncestorOmittedFromObstacles_RouteCrossesAncestorWall
(a nested endpoint's route crosses its enclosing container's wall when that container is omitted from the connection's
obstacles), RouteBatch_PerConnectionObstacles_AreHonoredIndependently (a blocker present in one connection's obstacle
set is avoided there while another connection stays straight),
Route_BatchOverload_MatchesInternalBatchWithSharedObstacles (the public batch overload yields the same routes as the
internal entry point with a shared list), RouteBatch_FixedAnchorOnFace_MovesBatchAnchorClearOfIt (an anchor on an
occupied face position moves to a free spot), and TryResolveFaceAnchor_PointOnFace_ResolvesFaceAndAlong.

- **`Rendering-Layout-ConnectorRouter-DiamondOutlineAnchors`**:
  LayeredLayout_DiamondSourceTwoTargets_Down_FirstRunStartsOnOutlineAndIsClean,
  LayeredLayout_DiamondSingleEdge_AnchorsOnTheVertex,
  LayeredLayout_DiamondSource_AnyDirection_StartsOnOutlinePerpendicular,
  LayeredLayout_DiamondTarget_TwoSources_EndsOnOutlineWithCleanFinalRun,
  LayeredLayout_DiamondDecisionThreeBranches_AnchorsDistinctAndOnOutline,
  ConnectorRouter_Route_DiamondTarget_AnchorsOnOutline,
  HierarchicalLayout_DiamondInsideContainer_CrossContainerEdgeStartsOnOutline,
  DiamondGeometry_ProjectToSurface_IsZeroAtVertexAndLinearToTheCorner,
  ContainmentAndAutoLayout_DiamondNode_PassesShapeThrough,
  LayeredLayout_DiamondTarget_ThreeSources_MarkerHeadsKeepClearOfEachOther,
  LayeredLayout_DiamondTarget_TwoSources_MarkerHeadsKeepClear,
  LayeredLayout_DiamondDecisionThreeBranches_SourceEndsWithoutMarker_KeepClearanceGrowthOnly,
  LayeredLayout_DiamondSingleEdge_KeepsRequestedSizeAndVertexAnchor,
  LayeredLayout_RectangleTarget_ThreeSources_SizeAndAnchorsUnchanged
- **`Rendering-Layout-ConnectorRouter-EndLabelPropagation`**: ConnectorRouter_Connection_EndLabelsReachRoutedLine
- **`Rendering-Layout-ConnectorRouter-ExistingLineCrossingCost`**:
  RouteBatch_ExistingInteriorLine_AvoidsCrossingWhenAlternativeExists,
  RouteBatch_ExistingInteriorLine_NoAlternative_StillRoutes, RouteBatch_NoExistingLines_ByteIdentical
- **`Rendering-Layout-ConnectorRouter-TidyReplacement`**: IsTidyReplacement_FewerCrossingsModestDetour_Accepts,
  IsTidyReplacement_FewerCrossingsLongWrap_Rejects, IsTidyReplacement_FewerCrossingsManyMoreBends_Rejects,
  IsTidyReplacement_AddsHairpin_Rejects, IsTidyReplacement_RunsNearlyOnTopOfAnotherConnector_Rejects,
  IsTidyReplacement_EqualCrossings_AcceptsOnlyLowerVisualCost
- **`Rendering-Layout-ConnectorRouter-RouteQuality`**: RouteQuality_PathLength_Polyline_SumsSegments,
  RouteQuality_BendCount_CollinearWaypoints_AreNotBends, RouteQuality_HairpinCount_EastSouthWest_IsOneHairpin,
  RouteQuality_CloseParallelLength_GapAndOverlap_CountsOnlySqueezedRuns,
  RouteQuality_WeightedCloseParallelLength_NearerRunsWeighMore, RouteQuality_Measure_TwoConnectors_SumsAllMetrics,
  RouteQuality_Measure_BenchmarkScene_AgreesWithLayoutQualityMetrics
