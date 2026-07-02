# HierarchicalLayoutAlgorithm Unit Verification

Part of the [Rendering Layout Verification](rendering-layout.md).

This document maps the hierarchical-layout-algorithm unit requirements to named test scenarios.

## HierarchicalLayoutAlgorithm Scenarios

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
  a box.
- **Per-node algorithm** (`Rendering-Layout-HierarchicalLayout-PerNodeAlgorithm`):
  `Apply_ContainmentRootWithLayeredContainer_Composes` and
  `Apply_LayeredRootWithContainmentContainer_Composes` confirm a container overriding its algorithm is
  laid out with that algorithm while its parent uses another, composing with nested children.
- **Hierarchy handling** (`Rendering-Layout-HierarchicalLayout-HierarchyHandling`):
  `Apply_TwoLevelNesting_SizesContainerAndNestsChildrenAbsolutely` confirms children are laid out in
  their own coordinate space and composed into the parent at absolute coordinates.
  `Apply_CompoundGraph_DoesNotMutateInputNodeSizes` confirms the engine sizes containers over an
  internal sized view and never mutates the caller's input node dimensions.
- **Cross-container edge** (`Rendering-Layout-HierarchicalLayout-CrossContainerEdge`):
  `Apply_CrossContainerEdge_RoutesAroundInterveningContainer` confirms an edge between children of
  different sibling containers is routed at the owning scope and no routed segment passes through the
  intervening container's interior.
- **Validation** (`Rendering-Layout-HierarchicalLayout-Validation`): `Apply_NullGraph_Throws`,
  `Apply_NullOptions_Throws`, and `Constructor_NullRegistry_Throws` confirm null arguments are rejected
  with an argument-null error.

## Requirements Coverage

- **`Rendering-Layout-HierarchicalLayout-Identity`**:
  Id_IsHierarchical
- **`Rendering-Layout-HierarchicalLayout-FlatEquivalence`**:
  Apply_FlatRandomGraphs_MatchLayeredAlgorithmExactly, Apply_FlatRandomGraphs_MatchContainmentAlgorithmExactly
- **`Rendering-Layout-HierarchicalLayout-NestsChildren`**:
  Apply_TwoLevelNesting_SizesContainerAndNestsChildrenAbsolutely, Apply_ThreeLevelNesting_Succeeds
- **`Rendering-Layout-HierarchicalLayout-PerNodeAlgorithm`**:
  Apply_ContainmentRootWithLayeredContainer_Composes, Apply_LayeredRootWithContainmentContainer_Composes
- **`Rendering-Layout-HierarchicalLayout-HierarchyHandling`**:
  Apply_TwoLevelNesting_SizesContainerAndNestsChildrenAbsolutely, Apply_CompoundGraph_DoesNotMutateInputNodeSizes
- **`Rendering-Layout-HierarchicalLayout-CrossContainerEdge`**:
  Apply_CrossContainerEdge_RoutesAroundInterveningContainer
- **`Rendering-Layout-HierarchicalLayout-Validation`**:
  Apply_NullGraph_Throws, Apply_NullOptions_Throws, Constructor_NullRegistry_Throws
