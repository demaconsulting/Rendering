## SkiaSharp Verification

This document provides the verification evidence for the SkiaSharp OTS software item. Requirements
for this OTS item are defined in the SkiaSharp OTS Software Requirements document.

### Required Functionality

SkiaSharp provides the bitmap canvas, drawing primitives, typeface/text rendering, and image
encoders that `DemaConsulting.Rendering.Skia` uses to rasterize a placed `LayoutTree` into PNG,
JPEG, and WEBP output. Its correct operation is confirmed by the repository's own renderer tests
passing.

### Verification Approach

SkiaSharp is a runtime library rather than build/compliance tooling, so — like xUnit — it has no
separate self-validation suite. It is verified indirectly through this repository's own renderer
tests: each scenario below names a real test that exercises a SkiaSharp feature (bitmap drawing,
text rendering, or image encoding) and asserts on the resulting output. A passing test run
constitutes evidence that SkiaSharp performs the required functionality correctly.

### Test Scenarios

#### Render_SingleBox_ProducesPngSignature

**Scenario**: `PngRenderer` uses SkiaSharp's `SKBitmap`/`SKCanvas` to draw a single box and encodes
the result as PNG.

**Expected**: The output begins with the PNG signature bytes, confirming SkiaSharp's PNG encoder
ran successfully.

**Requirement coverage**: `Rendering-OTS-SkiaSharp-Rasterize`, `Rendering-OTS-SkiaSharp-Encode`.

#### JpegRenderer_Render_ProducesJpegSignature

**Scenario**: `JpegRenderer` draws the same layout onto a SkiaSharp bitmap canvas and encodes it as
JPEG.

**Expected**: The output begins with the JPEG signature bytes, confirming SkiaSharp's JPEG encoder
ran successfully.

**Requirement coverage**: `Rendering-OTS-SkiaSharp-Encode`.

#### WebpRenderer_Render_ProducesWebpContainerHeader

**Scenario**: `WebpRenderer` draws the same layout onto a SkiaSharp bitmap canvas and encodes it as
WEBP.

**Expected**: The output begins with the WEBP container header (`RIFF`/`WEBP`), confirming
SkiaSharp's WEBP encoder ran successfully.

**Requirement coverage**: `Rendering-OTS-SkiaSharp-Encode`.

#### PngRenderer_Render_SingleLine_PixelOnLineIsStrokeColor

**Scenario**: `PngRenderer` draws a single connector line with SkiaSharp's `SKPaint` stroke drawing.

**Expected**: The pixel sampled on the drawn line matches the configured stroke color, confirming
SkiaSharp's shape-drawing primitives operate correctly.

**Requirement coverage**: `Rendering-OTS-SkiaSharp-Rasterize`.

#### PngRenderer_Render_LabelWithXmlSpecialCharacters_ProducesValidPng

**Scenario**: `PngRenderer` renders a node label containing special characters using SkiaSharp's
embedded Noto Sans `SKTypeface` and `SKPaint` text drawing.

**Expected**: A valid, non-empty PNG is produced, confirming SkiaSharp's typeface loading and text
rendering operate correctly regardless of label content.

**Requirement coverage**: `Rendering-OTS-SkiaSharp-Text`.

### Requirements Coverage

- **`Rendering-OTS-SkiaSharp-Rasterize`**: Render_SingleBox_ProducesPngSignature,
  PngRenderer_Render_SingleLine_PixelOnLineIsStrokeColor
- **`Rendering-OTS-SkiaSharp-Text`**: PngRenderer_Render_LabelWithXmlSpecialCharacters_ProducesValidPng
- **`Rendering-OTS-SkiaSharp-Encode`**: Render_SingleBox_ProducesPngSignature,
  JpegRenderer_Render_ProducesJpegSignature, WebpRenderer_Render_ProducesWebpContainerHeader
