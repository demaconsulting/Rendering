## CanvasNet Verification

This document provides the verification evidence for the CanvasNet OTS software item. Requirements
for this OTS item are defined in the CanvasNet OTS Software Requirements document.

### Required Functionality

CanvasNet provides the managed raster surface, drawing primitives, typeface and text rendering,
and image encoders that `DemaConsulting.Rendering.CanvasNet` uses to rasterize a placed
`LayoutTree` into PNG and JPEG output. Its correct operation is confirmed by the repository's own
renderer tests passing.

### Verification Approach

CanvasNet is a runtime library rather than build/compliance tooling, so — like xUnit — it has no
separate self-validation suite. It is verified indirectly through this repository's own renderer
tests: each scenario below names a real test that exercises a CanvasNet feature (surface drawing,
text rendering, or image encoding) and asserts on the resulting output. A passing test run
constitutes evidence that CanvasNet performs the required functionality correctly.

### Test Scenarios

#### Render_SingleBox_ProducesPngSignature

**Scenario**: `PngRenderer` uses CanvasNet's managed `Surface` and drawing `Canvas` to draw a
single box and encodes the result as PNG.

**Expected**: The output begins with the PNG signature bytes, confirming CanvasNet's PNG codec ran
successfully.

**Requirement coverage**: `Rendering-OTS-CanvasNet-Rasterize`,
`Rendering-OTS-CanvasNet-EncodePng`.

#### JpegRenderer_Render_ProducesJpegSignature

**Scenario**: `JpegRenderer` draws the same layout onto a CanvasNet surface and encodes it as
JPEG.

**Expected**: The output begins with the JPEG signature bytes, confirming CanvasNet's JPEG codec
ran successfully.

**Requirement coverage**: `Rendering-OTS-CanvasNet-EncodeJpeg`.

#### PngRenderer_Render_SingleLine_PixelOnLineIsStrokeColor

**Scenario**: `PngRenderer` draws a single connector line with CanvasNet's managed stroke drawing.

**Expected**: The pixel sampled on the drawn line matches the configured stroke color, confirming
CanvasNet's shape-drawing primitives operate correctly.

**Requirement coverage**: `Rendering-OTS-CanvasNet-Rasterize`.

#### PngRenderer_Render_LabelWithXmlSpecialCharacters_ProducesValidPng

**Scenario**: `PngRenderer` renders a node label containing special characters using CanvasNet's
embedded Noto Sans `TrueTypeFont` and `TextRenderer` path.

**Expected**: A valid, non-empty PNG is produced, confirming CanvasNet's typeface loading and text
rendering operate correctly regardless of label content.

**Requirement coverage**: `Rendering-OTS-CanvasNet-Text`.

### Requirements Coverage

- **`Rendering-OTS-CanvasNet-Rasterize`**: Render_SingleBox_ProducesPngSignature,
  PngRenderer_Render_SingleLine_PixelOnLineIsStrokeColor
- **`Rendering-OTS-CanvasNet-Text`**:
  PngRenderer_Render_LabelWithXmlSpecialCharacters_ProducesValidPng
- **`Rendering-OTS-CanvasNet-EncodePng`**: Render_SingleBox_ProducesPngSignature
- **`Rendering-OTS-CanvasNet-EncodeJpeg`**: JpegRenderer_Render_ProducesJpegSignature
