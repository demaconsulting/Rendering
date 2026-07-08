// <copyright file="HeuristicTextMeasurer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Layout;

/// <summary>
/// Dependency-free fallback <see cref="ITextMeasurer"/> used by <see cref="LayeredLayoutAlgorithm"/>
/// when <see cref="CoreOptions.TextMeasurer"/> is unset: estimates advance width as a small
/// average-advance-width-per-character approximation, with no font metrics involved.
/// </summary>
/// <remarks>
/// <see cref="GlyphWidthFactor"/> is the same <c>0.6</c> magic number already hardcoded in both
/// bundled renderers' text-fit helpers (<c>SvgRenderer.FitTextLength</c> and
/// <c>SkiaRasterRenderer.FitFontSize</c>'s equivalent estimate), so this heuristic's estimate is
/// consistent with what those renderers already assume when they shrink-to-fit text — it is not a
/// new, independently-invented estimate.
/// </remarks>
internal sealed class HeuristicTextMeasurer : ITextMeasurer
{
    /// <summary>
    /// Rough average glyph-width factor (fraction of font size) shared with the renderers' own
    /// text-fit helpers.
    /// </summary>
    private const double GlyphWidthFactor = 0.6;

    /// <inheritdoc/>
    /// <remarks>
    /// <paramref name="bold"/> and <paramref name="italic"/> are accepted per the interface but do
    /// not change the estimate: this heuristic is a plain average-advance-width-per-character
    /// approximation with no font metrics to differentiate style weight/slant.
    /// </remarks>
    public double MeasureWidth(string text, double fontSize, bool bold, bool italic) =>
        text.Length * fontSize * GlyphWidthFactor;
}
