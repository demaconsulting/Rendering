## SkiaTextMeasurer Unit Verification

Part of the Rendering.Skia Verification.

This document describes the verification design for the `SkiaTextMeasurer` unit (and its shared
`SkiaTypefaces` typeface-loading helper) of the `DemaConsulting.Rendering.Skia` system. It maps every
SkiaTextMeasurer unit requirement to at least one named test scenario so a reviewer can confirm
coverage without reading the test code.

### SkiaTextMeasurer Verification Approach

`SkiaTextMeasurer` is verified by direct in-process xUnit tests that construct the measurer and call
`MeasureWidth` with real strings, font sizes, and bold/italic combinations, asserting on the returned
`double` values and their relative ordering (rather than on an exact pixel value, since real glyph
metrics vary slightly across SkiaSharp versions and platforms). No mocking is used: the measurer draws
against the package's real embedded Noto Sans typefaces through `SkiaTypefaces`, exactly as it would
in production.

### SkiaTextMeasurer Test Environment

- **Framework**: xUnit v3.
- **Target frameworks**: `net8.0`, `net9.0`, `net10.0`.
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Project**: `test/DemaConsulting.Rendering.Skia.Tests/SkiaTextMeasurerTests.cs`.
- **Native assets**: the SkiaSharp platform-specific native asset packages must be available at test
  time; no other setup beyond `dotnet restore` is required.
- **Isolation**: each test constructs its own `SkiaTextMeasurer` instance; there is no shared mutable
  state between tests.

### SkiaTextMeasurer Acceptance Criteria

A verification run passes when every scenario below executes without an unexpected exception and the
observed measured widths satisfy the expected relative ordering (wider for longer text or a larger
font size, zero for an empty string, a thrown `ArgumentNullException` for null text, and a distinct
value for a bold variant versus the regular one). Any violation of these relative orderings, or any
missing/unexpected exception, constitutes a failure.

### SkiaTextMeasurer Unit Scenarios

#### Measured width scales with text length and font size

Tests `MeasureWidth_LongerText_MeasuresWider` and `MeasureWidth_LargerFontSize_MeasuresWider` confirm
a longer string measures at least as wide as a shorter one at the same font size, and a larger font
size measures at least as wide as a smaller one for the same string.

**Covers**: `Rendering-Skia-SkiaTextMeasurer-MeasuresRealFontMetrics`.

#### Empty string measures zero and null text is rejected

Test `MeasureWidth_EmptyString_ReturnsZero` confirms an empty string measures `0.0`.
Test `MeasureWidth_NullText_ThrowsArgumentNullException` confirms a null `text` argument is rejected
with `ArgumentNullException`.

**Covers**: `Rendering-Skia-SkiaTextMeasurer-MeasuresRealFontMetrics`.

#### Bold text measures against a distinct typeface from regular

Test `MeasureWidth_Bold_UsesDistinctTypefaceFromRegular` confirms the same string measured bold
differs from the same string measured regular, proving the `bold` flag actually selects a different
embedded typeface rather than being ignored.

**Covers**: `Rendering-Skia-SkiaTextMeasurer-MeasuresRealFontMetrics`.

#### Shared typeface resolution is stable and distinct per variant

Test `SkiaTypefaces_Resolve_ReturnsStableDistinctTypefacesPerVariant` resolves each of the four
bold/italic combinations twice and asserts the same combination returns the same `SKTypeface`
instance both times (stability), while different combinations return different instances
(distinctness) — confirming `SkiaRasterRenderer` and `SkiaTextMeasurer` measure and draw against
the exact same lazily-loaded typeface objects.

**Covers**: `Rendering-Skia-SkiaTypefaces-SharedResolution`.

### Requirements Coverage

- **`Rendering-Skia-SkiaTextMeasurer-MeasuresRealFontMetrics`**:
  MeasureWidth_LongerText_MeasuresWider, MeasureWidth_LargerFontSize_MeasuresWider,
  MeasureWidth_EmptyString_ReturnsZero, MeasureWidth_NullText_ThrowsArgumentNullException,
  MeasureWidth_Bold_UsesDistinctTypefaceFromRegular
- **`Rendering-Skia-SkiaTypefaces-SharedResolution`**:
  SkiaTypefaces_Resolve_ReturnsStableDistinctTypefacesPerVariant
