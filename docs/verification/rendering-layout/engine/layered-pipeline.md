### Layered Pipeline Unit Verification

Part of the Rendering Layout Verification.

This document maps the layered-pipeline unit requirements to named test scenarios.

The pipeline stages are exercised individually, and the assembled pipeline is checked for byte-exact
equivalence with the legacy oracle. Dependencies are real (each stage operates on a `LayeredGraph`);
nothing is mocked.

#### Verification Approach

The `LayeredPipeline` unit — the ELK-style staged Sugiyama pipeline over a `LayeredGraph` — is
verified by direct xUnit unit tests at three levels: (1) each stage is exercised in isolation on a
`LayeredGraph` (cycle breaker, layer assigner, long-edge splitter, crossing minimizer,
Brandes-Köpf placer, port distributor, orthogonal router, long-edge joiner, component packer,
axis transform); (2) the assembled `LayeredLayoutPipeline` is byte-compared to a legacy oracle on
random and named topologies; and (3) input-validation tests confirm null/unsupported inputs are
rejected. No stage is mocked — real `LayeredGraph` instances flow through every check.

#### Test Environment

- **Framework**: xUnit v3 running on the .NET SDK.
- **Runner**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Projects**: `test/DemaConsulting.Rendering.Layout.Tests/Engine/Layered/` (per-stage tests) and
  the pipeline-level tests in the same test project.
- **Dependencies**: no external services, files, or network access. Random-graph and named-topology
  tests use deterministic seeds so byte-identity checks are reproducible. The legacy oracle is a
  reference implementation compiled into the test project.
- **Isolation**: each test builds its own `LayeredGraph`; the pipeline and stages are stateless
  between calls.

#### Acceptance Criteria

A verification run passes when every named scenario below asserts without unexpected exception, and
the referenced tests cover each `Rendering-Layout-LayeredPipeline-*` requirement. Any drift in
per-stage geometry, in byte-identity with the legacy oracle on the random and named topologies,
in supported hierarchy handling (recursive input must throw), in flow directions, in orthogonal
waypoint shape, in back-edge approach behavior, in component packing determinism, in shared
`LayeredGraph` state validation, or in `LayeredLayoutPipeline` input validation constitutes a
failure.

#### Test Scenarios

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
- **Merge parallel edges** (`Rendering-Layout-LayeredPipeline-MergeParallelEdges`):
  `CycleBreaker_Apply_MergeParallelEdgesFalse_RetainsEveryParallelEdgeInstance` confirms every
  parallel edge instance survives into `graph.Acyclic` when the option is `false`, and
  `CycleBreaker_Apply_MergeParallelEdgesDefaultTrue_CollapsesDuplicates` confirms the default `true`
  still collapses duplicates exactly as before this property existed.
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
  `PortDistributor_Apply_SingleEdge_PortsLieWithinNodeFaces`,
  `PortDistributor_Apply_AssignsPortYForEverySubEdge`, and
  `PortDistributor_Apply_SmallFace_PortsLieWithinNodeFacesWithoutThrowing` (a face smaller than the
  clearance band degrades gracefully instead of throwing an inverted-clamp `ArgumentException`).
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
- **Component packing propagates back-edge approach**
  (`Rendering-Layout-LayeredPipeline-PackedComponentsBackEdgeApproach`):
  `ComponentPacker_Apply_MultiComponent_PropagatesBackEdgeEntryApproach` builds two disconnected
  triangle components, each with a short cycle producing a long back edge, and confirms the routed
  back-edge corridor reflects the parent graph's configured `BackEdgeEntryApproach` in every packed
  component instead of always reverting to the class default.
- **Shared state** (`Rendering-Layout-LayeredPipeline-SharedState`):
  `LayeredGraph_Constructor_ValidInput_StoresNodesEdgesDirectionAndCount`,
  `LayeredGraph_Constructor_NullNodes_ThrowsArgumentNullException`, and
  `LayeredGraph_Constructor_NullEdges_ThrowsArgumentNullException`.
- **Input validation** (`Rendering-Layout-LayeredPipeline-InputValidation`):
  `LayeredLayoutPipeline_AddStage_NullStage_ThrowsArgumentNullException` and
  `LayeredLayoutPipeline_Run_NullGraph_ThrowsArgumentNullException`.
- **Shape-aware anchors** (`Rendering-Layout-LayeredPipeline-ShapeAwareAnchors`):
  `PortDistributor_Apply_FolderTargetTopFace_Down_PortExcludesTabRegion`,
  `PortDistributor_Apply_NoteSourceRightFace_Right_PortExcludesFoldRegion`, and
  `PortDistributor_Apply_RectangleNode_MatchesPlainFullSpanFormula` (port-distribution stage);
  `LongEdgeJoiner_Apply_FolderTargetTopFace_Down_ProjectsInwardByTabHeight`,
  `LongEdgeJoiner_Apply_FolderSourceTopFace_Up_ProjectsInwardByTabHeight`,
  `LongEdgeJoiner_Apply_FolderNode_Right_NonTopFaceUnaffected`,
  `LongEdgeJoiner_Apply_FolderNode_Left_NonTopFaceUnaffected`, and
  `LongEdgeJoiner_Apply_RectangleNode_MatchesPlainFormula` (long-edge-joining stage); and the
  full-pipeline (`LayeredLayoutAlgorithm`) confirmations
  `Apply_DownDirection_FolderTarget_ProjectsEndpointToRecessedTop`,
  `Apply_UpDirection_FolderSource_ProjectsEndpointToRecessedTop`,
  `Apply_RightDirection_FolderTarget_LeftFaceUnaffectedByTab`,
  `Apply_LeftDirection_FolderTarget_RightFaceUnaffectedByTab`,
  `Apply_DownDirection_NoteTarget_ExcludesFoldFromLandingZone`, and
  `Apply_RectangleChain_MatchesPreShapeAwarenessOutput`.

#### Requirements Coverage

- **`Rendering-Layout-LayeredPipeline-StagedPipeline`**:
  LayeredLayoutPipeline_RunDefaultStages_ChainGraph_PopulatesWaypointsWithoutThrowing,
  Pipeline_MatchesLegacyOracle_OnRandomGraphs
- **`Rendering-Layout-LayeredPipeline-BehaviorPreserving`**:
  Pipeline_MatchesLegacyOracle_OnRandomGraphs, Pipeline_MatchesLegacyOracle_OnEmptyGraph,
  Pipeline_MatchesLegacyOracle_OnDroneLikeGraph, Pipeline_MatchesLegacyOracle_OnWorkstationLikeGraph,
  Pipeline_MatchesLegacyOracle_OnNamedTopologies
- **`Rendering-Layout-LayeredPipeline-FlatHierarchyOnly`**:
  LayeredLayoutPipeline_Build_RecursiveHierarchy_ThrowsNotSupportedException
- **`Rendering-Layout-LayeredPipeline-Directions`**:
  AxisTransform_Apply_RightDirection_LeavesCoordinatesUnchanged,
  AxisTransform_Apply_Right_PlacesTargetEastWithCorrectFaces,
  AxisTransform_Apply_Down_PlacesTargetSouthWithCorrectFaces,
  AxisTransform_Apply_Left_PlacesTargetWestWithCorrectFaces, AxisTransform_Apply_Up_PlacesTargetNorthWithCorrectFaces
- **`Rendering-Layout-LayeredPipeline-OrthogonalConnectors`**:
  AxisTransform_Apply_Right_ProducesOrthogonalWaypoints, AxisTransform_Apply_Down_ProducesOrthogonalWaypoints,
  AxisTransform_Apply_Left_ProducesOrthogonalWaypoints, AxisTransform_Apply_Up_ProducesOrthogonalWaypoints
- **`Rendering-Layout-LayeredPipeline-CycleBreaking`**:
  CycleBreaker_Apply_GraphWithCycle_ProducesAcyclicEdgeSet, CycleBreaker_Apply_SelfLoopsAndDuplicates_AreRemoved
- **`Rendering-Layout-LayeredPipeline-MergeParallelEdges`**:
  CycleBreaker_Apply_MergeParallelEdgesFalse_RetainsEveryParallelEdgeInstance,
  CycleBreaker_Apply_MergeParallelEdgesDefaultTrue_CollapsesDuplicates
- **`Rendering-Layout-LayeredPipeline-LayerAssignment`**:
  LayerAssigner_Apply_LinearChain_AssignsMonotonicLayers, LayerAssigner_Apply_DiamondGraph_AssignsLongestPathLayers
- **`Rendering-Layout-LayeredPipeline-LongEdgeSplitting`**:
  LongEdgeSplitter_Apply_SpanOneEdge_AddsNoDummyNodes,
  LongEdgeSplitter_Apply_LongEdge_InsertsDummyNodesPerIntermediateLayer
- **`Rendering-Layout-LayeredPipeline-CrossingMinimization`**:
  CrossingMinimizer_Apply_CrossingProneOrdering_ReducesCrossings,
  CrossingMinimizer_Apply_TwoLayerGraph_GroupsNodesByLayer, CrossingMinimizer_Apply_AllAugmentedNodesAppearInGroups
- **`Rendering-Layout-LayeredPipeline-CoordinateAssignment`**:
  BrandesKopfPlacer_Apply_ChainGraph_AssignsCoordinateArrays,
  BrandesKopfPlacer_Apply_ColumnsAreLeftToRightInLayerOrder,
  BrandesKopfPlacer_Apply_SymmetricFork_CentersSourceBetweenTargets
- **`Rendering-Layout-LayeredPipeline-PortDistribution`**:
  PortDistributor_Apply_SingleEdge_PortsLieWithinNodeFaces, PortDistributor_Apply_AssignsPortYForEverySubEdge,
  PortDistributor_Apply_SmallFace_PortsLieWithinNodeFacesWithoutThrowing
- **`Rendering-Layout-LayeredPipeline-OrthogonalRouting`**:
  OrthogonalRouter_Apply_StraightEdge_ProducesNoBendPoints,
  OrthogonalRouter_Apply_EveryBendListIsEmptyOrVerticalSegment
- **`Rendering-Layout-LayeredPipeline-BackEdgeApproach`**:
  OrthogonalRouter_DefaultApproach_IsByteIdenticalToLegacy, OrthogonalRouter_CustomApproach_PushesEntryStubOutward,
  OrthogonalRouter_ReversedEdge_DefaultApproachClearsClearance, OrthogonalRouter_ForwardEdges_GeometryUnchanged,
  OrthogonalRouter_AcyclicGraph_NoApproachChange, OrthogonalRouter_DecorationAwareApproach_ClearsMarkerAlongLength
- **`Rendering-Layout-LayeredPipeline-LongEdgeJoining`**:
  LongEdgeJoiner_Apply_SingleEdge_ProducesWaypointsPerOriginalEdge,
  LongEdgeJoiner_Apply_LongEdge_ConcatenatesSubEdgeBendPoints
- **`Rendering-Layout-LayeredPipeline-ComponentPacking`**:
  ComponentPacker_Apply_DisconnectedSingletons_PackSeparately, ComponentPacker_Apply_ConnectedCore_StaysOneComponent,
  ComponentPacker_Apply_SingleComponent_EqualsDefaultPipeline, ComponentPacker_Apply_ComponentOrder_IsDeterministic,
  ComponentPacker_Apply_Waypoints_TranslatedWithComponent, ComponentPacker_Apply_EmptyGraph_IsNoOp,
  ComponentPacker_Apply_SingleComponent_ParallelAndSelfEdges_ProducesAlignedWaypoints,
  ComponentPacker_Apply_MultiComponent_ParallelAndSelfEdges_MergesAlignedWaypoints
- **`Rendering-Layout-LayeredPipeline-PackedComponentsBackEdgeApproach`**:
  ComponentPacker_Apply_MultiComponent_PropagatesBackEdgeEntryApproach
- **`Rendering-Layout-LayeredPipeline-SharedState`**:
  LayeredGraph_Constructor_ValidInput_StoresNodesEdgesDirectionAndCount,
  LayeredGraph_Constructor_NullNodes_ThrowsArgumentNullException,
  LayeredGraph_Constructor_NullEdges_ThrowsArgumentNullException
- **`Rendering-Layout-LayeredPipeline-InputValidation`**:
  LayeredLayoutPipeline_AddStage_NullStage_ThrowsArgumentNullException,
  LayeredLayoutPipeline_Run_NullGraph_ThrowsArgumentNullException
- **`Rendering-Layout-LayeredPipeline-ShapeAwareAnchors`**:
  PortDistributor_Apply_FolderTargetTopFace_Down_PortExcludesTabRegion,
  PortDistributor_Apply_NoteSourceRightFace_Right_PortExcludesFoldRegion,
  PortDistributor_Apply_RectangleNode_MatchesPlainFullSpanFormula,
  LongEdgeJoiner_Apply_FolderTargetTopFace_Down_ProjectsInwardByTabHeight,
  LongEdgeJoiner_Apply_FolderSourceTopFace_Up_ProjectsInwardByTabHeight,
  LongEdgeJoiner_Apply_FolderNode_Right_NonTopFaceUnaffected,
  LongEdgeJoiner_Apply_FolderNode_Left_NonTopFaceUnaffected,
  LongEdgeJoiner_Apply_RectangleNode_MatchesPlainFormula,
  Apply_DownDirection_FolderTarget_ProjectsEndpointToRecessedTop,
  Apply_UpDirection_FolderSource_ProjectsEndpointToRecessedTop,
  Apply_RightDirection_FolderTarget_LeftFaceUnaffectedByTab,
  Apply_LeftDirection_FolderTarget_RightFaceUnaffectedByTab,
  Apply_DownDirection_NoteTarget_ExcludesFoldFromLandingZone,
  Apply_RectangleChain_MatchesPreShapeAwarenessOutput
