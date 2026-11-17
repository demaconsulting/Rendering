// <copyright file="LayoutLine.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// An immutable 2-D point with absolute coordinates.
/// </summary>
/// <param name="X">Absolute X coordinate in logical pixels.</param>
/// <param name="Y">Absolute Y coordinate in logical pixels.</param>
public sealed record Point2D(double X, double Y);

/// <summary>
/// Style of an end marker (arrowhead, diamond, circle, or bar) at a line end.
/// </summary>
public enum EndMarkerStyle
{
    /// <summary>No end marker.</summary>
    None,

    /// <summary>Open chevron: a two-stroke V with no closing edge.</summary>
    OpenChevron,

    /// <summary>Hollow (unfilled) closed triangle.</summary>
    HollowTriangle,

    /// <summary>Hollow closed triangle with a perpendicular crossbar (for redefinition).</summary>
    HollowTriangleCrossbar,

    /// <summary>Filled (solid) triangle arrow.</summary>
    FilledArrow,

    /// <summary>Hollow (open) diamond.</summary>
    HollowDiamond,

    /// <summary>Filled diamond.</summary>
    FilledDiamond,

    /// <summary>Circle.</summary>
    Circle,

    /// <summary>Bar (perpendicular line).</summary>
    Bar,

    /// <summary>
    /// Hollow closed triangle followed by two dots, one on each side of the line just behind the
    /// triangle base (SysML v2 feature typing / definition).
    /// </summary>
    HollowTriangleTwoDots,

    /// <summary>
    /// Hollow closed triangle followed by four dots in a 2x2 grid straddling the line just behind
    /// the triangle base (SysML v2 reference subsetting).
    /// </summary>
    HollowTriangleFourDots,

    /// <summary>
    /// Circle containing a plus sign, with the far edge of the circle on the line endpoint
    /// (SysML v2 owned membership).
    /// </summary>
    CircledPlus,
}

/// <summary>
/// Stroke style of a line.
/// </summary>
public enum LineStyle
{
    /// <summary>Solid line.</summary>
    Solid,

    /// <summary>Dashed line.</summary>
    Dashed,

    /// <summary>Dotted line.</summary>
    Dotted,
}

/// <summary>
/// A pre-routed orthogonal line connecting two nodes. Corner rounding is controlled by <c>Theme.LineCornerRadius</c>.
/// </summary>
/// <param name="Waypoints">Ordered absolute waypoints; the renderer draws straight segments between consecutive points.</param>
/// <param name="SourceEnd">End-marker style at the source (start) end of the line.</param>
/// <param name="TargetEnd">End-marker style at the target (end) end of the line.</param>
/// <param name="LineStyle">Stroke style (solid, dashed, dotted) for the line.</param>
/// <param name="MidpointLabel">Optional text label placed at the midpoint of the line; <see langword="null"/> for unlabelled lines.</param>
/// <param name="SourceEndLabel">
/// Optional labels near the source (<c>Waypoints[0]</c>) end of the line, for example the role name and
/// multiplicity of a SysML connection end; <see langword="null"/> (the default) for none. Placed at render
/// time by <c>ConnectorLabelPlacer.PlaceEndLabels</c>, so existing lines are unchanged.
/// </param>
/// <param name="TargetEndLabel">
/// Optional labels near the target (<c>Waypoints[^1]</c>) end of the line; <see langword="null"/> (the
/// default) for none.
/// </param>
public sealed record LayoutLine(
    IReadOnlyList<Point2D> Waypoints,
    EndMarkerStyle SourceEnd,
    EndMarkerStyle TargetEnd,
    LineStyle LineStyle,
    string? MidpointLabel,
    LineEndLabel? SourceEndLabel = null,
    LineEndLabel? TargetEndLabel = null) : LayoutNode;

/// <summary>
/// The labels drawn near one end of a connector (a SysML connection end): a role name and a
/// multiplicity with its adornments. The two slots sit on opposite sides of the line, matching the
/// SysML v2 graphical notation: <see cref="Primary"/> (the role name) above a horizontal end run or to
/// the right of a vertical one, and <see cref="Secondary"/> (the multiplicity plus adornments such as
/// <c>ordered</c> or <c>subsets x</c>) below a horizontal end run or to the left of a vertical one.
/// </summary>
/// <remarks>
/// A slot that is <see langword="null"/> or empty is not drawn. The consumer composes the text of each
/// slot; the renderers draw it verbatim in the body font.
/// </remarks>
/// <param name="Primary">The text above (horizontal run) or right of (vertical run) the line near the end, typically the role name.</param>
/// <param name="Secondary">The text below (horizontal run) or left of (vertical run) the line near the end, typically the multiplicity.</param>
public sealed record LineEndLabel(string? Primary, string? Secondary = null);
