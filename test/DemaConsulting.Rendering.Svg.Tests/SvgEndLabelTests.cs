// <copyright file="SvgEndLabelTests.cs" company="DemaConsulting">
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
///     Tests for the SVG rendering of connection end labels: one text element per non-empty slot, drawn at
///     the position <see cref="ConnectorLabelPlacer.PlaceEndLabels"/> chose, with the canvas grown to
///     include every label.
/// </summary>
public sealed class SvgEndLabelTests
{
    private static string Render(LayoutTree tree, double scale = 1.0)
    {
        using var output = new MemoryStream();
        new SvgRenderer().Render(tree, new RenderOptions(Themes.Light, scale), output);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static string F(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private static LayoutLine Line(LineEndLabel? source, LineEndLabel? target, double x1 = 300) =>
        new([new Point2D(100, 60), new Point2D(x1, 60)], EndMarkerStyle.None, EndMarkerStyle.FilledArrow, LineStyle.Solid, null, source, target);

    private static LayoutTree Tree(LayoutLine line, double width = 400) =>
        new(width, 120, [new LayoutBox(20, 30, 80, 60, "A", 0, BoxShape.Rectangle, [], []), line]);

    private static (string X, string Y) TextPosition(string svg, string text)
    {
        var match = Regex.Match(svg, $"<text x=\"([0-9.\\-]+)\" y=\"([0-9.\\-]+)\"[^>]*>{Regex.Escape(text)}</text>");
        Assert.True(match.Success, $"Expected a <text> element containing '{text}'.");
        return (match.Groups[1].Value, match.Groups[2].Value);
    }

    private static IReadOnlyList<EndLabelPlacement> Expected(LayoutTree tree) =>
        ConnectorLabelPlacer.PlaceEndLabels(
            tree.Nodes.OfType<LayoutLine>(),
            Themes.Light.FontSizeBody,
            tree.Nodes.OfType<LayoutBox>(),
            NotationMetrics.MarkerScale(Themes.Light));

    /// <summary>Every non-empty slot is drawn as a text element at the placer's position.</summary>
    [Fact]
    public void SvgRenderer_EndLabels_DrawnAtThePlacersPositions()
    {
        // Arrange
        var tree = Tree(Line(new LineEndLabel("owner", "1"), new LineEndLabel("parts", "0..*")));

        // Act
        var svg = Render(tree);

        // Assert
        var expected = Expected(tree);
        Assert.Equal(4, expected.Count);
        foreach (var placement in expected)
        {
            var (x, y) = TextPosition(svg, placement.Text);
            Assert.Equal(F(placement.X), x);
            Assert.Equal(F(placement.Y), y);
        }
    }

    /// <summary>
    ///     End labels use the connector midpoint label's text style (font, fill, centering) but no background
    ///     filter: they sit beside their line, so a background could only erase a neighboring connector.
    /// </summary>
    [Fact]
    public void SvgRenderer_EndLabels_UseTheMidpointLabelFontWithoutABackground()
    {
        // Arrange
        var tree = Tree(Line(new LineEndLabel("owner"), null));

        // Act
        var svg = Render(tree);

        // Assert
        var text = Regex.Match(svg, "<text [^>]*>owner</text>").Value;
        Assert.Contains("text-anchor=\"middle\"", text, StringComparison.Ordinal);
        Assert.Contains("dominant-baseline=\"middle\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("filter=", text, StringComparison.Ordinal);
        Assert.Contains($"font-size=\"{F(Themes.Light.FontSizeBody)}\"", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     An end label at a marker end on a port is placed from the marker's retracted (painted) tip, not
    ///     the port centre: the renderer passes its ports to the placer, and the text keeps at least 4 px
    ///     from the retracted marker.
    /// </summary>
    [Fact]
    public void SvgRenderer_PortEnd_LabelClearOfRetractedMarker()
    {
        // Arrange: a line arriving at a port on the right face of box A, with a target end label.
        var appearance = new PortAppearance(PortDirection.In);
        var port = new LayoutPort(100, 60, PortSide.Right, null, Appearance: appearance);
        var line = new LayoutLine(
            [new Point2D(300, 60), new Point2D(100, 60)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            null,
            null,
            new LineEndLabel("feeds", "0..*"));
        var tree = new LayoutTree(400, 120, [new LayoutBox(20, 30, 80, 60, "A", 0, BoxShape.Rectangle, [], []), port, line]);

        // Act
        var svg = Render(tree);

        // Assert
        var boxes = tree.Nodes.OfType<LayoutBox>();
        var scale = NotationMetrics.MarkerScale(Themes.Light);
        var withPorts = ConnectorLabelPlacer.PlaceEndLabels([line], Themes.Light.FontSizeBody, boxes, scale, null, [port]);
        var without = ConnectorLabelPlacer.PlaceEndLabels([line], Themes.Light.FontSizeBody, boxes, scale);
        Assert.Equal(2, withPorts.Count);
        var markerEnd = 100 + NotationMetrics.PortGlyphHalfSizeFor(appearance) + (NotationMetrics.AlongLineLength(EndMarkerStyle.FilledArrow) * scale);
        foreach (var placement in withPorts)
        {
            var (x, y) = TextPosition(svg, placement.Text);
            Assert.Equal(F(placement.X), x);
            Assert.Equal(F(placement.Y), y);
            Assert.True(placement.X - placement.HalfWidth + 2.0 - markerEnd >= 4.0 - 1e-6, $"'{placement.Text}' is too close to the retracted marker");
            Assert.True(placement.X > without.Single(w => w.Text == placement.Text).X, "the port retraction must shift the label outward");
        }
    }

    /// <summary>Null and empty slots produce no text element.</summary>
    [Fact]
    public void SvgRenderer_NullOrEmptyEndLabelSlots_ProduceNoText()
    {
        // Arrange
        var tree = Tree(Line(new LineEndLabel(null, string.Empty), null));

        // Act
        var svg = Render(tree);

        // Assert: the only text is the box label.
        Assert.Single(Regex.Matches(svg, "<text "));
    }

    /// <summary>A line with no end labels renders no text at all: default output is unchanged.</summary>
    [Fact]
    public void SvgRenderer_LineWithoutEndLabels_ProducesNoLabelText()
    {
        // Act
        var svg = Render(Tree(Line(null, null)));

        // Assert: the only text is the box label.
        Assert.Single(Regex.Matches(svg, "<text "));
    }

    /// <summary>Label text is XML-escaped.</summary>
    [Fact]
    public void SvgRenderer_EndLabelText_IsXmlEscaped()
    {
        // Act
        var svg = Render(Tree(Line(new LineEndLabel("a<b & c", null), null)));

        // Assert
        Assert.Contains(">a&lt;b &amp; c</text>", svg, StringComparison.Ordinal);
    }

    /// <summary>The canvas grows to include a label that extends past the declared size.</summary>
    [Fact]
    public void SvgRenderer_EndLabelBeyondTheCanvas_GrowsTheCanvas()
    {
        // Arrange: a tall vertical line near the right edge with a wide label to its right.
        var line = new LayoutLine(
            [new Point2D(195, 20), new Point2D(195, 100)],
            EndMarkerStyle.None,
            EndMarkerStyle.None,
            LineStyle.Solid,
            null,
            new LineEndLabel("a long role name", null),
            null);
        var tree = new LayoutTree(200, 120, [line]);

        // Act
        var svg = Render(tree);

        // Assert
        var placement = Assert.Single(Expected(tree));
        var width = double.Parse(Regex.Match(svg, "<svg [^>]*width=\"([0-9.]+)\"").Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.True(placement.X + placement.HalfWidth > 200, "the label must extend past the declared width");
        Assert.True(width >= placement.X + placement.HalfWidth - 0.01, $"canvas width {width} must include the label");
    }

    /// <summary>The render scale applies to end-label positions and font size.</summary>
    [Fact]
    public void SvgRenderer_EndLabels_Scaled_ScalePositionAndFontSize()
    {
        // Arrange
        var tree = Tree(Line(new LineEndLabel("owner"), null));
        var placement = Assert.Single(Expected(tree));

        // Act
        var svg = Render(tree, 2.0);

        // Assert
        var (x, y) = TextPosition(svg, "owner");
        Assert.Equal(F(placement.X * 2.0), x);
        Assert.Equal(F(placement.Y * 2.0), y);
        Assert.Contains($"font-size=\"{F(Themes.Light.FontSizeBody * 2.0)}\"", Regex.Match(svg, "<text [^>]*>owner</text>").Value, StringComparison.Ordinal);
    }
}
