// <copyright file="LayoutLifeline.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// A sequence-diagram lifeline: a vertical dashed line with a header box.
/// </summary>
/// <param name="CentreX">Absolute X coordinate of the lifeline centre in logical pixels.</param>
/// <param name="TopY">Absolute Y coordinate of the top of the lifeline in logical pixels.</param>
/// <param name="BottomY">Absolute Y coordinate of the bottom of the lifeline in logical pixels.</param>
/// <param name="Label">
/// Text label displayed in the header box. May carry an already-formatted <c>name : Type</c> string;
/// the renderer does not parse it.
/// </param>
/// <param name="HeaderWidth">Width of the header box in logical pixels.</param>
/// <param name="HeaderHeight">Height of the header box in logical pixels.</param>
/// <param name="Keyword">
/// Optional keyword drawn as a «keyword» line above the label inside the header;
/// <see langword="null"/> for none. Size the header with <c>BoxMetrics.LifelineHeaderHeight</c>.
/// </param>
/// <param name="HeaderShape">
/// Header outline shape: <see cref="BoxShape.Rectangle"/> (default) or <see cref="BoxShape.RoundedRectangle"/>.
/// Any other value is drawn as a rectangle.
/// </param>
/// <param name="HeaderAppearance">
/// Optional presentation of the header: <see cref="BoxAppearance.OutlineStyle"/>,
/// <see cref="BoxAppearance.NameStyle"/> (label; default bold) and <see cref="BoxAppearance.KeywordStyle"/>
/// are honored, other members are ignored. <see langword="null"/> is the legacy look.
/// </param>
/// <param name="HeaderCornerRadius">
/// Corner radius for a <see cref="BoxShape.RoundedRectangle"/> header; <see langword="null"/> uses the
/// theme's rounded-rectangle radius.
/// </param>
public sealed record LayoutLifeline(
    double CentreX,
    double TopY,
    double BottomY,
    string Label,
    double HeaderWidth,
    double HeaderHeight,
    string? Keyword = null,
    BoxShape HeaderShape = BoxShape.Rectangle,
    BoxAppearance? HeaderAppearance = null,
    double? HeaderCornerRadius = null) : LayoutNode;

/// <summary>
/// An activation bar on a lifeline: a narrow rectangle indicating when the lifeline is active.
/// </summary>
/// <param name="CentreX">Absolute X coordinate of the activation bar centre in logical pixels.</param>
/// <param name="TopY">Absolute Y coordinate of the top of the activation bar in logical pixels.</param>
/// <param name="BottomY">Absolute Y coordinate of the bottom of the activation bar in logical pixels.</param>
public sealed record LayoutActivation(
    double CentreX,
    double TopY,
    double BottomY) : LayoutNode;
