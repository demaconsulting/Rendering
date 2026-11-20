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
- **Project**: `test/DemaConsulting.Rendering.Layout.Tests/HierarchicalLayoutAlgorithmTests.cs`, plus
  `NotationPrimitivePassThroughTests.cs` (appearance pass-through and in-tab folder title reserve) in the
  same project. `DirectionLayoutGeometryTests.cs` and `EdgeKeywordLabelLayoutTests.cs` (with the shared
  `DirectionScenes.cs` and `LayoutGeometryAssert.cs` helpers) cover isolated-node packing and canvas
  containment of connectors and labels. The layout-quality tests (`CrossHierarchyOrderingTests.cs`,
  `HierarchicalCrossOrderingTests.cs`, `OrderRankPlumbingTests.cs`, `LayerSpacingTests.cs`,
  `LayoutCompactnessTests.cs` and `CanvasMarginTests.cs`, over the shared `LayoutQualityScenes.cs`,
  `LayoutQualityMetrics.cs` and `LayoutQualityBaseline.cs` helpers) lay out benchmark scenes (the Repro and nested
  scenes, the general view and the dense usage tree) and measure crossings, canvas area, route quality and margins
  against the numbers of the layout before the change; the Gallery tests in
  `test/DemaConsulting.Rendering.Gallery/GalleryShowcaseTests.cs` repeat the checks on rendered SVG and PNG.
- **Dependencies**: no external services, files, or network access; every test constructs its own
  in-memory `LayoutGraph` and `LayoutOptions` instances. Deterministic pseudo-random graphs use
  fixed seeds so the equivalence suite is reproducible.
- **Isolation**: each test builds its own inputs; the algorithm and its default registry are
  stateless between calls.

### Acceptance Criteria

A verification run passes when every named scenario below asserts without unexpected exception, and
the referenced tests cover each `Rendering-Layout-HierarchicalLayout-*` requirement. Any drift in
the stable identifier (`"hierarchical"`), in the flat-graph byte-equivalence guarantee, in
container sizing (padding, title band, including the absent body title band of a name-in-tab folder
and the honored explicit title height), in the carrying of container and leaf appearance onto placed
boxes, in per-scope algorithm resolution, in cross-container
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
- **Cross-hierarchy clean routing** (`Rendering-Layout-HierarchicalLayout-CrossHierarchyEdge-CleanRouting`; the
  nested-anchor rule's straight-lane, jog and shared-face cases are verified here too): the gallery's two nested
  diagrams are replicated exactly (folder containers, 120 x 50 leaves, `auto` and `hierarchical`/`Down` roots) and
  asserted against the strict criteria that exposed the original defects. For the `auto` diagram (Storage declared
  before API): `Apply_GalleryCrossHierarchyAuto_UiToDb_LongLegsAndNoBorderHugging` (first and final legs at least 24,
  first leg at least 36, at most two bends, no run within 24 px of a Client / Server / Storage border),
  `Apply_GalleryCrossHierarchyAuto_SessionToApi_IsStraight` (one two-waypoint segment) and
  `Apply_GalleryCrossHierarchyAuto_ConnectorsDoNotCrossEachOther` (zero crossings, so Auth to Storage's arrowhead is not
  crossed). For the downward diagram: `Apply_GalleryCrossHierarchyDown_CustomerEdges_CleanAndDirect` (Customer to Cart
  overlaps its target's face by only 13 px, too narrow to draw straight without the arrowhead overhanging a corner, so
  it is a clean Z — legs and interior segments of at least 24, at most two bends, ports clear of the corners, length at
  most 1.25 times the Manhattan distance; Customer to Invoice at most two bends under the same limits),
  `Apply_GalleryCrossHierarchyDown_ToFulfillment_OneBendLongApproachClearOfBorders` (Checkout to Inventory and Payment
  to Shipping use one bend, a final leg of at least 36, and keep 24 px from Fulfillment's border) and
  `Apply_GalleryCrossHierarchyDown_AllConnectors_NoCrossingsAndComfortable` (zero crossings; every connector has legs of
  at least 24, no tiny jog and no border hugging). All of these fail on the fixed-anchor router. The obstacle diagram
  (Source to Sink between outer containers past an intervening container) is replicated by
  `Apply_CrossHierarchyEdge_NoCleanStraightLane_RoutesCleanZBelowObstacle` (50 px endpoints: no lane is both 24 px under
  the Obstacle and 8 px from the face corners, so the connector is a clean Z of at most four bends whose long run is at
  least 24 px under the Obstacle's border, direct within 1.25) and
  `Apply_CrossHierarchyEdge_CleanLaneWithinBothFaces_StaysStraight` (90 px endpoints: the lane 24 px under the Obstacle
  lies inside both faces, so Source to Sink stays one straight segment);
  `Apply_CrossHierarchyEdge_JogRequired_FinalApproachAtLeastArrowPlusRadius` and
  `Apply_CrossHierarchyEdge_SharedNestedFace_PortsStayDistinct` guard the unavoidable-jog final approach and distinct
  ports on a shared nested face. `AssertComfortableConnector` and `AssertCleanRoute`, used by every nested-edge test
  above, additionally enforce that both ports lie at least the marker reach (7.125 px, computed from the theme and
  notation metrics) from the ends of their face — so no arrowhead wing touches or overhangs a corner — and
  `AssertCleanRoute` a first leg of at least the connector stub and a final leg of at least the end marker plus the bend
  radius. The tests assert these properties, not exact waypoints.
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
  `MergeRegionDecomposer_FanOut_EveryDelegatedEdge_IsStrictlyOrthogonalWithNoDirectDiagonal`, which inspect every
  segment of every converging edge and fail on any direct diagonal — the exact full-waypoint check the earlier
  reconciliation approach lacked, both of which fail on that old code and pass on the combined pass.
  `HierarchicalLayoutAlgorithm_ThreeLevelDelegationChain_EndToEnd_ProducesConnectedOrthogonalPath` extends this to a
  three-level delegation chain, confirming the whole chain reads as one connected, strictly orthogonal path from the
  outermost sibling down to the innermost leaf across both boundary crossings.
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
- **Appearance pass-through** (`Rendering-Layout-HierarchicalLayout-AppearancePassThrough`):
  `HierarchicalLayout_ContainerAndLeafAppearance_ReachPlacedBoxes` lays out a container holding a leaf
  and confirms each node's `BoxAppearance` is the very instance carried by its placed box, at both depths.
- **Folder tab title reserve** (`Rendering-Layout-HierarchicalLayout-FolderTabTitleReserve`):
  `HierarchicalLayout_FolderNameInTab_ReservesNoBodyTitleBand` lays out the same labeled folder with a
  child twice, once with the name drawn in the tab and once in the body, and confirms the child sits
  exactly the default container title height (24 px) higher when the name is in the tab;
  `HierarchicalLayout_FolderNameInTabWithExplicitTitleHeight_KeepsReserve` confirms that with an explicit
  `TitleHeight` of 30 px the in-tab folder's child sits at exactly the same offset as the body-titled
  folder's, so the caller's reserve is honored;
  `HierarchicalLayout_FolderNameInTabWithKeyword_ChildClearsKeywordLine` gives the in-tab folder a
  `Keyword` and confirms the child sits at least the tab height plus the keyword band below the folder
  top and at exactly the same offset as the body-titled keyword folder's child (the default reserve is
  kept).

- **End label pass-through** (`Rendering-Layout-HierarchicalLayout-EndLabelPassThrough`):
  `HierarchicalLayout_LeafEdgeEndLabels_ReachPlacedLine` and
  `HierarchicalLayout_CrossContainerEdgeEndLabels_ReachPlacedLine` assert the labels reach the placed line for a
  leaf-scope edge and for an edge between nodes of two different containers.
  `HierarchicalLayout_SizedViewEdgeEndLabels_ReachPlacedLine` adds a container to the scope of the labeled leaf
  edge, which forces the scope to be laid out through the copied sized view (a flat scope is laid out directly).

- **Port appearance pass-through** (`Rendering-Layout-HierarchicalLayout-PortAppearancePassThrough`):
  `HierarchicalLayout_LeafPortAppearance_ReachesPlacedPorts` and
  `HierarchicalLayout_BoundaryPortAppearance_ReachesTheSharedAnchor` assert the appearance reaches a leaf-scope
  port and the shared anchor of a boundary port. `HierarchicalLayout_SizedViewPortAppearance_ReachesPlacedPorts`
  places a container beside the ported leaf edge so the ports are copied onto the sized view with their labels
  and `Appearance` and the placed ports still carry the appearance.

- **Isolated nodes** (`Rendering-Layout-HierarchicalLayout-IsolatedNodesNoOverlap`):
  `HierarchicalLayout_IsolatedNodesInsideAndOutsideFolder_DoNotOverlap` (a theory over Down, Up, Left, and Right)
  lays out isolated non-square nodes both inside and outside a folder and asserts no two boxes overlap.
- **Content inside the canvas** (`Rendering-Layout-HierarchicalLayout-ContentInsideCanvas`):
  `HierarchicalLayout_GeneralView_IsOverlapFreeAndInsideCanvas` and
  `HierarchicalLayout_RootFolderConnectors_StayInsideCanvas` (theories over every direction) assert that every
  connector waypoint, including cross-container connectors detouring around a root folder, lies inside the
  canvas; `HierarchicalLayout_ImportLabels_AreInsideCanvasAndClear`,
  `HierarchicalLayout_ManyImportLabelsBetweenRootFolders_AreInsideCanvasAndClear`, and
  `HierarchicalLayout_KeywordLabeledView_LabelsInsideCanvasAndAnchorsApart` assert that every placed connector
  label (`«import»` and similar) lies inside the canvas and clear of boxes and other labels, with arrowhead
  anchors kept apart; the two Gallery tests
  `Gallery_UpDirectionGeneralView_RendersSvgAndPngInsideCanvasWithoutOverlaps` and
  `Gallery_EdgeKeywordLabels_RendersSvgAndPngWithLabelsInsideCanvas` assert the same on rendered SVG and PNG.

- **Layer spacing cascade** (`Rendering-Layout-HierarchicalLayout-LayerSpacingCascades`):
  `LayoutEngine_Layout_LayerSpacingOnContainerChildren_CascadesToInterior` sets the value on a container's
  `Children` graph and asserts the interior corridor follows it; `..._LayerSpacingOnBoundaryPortScope_IsHonored`
  asserts the boundary-port combined pass honors the value, `..._LayerSpacingZeroOnBoundaryPortScope_KeepsMarkerRun`
  asserts that at a spacing of zero every boundary-port connector still ends in a straight run at least as long as
  its end marker (the synthetic delegation and container-link segments carry the marker approach), and
  `..._LayerSpacingBoundaryPortUnset_SameAsExplicit70`
  asserts an unset value is the same layout as an explicit 70.
- **Order ranks** (`Rendering-Layout-HierarchicalLayout-OrderRanks`):
  `HierarchicalLayoutAlgorithm_OrderRanks_TranslatedThroughSizedView` and `..._InheritedByNestedScope` assert the
  hint reaches the leaf algorithm and nested scopes; `..._DoesNotMutateCallerGraph` asserts the caller's graph is
  untouched; `..._UnrelatedKeys_IdenticalToUnordered` asserts keys that match no node change nothing.
- **Cross-hierarchy ordering** (`Rendering-Layout-HierarchicalLayout-CrossHierarchyOrdering`):
  `LayoutEngine_Layout_Repro2_AnyInsertionOrder_ZeroCrossings` and
  `LayoutEngine_Layout_Repro3_InteriorConnector_ZeroCrossings` (theories over every root, flow direction and insertion
  order) assert zero crossings; `..._Repro3_InteriorMemberInsertedFirst_ZeroCrossings` and
  `..._NestedDepth_LeafPartnerAcrossTwoLevels_ZeroCrossings` cover the member-inserted-first and two-level cases;
  `..._Repro2_InsertionOrderIndependent_SameCrossingsAndCompactCanvas` asserts the same crossings and a compact canvas
  for every insertion order; `LayoutEngine_Layout_GeneralViewUp_AnyInsertionOrder_CrossingsNotAboveMainOrPrototype`
  asserts the general view, in the fixed order and six shuffled orders, never has more crossings than the layout before
  the change or the manual-order prototype (the recorded residual for the dense usage tree, 43 crossings, is an
  unchanged baseline, not a bound met by this work).
- **Ordering is bounded and deterministic** (`Rendering-Layout-HierarchicalLayout-OrderingBounded`):
  `HierarchicalLayoutAlgorithm_ApplyCore_RefinementPassBudget_AtMostSevenLayouts` counts the routed layouts of a
  refined scope; `..._NoCrossContainerEdges_NotRefined` asserts a diagram without cross-container edges is not
  refined; `..._NestedScene_ReusesUnchangedInteriorLayouts` asserts memoization hits;
  `..._WithAndWithoutContext_IdenticalTree` and `LayoutEngine_Layout_LayoutTwice_IdenticalTree` assert
  determinism; `LayoutEngine_Layout_NoCrossHierarchyEdges_FlatGeometryUnchanged` asserts a diagram without
  cross-hierarchy edges keeps its geometry (a golden fingerprint taken before the change, after the margin).
- **Interior-line crossing cost** (`Rendering-Layout-HierarchicalLayout-InteriorConnectorCrossingCost`):
  `HierarchicalLayoutAlgorithm_CountCrossings_CrossingLines_CountsOne`,
  `..._TouchingAndCollinearLines_CountsZero` and `..._InteriorLine_CountsAgainstOuterLine` test the counter; the
  router-side behavior is verified by the `RouteBatch_ExistingInteriorLine_*` tests (see the ConnectorRouter
  verification).
- **Container tightening** (`Rendering-Layout-HierarchicalLayout-ContainerTightening`):
  `LayoutEngine_Layout_Container_TightensToContentPlusPadding` (a theory over the flow directions) asserts each
  container keeps its padding (12 pixels, or the 16.37 pixel arrowhead room where a decorated connector enters)
  from the content and the title band above it, on the scenes whose scope keeps tightening;
  `LayoutEngine_Layout_BenchmarkScene_StrictlyBetterThanBaseline` asserts the improving scenes have a smaller
  canvas together with a smaller empty-square ratio or a larger block-area ratio than before.
- **Tightening route-quality gate** (`Rendering-Layout-HierarchicalLayout-TighteningRouteQualityGate`):
  `LayoutEngine_Layout_BenchmarkScene_RouteQualityNotWorseThanBaseline` asserts, for every benchmark scene,
  connector length and bends within 110% of the layout before the change and hairpins and close parallel runs no
  worse;   `..._NeverWorseThanBaseline` asserts crossings, the largest empty square, the block-area ratio and the canvas
  area never get worse than before the change;
  `..._GeometryStaysClean` asserts no sibling overlap, everything inside the canvas and no label over a box or
  another label.
- **Entry arrowhead room** (`Rendering-Layout-HierarchicalLayout-EntryArrowheadRoom`):
  `LayoutEngine_Layout_BenchmarkScene_MarkersClearContainerBorders` asserts no end marker of any benchmark scene
  (and, through the gallery writer, of any gallery image) overlaps a container border.
- **Anchor separation** (`Rendering-Layout-HierarchicalLayout-AnchorSeparation`):
  `LayoutEngine_Layout_BenchmarkScene_AnchorsStaySeparatedOnSharedFace` asserts that in every benchmark scene any two
  connector ends on one box face are at least 16 pixels apart (NestedPackagesUp once had the «satisfy» connector leave
  RangeReq's top face at the specialization's point, because a re-routing pass offered a connector moved onto that
  face its naive anchor even where a neighbor already sat). NestedPackagesRight and DenseUsageTree keep main's
  recorded residual (connector ends sharing a point on a full face), so their bound is main's, never worse.
- **Canvas padding** (`Rendering-Layout-HierarchicalLayout-CanvasPadding`): `CanvasMarginTests` asserts the margin
  between the content and each canvas edge is the leaf output's padding for the benchmark, cross-hierarchy and
  nested-package scenes in every flow direction and root
  (`LayoutEngine_Layout_BenchmarkScene_CanvasHugsContent`, `..._CrossHierarchyScenes_CanvasHugsContent`,
  `..._NestedPackagesAndGeneralView_CanvasHugsContent`).
- **Routing work budget** (`Rendering-Layout-HierarchicalLayout-RoutingBudget`): `RoutingPerformanceTests` lays out
  the synthetic package (six folders of five leaves, a spanning tree of interior connectors in each, seeded random
  cross-folder connectors declared in the package scope, the shape of the Annex A general views).
  `LayoutEngine_Layout_TinyWorkLimit_FallsBackToElbowRoutesAndWarns` lays out ten connectors under a hard limit of
  6 000 units and confirms every connector is drawn as an orthogonal polyline, the tree's warnings name a limited
  scope that used the elbow route, and two runs produce the same geometry and the same warnings;
  `..._LimitedScope_WarningSurvivesAutoLayoutAndCanvasFit` adds an isolated node so the automatic layout splits and
  repacks the pieces and confirms the warning survives that and the canvas fit;
  `..._ScopeAtWeightLimit_RoutesAtReducedEffortAndWarns` sets the weight limit to 300 (the package weighs 360) and
  confirms the reduced-effort layout is drawn, flagged, reproducible and different from the full-effort one;
  `..._GenerousWorkLimit_EqualsUnlimitedLayout` confirms limits with room to spare give the golden geometry and no
  warning. `..._BenchmarkScene_IsNeverLimitedByDefaultWorkLimits` (a theory over every scene of the
  layout-quality benchmark set) and `..._GeneralViewUp_IsNeverLimitedByDefaultWorkLimits` (every insertion order
  of the heaviest scene) confirm the default limits never touch a benchmark scene.
  `..._OversizeNestedPackage_RoutesEveryScopeOnce` lays out a package whose own scope holds 80 connectors over four
  sub-packages of three folders (each sub-package scope small enough, weight below 600, to be routed in full on its
  own) with a refinement context the test can read, and confirms every connector is drawn orthogonally, no scope
  of the run was routed in more than one pass (the oversize package scope is laid out once, loose from the start),
  the four sub-packages took the reduction over from the package (`InheritedReducedScopes`), and exactly one
  warning appears and says it holds for the scopes inside; `..._NamesOnlyTheOversizeScope` confirms the warning
  names the package's scope and not a sub-package's; `..._OversizeNestedPackage_RepeatedRunsAreIdentical` confirms
  geometry and warnings are the same on a second run; `..._SmallNestedPackage_InheritsNoReduction` confirms the same
  hierarchy with six package connectors inherits nothing and warns of nothing. The `RoutingWorkBudgetTests`
  `..._InheritReduced_*` and `..._ForcedReduced_*` tests confirm an inherited reduction reduces the budget without
  being its own limit, that the scope's own work still reaches its own limits, and that only size (not spent
  work) forces the reduction that descendants inherit.
- **Routing performance** (`Rendering-Layout-HierarchicalLayout-RoutingPerformance`):
  `LayoutEngine_Layout_FortyCrossContainerEdges_CompletesWithinBound` lays out 40 cross-container connectors on a pool
  thread and fails (instead of hanging the run) if the layout takes more than ten seconds; it fails on the version
  before the fix, which needs minutes. `..._ThreeHundredTwentyCrossContainerEdges_CompletesWithinBound` and
  `..._SixHundredFortyCrossContainerEdges_CompletesWithinBound` do the same for 320 connectors (bound ten seconds, about
  one second measured; thirteen seconds before the soft-lane limits) and 640 connectors (bound fifteen seconds, about
  2.6 seconds measured). `..._RoutingWork_GrowsSubQuadraticallyWithEdgeCount` reads no clock: it lays out 160, 320 and
  640 connectors with a refinement context and confirms the largest routing work any scope spent (the budget's count of
  grid nodes and search states) grows by less than 3.5 times per doubling (measured about 3.0 and 2.6; a quadratic
  router would multiply it by four). `..._FortyCrossContainerEdges_RepeatedRunsAreIdentical` lays the package out twice
  and compares the geometry. `..._FiveCrossContainerEdges_MatchesGoldenGeometry` and
  `..._TenCrossContainerEdges_MatchesGoldenGeometry` compare a fingerprint (SHA-256 of every box and connector
  coordinate rounded to three decimals) of the five- and ten-connector packages with the fingerprints captured from the
  unoptimized router, so the exact speed-ups cannot change a coordinate.

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
- **`Rendering-Layout-HierarchicalLayout-AppearancePassThrough`**:
  HierarchicalLayout_ContainerAndLeafAppearance_ReachPlacedBoxes
- **`Rendering-Layout-HierarchicalLayout-FolderTabTitleReserve`**:
  HierarchicalLayout_FolderNameInTab_ReservesNoBodyTitleBand,
  HierarchicalLayout_FolderNameInTabWithExplicitTitleHeight_KeepsReserve,
  HierarchicalLayout_FolderNameInTabWithKeyword_ChildClearsKeywordLine
- **`Rendering-Layout-HierarchicalLayout-EndLabelPassThrough`**:
  HierarchicalLayout_LeafEdgeEndLabels_ReachPlacedLine, HierarchicalLayout_SizedViewEdgeEndLabels_ReachPlacedLine,
  HierarchicalLayout_CrossContainerEdgeEndLabels_ReachPlacedLine
- **`Rendering-Layout-HierarchicalLayout-PortAppearancePassThrough`**:
  HierarchicalLayout_LeafPortAppearance_ReachesPlacedPorts,
  HierarchicalLayout_SizedViewPortAppearance_ReachesPlacedPorts,
  HierarchicalLayout_BoundaryPortAppearance_ReachesTheSharedAnchor
- **`Rendering-Layout-HierarchicalLayout-IsolatedNodesNoOverlap`**:
  HierarchicalLayout_IsolatedNodesInsideAndOutsideFolder_DoNotOverlap
- **`Rendering-Layout-HierarchicalLayout-ContentInsideCanvas`**:
  HierarchicalLayout_GeneralView_IsOverlapFreeAndInsideCanvas, HierarchicalLayout_RootFolderConnectors_StayInsideCanvas,
  HierarchicalLayout_ImportLabels_AreInsideCanvasAndClear,
  HierarchicalLayout_ManyImportLabelsBetweenRootFolders_AreInsideCanvasAndClear,
  HierarchicalLayout_KeywordLabeledView_LabelsInsideCanvasAndAnchorsApart,
  Gallery_UpDirectionGeneralView_RendersSvgAndPngInsideCanvasWithoutOverlaps,
  Gallery_EdgeKeywordLabels_RendersSvgAndPngWithLabelsInsideCanvas
- **`Rendering-Layout-HierarchicalLayout-LayerSpacingCascades`**:
  LayoutEngine_Layout_LayerSpacingOnContainerChildren_CascadesToInterior,
  LayoutEngine_Layout_LayerSpacingOnBoundaryPortScope_IsHonored,
  LayoutEngine_Layout_LayerSpacingZeroOnBoundaryPortScope_KeepsMarkerRun,
  LayoutEngine_Layout_LayerSpacingBoundaryPortUnset_SameAsExplicit70
- **`Rendering-Layout-HierarchicalLayout-OrderRanks`**:
  HierarchicalLayoutAlgorithm_OrderRanks_TranslatedThroughSizedView,
  HierarchicalLayoutAlgorithm_OrderRanks_InheritedByNestedScope,
  HierarchicalLayoutAlgorithm_OrderRanks_DoesNotMutateCallerGraph,
  HierarchicalLayoutAlgorithm_OrderRanks_UnrelatedKeys_IdenticalToUnordered
- **`Rendering-Layout-HierarchicalLayout-CrossHierarchyOrdering`**:
  LayoutEngine_Layout_Repro2_AnyInsertionOrder_ZeroCrossings,
  LayoutEngine_Layout_Repro3_InteriorConnector_ZeroCrossings,
  LayoutEngine_Layout_Repro3_InteriorMemberInsertedFirst_ZeroCrossings,
  LayoutEngine_Layout_Repro2_InsertionOrderIndependent_SameCrossingsAndCompactCanvas,
  LayoutEngine_Layout_NestedDepth_LeafPartnerAcrossTwoLevels_ZeroCrossings,
  LayoutEngine_Layout_GeneralViewUp_AnyInsertionOrder_CrossingsNotAboveMainOrPrototype
- **`Rendering-Layout-HierarchicalLayout-OrderingBounded`**:
  HierarchicalLayoutAlgorithm_ApplyCore_RefinementPassBudget_AtMostSevenLayouts,
  HierarchicalLayoutAlgorithm_ApplyCore_NoCrossContainerEdges_NotRefined,
  HierarchicalLayoutAlgorithm_ApplyCore_NestedScene_ReusesUnchangedInteriorLayouts,
  HierarchicalLayoutAlgorithm_ApplyCore_WithAndWithoutContext_IdenticalTree,
  LayoutEngine_Layout_NoCrossHierarchyEdges_FlatGeometryUnchanged, LayoutEngine_Layout_LayoutTwice_IdenticalTree
- **`Rendering-Layout-HierarchicalLayout-InteriorConnectorCrossingCost`**:
  HierarchicalLayoutAlgorithm_CountCrossings_CrossingLines_CountsOne,
  HierarchicalLayoutAlgorithm_CountCrossings_TouchingAndCollinearLines_CountsZero,
  HierarchicalLayoutAlgorithm_CountCrossings_InteriorLine_CountsAgainstOuterLine
- **`Rendering-Layout-HierarchicalLayout-ContainerTightening`**:
  LayoutEngine_Layout_Container_TightensToContentPlusPadding,
  LayoutEngine_Layout_BenchmarkScene_StrictlyBetterThanBaseline
- **`Rendering-Layout-HierarchicalLayout-TighteningRouteQualityGate`**:
  LayoutEngine_Layout_BenchmarkScene_RouteQualityNotWorseThanBaseline,
  LayoutEngine_Layout_BenchmarkScene_NeverWorseThanBaseline, LayoutEngine_Layout_BenchmarkScene_GeometryStaysClean
- **`Rendering-Layout-HierarchicalLayout-EntryArrowheadRoom`**:
  LayoutEngine_Layout_BenchmarkScene_MarkersClearContainerBorders
- **`Rendering-Layout-HierarchicalLayout-AnchorSeparation`**:
  LayoutEngine_Layout_BenchmarkScene_AnchorsStaySeparatedOnSharedFace
- **`Rendering-Layout-HierarchicalLayout-CanvasPadding`**:
  LayoutEngine_Layout_BenchmarkScene_CanvasHugsContent, LayoutEngine_Layout_CrossHierarchyScenes_CanvasHugsContent,
  LayoutEngine_Layout_NestedPackagesAndGeneralView_CanvasHugsContent
- **`Rendering-Layout-HierarchicalLayout-RoutingBudget`**:
  LayoutEngine_Layout_TinyWorkLimit_FallsBackToElbowRoutesAndWarns,
  LayoutEngine_Layout_GenerousWorkLimit_EqualsUnlimitedLayout,
  LayoutEngine_Layout_ScopeAtWeightLimit_RoutesAtReducedEffortAndWarns,
  LayoutEngine_Layout_LimitedScope_WarningSurvivesAutoLayoutAndCanvasFit,
  LayoutEngine_Layout_BenchmarkScene_IsNeverLimitedByDefaultWorkLimits,
  LayoutEngine_Layout_GeneralViewUp_IsNeverLimitedByDefaultWorkLimits,
  LayoutEngine_Layout_OversizeNestedPackage_RoutesEveryScopeOnce,
  LayoutEngine_Layout_OversizeNestedPackage_RepeatedRunsAreIdentical,
  LayoutEngine_Layout_OversizeNestedPackage_NamesOnlyTheOversizeScope,
  LayoutEngine_Layout_SmallNestedPackage_InheritsNoReduction,
  RoutingWorkBudget_InheritReduced_ReducesWithoutAnOwnLimit,
  RoutingWorkBudget_InheritReduced_OwnWorkStillReachesLimits,
  RoutingWorkBudget_ForcedReduced_OnlyBySizeOrInheritance
- **`Rendering-Layout-HierarchicalLayout-RoutingPerformance`**:
  LayoutEngine_Layout_FortyCrossContainerEdges_CompletesWithinBound,
  LayoutEngine_Layout_ThreeHundredTwentyCrossContainerEdges_CompletesWithinBound,
  LayoutEngine_Layout_SixHundredFortyCrossContainerEdges_CompletesWithinBound,
  LayoutEngine_Layout_RoutingWork_GrowsSubQuadraticallyWithEdgeCount,
  LayoutEngine_Layout_FortyCrossContainerEdges_RepeatedRunsAreIdentical,
  LayoutEngine_Layout_FiveCrossContainerEdges_MatchesGoldenGeometry,
  LayoutEngine_Layout_TenCrossContainerEdges_MatchesGoldenGeometry
