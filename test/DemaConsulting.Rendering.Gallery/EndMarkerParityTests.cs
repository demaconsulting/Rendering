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

    /// <summary>
    ///     For every style, at both line ends, in the default and a thicker-stroke theme, the STROKED glyph tip
    ///     lands on the line endpoint in BOTH renderers: the SVG marker's painted tip (computed from the
    ///     marker definition) is exactly on the endpoint and the PNG's painted tip (probed from pixels) is on
    ///     it within a pixel, so the two agree with each other. This is the cross-renderer regression test
    ///     for demaconsulting/Rendering#49: a tip past the endpoint pokes into the target box.
    /// </summary>
    /// <param name="style">The end-marker style.</param>
    /// <param name="atTarget">Whether the marker is at the target (end) or source (start) of the line.</param>
    /// <param name="thick">Whether to use a thicker-stroke theme.</param>
    [Theory]
    [MemberData(nameof(StyleEndTheme))]
    public void EndMarkerParity_TipLandsOnEndpoint_InBothRenderers(EndMarkerStyle style, bool atTarget, bool thick)
    {
        var theme = thick ? Themes.Light with { StrokeWidth = 2.5 } : Themes.Light;
        var endpoint = atTarget ? LineEndX : LineStartX;
        var direction = atTarget ? 1.0 : -1.0;

        var svgTip = SvgPaintedTip(style, atTarget, endpoint, direction, theme);
        var pngTip = PngPaintedTip(style, atTarget, theme);

        Assert.True(Math.Abs(svgTip - endpoint) < 1e-3, $"{style}: SVG painted tip {svgTip} must be on the endpoint {endpoint}.");
        Assert.True(Math.Abs(pngTip - endpoint) <= 1.0, $"{style}: PNG painted tip {pngTip} must be on the endpoint {endpoint} (within a pixel).");
        Assert.True(Math.Abs(svgTip - pngTip) <= 1.0, $"{style}: SVG tip {svgTip} and PNG tip {pngTip} must agree.");
    }

    /// <summary>Theory data: every style at both ends in both themes.</summary>
    public static TheoryData<EndMarkerStyle, bool, bool> StyleEndTheme
    {
        get
        {
            var data = new TheoryData<EndMarkerStyle, bool, bool>();
            foreach (var style in Enum.GetValues<EndMarkerStyle>().Where(style => style != EndMarkerStyle.None))
            {
                foreach (var atTarget in new[] { true, false })
                {
                    foreach (var thick in new[] { false, true })
                    {
                        data.Add(style, atTarget, thick);
                    }
                }
            }

            return data;
        }
    }

    /// <summary>
    ///     A marker end on a port is shortened identically by both renderers: the SVG path ends at the port's
    ///     outer edge and the PNG arrow base sits one marker length behind that same edge, so the arrowhead
    ///     is not hidden under the port glyph in either output.
    /// </summary>
    [Fact]
    public void EndMarkerParity_PortEnd_ShortenedIdenticallyInBothRenderers()
    {
        var theme = Themes.Light;
        var port = new LayoutPort(LineEndX, LineY, PortSide.Left, null);
        var line = new LayoutLine(
            [new Point2D(LineStartX, LineY), new Point2D(LineEndX, LineY)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            null);
        var tree = new LayoutTree(200, 100, [line, port]);

        string svg;
        using (var output = new MemoryStream())
        {
            new SvgRenderer().Render(tree, new RenderOptions(theme), output);
            svg = Encoding.UTF8.GetString(output.ToArray());
        }

        var pathEnd = Number(Regex.Match(svg, "<path d=\"M [\\d.]+ [\\d.]+ L ([\\d.]+) [\\d.]+\"").Groups[1].Value);
        Assert.Equal(LineEndX - NotationMetrics.PortHalfSize, pathEnd, 1e-6);

        // The arrow base is one marker anchor behind the shortened endpoint in the SVG...
        var (_, refX) = NotationMetrics.MarkerAnchor(EndMarkerStyle.FilledArrow, theme);
        var baseX = pathEnd - (refX * NotationMetrics.MarkerScale(theme));

        // ...and the PNG draws the base ink at that same column (the wide base corners just above the line).
        using var surface = RenderPng(tree, new RenderOptions(theme));
        var column = (int)Math.Round(baseX);
        var darkest = 0.0;
        for (var y = 45; y <= 46; y++)
        {
            darkest = Math.Max(darkest, (255.0 - surface[column, y].R) / (255.0 - 0x1A));
        }

        Assert.True(darkest >= 0.5, $"Expected PNG arrow-base ink at column {column} (SVG base x {baseX}).");
    }

    /// <summary>Returns the x, in pixels, of the SVG marker's painted tip (the stroked glyph's far point).</summary>
    private static double SvgPaintedTip(EndMarkerStyle style, bool atTarget, double endpoint, double direction, Theme theme)
    {
        string svg;
        using (var output = new MemoryStream())
        {
            new SvgRenderer().Render(HorizontalLine(style, atTarget), new RenderOptions(theme), output);
            svg = Encoding.UTF8.GetString(output.ToArray());
        }

        var pathStroke = Number(Regex.Match(svg, "<path [^>]*stroke-width=\"([^\"]+)\"[^>]*marker-(?:start|end)").Groups[1].Value);
        var id = Regex.Match(svg, "marker-(?:start|end)=\"url\\(#([^)]+)\\)\"").Groups[1].Value;
        var start = svg.IndexOf($"<marker id=\"{id}\"", StringComparison.Ordinal);
        var marker = svg[start..svg.IndexOf("</marker>", start, StringComparison.Ordinal)];

        var header = MarkerHeader().Match(marker);
        var width = Number(header.Groups[1].Value);
        var anchor = Number(header.Groups[3].Value);

        // The start variant mirrors the glyph about the box, so measure its anchor from the far side.
        var tipAnchor = atTarget ? anchor : width - anchor;
        var paintedMax = double.MinValue;
        foreach (Match element in Element().Matches(marker))
        {
            paintedMax = Math.Max(paintedMax, PaintedMaxX(element.Groups[1].Value, element.Groups[2].Value));
        }

        // Clip to the marker box exactly as a browser does, then convert marker units to pixels.
        paintedMax = Math.Min(paintedMax, width);
        return endpoint + (direction * (paintedMax - tipAnchor) * pathStroke);
    }

    /// <summary>
    ///     Returns the x, in pixels, of the PNG marker's painted tip: the outer edge of the last column
    ///     (toward the line's free end) that carries at least half the stroke color's darkness on the axis band.
    /// </summary>
    private static double PngPaintedTip(EndMarkerStyle style, bool atTarget, Theme theme)
    {
        using var surface = RenderPng(HorizontalLine(style, atTarget), new RenderOptions(theme));
        var tip = atTarget ? double.MinValue : double.MaxValue;
        for (var x = 0; x < 200; x++)
        {
            for (var y = 20; y < 80; y++)
            {
                if ((255.0 - surface[x, y].R) / (255.0 - 0x1A) < 0.5)
                {
                    continue;
                }

                // A target tip is the right edge of the right-most inked column; a source tip the left edge of the left-most.
                tip = atTarget ? Math.Max(tip, x + 1.0) : Math.Min(tip, x);
            }
        }

        return tip;
    }

    /// <summary>
    ///     Returns the largest marker-box x any painted pixel of the element can reach: the union of the
    ///     stroked segments' rectangles (exact for bevel joins and butt caps), or the circle radius plus the
    ///     half stroke.
    /// </summary>
    private static double PaintedMaxX(string tag, string attributes)
    {
        var strokeWidth = AttributeOrNull(attributes, "stroke-width");
        var half = strokeWidth is null ? 0.0 : Number(strokeWidth) / 2.0;
        if (tag == "circle")
        {
            return Number(Attribute(attributes, "cx")) + Number(Attribute(attributes, "r")) + half;
        }

        List<(double X, double Y)> points;
        if (tag == "line")
        {
            points =
            [
                (Number(Attribute(attributes, "x1")), Number(Attribute(attributes, "y1"))),
                (Number(Attribute(attributes, "x2")), Number(Attribute(attributes, "y2"))),
            ];
        }
        else
        {
            points = Attribute(attributes, "points").Split(',', StringSplitOptions.TrimEntries)
                .Select(pair => pair.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Select(parts => (X: Number(parts[0]), Y: Number(parts[1])))
                .ToList();
        }

        var closed = tag == "polygon";
        var max = double.MinValue;
        var segmentCount = closed ? points.Count : points.Count - 1;
        for (var i = 0; i < segmentCount; i++)
        {
            var (x0, y0) = points[i];
            var (x1, y1) = points[(i + 1) % points.Count];
            var length = Math.Sqrt(((x1 - x0) * (x1 - x0)) + ((y1 - y0) * (y1 - y0)));
            var normalX = Math.Abs((y1 - y0) / length);
            max = Math.Max(max, Math.Max(x0, x1) + (normalX * half));
        }

        return max;
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
