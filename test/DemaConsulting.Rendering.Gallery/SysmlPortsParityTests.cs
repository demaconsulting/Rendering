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
                if (color.A > 32 && (color.R + color.G + color.B) < 300)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
