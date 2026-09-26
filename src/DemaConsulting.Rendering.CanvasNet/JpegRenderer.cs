// <copyright file="JpegRenderer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.CanvasNet;

/// <summary>
/// Renders a <see cref="LayoutTree"/> to a JPEG image using CanvasNet. JPEG is a lossy format with
/// no transparency; the renderer draws on the opaque theme background color shared by all raster
/// renderers.
/// </summary>
public sealed class JpegRenderer : IRenderer
{
    /// <summary>JPEG encoding quality used to match the Skia renderer's public behavior.</summary>
    private const int EncodingQuality = 90;

    /// <summary>Shared JPEG encoder implementation holding the raster drawing logic.</summary>
    private static readonly JpegRendererCore Core = new();

    /// <inheritdoc/>
    public string MediaType => Core.MediaType;

    /// <inheritdoc/>
    public string DefaultExtension => Core.DefaultExtension;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions => Core.FileExtensions;

    /// <inheritdoc/>
    public void Render(LayoutTree layout, RenderOptions options, Stream output) => Core.Render(layout, options, output);

    /// <summary>Internal JPEG encoder implementation used by the public wrapper.</summary>
    private sealed class JpegRendererCore : CanvasNetRasterRenderer
    {
        /// <inheritdoc/>
        public override string MediaType => "image/jpeg";

        /// <inheritdoc/>
        public override string DefaultExtension => ".jpg";

        /// <inheritdoc/>
        public override IReadOnlyList<string> FileExtensions => [".jpg", ".jpeg"];

        /// <inheritdoc/>
        protected override void Save(Surface surface, Stream output)
        {
            ArgumentNullException.ThrowIfNull(surface);
            ArgumentNullException.ThrowIfNull(output);
            JpegCodec.Save(surface, output, EncodingQuality);
        }
    }
}
