// <copyright file="PngRenderer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using SkiaSharp;

namespace DemaConsulting.Rendering.Skia;

/// <summary>
/// Renders a <see cref="LayoutTree"/> to a lossless PNG image using SkiaSharp.
/// </summary>
/// <remarks>
/// Raster output requires the <c>SkiaSharp</c> package (a transitive dependency of this package); no
/// native handles are exposed and the caller owns the output stream.
/// </remarks>
/// <example>
/// Render a placed <see cref="LayoutTree"/> to a PNG file:
/// <code>
/// using System.IO;
/// using DemaConsulting.Rendering;
/// using DemaConsulting.Rendering.Abstractions;
/// using DemaConsulting.Rendering.Skia;
///
/// // 'tree' is a placed LayoutTree produced by a layout algorithm (see DemaConsulting.Rendering.Layout).
/// using var output = File.Create("diagram.png");
/// new PngRenderer().Render(tree, new RenderOptions(Themes.Light), output);
/// </code>
/// </example>
public sealed class PngRenderer : SkiaRasterRenderer
{
    /// <inheritdoc/>
    protected override SKEncodedImageFormat EncodedFormat => SKEncodedImageFormat.Png;

    /// <inheritdoc/>
    public override string MediaType => "image/png";

    /// <inheritdoc/>
    public override string DefaultExtension => ".png";

    /// <inheritdoc/>
    public override IReadOnlyList<string> FileExtensions => [".png"];
}
