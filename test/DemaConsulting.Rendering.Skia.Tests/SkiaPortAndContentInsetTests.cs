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
    ///     Proves the boundary-port dual-label rule in the raster renderer: a left-side port carrying
    ///     both an external and an internal label draws foreground pixels on BOTH sides of the port
    ///     glyph (the external label outward/left, the internal label inward/right), whereas an
    ///     external-label-only port draws its single label inward only, leaving the outward side blank.
    ///     This guards the both-labels geometry the SVG unit test asserts, mirrored in the raster path.
    /// </summary>
    [Fact]
    public void PngRenderer_RenderPort_BothLabels_DrawsLabelsOnBothSidesOfPort()
    {
        // Arrange: identical left-side ports, one with both labels, one external-only.
        var renderer = new PngRenderer();
        var options = new RenderOptions(Themes.Light);
        const int portCentreX = 100;

        var bothLabels = RenderToBitmap(
            renderer, options, new LayoutPort(portCentreX, 50, PortSide.Left, ExternalLabel: "ext", InternalLabel: "int"));
        var externalOnly = RenderToBitmap(
            renderer, options, new LayoutPort(portCentreX, 50, PortSide.Left, ExternalLabel: "solo"));

        try
        {
            var background = SKColor.Parse(Themes.Light.BackgroundColor);

            // Regions strictly outside the port glyph: outward (left of the port) and inward (right).
            // The glyph itself spans a few pixels around the centre, so leave a margin either side.
            const int outwardMaxX = 88;   // columns 0..88 lie left of the glyph (outward face)
            const int inwardMinX = 112;   // columns 112.. lie right of the glyph (inward face)

            // Both-labels port: foreground appears on both the outward and inward sides.
            Assert.True(
                HasForegroundInColumnRange(bothLabels, background, 0, outwardMaxX),
                "both-labels port should draw its external label on the outward (left) side");
            Assert.True(
                HasForegroundInColumnRange(bothLabels, background, inwardMinX, bothLabels.Width - 1),
                "both-labels port should draw its internal label on the inward (right) side");

            // External-only port: nothing outward; the single label reads inward only.
            Assert.False(
                HasForegroundInColumnRange(externalOnly, background, 0, outwardMaxX),
                "external-only port must not draw anything on the outward (left) side");
            Assert.True(
                HasForegroundInColumnRange(externalOnly, background, inwardMinX, externalOnly.Width - 1),
                "external-only port should draw its label inward (right) exactly like a legacy port");
        }
        finally
        {
            bothLabels.Dispose();
            externalOnly.Dispose();
        }
    }

    /// <summary>Renders a single port on a 200x100 layout and decodes the PNG into a bitmap.</summary>
    private static SKBitmap RenderToBitmap(PngRenderer renderer, RenderOptions options, LayoutPort port)
    {
        using var stream = new MemoryStream();
        renderer.Render(new LayoutTree(200, 100, [port]), options, stream);
        stream.Position = 0;
        using var data = SKData.Create(stream);
        var bitmap = SKBitmap.Decode(data);
        Assert.NotNull(bitmap);
        return bitmap!;
    }

    /// <summary>Returns whether any non-background pixel exists in the inclusive column range [minX, maxX].</summary>
    private static bool HasForegroundInColumnRange(SKBitmap bitmap, SKColor background, int minX, int maxX)
    {
        var lo = Math.Max(0, minX);
        var hi = Math.Min(bitmap.Width - 1, maxX);
        for (var x = lo; x <= hi; x++)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                if (bitmap.GetPixel(x, y) != background)
                {
                    return true;
                }
            }
        }

        return false;
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
    ///     Proves that rendering a Note-shaped box with a compartment and no Label/Keyword does not
    ///     draw a stray divider line protruding past the note's folded-corner cutout. With no title
    ///     area, the first compartment's divider would land exactly on the box's own top edge; for a
    ///     plain rectangle that's an invisible duplicate, but for Note it must not extend past the
    ///     diagonal fold (from xFold to the right edge) as a visible artifact.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_NoteBoxWithCompartmentAndNoTitle_NoStrayLinePastFold()
    {
        // Arrange: a Note-shaped box with a compartment but no Label/Keyword
        var renderer = new PngRenderer();
        var compartment = new LayoutCompartment(null, ["Some body text"]);
        var box = new LayoutBox(10, 10, 150, 80, null, 0, BoxShape.Note, [compartment], []);
        var options = new RenderOptions(Themes.Light);
        var background = SKColor.Parse(Themes.Light.BackgroundColor);

        using var stream = new MemoryStream();
        renderer.Render(new LayoutTree(200, 120, [box]), options, stream);
        stream.Position = 0;
        using var data = SKData.Create(stream);
        using var bitmap = SKBitmap.Decode(data);

        // Compute the fold's scaled x-position (matching RenderNotePng's own geometry) and scan a
        // vertical band just below the box's top edge. The divider (a ~1.5px stroke) can land
        // partially on adjacent rows once anti-aliased, so a single row risked missing or flaking
        // on the regression; scanning several rows makes the check reliable. The diagonal fold edge
        // has a 1:1 slope (rise == run == fold size) starting at (xFold, yTop), so the scan's xStart
        // is offset past where that diagonal (plus its anti-aliasing) could reach within the band,
        // ensuring only a genuine stray horizontal divider - not the legitimate fold edge - would be
        // detected.
        var fold = Math.Min(Math.Min(box.Width, box.Height) * NotationMetrics.NoteFoldFraction, NotationMetrics.NoteFoldMaxSize);
        var scale = options.Scale;
        var xFold = (int)((box.X + box.Width - fold) * scale);
        var yTop = (int)(box.Y * scale);
        const int bandHeight = 4;
        const int diagonalClearance = 3;

        var rightmost = RightmostForegroundX(
            bitmap,
            background,
            yStart: yTop,
            yEnd: yTop + bandHeight,
            xStart: xFold + bandHeight + diagonalClearance);

        // Assert: no stray foreground pixel found past the fold at the top edge
        Assert.Equal(-1, rightmost);
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
    ///     Proves that a box title centers on the box's full geometric width, independent of any
    ///     asymmetric <see cref="LayoutBox.ContentInsetLeft"/>/<see cref="LayoutBox.ContentInsetRight"/>:
    ///     the title occupies its own row above any left/right port labels (which sit at the box's
    ///     vertical center), so its drawn pixels start at the same x-coordinate whether or not the box
    ///     declares asymmetric content insets.
    /// </summary>
    [Fact]
    public void PngRenderer_RenderBoxTitle_AsymmetricContentInsets_StaysAtGeometricCenter()
    {
        // Arrange: two boxes, one with a symmetric (zero) inset and one with a large left inset only,
        // both otherwise identical. Both carry a trivial nested child (tucked in a bottom corner well
        // outside the title's scan band) so they are non-leaf and keep the title top-pinned rather
        // than vertically centered, isolating this test's horizontal-position invariant from the
        // leaf-box title-centering behavior exercised elsewhere.
        var renderer = new PngRenderer();
        var nested = new LayoutBox(150, 90, 40, 8, null, 0, BoxShape.Rectangle, [], []);
        var plain = new LayoutBox(0, 0, 200, 100, "Hub", 0, BoxShape.Rectangle, [], [nested]);
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
        Assert.Equal(plainLeftmost, insetLeftmost);
    }

    /// <summary>
    ///     Proves that a port glyph's rendered square carries a contrasting outline distinct from its
    ///     fill: a pixel just inside the glyph's edge (within the outline's stroke band) differs from
    ///     the fill color sampled at the glyph's center, so the port glyph remains visually
    ///     distinguishable from a solid-filled arrowhead marker that may land on/near the same box
    ///     edge, rather than the two merging into an indistinguishable blob.
    /// </summary>
    [Fact]
    public void PngRenderer_RenderPort_Rect_HasStrokeDistinctFromFill()
    {
        // Arrange: render at a larger scale so the 1px (logical) outline is several pixels wide and
        // reliably sampled.
        var renderer = new PngRenderer();
        var port = new LayoutPort(50, 50, PortSide.Left, "in");
        var layout = new LayoutTree(100, 100, [port]);
        var options = new RenderOptions(Themes.Light) with { Scale = 8.0 };
        using var stream = new MemoryStream();

        // Act
        renderer.Render(layout, options, stream);

        // Assert: a pixel near the glyph's edge (inside the outline stroke band) differs from the
        // fill color sampled at the glyph's exact center, and matches the theme's background color.
        stream.Position = 0;
        using var data = SKData.Create(stream);
        using var bitmap = SKBitmap.Decode(data);
        Assert.NotNull(bitmap);

        var scale = options.Scale;
        var centreX = (int)(port.CentreX * scale);
        var centreY = (int)(port.CentreY * scale);
        var edgeX = (int)((port.CentreX - NotationMetrics.PortHalfSize) * scale) + 2;

        var fillPixel = bitmap!.GetPixel(centreX, centreY);
        var edgePixel = bitmap.GetPixel(edgeX, centreY);
        var strokeColor = SKColor.Parse(Themes.Light.StrokeColor);
        var backgroundColor = SKColor.Parse(Themes.Light.BackgroundColor);

        Assert.Equal(strokeColor, fillPixel);
        Assert.NotEqual(fillPixel, edgePixel);
        Assert.Equal(backgroundColor, edgePixel);
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
