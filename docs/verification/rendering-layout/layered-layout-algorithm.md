## LayeredLayoutAlgorithm Unit Verification

Part of the Rendering Layout Verification.

This document maps the layered-layout-algorithm unit requirements to named test scenarios.

### Verification Approach

`LayeredLayoutAlgorithm` is verified by direct xUnit unit tests that call `Apply(graph, options)` on
synthetic `LayoutGraph` inputs. The tests exercise the real ELK-style layered pipeline end-to-end
(no stage is mocked) so identity, placement, routing, and direction handling are all observed on
production code paths.

### Test Environment

- **Framework**: xUnit v3 running on the .NET SDK.
- **Runner**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Project**: `test/DemaConsulting.Rendering.Layout.Tests/LayeredLayoutAlgorithmTests.cs`.
- **Dependencies**: no external services, files, or network access; every test constructs its own
  in-memory `LayoutGraph` and `LayoutOptions` instances.
- **Isolation**: each test builds its own inputs; the algorithm is stateless between calls.

### Acceptance Criteria

A verification run passes when every named scenario below asserts without unexpected exception, and
the referenced tests cover each `Rendering-Layout-LayeredAlgorithm-*` requirement. Any drift in the
stable identifier (`"layered"`), in the one-box-per-node / one-line-per-edge placement contract, in
empty-graph handling, in flow-direction honoring, in shape-aware endpoint projection/exclusion, or in
the argument-null validation behavior constitutes a failure.

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
  `Apply_ParallelEdges_MergeParallelEdgesDefaultTrue_EmitsExactlyOneLine` confirms three parallel
  edges collapse to exactly one `LayoutLine` at the default; `Apply_ParallelEdges_MergeParallelEdgesFalse_RetainsEveryEdge`
  confirms setting the option to `false` retains all three as independently-labeled lines;
  `Apply_MergeParallelEdges_GraphOverridesOptions` confirms a graph-scope value takes precedence over
  an options-scope value; `Apply_ParallelEdges_MergeParallelEdgesDefaultTrue_OmitsLabelOnCollapse`
  confirms the merged line's label is `null` (omitted) when 3 labeled parallel edges collapse into
  it, rather than keeping any single surviving edge's label; and
  `Apply_SingleEdge_MergeParallelEdgesDefaultTrue_KeepsOwnLabel` confirms a genuinely
  non-duplicated single edge still keeps its own label.
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
- **`Rendering-Layout-LayeredAlgorithm-ShapeAwareRouting`**:
  Apply_DownDirection_FolderTarget_ProjectsEndpointToRecessedTop,
  Apply_UpDirection_FolderSource_ProjectsEndpointToRecessedTop,
  Apply_RightDirection_FolderTarget_LeftFaceUnaffectedByTab,
  Apply_LeftDirection_FolderTarget_RightFaceUnaffectedByTab,
  Apply_DownDirection_NoteTarget_ExcludesFoldFromLandingZone,
  Apply_RectangleChain_MatchesPreShapeAwarenessOutput
