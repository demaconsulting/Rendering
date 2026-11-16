// <copyright file="EndMarkerParityTests.cs" company="DemaConsulting">
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
///     Proves that the SVG and PNG (CanvasNet) renderers draw every end marker at the same size and
///     place. The SVG marker definitions are the reference: their geometry is read back from the
///     rendered document and converted to pixels with the line's stroke width (SVG marker units), then
///     compared with the pixel footprint of the same marker rendered by CanvasNet. This lives in the
///     gallery project because it is the one test project that references both renderers.
/// </summary>
public sealed partial class EndMarkerParityTests
{
    /// <summary>Row of the horizontal test line.</summary>
    private const double LineY = 50.0;

    /// <summary>First x of the horizontal test line.</summary>
    private const double LineStartX = 40.0;

    /// <summary>Last x of the horizontal test line.</summary>
    private const double LineEndX = 160.0;

    /// <summary>
    ///     Maximum disagreement, in pixels, between a PNG footprint edge and the SVG marker edge. It covers
    ///     anti-aliasing and the SVG marker-box clip (SVG clips a marker to its box; the raster draws the
    ///     full stroke), which differ by at most the marker stroke half-width.
    /// </summary>
    private const double Tolerance = 1.75;

    /// <summary>The end-marker styles other than <see cref="EndMarkerStyle.None"/>.</summary>
    public static TheoryData<EndMarkerStyle> Styles
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
    ///     For every style at the target end, the PNG marker footprint (its pixels differing from the
    ///     undecorated line) has the same along-line and across-line extents as the SVG marker geometry
    ///     converted to pixels, so both renderers draw the marker at the same size.
    /// </summary>
    /// <param name="style">The end-marker style.</param>
    [Theory]
    [MemberData(nameof(Styles))]
    public void EndMarkerParity_TargetEnd_PngFootprintMatchesSvgGeometry(EndMarkerStyle style)
    {
        var expected = SvgFootprint(style, tipX: LineEndX, direction: 1.0);
        var actual = PngFootprint(style, atTarget: true);

        AssertSameFootprint(style, expected, actual);
    }

    /// <summary>
    ///     For every style at the source end, the PNG footprint is the mirror of the SVG end-marker
    ///     geometry (the glyph points out of the line start), at the same size.
    /// </summary>
    /// <param name="style">The end-marker style.</param>
    [Theory]
    [MemberData(nameof(Styles))]
    public void EndMarkerParity_SourceEnd_PngFootprintMatchesMirroredSvgGeometry(EndMarkerStyle style)
    {
        var expected = SvgFootprint(style, tipX: LineStartX, direction: -1.0);
        var actual = PngFootprint(style, atTarget: false);

        AssertSameFootprint(style, expected, actual);
    }

    /// <summary>
    ///     The marker footprint scales with the theme stroke width in both renderers: a thicker-stroke
    ///     theme grows the PNG footprint and the SVG footprint identically.
    /// </summary>
    [Fact]
    public void EndMarkerParity_ThickerStrokeTheme_FootprintsStillMatch()
    {
        var theme = Themes.Light with { StrokeWidth = 2.5 };

        var expected = SvgFootprint(EndMarkerStyle.HollowTriangleTwoDots, LineEndX, 1.0, theme);
        var actual = PngFootprint(EndMarkerStyle.HollowTriangleTwoDots, atTarget: true, theme);

        // A thicker stroke widens the SVG marker-box clip, which the raster does not apply, by half the
        // marker stroke width; a renderer that ignored the scale would be off by far more than this.
        AssertSameFootprint(EndMarkerStyle.HollowTriangleTwoDots, expected, actual, 1.0 + (NotationMetrics.MarkerStrokeWidth(theme) / 2.0));
    }

    private static void AssertSameFootprint(
        EndMarkerStyle style,
        (double MinX, double MaxX, double MinY, double MaxY) expected,
        (double MinX, double MaxX, double MinY, double MaxY) actual,
        double tolerance = Tolerance)
    {
        var message = $"{style}: SVG {expected} vs PNG {actual}";
        Assert.True(Math.Abs(expected.MinX - actual.MinX) <= tolerance, $"min x: {message}");
        Assert.True(Math.Abs(expected.MaxX - actual.MaxX) <= tolerance, $"max x: {message}");
        Assert.True(Math.Abs(expected.MinY - actual.MinY) <= tolerance, $"min y: {message}");
        Assert.True(Math.Abs(expected.MaxY - actual.MaxY) <= tolerance, $"max y: {message}");
    }

    private static LayoutTree HorizontalLine(EndMarkerStyle style, bool atTarget) =>
        new(
            200,
            100,
            [
                new LayoutLine(
                    [new Point2D(LineStartX, LineY), new Point2D(LineEndX, LineY)],
                    atTarget ? EndMarkerStyle.None : style,
                    atTarget ? style : EndMarkerStyle.None,
                    LineStyle.Solid,
                    null),
            ]);

    /// <summary>
    ///     Returns the pixel footprint of a PNG marker: the bounding box of the pixels that differ between
    ///     the line with the marker and the same line without it.
    /// </summary>
    private static (double MinX, double MaxX, double MinY, double MaxY) PngFootprint(
        EndMarkerStyle style,
        bool atTarget,
        Theme? theme = null)
    {
        var options = new RenderOptions(theme ?? Themes.Light);
        using var with = RenderPng(HorizontalLine(style, atTarget), options);
        using var without = RenderPng(HorizontalLine(EndMarkerStyle.None, atTarget), options);

        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        for (var y = 0; y < 100; y++)
        {
            for (var x = 0; x < 200; x++)
            {
                if (Differs(with[x, y], without[x, y]))
                {
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }
        }

        Assert.True(maxX >= minX, $"{style}: the PNG marker drew no pixels.");
        return (minX, maxX + 1, minY, maxY + 1);
    }

    private static Surface RenderPng(LayoutTree tree, RenderOptions options)
    {
        using var stream = new MemoryStream();
        new PngRenderer().Render(tree, options, stream);
        stream.Position = 0;
        return PngCodec.Load(stream);
    }

    private static bool Differs(Rgba32 a, Rgba32 b) =>
        Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) + Math.Abs(a.A - b.A) > 60;

    /// <summary>
    ///     Returns the visible pixel footprint of an SVG end marker: the bounding box of its geometry
    ///     (including half the stroke width) clipped to its marker box, converted from marker units to
    ///     pixels with the line stroke width, and placed relative to the line endpoint.
    /// </summary>
    private static (double MinX, double MaxX, double MinY, double MaxY) SvgFootprint(
        EndMarkerStyle style,
        double tipX,
        double direction,
        Theme? theme = null)
    {
        theme ??= Themes.Light;
        string svg;
        using (var output = new MemoryStream())
        {
            new SvgRenderer().Render(HorizontalLine(style, atTarget: true), new RenderOptions(theme), output);
            svg = Encoding.UTF8.GetString(output.ToArray());
        }

        // SVG markers scale by the stroke width of the path that references them.
        var pathStroke = Number(Regex.Match(svg, "<path [^>]*stroke-width=\"([^\"]+)\"[^>]*marker-end").Groups[1].Value);

        var id = Regex.Match(svg, "marker-end=\"url\\(#([^)]+)\\)\"").Groups[1].Value;
        var start = svg.IndexOf($"<marker id=\"{id}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{style}: marker '{id}' is not defined.");
        var end = svg.IndexOf("</marker>", start, StringComparison.Ordinal);
        var marker = svg[start..end];

        var header = MarkerHeader().Match(marker);
        var width = Number(header.Groups[1].Value);
        var height = Number(header.Groups[2].Value);
        var refX = Number(header.Groups[3].Value);
        var refY = Number(header.Groups[4].Value);

        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        void Include(double x0, double x1, double y0, double y1)
        {
            minX = Math.Min(minX, x0);
            maxX = Math.Max(maxX, x1);
            minY = Math.Min(minY, y0);
            maxY = Math.Max(maxY, y1);
        }

        foreach (Match element in Element().Matches(marker))
        {
            var tag = element.Groups[1].Value;
            var attributes = element.Groups[2].Value;
            var half = AttributeOrNull(attributes, "stroke-width") is { } sw ? Number(sw) / 2.0 : 0.0;
            switch (tag)
            {
                case "polygon":
                case "polyline":
                    var points = Attribute(attributes, "points").Split(',', StringSplitOptions.TrimEntries)
                        .Select(pair => pair.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        .Select(parts => (X: Number(parts[0]), Y: Number(parts[1])))
                        .ToList();
                    Include(points.Min(p => p.X) - half, points.Max(p => p.X) + half, points.Min(p => p.Y) - half, points.Max(p => p.Y) + half);
                    break;

                case "circle":
                    var cx = Number(Attribute(attributes, "cx"));
                    var cy = Number(Attribute(attributes, "cy"));
                    var r = Number(Attribute(attributes, "r")) + half;
                    Include(cx - r, cx + r, cy - r, cy + r);
                    break;

                case "line":
                    var x1 = Number(Attribute(attributes, "x1"));
                    var x2 = Number(Attribute(attributes, "x2"));
                    var y1 = Number(Attribute(attributes, "y1"));
                    var y2 = Number(Attribute(attributes, "y2"));

                    // Butt caps: the stroke widens the line only perpendicular to its direction.
                    var padX = Math.Abs(x2 - x1) < 1e-9 ? half : 0.0;
                    var padY = Math.Abs(y2 - y1) < 1e-9 ? half : 0.0;
                    Include(Math.Min(x1, x2) - padX, Math.Max(x1, x2) + padX, Math.Min(y1, y2) - padY, Math.Max(y1, y2) + padY);
                    break;
            }
        }

        // The browser clips a marker to its box.
        minX = Math.Max(minX, 0.0);
        maxX = Math.Min(maxX, width);
        minY = Math.Max(minY, 0.0);
        maxY = Math.Min(maxY, height);

        // Convert marker units to pixels; the endpoint is the anchor, and the source end mirrors the glyph.
        double AlongPx(double x) => tipX + (direction * (x - refX) * pathStroke);
        var alongA = AlongPx(minX);
        var alongB = AlongPx(maxX);
        return (
            Math.Min(alongA, alongB),
            Math.Max(alongA, alongB),
            LineY + ((minY - refY) * pathStroke),
            LineY + ((maxY - refY) * pathStroke));
    }

    private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

    private static string Attribute(string attributes, string name) =>
        AttributeOrNull(attributes, name) ?? throw new InvalidOperationException($"Missing attribute '{name}'.");

    private static string? AttributeOrNull(string attributes, string name)
    {
        var match = Regex.Match(attributes, $"(?:^|\\s){Regex.Escape(name)}=\"([^\"]*)\"");
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex("markerWidth=\"([^\"]+)\" markerHeight=\"([^\"]+)\" refX=\"([^\"]+)\" refY=\"([^\"]+)\"")]
    private static partial Regex MarkerHeader();

    [GeneratedRegex("<(polygon|polyline|circle|line) ([^>]*?)/>")]
    private static partial Regex Element();
}
