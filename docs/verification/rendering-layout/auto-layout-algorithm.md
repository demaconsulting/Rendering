## AutoLayoutAlgorithm Unit Verification

Part of the Rendering Layout Verification.

This document maps the auto-layout-algorithm unit requirements to named test scenarios.

### Verification Approach

`AutoLayoutAlgorithm` is verified by direct xUnit unit tests that call `Apply(graph)` on synthetic
`LayoutGraph` inputs, composing the real bundled leaf algorithms (`LayeredLayoutAlgorithm`,
`ContainmentLayoutAlgorithm`, `HierarchicalLayoutAlgorithm`) rather than mocks, so routing, the
fast-path equivalence guarantee, and the multi-group split/pack path are all observed on production
code paths. A dump-string comparison helper (mirroring the pattern in
`HierarchicalLayoutAlgorithmTests.cs`) is used wherever two `LayoutTree` instances must be compared for
content equality, since record equality on a `LayoutTree` falls back to reference equality for its
`List<T>`-backed members.

### Test Environment

- **Framework**: xUnit v3 running on the .NET SDK.
- **Runner**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Project**: `test/DemaConsulting.Rendering.Layout.Tests/AutoLayoutAlgorithmTests.cs`.
- **Dependencies**: no external services, files, or network access; every test constructs its own
  in-memory `LayoutGraph` and `LayoutOptions` instances.
- **Isolation**: each test builds its own inputs; the algorithm is stateless between calls.

### Acceptance Criteria

A verification run passes when every named scenario below asserts without unexpected exception, and
the referenced tests cover each `Rendering-Layout-AutoAlgorithm-*` requirement. Any drift in the stable
identifier (`"auto"`), in the component-routing rule, in the fast-path byte-identity guarantee, in the
multi-group packing behavior, in cascaded-option handling, in empty-graph handling, or in the
argument-null validation behavior constitutes a failure.

### Test Scenarios

- **Identity** (`Rendering-Layout-AutoAlgorithm-Identity`): `Id_IsAuto` asserts the algorithm reports
  the stable `"auto"` identifier.
- **Routes connected cluster** (`Rendering-Layout-AutoAlgorithm-RoutesConnectedCluster`):
  `Apply_SingleConnectedComponent_MatchesLayeredAlgorithmDirectly` confirms a single connected,
  childless component matches the layered algorithm's own output exactly;
  `Apply_SingleNodeWithSelfLoop_RoutesToLayeredNotContainment` confirms a self-loop-only node is routed
  to layered rather than folded into the singleton bucket; `Apply_MultipleDisconnectedClusters_PacksEachClusterWithoutOverlap`
  confirms multiple independent connected clusters are each routed through layered and packed without
  overlapping.
- **Routes container** (`Rendering-Layout-AutoAlgorithm-RoutesContainer`):
  `Apply_IsolatedNodeWithChildren_MatchesHierarchicalAlgorithmDirectly` confirms a single isolated
  container node still routes to hierarchical; `Apply_NestedContainers_RoutesToHierarchical` confirms a
  two-level-deep nesting is still recognized and routed to hierarchical rather than treated as flat.
- **Routes singleton bucket** (`Rendering-Layout-AutoAlgorithm-RoutesSingletonBucket`):
  `Apply_AllIsolatedSingletons_MatchesContainmentAlgorithmDirectly` confirms a graph of nothing but
  childless, edgeless singletons matches the containment algorithm's own output exactly;
  `Apply_ClusterPlusIsolatedSingletons_PacksBothGroupsWithoutOverlap` confirms a mix of one connected
  cluster and several singletons packs the cluster (layered) and the singleton bucket (containment)
  without their bounding boxes overlapping.
- **Fast-path equivalence** (`Rendering-Layout-AutoAlgorithm-FastPathEquivalence`): the same tests
  listed under connected-cluster, container, and singleton-bucket routing above each assert exact
  (dump-string) equality against the corresponding leaf algorithm applied directly, confirming the
  single-group fast path never splits, copies, or repacks a graph unnecessarily.
- **Multi-group packing** (`Rendering-Layout-AutoAlgorithm-MultiGroupPacking`):
  `Apply_ClusterPlusIsolatedSingletons_PacksBothGroupsWithoutOverlap` and
  `Apply_MultipleDisconnectedClusters_PacksEachClusterWithoutOverlap` confirm every group's placed
  boxes stay within the combined canvas and no two groups' boxes overlap.
- **Cascades options** (`Rendering-Layout-AutoAlgorithm-CascadesOptions`):
  `Apply_GraphLevelOptionOverride_AppliesToSplitComponents` confirms a graph-level cascaded option
  still applies to a split-off component; `Apply_GraphDeclaresAutoAlgorithmWithNestedContainerGroup_DoesNotThrow`
  is a regression test confirming that when the graph itself explicitly declares
  `CoreOptions.Algorithm = "auto"` and the multi-group split routes one group to hierarchical with a
  genuinely nested (2+ level) container, the algorithm-selection reset prevents the hierarchical
  algorithm's own nested cascade from attempting to resolve `"auto"` and throwing a lookup error.
- **Empty graph** (`Rendering-Layout-AutoAlgorithm-EmptyGraph`): `Apply_EmptyGraph_ReturnsEmptyTree`
  confirms an empty graph yields an empty placed layout tree.
- **Validation** (`Rendering-Layout-AutoAlgorithm-Validation`): `Apply_NullGraph_ThrowsArgumentNullException`
  and `ApplyCore_NullOptions_ThrowsArgumentNullException` confirm null arguments are rejected with an
  argument-null error.

### Requirements Coverage

- **`Rendering-Layout-AutoAlgorithm-Identity`**:
  Id_IsAuto
- **`Rendering-Layout-AutoAlgorithm-RoutesConnectedCluster`**:
  Apply_SingleConnectedComponent_MatchesLayeredAlgorithmDirectly,
  Apply_SingleNodeWithSelfLoop_RoutesToLayeredNotContainment,
  Apply_MultipleDisconnectedClusters_PacksEachClusterWithoutOverlap
- **`Rendering-Layout-AutoAlgorithm-RoutesContainer`**:
  Apply_IsolatedNodeWithChildren_MatchesHierarchicalAlgorithmDirectly,
  Apply_NestedContainers_RoutesToHierarchical
- **`Rendering-Layout-AutoAlgorithm-RoutesSingletonBucket`**:
  Apply_AllIsolatedSingletons_MatchesContainmentAlgorithmDirectly,
  Apply_ClusterPlusIsolatedSingletons_PacksBothGroupsWithoutOverlap
- **`Rendering-Layout-AutoAlgorithm-FastPathEquivalence`**:
  Apply_SingleConnectedComponent_MatchesLayeredAlgorithmDirectly,
  Apply_AllIsolatedSingletons_MatchesContainmentAlgorithmDirectly,
  Apply_IsolatedNodeWithChildren_MatchesHierarchicalAlgorithmDirectly,
  Apply_NestedContainers_RoutesToHierarchical, Apply_SingleNodeWithSelfLoop_RoutesToLayeredNotContainment
- **`Rendering-Layout-AutoAlgorithm-MultiGroupPacking`**:
  Apply_ClusterPlusIsolatedSingletons_PacksBothGroupsWithoutOverlap,
  Apply_MultipleDisconnectedClusters_PacksEachClusterWithoutOverlap
- **`Rendering-Layout-AutoAlgorithm-CascadesOptions`**:
  Apply_GraphLevelOptionOverride_AppliesToSplitComponents,
  Apply_GraphDeclaresAutoAlgorithmWithNestedContainerGroup_DoesNotThrow
- **`Rendering-Layout-AutoAlgorithm-EmptyGraph`**:
  Apply_EmptyGraph_ReturnsEmptyTree
- **`Rendering-Layout-AutoAlgorithm-Validation`**:
  Apply_NullGraph_ThrowsArgumentNullException, ApplyCore_NullOptions_ThrowsArgumentNullException
