// <copyright file="SkiaPortAndContentInsetTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using SkiaSharp;

namespace DemaConsulting.Rendering.Skia.Tests;

/// <summary>
///     Tests for <see cref="SkiaRasterRenderer"/>'s port-glyph/label rendering and its use of
///     <see cref="LayoutBox.ContentInsetLeft"/>/Right/Top/Bottom when placing title and compartment
///     content.
/// </summary>
public sealed class SkiaPortAndContentInsetTests
{
    /// <summary>
    ///     Proves that rendering a single <see cref="LayoutPort"/> with a label produces a decodable,
    ///     non-empty bitmap containing non-background pixels (the port glyph and its label), for
    ///     each of the four <see cref="PortSide"/> values.
    /// </summary>
    [Theory]
    [InlineData(PortSide.Left)]
    [InlineData(PortSide.Right)]
    [InlineData(PortSide.Top)]
    [InlineData(PortSide.Bottom)]
    public void PngRenderer_RenderPort_AnySide_ProducesNonBackgroundPixels(PortSide side)
    {
        // Arrange
        var renderer = new PngRenderer();
        var port = new LayoutPort(100, 50, side, "label");
        var layout = new LayoutTree(200, 100, [port]);
        var options = new RenderOptions(Themes.Light);
        using var stream = new MemoryStream();

        // Act
        renderer.Render(layout, options, stream);

        // Assert: a decodable bitmap with at least one non-background pixel exists.
        stream.Position = 0;
        using var data = SKData.Create(stream);
        using var bitmap = SKBitmap.Decode(data);
        Assert.NotNull(bitmap);

        var background = SKColor.Parse(Themes.Light.BackgroundColor);
        var hasForeground = false;
        for (var y = 0; y < bitmap.Height && !hasForeground; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) != background)
                {
                    hasForeground = true;
                    break;
                }
            }
        }

        Assert.True(hasForeground);
    }

    /// <summary>
    ///     Proves that a box's non-zero <see cref="LayoutBox.ContentInsetLeft"/> shifts the leftmost
    ///     drawn compartment-row pixel further right than an otherwise-identical box with no content
    ///     insets, confirming the renderer reads the inset when placing compartment text.
    /// </summary>
    [Fact]
    public void PngRenderer_RenderBoxCompartments_ContentInsetLeft_ShiftsRowContentRight()
    {
        // Arrange: two identical boxes with a compartment row, one with a left content inset.
        var renderer = new PngRenderer();
        var compartments = new[] { new LayoutCompartment(null, ["row text"]) };
        var plain = new LayoutBox(0, 0, 200, 100, "Title", 0, BoxShape.Rectangle, compartments, []);
        var inset = plain with { ContentInsetLeft = 60.0 };
        var options = new RenderOptions(Themes.Light);
        var background = SKColor.Parse(Themes.Light.BackgroundColor);

        using var plainStream = new MemoryStream();
        renderer.Render(new LayoutTree(200, 100, [plain]), options, plainStream);
        plainStream.Position = 0;
        using var plainData = SKData.Create(plainStream);
        using var plainBitmap = SKBitmap.Decode(plainData);

        using var insetStream = new MemoryStream();
        renderer.Render(new LayoutTree(200, 100, [inset]), options, insetStream);
        insetStream.Position = 0;
        using var insetData = SKData.Create(insetStream);
        using var insetBitmap = SKBitmap.Decode(insetData);

        // Restrict the scan to a horizontal band well below the title/divider and away from the
        // box's own left border stroke, so only the compartment row's own text pixels are found.
        var plainLeftmost = LeftmostForegroundX(plainBitmap, background, yStart: 35, yEnd: 44, xStart: 2);
        var insetLeftmost = LeftmostForegroundX(insetBitmap, background, yStart: 35, yEnd: 44, xStart: 2);

        // Assert: the inset box's leftmost non-background content pixel is further right.
        Assert.True(plainLeftmost >= 0);
        Assert.True(insetLeftmost >= 0);
        Assert.True(insetLeftmost > plainLeftmost);
    }

    /// <summary>Finds the x-coordinate of the leftmost non-background pixel within a Y band and X start.</summary>
    private static int LeftmostForegroundX(SKBitmap bitmap, SKColor background, int yStart, int yEnd, int xStart)
    {
        for (var x = xStart; x < bitmap.Width; x++)
        {
            for (var y = yStart; y < yEnd; y++)
            {
                if (bitmap.GetPixel(x, y) != background)
                {
                    return x;
                }
            }
        }

        return -1;
    }
}
