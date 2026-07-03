# Rendering Abstractions Verification

This document describes the system-level verification design for the
`DemaConsulting.Rendering.Abstractions` system and links to the per-unit verification documents for
its six units. It records the verification strategy, test environment, and acceptance criteria shared
by every unit, and maps each system-level requirement to named test scenarios. The detailed
per-requirement scenarios live in the unit documents:

- Rendering Contracts Unit Verification
- Registries Unit Verification
- Theme Unit Verification
- Notation Metrics Unit Verification
- Box Metrics Unit Verification
- Connector Label Placer Unit Verification

## Verification Approach

The abstractions system is verified through in-process unit tests. The contract interfaces
(`ILayoutAlgorithm`, `IRenderer`) are exercised through minimal fake implementations that the registry
tests register and resolve, confirming the identity members of each contract. The registry tests also
verify extension-based renderer lookup using advertised file-extension aliases. The registries, theme,
and the three geometry helpers are pure and deterministic, so their tests construct inputs directly
and assert on returned values. No external services or filesystem access are involved, and the only
test doubles are the in-test `FakeAlgorithm` and `FakeRenderer` used to exercise registry lookup.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK.
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Dependencies**: none; no external services, network, or filesystem access.
- **Isolation**: each test constructs its own registry, theme reference, or geometry inputs.
- **Test project**: `DemaConsulting.Rendering.Abstractions.Tests` (`RegistryTests.cs`,
  `ThemeTests.cs`, `NotationMetricsTests.cs`, `BoxMetricsTests.cs`, `ConnectorLabelPlacerTests.cs`).

## Acceptance Criteria

A verification run passes when every scenario in this system document and in the six unit documents
passes without error or unexpected exception. Any wrong returned value, wrong geometry, or unexpected
exception constitutes a failure.

## Test Scenarios

The system requirements are satisfied through the unit scenarios documented in the per-unit
verification files; the representative system-level scenarios are:

- **`Rendering-Abstractions-Extensibility`**:
  LayoutAlgorithmRegistry_RegisterThenResolve_ReturnsAlgorithm,
  RendererRegistry_RegisterThenResolve_ReturnsRenderer,
  RendererRegistry_ResolveByExtension_MatchesAdvertisedExtensions (see
  Rendering Contracts Unit Verification and
  Registries Unit Verification)
- **`Rendering-Abstractions-Theming`**: ConnectorApproachZone_SumsStubBendAndClearance,
  Themes_HaveExpectedConnectorGeometry (see Theme Unit Verification)
- **`Rendering-Abstractions-SharedGeometry`**: TriangleFamily_HasCanonicalValues,
  BoxMetrics_FolderTabHeight_DerivesFromThemeBodyFontAndPadding,
  Place_SingleLine_UsesLongestSegmentMidpoint (see
  Notation Metrics Unit Verification,
  Box Metrics Unit Verification, and
  Connector Label Placer Unit Verification)
