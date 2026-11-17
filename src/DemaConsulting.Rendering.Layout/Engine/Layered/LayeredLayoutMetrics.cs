// <copyright file="LayeredLayoutMetrics.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout.Engine.Layered;

/// <summary>
/// Fixed spacing and tolerance constants shared by every layered-layout stage.
/// </summary>
/// <remarks>
/// These values are intentionally identical to the constants previously embedded in the
/// monolithic interconnection engine; the extraction preserves them exactly so the pipeline
/// reproduces the legacy output byte for byte.
/// </remarks>
internal static class LayeredLayoutMetrics
{
    /// <summary>Vertical gap between adjacent nodes stacked within the same layer.</summary>
    internal const double NodeSpacing = 30.0;

    /// <summary>Minimum corridor width (node-node spacing) between adjacent columns.</summary>
    internal const double CorridorMinWidth = 70.0;

    /// <summary>Slot-to-slot spacing within a corridor (ELK edgeEdgeSpacing).</summary>
    internal const double EdgeSpacing = 16.0;

    /// <summary>Clearance from corridor edge to the nearest routing slot (ELK edgeNodeSpacing).</summary>
    internal const double ConnectorClearance = 10.0;

    /// <summary>Uniform padding added around the placed content.</summary>
    internal const double Padding = 20.0;

    /// <summary>Number of Barycenter ordering sweeps (down + up = one round).</summary>
    internal const int BarycentricSweeps = 4;

    /// <summary>Tolerance for treating a segment as straight (no bend points needed).</summary>
    internal const double StraightTolerance = 1e-6;

    /// <summary>Clearance added around a port label's measured width, matching a box's own port-label content insets.</summary>
    internal const double PortLabelClearance = 4.0;

    /// <summary>Clear gap, in logical pixels, kept between the painted extents of two adjacent end markers on one face.</summary>
    internal const double MarkerGap = 4.0;

    /// <summary>
    /// Returns the centre-to-centre pitch two connectors ending on the same face need so their end
    /// markers never touch: the marker's painted width (its across extent at the light theme's marker
    /// scale plus the painted stroke) plus <see cref="MarkerGap"/>, never less than <see cref="EdgeSpacing"/>.
    /// </summary>
    /// <param name="style">The end marker drawn at the connector end.</param>
    /// <returns>The pitch in logical pixels; zero for <see cref="EndMarkerStyle.None"/>.</returns>
    internal static double MarkerFacePitch(EndMarkerStyle style)
    {
        if (style == EndMarkerStyle.None)
        {
            return 0.0;
        }

        var painted = (2.0 * NotationMetrics.AcrossHalfWidth(style) * NotationMetrics.MarkerScale(Themes.Light))
            + NotationMetrics.MarkerStrokeWidth(Themes.Light);
        return Math.Max(EdgeSpacing, painted + MarkerGap);
    }

    /// <summary>
    /// Computes the vertical band, in logical pixels, that a box's title (keyword line, if any, then
    /// the name line) occupies at the top of the box — used both to reserve a node's
    /// <c>ContentInsetTop</c> for a box with left/right ports and a title, and to exclude that same
    /// band from <see cref="PortDistributor"/>'s left/right port placement (via
    /// <see cref="Engine.LayerNode.TitleReserveTop"/>), so a left/right port can never land in the
    /// same row as the title regardless of how many ports share that face.
    /// </summary>
    /// <param name="hasLabel">Whether the box has a name label.</param>
    /// <param name="hasKeyword">Whether the box has a keyword line above the name.</param>
    /// <param name="assumedFontSize">The <c>CoreOptions.AssumedFontSize</c>-derived font size.</param>
    /// <returns>The title band height in logical pixels, or 0 when there is no title at all.</returns>
    internal static double ResolveTitleReserveTop(bool hasLabel, bool hasKeyword, double assumedFontSize)
    {
        // A zero/negative font size means the caller has no real text-metrics context (e.g. a pipeline
        // that hasn't opted into this reservation yet) — treat that as "no reserve" rather than still
        // applying the bare clearance floor below, which would otherwise shift geometry for callers
        // that pass 0.0 specifically to opt out.
        if (!hasLabel && !hasKeyword || assumedFontSize <= 0.0)
        {
            return 0.0;
        }

        // One title line needs roughly double assumedFontSize once real rendering
        // (Theme.FontSizeTitle plus a LabelPadding gap on either side — see
        // BoxMetrics.TitleAreaHeight) is accounted for; assumedFontSize alone approximates a port
        // label's font, which is smaller than a bold title line, so a bare single multiplier
        // under-reserves and leaves the title's own rendered extent poking into the port band it
        // was meant to be excluded from. Mirrors the existing top/bottom-port title-clearance
        // formula's use of the same doubled margin for the same reason.
        var lines = (hasLabel ? 1.0 : 0.0) + (hasKeyword ? 1.0 : 0.0);
        return (lines * 2.0 * assumedFontSize) + (2.0 * PortLabelClearance);
    }
}
