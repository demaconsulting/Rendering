// <copyright file="EdgeCountGapWidener.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Engine;

/// <summary>
///     Widens a single node-to-node gap so a fan of parallel connectors routed through it has room to
///     spread into distinct orthogonal lanes instead of being crushed into one narrow channel.
/// </summary>
/// <remarks>
///     This helper exists to give the containment packer and the hierarchical algorithm's
///     post-placement pass the <em>same</em> corridor-width math the layered pipeline's
///     <c>BrandesKopfPlacer</c> already uses between adjacent columns. Both call sites size the gap
///     between two boxes from the number of edges the connector router must fan through it, so
///     extracting the formula here keeps the three widening rules byte-identical and prevents the
///     copy-paste drift that would otherwise let one call site's spacing diverge from another's. The
///     method is a pure, stateless function of its two inputs and is safe for concurrent use.
/// </remarks>
internal static class EdgeCountGapWidener
{
    /// <summary>
    ///     Widens <paramref name="baseGap"/> to the corridor width a fan of <paramref name="edgeCount"/>
    ///     parallel connectors needs, never narrowing below the supplied base gap.
    /// </summary>
    /// <remarks>
    ///     The corridor width mirrors ELK's routing-width formula (and this library's own
    ///     <c>BrandesKopfPlacer</c> corridor sizing): a clearance
    ///     (<see cref="LayeredLayoutMetrics.ConnectorClearance"/>) on each side of the fan plus one
    ///     slot-to-slot spacing (<see cref="LayeredLayoutMetrics.EdgeSpacing"/>) for every gap between
    ///     adjacent connectors. Combining that with <paramref name="baseGap"/> via <see cref="Math.Max(double, double)"/>
    ///     makes the widening strictly additive: an edge count of zero or one leaves the base gap
    ///     unchanged (a single connector needs no extra lane), so every caller that supplies a low count
    ///     keeps its pre-existing spacing exactly.
    /// </remarks>
    /// <param name="baseGap">
    ///     The un-widened gap, in logical pixels, kept between the two boxes when no fan of connectors
    ///     needs extra room. Returned unchanged when it already meets or exceeds the corridor width.
    /// </param>
    /// <param name="edgeCount">
    ///     The number of connectors routed through this gap. Values of zero or one never widen the gap;
    ///     each additional connector adds one <see cref="LayeredLayoutMetrics.EdgeSpacing"/> of lane
    ///     width. Negative values are treated as zero by the formula (the subtraction floors at the base
    ///     gap through <see cref="Math.Max(double, double)"/>).
    /// </param>
    /// <returns>
    ///     The larger of <paramref name="baseGap"/> and the corridor width required by
    ///     <paramref name="edgeCount"/> parallel connectors, in logical pixels.
    /// </returns>
    public static double Widen(double baseGap, int edgeCount)
    {
        // A single connector (or none) needs no extra lane at all: short-circuit before the corridor
        // formula so the base gap is always returned unchanged, regardless of ConnectorClearance. The
        // formula's own 2*ConnectorClearance term is not itself zero for edgeCount == 1, so leaving this
        // case to Math.Max below would incorrectly widen any base gap narrower than that fixed
        // clearance floor even though a lone connector needs no fan-out room whatsoever.
        if (edgeCount <= 1)
        {
            return baseGap;
        }

        // A fan of n connectors needs a clearance on each side plus one inter-connector spacing per gap
        // between adjacent connectors (n - 1 gaps). Math.Max keeps the result at least the base gap.
        var corridorWidth =
            (2.0 * LayeredLayoutMetrics.ConnectorClearance) + ((edgeCount - 1) * LayeredLayoutMetrics.EdgeSpacing);
        return Math.Max(baseGap, corridorWidth);
    }
}
