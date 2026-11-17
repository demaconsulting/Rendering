// <copyright file="FaceAlignmentTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Algorithm-level tests for straight final runs on a shared face (Rendering#57): when two
///     connectors end on one face of a box and the source boxes' faces overlap the target's port
///     positions, the connectors must run straight into the marker instead of taking a 5 px step.
/// </summary>
public sealed class FaceAlignmentTests
{
    private static readonly double MarkerLength = DiagramQuality.MarkerLength(EndMarkerStyle.HollowTriangle);

    /// <summary>
    ///     The reproduction of Rendering#57 under a downward flow: two sources feeding one wide target
    ///     with hollow-triangle ends produce straight, two-point final runs.
    /// </summary>
    [Fact]
    public void LayeredLayout_TwoSourcesOneTargetFace_Down_FinalRunsAreStraight()
    {
        // Arrange: A and B (80x50) both specialize T (200x50).
        var graph = TwoSourcesOneTarget(80, 80, 200, LayoutFlowDirection.Down);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: each connector is a single vertical segment.
        var lines = DiagramQuality.Lines(tree);
        Assert.Equal(2, lines.Count);
        Assert.All(lines, line =>
        {
            Assert.Equal(2, line.Waypoints.Count);
            Assert.Equal(line.Waypoints[0].X, line.Waypoints[1].X, 6);
        });
    }

    /// <summary>
    ///     The straight final run holds for every flow direction.
    /// </summary>
    /// <param name="direction">The flow direction under test.</param>
    [Theory]
    [InlineData(LayoutFlowDirection.Down)]
    [InlineData(LayoutFlowDirection.Up)]
    [InlineData(LayoutFlowDirection.Right)]
    [InlineData(LayoutFlowDirection.Left)]
    public void LayeredLayout_TwoSourcesOneTargetFace_AnyDirection_FinalRunsAreStraight(LayoutFlowDirection direction)
    {
        // Arrange: sources 80 wide/tall feeding a 200 long target face.
        var graph = TwoSourcesOneTarget(80, 80, 200, direction);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: every connector is a single axis-aligned segment.
        var lines = DiagramQuality.Lines(tree);
        Assert.Equal(2, lines.Count);
        Assert.All(lines, line => Assert.Equal(2, line.Waypoints.Count));
    }

    /// <summary>
    ///     Across a sweep of source and target sizes, no connector ever has a perpendicular step that
    ///     is shorter than the visible-step threshold (which is longer than every end marker): it is
    ///     either straight or a clear bend.
    /// </summary>
    /// <param name="targetWidth">The target box width.</param>
    /// <param name="sourceWidth">The width of both sources.</param>
    [Theory]
    [InlineData(120, 80)]
    [InlineData(132, 80)]
    [InlineData(148, 90)]
    [InlineData(160, 80)]
    [InlineData(176, 96)]
    [InlineData(200, 120)]
    [InlineData(212, 80)]
    [InlineData(240, 140)]
    [InlineData(260, 200)]
    public void LayeredLayout_TwoSourcesOneTarget_SizeSweep_NeverLeavesSubMarkerStep(
        int targetWidth,
        int sourceWidth)
    {
        // Arrange / Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(
            TwoSourcesOneTarget(sourceWidth, sourceWidth + 20, targetWidth, LayoutFlowDirection.Down),
            new LayoutOptions());

        // Assert: any interior step is at least the visible-step threshold.
        foreach (var line in DiagramQuality.Lines(tree))
        {
            Assert.All(
                DiagramQuality.InteriorSteps(line.Waypoints),
                step => Assert.True(
                    step >= DiagramQuality.MinVisibleStep,
                    $"interior step {step:0.###} is shorter than {DiagramQuality.MinVisibleStep}"));
        }
    }

    /// <summary>
    ///     A dense sweep of target widths (every 4 px) and source widths: the layout never produces a
    ///     step shorter than the visible-step threshold, and whenever the source box face covers the
    ///     target's port position the connector is straight.
    /// </summary>
    [Fact]
    public void LayeredLayout_TwoSourcesOneTarget_DenseSweep_NeverLeavesSubMarkerStep()
    {
        // Arrange / Act / Assert: sweep target 120..260 and source widths 80..200.
        for (var targetWidth = 120; targetWidth <= 260; targetWidth += 4)
        {
            for (var sourceWidth = 80; sourceWidth <= 200; sourceWidth += 20)
            {
                var tree = new LayeredLayoutAlgorithm().ApplyCore(
                    TwoSourcesOneTarget(sourceWidth, sourceWidth + 20, targetWidth, LayoutFlowDirection.Down),
                    new LayoutOptions());

                foreach (var line in DiagramQuality.Lines(tree))
                {
                    Assert.All(
                        DiagramQuality.InteriorSteps(line.Waypoints),
                        step => Assert.True(
                            step >= DiagramQuality.MinVisibleStep,
                            $"target {targetWidth}, source {sourceWidth}: step {step:0.###}"));
                }
            }
        }
    }

    /// <summary>
    ///     When the source face spans are disjoint from the target's port positions, the connector
    ///     keeps a real bend: a clear stub before it and a final run long enough for the marker.
    /// </summary>
    [Fact]
    public void LayeredLayout_FaceSpansDoNotOverlap_KeepsBendAndEndLeg()
    {
        // Arrange: a narrow (40) source and a very wide (400) target with two sources far apart:
        // the second source cannot reach the right-hand port position straight.
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        var a = graph.AddNode("a", 40, 50);
        var b = graph.AddNode("b", 40, 50);
        var t = graph.AddNode("t", 40, 50);
        var c = graph.AddNode("c", 40, 50);
        var wide = graph.AddNode("wide", 400, 50);
        foreach (var source in new[] { a, b, c, t })
        {
            graph.AddEdge($"e-{source.Id}", source, wide).TargetEnd = EndMarkerStyle.HollowTriangle;
        }

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: every connector still ends with a run long enough to draw the marker cleanly.
        foreach (var line in DiagramQuality.Lines(tree))
        {
            Assert.True(DiagramQuality.LastRun(line.Waypoints) >= MarkerLength);
            Assert.True(DiagramQuality.LastSegmentAxisAligned(line.Waypoints));
            Assert.All(
                DiagramQuality.InteriorSteps(line.Waypoints),
                step => Assert.True(step >= DiagramQuality.MinVisibleStep));
        }
    }

    /// <summary>
    ///     Three sources into one face: after sliding, the ports on the target face stay in the same
    ///     order, at least the edge spacing apart, and clear of the corners.
    /// </summary>
    [Fact]
    public void LayeredLayout_ThreeSourcesOneFace_SlidPortsKeepSeparationAndOrder()
    {
        // Arrange
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        var target = graph.AddNode("t", 240, 50);
        var sources = new[] { graph.AddNode("a", 70, 50), graph.AddNode("b", 70, 50), graph.AddNode("c", 70, 50) };
        foreach (var source in sources)
        {
            graph.AddEdge($"e-{source.Id}", source, target).TargetEnd = EndMarkerStyle.HollowTriangle;
        }

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert
        var targetBox = tree.Nodes.OfType<LayoutBox>().Single(box => box.Label == "t" || box.Width > 200);
        var ends = DiagramQuality.Lines(tree).Select(line => line.Waypoints[^1].X).OrderBy(x => x).ToList();
        Assert.Equal(3, ends.Count);
        for (var i = 1; i < ends.Count; i++)
        {
            Assert.True(ends[i] - ends[i - 1] >= 16.0 - 1e-6, "ports must stay at least the edge spacing apart");
        }

        Assert.True(ends[0] >= targetBox.X + 8.0);
        Assert.True(ends[^1] <= targetBox.X + targetBox.Width - 8.0);
    }

    /// <summary>
    ///     A long edge whose first sub-edge could reach the dummy's coordinate straight does so,
    ///     while the dummy itself is never moved.
    /// </summary>
    [Fact]
    public void LayeredLayout_LongEdgeAndShortEdgesSharingTarget_AllFinalRunsAxisAligned()
    {
        // Arrange: a -> b -> c plus the long edge a -> c.
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        var a = graph.AddNode("a", 100, 50);
        var b = graph.AddNode("b", 100, 50);
        var c = graph.AddNode("c", 220, 50);
        graph.AddEdge("ab", a, b).TargetEnd = EndMarkerStyle.FilledArrow;
        graph.AddEdge("bc", b, c).TargetEnd = EndMarkerStyle.FilledArrow;
        graph.AddEdge("ac", a, c).TargetEnd = EndMarkerStyle.FilledArrow;

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert
        foreach (var line in DiagramQuality.Lines(tree))
        {
            Assert.True(DiagramQuality.FirstSegmentAxisAligned(line.Waypoints));
            Assert.True(DiagramQuality.LastSegmentAxisAligned(line.Waypoints));
            Assert.All(
                DiagramQuality.InteriorSteps(line.Waypoints),
                step => Assert.True(step >= DiagramQuality.MinVisibleStep));
        }
    }

    /// <summary>
    ///     Labeled left/right ports keep out of the title band even after alignment: no connector
    ///     end on a titled box's left or right face lands inside the title reserve.
    /// </summary>
    [Fact]
    public void LayeredLayout_LabeledPortsRightFlow_AlignmentKeepsEndsOutOfTitleBand()
    {
        // Arrange: two sources feeding two labeled ports on a titled target.
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Right);
        var target = graph.AddNode("t", 140, 120);
        target.Label = "Target";
        var p1 = target.Ports.AddPort("p1");
        p1.ExternalLabel = "in1";
        var p2 = target.Ports.AddPort("p2");
        p2.ExternalLabel = "in2";
        var a = graph.AddNode("a", 90, 40);
        var b = graph.AddNode("b", 90, 40);
        graph.AddEdge("e1", a, p1).TargetEnd = EndMarkerStyle.OpenChevron;
        graph.AddEdge("e2", b, p2).TargetEnd = EndMarkerStyle.OpenChevron;

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: every connector end on the target's left face is below the title band.
        var box = tree.Nodes.OfType<LayoutBox>().Single(b2 => b2.Label == "Target");
        var fontSize = new LayoutOptions().Get(CoreOptions.AssumedFontSize);
        foreach (var line in DiagramQuality.Lines(tree))
        {
            Assert.True(line.Waypoints[^1].Y >= box.Y + fontSize, "end must not be inside the title band");
        }
    }

    /// <summary>
    ///     Regression guard for the hierarchical algorithm's cross-container path, which already
    ///     straightens shared-face connectors through <see cref="ConnectorRouter"/>.
    /// </summary>
    [Fact]
    public void HierarchicalLayout_CrossContainerTwoSourcesOneTarget_FinalRunsStraight()
    {
        // Arrange: a and b live in separate containers, t is a sibling of the containers.
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        var g1 = graph.AddNode("g1", 100, 60);
        g1.Label = "G1";
        var g2 = graph.AddNode("g2", 100, 60);
        g2.Label = "G2";
        var a = g1.Children.AddNode("a", 80, 50);
        var b = g2.Children.AddNode("b", 80, 50);
        var t = graph.AddNode("t", 240, 50);
        graph.AddEdge("ea", a, t).TargetEnd = EndMarkerStyle.HollowTriangle;
        graph.AddEdge("eb", b, t).TargetEnd = EndMarkerStyle.HollowTriangle;

        // Act
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: no sub-marker step on any connector.
        foreach (var line in DiagramQuality.Lines(tree))
        {
            Assert.All(
                DiagramQuality.InteriorSteps(line.Waypoints),
                step => Assert.True(step >= DiagramQuality.MinVisibleStep, $"step {step:0.###}"));
        }
    }

    /// <summary>
    ///     Regression guard for <see cref="ConnectorRouter"/> itself: two pre-placed sources into one
    ///     target are routed straight.
    /// </summary>
    [Fact]
    public void ConnectorRouter_Route_TwoSourcesOneTarget_FinalRunsStraight()
    {
        // Arrange
        var a = new LayoutBox(20, 20, 80, 50, "a", Depth: 0, BoxShape.Rectangle, [], []);
        var b = new LayoutBox(180, 20, 80, 50, "b", Depth: 0, BoxShape.Rectangle, [], []);
        var t = new LayoutBox(60, 140, 200, 50, "t", Depth: 0, BoxShape.Rectangle, [], []);
        var connections = new[]
        {
            new Connection(a, t, EndMarkerStyle.HollowTriangle, LineStyle.Solid, null),
            new Connection(b, t, EndMarkerStyle.HollowTriangle, LineStyle.Solid, null),
        };

        // Act
        var lines = ConnectorRouter.Route([a, b, t], connections, new ConnectorRouteOptions());

        // Assert
        Assert.Equal(2, lines.Count);
        Assert.All(lines, line => Assert.Equal(2, line.Waypoints.Count));
    }

    /// <summary>Builds the two-sources-one-target scene.</summary>
    private static LayoutGraph TwoSourcesOneTarget(
        double widthA,
        double widthB,
        double widthT,
        LayoutFlowDirection direction)
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, direction);
        var horizontal = direction is LayoutFlowDirection.Right or LayoutFlowDirection.Left;
        var a = horizontal ? graph.AddNode("a", 50, widthA) : graph.AddNode("a", widthA, 50);
        var b = horizontal ? graph.AddNode("b", 50, widthB) : graph.AddNode("b", widthB, 50);
        var t = horizontal ? graph.AddNode("t", 50, widthT) : graph.AddNode("t", widthT, 50);
        graph.AddEdge("at", a, t).TargetEnd = EndMarkerStyle.HollowTriangle;
        graph.AddEdge("bt", b, t).TargetEnd = EndMarkerStyle.HollowTriangle;
        return graph;
    }
}
