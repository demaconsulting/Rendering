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
/// <param name="NestedLeanWeight">
/// The summed routing weight of the scopes inside a scope that routes connectors itself, from which those
/// inner scopes are laid out <em>lean</em> (see <see cref="RoutingWorkBudget.Lean"/>): one routed pass at
/// the full effort. Every layout of the enclosing scope lays its inner scopes out again, so the passes of a
/// hierarchy multiply level by level; a hierarchy this heavy cannot afford the ordering refinement and the
/// extra layouts at every level, and the enclosing scope keeps them for itself.
/// </param>
internal sealed record RoutingWorkLimits(
    long Soft,
    long Hard,
    long ReducedWeight = long.MaxValue,
    long NestedLeanWeight = long.MaxValue);

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

    /// <summary>Whether the scope's size alone put it in the <see cref="RoutingTier.Reduced"/> tier.</summary>
    private bool _forcedReduced;

    /// <summary>Whether the scope took the <see cref="RoutingTier.Reduced"/> tier over from the oversize scope that holds it.</summary>
    private bool _inheritedReduced;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoutingWorkBudget"/> class with no work spent.
    /// </summary>
    /// <param name="limits">The soft and hard work limits.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is <see langword="null"/>.</exception>
    public RoutingWorkBudget(RoutingWorkLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
    }

    /// <summary>Gets the work units spent so far.</summary>
    public long Spent { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the scope is routed at the <see cref="RoutingTier.Reduced"/> effort
    /// from the start, because its own size or that of a scope that holds it is too large for the full effort.
    /// </summary>
    public bool ForcedReduced => _forcedReduced || _inheritedReduced;

    /// <summary>Gets a value indicating whether the scope's own size put it in the <see cref="RoutingTier.Reduced"/> tier.</summary>
    public bool SizeForced => _forcedReduced;

    /// <summary>
    /// Gets a value indicating whether the scope is laid out <em>lean</em>: with one routed pass at the full
    /// effort (the containers a routed connector enters keep their room from the start; no second pass for
    /// the connectors that hugged a border, no reference or middle layout, no ordering refinement). A lean
    /// scope is not limited: its router effort, its tier and its warnings are those of any scope at the
    /// full effort, it only does not repeat its layout.
    /// </summary>
    public bool Lean { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the scopes inside the scope are laid out lean: the scope itself holds
    /// a hierarchy heavy enough (see <see cref="RoutingWorkLimits.NestedLeanWeight"/>), or is lean itself.
    /// </summary>
    public bool LeansInnerScopes { get; private set; }

    /// <summary>Gets the summed weight of the inner scopes from which they are laid out lean.</summary>
    public long NestedLeanWeight => _limits.NestedLeanWeight;

    /// <summary>
    /// Lays the scope out lean (see <see cref="Lean"/>), and so every scope inside it: they are inner scopes
    /// of the heavy hierarchy too.
    /// </summary>
    public void MarkLean()
    {
        Lean = true;
        LeansInnerScopes = true;
    }

    /// <summary>
    /// Lays the scopes inside the scope out lean, because the scope holds a hierarchy of inner scopes heavy
    /// enough that repeating each of them for every layout of the scope costs more than it is worth. The scope
    /// itself keeps the full effort.
    /// </summary>
    public void MarkInnerScopesLean() => LeansInnerScopes = true;

    /// <summary>Gets the effort tier the work spent so far, and the size of the scope, allow.</summary>
    public RoutingTier Tier => TierOf(ForcedReduced);

    /// <summary>
    /// Gets the effort tier the scope's own size and work allow, leaving out the reduction it inherited:
    /// the tier a hint to the user is about.
    /// </summary>
    public RoutingTier OwnTier => TierOf(_forcedReduced);

    /// <summary>Gets the lowest tier the scope's own size and work have put it in (see <see cref="OwnTier"/>).</summary>
    public RoutingTier OwnLowestTier { get; private set; }

    /// <summary>Gets the number of connectors that were routed with the simple elbow route because the budget ran out.</summary>
    public int ElbowRoutes { get; private set; }

    /// <summary>Gets the work units that may still be spent before the budget drops to <see cref="RoutingTier.Minimal"/>.</summary>
    public long RemainingHard => Math.Max(0L, _limits.Hard - Spent);

    /// <summary>
    /// Returns whether spending <paramref name="units"/> more work would leave the scope at the
    /// <see cref="RoutingTier.Full"/> effort from the first unit to the last: it is at that effort now, and
    /// the work stays below both the soft and the hard limit. It is the condition under which routing a
    /// problem whose cost is known beforehand cannot meet a limit, so its outcome does not depend on the
    /// work already spent.
    /// </summary>
    /// <param name="units">The work units about to be spent; never negative.</param>
    /// <returns><see langword="true"/> when the effort stays full throughout.</returns>
    public bool WouldStayFull(long units) =>
        Tier == RoutingTier.Full && Spent + units < Math.Min(_limits.Soft, _limits.Hard);

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

    /// <summary>Remembers the lowest tier the scope's own size and work have put it in.</summary>
    private void NoteTier()
    {
        var own = OwnTier;
        if (own > OwnLowestTier)
        {
            OwnLowestTier = own;
        }
    }
}
