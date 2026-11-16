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
        // The base edge is a vertical stroke at the marker base column (the notation base position
        // times the shared marker scale); sample it just above and below the line, clear of both the
        // line stroke and the diagonal edges.
        var baseColumn = (int)Math.Round(150 - ((NotationMetrics.EndMarkerRefX + NotationMetrics.TipRetraction(EndMarkerStyle.HollowTriangle, Themes.Light)) * NotationMetrics.MarkerScale(Themes.Light)));
        var chevronInk = CountInk(chevron, baseColumn - 1, 46, baseColumn + 1, 47) + CountInk(chevron, baseColumn - 1, 53, baseColumn + 1, 54);
        var triangleInk = CountInk(triangle, baseColumn - 1, 46, baseColumn + 1, 47) + CountInk(triangle, baseColumn - 1, 53, baseColumn + 1, 54);

        // Assert
        Assert.True(
            chevronInk < triangleInk,
            $"Expected open chevron ({chevronInk}) to have fewer base-edge ink pixels than closed triangle ({triangleInk}).");
    }

    /// <summary>
    ///     The PNG filled-arrow spans <see cref="NotationMetrics.EndMarkerLength"/> scaled by the shared marker scale along the line —
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
        var expected = NotationMetrics.EndMarkerLength * NotationMetrics.MarkerScale(Themes.Light);
        Assert.InRange(alongLength, expected - 2, expected + 2);
    }

    /// <summary>
    ///     The PNG arrow base spans the shared <see cref="NotationMetrics.EndMarkerWidth"/> (times the marker scale) across the
    ///     line, matching the SVG marker height. Measured at the marker base column.
    /// </summary>
    [Fact]
    public void FilledArrow_BaseWidth_MatchesNotationMetrics()
    {
        // Arrange
        var options = new RenderOptions(Themes.Light);
        using var surface = RenderToSurface(HorizontalLineTo(EndMarkerStyle.FilledArrow), options);

        // Act
        var baseX = (int)Math.Round(150 - ((NotationMetrics.EndMarkerRefX + NotationMetrics.TipRetraction(EndMarkerStyle.FilledArrow, Themes.Light)) * NotationMetrics.MarkerScale(Themes.Light)));
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
        var expectedWidth = NotationMetrics.EndMarkerWidth * NotationMetrics.MarkerScale(Themes.Light);
        Assert.InRange(measuredWidth, expectedWidth - 2, expectedWidth + 2);
    }

    /// <summary>All end-marker styles, for the tip-landing theories.</summary>
    public static TheoryData<EndMarkerStyle> AllStyles
    {
        get
        {
            var data = new TheoryData<EndMarkerStyle>();
            foreach (var style in Enum.GetValues<EndMarkerStyle>().Where(style => style != EndMarkerStyle.None))
            {
                data.Add(style);
            }

            return data;
        }
    }

    /// <summary>
    ///     Returns the darkness (0 to 1, relative to the Light theme stroke color on a white background)
    ///     of a pixel, so anti-aliased edge pixels count fractionally.
    /// </summary>
    private static double Coverage(Rgba32 color) => Math.Clamp((255.0 - color.R) / (255.0 - 0x1A), 0.0, 1.0);

    /// <summary>
    ///     For every style at a TARGET end, no pixel at or past the endpoint column carries marker ink:
    ///     the stroked tip lands on the endpoint (the box boundary) instead of poking 1-2 px past it.
    ///     Regression test for demaconsulting/Rendering#49.
    /// </summary>
    /// <param name="style">The end-marker style.</param>
    [Theory]
    [MemberData(nameof(AllStyles))]
    public void EndMarker_TargetEnd_PaintedTipDoesNotPassEndpoint(EndMarkerStyle style)
    {
        using var surface = RenderToSurface(HorizontalLineTo(style), new RenderOptions(Themes.Light));

        // The endpoint is x = 150, a pixel boundary: columns 150.. are past it. Allow only faint
        // anti-aliasing (< 25% coverage) from float rounding at the boundary.
        for (var x = 150; x < 165; x++)
        {
            for (var y = 30; y < 70; y++)
            {
                Assert.True(Coverage(surface[x, y]) < 0.25, $"{style}: marker ink {Coverage(surface[x, y]):F2} at ({x},{y}) lies past the endpoint x=150.");
            }
        }

        // And the marker does reach the endpoint: some column just inside it carries strong ink.
        var reachesEndpoint = false;
        for (var y = 30; y < 70; y++)
        {
            reachesEndpoint |= Coverage(surface[149, y]) >= 0.5 || Coverage(surface[148, y]) >= 0.5;
        }

        Assert.True(reachesEndpoint, $"{style}: the marker tip must reach the endpoint column.");
    }

    /// <summary>
    ///     For every style at a SOURCE end, no pixel left of the start point carries marker ink: the mirrored
    ///     glyph's tip lands on the start point.
    /// </summary>
    /// <param name="style">The end-marker style.</param>
    [Theory]
    [MemberData(nameof(AllStyles))]
    public void EndMarker_SourceEnd_PaintedTipDoesNotPassStartPoint(EndMarkerStyle style)
    {
        var line = new LayoutLine([new Point2D(50, 50), new Point2D(190, 50)], style, EndMarkerStyle.None, LineStyle.Solid, null);
        using var surface = RenderToSurface(new LayoutTree(200, 100, [line]), new RenderOptions(Themes.Light));

        for (var x = 35; x < 50; x++)
        {
            for (var y = 30; y < 70; y++)
            {
                Assert.True(Coverage(surface[x, y]) < 0.25, $"{style}: marker ink {Coverage(surface[x, y]):F2} at ({x},{y}) lies past the start x=50.");
            }
        }
    }

    /// <summary>
    ///     A marker landing on a box face leaves the box interior untouched: with the target box's left face
    ///     at x = 150, the interior column just inside the border carries no marker ink.
    /// </summary>
    /// <param name="style">The end-marker style.</param>
    [Theory]
    [MemberData(nameof(AllStyles))]
    public void EndMarker_OnBoxFace_DoesNotInkTheBoxInterior(EndMarkerStyle style)
    {
        var box = new LayoutBox(150, 20, 80, 60, null, 0, BoxShape.Rectangle, [], []);
        var line = new LayoutLine([new Point2D(10, 50), new Point2D(150, 50)], EndMarkerStyle.None, style, LineStyle.Solid, null);
        using var surface = RenderToSurface(new LayoutTree(240, 100, [box, line]), new RenderOptions(Themes.Light));

        // The 1.5 px border spans x 149.25 to 150.75; column 151 is wholly inside the box.
        for (var y = 30; y < 70; y++)
        {
            Assert.True(Coverage(surface[151, y]) < 0.25, $"{style}: marker ink {Coverage(surface[151, y]):F2} at (151,{y}) is inside the target box.");
        }
    }

    /// <summary>
    ///     A marker end that sits on a port glyph is shortened by the port half size so the arrowhead is not
    ///     hidden under the 8x8 port square: the marker base stays visible at its full distance from the
    ///     port's outer edge.
    /// </summary>
    [Fact]
    public void EndMarker_OnPort_IsNotHiddenUnderPortGlyph()
    {
        // Arrange: a port glyph centered on the line end at (150, 50); the glyph covers x 146 to 154.
        var port = new LayoutPort(150, 50, PortSide.Left, null);
        var line = new LayoutLine([new Point2D(10, 50), new Point2D(150, 50)], EndMarkerStyle.None, EndMarkerStyle.FilledArrow, LineStyle.Solid, null);
        using var surface = RenderToSurface(new LayoutTree(200, 100, [line, port]), new RenderOptions(Themes.Light));

        // Act: the arrow's base column. The tip lands on the port's outer edge (x = 146), so the base is
        // one marker length (plus retraction) further back than the port edge.
        var scale = NotationMetrics.MarkerScale(Themes.Light);
        var baseX = 146 - ((NotationMetrics.EndMarkerRefX + NotationMetrics.TipRetraction(EndMarkerStyle.FilledArrow, Themes.Light)) * scale);

        // Assert: strong ink on the base column above the line stroke (the arrow's wide base corners).
        var column = (int)Math.Round(baseX);
        var found = false;
        for (var y = 45; y <= 46; y++)
        {
            found |= Coverage(surface[column, y]) >= 0.5;
        }

        Assert.True(found, $"Expected the arrow base ink near column {column} (tip on the port's outer edge x=146).");
    }
}
