// <copyright file="ITextMeasurer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// Measures the horizontal advance width of a run of text at a given font size and style, using
/// the library's one hardcoded font family (Noto Sans). Set on <see cref="CoreOptions.TextMeasurer"/>
/// so a layout algorithm can reserve pixel-accurate margins (for example
/// <see cref="LayoutBox.ContentInsetLeft"/>/<see cref="LayoutBox.ContentInsetRight"/>) for port
/// label text before any renderer runs.
/// </summary>
/// <remarks>
/// This is a layout-time measurement contract, not a rendering contract: it exists so a layout
/// algorithm can ask "how wide would this text be?" without depending on a concrete renderer.
/// The bundled <c>layered</c> algorithm falls back to a dependency-free heuristic estimator when
/// no <see cref="ITextMeasurer"/> is configured; a renderer package (for example
/// <c>DemaConsulting.Rendering.Skia</c>) may supply a real font-metric-backed implementation for
/// pixel-accurate results that match what it will actually draw.
/// </remarks>
public interface ITextMeasurer
{
    /// <summary>
    /// Measures the horizontal advance width, in logical pixels, of <paramref name="text"/> rendered
    /// at <paramref name="fontSize"/> with the requested style.
    /// </summary>
    /// <param name="text">The text to measure. An empty string measures as zero.</param>
    /// <param name="fontSize">The font size, in logical pixels, to measure at. Must be positive.</param>
    /// <param name="bold">Whether to measure the bold variant of the font.</param>
    /// <param name="italic">Whether to measure the italic variant of the font.</param>
    /// <returns>The estimated or measured advance width, in logical pixels.</returns>
    double MeasureWidth(string text, double fontSize, bool bold, bool italic);
}
