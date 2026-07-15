// <copyright file="ContainmentLayoutAlgorithmTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests for the bundled <see cref="ContainmentLayoutAlgorithm"/>, exercising the full
///     graph-to-placed-tree path: row packing of nodes plus routing of edges around them.
/// </summary>
public sealed class ContainmentLayoutAlgorithmTests
{
    /// <summary>
    ///     Proves that the algorithm advertises the stable "containment" identifier.
    /// </summary>
    [Fact]
    public void Id_IsContainment()
    {
        // Act / Assert
        Assert.Equal("containment", new ContainmentLayoutAlgorithm().Id);
    }

    /// <summary>
    ///     Proves that a node's <see cref="LayoutGraphNode.Shape"/>, <see cref="LayoutGraphNode.Keyword"/>,
    ///     <see cref="LayoutGraphNode.Compartments"/>, and optional shape-geometry hints flow through to
    ///     the packed <see cref="LayoutBox"/> unchanged, so routing and rendering can agree on the box's
    ///     real outline after packing.
    /// </summary>
    [Fact]
    public void Apply_NodeWithShapeKeywordAndCompartments_PropagatesToPackedBox()
    {
        // Arrange
        var graph = new LayoutGraph();
        var node = graph.AddNode("pkg", 120, 80);
        node.Label = "Powertrain";
        node.Shape = BoxShape.Folder;
        node.Keyword = "package";
        node.Compartments = [new LayoutCompartment(null, ["Engine", "Gearbox"])];
        node.RoundedCornerRadius = 10.0;
        node.FolderTabWidth = 76.0;
        node.FolderTabHeight = 24.0;

        // Act
        var tree = new ContainmentLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert
        var box = Assert.Single(tree.Nodes.OfType<LayoutBox>());
        Assert.Equal(BoxShape.Folder, box.Shape);
        Assert.Equal("package", box.Keyword);
        Assert.Equal(node.Compartments, box.Compartments);
        Assert.Equal(10.0, box.RoundedCornerRadius);
        Assert.Equal(76.0, box.FolderTabWidth);
        Assert.Equal(24.0, box.FolderTabHeight);
    }

    /// <summary>
    ///     Proves that the nodes are packed as non-overlapping boxes, in input order, entirely within
    ///     the returned canvas.
    /// </summary>
    [Fact]
    public void Apply_Graph_PacksNodesNonOverlappingInInputOrderWithinCanvas()
    {
        // Arrange: a set of labelled peer nodes with no edges
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 120, 40);
        var c = graph.AddNode("c", 60, 40);
        var d = graph.AddNode("d", 90, 40);
        a.Label = "A";
        b.Label = "B";
        c.Label = "C";
        d.Label = "D";

        // Act
        var tree = new ContainmentLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: one box per node, emitted in input order
        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        Assert.Equal(4, boxes.Count);
        Assert.Equal(["A", "B", "C", "D"], boxes.Select(box => box.Label));

        // Every box lies fully within the reported canvas.
        foreach (var box in boxes)
        {
            Assert.True(box.X >= 0);
            Assert.True(box.Y >= 0);
            Assert.True(box.X + box.Width <= tree.Width + 1e-9);
            Assert.True(box.Y + box.Height <= tree.Height + 1e-9);
        }

        // No two boxes overlap.
        for (var i = 0; i < boxes.Count; i++)
        {
            for (var j = i + 1; j < boxes.Count; j++)
            {
                Assert.False(Overlaps(boxes[i], boxes[j]), $"Boxes {i} and {j} overlap.");
            }
        }
    }

    /// <summary>
    ///     Proves that one connector is routed per input edge and that each carries the edge's target
    ///     end marker, line style, and label.
    /// </summary>
    [Fact]
    public void Apply_Graph_RoutesOneConnectorPerEdgeCarryingStyling()
    {
        // Arrange: three nodes joined by two styled edges
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        var c = graph.AddNode("c", 80, 40);
        var e1 = graph.AddEdge("e1", a, b);
        e1.TargetEnd = EndMarkerStyle.FilledArrow;
        e1.LineStyle = LineStyle.Dashed;
        e1.Label = "owns";
        var e2 = graph.AddEdge("e2", a, c);
        e2.TargetEnd = EndMarkerStyle.HollowDiamond;

        // Act
        var tree = new ContainmentLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: one line per edge, each carrying its styling and a routed polyline
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();
        Assert.Equal(2, lines.Count);
        Assert.All(lines, line => Assert.True(line.Waypoints.Count >= 2));

        Assert.Equal(EndMarkerStyle.FilledArrow, lines[0].TargetEnd);
        Assert.Equal(LineStyle.Dashed, lines[0].LineStyle);
        Assert.Equal("owns", lines[0].MidpointLabel);
        Assert.Equal(EndMarkerStyle.None, lines[0].SourceEnd);

        Assert.Equal(EndMarkerStyle.HollowDiamond, lines[1].TargetEnd);
    }

    /// <summary>
    ///     Proves that an edge whose endpoints are separated by an intervening box routes around that
    ///     box rather than through its interior.
    /// </summary>
    [Fact]
    public void Apply_EdgeCrossingInterveningBox_RoutesAroundIt()
    {
        // Arrange: three wide nodes packed onto a single row so the middle node sits squarely between
        // the first and third; the edge from the first to the third must steer around the middle box.
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 60, 240);
        graph.AddNode("mid", 60, 240);
        var b = graph.AddNode("b", 60, 240);
        graph.AddEdge("e", a, b);

        // Act
        var tree = new ContainmentLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: the three boxes packed onto one row, with the middle box between the endpoints
        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        Assert.Equal(3, boxes.Count);
        var interveningBox = boxes[1];
        Assert.Equal(boxes[0].Y, interveningBox.Y);
        Assert.Equal(interveningBox.Y, boxes[2].Y);
        Assert.True(boxes[0].X < interveningBox.X);
        Assert.True(interveningBox.X < boxes[2].X);

        // The single routed connector avoids the intervening box's interior.
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());
        var obstacle = new Rect(interveningBox.X, interveningBox.Y, interveningBox.Width, interveningBox.Height);
        for (var i = 0; i < line.Waypoints.Count - 1; i++)
        {
            Assert.False(
                SegmentCrossesRect(line.Waypoints[i], line.Waypoints[i + 1], obstacle),
                $"Segment {i} crosses the intervening box.");
        }
    }

    /// <summary>
    ///     Proves that an empty graph produces an empty, positively-sized canvas.
    /// </summary>
    [Fact]
    public void Apply_EmptyGraph_ReturnsEmptyCanvas()
    {
        // Act
        var tree = new ContainmentLayoutAlgorithm().ApplyCore(new LayoutGraph(), new LayoutOptions());

        // Assert
        Assert.Empty(tree.Nodes);
        Assert.True(tree.Width > 0);
        Assert.True(tree.Height > 0);
    }

    /// <summary>
    ///     Proves that an edge referencing a node outside the graph's top-level nodes is skipped.
    /// </summary>
    [Fact]
    public void Apply_EdgeReferencingOutOfGraphNode_IsSkipped()
    {
        // Arrange: a graph with two top-level nodes and one edge between them, plus a second edge whose
        // target is a descendant node that is not a top-level node of the graph.
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        graph.AddEdge("in", a, b);

        // A nested descendant node added to the LCA (root) graph as a cross-container edge endpoint.
        var inner = a.Children.AddNode("inner", 40, 20);
        graph.AddEdge("out", a, inner);

        // Act
        var tree = new ContainmentLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: only the in-graph edge is routed; the out-of-graph edge is skipped
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();
        Assert.Single(lines);
    }

    /// <summary>
    ///     Proves that a null graph argument is rejected.
    /// </summary>
    [Fact]
    public void Apply_NullGraph_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(
            () => new ContainmentLayoutAlgorithm().ApplyCore(null!, new LayoutOptions()));
    }

    /// <summary>
    ///     Proves that a null options argument is rejected.
    /// </summary>
    [Fact]
    public void Apply_NullOptions_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(
            () => new ContainmentLayoutAlgorithm().ApplyCore(new LayoutGraph(), null!));
    }

    /// <summary>
    ///     Proves that an explicit <see cref="CoreOptions.EdgeRouting"/> override carried on the graph
    ///     itself is honored (routing still succeeds) even when the supplied options declares no routing
    ///     style at all, mirroring how <see cref="LayeredLayoutAlgorithm"/> resolves
    ///     <see cref="CoreOptions.Direction"/> from the graph before falling back to the options.
    ///     Regression guard: previously this algorithm read <c>options.Get(CoreOptions.EdgeRouting)</c>
    ///     directly, silently ignoring any override carried on the graph itself — a genuine defect for a
    ///     standalone-callable algorithm, and for a scope inside <see cref="HierarchicalLayoutAlgorithm"/>
    ///     whose own graph carries the override. <see cref="EdgeRouting"/> declares only one member
    ///     (<see cref="EdgeRouting.Orthogonal"/>) today, so this cannot be an output-geometry-differing
    ///     test; it proves the graph-first resolution path is exercised without throwing and still
    ///     produces valid routed output.
    /// </summary>
    [Fact]
    public void Apply_EdgeRoutingOverrideOnGraphScope_IsHonored()
    {
        // Arrange: a graph with an explicit EdgeRouting override and no such override on the options.
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 60, 240);
        graph.AddNode("mid", 60, 240);
        var b = graph.AddNode("b", 60, 240);
        graph.AddEdge("e", a, b);
        graph.Set(CoreOptions.EdgeRouting, EdgeRouting.Orthogonal);

        // Act
        var tree = new ContainmentLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: the edge is still routed using the graph's own (orthogonal) style.
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());
        Assert.True(line.Waypoints.Count >= 2);
    }

    /// <summary>
    ///     Proves that many small wide boxes (a shape whose area-based content width would otherwise
    ///     under-estimate the right packing budget) wrap into more than one row, instead of a single
    ///     narrow column, thanks to the column-count-based width candidate.
    /// </summary>
    [Fact]
    public void Apply_ManySmallWideBoxes_PacksIntoMultipleColumns()
    {
        // Arrange: twelve small, wide boxes (160x40) — area alone would suggest a narrow content width,
        // but a column-count-based candidate should widen the budget enough for multiple columns.
        var graph = new LayoutGraph();
        for (var i = 0; i < 12; i++)
        {
            graph.AddNode($"n{i}", 160, 40).Label = $"N{i}";
        }

        // Act
        var tree = new ContainmentLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: the boxes span more than one distinct row (Y position) — i.e., multi-column packing —
        // rather than being forced into a single long column.
        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        Assert.Equal(12, boxes.Count);
        var distinctRows = boxes.Select(box => box.Y).Distinct().Count();
        Assert.True(distinctRows > 1, $"Expected multiple rows, but all {boxes.Count} boxes landed on {distinctRows} row(s).");

        var distinctColumns = boxes.Select(box => box.X).Distinct().Count();
        Assert.True(distinctColumns > 1, $"Expected multiple columns, but all {boxes.Count} boxes landed on {distinctColumns} column(s).");
    }

    /// <summary>
    ///     Proves the end-to-end containment path widens the horizontal gap between two same-row peer
    ///     boxes joined by a fan of parallel edges: the gap grows past the default <c>NodeSpacing</c> to
    ///     the connector-corridor width, giving each connector its own routing lane.
    /// </summary>
    [Fact]
    public void Apply_SameRowPeersWithParallelEdges_WidensGapPastNodeSpacing()
    {
        // Arrange: two tall, narrow peer boxes sized so the packer places them side by side, joined by
        // eight parallel edges. Tall/narrow keeps 2*width + NodeSpacing under the area-based content
        // width so both land on one row.
        var graph = new LayoutGraph();
        var left = graph.AddNode("left", 100, 218);
        var right = graph.AddNode("right", 100, 218);
        left.Label = "Left";
        right.Label = "Right";
        for (var i = 0; i < 8; i++)
        {
            graph.AddEdge($"e{i}", left, right);
        }

        // Act
        var tree = new ContainmentLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: the two boxes share a row and the gap between them equals the eight-connector corridor
        // width (2*10 + 7*16 = 132), comfortably wider than the 24px NodeSpacing default.
        var boxes = tree.Nodes.OfType<LayoutBox>().OrderBy(box => box.X).ToList();
        Assert.Equal(2, boxes.Count);
        Assert.Equal(boxes[0].Y, boxes[1].Y);
        var gap = boxes[1].X - (boxes[0].X + boxes[0].Width);
        Assert.Equal(132.0, gap);
        Assert.True(gap > 24.0, "Expected the fan to widen the gap past the 24px NodeSpacing default.");
    }

    /// <summary>
    ///     Proves the horizontal-only widening leaves a vertical stack byte-identical: a small Source box
    ///     stacked above a tall Target box (joined by nine parallel edges) lands at exactly the same box
    ///     positions as the same boxes with no edges at all, because the pair spans two rows and the
    ///     widening never touches the vertical gap.
    /// </summary>
    [Fact]
    public void Apply_VerticalStackWithParallelEdges_LeavesBoxPositionsUnchanged()
    {
        // Arrange: a small Source over a tall Target — sized so 2*width + NodeSpacing exceeds the
        // area-based content width, forcing the Target to wrap below the Source (a vertical stack).
        static LayoutGraph BuildGraph(int edgeCount)
        {
            var graph = new LayoutGraph();
            var source = graph.AddNode("source", 130, 50);
            var target = graph.AddNode("target", 130, 242);
            source.Label = "Source";
            target.Label = "Target";
            for (var i = 0; i < edgeCount; i++)
            {
                graph.AddEdge($"e{i}", source, target);
            }

            return graph;
        }

        // Act: lay out the stack with nine parallel edges and, as the baseline, with no edges at all.
        var withEdges = new ContainmentLayoutAlgorithm().ApplyCore(BuildGraph(9), new LayoutOptions());
        var withoutEdges = new ContainmentLayoutAlgorithm().ApplyCore(BuildGraph(0), new LayoutOptions());

        // Assert: the boxes stack (distinct rows) and every box position is byte-identical between the
        // two runs — the horizontal widening left the vertical gap and the stack untouched.
        var edgedBoxes = withEdges.Nodes.OfType<LayoutBox>().ToList();
        var plainBoxes = withoutEdges.Nodes.OfType<LayoutBox>().ToList();
        Assert.Equal(2, edgedBoxes.Count);
        Assert.True(edgedBoxes[1].Y > edgedBoxes[0].Y, "Expected the Target to wrap below the Source.");
        for (var i = 0; i < plainBoxes.Count; i++)
        {
            Assert.Equal(plainBoxes[i].X, edgedBoxes[i].X);
            Assert.Equal(plainBoxes[i].Y, edgedBoxes[i].Y);
            Assert.Equal(plainBoxes[i].Width, edgedBoxes[i].Width);
            Assert.Equal(plainBoxes[i].Height, edgedBoxes[i].Height);
        }
    }

    /// <summary>
    ///     Determines whether two boxes overlap with a positive-area intersection.
    /// </summary>
    private static bool Overlaps(LayoutBox a, LayoutBox b) =>
        a.X < b.X + b.Width &&
        b.X < a.X + a.Width &&
        a.Y < b.Y + b.Height &&
        b.Y < a.Y + a.Height;

    /// <summary>
    ///     Returns true when the axis-aligned segment passes through the strict interior of the rect.
    /// </summary>
    private static bool SegmentCrossesRect(Point2D a, Point2D b, Rect r)
    {
        if (Math.Abs(a.Y - b.Y) < 1e-6)
        {
            // Horizontal segment
            var y = a.Y;
            var xa = Math.Min(a.X, b.X);
            var xb = Math.Max(a.X, b.X);
            return r.Y < y && y < r.Y + r.Height &&
                   Math.Max(xa, r.X) < Math.Min(xb, r.X + r.Width);
        }

        // Vertical segment
        var x = a.X;
        var ya = Math.Min(a.Y, b.Y);
        var yb = Math.Max(a.Y, b.Y);
        return r.X < x && x < r.X + r.Width &&
               Math.Max(ya, r.Y) < Math.Min(yb, r.Y + r.Height);
    }
}
