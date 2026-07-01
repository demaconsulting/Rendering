// <copyright file="WebpRenderer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using SkiaSharp;

namespace DemaConsulting.Rendering.Skia;

/// <summary>
/// Renders a <see cref="LayoutTree"/> to a WEBP image using SkiaSharp at a high quality setting.
/// </summary>
public sealed class WebpRenderer : SkiaRasterRenderer
{
    /// <inheritdoc/>
    protected override SKEncodedImageFormat EncodedFormat => SKEncodedImageFormat.Webp;

    /// <inheritdoc/>
    protected override int EncodingQuality => 90;

    /// <inheritdoc/>
    public override string MediaType => "image/webp";

    /// <inheritdoc/>
    public override string DefaultExtension => ".webp";

    /// <inheritdoc/>
    public override IReadOnlyList<string> FileExtensions => [".webp"];
}
