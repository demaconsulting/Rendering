// <copyright file="NotationMetrics.Ports.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Abstractions;

/// <summary>
/// The horizontal alignment of a port label's text relative to its anchor point.
/// </summary>
public enum PortLabelAlign
{
    /// <summary>The text starts at the anchor and extends to its right.</summary>
    Start,

    /// <summary>The text ends at the anchor and extends to its left.</summary>
    End,
}

/// <summary>
/// Where a renderer draws an outward port label: the anchor point of the text, whose vertical centre is
/// <see cref="Y"/> (SVG <c>dominant-baseline="middle"</c>; the raster renderer centers the text on it), and
/// the horizontal alignment about <see cref="X"/>.
/// </summary>
/// <param name="X">Anchor X coordinate, in logical pixels.</param>
/// <param name="Y">Vertical centre of the text, in logical pixels.</param>
/// <param name="Align">Horizontal alignment of the text about <paramref name="X"/>.</param>
public readonly record struct PortLabelAnchor(double X, double Y, PortLabelAlign Align);

/// <content>
/// Shared geometry of the SysML-style port glyphs: sizes, direction arrows, and the outward label.
/// </content>
public static partial class NotationMetrics
{
    /// <summary>
    /// Half size, in logical pixels, of an outlined port glyph (a 12 × 12 rounded square), large enough to
    /// hold a direction arrow.
    /// </summary>
    public const double PortGlyphHalfSize = 6.0;

    /// <summary>Corner radius, in logical pixels, of an outlined port glyph.</summary>
    public const double PortGlyphCornerRadius = 2.0;

    /// <summary>Radius, in logical pixels, of a proxy port circle.</summary>
    public const double PortProxyRadius = 5.0;

    /// <summary>Half length, in logical pixels, of a direction arrow's shaft inside the glyph.</summary>
    public const double PortArrowHalfLength = 3.0;

    /// <summary>Size, in logical pixels, of each leg of a direction arrow's head (along and across the shaft).</summary>
    public const double PortArrowHeadSize = 2.0;

    /// <summary>Clearance, in logical pixels, kept between an outward label and the widest end marker beside it.</summary>
    private const double PortLabelConnectorClearance = 4.0;

    /// <summary>Half the height of a port label's text box, as a fraction of the body font size.</summary>
    private const double PortLabelHalfHeightFactor = 0.65;

    /// <summary>
    /// Returns the distance, in logical pixels, an outward port label keeps from the connector it sits
    /// beside: the half width of the widest end marker a connector can end with there (the diamond) at the
    /// theme's marker scale, plus a little clearance, so a label never touches an arrowhead on that
    /// connector.
    /// </summary>
    /// <param name="theme">The theme providing the marker scale.</param>
    /// <returns>The clearance from the connector axis.</returns>
    public static double PortLabelConnectorGap(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return (Math.Max(EndMarkerHalfWidth, DiamondHalfWidth) * MarkerScale(theme)) + PortLabelConnectorClearance;
    }

    /// <summary>
    /// Returns the half size, in logical pixels, of the glyph a port with <paramref name="appearance"/> is
    /// drawn with — the legacy <see cref="PortHalfSize"/> for no appearance or a filled square,
    /// <see cref="PortGlyphHalfSize"/> for an outlined glyph, and <see cref="PortProxyRadius"/> for a proxy
    /// circle. It is the single source for the marker shortening (so an arrowhead's tip lands on the glyph's
    /// outer edge) and the inward label offset in both renderers.
    /// </summary>
    /// <param name="appearance">The port appearance, or <see langword="null"/> for the legacy look.</param>
    /// <returns>The glyph half size.</returns>
    public static double PortGlyphHalfSizeFor(PortAppearance? appearance) =>
        appearance?.ResolvedGlyph switch
        {
            PortGlyph.Outlined => PortGlyphHalfSize,
            PortGlyph.Proxy => PortProxyRadius,
            _ => PortHalfSize,
        };

    /// <summary>
    /// Returns how far, in logical pixels, a renderer retracts a connector's marker end from a port centre
    /// along the end segment so the painted marker tip lands on the port glyph's outer edge: the glyph half
    /// size (<see cref="PortGlyphHalfSizeFor"/>) when the end carries a marker, coincides with a port centre
    /// (within 0.5 px), and the end segment is longer than that; zero otherwise. It is the single rule the
    /// renderers shorten lines by and the end-label placer measures from, so the two never disagree.
    /// </summary>
    /// <param name="end">The connector end point.</param>
    /// <param name="adjacent">The adjacent waypoint, which gives the end segment's length.</param>
    /// <param name="style">The end marker drawn at <paramref name="end"/>.</param>
    /// <param name="ports">Every port in the tree, or <see langword="null"/> for none.</param>
    /// <returns>The retraction distance; zero when the end is not retracted.</returns>
    public static double PortEndRetraction(Point2D end, Point2D adjacent, EndMarkerStyle style, IEnumerable<LayoutPort>? ports)
    {
        const double portTolerance = 0.5;
        if (style == EndMarkerStyle.None || ports is null)
        {
            return 0.0;
        }

        var onPort = ports.FirstOrDefault(port => Math.Abs(port.CentreX - end.X) <= portTolerance && Math.Abs(port.CentreY - end.Y) <= portTolerance);
        if (onPort is null)
        {
            return 0.0;
        }

        var halfSize = PortGlyphHalfSizeFor(onPort.Appearance);
        var dx = adjacent.X - end.X;
        var dy = adjacent.Y - end.Y;
        return Math.Sqrt((dx * dx) + (dy * dy)) > halfSize ? halfSize : 0.0;
    }

    /// <summary>
    /// Returns the polylines of a port's direction arrow in glyph-local coordinates (offsets from the port
    /// centre, in logical pixels), oriented for the face the port sits on: <see cref="PortDirection.In"/>
    /// points into the owning part (a left port's arrow points right, a right port's left, a top port's
    /// down, a bottom port's up), <see cref="PortDirection.Out"/> points away from it, and
    /// <see cref="PortDirection.InOut"/> is double-headed. The first polyline is the shaft; each head is a
    /// further polyline of three points.
    /// </summary>
    /// <param name="direction">The arrow direction.</param>
    /// <param name="side">The box face the port sits on.</param>
    /// <returns>The arrow polylines; empty for <see cref="PortDirection.None"/>.</returns>
    public static IReadOnlyList<IReadOnlyList<Point2D>> PortArrowSegments(PortDirection direction, PortSide side)
    {
        if (direction == PortDirection.None)
        {
            return [];
        }

        // Unit vector pointing into the owning part, and the perpendicular to it.
        var (nx, ny) = side switch
        {
            PortSide.Left => (1.0, 0.0),
            PortSide.Right => (-1.0, 0.0),
            PortSide.Top => (0.0, 1.0),
            _ => (0.0, -1.0),
        };
        var (px, py) = (-ny, nx);

        // A point at distance u along the arrow axis (into the part) and v across it.
        Point2D At(double u, double v) => new((u * nx) + (v * px), (u * ny) + (v * py));

        // An arrow head with its tip at the given axis position, opening back along the shaft.
        IReadOnlyList<Point2D> Head(double tip, double toward) =>
        [
            At(tip - (toward * PortArrowHeadSize), PortArrowHeadSize),
            At(tip, 0.0),
            At(tip - (toward * PortArrowHeadSize), -PortArrowHeadSize),
        ];

        var segments = new List<IReadOnlyList<Point2D>>
        {
            new[] { At(-PortArrowHalfLength, 0.0), At(PortArrowHalfLength, 0.0) },
        };

        if (direction is PortDirection.In or PortDirection.InOut)
        {
            segments.Add(Head(PortArrowHalfLength, 1.0));
        }

        if (direction is PortDirection.Out or PortDirection.InOut)
        {
            segments.Add(Head(-PortArrowHalfLength, -1.0));
        }

        return segments;
    }

    /// <summary>
    /// Returns whether a port's external label is drawn outward — outside the owning box, beside the
    /// connector — which is when the port's appearance asks for <see cref="PortLabelPlacement.Outward"/>
    /// and it has an external label.
    /// </summary>
    /// <param name="port">The port.</param>
    /// <returns><see langword="true"/> when the external label is drawn outward.</returns>
    public static bool IsOutwardLabel(LayoutPort port)
    {
        ArgumentNullException.ThrowIfNull(port);
        return port.Appearance?.LabelPlacement == PortLabelPlacement.Outward && !string.IsNullOrEmpty(port.ExternalLabel);
    }

    /// <summary>
    /// Returns the anchor of a port's outward label. On a left or right port the text is above the
    /// connector line, ending at (left) or starting at (right) a point one glyph half size plus the label
    /// padding beyond the face; on a top or bottom port the text is beside the connector, to its right,
    /// just outside the face. The label never sits on the connector and is entirely outside the owning box.
    /// </summary>
    /// <param name="port">The port.</param>
    /// <param name="theme">The theme providing the font size and label padding.</param>
    /// <returns>The text anchor.</returns>
    public static PortLabelAnchor PortOutwardLabelAnchor(LayoutPort port, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(port);
        ArgumentNullException.ThrowIfNull(theme);

        var reach = PortGlyphHalfSizeFor(port.Appearance) + theme.LabelPadding;
        var halfHeight = theme.FontSizeBody * PortLabelHalfHeightFactor;
        var gap = PortLabelConnectorGap(theme);
        return port.Side switch
        {
            PortSide.Left => new PortLabelAnchor(port.CentreX - reach, port.CentreY - halfHeight - gap, PortLabelAlign.End),
            PortSide.Right => new PortLabelAnchor(port.CentreX + reach, port.CentreY - halfHeight - gap, PortLabelAlign.Start),
            PortSide.Top => new PortLabelAnchor(port.CentreX + gap, port.CentreY - reach - halfHeight, PortLabelAlign.Start),
            _ => new PortLabelAnchor(port.CentreX + gap, port.CentreY + reach + halfHeight, PortLabelAlign.Start),
        };
    }

    /// <summary>
    /// Returns the bounding box of a port's outward label, as a centre and half extents, for collision
    /// tests and canvas growth: the estimated text width (<see cref="PortLabelWidthEstimator"/>) by the
    /// text height, positioned from <see cref="PortOutwardLabelAnchor"/>.
    /// </summary>
    /// <param name="port">The port.</param>
    /// <param name="text">The label text.</param>
    /// <param name="theme">The theme providing the font size and label padding.</param>
    /// <returns>The label bounds.</returns>
    public static LabelPlacement PortOutwardLabelBounds(LayoutPort port, string text, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(port);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(theme);

        var anchor = PortOutwardLabelAnchor(port, theme);
        var halfWidth = PortLabelWidthEstimator.MeasureWidth(text, theme.FontSizeBody) / 2.0;
        var halfHeight = theme.FontSizeBody * PortLabelHalfHeightFactor;
        var centreX = anchor.Align == PortLabelAlign.End ? anchor.X - halfWidth : anchor.X + halfWidth;
        return new LabelPlacement(centreX, anchor.Y, halfWidth, halfHeight);
    }

    /// <summary>
    /// Returns the bounding box of a port's glyph, as a centre and half extents, for collision tests: the
    /// square (or the proxy circle's bounding square) around the port centre.
    /// </summary>
    /// <param name="port">The port.</param>
    /// <returns>The glyph bounds.</returns>
    public static LabelPlacement PortGlyphBounds(LayoutPort port)
    {
        ArgumentNullException.ThrowIfNull(port);
        var half = PortGlyphHalfSizeFor(port.Appearance);
        return new LabelPlacement(port.CentreX, port.CentreY, half, half);
    }
}
