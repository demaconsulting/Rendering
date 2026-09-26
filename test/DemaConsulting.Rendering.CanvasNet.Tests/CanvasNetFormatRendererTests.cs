// <copyright file="CanvasNetFormatRendererTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.CanvasNet;

namespace DemaConsulting.Rendering.CanvasNet.Tests;

/// <summary>
///     Tests for the CanvasNet raster renderers that share a rasterization core but emit different
///     image formats (<see cref="PngRenderer"/> and <see cref="JpegRenderer"/>).
/// </summary>
public class CanvasNetFormatRendererTests
{
    /// <summary>
    ///     Builds a simple one-box layout used by format smoke tests.
    /// </summary>
    /// <returns>A small sample layout tree.</returns>
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
        // Arrange
        var renderer = new JpegRenderer();
        using var stream = new MemoryStream();

        // Act
        renderer.Render(SampleTree(), new RenderOptions(Themes.Light), stream);

        // Assert
        var bytes = stream.ToArray();
        Assert.True(bytes.Length > 3);

        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
        Assert.Equal(0xFF, bytes[2]);
        Assert.Equal("image/jpeg", renderer.MediaType);
        Assert.Equal(".jpg", renderer.DefaultExtension);
        Assert.Contains(".jpeg", renderer.FileExtensions);
    }

    /// <summary>
    ///     Proves that the PNG renderer advertises a single .png extension that includes its default.
    /// </summary>
    [Fact]
    public void PngRenderer_FileExtensions_ContainsDefault()
    {
        // Arrange
        var renderer = new PngRenderer();

        // Act / Assert
        Assert.Contains(renderer.DefaultExtension, renderer.FileExtensions);
        Assert.Equal(".png", renderer.DefaultExtension);
    }
}
