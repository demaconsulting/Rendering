// <copyright file="PngRendererTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.CanvasNet;

namespace DemaConsulting.Rendering.CanvasNet.Tests;

/// <summary>
///     Smoke tests for <see cref="PngRenderer"/>, proving a placed layout tree renders to a PNG image.
/// </summary>
public class PngRendererTests
{
    /// <summary>
    ///     Proves that rendering a simple box produces a byte stream with the PNG file signature.
    /// </summary>
    [Fact]
    public void Render_SingleBox_ProducesPngSignature()
    {
        // Arrange
        var tree = new LayoutTree(100, 60, new LayoutNode[]
        {
            new LayoutBox(10, 10, 80, 40, "Box", 0, BoxShape.Rectangle, [], []),
        });
        var renderer = new PngRenderer();
        using var stream = new MemoryStream();

        // Act
        renderer.Render(tree, new RenderOptions(Themes.Light), stream);

        // Assert
        var bytes = stream.ToArray();
        Assert.True(bytes.Length > 8);

        var signature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Assert.Equal(signature, bytes[..8]);
        Assert.Equal("image/png", renderer.MediaType);
    }
}
