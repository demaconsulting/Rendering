// <copyright file="SvgDiamondShapeTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Svg;

namespace DemaConsulting.Rendering.Svg.Tests;

/// <summary>
///     Tests for the SVG rendering of <see cref="BoxShape.Diamond"/>: a polygon through the four
///     bounding-box edge midpoints with the same fill, stroke, and dash handling as every other shape.
/// </summary>
public sealed class SvgDiamondShapeTests
{
    private static string Render(double scale, params LayoutNode[] nodes)
    {
        using var output = new MemoryStream();
        new SvgRenderer().Render(new LayoutTree(300, 200, nodes), new RenderOptions(Themes.Light, scale), output);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static string F(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>Returns the diamond outline polygon (the one that starts at the top vertex), not a marker.</summary>
    private static string DiamondPolygon(string svg) =>
        Regex.Match(svg, $"<polygon points=\"{F(120)},{F(20)}[^>]*/>").Value;

    private static LayoutBox Diamond(BoxAppearance? appearance = null, string? label = null) =>
        new(20, 20, 200, 100, label, Depth: 1, BoxShape.Diamond, [], [], Appearance: appearance);

    /// <summary>The diamond is one polygon whose points are the four box-edge midpoints.</summary>
    [Fact]
    public void SvgRenderer_Diamond_PolygonPointsAreTheFourEdgeMidpoints()
    {
        // Act
        var svg = Render(1.0, Diamond());

        // Assert
        var expected = $"points=\"{F(120)},{F(20)} {F(220)},{F(70)} {F(120)},{F(120)} {F(20)},{F(70)}\"";
        Assert.Contains("<polygon", svg, StringComparison.Ordinal);
        Assert.Contains(expected, svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<rect x=\"20.00\" y=\"20.00\" width=\"200.00\"", svg, StringComparison.Ordinal);
    }

    /// <summary>The polygon carries the depth fill and the theme stroke like any other box outline.</summary>
    [Fact]
    public void SvgRenderer_Diamond_UsesDepthFillAndThemeStroke()
    {
        // Act
        var svg = Render(1.0, Diamond());

        // Assert
        var polygon = DiamondPolygon(svg);
        Assert.Contains($"fill=\"{Themes.Light.DepthFillColors[1]}\"", polygon, StringComparison.Ordinal);
        Assert.Contains($"stroke=\"{Themes.Light.StrokeColor}\"", polygon, StringComparison.Ordinal);
        Assert.Contains($"stroke-width=\"{F(Themes.Light.StrokeWidth)}\"", polygon, StringComparison.Ordinal);
        Assert.DoesNotContain("stroke-dasharray", polygon, StringComparison.Ordinal);
    }

    /// <summary>A dashed outline appearance puts the dash array on the diamond polygon.</summary>
    [Fact]
    public void SvgRenderer_Diamond_DashedAppearance_AddsDashArray()
    {
        // Act
        var svg = Render(1.0, Diamond(new BoxAppearance(LineStyle.Dashed)));

        // Assert
        var polygon = DiamondPolygon(svg);
        Assert.Contains("stroke-dasharray=\"6 3\"", polygon, StringComparison.Ordinal);
    }

    /// <summary>The polygon scales with the render scale like every other outline.</summary>
    [Fact]
    public void SvgRenderer_Diamond_Scaled_ScalesThePolygonPoints()
    {
        // Act
        var svg = Render(2.0, Diamond());

        // Assert
        Assert.Contains($"points=\"{F(240)},{F(40)} {F(440)},{F(140)} {F(240)},{F(240)} {F(40)},{F(140)}\"", svg, StringComparison.Ordinal);
    }

    /// <summary>A rectangle is still drawn as a plain rect: the default output is unchanged.</summary>
    [Fact]
    public void SvgRenderer_Rectangle_StillRendersAsRectNotPolygon()
    {
        // Act
        var svg = Render(1.0, Diamond() with { Shape = BoxShape.Rectangle });

        // Assert
        Assert.Contains("<rect x=\"20.00\" y=\"20.00\" width=\"200.00\" height=\"100.00\"", svg, StringComparison.Ordinal);
        Assert.DoesNotContain($"<polygon points=\"{F(120)},{F(20)}", svg, StringComparison.Ordinal);
    }

    /// <summary>A label on a centered-title diamond is drawn horizontally centered on the diamond.</summary>
    [Fact]
    public void SvgRenderer_Diamond_Label_IsCenteredOnTheDiamond()
    {
        // Act
        var svg = Render(1.0, Diamond(label: "Decide") with { CenterTitle = true });

        // Assert
        var text = Regex.Match(svg, "<text [^>]*>Decide</text>").Value;
        Assert.Contains($"x=\"{F(120)}\"", text, StringComparison.Ordinal);
    }
}
