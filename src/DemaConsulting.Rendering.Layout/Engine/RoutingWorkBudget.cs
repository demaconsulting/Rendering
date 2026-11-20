// <copyright file="RoutingWorkBudget.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Layout.Engine;

/// <summary>
/// How much of the connector-routing effort a scope may still spend. The tiers only ever shed work that
/// improves a route, never work that is needed to produce one.
/// </summary>
internal enum RoutingTier
{
    /// <summary>The full effort: every face pair, every re-routing pass, every layout pass of the scope.</summary>
    Full,

    /// <summary>
    /// The reduced effort: every connector gets its plain route (searched in a window around its two ends,
    /// where the full effort searches the whole canvas, with the soft lanes of the grid limited to the
    /// nearest perpendicular ones), and none of the searches that improve a route
    /// are made: no alternative faces, no re-routing passes, no wide-lane re-run of a batch, no extra
    /// layout pass of the scope.
    /// </summary>
    Reduced,

    /// <summary>No search at all: every remaining connector gets the simple, obstacle-aware elbow route.</summary>
    Minimal,
}

/// <summary>
/// The work limits of a <see cref="RoutingWorkBudget"/>, in the budget's work units.
/// </summary>
/// <param name="Soft">The work after which a scope drops to the <see cref="RoutingTier.Reduced"/> tier.</param>
/// <param name="Hard">The work after which a scope drops to the <see cref="RoutingTier.Minimal"/> tier.</param>
/// <param name="ReducedWeight">
/// The routing weight (connectors the scope routes times the nodes under it) from which a scope starts in
/// the <see cref="RoutingTier.Reduced"/> tier: such a scope is known from its size alone to cost more than
/// the full effort is worth, and spending part of the budget to find that out would only delay the same
/// outcome.
/// </param>
internal sealed record RoutingWorkLimits(long Soft, long Hard, long ReducedWeight = long.MaxValue);

/// <summary>
/// A deterministic account of the routing work one layout scope has spent, and the effort tier that follows
/// from it, so the cost of routing a scope is bounded however many connectors it holds.
/// </summary>
/// <remarks>
/// <para>
/// Work is counted in units that depend only on the input: one unit per routing-grid node a search builds
/// and one per search state it expands. The budget never reads a clock, so the tier a scope reaches, and
/// therefore every route drawn, is the same on every run, machine and platform.
/// </para>
/// <para>
/// One budget serves every pass over one scope (the tight, loose and reference layouts and the ordering
/// proposals), so the passes made last are the first to be reduced and a heavy scope cannot starve another.
/// A budget belongs to one layout run and is not thread safe.
/// </para>
/// </remarks>
internal sealed class RoutingWorkBudget
{
    /// <summary>The work limits this budget enforces.</summary>
    private readonly RoutingWorkLimits _limits;

    /// <summary>The token that stops the routing the budget serves, or <see cref="CancellationToken.None"/>.</summary>
    private readonly CancellationToken _cancellation;

    /// <summary>Whether the scope's size alone put it in the <see cref="RoutingTier.Reduced"/> tier.</summary>
    private bool _forcedReduced;

    /// <summary>Whether the scope took the <see cref="RoutingTier.Reduced"/> tier over from the oversize scope that holds it.</summary>
    private bool _inheritedReduced;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoutingWorkBudget"/> class with no work spent.
    /// </summary>
    /// <param name="limits">The soft and hard work limits.</param>
    /// <param name="cancellation">
    /// An optional token that stops the routing the budget serves (see <see cref="ThrowIfCancellationRequested"/>).
    /// It never influences a tier, so the routes drawn do not depend on it.
    /// </param>
    public RoutingWorkBudget(RoutingWorkLimits limits, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
        _cancellation = cancellation;
    }

    /// <summary>
    /// Throws an <see cref="OperationCanceledException"/> when the token the budget was created with is canceled.
    /// </summary>
    public void ThrowIfCancellationRequested() => _cancellation.ThrowIfCancellationRequested();

    /// <summary>Gets the work units spent so far.</summary>
    public long Spent { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the scope is routed at the <see cref="RoutingTier.Reduced"/> effort
    /// from the start, because its own size or that of a scope that holds it is too large for the full effort.
    /// </summary>
    public bool ForcedReduced => _forcedReduced || _inheritedReduced;

    /// <summary>Gets a value indicating whether the scope's own size put it in the <see cref="RoutingTier.Reduced"/> tier.</summary>
    public bool SizeForced => _forcedReduced;

    /// <summary>Gets a value indicating whether the scope took the reduced effort over from the scope that holds it.</summary>
    public bool InheritedReduced => _inheritedReduced;

    /// <summary>Gets the effort tier the work spent so far, and the size of the scope, allow.</summary>
    public RoutingTier Tier => TierOf(ForcedReduced);

    /// <summary>
    /// Gets the effort tier the scope's own size and work allow, leaving out the reduction it inherited:
    /// the tier a hint to the user is about.
    /// </summary>
    public RoutingTier OwnTier => TierOf(_forcedReduced);

    /// <summary>Gets the lowest tier (the largest reduction) the budget has been in.</summary>
    public RoutingTier LowestTier { get; private set; }

    /// <summary>Gets the lowest tier the scope's own size and work have put it in (see <see cref="OwnTier"/>).</summary>
    public RoutingTier OwnLowestTier { get; private set; }

    /// <summary>Gets the number of connectors that were routed with the simple elbow route because the budget ran out.</summary>
    public int ElbowRoutes { get; private set; }

    /// <summary>Gets the work units that may still be spent before the budget drops to <see cref="RoutingTier.Minimal"/>.</summary>
    public long RemainingHard => Math.Max(0L, _limits.Hard - Spent);

    /// <summary>
    /// Adds <paramref name="units"/> of work to the account.
    /// </summary>
    /// <param name="units">The work units spent; never negative.</param>
    public void Charge(long units)
    {
        Spent += units;
        NoteTier();
    }

    /// <summary>
    /// Tells the budget how large the scope it serves is, so a scope too large for the full effort starts
    /// directly in the <see cref="RoutingTier.Reduced"/> tier.
    /// </summary>
    /// <param name="weight">The scope's routing weight: connectors routed times nodes under the scope.</param>
    public void NoteScopeWeight(long weight)
    {
        if (weight >= _limits.ReducedWeight)
        {
            _forcedReduced = true;
            NoteTier();
        }
    }

    /// <summary>
    /// Takes the reduced effort over from the oversize scope that holds the scope this budget serves: the
    /// scope is drawn in the same oversize diagram, so it is routed at the same effort. The reduction is
    /// not the scope's own limit (see <see cref="OwnTier"/>), so it is not reported as one.
    /// </summary>
    public void InheritReduced()
    {
        _inheritedReduced = true;
        NoteTier();
    }

    /// <summary>
    /// Records that a connector was routed with the simple elbow route because the budget ran out.
    /// </summary>
    public void RecordElbowRoute() => ElbowRoutes++;

    /// <summary>Returns the tier the work spent allows when the scope is, or is not, reduced from the start.</summary>
    /// <param name="reducedFromStart">Whether the scope starts in the reduced tier.</param>
    /// <returns>The tier.</returns>
    private RoutingTier TierOf(bool reducedFromStart)
    {
        if (Spent >= _limits.Hard)
        {
            return RoutingTier.Minimal;
        }

        return reducedFromStart || Spent >= _limits.Soft ? RoutingTier.Reduced : RoutingTier.Full;
    }

    /// <summary>Remembers the lowest tier reached.</summary>
    private void NoteTier()
    {
        var tier = Tier;
        if (tier > LowestTier)
        {
            LowestTier = tier;
        }

        var own = OwnTier;
        if (own > OwnLowestTier)
        {
            OwnLowestTier = own;
        }
    }
}
