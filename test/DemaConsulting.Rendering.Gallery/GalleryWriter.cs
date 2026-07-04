// <copyright file="GalleryWriter.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Text;

using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout;
using DemaConsulting.Rendering.Skia;
using DemaConsulting.Rendering.Svg;

using SkiaSharp;

namespace DemaConsulting.Rendering.Gallery;

/// <summary>
///     Lays out a <see cref="LayoutGraph"/>, renders it to the gallery output directory, and asserts the
///     produced file is a valid, non-empty image. This is the shared engine behind every showcase fact,
///     so each fact reduces to "describe a graph, name a file, pick a theme".
/// </summary>
internal static class GalleryWriter
{
    private static readonly SvgRenderer SvgRenderer = new();
    private static readonly PngRenderer PngRenderer = new();

    /// <summary>PNG file signature (the eight leading bytes of every PNG stream).</summary>
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    ///     Lays out <paramref name="graph"/> with whatever algorithm and options it declares (see
    ///     <see cref="LayoutEngine"/>), renders it to <paramref name="fileName"/> as SVG, and asserts the
    ///     result is a well-formed SVG document.
    /// </summary>
    /// <param name="fileName">Stable output filename (for example <c>layered-pipeline.svg</c>).</param>
    /// <param name="graph">
    /// The graph to lay out. Configure it directly (for example
    /// <c>graph.Set(CoreOptions.Algorithm, "layered")</c>) before calling this method.
    /// </param>
    /// <param name="theme">The theme to render with.</param>
    public static void Svg(string fileName, LayoutGraph graph, Theme theme)
    {
        var tree = LayoutEngine.Layout(graph);
        var path = Path.Combine(GalleryOutput.ResolveDirectory(), fileName);

        using (var stream = File.Create(path))
        {
            SvgRenderer.Render(tree, new RenderOptions(theme), stream);
        }

        AssertValidSvg(path);
    }

    /// <summary>
    ///     Lays out <paramref name="graph"/> with whatever algorithm and options it declares (see
    ///     <see cref="LayoutEngine"/>), renders it to <paramref name="fileName"/> as PNG, and asserts the
    ///     result decodes as a valid raster image.
    /// </summary>
    /// <param name="fileName">Stable output filename (for example <c>layered-pipeline.png</c>).</param>
    /// <param name="graph">
    /// The graph to lay out. Configure it directly (for example
    /// <c>graph.Set(CoreOptions.Algorithm, "layered")</c>) before calling this method.
    /// </param>
    /// <param name="theme">The theme to render with.</param>
    public static void Png(string fileName, LayoutGraph graph, Theme theme)
    {
        var tree = LayoutEngine.Layout(graph);
        var path = Path.Combine(GalleryOutput.ResolveDirectory(), fileName);

        using (var stream = File.Create(path))
        {
            PngRenderer.Render(tree, new RenderOptions(theme), stream);
        }

        AssertValidPng(path);
    }

    /// <summary>Asserts that the file exists and contains a well-formed, non-empty SVG document.</summary>
    /// <param name="path">Absolute path of the generated SVG file.</param>
    public static void AssertValidSvg(string path)
    {
        Assert.True(File.Exists(path), $"Expected SVG file to exist: {path}");
        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.False(string.IsNullOrWhiteSpace(content), $"SVG file is empty: {path}");
        Assert.Contains("<svg", content, StringComparison.Ordinal);
        Assert.Contains("</svg>", content, StringComparison.Ordinal);
    }

    /// <summary>Asserts that the file exists, starts with the PNG signature, and decodes to a bitmap.</summary>
    /// <param name="path">Absolute path of the generated PNG file.</param>
    public static void AssertValidPng(string path)
    {
        Assert.True(File.Exists(path), $"Expected PNG file to exist: {path}");
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > PngSignature.Length, $"PNG file is too small: {path}");
        Assert.True(
            bytes.Take(PngSignature.Length).SequenceEqual(PngSignature),
            $"File does not start with the PNG signature: {path}");

        using var bitmap = SKBitmap.Decode(path);
        Assert.NotNull(bitmap);
        Assert.True(bitmap.Width > 0 && bitmap.Height > 0, $"Decoded PNG has no pixels: {path}");
    }
}
