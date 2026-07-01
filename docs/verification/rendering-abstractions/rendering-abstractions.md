# Rendering Abstractions Verification

This document describes the verification design for the `DemaConsulting.Rendering.Abstractions`
system and its six units: rendering-contracts, registries, theme, notation-metrics, box-metrics, and
connector-label-placer. It maps every requirement to at least one named test scenario so a reviewer
can confirm coverage without reading the test code.

## Verification Strategy

The abstractions system is verified through in-process unit tests. The contract interfaces
(`ILayoutAlgorithm`, `IRenderer`) are exercised through minimal fake implementations that the
registry tests register and resolve, confirming the identity members of each contract. The registries,
theme, and the three geometry helpers are pure and deterministic, so their tests construct inputs
directly and assert on returned values. No external services or filesystem access are involved, and
the only test doubles are the in-test `FakeAlgorithm` and `FakeRenderer` used to exercise registry
lookup.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK.
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Dependencies**: none; no external services, network, or filesystem access.
- **Isolation**: each test constructs its own registry, theme reference, or geometry inputs.
- **Test project**: `DemaConsulting.Rendering.Abstractions.Tests` (`RegistryTests.cs`,
  `ThemeTests.cs`, `NotationMetricsTests.cs`, `BoxMetricsTests.cs`, `ConnectorLabelPlacerTests.cs`).

## Rendering Contracts Unit Scenarios

The contract interfaces carry no behavior of their own; their identity members are verified through
the fake implementations registered in the registry tests. `FakeAlgorithm` implements
`ILayoutAlgorithm` (returning `Id` "fake") and `FakeRenderer` implements `IRenderer` (returning
`MediaType` "text/plain" and `DefaultExtension` ".txt").

### Algorithm contract identity is exercised

Test `LayoutAlgorithmRegistry_RegisterThenResolve_ReturnsAlgorithm` registers a `FakeAlgorithm`,
resolves it, and reads the resolved algorithm's `Id`, asserting it is "fake" and thereby exercising
`ILayoutAlgorithm.Id`.

**Covers**: `Rendering-Abstractions-Contracts-Algorithm`.

### Renderer contract identity is exercised

Test `RendererRegistry_RegisterThenResolve_ReturnsRenderer` registers a `FakeRenderer`, resolves it,
and reads the resolved renderer's `MediaType`, asserting it is "text/plain" and thereby exercising
`IRenderer.MediaType`.

**Covers**: `Rendering-Abstractions-Contracts-Renderer`.

## Registries Unit Scenarios

### Algorithm registers and resolves by id

Test `LayoutAlgorithmRegistry_RegisterThenResolve_ReturnsAlgorithm` registers an algorithm, then
asserts that `Contains("fake")` is true and `Resolve("fake")` returns the registered algorithm.

**Covers**: `Rendering-Abstractions-Registries-ResolveAlgorithm`.

### Renderer registers and resolves by media type

Test `RendererRegistry_RegisterThenResolve_ReturnsRenderer` registers a renderer, then asserts that
`Contains("text/plain")` is true and `Resolve("text/plain")` returns the registered renderer.

**Covers**: `Rendering-Abstractions-Registries-ResolveRenderer`.

### Resolving a missing id throws

Test `LayoutAlgorithmRegistry_ResolveMissing_Throws` resolves an identifier that was never registered
and asserts that `Resolve` throws `KeyNotFoundException`.

**Covers**: `Rendering-Abstractions-Registries-MissingThrows`.

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

## Notation Metrics Unit Scenarios

### Triangle marker geometry is canonical

Tests `TriangleFamily_HasCanonicalValues`, `TriangleVertices_ReproduceSvgBoxPoints`, and
`TriangleVertices_Apex_OvershootsEndpoint` assert the triangle-family constants (10x7, refX 9), map the
shared vertices back to the SVG box points "0 0, 10 3.5, 0 7", and confirm the apex overshoots the
endpoint by the documented amount.

**Covers**: `Rendering-Abstractions-NotationMetrics-TriangleGeometry`.

### Diamond marker geometry is canonical

Tests `Diamond_HasCanonicalValues`, `DiamondVertices_ReproduceSvgBoxPoints`, and
`DiamondVertices_FarPoint_LandsOnEndpoint` assert the diamond constants (14x8, refX 13), map the shared
vertices back to the SVG box points "1 4, 7 0, 13 4, 7 8", and confirm one vertex sits exactly on the
line endpoint.

**Covers**: `Rendering-Abstractions-NotationMetrics-DiamondGeometry`.

### Circle and bar geometry is canonical

Test `CircleAndBar_HaveCanonicalValues` asserts the circle (radius 4, box 10, centre 5, refX 9) and
bar (4x12, half-along 2, half 6) constants match the historical markers.

**Covers**: `Rendering-Abstractions-NotationMetrics-CircleBarGeometry`.

### Crossbar is a derived fraction

Test `Crossbar_IsDerivedFraction` asserts the crossbar fraction is 0.7 and the derived position is
7.0 (0.7 x the marker length).

**Covers**: `Rendering-Abstractions-NotationMetrics-Crossbar`.

### Along-line length matches the marker box

Test `AlongLineLength_MatchesMarkerBox` reads `AlongLineLength` for each `EndMarkerStyle` and asserts
each reports its documented length (0 for None, 10 for the triangle family and circle, 14 for
diamonds, 4 for bar).

**Covers**: `Rendering-Abstractions-NotationMetrics-AlongLineLength`.

### Box-decoration proportions are documented

Tests `Port_SizeIsTwiceHalfSize`, `FolderTab_HasDocumentedConstants`, `NoteFold_HasDocumentedConstants`,
`RoundedRectRadius_IsThemeRadiusTimesFactor`, `BadgeFractions_HaveDocumentedValues`, and
`LabelBackground_ExtentMatchesInset` assert the port square, folder-tab, note-fold, rounded-rectangle
corner, badge, and label-background constants and derivations.

**Covers**: `Rendering-Abstractions-NotationMetrics-BoxDecorations`.

## Box Metrics Unit Scenarios

### Folder-tab height derives from theme

Test `BoxMetrics_FolderTabHeight_DerivesFromThemeBodyFontAndPadding` calls `FolderTabHeight` with the
Light theme (body font 12, padding 6) and asserts the height is 24, equal to the body font size plus
two label paddings.

**Covers**: `Rendering-Abstractions-BoxMetrics-FolderTabHeight`.

### Title-area height reflects present lines

Tests `BoxMetrics_TitleAreaHeight_NoLabelNoKeyword_IsZero`, `BoxMetrics_TitleAreaHeight_LabelOnly_ReservesTitleLine`,
and `BoxMetrics_TitleAreaHeight_LabelAndKeyword_ReservesBothLines` assert that no title area is
reserved when neither a name nor a keyword is present, that a labelled box reserves padding plus one
title line, and that a keyword-and-name box reserves padding plus both lines.

**Covers**: `Rendering-Abstractions-BoxMetrics-TitleAreaHeight`.

## Connector Label Placer Unit Scenarios

### Unlabelled line is omitted

Test `Place_LineWithoutLabel_IsOmitted` places labels for a single line whose `MidpointLabel` is null
and asserts the result is empty.

**Covers**: `Rendering-Abstractions-ConnectorLabelPlacer-OmitUnlabelled`.

### Label uses the longest segment midpoint

Test `Place_SingleLine_UsesLongestSegmentMidpoint` places a label for a line with a short vertical
stub followed by a long horizontal run and asserts the label lands at the midpoint of the long run
(100, 10).

**Covers**: `Rendering-Abstractions-ConnectorLabelPlacer-LongestSegment`.

### Colliding labels are separated

Test `Place_CollidingLabels_AreSeparated` places labels for two lines whose longest-segment midpoints
coincide and asserts the first keeps the preferred midpoint (100, 0) while the second is nudged to a
different Y so the two do not overlap.

**Covers**: `Rendering-Abstractions-ConnectorLabelPlacer-AvoidOverlap`.

## Acceptance Criteria

A verification run passes when every scenario above passes without error or unexpected exception. Any
wrong returned value, wrong geometry, or unexpected exception constitutes a failure.

## Requirements Coverage

- **`Rendering-Abstractions-Extensibility`**: LayoutAlgorithmRegistry_RegisterThenResolve_ReturnsAlgorithm,
  RendererRegistry_RegisterThenResolve_ReturnsRenderer
- **`Rendering-Abstractions-Theming`**: ConnectorApproachZone_SumsStubBendAndClearance,
  Themes_HaveExpectedConnectorGeometry
- **`Rendering-Abstractions-SharedGeometry`**: TriangleFamily_HasCanonicalValues,
  BoxMetrics_FolderTabHeight_DerivesFromThemeBodyFontAndPadding, Place_SingleLine_UsesLongestSegmentMidpoint
- **`Rendering-Abstractions-Contracts-Algorithm`**: LayoutAlgorithmRegistry_RegisterThenResolve_ReturnsAlgorithm
- **`Rendering-Abstractions-Contracts-Renderer`**: RendererRegistry_RegisterThenResolve_ReturnsRenderer
- **`Rendering-Abstractions-Registries-ResolveAlgorithm`**: LayoutAlgorithmRegistry_RegisterThenResolve_ReturnsAlgorithm
- **`Rendering-Abstractions-Registries-ResolveRenderer`**: RendererRegistry_RegisterThenResolve_ReturnsRenderer
- **`Rendering-Abstractions-Registries-MissingThrows`**: LayoutAlgorithmRegistry_ResolveMissing_Throws
- **`Rendering-Abstractions-Theme-ApproachZone`**: ConnectorApproachZone_SumsStubBendAndClearance
- **`Rendering-Abstractions-Theme-BuiltInGeometry`**: Themes_HaveExpectedConnectorGeometry
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
- **`Rendering-Abstractions-BoxMetrics-FolderTabHeight`**: BoxMetrics_FolderTabHeight_DerivesFromThemeBodyFontAndPadding
- **`Rendering-Abstractions-BoxMetrics-TitleAreaHeight`**: BoxMetrics_TitleAreaHeight_NoLabelNoKeyword_IsZero,
  BoxMetrics_TitleAreaHeight_LabelOnly_ReservesTitleLine, BoxMetrics_TitleAreaHeight_LabelAndKeyword_ReservesBothLines
- **`Rendering-Abstractions-ConnectorLabelPlacer-OmitUnlabelled`**: Place_LineWithoutLabel_IsOmitted
- **`Rendering-Abstractions-ConnectorLabelPlacer-LongestSegment`**: Place_SingleLine_UsesLongestSegmentMidpoint
- **`Rendering-Abstractions-ConnectorLabelPlacer-AvoidOverlap`**: Place_CollidingLabels_AreSeparated
