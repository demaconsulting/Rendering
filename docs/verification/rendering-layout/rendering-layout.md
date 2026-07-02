## Rendering.Layout Verification Design

This document describes the verification design for the Rendering.Layout software system (package
`DemaConsulting.Rendering.Layout`). It maps every requirement defined in the Rendering.Layout System
Requirements to at least one named test scenario so reviewers can confirm test completeness without
reading the test code.

### Verification Strategy

Rendering.Layout is verified almost entirely through fast, deterministic unit tests that exercise
each engine and each pipeline stage in isolation against synthetic geometric input. Because every
engine computes from plain geometric inputs (sizes, edges, anchors) and holds no instance state, no
mocking or stubbing is required: each test constructs its own inputs and asserts on the observable
geometry (rectangles, layer indices, waypoints, region sizes) or on the exception thrown for invalid
input.

Two families of tests carry special weight:

- **Legacy-oracle equivalence tests** (`Pipeline_MatchesLegacyOracle_*`) run both the layered
  pipeline and a preserved copy of the previous monolithic engine
  (`LegacyInterconnectionLayoutEngineOracle`) over the same inputs and assert the two outputs are
  **byte-for-byte identical** — the same rectangles, totals, layer assignments, and connector
  waypoints. These tests are the primary evidence that the staged extraction preserved behavior.
- **Byte-identity routing tests** (`OrthogonalRouter_DefaultApproach_IsByteIdenticalToLegacy`,
  `OrthogonalRouter_ForwardEdges_GeometryUnchanged`, `OrthogonalRouter_AcyclicGraph_NoApproachChange`)
  assert that the configurable back-edge approach leaves forward and acyclic geometry unchanged at
  the default value.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK.
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Location**: `test/DemaConsulting.Rendering.Layout.Tests/`, with `Engine/` and `Engine/Layered/`
  subfolders mirroring the source structure, plus `LayeredLayoutAlgorithmTests.cs`,
  `ContainmentLayoutAlgorithmTests.cs`, and `HierarchicalLayoutAlgorithmTests.cs` at the root.
- **Dependencies**: No external services, files, or network access. Tests use in-memory synthetic
  graphs and geometry only.
- **Isolation**: Each test builds its own inputs; engines are stateless, so no shared state exists
  between tests.

## Engine Subsystem Verification

### OrthogonalEdgeRouter Scenarios

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

### ContainmentPacker Scenarios

- **Single row** (`Rendering-Layout-ContainmentPacker-SingleRow`): `Pack_ItemsFitInRow_ShareSameRow`
  asserts items that fit the width budget share one row, left to right.
- **Wrapping** (`Rendering-Layout-ContainmentPacker-Wrapping`):
  `Pack_ItemsExceedWidth_WrapToNewRow` confirms an overflowing item starts a new row beneath the
  current one.
- **No overlap** (`Rendering-Layout-ContainmentPacker-NoOverlap`):
  `Pack_MixedSizes_ProducesNoOverlaps` asserts no two packed rectangles overlap for a mix of sizes.
- **Within bounds** (`Rendering-Layout-ContainmentPacker-WithinBounds`):
  `Pack_MixedSizes_AllRectsWithinBounds` asserts every rectangle lies inside the reported region.
- **Oversized item** (`Rendering-Layout-ContainmentPacker-OversizedItem`):
  `Pack_ItemWiderThanContentWidth_PlacedAloneAndRegionWidens` confirms an oversized item is placed
  alone and the region widens to contain it.
- **Empty input** (`Rendering-Layout-ContainmentPacker-EmptyInput`):
  `Pack_EmptyList_ReturnsPaddingOnlyRegion` confirms an empty input yields a padding-only region.
- **Single item** (`Rendering-Layout-ContainmentPacker-SingleItem`):
  `Pack_SingleItem_PositionsAtPaddingOrigin` confirms a lone item lands at the padding origin with
  the region sized to wrap it.

### InterconnectionLayoutEngine Scenarios

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

## Layered Pipeline Verification

The pipeline stages are exercised individually, and the assembled pipeline is checked for byte-exact
equivalence with the legacy oracle. Dependencies are real (each stage operates on a `LayeredGraph`);
nothing is mocked.

- **Staged pipeline** (`Rendering-Layout-LayeredPipeline-StagedPipeline`):
  `LayeredLayoutPipeline_RunDefaultStages_ChainGraph_PopulatesWaypointsWithoutThrowing` runs the full
  default sequence and confirms waypoints are populated; `Pipeline_MatchesLegacyOracle_OnRandomGraphs`
  confirms the assembled sequence matches the oracle.
- **Behavior preserving** (`Rendering-Layout-LayeredPipeline-BehaviorPreserving`): the byte-for-byte
  legacy-oracle equivalence suite — `Pipeline_MatchesLegacyOracle_OnRandomGraphs`,
  `Pipeline_MatchesLegacyOracle_OnEmptyGraph`, `Pipeline_MatchesLegacyOracle_OnDroneLikeGraph`,
  `Pipeline_MatchesLegacyOracle_OnWorkstationLikeGraph`, and
  `Pipeline_MatchesLegacyOracle_OnNamedTopologies`.
- **Flat hierarchy only** (`Rendering-Layout-LayeredPipeline-FlatHierarchyOnly`):
  `LayeredLayoutPipeline_Build_RecursiveHierarchy_ThrowsNotSupportedException` confirms recursive
  handling fails fast.
- **Directions** (`Rendering-Layout-LayeredPipeline-Directions`):
  `AxisTransform_Apply_RightDirection_LeavesCoordinatesUnchanged`,
  `AxisTransform_Apply_Right_PlacesTargetEastWithCorrectFaces`,
  `AxisTransform_Apply_Down_PlacesTargetSouthWithCorrectFaces`,
  `AxisTransform_Apply_Left_PlacesTargetWestWithCorrectFaces`, and
  `AxisTransform_Apply_Up_PlacesTargetNorthWithCorrectFaces` cover all four flow directions.
- **Orthogonal connectors** (`Rendering-Layout-LayeredPipeline-OrthogonalConnectors`):
  `AxisTransform_Apply_Right_ProducesOrthogonalWaypoints`,
  `AxisTransform_Apply_Down_ProducesOrthogonalWaypoints`,
  `AxisTransform_Apply_Left_ProducesOrthogonalWaypoints`, and
  `AxisTransform_Apply_Up_ProducesOrthogonalWaypoints` assert axis-aligned waypoints per direction.
- **Cycle breaking** (`Rendering-Layout-LayeredPipeline-CycleBreaking`):
  `CycleBreaker_Apply_GraphWithCycle_ProducesAcyclicEdgeSet` and
  `CycleBreaker_Apply_SelfLoopsAndDuplicates_AreRemoved`.
- **Layer assignment** (`Rendering-Layout-LayeredPipeline-LayerAssignment`):
  `LayerAssigner_Apply_LinearChain_AssignsMonotonicLayers` and
  `LayerAssigner_Apply_DiamondGraph_AssignsLongestPathLayers`.
- **Long-edge splitting** (`Rendering-Layout-LayeredPipeline-LongEdgeSplitting`):
  `LongEdgeSplitter_Apply_SpanOneEdge_AddsNoDummyNodes` and
  `LongEdgeSplitter_Apply_LongEdge_InsertsDummyNodesPerIntermediateLayer`.
- **Crossing minimization** (`Rendering-Layout-LayeredPipeline-CrossingMinimization`):
  `CrossingMinimizer_Apply_CrossingProneOrdering_ReducesCrossings`,
  `CrossingMinimizer_Apply_TwoLayerGraph_GroupsNodesByLayer`, and
  `CrossingMinimizer_Apply_AllAugmentedNodesAppearInGroups`.
- **Coordinate assignment** (`Rendering-Layout-LayeredPipeline-CoordinateAssignment`):
  `BrandesKopfPlacer_Apply_ChainGraph_AssignsCoordinateArrays`,
  `BrandesKopfPlacer_Apply_ColumnsAreLeftToRightInLayerOrder`, and
  `BrandesKopfPlacer_Apply_SymmetricFork_CentersSourceBetweenTargets`.
- **Port distribution** (`Rendering-Layout-LayeredPipeline-PortDistribution`):
  `PortDistributor_Apply_SingleEdge_PortsLieWithinNodeFaces` and
  `PortDistributor_Apply_AssignsPortYForEverySubEdge`.
- **Orthogonal routing** (`Rendering-Layout-LayeredPipeline-OrthogonalRouting`):
  `OrthogonalRouter_Apply_StraightEdge_ProducesNoBendPoints` and
  `OrthogonalRouter_Apply_EveryBendListIsEmptyOrVerticalSegment`.
- **Back-edge approach** (`Rendering-Layout-LayeredPipeline-BackEdgeApproach`):
  `OrthogonalRouter_DefaultApproach_IsByteIdenticalToLegacy` (byte-identity),
  `OrthogonalRouter_ForwardEdges_GeometryUnchanged`, `OrthogonalRouter_AcyclicGraph_NoApproachChange`,
  `OrthogonalRouter_CustomApproach_PushesEntryStubOutward`,
  `OrthogonalRouter_ReversedEdge_DefaultApproachClearsClearance`, and
  `OrthogonalRouter_DecorationAwareApproach_ClearsMarkerAlongLength`.
- **Long-edge joining** (`Rendering-Layout-LayeredPipeline-LongEdgeJoining`):
  `LongEdgeJoiner_Apply_SingleEdge_ProducesWaypointsPerOriginalEdge` and
  `LongEdgeJoiner_Apply_LongEdge_ConcatenatesSubEdgeBendPoints`.
- **Component packing** (`Rendering-Layout-LayeredPipeline-ComponentPacking`):
  `ComponentPacker_Apply_DisconnectedSingletons_PackSeparately`,
  `ComponentPacker_Apply_ConnectedCore_StaysOneComponent`,
  `ComponentPacker_Apply_SingleComponent_EqualsDefaultPipeline`,
  `ComponentPacker_Apply_ComponentOrder_IsDeterministic`,
  `ComponentPacker_Apply_Waypoints_TranslatedWithComponent`, `ComponentPacker_Apply_EmptyGraph_IsNoOp`,
  `ComponentPacker_Apply_SingleComponent_ParallelAndSelfEdges_ProducesAlignedWaypoints`, and
  `ComponentPacker_Apply_MultiComponent_ParallelAndSelfEdges_MergesAlignedWaypoints`.
- **Shared state** (`Rendering-Layout-LayeredPipeline-SharedState`):
  `LayeredGraph_Constructor_ValidInput_StoresNodesEdgesDirectionAndCount`,
  `LayeredGraph_Constructor_NullNodes_ThrowsArgumentNullException`, and
  `LayeredGraph_Constructor_NullEdges_ThrowsArgumentNullException`.
- **Input validation** (`Rendering-Layout-LayeredPipeline-InputValidation`):
  `LayeredLayoutPipeline_AddStage_NullStage_ThrowsArgumentNullException` and
  `LayeredLayoutPipeline_Run_NullGraph_ThrowsArgumentNullException`.

## LayeredLayoutAlgorithm Verification

- **Identity** (`Rendering-Layout-LayeredAlgorithm-Identity`): `Id_IsLayered` asserts the algorithm
  reports the stable `"layered"` identifier.
- **Places and routes** (`Rendering-Layout-LayeredAlgorithm-PlacesAndRoutes`):
  `Apply_ChainGraph_PlacesLayeredBoxesAndRoutesEdges` confirms one placed box per node and one routed
  connector per edge for a chain graph.
- **Empty graph** (`Rendering-Layout-LayeredAlgorithm-EmptyGraph`):
  `Apply_EmptyGraph_ReturnsEmptyCanvas` confirms an empty graph yields an empty placed layout tree.
- **Validation** (`Rendering-Layout-LayeredAlgorithm-Validation`): `Apply_NullGraph_Throws` confirms
  a null graph argument is rejected with an argument-null error, and `Apply_NullOptions_Throws`
  confirms a null options argument is likewise rejected with an argument-null error.

## ConnectorRouter Verification

### EdgeRouting Option Scenarios

- **Per-scope selection** (`Rendering-Layout-EdgeRouting-Selection`):
  `CoreOptions_EdgeRouting_DefaultsToOrthogonal` and `CoreOptions_EdgeRouting_HasStableId` confirm the
  `rendering.edgerouting` key defaults to `Orthogonal` and carries the ELK-flavored id;
  `CoreOptions_EdgeRouting_SelectablePerScope` sets and reads the style back through the property
  system, and `CoreOptions_EdgeRouting_UnsetReturnsDefault` confirms an unset scope falls back to the
  orthogonal default.
- **Route-option defaults** (`Rendering-Layout-EdgeRouting-Defaults`):
  `ConnectorRouteOptions_Defaults_AreOrthogonalWithTwelvePixelClearance` confirms the default style is
  orthogonal, the default clearance is twelve logical pixels, and the clearance is caller-overridable.

### ConnectorRouter Scenarios

- **Anchors face each other** (`Rendering-Layout-ConnectorRouter-AnchorsFaceEachOther`):
  `Route_TargetToTheRight_AnchorsFaceEachOther` and `Route_TargetBelow_AnchorsFaceEachOther` confirm
  the route starts and ends on the box faces that point at the opposing box.
- **Obstacle avoidance** (`Rendering-Layout-ConnectorRouter-AvoidsObstacles`):
  `Route_ObstacleBetweenEndpoints_RoutesAroundInterior` confirms the route is orthogonal and never
  enters an intervening box's interior.
- **Endpoint exclusion** (`Rendering-Layout-ConnectorRouter-ExcludesEndpoints`):
  `Route_EndpointBoxes_AreExcludedFromObstacles` confirms the connector reaches the endpoints' boundary
  anchors even though both boxes appear in the box list.
- **Styling carried** (`Rendering-Layout-ConnectorRouter-CarriesStyling`):
  `Route_Connection_CarriesRequestedStyling` confirms the requested target marker, line style, and
  label flow onto the produced line while the source end stays unmarked.
- **Batch order** (`Rendering-Layout-ConnectorRouter-BatchOrder`):
  `Route_MultipleConnections_ReturnsOneLinePerConnectionInOrder` confirms one line per connection in
  input order.
- **Validation** (`Rendering-Layout-ConnectorRouter-Validation`): `Route_NullBoxes_Throws`,
  `Route_NullConnections_Throws`, `Route_NullOptions_Throws`, and `Route_NullConnection_Throws` confirm
  null arguments are rejected with an argument-null error.

## ContainmentLayout Verification

### ContainmentLayout Scenarios

- **Order preserved** (`Rendering-Layout-ContainmentLayout-Order`):
  `Pack_ItemsFitInRow_PreservesOrderLeftToRight` confirms the packed children keep their input order,
  positioned left to right along a shared row.
- **No overlap** (`Rendering-Layout-ContainmentLayout-NoOverlap`): `Pack_MixedSizes_ProducesNoOverlaps`
  asserts no two packed children overlap for a multi-row mix of sizes.
- **Within region** (`Rendering-Layout-ContainmentLayout-WithinRegion`):
  `Pack_MixedSizes_AllChildrenWithinRegion` asserts every child lies inside the reported region.
- **Wrapping** (`Rendering-Layout-ContainmentLayout-Wrapping`):
  `Pack_ChildExceedsWidth_WrapsToNewRow` confirms an overflowing child starts a new row beneath the
  current one at the left origin.
- **Oversized child** (`Rendering-Layout-ContainmentLayout-OversizedChild`):
  `Pack_OversizedChild_PlacedAloneAndRegionWidens` confirms a child wider than the content width is
  placed alone and the region widens to contain it.
- **Empty input** (`Rendering-Layout-ContainmentLayout-EmptyInput`):
  `Pack_EmptyInput_ReturnsPaddingOnlyRegion` confirms an empty input yields no children and a
  padding-only region.
- **Fields preserved** (`Rendering-Layout-ContainmentLayout-PreservesFields`):
  `Pack_PreservesNonPositionFields` confirms label, depth, shape, compartments, nested children, and
  keyword survive unchanged while only X and Y are updated.
- **Option defaults** (`Rendering-Layout-ContainmentLayout-Defaults`):
  `ContainmentOptions_Defaults_AreSensibleGapsAndPadding` confirms the default gaps are eight pixels and
  the default padding is twelve pixels.
- **Validation** (`Rendering-Layout-ContainmentLayout-Validation`): `Pack_NullChildren_Throws`,
  `Pack_NullOptions_Throws`, and `Pack_NullChildElement_Throws` confirm null arguments are rejected with
  an argument-null error.

## ContainmentLayoutAlgorithm Verification

### ContainmentLayoutAlgorithm Scenarios

- **Identity** (`Rendering-Layout-ContainmentAlgorithm-Identity`): `Id_IsContainment` asserts the
  algorithm reports the stable `"containment"` identifier.
- **Packs nodes** (`Rendering-Layout-ContainmentAlgorithm-PacksNodes`):
  `Apply_Graph_PacksNodesNonOverlappingInInputOrderWithinCanvas` confirms one placed box per node, in
  input order, with no two boxes overlapping and every box inside the reported canvas.
- **Routes edges** (`Rendering-Layout-ContainmentAlgorithm-RoutesEdges`):
  `Apply_Graph_RoutesOneConnectorPerEdgeCarryingStyling` confirms one routed connector per input edge,
  each carrying the edge's target end marker, line style, and label.
- **Routes around obstacle** (`Rendering-Layout-ContainmentAlgorithm-RoutesAroundObstacle`):
  `Apply_EdgeCrossingInterveningBox_RoutesAroundIt` confirms an edge whose endpoints straddle an
  intervening packed box is routed around that box's interior.
- **Empty graph** (`Rendering-Layout-ContainmentAlgorithm-EmptyGraph`):
  `Apply_EmptyGraph_ReturnsEmptyCanvas` confirms an empty graph yields an empty placed tree with a
  positive-size canvas.
- **Skips out-of-graph edges** (`Rendering-Layout-ContainmentAlgorithm-SkipsOutOfGraphEdges`):
  `Apply_EdgeReferencingOutOfGraphNode_IsSkipped` confirms an edge whose endpoint is not a top-level
  node is skipped rather than routed.
- **Validation** (`Rendering-Layout-ContainmentAlgorithm-Validation`): `Apply_NullGraph_Throws` and
  `Apply_NullOptions_Throws` confirm null arguments are rejected with an argument-null error.

## HierarchicalLayoutAlgorithm Verification

### HierarchicalLayoutAlgorithm Scenarios

- **Identity** (`Rendering-Layout-HierarchicalLayout-Identity`): `Id_IsHierarchical` asserts the engine
  reports the stable `"hierarchical"` identifier.
- **Flat equivalence** (`Rendering-Layout-HierarchicalLayout-FlatEquivalence`):
  `Apply_FlatRandomGraphs_MatchLayeredAlgorithmExactly` and
  `Apply_FlatRandomGraphs_MatchContainmentAlgorithmExactly` feed hundreds of pseudo-random flat graphs
  through the engine and the selected leaf algorithm and deep-compare the two placed trees bit-for-bit
  (canvas size, node kinds, box geometry and attributes, and every line waypoint), proving a graph with
  no container nodes is placed identically to the leaf algorithm applied directly.
- **Nests children** (`Rendering-Layout-HierarchicalLayout-NestsChildren`):
  `Apply_TwoLevelNesting_SizesContainerAndNestsChildrenAbsolutely` confirms a container is sized to
  enclose its children and each nested box lies within the container's bounds;
  `Apply_ThreeLevelNesting_Succeeds` confirms three levels compose so a box contains a box that contains
  a box.
- **Per-node algorithm** (`Rendering-Layout-HierarchicalLayout-PerNodeAlgorithm`):
  `Apply_ContainmentRootWithLayeredContainer_Composes` and
  `Apply_LayeredRootWithContainmentContainer_Composes` confirm a container overriding its algorithm is
  laid out with that algorithm while its parent uses another, composing with nested children.
- **Hierarchy handling** (`Rendering-Layout-HierarchicalLayout-HierarchyHandling`):
  `Apply_TwoLevelNesting_SizesContainerAndNestsChildrenAbsolutely` confirms children are laid out in
  their own coordinate space and composed into the parent at absolute coordinates.
  `Apply_CompoundGraph_DoesNotMutateInputNodeSizes` confirms the engine sizes containers over an
  internal sized view and never mutates the caller's input node dimensions.
- **Cross-container edge** (`Rendering-Layout-HierarchicalLayout-CrossContainerEdge`):
  `Apply_CrossContainerEdge_RoutesAroundInterveningContainer` confirms an edge between children of
  different sibling containers is routed at the owning scope and no routed segment passes through the
  intervening container's interior.
- **Validation** (`Rendering-Layout-HierarchicalLayout-Validation`): `Apply_NullGraph_Throws`,
  `Apply_NullOptions_Throws`, and `Constructor_NullRegistry_Throws` confirm null arguments are rejected
  with an argument-null error.

## LayoutAlgorithms and LayoutEngine Verification

### DefaultLayout Scenarios

- **Bundled algorithms** (`Rendering-Layout-DefaultRegistry-BundledAlgorithms`):
  `CreateDefaultRegistry_ResolvesLayeredAlgorithm`, `CreateDefaultRegistry_ResolvesContainmentAlgorithm`,
  and `CreateDefaultRegistry_ResolvesHierarchicalAlgorithm` confirm each bundled algorithm resolves by
  its identifier; `CreateDefaultRegistry_RegistersOnlyTheThreeBundledAlgorithms` confirms exactly those
  three identifiers are present; `CreateDefaultRegistry_ReturnsIndependentInstances` confirms registering
  into one returned registry does not affect a registry from a separate call.
- **Default algorithm** (`Rendering-Layout-LayoutEngine-DefaultAlgorithm`): `DefaultAlgorithmId_IsHierarchical`
  confirms the facade's declared default is `"hierarchical"`;
  `Layout_FlatGraphNoAlgorithmDeclared_MatchesLayeredLeafExactly` confirms an undeclared flat graph is
  laid out through the hierarchical engine to a result identical to the layered leaf algorithm.
- **Resolution** (`Rendering-Layout-LayoutEngine-Resolution`):
  `Layout_OptionsDeclareLayered_MatchesLayeredAlgorithmExactly` and
  `Layout_OptionsDeclareContainment_MatchesContainmentAlgorithmExactly` confirm an explicit options
  declaration selects that algorithm; `Layout_GraphDeclarationOverridesOptions` confirms a graph-level
  declaration takes precedence over the options.
- **Flat equivalence** (`Rendering-Layout-LayoutEngine-FlatEquivalence`):
  `Layout_FlatGraphNoAlgorithmDeclared_MatchesLayeredLeafExactly` and
  `Layout_OptionsDeclareContainment_MatchesContainmentAlgorithmExactly` deep-compare the facade's output
  bit-for-bit with the leaf algorithm applied directly, proving the facade changes no existing output.
- **Nested composition** (`Rendering-Layout-LayoutEngine-NestedComposition`):
  `Layout_NestedGraphNoAlgorithmDeclared_ProducesComposedTree` confirms an undeclared nested graph is
  composed so the container box carries its recursively laid-out children.
- **Custom registry** (`Rendering-Layout-LayoutEngine-CustomRegistry`):
  `Layout_CustomRegistry_ResolvesRegisteredAlgorithm` confirms a caller-supplied registry's algorithm is
  resolved and applied; `Layout_UnregisteredAlgorithm_Throws` confirms an unknown identifier surfaces a
  key-not-found error.
- **Validation** (`Rendering-Layout-LayoutEngine-Validation`): `Layout_NullGraph_Throws`,
  `Layout_NullOptions_Throws`, and `Layout_NullRegistry_Throws` confirm null arguments are rejected with
  an argument-null error.

## Acceptance Criteria

A verification run passes when every named test method above is discovered, executed, and passes
without error or exception beyond those explicitly asserted. The legacy-oracle equivalence tests and
the byte-identity routing tests must show exact geometric equality; any deviation constitutes a
failure.

## Requirements Coverage

Every requirement maps to at least one named test scenario:

- **`Rendering-Layout-Algorithm`**: Apply_ChainGraph_PlacesLayeredBoxesAndRoutesEdges, Id_IsLayered
- **`Rendering-Layout-Interconnection`**: Place_LinearChain_MonotonicLayerAssignment,
  Place_WorkstationTopology_CorrectLayersAndNoOverlap
- **`Rendering-Layout-StagedPipeline`**: LayeredLayoutPipeline_RunDefaultStages_ChainGraph_PopulatesWaypointsWithoutThrowing,
  Pipeline_MatchesLegacyOracle_OnRandomGraphs
- **`Rendering-Layout-OrthogonalRouting`**: Route_NoObstacles_ProducesOrthogonalPath,
  Route_ObstacleBetween_RoutesAround
- **`Rendering-Layout-Containment`**: Pack_MixedSizes_ProducesNoOverlaps, Pack_ItemsFitInRow_ShareSameRow
- **`Rendering-Layout-OrthogonalEdgeRouter-Orthogonal`**: Route_NoObstacles_ProducesOrthogonalPath,
  Route_AlignedEndpoints_ProducesStraightLine
- **`Rendering-Layout-OrthogonalEdgeRouter-AvoidObstacles`**: Route_ObstacleBetween_RoutesAround,
  Route_MultipleObstacles_RemainsValid,
  RouteWithStatus_ObstacleBetween_RoutesAroundWithoutCrossing
- **`Rendering-Layout-OrthogonalEdgeRouter-Clearance`**: RouteWithStatus_CleanRoute_KeepsClearanceFromObstacles
- **`Rendering-Layout-OrthogonalEdgeRouter-PerpendicularEnds`**: Route_WithSourceSide_LeavesPerpendicular,
  Route_WithTargetSide_EntersPerpendicular
- **`Rendering-Layout-OrthogonalEdgeRouter-CrossingStatus`**: RouteWithStatus_NoBlockingObstacle_ReportsNotCrossed,
  RouteWithStatus_TargetEnclosedByObstacle_ReportsCrossed
- **`Rendering-Layout-OrthogonalEdgeRouter-CostBands`**: RouteWithStatus_HighwayBand_PrefersBandedDetour
- **`Rendering-Layout-ContainmentPacker-SingleRow`**: Pack_ItemsFitInRow_ShareSameRow
- **`Rendering-Layout-ContainmentPacker-Wrapping`**: Pack_ItemsExceedWidth_WrapToNewRow
- **`Rendering-Layout-ContainmentPacker-NoOverlap`**: Pack_MixedSizes_ProducesNoOverlaps
- **`Rendering-Layout-ContainmentPacker-WithinBounds`**: Pack_MixedSizes_AllRectsWithinBounds
- **`Rendering-Layout-ContainmentPacker-OversizedItem`**: Pack_ItemWiderThanContentWidth_PlacedAloneAndRegionWidens
- **`Rendering-Layout-ContainmentPacker-EmptyInput`**: Pack_EmptyList_ReturnsPaddingOnlyRegion
- **`Rendering-Layout-ContainmentPacker-SingleItem`**: Pack_SingleItem_PositionsAtPaddingOrigin
- **`Rendering-Layout-InterconnectionEngine-Layering`**: Place_LinearChain_MonotonicLayerAssignment,
  Place_WorkstationTopology_CorrectLayersAndNoOverlap
- **`Rendering-Layout-InterconnectionEngine-NonOverlapping`**: Place_WorkstationTopology_CorrectLayersAndNoOverlap
- **`Rendering-Layout-InterconnectionEngine-DummyNodes`**: Place_LongEdge_RectCountEqualsInputNodeCount,
  Place_LongEdge_RoutesViaDummyNodesWithinBounds
- **`Rendering-Layout-InterconnectionEngine-Waypoints`**: Place_SingleEdge_ProducesStraightTwoWaypointPath,
  Place_LongEdge_RoutesViaDummyNodesWithinBounds
- **`Rendering-Layout-InterconnectionEngine-Deterministic`**: Place_LinearChain_MonotonicLayerAssignment,
  Place_WorkstationTopology_CorrectLayersAndNoOverlap
- **`Rendering-Layout-LayeredPipeline-StagedPipeline`**: LayeredLayoutPipeline_RunDefaultStages_ChainGraph_PopulatesWaypointsWithoutThrowing,
  Pipeline_MatchesLegacyOracle_OnRandomGraphs
- **`Rendering-Layout-LayeredPipeline-BehaviorPreserving`**: Pipeline_MatchesLegacyOracle_OnRandomGraphs,
  Pipeline_MatchesLegacyOracle_OnEmptyGraph,
  Pipeline_MatchesLegacyOracle_OnDroneLikeGraph,
  Pipeline_MatchesLegacyOracle_OnWorkstationLikeGraph,
  Pipeline_MatchesLegacyOracle_OnNamedTopologies
- **`Rendering-Layout-LayeredPipeline-FlatHierarchyOnly`**: LayeredLayoutPipeline_Build_RecursiveHierarchy_ThrowsNotSupportedException
- **`Rendering-Layout-LayeredPipeline-Directions`**: AxisTransform_Apply_RightDirection_LeavesCoordinatesUnchanged,
  AxisTransform_Apply_Right_PlacesTargetEastWithCorrectFaces,
  AxisTransform_Apply_Down_PlacesTargetSouthWithCorrectFaces,
  AxisTransform_Apply_Left_PlacesTargetWestWithCorrectFaces,
  AxisTransform_Apply_Up_PlacesTargetNorthWithCorrectFaces
- **`Rendering-Layout-LayeredPipeline-OrthogonalConnectors`**: AxisTransform_Apply_Right_ProducesOrthogonalWaypoints,
  AxisTransform_Apply_Down_ProducesOrthogonalWaypoints,
  AxisTransform_Apply_Left_ProducesOrthogonalWaypoints,
  AxisTransform_Apply_Up_ProducesOrthogonalWaypoints
- **`Rendering-Layout-LayeredPipeline-CycleBreaking`**: CycleBreaker_Apply_GraphWithCycle_ProducesAcyclicEdgeSet,
  CycleBreaker_Apply_SelfLoopsAndDuplicates_AreRemoved
- **`Rendering-Layout-LayeredPipeline-LayerAssignment`**: LayerAssigner_Apply_LinearChain_AssignsMonotonicLayers,
  LayerAssigner_Apply_DiamondGraph_AssignsLongestPathLayers
- **`Rendering-Layout-LayeredPipeline-LongEdgeSplitting`**: LongEdgeSplitter_Apply_SpanOneEdge_AddsNoDummyNodes,
  LongEdgeSplitter_Apply_LongEdge_InsertsDummyNodesPerIntermediateLayer
- **`Rendering-Layout-LayeredPipeline-CrossingMinimization`**: CrossingMinimizer_Apply_CrossingProneOrdering_ReducesCrossings,
  CrossingMinimizer_Apply_TwoLayerGraph_GroupsNodesByLayer,
  CrossingMinimizer_Apply_AllAugmentedNodesAppearInGroups
- **`Rendering-Layout-LayeredPipeline-CoordinateAssignment`**: BrandesKopfPlacer_Apply_ChainGraph_AssignsCoordinateArrays,
  BrandesKopfPlacer_Apply_ColumnsAreLeftToRightInLayerOrder,
  BrandesKopfPlacer_Apply_SymmetricFork_CentersSourceBetweenTargets
- **`Rendering-Layout-LayeredPipeline-PortDistribution`**: PortDistributor_Apply_SingleEdge_PortsLieWithinNodeFaces,
  PortDistributor_Apply_AssignsPortYForEverySubEdge
- **`Rendering-Layout-LayeredPipeline-OrthogonalRouting`**: OrthogonalRouter_Apply_StraightEdge_ProducesNoBendPoints,
  OrthogonalRouter_Apply_EveryBendListIsEmptyOrVerticalSegment
- **`Rendering-Layout-LayeredPipeline-BackEdgeApproach`**: OrthogonalRouter_DefaultApproach_IsByteIdenticalToLegacy,
  OrthogonalRouter_CustomApproach_PushesEntryStubOutward,
  OrthogonalRouter_ReversedEdge_DefaultApproachClearsClearance,
  OrthogonalRouter_ForwardEdges_GeometryUnchanged,
  OrthogonalRouter_AcyclicGraph_NoApproachChange,
  OrthogonalRouter_DecorationAwareApproach_ClearsMarkerAlongLength
- **`Rendering-Layout-LayeredPipeline-LongEdgeJoining`**: LongEdgeJoiner_Apply_SingleEdge_ProducesWaypointsPerOriginalEdge,
  LongEdgeJoiner_Apply_LongEdge_ConcatenatesSubEdgeBendPoints
- **`Rendering-Layout-LayeredPipeline-ComponentPacking`**: ComponentPacker_Apply_DisconnectedSingletons_PackSeparately,
  ComponentPacker_Apply_ConnectedCore_StaysOneComponent,
  ComponentPacker_Apply_SingleComponent_EqualsDefaultPipeline,
  ComponentPacker_Apply_ComponentOrder_IsDeterministic,
  ComponentPacker_Apply_Waypoints_TranslatedWithComponent,
  ComponentPacker_Apply_EmptyGraph_IsNoOp,
  ComponentPacker_Apply_SingleComponent_ParallelAndSelfEdges_ProducesAlignedWaypoints,
  ComponentPacker_Apply_MultiComponent_ParallelAndSelfEdges_MergesAlignedWaypoints
- **`Rendering-Layout-LayeredPipeline-SharedState`**: LayeredGraph_Constructor_ValidInput_StoresNodesEdgesDirectionAndCount,
  LayeredGraph_Constructor_NullNodes_ThrowsArgumentNullException,
  LayeredGraph_Constructor_NullEdges_ThrowsArgumentNullException
- **`Rendering-Layout-LayeredPipeline-InputValidation`**: LayeredLayoutPipeline_AddStage_NullStage_ThrowsArgumentNullException,
  LayeredLayoutPipeline_Run_NullGraph_ThrowsArgumentNullException
- **`Rendering-Layout-LayeredAlgorithm-Identity`**: Id_IsLayered
- **`Rendering-Layout-LayeredAlgorithm-PlacesAndRoutes`**: Apply_ChainGraph_PlacesLayeredBoxesAndRoutesEdges
- **`Rendering-Layout-LayeredAlgorithm-EmptyGraph`**: Apply_EmptyGraph_ReturnsEmptyCanvas
- **`Rendering-Layout-LayeredAlgorithm-Validation`**: Apply_NullGraph_Throws, Apply_NullOptions_Throws
- **`Rendering-Layout-ConnectorRouting`**: Route_TargetToTheRight_AnchorsFaceEachOther,
  Route_ObstacleBetweenEndpoints_RoutesAroundInterior
- **`Rendering-Layout-EdgeRouting-Selection`**: CoreOptions_EdgeRouting_DefaultsToOrthogonal,
  CoreOptions_EdgeRouting_HasStableId, CoreOptions_EdgeRouting_SelectablePerScope,
  CoreOptions_EdgeRouting_UnsetReturnsDefault
- **`Rendering-Layout-EdgeRouting-Defaults`**: ConnectorRouteOptions_Defaults_AreOrthogonalWithTwelvePixelClearance
- **`Rendering-Layout-ConnectorRouter-AnchorsFaceEachOther`**: Route_TargetToTheRight_AnchorsFaceEachOther,
  Route_TargetBelow_AnchorsFaceEachOther
- **`Rendering-Layout-ConnectorRouter-AvoidsObstacles`**: Route_ObstacleBetweenEndpoints_RoutesAroundInterior
- **`Rendering-Layout-ConnectorRouter-ExcludesEndpoints`**: Route_EndpointBoxes_AreExcludedFromObstacles
- **`Rendering-Layout-ConnectorRouter-CarriesStyling`**: Route_Connection_CarriesRequestedStyling
- **`Rendering-Layout-ConnectorRouter-BatchOrder`**: Route_MultipleConnections_ReturnsOneLinePerConnectionInOrder
- **`Rendering-Layout-ConnectorRouter-Validation`**: Route_NullBoxes_Throws, Route_NullConnections_Throws,
  Route_NullOptions_Throws, Route_NullConnection_Throws
- **`Rendering-Layout-ContainmentPlacement`**: Pack_ItemsFitInRow_PreservesOrderLeftToRight,
  Pack_MixedSizes_ProducesNoOverlaps
- **`Rendering-Layout-ContainmentLayout-Order`**: Pack_ItemsFitInRow_PreservesOrderLeftToRight
- **`Rendering-Layout-ContainmentLayout-NoOverlap`**: Pack_MixedSizes_ProducesNoOverlaps
- **`Rendering-Layout-ContainmentLayout-WithinRegion`**: Pack_MixedSizes_AllChildrenWithinRegion
- **`Rendering-Layout-ContainmentLayout-Wrapping`**: Pack_ChildExceedsWidth_WrapsToNewRow
- **`Rendering-Layout-ContainmentLayout-OversizedChild`**: Pack_OversizedChild_PlacedAloneAndRegionWidens
- **`Rendering-Layout-ContainmentLayout-EmptyInput`**: Pack_EmptyInput_ReturnsPaddingOnlyRegion
- **`Rendering-Layout-ContainmentLayout-PreservesFields`**: Pack_PreservesNonPositionFields
- **`Rendering-Layout-ContainmentLayout-Defaults`**: ContainmentOptions_Defaults_AreSensibleGapsAndPadding
- **`Rendering-Layout-ContainmentLayout-Validation`**: Pack_NullChildren_Throws, Pack_NullOptions_Throws,
  Pack_NullChildElement_Throws
- **`Rendering-Layout-ContainmentAlgorithm`**: Apply_Graph_PacksNodesNonOverlappingInInputOrderWithinCanvas,
  Id_IsContainment
- **`Rendering-Layout-ContainmentAlgorithm-Identity`**: Id_IsContainment
- **`Rendering-Layout-ContainmentAlgorithm-PacksNodes`**: Apply_Graph_PacksNodesNonOverlappingInInputOrderWithinCanvas
- **`Rendering-Layout-ContainmentAlgorithm-RoutesEdges`**: Apply_Graph_RoutesOneConnectorPerEdgeCarryingStyling
- **`Rendering-Layout-ContainmentAlgorithm-RoutesAroundObstacle`**: Apply_EdgeCrossingInterveningBox_RoutesAroundIt
- **`Rendering-Layout-ContainmentAlgorithm-EmptyGraph`**: Apply_EmptyGraph_ReturnsEmptyCanvas
- **`Rendering-Layout-ContainmentAlgorithm-SkipsOutOfGraphEdges`**: Apply_EdgeReferencingOutOfGraphNode_IsSkipped
- **`Rendering-Layout-ContainmentAlgorithm-Validation`**: Apply_NullGraph_Throws, Apply_NullOptions_Throws
- **`Rendering-Layout-HierarchicalLayout`**: Id_IsHierarchical, Apply_FlatRandomGraphs_MatchLayeredAlgorithmExactly
- **`Rendering-Layout-HierarchicalLayout-Identity`**: Id_IsHierarchical
- **`Rendering-Layout-HierarchicalLayout-FlatEquivalence`**: Apply_FlatRandomGraphs_MatchLayeredAlgorithmExactly,
  Apply_FlatRandomGraphs_MatchContainmentAlgorithmExactly
- **`Rendering-Layout-HierarchicalLayout-NestsChildren`**:
  Apply_TwoLevelNesting_SizesContainerAndNestsChildrenAbsolutely, Apply_ThreeLevelNesting_Succeeds
- **`Rendering-Layout-HierarchicalLayout-PerNodeAlgorithm`**: Apply_ContainmentRootWithLayeredContainer_Composes,
  Apply_LayeredRootWithContainmentContainer_Composes
- **`Rendering-Layout-HierarchicalLayout-HierarchyHandling`**:
  Apply_TwoLevelNesting_SizesContainerAndNestsChildrenAbsolutely,
  Apply_CompoundGraph_DoesNotMutateInputNodeSizes
- **`Rendering-Layout-HierarchicalLayout-CrossContainerEdge`**:
  Apply_CrossContainerEdge_RoutesAroundInterveningContainer
- **`Rendering-Layout-HierarchicalLayout-Validation`**: Apply_NullGraph_Throws, Apply_NullOptions_Throws,
  Constructor_NullRegistry_Throws
- **`Rendering-Layout-DefaultLayout`**: CreateDefaultRegistry_RegistersOnlyTheThreeBundledAlgorithms,
  Layout_FlatGraphNoAlgorithmDeclared_MatchesLayeredLeafExactly
- **`Rendering-Layout-DefaultRegistry-BundledAlgorithms`**: CreateDefaultRegistry_ResolvesLayeredAlgorithm,
  CreateDefaultRegistry_ResolvesContainmentAlgorithm, CreateDefaultRegistry_ResolvesHierarchicalAlgorithm,
  CreateDefaultRegistry_RegistersOnlyTheThreeBundledAlgorithms, CreateDefaultRegistry_ReturnsIndependentInstances
- **`Rendering-Layout-LayoutEngine-DefaultAlgorithm`**: DefaultAlgorithmId_IsHierarchical,
  Layout_FlatGraphNoAlgorithmDeclared_MatchesLayeredLeafExactly
- **`Rendering-Layout-LayoutEngine-Resolution`**: Layout_OptionsDeclareLayered_MatchesLayeredAlgorithmExactly,
  Layout_OptionsDeclareContainment_MatchesContainmentAlgorithmExactly, Layout_GraphDeclarationOverridesOptions
- **`Rendering-Layout-LayoutEngine-FlatEquivalence`**: Layout_FlatGraphNoAlgorithmDeclared_MatchesLayeredLeafExactly,
  Layout_OptionsDeclareContainment_MatchesContainmentAlgorithmExactly
- **`Rendering-Layout-LayoutEngine-NestedComposition`**: Layout_NestedGraphNoAlgorithmDeclared_ProducesComposedTree
- **`Rendering-Layout-LayoutEngine-CustomRegistry`**: Layout_CustomRegistry_ResolvesRegisteredAlgorithm,
  Layout_UnregisteredAlgorithm_Throws
- **`Rendering-Layout-LayoutEngine-Validation`**: Layout_NullGraph_Throws, Layout_NullOptions_Throws,
  Layout_NullRegistry_Throws
