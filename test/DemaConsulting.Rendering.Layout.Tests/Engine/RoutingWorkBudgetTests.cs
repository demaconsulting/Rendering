// <copyright file="RoutingWorkBudgetTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Layout.Engine;

namespace DemaConsulting.Rendering.Layout.Tests.Engine;

/// <summary>
///     Tests for <see cref="RoutingWorkBudget"/>, the deterministic work account that bounds the routing of a
///     scope.
/// </summary>
public sealed class RoutingWorkBudgetTests
{
    /// <summary>
    ///     A budget with no work spent is at the full effort.
    /// </summary>
    [Fact]
    public void RoutingWorkBudget_New_IsFullEffort()
    {
        // Arrange / Act
        var budget = new RoutingWorkBudget(new RoutingWorkLimits(Soft: 100, Hard: 200));

        // Assert
        Assert.Equal(RoutingTier.Full, budget.Tier);
        Assert.Equal(0, budget.Spent);
        Assert.Equal(200, budget.RemainingHard);
    }

    /// <summary>
    ///     The tier follows the work spent: reduced at the soft limit, minimal at the hard limit.
    /// </summary>
    [Fact]
    public void RoutingWorkBudget_Charge_ReachesSoftThenHardLimit()
    {
        // Arrange
        var budget = new RoutingWorkBudget(new RoutingWorkLimits(Soft: 100, Hard: 200));

        // Act / Assert: just below, at, and past each limit
        budget.Charge(99);
        Assert.Equal(RoutingTier.Full, budget.Tier);
        budget.Charge(1);
        Assert.Equal(RoutingTier.Reduced, budget.Tier);
        budget.Charge(99);
        Assert.Equal(RoutingTier.Reduced, budget.Tier);
        budget.Charge(1);
        Assert.Equal(RoutingTier.Minimal, budget.Tier);
        Assert.Equal(0, budget.RemainingHard);
        Assert.Equal(RoutingTier.Minimal, budget.LowestTier);
    }

    /// <summary>
    ///     A scope whose size alone reaches the weight limit is reduced without any work spent, and keeps
    ///     the whole hard limit for the work it still does.
    /// </summary>
    [Fact]
    public void RoutingWorkBudget_NoteScopeWeight_AtLimit_ReducesWithoutSpending()
    {
        // Arrange
        var budget = new RoutingWorkBudget(new RoutingWorkLimits(Soft: 100, Hard: 200, ReducedWeight: 50));

        // Act / Assert
        budget.NoteScopeWeight(49);
        Assert.Equal(RoutingTier.Full, budget.Tier);
        budget.NoteScopeWeight(50);
        Assert.Equal(RoutingTier.Reduced, budget.Tier);
        Assert.Equal(0, budget.Spent);
        Assert.Equal(200, budget.RemainingHard);
    }

    /// <summary>
    ///     The lowest tier reached and the count of elbow routes are remembered.
    /// </summary>
    [Fact]
    public void RoutingWorkBudget_RecordElbowRoute_CountsRoutes()
    {
        // Arrange
        var budget = new RoutingWorkBudget(new RoutingWorkLimits(Soft: 1, Hard: 2));

        // Act
        budget.RecordElbowRoute();
        budget.RecordElbowRoute();

        // Assert
        Assert.Equal(2, budget.ElbowRoutes);
    }

    /// <summary>
    ///     A budget that takes the reduced effort over from the oversize scope that holds its scope is
    ///     reduced from the start, but the reduction is not the scope's own limit.
    /// </summary>
    [Fact]
    public void RoutingWorkBudget_InheritReduced_ReducesWithoutAnOwnLimit()
    {
        // Arrange
        var budget = new RoutingWorkBudget(new RoutingWorkLimits(Soft: 100, Hard: 200, ReducedWeight: 50));

        // Act
        budget.InheritReduced();

        // Assert: reduced and forced, yet the scope's own tier is untouched
        Assert.Equal(RoutingTier.Reduced, budget.Tier);
        Assert.Equal(RoutingTier.Reduced, budget.LowestTier);
        Assert.True(budget.ForcedReduced);
        Assert.True(budget.InheritedReduced);
        Assert.False(budget.SizeForced);
        Assert.Equal(RoutingTier.Full, budget.OwnTier);
        Assert.Equal(RoutingTier.Full, budget.OwnLowestTier);
        Assert.Equal(0, budget.Spent);
    }

    /// <summary>
    ///     An inherited reduction still lets the scope's own work run it down to the minimal tier, and that
    ///     is the scope's own limit.
    /// </summary>
    [Fact]
    public void RoutingWorkBudget_InheritReduced_OwnWorkStillReachesLimits()
    {
        // Arrange
        var budget = new RoutingWorkBudget(new RoutingWorkLimits(Soft: 100, Hard: 200));
        budget.InheritReduced();

        // Act / Assert: the own soft limit, then the hard limit
        budget.Charge(100);
        Assert.Equal(RoutingTier.Reduced, budget.OwnTier);
        Assert.Equal(RoutingTier.Reduced, budget.OwnLowestTier);
        budget.Charge(100);
        Assert.Equal(RoutingTier.Minimal, budget.Tier);
        Assert.Equal(RoutingTier.Minimal, budget.OwnTier);
        Assert.Equal(RoutingTier.Minimal, budget.OwnLowestTier);
    }

    /// <summary>
    ///     A scope that is reduced by its own size is size-forced and its descendants inherit the reduction;
    ///     a scope reduced only by work spent is not forced.
    /// </summary>
    [Fact]
    public void RoutingWorkBudget_ForcedReduced_OnlyBySizeOrInheritance()
    {
        // Arrange
        var bySize = new RoutingWorkBudget(new RoutingWorkLimits(Soft: 100, Hard: 200, ReducedWeight: 50));
        var byWork = new RoutingWorkBudget(new RoutingWorkLimits(Soft: 100, Hard: 200, ReducedWeight: 50));

        // Act
        bySize.NoteScopeWeight(50);
        byWork.Charge(100);

        // Assert
        Assert.True(bySize.ForcedReduced);
        Assert.True(bySize.SizeForced);
        Assert.Equal(RoutingTier.Reduced, bySize.OwnLowestTier);
        Assert.False(byWork.ForcedReduced);
        Assert.Equal(RoutingTier.Reduced, byWork.Tier);
    }

    /// <summary>
    ///     A budget needs limits.
    /// </summary>
    [Fact]
    public void RoutingWorkBudget_NullLimits_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new RoutingWorkBudget(null!));
    }
}
