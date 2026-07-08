// <copyright file="PortLabelWidthEstimatorTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Abstractions.Tests;

/// <summary>
///     Tests for the dependency-free <see cref="PortLabelWidthEstimator"/> heuristic shared by
///     <c>LayeredLayoutAlgorithm</c> (layout-time port-label inset/<c>MaxLabelWidth</c> sizing) and
///     <c>SvgRenderer</c> (render-time port-label <c>textLength</c> squeeze decision).
/// </summary>
public sealed class PortLabelWidthEstimatorTests
{
    /// <summary>
    ///     Proves that measured width scales linearly with font size, confirming the
    ///     <c>fontSize / 100.0</c> scaling basis.
    /// </summary>
    [Fact]
    public void MeasureWidth_ScalesLinearlyWithFontSize()
    {
        var small = PortLabelWidthEstimator.MeasureWidth("sample text", 10.0);
        var large = PortLabelWidthEstimator.MeasureWidth("sample text", 30.0);

        Assert.Equal(3.0, large / small, precision: 6);
    }

    /// <summary>
    ///     Proves that a longer string measures wider than a shorter one at the same font size.
    /// </summary>
    [Fact]
    public void MeasureWidth_LongerText_MeasuresWider()
    {
        var shortWidth = PortLabelWidthEstimator.MeasureWidth("hi", 12.0);
        var longWidth = PortLabelWidthEstimator.MeasureWidth("hello world", 12.0);

        Assert.True(longWidth > shortWidth);
    }

    /// <summary>
    ///     Proves that an empty string measures to zero width.
    /// </summary>
    [Fact]
    public void MeasureWidth_EmptyString_ReturnsZero()
    {
        Assert.Equal(0.0, PortLabelWidthEstimator.MeasureWidth(string.Empty, 12.0));
    }

    /// <summary>
    ///     Proves that a null text argument is rejected.
    /// </summary>
    [Fact]
    public void MeasureWidth_NullText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PortLabelWidthEstimator.MeasureWidth(null!, 12.0));
    }

    /// <summary>
    ///     Proves that a single mapped character measures to exactly its table entry at
    ///     <c>fontSize = 100.0</c>, pinning the table's scale basis.
    /// </summary>
    [Fact]
    public void MeasureWidth_KnownCharacter_MatchesMappedWidth()
    {
        Assert.Equal(87.0, PortLabelWidthEstimator.MeasureWidth("M", 100.0));
    }

    /// <summary>
    ///     Proves that a character outside the mapped table falls back to the median width.
    /// </summary>
    [Fact]
    public void MeasureWidth_UnmappedCharacter_UsesMedianFallback()
    {
        Assert.Equal(55.0, PortLabelWidthEstimator.MeasureWidth("€", 100.0));
    }
}
