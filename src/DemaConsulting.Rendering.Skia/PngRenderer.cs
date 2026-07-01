// <copyright file="PngRenderer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using SkiaSharp;

namespace DemaConsulting.Rendering.Skia;

/// <summary>
/// Renders a <see cref="LayoutTree"/> to a lossless PNG image using SkiaSharp.
/// </summary>
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
