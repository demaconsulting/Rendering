// <copyright file="InterconnectionLayoutEngineTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Layout.Engine;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Tests.Engine;

/// <summary>
///     Tests for <see cref="InterconnectionLayoutEngine"/> covering longest-path layering,
///     dummy-node routing for long edges, non-overlapping placement, and connector waypoints.
/// </summary>
public sealed class InterconnectionLayoutEngineTests
{
    /// <summary>
    ///     A null node list is rejected before the layered pipeline is assembled so callers get a
    ///     clear argument-contract failure rather than a deeper routing exception.
    /// </summary>
    [Fact]
    public void Place_NullNodes_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<LayerNode> nodes = null!;
        var edges = new List<LayerEdge>();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => InterconnectionLayoutEngine.Place(nodes, edges));
    }

    /// <summary>
    ///     A null edge list is rejected before any layering or routing work starts so the entry-point
    ///     contract stays explicit and deterministic.
    /// </summary>
    [Fact]
    public void Place_NullEdges_ThrowsArgumentNullException()
    {
        // Arrange
        var nodes = new List<LayerNode> { new(60, 40) };
        IReadOnlyList<LayerEdge> edges = null!;

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => InterconnectionLayoutEngine.Place(nodes, edges));
    }

    /// <summary>
    ///     A simple directed chain A(0)→B(1)→C(2) produces three monotonically increasing
    ///     layer indices: 0, 1, 2. Longest-path layering assigns each node to the length
    ///     of the longest path from any source.
    /// </summary>
    [Fact]
    public void Place_LinearChain_MonotonicLayerAssignment()
    {
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(1, 2) };

        var result = InterconnectionLayoutEngine.Place(nodes, edges);

        Assert.Equal(0, result.NodeLayers[0]);
        Assert.Equal(1, result.NodeLayers[1]);
        Assert.Equal(2, result.NodeLayers[2]);
    }

    /// <summary>
    ///     A span-1 edge between two equal-height nodes produces exactly two waypoints
    ///     (a straight horizontal path): source-right-port and target-left-port.
    ///     The Brandes-Köpf algorithm aligns the two nodes' ports at the same Y so no
    ///     bend points are needed.
    /// </summary>
    [Fact]
    public void Place_SingleEdge_ProducesStraightTwoWaypointPath()
    {
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40) };
        var edges = new List<LayerEdge> { new(0, 1) };

        var result = InterconnectionLayoutEngine.Place(nodes, edges);

        Assert.Single(result.ConnectorWaypoints);
        Assert.Equal(2, result.ConnectorWaypoints[0].Count);
    }

    /// <summary>
    ///     A simple 3-cycle is broken into an acyclic edge set by reversing exactly one back edge, and
    ///     the returned waypoint list stays index-aligned with that retained edge set.
    /// </summary>
    [Fact]
    public void Place_CyclicGraph_ReversesBackEdgeAndProducesWaypoint()
    {
        // Arrange: a 3-cycle requires one retained edge to be reversed to become acyclic.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(1, 2), new(2, 0) };
        var originalEdges = edges.ToHashSet();

        // Act
        var result = InterconnectionLayoutEngine.Place(nodes, edges);

        // Assert: the retained acyclic edge set stays index-aligned with the waypoint list.
        Assert.Equal(result.AcyclicEdges.Count, result.ConnectorWaypoints.Count);
        Assert.Equal(3, result.AcyclicEdges.Count);

        var reversedEntry = result.AcyclicEdges
            .Select((edge, index) => new { edge, index })
            .Single(entry =>
                !originalEdges.Contains(entry.edge) &&
                originalEdges.Any(original =>
                    original.Source == entry.edge.Target &&
                    original.Target == entry.edge.Source));

        Assert.True(reversedEntry.index >= 0);
        Assert.True(
            result.ConnectorWaypoints[reversedEntry.index].Count >= 2,
            "Expected the reversed retained edge to have a routed waypoint list.");
    }

    /// <summary>
    ///     The rect count always equals the number of input nodes — long edges use dummy-node
    ///     routing through intermediate layer gaps, not additional real boxes.
    ///     A diamond topology with an extra 0→3 long edge produces exactly four rects and
    ///     five waypoint lists.
    /// </summary>
    [Fact]
    public void Place_LongEdge_RectCountEqualsInputNodeCount()
    {
        // 0→1→3 and 0→2→3 place: 0 at layer 0, 1/2 at layer 1, 3 at layer 2.
        // Adding 0→3 creates a span-2 long edge routed via a dummy node.
        var nodes = new List<LayerNode>
        {
            new(80, 50),
            new(80, 50),
            new(80, 50),
            new(80, 50),
        };
        var edges = new List<LayerEdge>
        {
            new(0, 1),
            new(0, 2),
            new(1, 3),
            new(2, 3),
            new(0, 3),  // span-2: one dummy node in layer 1
        };

        var result = InterconnectionLayoutEngine.Place(nodes, edges);

        Assert.Equal(4, result.Rects.Count);
        Assert.Equal(5, result.ConnectorWaypoints.Count);
    }

    /// <summary>
    ///     A long edge (span &gt; 1) is routed via dummy nodes in intermediate layers.
    ///     A span-3 edge uses two dummies, producing at least four waypoints, all within
    ///     the diagram bounds.
    /// </summary>
    [Fact]
    public void Place_LongEdge_RoutesViaDummyNodesWithinBounds()
    {
        // Chain 0→1→2→3 sets layers 0,1,2,3; adding 0→3 spans 3 layers.
        var nodes = Enumerable.Repeat(new LayerNode(80, 50), 4).ToList();
        var edges = new List<LayerEdge>
        {
            new(0, 1),
            new(1, 2),
            new(2, 3),
            new(0, 3),  // span-3: two dummy nodes in layers 1 and 2
        };

        var result = InterconnectionLayoutEngine.Place(nodes, edges);

        var longEdgeWp = result.ConnectorWaypoints[3];

        // A span-3 long edge has 3 sub-edges, each contributing 0–2 bend points.
        // Minimum total: 2 (source port + target port); at least 4 when any sub-edge is non-straight.
        Assert.True(longEdgeWp.Count >= 4, $"Expected at least 4 waypoints for a span-3 long edge, got {longEdgeWp.Count}.");

        // All waypoints must lie within the diagram bounds.
        foreach (var wp in longEdgeWp)
        {
            Assert.InRange(wp.X, 0.0, result.TotalWidth);
            Assert.InRange(wp.Y, 0.0, result.TotalHeight);
        }
    }

    /// <summary>
    ///     The Workstation topology (7 parts, 8 connections) produces the exact layer
    ///     assignments that longest-path layering guarantees and all seven rects are
    ///     non-overlapping.
    /// </summary>
    [Fact]
    public void Place_WorkstationTopology_CorrectLayersAndNoOverlap()
    {
        // Part order matches SysML file: cpu=0 memory=1 graphics=2 storage=3 psu=4 network=5 board=6
        var nodes = Enumerable.Repeat(new LayerNode(130, 60), 7).ToList();
        var edges = new List<LayerEdge>
        {
            new(6, 0),  // board → cpu
            new(6, 1),  // board → memory
            new(6, 2),  // board → graphics
            new(6, 3),  // board → storage
            new(6, 5),  // board → network
            new(4, 6),  // psu   → board
            new(4, 2),  // psu   → graphics
            new(0, 1),  // cpu   → memory
        };

        var result = InterconnectionLayoutEngine.Place(nodes, edges);

        // Longest-path layers.
        Assert.Equal(0, result.NodeLayers[4]);  // psu:     no incoming
        Assert.Equal(1, result.NodeLayers[6]);  // board:   psu → board
        Assert.Equal(2, result.NodeLayers[0]);  // cpu:     board → cpu
        Assert.Equal(2, result.NodeLayers[2]);  // graphics: max(psu→2, board→2) = 2
        Assert.Equal(2, result.NodeLayers[3]);  // storage: board → storage
        Assert.Equal(2, result.NodeLayers[5]);  // network: board → network
        Assert.Equal(3, result.NodeLayers[1]);  // memory:  max(board→2+1, cpu→2+1) = 3

        // Output counts.
        Assert.Equal(7, result.Rects.Count);
        Assert.Equal(8, result.ConnectorWaypoints.Count);

        // No two rects overlap.
        for (var i = 0; i < result.Rects.Count; i++)
        {
            for (var j = i + 1; j < result.Rects.Count; j++)
            {
                Assert.False(
                    Overlaps(result.Rects[i], result.Rects[j]),
                    $"Rects {i} and {j} overlap: {result.Rects[i]} / {result.Rects[j]}");
            }
        }
    }

    /// <summary>
    ///     A downward flow direction transposes the layout: the same chain that lays out wider than it
    ///     is tall in the default rightward direction lays out taller than it is wide when placed
    ///     downward, with its boxes stacked in strictly increasing Y. This exercises the direction-aware
    ///     total-dimension computation.
    /// </summary>
    [Fact]
    public void Place_DownDirection_TransposesTotalsRelativeToRight()
    {
        var nodes = new List<LayerNode> { new(80, 40), new(80, 40), new(80, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(1, 2) };

        var right = InterconnectionLayoutEngine.Place(nodes, edges, LayoutDirection.Right);
        var down = InterconnectionLayoutEngine.Place(nodes, edges, LayoutDirection.Down);

        // Rightward flow is wider than tall; the downward flow transposes those dimensions.
        Assert.True(right.TotalWidth > right.TotalHeight);
        Assert.True(down.TotalHeight > down.TotalWidth);

        // The downward flow stacks the chain's boxes in strictly increasing Y.
        Assert.True(down.Rects[0].Y < down.Rects[1].Y);
        Assert.True(down.Rects[1].Y < down.Rects[2].Y);
    }

    /// <summary>
    ///     Omitting the optional direction argument preserves the established rightward default so
    ///     existing callers see identical geometry to an explicit <see cref="LayoutDirection.Right"/>.
    /// </summary>
    [Fact]
    public void Place_DefaultDirection_MatchesRightFlow()
    {
        // Arrange
        var nodes = new List<LayerNode> { new(80, 40), new(80, 40), new(80, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(1, 2) };

        // Act
        var @default = InterconnectionLayoutEngine.Place(nodes, edges);
        var right = InterconnectionLayoutEngine.Place(nodes, edges, LayoutDirection.Right);

        // Assert
        Assert.Equal(right.TotalWidth, @default.TotalWidth);
        Assert.Equal(right.TotalHeight, @default.TotalHeight);
        Assert.Equal(right.NodeLayers, @default.NodeLayers);
        Assert.Equal(right.Rects, @default.Rects);
        Assert.Equal(right.AcyclicEdges, @default.AcyclicEdges);
        Assert.Equal(right.ConnectorWaypoints.Count, @default.ConnectorWaypoints.Count);
        for (var i = 0; i < right.ConnectorWaypoints.Count; i++)
        {
            Assert.Equal(right.ConnectorWaypoints[i], @default.ConnectorWaypoints[i]);
        }
    }

    /// <summary>
    ///     Omitting the optional node-spacing argument preserves the engine's original fixed 30.0
    ///     constant so existing callers see identical geometry to before the parameter existed.
    /// </summary>
    [Fact]
    public void Place_DefaultNodeSpacing_MatchesExplicitEngineConstant()
    {
        // Arrange: a fan-out (one source, two same-sized children) stacks its children in the same
        // layer, so their gap is driven directly by node spacing.
        var nodes = new List<LayerNode> { new(80, 40), new(80, 40), new(80, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(0, 2) };

        // Act
        var @default = InterconnectionLayoutEngine.Place(nodes, edges, LayoutDirection.Right);
        var explicitConstant = InterconnectionLayoutEngine.Place(
            nodes, edges, LayoutDirection.Right, LayeredLayoutMetrics.NodeSpacing);

        // Assert
        Assert.Equal(explicitConstant.Rects, @default.Rects);
        Assert.Equal(explicitConstant.TotalWidth, @default.TotalWidth);
        Assert.Equal(explicitConstant.TotalHeight, @default.TotalHeight);
    }

    /// <summary>
    ///     A larger node-spacing value strictly widens the gap between two same-layer siblings — a
    ///     regression guard against the parameter being silently ignored.
    /// </summary>
    [Fact]
    public void Place_LargerNodeSpacing_WidensSiblingGap()
    {
        // Arrange
        var nodes = new List<LayerNode> { new(80, 40), new(80, 40), new(80, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(0, 2) };

        // Act
        var small = InterconnectionLayoutEngine.Place(nodes, edges, LayoutDirection.Right, nodeSpacing: 40.0);
        var large = InterconnectionLayoutEngine.Place(nodes, edges, LayoutDirection.Right, nodeSpacing: 100.0);

        // Assert: the gap between the two stacked children (rects 1 and 2) widens with spacing.
        var smallGap = small.Rects[2].Y - (small.Rects[1].Y + small.Rects[1].Height);
        var largeGap = large.Rects[2].Y - (large.Rects[1].Y + large.Rects[1].Height);
        Assert.True(largeGap > smallGap);
    }

    /// <summary>
    ///     Re-running the engine with identical input produces identical rects, totals, retained
    ///     acyclic edges, layer indices, and connector waypoints.
    /// </summary>
    [Fact]
    public void Place_RepeatedInvocation_ProducesIdenticalGeometry()
    {
        // Arrange
        var nodes = new List<LayerNode> { new(80, 40), new(80, 40), new(80, 40), new(80, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(0, 2), new(1, 3), new(2, 3), new(0, 3) };

        // Act
        var first = InterconnectionLayoutEngine.Place(nodes, edges);
        var second = InterconnectionLayoutEngine.Place(nodes, edges);

        // Assert
        Assert.Equal(first.TotalWidth, second.TotalWidth);
        Assert.Equal(first.TotalHeight, second.TotalHeight);
        Assert.Equal(first.NodeLayers, second.NodeLayers);
        Assert.Equal(first.Rects, second.Rects);
        Assert.Equal(first.AcyclicEdges, second.AcyclicEdges);
        Assert.Equal(first.ConnectorWaypoints.Count, second.ConnectorWaypoints.Count);
        for (var i = 0; i < first.ConnectorWaypoints.Count; i++)
        {
            Assert.Equal(first.ConnectorWaypoints[i], second.ConnectorWaypoints[i]);
        }
    }

    /// <summary>
    ///     A 5-node cycle with one node much taller than its neighbors forces a back edge to be
    ///     reversed and routed via <c>LayeredCorridorRouter</c>'s wrap-around approach. The node-rect-only
    ///     canvas extents used to under-count this routing's actual bend-point geometry, clipping the
    ///     connector; every waypoint must now lie within the reported canvas bounds.
    /// </summary>
    [Fact]
    public void Place_CyclicGraphWithTallNode_AllWaypointsWithinCanvasBounds()
    {
        // Arrange: a 5-cycle (0→1→2→3→4→0) plus an extra long edge (0→3) forces one edge to reverse
        // during cycle-breaking; node 2's much larger height stresses the cross-axis extent.
        var nodes = new List<LayerNode>
        {
            new(80, 40),
            new(80, 40),
            new(80, 120),
            new(80, 40),
            new(80, 40),
        };
        var edges = new List<LayerEdge>
        {
            new(0, 1),
            new(1, 2),
            new(2, 3),
            new(3, 4),
            new(4, 0),
            new(0, 3),
        };

        // Act
        var result = InterconnectionLayoutEngine.Place(nodes, edges);

        // Assert: every routed bend point must lie within the reported canvas dimensions.
        foreach (var wp in result.ConnectorWaypoints)
        {
            foreach (var p in wp)
            {
                Assert.InRange(p.X, 0.0, result.TotalWidth);
                Assert.InRange(p.Y, 0.0, result.TotalHeight);
            }
        }
    }

    private static bool Overlaps(Rect a, Rect b) =>
        a.X < b.X + b.Width &&
        b.X < a.X + a.Width &&
        a.Y < b.Y + b.Height &&
        b.Y < a.Y + a.Height;
}
