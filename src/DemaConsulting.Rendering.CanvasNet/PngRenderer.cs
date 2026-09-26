// <copyright file="PngRenderer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.CanvasNet;

/// <summary>
/// Renders a <see cref="LayoutTree"/> to a lossless PNG image using CanvasNet.
/// </summary>
/// <remarks>
/// Raster output requires the <c>DemaConsulting.CanvasNet</c> package (a transitive dependency of
/// this package); no native handles are exposed and the caller owns the output stream.
/// </remarks>
/// <example>
/// Render a placed <see cref="LayoutTree"/> to a PNG file:
/// <code>
/// using System.IO;
/// using DemaConsulting.Rendering;
/// using DemaConsulting.Rendering.Abstractions;
/// using DemaConsulting.Rendering.CanvasNet;
///
/// // 'tree' is a placed LayoutTree produced by a layout algorithm (see DemaConsulting.Rendering.Layout).
/// using var output = File.Create("diagram.png");
/// new PngRenderer().Render(tree, new RenderOptions(Themes.Light), output);
/// </code>
/// </example>
public sealed class PngRenderer : IRenderer
{
    /// <summary>Shared PNG encoder implementation holding the raster drawing logic.</summary>
    private static readonly PngRendererCore Core = new();

    /// <inheritdoc/>
    public string MediaType => Core.MediaType;

    /// <inheritdoc/>
    public string DefaultExtension => Core.DefaultExtension;

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions => Core.FileExtensions;

    /// <inheritdoc/>
    public void Render(LayoutTree layout, RenderOptions options, Stream output) => Core.Render(layout, options, output);

    /// <summary>
    /// Draws the layout tree to an in-memory surface before PNG encoding so tests can sample the
    /// exact rendered pixels without a decode round-trip.
    /// </summary>
    /// <param name="layout">The layout tree to render.</param>
    /// <param name="options">Render options including theme and scale.</param>
    /// <returns>The rendered surface. The caller owns the returned surface and must dispose it.</returns>
    internal static Surface RenderToSurface(LayoutTree layout, RenderOptions options) => PngRendererCore.RenderToSurface(layout, options);

    /// <summary>Internal PNG encoder implementation used by the public wrapper.</summary>
    private sealed class PngRendererCore : CanvasNetRasterRenderer
    {
        /// <inheritdoc/>
        public override string MediaType => "image/png";

        /// <inheritdoc/>
        public override string DefaultExtension => ".png";

        /// <inheritdoc/>
        public override IReadOnlyList<string> FileExtensions => [".png"];

        /// <inheritdoc/>
        protected override void Save(Surface surface, Stream output)
        {
            ArgumentNullException.ThrowIfNull(surface);
            ArgumentNullException.ThrowIfNull(output);
            PngCodec.Save(surface, output);
        }
    }
}
