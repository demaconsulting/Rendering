// <copyright file="EdgeCountGapWidenerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Layout.Engine;

namespace DemaConsulting.Rendering.Layout.Tests.Engine;

/// <summary>
///     Tests for <see cref="EdgeCountGapWidener"/>, the shared connector-corridor gap formula used by
///     both the containment packer and the hierarchical algorithm's sibling-widening pass.
/// </summary>
public sealed class EdgeCountGapWidenerTests
{
    /// <summary>
    ///     A fan of several connectors widens the gap to the corridor width — a clearance on each side
    ///     plus one inter-connector spacing per gap between adjacent connectors.
    /// </summary>
    [Fact]
    public void Widen_ManyConnectors_ReturnsCorridorWidth()
    {
        // Arrange: a base gap far below the corridor width for eight connectors
        // Corridor = 2*ConnectorClearance(10) + (8 - 1)*EdgeSpacing(16) = 20 + 112 = 132.
        const double baseGap = 5.0;

        // Act
        var result = EdgeCountGapWidener.Widen(baseGap, edgeCount: 8);

        // Assert
        Assert.Equal(132.0, result);
    }

    /// <summary>
    ///     Two connectors widen the gap to a single clearance-bounded slot (one inter-connector spacing).
    /// </summary>
    [Fact]
    public void Widen_TwoConnectors_ReturnsSingleSlotCorridor()
    {
        // Corridor = 2*10 + (2 - 1)*16 = 20 + 16 = 36.
        var result = EdgeCountGapWidener.Widen(baseGap: 5.0, edgeCount: 2);

        Assert.Equal(36.0, result);
    }

    /// <summary>
    ///     A single connector needs no extra lane, so the base gap is returned unchanged.
    /// </summary>
    [Fact]
    public void Widen_SingleConnector_ReturnsBaseGap()
    {
        // Corridor for one connector = 2*10 + 0 = 20, below the base gap, so the base gap wins.
        var result = EdgeCountGapWidener.Widen(baseGap: 24.0, edgeCount: 1);

        Assert.Equal(24.0, result);
    }

    /// <summary>
    ///     A single connector needs no extra lane even when the base gap is narrower than the fixed
    ///     2*ConnectorClearance floor that the corridor formula would otherwise apply — the degenerate
    ///     short-circuit must return the base gap unchanged rather than widen it.
    /// </summary>
    [Fact]
    public void Widen_SingleConnector_BaseGapBelowClearanceFloor_ReturnsBaseGapUnwidened()
    {
        // Corridor for one connector = 2*10 + 0 = 20, which exceeds this base gap of 8, so a naive
        // Math.Max would incorrectly widen it. The degenerate short-circuit must prevent that.
        var result = EdgeCountGapWidener.Widen(baseGap: 8.0, edgeCount: 1);

        Assert.Equal(8.0, result);
    }

    /// <summary>
    ///     A zero (or negative) connector count never narrows the gap below the supplied base gap.
    /// </summary>
    [Fact]
    public void Widen_ZeroConnectors_ReturnsBaseGap()
    {
        // Corridor = 2*10 + (0 - 1)*16 = 20 - 16 = 4, below the base gap, so the base gap wins.
        var result = EdgeCountGapWidener.Widen(baseGap: 24.0, edgeCount: 0);

        Assert.Equal(24.0, result);
    }

    /// <summary>
    ///     A zero connector count never widens the gap either, even when the base gap is very small —
    ///     the degenerate short-circuit applies uniformly to counts of zero and one.
    /// </summary>
    [Fact]
    public void Widen_ZeroConnectors_BaseGapBelowClearanceFloor_ReturnsBaseGapUnwidened()
    {
        var result = EdgeCountGapWidener.Widen(baseGap: 3.0, edgeCount: 0);

        Assert.Equal(3.0, result);
    }

    /// <summary>
    ///     When the base gap already exceeds the corridor width, the base gap is preserved (the widener
    ///     never shrinks a gap).
    /// </summary>
    [Fact]
    public void Widen_BaseGapExceedsCorridor_ReturnsBaseGap()
    {
        // Corridor for two connectors = 36; a base gap of 200 already exceeds it.
        var result = EdgeCountGapWidener.Widen(baseGap: 200.0, edgeCount: 2);

        Assert.Equal(200.0, result);
    }
}
