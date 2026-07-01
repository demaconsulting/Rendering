# Rendering.Svg Verification Design

This document describes the verification strategy for the DemaConsulting.Rendering.Svg system
and its single unit, the SvgRenderer class. Requirements for this system are defined in the
Rendering.Svg System and Unit Requirements document.

## Verification Strategy

The SVG renderer is verified through xUnit tests that render a placed `LayoutTree` to an SVG
document and assert on the emitted markup. Because SVG output is text, each scenario decodes
the written stream to a string and inspects it for the expected elements, attributes, and
values. No mocking is required: the renderer is a pure, stateless function of its inputs, so
tests construct concrete `LayoutTree` inputs and a `Themes.Light` theme and verify the exact
output. End-marker geometry is additionally verified against the shared `NotationMetrics`
source to prove the SVG markers are derived from the single-source geometry shared with the
PNG renderer.

Tests reside in the `DemaConsulting.Rendering.Svg.Tests` project across three files:
`SvgRendererTests.cs` (smoke coverage), `SvgRendererPortedTests.cs` (per-node-kind markup
coverage), and `SvgEndMarkerTests.cs` (marker definition and geometry coverage).

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, databases, or network access required
- **Isolation**: Each test method constructs its own `SvgRenderer`, `LayoutTree`, and
  `MemoryStream`; no shared state between tests
- **Inputs**: Placed `LayoutTree` fixtures and the `Themes.Light` theme

## External Interface Simulation

The system has no external interfaces requiring simulation. It is a pure in-process .NET
library that writes bytes to a caller-supplied `Stream`. Tests supply a `MemoryStream`,
decode the written bytes as UTF-8, and assert on the resulting SVG text.

## Unit-Level Test Scenarios

### Implements IRenderer

**Test**: `Render_SingleBox_ProducesSvgDocument`

Renders a single-box tree and asserts that the output contains an `<svg` root element, that
`MediaType` is `"image/svg+xml"`, and that `DefaultExtension` is `".svg"`. Confirms the
renderer satisfies the shared renderer contract and reports the correct media type and
extension.

### Renders SVG Document

**Test**: `SvgRenderer_Render_EmptyTree_ProducesSvgDocument`

Renders an empty layout tree and asserts that a well-formed SVG document containing the root
`svg` element is still produced. Confirms the empty-tree boundary case yields a valid
container.

### Renders Box

**Tests**: `SvgRenderer_Render_SingleBox_ProducesRectElement`,
`SvgRenderer_Render_BoxRoundedRectangle_ProducesRxAttribute`,
`SvgRenderer_Render_BoxWithCompartment_ProducesLineAndText`

Verify that a box renders as a `<rect>` element, that a rounded-rectangle box adds the `rx`
attribute, and that a box with a compartment emits both the divider `<line>` and the
compartment `<text>`. Together they cover the plain, rounded, and compartmented box forms.

### Renders Label

**Tests**: `SvgRenderer_Render_SingleLabel_ProducesTextElement`,
`SvgRenderer_Render_LabelWithBold_ProducesBoldAttribute`,
`SvgRenderer_Render_LabelWithItalic_ProducesItalicAttribute`,
`SvgRenderer_Render_LabelWithXmlSpecialCharacters_ProducesWellFormedEscapedSvg`

Verify that a label renders as a `<text>` element, that bold and italic styling produce the
corresponding `font-weight`/`font-style` attributes, and that XML-special characters are
escaped so the document remains well-formed. The escaping scenario is the key error-input
boundary case.

### Renders Line

**Tests**: `SvgRenderer_Render_SingleLine_ProducesPathElement`,
`SvgRenderer_Render_SingleLine_WithCornerRadius_ProducesArcInPath`,
`SvgRenderer_Render_SingleLine_Dashed_ProducesDashArray`,
`SvgRenderer_Render_LineWithMidpointLabel_ProducesTextElement`

Verify that a connector renders as a `<path>` element, that a positive corner radius emits an
arc (`A`) command in the path, that a dashed line style emits `stroke-dasharray`, and that a
midpoint label emits a `<text>` element. Together they cover straight, rounded, dashed, and
labelled connectors.

### Renders Node Kinds

**Tests**: `SvgRenderer_Render_SinglePort_ProducesRect`,
`SvgRenderer_Render_SingleBadge_FilledCircle_ProducesCircle`,
`SvgRenderer_Render_SingleBand_ProducesRect`,
`SvgRenderer_Render_SingleLifeline_ProducesRectAndLine`,
`SvgRenderer_Render_SingleActivation_ProducesRect`,
`SvgRenderer_Render_SingleGrid_ProducesRects`

Verify that each remaining node kind - port, badge, band, lifeline, activation, and grid -
renders its notation-appropriate SVG elements. Confirms the type dispatch handles every
`LayoutNode` subtype so all view types render completely.

### Emits End Markers

**Tests**: `SvgRenderer_Render_SingleLine_WithOpenArrowhead_ProducesMarkerEnd`,
`SvgRenderer_Render_SingleLine_WithDiamondArrowhead_ProducesDiamondMarker`,
`SvgRenderer_Render_SingleLine_WithOpenCrossbarArrowhead_ProducesOpenCrossbarMarker`,
`OpenChevron_IsDefinedAsPolyline`, `HollowTriangle_IsDefinedAsClosedPolygon`,
`TriangleMarker_DimensionsDeriveFromNotationMetrics`,
`DiamondMarker_DimensionsDeriveFromNotationMetrics`,
`OpenChevronLine_ReferencesOpenChevronMarker`

Verify that connector ends reference the correct `marker-start`/`marker-end` definitions for
the open, diamond, and crossbar variants; that the open chevron is defined as an open
`<polyline>` while the hollow triangle is a closed `<polygon>`; that the triangle and diamond
marker dimensions equal the values from `NotationMetrics`; and that an open-chevron line
references the open-chevron marker. The geometry scenarios prove the SVG markers are derived
from the shared single-source notation metrics.

## System-Level Test Scenario

### Renders a placed layout tree to SVG

**Test**: `Render_SingleBox_ProducesSvgDocument`

Exercises the system end to end: constructs a `LayoutTree` with a single placed box, renders
it through the public `IRenderer.Render` entry point, and confirms a valid SVG document is
written to the output stream. Serves as the integration check that the system produces valid
SVG for a representative diagram.

## Acceptance Criteria

A verification run passes when every scenario above passes without error or exception beyond
those explicitly asserted. Any unexpected exception, wrong emitted element, wrong attribute
value, or geometry that does not match `NotationMetrics` constitutes a failure.

## Requirements Coverage

| Requirement ID | Test Scenario(s) |
| --- | --- |
| Rendering-Svg-WriteSvgDocument | Renders a placed layout tree to SVG |
| Rendering-Svg-SvgRenderer-ImplementsIRenderer | Implements IRenderer |
| Rendering-Svg-SvgRenderer-RenderDocument | Renders SVG Document |
| Rendering-Svg-SvgRenderer-RenderBox | Renders Box |
| Rendering-Svg-SvgRenderer-RenderLabel | Renders Label |
| Rendering-Svg-SvgRenderer-RenderLine | Renders Line |
| Rendering-Svg-SvgRenderer-RenderNodeKinds | Renders Node Kinds |
| Rendering-Svg-SvgRenderer-EndMarkers | Emits End Markers |
