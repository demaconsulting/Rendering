## Theme Unit Verification

Part of the Rendering Abstractions Verification.

This document describes the verification design for the theme unit of the
`DemaConsulting.Rendering.Abstractions` system. It maps every theme unit requirement to at least one
named test scenario so a reviewer can confirm coverage without reading the test code. The verification
strategy, test environment, and acceptance criteria are described in the
system verification document; the test project is
`DemaConsulting.Rendering.Abstractions.Tests` (`ThemeTests.cs`).

### Verification Approach

The theme unit is verified with in-process xUnit unit tests that read properties of the built-in
`Themes.Light`, `Themes.Dark`, and `Themes.Print` instances and call `Theme.ConnectorApproachZone`
directly. No mocking is used because `Theme` is an immutable `sealed record`. The tests operate on
the shipped built-in themes as the representative inputs; caller-constructed `Theme` instances
share the same value-type semantics and require no separate scenarios.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK on `net8.0`, `net9.0`, and `net10.0`.
- **Execution**: `dotnet test` invoked by `build.ps1` and by the CI pipeline.
- **Test project**: `DemaConsulting.Rendering.Abstractions.Tests` (`ThemeTests.cs`).
- **External dependencies**: none.

### Acceptance Criteria

The unit is considered verified when every scenario listed below passes. Each test must produce the
documented arithmetic result (for `ConnectorApproachZone`) or the documented built-in geometry
values; any drift in a built-in theme value or in the approach-zone formula constitutes a failure.

### Test Scenarios

#### Approach zone sums stub, bend, and clearance

Test `ConnectorApproachZone_SumsStubBendAndClearance` calls `ConnectorApproachZone(10.0)` on the Light
theme (stub 8, bend radius 4) and asserts the result is 22.0.

**Covers**: `Rendering-Abstractions-Theme-ApproachZone`.

#### Built-in themes carry expected geometry

Test `Themes_HaveExpectedConnectorGeometry` reads the connector stub and bend radius of the Light,
Dark, and Print themes and asserts Light and Dark carry stub 8 and bend radius 4 while Print carries
stub 6 and bend radius 0.

**Covers**: `Rendering-Abstractions-Theme-BuiltInGeometry`.

### Requirements Coverage

- **`Rendering-Abstractions-Theme-ApproachZone`**: ConnectorApproachZone_SumsStubBendAndClearance
- **`Rendering-Abstractions-Theme-BuiltInGeometry`**: Themes_HaveExpectedConnectorGeometry
