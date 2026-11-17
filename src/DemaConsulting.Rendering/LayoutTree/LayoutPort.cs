// <copyright file="LayoutPort.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// Side of a box that a port is attached to.
/// </summary>
public enum PortSide
{
    /// <summary>Port is on the top edge.</summary>
    Top,

    /// <summary>Port is on the bottom edge.</summary>
    Bottom,

    /// <summary>Port is on the left edge.</summary>
    Left,

    /// <summary>Port is on the right edge.</summary>
    Right,
}

/// <summary>
/// The direction arrow drawn inside an outlined port glyph (SysML v2 flow direction of a port).
/// </summary>
public enum PortDirection
{
    /// <summary>No arrow (the default).</summary>
    None,

    /// <summary>An arrow pointing into the owning part (<c>in</c>).</summary>
    In,

    /// <summary>An arrow pointing out of the owning part (<c>out</c>).</summary>
    Out,

    /// <summary>A double-headed arrow (<c>inout</c>).</summary>
    InOut,
}

/// <summary>
/// The glyph a port is drawn with.
/// </summary>
public enum PortGlyph
{
    /// <summary>
    /// Chosen from the rest of the appearance: <see cref="Filled"/> (the legacy 8×8 filled square) when
    /// there is no direction arrow and the outline is solid, otherwise <see cref="Outlined"/>.
    /// </summary>
    Auto,

    /// <summary>The legacy small filled square.</summary>
    Filled,

    /// <summary>A larger background-filled rounded square with an outline, optionally dashed, and an optional direction arrow inside.</summary>
    Outlined,

    /// <summary>A filled circle (the SysML v2 proxy port, drawn on the connector line).</summary>
    Proxy,
}

/// <summary>
/// Where a port's external label is drawn.
/// </summary>
public enum PortLabelPlacement
{
    /// <summary>
    /// The legacy rule: a plain port's label is inside the owning box beside the glyph; a boundary
    /// port's external label is on the outward face.
    /// </summary>
    Auto,

    /// <summary>The external label is drawn inside the owning box beside the glyph.</summary>
    Inward,

    /// <summary>
    /// The external label is drawn outside the owning box, beside the connector, never on top of it (the
    /// SysML v2 notation draws <c>name : Type</c> outside the port).
    /// </summary>
    Outward,
}

/// <summary>
/// How a port is drawn: its glyph, direction arrow, outline style, and label placement. Every default
/// reproduces the legacy look, and a <see langword="null"/> appearance on a <see cref="LayoutPort"/> is the
/// legacy look exactly.
/// </summary>
/// <param name="Direction">The direction arrow inside the glyph; <see cref="PortDirection.None"/> by default.</param>
/// <param name="Glyph">The glyph; <see cref="PortGlyph.Auto"/> by default.</param>
/// <param name="OutlineStyle">The outline stroke style (<see cref="LineStyle.Dashed"/> for a SysML reference port); solid by default.</param>
/// <param name="LabelPlacement">Where the external label is drawn; <see cref="PortLabelPlacement.Auto"/> by default.</param>
public sealed record PortAppearance(
    PortDirection Direction = PortDirection.None,
    PortGlyph Glyph = PortGlyph.Auto,
    LineStyle OutlineStyle = LineStyle.Solid,
    PortLabelPlacement LabelPlacement = PortLabelPlacement.Auto)
{
    /// <summary>
    /// Gets the glyph this appearance is drawn with after resolving <see cref="PortGlyph.Auto"/>: the
    /// legacy <see cref="PortGlyph.Filled"/> square when there is no direction arrow and the outline is
    /// solid, otherwise <see cref="PortGlyph.Outlined"/>.
    /// </summary>
    public PortGlyph ResolvedGlyph
    {
        get
        {
            if (Glyph != PortGlyph.Auto)
            {
                return Glyph;
            }

            return Direction == PortDirection.None && OutlineStyle == LineStyle.Solid
                ? PortGlyph.Filled
                : PortGlyph.Outlined;
        }
    }
}

/// <summary>
/// A port node pinned to the edge of its parent box. Position is absolute.
/// </summary>
/// <param name="CentreX">Absolute X coordinate of the port centre in logical pixels.</param>
/// <param name="CentreY">Absolute Y coordinate of the port centre in logical pixels.</param>
/// <param name="Side">Edge of the parent box that this port is attached to.</param>
/// <param name="ExternalLabel">
/// Optional label for the edge crossing into or out of the box from outside its container. On a plain
/// (non-boundary) port — one whose <paramref name="InternalLabel"/> is <see langword="null"/> — this is
/// rendered <em>inward</em>, beside the port glyph, exactly as the single legacy label was. On a
/// boundary port (one that also carries an <paramref name="InternalLabel"/>) it is rendered on the
/// <em>outward</em> face instead. <see langword="null"/> when no external label should be shown.
/// </param>
/// <param name="InternalLabel">
/// Optional label for the delegation edge into the box's own child scope. Always rendered <em>inward</em>
/// beside the port glyph. Its presence marks this as a genuine boundary port and moves
/// <paramref name="ExternalLabel"/> to the outward face. <see langword="null"/> on a plain port, which
/// preserves the exact legacy rendering of every pre-existing call site.
/// </param>
/// <param name="MaxLabelWidth">
/// Maximum width, in logical pixels, a renderer should allow either label to occupy before squeezing it
/// (the same way a box title is squeezed to fit). Computed by the layout algorithm from the owning box's
/// width, since a <see cref="LayoutPort"/> has no direct reference to its owning <see cref="LayoutBox"/>.
/// Defaults to <see cref="double.PositiveInfinity"/> (no constraint), which preserves the behavior of
/// every pre-existing 4-argument <c>new LayoutPort(x, y, side, label)</c> call site.
/// </param>
/// <param name="SourcePort">
/// Engine-only plumbing identity: the <see cref="LayoutGraphPort"/> that produced this placed anchor
/// during the leaf layout pass, if any. This exists solely so a later reconciliation stage (such as the
/// hierarchical layout algorithm's boundary-port resolver) can recover, by reference identity, which
/// graph port originated a given placed anchor — this is required because <paramref name="ExternalLabel"/>
/// is optional and frequently <see langword="null"/>, so it cannot safely be used as an identity key.
/// This is <em>not</em> rendering data: renderers must continue to read only
/// <see cref="CentreX"/>/<see cref="CentreY"/>/<see cref="Side"/>/<paramref name="ExternalLabel"/>/
/// <paramref name="InternalLabel"/>/<paramref name="MaxLabelWidth"/> and must ignore this field.
/// Defaults to <see langword="null"/>, which preserves the behavior of every pre-existing call site.
/// </param>
/// <param name="Appearance">
/// Optional glyph, direction arrow, outline style, and label placement (see <see cref="PortAppearance"/>).
/// Unlike <paramref name="SourcePort"/> this <em>is</em> rendering data that renderers read.
/// <see langword="null"/> (the default) is the legacy look exactly.
/// </param>
public sealed record LayoutPort(
    double CentreX,
    double CentreY,
    PortSide Side,
    string? ExternalLabel,
    string? InternalLabel = null,
    double MaxLabelWidth = double.PositiveInfinity,
    LayoutGraphPort? SourcePort = null,
    PortAppearance? Appearance = null) : LayoutNode;
