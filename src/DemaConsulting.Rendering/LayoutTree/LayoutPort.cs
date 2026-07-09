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
public sealed record LayoutPort(
    double CentreX,
    double CentreY,
    PortSide Side,
    string? ExternalLabel,
    string? InternalLabel = null,
    double MaxLabelWidth = double.PositiveInfinity,
    LayoutGraphPort? SourcePort = null) : LayoutNode;
