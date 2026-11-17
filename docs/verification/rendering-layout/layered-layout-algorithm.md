## LayeredLayoutAlgorithm Unit Verification

Part of the Rendering Layout Verification.

This document maps the layered-layout-algorithm unit requirements to named test scenarios.

### Verification Approach

`LayeredLayoutAlgorithm` is verified by direct xUnit unit tests that call `ApplyCore(graph, options)` on
synthetic `LayoutGraph` inputs. The tests exercise the real ELK-style layered pipeline end-to-end
(no stage is mocked) so identity, placement, routing, and direction handling are all observed on
production code paths.

### Test Environment

- **Framework**: xUnit v4 running on the .NET SDK.
- **Runner**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Project**: `test/DemaConsulting.Rendering.Layout.Tests/LayeredLayoutAlgorithmTests.cs`, plus
  `NotationPrimitivePassThroughTests.cs` (appearance pass-through) and `BackEdgeEntryApproachTests.cs`
  (back-edge entry approach and label clearance) in the same project.
- **Dependencies**: no external services, files, or network access; every test constructs its own
  in-memory `LayoutGraph` and `LayoutOptions` instances.
- **Isolation**: each test builds its own inputs; the algorithm is stateless between calls.

### Acceptance Criteria

A verification run passes when every named scenario below asserts without unexpected exception, and
the referenced tests cover each `Rendering-Layout-LayeredAlgorithm-*` requirement. Any drift in the
stable identifier (`"layered"`), in the one-box-per-node / one-line-per-edge placement contract, in
empty-graph handling, in flow-direction honoring, in shape-aware endpoint projection/exclusion, in the
node-appearance pass-through (a present appearance must reach the placed box, an absent one must stay
`null`), in the back-edge final-leg length (every connector's final leg must cover the rendered end
marker plus the corner radius plus the clean-leg margin for every direction and marker style, while an
undecorated graph keeps exactly the connector-clearance approach and forward legs never change), in the
connector-label clearance of node borders, or in the argument-null validation behavior constitutes a
failure.

### Test Scenarios

- **Identity** (`Rendering-Layout-LayeredAlgorithm-Identity`): `Id_IsLayered` asserts the algorithm
  reports the stable `"layered"` identifier.
- **Places and routes** (`Rendering-Layout-LayeredAlgorithm-PlacesAndRoutes`):
  `Apply_ChainGraph_PlacesLayeredBoxesAndRoutesEdges` confirms one placed box per node and one routed
  connector per edge for a chain graph.
- **Empty graph** (`Rendering-Layout-LayeredAlgorithm-EmptyGraph`):
  `Apply_EmptyGraph_ReturnsEmptyCanvas` confirms an empty graph yields an empty placed layout tree.
- **Direction** (`Rendering-Layout-LayeredAlgorithm-Direction`):
  `Apply_DownDirection_FlowsTopToBottom` confirms a downward flow stacks the chain in strictly
  increasing Y on a taller-than-wide canvas; `Apply_DownDirection_DiffersFromRight` guards against the
  option being ignored by confirming the downward layout is genuinely different from the rightward one;
  `Apply_DownDirectionOnGraphScope_IsHonored` confirms the direction is honored when carried on the
  graph scope; and `Apply_DefaultDirection_FlowsLeftToRight` confirms the unset default still flows
  left-to-right so existing callers are unaffected.
- **Validation** (`Rendering-Layout-LayeredAlgorithm-Validation`): `Apply_NullGraph_Throws` confirms
  a null graph argument is rejected with an argument-null error, and `Apply_NullOptions_Throws`
  confirms a null options argument is likewise rejected with an argument-null error.
- **Merge parallel edges** (`Rendering-Layout-LayeredAlgorithm-MergeParallelEdges`):
  `Apply_ParallelEdges_MergeParallelEdgesDefaultTrue_EmitsExactlyOneLine` confirms three parallel edges collapse to
  exactly one `LayoutLine` at the default; `Apply_ParallelEdges_MergeParallelEdgesFalse_RetainsEveryEdge` confirms
  setting the option to `false` retains all three as independently-labeled lines;
  `Apply_MergeParallelEdges_GraphOverridesOptions` confirms a graph-scope value takes precedence over an options-scope
  value; `Apply_ParallelEdges_MergeParallelEdgesDefaultTrue_OmitsLabelOnCollapse` confirms the merged line's label is
  `null` (omitted) when 3 labeled parallel edges collapse into it, rather than keeping any single surviving edge's
  label; and `Apply_SingleEdge_MergeParallelEdgesDefaultTrue_KeepsOwnLabel` confirms a genuinely non-duplicated single
  edge still keeps its own label.
- **Port emission** (`Rendering-Layout-LayeredAlgorithm-PortEmission`):
  `Apply_EdgeWithPortEndpoint_EmitsLayoutPortWithExternalLabel` confirms an edge whose endpoint is a
  named `LayoutGraphPort` emits exactly one `LayoutPort` at the routed anchor, carrying the port's
  `ExternalLabel`; `Apply_EdgeWithPortEndpoint_ComputesFiniteMaxLabelWidth` confirms the emitted
  port's `MaxLabelWidth` is finite and bounded to roughly half the owning box's placed width.
- **Content inset** (`Rendering-Layout-LayeredAlgorithm-ContentInset`):
  `Apply_NodeWithLeftPort_ComputesNonZeroContentInsetLeft` confirms a node with a labeled left-side
  port receives a positive `ContentInsetLeft` while its other sides and a port-free sibling box stay
  zero.
- **Auto-grow minimum size** (`Rendering-Layout-LayeredAlgorithm-AutoGrowMinimumSize`):
  `Apply_NodeAlreadyLargeEnough_SizeUnchanged` confirms a node whose caller-supplied size already
  meets the computed minimum is left completely unchanged (no shrinking, ever);
  `Apply_NodeWithTopAndBottomPorts_TooSmall_AutoGrowsHeight` confirms an undersized node with a
  title and both a top and a bottom port is grown taller than its caller-supplied height, and that
  its `ContentInsetTop` is widened enough that a renderer's title-start position clears the top
  port's own rendered label; `Apply_AutoGrownNode_DoesNotOverlapSiblings` confirms a grown node's
  placed rectangle never overlaps an already-placed sibling; and
  `Apply_NoNodeNeedsGrowth_PassTwoSkipped_LayoutUnaffected` confirms a layout where no node needs
  growth is completely unaffected by the auto-grow mechanism (pass 2 never runs).
- **Parallel-label spacing** (`Rendering-Layout-LayeredAlgorithm-ParallelLabelSpacing`):
  `Apply_ThreeParallelLabeledEdges_LabelsLandOnTheirOwnLine` lays out 3 independent labeled parallel
  connectors between the same two boxes (`MergeParallelEdges = false`) and asserts, for each line,
  that `ConnectorLabelPlacer.Place`'s chosen label Y coordinate matches that line's own straight
  Y-coordinate (confirming the first-pass, no-nudge placement succeeds for every label instead of
  one colliding and being displaced far from its own line), and that adjacent parallel lanes end up
  spaced at least a full `ConnectorLabelPlacer.EstimateLabelHeight` apart.
  `Apply_ThreeParallelLabeledEdges_Down_BoxWidthGrowsAndLabelsLandOnTheirOwnLine` is the vertical-flow
  (`Direction.Down`) mirror: it lays out the same 3 parallel labeled connectors but attaching to the
  Top/Bottom faces (where `PortDistributor` spreads anchors horizontally instead of vertically), and
  asserts both boxes' Width (not Height) auto-grows past the caller-supplied size, that each label's
  chosen X coordinate matches its own line's straight X coordinate, and that adjacent parallel lanes
  end up spaced at least the widest label's `ConnectorLabelPlacer.EstimateLabelWidth` apart —
  confirming the auto-grow floor correctly grows the axis `PortDistributor` actually spreads anchors
  along for a Top/Bottom face, not just a Left/Right one.
- **Port-label width floor** (`Rendering-Layout-LayeredAlgorithm-PortLabelWidthFloor`):
  `Apply_NodeWithLongLeftPortLabel_GrowsWidthEnoughToAvoidSqueeze` reproduces the
  `ports-showcase-horizontal` gallery repro directly: a small caller-supplied box receives a long
  left-port `ExternalLabel`, and the test asserts both that the box's `Width` grows past its
  caller-supplied size and that the emitted `LayoutPort.MaxLabelWidth` is at least the label's own
  width as measured by the same shared `Rendering.Abstractions.PortLabelWidthEstimator.MeasureWidth`
  the production code uses (i.e. no squeeze is needed).
  `Apply_NodeWithLongLeftAndRightPortLabels_BothSidesAvoidSqueeze` confirms the `Math.Max` composition
  across both sides: a node with long labeled ports on both its left and right faces grows wide
  enough that both sides' `MaxLabelWidth` independently satisfy the no-squeeze bound, proving neither
  side's floor starves the other.
- **Shape-aware routing** (`Rendering-Layout-LayeredAlgorithm-ShapeAwareRouting`):
  `Apply_DownDirection_FolderTarget_ProjectsEndpointToRecessedTop` and
  `Apply_UpDirection_FolderSource_ProjectsEndpointToRecessedTop` confirm a `BoxShape.Folder` node's
  routed endpoint on its real Top face is projected inward to the recessed body outline;
  `Apply_RightDirection_FolderTarget_LeftFaceUnaffectedByTab` and
  `Apply_LeftDirection_FolderTarget_RightFaceUnaffectedByTab` confirm the folder tab never spuriously
  affects a face other than the real Top face; `Apply_DownDirection_NoteTarget_ExcludesFoldFromLandingZone`
  confirms a `BoxShape.Note`'s folded-corner strip is excluded from the connector's landing zone; and
  `Apply_RectangleChain_MatchesPreShapeAwarenessOutput` pins the exact placement and routing of a
  plain-`Rectangle` chain to prove the new shape-aware code never engages for the default shape.
- **Appearance pass-through** (`Rendering-Layout-LayeredAlgorithm-AppearancePassThrough`):
  `LayeredLayout_NodeAppearance_ReachesPlacedBox` confirms a node's `BoxAppearance` reference is the
  very instance carried by the placed box, and `LayeredLayout_NodeWithoutAppearance_StaysNull` confirms a
  node with no appearance leaves the placed box's `Appearance` null (the legacy look).
- **Folder tab title reserve** (`Rendering-Layout-LayeredAlgorithm-FolderTabTitleReserve`):
  `LayeredLayout_FolderNameInTabWithTopPort_ReservesNoTitleInset` lays out a folder with a labeled top port
  twice, once with the name drawn in the tab and once in the body, and confirms the in-tab folder's top
  content inset is smaller (the body title band is not reserved for a title that is not drawn there) while
  the body-titled folder still reserves it;
  `LayeredLayout_FolderNameInTabWithKeywordAndTopPort_ReservesTitleInset` and
  `LayeredLayout_FolderNameInTabWithKeywordAndBottomPort_ReservesTitleInset` add a `Keyword` to the in-tab
  folder and confirm its top (respectively bottom) content inset equals the body-titled folder's and
  exceeds the keyword-less in-tab folder's, because the keyword line is still drawn in the body;
  `LayeredLayout_FolderNameInTabWithKeywordAndSmallHeight_GrowsLikeBodyTitled` confirms the port-driven
  growth path also sees the keyword (the in-tab keyword folder's height is at least the body-titled
  folder's).
- **Back-edge entry approach** (`Rendering-Layout-LayeredAlgorithm-BackEdgeEntryApproach`): the tests
  lay out a six-state, seven-transition elevator state machine (two reversed edges among forward ones,
  `MergeParallelEdges = false`) through `LayoutEngine.Layout`.
  `LayeredLayout_BackEdgeWithOpenChevron_FinalLegCoversChevronAndCorner` confirms every connector's final
  leg (forward and reversed) is at least the open chevron's rendered length plus the corner radius plus the
  clean-leg margin; `LayeredLayout_BackEdgeWithOpenChevron_AllDirectionsKeepCleanFinalLeg` repeats the
  check for the Down, Up, Right, and Left flow directions;
  `LayeredLayout_BackEdgeWithAnyMarker_FinalLegCoversRenderedMarker` repeats it for the filled arrow,
  hollow triangle, hollow diamond, four-dot hollow triangle, and circled plus end markers, each sized from
  its own rendered length; and `ResolveBackEdgeEntryApproach_DerivesFromWidestDecoration` pins the
  derivation: the connector clearance for no edges or an undecorated edge, the open-chevron value for a
  chevron edge, and the filled-diamond value when that is the widest decoration.
- **Back-edge entry approach, legacy preserved**
  (`Rendering-Layout-LayeredAlgorithm-BackEdgeEntryApproachLegacyPreserved`):
  `LayeredLayout_BackEdgeWithOpenChevron_ForwardLegsUnchanged` confirms the forward connectors' final
  legs are identical between the decorated and undecorated graph, and
  `LayeredLayout_UndecoratedBackEdge_KeepsLegacyApproach` confirms the reversed edges of the undecorated
  graph end on exactly `ConnectorClearance`.
- **Labels clear of node borders** (`Rendering-Layout-LayeredAlgorithm-LabelsClearOfNodeBorders`):
  `LayeredLayout_ElevatorLabels_DoNotCrossNodeBorders` lays out the decorated elevator, places its
  connector labels with the obstacle-aware `ConnectorLabelPlacer.Place` given the placed boxes, and
  asserts every label rectangle is disjoint from every box.

### Requirements Coverage

- **`Rendering-Layout-LayeredAlgorithm-Identity`**:
  Id_IsLayered
- **`Rendering-Layout-LayeredAlgorithm-PlacesAndRoutes`**:
  Apply_ChainGraph_PlacesLayeredBoxesAndRoutesEdges
- **`Rendering-Layout-LayeredAlgorithm-EmptyGraph`**:
  Apply_EmptyGraph_ReturnsEmptyCanvas
- **`Rendering-Layout-LayeredAlgorithm-Direction`**:
  Apply_DownDirection_FlowsTopToBottom, Apply_DownDirection_DiffersFromRight,
  Apply_DownDirectionOnGraphScope_IsHonored, Apply_DefaultDirection_FlowsLeftToRight
- **`Rendering-Layout-LayeredAlgorithm-Validation`**:
  Apply_NullGraph_Throws, Apply_NullOptions_Throws
- **`Rendering-Layout-LayeredAlgorithm-MergeParallelEdges`**:
  Apply_ParallelEdges_MergeParallelEdgesDefaultTrue_EmitsExactlyOneLine,
  Apply_ParallelEdges_MergeParallelEdgesFalse_RetainsEveryEdge,
  Apply_MergeParallelEdges_GraphOverridesOptions,
  Apply_ParallelEdges_MergeParallelEdgesDefaultTrue_OmitsLabelOnCollapse,
  Apply_SingleEdge_MergeParallelEdgesDefaultTrue_KeepsOwnLabel
- **`Rendering-Layout-LayeredAlgorithm-PortEmission`**:
  Apply_EdgeWithPortEndpoint_EmitsLayoutPortWithExternalLabel,
  Apply_EdgeWithPortEndpoint_ComputesFiniteMaxLabelWidth
- **`Rendering-Layout-LayeredAlgorithm-ContentInset`**:
  Apply_NodeWithLeftPort_ComputesNonZeroContentInsetLeft
- **`Rendering-Layout-LayeredAlgorithm-AutoGrowMinimumSize`**:
  Apply_NodeAlreadyLargeEnough_SizeUnchanged, Apply_NodeWithTopAndBottomPorts_TooSmall_AutoGrowsHeight,
  Apply_AutoGrownNode_DoesNotOverlapSiblings, Apply_NoNodeNeedsGrowth_PassTwoSkipped_LayoutUnaffected
- **`Rendering-Layout-LayeredAlgorithm-ParallelLabelSpacing`**:
  Apply_ThreeParallelLabeledEdges_LabelsLandOnTheirOwnLine,
  Apply_ThreeParallelLabeledEdges_Down_BoxWidthGrowsAndLabelsLandOnTheirOwnLine
- **`Rendering-Layout-LayeredAlgorithm-PortLabelWidthFloor`**:
  Apply_NodeWithLongLeftPortLabel_GrowsWidthEnoughToAvoidSqueeze,
  Apply_NodeWithLongLeftAndRightPortLabels_BothSidesAvoidSqueeze
- **`Rendering-Layout-LayeredAlgorithm-ShapeAwareRouting`**:
  Apply_DownDirection_FolderTarget_ProjectsEndpointToRecessedTop,
  Apply_UpDirection_FolderSource_ProjectsEndpointToRecessedTop,
  Apply_RightDirection_FolderTarget_LeftFaceUnaffectedByTab,
  Apply_LeftDirection_FolderTarget_RightFaceUnaffectedByTab,
  Apply_DownDirection_NoteTarget_ExcludesFoldFromLandingZone,
  Apply_RectangleChain_MatchesPreShapeAwarenessOutput
- **`Rendering-Layout-LayeredAlgorithm-LayoutOnlyEdges`**:
  Apply_LayoutOnlyEdge_AffectsLayeringButEmitsNoLine (two nodes with no edge share a layer; adding only a
  layout-only edge layers the target after the source yet emits no line or port, for both
  `MergeParallelEdges` settings), Apply_LayoutOnlyEdgeParallelToRealEdge_KeepsRealEdgeAndItsLabel
- **`Rendering-Layout-LayeredAlgorithm-RealEdgeNeverHiddenByLayoutOnlyEdge`**:
  Apply_LayoutOnlyEdgeDeclaredFirstWithMergeParallelEdges_EmitsRealConnector (a layout-only `A -> B` edge
  declared FIRST, then a labeled real edge `A -> B` or `B -> A`, with `MergeParallelEdges = true`: exactly
  one line is emitted and it carries the real edge's label and end marker),
  Apply_LayoutOnlyEdgeFirstThenTwoRealParallelEdgesWithMerge_EmitsOneMergedConnector (layout-only first,
  then two real parallel edges: one merged connector survives),
  Apply_LayoutOnlyEdgeFirstWithMergeParallelEdgesFalse_EmitsRealConnectorOnly (guard: without merging,
  only the real connector is emitted)
- **`Rendering-Layout-LayeredAlgorithm-UserEdgeIdNeverTreatedAsLayoutOnly`**:
  Apply_UserEdgeWithOldReservedPrefix_StillEmitsLine (an ordinary edge whose identifier is the literal
  former reserved prefix `"\u0001layout-only:"` plus a suffix still emits its line, for both
  `MergeParallelEdges` settings)
- **`Rendering-Layout-LayeredAlgorithm-AppearancePassThrough`**:
  LayeredLayout_NodeAppearance_ReachesPlacedBox, LayeredLayout_NodeWithoutAppearance_StaysNull
- **`Rendering-Layout-LayeredAlgorithm-FolderTabTitleReserve`**:
  LayeredLayout_FolderNameInTabWithTopPort_ReservesNoTitleInset,
  LayeredLayout_FolderNameInTabWithKeywordAndTopPort_ReservesTitleInset,
  LayeredLayout_FolderNameInTabWithKeywordAndBottomPort_ReservesTitleInset,
  LayeredLayout_FolderNameInTabWithKeywordAndSmallHeight_GrowsLikeBodyTitled
- **`Rendering-Layout-LayeredAlgorithm-BackEdgeEntryApproach`**:
  LayeredLayout_BackEdgeWithOpenChevron_FinalLegCoversChevronAndCorner,
  LayeredLayout_BackEdgeWithOpenChevron_AllDirectionsKeepCleanFinalLeg,
  LayeredLayout_BackEdgeWithAnyMarker_FinalLegCoversRenderedMarker,
  ResolveBackEdgeEntryApproach_DerivesFromWidestDecoration
- **`Rendering-Layout-LayeredAlgorithm-BackEdgeEntryApproachLegacyPreserved`**:
  LayeredLayout_BackEdgeWithOpenChevron_ForwardLegsUnchanged,
  LayeredLayout_UndecoratedBackEdge_KeepsLegacyApproach
- **`Rendering-Layout-LayeredAlgorithm-LabelsClearOfNodeBorders`**:
  LayeredLayout_ElevatorLabels_DoNotCrossNodeBorders
