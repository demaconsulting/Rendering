// <copyright file="RenderedCustomThemeContainmentTests.cs" company="DemaConsulting">
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
///     Proves both renderers keep every connector label, port glyph, outward port label, and end marker inside
///     the canvas when the caller's <see cref="Theme"/> differs from the one the layout assumed (a much larger
///     body font and label padding): labels near the left or top edge are measured with the actual theme at
///     render time, and the canvas is grown and the content translated when anything would extend past the
///     origin. The layout-time fit cannot know the render theme, so this is the renderers' own guarantee.
/// </summary>
public sealed class RenderedCustomThemeContainmentTests
{
    /// <summary>Tolerance, in logical pixels, for limited-precision coordinates.</summary>
    private const double Tolerance = 0.6;

    private static readonly Theme LargeTheme = Themes.Light with { FontSizeBody = 24.0, LabelPadding = 12.0 };

    private static readonly Regex Root = new(
        """<svg [^>]*width="(?<w>[0-9.]+)" height="(?<h>[0-9.]+)" viewBox="0 0 [0-9.]+ [0-9.]+">""",
        RegexOptions.CultureInvariant);

    private static readonly Regex Group = new(
        """<g transform="translate\((?<x>[-0-9.]+) (?<y>[-0-9.]+)\)">""",
        RegexOptions.CultureInvariant);

    private static readonly Regex TextElement = new(
        """<text x="(?<x>[-0-9.]+)" y="(?<y>[-0-9.]+)"[^>]*font-size="(?<size>[0-9.]+)"[^>]*text-anchor="(?<anchor>\w+)"[^>]*>(?<text>[^<]*)</text>""",
        RegexOptions.CultureInvariant);

    /// <summary>
    ///     With a large body font and label padding, every label, port glyph and outward port label of the
    ///     rendered SVG lies inside the canvas, although the tree puts them against the left and top edges.
    /// </summary>
    [Fact]
    public void RenderedSvg_LargeTheme_LabelsAtLeftAndTopEdge_AreInsideCanvas()
    {
        // Arrange
        var tree = EdgeTree(0.0);

        // Act
        var svg = RenderSvg(tree, LargeTheme);

        // Assert
        var root = Root.Match(svg);
        Assert.True(root.Success);
        var width = double.Parse(root.Groups["w"].Value, CultureInfo.InvariantCulture);
        var height = double.Parse(root.Groups["h"].Value, CultureInfo.InvariantCulture);
        var group = Group.Match(svg);
        var dx = group.Success ? double.Parse(group.Groups["x"].Value, CultureInfo.InvariantCulture) : 0.0;
        var dy = group.Success ? double.Parse(group.Groups["y"].Value, CultureInfo.InvariantCulture) : 0.0;
        Assert.True(dx > 0.0 || dy > 0.0, "the scenario must need a translation");

        var texts = TextElement.Matches(svg).Where(m => m.Groups["text"].Value is not ("A" or "port : Type")).ToList();
        Assert.NotEmpty(texts);
        foreach (var text in texts)
        {
            var size = double.Parse(text.Groups["size"].Value, CultureInfo.InvariantCulture);
            var textWidth = ConnectorLabelPlacer.EstimateLabelWidth(System.Net.WebUtility.HtmlDecode(text.Groups["text"].Value), size);
            var x = double.Parse(text.Groups["x"].Value, CultureInfo.InvariantCulture) + dx;
            var y = double.Parse(text.Groups["y"].Value, CultureInfo.InvariantCulture) + dy;
            var left = text.Groups["anchor"].Value switch
            {
                "middle" => x - (textWidth / 2.0),
                "end" => x - textWidth,
                _ => x,
            };
            Assert.True(left >= -Tolerance, $"text '{text.Groups["text"].Value}' starts at {left:F1}");
            Assert.True(left + textWidth <= width + Tolerance, $"text '{text.Groups["text"].Value}' ends at {left + textWidth:F1} of {width:F1}");
            Assert.True(y - (size / 2.0) >= -Tolerance, $"text '{text.Groups["text"].Value}' is above the canvas");
            Assert.True(y + (size / 2.0) <= height + Tolerance, $"text '{text.Groups["text"].Value}' is below the canvas");
        }

        var port = Ports(tree).Single();
        var glyph = NotationMetrics.PortGlyphBounds(port);
        var label = NotationMetrics.PortOutwardLabelBounds(port, port.ExternalLabel!, LargeTheme);
        Assert.True(glyph.X - glyph.HalfWidth + dx >= -Tolerance && glyph.Y - glyph.HalfHeight + dy >= -Tolerance);
        Assert.True(label.X - label.HalfWidth + dx >= -Tolerance && label.Y - label.HalfHeight + dy >= -Tolerance);
        Assert.True(label.X + label.HalfWidth + dx <= width + Tolerance && label.Y + label.HalfHeight + dy <= height + Tolerance);
    }

    /// <summary>
    ///     The PNG of a tree whose labels, port label and markers hang past the origin shows exactly the
    ///     same ink (same bounding-box size) as the PNG of the same tree moved well inside the canvas, so
    ///     nothing is clipped at the left or top edge.
    /// </summary>
    [Fact]
    public void RenderedPng_LargeTheme_LabelsAtLeftAndTopEdge_AreNotClipped()
    {
        // Arrange: the same content, once against the origin and once moved 300 px inside.
        var edge = EdgeTree(0.0);
        var inside = EdgeTree(300.0);

        // Act
        using var edgeSurface = RenderPng(edge, LargeTheme);
        using var insideSurface = RenderPng(inside, LargeTheme);

        // Assert
        var edgeInk = InkBounds(edgeSurface);
        var insideInk = InkBounds(insideSurface);
        Assert.Equal(insideInk.MaxX - insideInk.MinX, edgeInk.MaxX - edgeInk.MinX, 1.5);
        Assert.Equal(insideInk.MaxY - insideInk.MinY, edgeInk.MaxY - edgeInk.MinY, 1.5);
        Assert.True(edgeInk.MinX >= 0 && edgeInk.MinY >= 0);
    }

    /// <summary>
    ///     With the default theme a tree whose content is inside its canvas renders without any translation
    ///     group, so existing output is unchanged.
    /// </summary>
    [Fact]
    public void RenderedSvg_DefaultTheme_ContentInsideCanvas_HasNoTranslationGroup()
    {
        // Arrange
        var tree = EdgeTree(300.0);

        // Act
        var svg = RenderSvg(tree, Themes.Light);

        // Assert
        Assert.DoesNotContain("<g transform=\"translate(", svg, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A hand-built tree with content against the left and top edges: a vertical dashed connector with a
    ///     keyword midpoint label and an arrowhead, a horizontal connector with end labels above its end, and an
    ///     outward-labeled port on the left face of a box.
    /// </summary>
    /// <param name="offset">Distance, in logical pixels, the whole content is moved right and down.</param>
    private static LayoutTree EdgeTree(double offset)
    {
        var box = new LayoutBox(offset + 60, offset + 40, 100, 60, "A", 0, BoxShape.Rectangle, [], []);
        var vertical = new LayoutLine(
            [new Point2D(offset + 8, offset + 30), new Point2D(offset + 8, offset + 250)],
            EndMarkerStyle.None,
            EndMarkerStyle.OpenChevron,
            LineStyle.Dashed,
            "«import»*");
        var horizontal = new LayoutLine(
            [new Point2D(offset + 100, offset + 6), new Point2D(offset + 300, offset + 6)],
            EndMarkerStyle.None,
            EndMarkerStyle.OpenChevron,
            LineStyle.Solid,
            null,
            new LineEndLabel("role", "1"),
            new LineEndLabel("target", "0..*"));
        var port = new LayoutPort(
            offset + 60,
            offset + 70,
            PortSide.Left,
            "port : Type",
            Appearance: new PortAppearance(PortDirection.In, LabelPlacement: PortLabelPlacement.Outward));
        return new LayoutTree(offset + 400, offset + 300, [box, vertical, horizontal, port]);
    }

    private static List<LayoutPort> Ports(LayoutTree tree) => [.. tree.Nodes.OfType<LayoutPort>()];

    private static string RenderSvg(LayoutTree tree, Theme theme)
    {
        using var output = new MemoryStream();
        new SvgRenderer().Render(tree, new RenderOptions(theme), output);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static Surface RenderPng(LayoutTree tree, Theme theme)
    {
        using var stream = new MemoryStream();
        new PngRenderer().Render(tree, new RenderOptions(theme), stream);
        stream.Position = 0;
        return PngCodec.Load(stream);
    }

    private static (int MinX, int MinY, int MaxX, int MaxY) InkBounds(Surface surface)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                var color = surface[x, y];
                if (color.A > 32 && (color.R + color.G + color.B) < 600)
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
}
