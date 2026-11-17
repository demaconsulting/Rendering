// <copyright file="SvgRenderer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Svg;

/// <summary>
/// Renders a <see cref="LayoutTree"/> to SVG format using zero external dependencies.
/// </summary>
/// <remarks>
/// The renderer is pure and stateless: each call to <see cref="Render"/> builds a complete
/// SVG document from the supplied <see cref="LayoutTree"/> and writes it to the output stream.
/// No state is shared between calls. The produced SVG targets SVG 1.1 and is encoded in UTF-8.
///
/// Node rendering:
/// - <see cref="LayoutBox"/> → <c>&lt;rect&gt;</c> (with <c>rx</c>/<c>ry</c> for rounded
///   corners) + optional <c>&lt;text&gt;</c> for the label + <c>&lt;line&gt;</c> dividers
///   and <c>&lt;text&gt;</c> rows for each compartment; children rendered recursively.
/// - <see cref="LayoutLine"/> → <c>&lt;path&gt;</c> with M/L/A commands for corner-radius-
///   aware bends; arrowheads as marker references defined in <c>&lt;defs&gt;</c>; optional
///   midpoint label as <c>&lt;text&gt;</c>.
/// - <see cref="LayoutLabel"/> → <c>&lt;text&gt;</c>.
/// - <see cref="LayoutPort"/> → <c>&lt;rect&gt;</c> filled square with optional
///   <c>&lt;text&gt;</c> label offset away from the attached edge.
/// - <see cref="LayoutBadge"/> → shape-specific SVG elements centered at the badge position
///   with optional <c>&lt;text&gt;</c> label.
/// - <see cref="LayoutBand"/> → <c>&lt;rect&gt;</c> with optional rotated or horizontal
///   <c>&lt;text&gt;</c> label; children rendered recursively.
/// - <see cref="LayoutLifeline"/> → <c>&lt;rect&gt;</c> header + dashed
///   <c>&lt;line&gt;</c> stem + <c>&lt;text&gt;</c> label.
/// - <see cref="LayoutActivation"/> → <c>&lt;rect&gt;</c> with white fill.
/// - <see cref="LayoutGrid"/> → bordered <c>&lt;rect&gt;</c> cells with per-cell
///   <c>&lt;text&gt;</c> elements.
/// - All other node types are silently skipped for forward compatibility.
///
/// Fill colors are derived from <see cref="Theme.DepthFillColors"/> using modulo wrapping on
/// <see cref="LayoutBox.Depth"/>.
/// </remarks>
public sealed class SvgRenderer : IRenderer
{
    /// <summary>Closing tag for an SVG marker element, indented to match the defs block.</summary>
    private const string MarkerClose = "    </marker>";

    /// <summary>SVG text-anchor value for centered alignment.</summary>
    private const string TextAnchorMiddle = "middle";

    /// <summary>
    /// Stroke width, in logical pixels (before scale), of the contrasting outline drawn around a port
    /// glyph square. Distinguishes the port glyph from an arrowhead marker that may land on/near the
    /// same box edge (both are otherwise solid-filled shapes with no border of their own), so the two
    /// remain visually distinct instead of merging into a single blob.
    /// </summary>
    private const double PortGlyphStrokeWidth = 1.0;

    /// <summary>
    /// Extra width, in logical pixels, added to each side of an outward port label's rectangle when it is
    /// handed to the end-label placer, so an end label on the same run keeps a visible gap from it.
    /// </summary>
    private const double OutwardLabelEndLabelGap = 5.0;

    /// <inheritdoc/>
    public string MediaType => "image/svg+xml";

    /// <inheritdoc/>
    public string DefaultExtension => ".svg";

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions => [".svg"];

    /// <inheritdoc/>
    /// <remarks>
    /// Writes a complete SVG 1.1 document to <paramref name="output"/>. The canvas size is
    /// taken from <see cref="LayoutTree.Width"/> and <see cref="LayoutTree.Height"/>.
    /// All coordinates are expressed as doubles formatted to two decimal places.
    /// </remarks>
    public void Render(LayoutTree layout, RenderOptions options, Stream output)
    {
        // Validate inputs — null arguments would produce silent failures
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        // Build the SVG document in memory then flush once to avoid partial writes
        var sb = new StringBuilder();
        var theme = options.Theme;

        // Resolve every connector label's placement (position and size) before sizing the canvas: a
        // label nudged to avoid colliding with another can land outside the box/routing geometry's
        // extent, so the canvas must be sized only after label placement is known, not before.
        var lines = CollectLines(layout.Nodes).ToList();
        var labelPositions = ConnectorLabelPlacer.Place(lines, theme.FontSizeBody, CollectBoxes(layout.Nodes), NotationMetrics.MarkerScale(theme));

        // Connection end labels (role name, multiplicity) are placed after the midpoint labels, which they
        // yield to, so midpoint placement is identical with or without them.
        // Port glyphs and outward port labels are drawn content too, so end labels keep clear of them.
        var allPorts = new List<LayoutPort>();
        CollectPorts(layout.Nodes, allPorts);
        var portRects = PortOccupiedRects(allPorts, theme);
        var endLabelPlacements = ConnectorLabelPlacer.PlaceEndLabels(
            lines,
            theme.FontSizeBody,
            CollectBoxes(layout.Nodes),
            NotationMetrics.MarkerScale(theme),
            labelPositions.Values.Concat(portRects));

        // Compute canvas dimensions, ensuring a minimum 1×1 canvas, then grow to fully include every
        // placed label's bounding box (a label can be nudged beyond the box/routing geometry's
        // extent that layout.Width/Height was computed from).
        var width = Math.Max(1.0, layout.Width * options.Scale);
        var height = Math.Max(1.0, layout.Height * options.Scale);
        foreach (var placement in labelPositions.Values)
        {
            width = Math.Max(width, (placement.X + placement.HalfWidth) * options.Scale);
            height = Math.Max(height, (placement.Y + placement.HalfHeight) * options.Scale);
        }

        foreach (var placement in endLabelPlacements)
        {
            width = Math.Max(width, (placement.X + placement.HalfWidth) * options.Scale);
            height = Math.Max(height, (placement.Y + placement.HalfHeight) * options.Scale);
        }

        // An outward port label hangs outside its box and can extend past the declared canvas.
        foreach (var port in allPorts.Where(NotationMetrics.IsOutwardLabel))
        {
            var bounds = NotationMetrics.PortOutwardLabelBounds(port, port.ExternalLabel!, theme);
            width = Math.Max(width, (bounds.X + bounds.HalfWidth) * options.Scale);
            height = Math.Max(height, (bounds.Y + bounds.HalfHeight) * options.Scale);
        }

        // Write SVG root element with explicit namespace and viewBox
        sb.Append(
            $"""<svg xmlns="http://www.w3.org/2000/svg" width="{F(width)}" height="{F(height)}" viewBox="0 0 {F(width)} {F(height)}">""");
        sb.AppendLine();

        // Write defs section with all line-end markers (the dotted-triangle and circled-plus markers
        // are emitted only when a line in the tree actually uses them)
        WriteEndMarkerDefs(sb, theme, CollectLazyMarkers(lines));

        // Render all top-level nodes recursively (wires are drawn without their labels here). A marker end that
        // sits on a port glyph is shortened to the glyph's outer edge first, so the arrowhead is not hidden
        // under the (later-drawn) port square.
        foreach (var node in ShortenPortEnds(layout.Nodes))
        {
            RenderNode(sb, node, theme, options.Scale);
        }

        // Final pass: draw every connector label on top of all wires and boxes, so that no later
        // wire can draw over an earlier wire's label. Positions were computed up front (before
        // sizing the canvas, above) so that labels that would collide (for example where two
        // connectors cross) are spread apart.
        foreach (var line in lines)
        {
            if (line.MidpointLabel is not null && labelPositions.TryGetValue(line, out var pos))
            {
                RenderLineLabel(sb, line, theme, options.Scale, pos.X, pos.Y);
            }
        }

        foreach (var placement in endLabelPlacements)
        {
            RenderEndLabel(sb, placement, theme, options.Scale);
        }

        // Close SVG root
        sb.AppendLine("</svg>");

        // Encode as UTF-8 and write to output stream
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        output.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Writes the <c>&lt;defs&gt;</c> block containing all line-end (connector decoration) marker
    /// definitions used by <see cref="RenderLine"/>.
    /// </summary>
    /// <remarks>
    /// Markers always defined: <c>line-end-open-chevron</c> (open two-stroke chevron drawn as a
    /// <c>&lt;polyline&gt;</c> with no closing base edge), <c>line-end-hollow-triangle</c> (hollow
    /// triangle), <c>line-end-filled-arrow</c> (filled triangle), <c>line-end-hollow-triangle-crossbar</c>
    /// (hollow triangle with perpendicular crossbar), <c>line-end-hollow-diamond</c> (hollow diamond),
    /// <c>line-end-filled-diamond</c> (filled diamond), <c>line-end-circle</c> (hollow circle), and
    /// <c>line-end-bar</c> (perpendicular bar). Written only when a line uses them:
    /// <c>line-end-hollow-triangle-two-dots</c>, <c>line-end-hollow-triangle-four-dots</c> and
    /// <c>line-end-circled-plus</c>, plus a mirrored <c>line-start-…</c> variant of any style used at
    /// the start of a line (an SVG <c>marker-start</c> marker's x axis points into the line, so the
    /// end-marker geometry would otherwise sit outside the line start, pointing inward). Every
    /// coordinate is derived from <see cref="NotationMetrics"/>, so a geometry literal never appears
    /// twice and the PNG renderer draws the identical shapes from the same source constants. Every
    /// marker is drawn at <see cref="NotationMetrics.MarkerScale(Theme)"/> (the stroke width, via the SVG
    /// default <c>markerUnits="strokeWidth"</c>), and the PNG renderer applies the same scale so the two
    /// formats draw each marker at the identical size.
    /// </remarks>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="theme">Theme providing stroke color and width.</param>
    /// <param name="lazyMarkers">
    /// The on-demand markers (and whether the start or end variant) that lines in the tree use; only
    /// these are written after the always-defined set, so a tree that uses none produces the historical
    /// defs byte-for-byte.
    /// </param>
    private static void WriteEndMarkerDefs(
        StringBuilder sb,
        Theme theme,
        IReadOnlyList<(EndMarkerStyle Style, bool IsStart)> lazyMarkers)
    {
        var bg = theme.BackgroundColor;

        sb.AppendLine("  <defs>");

        // The eight historical end markers, always written (in this fixed order) so documents that
        // use any of them stay byte-identical to what earlier versions produced.
        foreach (var style in AlwaysDefinedMarkers)
        {
            WriteMarker(sb, theme, style, isStart: false);
        }

        // The dotted-triangle and circled-plus end markers (SysML typing, reference subsetting, owned
        // membership) and every start-of-line marker, written only when used.
        foreach (var (style, isStart) in lazyMarkers)
        {
            WriteMarker(sb, theme, style, isStart);
        }

        // Auto-sizing background for text drawn over lines (e.g. message and guard labels), painted in
        // the theme background so it occludes the connector line consistently under any theme.
        // The filter region defaults to the text bounding box; the small negative inset adds padding.
        sb.Append(
            $"""    <filter id="label-bg" x="{M(-NotationMetrics.LabelBgInset)}" y="{M(-NotationMetrics.LabelBgInset)}" width="{M(NotationMetrics.LabelBgExtent)}" height="{M(NotationMetrics.LabelBgExtent)}">""");
        sb.AppendLine();
        sb.AppendLine($"""      <feFlood flood-color="{bg}"/>""");
        sb.AppendLine("""      <feComposite in="SourceGraphic" operator="over"/>""");
        sb.AppendLine("    </filter>");

        sb.AppendLine("  </defs>");
    }

    /// <summary>
    /// Builds an SVG <c>points</c> string from tip-relative marker vertices by mapping each vertex to
    /// marker-box coordinates: <c>boxX = refAlong - Along</c> and <c>boxY = refAcross + Across</c>.
    /// </summary>
    /// <param name="vertices">Tip-relative marker vertices in draw order.</param>
    /// <param name="refAlong">Along-line position of the marker box anchor (the SVG <c>refX</c>).</param>
    /// <param name="refAcross">Across-line position of the marker box anchor (the SVG <c>refY</c>).</param>
    /// <returns>An SVG <c>points</c> attribute value, for example <c>"0 0, 10 3.5, 0 7"</c>.</returns>
    private static string MarkerPoints(
        IReadOnlyList<MarkerVertex> vertices,
        double refAlong,
        double refAcross)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < vertices.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(
                $"{M(refAlong - vertices[i].Along)} {M(refAcross + vertices[i].Across)}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// The eight historical end-marker styles whose <c>line-end-…</c> definitions are always written
    /// to <c>&lt;defs&gt;</c>, in the order earlier versions wrote them.
    /// </summary>
    private static readonly EndMarkerStyle[] AlwaysDefinedMarkers =
    [
        EndMarkerStyle.OpenChevron,
        EndMarkerStyle.HollowTriangle,
        EndMarkerStyle.FilledArrow,
        EndMarkerStyle.HollowTriangleCrossbar,
        EndMarkerStyle.HollowDiamond,
        EndMarkerStyle.FilledDiamond,
        EndMarkerStyle.Circle,
        EndMarkerStyle.Bar,
    ];

    /// <summary>
    /// Returns whether a marker definition is written only when a line uses it: every start-of-line
    /// marker (a <c>marker-start</c> marker is a mirrored variant, because its x axis points into the
    /// line rather than out of it) and the dotted-triangle and circled-plus end markers.
    /// </summary>
    /// <param name="style">The end-marker style.</param>
    /// <param name="isStart">Whether the marker is used at the start of a line.</param>
    /// <returns><see langword="true"/> when the definition is written on demand.</returns>
    private static bool IsLazyMarker(EndMarkerStyle style, bool isStart) =>
        style != EndMarkerStyle.None && (isStart || !AlwaysDefinedMarkers.Contains(style));

    /// <summary>
    /// Returns the SVG marker id of an end-marker style. The end variant (<c>line-end-…</c>) is
    /// referenced by <c>marker-end</c>; the start variant (<c>line-start-…</c>) is geometrically
    /// mirrored along the line, because a <c>marker-start</c> marker's x axis points into the line
    /// (<c>orient="auto"</c>), so its tip anchor must sit at the near end of the marker box and its
    /// body extend along the line.
    /// </summary>
    /// <param name="style">The end-marker style.</param>
    /// <param name="isStart">Whether the start-of-line variant is wanted.</param>
    /// <returns>The marker id.</returns>
    private static string MarkerId(EndMarkerStyle style, bool isStart)
    {
        var name = style switch
        {
            EndMarkerStyle.OpenChevron => "open-chevron",
            EndMarkerStyle.HollowTriangle => "hollow-triangle",
            EndMarkerStyle.HollowTriangleCrossbar => "hollow-triangle-crossbar",
            EndMarkerStyle.FilledArrow => "filled-arrow",
            EndMarkerStyle.HollowDiamond => "hollow-diamond",
            EndMarkerStyle.FilledDiamond => "filled-diamond",
            EndMarkerStyle.Circle => "circle",
            EndMarkerStyle.Bar => "bar",
            EndMarkerStyle.HollowTriangleTwoDots => "hollow-triangle-two-dots",
            EndMarkerStyle.HollowTriangleFourDots => "hollow-triangle-four-dots",
            _ => "circled-plus",
        };
        return (isStart ? "line-start-" : "line-end-") + name;
    }

    /// <summary>
    /// Resolves the <c>marker-start</c>/<c>marker-end</c> attribute for a line end, or an empty string
    /// when the style has no marker.
    /// </summary>
    /// <param name="style">The end-marker style.</param>
    /// <param name="isStart">Whether this is the source (start) end of the line.</param>
    /// <returns>A leading-space attribute fragment, or an empty string.</returns>
    private static string MarkerAttribute(EndMarkerStyle style, bool isStart)
    {
        if (style == EndMarkerStyle.None)
        {
            return string.Empty;
        }

        var attribute = isStart ? "marker-start" : "marker-end";
        return $" {attribute}=\"url(#{MarkerId(style, isStart)})\"";
    }

    /// <summary>
    /// Collects the distinct on-demand marker definitions used by a set of lines, in a stable order.
    /// </summary>
    /// <param name="lines">The lines of the tree.</param>
    /// <returns>The distinct (style, isStart) pairs that need a definition.</returns>
    private static List<(EndMarkerStyle Style, bool IsStart)> CollectLazyMarkers(IEnumerable<LayoutLine> lines)
    {
        var used = new HashSet<(EndMarkerStyle Style, bool IsStart)>();
        foreach (var line in lines)
        {
            if (IsLazyMarker(line.SourceEnd, isStart: true))
            {
                used.Add((line.SourceEnd, true));
            }

            if (IsLazyMarker(line.TargetEnd, isStart: false))
            {
                used.Add((line.TargetEnd, false));
            }
        }

        return [.. used.OrderBy(marker => marker.IsStart).ThenBy(marker => marker.Style)];
    }

    /// <summary>
    /// Writes one <c>&lt;marker&gt;</c> definition. The end variant draws the glyph with its tip anchor
    /// (<c>refX</c>) on the line endpoint and its body behind it; the start variant wraps the same glyph
    /// in a mirror transform and mirrors the anchor, so the glyph points out of the line start exactly
    /// as the end variant points out of the line end.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="theme">Theme providing stroke color, width, and background.</param>
    /// <param name="style">The marker style.</param>
    /// <param name="isStart">Whether to write the start-of-line (mirrored) variant.</param>
    private static void WriteMarker(StringBuilder sb, Theme theme, EndMarkerStyle style, bool isStart)
    {
        var (length, height, refX, refY, body) = DescribeMarker(style, theme);
        var anchorX = isStart ? length - refX : refX;
        sb.Append(
            $"""    <marker id="{MarkerId(style, isStart)}" markerWidth="{M(length)}" markerHeight="{M(height)}" refX="{M(anchorX)}" refY="{M(refY)}" orient="auto">""");
        sb.AppendLine();
        if (isStart)
        {
            sb.AppendLine($"""      <g transform="matrix(-1 0 0 1 {M(length)} 0)">""");
        }

        foreach (var element in body)
        {
            sb.AppendLine(element);
        }

        if (isStart)
        {
            sb.AppendLine("      </g>");
        }

        sb.AppendLine(MarkerClose);
    }

    /// <summary>
    /// Describes the end variant of a marker: its box (length and height), its tip anchor, and the SVG
    /// elements of its glyph. The glyph coordinates come straight from <see cref="NotationMetrics"/>; the
    /// box length and tip anchor come from <see cref="NotationMetrics.MarkerAnchor(EndMarkerStyle, Theme)"/>,
    /// which retracts the anchor so the STROKED tip (not the geometric vertex) lands on the line endpoint.
    /// </summary>
    /// <param name="style">The marker style.</param>
    /// <param name="theme">Theme providing stroke color, width, and background.</param>
    /// <returns>The marker box, tip anchor and body elements.</returns>
    private static (double Length, double Height, double RefX, double RefY, List<string> Body) DescribeMarker(
        EndMarkerStyle style,
        Theme theme)
    {
        var (_, height, _, refY, body) = DescribeGlyph(style, theme);
        var (length, anchor) = NotationMetrics.MarkerAnchor(style, theme);
        return (length, height, anchor, refY, body);
    }

    /// <summary>
    /// Describes the glyph of a marker in marker-box coordinates (the elements, the box height and the
    /// vertical centre); the box length and tip anchor in the returned tuple are the glyph's own and are
    /// superseded by <see cref="DescribeMarker"/>. Polygons and polylines join with a bevel so the painted
    /// tip is the bevel corner (as in the raster renderer), not a miter spike.
    /// </summary>
    /// <param name="style">The marker style.</param>
    /// <param name="theme">Theme providing stroke color, width, and background.</param>
    /// <returns>The marker box, tip anchor and body elements.</returns>
    private static (double Length, double Height, double RefX, double RefY, List<string> Body) DescribeGlyph(
        EndMarkerStyle style,
        Theme theme)
    {
        var stroke = theme.StrokeColor;
        var sw = F(theme.StrokeWidth);
        var bg = theme.BackgroundColor;

        string Polygon(string points, string fill) =>
            $"""      <polygon points="{points}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}" stroke-linejoin="bevel"/>""";
        string Line(double x1, double y1, double x2, double y2) =>
            $"""      <line x1="{M(x1)}" y1="{M(y1)}" x2="{M(x2)}" y2="{M(y2)}" stroke="{stroke}" stroke-width="{sw}"/>""";
        string Circle(double cx, double cy, double r, string fill, bool outlined) =>
            outlined
                ? $"""      <circle cx="{M(cx)}" cy="{M(cy)}" r="{M(r)}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}"/>"""
                : $"""      <circle cx="{M(cx)}" cy="{M(cy)}" r="{M(r)}" fill="{fill}"/>""";

        switch (style)
        {
            case EndMarkerStyle.HollowDiamond:
            case EndMarkerStyle.FilledDiamond:
                var diamondPoints = MarkerPoints(
                    NotationMetrics.DiamondVertices(), NotationMetrics.DiamondRefX, NotationMetrics.DiamondHalfWidth);
                return (
                    NotationMetrics.DiamondLength,
                    NotationMetrics.DiamondWidth,
                    NotationMetrics.DiamondRefX,
                    NotationMetrics.DiamondHalfWidth,
                    [Polygon(diamondPoints, style == EndMarkerStyle.FilledDiamond ? stroke : bg)]);

            case EndMarkerStyle.Circle:
                return (
                    NotationMetrics.CircleMarkerBox,
                    NotationMetrics.CircleMarkerBox,
                    NotationMetrics.CircleRefX,
                    NotationMetrics.CircleCenter,
                    [Circle(NotationMetrics.CircleCenter, NotationMetrics.CircleCenter, NotationMetrics.CircleRadius, bg, outlined: true)]);

            case EndMarkerStyle.Bar:
                return (
                    NotationMetrics.BarAlong,
                    NotationMetrics.BarAcross,
                    NotationMetrics.BarHalfAlong,
                    NotationMetrics.BarHalf,
                    [Line(NotationMetrics.BarHalfAlong, 0, NotationMetrics.BarHalfAlong, NotationMetrics.BarAcross)]);

            case EndMarkerStyle.CircledPlus:
                var centre = NotationMetrics.CircledPlusCenter;
                var arm = NotationMetrics.CircledPlusArmHalfLength;
                return (
                    NotationMetrics.CircledPlusMarkerBox,
                    NotationMetrics.CircledPlusMarkerBox,
                    NotationMetrics.CircledPlusRefX,
                    centre,
                    [
                        Circle(centre, centre, NotationMetrics.CircledPlusRadius, bg, outlined: true),
                        Line(centre - arm, centre, centre + arm, centre),
                        Line(centre, centre - arm, centre, centre + arm),
                    ]);

            default:
                // The triangle family: open chevron, hollow/filled triangle, crossbar, and the dotted variants
                var (length, tipAnchor) = NotationMetrics.TriangleFamilyBox(style);
                var trianglePoints = MarkerPoints(
                    NotationMetrics.TriangleVertices(), tipAnchor, NotationMetrics.EndMarkerHalfWidth);
                var body = new List<string>();
                if (style == EndMarkerStyle.OpenChevron)
                {
                    body.Add($"""      <polyline points="{trianglePoints}" fill="none" stroke="{stroke}" stroke-width="{sw}" stroke-linejoin="bevel"/>""");
                }
                else
                {
                    body.Add(Polygon(trianglePoints, style == EndMarkerStyle.FilledArrow ? stroke : bg));
                }

                if (style == EndMarkerStyle.HollowTriangleCrossbar)
                {
                    // The redefinition bar sits behind the triangle base, spanning the base height
                    var barX = tipAnchor - NotationMetrics.CrossbarAlong;
                    var barTop = NotationMetrics.EndMarkerHalfWidth - NotationMetrics.CrossbarHalfLength;
                    body.Add(Line(barX, barTop, barX, barTop + (2.0 * NotationMetrics.CrossbarHalfLength)));
                }

                foreach (var dot in NotationMetrics.DotCenters(style))
                {
                    body.Add(Circle(
                        tipAnchor - dot.Along,
                        NotationMetrics.EndMarkerHalfWidth + dot.Across,
                        NotationMetrics.DotRadius,
                        stroke,
                        outlined: false));
                }

                return (length, NotationMetrics.EndMarkerWidth, tipAnchor, NotationMetrics.EndMarkerHalfWidth, body);
        }
    }

    /// <summary>
    /// Formats a notation-metric coordinate for an SVG marker definition: rounded to six decimals
    /// (to absorb floating-point noise in derived constants such as the crossbar position) and emitted
    /// with the minimal invariant-culture representation, so a derived value reproduces the historical
    /// literal byte-for-byte (for example <c>10</c>, <c>3.5</c>, <c>-0.05</c>).
    /// </summary>
    /// <param name="value">The notation-metric coordinate.</param>
    /// <returns>The formatted number.</returns>
    private static string M(double value) =>
        Math.Round(value, 6).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Dispatches a single <see cref="LayoutNode"/> to the appropriate typed render method.
    /// Unknown concrete types are silently skipped so that future node types do not break
    /// existing callers.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="node">The node to render.</param>
    /// <param name="theme">Visual theme providing colors and dimensions.</param>
    /// <param name="scale">Uniform scale factor applied to all coordinates.</param>
    private static void RenderNode(StringBuilder sb, LayoutNode node, Theme theme, double scale)
    {
        switch (node)
        {
            case LayoutBox box:
                RenderBox(sb, box, theme, scale);
                break;

            case LayoutLine line:
                RenderLine(sb, line, theme, scale);
                break;

            case LayoutLabel label:
                RenderLabel(sb, label, theme, scale);
                break;

            case LayoutPort port:
                RenderPort(sb, port, theme, scale);
                break;

            case LayoutBadge badge:
                RenderBadge(sb, badge, theme, scale);
                break;

            case LayoutBand band:
                RenderBand(sb, band, theme, scale);
                break;

            case LayoutLifeline lifeline:
                RenderLifeline(sb, lifeline, theme, scale);
                break;

            case LayoutActivation activation:
                RenderActivation(sb, activation, theme, scale);
                break;

            case LayoutGrid grid:
                RenderGrid(sb, grid, theme, scale);
                break;

            default:
                // Skip unknown node types for forward compatibility
                break;
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutBox"/> as a <c>&lt;rect&gt;</c> with <c>rx</c>/<c>ry</c>
    /// attributes for rounded corners, an optional centered <c>&lt;text&gt;</c> label,
    /// horizontal divider <c>&lt;line&gt;</c> elements and text rows for each compartment,
    /// then recursively renders its children.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="box">The box node to render.</param>
    /// <param name="theme">Visual theme providing fill colors, stroke, and font size.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderBox(StringBuilder sb, LayoutBox box, Theme theme, double scale)
    {
        // Derive fill color from theme using depth modulo wrapping
        var fillColor = theme.DepthFillColors[box.Depth % theme.DepthFillColors.Count];

        // Draw the box outline (shape-specific)
        RenderBoxOutline(sb, box, theme, fillColor, scale);

        // Draw the optional send/accept corner icon
        RenderBoxCornerIcon(sb, box, theme, scale);

        // Draw the keyword and label in the title area
        RenderBoxTitle(sb, box, theme, scale);

        // Render compartments below the label area
        if (box.Compartments.Count > 0)
        {
            RenderBoxCompartments(sb, box, theme, scale);
        }

        // Render children recursively
        foreach (var child in box.Children)
        {
            RenderNode(sb, child, theme, scale);
        }
    }

    /// <summary>
    /// Renders the outline (border and fill) of a <see cref="LayoutBox"/>, selecting the path
    /// geometry based on <see cref="LayoutBox.Shape"/>.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="box">The box whose outline is drawn.</param>
    /// <param name="theme">Visual theme providing stroke settings and corner radius.</param>
    /// <param name="fillColor">Resolved fill color for the box interior.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderBoxOutline(StringBuilder sb, LayoutBox box, Theme theme, string fillColor, double scale)
    {
        var x = box.X * scale;
        var y = box.Y * scale;
        var w = box.Width * scale;
        var h = box.Height * scale;

        switch (box.Shape)
        {
            case BoxShape.Folder:
                RenderFolderOutline(sb, box, theme, fillColor, scale);
                break;

            case BoxShape.Note:
                RenderNoteOutline(sb, box, theme, fillColor, scale);
                break;

            case BoxShape.Diamond:
                var vertices = NotationMetrics.DiamondBoxVertices(x, y, w, h);
                var points = string.Join(" ", vertices.Select(vertex => $"{F(vertex.X)},{F(vertex.Y)}"));
                sb.Append(
                    $"""  <polygon points="{points}" fill="{fillColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"{OutlineDash(box)}/>""");
                sb.AppendLine();
                break;

            case BoxShape.RoundedRectangle:
                var cornerRadius = ResolveRoundedCornerRadius(box, theme);
                var cornerStr = cornerRadius > 0
                    ? $" rx=\"{F(cornerRadius * scale)}\" ry=\"{F(cornerRadius * scale)}\""
                    : string.Empty;
                sb.Append(
                    $"""  <rect x="{F(x)}" y="{F(y)}" width="{F(w)}" height="{F(h)}" fill="{fillColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"{cornerStr}{OutlineDash(box)}/>""");
                sb.AppendLine();
                break;

            default:
                sb.Append(
                    $"""  <rect x="{F(x)}" y="{F(y)}" width="{F(w)}" height="{F(h)}" fill="{fillColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"{OutlineDash(box)}/>""");
                sb.AppendLine();
                break;
        }
    }

    /// <summary>
    /// Returns the <c>stroke-dasharray</c> attribute fragment for a box outline, or an empty string for
    /// a solid (or unspecified) outline.
    /// </summary>
    /// <param name="box">The box whose outline style is resolved.</param>
    /// <returns>A leading-space attribute fragment, or an empty string.</returns>
    private static string OutlineDash(LayoutBox box) =>
        DashAttribute(box.Appearance?.OutlineStyle ?? LineStyle.Solid);

    /// <summary>
    /// Returns the <c>stroke-dasharray</c> attribute fragment for a stroke style: <c>6 3</c> for
    /// dashed, <c>2 2</c> for dotted, and an empty string for solid.
    /// </summary>
    /// <param name="style">The stroke style.</param>
    /// <returns>A leading-space attribute fragment, or an empty string.</returns>
    private static string DashAttribute(LineStyle style) => style switch
    {
        LineStyle.Dashed => " stroke-dasharray=\"6 3\"",
        LineStyle.Dotted => " stroke-dasharray=\"2 2\"",
        _ => string.Empty,
    };

    /// <summary>
    /// Renders a folder-shaped outline (a tab at the top-left above a full-width body),
    /// used for package nodes.
    /// </summary>
    private static void RenderFolderOutline(StringBuilder sb, LayoutBox box, Theme theme, string fillColor, double scale)
    {
        var tabHeight = ResolveFolderTabHeight(box, theme);
        var tabWidth = ResolveFolderTabWidth(box, theme);

        var x = box.X * scale;
        var yTab = box.Y * scale;
        var yBody = (box.Y + tabHeight) * scale;
        var xTabRight = (box.X + tabWidth) * scale;
        var xRight = (box.X + box.Width) * scale;
        var yBottom = (box.Y + box.Height) * scale;

        sb.Append(
            $"""  <path d="M {F(x)} {F(yBody)} L {F(x)} {F(yTab)} L {F(xTabRight)} {F(yTab)} L {F(xTabRight)} {F(yBody)} L {F(xRight)} {F(yBody)} L {F(xRight)} {F(yBottom)} L {F(x)} {F(yBottom)} Z" fill="{fillColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"{OutlineDash(box)}/>""");
        sb.AppendLine();
    }

    /// <summary>
    /// Resolves the rounded-corner radius for a box, preferring a caller-supplied placed-box value so
    /// routing and rendering can agree on the exact outline geometry. A negative caller-supplied value
    /// is clamped to zero.
    /// </summary>
    private static double ResolveRoundedCornerRadius(LayoutBox box, Theme theme) =>
        box.RoundedCornerRadius.HasValue
            ? Math.Max(0.0, box.RoundedCornerRadius.Value)
            : NotationMetrics.RoundedRectRadius(theme);

    /// <summary>
    /// Resolves the folder tab width for a box, preferring a caller-supplied placed-box value so
    /// routing and rendering can agree on the exact top-face geometry. A negative caller-supplied value
    /// is clamped to zero.
    /// </summary>
    private static double ResolveFolderTabWidth(LayoutBox box, Theme theme) =>
        BoxMetrics.ResolveFolderTabWidth(box, theme);

    /// <summary>
    /// Resolves the folder tab height for a box, preferring a caller-supplied placed-box value so
    /// routing and rendering can agree on the exact top-face projection offset. A negative
    /// caller-supplied value is clamped to zero.
    /// </summary>
    private static double ResolveFolderTabHeight(LayoutBox box, Theme theme) =>
        BoxMetrics.ResolveFolderTabHeight(box, theme);

    /// <summary>
    /// Resolves the top Y coordinate (unscaled) of the title/label area for a box. For a
    /// <see cref="BoxShape.Folder"/> outline, the title area is recessed below the tab so that
    /// keyword/label text and compartments never overlap the (otherwise empty) tab notch.
    /// </summary>
    private static double ResolveTitleAreaTop(LayoutBox box, Theme theme) =>
        box.Shape == BoxShape.Folder
            ? box.Y + ResolveFolderTabHeight(box, theme)
            : box.Y;

    /// <summary>
    /// Renders a note-shaped outline (a rectangle with a folded-down top-right corner),
    /// used for documentation and comment nodes.
    /// </summary>
    private static void RenderNoteOutline(StringBuilder sb, LayoutBox box, Theme theme, string fillColor, double scale)
    {
        var fold = BoxMetrics.NoteFoldSize(box);

        var x = box.X * scale;
        var y = box.Y * scale;
        var xRight = (box.X + box.Width) * scale;
        var xFold = (box.X + box.Width - fold) * scale;
        var yFold = (box.Y + fold) * scale;
        var yBottom = (box.Y + box.Height) * scale;

        // Main body with the top-right corner cut
        sb.Append(
            $"""  <path d="M {F(x)} {F(y)} L {F(xFold)} {F(y)} L {F(xRight)} {F(yFold)} L {F(xRight)} {F(yBottom)} L {F(x)} {F(yBottom)} Z" fill="{fillColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"{OutlineDash(box)}/>""");
        sb.AppendLine();

        // The folded corner triangle
        sb.Append(
            $"""  <path d="M {F(xFold)} {F(y)} L {F(xFold)} {F(yFold)} L {F(xRight)} {F(yFold)}" fill="none" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"{OutlineDash(box)}/>""");
        sb.AppendLine();
    }

    /// <summary>
    /// Renders the optional keyword line and name label in the title area of a box, using the box's
    /// <see cref="BoxAppearance"/> text styles (the legacy italic keyword and bold name by default).
    /// A folder whose appearance requests the name in the tab draws the name inside the tab instead
    /// of the body.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="box">Box whose title is rendered.</param>
    /// <param name="theme">Visual theme providing font sizes and padding.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderBoxTitle(StringBuilder sb, LayoutBox box, Theme theme, double scale)
    {
        var centerX = (box.X + (box.Width / 2.0)) * scale;
        var cursorY = BoxMetrics.TitleCursorTop(box, theme);
        var appearance = box.Appearance ?? new BoxAppearance();
        var nameStyle = TextStyleAttributes(appearance.ResolvedNameStyle);

        // Keyword line (smaller, guillemet-wrapped) above the name
        if (box.Keyword != null)
        {
            var kwY = (cursorY + theme.FontSizeBody / 2.0) * scale;
            sb.Append(
                $"""  <text x="{F(centerX)}" y="{F(kwY)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}"{TextStyleAttributes(appearance.ResolvedKeywordStyle)} fill="{theme.StrokeColor}" text-anchor="middle" dominant-baseline="middle">{EscapeXml("\u00AB" + box.Keyword + "\u00BB")}</text>""");
            sb.AppendLine();
            cursorY += theme.FontSizeBody + theme.LabelPadding;
        }

        // Name label drawn inside the folder tab (SysML "package with name in tab")
        if (BoxMetrics.IsNameInTab(box))
        {
            var tabWidth = ResolveFolderTabWidth(box, theme);
            var tabHeight = ResolveFolderTabHeight(box, theme);
            var tabTextX = (box.X + theme.LabelPadding) * scale;
            var tabTextY = (box.Y + (tabHeight / 2.0)) * scale;
            var tabFit = FitTextLength(box.Label!, theme.FontSizeBody, tabWidth - (2 * theme.LabelPadding), scale, useAccurateEstimator: true);
            sb.Append(
                $"""  <text x="{F(tabTextX)}" y="{F(tabTextY)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}"{nameStyle} fill="{theme.StrokeColor}" text-anchor="start" dominant-baseline="middle"{tabFit}>{EscapeXml(box.Label!)}</text>""");
            sb.AppendLine();
            return;
        }

        // Name label — only constrain width when the text would actually overflow the box,
        // so short labels render at their natural size instead of being stretched to fill.
        if (box.Label != null)
        {
            var textY = (cursorY + theme.FontSizeTitle / 2.0) * scale;
            var availableWidth = box.Width - (2 * theme.LabelPadding);
            var fit = FitTextLength(box.Label, theme.FontSizeTitle, availableWidth, scale);
            sb.Append(
                $"""  <text x="{F(centerX)}" y="{F(textY)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeTitle * scale)}"{nameStyle} fill="{theme.StrokeColor}" text-anchor="middle" dominant-baseline="middle"{fit}>{EscapeXml(box.Label)}</text>""");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Returns the <c>font-weight</c>/<c>font-style</c> attribute fragment for a text style. Only
    /// non-default values are emitted (<c>bold</c>, <c>italic</c>), so the legacy bold name, italic
    /// keyword, and bold-italic compartment title reproduce their historical markup exactly.
    /// </summary>
    /// <param name="style">The text style.</param>
    /// <returns>A leading-space attribute fragment, or an empty string for regular upright text.</returns>
    private static string TextStyleAttributes(TextStyle style) =>
        (style.Weight == FontWeight.Bold ? " font-weight=\"bold\"" : string.Empty)
        + (style.Style == FontStyle.Italic ? " font-style=\"italic\"" : string.Empty);

    /// <summary>
    /// Renders the send/accept corner icon of a box as a polygon in the top-left corner of its body
    /// (below the tab for a folder), filled with the theme background and stroked in the theme stroke.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="box">The box whose icon is drawn.</param>
    /// <param name="theme">Visual theme providing stroke and background colors.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderBoxCornerIcon(StringBuilder sb, LayoutBox box, Theme theme, double scale)
    {
        var icon = box.Appearance?.CornerIcon ?? BoxCornerIcon.None;
        var vertices = NotationMetrics.CornerIconVertices(icon);
        if (vertices.Count == 0)
        {
            return;
        }

        var originX = box.X + NotationMetrics.CornerIconInset;
        var originY = ResolveTitleAreaTop(box, theme) + NotationMetrics.CornerIconInset;
        var points = string.Join(
            " ",
            vertices.Select(v => $"{F((originX + v.Along) * scale)},{F((originY + v.Across) * scale)}"));
        sb.Append(
            $"""  <polygon points="{points}" fill="{theme.BackgroundColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
        sb.AppendLine();
    }

    /// <summary>
    /// Returns an SVG <c>textLength</c>/<c>lengthAdjust</c> attribute fragment that constrains text
    /// to <paramref name="availableWidth"/> only when the text's estimated natural width exceeds it;
    /// otherwise returns an empty string so the text renders at its natural width (no stretching).
    /// </summary>
    /// <param name="text">The text to be rendered.</param>
    /// <param name="fontSize">Unscaled font size of the text.</param>
    /// <param name="availableWidth">Unscaled width available for the text.</param>
    /// <param name="scale">Uniform scale factor.</param>
    /// <param name="useAccurateEstimator">
    /// When <see langword="true"/>, estimates natural width via the shared
    /// <see cref="PortLabelWidthEstimator"/> — the same per-character estimator the layout engine
    /// uses to size a port's <see cref="LayoutPort.MaxLabelWidth"/> — so a port label whose
    /// <see cref="LayoutPort.MaxLabelWidth"/> already covers its measured natural width never emits a
    /// <c>textLength</c> attribute. When <see langword="false"/> (the default), uses the coarser flat
    /// <c>length * fontSize * 0.6</c> heuristic, unchanged for box titles and generic/connector
    /// labels, which have no analogous layout-time estimator to reconcile against.
    /// </param>
    /// <returns>A leading-space attribute fragment, or an empty string when no constraint is needed.</returns>
    private static string FitTextLength(
        string text,
        double fontSize,
        double availableWidth,
        double scale,
        bool useAccurateEstimator = false)
    {
        var estimatedWidth = useAccurateEstimator
            ? PortLabelWidthEstimator.MeasureWidth(text, fontSize)
            : text.Length * fontSize * 0.6; // Rough average glyph-width estimate.
        if (availableWidth <= 0 || estimatedWidth <= availableWidth)
        {
            return string.Empty;
        }

        return $""" textLength="{F(availableWidth * scale)}" lengthAdjust="spacingAndGlyphs" """.TrimEnd();
    }

    /// <summary>
    /// Renders the compartments of a <see cref="LayoutBox"/> below the title area as SVG
    /// <c>&lt;line&gt;</c> dividers and <c>&lt;text&gt;</c> elements.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="box">Box whose compartments are rendered.</param>
    /// <param name="theme">Visual theme providing font sizes, padding, and stroke settings.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderBoxCompartments(StringBuilder sb, LayoutBox box, Theme theme, double scale)
    {
        // Compartments start below the title area (keyword + label), computed via shared metrics
        var labelAreaHeight = BoxMetrics.TitleAreaHeight(box, theme);

        // Deliberately based on the computed title-area height (labelAreaHeight > 0), not on
        // "is Label/Keyword set": a theme with zero title padding/font-size would give
        // labelAreaHeight == 0 even with a Label present, and the leading divider must still be
        // skipped in that case to avoid the stray-line bug this fix addresses (see below).
        var titleAreaOccupiesSpace = labelAreaHeight > 0;
        var compartmentY = ResolveTitleAreaTop(box, theme) + box.ContentInsetTop + labelAreaHeight;

        var isFirstCompartment = true;
        foreach (var compartment in box.Compartments)
        {
            // The divider above the first compartment only has something to separate from when the
            // title area above it occupies real space. With no such space, this divider would sit
            // in the un-reserved region near the box's own top edge — redundant for a plain
            // rectangle (any coinciding lines are invisible duplicates) but a genuine visible defect
            // for shapes whose top edge isn't a plain straight line across the full width (e.g.
            // Note's folded corner cutout), where the full-width divider ignores the cut and renders
            // as a stray line past the fold.
            if (!isFirstCompartment || titleAreaOccupiesSpace)
            {
                // A Note's folded corner cuts its top-right corner short, so any full-width divider
                // whose Y would still fall within that cutout (possible for a non-first compartment
                // immediately following an empty leading one) must be pushed down to at least the
                // fold's bottom edge; otherwise the line overshoots the cut and reads as a stray
                // mark crossing the folded corner. Only the drawn line's Y is clamped here, not
                // compartmentY itself, so row text position is unaffected.
                var dividerY = box.Shape == BoxShape.Note
                    ? Math.Max(compartmentY, box.Y + BoxMetrics.NoteFoldSize(box))
                    : compartmentY;

                // Full-width horizontal divider at the top of this compartment
                sb.Append(
                    $"""  <line x1="{F(box.X * scale)}" y1="{F(dividerY * scale)}" x2="{F((box.X + box.Width) * scale)}" y2="{F(dividerY * scale)}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
                sb.AppendLine();
            }

            isFirstCompartment = false;

            // Draw optional compartment title (bold italic and left-aligned unless the compartment
            // requests another alignment/style, e.g. the SysML centered italic title)
            if (compartment.Title != null)
            {
                var centered = compartment.TitleAlign == TextAlign.Center;
                var titleX = centered
                    ? (box.X + box.ContentInsetLeft + ((box.Width - box.ContentInsetLeft - box.ContentInsetRight) / 2.0)) * scale
                    : (box.X + theme.LabelPadding + box.ContentInsetLeft) * scale;
                var titleY = (compartmentY + theme.LabelPadding + theme.FontSizeBody / 2.0) * scale;
                var titleAnchor = centered ? TextAnchorMiddle : "start";
                sb.Append(
                    $"""  <text x="{F(titleX)}" y="{F(titleY)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}"{TextStyleAttributes(compartment.ResolvedTitleStyle)} fill="{theme.StrokeColor}" text-anchor="{titleAnchor}" dominant-baseline="middle">{EscapeXml(compartment.Title)}</text>""");
                sb.AppendLine();
                compartmentY += theme.LabelPadding + theme.FontSizeBody + theme.LabelPadding;
            }

            // Draw each body row with body font size and left-aligned indent
            foreach (var row in compartment.Rows)
            {
                var rowX = (box.X + theme.LabelPadding + box.ContentInsetLeft) * scale;
                var rowY = (compartmentY + theme.LabelPadding + theme.FontSizeBody / 2.0) * scale;
                sb.Append(
                    $"""  <text x="{F(rowX)}" y="{F(rowY)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}" fill="{theme.StrokeColor}" text-anchor="start" dominant-baseline="middle">{EscapeXml(row)}</text>""");
                sb.AppendLine();
                compartmentY += theme.LabelPadding + theme.FontSizeBody;
            }

            // Bottom gap so the last row clears the next compartment divider.
            compartmentY += theme.LabelPadding;
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutLine"/> as an SVG <c>&lt;path&gt;</c> element with
    /// arc-at-bend corner rounding, optional arrowhead marker references, optional dashing,
    /// and an optional midpoint label.
    /// </summary>
    /// <remarks>
    /// When <see cref="Theme.LineCornerRadius"/> is zero, each interior waypoint is
    /// connected with a plain <c>L</c> command. When the radius is positive, each interior
    /// waypoint is replaced with a shortened incoming <c>L</c> command and an <c>A</c>
    /// (arc) command whose sweep direction is derived from the cross product of the incoming
    /// and outgoing direction vectors.
    /// </remarks>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="line">The line node to render.</param>
    /// <param name="theme">Visual theme providing stroke color and width.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderLine(StringBuilder sb, LayoutLine line, Theme theme, double scale)
    {
        // Lines with fewer than 2 waypoints cannot be drawn
        if (line.Waypoints.Count < 2)
        {
            return;
        }

        // Build SVG path data with optional arc-at-bend corner rounding. The final corner before each
        // end is clamped so the rounded arc completes at least the marker's along-line length before
        // the endpoint, keeping the decoration on a clean straight approach.
        var pathData = BuildLinePath(
            line.Waypoints,
            theme.LineCornerRadius,
            scale,
            NotationMetrics.RenderedAlongLineLength(line.SourceEnd, theme),
            NotationMetrics.RenderedAlongLineLength(line.TargetEnd, theme));

        // Resolve line-end marker attribute strings (a start marker is a mirrored line-start-… variant)
        var markerStart = MarkerAttribute(line.SourceEnd, isStart: true);
        var markerEnd = MarkerAttribute(line.TargetEnd, isStart: false);

        // Determine stroke dash pattern for non-solid lines
        var dashArray = line.LineStyle switch
        {
            LineStyle.Dashed => " stroke-dasharray=\"6 3\"",
            LineStyle.Dotted => " stroke-dasharray=\"2 2\"",
            _ => string.Empty
        };

        sb.Append(
            $"""  <path d="{pathData}" fill="none" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"{markerStart}{markerEnd}{dashArray}/>""");
        sb.AppendLine();

        // Note: the midpoint label is intentionally NOT drawn here. It is drawn in a final pass
        // (see RenderLineLabel) so that no later wire can draw over an earlier wire's label.
    }

    /// <summary>
    /// Renders a line's optional midpoint label as a centered text element with an auto-sizing white
    /// background (via the <c>label-bg</c> filter). Called in a final pass after all wires and boxes
    /// are drawn so labels are never drawn over by another wire.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="line">The line whose label is rendered.</param>
    /// <param name="theme">Visual theme providing font and color settings.</param>
    /// <param name="scale">Uniform scale factor.</param>
    /// <param name="midX">Pre-computed label centre X in logical pixels.</param>
    /// <param name="midY">Pre-computed label centre Y in logical pixels.</param>
    private static void RenderLineLabel(StringBuilder sb, LayoutLine line, Theme theme, double scale, double midX, double midY)
    {
        if (line.MidpointLabel is null)
        {
            return;
        }

        sb.Append(
            $"""  <text x="{F(midX * scale)}" y="{F(midY * scale)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}" fill="{theme.StrokeColor}" text-anchor="middle" dominant-baseline="middle" filter="url(#label-bg)">{EscapeXml(line.MidpointLabel)}</text>""");
        sb.AppendLine();
    }

    /// <summary>
    /// Renders one connection end label (a role name or multiplicity slot) as a centered text element
    /// with the same font as a midpoint label but no background (it sits beside its line, clear of it by construction, so a background could only erase a neighboring connector), at the position
    /// <see cref="ConnectorLabelPlacer.PlaceEndLabels"/> chose. Called in the final label pass so no wire
    /// draws over it.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="placement">The resolved end-label placement.</param>
    /// <param name="theme">Visual theme providing font and color settings.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderEndLabel(StringBuilder sb, EndLabelPlacement placement, Theme theme, double scale)
    {
        sb.Append(
            $"""  <text x="{F(placement.X * scale)}" y="{F(placement.Y * scale)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}" fill="{theme.StrokeColor}" text-anchor="middle" dominant-baseline="middle">{EscapeXml(placement.Text)}</text>""");
        sb.AppendLine();
    }

    /// <summary>Recursively collects all <see cref="LayoutBox"/> nodes from a node tree.</summary>
    /// <param name="nodes">Top-level nodes to walk.</param>
    /// <returns>Every box node, including those nested inside boxes or bands, parents before children.</returns>
    private static IEnumerable<LayoutBox> CollectBoxes(IReadOnlyList<LayoutNode> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case LayoutBox box:
                    yield return box;
                    foreach (var inner in CollectBoxes(box.Children))
                    {
                        yield return inner;
                    }

                    break;

                case LayoutBand band:
                    foreach (var inner in CollectBoxes(band.Children))
                    {
                        yield return inner;
                    }

                    break;

                default:
                    break;
            }
        }
    }

    /// <summary>Recursively collects all <see cref="LayoutPort"/> nodes from a node tree.</summary>
    /// <param name="nodes">Top-level nodes to walk.</param>
    /// <param name="ports">Receives every port found, including those nested inside boxes or bands.</param>
    private static void CollectPorts(IReadOnlyList<LayoutNode> nodes, List<LayoutPort> ports)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case LayoutPort port:
                    ports.Add(port);
                    break;

                case LayoutBox box:
                    CollectPorts(box.Children, ports);
                    break;

                case LayoutBand band:
                    CollectPorts(band.Children, ports);
                    break;

                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Returns the node list with every line whose marker end sits on a port centre shortened by
    /// <see cref="NotationMetrics.PortHalfSize"/> along its end segment, so the painted marker tip lands on
    /// the port square's outer edge instead of being hidden under the glyph. The layout tree itself (and the
    /// connector label placement computed from it) is not modified.
    /// </summary>
    /// <param name="nodes">The tree's top-level nodes.</param>
    /// <returns>The original list when the tree has no ports; otherwise a rewritten copy.</returns>
    private static IReadOnlyList<LayoutNode> ShortenPortEnds(IReadOnlyList<LayoutNode> nodes)
    {
        var ports = new List<LayoutPort>();
        CollectPorts(nodes, ports);
        return ports.Count == 0 ? nodes : RewriteForPorts(nodes, ports);
    }

    /// <summary>Rewrites the lines of a node list (recursively) for <see cref="ShortenPortEnds"/>.</summary>
    /// <param name="nodes">The nodes to rewrite.</param>
    /// <param name="ports">Every port in the tree.</param>
    /// <returns>The rewritten nodes.</returns>
    private static List<LayoutNode> RewriteForPorts(IReadOnlyList<LayoutNode> nodes, List<LayoutPort> ports)
    {
        var result = new List<LayoutNode>(nodes.Count);
        foreach (var node in nodes)
        {
            result.Add(node switch
            {
                LayoutLine line => ShortenLineForPorts(line, ports),
                LayoutBox box => box with { Children = RewriteForPorts(box.Children, ports) },
                LayoutBand band => band with { Children = RewriteForPorts(band.Children, ports) },
                _ => node,
            });
        }

        return result;
    }

    /// <summary>Shortens a line's marker ends that sit on a port centre (see <see cref="ShortenPortEnds"/>).</summary>
    /// <param name="line">The line.</param>
    /// <param name="ports">Every port in the tree.</param>
    /// <returns>The line, or a copy with shortened end waypoints.</returns>
    private static LayoutLine ShortenLineForPorts(LayoutLine line, List<LayoutPort> ports)
    {
        var waypoints = line.Waypoints;
        if (waypoints.Count < 2)
        {
            return line;
        }

        var first = line.SourceEnd == EndMarkerStyle.None ? null : RetreatFromPort(waypoints[0], waypoints[1], ports);
        var last = line.TargetEnd == EndMarkerStyle.None ? null : RetreatFromPort(waypoints[^1], waypoints[^2], ports);
        if (first is null && last is null)
        {
            return line;
        }

        var shortened = new List<Point2D>(waypoints);
        if (first is not null)
        {
            shortened[0] = first;
        }

        if (last is not null)
        {
            shortened[^1] = last;
        }

        return line with { Waypoints = shortened };
    }

    /// <summary>
    /// Moves <paramref name="end"/> toward <paramref name="adjacent"/> by <see cref="NotationMetrics.PortHalfSize"/>
    /// when it coincides with a port centre and the end segment is longer than that.
    /// </summary>
    /// <param name="end">The line end point.</param>
    /// <param name="adjacent">The adjacent waypoint.</param>
    /// <param name="ports">Every port in the tree.</param>
    /// <returns>The retreated point, or <see langword="null"/> when the end is not on a port.</returns>
    private static Point2D? RetreatFromPort(Point2D end, Point2D adjacent, List<LayoutPort> ports)
    {
        const double portTolerance = 0.5;
        var onPort = ports.Find(port => Math.Abs(port.CentreX - end.X) <= portTolerance && Math.Abs(port.CentreY - end.Y) <= portTolerance);
        if (onPort is null)
        {
            return null;
        }

        // The retreat is the glyph's own half size (4 px for the legacy square, more for an outlined glyph
        // or proxy circle), so the painted tip lands on the glyph's outer edge whichever glyph it is.
        var halfSize = NotationMetrics.PortGlyphHalfSizeFor(onPort.Appearance);

        var dx = adjacent.X - end.X;
        var dy = adjacent.Y - end.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length <= halfSize)
        {
            return null;
        }

        var fraction = halfSize / length;
        return new Point2D(end.X + (dx * fraction), end.Y + (dy * fraction));
    }

    /// <summary>Recursively collects all <see cref="LayoutLine"/> nodes from a node tree.</summary>
    /// <param name="nodes">Top-level nodes to walk.</param>
    /// <returns>Every line node, including those nested inside boxes or bands.</returns>
    private static IEnumerable<LayoutLine> CollectLines(IReadOnlyList<LayoutNode> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case LayoutLine line:
                    yield return line;
                    break;

                case LayoutBox box:
                    foreach (var inner in CollectLines(box.Children))
                    {
                        yield return inner;
                    }

                    break;

                case LayoutBand band:
                    foreach (var inner in CollectLines(band.Children))
                    {
                        yield return inner;
                    }

                    break;

                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Builds the SVG path <c>d</c> attribute string for a polyline, applying arc-at-bend
    /// corner rounding for each interior waypoint when <paramref name="cornerRadius"/> is
    /// greater than zero.
    /// </summary>
    /// <remarks>
    /// For each interior waypoint the incoming segment is shortened by
    /// <paramref name="cornerRadius"/> and an SVG arc (<c>A</c> command) bridges the gap.
    /// The arc sweep direction is determined by the cross product of the incoming and outgoing
    /// direction vectors: positive cross product (clockwise turn in SVG screen space) uses
    /// sweep-flag 1; negative uses sweep-flag 0. The radius is clamped to half the shorter
    /// adjacent segment so the arc never overshoots.
    /// </remarks>
    /// <param name="waypoints">Ordered waypoints; must contain at least 2 entries.</param>
    /// <param name="cornerRadius">Corner rounding radius in logical pixels; 0 disables arcs.</param>
    /// <param name="scale">Uniform scale factor.</param>
    /// <param name="sourceMarkerLength">
    /// Rendered along-line length of the end marker at the first waypoint (source endpoint), in logical
    /// pixels (<see cref="NotationMetrics.RenderedAlongLineLength"/>); zero without a marker.
    /// </param>
    /// <param name="targetMarkerLength">
    /// Rendered along-line length of the end marker at the last waypoint (target endpoint), in logical
    /// pixels; zero without a marker.
    /// </param>
    /// <returns>SVG path data string starting with <c>M</c>.</returns>
    private static string BuildLinePath(
        IReadOnlyList<Point2D> waypoints,
        double cornerRadius,
        double scale,
        double sourceMarkerLength,
        double targetMarkerLength)
    {
        var sb = new StringBuilder();
        var first = waypoints[0];
        sb.Append($"M {F(first.X * scale)} {F(first.Y * scale)}");

        if (cornerRadius <= 0)
        {
            // No corner rounding: plain M/L path
            for (var i = 1; i < waypoints.Count; i++)
            {
                var wp = waypoints[i];
                sb.Append($" L {F(wp.X * scale)} {F(wp.Y * scale)}");
            }

            return sb.ToString();
        }

        // Shared tolerance for both the direction-collinearity check and the degenerate
        // zero-length-segment check below - a single named constant avoids a second magic
        // number drifting out of sync with the first.
        const double DirectionTolerance = 0.001;

        // Arc-at-bends: replace each interior waypoint with a shortened L + arc A command
        for (var i = 1; i < waypoints.Count; i++)
        {
            var cur = waypoints[i];

            var isInterior = i < waypoints.Count - 1;
            if (!isInterior)
            {
                // Last waypoint: plain line to the endpoint
                sb.Append($" L {F(cur.X * scale)} {F(cur.Y * scale)}");
                continue;
            }

            var prev = waypoints[i - 1];
            var next = waypoints[i + 1];

            // Incoming direction from prev to cur
            var inDx = cur.X - prev.X;
            var inDy = cur.Y - prev.Y;
            var inLen = Math.Sqrt(inDx * inDx + inDy * inDy);

            // Outgoing direction from cur to next
            var outDx = next.X - cur.X;
            var outDy = next.Y - cur.Y;
            var outLen = Math.Sqrt(outDx * outDx + outDy * outDy);

            // Collinearity check: if the incoming and outgoing directions are parallel and
            // same-sense (normalized cross product ~0 and dot product positive, i.e. not a
            // reversal), this waypoint sits on a straight run rather than a real corner. Skip
            // the arc-rounding entirely so the line continues straight through it - rounding a
            // waypoint that is not an actual turn would draw a spurious bump in the path. Both
            // lengths are checked against DirectionTolerance first so this never divides by a
            // near-zero length; genuinely degenerate segments fall through to the next check.
            if (inLen >= DirectionTolerance && outLen >= DirectionTolerance)
            {
                var crossNorm = (inDx * outDy - inDy * outDx) / (inLen * outLen);
                var dotNorm = (inDx * outDx + inDy * outDy) / (inLen * outLen);
                if (Math.Abs(crossNorm) <= DirectionTolerance && dotNorm > 0)
                {
                    // Collinear waypoint: no corner to round, continue straight through it
                    sb.Append($" L {F(cur.X * scale)} {F(cur.Y * scale)}");
                    continue;
                }
            }

            if (inLen < DirectionTolerance || outLen < DirectionTolerance)
            {
                // Degenerate segment: fall back to a plain line command
                sb.Append($" L {F(cur.X * scale)} {F(cur.Y * scale)}");
                continue;
            }

            // Normalize both direction vectors
            var inNx = inDx / inLen;
            var inNy = inDy / inLen;
            var outNx = outDx / outLen;
            var outNy = outDy / outLen;

            // Clamp radius so the arc never overshoots either adjacent segment
            var r = Math.Min(cornerRadius, Math.Min(inLen / 2.0, outLen / 2.0));

            // Decoration-aware final-corner clamp: keep the rounded corner from intruding into an
            // end-marker decoration. The first interior corner must leave at least the source marker's
            // along-line length of straight run after the source endpoint; the last interior corner
            // must leave at least the target marker's along-line length of straight run before the
            // target endpoint. For forward edges (whose approaches are already long) this is a no-op.
            if (i == 1)
            {
                r = Math.Min(r, inLen - sourceMarkerLength);
            }

            if (i == waypoints.Count - 2)
            {
                r = Math.Min(r, outLen - targetMarkerLength);
            }

            if (r <= 0.0)
            {
                // The decoration consumes the whole approach: fall back to a plain line (no arc).
                sb.Append($" L {F(cur.X * scale)} {F(cur.Y * scale)}");
                continue;
            }

            // Endpoint of the shortened incoming segment (just before the corner)
            var shortEndX = cur.X - inNx * r;
            var shortEndY = cur.Y - inNy * r;

            // Start of the outgoing segment after the arc (just past the corner)
            var shortStartX = cur.X + outNx * r;
            var shortStartY = cur.Y + outNy * r;

            // Cross product z-component determines clockwise vs counter-clockwise arc in SVG
            // (positive cross = clockwise screen turn = sweep-flag 1)
            var cross = inNx * outNy - inNy * outNx;
            var sweep = cross > 0 ? "1" : "0";

            sb.Append(
                $" L {F(shortEndX * scale)} {F(shortEndY * scale)}");
            sb.Append(
                $" A {F(r * scale)} {F(r * scale)} 0 0 {sweep} {F(shortStartX * scale)} {F(shortStartY * scale)}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Renders a <see cref="LayoutLabel"/> as an SVG <c>&lt;text&gt;</c> element with
    /// <c>text-anchor</c> derived from <see cref="TextAlign"/>.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="label">The label node to render.</param>
    /// <param name="theme">Visual theme providing font and color settings.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderLabel(StringBuilder sb, LayoutLabel label, Theme theme, double scale)
    {
        var x = label.X * scale;
        var y = label.Y * scale;
        var anchor = label.Align switch
        {
            TextAlign.Center => TextAnchorMiddle,
            TextAlign.Right => "end",
            _ => "start"
        };
        var fontWeight = label.Weight == FontWeight.Bold ? "bold" : "normal";
        var fontStyle = label.Style == FontStyle.Italic ? "italic" : "normal";

        // Only constrain width when the text would overflow MaxWidth (no stretching of short text).
        var textLengthAttr = FitTextLength(label.Text, label.FontSize, label.MaxWidth, scale);

        sb.Append(
            $"""  <text x="{F(x)}" y="{F(y)}" font-family="Noto Sans, sans-serif" font-size="{F(label.FontSize * scale)}" font-weight="{fontWeight}" font-style="{fontStyle}" fill="{theme.StrokeColor}" text-anchor="{anchor}" dominant-baseline="middle"{textLengthAttr}>{EscapeXml(label.Text)}</text>""");
        sb.AppendLine();
    }

    /// <summary>
    /// Renders a <see cref="LayoutPort"/> as a filled 8×8 <c>&lt;rect&gt;</c> centered at
    /// the port position, with an optional <c>&lt;text&gt;</c> label offset away from the
    /// attached edge.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="port">The port node to render.</param>
    /// <param name="theme">Visual theme providing stroke color and font settings.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderPort(StringBuilder sb, LayoutPort port, Theme theme, double scale)
    {
        // Glyph: the legacy filled 8x8 square (byte-identical markup) unless the appearance asks for an
        // outlined glyph (rounded square with an optional dashed outline and direction arrow) or a proxy
        // circle. The geometry comes from NotationMetrics so the raster renderer draws the same shapes.
        switch (port.Appearance?.ResolvedGlyph)
        {
            case PortGlyph.Outlined:
                RenderOutlinedPortGlyph(sb, port, theme, scale);
                break;

            case PortGlyph.Proxy:
                sb.Append(
                    $"""  <circle cx="{F(port.CentreX * scale)}" cy="{F(port.CentreY * scale)}" r="{F(NotationMetrics.PortProxyRadius * scale)}" fill="{theme.StrokeColor}" stroke="{theme.BackgroundColor}" stroke-width="{F(PortGlyphStrokeWidth * scale)}"/>""");
                sb.AppendLine();
                break;

            default:
                var rx = (port.CentreX - NotationMetrics.PortHalfSize) * scale;
                var ry = (port.CentreY - NotationMetrics.PortHalfSize) * scale;
                var rs = NotationMetrics.PortSize * scale;
                sb.Append(
                    $"""  <rect x="{F(rx)}" y="{F(ry)}" width="{F(rs)}" height="{F(rs)}" fill="{theme.StrokeColor}" stroke="{theme.BackgroundColor}" stroke-width="{F(PortGlyphStrokeWidth * scale)}"/>""");
                sb.AppendLine();
                break;
        }

        // Labels. InternalLabel (when present) always renders inward, toward the box interior, beside
        // the port glyph. ExternalLabel renders inward too whenever there is no InternalLabel (a plain,
        // non-boundary port — byte-identical to the single-label behavior every existing call site
        // relies on), and only renders on the outward face when an InternalLabel is also present (a
        // genuine boundary port), mirroring the inward offset across the port centre.
        if (port.InternalLabel != null)
        {
            EmitPortLabel(sb, port, port.InternalLabel, port.Side, theme, scale);
        }

        if (NotationMetrics.IsOutwardLabel(port))
        {
            EmitOutwardPortLabel(sb, port, theme, scale);
        }
        else if (port.ExternalLabel != null)
        {
            var side = port.InternalLabel != null ? OppositeSide(port.Side) : port.Side;
            EmitPortLabel(sb, port, port.ExternalLabel, side, theme, scale);
        }
    }

    /// <summary>
    /// Renders an outlined port glyph: a background-filled rounded square with the theme stroke (dashed for
    /// a reference port), then the direction arrow as one polyline per segment of
    /// <see cref="NotationMetrics.PortArrowSegments"/> around the port centre.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="port">The port whose glyph is drawn.</param>
    /// <param name="theme">Visual theme providing stroke and background colors.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderOutlinedPortGlyph(StringBuilder sb, LayoutPort port, Theme theme, double scale)
    {
        var half = NotationMetrics.PortGlyphHalfSize;
        var corner = NotationMetrics.PortGlyphCornerRadius * scale;
        var dash = DashAttribute(port.Appearance?.OutlineStyle ?? LineStyle.Solid);
        sb.Append(
            $"""  <rect x="{F((port.CentreX - half) * scale)}" y="{F((port.CentreY - half) * scale)}" width="{F(2.0 * half * scale)}" height="{F(2.0 * half * scale)}" rx="{F(corner)}" ry="{F(corner)}" fill="{theme.BackgroundColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"{dash}/>""");
        sb.AppendLine();

        foreach (var segment in NotationMetrics.PortArrowSegments(port.Appearance?.Direction ?? PortDirection.None, port.Side))
        {
            var points = string.Join(" ", segment.Select(point => $"{F((port.CentreX + point.X) * scale)},{F((port.CentreY + point.Y) * scale)}"));
            sb.Append(
                $"""  <polyline points="{points}" fill="none" stroke="{theme.StrokeColor}" stroke-width="{F(PortGlyphStrokeWidth * scale)}" stroke-linecap="round" stroke-linejoin="round"/>""");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Emits a port's external label outside the owning box, beside the connector, at the anchor
    /// <see cref="NotationMetrics.PortOutwardLabelAnchor"/> returns (never squeezed: it lies outside the box).
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="port">The port whose outward label is drawn.</param>
    /// <param name="theme">Visual theme providing font settings.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void EmitOutwardPortLabel(StringBuilder sb, LayoutPort port, Theme theme, double scale)
    {
        var anchor = NotationMetrics.PortOutwardLabelAnchor(port, theme);
        var textAnchor = anchor.Align == PortLabelAlign.End ? "end" : "start";
        sb.Append(
            $"""  <text x="{F(anchor.X * scale)}" y="{F(anchor.Y * scale)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}" fill="{theme.StrokeColor}" text-anchor="{textAnchor}" dominant-baseline="middle">{EscapeXml(port.ExternalLabel!)}</text>""");
        sb.AppendLine();
    }

    /// <summary>
    /// Returns the rectangles the ports occupy — each glyph plus each outward label — that end labels must
    /// keep clear of.
    /// </summary>
    /// <param name="ports">Every port in the tree.</param>
    /// <param name="theme">Visual theme providing font settings.</param>
    /// <returns>The occupied rectangles.</returns>
    private static List<LabelPlacement> PortOccupiedRects(List<LayoutPort> ports, Theme theme)
    {
        var rects = new List<LabelPlacement>(ports.Count);
        foreach (var port in ports)
        {
            rects.Add(NotationMetrics.PortGlyphBounds(port));
            if (NotationMetrics.IsOutwardLabel(port))
            {
                // Padded along the text so an end label keeps a visible gap from the port label beside it.
                var bounds = NotationMetrics.PortOutwardLabelBounds(port, port.ExternalLabel!, theme);
                rects.Add(bounds with { HalfWidth = bounds.HalfWidth + OutwardLabelEndLabelGap });
            }
        }

        return rects;
    }

    /// <summary>Returns the box edge opposite <paramref name="side"/>, used to place an outward label.</summary>
    private static PortSide OppositeSide(PortSide side) => side switch
    {
        PortSide.Top => PortSide.Bottom,
        PortSide.Bottom => PortSide.Top,
        PortSide.Left => PortSide.Right,
        _ => PortSide.Left,
    };

    /// <summary>
    /// Emits one port <c>&lt;text&gt;</c> label offset from the port center toward the interior of the
    /// box on <paramref name="offsetSide"/> (which equals the port's own side for an inward label and the
    /// opposite side for an outward one), using the same offset formula for both so an inward and an
    /// outward label on one boundary port sit symmetrically about the port center. A boundary port (one
    /// carrying both an <see cref="LayoutPort.InternalLabel"/> and an <see cref="LayoutPort.ExternalLabel"/>)
    /// reserves extra clearance beyond the plain port-glyph offset: unlike a plain port's single label —
    /// always drawn on the side <em>away</em> from wherever its own connecting edge's arrowhead could
    /// land — a boundary port's external label is deliberately drawn on the <em>outward</em> face, the
    /// same side its external approach edge may terminate with an end-marker glyph
    /// (<see cref="NotationMetrics.EndMarkerLength"/> long). Without this extra clearance the label text
    /// and that marker visually collide (see the "command"/"dispatch" gallery regression). Applying the
    /// same widened offset to both the internal and external label keeps them symmetric about the port
    /// center even though only the external side is ever actually at risk in practice.
    /// </summary>
    private static void EmitPortLabel(StringBuilder sb, LayoutPort port, string text, PortSide offsetSide, Theme theme, double scale)
    {
        var offset = NotationMetrics.PortGlyphHalfSizeFor(port.Appearance) + theme.LabelPadding
            + (port.InternalLabel != null && port.ExternalLabel != null ? NotationMetrics.EndMarkerLength : 0.0);
        var (labelX, labelY, anchor) = offsetSide switch
        {
            PortSide.Top => (port.CentreX, port.CentreY + offset + theme.FontSizeBody, TextAnchorMiddle),
            PortSide.Bottom => (port.CentreX, port.CentreY - offset, TextAnchorMiddle),
            PortSide.Left => (port.CentreX + offset, port.CentreY + theme.FontSizeBody, "start"),
            _ => (port.CentreX - offset, port.CentreY + theme.FontSizeBody, "end")
        };

        sb.Append(
            $"""  <text x="{F(labelX * scale)}" y="{F(labelY * scale)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}" fill="{theme.StrokeColor}" text-anchor="{anchor}" dominant-baseline="middle"{FitTextLength(text, theme.FontSizeBody, port.MaxLabelWidth, scale, useAccurateEstimator: true)}>{EscapeXml(text)}</text>""");
        sb.AppendLine();
    }

    /// <summary>
    /// Renders a <see cref="LayoutBadge"/> as shape-specific SVG elements centered at the
    /// badge position, with an optional <c>&lt;text&gt;</c> label to the right.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="badge">The badge node to render.</param>
    /// <param name="theme">Visual theme providing stroke color, width, and font settings.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderBadge(StringBuilder sb, LayoutBadge badge, Theme theme, double scale)
    {
        var cx = badge.CentreX * scale;
        var cy = badge.CentreY * scale;
        var r = badge.Size / 2.0 * scale;
        var sw = F(theme.StrokeWidth);

        // Draw the badge shape centered at (cx, cy) within bounding radius r
        switch (badge.Shape)
        {
            case BadgeShape.FilledCircle:
                sb.Append(
                    $"""  <circle cx="{F(cx)}" cy="{F(cy)}" r="{F(r)}" fill="{theme.StrokeColor}"/>""");
                sb.AppendLine();
                break;

            case BadgeShape.Bullseye:
                // Outer filled circle + white inner circle for the ring effect
                sb.Append(
                    $"""  <circle cx="{F(cx)}" cy="{F(cy)}" r="{F(r)}" fill="{theme.StrokeColor}"/>""");
                sb.AppendLine();
                sb.Append(
                    $"""  <circle cx="{F(cx)}" cy="{F(cy)}" r="{F(r * NotationMetrics.BadgeBullseyeInnerFraction)}" fill="white" stroke="{theme.StrokeColor}" stroke-width="{sw}"/>""");
                sb.AppendLine();
                break;

            case BadgeShape.Diamond:
                sb.Append(
                    $"""  <polygon points="{F(cx)},{F(cy - r)} {F(cx + r)},{F(cy)} {F(cx)},{F(cy + r)} {F(cx - r)},{F(cy)}" fill="none" stroke="{theme.StrokeColor}" stroke-width="{sw}"/>""");
                sb.AppendLine();
                break;

            case BadgeShape.HorizontalBar:
                sb.Append(
                    $"""  <line x1="{F(cx - r * NotationMetrics.BadgeBarLengthFraction)}" y1="{F(cy)}" x2="{F(cx + r * NotationMetrics.BadgeBarLengthFraction)}" y2="{F(cy)}" stroke="{theme.StrokeColor}" stroke-width="{sw}"/>""");
                sb.AppendLine();
                break;

            case BadgeShape.VerticalBar:
                sb.Append(
                    $"""  <line x1="{F(cx)}" y1="{F(cy - r * NotationMetrics.BadgeBarLengthFraction)}" x2="{F(cx)}" y2="{F(cy + r * NotationMetrics.BadgeBarLengthFraction)}" stroke="{theme.StrokeColor}" stroke-width="{sw}"/>""");
                sb.AppendLine();
                break;

            case BadgeShape.ThickHorizontalBar:
                // Filled bar: Size is the full length along the axis, Thickness (default notation
                // thickness) across it
                var hBarThickness = (badge.Thickness ?? NotationMetrics.ThickBarThickness) * scale;
                sb.Append(
                    $"""  <rect x="{F(cx - r)}" y="{F(cy - hBarThickness / 2.0)}" width="{F(r * 2.0)}" height="{F(hBarThickness)}" fill="{theme.StrokeColor}"/>""");
                sb.AppendLine();
                break;

            case BadgeShape.ThickVerticalBar:
                var vBarThickness = (badge.Thickness ?? NotationMetrics.ThickBarThickness) * scale;
                sb.Append(
                    $"""  <rect x="{F(cx - vBarThickness / 2.0)}" y="{F(cy - r)}" width="{F(vBarThickness)}" height="{F(r * 2.0)}" fill="{theme.StrokeColor}"/>""");
                sb.AppendLine();
                break;

            case BadgeShape.CrossedCircle:
                // Hollow circle (theme background fill) crossed by an X whose arms end on the circle
                var arm = r * NotationMetrics.CrossedCircleArmFraction;
                sb.Append(
                    $"""  <circle cx="{F(cx)}" cy="{F(cy)}" r="{F(r)}" fill="{theme.BackgroundColor}" stroke="{theme.StrokeColor}" stroke-width="{sw}"/>""");
                sb.AppendLine();
                sb.Append(
                    $"""  <line x1="{F(cx - arm)}" y1="{F(cy - arm)}" x2="{F(cx + arm)}" y2="{F(cy + arm)}" stroke="{theme.StrokeColor}" stroke-width="{sw}"/>""");
                sb.AppendLine();
                sb.Append(
                    $"""  <line x1="{F(cx - arm)}" y1="{F(cy + arm)}" x2="{F(cx + arm)}" y2="{F(cy - arm)}" stroke="{theme.StrokeColor}" stroke-width="{sw}"/>""");
                sb.AppendLine();
                break;

            default:
                // Unknown badge shapes are skipped for forward compatibility
                break;
        }

        // Optional label to the right of the bounding circle (of the bar for a thick vertical bar)
        if (badge.Label != null)
        {
            var labelHalfExtent = badge.Shape == BadgeShape.ThickVerticalBar
                ? (badge.Thickness ?? NotationMetrics.ThickBarThickness) / 2.0
                : badge.Size / 2.0;
            var labelX = (badge.CentreX + labelHalfExtent + theme.LabelPadding) * scale;
            var labelY = (badge.CentreY + theme.FontSizeBody / 2.0) * scale;
            sb.Append(
                $"""  <text x="{F(labelX)}" y="{F(labelY)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}" fill="{theme.StrokeColor}" text-anchor="start" dominant-baseline="middle">{EscapeXml(badge.Label)}</text>""");
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutBand"/> as a <c>&lt;rect&gt;</c> with a depth-0 fill,
    /// an optional label (rotated for Horizontal orientation, horizontal for Vertical),
    /// then recursively renders its children.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="band">The band node to render.</param>
    /// <param name="theme">Visual theme providing fill colors, stroke, and font settings.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderBand(StringBuilder sb, LayoutBand band, Theme theme, double scale)
    {
        var x = band.X * scale;
        var y = band.Y * scale;
        var w = band.Width * scale;
        var h = band.Height * scale;
        var fillColor = theme.DepthFillColors[0];

        sb.Append(
            $"""  <rect x="{F(x)}" y="{F(y)}" width="{F(w)}" height="{F(h)}" fill="{fillColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"{DashAttribute(band.OutlineStyle)}/>""");
        sb.AppendLine();

        // Draw the optional label; Horizontal → rotated 90° CCW on left edge, Vertical → top
        if (band.Label != null)
        {
            if (band.Orientation == BandOrientation.Horizontal)
            {
                // Label center on the left edge strip, rotated 90° CCW
                var labelCx = (band.X + theme.LabelPadding + theme.FontSizeBody / 2.0) * scale;
                var labelCy = (band.Y + band.Height / 2.0) * scale;
                sb.Append(
                    $"""  <text x="0" y="0" transform="translate({F(labelCx)},{F(labelCy)}) rotate(-90)" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}" fill="{theme.StrokeColor}" text-anchor="middle" dominant-baseline="middle">{EscapeXml(band.Label)}</text>""");
                sb.AppendLine();
            }
            else
            {
                // Horizontal label at the top of the band
                var textX = (band.X + band.Width / 2.0) * scale;
                var textY = (band.Y + theme.LabelPadding + theme.FontSizeBody / 2.0) * scale;
                sb.Append(
                    $"""  <text x="{F(textX)}" y="{F(textY)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}" fill="{theme.StrokeColor}" text-anchor="middle" dominant-baseline="middle">{EscapeXml(band.Label)}</text>""");
                sb.AppendLine();
            }
        }

        // Render children recursively
        foreach (var child in band.Children)
        {
            RenderNode(sb, child, theme, scale);
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutLifeline"/> as a header <c>&lt;rect&gt;</c> with a
    /// centered label, followed by a dashed <c>&lt;line&gt;</c> stem running from the
    /// bottom of the header to <see cref="LayoutLifeline.BottomY"/>.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="lifeline">The lifeline node to render.</param>
    /// <param name="theme">Visual theme providing colors, font, and stroke settings.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderLifeline(StringBuilder sb, LayoutLifeline lifeline, Theme theme, double scale)
    {
        // Header box: centered at CentreX, top edge at TopY
        var headerLeft = lifeline.CentreX - lifeline.HeaderWidth / 2.0;
        var hx = headerLeft * scale;
        var hy = lifeline.TopY * scale;
        var hw = lifeline.HeaderWidth * scale;
        var hh = lifeline.HeaderHeight * scale;
        var fillColor = theme.DepthFillColors[0];
        var appearance = lifeline.HeaderAppearance ?? new BoxAppearance();

        // Rounded headers use the box corner radius (caller-supplied or the theme's rounded-rectangle radius)
        var cornerStr = string.Empty;
        if (lifeline.HeaderShape == BoxShape.RoundedRectangle)
        {
            var radius = lifeline.HeaderCornerRadius.HasValue
                ? Math.Max(0.0, lifeline.HeaderCornerRadius.Value)
                : NotationMetrics.RoundedRectRadius(theme);
            if (radius > 0)
            {
                cornerStr = $" rx=\"{F(radius * scale)}\" ry=\"{F(radius * scale)}\"";
            }
        }

        sb.Append(
            $"""  <rect x="{F(hx)}" y="{F(hy)}" width="{F(hw)}" height="{F(hh)}" fill="{fillColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"{cornerStr}{DashAttribute(appearance.OutlineStyle)}/>""");
        sb.AppendLine();

        // Label (and optional «keyword» line above it) centered within the header box
        var textX = lifeline.CentreX * scale;
        var textY = (lifeline.TopY + lifeline.HeaderHeight / 2.0) * scale;
        if (lifeline.Keyword != null)
        {
            var blockHeight = BoxMetrics.LifelineHeaderHeight(theme, hasKeyword: true);
            var blockTop = lifeline.TopY + Math.Max(0.0, (lifeline.HeaderHeight - blockHeight) / 2.0);
            var keywordY = (blockTop + theme.LabelPadding + theme.FontSizeBody / 2.0) * scale;
            textY = (blockTop + (2.0 * theme.LabelPadding) + theme.FontSizeBody + theme.FontSizeBody / 2.0) * scale;
            sb.Append(
                $"""  <text x="{F(textX)}" y="{F(keywordY)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}"{TextStyleAttributes(appearance.ResolvedKeywordStyle)} fill="{theme.StrokeColor}" text-anchor="middle" dominant-baseline="middle">{EscapeXml("\u00AB" + lifeline.Keyword + "\u00BB")}</text>""");
            sb.AppendLine();
        }

        sb.Append(
            $"""  <text x="{F(textX)}" y="{F(textY)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}"{TextStyleAttributes(appearance.ResolvedNameStyle)} fill="{theme.StrokeColor}" text-anchor="middle" dominant-baseline="middle">{EscapeXml(lifeline.Label)}</text>""");
        sb.AppendLine();

        // Dashed vertical stem from the bottom of the header to BottomY
        var stemX = lifeline.CentreX * scale;
        var stemTopY = (lifeline.TopY + lifeline.HeaderHeight) * scale;
        var stemBottomY = lifeline.BottomY * scale;
        sb.Append(
            $"""  <line x1="{F(stemX)}" y1="{F(stemTopY)}" x2="{F(stemX)}" y2="{F(stemBottomY)}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}" stroke-dasharray="6 3"/>""");
        sb.AppendLine();
    }

    /// <summary>
    /// Renders a <see cref="LayoutActivation"/> as a narrow white-filled
    /// <c>&lt;rect&gt;</c> with a stroke border, centered at
    /// <see cref="LayoutActivation.CentreX"/>.
    /// </summary>
    /// <remarks>
    /// The bar width is <c>Theme.LabelPadding * 2</c> so it scales proportionally with
    /// the diagram's text padding.
    /// </remarks>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="activation">The activation node to render.</param>
    /// <param name="theme">Visual theme providing stroke settings and padding.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderActivation(StringBuilder sb, LayoutActivation activation, Theme theme, double scale)
    {
        // Bar width = LabelPadding * 2, centered at CentreX
        var halfWidth = theme.LabelPadding;
        var ax = (activation.CentreX - halfWidth) * scale;
        var ay = activation.TopY * scale;
        var aw = halfWidth * 2.0 * scale;
        var ah = (activation.BottomY - activation.TopY) * scale;

        sb.Append(
            $"""  <rect x="{F(ax)}" y="{F(ay)}" width="{F(aw)}" height="{F(ah)}" fill="white" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
        sb.AppendLine();
    }

    /// <summary>
    /// Renders a <see cref="LayoutGrid"/> as a bordered table. Header rows are filled with
    /// the depth-1 theme color; body rows use the depth-0 color. Each cell contains a
    /// <c>&lt;text&gt;</c> element aligned per <see cref="LayoutGridCell.Align"/>.
    /// </summary>
    /// <param name="sb">String builder receiving the SVG markup.</param>
    /// <param name="grid">The grid node to render.</param>
    /// <param name="theme">Visual theme providing fill colors, stroke, and font settings.</param>
    /// <param name="scale">Uniform scale factor.</param>
    private static void RenderGrid(StringBuilder sb, LayoutGrid grid, Theme theme, double scale)
    {
        // Header rows use depth-1 color; body rows use depth-0 color
        var headerFill = theme.DepthFillColors[1 % theme.DepthFillColors.Count];
        var bodyFill = theme.DepthFillColors[0];

        var currentY = grid.Y;
        foreach (var row in grid.Rows)
        {
            // Row height = maximum cell height in this row
            var rowHeight = 0.0;
            foreach (var cell in row.Cells)
            {
                rowHeight = Math.Max(rowHeight, cell.Height);
            }

            var fillColor = row.IsHeader ? headerFill : bodyFill;
            var currentX = grid.X;

            foreach (var cell in row.Cells)
            {
                var cx = currentX * scale;
                var cy = currentY * scale;
                var cw = cell.Width * scale;
                var ch = rowHeight * scale;

                // Cell background and border
                sb.Append(
                    $"""  <rect x="{F(cx)}" y="{F(cy)}" width="{F(cw)}" height="{F(ch)}" fill="{fillColor}" stroke="{theme.StrokeColor}" stroke-width="{F(theme.StrokeWidth)}"/>""");
                sb.AppendLine();

                // Cell text properties are computed in a helper to stay within complexity limits
                var (anchor, textX, fontWeightAttr) = GetCellTextProperties(cell, currentX, theme, row.IsHeader);
                var textY = currentY + rowHeight / 2.0;

                sb.Append(
                    $"""  <text x="{F(textX * scale)}" y="{F(textY * scale)}" font-family="Noto Sans, sans-serif" font-size="{F(theme.FontSizeBody * scale)}"{fontWeightAttr} fill="{theme.StrokeColor}" text-anchor="{anchor}" dominant-baseline="middle">{EscapeXml(cell.Text)}</text>""");
                sb.AppendLine();

                currentX += cell.Width;
            }

            currentY += rowHeight;
        }
    }

    /// <summary>
    /// Computes the SVG text-anchor, horizontal text position, and font-weight attribute for a
    /// single grid cell. Extracted from <see cref="RenderGrid"/> to reduce its cognitive complexity.
    /// </summary>
    /// <param name="cell">The grid cell whose alignment drives the computed values.</param>
    /// <param name="cellLeft">Logical X origin of the cell within the grid.</param>
    /// <param name="theme">Visual theme providing label padding.</param>
    /// <param name="isHeader">
    /// <see langword="true"/> when the cell belongs to a header row; controls font weight.
    /// </param>
    /// <returns>
    /// A tuple of (<c>Anchor</c>, <c>TextX</c>, <c>FontWeightAttr</c>) ready for SVG output.
    /// </returns>
    private static (string Anchor, double TextX, string FontWeightAttr) GetCellTextProperties(
        LayoutGridCell cell,
        double cellLeft,
        Theme theme,
        bool isHeader)
    {
        var anchor = cell.Align switch
        {
            TextAlign.Center => TextAnchorMiddle,
            TextAlign.Right => "end",
            _ => "start"
        };
        var textX = cell.Align switch
        {
            TextAlign.Center => cellLeft + cell.Width / 2.0,
            TextAlign.Right => cellLeft + cell.Width - theme.LabelPadding,
            _ => cellLeft + theme.LabelPadding
        };
        var fontWeightAttr = isHeader ? " font-weight=\"bold\"" : string.Empty;
        return (anchor, textX, fontWeightAttr);
    }

    /// <summary>
    /// Formats a double to two decimal places using invariant culture.
    /// </summary>
    /// <param name="value">Value to format.</param>
    /// <returns>String representation with exactly two decimal digits.</returns>
    private static string F(double value) =>
        value.ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>
    /// Escapes XML special characters in a string for safe embedding in SVG text content.
    /// </summary>
    /// <param name="text">Raw text to escape.</param>
    /// <returns>XML-safe text with <c>&lt;</c>, <c>&gt;</c>, and <c>&amp;</c> replaced by
    /// their entity equivalents.</returns>
    private static string EscapeXml(string text) =>
        text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
}
