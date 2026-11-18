// <copyright file="EndLabelLayoutSpaceTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout.Engine;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests that a laid-out diagram with role-name and multiplicity labels on both ends of every
///     connector leaves room for them: the placed label rectangles overlap no box, no other label, and no
///     connector segment other than the one they sit beside.
/// </summary>
public sealed class EndLabelLayoutSpaceTests
{
    /// <summary>Right and downward flows of a small ownership diagram keep every end label clear.</summary>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [InlineData(LayoutFlowDirection.Right)]
    [InlineData(LayoutFlowDirection.Down)]
    public void LayeredLayout_OwnershipDiagramWithEndLabels_LabelsOverlapNothing(LayoutFlowDirection direction)
    {
        // Arrange: one owner with three owned parts, each connection labeled at both ends.
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "layered");
        graph.Set(CoreOptions.Direction, direction);
        var owner = graph.AddNode("owner", 120, 60);
        owner.Label = "Vehicle";
        for (var i = 0; i < 3; i++)
        {
            var part = graph.AddNode($"p{i}", 120, 60);
            part.Label = $"Part{i}";
            var edge = graph.AddEdge($"e{i}", owner, part);
            edge.TargetEnd = EndMarkerStyle.FilledDiamond;
            edge.SourceEndLabel = new LineEndLabel("owner", "1");
            edge.TargetEndLabel = new LineEndLabel($"part{i}", "0..*");
        }

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        AssertNoEndLabelOverlaps(tree);
    }

    /// <summary>A chain with labels on every end keeps the labels clear of the boxes and neighbors.</summary>
    [Fact]
    public void LayeredLayout_ChainWithEndLabels_LabelsOverlapNothing()
    {
        // Arrange
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "layered");
        var previous = graph.AddNode("n0", 100, 50);
        previous.Label = "N0";
        for (var i = 1; i < 4; i++)
        {
            var next = graph.AddNode($"n{i}", 100, 50);
            next.Label = $"N{i}";
            var edge = graph.AddEdge($"e{i}", previous, next);
            edge.TargetEnd = EndMarkerStyle.OpenChevron;
            edge.SourceEndLabel = new LineEndLabel("src", "1");
            edge.TargetEndLabel = new LineEndLabel("dst", "0..*");
            previous = next;
        }

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        AssertNoEndLabelOverlaps(tree);
    }

    /// <summary>
    ///     End-label space requested on an edge is held by the corridor it crosses (the corridor is the larger
    ///     of the layer spacing and the routing floor plus that space) and pushes the first bend out by the
    ///     source-end share, so the labels fit on the straight end runs.
    /// </summary>
    [Fact]
    public void InterconnectionLayoutEngine_Place_EdgeLabelAlong_WidensCorridorAndPushesFirstBend()
    {
        // Arrange: a fan-out forces a bend in both connectors.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 120) };
        var plainEdges = new List<LayerEdge> { new(0, 1), new(0, 2) };
        var labeledEdges = new List<LayerEdge> { new(0, 1, SourceLabelAlong: 40, TargetLabelAlong: 25), new(0, 2) };

        // Act
        var plain = InterconnectionLayoutEngine.Place(nodes, plainEdges);
        var labeled = InterconnectionLayoutEngine.Place(nodes, labeledEdges);

        // Assert: the plain corridor is the 70 px layer spacing (the routing floor of two slots is 36) and the
        // labeled one holds the routing floor plus the requested 65 px, not 70 px plus 65 px ...
        var plainGap = plain.Rects[1].X - (plain.Rects[0].X + plain.Rects[0].Width);
        var labeledGap = labeled.Rects[1].X - (labeled.Rects[0].X + labeled.Rects[0].Width);
        Assert.Equal(70.0, plainGap, 6);
        Assert.Equal(36.0 + 65.0, labeledGap, 6);

        // ... and the first bend of every connector leaving that column moves out by the source share.
        var plainBend = plain.ConnectorWaypoints.Select(w => w.Where(p => p.X > plain.Rects[0].X + 60 + 1).Min(p => p.X)).Min();
        var labeledBend = labeled.ConnectorWaypoints.Select(w => w.Where(p => p.X > labeled.Rects[0].X + 60 + 1).Min(p => p.X)).Min();
        Assert.Equal(plainBend + 40, labeledBend, 6);
    }

    /// <summary>
    ///     A reversed (back) edge swaps its end-label reservations with its endpoints, so each reservation
    ///     stays with the node its label belongs to.
    /// </summary>
    [Fact]
    public void CycleBreaker_ReversedEdge_SwapsEndLabelReservations()
    {
        // Arrange: 0 -> 1 -> 0 makes the second edge a back edge.
        var graph = new LayeredGraph(
            [new LayerNode(60, 40), new LayerNode(60, 40)],
            [new LayerEdge(0, 1, 10, 20), new LayerEdge(1, 0, 30, 40)],
            LayoutDirection.Right)
        {
            MergeParallelEdges = false,
        };

        // Act
        new CycleBreaker().Apply(graph);

        // Assert
        var forward = graph.Acyclic[0];
        var reversed = graph.Acyclic[1];
        Assert.Equal((10.0, 20.0), (forward.SourceLabelAlong, forward.TargetLabelAlong));
        Assert.True(graph.AcyclicReversed[1]);
        Assert.Equal((0, 1), (reversed.Source, reversed.Target));
        Assert.Equal((40.0, 30.0), (reversed.SourceLabelAlong, reversed.TargetLabelAlong));
    }

    private static void AssertNoEndLabelOverlaps(LayoutTree tree)
    {
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();
        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        var placements = ConnectorLabelPlacer.PlaceEndLabels(
            lines,
            Themes.Light.FontSizeBody,
            boxes,
            NotationMetrics.MarkerScale(Themes.Light));
        Assert.NotEmpty(placements);

        for (var i = 0; i < placements.Count; i++)
        {
            var label = placements[i];

            foreach (var box in boxes)
            {
                Assert.False(
                    Overlaps(label, box.X, box.Y, box.X + box.Width, box.Y + box.Height),
                    $"label '{label.Text}' overlaps box '{box.Label}'");
            }

            for (var j = i + 1; j < placements.Count; j++)
            {
                var other = placements[j];
                Assert.False(
                    Overlaps(label, other.X - other.HalfWidth, other.Y - other.HalfHeight, other.X + other.HalfWidth, other.Y + other.HalfHeight),
                    $"label '{label.Text}' overlaps label '{other.Text}'");
            }

            foreach (var line in lines)
            {
                for (var s = 0; s + 1 < line.Waypoints.Count; s++)
                {
                    if (ReferenceEquals(line, label.Line) && IsEndSegment(line, label.End, s))
                    {
                        continue;
                    }

                    Assert.False(
                        SegmentCrosses(label, line.Waypoints[s], line.Waypoints[s + 1]),
                        $"label '{label.Text}' is crossed by a connector segment");
                }
            }
        }
    }

    private static bool IsEndSegment(LayoutLine line, LineEnd end, int segment) =>
        end == LineEnd.Source ? segment == 0 : segment == line.Waypoints.Count - 2;

    private static bool Overlaps(EndLabelPlacement label, double left, double top, double right, double bottom) =>
        label.X - label.HalfWidth < right && label.X + label.HalfWidth > left &&
        label.Y - label.HalfHeight < bottom && label.Y + label.HalfHeight > top;

    private static bool SegmentCrosses(EndLabelPlacement label, Point2D a, Point2D b)
    {
        var left = Math.Min(a.X, b.X);
        var right = Math.Max(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        var bottom = Math.Max(a.Y, b.Y);
        return label.X - label.HalfWidth <= right && label.X + label.HalfWidth >= left &&
               label.Y - label.HalfHeight <= bottom && label.Y + label.HalfHeight >= top;
    }
}
