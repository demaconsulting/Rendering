## HierarchicalLayoutAlgorithm Unit Verification

Part of the Rendering Layout Verification.

This document maps the hierarchical-layout-algorithm unit requirements to named test scenarios.

### Verification Approach

`HierarchicalLayoutAlgorithm` is verified by direct xUnit unit tests that call `ApplyCore(graph,
options)` on synthetic flat and nested `LayoutGraph` inputs. The tests use the real bundled leaf
algorithms (`LayeredLayoutAlgorithm`, `ContainmentLayoutAlgorithm`) rather than mocks, so
per-scope resolution, container sizing, cross-container routing through `ConnectorRouter`, and the
flat-graph equivalence fast path are all observed on production code paths. A subset of tests
deep-compares the algorithm's `LayoutTree` output against the leaf algorithm applied directly on
hundreds of pseudo-random flat graphs to prove byte-identical behavior.

### Test Environment

- **Framework**: xUnit v4 running on the .NET SDK.
- **Runner**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Project**: `test/DemaConsulting.Rendering.Layout.Tests/HierarchicalLayoutAlgorithmTests.cs`.
- **Dependencies**: no external services, files, or network access; every test constructs its own
  in-memory `LayoutGraph` and `LayoutOptions` instances. Deterministic pseudo-random graphs use
  fixed seeds so the equivalence suite is reproducible.
- **Isolation**: each test builds its own inputs; the algorithm and its default registry are
  stateless between calls.

### Acceptance Criteria

A verification run passes when every named scenario below asserts without unexpected exception, and
the referenced tests cover each `Rendering-Layout-HierarchicalLayout-*` requirement. Any drift in
the stable identifier (`"hierarchical"`), in the flat-graph byte-equivalence guarantee, in
container sizing (padding, title band), in per-scope algorithm resolution, in cross-container
LCA edge routing, in same-scope named-port edge routing (or its ancestor-coordinate translation), in
the boundary (delegation) port combined-pass routing behavior (one shared anchor with every converging
edge routed orthogonally), in the narrowed boundary-crossing port
`NotSupportedException` behavior, or in the argument-null validation
behavior constitutes a failure. Mutation of input node sizes also constitutes a failure.

### Test Scenarios

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
  a box. `HierarchicalLayoutAlgorithm_ThreeLevelChain_InnermostContainerGrows_GrowthCascadesThroughEveryEnclosingLevel`
  confirms the two-pass cascading sizing: when the innermost container must grow, the growth cascades
  outward through every enclosing level's own sizing pass so no ancestor clips its now-larger descendant.
- **Per-node algorithm** (`Rendering-Layout-HierarchicalLayout-PerNodeAlgorithm`):
  `Apply_ContainmentRootWithLayeredContainer_Composes` and
  `Apply_LayeredRootWithContainmentContainer_Composes` confirm a container overriding its algorithm is
  laid out with that algorithm while its parent uses another, composing with nested children.
- **Hierarchy handling** (`Rendering-Layout-HierarchicalLayout-HierarchyHandling`):
  `Apply_TwoLevelNesting_SizesContainerAndNestsChildrenAbsolutely` confirms children are laid out in
  their own coordinate space and composed into the parent at absolute coordinates.
  `Apply_CompoundGraph_DoesNotMutateInputNodeSizes` confirms the engine sizes containers over an
  internal sized view and never mutates the caller's input node dimensions.
  `HierarchicalLayoutAlgorithm_NoBoundaryPortHierarchy_OutputIsByteIdenticalBeforeAndAfterChange`
  confirms the combined-pass gate is additive: a two-container, cross-container-edge hierarchy with no
  boundary port still takes the leaf-composition path (not the combined pass). Its golden snapshot was
  refreshed deliberately when cross-hierarchy edges began steering placement and anchoring on nested
  nodes: the two containers, joined by an edge between their children, now sit side by side instead of
  stacked, and the connector runs between the nested children.
- **Cross-container edge** (`Rendering-Layout-HierarchicalLayout-CrossContainerEdge`):
  `Apply_CrossContainerEdge_RoutesAroundInterveningContainer` confirms an edge between children of
  different sibling containers is routed at the owning scope and no routed segment passes through the
  intervening container's interior. `Apply_CrossContainerEdge_ToWideContainer_DoesNotCrossEndpointBoxes`
  confirms the connector to a wide container's nested child ends on that child and crosses no leaf box
  (the enclosing container's wall is the only boundary it legitimately crosses).
- **Cross-hierarchy nested anchors** (`Rendering-Layout-HierarchicalLayout-CrossHierarchyEdge-NestedAnchors`):
  a shared `AssertCleanRoute` oracle checks a connector is orthogonal, starts and ends on the boundaries
  of the actual nested endpoint boxes, leaves and enters perpendicular and outward from those faces,
  carries the arrowhead at the target end, and never crosses the interior of any box other than the
  endpoints' enclosing containers nor the title text of those containers. It is applied, for both the
  `auto` and `hierarchical` root algorithms, by
  `Apply_CrossHierarchyEdge_TwoLevels_AnchorsOnNestedBoxesAndAvoidsUnrelatedBoxes` (with an unrelated
  third container), `Apply_CrossHierarchyEdge_DepthTwoToDepthOne_AnchorsOnNestedBoxes`,
  `Apply_CrossHierarchyEdge_FourLevelNesting_AnchorsOnInnermostBoxes`,
  `Apply_CrossHierarchyEdge_DeclaredInMiddleContainer_AnchorsOnNestedBoxes` (the edge declared on a
  middle container's own graph),
  `Apply_CrossHierarchyEdges_MixedLeafAndContainerChildren_AnchorOnActualEndpoints` (leaf to nested,
  nested to leaf, nested to nested at one scope), and
  `Apply_CrossHierarchyEdge_ToContainerItself_AnchorsOnContainerBoundary` (an edge naming a container
  stays on its boundary while an edge to a nested node anchors on that node).
  `Apply_CrossHierarchyEdges_ConvergingOnOneNestedNode_UseDistinctAnchors` confirms three connectors
  converging on one nested node end at distinct boundary points.
  `Apply_CrossHierarchyEdge_FromNodeWithInternalEdge_AnchorsApartFromInternalAnchor` confirms a
  connector leaving a nested node that already has an internal connector on the same face does not start
  at the internal connector's end point. `Apply_CrossHierarchyEdges_ConnectorsDoNotOverlapCollinearly`
  confirms two long connectors with aligned endpoint rows share no collinear run (the `auto` root).
  `Apply_CrossHierarchyEdge_WithBoundaryPortHierarchy_AnchorsOnNestedBoxes` confirms the combined-pass
  (boundary-port) path also anchors on the nested nodes.
  `Apply_CrossHierarchyEdge_CustomLeafAlgorithm_ReceivesNoLayoutOnlyEdges` confirms a non-bundled leaf
  algorithm is never handed a layout-only edge yet the nested routing still applies.
  `Apply_EdgeBetweenContainerAndItsOwnDescendant_IsSkipped` confirms the container-to-own-descendant
  edge (which cannot be drawn between two distinct boxes) neither throws nor draws,
  `Apply_NestedPortEdgeAcrossContainers_StillThrowsNotSupported`
  confirms the existing named-port limitation is unchanged, and
  `Apply_CrossHierarchyEdge_DoesNotMutateInputAndIsDeterministic` confirms the caller's graph is
  untouched (no proxy edge leaks into any scope) and repeated runs are identical.
- **Cross-hierarchy ancestor placement** (`Rendering-Layout-HierarchicalLayout-CrossHierarchyEdge-AncestorPlacement`):
  `Apply_CrossHierarchyEdge_LayeredRoot_PlacesJoinedContainersAdjacent` confirms that with a layered
  root two containers joined only by an edge between their nested nodes sit in adjacent layers with an
  unrelated container not between them, and
  `Apply_CrossHierarchyEdges_ContainmentRoot_WidensGapForParallelEdges` confirms a fan of parallel
  nested-endpoint edges widens the gap between two side-by-side containers relative to a single edge.
- **Cross-hierarchy same container** (`Rendering-Layout-HierarchicalLayout-CrossHierarchyEdge-SameContainer`):
  `Apply_CrossHierarchyEdge_RootDeclaredBothEndpointsUnderOneContainer_RoutesBetweenNestedNodes`
  confirms a root-declared edge between nodes in two sub-containers of one top-level container is routed
  between those nested nodes instead of dropped.
- **Cross-hierarchy clean routing** (`Rendering-Layout-HierarchicalLayout-CrossHierarchyEdge-CleanRouting`;
  the nested-anchor rule's straight-lane, jog and shared-face cases are verified here too):
  the gallery's two nested diagrams are replicated exactly (folder containers, 120 x 50 leaves, `auto`
  and `hierarchical`/`Down` roots) and asserted against the strict criteria that exposed the original
  defects. For the `auto` diagram (Storage declared before API): `Apply_GalleryCrossHierarchyAuto_UiToDb_LongLegsAndNoBorderHugging`
  (first and final legs at least 24, first leg at least 36, at most two bends, no run within 24 px of a
  Client / Server / Storage border), `Apply_GalleryCrossHierarchyAuto_SessionToApi_IsStraight` (one two-
  waypoint segment) and `Apply_GalleryCrossHierarchyAuto_ConnectorsDoNotCrossEachOther` (zero crossings,
  so Auth to Storage's arrowhead is not crossed). For the downward diagram:
  `Apply_GalleryCrossHierarchyDown_CustomerEdges_CleanAndDirect` (Customer to Cart overlaps its target's
  face by only 13 px, too narrow to draw straight without the arrowhead overhanging a corner, so it is a
  clean Z — legs and interior segments of at least 24, at most two bends, ports clear of the corners, length
  at most 1.25 times the Manhattan distance; Customer to Invoice at most two bends under the same limits),
  `Apply_GalleryCrossHierarchyDown_ToFulfillment_OneBendLongApproachClearOfBorders` (Checkout to Inventory
  and Payment to Shipping use one bend, a final leg of at least 36, and keep 24 px from Fulfillment's
  border) and `Apply_GalleryCrossHierarchyDown_AllConnectors_NoCrossingsAndComfortable` (zero crossings;
  every connector has legs of at least 24, no tiny jog and no border hugging). All of these fail on the
  fixed-anchor router. The obstacle diagram (Source to Sink between outer containers past an intervening
  container) is replicated by `Apply_CrossHierarchyEdge_NoCleanStraightLane_RoutesCleanZBelowObstacle`
  (50 px endpoints: no lane is both 24 px under the Obstacle and 8 px from the face corners, so the
  connector is a clean Z of at most four bends whose long run is at least 24 px under the Obstacle's
  border, direct within 1.25) and `Apply_CrossHierarchyEdge_CleanLaneWithinBothFaces_StaysStraight` (90 px
  endpoints: the lane 24 px under the Obstacle lies inside both faces, so Source to Sink stays one
  straight segment); `Apply_CrossHierarchyEdge_JogRequired_FinalApproachAtLeastArrowPlusRadius` and
  `Apply_CrossHierarchyEdge_SharedNestedFace_PortsStayDistinct` guard the unavoidable-jog final approach
  and distinct ports on a shared nested face. `AssertComfortableConnector` and `AssertCleanRoute`, used by
  every nested-edge test above, additionally enforce that both ports lie at least the marker reach
  (7.125 px, computed from the theme and notation metrics) from the ends of their face — so no arrowhead
  wing touches or overhangs a corner — and `AssertCleanRoute` a first leg of at least the connector stub
  and a final leg of at least the end marker plus the bend radius. The tests assert these properties, not
  exact waypoints.
- **Layout-only marker is collision-proof**
  (`Rendering-Layout-HierarchicalLayout-LayoutOnlyMarkerIsCollisionProof`):
  `IsLayoutOnlyEdge_MarkedEdgeTrueUnmarkedFalseRegardlessOfId` confirms the marker is keyed by edge
  object identity (a marked edge reports layout-only, an unmarked edge - even one whose identifier is
  the literal former reserved prefix - does not), and
  `Apply_UserLeafEdgeNamedLikeProxyOfRoutedEdge_RendersBoth` (`auto` and `hierarchical` roots) confirms
  a root leaf edge whose identifier is `"\u0001layout-only:e1"` neither collides with the proxy of the
  routed cross-hierarchy edge `e1` (no duplicate-identifier error) nor is dropped: both lines render.
- **Propagates direction** (`Rendering-Layout-HierarchicalLayout-CascadesOptions`):
  `Apply_ContainerWithDirectionOverride_HonorsNestedDirection` sets `CoreOptions.Direction` on a
  container's own children graph (while the top-level options select the default direction) and
  confirms the nested chain is laid out with the container's own override — stacking vertically.
  `Apply_ThreeLevelDirectionCascade_InheritsThroughUnsetMiddleLevel` confirms a direction override set
  two levels up cascades through an intermediate container that sets nothing of its own, reaching a
  third-level leaf chain. `Apply_ThreeLevelDirectionCascade_MidLevelOverrideTakesPrecedence` confirms a
  deeper, explicit override wins over an inherited ancestor value rather than the ancestor's value
  winning because it was set first or higher in the tree.
- **Cascades to every CoreOptions property**
  (`Rendering-Layout-HierarchicalLayout-CascadesAllProperties`):
  `Apply_ThreeLevelEdgeRoutingCascade_ReachesEveryLeafAlgorithmCall` uses a recording leaf-algorithm
  test double to confirm the cascaded effective options snapshot — including `CoreOptions.EdgeRouting`
  and an arbitrary custom marker property proving generality — actually reaches every leaf-algorithm
  invocation across three levels of nesting, with the deepest scope's own override winning over an
  inherited ancestor value.
- **Honors scope edge routing** (`Rendering-Layout-HierarchicalLayout-HonorsScopeEdgeRouting`):
  `Apply_CrossContainerEdge_HonorsScopeEdgeRoutingOverride` confirms a cross-container edge is routed
  using the owning scope's own cascaded `CoreOptions.EdgeRouting` override rather than the root
  options.
- **Same-scope port edges** (`Rendering-Layout-HierarchicalLayout-SameScopePortEdges`):
  `Apply_SameScopePortEdge_WithUnrelatedContainerElsewhere_RoutesLikePortEdge` confirms a port-to-node
  edge between two root-level siblings is routed by the leaf algorithm — emitting exactly one
  `LayoutPort` carrying the port's external label, anchored on the source box's boundary, and exactly
  one connecting `LayoutLine` — even though the scope also contains an unrelated container node
  elsewhere with no edges of its own.
  `Apply_NestedContainerPortEdge_TranslatesPortIntoAncestorCoordinates` confirms a `LayoutPort` emitted
  by a nested container's own leaf pass is correctly translated into the ancestor's absolute
  coordinates when composed, landing within the composed container box's bounds.
- **Boundary port delegation** (`Rendering-Layout-HierarchicalLayout-BoundaryPortDelegation`):
  `Apply_BoundaryPortWithExternalAndInternalEdges_EmitsOneSharedAnchorCarryingBothLabels` confirms a
  container's boundary port resolves to exactly one `LayoutPort` anchor carrying both the external and
  internal labels on the container boundary, with both the external approach edge and the internal
  delegation connector reaching that one anchor.
- **Boundary port fan consolidation** (`Rendering-Layout-HierarchicalLayout-BoundaryPortFanMerge`):
  `Apply_BoundaryPortFanOut_ResolvesToOneSharedAnchor` confirms external and internal fan-out (two
  approach edges and two delegation edges) still consolidate onto one shared anchor.
- **Independent boundary ports** (`Rendering-Layout-HierarchicalLayout-IndependentBoundaryPorts`):
  `Apply_TwoIndependentBoundaryPortsOnOneContainer_EmitsTwoAnchors` confirms two independent boundary
  ports on one container each resolve to their own shared anchor.
  `Apply_TwoIndependentBoundaryPortsWithSharedNullExternalLabel_PreservesConnectorProvenance` and
  `Apply_TwoIndependentBoundaryPortsWithIdenticalExternalLabel_PreservesConnectorProvenance` go beyond
  anchor count and label pairing to assert connector *provenance*: each anchor's external connector is
  traced back to its own true external sibling and its internal connector to its own true child, with no
  cross-wiring, in the case (a shared or both-null `ExternalLabel`) that previously caused label-string
  matching to silently mis-associate the two ports' anchors.
- **Boundary port orthogonal routing** (`Rendering-Layout-HierarchicalLayout-BoundaryPortOrthogonal`):
  Full-waypoint-shape regression is asserted by
  `MergeRegionDecomposer_FanIn_EveryConvergingEdge_IsStrictlyOrthogonalWithNoDirectDiagonal` and
  `MergeRegionDecomposer_FanOut_EveryDelegatedEdge_IsStrictlyOrthogonalWithNoDirectDiagonal`, which
  inspect every segment of every converging edge and fail on any direct diagonal — the exact
  full-waypoint check the earlier reconciliation approach lacked, both of which fail on that old code
  and pass on the combined pass. `HierarchicalLayoutAlgorithm_ThreeLevelDelegationChain_EndToEnd_ProducesConnectedOrthogonalPath`
  extends this to a three-level delegation chain, confirming the whole chain reads as one connected,
  strictly orthogonal path from the outermost sibling down to the innermost leaf across both boundary
  crossings.
- **Sibling-container gap widening** (`Rendering-Layout-HierarchicalLayout-SiblingContainerGapWidening`):
  `Apply_SiblingContainersWithEightCrossEdges_WidensGapToCorridorWidth` confirms two side-by-side peer
  containers joined by eight cross-container edges are spread apart to the shared connector-corridor
  width (so the parallel fan gets distinct routing lanes).
- **Sibling-container gap widening leaves single-edge pairs unaffected**
  (`Rendering-Layout-HierarchicalLayout-SiblingContainerGapWideningSingleEdgeUnaffected`):
  `Apply_SiblingContainersWithSingleCrossEdge_LeavesGapUnwidened` confirms a pair joined by a single
  cross-container edge keeps the unwidened baseline gap.
- **Sibling-container gap widening leaves the boundary-port path unaffected**
  (`Rendering-Layout-HierarchicalLayout-SiblingGapWideningPortPathUnaffected`):
  `HierarchicalLayoutAlgorithm_NoBoundaryPortHierarchy_OutputIsByteIdenticalBeforeAndAfterChange`
  (the existing golden regression, exercising the no-boundary-port path with one cross-container edge)
  continues to pass byte-identical, proving the pass is inert when no pair qualifies, and
  `Apply_BoundaryPortWithParallelApproaches_ResolvesToOneSharedAnchorUnaffectedByWidening` confirms the
  boundary-port path (where the widening is deliberately deferred) resolves its parallel approaches to
  one shared anchor unaffected by the new pass.
- **Boundary port edge throws** (`Rendering-Layout-HierarchicalLayout-BoundaryPortEdgeThrows`):
  `Apply_PortOnNonContainerCrossingIntoDifferentContainer_Throws` confirms a port owned by a plain
  (non-container) node with an edge straight into a different container's nested child — a
  boundary-crossing port edge the delegation mechanism does not cover — throws `NotSupportedException`
  with a message identifying named ports crossing a container boundary as not supported, rather than the
  edge being silently dropped.
- **Validation** (`Rendering-Layout-HierarchicalLayout-ValidatesGraph`,
  `Rendering-Layout-HierarchicalLayout-ValidatesOptions`,
  `Rendering-Layout-HierarchicalLayout-ValidatesRegistry`): `Apply_NullGraph_Throws`,
  `Apply_NullOptions_Throws`, and `Constructor_NullRegistry_Throws` confirm a null graph, null options,
  and null registry are each rejected with an argument-null error.

### Requirements Coverage

- **`Rendering-Layout-HierarchicalLayout-Identity`**:
  Id_IsHierarchical
- **`Rendering-Layout-HierarchicalLayout-FlatEquivalence`**:
  Apply_FlatRandomGraphs_MatchLayeredAlgorithmExactly, Apply_FlatRandomGraphs_MatchContainmentAlgorithmExactly
- **`Rendering-Layout-HierarchicalLayout-NestsChildren`**:
  Apply_TwoLevelNesting_SizesContainerAndNestsChildrenAbsolutely, Apply_ThreeLevelNesting_Succeeds,
  HierarchicalLayoutAlgorithm_ThreeLevelChain_InnermostContainerGrows_GrowthCascadesThroughEveryEnclosingLevel
- **`Rendering-Layout-HierarchicalLayout-PerNodeAlgorithm`**:
  Apply_ContainmentRootWithLayeredContainer_Composes, Apply_LayeredRootWithContainmentContainer_Composes
- **`Rendering-Layout-HierarchicalLayout-HierarchyHandling`**:
  Apply_TwoLevelNesting_SizesContainerAndNestsChildrenAbsolutely, Apply_CompoundGraph_DoesNotMutateInputNodeSizes,
  HierarchicalLayoutAlgorithm_NoBoundaryPortHierarchy_OutputIsByteIdenticalBeforeAndAfterChange
- **`Rendering-Layout-HierarchicalLayout-LayoutOnlyMarkerIsCollisionProof`**:
  IsLayoutOnlyEdge_MarkedEdgeTrueUnmarkedFalseRegardlessOfId,
  Apply_UserLeafEdgeNamedLikeProxyOfRoutedEdge_RendersBoth
- **`Rendering-Layout-HierarchicalLayout-CrossContainerEdge`**:
  Apply_CrossContainerEdge_RoutesAroundInterveningContainer
- **`Rendering-Layout-HierarchicalLayout-CrossHierarchyEdge-CleanRouting`**:
  Apply_GalleryCrossHierarchyAuto_UiToDb_LongLegsAndNoBorderHugging,
  Apply_GalleryCrossHierarchyAuto_SessionToApi_IsStraight,
  Apply_GalleryCrossHierarchyAuto_ConnectorsDoNotCrossEachOther,
  Apply_GalleryCrossHierarchyDown_CustomerEdges_CleanAndDirect,
  Apply_GalleryCrossHierarchyDown_ToFulfillment_OneBendLongApproachClearOfBorders,
  Apply_GalleryCrossHierarchyDown_AllConnectors_NoCrossingsAndComfortable,
  Apply_CrossHierarchyEdge_NoCleanStraightLane_RoutesCleanZBelowObstacle,
  Apply_CrossHierarchyEdge_CleanLaneWithinBothFaces_StaysStraight
- **`Rendering-Layout-HierarchicalLayout-SiblingContainerGapWidening`**:
  Apply_SiblingContainersWithEightCrossEdges_WidensGapToCorridorWidth
- **`Rendering-Layout-HierarchicalLayout-SiblingContainerGapWideningSingleEdgeUnaffected`**:
  Apply_SiblingContainersWithSingleCrossEdge_LeavesGapUnwidened
- **`Rendering-Layout-HierarchicalLayout-SiblingGapWideningPortPathUnaffected`**:
  Apply_BoundaryPortWithParallelApproaches_ResolvesToOneSharedAnchorUnaffectedByWidening,
  HierarchicalLayoutAlgorithm_NoBoundaryPortHierarchy_OutputIsByteIdenticalBeforeAndAfterChange
- **`Rendering-Layout-HierarchicalLayout-CascadesOptions`**:
  Apply_ContainerWithDirectionOverride_HonorsNestedDirection,
  Apply_ThreeLevelDirectionCascade_InheritsThroughUnsetMiddleLevel,
  Apply_ThreeLevelDirectionCascade_MidLevelOverrideTakesPrecedence
- **`Rendering-Layout-HierarchicalLayout-CascadesAllProperties`**:
  Apply_ThreeLevelEdgeRoutingCascade_ReachesEveryLeafAlgorithmCall
- **`Rendering-Layout-HierarchicalLayout-HonorsScopeEdgeRouting`**:
  Apply_CrossContainerEdge_HonorsScopeEdgeRoutingOverride
- **`Rendering-Layout-HierarchicalLayout-SameScopePortEdges`**:
  Apply_SameScopePortEdge_WithUnrelatedContainerElsewhere_RoutesLikePortEdge,
  Apply_NestedContainerPortEdge_TranslatesPortIntoAncestorCoordinates
- **`Rendering-Layout-HierarchicalLayout-BoundaryPortDelegation`**:
  Apply_BoundaryPortWithExternalAndInternalEdges_EmitsOneSharedAnchorCarryingBothLabels
- **`Rendering-Layout-HierarchicalLayout-BoundaryPortFanMerge`**:
  Apply_BoundaryPortFanOut_ResolvesToOneSharedAnchor
- **`Rendering-Layout-HierarchicalLayout-IndependentBoundaryPorts`**:
  Apply_TwoIndependentBoundaryPortsOnOneContainer_EmitsTwoAnchors,
  Apply_TwoIndependentBoundaryPortsWithSharedNullExternalLabel_PreservesConnectorProvenance,
  Apply_TwoIndependentBoundaryPortsWithIdenticalExternalLabel_PreservesConnectorProvenance
- **`Rendering-Layout-HierarchicalLayout-BoundaryPortOrthogonal`**:
  MergeRegionDecomposer_FanIn_EveryConvergingEdge_IsStrictlyOrthogonalWithNoDirectDiagonal,
  MergeRegionDecomposer_FanOut_EveryDelegatedEdge_IsStrictlyOrthogonalWithNoDirectDiagonal,
  HierarchicalLayoutAlgorithm_ThreeLevelDelegationChain_EndToEnd_ProducesConnectedOrthogonalPath
- **`Rendering-Layout-HierarchicalLayout-BoundaryPortEdgeThrows`**:
  Apply_PortOnNonContainerCrossingIntoDifferentContainer_Throws
- **`Rendering-Layout-HierarchicalLayout-ValidatesGraph`**:
  Apply_NullGraph_Throws
- **`Rendering-Layout-HierarchicalLayout-ValidatesOptions`**:
  Apply_NullOptions_Throws
- **`Rendering-Layout-HierarchicalLayout-ValidatesRegistry`**:
  Constructor_NullRegistry_Throws
