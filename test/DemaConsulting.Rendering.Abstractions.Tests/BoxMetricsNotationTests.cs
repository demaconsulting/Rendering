// <copyright file="BoxMetricsNotationTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Abstractions.Tests;

/// <summary>
///     Tests for the notation-primitive additions to <see cref="BoxMetrics"/>: the folder name-in-tab
///     title rule, the centralized folder-tab resolution, and the lifeline header height.
/// </summary>
public sealed class BoxMetricsNotationTests
{
    private static readonly BoxAppearance InTab = new(FolderTitle: FolderTitlePlacement.Tab);

    private static LayoutBox Folder(string? label, BoxAppearance? appearance, string? keyword = null, double width = 300) =>
        new(0, 0, width, 100, label, 0, BoxShape.Folder, [], [], Keyword: keyword, Appearance: appearance);

    /// <summary>
    ///     A box with no appearance has exactly the legacy title-area height.
    /// </summary>
    [Fact]
    public void TitleAreaHeightForBox_DefaultBox_EqualsLegacyOverload()
    {
        var theme = Themes.Light;
        var box = new LayoutBox(0, 0, 100, 50, "A", 0, BoxShape.Folder, [], [], Keyword: "package");

        Assert.Equal(BoxMetrics.TitleAreaHeight(theme, true, true), BoxMetrics.TitleAreaHeight(box, theme));
    }

    /// <summary>
    ///     A folder whose name is drawn in the tab reserves no body title band for the name.
    /// </summary>
    [Fact]
    public void TitleAreaHeightForBox_FolderNameInTab_ExcludesNameLine()
    {
        var theme = Themes.Light;

        Assert.Equal(0.0, BoxMetrics.TitleAreaHeight(Folder("Pkg", InTab), theme));
        Assert.Equal(BoxMetrics.TitleAreaHeight(theme, false, true), BoxMetrics.TitleAreaHeight(Folder("Pkg", InTab, "package"), theme));
    }

    /// <summary>
    ///     The tab-title flag is ignored for a non-folder shape.
    /// </summary>
    [Fact]
    public void TitleAreaHeightForBox_NonFolderWithTabAppearance_IsUnaffected()
    {
        var theme = Themes.Light;
        var box = new LayoutBox(0, 0, 100, 50, "A", 0, BoxShape.Rectangle, [], [], Appearance: InTab);

        Assert.Equal(BoxMetrics.TitleAreaHeight(theme, true, false), BoxMetrics.TitleAreaHeight(box, theme));
    }

    /// <summary>
    ///     Null arguments are rejected by the box-aware title-area height.
    /// </summary>
    [Fact]
    public void TitleAreaHeightForBox_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => BoxMetrics.TitleAreaHeight(null!, Themes.Light));
        Assert.Throws<ArgumentNullException>(() => BoxMetrics.TitleAreaHeight(Folder("A", null), null!));
    }

    /// <summary>
    ///     The name is in the tab only for a labeled folder that requests the tab placement, whether
    ///     asked of a box or of its shape, label and appearance.
    /// </summary>
    [Fact]
    public void IsNameInTab_OnlyForLabeledFolderRequestingTab()
    {
        Assert.True(BoxMetrics.IsNameInTab(Folder("Pkg", InTab)));
        Assert.False(BoxMetrics.IsNameInTab(Folder(null, InTab)));
        Assert.False(BoxMetrics.IsNameInTab(Folder("Pkg", null)));
        Assert.False(BoxMetrics.IsNameInTab(Folder("Pkg", new BoxAppearance())));
        Assert.True(BoxMetrics.IsNameInTab(BoxShape.Folder, "Pkg", InTab));
        Assert.False(BoxMetrics.IsNameInTab(BoxShape.Rectangle, "Pkg", InTab));
        Assert.Throws<ArgumentNullException>(() => BoxMetrics.IsNameInTab(null!));
    }

    /// <summary>
    ///     Without the tab placement the folder-tab width is the historical estimate, exactly.
    /// </summary>
    [Fact]
    public void ResolveFolderTabWidth_BodyTitle_MatchesLegacyFormula()
    {
        var theme = Themes.Light;
        var box = Folder("Utilities", null, width: 400);

        var legacy = Math.Min(
            box.Width * NotationMetrics.FolderTabMaxWidthFraction,
            Math.Max(
                NotationMetrics.FolderTabMinWidth,
                ("Utilities".Length * theme.FontSizeBody * NotationMetrics.FolderLabelCharWidthFactor) + (2.0 * theme.LabelPadding)));

        Assert.Equal(legacy, BoxMetrics.ResolveFolderTabWidth(box, theme));
    }

    /// <summary>
    ///     A caller-supplied tab width is honored (and clamped to zero when negative).
    /// </summary>
    [Fact]
    public void ResolveFolderTabWidth_ExplicitWidth_IsHonored()
    {
        var theme = Themes.Light;

        Assert.Equal(77.0, BoxMetrics.ResolveFolderTabWidth(Folder("A", InTab) with { FolderTabWidth = 77.0 }, theme));
        Assert.Equal(0.0, BoxMetrics.ResolveFolderTabWidth(Folder("A", null) with { FolderTabWidth = -5.0 }, theme));
    }

    /// <summary>
    ///     A name-in-tab folder gets a tab sized to the measured name plus padding, never narrower than
    ///     the minimum tab width and never wider than the box.
    /// </summary>
    [Fact]
    public void ResolveFolderTabWidth_NameInTab_FitsMeasuredName()
    {
        var theme = Themes.Light;
        const string Name = "AVeryLongPackageNameThatNeedsAWideTab";
        var nameWidth = PortLabelWidthEstimator.MeasureWidth(Name, theme.FontSizeBody);

        var wide = BoxMetrics.ResolveFolderTabWidth(Folder(Name, InTab, width: 800), theme);
        var narrow = BoxMetrics.ResolveFolderTabWidth(Folder("A", InTab, width: 800), theme);
        var capped = BoxMetrics.ResolveFolderTabWidth(Folder(Name, InTab, width: 90), theme);

        Assert.Equal(nameWidth + (2.0 * theme.LabelPadding), wide, 1e-9);
        Assert.Equal(NotationMetrics.FolderTabMinWidth, narrow, 1e-9);
        Assert.Equal(90.0, capped, 1e-9);
    }

    /// <summary>
    ///     The folder-tab height is the caller-supplied value (clamped to zero) or the theme default.
    /// </summary>
    [Fact]
    public void ResolveFolderTabHeight_ExplicitAndDefault()
    {
        var theme = Themes.Light;

        Assert.Equal(BoxMetrics.FolderTabHeight(theme), BoxMetrics.ResolveFolderTabHeight(Folder("A", null), theme));
        Assert.Equal(31.0, BoxMetrics.ResolveFolderTabHeight(Folder("A", null) with { FolderTabHeight = 31.0 }, theme));
        Assert.Equal(0.0, BoxMetrics.ResolveFolderTabHeight(Folder("A", null) with { FolderTabHeight = -1.0 }, theme));
        Assert.Throws<ArgumentNullException>(() => BoxMetrics.ResolveFolderTabHeight(null!, theme));
        Assert.Throws<ArgumentNullException>(() => BoxMetrics.ResolveFolderTabWidth(Folder("A", null), null!));
    }

    /// <summary>
    ///     A keyword adds one body-font line plus padding to the lifeline header height.
    /// </summary>
    [Fact]
    public void LifelineHeaderHeight_KeywordAddsOneLine()
    {
        var theme = Themes.Light;

        var plain = BoxMetrics.LifelineHeaderHeight(theme, hasKeyword: false);
        var keyword = BoxMetrics.LifelineHeaderHeight(theme, hasKeyword: true);

        Assert.Equal((2.0 * theme.LabelPadding) + theme.FontSizeBody, plain, 1e-9);
        Assert.Equal(plain + theme.FontSizeBody + theme.LabelPadding, keyword, 1e-9);
    }

    /// <summary>
    ///     A null theme is rejected by <see cref="BoxMetrics.LifelineHeaderHeight"/>.
    /// </summary>
    [Fact]
    public void LifelineHeaderHeight_NullTheme_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => BoxMetrics.LifelineHeaderHeight(null!, true));
    }

    /// <summary>
    ///     A centered name-in-tab folder with a keyword places its title block using only the keyword line.
    /// </summary>
    [Fact]
    public void TitleCursorTop_FolderNameInTabCentered_UsesKeywordOnlyTitleHeight()
    {
        var theme = Themes.Light;
        var tabbed = Folder("Pkg", InTab, "package") with { CenterTitle = true, Height = 200 };
        var bodied = Folder("Pkg", null, "package") with { CenterTitle = true, Height = 200 };

        var tabbedTop = BoxMetrics.TitleCursorTop(tabbed, theme);
        var bodiedTop = BoxMetrics.TitleCursorTop(bodied, theme);

        // A shorter title block is centered lower in the same available height.
        Assert.True(tabbedTop > bodiedTop);
    }
}
