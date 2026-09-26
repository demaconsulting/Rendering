// <copyright file="PngEndMarkerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.CanvasNet;

namespace DemaConsulting.Rendering.CanvasNet.Tests;

/// <summary>
///     Tests for PNG line-end (connector decoration) markers. These confirm that the open chevron
///     is drawn OPEN (two strokes, no closing base edge), and that the PNG marker geometry matches
///     the shared <see cref="NotationMetrics"/> source used by the SVG renderer (along-line overshoot
///     and across-line width), so SVG and PNG produce identical end-marker geometry.
/// </summary>
public sealed class PngEndMarkerTests
{
    /// <summary>
    ///     Renders the supplied layout into a CanvasNet surface so tests can sample exact rendered pixels.
    /// </summary>
    /// <param name="layout">Layout tree to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    /// <returns>Rendered surface. Caller must dispose it.</returns>
    private static Surface RenderToSurface(LayoutTree layout, RenderOptions options) => PngRenderer.RenderToSurface(layout, options);

    /// <summary>
    ///     Returns whether a sampled pixel is visibly darker than the white background.
    /// </summary>
    /// <param name="color">Sampled pixel color.</param>
    /// <returns><see langword="true"/> when the pixel counts as ink.</returns>
    private static bool IsInk(Rgba32 color) => color.A > 32 && (color.R + color.G + color.B) < 600;

    /// <summary>
    ///     Counts ink pixels in the supplied inclusive rectangle.
    /// </summary>
    /// <param name="surface">Rendered surface to scan.</param>
    /// <param name="x0">Minimum X, inclusive.</param>
    /// <param name="y0">Minimum Y, inclusive.</param>
    /// <param name="x1">Maximum X, inclusive.</param>
    /// <param name="y1">Maximum Y, inclusive.</param>
    /// <returns>The number of ink pixels in the region.</returns>
    private static int CountInk(Surface surface, int x0, int y0, int x1, int y1)
    {
        var count = 0;
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (IsInk(surface[x, y]))
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    ///     Builds a simple horizontal line whose target marker lands at (150, 50).
    /// </summary>
    /// <param name="target">Target end-marker style.</param>
    /// <returns>Layout containing the single line.</returns>
    private static LayoutTree HorizontalLineTo(EndMarkerStyle target)
    {
        var line = new LayoutLine(
            [new Point2D(10, 50), new Point2D(150, 50)],
            EndMarkerStyle.None,
            target,
            LineStyle.Solid,
            null);
        return new LayoutTree(200, 100, [line]);
    }

    /// <summary>
    ///     The open chevron is drawn OPEN: it has strictly fewer ink pixels in the marker zone than
    ///     the closed hollow triangle (which adds the closing base edge).
    /// </summary>
    [Fact]
    public void OpenChevron_HasFewerInkPixelsThanClosedTriangle()
    {
        // Arrange
        var options = new RenderOptions(Themes.Light);
        using var chevron = RenderToSurface(HorizontalLineTo(EndMarkerStyle.OpenChevron), options);
        using var triangle = RenderToSurface(HorizontalLineTo(EndMarkerStyle.HollowTriangle), options);

        // Act
        var chevronInk = CountInk(chevron, 138, 42, 144, 58);
        var triangleInk = CountInk(triangle, 138, 42, 144, 58);

        // Assert
        Assert.True(
            chevronInk < triangleInk,
            $"Expected open chevron ({chevronInk}) to have fewer base-edge ink pixels than closed triangle ({triangleInk}).");
    }

    /// <summary>
    ///     The PNG filled-arrow spans <see cref="NotationMetrics.EndMarkerLength"/> along the line —
    ///     from the base (at <see cref="NotationMetrics.EndMarkerRefX"/> behind the endpoint) to the apex
    ///     (overshooting the endpoint) — matching the SVG marker box length. Measured from the widest
    ///     (base) column to the furthest ink column.
    /// </summary>
    [Fact]
    public void FilledArrow_AlongLength_MatchesNotationMetrics()
    {
        // Arrange
        var options = new RenderOptions(Themes.Light);
        using var surface = RenderToSurface(HorizontalLineTo(EndMarkerStyle.FilledArrow), options);

        // Act
        var baseX = -1;
        var baseExtent = 0;
        var tipX = -1;
        for (var x = 135; x <= 155; x++)
        {
            var minY = int.MaxValue;
            var maxY = int.MinValue;
            for (var y = 43; y <= 57; y++)
            {
                if (IsInk(surface[x, y]))
                {
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }

            if (maxY < minY)
            {
                continue;
            }

            var extent = maxY - minY + 1;
            if (extent > baseExtent)
            {
                baseExtent = extent;
                baseX = x;
            }

            tipX = x;
        }

        // Assert
        Assert.True(baseX >= 0, "Expected to locate the marker base column.");
        var alongLength = tipX - baseX;
        Assert.InRange(alongLength, NotationMetrics.EndMarkerLength - 2, NotationMetrics.EndMarkerLength + 2);
    }

    /// <summary>
    ///     The PNG arrow base spans the shared <see cref="NotationMetrics.EndMarkerWidth"/> across the
    ///     line, matching the SVG marker height. Measured at the marker base column.
    /// </summary>
    [Fact]
    public void FilledArrow_BaseWidth_MatchesNotationMetrics()
    {
        // Arrange
        var options = new RenderOptions(Themes.Light);
        using var surface = RenderToSurface(HorizontalLineTo(EndMarkerStyle.FilledArrow), options);

        // Act
        var baseX = (int)Math.Round(150 - NotationMetrics.EndMarkerRefX);
        var minY = int.MaxValue;
        var maxY = int.MinValue;
        for (var y = 40; y <= 60; y++)
        {
            if (IsInk(surface[baseX, y]))
            {
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }

        // Assert
        Assert.True(maxY >= minY, "Expected ink at the marker base column.");
        var measuredWidth = maxY - minY + 1;
        Assert.InRange(measuredWidth, NotationMetrics.EndMarkerWidth - 2, NotationMetrics.EndMarkerWidth + 2);
    }
}
