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
empty-graph handling, in flow-direction honoring, or in the argument-null validation behavior
constitutes a failure.

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
