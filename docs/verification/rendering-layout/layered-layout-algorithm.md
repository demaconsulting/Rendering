# LayeredLayoutAlgorithm Unit Verification

Part of the Rendering Layout Verification.

This document maps the layered-layout-algorithm unit requirements to named test scenarios.

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

## Requirements Coverage

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
