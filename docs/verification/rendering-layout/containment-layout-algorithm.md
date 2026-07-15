## ContainmentLayoutAlgorithm Unit Verification

Part of the Rendering Layout Verification.

This document maps the containment-layout-algorithm unit requirements to named test scenarios.

### Verification Approach

`ContainmentLayoutAlgorithm` is verified by direct xUnit unit tests that call `Apply(graph,
options)` on synthetic `LayoutGraph` inputs. The tests use the real `ContainmentPacker` and
`ConnectorRouter` collaborators (no mocks) so identity, packing, routing (including obstacle
avoidance), and validation are all observed on production code paths.

### Test Environment

- **Framework**: xUnit v3 running on the .NET SDK.
- **Runner**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Project**: `test/DemaConsulting.Rendering.Layout.Tests/ContainmentLayoutAlgorithmTests.cs`, with the
  shared corridor-width helper covered by
  `test/DemaConsulting.Rendering.Layout.Tests/Engine/EdgeCountGapWidenerTests.cs`.
- **Dependencies**: no external services, files, or network access; every test constructs its own
  in-memory `LayoutGraph` and `LayoutOptions` instances.
- **Isolation**: each test builds its own inputs; the algorithm is stateless between calls.

### Acceptance Criteria

A verification run passes when every named scenario below asserts without unexpected exception, and
the referenced tests cover each `Rendering-Layout-ContainmentAlgorithm-*` requirement. Any drift in
the stable identifier (`"containment"`), in the one-box-per-node placement contract, in
edge-per-input-edge routing with styling passthrough, in obstacle avoidance for intervening boxes,
in empty-graph handling, in skipping of out-of-graph edges, or in the argument-null validation
behavior constitutes a failure.

### Test Scenarios

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
- **Honors scope edge routing** (`Rendering-Layout-ContainmentAlgorithm-HonorsScopeEdgeRouting`):
  `Apply_EdgeRoutingOverrideOnGraphScope_IsHonored` confirms an explicit `CoreOptions.EdgeRouting`
  override carried on the graph itself is honored (routing still succeeds) even when the supplied
  options declares no routing style, mirroring `LayeredLayoutAlgorithm`'s graph-then-options resolution
  of `CoreOptions.Direction`.
- **Edge-count gap widening** (`Rendering-Layout-ContainmentAlgorithm-EdgeCountGapWidening`):
  `Apply_SameRowPeersWithParallelEdges_WidensGapPastNodeSpacing` confirms two peer boxes packed on the
  same row and joined by many parallel edges are spread apart by more than the default node spacing.
- **Vertical stack unaffected by gap widening**
  (`Rendering-Layout-ContainmentAlgorithm-VerticalStackUnaffected`):
  `Apply_VerticalStackWithParallelEdges_LeavesBoxPositionsUnchanged` confirms a vertically stacked
  source-over-target pair keeps byte-identical box positions whether it carries nine edges or none,
  proving the widening is horizontal-only.
- **Corridor width formula** (`Rendering-Layout-ContainmentAlgorithm-CorridorWidthFormula`): the
  `EdgeCountGapWidener` tests (`Widen_ManyConnectors_ReturnsCorridorWidth`, `Widen_TwoConnectors`,
  `Widen_SingleConnector`, `Widen_ZeroConnectors`, `Widen_BaseGapExceedsCorridor`) confirm the shared
  formula the algorithm feeds the packer returns the connector-corridor width and never shrinks the
  supplied base gap.

### Requirements Coverage

- **`Rendering-Layout-ContainmentAlgorithm-Identity`**:
  Id_IsContainment
- **`Rendering-Layout-ContainmentAlgorithm-PacksNodes`**:
  Apply_Graph_PacksNodesNonOverlappingInInputOrderWithinCanvas
- **`Rendering-Layout-ContainmentAlgorithm-RoutesEdges`**:
  Apply_Graph_RoutesOneConnectorPerEdgeCarryingStyling
- **`Rendering-Layout-ContainmentAlgorithm-RoutesAroundObstacle`**:
  Apply_EdgeCrossingInterveningBox_RoutesAroundIt
- **`Rendering-Layout-ContainmentAlgorithm-EmptyGraph`**:
  Apply_EmptyGraph_ReturnsEmptyCanvas
- **`Rendering-Layout-ContainmentAlgorithm-SkipsOutOfGraphEdges`**:
  Apply_EdgeReferencingOutOfGraphNode_IsSkipped
- **`Rendering-Layout-ContainmentAlgorithm-Validation`**:
  Apply_NullGraph_Throws, Apply_NullOptions_Throws
- **`Rendering-Layout-ContainmentAlgorithm-HonorsScopeEdgeRouting`**:
  Apply_EdgeRoutingOverrideOnGraphScope_IsHonored
- **`Rendering-Layout-ContainmentAlgorithm-EdgeCountGapWidening`**:
  Apply_SameRowPeersWithParallelEdges_WidensGapPastNodeSpacing
- **`Rendering-Layout-ContainmentAlgorithm-VerticalStackUnaffected`**:
  Apply_VerticalStackWithParallelEdges_LeavesBoxPositionsUnchanged
- **`Rendering-Layout-ContainmentAlgorithm-CorridorWidthFormula`**:
  Widen_ManyConnectors_ReturnsCorridorWidth, Widen_TwoConnectors, Widen_SingleConnector,
  Widen_ZeroConnectors, Widen_BaseGapExceedsCorridor
