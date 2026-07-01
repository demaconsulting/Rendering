// <copyright file="SkiaFormatRendererTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Text;

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Skia.Tests;

/// <summary>
///     Tests for the SkiaSharp raster renderers that share a rasterization core but emit different
///     image formats (<see cref="PngRenderer"/>, <see cref="JpegRenderer"/>, <see cref="WebpRenderer"/>).
/// </summary>
public class SkiaFormatRendererTests
{
    private static LayoutTree SampleTree() => new(100, 60, new LayoutNode[]
    {
        new LayoutBox(10, 10, 80, 40, "Box", 0, BoxShape.Rectangle, [], []),
    });

    /// <summary>
    ///     Proves that the JPEG renderer produces a byte stream with the JPEG SOI signature and
    ///     advertises the JPEG media type and extensions.
    /// </summary>
    [Fact]
    public void JpegRenderer_Render_ProducesJpegSignature()
    {
        var renderer = new JpegRenderer();
        using var stream = new MemoryStream();

        renderer.Render(SampleTree(), new RenderOptions(Themes.Light), stream);

        var bytes = stream.ToArray();
        Assert.True(bytes.Length > 3);

        // JPEG streams begin with the Start-Of-Image marker FF D8 FF.
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
        Assert.Equal(0xFF, bytes[2]);
        Assert.Equal("image/jpeg", renderer.MediaType);
        Assert.Equal(".jpg", renderer.DefaultExtension);
        Assert.Contains(".jpeg", renderer.FileExtensions);
    }

    /// <summary>
    ///     Proves that the WEBP renderer produces a byte stream with the RIFF/WEBP container header
    ///     and advertises the WEBP media type and extension.
    /// </summary>
    [Fact]
    public void WebpRenderer_Render_ProducesWebpContainerHeader()
    {
        var renderer = new WebpRenderer();
        using var stream = new MemoryStream();

        renderer.Render(SampleTree(), new RenderOptions(Themes.Light), stream);

        var bytes = stream.ToArray();
        Assert.True(bytes.Length > 12);

        // WEBP is a RIFF container: bytes 0-3 spell "RIFF" and bytes 8-11 spell "WEBP".
        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("WEBP", Encoding.ASCII.GetString(bytes, 8, 4));
        Assert.Equal("image/webp", renderer.MediaType);
        Assert.Equal(".webp", renderer.DefaultExtension);
    }

    /// <summary>
    ///     Proves that the PNG renderer advertises a single .png extension that includes its default.
    /// </summary>
    [Fact]
    public void PngRenderer_FileExtensions_ContainsDefault()
    {
        var renderer = new PngRenderer();

        Assert.Contains(renderer.DefaultExtension, renderer.FileExtensions);
        Assert.Equal(".png", renderer.DefaultExtension);
    }
}
