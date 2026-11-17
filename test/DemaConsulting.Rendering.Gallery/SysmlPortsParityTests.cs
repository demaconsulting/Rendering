// <copyright file="SysmlPortsParityTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.CanvasNet;
using DemaConsulting.Rendering.Svg;

namespace DemaConsulting.Rendering.Gallery;

/// <summary>
///     Proves the SVG and PNG (CanvasNet) renderers draw the SysML notation added for ports and connection
///     ends — the diamond node outline, the port glyphs and labels, and the connection end labels — at the
///     same places. The SVG document is the reference (its geometry is read back from the markup); the
///     PNG is probed from pixels. This lives in the gallery project because it is the one test project that
///     references both renderers.
/// </summary>
public sealed class SysmlPortsParityTests
{
    /// <summary>Maximum disagreement, in pixels, between a PNG edge and the SVG geometry (anti-aliasing).</summary>
    private const double Tolerance = 1.75;

    /// <summary>
    ///     The PNG diamond outline passes through every vertex of the SVG polygon and through the midpoint
    ///     of every edge, within the anti-aliasing tolerance, so both renderers draw the same diamond.
    /// </summary>
    [Fact]
    public void SysmlPortsParity_DiamondShape_PngOutlineMatchesSvgPolygon()
    {
        // Arrange
        var box = new LayoutBox(30, 20, 120, 80, null, Depth: 1, BoxShape.Diamond, [], []);
        var tree = new LayoutTree(180, 120, [box]);
        var options = new RenderOptions(Themes.Light);

        // Act
        var svgPoints = ParseDiamondPolygon(RenderSvg(tree, options));
        using var surface = RenderPng(tree, options);

        // Assert: four vertices, each with PNG ink within the tolerance; so is each edge midpoint.
        Assert.Equal(4, svgPoints.Count);
        for (var i = 0; i < svgPoints.Count; i++)
        {
            var current = svgPoints[i];
            var next = svgPoints[(i + 1) % svgPoints.Count];
            Assert.True(HasInkWithin(surface, current.X, current.Y), $"no PNG ink at SVG vertex {current}");
            Assert.True(
                HasInkWithin(surface, (current.X + next.X) / 2.0, (current.Y + next.Y) / 2.0),
                $"no PNG ink at the midpoint of SVG edge {i}");
        }
    }

    /// <summary>
    ///     The PNG ink of an end label is centered on the anchor the SVG text element carries (both renderers
    ///     draw the same placement), within the glyph-shape tolerance of centering text by its x-height.
    /// </summary>
    [Fact]
    public void SysmlPortsParity_EndLabel_PngInkCentroidMatchesSvgTextAnchor()
    {
        // Arrange: a horizontal connector with a role-name label above its source end.
        var line = new LayoutLine(
            [new Point2D(60, 80), new Point2D(260, 80)],
            EndMarkerStyle.None,
            EndMarkerStyle.None,
            LineStyle.Solid,
            null,
            new LineEndLabel("owner", null),
            null);
        var tree = new LayoutTree(320, 120, [line]);
        var options = new RenderOptions(Themes.Light);

        // Act
        var svg = RenderSvg(tree, options);
        using var surface = RenderPng(tree, options);

        // Assert
        var match = Regex.Match(svg, "<text x=\"([0-9.]+)\" y=\"([0-9.]+)\"[^>]*>owner</text>");
        Assert.True(match.Success, "Expected the end label text element in the SVG.");
        var anchorX = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var anchorY = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

        double sumX = 0;
        double sumY = 0;
        var count = 0;
        for (var y = (int)(anchorY - 12); y <= 77; y++)
        {
            for (var x = (int)(anchorX - 30); x <= (int)(anchorX + 30); x++)
            {
                var color = surface[x, y];
                if (color.A > 32 && (color.R + color.G + color.B) < 450)
                {
                    sumX += x;
                    sumY += y;
                    count++;
                }
            }
        }

        Assert.True(count > 10, "Expected the label's ink in the PNG.");
        Assert.True(Math.Abs((sumX / count) - anchorX) <= 3.0, $"x centroid {sumX / count:0.0} vs anchor {anchorX:0.0}");
        Assert.True(Math.Abs((sumY / count) - anchorY) <= 3.5, $"y centroid {sumY / count:0.0} vs anchor {anchorY:0.0}");
    }

    /// <summary>Theory data: every direction on every side, solid and dashed.</summary>
    public static TheoryData<PortDirection, PortSide, bool> GlyphCases
    {
        get
        {
            var data = new TheoryData<PortDirection, PortSide, bool>();
            foreach (var direction in new[] { PortDirection.In, PortDirection.Out, PortDirection.InOut })
            {
                foreach (var side in new[] { PortSide.Left, PortSide.Right, PortSide.Top, PortSide.Bottom })
                {
                    foreach (var dashed in new[] { false, true })
                    {
                        data.Add(direction, side, dashed);
                    }
                }
            }

            return data;
        }
    }

    /// <summary>
    ///     For every direction, side, and outline style, the PNG glyph's ink footprint matches the SVG
    ///     rounded square (read back from the markup) and its arrow ink lies where the shared arrow geometry
    ///     puts it, within the anti-aliasing tolerance.
    /// </summary>
    /// <param name="direction">The arrow direction.</param>
    /// <param name="side">The port side.</param>
    /// <param name="dashed">Whether the outline is dashed.</param>
    [Theory]
    [MemberData(nameof(GlyphCases))]
    public void SysmlPortsParity_OutlinedGlyph_PngFootprintAndArrowMatchSvg(PortDirection direction, PortSide side, bool dashed)
    {
        // Arrange
        var port = new LayoutPort(
            60,
            60,
            side,
            null,
            Appearance: new PortAppearance(direction, OutlineStyle: dashed ? LineStyle.Dashed : LineStyle.Solid));
        var tree = new LayoutTree(120, 120, [port]);
        var options = new RenderOptions(Themes.Light);

        // Act
        var svg = RenderSvg(tree, options);
        using var surface = RenderPng(tree, options);

        // Assert: the SVG square.
        var rect = Regex.Match(svg, "<rect x=\"([0-9.]+)\" y=\"([0-9.]+)\" width=\"([0-9.]+)\" height=\"([0-9.]+)\" rx=");
        Assert.True(rect.Success, "Expected the glyph rect in the SVG.");
        var x = double.Parse(rect.Groups[1].Value, CultureInfo.InvariantCulture);
        var y = double.Parse(rect.Groups[2].Value, CultureInfo.InvariantCulture);
        var w = double.Parse(rect.Groups[3].Value, CultureInfo.InvariantCulture);

        // The PNG ink (anything darker than light gray) fits the square outline within the tolerance, and
        // its extent reaches (nearly) every edge of the square — dashes leave gaps along an edge but the
        // outline still touches all four sides.
        var (minX, minY, maxX, maxY) = InkBounds(surface, 40, 40, 80, 80);
        Assert.InRange(minX, x - Tolerance, x + Tolerance + 1);
        Assert.InRange(minY, y - Tolerance, y + Tolerance + 1);
        Assert.InRange(maxX, x + w - Tolerance - 1, x + w + Tolerance);
        Assert.InRange(maxY, y + w - Tolerance - 1, y + w + Tolerance);

        // The arrow: every SVG arrow polyline vertex has PNG ink within the tolerance.
        var body = svg[svg.IndexOf("</defs>", StringComparison.Ordinal)..];
        foreach (Match polyline in Regex.Matches(body, "<polyline points=\"([^\"]*)\""))
        {
            foreach (var pair in polyline.Groups[1].Value.Split(' '))
            {
                var parts = pair.Split(',');
                var px = double.Parse(parts[0], CultureInfo.InvariantCulture);
                var py = double.Parse(parts[1], CultureInfo.InvariantCulture);
                Assert.True(HasInkWithin(surface, px, py), $"no PNG ink at SVG arrow vertex ({px}, {py})");
            }
        }
    }

    /// <summary>The proxy circle's centre and radius agree between the SVG markup and the PNG pixels.</summary>
    [Fact]
    public void SysmlPortsParity_ProxyPort_PngCircleMatchesSvgCircle()
    {
        // Arrange
        var port = new LayoutPort(60, 60, PortSide.Left, null, Appearance: new PortAppearance(Glyph: PortGlyph.Proxy));
        var tree = new LayoutTree(120, 120, [port]);
        var options = new RenderOptions(Themes.Light);

        // Act
        var svg = RenderSvg(tree, options);
        using var surface = RenderPng(tree, options);

        // Assert
        var circle = Regex.Match(svg[svg.IndexOf("</defs>", StringComparison.Ordinal)..], "<circle cx=\"([0-9.]+)\" cy=\"([0-9.]+)\" r=\"([0-9.]+)\"");
        Assert.True(circle.Success);
        var cx = double.Parse(circle.Groups[1].Value, CultureInfo.InvariantCulture);
        var cy = double.Parse(circle.Groups[2].Value, CultureInfo.InvariantCulture);
        var r = double.Parse(circle.Groups[3].Value, CultureInfo.InvariantCulture);
        var (minX, minY, maxX, maxY) = InkBounds(surface, 40, 40, 80, 80);
        Assert.InRange((minX + maxX + 1) / 2.0, cx - Tolerance, cx + Tolerance);
        Assert.InRange((minY + maxY + 1) / 2.0, cy - Tolerance, cy + Tolerance);
        Assert.InRange((maxX - minX + 1) / 2.0, r - Tolerance, r + Tolerance);
    }

    /// <summary>
    ///     An outward label is drawn at the same anchor in both renderers: the PNG text's edge nearest the
    ///     box lies on the SVG anchor x (end or start aligned) and its ink is centered on the SVG anchor y.
    /// </summary>
    /// <param name="side">The port side.</param>
    [Theory]
    [InlineData(PortSide.Left)]
    [InlineData(PortSide.Right)]
    [InlineData(PortSide.Top)]
    [InlineData(PortSide.Bottom)]
    public void SysmlPortsParity_OutwardLabel_PngTextMatchesSvgAnchor(PortSide side)
    {
        // Arrange: a box with the port at the middle of the chosen face.
        var box = new LayoutBox(90, 90, 100, 60, null, 0, BoxShape.Rectangle, [], []);
        var (cx, cy) = side switch
        {
            PortSide.Left => (90.0, 120.0),
            PortSide.Right => (190.0, 120.0),
            PortSide.Top => (140.0, 90.0),
            _ => (140.0, 150.0),
        };
        var port = new LayoutPort(cx, cy, side, "speed : Real", Appearance: new PortAppearance(LabelPlacement: PortLabelPlacement.Outward));
        var tree = new LayoutTree(300, 240, [box, port]);
        var options = new RenderOptions(Themes.Light);

        // Act
        var svg = RenderSvg(tree, options);
        using var surface = RenderPng(tree, options);

        // Assert
        var text = Regex.Match(svg, "<text x=\"([0-9.\\-]+)\" y=\"([0-9.\\-]+)\"[^>]*text-anchor=\"(start|end)\"[^>]*>speed : Real</text>");
        Assert.True(text.Success);
        var anchorX = double.Parse(text.Groups[1].Value, CultureInfo.InvariantCulture);
        var anchorY = double.Parse(text.Groups[2].Value, CultureInfo.InvariantCulture);
        var bounds = NotationMetrics.PortOutwardLabelBounds(port, "speed : Real", Themes.Light);

        // Ink within the label's own rectangle (excluding the glyph and connector areas).
        var (minX, minY, maxX, maxY) = InkBounds(
            surface,
            (int)Math.Floor(bounds.X - bounds.HalfWidth - 4),
            (int)Math.Floor(bounds.Y - bounds.HalfHeight - 1),
            (int)Math.Ceiling(bounds.X + bounds.HalfWidth + 4),
            (int)Math.Ceiling(bounds.Y + bounds.HalfHeight + 1),
            lightThreshold: 150);
        if (text.Groups[3].Value == "start")
        {
            Assert.InRange(minX, anchorX - 2.5, anchorX + 2.5);
        }
        else
        {
            Assert.InRange(maxX + 1, anchorX - 2.5, anchorX + 2.5);
        }

        Assert.InRange((minY + maxY + 1) / 2.0, anchorY - 3.5, anchorY + 3.5);
    }

    /// <summary>
    ///     A marker end on an outlined port is shortened by the glyph's own half size in both renderers: the
    ///     SVG path ends 6 px short of the port centre and the PNG arrow base is at the matching column.
    /// </summary>
    [Fact]
    public void SysmlPortsParity_PortGlyph_MarkerEndShortenedIdenticallyInBothRenderers()
    {
        // Arrange
        var theme = Themes.Light;
        var port = new LayoutPort(160, 50, PortSide.Left, null, Appearance: new PortAppearance(PortDirection.In));
        var line = new LayoutLine(
            [new Point2D(40, 50), new Point2D(160, 50)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            null);
        var tree = new LayoutTree(200, 100, [line, port]);
        var options = new RenderOptions(theme);

        // Act
        var svg = RenderSvg(tree, options);
        using var surface = RenderPng(tree, options);

        // Assert: the SVG path ends at the glyph's outer edge ...
        var pathEnd = double.Parse(
            Regex.Match(svg, "<path d=\"M [\\d.]+ [\\d.]+ L ([\\d.]+) [\\d.]+\"").Groups[1].Value,
            CultureInfo.InvariantCulture);
        Assert.Equal(160 - NotationMetrics.PortGlyphHalfSize, pathEnd, 1e-6);

        // ... the arrow base is one marker anchor behind it, and the PNG draws ink at that same column.
        var (_, refX) = NotationMetrics.MarkerAnchor(EndMarkerStyle.FilledArrow, theme);
        var column = (int)Math.Round(pathEnd - (refX * NotationMetrics.MarkerScale(theme)));
        var darkest = 0.0;
        for (var y = 45; y <= 46; y++)
        {
            darkest = Math.Max(darkest, (255.0 - surface[column, y].R) / (255.0 - 0x1A));
        }

        Assert.True(darkest >= 0.5, $"Expected PNG arrow-base ink at column {column}.");
    }

    private static (int MinX, int MinY, int MaxX, int MaxY) InkBounds(Surface surface, int x0, int y0, int x1, int y1, int lightThreshold = 100)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (var y = Math.Max(0, y0); y <= Math.Min(surface.Height - 1, y1); y++)
        {
            for (var x = Math.Max(0, x0); x <= Math.Min(surface.Width - 1, x1); x++)
            {
                var color = surface[x, y];
                if (color.A > 32 && (255 * 3) - (color.R + color.G + color.B) > lightThreshold * 3 / 2)
                {
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }
        }

        Assert.True(minX != int.MaxValue, "Expected some ink.");
        return (minX, minY, maxX, maxY);
    }
    private static Surface RenderPng(LayoutTree tree, RenderOptions options)
    {
        using var stream = new MemoryStream();
        new PngRenderer().Render(tree, options, stream);
        stream.Position = 0;
        return PngCodec.Load(stream);
    }

    private static string RenderSvg(LayoutTree tree, RenderOptions options)
    {
        using var output = new MemoryStream();
        new SvgRenderer().Render(tree, options, output);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    /// <summary>Reads the diamond outline polygon (the one with exactly four points that starts at the top vertex).</summary>
    private static List<(double X, double Y)> ParseDiamondPolygon(string svg)
    {
        var match = Regex.Match(svg, "<polygon points=\"([0-9.]+),([0-9.]+) ([0-9.]+),([0-9.]+) ([0-9.]+),([0-9.]+) ([0-9.]+),([0-9.]+)\"");
        Assert.True(match.Success, "Expected the diamond outline polygon in the SVG.");
        var points = new List<(double, double)>();
        for (var i = 1; i < match.Groups.Count; i += 2)
        {
            points.Add((
                double.Parse(match.Groups[i].Value, CultureInfo.InvariantCulture),
                double.Parse(match.Groups[i + 1].Value, CultureInfo.InvariantCulture)));
        }

        return points;
    }

    private static bool HasInkWithin(Surface surface, double x, double y)
    {
        var reach = (int)Math.Ceiling(Tolerance);
        for (var dy = -reach; dy <= reach; dy++)
        {
            for (var dx = -reach; dx <= reach; dx++)
            {
                var color = surface[(int)Math.Round(x) + dx, (int)Math.Round(y) + dy];
                if (color.A > 32 && (color.R + color.G + color.B) < 650)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
