## Port Label Width Estimator Unit Verification

Part of the Rendering Abstractions Verification.

This document describes the verification design for the port-label-width-estimator unit of the
`DemaConsulting.Rendering.Abstractions` system. It maps every port-label-width-estimator unit
requirement to at least one named test scenario so a reviewer can confirm coverage without reading
the test code. The verification strategy, test environment, and acceptance criteria are described in
the system verification document; the test project is
`DemaConsulting.Rendering.Abstractions.Tests` (`PortLabelWidthEstimatorTests.cs`).

### Verification Approach

The port-label-width-estimator unit is verified with in-process xUnit unit tests that call
`PortLabelWidthEstimator.MeasureWidth` directly with a variety of text and font-size inputs. No
mocking is used because the method is a pure static function of a string and a `double`. The tests
cover the linear font-size scaling basis, relative comparisons between differently-sized strings, the
empty-string zero case, a pinned known-character value at the table's nominal 100px basis, the
median-width fallback for an unmapped character, and the null-argument guard.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK on `net8.0`, `net9.0`, and `net10.0`.
- **Execution**: `dotnet test` invoked by `build.ps1` and by the CI pipeline.
- **Test project**: `DemaConsulting.Rendering.Abstractions.Tests` (`PortLabelWidthEstimatorTests.cs`).
- **External dependencies**: none.

### Acceptance Criteria

The unit is considered verified when every scenario listed below passes. Each test must return the
documented estimated width, in logical pixels, or throw the documented exception; any wrong
arithmetic result, unexpected exception, or missing coverage of the table/fallback behavior
constitutes a failure.

### Test Scenarios

#### Measured width scales linearly with font size

Test `MeasureWidth_ScalesLinearlyWithFontSize` measures the same text at font sizes 10.0 and 30.0
and asserts the ratio is exactly 3.0, confirming the `fontSize / 100.0` scaling basis.

**Covers**: `Rendering-Abstractions-PortLabelWidthEstimator-MeasureWidth`.

#### Longer text measures wider

Test `MeasureWidth_LongerText_MeasuresWider` asserts that `"hello world"` measures wider than
`"hi"` at the same font size.

**Covers**: `Rendering-Abstractions-PortLabelWidthEstimator-MeasureWidth`.

#### Known character matches its mapped width

Test `MeasureWidth_KnownCharacter_MatchesMappedWidth` measures `"M"` at `fontSize = 100.0` and
asserts the result is exactly `87.0`, pinning the table's nominal 100px scale basis.

**Covers**: `Rendering-Abstractions-PortLabelWidthEstimator-MeasureWidth`.

#### Empty string measures to zero

Test `MeasureWidth_EmptyString_ReturnsZero` measures `string.Empty` and asserts the result is `0.0`.

**Covers**: `Rendering-Abstractions-PortLabelWidthEstimator-EmptyText`.

#### Unmapped character uses the median fallback

Test `MeasureWidth_UnmappedCharacter_UsesMedianFallback` measures a character outside the mapped
table (`"€"`) at `fontSize = 100.0` and asserts the result is exactly `55.0`, the fallback median
width.

**Covers**: `Rendering-Abstractions-PortLabelWidthEstimator-UnknownCharacterFallback`.

#### Null text is rejected

Test `MeasureWidth_NullText_ThrowsArgumentNullException` calls `MeasureWidth` with a `null` text
argument and asserts that an `ArgumentNullException` is thrown.

**Covers**: `Rendering-Abstractions-PortLabelWidthEstimator-RejectNullText`.

### Requirements Coverage

- **`Rendering-Abstractions-PortLabelWidthEstimator-MeasureWidth`**: MeasureWidth_ScalesLinearlyWithFontSize,
  MeasureWidth_LongerText_MeasuresWider, MeasureWidth_KnownCharacter_MatchesMappedWidth
- **`Rendering-Abstractions-PortLabelWidthEstimator-EmptyText`**: MeasureWidth_EmptyString_ReturnsZero
- **`Rendering-Abstractions-PortLabelWidthEstimator-UnknownCharacterFallback`**: MeasureWidth_UnmappedCharacter_UsesMedianFallback
- **`Rendering-Abstractions-PortLabelWidthEstimator-RejectNullText`**: MeasureWidth_NullText_ThrowsArgumentNullException
