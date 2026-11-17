// <copyright file="SvgPortGlyphTests.cs" company="DemaConsulting">
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
///     Tests for the SVG rendering of the SysML-style port glyphs: the legacy filled square is unchanged;
///     an outlined glyph is a background-filled rounded square with a direction arrow; a dashed outline has
///     a dash array; a proxy port is a filled circle; an outward label sits outside the owning box; and a
///     marker end is shortened by the glyph's own half size.
/// </summary>
public sealed class SvgPortGlyphTests
{
    private static string Render(LayoutTree tree, double scale = 1.0)
    {
        using var output = new MemoryStream();
        new SvgRenderer().Render(tree, new RenderOptions(Themes.Light, scale), output);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static string F(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private static LayoutTree Tree(params LayoutNode[] nodes) => new(300, 200, nodes);

    private static LayoutPort Port(PortSide side, PortAppearance? appearance, string? label = null) =>
        new(100, 80, side, label, Appearance: appearance);

    /// <summary>The legacy port glyph is exactly the historical markup, with or without a default appearance.</summary>
    [Fact]
    public void SvgRenderer_LegacyPort_IsTheHistoricalFilledSquare()
    {
        // Arrange
        var expected = $"<rect x=\"{F(96)}\" y=\"{F(76)}\" width=\"{F(8)}\" height=\"{F(8)}\" fill=\"{Themes.Light.StrokeColor}\" stroke=\"{Themes.Light.BackgroundColor}\" stroke-width=\"{F(1)}\"/>";

        // Act
        var plain = Render(Tree(Port(PortSide.Left, null)));
        var defaulted = Render(Tree(Port(PortSide.Left, new PortAppearance())));

        // Assert
        Assert.Contains(expected, plain, StringComparison.Ordinal);
        Assert.Equal(plain, defaulted);
    }

    /// <summary>An outlined glyph is a 12 px rounded square filled with the background and stroked in the theme stroke.</summary>
    [Fact]
    public void SvgRenderer_OutlinedPort_IsARoundedBackgroundFilledSquare()
    {
        // Act
        var svg = Render(Tree(Port(PortSide.Left, new PortAppearance(PortDirection.None, PortGlyph.Outlined))));

        // Assert
        var rect = Regex.Match(svg, "<rect x=\"94.00\" y=\"74.00\"[^>]*/>").Value;
        Assert.NotEmpty(rect);
        Assert.Contains($"width=\"{F(12)}\" height=\"{F(12)}\"", rect, StringComparison.Ordinal);
        Assert.Contains($"rx=\"{F(NotationMetrics.PortGlyphCornerRadius)}\"", rect, StringComparison.Ordinal);
        Assert.Contains($"fill=\"{Themes.Light.BackgroundColor}\"", rect, StringComparison.Ordinal);
        Assert.Contains($"stroke=\"{Themes.Light.StrokeColor}\"", rect, StringComparison.Ordinal);
        Assert.DoesNotContain("stroke-dasharray", rect, StringComparison.Ordinal);
    }

    /// <summary>A dashed outline (a SysML reference port) carries the dash array on the glyph.</summary>
    [Fact]
    public void SvgRenderer_DashedPort_HasADashArray()
    {
        // Act
        var svg = Render(Tree(Port(PortSide.Left, new PortAppearance(OutlineStyle: LineStyle.Dashed))));

        // Assert
        var rect = Regex.Match(svg, "<rect x=\"94.00\" y=\"74.00\"[^>]*/>").Value;
        Assert.Contains("stroke-dasharray=\"6 3\"", rect, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Every direction arrow on every side is drawn as one polyline per segment, at the glyph-local points
    ///     of <see cref="NotationMetrics.PortArrowSegments"/> around the port centre.
    /// </summary>
    /// <param name="direction">The arrow direction.</param>
    /// <param name="side">The port side.</param>
    [Theory]
    [InlineData(PortDirection.In, PortSide.Left)]
    [InlineData(PortDirection.In, PortSide.Right)]
    [InlineData(PortDirection.In, PortSide.Top)]
    [InlineData(PortDirection.In, PortSide.Bottom)]
    [InlineData(PortDirection.Out, PortSide.Left)]
    [InlineData(PortDirection.Out, PortSide.Right)]
    [InlineData(PortDirection.Out, PortSide.Top)]
    [InlineData(PortDirection.Out, PortSide.Bottom)]
    [InlineData(PortDirection.InOut, PortSide.Left)]
    [InlineData(PortDirection.InOut, PortSide.Top)]
    public void SvgRenderer_PortArrow_IsDrawnFromTheSharedGeometry(PortDirection direction, PortSide side)
    {
        // Act
        var svg = Render(Tree(Port(side, new PortAppearance(direction))));

        // Assert
        var segments = NotationMetrics.PortArrowSegments(direction, side);
        var body = svg[svg.IndexOf("</defs>", StringComparison.Ordinal)..];
        var polylines = Regex.Matches(body, "<polyline points=\"([^\"]*)\"[^>]*/>")
            .Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .ToList();
        Assert.Equal(segments.Count, polylines.Count);
        for (var i = 0; i < segments.Count; i++)
        {
            var expected = string.Join(" ", segments[i].Select(point => $"{F(100 + point.X)},{F(80 + point.Y)}"));
            Assert.Equal(expected, polylines[i]);
        }
    }

    /// <summary>A port without a direction has no arrow.</summary>
    [Fact]
    public void SvgRenderer_PortWithoutDirection_HasNoArrow()
    {
        // Act
        var svg = Render(Tree(Port(PortSide.Left, new PortAppearance(OutlineStyle: LineStyle.Dashed))));

        // Assert
        Assert.DoesNotContain("<polyline", svg[svg.IndexOf("</defs>", StringComparison.Ordinal)..], StringComparison.Ordinal);
    }

    /// <summary>A proxy port is a filled circle of the proxy radius.</summary>
    [Fact]
    public void SvgRenderer_ProxyPort_IsAFilledCircle()
    {
        // Act
        var svg = Render(Tree(Port(PortSide.Left, new PortAppearance(Glyph: PortGlyph.Proxy))));

        // Assert
        Assert.Contains(
            $"<circle cx=\"{F(100)}\" cy=\"{F(80)}\" r=\"{F(NotationMetrics.PortProxyRadius)}\" fill=\"{Themes.Light.StrokeColor}\"",
            svg,
            StringComparison.Ordinal);
    }

    /// <summary>An outward label is drawn at the shared anchor, outside the owning box on every side.</summary>
    /// <param name="side">The port side.</param>
    [Theory]
    [InlineData(PortSide.Left)]
    [InlineData(PortSide.Right)]
    [InlineData(PortSide.Top)]
    [InlineData(PortSide.Bottom)]
    public void SvgRenderer_OutwardLabel_IsDrawnAtTheSharedAnchorOutsideTheBox(PortSide side)
    {
        // Arrange: a box with the port on the middle of the chosen face.
        var box = new LayoutBox(100, 80, 100, 60, "B", 0, BoxShape.Rectangle, [], []);
        var (cx, cy) = side switch
        {
            PortSide.Left => (100.0, 110.0),
            PortSide.Right => (200.0, 110.0),
            PortSide.Top => (150.0, 80.0),
            _ => (150.0, 140.0),
        };
        var port = new LayoutPort(cx, cy, side, "speed : Real", Appearance: new PortAppearance(PortDirection.In, LabelPlacement: PortLabelPlacement.Outward));

        // Act
        var svg = Render(Tree(box, port));

        // Assert
        var anchor = NotationMetrics.PortOutwardLabelAnchor(port, Themes.Light);
        var text = Regex.Match(svg, "<text x=\"([0-9.\\-]+)\" y=\"([0-9.\\-]+)\"[^>]*text-anchor=\"(start|end)\"[^>]*>speed : Real</text>");
        Assert.True(text.Success, "Expected the outward label text element.");
        Assert.Equal(F(anchor.X), text.Groups[1].Value);
        Assert.Equal(F(anchor.Y), text.Groups[2].Value);
        Assert.Equal(anchor.Align == PortLabelAlign.End ? "end" : "start", text.Groups[3].Value);
        var bounds = NotationMetrics.PortOutwardLabelBounds(port, "speed : Real", Themes.Light);
        var overlapsBox = bounds.X - bounds.HalfWidth < 200 && bounds.X + bounds.HalfWidth > 100 &&
                          bounds.Y - bounds.HalfHeight < 140 && bounds.Y + bounds.HalfHeight > 80;
        Assert.False(overlapsBox);
    }

    /// <summary>An inward label of an outlined port is offset by the larger glyph, not the legacy half size.</summary>
    [Fact]
    public void SvgRenderer_InwardLabelOfOutlinedPort_IsOffsetByTheGlyphHalfSize()
    {
        // Act
        var svg = Render(Tree(Port(PortSide.Left, new PortAppearance(PortDirection.In), "p")));
        var legacy = Render(Tree(Port(PortSide.Left, null, "p")));

        // Assert: a left port's inward label starts at centreX + glyphHalf + LabelPadding.
        var x = Regex.Match(svg, "<text x=\"([0-9.\\-]+)\"[^>]*>p</text>").Groups[1].Value;
        var legacyX = Regex.Match(legacy, "<text x=\"([0-9.\\-]+)\"[^>]*>p</text>").Groups[1].Value;
        Assert.Equal(F(100 + NotationMetrics.PortGlyphHalfSize + Themes.Light.LabelPadding), x);
        Assert.Equal(F(100 + NotationMetrics.PortHalfSize + Themes.Light.LabelPadding), legacyX);
    }

    /// <summary>A marker end on a port is shortened by the glyph's own half size.</summary>
    /// <param name="appearanceKind">Which appearance the port has.</param>
    /// <param name="expectedHalfSize">The half size the line end retreats by.</param>
    [Theory]
    [InlineData("legacy", 4.0)]
    [InlineData("outlined", 6.0)]
    [InlineData("proxy", 5.0)]
    public void SvgRenderer_MarkerEndOnPort_IsShortenedByTheGlyphHalfSize(string appearanceKind, double expectedHalfSize)
    {
        // Arrange
        PortAppearance? appearance = appearanceKind switch
        {
            "outlined" => new PortAppearance(PortDirection.In),
            "proxy" => new PortAppearance(Glyph: PortGlyph.Proxy),
            _ => null,
        };
        var line = new LayoutLine(
            [new Point2D(20, 80), new Point2D(100, 80)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            null);

        // Act
        var svg = Render(Tree(line, Port(PortSide.Left, appearance)));

        // Assert
        var end = double.Parse(
            Regex.Match(svg, "<path d=\"M [\\d.]+ [\\d.]+ L ([\\d.]+) [\\d.]+\"").Groups[1].Value,
            CultureInfo.InvariantCulture);
        Assert.Equal(100 - expectedHalfSize, end, 6);
    }

    /// <summary>The canvas grows to include an outward label that extends past the declared width.</summary>
    [Fact]
    public void SvgRenderer_OutwardLabelBeyondTheCanvas_GrowsTheCanvas()
    {
        // Arrange: a right port near the right edge with a long outward label.
        var port = new LayoutPort(
            290,
            100,
            PortSide.Right,
            "a long port name : SomeLongType",
            Appearance: new PortAppearance(LabelPlacement: PortLabelPlacement.Outward));
        var tree = new LayoutTree(300, 200, [port]);

        // Act
        var svg = Render(tree);

        // Assert
        var bounds = NotationMetrics.PortOutwardLabelBounds(port, port.ExternalLabel!, Themes.Light);
        var width = double.Parse(Regex.Match(svg, "<svg [^>]*width=\"([0-9.]+)\"").Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.True(bounds.X + bounds.HalfWidth > 300);
        Assert.True(width >= bounds.X + bounds.HalfWidth - 0.01, $"canvas width {width} must include the label");
    }
}
