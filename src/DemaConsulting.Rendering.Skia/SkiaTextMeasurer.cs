// <copyright file="SkiaTextMeasurer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using SkiaSharp;

namespace DemaConsulting.Rendering.Skia;

/// <summary>
/// SkiaSharp-backed <see cref="ITextMeasurer"/> implementation, using the same embedded Noto Sans
/// typefaces (see <see cref="SkiaTypefaces"/>) and font-metric measurement
/// (<c>SKFont.MeasureText(string)</c>) that <see cref="SkiaRasterRenderer"/> uses to draw text, so a layout algorithm configured with
/// this measurer reserves pixel-accurate margins that match what a Skia-backed renderer will
/// actually draw.
/// </summary>
/// <remarks>
/// Set an instance of this class on <see cref="CoreOptions.TextMeasurer"/> to replace the bundled
/// layout engine's dependency-free heuristic estimator with real font metrics.
/// </remarks>
public sealed class SkiaTextMeasurer : ITextMeasurer
{
    /// <inheritdoc/>
    public double MeasureWidth(string text, double fontSize, bool bold, bool italic)
    {
        ArgumentNullException.ThrowIfNull(text);

        using var font = new SKFont(SkiaTypefaces.Resolve(bold, italic), (float)fontSize);
        return font.MeasureText(text);
    }
}
