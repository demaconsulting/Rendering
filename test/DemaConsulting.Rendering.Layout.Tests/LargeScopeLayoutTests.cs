// <copyright file="LargeScopeLayoutTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests for the layout of packages with very many cross-container connectors (Rendering#75): such a
///     package used to take minutes or hours to lay out, and now every size completes with a valid drawing.
///     They lay out a synthetic package of folder containers joined by cross-container connectors (the shape
///     of the OMG Annex A "General" views), and nested packages, through the public layout API only, and
///     check what a caller can observe: every connector is drawn, orthogonally, ending on the boxes it joins;
///     the boxes do not overlap and stay on the canvas; a scope too large for the full routing effort is
///     reported in the tree's warnings (and a small one is not); the same input gives the same drawing on
///     every run; and the small scenes the routing speed-ups must not change lay out to the geometry the
///     unoptimized router produced. How long a layout takes is a design property verified by the opt-in
///     benchmark (see the benchmarks folder), not by a test.
/// </summary>
public sealed class LargeScopeLayoutTests
{
    /// <summary>The number of sub-packages of the nested oversize package.</summary>
    private const int SubPackages = 4;

    /// <summary>The number of folders of each sub-package of the nested oversize package.</summary>
    private const int FoldersPerSubPackage = 2;

    /// <summary>The number of leaves of each folder of a sub-package of the nested oversize package.</summary>
    private const int LeavesPerSubFolder = 3;

    /// <summary>The number of connectors between the folders of one sub-package of the nested oversize package.</summary>
    private const int SubPackageInnerEdges = 3;

    /// <summary>The number of connectors the package scope of the nested oversize package holds.</summary>
    private const int OversizePackageEdges = 80;

    /// <summary>The distance, in logical pixels, within which a connector end counts as lying on a box outline.</summary>
    private const double OutlineTolerance = 1.0;

    /// <summary>
    ///     Lays out the synthetic package of <paramref name="crossEdges"/> connectors twice and asserts what a
    ///     caller can observe of a scope too large for the full routing effort: every connector is drawn, as an
    ///     orthogonal polyline that ends on the boxes it joins, the boxes neither overlap nor leave the canvas,
    ///     exactly one warning says the scope was routed at the reduced effort, and both layouts are identical.
    /// </summary>
    /// <param name="crossEdges">The number of cross-container connectors.</param>
    private static void AssertLargeScopeLaidOutValidly(int crossEdges)
    {
        // Arrange / Act: lay the same package out twice
        var tree = LayoutEngine.Layout(RoutingScenes.SyntheticPackage(crossEdges));
        var again = LayoutEngine.Layout(RoutingScenes.SyntheticPackage(crossEdges));

        // Assert: every connector was drawn, orthogonally, ending on the boxes it joins
        Assert.Equal(RoutingScenes.SyntheticLineCount(crossEdges), RoutingScenes.CountLines(tree.Nodes));
        AssertAllLinesOrthogonal(tree.Nodes);
        AssertEveryLineEndsOnABox(tree);

        // The boxes neither overlap nor leave the canvas
        LayoutGeometryAssert.AssertNoSiblingOverlap(tree);
        LayoutGeometryAssert.AssertWithinCanvas(tree);

        // The scope was too large for the full effort, and the tree says so once
        var hint = Assert.Single(tree.Warnings, warning => warning.Contains("work limit", StringComparison.Ordinal));
        Assert.Contains("without the extra quality searches", hint, StringComparison.Ordinal);

        // The layout depends on the input only
        Assert.Equal(RoutingScenes.Fingerprint(tree), RoutingScenes.Fingerprint(again));
        Assert.Equal(tree.Warnings, again.Warnings);
    }

    /// <summary>
    ///     Proves forty cross-container connectors lay out to a valid drawing that says its scope was routed at
    ///     the reduced effort (the full effort took minutes; Rendering#75).
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_FortyCrossContainerEdges_DrawsEveryConnectorAndReportsReducedEffort() =>
        AssertLargeScopeLaidOutValidly(40);

    /// <summary>
    ///     Proves three hundred and twenty cross-container connectors lay out to a valid drawing that says
    ///     its scope was routed at the reduced effort.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_ThreeHundredTwentyCrossContainerEdges_DrawsEveryConnectorAndReportsReducedEffort() =>
        AssertLargeScopeLaidOutValidly(320);

    /// <summary>
    ///     Proves six hundred and forty cross-container connectors lay out to a valid drawing that says its
    ///     scope was routed at the reduced effort: a package this size could not be drawn at all before.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_SixHundredFortyCrossContainerEdges_DrawsEveryConnectorAndReportsReducedEffort() =>
        AssertLargeScopeLaidOutValidly(640);

    /// <summary>
    ///     Proves a package is reported as routed at the reduced effort exactly when its scope is too large for
    ///     the full effort: from sixteen cross-container connectors up the tree carries one hint.
    /// </summary>
    /// <param name="crossEdges">The number of cross-container connectors.</param>
    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(80)]
    [InlineData(160)]
    public void LayoutEngine_Layout_OversizeScope_ReportsReducedEffortOnce(int crossEdges)
    {
        // Arrange
        var graph = RoutingScenes.SyntheticPackage(crossEdges);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert: one hint, and every connector drawn
        var hint = Assert.Single(tree.Warnings, warning => warning.Contains("work limit", StringComparison.Ordinal));
        Assert.Contains("scope of 6 node(s)", hint, StringComparison.Ordinal);
        Assert.Equal(RoutingScenes.SyntheticLineCount(crossEdges), RoutingScenes.CountLines(tree.Nodes));
    }

    /// <summary>
    ///     Proves a package whose own scope is too large for the full routing effort is drawn completely and
    ///     carries the reduction into the scopes inside it: every connector is drawn orthogonally, and the one
    ///     hint names the oversize scope and says it holds for the scopes inside it.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_OversizeNestedPackage_DrawsEveryConnectorAndWarnsOnce()
    {
        // Arrange: a package of sub-packages whose own scope holds 80 connectors, each sub-package small
        var graph = BuildOversizeNestedPackage();

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert: every connector is drawn, orthogonally, ending on the boxes it joins
        Assert.Equal(OversizeNestedLineCount, RoutingScenes.CountLines(tree.Nodes));
        AssertAllLinesOrthogonal(tree.Nodes);
        AssertEveryLineEndsOnABox(tree);
        LayoutGeometryAssert.AssertNoSiblingOverlap(tree);

        // One hint for the scope that is really oversize, saying it holds for the scopes inside it
        var hint = Assert.Single(tree.Warnings, warning => warning.Contains("work limit", StringComparison.Ordinal));
        Assert.Contains("scopes inside it", hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the nested oversize layout is deterministic: two layouts of the same hierarchy are
    ///     identical in geometry and warnings.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_OversizeNestedPackage_RepeatedRunsAreIdentical()
    {
        // Arrange / Act
        var first = LayoutEngine.Layout(BuildOversizeNestedPackage());
        var second = LayoutEngine.Layout(BuildOversizeNestedPackage());

        // Assert
        Assert.Equal(RoutingScenes.Fingerprint(first), RoutingScenes.Fingerprint(second));
        Assert.Equal(first.Warnings, second.Warnings);
    }

    /// <summary>
    ///     Proves a hierarchy whose scopes all stay under the weight limit is not reduced anywhere: every
    ///     connector is drawn and no warning is raised.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_SmallNestedPackage_HasNoWarnings()
    {
        // Arrange: the same hierarchy with only 2 sub-packages and 2 connectors in the package scope
        var graph = RoutingScenes.NestedPackage(2, FoldersPerSubPackage, LeavesPerSubFolder, SubPackageInnerEdges, 2);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        Assert.Empty(tree.Warnings);
        Assert.Equal(
            RoutingScenes.NestedLineCount(2, FoldersPerSubPackage, LeavesPerSubFolder, SubPackageInnerEdges, 2),
            RoutingScenes.CountLines(tree.Nodes));
        AssertAllLinesOrthogonal(tree.Nodes);
    }

    /// <summary>
    ///     Proves the hint of a limited hierarchy names the scope that is really oversize: the package that
    ///     holds the connectors is the only scope it names, not the sub-packages that inherited the reduction.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_OversizeNestedPackage_NamesOnlyTheOversizeScope()
    {
        // Arrange
        var graph = BuildOversizeNestedPackage();

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert: the hint names the scope of the sub-packages, not a sub-package's scope
        var hint = Assert.Single(tree.Warnings, warning => warning.Contains("work limit", StringComparison.Ordinal));
        Assert.Contains($"scope of {SubPackages} node(s)", hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the routing speed-ups changed no coordinate: the five-connector synthetic package lays out
    ///     to exactly the geometry the unoptimized router produced, and carries no warning.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_FiveCrossContainerEdges_MatchesGoldenGeometry()
    {
        // Arrange
        var graph = RoutingScenes.SyntheticPackage(5);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert: the fingerprint equals the one captured from the unoptimized router, and the scope is not limited
        Assert.Equal("A5A6846465EC", RoutingScenes.Fingerprint(tree));
        Assert.Empty(tree.Warnings);
    }

    /// <summary>
    ///     Proves the routing speed-ups changed no coordinate: the ten-connector synthetic package lays out
    ///     to exactly the geometry the unoptimized router produced, and carries no warning.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_TenCrossContainerEdges_MatchesGoldenGeometry()
    {
        // Arrange
        var graph = RoutingScenes.SyntheticPackage(10);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert: the fingerprint equals the one captured from the unoptimized router, and the scope is not limited
        Assert.Equal("0B2A5365156A", RoutingScenes.Fingerprint(tree));
        Assert.Empty(tree.Warnings);
    }

    /// <summary>
    ///     Proves the routing speed-ups changed no coordinate of a nested package: two sub-packages joined
    ///     by three package connectors lay out to exactly the geometry the unoptimized router produced.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_NestedPackageWithPackageEdges_MatchesGoldenGeometry()
    {
        // Arrange
        var graph = RoutingScenes.NestedPackage(2, FoldersPerSubPackage, LeavesPerSubFolder, SubPackageInnerEdges, 3);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        Assert.Equal("4AAD93D3BA87", RoutingScenes.Fingerprint(tree));
        Assert.Empty(tree.Warnings);
    }

    /// <summary>
    ///     Proves the routing speed-ups changed no coordinate of a sub-package that routes its own
    ///     connectors between three folders of four leaves.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_SingleSubPackageOfThreeFolders_MatchesGoldenGeometry()
    {
        // Arrange
        var graph = RoutingScenes.NestedPackage(1, 3, 4, 6, 0);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        Assert.Equal("454BE8A40883", RoutingScenes.Fingerprint(tree));
        Assert.Empty(tree.Warnings);
    }

    /// <summary>
    ///     Proves the layout is deterministic: two layouts of the same large package are identical.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_FortyCrossContainerEdges_RepeatedRunsAreIdentical()
    {
        // Arrange / Act: lay the same package out twice
        var first = RoutingScenes.Fingerprint(LayoutEngine.Layout(RoutingScenes.SyntheticPackage(40)));
        var second = RoutingScenes.Fingerprint(LayoutEngine.Layout(RoutingScenes.SyntheticPackage(40)));

        // Assert: identical geometry
        Assert.Equal(first, second);
    }

    /// <summary>
    ///     Proves no scene of the layout-quality benchmark set is reported as routed at the reduced effort:
    ///     the limits sit above the work the heaviest scene needs, so a future change that makes routing
    ///     dearer is noticed here rather than silently degrading the benchmark diagrams.
    /// </summary>
    /// <param name="index">The index of the benchmark scene.</param>
    [Theory]
    [MemberData(nameof(BenchmarkSceneIndexes))]
    public void LayoutEngine_Layout_BenchmarkScene_HasNoReducedEffortWarning(int index)
    {
        // Arrange
        var scene = LayoutQualityScenes.Benchmarks[index];

        // Act
        var tree = LayoutEngine.Layout(scene.Build());

        // Assert: no routing limit warning
        Assert.DoesNotContain(tree.Warnings, warning => warning.Contains("work limit", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Proves the heaviest benchmark scene, the SysML general view in every insertion order the baseline
    ///     checks, is not reported as routed at the reduced effort.
    /// </summary>
    /// <param name="seed">The shuffle seed, or <see langword="null"/> for the fixed scattered order.</param>
    [Theory]
    [MemberData(nameof(GeneralViewSeeds))]
    public void LayoutEngine_Layout_GeneralViewUp_HasNoReducedEffortWarning(int? seed)
    {
        // Act
        var tree = LayoutEngine.Layout(LayoutQualityScenes.GeneralViewUp(LayoutFlowDirection.Up, seed));

        // Assert
        Assert.DoesNotContain(tree.Warnings, warning => warning.Contains("work limit", StringComparison.Ordinal));
    }

    /// <summary>The indexes of the benchmark scenes.</summary>
    /// <returns>The theory data.</returns>
    public static TheoryData<int> BenchmarkSceneIndexes()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < LayoutQualityScenes.Benchmarks.Count; i++)
        {
            data.Add(i);
        }

        return data;
    }

    /// <summary>The insertion orders of the general-view scene the baseline checks.</summary>
    /// <returns>The theory data.</returns>
    public static TheoryData<int?> GeneralViewSeeds()
    {
        var data = new TheoryData<int?>();
        foreach (var seed in LayoutQualityBaseline.GeneralViewUpOrders)
        {
            data.Add(seed);
        }

        return data;
    }

    /// <summary>
    ///     Proves the hint survives the whole pipeline: an automatic layout of a package whose routing is
    ///     reduced, laid out with an unrelated isolated node beside it so the automatic layout splits and
    ///     repacks the pieces, followed by the canvas fit, still carries the warning.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_LimitedScope_WarningSurvivesAutoLayoutAndCanvasFit()
    {
        // Arrange: the 40-connector package with an isolated node beside it
        var graph = RoutingScenes.SyntheticPackage(40);
        graph.AddNode("loner", 80, 40);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        Assert.Contains(tree.Warnings, warning => warning.Contains("work limit", StringComparison.Ordinal));
    }

    /// <summary>Gets the number of connector lines the nested oversize package draws.</summary>
    private static int OversizeNestedLineCount =>
        RoutingScenes.NestedLineCount(SubPackages, FoldersPerSubPackage, LeavesPerSubFolder, SubPackageInnerEdges, OversizePackageEdges);

    /// <summary>
    ///     Builds the nested oversize package: four small sub-packages and a package scope of 80 connectors.
    /// </summary>
    /// <returns>The graph to lay out.</returns>
    private static LayoutGraph BuildOversizeNestedPackage() =>
        RoutingScenes.NestedPackage(SubPackages, FoldersPerSubPackage, LeavesPerSubFolder, SubPackageInnerEdges, OversizePackageEdges);

    /// <summary>Asserts that every connector line under <paramref name="nodes"/> is an orthogonal polyline.</summary>
    /// <param name="nodes">The nodes to check.</param>
    private static void AssertAllLinesOrthogonal(IReadOnlyList<LayoutNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is LayoutLine line)
            {
                Assert.True(line.Waypoints.Count >= 2, "A connector has fewer than two waypoints.");
                for (var i = 0; i < line.Waypoints.Count - 1; i++)
                {
                    var p = line.Waypoints[i];
                    var q = line.Waypoints[i + 1];
                    Assert.True(Math.Abs(p.X - q.X) < 1e-6 || Math.Abs(p.Y - q.Y) < 1e-6, "A connector segment is not axis-aligned.");
                }
            }
            else if (node is LayoutBox box)
            {
                AssertAllLinesOrthogonal(box.Children);
            }
        }
    }

    /// <summary>
    ///     Asserts that both ends of every connector line of <paramref name="tree"/> lie on the outline of
    ///     some box: a connector that is drawn but joins nothing is not a drawn connector.
    /// </summary>
    /// <param name="tree">The laid-out tree.</param>
    private static void AssertEveryLineEndsOnABox(LayoutTree tree)
    {
        var boxes = LayoutGeometryAssert.AllBoxes(tree);
        foreach (var line in LayoutGeometryAssert.AllLines(tree))
        {
            Assert.True(OnSomeOutline(boxes, line.Waypoints[0]), $"The start {line.Waypoints[0]} of a connector is on no box outline.");
            Assert.True(OnSomeOutline(boxes, line.Waypoints[^1]), $"The end {line.Waypoints[^1]} of a connector is on no box outline.");
        }
    }

    /// <summary>Returns whether <paramref name="point"/> lies on the outline of one of <paramref name="boxes"/>.</summary>
    /// <param name="boxes">The boxes.</param>
    /// <param name="point">The point to test.</param>
    /// <returns><see langword="true"/> when the point is within the outline tolerance of a box's border.</returns>
    private static bool OnSomeOutline(IReadOnlyList<LayoutBox> boxes, Point2D point)
    {
        foreach (var box in boxes)
        {
            var insideX = point.X >= box.X - OutlineTolerance && point.X <= box.X + box.Width + OutlineTolerance;
            var insideY = point.Y >= box.Y - OutlineTolerance && point.Y <= box.Y + box.Height + OutlineTolerance;
            var nearVerticalSide = Math.Abs(point.X - box.X) <= OutlineTolerance || Math.Abs(point.X - (box.X + box.Width)) <= OutlineTolerance;
            var nearHorizontalSide = Math.Abs(point.Y - box.Y) <= OutlineTolerance || Math.Abs(point.Y - (box.Y + box.Height)) <= OutlineTolerance;
            if (insideX && insideY && (nearVerticalSide || nearHorizontalSide))
            {
                return true;
            }
        }

        return false;
    }
}
