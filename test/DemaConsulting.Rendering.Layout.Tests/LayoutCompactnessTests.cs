// <copyright file="LayoutCompactnessTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Regression tests for the compactness half of Rendering#60: the benchmark scenes of
///     <see cref="LayoutQualityScenes"/> must never measure worse than main@219650c
///     (<see cref="LayoutQualityBaseline"/>) on connector crossings, empty space (diagram-quality
///     criterion B4), or block area ratio (criterion B5), must measure strictly better where a fix targets
///     them, and containers must hug their content.
/// </summary>
public sealed class LayoutCompactnessTests
{
    /// <summary>Slack, in measurement units, for comparing a floating point metric with its baseline.</summary>
    private const double Slack = 1e-6;

    /// <summary>The distance a container keeps between its border and its content, in logical pixels.</summary>
    private const double ContainerPadding = 12.0;

    /// <summary>Tolerance, in logical pixels, for a container margin.</summary>
    private const double MarginTolerance = 0.5;

    /// <summary>Every benchmark scene name.</summary>
    public static TheoryData<string> AllScenes()
    {
        var scenes = new TheoryData<string>();
        foreach (var scene in LayoutQualityScenes.Benchmarks)
        {
            scenes.Add(scene.Name);
        }

        return scenes;
    }

    /// <summary>The benchmark scenes a layout-quality fix must make strictly better.</summary>
    public static TheoryData<string> ImprovingScenes()
    {
        var scenes = new TheoryData<string>();
        foreach (var name in LayoutQualityBaseline.MustImprove)
        {
            scenes.Add(name);
        }

        return scenes;
    }

    /// <summary>
    ///     Proves no benchmark scene got worse than main@219650c on crossings, the largest empty square, the
    ///     block area ratio, or canvas area.
    /// </summary>
    /// <param name="scene">The benchmark scene name.</param>
    [Theory]
    [MemberData(nameof(AllScenes))]
    public void LayoutEngine_Layout_BenchmarkScene_NeverWorseThanBaseline(string scene)
    {
        // Arrange
        var baseline = LayoutQualityBaseline.Scenes[scene];

        // Act
        var report = LayoutQualityMetrics.Measure(LayoutEngine.Layout(Build(scene)));

        // Assert
        Assert.True(report.Crossings <= baseline.Crossings, $"{scene}: crossings {report.Crossings} > {baseline.Crossings}");
        Assert.True(report.B4Ratio <= baseline.B4Ratio + Slack, $"{scene}: B4 {report.B4Ratio} > {baseline.B4Ratio}");
        Assert.True(report.BlockAreaRatio >= baseline.BlockAreaRatio - Slack, $"{scene}: B5 {report.BlockAreaRatio} < {baseline.BlockAreaRatio}");
        Assert.True(report.CanvasArea <= baseline.CanvasArea + Slack, $"{scene}: area {report.CanvasArea} > {baseline.CanvasArea}");
    }

    /// <summary>
    ///     Proves no benchmark scene's connectors got visibly worse than main@219650c: the total connector
    ///     length and the number of bends stay within 10% of main's, the number of hairpins and the length of
    ///     close parallel runs (closer than 16 px for more than 20 px) stay at or below main's, with no exceptions.
    ///     A crossing is not worth a wrap around a container.
    /// </summary>
    /// <param name="scene">The benchmark scene name.</param>
    [Theory]
    [MemberData(nameof(AllScenes))]
    public void LayoutEngine_Layout_BenchmarkScene_RouteQualityNotWorseThanBaseline(string scene)
    {
        // Arrange
        var limit = LayoutQualityBaseline.RouteQualityLimits(scene);

        // Act
        var report = LayoutQualityMetrics.Measure(LayoutEngine.Layout(Build(scene)));

        // Assert
        Assert.True(report.TotalConnectorLength <= limit.TotalConnectorLength + Slack, $"{scene}: length {report.TotalConnectorLength:0.#} > {limit.TotalConnectorLength:0.#}");
        Assert.True(report.TotalBends <= limit.TotalBends, $"{scene}: bends {report.TotalBends} > {limit.TotalBends}");
        Assert.True(report.UTurns <= limit.UTurns, $"{scene}: hairpins {report.UTurns} > {limit.UTurns}");
        Assert.True(report.ParallelRunLength <= limit.ParallelRunLength + Slack, $"{scene}: close parallel runs {report.ParallelRunLength:0.#} > {limit.ParallelRunLength:0.#}");
    }

    /// <summary>
    ///     Proves no connector decoration overlaps the border of a container the connector enters, in every
    ///     benchmark scene: the gap between the border and the box the connector ends on is at least as long as
    ///     the decoration (tightening a container may not squeeze an arrowhead into the border).
    /// </summary>
    /// <param name="scene">The benchmark scene name.</param>
    [Theory]
    [MemberData(nameof(AllScenes))]
    public void LayoutEngine_Layout_BenchmarkScene_MarkersClearContainerBorders(string scene)
    {
        // Act
        var tree = LayoutEngine.Layout(Build(scene));

        // Assert
        LayoutGeometryAssert.AssertMarkersClearContainerBorders(tree, scene);
    }

    /// <summary>
    ///     Proves each scene a fix targets is strictly better than main@219650c: a smaller canvas together
    ///     with a smaller empty-square ratio (B4) or a larger block area ratio (B5).
    /// </summary>
    /// <param name="scene">The benchmark scene name.</param>
    [Theory]
    [MemberData(nameof(ImprovingScenes))]
    public void LayoutEngine_Layout_BenchmarkScene_StrictlyBetterThanBaseline(string scene)
    {
        // Arrange
        var baseline = LayoutQualityBaseline.Scenes[scene];

        // Act
        var report = LayoutQualityMetrics.Measure(LayoutEngine.Layout(Build(scene)));

        // Assert
        Assert.True(report.CanvasArea < baseline.CanvasArea - Slack, $"{scene}: area {report.CanvasArea} is not below {baseline.CanvasArea}");
        Assert.True(
            report.B4Ratio < baseline.B4Ratio - Slack || report.BlockAreaRatio > baseline.BlockAreaRatio + Slack,
            $"{scene}: neither B4 ({report.B4Ratio} vs {baseline.B4Ratio}) nor B5 ({report.BlockAreaRatio} vs {baseline.BlockAreaRatio}) improved");
    }

    /// <summary>
    ///     Proves every pair of connector ends on one box face is at least 16 px apart in every benchmark scene
    ///     (diagram-quality criteria C2 and C6: each connector has its own port), except where main@219650c
    ///     already shared a point on a full face (see <see cref="LayoutQualityBaseline.MinAnchorSeparation"/>).
    ///     NestedPackagesUp once had the «satisfy» connector leave RangeReq's top face at the very point of the
    ///     specialization, because a re-routing pass moved it onto that face at its naive anchor.
    /// </summary>
    /// <param name="scene">The benchmark scene name.</param>
    [Theory]
    [MemberData(nameof(AllScenes))]
    public void LayoutEngine_Layout_BenchmarkScene_AnchorsStaySeparatedOnSharedFace(string scene)
    {
        // Arrange
        var minimum = LayoutQualityBaseline.MinAnchorSeparation(scene);

        // Act
        var tree = LayoutEngine.Layout(Build(scene));

        // Assert
        var separation = LayoutQualityMetrics.MinAnchorSeparationOnSharedFace(tree);
        Assert.True(
            separation >= minimum - Slack,
            $"{scene}: two connector ends share a face {separation:0.##} px apart (< {minimum})");
    }

    /// <summary>
    ///     Proves the benchmark layouts keep the invariants whatever else changes: no overlapping siblings,
    ///     everything inside the canvas, and no label over a box or another label.
    /// </summary>
    /// <param name="scene">The benchmark scene name.</param>
    [Theory]
    [MemberData(nameof(AllScenes))]
    public void LayoutEngine_Layout_BenchmarkScene_GeometryStaysClean(string scene)
    {
        // Arrange
        var graph = Build(scene);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        LayoutGeometryAssert.AssertNoSiblingOverlap(tree);
        LayoutGeometryAssert.AssertWithinCanvas(tree);
    }

    /// <summary>
    ///     Proves a container is sized to its content plus one padding: its border keeps exactly 12 px from
    ///     the leftmost, rightmost and lowest child box or connector, and at least 12 px from the topmost
    ///     (the title band adds to the top), instead of the 32 px main@219650c left on every side.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [InlineData(LayoutFlowDirection.Right)]
    [InlineData(LayoutFlowDirection.Up)]
    public void LayoutEngine_Layout_Container_TightensToContentPlusPadding(LayoutFlowDirection direction)
    {
        // Arrange: unlabeled scenes, so the content is exactly the child boxes and connectors. NestedDepth is
        // not one of them: tightening its containers leaves a crossing the loose layout does not have, so its
        // scope keeps main@219650c's room (see HierarchicalLayoutAlgorithm.KeepsTightening).
        var graphs = new[]
        {
            LayoutQualityScenes.Repro2("hierarchical", direction, ["H", "C", "S"]),
            LayoutQualityScenes.Repro3("hierarchical", direction, ["H", "C", "S"]),
        };

        foreach (var graph in graphs)
        {
            // Act
            var tree = LayoutEngine.Layout(graph);

            // Assert: every container hugs its content
            var containers = LayoutQualityMetrics.AllBoxes(tree).Where(box => box.Children.Any()).ToList();
            Assert.NotEmpty(containers);
            foreach (var container in containers)
            {
                AssertHugsContent(container);
            }
        }
    }

    /// <summary>
    ///     Proves the <c>auto</c> layout of a tall connected chain beside isolated definitions needs a
    ///     smaller canvas than main@219650c, by packing the isolated definitions into the free space.
    /// </summary>
    [Fact]
    public void AutoLayoutAlgorithm_Apply_TallComponentPlusIsolated_CanvasAreaBelowBaseline()
    {
        // Arrange
        var baseline = LayoutQualityBaseline.Scenes["AutoTallPlusIsolated"];

        // Act
        var tree = LayoutEngine.Layout(LayoutQualityScenes.AutoTallPlusIsolated());

        // Assert
        Assert.True(LayoutQualityMetrics.CanvasArea(tree) < baseline.CanvasArea - Slack);
    }

    /// <summary>Builds a fresh graph of the named benchmark scene.</summary>
    private static LayoutGraph Build(string scene) =>
        LayoutQualityScenes.Benchmarks.Single(candidate => candidate.Name == scene).Build();

    /// <summary>Asserts a container keeps the container padding around its child boxes and connectors.</summary>
    private static void AssertHugsContent(LayoutBox container)
    {
        var left = double.PositiveInfinity;
        var top = double.PositiveInfinity;
        var right = double.NegativeInfinity;
        var bottom = double.NegativeInfinity;
        void Include(double x1, double y1, double x2, double y2)
        {
            left = Math.Min(left, x1);
            top = Math.Min(top, y1);
            right = Math.Max(right, x2);
            bottom = Math.Max(bottom, y2);
        }

        foreach (var child in container.Children)
        {
            switch (child)
            {
                case LayoutBox box:
                    Include(box.X, box.Y, box.X + box.Width, box.Y + box.Height);
                    break;
                case LayoutLine line:
                    foreach (var point in line.Waypoints)
                    {
                        Include(point.X, point.Y, point.X, point.Y);
                    }

                    break;
                default:
                    break;
            }
        }

        // A container a decorated connector enters keeps the room the decoration needs (an arrowhead is 15.4 px
        // long) between its border and the box the connector ends on; every other container keeps the padding.
        var padding = left - container.X;
        var arrowPadding = NotationMetrics.RenderedAlongLineLength(EndMarkerStyle.HollowTriangle, Themes.Light) + Themes.Light.CleanLegMargin;
        Assert.True(
            Math.Abs(padding - ContainerPadding) <= MarginTolerance || Math.Abs(padding - arrowPadding) <= MarginTolerance,
            $"the padding {padding:0.##} is neither {ContainerPadding} nor the {arrowPadding:0.##} an arrowhead needs");
        Assert.Equal(padding, (container.X + container.Width) - right, MarginTolerance);
        Assert.Equal(padding, (container.Y + container.Height) - bottom, MarginTolerance);
        Assert.True(top - container.Y >= padding - MarginTolerance, "the content starts above the container padding");
    }
}
