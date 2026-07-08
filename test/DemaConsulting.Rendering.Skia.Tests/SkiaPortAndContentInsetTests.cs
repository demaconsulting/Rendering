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

    /// <summary>Finds the x-coordinate of the rightmost non-background pixel within a Y band and X range.</summary>
    private static int RightmostForegroundX(SKBitmap bitmap, SKColor background, int yStart, int yEnd, int xStart = 0, int? xEnd = null)
    {
        var effectiveXEnd = xEnd ?? bitmap.Width - 1;
        for (var x = effectiveXEnd; x >= xStart; x--)
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

    /// <summary>
    ///     Proves that when 3+ parallel labeled connectors force the label placer to nudge labels
    ///     downward to avoid collisions, the raster renderer grows the bitmap (rather than sizing it
    ///     from the pre-label-placement box/routing geometry alone) so every label stays fully within
    ///     the final bitmap bounds — none are clipped off-canvas. A raster bitmap cannot grow once
    ///     allocated, so this proves the bitmap is sized only after label placement is resolved.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_ManyCollidingConnectorLabels_BitmapGrowsToFitAllLabels()
    {
        // Arrange: three horizontal lines close enough together (4px apart) that their preferred
        // midpoint labels collide and must be nudged apart, using a deliberately small declared
        // canvas height (60) that is too small to fit the nudged labels without growing.
        var renderer = new PngRenderer();
        var lineA = new LayoutLine([new Point2D(0, 20), new Point2D(200, 20)], EndMarkerStyle.None, EndMarkerStyle.FilledArrow, LineStyle.Solid, "primary");
        var lineB = new LayoutLine([new Point2D(0, 24), new Point2D(200, 24)], EndMarkerStyle.None, EndMarkerStyle.FilledArrow, LineStyle.Solid, "retry");
        var lineC = new LayoutLine([new Point2D(0, 28), new Point2D(200, 28)], EndMarkerStyle.None, EndMarkerStyle.FilledArrow, LineStyle.Solid, "audit");
        var layout = new LayoutTree(220, 60, [lineA, lineB, lineC]);
        var options = new RenderOptions(Themes.Light);
        using var stream = new MemoryStream();

        // Act
        renderer.Render(layout, options, stream);

        // Assert: the bitmap grew past the declared 60px height to accommodate the stacked labels,
        // and there is foreground content (a label) drawn all the way down near the bottom of the
        // grown bitmap (i.e. it was not clipped at the old, smaller height).
        stream.Position = 0;
        using var data = SKData.Create(stream);
        using var bitmap = SKBitmap.Decode(data);
        Assert.NotNull(bitmap);
        Assert.True(bitmap!.Height > 60, $"Expected bitmap height to grow past 60, was {bitmap.Height}.");

        var background = SKColor.Parse(Themes.Light.BackgroundColor);
        var hasContentNearBottom = false;
        for (var y = bitmap.Height - 15; y < bitmap.Height && !hasContentNearBottom; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) != background)
                {
                    hasContentNearBottom = true;
                    break;
                }
            }
        }

        Assert.True(hasContentNearBottom, "Expected label content to reach near the grown bitmap's bottom edge.");
    }

    /// <summary>
    ///     Proves that a box title centers on the inset-adjusted content area, not the full box
    ///     width: when <see cref="LayoutBox.ContentInsetLeft"/> exceeds
    ///     <see cref="LayoutBox.ContentInsetRight"/>, the title's drawn pixels shift right of true
    ///     box-center (toward the smaller/un-inset side).
    /// </summary>
    [Fact]
    public void PngRenderer_RenderBoxTitle_AsymmetricContentInsets_ShiftsTitleOffBoxCenter()
    {
        // Arrange: two boxes, one with a symmetric (zero) inset and one with a large left inset only,
        // both otherwise identical.
        var renderer = new PngRenderer();
        var plain = new LayoutBox(0, 0, 200, 100, "Hub", 0, BoxShape.Rectangle, [], []);
        var inset = plain with { ContentInsetLeft = 60.0, ContentInsetRight = 0.0 };
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

        // Restrict the scan to the title's vertical band (near the top of the box).
        var plainLeftmost = LeftmostForegroundX(plainBitmap!, background, yStart: 2, yEnd: 25, xStart: 2);
        var insetLeftmost = LeftmostForegroundX(insetBitmap!, background, yStart: 2, yEnd: 25, xStart: 2);

        Assert.True(plainLeftmost >= 0);
        Assert.True(insetLeftmost >= 0);
        Assert.True(insetLeftmost > plainLeftmost, $"Expected inset title to start further right ({insetLeftmost}) than plain title ({plainLeftmost}).");
    }

    /// <summary>
    ///     Proves that a long port label bounded by a finite <see cref="LayoutPort.MaxLabelWidth"/> is
    ///     squeezed to fit rather than rendering at its full natural width: rendering the same long
    ///     left-port label unconstrained (default <see cref="double.PositiveInfinity"/>) reaches
    ///     substantially further right than rendering it bounded to roughly half the box's inner
    ///     width, proving the bound actually constrains the drawn glyph width.
    /// </summary>
    [Fact]
    public void PngRenderer_RenderPort_LongLabelWithMaxLabelWidth_SqueezesToFit()
    {
        // Arrange: a narrow box with a long left-port label, once unconstrained (default) and once
        // bounded to roughly half the box's inner width — mirroring the layout algorithm's own
        // "half box width" policy for MaxLabelWidth.
        var renderer = new PngRenderer();
        const double boxWidth = 300.0;
        const string longLabel = "a rather long incoming data label";
        var unconstrainedPort = new LayoutPort(10, 50, PortSide.Left, longLabel);
        var constrainedPort = new LayoutPort(10, 50, PortSide.Left, longLabel, MaxLabelWidth: (boxWidth / 2.0) - 4.0);
        var options = new RenderOptions(Themes.Light);
        var background = SKColor.Parse(Themes.Light.BackgroundColor);

        using var unconstrainedStream = new MemoryStream();
        renderer.Render(new LayoutTree(boxWidth, 100, [unconstrainedPort]), options, unconstrainedStream);
        unconstrainedStream.Position = 0;
        using var unconstrainedData = SKData.Create(unconstrainedStream);
        using var unconstrainedBitmap = SKBitmap.Decode(unconstrainedData);

        using var constrainedStream = new MemoryStream();
        renderer.Render(new LayoutTree(boxWidth, 100, [constrainedPort]), options, constrainedStream);
        constrainedStream.Position = 0;
        using var constrainedData = SKData.Create(constrainedStream);
        using var constrainedBitmap = SKBitmap.Decode(constrainedData);

        // Act
        var unconstrainedRightmost = RightmostForegroundX(unconstrainedBitmap!, background, yStart: 40, yEnd: 60);
        var constrainedRightmost = RightmostForegroundX(constrainedBitmap!, background, yStart: 40, yEnd: 60);

        // Assert: the constrained label's rightmost drawn pixel is significantly further left than
        // the unconstrained label's, proving the squeeze actually reduced the rendered width.
        Assert.True(unconstrainedRightmost >= 0);
        Assert.True(constrainedRightmost >= 0);
        Assert.True(
            constrainedRightmost < unconstrainedRightmost,
            $"Expected the MaxLabelWidth-constrained label's rightmost pixel ({constrainedRightmost}) " +
            $"to be left of the unconstrained label's ({unconstrainedRightmost}).");
    }
}
