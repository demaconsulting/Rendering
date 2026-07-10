// <copyright file="BoxMetricsTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Abstractions.Tests;

/// <summary>
///     Tests for <see cref="BoxMetrics"/>, the shared box title-area and folder-tab height
///     formulas that keep space reserved by the layout strategies equal to space drawn by the
///     renderers. Values are asserted against the Light theme's font sizes and label padding.
/// </summary>
public sealed class BoxMetricsTests
{
    /// <summary>The folder-tab height is the body font size plus two label paddings.</summary>
    [Fact]
    public void BoxMetrics_FolderTabHeight_DerivesFromThemeBodyFontAndPadding()
    {
        // Arrange: the Light theme (FontSizeBody 12, LabelPadding 6).
        var theme = Themes.Light;

        // Act
        var height = BoxMetrics.FolderTabHeight(theme);

        // Assert: body font size plus two label paddings (12 + 2*6 = 24).
        Assert.Equal(theme.FontSizeBody + (2.0 * theme.LabelPadding), height);
        Assert.Equal(24.0, height);
    }

    /// <summary>A box with neither a name label nor a keyword reserves no title area.</summary>
    [Fact]
    public void BoxMetrics_TitleAreaHeight_NoLabelNoKeyword_IsZero()
    {
        // Arrange
        var theme = Themes.Light;

        // Act
        var height = BoxMetrics.TitleAreaHeight(theme, hasLabel: false, hasKeyword: false);

        // Assert
        Assert.Equal(0.0, height);
    }

    /// <summary>A labelled box reserves padding plus one title line.</summary>
    [Fact]
    public void BoxMetrics_TitleAreaHeight_LabelOnly_ReservesTitleLine()
    {
        // Arrange
        var theme = Themes.Light;

        // Act
        var height = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: false);

        // Assert: leading padding + (title font + trailing padding).
        Assert.Equal(theme.LabelPadding + theme.FontSizeTitle + theme.LabelPadding, height);
    }

    /// <summary>A box with a keyword and a name reserves padding plus both lines.</summary>
    [Fact]
    public void BoxMetrics_TitleAreaHeight_LabelAndKeyword_ReservesBothLines()
    {
        // Arrange
        var theme = Themes.Light;

        // Act
        var height = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true);

        // Assert: leading padding + keyword line + name line, each followed by padding.
        Assert.Equal(
            theme.LabelPadding + theme.FontSizeBody + theme.LabelPadding + theme.FontSizeTitle + theme.LabelPadding,
            height);
    }

    /// <summary>A null theme is rejected with an <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void BoxMetrics_FolderTabHeight_NullTheme_ThrowsArgumentNullException()
    {
        // Arrange
        Theme? theme = null;

        // Act
        void Act() => BoxMetrics.FolderTabHeight(theme!);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    /// <summary>A null theme is rejected with an <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void BoxMetrics_TitleAreaHeight_NullTheme_ThrowsArgumentNullException()
    {
        // Arrange
        Theme? theme = null;

        // Act
        void Act() => BoxMetrics.TitleAreaHeight(theme!, hasLabel: true, hasKeyword: true);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    /// <summary>No left/right ports means the existing inset passes through unchanged.</summary>
    [Fact]
    public void BoxMetrics_ResolveTitleVsSidePortContentInsetTop_NoSidePorts_ReturnsExistingInset()
    {
        // Arrange
        var theme = Themes.Light;

        // Act
        var inset = BoxMetrics.ResolveTitleVsSidePortContentInsetTop(
            theme,
            hasLeftOrRightPorts: false,
            hasLabel: true,
            hasKeyword: true,
            existingInsetTop: 5.0);

        // Assert
        Assert.Equal(5.0, inset);
    }

    /// <summary>No label and no keyword means there is no title to protect against.</summary>
    [Fact]
    public void BoxMetrics_ResolveTitleVsSidePortContentInsetTop_NoTitle_ReturnsExistingInset()
    {
        // Arrange
        var theme = Themes.Light;

        // Act
        var inset = BoxMetrics.ResolveTitleVsSidePortContentInsetTop(
            theme,
            hasLeftOrRightPorts: true,
            hasLabel: false,
            hasKeyword: false,
            existingInsetTop: 3.0);

        // Assert
        Assert.Equal(3.0, inset);
    }

    /// <summary>Left/right ports plus a title reserve at least the title-area height.</summary>
    [Fact]
    public void BoxMetrics_ResolveTitleVsSidePortContentInsetTop_SidePortsWithTitle_ReservesTitleAreaHeight()
    {
        // Arrange
        var theme = Themes.Light;
        var expectedTitleHeight = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true);

        // Act
        var inset = BoxMetrics.ResolveTitleVsSidePortContentInsetTop(
            theme,
            hasLeftOrRightPorts: true,
            hasLabel: true,
            hasKeyword: true,
            existingInsetTop: 0.0);

        // Assert
        Assert.Equal(expectedTitleHeight, inset);
    }

    /// <summary>A larger pre-existing inset (e.g. from a compartment header) is preserved.</summary>
    [Fact]
    public void BoxMetrics_ResolveTitleVsSidePortContentInsetTop_ExistingInsetLarger_KeepsExistingInset()
    {
        // Arrange
        var theme = Themes.Light;
        var titleHeight = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: false);
        var largerExisting = titleHeight + 100.0;

        // Act
        var inset = BoxMetrics.ResolveTitleVsSidePortContentInsetTop(
            theme,
            hasLeftOrRightPorts: true,
            hasLabel: true,
            hasKeyword: false,
            existingInsetTop: largerExisting);

        // Assert
        Assert.Equal(largerExisting, inset);
    }

    /// <summary>A null theme is rejected with an <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void BoxMetrics_ResolveTitleVsSidePortContentInsetTop_NullTheme_ThrowsArgumentNullException()
    {
        // Arrange
        Theme? theme = null;

        // Act
        void Act() => BoxMetrics.ResolveTitleVsSidePortContentInsetTop(theme!, true, true, true);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }
}
