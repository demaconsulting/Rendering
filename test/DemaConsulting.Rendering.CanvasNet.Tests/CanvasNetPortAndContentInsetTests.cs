// <copyright file="CanvasNetPortAndContentInsetTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.CanvasNet;

namespace DemaConsulting.Rendering.CanvasNet.Tests;

/// <summary>
///     Tests for <see cref="CanvasNetRasterRenderer"/>'s port-glyph/label rendering and its use of
///     <see cref="LayoutBox.ContentInsetLeft"/>/Right/Top/Bottom when placing title and compartment
///     content.
/// </summary>
public sealed class CanvasNetPortAndContentInsetTests
{
    /// <summary>
    ///     Proves that rendering a single <see cref="LayoutPort"/> with a label produces a decodable,
    ///     non-empty bitmap containing non-background pixels for each of the four
    ///     <see cref="PortSide"/> values.
    /// </summary>
    /// <param name="side">Port side to render.</param>
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

        // Assert
        stream.Position = 0;
        using var surface = PngCodec.Load(stream);
        var background = Rgba32.Parse(Themes.Light.BackgroundColor);
        var hasForeground = false;
        for (var y = 0; y < surface.Height && !hasForeground; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                if (surface[x, y] != background)
                {
                    hasForeground = true;
                    break;
                }
            }
        }

        Assert.True(hasForeground);
    }

    /// <summary>
    ///     Proves that a Left/Right port label has real vertical clearance from its own port glyph
    ///     (and therefore the connector line passing through the port center): the label's topmost
    ///     foreground pixel must sit strictly below the bottom of the port glyph square
    ///     (<c>CentreY + PortHalfSize</c>). Pins this invariant so a regression to the old,
    ///     insufficient <c>CentreY + FontSizeBody / 2</c> offset — which let the label vertically
    ///     overlap the port glyph and its connector line — would be caught, rather than merely
    ///     checking the label is on the correct horizontal side.
    /// </summary>
    /// <param name="side">Port side to render.</param>
    [Theory]
    [InlineData(PortSide.Left)]
    [InlineData(PortSide.Right)]
    public void PngRenderer_RenderPort_LeftRightLabel_ClearsPortGlyphVertically(PortSide side)
    {
        // Arrange: a generously scaled render so the label's topmost pixel row is measured precisely.
        const double portCentreY = 50;
        var port = new LayoutPort(100, portCentreY, side, "label");
        var layout = new LayoutTree(200, 100, [port]);
        var options = new RenderOptions(Themes.Light) with { Scale = 4.0 };
        var background = Rgba32.Parse(Themes.Light.BackgroundColor);

        using var surface = RenderToSurface(layout, options);

        // Act: scan only the label's column range (beyond the port glyph square) for the topmost
        // foreground pixel row, so the port glyph itself is excluded from the measurement.
        var scale = options.Scale;
        var glyphMaxX = (int)((port.CentreX + NotationMetrics.PortHalfSize) * scale);
        var glyphMinX = (int)((port.CentreX - NotationMetrics.PortHalfSize) * scale);
        var (labelMinX, labelMaxX) = side == PortSide.Left
            ? (glyphMaxX + 1, surface.Width - 1)
            : (0, glyphMinX - 1);

        var topmostLabelY = -1;
        for (var y = 0; y < surface.Height && topmostLabelY < 0; y++)
        {
            for (var x = Math.Max(0, labelMinX); x <= Math.Min(surface.Width - 1, labelMaxX); x++)
            {
                if (surface[x, y] != background)
                {
                    topmostLabelY = y;
                    break;
                }
            }
        }

        // Assert
        Assert.True(topmostLabelY >= 0, "Expected to find foreground label pixels.");
        var portGlyphBottomY = (int)((portCentreY + NotationMetrics.PortHalfSize) * scale);
        Assert.True(
            topmostLabelY > portGlyphBottomY,
            $"Expected the label's topmost pixel row ({topmostLabelY}) to be below the port glyph's " +
            $"bottom edge ({portGlyphBottomY}), confirming real vertical clearance from the connector line.");
    }

    /// <summary>
    ///     Proves the boundary-port dual-label rule in the raster renderer: a left-side port carrying
    ///     both an external and an internal label draws foreground pixels on both sides of the port
    ///     glyph, whereas an external-label-only port draws its single label inward only.
    /// </summary>
    [Fact]
    public void PngRenderer_RenderPort_BothLabels_DrawsLabelsOnBothSidesOfPort()
    {
        // Arrange
        var options = new RenderOptions(Themes.Light);
        const int portCentreX = 100;

        using var bothLabels = RenderToSurface(
            new LayoutTree(
                200,
                100,
                [new LayoutPort(portCentreX, 50, PortSide.Left, ExternalLabel: "ext", InternalLabel: "int")]),
            options);
        using var externalOnly = RenderToSurface(
            new LayoutTree(
                200,
                100,
                [new LayoutPort(portCentreX, 50, PortSide.Left, ExternalLabel: "solo")]),
            options);

        // Act
        var background = Rgba32.Parse(Themes.Light.BackgroundColor);
        const int outwardMaxX = 88;
        const int inwardMinX = 112;

        // Assert
        Assert.True(
            HasForegroundInColumnRange(bothLabels, background, 0, outwardMaxX),
            "both-labels port should draw its external label on the outward (left) side");
        Assert.True(
            HasForegroundInColumnRange(bothLabels, background, inwardMinX, bothLabels.Width - 1),
            "both-labels port should draw its internal label on the inward (right) side");
        Assert.False(
            HasForegroundInColumnRange(externalOnly, background, 0, outwardMaxX),
            "external-only port must not draw anything on the outward (left) side");
        Assert.True(
            HasForegroundInColumnRange(externalOnly, background, inwardMinX, externalOnly.Width - 1),
            "external-only port should draw its label inward (right) exactly like a legacy port");
    }

    /// <summary>
    ///     Returns whether any non-background pixel exists in the inclusive column range.
    /// </summary>
    /// <param name="surface">Surface to scan.</param>
    /// <param name="background">Background color treated as empty.</param>
    /// <param name="minX">Minimum X, inclusive.</param>
    /// <param name="maxX">Maximum X, inclusive.</param>
    /// <returns><see langword="true"/> when any foreground pixel is found.</returns>
    private static bool HasForegroundInColumnRange(Surface surface, Rgba32 background, int minX, int maxX)
    {
        var lo = Math.Max(0, minX);
        var hi = Math.Min(surface.Width - 1, maxX);
        for (var x = lo; x <= hi; x++)
        {
            for (var y = 0; y < surface.Height; y++)
            {
                if (surface[x, y] != background)
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
    ///     insets.
    /// </summary>
    [Fact]
    public void PngRenderer_RenderBoxCompartments_ContentInsetLeft_ShiftsRowContentRight()
    {
        // Arrange
        var compartments = new[] { new LayoutCompartment(null, ["row text"]) };
        var plain = new LayoutBox(0, 0, 200, 100, "Title", 0, BoxShape.Rectangle, compartments, []);
        var inset = plain with { ContentInsetLeft = 60.0 };
        var options = new RenderOptions(Themes.Light);
        var background = Rgba32.Parse(Themes.Light.BackgroundColor);

        using var plainSurface = RenderToSurface(new LayoutTree(200, 100, [plain]), options);
        using var insetSurface = RenderToSurface(new LayoutTree(200, 100, [inset]), options);

        // Act
        var plainLeftmost = LeftmostForegroundX(plainSurface, background, yStart: 35, yEnd: 44, xStart: 2);
        var insetLeftmost = LeftmostForegroundX(insetSurface, background, yStart: 35, yEnd: 44, xStart: 2);

        // Assert
        Assert.True(plainLeftmost >= 0);
        Assert.True(insetLeftmost >= 0);
        Assert.True(insetLeftmost > plainLeftmost);
    }

    /// <summary>
    ///     Finds the X coordinate of the leftmost non-background pixel within a scan band.
    /// </summary>
    /// <param name="surface">Surface to scan.</param>
    /// <param name="background">Background color treated as empty.</param>
    /// <param name="yStart">Inclusive Y start.</param>
    /// <param name="yEnd">Exclusive Y end.</param>
    /// <param name="xStart">Inclusive X start.</param>
    /// <returns>The first foreground X coordinate, or -1 if none exists.</returns>
    private static int LeftmostForegroundX(Surface surface, Rgba32 background, int yStart, int yEnd, int xStart)
    {
        for (var x = xStart; x < surface.Width; x++)
        {
            for (var y = yStart; y < yEnd; y++)
            {
                if (surface[x, y] != background)
                {
                    return x;
                }
            }
        }

        return -1;
    }

    /// <summary>
    ///     Finds the X coordinate of the rightmost non-background pixel within a scan band.
    /// </summary>
    /// <param name="surface">Surface to scan.</param>
    /// <param name="background">Background color treated as empty.</param>
    /// <param name="yStart">Inclusive Y start.</param>
    /// <param name="yEnd">Exclusive Y end.</param>
    /// <param name="xStart">Inclusive X start.</param>
    /// <param name="xEnd">Inclusive X end, or null for the right edge.</param>
    /// <returns>The last foreground X coordinate, or -1 if none exists.</returns>
    private static int RightmostForegroundX(Surface surface, Rgba32 background, int yStart, int yEnd, int xStart = 0, int? xEnd = null)
    {
        var effectiveXEnd = xEnd ?? surface.Width - 1;
        for (var x = effectiveXEnd; x >= xStart; x--)
        {
            for (var y = yStart; y < yEnd; y++)
            {
                if (surface[x, y] != background)
                {
                    return x;
                }
            }
        }

        return -1;
    }

    /// <summary>
    ///     Proves that rendering a Note-shaped box with a compartment and no Label/Keyword does not
    ///     draw a stray divider line protruding past the note's folded-corner cutout.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_NoteBoxWithCompartmentAndNoTitle_NoStrayLinePastFold()
    {
        // Arrange
        var compartment = new LayoutCompartment(null, ["Some body text"]);
        var box = new LayoutBox(10, 10, 150, 80, null, 0, BoxShape.Note, [compartment], []);
        var options = new RenderOptions(Themes.Light);
        var background = Rgba32.Parse(Themes.Light.BackgroundColor);

        using var surface = RenderToSurface(new LayoutTree(200, 120, [box]), options);

        // Act
        var fold = Math.Min(Math.Min(box.Width, box.Height) * NotationMetrics.NoteFoldFraction, NotationMetrics.NoteFoldMaxSize);
        var scale = options.Scale;
        var xFold = (int)Math.Ceiling((box.X + box.Width - fold) * scale);
        var yTop = (int)(box.Y * scale);
        const int bandHeight = 4;
        const int diagonalClearance = 3;

        var rightmost = RightmostForegroundX(
            surface,
            background,
            yStart: yTop,
            yEnd: yTop + bandHeight,
            xStart: xFold + bandHeight + diagonalClearance);

        // Assert
        Assert.Equal(-1, rightmost);
    }

    /// <summary>
    ///     Proves that when a Note-shaped box has an empty leading compartment followed by a populated
    ///     compartment, the second compartment's divider is never drawn inside the folded-corner cutout.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_NoteBoxWithEmptyLeadingCompartment_NoDividerAboveFoldBottom()
    {
        // Arrange
        var emptyCompartment = new LayoutCompartment(null, []);
        var contentCompartment = new LayoutCompartment(null, ["Some body text"]);
        var box = new LayoutBox(10, 10, 150, 80, null, 0, BoxShape.Note, [emptyCompartment, contentCompartment], []);
        var options = new RenderOptions(Themes.Light);
        var background = Rgba32.Parse(Themes.Light.BackgroundColor);

        using var surface = RenderToSurface(new LayoutTree(200, 120, [box]), options);

        // Act
        var fold = BoxMetrics.NoteFoldSize(box);
        var scale = options.Scale;
        var midX = (int)((box.X + (box.Width / 2.0)) * scale);
        var yTop = (int)(box.Y * scale);
        var yFoldBottom = (int)((box.Y + fold) * scale);

        var strayDivider = false;
        for (var y = yTop + 1; y < yFoldBottom - 1; y++)
        {
            if (surface[midX, y] != background)
            {
                strayDivider = true;
                break;
            }
        }

        // Assert
        Assert.False(strayDivider);
    }

    /// <summary>
    ///     Proves that when three colliding connector labels are nudged downward to avoid collisions,
    ///     the raster renderer grows the bitmap so every label stays fully within the final bounds.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_ManyCollidingConnectorLabels_BitmapGrowsToFitAllLabels()
    {
        // Arrange
        var lineA = new LayoutLine([new Point2D(0, 20), new Point2D(200, 20)], EndMarkerStyle.None, EndMarkerStyle.FilledArrow, LineStyle.Solid, "primary");
        var lineB = new LayoutLine([new Point2D(0, 24), new Point2D(200, 24)], EndMarkerStyle.None, EndMarkerStyle.FilledArrow, LineStyle.Solid, "retry");
        var lineC = new LayoutLine([new Point2D(0, 28), new Point2D(200, 28)], EndMarkerStyle.None, EndMarkerStyle.FilledArrow, LineStyle.Solid, "audit");
        var layout = new LayoutTree(220, 60, [lineA, lineB, lineC]);
        var options = new RenderOptions(Themes.Light);

        // Act
        using var surface = RenderToSurface(layout, options);

        // Assert
        Assert.True(surface.Height > 60, $"Expected bitmap height to grow past 60, was {surface.Height}.");

        var background = Rgba32.Parse(Themes.Light.BackgroundColor);
        var hasContentNearBottom = false;
        for (var y = surface.Height - 15; y < surface.Height && !hasContentNearBottom; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                if (surface[x, y] != background)
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
    ///     asymmetric content insets.
    /// </summary>
    [Fact]
    public void PngRenderer_RenderBoxTitle_AsymmetricContentInsets_StaysAtGeometricCenter()
    {
        // Arrange
        var nested = new LayoutBox(150, 90, 40, 8, null, 0, BoxShape.Rectangle, [], []);
        var plain = new LayoutBox(0, 0, 200, 100, "Hub", 0, BoxShape.Rectangle, [], [nested]);
        var inset = plain with { ContentInsetLeft = 60.0, ContentInsetRight = 0.0 };
        var options = new RenderOptions(Themes.Light);
        var background = Rgba32.Parse(Themes.Light.BackgroundColor);

        using var plainSurface = RenderToSurface(new LayoutTree(200, 100, [plain]), options);
        using var insetSurface = RenderToSurface(new LayoutTree(200, 100, [inset]), options);

        // Act
        var plainLeftmost = LeftmostForegroundX(plainSurface, background, yStart: 2, yEnd: 25, xStart: 2);
        var insetLeftmost = LeftmostForegroundX(insetSurface, background, yStart: 2, yEnd: 25, xStart: 2);

        // Assert
        Assert.True(plainLeftmost >= 0);
        Assert.True(insetLeftmost >= 0);
        Assert.Equal(plainLeftmost, insetLeftmost);
    }

    /// <summary>
    ///     Proves that a port glyph's rendered square carries a contrasting outline distinct from its
    ///     fill.
    /// </summary>
    [Fact]
    public void PngRenderer_RenderPort_Rect_HasStrokeDistinctFromFill()
    {
        // Arrange
        var port = new LayoutPort(50, 50, PortSide.Left, "in");
        var layout = new LayoutTree(100, 100, [port]);
        var options = new RenderOptions(Themes.Light) with { Scale = 8.0 };

        // Act
        using var surface = RenderToSurface(layout, options);

        // Assert
        var scale = options.Scale;
        var centreX = (int)(port.CentreX * scale);
        var centreY = (int)(port.CentreY * scale);
        var edgeX = (int)((port.CentreX - NotationMetrics.PortHalfSize) * scale) + 2;

        var fillPixel = surface[centreX, centreY];
        var edgePixel = surface[edgeX, centreY];
        var strokeColor = Rgba32.Parse(Themes.Light.StrokeColor);
        var backgroundColor = Rgba32.Parse(Themes.Light.BackgroundColor);

        Assert.Equal(strokeColor, fillPixel);
        Assert.NotEqual(fillPixel, edgePixel);
        Assert.Equal(backgroundColor, edgePixel);
    }

    /// <summary>
    ///     Proves that a long port label bounded by a finite <see cref="LayoutPort.MaxLabelWidth"/> is
    ///     squeezed to fit rather than rendering at its full natural width.
    /// </summary>
    [Fact]
    public void PngRenderer_RenderPort_LongLabelWithMaxLabelWidth_SqueezesToFit()
    {
        // Arrange
        const double boxWidth = 300.0;
        const string longLabel = "a rather long incoming data label";
        var unconstrainedPort = new LayoutPort(10, 50, PortSide.Left, longLabel);
        var constrainedPort = new LayoutPort(10, 50, PortSide.Left, longLabel, MaxLabelWidth: (boxWidth / 2.0) - 4.0);
        var options = new RenderOptions(Themes.Light);
        var background = Rgba32.Parse(Themes.Light.BackgroundColor);

        using var unconstrainedSurface = RenderToSurface(new LayoutTree(boxWidth, 100, [unconstrainedPort]), options);
        using var constrainedSurface = RenderToSurface(new LayoutTree(boxWidth, 100, [constrainedPort]), options);

        // Act
        var unconstrainedRightmost = RightmostForegroundX(unconstrainedSurface, background, yStart: 40, yEnd: 60);
        var constrainedRightmost = RightmostForegroundX(constrainedSurface, background, yStart: 40, yEnd: 60);

        // Assert
        Assert.True(unconstrainedRightmost >= 0);
        Assert.True(constrainedRightmost >= 0);
        Assert.True(
            constrainedRightmost < unconstrainedRightmost,
            $"Expected the MaxLabelWidth-constrained label's rightmost pixel ({constrainedRightmost}) " +
            $"to be left of the unconstrained label's ({unconstrainedRightmost}).");
    }

    /// <summary>
    ///     Renders a layout tree into a CanvasNet surface so tests can inspect exact pixels.
    /// </summary>
    /// <param name="layout">Layout tree to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    /// <returns>Rendered surface. Caller must dispose it.</returns>
    private static Surface RenderToSurface(LayoutTree layout, RenderOptions options) => PngRenderer.RenderToSurface(layout, options);
}
