// <copyright file="CrossHierarchyOrderingTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Regression tests for Rendering#48: the interior order of a container must follow the positions of its
///     cross-hierarchy partners, so connectors between containers do not cross when an uncrossed arrangement
///     exists, whatever order the caller inserted the nodes in, and connectors the declaring scope routes
///     must not cross the interior connectors of the containers they enter.
/// </summary>
public sealed class CrossHierarchyOrderingTests
{
    /// <summary>
    ///     Every root algorithm, flow direction, and insertion order of the first folder's members, the cells
    ///     the Rendering#48 repro scenes are checked in. The order is passed as its letters (for example
    ///     <c>CSH</c>).
    /// </summary>
    public static TheoryData<string, LayoutFlowDirection, string> RootDirectionOrderCells()
    {
        var cells = new TheoryData<string, LayoutFlowDirection, string>();
        foreach (var root in LayoutQualityScenes.RootAlgorithms)
        {
            foreach (var direction in LayoutQualityScenes.AllDirections)
            {
                foreach (var order in LayoutQualityScenes.InsertionOrders)
                {
                    cells.Add(root, direction, string.Concat(order));
                }
            }
        }

        return cells;
    }

    /// <summary>
    ///     Every root algorithm and flow direction.
    /// </summary>
    public static TheoryData<string, LayoutFlowDirection> RootDirectionCells()
    {
        var cells = new TheoryData<string, LayoutFlowDirection>();
        foreach (var root in LayoutQualityScenes.RootAlgorithms)
        {
            foreach (var direction in LayoutQualityScenes.AllDirections)
            {
                cells.Add(root, direction);
            }
        }

        return cells;
    }

    /// <summary>
    ///     Proves Repro 2 of Rendering#48 lays out with no connector crossing for every insertion order of
    ///     the first folder's members, in every flow direction and under both routing roots.
    /// </summary>
    /// <param name="root">The root algorithm id.</param>
    /// <param name="direction">The flow direction.</param>
    /// <param name="order">The insertion order of the folder members.</param>
    [Theory]
    [MemberData(nameof(RootDirectionOrderCells))]
    public void LayoutEngine_Layout_Repro2_AnyInsertionOrder_ZeroCrossings(string root, LayoutFlowDirection direction, string order)
    {
        // Arrange: the repro graph with the members inserted in the requested order
        var graph = LayoutQualityScenes.Repro2(root, direction, ToOrder(order));

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert: a planar arrangement exists, so no two connectors cross
        Assert.Equal(0, LayoutQualityMetrics.CountCrossings(tree));
    }

    /// <summary>
    ///     Proves Repro 3 of Rendering#48 (Repro 2 plus an interior connector <c>M→E</c> of the container
    ///     the declaring scope's connector <c>H→E</c> enters) lays out with no crossing, including the
    ///     crossing of that interior connector, in every flow direction, root, and insertion order.
    /// </summary>
    /// <param name="root">The root algorithm id.</param>
    /// <param name="direction">The flow direction.</param>
    /// <param name="order">The insertion order of the folder members.</param>
    [Theory]
    [MemberData(nameof(RootDirectionOrderCells))]
    public void LayoutEngine_Layout_Repro3_InteriorConnector_ZeroCrossings(string root, LayoutFlowDirection direction, string order)
    {
        // Arrange
        var graph = LayoutQualityScenes.Repro3(root, direction, ToOrder(order));

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        Assert.Equal(0, LayoutQualityMetrics.CountCrossings(tree));
    }

    /// <summary>
    ///     Proves Repro 3 is also crossing-free when the interior member <c>M</c> is inserted before the
    ///     entered node <c>E</c>.
    /// </summary>
    /// <param name="root">The root algorithm id.</param>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [MemberData(nameof(RootDirectionCells))]
    public void LayoutEngine_Layout_Repro3_InteriorMemberInsertedFirst_ZeroCrossings(string root, LayoutFlowDirection direction)
    {
        // Arrange
        var graph = LayoutQualityScenes.Repro3(root, direction, ["C", "S", "H"], interiorFirst: true);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        Assert.Equal(0, LayoutQualityMetrics.CountCrossings(tree));
    }

    /// <summary>
    ///     Proves the layout of Repro 2 does not depend on the insertion order: every order gives the same
    ///     crossing count, and a canvas no larger than the best main@219650c reaches over all orders.
    /// </summary>
    /// <param name="root">The root algorithm id.</param>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [MemberData(nameof(RootDirectionCells))]
    public void LayoutEngine_Layout_Repro2_InsertionOrderIndependent_SameCrossingsAndCompactCanvas(string root, LayoutFlowDirection direction)
    {
        // Arrange / Act: lay out every insertion order
        var trees = LayoutQualityScenes.InsertionOrders
            .Select(order => LayoutEngine.Layout(LayoutQualityScenes.Repro2(root, direction, order)))
            .ToList();

        // Assert: one crossing count for all orders, and no canvas larger than main's best
        Assert.Single(trees.Select(LayoutQualityMetrics.CountCrossings).Distinct());
        var best = LayoutQualityBaseline.Repro2BestCanvasArea(direction);
        Assert.All(trees, tree => Assert.True(LayoutQualityMetrics.CanvasArea(tree) <= best + 1e-6));
    }

    /// <summary>
    ///     Proves a leaf nested three containers deep, a leaf in a sibling container, and a root leaf are
    ///     joined without crossings whatever order the first folder's members were inserted in.
    /// </summary>
    /// <param name="root">The root algorithm id.</param>
    /// <param name="direction">The flow direction.</param>
    /// <param name="order">The insertion order of the folder members.</param>
    [Theory]
    [MemberData(nameof(RootDirectionOrderCells))]
    public void LayoutEngine_Layout_NestedDepth_LeafPartnerAcrossTwoLevels_ZeroCrossings(string root, LayoutFlowDirection direction, string order)
    {
        // Arrange
        var graph = LayoutQualityScenes.NestedDepth(root, direction, ToOrder(order));

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        Assert.Equal(0, LayoutQualityMetrics.CountCrossings(tree));
    }

    /// <summary>
    ///     Proves the general view (Up) never has more crossings than the better of main@219650c and main
    ///     plus the caller-side two-pass ordering prototype, for the same insertion order, for the fixed
    ///     scattered order and several pseudo-random ones: the engine reaches on its own what a caller
    ///     could only reach by working around it, whatever order the items were inserted in.
    /// </summary>
    /// <remarks>
    ///     DO NOT RAISE THE BOUND. It is derived, not tuned: per insertion order it is
    ///     <c>min(main, main + prototype)</c> as measured on main@219650c (see
    ///     <see cref="LayoutQualityBaseline.GeneralViewUpCrossingBound"/>). If a change makes this fail, the
    ///     change made the layout worse than doing nothing (or than the caller's workaround) for that order;
    ///     fix the change, not the numbers.
    /// </remarks>
    /// <param name="seed">The shuffle seed; <see langword="null"/> is the fixed scattered order.</param>
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void LayoutEngine_Layout_GeneralViewUp_AnyInsertionOrder_CrossingsNotAboveMainOrPrototype(int? seed)
    {
        // Arrange
        var graph = LayoutQualityScenes.GeneralViewUp(LayoutFlowDirection.Up, seed);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        var bound = LayoutQualityBaseline.GeneralViewUpCrossingBound(seed);
        var crossings = LayoutQualityMetrics.CountCrossings(tree);
        Assert.True(
            crossings <= bound,
            $"the general view (seed {seed?.ToString() ?? "fixed order"}) has {crossings} crossings, more than the {bound} that main or main with the caller-side prototype reaches");
    }

    /// <summary>
    ///     Guard: a flat graph with no container and no cross-hierarchy edge keeps exactly the geometry
    ///     main@219650c produced (the ordering and compaction work must not touch it).
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    /// <param name="expected">The fingerprint of the main@219650c geometry.</param>
    [Theory]
    [InlineData(LayoutFlowDirection.Down, "42EA4DB77922408FFD5F29E3F9CF63BAB5A4BE2390100AA176DEF3DEDEA37691")]
    [InlineData(LayoutFlowDirection.Up, "70ED374EC843CE8F5ED5954E6F0658E42ABA4A4311CCF9FB78634F81AB5A5FA5")]
    [InlineData(LayoutFlowDirection.Left, "22D7836261AE9649A1341C7A53F331269869912434DD7E00C89A65AF03F24EB7")]
    [InlineData(LayoutFlowDirection.Right, "D9819ED78F590FFE6E602009A28B037ED591D9D07A86399CD61BF293E52CC9AB")]
    public void LayoutEngine_Layout_NoCrossHierarchyEdges_FlatGeometryUnchanged(LayoutFlowDirection direction, string expected)
    {
        // Arrange: four requirements pointing at one definition (flat, no container)
        var graph = DirectionScenes.SatisfyVerify(direction);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        Assert.Equal(expected, LayoutQualityMetrics.Fingerprint(tree));
    }

    /// <summary>
    ///     Proves the layout is deterministic: laying out two fresh copies of the same graph gives
    ///     geometrically identical trees (the ordering refinement must not depend on hash ordering or timing).
    /// </summary>
    /// <param name="scene">The benchmark scene name.</param>
    [Theory]
    [InlineData("Repro3Right")]
    [InlineData("NestedDepthRight")]
    [InlineData("GeneralViewUp")]
    public void LayoutEngine_Layout_LayoutTwice_IdenticalTree(string scene)
    {
        // Arrange
        var build = LayoutQualityScenes.Benchmarks.Single(candidate => candidate.Name == scene).Build;

        // Act
        var first = LayoutEngine.Layout(build());
        var second = LayoutEngine.Layout(build());

        // Assert
        Assert.Equal(LayoutQualityMetrics.Fingerprint(first), LayoutQualityMetrics.Fingerprint(second));
    }

    /// <summary>Splits an order such as <c>CSH</c> into its member ids.</summary>
    private static string[] ToOrder(string order) =>
        [.. order.Select(letter => letter.ToString())];
}
