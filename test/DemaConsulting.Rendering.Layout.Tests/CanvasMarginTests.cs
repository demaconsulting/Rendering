// <copyright file="CanvasMarginTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Regression tests for the outer canvas margin of a final layout (Rendering#60 compactness): the canvas
///     is exactly the drawn content (boxes, connector waypoints, resolved labels, ports) plus one padding on
///     all four sides, in every flow direction: no empty ring of canvas around the diagram, no content
///     touching or crossing a canvas edge, no more room on one side than on the opposite one.
/// </summary>
public sealed class CanvasMarginTests
{
    /// <summary>The padding the layered engine keeps around its content, in logical pixels.</summary>
    private const double LayeredPadding = 20.0;

    /// <summary>The padding the containment algorithm keeps around its content, in logical pixels.</summary>
    private const double ContainmentPadding = 12.0;

    /// <summary>Every benchmark scene name.</summary>
    public static TheoryData<string> BenchmarkScenes()
    {
        var scenes = new TheoryData<string>();
        foreach (var scene in LayoutQualityScenes.Benchmarks)
        {
            scenes.Add(scene.Name);
        }

        return scenes;
    }

    /// <summary>Every scene of <see cref="DirectionScenes"/> in every flow direction.</summary>
    public static TheoryData<string, LayoutFlowDirection> DirectionSceneCases()
    {
        var cases = new TheoryData<string, LayoutFlowDirection>();
        foreach (var name in new[] { "FlatIsolated", "ContainerIsolated", "GeneralView", "RootFolders", "SatisfyVerify", "KeywordLabels" })
        {
            foreach (var direction in LayoutQualityScenes.AllDirections)
            {
                cases.Add(name, direction);
            }
        }

        return cases;
    }

    /// <summary>Every flow direction.</summary>
    public static TheoryData<LayoutFlowDirection> Directions()
    {
        var cases = new TheoryData<LayoutFlowDirection>();
        foreach (var direction in LayoutQualityScenes.AllDirections)
        {
            cases.Add(direction);
        }

        return cases;
    }

    /// <summary>Every flow direction and both root algorithms of the cross-hierarchy repro scenes.</summary>
    public static TheoryData<string, LayoutFlowDirection> RootAndDirection()
    {
        var cases = new TheoryData<string, LayoutFlowDirection>();
        foreach (var root in LayoutQualityScenes.RootAlgorithms)
        {
            foreach (var direction in LayoutQualityScenes.AllDirections)
            {
                cases.Add(root, direction);
            }
        }

        return cases;
    }

    /// <summary>Proves every benchmark scene's canvas is its content plus one padding on every side.</summary>
    /// <param name="scene">The benchmark scene name.</param>
    [Theory]
    [MemberData(nameof(BenchmarkScenes))]
    public void LayoutEngine_Layout_BenchmarkScene_CanvasHugsContent(string scene)
    {
        // Act
        var tree = LayoutEngine.Layout(LayoutQualityScenes.Benchmarks.Single(candidate => candidate.Name == scene).Build());

        // Assert
        LayoutGeometryAssert.AssertCanvasHugsContent(tree, LayeredPadding, name: scene);
    }

    /// <summary>
    ///     Proves the layouts of the General View shaped scenes (flat, nested, keyword labeled, with isolated
    ///     definitions) keep one padding on every side whatever the flow direction.
    /// </summary>
    /// <param name="scene">The <see cref="DirectionScenes"/> scene name.</param>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [MemberData(nameof(DirectionSceneCases))]
    public void LayoutEngine_Layout_DirectionScene_CanvasHugsContent(string scene, LayoutFlowDirection direction)
    {
        // Arrange
        var graph = scene switch
        {
            "FlatIsolated" => DirectionScenes.FlatIsolated(direction),
            "ContainerIsolated" => DirectionScenes.ContainerIsolated(direction),
            "GeneralView" => DirectionScenes.GeneralView(direction),
            "RootFolders" => DirectionScenes.RootFolders(direction),
            "SatisfyVerify" => DirectionScenes.SatisfyVerify(direction),
            _ => DirectionScenes.KeywordLabels(direction),
        };

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        LayoutGeometryAssert.AssertCanvasHugsContent(tree, LayeredPadding, name: $"{scene} {direction}");
    }

    /// <summary>
    ///     Proves the cross-hierarchy repro scenes (nested folders joined by connectors across the hierarchy)
    ///     keep one padding on every side for both root algorithms in every direction.
    /// </summary>
    /// <param name="root">The root algorithm id.</param>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [MemberData(nameof(RootAndDirection))]
    public void LayoutEngine_Layout_CrossHierarchyScenes_CanvasHugsContent(string root, LayoutFlowDirection direction)
    {
        // Arrange
        var graphs = new (string Name, LayoutGraph Graph)[]
        {
            ("Repro2", LayoutQualityScenes.Repro2(root, direction, ["C", "S", "H"])),
            ("Repro3", LayoutQualityScenes.Repro3(root, direction, ["H", "C", "S"])),
            ("NestedDepth", LayoutQualityScenes.NestedDepth(root, direction, ["C", "S", "H"])),
        };

        foreach (var (name, graph) in graphs)
        {
            // Act
            var tree = LayoutEngine.Layout(graph);

            // Assert
            LayoutGeometryAssert.AssertCanvasHugsContent(tree, LayeredPadding, name: $"{name} {root} {direction}");
        }
    }

    /// <summary>
    ///     Proves the labeled nested package scene and the General View scene keep one padding on every side
    ///     in every direction.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [MemberData(nameof(Directions))]
    public void LayoutEngine_Layout_NestedPackagesAndGeneralView_CanvasHugsContent(LayoutFlowDirection direction)
    {
        // Act
        var packages = LayoutEngine.Layout(LayoutQualityScenes.NestedPackages(direction));
        var general = LayoutEngine.Layout(LayoutQualityScenes.GeneralViewUp(direction));

        // Assert
        LayoutGeometryAssert.AssertCanvasHugsContent(packages, LayeredPadding, name: $"NestedPackages {direction}");
        LayoutGeometryAssert.AssertCanvasHugsContent(general, LayeredPadding, name: $"GeneralView {direction}");
    }

    /// <summary>
    ///     Proves a plain layered chain, a layered chain with a labeled edge, and a hierarchical scene with one
    ///     container and a connector into it keep one padding on every side in every direction: the reflection
    ///     that realizes the Left and Up flows must not leave the content against one canvas edge and a double
    ///     margin at the opposite edge.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [MemberData(nameof(Directions))]
    public void LayoutEngine_Layout_LayeredAndHierarchicalChains_CanvasHugsContent(LayoutFlowDirection direction)
    {
        // Arrange
        var chain = Chain("layered", direction, labeled: false);
        var labeled = Chain("layered", direction, labeled: true);
        var nested = Chain("hierarchical", direction, labeled: true, container: true);

        // Act
        var chainTree = LayoutEngine.Layout(chain);
        var labeledTree = LayoutEngine.Layout(labeled);
        var nestedTree = LayoutEngine.Layout(nested);

        // Assert
        LayoutGeometryAssert.AssertCanvasHugsContent(chainTree, LayeredPadding, name: $"chain {direction}");
        LayoutGeometryAssert.AssertCanvasHugsContent(labeledTree, LayeredPadding, name: $"labeled chain {direction}");
        LayoutGeometryAssert.AssertCanvasHugsContent(nestedTree, LayeredPadding, name: $"nested chain {direction}");
    }

    /// <summary>
    ///     Proves the <c>auto</c> layout of several connected clusters beside isolated definitions (the engine
    ///     packs the pieces) keeps exactly one outer padding on every side in every direction.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [MemberData(nameof(Directions))]
    public void AutoLayoutAlgorithm_Apply_ClustersPlusIsolated_CanvasHugsContent(LayoutFlowDirection direction)
    {
        // Arrange
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "auto");
        graph.Set(CoreOptions.Direction, direction);
        for (var cluster = 0; cluster < 3; cluster++)
        {
            var head = graph.AddNode($"h{cluster}", 100, 50);
            head.Label = $"H{cluster}";
            for (var leaf = 0; leaf < 2 + cluster; leaf++)
            {
                var child = graph.AddNode($"c{cluster}_{leaf}", 100, 50);
                graph.AddEdge($"e{cluster}_{leaf}", head, child);
            }
        }

        for (var i = 0; i < 4; i++)
        {
            graph.AddNode($"solo{i}", 100, 50);
        }

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        LayoutGeometryAssert.AssertCanvasHugsContent(tree, LayeredPadding, name: $"auto clusters {direction}");
    }

    /// <summary>
    ///     Proves the containment algorithm keeps its own padding (12 px, unchanged since main) on every side,
    ///     in every direction.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [MemberData(nameof(Directions))]
    public void ContainmentLayoutAlgorithm_Apply_Boxes_CanvasHugsContentWithItsOwnPadding(LayoutFlowDirection direction)
    {
        // Arrange
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "containment");
        graph.Set(CoreOptions.Direction, direction);
        for (var i = 0; i < 7; i++)
        {
            graph.AddNode($"box{i}", 90 + (10 * i), 40);
        }

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        LayoutGeometryAssert.AssertCanvasHugsContent(tree, ContainmentPadding, name: $"containment {direction}");
    }

    /// <summary>Builds a three-node chain, optionally with edge labels and the middle node as a container.</summary>
    private static LayoutGraph Chain(string algorithm, LayoutFlowDirection direction, bool labeled, bool container = false)
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, algorithm);
        graph.Set(CoreOptions.Direction, direction);
        var a = graph.AddNode("a", 100, 50);
        var b = graph.AddNode("b", 100, 50);
        var c = graph.AddNode("c", 100, 50);
        ILayoutConnectable middle = b;
        if (container)
        {
            middle = b.Children.AddNode("inner", 80, 40);
            b.Children.AddNode("other", 80, 40);
        }

        var first = graph.AddEdge("ab", a, middle);
        var second = graph.AddEdge("bc", middle, c);
        if (labeled)
        {
            first.Label = "satisfy";
            second.Label = "connection";
        }

        return graph;
    }
}
