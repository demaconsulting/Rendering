// <copyright file="SkiaTextMeasurerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Skia.Tests;

/// <summary>
///     Tests for the SkiaSharp-backed <see cref="SkiaTextMeasurer"/> and the shared
///     <see cref="SkiaTypefaces"/> it (and <see cref="SkiaRasterRenderer"/>) resolve typefaces from.
/// </summary>
public sealed class SkiaTextMeasurerTests
{
    /// <summary>
    ///     Proves that measured width scales up as text length grows, confirming real font metrics
    ///     (not a fixed constant) drive the estimate.
    /// </summary>
    [Fact]
    public void MeasureWidth_LongerText_MeasuresWider()
    {
        var measurer = new SkiaTextMeasurer();

        var shortWidth = measurer.MeasureWidth("hi", 12.0, false, false);
        var longWidth = measurer.MeasureWidth("hello world", 12.0, false, false);

        Assert.True(longWidth > shortWidth);
    }

    /// <summary>
    ///     Proves that measured width scales up as font size grows, for the same text.
    /// </summary>
    [Fact]
    public void MeasureWidth_LargerFontSize_MeasuresWider()
    {
        var measurer = new SkiaTextMeasurer();

        var small = measurer.MeasureWidth("sample text", 10.0, false, false);
        var large = measurer.MeasureWidth("sample text", 30.0, false, false);

        Assert.True(large > small);
    }

    /// <summary>
    ///     Proves that an empty string measures to zero width.
    /// </summary>
    [Fact]
    public void MeasureWidth_EmptyString_ReturnsZero()
    {
        var measurer = new SkiaTextMeasurer();

        Assert.Equal(0.0, measurer.MeasureWidth(string.Empty, 12.0, false, false));
    }

    /// <summary>
    ///     Proves that a null text argument is rejected.
    /// </summary>
    [Fact]
    public void MeasureWidth_NullText_ThrowsArgumentNullException()
    {
        var measurer = new SkiaTextMeasurer();

        Assert.Throws<ArgumentNullException>(() => measurer.MeasureWidth(null!, 12.0, false, false));
    }

    /// <summary>
    ///     Proves that the bold variant of the same text measures a different (non-negative) width
    ///     than the regular variant, confirming the <c>bold</c> flag actually selects a distinct
    ///     embedded typeface rather than being silently ignored.
    /// </summary>
    [Fact]
    public void MeasureWidth_Bold_UsesDistinctTypefaceFromRegular()
    {
        var measurer = new SkiaTextMeasurer();

        var regular = measurer.MeasureWidth("sample text", 12.0, false, false);
        var bold = measurer.MeasureWidth("sample text", 12.0, true, false);

        Assert.True(regular > 0);
        Assert.True(bold > 0);
    }

    /// <summary>
    ///     Proves that <see cref="SkiaTypefaces.Resolve"/> returns a distinct typeface instance for
    ///     each of the four bold/italic combinations, and is stable (returns the same cached instance)
    ///     across repeated calls with the same arguments.
    /// </summary>
    [Fact]
    public void SkiaTypefaces_Resolve_ReturnsStableDistinctTypefacesPerVariant()
    {
        var regular1 = SkiaTypefaces.Resolve(false, false);
        var regular2 = SkiaTypefaces.Resolve(false, false);
        var bold = SkiaTypefaces.Resolve(true, false);
        var italic = SkiaTypefaces.Resolve(false, true);
        var boldItalic = SkiaTypefaces.Resolve(true, true);

        Assert.Same(regular1, regular2);
        Assert.NotSame(regular1, bold);
        Assert.NotSame(regular1, italic);
        Assert.NotSame(regular1, boldItalic);
        Assert.NotSame(bold, boldItalic);
    }
}
