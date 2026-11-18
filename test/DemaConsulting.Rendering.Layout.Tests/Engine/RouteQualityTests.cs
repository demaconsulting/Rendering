// <copyright file="RouteQualityTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Layout.Engine;

namespace DemaConsulting.Rendering.Layout.Tests.Engine;

/// <summary>
///     Tests for <see cref="RouteQuality"/>: path length, bends, hairpins and close parallel runs of orthogonal
///     connectors, and that the engine's measure agrees with the one the layout-quality tests use.
/// </summary>
public sealed class RouteQualityTests
{
    /// <summary>The length of a path is the sum of its orthogonal segment lengths.</summary>
    [Fact]
    public void RouteQuality_PathLength_Polyline_SumsSegments()
    {
        // Arrange
        Point2D[] path = [new(0, 0), new(100, 0), new(100, 40), new(60, 40)];

        // Act / Assert
        Assert.Equal(180.0, RouteQuality.PathLength(path), 6);
    }

    /// <summary>Only a change of direction is a bend: collinear waypoints are not.</summary>
    [Fact]
    public void RouteQuality_BendCount_CollinearWaypoints_AreNotBends()
    {
        // Arrange: east, east, south, south, west: two bends
        Point2D[] path = [new(0, 0), new(50, 0), new(100, 0), new(100, 30), new(100, 60), new(40, 60)];

        // Act / Assert
        Assert.Equal(2, RouteQuality.BendCount(path));
        Assert.Equal(0, RouteQuality.BendCount([new(0, 0), new(10, 0)]));
    }

    /// <summary>A hairpin is a leg that runs back against the direction of the leg two before it.</summary>
    [Fact]
    public void RouteQuality_HairpinCount_EastSouthWest_IsOneHairpin()
    {
        // Arrange: east, south, west (a hairpin) and a plain Z (east, south, east)
        Point2D[] hairpin = [new(0, 0), new(100, 0), new(100, 30), new(40, 30)];
        Point2D[] zig = [new(0, 0), new(100, 0), new(100, 30), new(160, 30)];

        // Act / Assert
        Assert.Equal(1, RouteQuality.HairpinCount(hairpin));
        Assert.Equal(0, RouteQuality.HairpinCount(zig));
    }

    /// <summary>
    ///     Two parallel runs closer than 16 px (but not on top of each other) for more than 20 px count; runs
    ///     16 px or more apart, on the same line, or overlapping briefly do not.
    /// </summary>
    [Fact]
    public void RouteQuality_CloseParallelLength_GapAndOverlap_CountsOnlySqueezedRuns()
    {
        // Arrange
        Point2D[] reference = [new(0, 0), new(200, 0)];
        Point2D[] tight = [new(50, 10), new(150, 10)];
        Point2D[] apart = [new(50, 16), new(150, 16)];
        Point2D[] onTop = [new(50, 0), new(150, 0)];
        Point2D[] brief = [new(190, 10), new(300, 10)];

        // Act / Assert
        Assert.Equal(100.0, RouteQuality.CloseParallelLength(reference, tight), 6);
        Assert.Equal(0.0, RouteQuality.CloseParallelLength(reference, apart), 6);
        Assert.Equal(0.0, RouteQuality.CloseParallelLength(reference, onTop), 6);
        Assert.Equal(0.0, RouteQuality.CloseParallelLength(reference, brief), 6);
    }

    /// <summary>
    ///     The closeness-weighted measure counts a run 2 px from another far more than one 12 px away, and the
    ///     near-coincident gap limit counts only the former.
    /// </summary>
    [Fact]
    public void RouteQuality_WeightedCloseParallelLength_NearerRunsWeighMore()
    {
        // Arrange
        Point2D[] reference = [new(0, 0), new(200, 0)];
        Point2D[] near = [new(50, 2), new(150, 2)];
        Point2D[] far = [new(50, 12), new(150, 12)];

        // Act
        var nearWeight = RouteQuality.WeightedCloseParallelLength(reference, near);
        var farWeight = RouteQuality.WeightedCloseParallelLength(reference, far);

        // Assert
        Assert.True(nearWeight > 3.0 * farWeight, $"near {nearWeight} should weigh much more than far {farWeight}");
        Assert.Equal(100.0, RouteQuality.CloseParallelLength(reference, near, maxGap: 4.0), 6);
        Assert.Equal(0.0, RouteQuality.CloseParallelLength(reference, far, maxGap: 4.0), 6);
    }

    /// <summary>The summary of a set of connectors adds their lengths, bends and hairpins and pairs their parallel runs.</summary>
    [Fact]
    public void RouteQuality_Measure_TwoConnectors_SumsAllMetrics()
    {
        // Arrange
        IReadOnlyList<IReadOnlyList<Point2D>> lines =
        [
            [new(0, 0), new(200, 0)],
            [new(50, 10), new(150, 10), new(150, 40), new(100, 40)],
        ];

        // Act
        var summary = RouteQuality.Measure(lines);

        // Assert
        Assert.Equal(200.0 + 100.0 + 30.0 + 50.0, summary.Length, 6);
        Assert.Equal(2, summary.Bends);
        Assert.Equal(1, summary.Hairpins);
        Assert.Equal(100.0, summary.ParallelLength, 6);
    }

    /// <summary>
    ///     The engine's measure and the layout-quality metrics the regression tests use agree on a real layout,
    ///     so a guard written against the metrics guards what the router and the ordering refinement weigh.
    /// </summary>
    [Fact]
    public void RouteQuality_Measure_BenchmarkScene_AgreesWithLayoutQualityMetrics()
    {
        // Arrange
        var tree = LayoutEngine.Layout(LayoutQualityScenes.NestedPackages(LayoutFlowDirection.Right));
        var lines = LayoutQualityMetrics.AllLines(tree).Select(line => line.Waypoints).ToList();

        // Act
        var summary = RouteQuality.Measure(lines);

        // Assert
        Assert.Equal(LayoutQualityMetrics.TotalConnectorLength(tree), summary.Length, 3);
        Assert.Equal(LayoutQualityMetrics.TotalBends(tree), summary.Bends);
        Assert.Equal(LayoutQualityMetrics.UTurns(tree), summary.Hairpins);
        Assert.Equal(LayoutQualityMetrics.ParallelRunLength(tree), summary.ParallelLength, 3);
    }
}
