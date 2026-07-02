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
- **`Rendering-Layout-LayeredAlgorithm-Validation`**:
  Apply_NullGraph_Throws, Apply_NullOptions_Throws
