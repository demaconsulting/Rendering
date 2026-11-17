// <copyright file="LayoutBox.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// Shape of a layout box.
/// </summary>
public enum BoxShape
{
    /// <summary>Plain rectangle.</summary>
    Rectangle,

    /// <summary>Rectangle with rounded corners.</summary>
    RoundedRectangle,

    /// <summary>Folder shape (rectangle with a tab on the top-left), used for packages.</summary>
    Folder,

    /// <summary>Note shape (rectangle with a folded-down top-right corner), used for documentation and comments.</summary>
    Note,

    /// <summary>
    /// Diamond (rhombus) inscribed in the bounding box, its four vertices at the midpoints of the box
    /// edges. Connectors anchor on the diamond's outline (the vertex for a single connector on a face)
    /// rather than on the bounding box, so decision and merge nodes read cleanly.
    /// </summary>
    Diamond,
}

/// <summary>
/// Where the name of a <see cref="BoxShape.Folder"/> box is drawn.
/// </summary>
public enum FolderTitlePlacement
{
    /// <summary>The name is drawn in the box body, below the (empty) tab. This is the default.</summary>
    Body,

    /// <summary>The name is drawn inside the folder tab (SysML v2 "package with name in tab").</summary>
    Tab,
}

/// <summary>
/// A small pictorial icon drawn in the top-left corner of a box body (SysML v2 send/accept actions).
/// </summary>
public enum BoxCornerIcon
{
    /// <summary>No icon.</summary>
    None,

    /// <summary>Send icon: a convex pentagon pointing right.</summary>
    Send,

    /// <summary>Accept icon: a pentagon pointing right with a triangular notch cut into its left edge.</summary>
    Accept,
}

/// <summary>
/// Optional presentation attributes of a <see cref="LayoutBox"/> beyond its geometry. Every member
/// defaults to the legacy appearance, so <see langword="null"/> and <c>new BoxAppearance()</c> are
/// equivalent.
/// </summary>
/// <param name="OutlineStyle">
/// Stroke style of the box outline: <see cref="LineStyle.Solid"/> (default), <see cref="LineStyle.Dashed"/>
/// or <see cref="LineStyle.Dotted"/>. Honored for every <see cref="BoxShape"/>.
/// </param>
/// <param name="NameStyle">
/// Weight/style of the name label; <see langword="null"/> means bold, upright (the legacy look).
/// </param>
/// <param name="KeywordStyle">
/// Weight/style of the «keyword» line; <see langword="null"/> means regular, italic (the legacy look).
/// </param>
/// <param name="FolderTitle">
/// Where the name of a <see cref="BoxShape.Folder"/> box is drawn; ignored for other shapes.
/// </param>
/// <param name="CornerIcon">Icon drawn in the top-left corner of the box body (below the tab for a folder).</param>
public sealed record BoxAppearance(
    LineStyle OutlineStyle = LineStyle.Solid,
    TextStyle? NameStyle = null,
    TextStyle? KeywordStyle = null,
    FolderTitlePlacement FolderTitle = FolderTitlePlacement.Body,
    BoxCornerIcon CornerIcon = BoxCornerIcon.None)
{
    /// <summary>Gets the name style with the legacy default (bold, upright) applied.</summary>
    public TextStyle ResolvedNameStyle => NameStyle ?? TextStyle.Bold;

    /// <summary>Gets the keyword style with the legacy default (regular, italic) applied.</summary>
    public TextStyle ResolvedKeywordStyle => KeywordStyle ?? TextStyle.Italic;
}

/// <summary>
/// A single compartment within a box (e.g., attributes section, operations section).
/// </summary>
/// <param name="Title">Optional compartment header text; <see langword="null"/> for untitled compartments.</param>
/// <param name="Rows">Text rows displayed inside the compartment.</param>
/// <param name="TitleAlign">
/// Horizontal alignment of <paramref name="Title"/>: <see cref="TextAlign.Left"/> (default) or
/// <see cref="TextAlign.Center"/> (SysML v2 compartment titles are centered between the separators).
/// <see cref="TextAlign.Right"/> is drawn left-aligned.
/// </param>
/// <param name="TitleStyle">
/// Weight/style of <paramref name="Title"/>; <see langword="null"/> means bold italic (the legacy look).
/// </param>
public sealed record LayoutCompartment(
    string? Title,
    IReadOnlyList<string> Rows,
    TextAlign TitleAlign = TextAlign.Left,
    TextStyle? TitleStyle = null)
{
    /// <summary>Gets the title style with the legacy default (bold, italic) applied.</summary>
    public TextStyle ResolvedTitleStyle => TitleStyle ?? TextStyle.BoldItalic;
}

/// <summary>
/// A rectangular container node with optional label, depth, compartments, and nested children.
/// </summary>
/// <param name="X">Absolute X coordinate of the left edge in logical pixels.</param>
/// <param name="Y">Absolute Y coordinate of the top edge in logical pixels.</param>
/// <param name="Width">Width of the box in logical pixels.</param>
/// <param name="Height">Height of the box in logical pixels.</param>
/// <param name="Label">Optional text label displayed at the top of the box.</param>
/// <param name="Depth">Nesting depth used by the renderer to index into <c>Theme.DepthFillColors</c>.</param>
/// <param name="Shape">Visual shape of the box outline.</param>
/// <param name="Compartments">Ordered list of compartments displayed below the label.</param>
/// <param name="Children">Nested layout nodes contained spatially within this box.</param>
/// <param name="Keyword">
/// Optional SysML keyword (e.g. <c>"part def"</c>, <c>"port"</c>) rendered on a smaller line
/// above the bold label, following the SysML v2 graphical convention. <see langword="null"/> when no
/// keyword should be shown.
/// </param>
/// <param name="RoundedCornerRadius">
/// Optional rounded-corner radius, in logical pixels, already resolved for a
/// <see cref="BoxShape.RoundedRectangle"/> outline. <see langword="null"/> means a downstream
/// renderer or router should use its own generic fallback.
/// </param>
/// <param name="FolderTabWidth">
/// Optional folder-tab width, in logical pixels, already resolved for a <see cref="BoxShape.Folder"/>
/// outline. <see langword="null"/> means a downstream renderer or router should use its own generic
/// fallback.
/// </param>
/// <param name="FolderTabHeight">
/// Optional folder-tab height, in logical pixels, already resolved for a <see cref="BoxShape.Folder"/>
/// outline. <see langword="null"/> means a downstream renderer or router should use its own generic
/// fallback.
/// </param>
/// <param name="ContentInsetLeft">
/// Reserved margin, in logical pixels, that a renderer must not draw title/compartment content into
/// on the left side of the box, because the box has at least one port anchored there. Zero (the
/// default) when the node has no ports on that side.
/// </param>
/// <param name="ContentInsetRight">
/// Reserved margin, in logical pixels, that a renderer must not draw title/compartment content into
/// on the right side of the box, because the box has at least one port anchored there. Zero (the
/// default) when the node has no ports on that side.
/// </param>
/// <param name="ContentInsetTop">
/// Reserved margin, in logical pixels, that a renderer must not draw title/compartment content into
/// on the top side of the box, because the box has at least one port anchored there. Zero (the
/// default) when the node has no ports on that side.
/// </param>
/// <param name="ContentInsetBottom">
/// Reserved margin, in logical pixels, that a renderer must not draw title/compartment content into
/// on the bottom side of the box, because the box has at least one port anchored there. Zero (the
/// default) when the node has no ports on that side.
/// </param>
/// <param name="CenterTitle">
/// Whether a renderer should center the title/keyword block vertically within the box's own content
/// height instead of pinning it directly under the top (folder-tab-adjusted) edge. <see langword="false"/>
/// (the default) preserves the original top-pinned behavior for every pre-existing call site,
/// including hand-built or legacy-flat <see cref="LayoutTree"/>s that never opted in. Only a layout
/// algorithm that knows a given box is a genuine atomic leaf (no visually-nested content of any
/// kind, including content the algorithm can't see because it wasn't modeled via <see cref="Children"/>)
/// should set this <see langword="true"/>; it is deliberately not inferred from
/// <see cref="Children"/>/<see cref="Compartments"/> being empty, since a caller-constructed box that
/// spans a large area with content positioned as flat siblings (rather than true nested
/// <see cref="Children"/>) would otherwise be misidentified as a small leaf eligible for centering.
/// </param>
/// <param name="Appearance">
/// Optional presentation attributes (outline stroke style, name/keyword text style, folder title
/// placement, corner icon). <see langword="null"/> (the default) is equivalent to
/// <c>new BoxAppearance()</c>: the legacy look.
/// </param>
public sealed record LayoutBox(
    double X,
    double Y,
    double Width,
    double Height,
    string? Label,
    int Depth,
    BoxShape Shape,
    IReadOnlyList<LayoutCompartment> Compartments,
    IReadOnlyList<LayoutNode> Children,
    string? Keyword = null,
    double? RoundedCornerRadius = null,
    double? FolderTabWidth = null,
    double? FolderTabHeight = null,
    double ContentInsetLeft = 0.0,
    double ContentInsetRight = 0.0,
    double ContentInsetTop = 0.0,
    double ContentInsetBottom = 0.0,
    bool CenterTitle = false,
    BoxAppearance? Appearance = null) : LayoutNode;
