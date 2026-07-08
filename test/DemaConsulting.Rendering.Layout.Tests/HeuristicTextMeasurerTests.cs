// <copyright file="HeuristicTextMeasurerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests for the dependency-free <see cref="HeuristicTextMeasurer"/> fallback used by
///     <see cref="LayeredLayoutAlgorithm"/> when no <see cref="CoreOptions.TextMeasurer"/> is set.
/// </summary>
public sealed class HeuristicTextMeasurerTests
{
    /// <summary>
    ///     Proves that the measured width scales linearly with text length: doubling the character
    ///     count exactly doubles the estimated width.
    /// </summary>
    [Fact]
    public void MeasureWidth_ScalesLinearlyWithTextLength()
    {
        var measurer = new HeuristicTextMeasurer();

        var short5 = measurer.MeasureWidth("abcde", 12.0, false, false);
        var long10 = measurer.MeasureWidth("abcdeabcde", 12.0, false, false);

        Assert.Equal(short5 * 2, long10, 6);
    }

    /// <summary>
    ///     Proves that the measured width scales linearly with font size, matching the documented
    ///     <c>text.Length * fontSize * 0.6</c> estimate shared with the renderers' own text-fit
    ///     helpers.
    /// </summary>
    [Fact]
    public void MeasureWidth_MatchesDocumentedGlyphWidthFactor()
    {
        var measurer = new HeuristicTextMeasurer();

        var width = measurer.MeasureWidth("abcd", 10.0, false, false);

        Assert.Equal(4 * 10.0 * 0.6, width, 6);
    }

    /// <summary>
    ///     Proves that an empty string measures to zero width.
    /// </summary>
    [Fact]
    public void MeasureWidth_EmptyString_ReturnsZero()
    {
        var measurer = new HeuristicTextMeasurer();

        Assert.Equal(0.0, measurer.MeasureWidth(string.Empty, 12.0, false, false));
    }

    /// <summary>
    ///     Proves that the <c>bold</c>/<c>italic</c> flags do not change the estimate, since this
    ///     heuristic has no font metrics to differentiate style weight or slant.
    /// </summary>
    [Fact]
    public void MeasureWidth_BoldItalicFlags_DoNotChangeEstimate()
    {
        var measurer = new HeuristicTextMeasurer();

        var plain = measurer.MeasureWidth("sample", 12.0, false, false);
        var boldItalic = measurer.MeasureWidth("sample", 12.0, true, true);

        Assert.Equal(plain, boldItalic);
    }
}
