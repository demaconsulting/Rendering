## SvgRenderer Unit Verification

Part of the Rendering.Svg Verification.

This document describes the verification design for the `SvgRenderer` unit of the
`DemaConsulting.Rendering.Svg` system. It maps every SvgRenderer unit requirement to at least one
named test scenario so a reviewer can confirm coverage without reading the test code. The verification
strategy, test environment, and acceptance criteria are described in the
system verification document; the test project is
`DemaConsulting.Rendering.Svg.Tests`.

### Verification Approach

Unit verification uses xUnit v3 tests in `DemaConsulting.Rendering.Svg.Tests` that construct
concrete `LayoutTree`, `RenderOptions`, and `MemoryStream` instances and invoke
`SvgRenderer.Render` directly. Because `SvgRenderer` is pure and stateless, no mocking or
stubbing is used: the real `Themes.Light` theme, the real `NotationMetrics` and
`ConnectorLabelPlacer` helpers from `DemaConsulting.Rendering.Abstractions`, and the real
`LayoutTree` and `LayoutNode` records from `DemaConsulting.Rendering` are exercised end-to-end.
Each test decodes the emitted UTF-8 bytes and asserts on the SVG text, element presence,
attribute values, well-formed XML, and geometric parity with `NotationMetrics`.

### Test Environment

- **Framework**: xUnit v3 on the .NET SDK, run against the `net8.0`, `net9.0`, and `net10.0`
  target frameworks.
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Dependencies**: none — no external services, databases, files, or network access are used;
  tests write to in-memory `MemoryStream` instances only.
- **Isolation**: each test constructs its own `SvgRenderer`, layout tree, render options, and
  output stream, so tests are independent and order-agnostic.

### Acceptance Criteria

A unit verification run passes when every named scenario listed below completes without
unexpected exception and every assertion holds. A failure is any missing SVG root element,
wrong renderer metadata (`MediaType`, `DefaultExtension`), wrong emitted SVG element or
attribute, malformed XML escaping, or marker geometry that does not match `NotationMetrics`.
Requirements coverage is enforced by the mapping in the _Requirements Coverage_ section below:
every `Rendering-Svg-SvgRenderer-*` requirement must map to at least one named scenario, and
every mapped scenario must exist in `DemaConsulting.Rendering.Svg.Tests` and pass.

### Test Scenarios

The named scenarios that satisfy each unit requirement are enumerated below under
_SvgRenderer Unit Scenarios_, grouped by rendering concern (renderer contract and metadata,
SVG document root and empty tree, box rectangle and compartments, label text and styling and
escaping, connector path and corners and dash pattern and label, additional node kinds, and
connector end markers). Every `Rendering-Svg-SvgRenderer-*` requirement is traced to at least
one named scenario in the _Requirements Coverage_ table at the end of this document.

### SvgRenderer Unit Scenarios

#### Renderer contract and metadata

Test `SvgRenderer_Render_SingleBox_ProducesSvgDocument` renders a single box, asserts that SVG markup
is produced, and checks `MediaType` is `image/svg+xml` and `DefaultExtension` is `.svg`.

**Covers**: `Rendering-Svg-SvgRenderer-ImplementsIRenderer`,
`Rendering-Svg-SvgRenderer-MediaType`, `Rendering-Svg-SvgRenderer-DefaultExtension`.

#### SVG document root and empty tree

Test `SvgRenderer_Render_EmptyTree_ProducesSvgDocument` renders an empty `LayoutTree`, asserts the
output stream is non-empty, and checks that the decoded text contains `<svg` and `</svg>`.

**Covers**: `Rendering-Svg-SvgRenderer-RenderDocument`,
`Rendering-Svg-SvgRenderer-RenderEmptyTree`.

#### Box rectangle, rounded corners, and compartments

Tests `SvgRenderer_Render_SingleBox_ProducesRectElement`,
`SvgRenderer_Render_BoxRoundedRectangle_ProducesRxAttribute`, and
`SvgRenderer_Render_BoxWithCompartment_ProducesLineAndText` render boxes and assert that a `<rect>`
element, an `rx` rounded-corner attribute, and compartment divider/text content are present.

**Covers**: `Rendering-Svg-SvgRenderer-RenderBox`,
`Rendering-Svg-SvgRenderer-RenderBoxRoundedCorners`,
`Rendering-Svg-SvgRenderer-RenderBoxCompartments`.

#### Label text, styling, and escaping

Tests `SvgRenderer_Render_SingleLabel_ProducesTextElement`,
`SvgRenderer_Render_LabelWithBold_ProducesBoldAttribute`,
`SvgRenderer_Render_LabelWithItalic_ProducesItalicAttribute`, and
`SvgRenderer_Render_LabelWithXmlSpecialCharacters_ProducesWellFormedEscapedSvg` render labels and
assert on `<text>`, bold and italic attributes, escaped XML entities, and that the output remains
well-formed XML.

**Covers**: `Rendering-Svg-SvgRenderer-RenderLabel`,
`Rendering-Svg-SvgRenderer-RenderLabelBold`, `Rendering-Svg-SvgRenderer-RenderLabelItalic`,
`Rendering-Svg-SvgRenderer-RenderLabelEscaping`.

#### Connector path, corners, dash pattern, and label

Tests `SvgRenderer_Render_SingleLine_ProducesPathElement`,
`SvgRenderer_Render_SingleLine_WithCornerRadius_ProducesArcInPath`,
`SvgRenderer_Render_SingleLine_Dashed_ProducesDashArray`, and
`SvgRenderer_Render_LineWithMidpointLabel_ProducesTextElement` render connector lines and assert on
`<path>`, an arc command, `stroke-dasharray`, and the connector label text.

The midpoint-label scenario asserts that a `<text>` element containing the label appears in the final
SVG. The design places connector labels in a final pass using `ConnectorLabelPlacer`; this scenario
does not assert the exact placed coordinates or collision-avoidance behavior.

**Covers**: `Rendering-Svg-SvgRenderer-RenderLine`,
`Rendering-Svg-SvgRenderer-RenderLineRoundedCorners`,
`Rendering-Svg-SvgRenderer-RenderLineDashed`,
`Rendering-Svg-SvgRenderer-RenderLineMidpointLabel`.

#### Additional node kinds

Tests `SvgRenderer_Render_SinglePort_ProducesRect`,
`SvgRenderer_Render_SingleBadge_FilledCircle_ProducesCircle`,
`SvgRenderer_Render_SingleBadge_Bullseye_ProducesConcentricCircles`,
`SvgRenderer_Render_SingleBadge_Diamond_ProducesPolygon`,
`SvgRenderer_Render_SingleBadge_HorizontalBar_ProducesLine`,
`SvgRenderer_Render_SingleBadge_VerticalBar_ProducesLine`,
`SvgRenderer_Render_SingleBand_ProducesRect`,
`SvgRenderer_Render_SingleLifeline_ProducesRectAndLine`,
`SvgRenderer_Render_SingleActivation_ProducesRect`, and
`SvgRenderer_Render_SingleGrid_ProducesRects` assert that ports, filled-circle badges, bands,
lifelines, activations, and grids emit their expected SVG element types.

**Covers**: `Rendering-Svg-SvgRenderer-RenderNodeKinds`,
`Rendering-Svg-SvgRenderer-RenderBadge`,
`Rendering-Svg-SvgRenderer-BadgeBullseye`,
`Rendering-Svg-SvgRenderer-BadgeDiamond`,
`Rendering-Svg-SvgRenderer-BadgeHorizontalBar`,
`Rendering-Svg-SvgRenderer-BadgeVerticalBar`, `Rendering-Svg-SvgRenderer-RenderBand`,
`Rendering-Svg-SvgRenderer-RenderLifeline`, `Rendering-Svg-SvgRenderer-RenderActivation`,
`Rendering-Svg-SvgRenderer-RenderGrid`.

#### Port label reads inward on every side, and a reserved inset shifts content

Theory tests `SvgRenderer_RenderPort_LeftRightLabel_ReadsInward` (Left/Right cases) and
`SvgRenderer_RenderPort_TopBottomLabel_ReadsInward` (Top/Bottom cases) each render a single port and
assert its label text element is positioned toward the box interior relative to the port glyph's
center: rightward for a left-side port, leftward for a right-side port, below a top-side port, and
above a bottom-side port — confirming the label never reads outward off the box on any of the four
sides. `SvgRenderer_RenderBoxCompartments_ContentInsetLeft_ShiftsRowTextRight` renders a box with a
positive `ContentInsetLeft` and asserts a compartment row's text starts further right than the
`Theme.LabelPadding`-only offset used when the inset is zero, confirming the renderer reads the
reserved margin rather than assuming a fixed offset.

**Covers**: `Rendering-Svg-SvgRenderer-RenderPortLabel`, `Rendering-Svg-SvgRenderer-ContentInset`.

#### Connector end markers

Tests `OpenChevron_IsDefinedAsPolyline` and `OpenChevronLine_ReferencesOpenChevronMarker` assert that
the open-chevron marker definition contains `<polyline>` and no `<polygon>`, and that an open-chevron
line contains `marker-end="url(#line-end-open-chevron)"`.

Tests `HollowTriangle_IsDefinedAsClosedPolygon`,
`SvgRenderer_Render_SingleLine_WithHollowTriangleArrowhead_ProducesMarkerEnd`, and
`TriangleMarker_DimensionsDeriveFromNotationMetrics` assert that the hollow-triangle marker definition
contains `<polygon>` and no `<polyline>`, a hollow-triangle target line contains a `marker-end`
attribute, and triangle marker dimensions and points match `NotationMetrics`.

Tests `DiamondMarker_DimensionsDeriveFromNotationMetrics` and
`SvgRenderer_Render_SingleLine_WithDiamondArrowhead_ProducesDiamondMarker` assert that diamond marker
dimensions and points match `NotationMetrics` and that the rendered SVG contains the
`line-end-hollow-diamond` id.

Test `SvgRenderer_Render_SingleLine_WithOpenCrossbarArrowhead_ProducesOpenCrossbarMarker` asserts that
the rendered SVG contains the `line-end-hollow-triangle-crossbar` id.

Tests `SvgRenderer_Render_SingleLine_WithFilledArrow_ProducesFilledArrowMarker`,
`SvgRenderer_Render_SingleLine_WithFilledDiamond_ProducesFilledDiamondMarker`,
`SvgRenderer_Render_SingleLine_WithCircleEnd_ProducesCircleMarker`, and
`SvgRenderer_Render_SingleLine_WithBarEnd_ProducesBarMarker` assert that the connector path carries
the marker reference for the filled-arrow, filled-diamond, circle, and bar end-marker variants.

The diamond and crossbar assertions now check that the connector path element carries the marker
reference itself — `marker-start="url(#line-end-hollow-diamond)"` for the source end and
`marker-end="url(#line-end-hollow-triangle-crossbar)"` for the target end — rather than only that the
marker id appears somewhere in the document. This prevents a false pass if the marker is defined in
`<defs>` but no longer referenced by the connector.

**Covers**: `Rendering-Svg-SvgRenderer-EndMarkers`,
`Rendering-Svg-SvgRenderer-EndMarkersOpenChevronReference`,
`Rendering-Svg-SvgRenderer-HollowTriangleEndMarkers`,
`Rendering-Svg-SvgRenderer-HollowTriangleEndMarkerReference`,
`Rendering-Svg-SvgRenderer-TriangleEndMarkerMetrics`,
`Rendering-Svg-SvgRenderer-DiamondEndMarkers`,
`Rendering-Svg-SvgRenderer-DiamondEndMarkerReference`,
`Rendering-Svg-SvgRenderer-CrossbarEndMarkers`,
`Rendering-Svg-SvgRenderer-EndMarkerFilledArrow`,
`Rendering-Svg-SvgRenderer-EndMarkerFilledDiamond`,
`Rendering-Svg-SvgRenderer-EndMarkerCircle`,
`Rendering-Svg-SvgRenderer-EndMarkerBar`.

### Requirements Coverage

- **`Rendering-Svg-SvgRenderer-ImplementsIRenderer`**:
  `SvgRenderer_Render_SingleBox_ProducesSvgDocument`
- **`Rendering-Svg-SvgRenderer-MediaType`**: `SvgRenderer_Render_SingleBox_ProducesSvgDocument`
- **`Rendering-Svg-SvgRenderer-DefaultExtension`**:
  `SvgRenderer_Render_SingleBox_ProducesSvgDocument`
- **`Rendering-Svg-SvgRenderer-RenderDocument`**:
  `SvgRenderer_Render_EmptyTree_ProducesSvgDocument`
- **`Rendering-Svg-SvgRenderer-RenderEmptyTree`**:
  `SvgRenderer_Render_EmptyTree_ProducesSvgDocument`
- **`Rendering-Svg-SvgRenderer-RenderBox`**: `SvgRenderer_Render_SingleBox_ProducesRectElement`
- **`Rendering-Svg-SvgRenderer-RenderBoxRoundedCorners`**:
  `SvgRenderer_Render_BoxRoundedRectangle_ProducesRxAttribute`
- **`Rendering-Svg-SvgRenderer-RenderBoxCompartments`**:
  `SvgRenderer_Render_BoxWithCompartment_ProducesLineAndText`
- **`Rendering-Svg-SvgRenderer-RenderLabel`**:
  `SvgRenderer_Render_SingleLabel_ProducesTextElement`
- **`Rendering-Svg-SvgRenderer-RenderLabelBold`**:
  `SvgRenderer_Render_LabelWithBold_ProducesBoldAttribute`
- **`Rendering-Svg-SvgRenderer-RenderLabelItalic`**:
  `SvgRenderer_Render_LabelWithItalic_ProducesItalicAttribute`
- **`Rendering-Svg-SvgRenderer-RenderLabelEscaping`**:
  `SvgRenderer_Render_LabelWithXmlSpecialCharacters_ProducesWellFormedEscapedSvg`
- **`Rendering-Svg-SvgRenderer-RenderLine`**: `SvgRenderer_Render_SingleLine_ProducesPathElement`
- **`Rendering-Svg-SvgRenderer-RenderLineRoundedCorners`**:
  `SvgRenderer_Render_SingleLine_WithCornerRadius_ProducesArcInPath`
- **`Rendering-Svg-SvgRenderer-RenderLineDashed`**:
  `SvgRenderer_Render_SingleLine_Dashed_ProducesDashArray`
- **`Rendering-Svg-SvgRenderer-RenderLineMidpointLabel`**:
  `SvgRenderer_Render_LineWithMidpointLabel_ProducesTextElement`
- **`Rendering-Svg-SvgRenderer-RenderNodeKinds`**: `SvgRenderer_Render_SinglePort_ProducesRect`
- **`Rendering-Svg-SvgRenderer-RenderPortLabel`**: `SvgRenderer_RenderPort_LeftRightLabel_ReadsInward`,
  `SvgRenderer_RenderPort_TopBottomLabel_ReadsInward`
- **`Rendering-Svg-SvgRenderer-ContentInset`**:
  `SvgRenderer_RenderBoxCompartments_ContentInsetLeft_ShiftsRowTextRight`
- **`Rendering-Svg-SvgRenderer-RenderBadge`**:
  `SvgRenderer_Render_SingleBadge_FilledCircle_ProducesCircle`
- **`Rendering-Svg-SvgRenderer-BadgeBullseye`**:
  `SvgRenderer_Render_SingleBadge_Bullseye_ProducesConcentricCircles`
- **`Rendering-Svg-SvgRenderer-BadgeDiamond`**:
  `SvgRenderer_Render_SingleBadge_Diamond_ProducesPolygon`
- **`Rendering-Svg-SvgRenderer-BadgeHorizontalBar`**:
  `SvgRenderer_Render_SingleBadge_HorizontalBar_ProducesLine`
- **`Rendering-Svg-SvgRenderer-BadgeVerticalBar`**:
  `SvgRenderer_Render_SingleBadge_VerticalBar_ProducesLine`
- **`Rendering-Svg-SvgRenderer-RenderBand`**: `SvgRenderer_Render_SingleBand_ProducesRect`
- **`Rendering-Svg-SvgRenderer-RenderLifeline`**:
  `SvgRenderer_Render_SingleLifeline_ProducesRectAndLine`
- **`Rendering-Svg-SvgRenderer-RenderActivation`**:
  `SvgRenderer_Render_SingleActivation_ProducesRect`
- **`Rendering-Svg-SvgRenderer-RenderGrid`**: `SvgRenderer_Render_SingleGrid_ProducesRects`
- **`Rendering-Svg-SvgRenderer-EndMarkers`**: `OpenChevron_IsDefinedAsPolyline`
- **`Rendering-Svg-SvgRenderer-EndMarkersOpenChevronReference`**:
  `OpenChevronLine_ReferencesOpenChevronMarker`
- **`Rendering-Svg-SvgRenderer-HollowTriangleEndMarkers`**:
  `HollowTriangle_IsDefinedAsClosedPolygon`
- **`Rendering-Svg-SvgRenderer-HollowTriangleEndMarkerReference`**:
  `SvgRenderer_Render_SingleLine_WithHollowTriangleArrowhead_ProducesMarkerEnd`
- **`Rendering-Svg-SvgRenderer-TriangleEndMarkerMetrics`**:
  `TriangleMarker_DimensionsDeriveFromNotationMetrics`
- **`Rendering-Svg-SvgRenderer-DiamondEndMarkers`**:
  `DiamondMarker_DimensionsDeriveFromNotationMetrics`
- **`Rendering-Svg-SvgRenderer-DiamondEndMarkerReference`**:
  `SvgRenderer_Render_SingleLine_WithDiamondArrowhead_ProducesDiamondMarker`
- **`Rendering-Svg-SvgRenderer-CrossbarEndMarkers`**:
  `SvgRenderer_Render_SingleLine_WithOpenCrossbarArrowhead_ProducesOpenCrossbarMarker`
- **`Rendering-Svg-SvgRenderer-EndMarkerFilledArrow`**:
  `SvgRenderer_Render_SingleLine_WithFilledArrow_ProducesFilledArrowMarker`
- **`Rendering-Svg-SvgRenderer-EndMarkerFilledDiamond`**:
  `SvgRenderer_Render_SingleLine_WithFilledDiamond_ProducesFilledDiamondMarker`
- **`Rendering-Svg-SvgRenderer-EndMarkerCircle`**:
  `SvgRenderer_Render_SingleLine_WithCircleEnd_ProducesCircleMarker`
- **`Rendering-Svg-SvgRenderer-EndMarkerBar`**:
  `SvgRenderer_Render_SingleLine_WithBarEnd_ProducesBarMarker`
