## Box Metrics Unit Verification

Part of the Rendering Abstractions Verification.

This document describes the verification design for the box-metrics unit of the
`DemaConsulting.Rendering.Abstractions` system. It maps every box-metrics unit requirement to at least
one named test scenario so a reviewer can confirm coverage without reading the test code. The
verification strategy, test environment, and acceptance criteria are described in the
system verification document; the test project is
`DemaConsulting.Rendering.Abstractions.Tests` (`BoxMetricsTests.cs` and `BoxMetricsNotationTests.cs`).

### Verification Approach

The box-metrics unit is verified with in-process xUnit unit tests that call
`BoxMetrics.FolderTabHeight` and `BoxMetrics.TitleAreaHeight` directly, passing one of the built-in
`Themes` as the theme input. No mocking is used because the helpers are pure static functions of a
`Theme` and a pair of booleans. The tests cover the folder-tab formula and every combination of the
`hasLabel` and `hasKeyword` inputs relevant to the title-area formula, including the empty-title
zero case and the label-plus-keyword additive case. The notation-primitive tests
(`BoxMetricsNotationTests.cs`) additionally construct hand-built folder `LayoutBox` values with and
without a `BoxAppearance` requesting the tab title placement, and assert that the box-aware title-area
height, the in-tab rule, the folder-tab width and height resolution, and the lifeline header height
equal the documented formulas, with the default (no appearance) box matching the legacy result exactly.

### Test Environment

- **Framework**: xUnit v4 running under the .NET SDK on `net8.0`, `net9.0`, `net10.0`, and `net11.0`.
- **Execution**: `dotnet test` invoked by `build.ps1` and by the CI pipeline.
- **Test project**: `DemaConsulting.Rendering.Abstractions.Tests` (`BoxMetricsTests.cs` and
  `BoxMetricsNotationTests.cs`).
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
when neither a name nor a keyword is present, that a labeled box reserves padding plus one title line,
and that a keyword-and-name box reserves padding plus both lines.

**Covers**: `Rendering-Abstractions-BoxMetrics-TitleAreaHeight`.

#### Null theme is rejected

Tests `BoxMetrics_FolderTabHeight_NullTheme_ThrowsArgumentNullException` and
`BoxMetrics_TitleAreaHeight_NullTheme_ThrowsArgumentNullException` call each helper with a `null`
theme and assert that an `ArgumentNullException` is thrown.

**Covers**: `Rendering-Abstractions-BoxMetrics-RejectNullTheme`.

#### Folder-tab width and height are resolved in one place

Test `ResolveFolderTabWidth_BodyTitle_MatchesLegacyFormula` builds a folder without the tab placement
and asserts the width equals the historical estimate (the smaller of the box-width fraction and the
larger of the minimum tab width and the label-length estimate plus two paddings). Test
`ResolveFolderTabWidth_ExplicitWidth_IsHonored` asserts an explicit `FolderTabWidth` is returned as
supplied (77) and a negative one is clamped to zero. Test `ResolveFolderTabWidth_NameInTab_FitsMeasuredName`
asserts a name-in-tab folder gets the measured name width plus two paddings, is never narrower than
`NotationMetrics.FolderTabMinWidth` (short name), and is capped at the box width (narrow box). Test
`ResolveFolderTabHeight_ExplicitAndDefault` asserts the default equals `FolderTabHeight(theme)`, an
explicit height (31) is honored, a negative one is clamped to zero, and a `null` box passed to
`ResolveFolderTabHeight` or a `null` theme passed to `ResolveFolderTabWidth` is rejected with an
`ArgumentNullException`.

**Covers**: `Rendering-Abstractions-BoxMetrics-FolderTabResolution`.

#### Box-aware title-area height excludes the in-tab name

Test `TitleAreaHeightForBox_DefaultBox_EqualsLegacyOverload` asserts a box without an appearance gets
exactly the flag-based result. Test `TitleAreaHeightForBox_FolderNameInTab_ExcludesNameLine` asserts a
name-in-tab folder reserves zero without a keyword and the keyword-only height with one. Test
`TitleAreaHeightForBox_NonFolderWithTabAppearance_IsUnaffected` asserts the tab placement is ignored for a
rectangle. Test `TitleAreaHeightForBox_NullArguments_Throw` asserts a `null` box or theme raises
`ArgumentNullException`. Test `IsNameInTab_OnlyForLabeledFolderRequestingTab` asserts the rule is true
only for a labeled folder whose appearance requests the tab (for both the box and the shape, label, and
appearance overloads) and that a `null` box is rejected. Test
`TitleCursorTop_FolderNameInTabCentered_UsesKeywordOnlyTitleHeight` asserts a centered name-in-tab folder
with a keyword begins its title block lower than the same folder with a body title, because its title
block is shorter.

**Covers**: `Rendering-Abstractions-BoxMetrics-TitleAreaHeightForBox`.

#### Lifeline header height adds a keyword line

Test `LifelineHeaderHeight_KeywordAddsOneLine` asserts the header height without a keyword is two
paddings plus the body font size, and that a keyword adds one body font size plus one padding. Test
`LifelineHeaderHeight_NullTheme_Throws` asserts a `null` theme raises `ArgumentNullException`.

**Covers**: `Rendering-Abstractions-BoxMetrics-LifelineHeaderHeight`.

### Requirements Coverage

- **`Rendering-Abstractions-BoxMetrics-FolderTabHeight`**: BoxMetrics_FolderTabHeight_DerivesFromThemeBodyFontAndPadding
- **`Rendering-Abstractions-BoxMetrics-TitleAreaHeight`**: BoxMetrics_TitleAreaHeight_NoLabelNoKeyword_IsZero,
  BoxMetrics_TitleAreaHeight_LabelOnly_ReservesTitleLine, BoxMetrics_TitleAreaHeight_LabelAndKeyword_ReservesBothLines
- **`Rendering-Abstractions-BoxMetrics-RejectNullTheme`**:
  BoxMetrics_FolderTabHeight_NullTheme_ThrowsArgumentNullException,
  BoxMetrics_TitleAreaHeight_NullTheme_ThrowsArgumentNullException
- **`Rendering-Abstractions-BoxMetrics-FolderTabResolution`**:
  ResolveFolderTabWidth_BodyTitle_MatchesLegacyFormula, ResolveFolderTabWidth_ExplicitWidth_IsHonored,
  ResolveFolderTabWidth_NameInTab_FitsMeasuredName, ResolveFolderTabHeight_ExplicitAndDefault
- **`Rendering-Abstractions-BoxMetrics-TitleAreaHeightForBox`**:
  TitleAreaHeightForBox_DefaultBox_EqualsLegacyOverload, TitleAreaHeightForBox_FolderNameInTab_ExcludesNameLine,
  TitleAreaHeightForBox_NonFolderWithTabAppearance_IsUnaffected, TitleAreaHeightForBox_NullArguments_Throw,
  IsNameInTab_OnlyForLabeledFolderRequestingTab, TitleCursorTop_FolderNameInTabCentered_UsesKeywordOnlyTitleHeight
- **`Rendering-Abstractions-BoxMetrics-LifelineHeaderHeight`**: LifelineHeaderHeight_KeywordAddsOneLine,
  LifelineHeaderHeight_NullTheme_Throws
