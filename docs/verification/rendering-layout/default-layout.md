## DefaultLayout Unit Verification

Part of the Rendering Layout Verification.

This document maps the default-layout unit requirements to named test scenarios.

### Verification Approach

`LayoutAlgorithms` (registry factory) and `LayoutEngine` (facade) are stateless static units, so
verification is by direct xUnit unit tests. The tests use the real bundled algorithms
(`LayeredLayoutAlgorithm`, `ContainmentLayoutAlgorithm`, `HierarchicalLayoutAlgorithm`) rather than
mocks so that resolution, flat-graph equivalence, and nested composition are all observed on the
production code paths. A subset of tests deep-compares the facade's `LayoutTree` output with the
leaf algorithm applied directly to guarantee byte-identical behavior for existing callers.

### Test Environment

- **Framework**: xUnit v3 running on the .NET SDK.
- **Runner**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Projects**: `test/DemaConsulting.Rendering.Layout.Tests/LayoutAlgorithmsTests.cs` and
  `test/DemaConsulting.Rendering.Layout.Tests/LayoutEngineTests.cs`.
- **Dependencies**: no external services, files, or network access; every test constructs its own
  in-memory `LayoutGraph` and `LayoutOptions` instances.
- **Isolation**: each test creates fresh inputs and (where applicable) its own registry via
  `LayoutAlgorithms.CreateDefaultRegistry()`, which returns an independent instance per call.

### Acceptance Criteria

A verification run passes when every named scenario below asserts without unexpected exception, and
the referenced tests cover each `Rendering-Layout-DefaultRegistry-*` and
`Rendering-Layout-LayoutEngine-*` requirement. Any drift in the bundled algorithm set, in the
default algorithm identifier (`"hierarchical"`), in the resolution against an explicit graph-level
declaration, in the flat-graph equivalence guarantee, in nested composition, or in the argument-
null validation behavior constitutes a failure.

### Test Scenarios

- **Bundled algorithms** (`Rendering-Layout-DefaultRegistry-BundledAlgorithms`):
  `CreateDefaultRegistry_ResolvesLayeredAlgorithm`, `CreateDefaultRegistry_ResolvesContainmentAlgorithm`,
  `CreateDefaultRegistry_ResolvesHierarchicalAlgorithm`, and `CreateDefaultRegistry_ResolvesAutoAlgorithm`
  confirm each bundled algorithm resolves by its identifier;
  `CreateDefaultRegistry_RegistersOnlyTheFourBundledAlgorithms` confirms exactly those four identifiers
  are present; `CreateDefaultRegistry_ReturnsIndependentInstances` confirms registering into one
  returned registry does not affect a registry from a separate call.
- **Default algorithm** (`Rendering-Layout-LayoutEngine-DefaultAlgorithm`): `DefaultAlgorithmId_IsHierarchical`
  confirms the facade's declared default is `"hierarchical"`;
  `Layout_FlatGraphNoAlgorithmDeclared_MatchesLayeredLeafExactly` confirms an undeclared flat graph is
  laid out through the hierarchical engine to a result identical to the layered leaf algorithm.
- **Resolution** (`Rendering-Layout-LayoutEngine-Resolution`):
  `Layout_GraphDeclaresLayered_MatchesLayeredAlgorithmExactly` and
  `Layout_GraphDeclaresContainment_MatchesContainmentAlgorithmExactly` confirm an explicit graph-level
  declaration selects that algorithm, applied via the resolved algorithm's `Apply`.
- **Flat equivalence** (`Rendering-Layout-LayoutEngine-FlatEquivalence`):
  `Layout_FlatGraphNoAlgorithmDeclared_MatchesLayeredLeafExactly` and
  `Layout_GraphDeclaresContainment_MatchesContainmentAlgorithmExactly` deep-compare the facade's output
  bit-for-bit with the leaf algorithm applied directly, proving the facade changes no existing output.
- **Nested composition** (`Rendering-Layout-LayoutEngine-NestedComposition`):
  `Layout_NestedGraphNoAlgorithmDeclared_ProducesComposedTree` confirms an undeclared nested graph is
  composed so the container box carries its recursively laid-out children.
- **Custom registry** (`Rendering-Layout-LayoutEngine-CustomRegistry`):
  `Layout_CustomRegistry_ResolvesRegisteredAlgorithm` confirms a caller-supplied registry's algorithm is
  resolved and applied; `Layout_UnregisteredAlgorithm_Throws` confirms an unknown identifier surfaces a
  key-not-found error.
- **Validation** (`Rendering-Layout-LayoutEngine-Validation`): `Layout_NullGraph_Throws` and
  `Layout_NullRegistry_Throws` confirm null arguments are rejected with an argument-null error.

### Requirements Coverage

- **`Rendering-Layout-DefaultRegistry-BundledAlgorithms`**:
  CreateDefaultRegistry_ResolvesLayeredAlgorithm, CreateDefaultRegistry_ResolvesContainmentAlgorithm,
  CreateDefaultRegistry_ResolvesHierarchicalAlgorithm, CreateDefaultRegistry_ResolvesAutoAlgorithm,
  CreateDefaultRegistry_RegistersOnlyTheFourBundledAlgorithms,
  CreateDefaultRegistry_ReturnsIndependentInstances
- **`Rendering-Layout-LayoutEngine-DefaultAlgorithm`**:
  DefaultAlgorithmId_IsHierarchical, Layout_FlatGraphNoAlgorithmDeclared_MatchesLayeredLeafExactly
- **`Rendering-Layout-LayoutEngine-Resolution`**:
  Layout_GraphDeclaresLayered_MatchesLayeredAlgorithmExactly,
  Layout_GraphDeclaresContainment_MatchesContainmentAlgorithmExactly
- **`Rendering-Layout-LayoutEngine-FlatEquivalence`**:
  Layout_FlatGraphNoAlgorithmDeclared_MatchesLayeredLeafExactly,
  Layout_GraphDeclaresContainment_MatchesContainmentAlgorithmExactly
- **`Rendering-Layout-LayoutEngine-NestedComposition`**:
  Layout_NestedGraphNoAlgorithmDeclared_ProducesComposedTree
- **`Rendering-Layout-LayoutEngine-CustomRegistry`**:
  Layout_CustomRegistry_ResolvesRegisteredAlgorithm, Layout_UnregisteredAlgorithm_Throws
- **`Rendering-Layout-LayoutEngine-Validation`**:
  Layout_NullGraph_Throws, Layout_NullRegistry_Throws
