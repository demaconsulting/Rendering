// <copyright file="LayerSpacingTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Regression tests for Rendering#60: <see cref="CoreOptions.LayerSpacing"/> must change the gap between
///     adjacent layers (a floor: the connector routing may still need more), cascade per scope like every
///     other option, and an unset option must keep the engine's historical 70 px corridor.
/// </summary>
public sealed class LayerSpacingTests
{
    /// <summary>The engine's historical corridor width between layers, in logical pixels.</summary>
    private const double HistoricalCorridor = 70.0;

    /// <summary>Tolerance, in logical pixels, for a corridor width.</summary>
    private const double Tolerance = 0.5;

    /// <summary>The algorithms that lay out a flat graph through the layered engine.</summary>
    public static readonly string[] Algorithms = ["layered", "auto", "hierarchical"];

    /// <summary>Every algorithm and flow direction.</summary>
    public static TheoryData<string, LayoutFlowDirection> AlgorithmDirectionCells()
    {
        var cells = new TheoryData<string, LayoutFlowDirection>();
        foreach (var algorithm in Algorithms)
        {
            foreach (var direction in LayoutQualityScenes.AllDirections)
            {
                cells.Add(algorithm, direction);
            }
        }

        return cells;
    }

    /// <summary>Every algorithm and flow direction, with the layer spacing to request.</summary>
    public static TheoryData<string, LayoutFlowDirection, double> AlgorithmDirectionSpacingCells()
    {
        var cells = new TheoryData<string, LayoutFlowDirection, double>();
        foreach (var algorithm in Algorithms)
        {
            foreach (var direction in LayoutQualityScenes.AllDirections)
            {
                cells.Add(algorithm, direction, 100.0);
                cells.Add(algorithm, direction, 150.0);
            }
        }

        return cells;
    }

    /// <summary>
    ///     Proves an unset <see cref="CoreOptions.LayerSpacing"/> keeps the historical 70 px corridor between
    ///     the layers of the Rendering#60 repro, and its 310 by 170 (Right and Left) or 270 by 210 (Up and
    ///     Down) canvas.
    /// </summary>
    /// <param name="algorithm">The root algorithm id.</param>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [MemberData(nameof(AlgorithmDirectionCells))]
    public void LayoutEngine_Layout_LayerSpacingUnset_CorridorIs70(string algorithm, LayoutFlowDirection direction)
    {
        // Arrange
        var graph = LayoutQualityScenes.FanOutPair(algorithm, direction, null);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert: the corridor and the canvas are the historical ones
        Assert.Equal(HistoricalCorridor, LayerGap(tree, direction), Tolerance);
        var horizontal = direction is LayoutFlowDirection.Right or LayoutFlowDirection.Left;
        Assert.Equal(horizontal ? 310.0 : 270.0, tree.Width, Tolerance);
        Assert.Equal(horizontal ? 170.0 : 210.0, tree.Height, Tolerance);
    }

    /// <summary>
    ///     Proves <see cref="CoreOptions.LayerSpacing"/> is honored: a spacing above the routing minimum sets
    ///     the gap between the two layers exactly, and the canvas grows by the difference to 70.
    /// </summary>
    /// <param name="algorithm">The root algorithm id.</param>
    /// <param name="direction">The flow direction.</param>
    /// <param name="spacing">The requested layer spacing.</param>
    [Theory]
    [MemberData(nameof(AlgorithmDirectionSpacingCells))]
    public void LayoutEngine_Layout_LayerSpacing_HonoredAsCorridorWidth(string algorithm, LayoutFlowDirection direction, double spacing)
    {
        // Arrange
        var unset = LayoutEngine.Layout(LayoutQualityScenes.FanOutPair(algorithm, direction, null));
        var graph = LayoutQualityScenes.FanOutPair(algorithm, direction, spacing);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert: the gap is the option, and only the flow axis of the canvas grew
        Assert.Equal(spacing, LayerGap(tree, direction), Tolerance);
        var horizontal = direction is LayoutFlowDirection.Right or LayoutFlowDirection.Left;
        var grown = horizontal ? tree.Width - unset.Width : tree.Height - unset.Height;
        var other = horizontal ? tree.Height - unset.Height : tree.Width - unset.Width;
        Assert.Equal(spacing - HistoricalCorridor, grown, Tolerance);
        Assert.Equal(0.0, other, Tolerance);
    }

    /// <summary>
    ///     Proves a layer spacing below what the connector routing needs stays clean for every end-marker
    ///     style and fan width: the final run is never shorter than the marker (at the spacing of zero as at the
    ///     default 70), the arrowheads keep apart and nothing overlaps.
    /// </summary>
    /// <param name="style">The end marker.</param>
    /// <param name="edgeCount">The number of connectors fanning out of one source.</param>
    [Theory]
    [InlineData(EndMarkerStyle.OpenChevron, 1)]
    [InlineData(EndMarkerStyle.OpenChevron, 4)]
    [InlineData(EndMarkerStyle.HollowTriangle, 2)]
    [InlineData(EndMarkerStyle.HollowTriangle, 4)]
    [InlineData(EndMarkerStyle.FilledDiamond, 1)]
    [InlineData(EndMarkerStyle.FilledDiamond, 3)]
    [InlineData(EndMarkerStyle.FilledArrow, 4)]
    public void LayoutEngine_Layout_LayerSpacingZero_KeepsMarkerRunPerStyle(EndMarkerStyle style, int edgeCount)
    {
        // Arrange
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "layered");
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Right);
        graph.Set(CoreOptions.LayerSpacing, 0.0);
        var source = graph.AddNode("source", 100, 50);
        source.Label = "source";
        for (var i = 0; i < edgeCount; i++)
        {
            var target = graph.AddNode($"target{i}", 100, 50);
            target.Label = $"target{i}";
            graph.AddEdge($"e{i}", source, target).TargetEnd = style;
        }

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        LayoutGeometryAssert.AssertClean(tree);
        foreach (var line in LayoutQualityMetrics.AllLines(tree))
        {
            Assert.True(
                DiagramQuality.LastRun(line.Waypoints) >= DiagramQuality.MarkerLength(style) - Tolerance,
                $"the final run of a {style} connector is shorter than its end marker");
        }
    }

    /// <summary>
    ///     Proves the option reaches the boundary-port (recursive) path: a root holding a container with a
    ///     delegation port honors <see cref="CoreOptions.LayerSpacing"/> in every corridor between the external
    ///     box and the container, and keeps the 70 px corridors when it is unset.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [InlineData(LayoutFlowDirection.Right)]
    [InlineData(LayoutFlowDirection.Down)]
    public void LayoutEngine_Layout_LayerSpacingOnBoundaryPortScope_IsHonored(LayoutFlowDirection direction)
    {
        // Act
        var unset = LayoutEngine.Layout(BoundaryPortScene(direction, null));
        var wide = LayoutEngine.Layout(BoundaryPortScene(direction, 150.0));

        // Assert: the external edge crosses the container boundary through a hierarchy-crossing dummy layer,
        // so the gap holds two corridors, each at least the requested spacing
        Assert.Equal(2.0 * HistoricalCorridor, ExternalGap(unset, direction), Tolerance);
        Assert.Equal(2.0 * 150.0, ExternalGap(wide, direction), Tolerance);
    }

    /// <summary>
    ///     Proves a decorated edge into a boundary port keeps a final straight run at least as long as its end
    ///     marker even at a layer spacing of zero: the synthetic delegation and container-link segments of
    ///     the recursive path carry the marker's approach, so the corridor they cross is never shorter than the
    ///     marker (Rendering#60).
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [InlineData(LayoutFlowDirection.Right)]
    [InlineData(LayoutFlowDirection.Down)]
    public void LayoutEngine_Layout_LayerSpacingZeroOnBoundaryPortScope_KeepsMarkerRun(LayoutFlowDirection direction)
    {
        // Act
        var tree = LayoutEngine.Layout(BoundaryPortScene(direction, 0.0));

        // Assert: both the external approach (ending at the container face) and the internal delegation
        // (ending at the leaf) finish in a straight run at least the marker long
        var lines = LayoutQualityMetrics.AllLines(tree).ToList();
        Assert.NotEmpty(lines);
        foreach (var line in lines)
        {
            Assert.True(
                DiagramQuality.LastRun(line.Waypoints) >= DiagramQuality.MarkerLength(EndMarkerStyle.OpenChevron) - Tolerance,
                $"the final run {DiagramQuality.LastRun(line.Waypoints)} of a boundary-port connector is shorter than its end marker");
        }
    }

    /// <summary>
    ///     Proves an unset <see cref="CoreOptions.LayerSpacing"/> and an explicit 70 give identical geometry on
    ///     the boundary-port (recursive) path, so the new default is exactly the historical corridor there too.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_LayerSpacingBoundaryPortUnset_SameAsExplicit70()
    {
        // Act
        var unset = LayoutEngine.Layout(BoundaryPortScene(LayoutFlowDirection.Right, null));
        var explicit70 = LayoutEngine.Layout(BoundaryPortScene(LayoutFlowDirection.Right, 70.0));

        // Assert
        Assert.Equal(LayoutQualityMetrics.Fingerprint(unset), LayoutQualityMetrics.Fingerprint(explicit70));
    }

    /// <summary>
    ///     Proves a layer spacing below what the connector routing needs is a floor, not an override: with
    ///     spacings of zero, ten and twenty the connectors still end in a straight run longer than their
    ///     end marker, the arrowheads keep apart, and nothing overlaps or leaves the canvas.
    /// </summary>
    /// <param name="spacing">The requested (too small) layer spacing.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(10.0)]
    [InlineData(20.0)]
    public void LayoutEngine_Layout_LayerSpacingBelowFloor_StillRoutesCleanly(double spacing)
    {
        // Arrange: a four-edge fan with open chevrons, laid out to the right
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "layered");
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Right);
        graph.Set(CoreOptions.LayerSpacing, spacing);
        var source = graph.AddNode("source", 100, 50);
        source.Label = "source";
        for (var i = 0; i < 4; i++)
        {
            var target = graph.AddNode($"target{i}", 100, 50);
            target.Label = $"target{i}";
            graph.AddEdge($"e{i}", source, target).TargetEnd = EndMarkerStyle.OpenChevron;
        }

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        LayoutGeometryAssert.AssertClean(tree);
        foreach (var line in LayoutQualityMetrics.AllLines(tree))
        {
            Assert.True(
                DiagramQuality.LastRun(line.Waypoints) >= DiagramQuality.MarkerLength(EndMarkerStyle.OpenChevron) - Tolerance,
                "the final run of a connector is shorter than its end marker");
        }
    }

    /// <summary>
    ///     Proves labeled corridors are sized by what the labels and routing need, not by 70 plus the labels:
    ///     the labeled interconnection is narrower than main@219650c produced, and setting a smaller
    ///     <see cref="CoreOptions.LayerSpacing"/> never widens it.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_LayerSpacingWithEndLabels_CorridorIsMaxOfOptionAndNeeds()
    {
        // Arrange
        var baseline = LayoutQualityBaseline.Scenes["InterconnectionLabeledCorridors"];

        // Act
        var unset = LayoutEngine.Layout(LayoutQualityScenes.Interconnection(null));
        var tight = LayoutEngine.Layout(LayoutQualityScenes.Interconnection(40.0));

        // Assert: strictly narrower than main, and a smaller option is never wider than the default
        Assert.True(unset.Width < baseline.CanvasWidth - Tolerance, "the labeled corridors were not tightened");
        Assert.True(tight.Width <= unset.Width + Tolerance, "a smaller LayerSpacing widened the diagram");
    }

    /// <summary>
    ///     Proves <see cref="CoreOptions.LayerSpacing"/> set on a container's <c>Children</c> graph cascades to
    ///     the layers inside that container.
    /// </summary>
    [Fact]
    public void LayoutEngine_Layout_LayerSpacingOnContainerChildren_CascadesToInterior()
    {
        // Arrange: a folder holding a to b, with the spacing set on the folder's interior only
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "hierarchical");
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Right);
        var folder = graph.AddNode("folder", 10, 10);
        folder.Label = "folder";
        folder.Shape = BoxShape.Folder;
        folder.Children.Set(CoreOptions.Algorithm, "layered");
        folder.Children.Set(CoreOptions.LayerSpacing, 130.0);
        var a = folder.Children.AddNode("a", 100, 50);
        a.Label = "a";
        var b = folder.Children.AddNode("b", 100, 50);
        b.Label = "b";
        folder.Children.AddEdge("a_b", a, b).TargetEnd = EndMarkerStyle.OpenChevron;

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        Assert.Equal(130.0, LayerGap(tree, LayoutFlowDirection.Right), Tolerance);
    }

    /// <summary>
    ///     Proves a <see cref="CoreOptions.LayerSpacing"/> set on the graph takes precedence over one passed
    ///     in the algorithm options, and the options value applies when the graph sets none.
    /// </summary>
    [Fact]
    public void LayeredLayoutAlgorithm_ApplyCore_LayerSpacingOnGraph_TakesPrecedenceOverOptions()
    {
        // Arrange
        var options = LayoutOptions.ForAlgorithm("layered");
        options.Set(CoreOptions.LayerSpacing, 100.0);
        var fromOptions = LayoutQualityScenes.FanOutPair("layered", LayoutFlowDirection.Right, null);
        var fromGraph = LayoutQualityScenes.FanOutPair("layered", LayoutFlowDirection.Right, 150.0);

        // Act
        var optionTree = new LayeredLayoutAlgorithm().ApplyCore(fromOptions, options);
        var graphTree = new LayeredLayoutAlgorithm().ApplyCore(fromGraph, options);

        // Assert
        Assert.Equal(100.0, LayerGap(optionTree, LayoutFlowDirection.Right), Tolerance);
        Assert.Equal(150.0, LayerGap(graphTree, LayoutFlowDirection.Right), Tolerance);
    }

    /// <summary>
    ///     Builds a hierarchical root holding an external box <c>ext</c> joined to a container
    ///     <c>box</c> through its delegation port, which reaches a leaf inside the container.
    /// </summary>
    private static LayoutGraph BoundaryPortScene(LayoutFlowDirection direction, double? layerSpacing)
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "hierarchical");
        graph.Set(CoreOptions.Direction, direction);
        if (layerSpacing is { } spacing)
        {
            graph.Set(CoreOptions.LayerSpacing, spacing);
        }

        var external = graph.AddNode("ext", 100, 50);
        external.Label = "ext";
        var container = graph.AddNode("box", 10, 10);
        container.Label = "box";
        container.Shape = BoxShape.Folder;
        var port = container.Ports.AddPort("p");
        var leaf = container.Children.AddNode("leaf", 100, 50);
        leaf.Label = "leaf";
        graph.AddEdge("ext_p", external, port).TargetEnd = EndMarkerStyle.OpenChevron;
        container.Children.AddEdge("p_leaf", port, leaf).TargetEnd = EndMarkerStyle.OpenChevron;
        return graph;
    }

    /// <summary>
    ///     Measures the gap between the external box <c>ext</c> and the container <c>box</c> along the flow
    ///     axis of <paramref name="direction"/>.
    /// </summary>
    private static double ExternalGap(LayoutTree tree, LayoutFlowDirection direction)
    {
        var boxes = LayoutQualityMetrics.AllBoxes(tree);
        var external = boxes.Single(box => box.Label == "ext");
        var container = boxes.Single(box => box.Label == "box");
        return direction == LayoutFlowDirection.Right
            ? container.X - (external.X + external.Width)
            : container.Y - (external.Y + external.Height);
    }

    /// <summary>
    ///     Measures the gap between the layer holding box <c>a</c> and the layer holding box <c>b</c> along the
    ///     flow axis of <paramref name="direction"/>.
    /// </summary>
    private static double LayerGap(LayoutTree tree, LayoutFlowDirection direction)
    {
        var boxes = LayoutQualityMetrics.AllBoxes(tree);
        var a = boxes.Single(box => box.Label == "a");
        var b = boxes.Single(box => box.Label == "b");
        return direction switch
        {
            LayoutFlowDirection.Right => b.X - (a.X + a.Width),
            LayoutFlowDirection.Left => a.X - (b.X + b.Width),
            LayoutFlowDirection.Down => b.Y - (a.Y + a.Height),
            _ => a.Y - (b.Y + b.Height),
        };
    }
}
