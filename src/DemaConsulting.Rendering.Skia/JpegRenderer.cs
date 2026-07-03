// <copyright file="JpegRenderer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using SkiaSharp;

namespace DemaConsulting.Rendering.Skia;

/// <summary>
/// Renders a <see cref="LayoutTree"/> to a JPEG image using SkiaSharp. JPEG is a lossy format with
/// no transparency; the renderer draws on the opaque theme background color shared by all raster
/// renderers.
/// </summary>
public sealed class JpegRenderer : SkiaRasterRenderer
{
    /// <inheritdoc/>
    protected override SKEncodedImageFormat EncodedFormat => SKEncodedImageFormat.Jpeg;

    /// <inheritdoc/>
    protected override int EncodingQuality => 90;

    /// <inheritdoc/>
    public override string MediaType => "image/jpeg";

    /// <inheritdoc/>
    public override string DefaultExtension => ".jpg";

    /// <inheritdoc/>
    public override IReadOnlyList<string> FileExtensions => [".jpg", ".jpeg"];
}
