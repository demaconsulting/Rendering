// <copyright file="PortLabelWidthEstimator.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Abstractions;

/// <summary>
/// Dependency-free heuristic used to estimate the horizontal advance width of a port label,
/// shared by <c>Rendering.Layout</c>'s <c>LayeredLayoutAlgorithm</c> (which uses it at layout
/// time to size a node's <c>ContentInsetLeft</c>/Right/Top/Bottom reserved margins and its
/// <c>MaxLabelWidth</c> floor) and <c>Rendering.Svg</c>'s <c>SvgRenderer</c> (which uses it at
/// render time to decide whether a port label needs a <c>textLength</c> squeeze constraint).
/// </summary>
/// <remarks>
/// Promoting this estimator to <c>Rendering.Abstractions</c> — alongside <see cref="NotationMetrics"/>,
/// <see cref="BoxMetrics"/>, and <see cref="ConnectorLabelPlacer"/> — ensures both consumers agree on
/// what "natural width" means for a given port label, so the layout engine's sizing decision and the
/// renderer's squeeze decision can never disagree for the same label. This is intentionally still a
/// "good enough to avoid colliding with box content" heuristic, not an extension point: no label
/// anywhere in this codebase wraps, truncates, or measures exactly. <see cref="NotoSansRelativeWidths"/>
/// approximates each character's advance width, in logical pixels, at a nominal 100px font size,
/// modeled on Noto Sans (the one font family the bundled renderers hardcode) — the values are
/// reasonable per-character approximations, not exact font-metric measurements, and are not
/// guaranteed to match any specific Noto Sans version pixel-for-pixel.
/// </remarks>
public static class PortLabelWidthEstimator
{
    /// <summary>
    /// Approximate relative advance width, in logical pixels, of each mapped character at a nominal
    /// 100px font size, modeled on Noto Sans. Characters not present here fall back to
    /// <see cref="MedianWidth"/>.
    /// </summary>
    private static readonly IReadOnlyDictionary<char, double> NotoSansRelativeWidths = new Dictionary<char, double>
    {
        // Space and digits (Noto Sans digits are near-uniform width).
        [' '] = 28,
        ['0'] = 60,
        ['1'] = 60,
        ['2'] = 60,
        ['3'] = 60,
        ['4'] = 60,
        ['5'] = 60,
        ['6'] = 60,
        ['7'] = 60,
        ['8'] = 60,
        ['9'] = 60,

        // Uppercase Latin letters.
        ['A'] = 72,
        ['B'] = 67,
        ['C'] = 72,
        ['D'] = 74,
        ['E'] = 62,
        ['F'] = 57,
        ['G'] = 76,
        ['H'] = 73,
        ['I'] = 31,
        ['J'] = 47,
        ['K'] = 69,
        ['L'] = 58,
        ['M'] = 87,
        ['N'] = 73,
        ['O'] = 76,
        ['P'] = 65,
        ['Q'] = 76,
        ['R'] = 68,
        ['S'] = 65,
        ['T'] = 61,
        ['U'] = 71,
        ['V'] = 68,
        ['W'] = 94,
        ['X'] = 66,
        ['Y'] = 65,
        ['Z'] = 61,

        // Lowercase Latin letters.
        ['a'] = 55,
        ['b'] = 60,
        ['c'] = 52,
        ['d'] = 60,
        ['e'] = 56,
        ['f'] = 33,
        ['g'] = 60,
        ['h'] = 60,
        ['i'] = 26,
        ['j'] = 26,
        ['k'] = 53,
        ['l'] = 26,
        ['m'] = 87,
        ['n'] = 60,
        ['o'] = 60,
        ['p'] = 60,
        ['q'] = 60,
        ['r'] = 39,
        ['s'] = 48,
        ['t'] = 34,
        ['u'] = 60,
        ['v'] = 53,
        ['w'] = 78,
        ['x'] = 53,
        ['y'] = 53,
        ['z'] = 48,

        // Common punctuation and symbols.
        ['.'] = 25,
        [','] = 25,
        ['!'] = 28,
        ['?'] = 52,
        [':'] = 25,
        [';'] = 25,
        ['\''] = 20,
        ['"'] = 34,
        ['('] = 33,
        [')'] = 33,
        ['-'] = 33,
        ['_'] = 50,
        ['/'] = 28,
        ['\\'] = 28,
        ['+'] = 56,
        ['='] = 56,
        ['*'] = 40,
        ['%'] = 89,
        ['&'] = 72,
        ['@'] = 92,
        ['#'] = 60,
        ['$'] = 60,
        ['^'] = 47,
        ['~'] = 56,
        ['<'] = 56,
        ['>'] = 56,
        ['['] = 33,
        [']'] = 33,
        ['{'] = 36,
        ['}'] = 36,
        ['|'] = 26,
    };

    /// <summary>
    /// Fallback advance width, in logical pixels at a nominal 100px font size, for any character
    /// not present in <see cref="NotoSansRelativeWidths"/> — the approximate median width across
    /// the mapped table.
    /// </summary>
    private const double MedianWidth = 55.0;

    /// <summary>
    /// Estimates the horizontal advance width, in logical pixels, of <paramref name="text"/> at
    /// <paramref name="fontSize"/>, by summing each character's mapped (or median-fallback) relative
    /// width, scaled from the table's nominal 100px basis to the requested font size.
    /// </summary>
    /// <param name="text">The text to measure. An empty string measures as zero.</param>
    /// <param name="fontSize">The font size, in logical pixels, to measure at. Must be positive.</param>
    /// <returns>The estimated advance width, in logical pixels.</returns>
    public static double MeasureWidth(string text, double fontSize)
    {
        ArgumentNullException.ThrowIfNull(text);

        var scale = fontSize / 100.0;
        var total = 0.0;
        foreach (var c in text)
        {
            total += NotoSansRelativeWidths.GetValueOrDefault(c, MedianWidth);
        }

        return total * scale;
    }
}
