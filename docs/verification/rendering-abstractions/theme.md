# Theme Unit Verification

Part of the [Rendering Abstractions Verification](rendering-abstractions.md).

This document describes the verification design for the theme unit of the
`DemaConsulting.Rendering.Abstractions` system. It maps every theme unit requirement to at least one
named test scenario so a reviewer can confirm coverage without reading the test code. The verification
strategy, test environment, and acceptance criteria are described in the
[system verification document](rendering-abstractions.md); the test project is
`DemaConsulting.Rendering.Abstractions.Tests` (`ThemeTests.cs`).

## Theme Unit Scenarios

### Approach zone sums stub, bend, and clearance

Test `ConnectorApproachZone_SumsStubBendAndClearance` calls `ConnectorApproachZone(10.0)` on the Light
theme (stub 8, bend radius 4) and asserts the result is 22.0.

**Covers**: `Rendering-Abstractions-Theme-ApproachZone`.

### Built-in themes carry expected geometry

Test `Themes_HaveExpectedConnectorGeometry` reads the connector stub and bend radius of the Light,
Dark, and Print themes and asserts Light and Dark carry stub 8 and bend radius 4 while Print carries
stub 6 and bend radius 0.

**Covers**: `Rendering-Abstractions-Theme-BuiltInGeometry`.

## Requirements Coverage

- **`Rendering-Abstractions-Theme-ApproachZone`**: ConnectorApproachZone_SumsStubBendAndClearance
- **`Rendering-Abstractions-Theme-BuiltInGeometry`**: Themes_HaveExpectedConnectorGeometry
