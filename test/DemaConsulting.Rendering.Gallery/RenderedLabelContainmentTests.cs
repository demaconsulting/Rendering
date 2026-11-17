// <copyright file="RenderedLabelContainmentTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text.RegularExpressions;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout;
using DemaConsulting.Rendering.Layout.Tests;
using DemaConsulting.Rendering.Svg;

namespace DemaConsulting.Rendering.Gallery;

/// <summary>
///     Proves that, for a graph with outward-labeled ports and connection end labels, every port glyph, port
///     label, and end label of the <em>rendered</em> SVG lies inside the canvas, and that the end-label
///     positions the layout's canvas fitting measured are the positions the renderer drew. The layout fits
///     the canvas with the same shared label resolver the renderers call, so the two cannot disagree about
///     where an end label goes (for example around a port glyph or an outward port label).
/// </summary>
public sealed class RenderedLabelContainmentTests
{
    /// <summary>Tolerance, in logical pixels, for coordinates written with limited precision.</summary>
    private const double Tolerance = 0.51;

    /// <summary>Matches one SVG <c>text</c> element: x, y, font size, anchor, content.</summary>
    private static readonly Regex TextElement = new(
        """<text x="(?<x>[-0-9.]+)" y="(?<y>[-0-9.]+)"[^>]*font-size="(?<size>[0-9.]+)"[^>]*text-anchor="(?<anchor>\w+)"[^>]*>(?<text>[^<]*)</text>""",
        RegexOptions.CultureInvariant);

    /// <summary>Gets every flow direction as theory data.</summary>
    public static TheoryData<LayoutFlowDirection> Directions =>
        new(DirectionScenes.AllDirections);

    /// <summary>
    ///     Every text drawn in the rendered SVG (port labels, end labels, titles) and every port glyph lies
    ///     inside the SVG canvas, in every flow direction.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [MemberData(nameof(Directions))]
    public void RenderedSvg_OutwardPortLabelsAndEndLabels_AreInsideCanvas(LayoutFlowDirection direction)
    {
        // Arrange
        var tree = LayoutEngine.Layout(GallerySysmlPorts.FuelSystem(direction));

        // Act
        var svg = Render(tree);
        var (width, height) = CanvasSize(svg);

        // Assert: every port label and end label text extent is inside the canvas (box titles are drawn
        // inside their own box and are measured by the box check of the layout tests).
        var titles = AllBoxes(tree).SelectMany(b => new[] { b.Label, b.Keyword }).Where(t => t is not null).ToHashSet();
        var texts = TextElement.Matches(svg).Where(m => !titles.Contains(System.Net.WebUtility.HtmlDecode(m.Groups["text"].Value))).ToList();
        Assert.NotEmpty(texts);
        foreach (var text in texts)
        {
            var size = double.Parse(text.Groups["size"].Value, CultureInfo.InvariantCulture);
            var textWidth = ConnectorLabelPlacer.EstimateLabelWidth(System.Net.WebUtility.HtmlDecode(text.Groups["text"].Value), size);
            var x = double.Parse(text.Groups["x"].Value, CultureInfo.InvariantCulture);
            var y = double.Parse(text.Groups["y"].Value, CultureInfo.InvariantCulture);
            var left = text.Groups["anchor"].Value switch
            {
                "middle" => x - (textWidth / 2.0),
                "end" => x - textWidth,
                _ => x,
            };
            Assert.True(left >= -Tolerance, $"text '{text.Groups["text"].Value}' starts at x={left:F1}, left of the canvas");
            Assert.True(left + textWidth <= width + Tolerance, $"text '{text.Groups["text"].Value}' ends at {left + textWidth:F1}, right of the {width:F1} canvas");
            Assert.True(y - (size / 2.0) >= -Tolerance, $"text '{text.Groups["text"].Value}' is above the canvas");
            Assert.True(y + (size / 2.0) <= height + Tolerance, $"text '{text.Groups["text"].Value}' is below the {height:F1} canvas");
        }

        // Assert: every port glyph and outward port label is inside the canvas.
        foreach (var port in AllPorts(tree))
        {
            AssertInside(NotationMetrics.PortGlyphBounds(port), width, height, "port glyph");
            if (NotationMetrics.IsOutwardLabel(port))
            {
                AssertInside(NotationMetrics.PortOutwardLabelBounds(port, port.ExternalLabel!, Themes.Light), width, height, "outward port label");
            }
        }
    }

    /// <summary>
    ///     The end-label positions the shared resolver predicts for the laid-out tree (what the canvas fitting
    ///     measured) equal the positions of the end-label text elements in the rendered SVG.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [MemberData(nameof(Directions))]
    public void RenderedSvg_EndLabels_AreAtThePositionsTheLayoutPredicted(LayoutFlowDirection direction)
    {
        // Arrange
        var tree = LayoutEngine.Layout(GallerySysmlPorts.FuelSystem(direction));
        var lines = new List<LayoutLine>();
        var boxes = new List<LayoutBox>();
        var ports = new List<LayoutPort>();
        Collect(tree.Nodes, lines, boxes, ports);

        // Act
        var predicted = ConnectorLabelPlacer.ResolveLabels(lines, boxes, ports, Themes.Light).EndLabels;
        var rendered = TextElement.Matches(Render(tree)).ToList();

        // Assert
        Assert.NotEmpty(predicted);
        foreach (var placement in predicted)
        {
            Assert.Contains(
                rendered,
                m => m.Groups["text"].Value == placement.Text
                    && Math.Abs(double.Parse(m.Groups["x"].Value, CultureInfo.InvariantCulture) - placement.X) <= Tolerance
                    && Math.Abs(double.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture) - placement.Y) <= Tolerance);
        }
    }

    private static void AssertInside(LabelPlacement bounds, double width, double height, string what)
    {
        Assert.True(bounds.X - bounds.HalfWidth >= -Tolerance && bounds.Y - bounds.HalfHeight >= -Tolerance, $"{what} lies above or left of the canvas");
        Assert.True(bounds.X + bounds.HalfWidth <= width + Tolerance && bounds.Y + bounds.HalfHeight <= height + Tolerance, $"{what} lies below or right of the {width:F1}x{height:F1} canvas");
    }

    private static string Render(LayoutTree tree)
    {
        using var stream = new MemoryStream();
        new SvgRenderer().Render(tree, new RenderOptions(Themes.Light), stream);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static (double Width, double Height) CanvasSize(string svg)
    {
        var match = Regex.Match(svg, """<svg [^>]*width="(?<w>[0-9.]+)" height="(?<h>[0-9.]+)" """, RegexOptions.CultureInvariant);
        return (
            double.Parse(match.Groups["w"].Value, CultureInfo.InvariantCulture),
            double.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture));
    }

    private static List<LayoutBox> AllBoxes(LayoutTree tree)
    {
        var boxes = new List<LayoutBox>();
        Collect(tree.Nodes, [], boxes, []);
        return boxes;
    }

    private static List<LayoutPort> AllPorts(LayoutTree tree)
    {
        var ports = new List<LayoutPort>();
        Collect(tree.Nodes, [], [], ports);
        return ports;
    }

    private static void Collect(IReadOnlyList<LayoutNode> nodes, List<LayoutLine> lines, List<LayoutBox> boxes, List<LayoutPort> ports)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case LayoutLine line:
                    lines.Add(line);
                    break;
                case LayoutPort port:
                    ports.Add(port);
                    break;
                case LayoutBox box:
                    boxes.Add(box);
                    Collect(box.Children, lines, boxes, ports);
                    break;
            }
        }
    }
}
