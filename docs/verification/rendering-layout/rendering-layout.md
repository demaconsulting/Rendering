# Rendering.Layout Verification

This document describes the system-level verification design for `DemaConsulting.Rendering.Layout` and
links to the subsystem and unit verification documents. Detailed per-requirement scenarios live in the
linked documents below; this file maps only the Rendering.Layout system requirements.

- [Engine Subsystem Verification](engine/engine.md)
- [EdgeRoutingOption Unit Verification](edge-routing-option.md)
- [ConnectorRouter Unit Verification](connector-router.md)
- [ContainmentLayout Unit Verification](containment-layout.md)
- [ContainmentLayoutAlgorithm Unit Verification](containment-layout-algorithm.md)
- [HierarchicalLayoutAlgorithm Unit Verification](hierarchical-layout-algorithm.md)
- [DefaultLayout Unit Verification](default-layout.md)
- [LayeredLayoutAlgorithm Unit Verification](layered-layout-algorithm.md)

## Verification Strategy

Rendering.Layout is verified through deterministic in-process xUnit tests over synthetic layout graphs
and geometry inputs. System coverage is established by representative scenarios for each public system
capability: named layered layout, reusable geometric engines, the staged pipeline, connector routing,
containment placement, containment and hierarchical algorithms, and the default layout facade.

Legacy-oracle and byte-identity routing tests remain unit-level evidence in the linked verification
documents. This system document records only the acceptance criteria and system-requirement coverage.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK.
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Location**: `test/DemaConsulting.Rendering.Layout.Tests/`.
- **Dependencies**: no external services, files, or network access; tests use in-memory graphs.
- **Isolation**: each test builds its own inputs; engines and algorithms are stateless between calls.

## Acceptance Criteria

A verification run passes when every representative system scenario below and every scenario in the
linked subsystem and unit verification documents passes without unexpected exception. Any wrong rectangle,
waypoint, layer assignment, algorithm identity, registry resolution, containment region, or unsupported
input behavior constitutes a failure.

## System Requirements Coverage

The system requirements are satisfied through subsystem and unit scenarios documented in the linked
verification files; representative system-level coverage is:

- **`Rendering-Layout-Algorithm`**: `Apply_ChainGraph_PlacesLayeredBoxesAndRoutesEdges`, `Id_IsLayered`
  (see [LayeredLayoutAlgorithm Unit Verification](layered-layout-algorithm.md)).
- **`Rendering-Layout-GeometricEngines`**: `Apply_ChainGraph_PlacesLayeredBoxesAndRoutesEdges` (see
  [Engine Subsystem Verification](engine/engine.md)).
- **`Rendering-Layout-StagedPipeline`**:
  `LayeredLayoutPipeline_RunDefaultStages_ChainGraph_PopulatesWaypointsWithoutThrowing`,
  `Pipeline_MatchesLegacyOracle_OnRandomGraphs` (see
  [LayeredPipeline Unit Verification](engine/layered-pipeline.md)).
- **`Rendering-Layout-ConnectorRouting`**: `Route_TargetToTheRight_AnchorsFaceEachOther`,
  `Route_ObstacleBetweenEndpoints_RoutesAroundInterior` (see
  [EdgeRoutingOption Unit Verification](edge-routing-option.md) and
  [ConnectorRouter Unit Verification](connector-router.md)).
- **`Rendering-Layout-ContainmentPlacement`**: `Pack_ItemsFitInRow_PreservesOrderLeftToRight`,
  `Pack_MixedSizes_ProducesNoOverlaps` (see
  [ContainmentLayout Unit Verification](containment-layout.md)).
- **`Rendering-Layout-ContainmentAlgorithm`**:
  `Apply_Graph_PacksNodesNonOverlappingInInputOrderWithinCanvas`, `Id_IsContainment` (see
  [ContainmentLayoutAlgorithm Unit Verification](containment-layout-algorithm.md)).
- **`Rendering-Layout-HierarchicalLayout`**: `Id_IsHierarchical`,
  `Apply_FlatRandomGraphs_MatchLayeredAlgorithmExactly` (see
  [HierarchicalLayoutAlgorithm Unit Verification](hierarchical-layout-algorithm.md)).
- **`Rendering-Layout-DefaultLayout`**: `CreateDefaultRegistry_RegistersOnlyTheThreeBundledAlgorithms`,
  `Layout_FlatGraphNoAlgorithmDeclared_MatchesLayeredLeafExactly` (see
  [DefaultLayout Unit Verification](default-layout.md)).
