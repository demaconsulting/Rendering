// <copyright file="HierarchicalCrossOrderingTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Layout.Engine;
namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests for the cross-hierarchy ordering refinement of <see cref="HierarchicalLayoutAlgorithm"/>: the
///     crossing counter it accepts proposals by, and the memoization and counters of its per-run context.
/// </summary>
public sealed class HierarchicalCrossOrderingTests
{
    /// <summary>Two connectors that cross at an interior point count as one crossing.</summary>
    [Fact]
    public void HierarchicalLayoutAlgorithm_CountCrossings_CrossingLines_CountsOne()
    {
        // Arrange: a horizontal and a vertical connector crossing at (50, 50)
        var tree = new LayoutTree(100.0, 100.0, [Line((0, 50), (100, 50)), Line((50, 0), (50, 100))]);

        // Act
        var crossings = HierarchicalLayoutAlgorithm.CountCrossings(tree);

        // Assert
        Assert.Equal(1, crossings);
    }

    /// <summary>Connectors that meet at a shared end point, or run along each other, do not cross.</summary>
    [Fact]
    public void HierarchicalLayoutAlgorithm_CountCrossings_TouchingAndCollinearLines_CountsZero()
    {
        // Arrange: two connectors sharing an end point, and a third running along the first
        var tree = new LayoutTree(
            100.0,
            100.0,
            [Line((0, 50), (50, 50)), Line((50, 50), (50, 100)), Line((10, 50), (40, 50))]);

        // Act
        var crossings = HierarchicalLayoutAlgorithm.CountCrossings(tree);

        // Assert
        Assert.Equal(0, crossings);
    }

    /// <summary>Connectors inside container boxes are counted against the connectors outside them.</summary>
    [Fact]
    public void HierarchicalLayoutAlgorithm_CountCrossings_InteriorLine_CountsAgainstOuterLine()
    {
        // Arrange: a container holding a vertical connector, and an outer connector through it
        var inner = Line((50, 20), (50, 80));
        var container = new LayoutBox(10.0, 10.0, 80.0, 80.0, "container", 0, BoxShape.Rectangle, [], [inner]);
        var tree = new LayoutTree(100.0, 100.0, [container, Line((0, 50), (100, 50))]);

        // Act
        var crossings = HierarchicalLayoutAlgorithm.CountCrossings(tree);

        // Assert
        Assert.Equal(1, crossings);
    }

    /// <summary>
    ///     A refinement that lays a nested scene out again reuses the interior layouts whose subtree order
    ///     did not change, and reports how many scopes it refined.
    /// </summary>
    [Fact]
    public void HierarchicalLayoutAlgorithm_ApplyCore_NestedScene_ReusesUnchangedInteriorLayouts()
    {
        // Arrange: a nested scene with a crossing in its first pass (insertion order S, C, H)
        var graph = LayoutQualityScenes.NestedDepth("hierarchical", LayoutFlowDirection.Right, ["S", "C", "H"]);
        var context = new HierarchicalLayoutAlgorithm.OrderingRefinementContext();
        var options = new LayoutOptions();
        options.Set<HierarchicalLayoutAlgorithm.OrderingRefinementContext?>(HierarchicalLayoutAlgorithm.RefinementContext, context);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, options);

        // Assert
        Assert.Equal(0, HierarchicalLayoutAlgorithm.CountCrossings(tree));
        Assert.True(context.RefinedScopes >= 1);
        Assert.True(context.PlacementPasses >= 1);
        Assert.True(context.MemoHits >= 1, "no interior layout was served from the memo");
    }

    /// <summary>
    ///     Lays out the same graph twice, once with a caller-supplied context and once without: the
    ///     context only observes, so both trees are geometrically identical.
    /// </summary>
    [Fact]
    public void HierarchicalLayoutAlgorithm_ApplyCore_WithAndWithoutContext_IdenticalTree()
    {
        // Arrange
        var observed = new HierarchicalLayoutAlgorithm.OrderingRefinementContext();
        var options = new LayoutOptions();
        options.Set<HierarchicalLayoutAlgorithm.OrderingRefinementContext?>(HierarchicalLayoutAlgorithm.RefinementContext, observed);

        // Act
        var with = new HierarchicalLayoutAlgorithm().ApplyCore(
            LayoutQualityScenes.Repro3("hierarchical", LayoutFlowDirection.Right, ["C", "S", "H"]),
            options);
        var without = new HierarchicalLayoutAlgorithm().ApplyCore(
            LayoutQualityScenes.Repro3("hierarchical", LayoutFlowDirection.Right, ["C", "S", "H"]),
            new LayoutOptions());

        // Assert
        Assert.Equal(LayoutQualityMetrics.Fingerprint(without), LayoutQualityMetrics.Fingerprint(with));
    }

    /// <summary>
    ///     Proves the ordering refinement is bounded: a scope is laid out and routed at most seven times
    ///     (the first pass and the proposals), and a scene with a scope that can be refined really was refined.
    /// </summary>
    /// <param name="scene">The benchmark scene name.</param>
    /// <param name="refined">Whether the scene has a scope the refinement must have worked on.</param>
    [Theory]
    [InlineData("Repro2Right", true)]
    [InlineData("Repro3Right", true)]
    [InlineData("NestedDepthRight", true)]
    [InlineData("NestedPackagesRight", false)]
    [InlineData("InterconnectionLabeledCorridors", false)]
    public void HierarchicalLayoutAlgorithm_ApplyCore_RefinementPassBudget_AtMostSevenLayouts(string scene, bool refined)
    {
        // Arrange: a refinement context the test can read the counters of
        var graph = LayoutQualityScenes.Benchmarks.Single(candidate => candidate.Name == scene).Build();
        var context = new HierarchicalLayoutAlgorithm.OrderingRefinementContext();
        var options = new LayoutOptions();
        options.Set<HierarchicalLayoutAlgorithm.OrderingRefinementContext?>(HierarchicalLayoutAlgorithm.RefinementContext, context);

        // Act
        _ = new HierarchicalLayoutAlgorithm().ApplyCore(graph, options);

        // Assert
        Assert.True(context.MaxLayoutsPerScope <= HierarchicalLayoutAlgorithm.MaxLayoutsPerRefinedScope);
        Assert.Equal(7, HierarchicalLayoutAlgorithm.MaxLayoutsPerRefinedScope);
        if (refined)
        {
            Assert.True(context.RefinedScopes > 0, "no scope was refined");
            Assert.True(context.MaxLayoutsPerScope >= 2, "the refinement made no extra layout");
        }
    }

    /// <summary>
    ///     Proves a scope without any cross-container connector is never refined and never pays for a
    ///     placement-only pass.
    /// </summary>
    [Fact]
    public void HierarchicalLayoutAlgorithm_ApplyCore_NoCrossContainerEdges_NotRefined()
    {
        // Arrange: one container with two members and an interior connector, and no connector between
        // containers
        var graph = new LayoutGraph();
        var holder = graph.AddNode("holder", 10, 10);
        var a = holder.Children.AddNode("a", 80, 30);
        var b = holder.Children.AddNode("b", 80, 30);
        holder.Children.AddEdge("a_b", a, b);
        graph.AddNode("other", 80, 30);
        var context = new HierarchicalLayoutAlgorithm.OrderingRefinementContext();
        var options = new LayoutOptions();
        options.Set<HierarchicalLayoutAlgorithm.OrderingRefinementContext?>(HierarchicalLayoutAlgorithm.RefinementContext, context);

        // Act
        _ = new HierarchicalLayoutAlgorithm().ApplyCore(graph, options);

        // Assert
        Assert.Equal(0, context.RefinedScopes);
        Assert.Equal(0, context.PlacementPasses);
    }

    /// <summary>
    ///     Proves a scope whose routing was cut off by its hard work limit is reported in the context's hints
    ///     as one that fell back to the simplified elbow route, naming the number of connectors and the scope.
    /// </summary>
    [Fact]
    public void HierarchicalLayoutAlgorithm_RecordRoutingBudget_ElbowRoutes_ReportsSimplifiedElbowHint()
    {
        // Arrange: a scope of two nodes, and a budget with no work left whose scope was sized for the reduced
        // effort and routed one connector with the elbow fallback
        var scope = new LayoutGraph();
        scope.AddNode("first", 80, 30);
        scope.AddNode("second", 80, 30);
        var budget = new RoutingWorkBudget(new RoutingWorkLimits(Soft: 0, Hard: 0, ReducedWeight: 1));
        budget.NoteScopeWeight(1);
        budget.RecordElbowRoute();
        var context = new HierarchicalLayoutAlgorithm.OrderingRefinementContext();

        // Act
        context.RecordRoutingBudget(scope, budget);

        // Assert: one hint that names the scope, its work limit and the elbow route
        var hint = Assert.Single(context.RoutingLimitWarnings);
        Assert.Contains("scope of 2 node(s) starting at 'first'", hint, StringComparison.Ordinal);
        Assert.Contains("work limit", hint, StringComparison.Ordinal);
        Assert.Contains("1 connector(s) were routed with the simplified elbow route", hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves a scope that never left the full effort leaves no hint.
    /// </summary>
    [Fact]
    public void HierarchicalLayoutAlgorithm_RecordRoutingBudget_FullEffort_ReportsNothing()
    {
        // Arrange
        var scope = new LayoutGraph();
        scope.AddNode("first", 80, 30);
        var budget = new RoutingWorkBudget(new RoutingWorkLimits(Soft: long.MaxValue / 4, Hard: long.MaxValue / 2));
        var context = new HierarchicalLayoutAlgorithm.OrderingRefinementContext();

        // Act
        context.RecordRoutingBudget(scope, budget);

        // Assert
        Assert.Empty(context.RoutingLimitWarnings);
    }

    /// <summary>Builds a connector between two points.</summary>
    private static LayoutLine Line((double X, double Y) from, (double X, double Y) to) =>
        new(
            [new Point2D(from.X, from.Y), new Point2D(to.X, to.Y)],
            EndMarkerStyle.None,
            EndMarkerStyle.None,
            LineStyle.Solid,
            null);
}
