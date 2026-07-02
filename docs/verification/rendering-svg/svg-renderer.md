# SvgRenderer Unit Verification

Part of the [Rendering.Svg Verification](rendering-svg.md).

This document describes the verification design for the `SvgRenderer` unit of the
`DemaConsulting.Rendering.Svg` system. It maps every SvgRenderer unit requirement to at least one
named test scenario so a reviewer can confirm coverage without reading the test code. The verification
strategy, test environment, and acceptance criteria are described in the
[system verification document](rendering-svg.md); the test project is
`DemaConsulting.Rendering.Svg.Tests`.

## SvgRenderer Unit Scenarios

### Renderer contract and metadata

Test `SvgRenderer_Render_SingleBox_ProducesSvgDocument` renders a single box, asserts that SVG markup
is produced, and checks `MediaType` is `image/svg+xml` and `DefaultExtension` is `.svg`.

**Covers**: `Rendering-Svg-SvgRenderer-ImplementsIRenderer`,
`Rendering-Svg-SvgRenderer-MediaType`, `Rendering-Svg-SvgRenderer-DefaultExtension`.

### SVG document root and empty tree

Test `SvgRenderer_Render_EmptyTree_ProducesSvgDocument` renders an empty `LayoutTree`, asserts the
output stream is non-empty, and checks that the decoded text contains `<svg` and `</svg>`.

**Covers**: `Rendering-Svg-SvgRenderer-RenderDocument`,
`Rendering-Svg-SvgRenderer-RenderEmptyTree`.

### Box rectangle, rounded corners, and compartments

Tests `SvgRenderer_Render_SingleBox_ProducesRectElement`,
`SvgRenderer_Render_BoxRoundedRectangle_ProducesRxAttribute`, and
`SvgRenderer_Render_BoxWithCompartment_ProducesLineAndText` render boxes and assert that a `<rect>`
element, an `rx` rounded-corner attribute, and compartment divider/text content are present.

**Covers**: `Rendering-Svg-SvgRenderer-RenderBox`,
`Rendering-Svg-SvgRenderer-RenderBoxRoundedCorners`,
`Rendering-Svg-SvgRenderer-RenderBoxCompartments`.

### Label text, styling, and escaping

Tests `SvgRenderer_Render_SingleLabel_ProducesTextElement`,
`SvgRenderer_Render_LabelWithBold_ProducesBoldAttribute`,
`SvgRenderer_Render_LabelWithItalic_ProducesItalicAttribute`, and
`SvgRenderer_Render_LabelWithXmlSpecialCharacters_ProducesWellFormedEscapedSvg` render labels and
assert on `<text>`, bold and italic attributes, escaped XML entities, and XML parseability.

**Covers**: `Rendering-Svg-SvgRenderer-RenderLabel`,
`Rendering-Svg-SvgRenderer-RenderLabelBold`, `Rendering-Svg-SvgRenderer-RenderLabelItalic`,
`Rendering-Svg-SvgRenderer-RenderLabelEscaping`.

### Connector path, corners, dash pattern, and label

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

### Additional node kinds

Tests `SvgRenderer_Render_SinglePort_ProducesRect`,
`SvgRenderer_Render_SingleBadge_FilledCircle_ProducesCircle`,
`SvgRenderer_Render_SingleBand_ProducesRect`,
`SvgRenderer_Render_SingleLifeline_ProducesRectAndLine`,
`SvgRenderer_Render_SingleActivation_ProducesRect`, and
`SvgRenderer_Render_SingleGrid_ProducesRects` assert that ports, filled-circle badges, bands,
lifelines, activations, and grids emit their expected SVG element types.

**Covers**: `Rendering-Svg-SvgRenderer-RenderNodeKinds`,
`Rendering-Svg-SvgRenderer-RenderBadge`, `Rendering-Svg-SvgRenderer-RenderBand`,
`Rendering-Svg-SvgRenderer-RenderLifeline`, `Rendering-Svg-SvgRenderer-RenderActivation`,
`Rendering-Svg-SvgRenderer-RenderGrid`.

### Connector end markers

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

The diamond and crossbar id-presence assertions inspect the full SVG document. Because the `<defs>`
block contains all marker ids, those assertions can pass even if the connector path does not reference
the marker. They are documented here as id-presence assertions rather than strong path-reference
assertions.

**Covers**: `Rendering-Svg-SvgRenderer-EndMarkers`,
`Rendering-Svg-SvgRenderer-EndMarkersOpenChevronReference`,
`Rendering-Svg-SvgRenderer-HollowTriangleEndMarkers`,
`Rendering-Svg-SvgRenderer-HollowTriangleEndMarkerReference`,
`Rendering-Svg-SvgRenderer-TriangleEndMarkerMetrics`,
`Rendering-Svg-SvgRenderer-DiamondEndMarkers`,
`Rendering-Svg-SvgRenderer-DiamondEndMarkerReference`,
`Rendering-Svg-SvgRenderer-CrossbarEndMarkers`.

## Requirements Coverage

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
- **`Rendering-Svg-SvgRenderer-RenderBadge`**:
  `SvgRenderer_Render_SingleBadge_FilledCircle_ProducesCircle`
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
