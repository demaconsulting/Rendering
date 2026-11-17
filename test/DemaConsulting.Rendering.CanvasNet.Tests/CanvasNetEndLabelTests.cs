// <copyright file="CanvasNetEndLabelTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.CanvasNet;

namespace DemaConsulting.Rendering.CanvasNet.Tests;

/// <summary>
///     Pixel tests for the CanvasNet (PNG) rendering of connection end labels: ink at the positions
///     <see cref="ConnectorLabelPlacer.PlaceEndLabels"/> chose, none where no label was placed, and a
///     surface grown to include every label.
/// </summary>
public sealed class CanvasNetEndLabelTests
{
    private static Surface Render(LayoutTree tree) =>
        PngRenderer.RenderToSurface(tree, new RenderOptions(Themes.Light));

    private static LayoutLine Line(LineEndLabel? source, LineEndLabel? target) =>
        new([new Point2D(100, 60), new Point2D(300, 60)], EndMarkerStyle.None, EndMarkerStyle.FilledArrow, LineStyle.Solid, null, source, target);

    private static LayoutTree Tree(LayoutLine line) =>
        new(400, 120, [new LayoutBox(20, 30, 80, 60, null, 0, BoxShape.Rectangle, [], []), line]);

    private static IReadOnlyList<EndLabelPlacement> Expected(LayoutTree tree) =>
        ConnectorLabelPlacer.PlaceEndLabels(
            tree.Nodes.OfType<LayoutLine>(),
            Themes.Light.FontSizeBody,
            tree.Nodes.OfType<LayoutBox>(),
            NotationMetrics.MarkerScale(Themes.Light));

    private static bool IsInk(Rgba32 color) => color.A > 32 && (color.R + color.G + color.B) < 450;

    private static int CountInk(Surface surface, double x0, double y0, double x1, double y1)
    {
        var count = 0;
        for (var y = (int)Math.Floor(y0); y <= (int)Math.Ceiling(y1); y++)
        {
            for (var x = (int)Math.Floor(x0); x <= (int)Math.Ceiling(x1); x++)
            {
                if (x >= 0 && y >= 0 && x < surface.Width && y < surface.Height && IsInk(surface[x, y]))
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>There is text ink inside every placed end-label rectangle.</summary>
    [Fact]
    public void PngEndLabels_HaveInkInsideEachPlacedRectangle()
    {
        // Arrange
        var tree = Tree(Line(new LineEndLabel("owner", "1"), new LineEndLabel("parts", "0..*")));

        // Act
        using var surface = Render(tree);

        // Assert
        var placements = Expected(tree);
        Assert.Equal(4, placements.Count);
        foreach (var placement in placements)
        {
            var ink = CountInk(
                surface,
                placement.X - placement.HalfWidth,
                placement.Y - placement.HalfHeight,
                placement.X + placement.HalfWidth,
                placement.Y + placement.HalfHeight);
            Assert.True(ink >= 6, $"label '{placement.Text}' has only {ink} ink pixels");
        }
    }

    /// <summary>The ink of a label is centered on its placement (the same anchor as the SVG text).</summary>
    [Fact]
    public void PngEndLabels_InkIsCenteredOnThePlacement()
    {
        // Arrange
        var tree = Tree(Line(new LineEndLabel("owner"), null));
        var placement = Assert.Single(Expected(tree));

        // Act
        using var surface = Render(tree);

        // Assert: horizontal and vertical ink centroids are within 3 px of the placement centre.
        double sumX = 0;
        double sumY = 0;
        var count = 0;
        // Stop above the connector (row 59-60) so the line itself is not counted as label ink.
        for (var y = (int)(placement.Y - placement.HalfHeight); y <= 57; y++)
        {
            for (var x = (int)(placement.X - placement.HalfWidth); x <= (int)(placement.X + placement.HalfWidth); x++)
            {
                if (IsInk(surface[x, y]))
                {
                    sumX += x;
                    sumY += y;
                    count++;
                }
            }
        }

        Assert.True(count > 0);
        Assert.InRange(sumX / count, placement.X - 3, placement.X + 3);
        Assert.True(Math.Abs((sumY / count) - placement.Y) <= 3, $"centroid {sumX / count:0.00},{sumY / count:0.00} vs placement {placement.X:0.00},{placement.Y:0.00}");
    }

    /// <summary>A line with no end labels has no ink above it, where the labels would otherwise sit.</summary>
    [Fact]
    public void PngLineWithoutEndLabels_HasNoInkBesideTheLine()
    {
        // Arrange
        var tree = Tree(Line(null, null));

        // Act
        using var surface = Render(tree);

        // Assert: the band 8 to 24 px above the line, between the box and the marker, is blank.
        Assert.Equal(0, CountInk(surface, 110, 36, 280, 52));
    }

    /// <summary>The surface grows to include a label that extends past the declared size.</summary>
    [Fact]
    public void PngEndLabelBeyondTheSurface_GrowsTheSurface()
    {
        // Arrange
        var line = new LayoutLine(
            [new Point2D(195, 20), new Point2D(195, 100)],
            EndMarkerStyle.None,
            EndMarkerStyle.None,
            LineStyle.Solid,
            null,
            new LineEndLabel("a long role name", null),
            null);
        var tree = new LayoutTree(200, 120, [line]);
        var placement = Assert.Single(Expected(tree));

        // Act
        using var surface = Render(tree);

        // Assert
        Assert.True(placement.X + placement.HalfWidth > 200);
        Assert.True(surface.Width >= (int)Math.Ceiling(placement.X + placement.HalfWidth));
    }
}
