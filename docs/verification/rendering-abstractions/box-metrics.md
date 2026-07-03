## Box Metrics Unit Verification

Part of the Rendering Abstractions Verification.

This document describes the verification design for the box-metrics unit of the
`DemaConsulting.Rendering.Abstractions` system. It maps every box-metrics unit requirement to at least
one named test scenario so a reviewer can confirm coverage without reading the test code. The
verification strategy, test environment, and acceptance criteria are described in the
system verification document; the test project is
`DemaConsulting.Rendering.Abstractions.Tests` (`BoxMetricsTests.cs`).

### Verification Approach

The box-metrics unit is verified with in-process xUnit unit tests that call
`BoxMetrics.FolderTabHeight` and `BoxMetrics.TitleAreaHeight` directly, passing one of the built-in
`Themes` as the theme input. No mocking is used because the helpers are pure static functions of a
`Theme` and a pair of booleans. The tests cover the folder-tab formula and every combination of the
`hasLabel` and `hasKeyword` inputs relevant to the title-area formula, including the empty-title
zero case and the label-plus-keyword additive case.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK on `net8.0`, `net9.0`, and `net10.0`.
- **Execution**: `dotnet test` invoked by `build.ps1` and by the CI pipeline.
- **Test project**: `DemaConsulting.Rendering.Abstractions.Tests` (`BoxMetricsTests.cs`).
- **External dependencies**: none.

### Acceptance Criteria

The unit is considered verified when every scenario listed below passes. Each test must return the
documented height in logical pixels; any wrong arithmetic result, unexpected exception, or missing
coverage of the reserved-space formulas constitutes a failure.

### Test Scenarios

#### Folder-tab height derives from theme

Test `BoxMetrics_FolderTabHeight_DerivesFromThemeBodyFontAndPadding` calls `FolderTabHeight` with the
Light theme (body font 12, padding 6) and asserts the height is 24, equal to the body font size plus
two label paddings.

**Covers**: `Rendering-Abstractions-BoxMetrics-FolderTabHeight`.

#### Title-area height reflects present lines

Tests `BoxMetrics_TitleAreaHeight_NoLabelNoKeyword_IsZero`,
`BoxMetrics_TitleAreaHeight_LabelOnly_ReservesTitleLine`, and
`BoxMetrics_TitleAreaHeight_LabelAndKeyword_ReservesBothLines` assert that no title area is reserved
when neither a name nor a keyword is present, that a labelled box reserves padding plus one title line,
and that a keyword-and-name box reserves padding plus both lines.

**Covers**: `Rendering-Abstractions-BoxMetrics-TitleAreaHeight`.

#### Null theme is rejected

Tests `BoxMetrics_FolderTabHeight_NullTheme_ThrowsArgumentNullException` and
`BoxMetrics_TitleAreaHeight_NullTheme_ThrowsArgumentNullException` call each helper with a `null`
theme and assert that an `ArgumentNullException` is thrown.

**Covers**: `Rendering-Abstractions-BoxMetrics-RejectNullTheme`.

### Requirements Coverage

- **`Rendering-Abstractions-BoxMetrics-FolderTabHeight`**: BoxMetrics_FolderTabHeight_DerivesFromThemeBodyFontAndPadding
- **`Rendering-Abstractions-BoxMetrics-TitleAreaHeight`**: BoxMetrics_TitleAreaHeight_NoLabelNoKeyword_IsZero,
  BoxMetrics_TitleAreaHeight_LabelOnly_ReservesTitleLine, BoxMetrics_TitleAreaHeight_LabelAndKeyword_ReservesBothLines
- **`Rendering-Abstractions-BoxMetrics-RejectNullTheme`**: BoxMetrics_FolderTabHeight_NullTheme_ThrowsArgumentNullException,
  BoxMetrics_TitleAreaHeight_NullTheme_ThrowsArgumentNullException
