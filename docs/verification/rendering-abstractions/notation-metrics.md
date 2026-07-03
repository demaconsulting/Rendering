## Notation Metrics Unit Verification

Part of the Rendering Abstractions Verification.

This document describes the verification design for the notation-metrics unit of the
`DemaConsulting.Rendering.Abstractions` system. It maps every notation-metrics unit requirement to at
least one named test scenario so a reviewer can confirm coverage without reading the test code. The
verification strategy, test environment, and acceptance criteria are described in the
system verification document; the test project is
`DemaConsulting.Rendering.Abstractions.Tests` (`NotationMetricsTests.cs`).

### Verification Approach

The notation-metrics unit is verified with in-process xUnit unit tests that read the public
constants and call the static helpers on `NotationMetrics` and the `MarkerVertex` record struct
directly. No mocking, dependency injection, or filesystem access is used. Where a helper takes a
`Theme` argument (`RoundedRectRadius(Theme)`), the tests pass one of the built-in `Themes` so the
inputs are deterministic. The tests both assert the canonical constant values (for example the
triangle 10x7 box) and reproduce the historical SVG marker points to prove the shared vertices
match the values shipped previously.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK on `net8.0`, `net9.0`, and `net10.0`.
- **Execution**: `dotnet test` invoked by `build.ps1` and by the CI pipeline.
- **Test project**: `DemaConsulting.Rendering.Abstractions.Tests` (`NotationMetricsTests.cs`).
- **External dependencies**: none.

### Acceptance Criteria

The unit is considered verified when every scenario listed below passes. Each test must return the
documented constant, derived value, or vertex list; any drift in a canonical value, in a derived
proportion, or in the vertex geometry constitutes a failure.

### Test Scenarios

#### Triangle marker geometry is canonical

Tests `TriangleFamily_HasCanonicalValues` and `TriangleVertices_ReproduceSvgBoxPoints` assert the
triangle-family constants (10x7, refX 9) and map the shared vertices back to the SVG box points
"0 0, 10 3.5, 0 7".

**Covers**: `Rendering-Abstractions-NotationMetrics-TriangleDimensions`.

#### Triangle apex overshoots the endpoint

Test `TriangleVertices_Apex_OvershootsEndpoint` confirms the apex overshoots the endpoint by the
documented amount.

**Covers**: `Rendering-Abstractions-NotationMetrics-TriangleApexOvershoot`.

#### Diamond marker geometry is canonical

Tests `Diamond_HasCanonicalValues` and `DiamondVertices_ReproduceSvgBoxPoints` assert the diamond
constants (14x8, refX 13) and map the shared vertices back to the SVG box points "1 4, 7 0, 13 4, 7 8".

**Covers**: `Rendering-Abstractions-NotationMetrics-DiamondDimensions`.

#### Diamond far point lands on the endpoint

Test `DiamondVertices_FarPoint_LandsOnEndpoint` confirms one vertex sits exactly on the line
endpoint.

**Covers**: `Rendering-Abstractions-NotationMetrics-DiamondFarPointOnEndpoint`.

#### Circle and bar geometry is canonical

Test `CircleAndBar_HaveCanonicalValues` asserts the circle (radius 4, box 10, centre 5, refX 9) and
bar (4x12, half-along 2, half 6) constants match the historical markers.

**Covers**: `Rendering-Abstractions-NotationMetrics-CircleBarGeometry`.

#### Crossbar is a derived fraction

Test `Crossbar_IsDerivedFraction` asserts the crossbar fraction is 0.7 and the derived position is 7.0
(0.7 x the marker length).

**Covers**: `Rendering-Abstractions-NotationMetrics-Crossbar`.

#### Along-line length matches the marker box

Test `AlongLineLength_MatchesMarkerBox` reads `AlongLineLength` for each `EndMarkerStyle` and asserts
each reports its documented length (0 for None, 10 for the triangle family and circle, 14 for
diamonds, 4 for bar).

**Covers**: `Rendering-Abstractions-NotationMetrics-AlongLineLength`.

#### Port square proportion is documented

Test `Port_SizeIsTwiceHalfSize` asserts the port square constant and its derivation.

**Covers**: `Rendering-Abstractions-NotationMetrics-PortSquare`.

#### Folder-tab proportion is documented

Test `FolderTab_HasDocumentedConstants` asserts the folder-tab constants.

**Covers**: `Rendering-Abstractions-NotationMetrics-FolderTab`.

#### Note-fold proportion is documented

Test `NoteFold_HasDocumentedConstants` asserts the note-fold constants.

**Covers**: `Rendering-Abstractions-NotationMetrics-NoteFold`.

#### Rounded-rectangle corner proportion is documented

Test `RoundedRectRadius_IsThemeRadiusTimesFactor` asserts the rounded-rectangle corner derivation.

**Covers**: `Rendering-Abstractions-NotationMetrics-RoundedRectCorner`.

#### Badge proportions are documented

Test `BadgeFractions_HaveDocumentedValues` asserts the badge fraction constants.

**Covers**: `Rendering-Abstractions-NotationMetrics-Badge`.

#### Label-background proportion is documented

Test `LabelBackground_ExtentMatchesInset` asserts the label-background extent derivation.

**Covers**: `Rendering-Abstractions-NotationMetrics-LabelBackground`.

### Requirements Coverage

- **`Rendering-Abstractions-NotationMetrics-TriangleDimensions`**: TriangleFamily_HasCanonicalValues,
  TriangleVertices_ReproduceSvgBoxPoints
- **`Rendering-Abstractions-NotationMetrics-TriangleApexOvershoot`**: TriangleVertices_Apex_OvershootsEndpoint
- **`Rendering-Abstractions-NotationMetrics-DiamondDimensions`**: Diamond_HasCanonicalValues,
  DiamondVertices_ReproduceSvgBoxPoints
- **`Rendering-Abstractions-NotationMetrics-DiamondFarPointOnEndpoint`**: DiamondVertices_FarPoint_LandsOnEndpoint
- **`Rendering-Abstractions-NotationMetrics-CircleBarGeometry`**: CircleAndBar_HaveCanonicalValues
- **`Rendering-Abstractions-NotationMetrics-Crossbar`**: Crossbar_IsDerivedFraction
- **`Rendering-Abstractions-NotationMetrics-AlongLineLength`**: AlongLineLength_MatchesMarkerBox
- **`Rendering-Abstractions-NotationMetrics-PortSquare`**: Port_SizeIsTwiceHalfSize
- **`Rendering-Abstractions-NotationMetrics-FolderTab`**: FolderTab_HasDocumentedConstants
- **`Rendering-Abstractions-NotationMetrics-NoteFold`**: NoteFold_HasDocumentedConstants
- **`Rendering-Abstractions-NotationMetrics-RoundedRectCorner`**: RoundedRectRadius_IsThemeRadiusTimesFactor
- **`Rendering-Abstractions-NotationMetrics-Badge`**: BadgeFractions_HaveDocumentedValues
- **`Rendering-Abstractions-NotationMetrics-LabelBackground`**: LabelBackground_ExtentMatchesInset
