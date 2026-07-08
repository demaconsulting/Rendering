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
  confirms setting the option to `false` retains all three as independently-labeled lines; and
  `Apply_MergeParallelEdges_GraphOverridesOptions` confirms a graph-scope value takes precedence over
  an options-scope value.
- **Port emission** (`Rendering-Layout-LayeredAlgorithm-PortEmission`):
  `Apply_EdgeWithPortEndpoint_EmitsLayoutPortWithExternalLabel` confirms an edge whose endpoint is a
  named `LayoutGraphPort` emits exactly one `LayoutPort` at the routed anchor, carrying the port's
  `ExternalLabel`.
- **Content inset** (`Rendering-Layout-LayeredAlgorithm-ContentInset`):
  `Apply_NodeWithLeftPort_ComputesNonZeroContentInsetLeft` confirms a node with a labeled left-side
  port receives a positive `ContentInsetLeft` while its other sides and a port-free sibling box stay
  zero.
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
  Apply_MergeParallelEdges_GraphOverridesOptions
- **`Rendering-Layout-LayeredAlgorithm-PortEmission`**:
  Apply_EdgeWithPortEndpoint_EmitsLayoutPortWithExternalLabel
- **`Rendering-Layout-LayeredAlgorithm-ContentInset`**:
  Apply_NodeWithLeftPort_ComputesNonZeroContentInsetLeft
- **`Rendering-Layout-LayeredAlgorithm-ShapeAwareRouting`**:
  Apply_DownDirection_FolderTarget_ProjectsEndpointToRecessedTop,
  Apply_UpDirection_FolderSource_ProjectsEndpointToRecessedTop,
  Apply_RightDirection_FolderTarget_LeftFaceUnaffectedByTab,
  Apply_LeftDirection_FolderTarget_RightFaceUnaffectedByTab,
  Apply_DownDirection_NoteTarget_ExcludesFoldFromLandingZone,
  Apply_RectangleChain_MatchesPreShapeAwarenessOutput
