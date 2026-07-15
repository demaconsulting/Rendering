### EdgeCountGapWidener Unit Verification

Part of the Rendering Layout Verification.

This document maps the edge-count-gap-widener unit requirements to named test scenarios.

#### Verification Approach

`EdgeCountGapWidener` is a stateless, pure static function, so verification is by direct xUnit unit
tests that call `Widen` with representative `baseGap`/`edgeCount` pairs and assert the exact returned
value. No mocks are used and no dependencies are injected: `LayeredLayoutMetrics.ConnectorClearance`
and `LayeredLayoutMetrics.EdgeSpacing` are the real production constants, so every test measures the
same corridor-width arithmetic the containment packer and the hierarchical algorithm consume.

#### Test Environment

- **Framework**: xUnit v3 running on the .NET SDK.
- **Runner**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Project**: `test/DemaConsulting.Rendering.Layout.Tests/Engine/EdgeCountGapWidenerTests.cs`.
- **Dependencies**: no external services, files, or network access; every test calls the static
  `Widen` method directly with literal inputs.
- **Isolation**: each test is independent; the method holds no state between calls.

#### Acceptance Criteria

A verification run passes when every named scenario below asserts the exact expected `double`
without unexpected exception, and the referenced tests cover each
`Rendering-Layout-EdgeCountGapWidener-*` requirement. A returned value that narrows below the
supplied base gap, an incorrect corridor-width computation, or a degenerate edge count (zero or one)
that unexpectedly widens the gap constitutes a failure.

#### Test Scenarios

- **Corridor formula** (`Rendering-Layout-EdgeCountGapWidener-CorridorFormula`):
  `Widen_ManyConnectors_ReturnsCorridorWidth` confirms a fan of eight connectors widens a small base
  gap to the full corridor width (`2 * ConnectorClearance + (n - 1) * EdgeSpacing`);
  `Widen_TwoConnectors_ReturnsSingleSlotCorridor` confirms two connectors widen the gap to a
  single-slot corridor.
- **Degenerate cases** (`Rendering-Layout-EdgeCountGapWidener-DegenerateCases`):
  `Widen_SingleConnector_ReturnsBaseGap` confirms one connector returns the base gap unchanged;
  `Widen_ZeroConnectors_ReturnsBaseGap` confirms zero connectors likewise return the base gap
  unchanged rather than narrowing it.
- **Never narrows** (`Rendering-Layout-EdgeCountGapWidener-NeverNarrows`):
  `Widen_BaseGapExceedsCorridor_ReturnsBaseGap` confirms a base gap that already exceeds the computed
  corridor width is preserved rather than shrunk to the corridor width.

#### Requirements Coverage

- **`Rendering-Layout-EdgeCountGapWidener-CorridorFormula`**:
  Widen_ManyConnectors_ReturnsCorridorWidth, Widen_TwoConnectors_ReturnsSingleSlotCorridor
- **`Rendering-Layout-EdgeCountGapWidener-DegenerateCases`**:
  Widen_SingleConnector_ReturnsBaseGap, Widen_ZeroConnectors_ReturnsBaseGap
- **`Rendering-Layout-EdgeCountGapWidener-NeverNarrows`**:
  Widen_BaseGapExceedsCorridor_ReturnsBaseGap
