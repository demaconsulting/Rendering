// <copyright file="LayoutBadge.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// Shape of a badge icon.
/// </summary>
public enum BadgeShape
{
    /// <summary>Solid filled circle.</summary>
    FilledCircle,

    /// <summary>Circle with concentric inner circle (bullseye).</summary>
    Bullseye,

    /// <summary>Diamond (rotated square).</summary>
    Diamond,

    /// <summary>Short horizontal bar.</summary>
    HorizontalBar,

    /// <summary>Short vertical bar.</summary>
    VerticalBar,

    /// <summary>
    /// Thick filled horizontal bar (SysML v2 fork/join node). The badge <c>Size</c> is the full bar
    /// length; the thickness defaults to <c>NotationMetrics.ThickBarThickness</c>.
    /// </summary>
    ThickHorizontalBar,

    /// <summary>
    /// Thick filled vertical bar (SysML v2 fork/join node). The badge <c>Size</c> is the full bar
    /// length; the thickness defaults to <c>NotationMetrics.ThickBarThickness</c>.
    /// </summary>
    ThickVerticalBar,

    /// <summary>Hollow circle crossed by an X whose arms end on the circle (SysML v2 terminate node).</summary>
    CrossedCircle,
}

/// <summary>
/// A small icon badge at an absolute centre position.
/// </summary>
/// <param name="CentreX">Absolute X coordinate of the badge centre in logical pixels.</param>
/// <param name="CentreY">Absolute Y coordinate of the badge centre in logical pixels.</param>
/// <param name="Size">Diameter of the badge bounding circle in logical pixels (full length for thick bars).</param>
/// <param name="Shape">Visual shape of the badge icon.</param>
/// <param name="Label">Optional text label displayed beside the badge; <see langword="null"/> for icon-only badges.</param>
/// <param name="Thickness">
/// Optional thickness, in logical pixels, across the axis of a <see cref="BadgeShape.ThickHorizontalBar"/>
/// or <see cref="BadgeShape.ThickVerticalBar"/>; <see langword="null"/> uses the default notation thickness.
/// Ignored for other shapes.
/// </param>
public sealed record LayoutBadge(
    double CentreX,
    double CentreY,
    double Size,
    BadgeShape Shape,
    string? Label,
    double? Thickness = null) : LayoutNode;
