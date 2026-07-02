# ContainmentLayoutAlgorithm Unit Verification

Part of the [Rendering Layout Verification](rendering-layout.md).

This document maps the containment-layout-algorithm unit requirements to named test scenarios.

## ContainmentLayoutAlgorithm Scenarios

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

## Requirements Coverage

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
