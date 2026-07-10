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
}

/// <summary>
/// A single compartment within a box (e.g., attributes section, operations section).
/// </summary>
/// <param name="Title">Optional compartment header text; <see langword="null"/> for untitled compartments.</param>
/// <param name="Rows">Text rows displayed inside the compartment.</param>
public sealed record LayoutCompartment(
    string? Title,
    IReadOnlyList<string> Rows);

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
    bool CenterTitle = false) : LayoutNode;
