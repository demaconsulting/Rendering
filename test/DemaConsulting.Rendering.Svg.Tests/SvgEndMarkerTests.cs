// <copyright file="SvgEndMarkerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Text;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Svg;

namespace DemaConsulting.Rendering.Svg.Tests;

/// <summary>
///     Tests for the SVG line-end (connector decoration) markers: open chevron is drawn OPEN, the
///     closed shapes stay closed, and every marker dimension is derived from <see cref="NotationMetrics"/>
///     (so SVG and PNG share one size source).
/// </summary>
public sealed class SvgEndMarkerTests
{
    private static string RenderLine(EndMarkerStyle source, EndMarkerStyle target, Theme? theme = null)
    {
        var line = new LayoutLine(
            [new Point2D(10, 10), new Point2D(90, 10)],
            source,
            target,
            LineStyle.Solid,
            null);
        var layout = new LayoutTree(200, 100, [line]);
        var options = new RenderOptions(theme ?? Themes.Light);
        return RenderToString(layout, options);
    }

    private static string RenderToString(LayoutTree layout, RenderOptions options)
    {
        var renderer = new SvgRenderer();
        using var output = new MemoryStream();
        renderer.Render(layout, options, output);
        output.Position = 0;
        using var reader = new StreamReader(output, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
        return reader.ReadToEnd();
    }

    /// <summary>The open-chevron marker is defined as an OPEN <c>&lt;polyline&gt;</c>, not a closed polygon.</summary>
    [Fact]
    public void OpenChevron_IsDefinedAsPolyline()
    {
        var svg = RenderLine(EndMarkerStyle.None, EndMarkerStyle.OpenChevron);

        // The marker definition immediately following the open-chevron id must be a polyline (open),
        // never a polygon (closed).
        var markerIndex = svg.IndexOf("id=\"line-end-open-chevron\"", StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, "Expected the open-chevron marker definition to be present.");

        var markerEnd = svg.IndexOf("</marker>", markerIndex, StringComparison.Ordinal);
        var markerBody = svg[markerIndex..markerEnd];
        Assert.Contains("<polyline", markerBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<polygon", markerBody, StringComparison.Ordinal);
    }

    /// <summary>The hollow-triangle marker stays a CLOSED <c>&lt;polygon&gt;</c>.</summary>
    [Fact]
    public void HollowTriangle_IsDefinedAsClosedPolygon()
    {
        var svg = RenderLine(EndMarkerStyle.None, EndMarkerStyle.HollowTriangle);

        var markerIndex = svg.IndexOf("id=\"line-end-hollow-triangle\"", StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, "Expected the hollow-triangle marker definition to be present.");

        var markerEnd = svg.IndexOf("</marker>", markerIndex, StringComparison.Ordinal);
        var markerBody = svg[markerIndex..markerEnd];
        Assert.Contains("<polygon", markerBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<polyline", markerBody, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The triangle marker box derives from the canonical <see cref="NotationMetrics"/> values
    ///     (10 x 7, apex on the tip anchor) plus the tip retraction, which moves the tip anchor back so the
    ///     STROKED tip (not the geometric apex) lands on the endpoint.
    /// </summary>
    [Fact]
    public void TriangleMarker_DimensionsDeriveFromNotationMetrics()
    {
        var svg = RenderLine(EndMarkerStyle.None, EndMarkerStyle.HollowTriangle);

        var (length, refX) = NotationMetrics.MarkerAnchor(EndMarkerStyle.HollowTriangle, Themes.Light);
        var expected =
            $"markerWidth=\"{Num(length)}\" " +
            $"markerHeight=\"{Num(NotationMetrics.EndMarkerWidth)}\" " +
            $"refX=\"{Num(refX)}\" " +
            $"refY=\"{Num(NotationMetrics.EndMarkerHalfWidth)}\"";
        Assert.Contains(expected, svg, StringComparison.Ordinal);

        // The anchor is the glyph apex (10) plus the retraction, never the old overshooting 9.
        Assert.Equal(NotationMetrics.EndMarkerRefX + NotationMetrics.TipRetraction(EndMarkerStyle.HollowTriangle, Themes.Light), refX, 1e-9);

        // The polygon points are the box mapping of the shared triangle vertices.
        Assert.Contains("points=\"0 0, 10 3.5, 0 7\"", svg, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The diamond marker box derives from the canonical <see cref="NotationMetrics"/> values
    ///     (14 x 8, far vertex at 13) plus the tip retraction, and its points are the shared diamond vertices.
    /// </summary>
    [Fact]
    public void DiamondMarker_DimensionsDeriveFromNotationMetrics()
    {
        var svg = RenderLine(EndMarkerStyle.None, EndMarkerStyle.HollowDiamond);

        var (length, refX) = NotationMetrics.MarkerAnchor(EndMarkerStyle.HollowDiamond, Themes.Light);
        var expected =
            $"markerWidth=\"{Num(length)}\" " +
            $"markerHeight=\"{Num(NotationMetrics.DiamondWidth)}\" " +
            $"refX=\"{Num(refX)}\" " +
            $"refY=\"{Num(NotationMetrics.DiamondHalfWidth)}\"";
        Assert.Contains(expected, svg, StringComparison.Ordinal);
        Assert.Contains("points=\"1 4, 7 0, 13 4, 7 8\"", svg, StringComparison.Ordinal);
    }

    /// <summary>All end-marker styles, for the painted-tip theories.</summary>
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
    ///     For every style, at the line end AND at the line start, the STROKED glyph tip (computed from the
    ///     marker definition with an independent stroke-geometry helper: segment rectangles for bevel-joined
    ///     polygons and butt-capped lines, the stroke half-width beyond a circle radius) lands exactly on the
    ///     marker's tip anchor, which is the line endpoint, and is not clipped by the marker box. This is
    ///     the regression test for demaconsulting/Rendering#49 (tips overlapping the target border).
    /// </summary>
    /// <param name="style">The end-marker style.</param>
    [Theory]
    [MemberData(nameof(AllStyles))]
    public void Marker_PaintedTip_LandsOnTipAnchor_AtBothEnds(EndMarkerStyle style)
    {
        AssertPaintedTipOnAnchor(RenderLine(EndMarkerStyle.None, style), "line-end-", style);
        AssertPaintedTipOnAnchor(RenderLine(style, EndMarkerStyle.None), "line-start-", style);
    }

    /// <summary>
    ///     The same painted-tip landing holds for a thicker-stroke theme (the retraction scales with the stroke width).
    /// </summary>
    /// <param name="style">The end-marker style.</param>
    [Theory]
    [MemberData(nameof(AllStyles))]
    public void Marker_PaintedTip_LandsOnTipAnchor_WithThickerStroke(EndMarkerStyle style)
    {
        var theme = Themes.Light with { StrokeWidth = 2.5 };
        AssertPaintedTipOnAnchor(RenderLine(EndMarkerStyle.None, style, theme), "line-end-", style);
        AssertPaintedTipOnAnchor(RenderLine(style, EndMarkerStyle.None, theme), "line-start-", style);
    }

    /// <summary>
    ///     Every polygon and polyline marker element joins with a bevel (matching the raster renderer), so
    ///     the painted tip is the bevel corner, not a miter spike.
    /// </summary>
    /// <param name="style">The end-marker style.</param>
    [Theory]
    [MemberData(nameof(AllStyles))]
    public void Marker_PolygonsAndPolylines_UseBevelJoin(EndMarkerStyle style)
    {
        var svg = RenderLine(EndMarkerStyle.None, style);
        var marker = MarkerDefinition(svg, "line-end-");
        foreach (System.Text.RegularExpressions.Match element in System.Text.RegularExpressions.Regex.Matches(marker, "<(polygon|polyline) [^>]*/>"))
        {
            Assert.Contains("stroke-linejoin=\"bevel\"", element.Value, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     A marker end that sits on a port glyph is shortened by the port half size, so the painted tip lands
    ///     on the port square's outer edge instead of being hidden under the glyph.
    /// </summary>
    [Fact]
    public void LineEndingAtPort_TargetMarker_IsShortenedToPortEdge()
    {
        var port = new LayoutPort(150, 50, PortSide.Left, null);
        var line = new LayoutLine([new Point2D(10, 50), new Point2D(150, 50)], EndMarkerStyle.None, EndMarkerStyle.FilledArrow, LineStyle.Solid, null);
        var svg = RenderToString(new LayoutTree(300, 100, [port, line]), new RenderOptions(Themes.Light));

        Assert.Contains($"d=\"M 10.00 50.00 L {P(150 - NotationMetrics.PortHalfSize)} 50.00\"", svg, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A marker at the START of a line that begins on a port is shortened the same way.
    /// </summary>
    [Fact]
    public void LineStartingAtPort_SourceMarker_IsShortenedToPortEdge()
    {
        var port = new LayoutPort(150, 50, PortSide.Right, null);
        var line = new LayoutLine([new Point2D(150, 50), new Point2D(250, 50)], EndMarkerStyle.HollowDiamond, EndMarkerStyle.None, LineStyle.Solid, null);
        var svg = RenderToString(new LayoutTree(300, 100, [port, line]), new RenderOptions(Themes.Light));

        Assert.Contains($"d=\"M {P(150 + NotationMetrics.PortHalfSize)} 50.00 L 250.00 50.00\"", svg, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A line end with no marker, or whose marker end is not on a port, keeps its original endpoint.
    /// </summary>
    [Fact]
    public void LineEnd_WithoutMarkerOrPort_IsNotShortened()
    {
        var port = new LayoutPort(150, 50, PortSide.Left, null);
        var noMarker = new LayoutLine([new Point2D(10, 50), new Point2D(150, 50)], EndMarkerStyle.None, EndMarkerStyle.None, LineStyle.Solid, null);
        var elsewhere = new LayoutLine([new Point2D(10, 80), new Point2D(150, 80)], EndMarkerStyle.None, EndMarkerStyle.FilledArrow, LineStyle.Solid, null);
        var svg = RenderToString(new LayoutTree(300, 100, [port, noMarker, elsewhere]), new RenderOptions(Themes.Light));

        Assert.Contains("d=\"M 10.00 50.00 L 150.00 50.00\"", svg, StringComparison.Ordinal);
        Assert.Contains("d=\"M 10.00 80.00 L 150.00 80.00\"", svg, StringComparison.Ordinal);
    }

    private static string P(double value) => value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

    private static string MarkerDefinition(string svg, string idPrefix)
    {
        var id = System.Text.RegularExpressions.Regex.Match(svg, "marker-(?:start|end)=\"url\\(#(" + idPrefix + "[^)]+)\\)\"").Groups[1].Value;
        var start = svg.IndexOf($"<marker id=\"{id}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"marker '{id}' is not defined");
        return svg[start..svg.IndexOf("</marker>", start, StringComparison.Ordinal)];
    }

    private static void AssertPaintedTipOnAnchor(string svg, string idPrefix, EndMarkerStyle style)
    {
        var marker = MarkerDefinition(svg, idPrefix);
        var header = System.Text.RegularExpressions.Regex.Match(marker, "markerWidth=\"([^\"]+)\" markerHeight=\"([^\"]+)\" refX=\"([^\"]+)\"");
        var length = double.Parse(header.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var anchor = double.Parse(header.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);

        // The start variant mirrors the glyph about the box, so its tip anchor is measured from the far side.
        var isStart = idPrefix == "line-start-";
        var tipAnchor = isStart ? length - anchor : anchor;

        var paintedTip = double.MinValue;
        foreach (System.Text.RegularExpressions.Match element in System.Text.RegularExpressions.Regex.Matches(marker, "<(polygon|polyline|circle|line) ([^>]*?)/>"))
        {
            paintedTip = Math.Max(paintedTip, PaintedMaxX(element.Groups[1].Value, element.Groups[2].Value));
        }

        Assert.True(
            Math.Abs(paintedTip - tipAnchor) < 1e-5,
            $"{style} ({idPrefix}): painted tip {paintedTip} must land on the tip anchor {tipAnchor}.");
        Assert.True(paintedTip <= length + 1e-5, $"{style} ({idPrefix}): the painted tip {paintedTip} is clipped by the marker box {length}.");
    }

    /// <summary>
    ///     Returns the largest marker-box x any painted pixel of the element can reach: the union of the
    ///     stroked segments' rectangles (this is exact for bevel joins and butt caps), or the circle radius
    ///     plus the half stroke.
    /// </summary>
    private static double PaintedMaxX(string tag, string attributes)
    {
        double Num(string name) => double.Parse(Attr(attributes, name)!, System.Globalization.CultureInfo.InvariantCulture);
        var strokeWidth = Attr(attributes, "stroke-width");
        var half = strokeWidth is null ? 0.0 : double.Parse(strokeWidth, System.Globalization.CultureInfo.InvariantCulture) / 2.0;

        if (tag == "circle")
        {
            return Num("cx") + Num("r") + half;
        }

        List<(double X, double Y)> points;
        var closed = tag == "polygon";
        if (tag == "line")
        {
            points = [(Num("x1"), Num("y1")), (Num("x2"), Num("y2"))];
        }
        else
        {
            points = Attr(attributes, "points")!
                .Split(',')
                .Select(pair => pair.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Select(parts => (double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture)))
                .ToList();
        }

        var max = double.MinValue;
        var segmentCount = closed ? points.Count : points.Count - 1;
        for (var i = 0; i < segmentCount; i++)
        {
            var (x0, y0) = points[i];
            var (x1, y1) = points[(i + 1) % points.Count];
            var length = Math.Sqrt(((x1 - x0) * (x1 - x0)) + ((y1 - y0) * (y1 - y0)));
            var normalX = -(y1 - y0) / length;
            foreach (var x in new[] { x0, x1 })
            {
                max = Math.Max(max, x + (Math.Abs(normalX) * half));
            }
        }

        return max;
    }

    private static string? Attr(string attributes, string name)
    {
        var match = System.Text.RegularExpressions.Regex.Match(attributes, $"(?:^|\\s){System.Text.RegularExpressions.Regex.Escape(name)}=\"([^\"]*)\"");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>An open-chevron line references the open-chevron marker via <c>marker-end</c>.</summary>
    [Fact]
    public void OpenChevronLine_ReferencesOpenChevronMarker()
    {
        var svg = RenderLine(EndMarkerStyle.None, EndMarkerStyle.OpenChevron);
        Assert.Contains("marker-end=\"url(#line-end-open-chevron)\"", svg, StringComparison.Ordinal);
    }

    /// <summary>A filled-arrow line references the filled-arrow marker via <c>marker-end</c>.</summary>
    [Fact]
    public void SvgRenderer_Render_SingleLine_WithFilledArrow_ProducesFilledArrowMarker()
    {
        var svg = RenderLine(EndMarkerStyle.None, EndMarkerStyle.FilledArrow);
        Assert.Contains("marker-end=\"url(#line-end-filled-arrow)\"", svg, StringComparison.Ordinal);
    }

    /// <summary>A filled-diamond line references the mirrored start-of-line filled-diamond marker via <c>marker-start</c>.</summary>
    [Fact]
    public void SvgRenderer_Render_SingleLine_WithFilledDiamond_ProducesFilledDiamondMarker()
    {
        var svg = RenderLine(EndMarkerStyle.FilledDiamond, EndMarkerStyle.None);
        Assert.Contains("marker-start=\"url(#line-start-filled-diamond)\"", svg, StringComparison.Ordinal);
    }

    /// <summary>A circle-ended line references the circle marker via <c>marker-end</c>.</summary>
    [Fact]
    public void SvgRenderer_Render_SingleLine_WithCircleEnd_ProducesCircleMarker()
    {
        var svg = RenderLine(EndMarkerStyle.None, EndMarkerStyle.Circle);
        Assert.Contains("marker-end=\"url(#line-end-circle)\"", svg, StringComparison.Ordinal);
    }

    /// <summary>A bar-ended line references the bar marker via <c>marker-end</c>.</summary>
    [Fact]
    public void SvgRenderer_Render_SingleLine_WithBarEnd_ProducesBarMarker()
    {
        var svg = RenderLine(EndMarkerStyle.None, EndMarkerStyle.Bar);
        Assert.Contains("marker-end=\"url(#line-end-bar)\"", svg, StringComparison.Ordinal);
    }

    private static string Num(double value) =>
        Math.Round(value, 6).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
