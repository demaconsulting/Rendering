// <copyright file="RoutingPerformanceTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout.Engine;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Regression tests for Rendering#75: cross-container connector routing must stay far below the cubic
///     cost it once had in the number of connectors (measured: sub-quadratic, about 2.6 times the work per
///     doubling at 640 connectors). They lay out a synthetic package of folder containers joined by
///     cross-container connectors (the shape of the OMG Annex A "General" views), and a nested package
///     whose own scope is oversize, and check that the layouts are produced within a generous wall-clock
///     bound, that the routing work grows slowly (counted in work units, without a clock), that they are
///     deterministic, and that the routing speed-ups did not change a single coordinate of the small scenes
///     the speed-ups must leave byte-identical.
/// </summary>
public sealed class RoutingPerformanceTests
{
    /// <summary>The number of folder containers of the synthetic package.</summary>
    private const int Containers = 6;

    /// <summary>The number of leaf boxes inside each folder.</summary>
    private const int LeavesPerContainer = 5;

    /// <summary>
    ///     The wall-clock bound of the 40-connector layout. The reference machine needs well under two
    ///     seconds; the bound is generous so a slow CI agent does not fail it, yet it is a fraction of the
    ///     minutes the unoptimized router needs.
    /// </summary>
    private static readonly TimeSpan FortyEdgeBound = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     The wall-clock bound of the 320-connector layout (the reference machine needs about a second).
    /// </summary>
    private static readonly TimeSpan ThreeHundredTwentyEdgeBound = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     The wall-clock bound of the 640-connector layout (the reference machine needs about three seconds).
    /// </summary>
    private static readonly TimeSpan SixHundredFortyEdgeBound = TimeSpan.FromSeconds(15);

    /// <summary>The number of sub-packages of the nested oversize package.</summary>
    private const int SubPackages = 4;

    /// <summary>The number of folders of each sub-package.</summary>
    private const int FoldersPerSubPackage = 3;

    /// <summary>The number of leaves of each folder of a sub-package.</summary>
    private const int LeavesPerSubFolder = 4;

    /// <summary>The number of connectors between the folders of one sub-package.</summary>
    private const int SubPackageInnerEdges = 6;

    /// <summary>The number of connector lines one sub-package draws on its own (its inner and its folders' spanning-tree ones).</summary>
    private const int SubPackageEdges = SubPackageInnerEdges + (FoldersPerSubPackage * (LeavesPerSubFolder - 1));

    /// <summary>
    ///     Returns the most routing work any one scope spent laying out the synthetic package of
    ///     <paramref name="crossEdges"/> connectors.
    /// </summary>
    /// <param name="crossEdges">The number of cross-container connectors.</param>
    /// <returns>The work, in routing budget units.</returns>
    private static long RoutingWorkOf(int crossEdges)
    {
        var context = new HierarchicalLayoutAlgorithm.OrderingRefinementContext();
        var graph = BuildSyntheticPackage(crossEdges);
        graph.Set<HierarchicalLayoutAlgorithm.OrderingRefinementContext?>(HierarchicalLayoutAlgorithm.RefinementContext, context);
        _ = LayoutEngine.Layout(graph);
        return context.MaxScopeWork;
    }

    /// <summary>
    ///     Builds a package of <paramref name="subPackages"/> sub-packages, each holding
    ///     <see cref="FoldersPerSubPackage"/> folders of <see cref="LeavesPerSubFolder"/> leaves joined by
    ///     <see cref="SubPackageInnerEdges"/> connectors, plus <paramref name="packageEdges"/> seeded random
    ///     connectors between leaves of different sub-packages, declared in the package scope. With enough
    ///     of them the package scope alone is too large for the full routing effort while every sub-package
    ///     scope is small.
    /// </summary>
    /// <param name="subPackages">The number of sub-packages.</param>
    /// <param name="packageEdges">The number of connectors in the package scope.</param>
    /// <returns>The graph to lay out.</returns>
    private static LayoutGraph BuildNestedOversizePackage(int subPackages, int packageEdges)
    {
        var rng = new Random(7);
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.MergeParallelEdges, false);
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        graph.Set(CoreOptions.Algorithm, AutoLayoutAlgorithm.AlgorithmId);

        var package = graph.AddNode("folder:Top", 0, 0);
        package.Label = "Top";
        package.Shape = BoxShape.Folder;
        package.Set(CoreOptions.Algorithm, AutoLayoutAlgorithm.AlgorithmId);

        // The sub-packages, their folders, the leaves, and the connectors inside each sub-package
        var leaves = new List<List<LayoutGraphNode>>();
        for (var s = 0; s < subPackages; s++)
        {
            var sub = package.Children.AddNode($"folder:Top::S{s}", 0, 0);
            sub.Label = $"S{s}";
            sub.Shape = BoxShape.Folder;
            sub.Set(CoreOptions.Algorithm, AutoLayoutAlgorithm.AlgorithmId);
            var subLeaves = new List<LayoutGraphNode>();
            var folderLeaves = new List<List<LayoutGraphNode>>();
            for (var f = 0; f < FoldersPerSubPackage; f++)
            {
                var folder = sub.Children.AddNode($"folder:Top::S{s}::F{f}", 0, 0);
                folder.Label = $"F{f}";
                folder.Shape = BoxShape.Folder;
                folder.Set(CoreOptions.Algorithm, AutoLayoutAlgorithm.AlgorithmId);
                var list = new List<LayoutGraphNode>();
                for (var l = 0; l < LeavesPerSubFolder; l++)
                {
                    var leaf = folder.Children.AddNode($"S{s}::F{f}::L{l}", 120, 60);
                    leaf.Label = $"L{l}";
                    list.Add(leaf);
                    if (l > 0)
                    {
                        folder.Children.AddEdge($"in{s}_{f}_{l}", list[(l - 1) / 2], leaf);
                    }
                }

                folderLeaves.Add(list);
                subLeaves.AddRange(list);
            }

            // Connectors between the folders of the sub-package, declared in the sub-package scope
            for (var e = 0; e < SubPackageInnerEdges; e++)
            {
                var a = e % FoldersPerSubPackage;
                var b = (a + 1 + (e / FoldersPerSubPackage)) % FoldersPerSubPackage;
                sub.Children.AddEdge($"sub{s}_{e}", folderLeaves[a][e % LeavesPerSubFolder], folderLeaves[b][(e + 1) % LeavesPerSubFolder]);
            }

            leaves.Add(subLeaves);
        }

        // The connectors between sub-packages, declared in the package scope
        var seen = new HashSet<(int, int, int, int)>();
        var added = 0;
        while (added < packageEdges)
        {
            var a = rng.Next(subPackages);
            var b = rng.Next(subPackages);
            if (a == b)
            {
                continue;
            }

            var i = rng.Next(leaves[a].Count);
            var j = rng.Next(leaves[b].Count);
            if (!seen.Add((a, i, b, j)))
            {
                continue;
            }

            package.Children.AddEdge($"top{added}", leaves[a][i], leaves[b][j]);
            added++;
        }

        return graph;
    }

    /// <summary>
    ///     Builds the synthetic package graph: <see cref="Containers"/> folders of
    ///     <see cref="LeavesPerContainer"/> leaves each (a spanning tree of interior connectors inside every
    ///     folder) plus <paramref name="crossEdges"/> seeded random connectors between leaves of different
    ///     folders, declared in the package scope.
    /// </summary>
    /// <param name="crossEdges">The number of cross-container connectors.</param>
    /// <param name="limits">The routing work limits to impose on the layout, or <see langword="null"/> for the defaults.</param>
    /// <returns>The graph to lay out.</returns>
    private static LayoutGraph BuildSyntheticPackage(int crossEdges, RoutingWorkLimits? limits = null)
    {
        // Arrange the root graph and the enclosing package folder
        var rng = new Random(1);
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.MergeParallelEdges, false);
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        graph.Set(CoreOptions.Algorithm, AutoLayoutAlgorithm.AlgorithmId);
        if (limits is not null)
        {
            graph.Set(HierarchicalLayoutAlgorithm.RoutingWorkLimitsOption, limits);
        }

        var package = graph.AddNode("folder:Pkg", 0, 0);
        package.Label = "Pkg";
        package.Shape = BoxShape.Folder;
        package.Set(CoreOptions.Algorithm, AutoLayoutAlgorithm.AlgorithmId);

        // Add the folders with their leaves and the interior spanning trees
        var leaves = new List<List<LayoutGraphNode>>();
        for (var c = 0; c < Containers; c++)
        {
            var folder = package.Children.AddNode($"folder:Pkg::F{c}", 0, 0);
            folder.Label = $"F{c}";
            folder.Shape = BoxShape.Folder;
            folder.Set(CoreOptions.Algorithm, AutoLayoutAlgorithm.AlgorithmId);
            var list = new List<LayoutGraphNode>();
            for (var l = 0; l < LeavesPerContainer; l++)
            {
                var leaf = folder.Children.AddNode($"F{c}::L{l}", 120, 60);
                leaf.Label = $"L{l}";
                list.Add(leaf);
                if (l > 0)
                {
                    folder.Children.AddEdge($"in{c}_{l}", list[(l - 1) / 2], leaf);
                }
            }

            leaves.Add(list);
        }

        // Add the distinct cross-container connectors in the package scope
        var seen = new HashSet<(int, int, int, int)>();
        var added = 0;
        while (added < crossEdges)
        {
            var a = rng.Next(Containers);
            var b = rng.Next(Containers);
            if (a == b)
            {
                continue;
            }

            var i = rng.Next(LeavesPerContainer);
            var j = rng.Next(LeavesPerContainer);
            if (!seen.Add((a, i, b, j)))
            {
                continue;
            }

            package.Children.AddEdge($"x{added}", leaves[a][i], leaves[b][j]);
            added++;
        }

        return graph;
    }

    /// <summary>
    ///     Returns a fingerprint of every box and connector coordinate of a laid-out tree, rounded to three
    ///     decimals so it is independent of floating-point printing differences between runtimes.
    /// </summary>
    /// <param name="tree">The laid-out tree.</param>
    /// <returns>A twelve-character hexadecimal digest.</returns>
    private static string Fingerprint(LayoutTree tree)
    {
        var text = new StringBuilder();
        Append(text, tree.Nodes);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
        var hex = new StringBuilder();
        for (var i = 0; i < 6; i++)
        {
            hex.Append(hash[i].ToString("X2", CultureInfo.InvariantCulture));
        }

        return hex.ToString();
    }

    /// <summary>Appends the coordinates of <paramref name="nodes"/> and their descendants to <paramref name="text"/>.</summary>
    /// <param name="text">The builder to append to.</param>
    /// <param name="nodes">The nodes to describe.</param>
    private static void Append(StringBuilder text, IReadOnlyList<LayoutNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is LayoutBox box)
            {
                text.Append(string.Format(CultureInfo.InvariantCulture, "B{0:F3},{1:F3},{2:F3},{3:F3};", box.X, box.Y, box.Width, box.Height));
                Append(text, box.Children);
            }
            else if (node is LayoutLine line)
            {
                text.Append('L');
                foreach (var waypoint in line.Waypoints)
                {
                    text.Append(string.Format(CultureInfo.InvariantCulture, "{0:F3},{1:F3} ", waypoint.X, waypoint.Y));
                }

                text.Append(';');
            }
        }
    }

    /// <summary>
    ///     Proves forty cross-container connectors lay out within the generous wall-clock bound, where the
    ///     unoptimized router needs minutes (Rendering#75). The layout runs on a pool thread so a runaway
    ///     routing search fails the test instead of hanging the run.
    /// </summary>
    [Fact]
    public async Task LayoutEngine_Layout_FortyCrossContainerEdges_CompletesWithinBound()
    {
        // Arrange: the 40-connector synthetic package
        var graph = BuildSyntheticPackage(40);

        // Act: lay it out on a pool thread under a wall-clock bound
        var layout = Task.Run(() => LayoutEngine.Layout(graph));
        var winner = await Task.WhenAny(layout, Task.Delay(FortyEdgeBound, TestContext.Current.CancellationToken));

        // Assert: it finished within the bound and every connector was drawn
        Assert.True(ReferenceEquals(winner, layout), "Laying out 40 cross-container connectors exceeded the wall-clock bound.");
        var tree = await layout;
        Assert.Equal(40 + (Containers * (LeavesPerContainer - 1)), CountLines(tree.Nodes));
    }

    /// <summary>
    ///     Proves three hundred and twenty cross-container connectors lay out within a generous wall-clock
    ///     bound (the reference machine needs about a second; before the soft-lane limits of the reduced
    ///     effort the layout took thirteen seconds and grew with the cube of the connector count).
    /// </summary>
    [Fact]
    public async Task LayoutEngine_Layout_ThreeHundredTwentyCrossContainerEdges_CompletesWithinBound()
    {
        // Arrange: the 320-connector synthetic package
        var graph = BuildSyntheticPackage(320);

        // Act: lay it out on a pool thread under a wall-clock bound
        var layout = Task.Run(() => LayoutEngine.Layout(graph));
        var winner = await Task.WhenAny(layout, Task.Delay(ThreeHundredTwentyEdgeBound, TestContext.Current.CancellationToken));

        // Assert: it finished within the bound and every connector was drawn
        Assert.True(ReferenceEquals(winner, layout), "Laying out 320 cross-container connectors exceeded the wall-clock bound.");
        var tree = await layout;
        Assert.Equal(320 + (Containers * (LeavesPerContainer - 1)), CountLines(tree.Nodes));
    }

    /// <summary>
    ///     Proves six hundred and forty cross-container connectors lay out within a generous wall-clock bound.
    /// </summary>
    [Fact]
    public async Task LayoutEngine_Layout_SixHundredFortyCrossContainerEdges_CompletesWithinBound()
    {
        // Arrange: the 640-connector synthetic package
        var graph = BuildSyntheticPackage(640);

        // Act: lay it out on a pool thread under a wall-clock bound
        var layout = Task.Run(() => LayoutEngine.Layout(graph));
        var winner = await Task.WhenAny(layout, Task.Delay(SixHundredFortyEdgeBound, TestContext.Current.CancellationToken));

        // Assert: it finished within the bound and every connector was drawn
        Assert.True(ReferenceEquals(winner, layout), "Laying out 640 cross-container connectors exceeded the wall-clock bound.");
        var tree = await layout;
        Assert.Equal(640 + (Containers * (LeavesPerContainer - 1)), CountLines(tree.Nodes));
    }

    /// <summary>
    ///     Proves, without reading a clock, that the routing work of a package grows sub-quadratically with
    ///     its connector count: each doubling from 160 to 640 connectors less than three and a half times the
    ///     work (the measured factors are about 3.0 and 2.6; a quadratic router would multiply it by four and
    ///     the cubic one this fix replaced by eight). The work is the deterministic account of grid nodes built
    ///     and search states expanded that the routing budget keeps.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_RoutingWork_GrowsSubQuadraticallyWithEdgeCount()
    {
        // Arrange / Act: the largest work any one scope spent, for 160, 320 and 640 connectors
        var work160 = RoutingWorkOf(160);
        var work320 = RoutingWorkOf(320);
        var work640 = RoutingWorkOf(640);

        // Assert: work was spent, and each doubling costs well under the 8x of cubic growth
        Assert.True(work160 > 0, "No routing work was recorded.");
        Assert.True(work320 < 3.5 * work160, $"Doubling 160 -> 320 connectors multiplied the work by {(double)work320 / work160:F2}.");
        Assert.True(work640 < 3.5 * work320, $"Doubling 320 -> 640 connectors multiplied the work by {(double)work640 / work320:F2}.");
    }

    /// <summary>
    ///     Proves a package whose own scope is too large for the full routing effort is routed once at the
    ///     reduced effort and carries the reduction into the scopes inside it, without a warning of their
    ///     own: the whole drawing is one oversize diagram, so the effort is spent once.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_OversizeNestedPackage_RoutesEveryScopeOnce()
    {
        // Arrange: a package of sub-packages whose own scope holds 80 connectors, each sub-package small
        var context = new HierarchicalLayoutAlgorithm.OrderingRefinementContext();
        var graph = BuildNestedOversizePackage(SubPackages, 80);
        graph.Set<HierarchicalLayoutAlgorithm.OrderingRefinementContext?>(HierarchicalLayoutAlgorithm.RefinementContext, context);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert: every connector is drawn, orthogonally
        Assert.Equal(80 + (SubPackages * SubPackageEdges), CountLines(tree.Nodes));
        AssertAllLinesOrthogonal(tree.Nodes);

        // Every scope was routed with one pass: the oversize one and the ones inside it
        Assert.Equal(1, context.MaxRoutedPassesPerScope);

        // The sub-packages took the reduction over from the package that holds them...
        Assert.Equal(SubPackages, context.InheritedReducedScopes);

        // ...without a hint of their own: one warning for the scope that is really oversize
        var limited = tree.Warnings.Where(warning => warning.Contains("work limit", StringComparison.Ordinal)).ToList();
        Assert.Single(limited);
        Assert.Contains("scopes inside it", limited[0], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the nested oversize layout is deterministic: two layouts of the same hierarchy are
    ///     identical in geometry and warnings.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_OversizeNestedPackage_RepeatedRunsAreIdentical()
    {
        // Arrange / Act
        var first = LayoutEngine.Layout(BuildNestedOversizePackage(SubPackages, 80));
        var second = LayoutEngine.Layout(BuildNestedOversizePackage(SubPackages, 80));

        // Assert
        Assert.Equal(Fingerprint(first), Fingerprint(second));
        Assert.Equal(first.Warnings, second.Warnings);
    }

    /// <summary>
    ///     Proves a hierarchy whose scopes all stay under the weight limit is not reduced anywhere: no scope
    ///     inherits a reduction and no warning is raised.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_SmallNestedPackage_InheritsNoReduction()
    {
        // Arrange: the same hierarchy with only 6 connectors in the package scope
        var context = new HierarchicalLayoutAlgorithm.OrderingRefinementContext();
        var graph = BuildNestedOversizePackage(SubPackages, 6);
        graph.Set<HierarchicalLayoutAlgorithm.OrderingRefinementContext?>(HierarchicalLayoutAlgorithm.RefinementContext, context);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        Assert.Equal(0, context.InheritedReducedScopes);
        Assert.Empty(tree.Warnings);
    }

    /// <summary>
    ///     Proves the hint of a limited hierarchy names the scope that is really oversize: the package that
    ///     holds the connectors is the only scope it names, not the sub-packages that inherited the reduction.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_OversizeNestedPackage_NamesOnlyTheOversizeScope()
    {
        // Arrange
        var graph = BuildNestedOversizePackage(SubPackages, 80);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert: the hint names the scope of the sub-packages, not a sub-package's scope
        var hint = Assert.Single(tree.Warnings, warning => warning.Contains("work limit", StringComparison.Ordinal));
        Assert.Contains($"scope of {SubPackages} node(s)", hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves the routing speed-ups changed no coordinate: the five-connector synthetic package lays out
    ///     to exactly the geometry the unoptimized router produced.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_FiveCrossContainerEdges_MatchesGoldenGeometry()
    {
        // Arrange
        var graph = BuildSyntheticPackage(5);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert: the fingerprint equals the one captured from the unoptimized router
        Assert.Equal("A5A6846465EC", Fingerprint(tree));
    }

    /// <summary>
    ///     Proves the routing speed-ups changed no coordinate: the ten-connector synthetic package lays out
    ///     to exactly the geometry the unoptimized router produced.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_TenCrossContainerEdges_MatchesGoldenGeometry()
    {
        // Arrange
        var graph = BuildSyntheticPackage(10);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert: the fingerprint equals the one captured from the unoptimized router
        Assert.Equal("0B2A5365156A", Fingerprint(tree));
    }

    /// <summary>
    ///     Proves the layout is deterministic: two layouts of the same large package are identical.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_FortyCrossContainerEdges_RepeatedRunsAreIdentical()
    {
        // Arrange / Act: lay the same package out twice
        var first = Fingerprint(LayoutEngine.Layout(BuildSyntheticPackage(40)));
        var second = Fingerprint(LayoutEngine.Layout(BuildSyntheticPackage(40)));

        // Assert: identical geometry
        Assert.Equal(first, second);
    }

    /// <summary>
    ///     Proves a scope whose routing is cut off by a tiny work limit still draws every connector as an
    ///     orthogonal polyline, says so in the tree's warnings, and does so identically on every run.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_TinyWorkLimit_FallsBackToElbowRoutesAndWarns()
    {
        // Arrange: a package whose routing work exceeds a hard limit of a few thousand units
        var limits = new RoutingWorkLimits(Soft: 2_000, Hard: 6_000);

        // Act
        var first = LayoutEngine.Layout(BuildSyntheticPackage(10, limits));
        var second = LayoutEngine.Layout(BuildSyntheticPackage(10, limits));

        // Assert: every connector is drawn, orthogonal, with at least two waypoints
        Assert.Equal(10 + (Containers * (LeavesPerContainer - 1)), CountLines(first.Nodes));
        AssertAllLinesOrthogonal(first.Nodes);

        // The hint names the limited scope and the simplified routing
        Assert.Contains(first.Warnings, warning => warning.Contains("work limit", StringComparison.Ordinal) && warning.Contains("elbow", StringComparison.Ordinal));

        // The cut-off is a function of the work done, not of the clock
        Assert.Equal(Fingerprint(first), Fingerprint(second));
        Assert.Equal(first.Warnings, second.Warnings);
    }

    /// <summary>
    ///     Proves a limit with room to spare changes nothing: the ten-connector package lays out to the
    ///     golden geometry and carries no warning.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_GenerousWorkLimit_EqualsUnlimitedLayout()
    {
        // Arrange
        var limits = new RoutingWorkLimits(Soft: long.MaxValue / 4, Hard: long.MaxValue / 2);

        // Act
        var tree = LayoutEngine.Layout(BuildSyntheticPackage(10, limits));

        // Assert
        Assert.Equal("0B2A5365156A", Fingerprint(tree));
        Assert.Empty(tree.Warnings);
    }

    /// <summary>
    ///     Proves a scope the weight limit puts at the reduced effort is laid out, flagged by a warning, and
    ///     identically on every run.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_ScopeAtWeightLimit_RoutesAtReducedEffortAndWarns()
    {
        // Arrange: ten connectors over 36 nodes weigh 360, so a weight limit of 300 reduces the scope
        var limits = new RoutingWorkLimits(Soft: long.MaxValue / 4, Hard: long.MaxValue / 2, ReducedWeight: 300);

        // Act
        var first = LayoutEngine.Layout(BuildSyntheticPackage(10, limits));
        var second = LayoutEngine.Layout(BuildSyntheticPackage(10, limits));

        // Assert
        Assert.Equal(10 + (Containers * (LeavesPerContainer - 1)), CountLines(first.Nodes));
        AssertAllLinesOrthogonal(first.Nodes);
        Assert.Contains(first.Warnings, warning => warning.Contains("without the extra quality searches", StringComparison.Ordinal));
        Assert.Equal(Fingerprint(first), Fingerprint(second));
        Assert.NotEqual("0B2A5365156A", Fingerprint(first));
    }

    /// <summary>
    ///     Proves no scene of the layout-quality benchmark set is ever cut back by the default work limits:
    ///     the limits sit above the work the heaviest scene needs, so a future change that makes routing
    ///     dearer is noticed here rather than silently degrading the benchmark diagrams.
    /// </summary>
    /// <param name="index">The index of the benchmark scene.</param>
    [Theory]
    [MemberData(nameof(BenchmarkSceneIndexes))]
    public void LayoutEngine_Layout_BenchmarkScene_IsNeverLimitedByDefaultWorkLimits(int index)
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
    ///     checks, is never cut back by the default work limits.
    /// </summary>
    /// <param name="seed">The shuffle seed, or <see langword="null"/> for the fixed scattered order.</param>
    [Theory]
    [MemberData(nameof(GeneralViewSeeds))]
    public void LayoutEngine_Layout_GeneralViewUp_IsNeverLimitedByDefaultWorkLimits(int? seed)
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
    ///     cut off, followed by the canvas fit, still carries the warning.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_LimitedScope_WarningSurvivesAutoLayoutAndCanvasFit()
    {
        // Arrange: the same limited package, laid out with an unrelated isolated node beside it so the
        // automatic layout splits and repacks the pieces
        var graph = BuildSyntheticPackage(10, new RoutingWorkLimits(Soft: 2_000, Hard: 6_000));
        graph.AddNode("loner", 80, 40);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        Assert.Contains(tree.Warnings, warning => warning.Contains("work limit", StringComparison.Ordinal));
    }

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

    /// <summary>Counts the connector lines in <paramref name="nodes"/> and their descendants.</summary>
    /// <param name="nodes">The nodes to count in.</param>
    /// <returns>The number of lines.</returns>
    private static int CountLines(IReadOnlyList<LayoutNode> nodes)
    {
        var count = 0;
        foreach (var node in nodes)
        {
            if (node is LayoutLine)
            {
                count++;
            }
            else if (node is LayoutBox box)
            {
                count += CountLines(box.Children);
            }
        }

        return count;
    }
}
