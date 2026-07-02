# Box Metrics Unit Verification

Part of the [Rendering Abstractions Verification](rendering-abstractions.md).

This document describes the verification design for the box-metrics unit of the
`DemaConsulting.Rendering.Abstractions` system. It maps every box-metrics unit requirement to at least
one named test scenario so a reviewer can confirm coverage without reading the test code. The
verification strategy, test environment, and acceptance criteria are described in the
[system verification document](rendering-abstractions.md); the test project is
`DemaConsulting.Rendering.Abstractions.Tests` (`BoxMetricsTests.cs`).

## Box Metrics Unit Scenarios

### Folder-tab height derives from theme

Test `BoxMetrics_FolderTabHeight_DerivesFromThemeBodyFontAndPadding` calls `FolderTabHeight` with the
Light theme (body font 12, padding 6) and asserts the height is 24, equal to the body font size plus
two label paddings.

**Covers**: `Rendering-Abstractions-BoxMetrics-FolderTabHeight`.

### Title-area height reflects present lines

Tests `BoxMetrics_TitleAreaHeight_NoLabelNoKeyword_IsZero`,
`BoxMetrics_TitleAreaHeight_LabelOnly_ReservesTitleLine`, and
`BoxMetrics_TitleAreaHeight_LabelAndKeyword_ReservesBothLines` assert that no title area is reserved
when neither a name nor a keyword is present, that a labelled box reserves padding plus one title line,
and that a keyword-and-name box reserves padding plus both lines.

**Covers**: `Rendering-Abstractions-BoxMetrics-TitleAreaHeight`.

## Requirements Coverage

- **`Rendering-Abstractions-BoxMetrics-FolderTabHeight`**: BoxMetrics_FolderTabHeight_DerivesFromThemeBodyFontAndPadding
- **`Rendering-Abstractions-BoxMetrics-TitleAreaHeight`**: BoxMetrics_TitleAreaHeight_NoLabelNoKeyword_IsZero,
  BoxMetrics_TitleAreaHeight_LabelOnly_ReservesTitleLine, BoxMetrics_TitleAreaHeight_LabelAndKeyword_ReservesBothLines
