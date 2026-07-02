# DefaultLayout Unit Verification

Part of the Rendering Layout Verification.

This document maps the default-layout unit requirements to named test scenarios.

## DefaultLayout Scenarios

- **Bundled algorithms** (`Rendering-Layout-DefaultRegistry-BundledAlgorithms`):
  `CreateDefaultRegistry_ResolvesLayeredAlgorithm`, `CreateDefaultRegistry_ResolvesContainmentAlgorithm`,
  and `CreateDefaultRegistry_ResolvesHierarchicalAlgorithm` confirm each bundled algorithm resolves by
  its identifier; `CreateDefaultRegistry_RegistersOnlyTheThreeBundledAlgorithms` confirms exactly those
  three identifiers are present; `CreateDefaultRegistry_ReturnsIndependentInstances` confirms registering
  into one returned registry does not affect a registry from a separate call.
- **Default algorithm** (`Rendering-Layout-LayoutEngine-DefaultAlgorithm`): `DefaultAlgorithmId_IsHierarchical`
  confirms the facade's declared default is `"hierarchical"`;
  `Layout_FlatGraphNoAlgorithmDeclared_MatchesLayeredLeafExactly` confirms an undeclared flat graph is
  laid out through the hierarchical engine to a result identical to the layered leaf algorithm.
- **Resolution** (`Rendering-Layout-LayoutEngine-Resolution`):
  `Layout_OptionsDeclareLayered_MatchesLayeredAlgorithmExactly` and
  `Layout_OptionsDeclareContainment_MatchesContainmentAlgorithmExactly` confirm an explicit options
  declaration selects that algorithm; `Layout_GraphDeclarationOverridesOptions` confirms a graph-level
  declaration takes precedence over the options.
- **Flat equivalence** (`Rendering-Layout-LayoutEngine-FlatEquivalence`):
  `Layout_FlatGraphNoAlgorithmDeclared_MatchesLayeredLeafExactly` and
  `Layout_OptionsDeclareContainment_MatchesContainmentAlgorithmExactly` deep-compare the facade's output
  bit-for-bit with the leaf algorithm applied directly, proving the facade changes no existing output.
- **Nested composition** (`Rendering-Layout-LayoutEngine-NestedComposition`):
  `Layout_NestedGraphNoAlgorithmDeclared_ProducesComposedTree` confirms an undeclared nested graph is
  composed so the container box carries its recursively laid-out children.
- **Custom registry** (`Rendering-Layout-LayoutEngine-CustomRegistry`):
  `Layout_CustomRegistry_ResolvesRegisteredAlgorithm` confirms a caller-supplied registry's algorithm is
  resolved and applied; `Layout_UnregisteredAlgorithm_Throws` confirms an unknown identifier surfaces a
  key-not-found error.
- **Validation** (`Rendering-Layout-LayoutEngine-Validation`): `Layout_NullGraph_Throws`,
  `Layout_NullOptions_Throws`, and `Layout_NullRegistry_Throws` confirm null arguments are rejected with
  an argument-null error.

## Requirements Coverage

- **`Rendering-Layout-DefaultRegistry-BundledAlgorithms`**:
  CreateDefaultRegistry_ResolvesLayeredAlgorithm, CreateDefaultRegistry_ResolvesContainmentAlgorithm,
  CreateDefaultRegistry_ResolvesHierarchicalAlgorithm, CreateDefaultRegistry_RegistersOnlyTheThreeBundledAlgorithms,
  CreateDefaultRegistry_ReturnsIndependentInstances
- **`Rendering-Layout-LayoutEngine-DefaultAlgorithm`**:
  DefaultAlgorithmId_IsHierarchical, Layout_FlatGraphNoAlgorithmDeclared_MatchesLayeredLeafExactly
- **`Rendering-Layout-LayoutEngine-Resolution`**:
  Layout_OptionsDeclareLayered_MatchesLayeredAlgorithmExactly,
  Layout_OptionsDeclareContainment_MatchesContainmentAlgorithmExactly, Layout_GraphDeclarationOverridesOptions
- **`Rendering-Layout-LayoutEngine-FlatEquivalence`**:
  Layout_FlatGraphNoAlgorithmDeclared_MatchesLayeredLeafExactly,
  Layout_OptionsDeclareContainment_MatchesContainmentAlgorithmExactly
- **`Rendering-Layout-LayoutEngine-NestedComposition`**:
  Layout_NestedGraphNoAlgorithmDeclared_ProducesComposedTree
- **`Rendering-Layout-LayoutEngine-CustomRegistry`**:
  Layout_CustomRegistry_ResolvesRegisteredAlgorithm, Layout_UnregisteredAlgorithm_Throws
- **`Rendering-Layout-LayoutEngine-Validation`**:
  Layout_NullGraph_Throws, Layout_NullOptions_Throws, Layout_NullRegistry_Throws
