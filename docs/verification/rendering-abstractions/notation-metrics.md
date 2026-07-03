## Notation Metrics Unit Verification

Part of the Rendering Abstractions Verification.

This document describes the verification design for the notation-metrics unit of the
`DemaConsulting.Rendering.Abstractions` system. It maps every notation-metrics unit requirement to at
least one named test scenario so a reviewer can confirm coverage without reading the test code. The
verification strategy, test environment, and acceptance criteria are described in the
system verification document; the test project is
`DemaConsulting.Rendering.Abstractions.Tests` (`NotationMetricsTests.cs`).

### Notation Metrics Unit Scenarios

#### Triangle marker geometry is canonical

Tests `TriangleFamily_HasCanonicalValues`, `TriangleVertices_ReproduceSvgBoxPoints`, and
`TriangleVertices_Apex_OvershootsEndpoint` assert the triangle-family constants (10x7, refX 9), map
the shared vertices back to the SVG box points "0 0, 10 3.5, 0 7", and confirm the apex overshoots the
endpoint by the documented amount.

**Covers**: `Rendering-Abstractions-NotationMetrics-TriangleGeometry`.

#### Diamond marker geometry is canonical

Tests `Diamond_HasCanonicalValues`, `DiamondVertices_ReproduceSvgBoxPoints`, and
`DiamondVertices_FarPoint_LandsOnEndpoint` assert the diamond constants (14x8, refX 13), map the
shared vertices back to the SVG box points "1 4, 7 0, 13 4, 7 8", and confirm one vertex sits exactly
on the line endpoint.

**Covers**: `Rendering-Abstractions-NotationMetrics-DiamondGeometry`.

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

#### Box-decoration proportions are documented

Tests `Port_SizeIsTwiceHalfSize`, `FolderTab_HasDocumentedConstants`,
`NoteFold_HasDocumentedConstants`, `RoundedRectRadius_IsThemeRadiusTimesFactor`,
`BadgeFractions_HaveDocumentedValues`, and `LabelBackground_ExtentMatchesInset` assert the port square,
folder-tab, note-fold, rounded-rectangle corner, badge, and label-background constants and derivations.

**Covers**: `Rendering-Abstractions-NotationMetrics-BoxDecorations`.

### Requirements Coverage

- **`Rendering-Abstractions-NotationMetrics-TriangleGeometry`**: TriangleFamily_HasCanonicalValues,
  TriangleVertices_ReproduceSvgBoxPoints, TriangleVertices_Apex_OvershootsEndpoint
- **`Rendering-Abstractions-NotationMetrics-DiamondGeometry`**: Diamond_HasCanonicalValues,
  DiamondVertices_ReproduceSvgBoxPoints, DiamondVertices_FarPoint_LandsOnEndpoint
- **`Rendering-Abstractions-NotationMetrics-CircleBarGeometry`**: CircleAndBar_HaveCanonicalValues
- **`Rendering-Abstractions-NotationMetrics-Crossbar`**: Crossbar_IsDerivedFraction
- **`Rendering-Abstractions-NotationMetrics-AlongLineLength`**: AlongLineLength_MatchesMarkerBox
- **`Rendering-Abstractions-NotationMetrics-BoxDecorations`**: Port_SizeIsTwiceHalfSize,
  FolderTab_HasDocumentedConstants, NoteFold_HasDocumentedConstants,
  RoundedRectRadius_IsThemeRadiusTimesFactor, BadgeFractions_HaveDocumentedValues,
  LabelBackground_ExtentMatchesInset
