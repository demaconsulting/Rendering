// <copyright file="BrandesKopfPlacerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Layout.Engine;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Tests.Engine.Layered;

/// <summary>
///     Tests for <see cref="BrandesKopfPlacer"/> covering assignment of finite coordinate arrays
///     and left-to-right column placement in layer order.
/// </summary>
public sealed class BrandesKopfPlacerTests
{
    /// <summary>
    ///     A chain (0-&gt;1-&gt;2) receives a finite X and Y for every augmented node and per-column
    ///     arrays sized to the number of layers.
    /// </summary>
    [Fact]
    public void BrandesKopfPlacer_Apply_ChainGraph_AssignsCoordinateArrays()
    {
        // Arrange / Act: place a three-node chain.
        var graph = BuildPlacedGraph(
            [new(60, 40), new(60, 40), new(60, 40)],
            [new(0, 1), new(1, 2)]);

        // Assert: coordinate arrays are sized correctly and finite.
        Assert.Equal(graph.AugNodes.Count, graph.AugX.Length);
        Assert.Equal(graph.AugNodes.Count, graph.AugY.Length);
        Assert.Equal(graph.Groups.Count, graph.ColumnX.Length);
        Assert.Equal(graph.Groups.Count, graph.MaxColWidth.Length);
        Assert.All(graph.AugX, x => Assert.True((!double.IsNaN(x) && !double.IsInfinity(x))));
        Assert.All(graph.AugY, y => Assert.True((!double.IsNaN(y) && !double.IsInfinity(y))));
    }

    /// <summary>
    ///     Layer columns are placed left to right, so each column's left edge is strictly greater
    ///     than the previous column's left edge.
    /// </summary>
    [Fact]
    public void BrandesKopfPlacer_Apply_ColumnsAreLeftToRightInLayerOrder()
    {
        // Arrange / Act: place a three-node chain (three layers).
        var graph = BuildPlacedGraph(
            [new(60, 40), new(60, 40), new(60, 40)],
            [new(0, 1), new(1, 2)]);

        // Assert: column left edges strictly increase.
        for (var l = 1; l < graph.ColumnX.Length; l++)
        {
            Assert.True(
                graph.ColumnX[l] > graph.ColumnX[l - 1],
                $"Column {l} left edge {graph.ColumnX[l]} is not right of column {l - 1} ({graph.ColumnX[l - 1]}).");
        }
    }

    /// <summary>
    ///     A symmetric fork (0-&gt;1, 0-&gt;2) places the source vertically centered between its two
    ///     targets, proving the four-pass balanced median placement rather than a naive single-pass
    ///     (e.g. top-aligned) placement, which would leave the source level with the first target.
    /// </summary>
    [Fact]
    public void BrandesKopfPlacer_Apply_SymmetricFork_CentersSourceBetweenTargets()
    {
        // Arrange / Act: one source fanning out to two equal-size targets in the next layer.
        var graph = BuildPlacedGraph(
            [new(60, 40), new(60, 40), new(60, 40)],
            [new(0, 1), new(0, 2)]);

        // The two targets occupy layer 1; the source occupies layer 0.
        var sourceY = graph.AugY[0];
        var targetMidpoint = (graph.AugY[1] + graph.AugY[2]) / 2.0;

        // Assert: the balanced placement centers the source between its two targets, and the two
        // targets are genuinely separated (so the centering is a non-trivial result).
        Assert.NotEqual(graph.AugY[1], graph.AugY[2]);
        Assert.Equal(targetMidpoint, sourceY, 3);
    }

    /// <summary>
    ///     A genuinely isolated node (zero edges) sharing layer 0 with an unrelated source whose single
    ///     edge targets the bottom-most node of a tall next layer (pulling the source's own Y position
    ///     far down to straighten that edge) ends up at exactly <see cref="LayeredLayoutMetrics.NodeSpacing"/>
    ///     from its neighbor, not an inflated gap — regression coverage for the isolated-node
    ///     layer-gap fix (Option B: <c>SqueezeTrailingIsolatedNodes</c>).
    /// </summary>
    [Fact]
    public void BrandesKopfPlacer_Apply_IsolatedNodeAfterPulledDownNeighbor_SqueezesGapToNodeSpacing()
    {
        // Arrange: layer 0 holds "entry" (index 0), a genuinely isolated node with zero edges (index 1),
        // and "farSource" (index 2); "entry" fans out to a five-node tall layer 1 (indices 3..7), each
        // feeding a shared "sink" (index 8), and "farSource" targets only the bottom-most of those five
        // (index 7), pulling farSource's own layer-0 Y position far down to straighten that edge.
        var nodes = new List<LayerNode>
        {
            new(120, 50), // 0: entry
            new(120, 50), // 1: isolated (zero edges)
            new(120, 50), // 2: farSource
            new(120, 50), // 3: hubA
            new(120, 50), // 4: hubB
            new(120, 50), // 5: hubC
            new(120, 50), // 6: hubD
            new(120, 50), // 7: hubE
            new(120, 60), // 8: sink
        };
        var edges = new List<LayerEdge>
        {
            new(0, 3), new(0, 4), new(0, 5), new(0, 6), new(0, 7),
            new(3, 8), new(4, 8), new(5, 8), new(6, 8), new(7, 8),
            new(2, 7),
        };
        var graph = BuildPlacedGraph(nodes, edges);

        // Layer 0 holds entry (0), isolated (1), and farSource (2); Option A clusters the isolated node
        // at the tail of the layer, so it is the last entry in graph.Groups[0] and the edge-bearing
        // neighbor immediately before it is the layer's new last edge-bearing node.
        var layer0 = graph.Groups[0];
        Assert.Equal(1, layer0[^1]);

        var neighbor = layer0[^2];
        var neighborBottom = graph.AugY[neighbor] + graph.AugNodes[neighbor].Height;
        var gap = graph.AugY[1] - neighborBottom;

        // Assert: the gap is exactly NodeSpacing, not an inflated multiple of it.
        Assert.Equal(LayeredLayoutMetrics.NodeSpacing, gap, 3);
    }

    /// <summary>
    ///     Adding a genuinely isolated node to an otherwise identical graph does not change the Y
    ///     coordinate of any edge-bearing (real or dummy) node — the isolated-node clustering and
    ///     gap-squeeze fix only ever moves nodes it identifies as isolated.
    /// </summary>
    [Fact]
    public void BrandesKopfPlacer_Apply_AddingIsolatedNode_LeavesEdgeBearingNodesUnchanged()
    {
        // Arrange: a baseline graph (a fork feeding a shared target) with no isolated node.
        var baseline = BuildPlacedGraph(
            [new(60, 40), new(60, 40), new(60, 40), new(60, 40)],
            [new(0, 1), new(0, 2), new(1, 3), new(2, 3)]);

        // The same graph, plus one genuinely isolated node (index 4, zero edges) appended to layer 0.
        var withIsolated = BuildPlacedGraph(
            [new(60, 40), new(60, 40), new(60, 40), new(60, 40), new(60, 40)],
            [new(0, 1), new(0, 2), new(1, 3), new(2, 3)]);

        // Act / Assert: every original (edge-bearing) node's Y coordinate is unchanged by the presence
        // of the isolated node.
        for (var i = 0; i < baseline.AugNodes.Count; i++)
        {
            Assert.Equal(baseline.AugY[i], withIsolated.AugY[i], 6);
        }
    }

    /// <summary>
    ///     A requested layer spacing above the routing floor sets the corridor between the first two columns
    ///     exactly, and the default (70) is the historical corridor.
    /// </summary>
    /// <param name="layerSpacing">The requested layer spacing.</param>
    [Theory]
    [InlineData(70.0)]
    [InlineData(100.0)]
    [InlineData(260.0)]
    public void BrandesKopfPlacer_Apply_LayerSpacingAboveFloor_SetsCorridorExactly(double layerSpacing)
    {
        // Arrange / Act: a two-node chain with the requested spacing
        var graph = BuildPlacedGraph([new(60, 40), new(60, 40)], [new(0, 1)], layerSpacing);

        // Assert
        Assert.Equal(layerSpacing, Corridor(graph, 0), 6);
    }

    /// <summary>A graph that never sets a layer spacing carries the historical 70 px corridor.</summary>
    [Fact]
    public void LayeredGraph_Constructor_LayerSpacing_DefaultsToHistoricalCorridor()
    {
        // Arrange / Act
        var graph = new LayeredGraph([], [], LayoutDirection.Right);

        // Assert
        Assert.Equal(70.0, graph.LayerSpacing, 6);
        Assert.Equal(LayeredLayoutMetrics.DefaultLayerSpacing, graph.LayerSpacing, 6);
    }

    /// <summary>
    ///     A layer spacing below the routing needs is a floor: the corridor equals the computed routing floor
    ///     (the slots of the fan plus the clearance, with the end-marker approach added when it fits the
    ///     historical corridor), never the requested value, for every marker style and fan width.
    /// </summary>
    /// <param name="style">The end marker of every connector.</param>
    /// <param name="edgeCount">The number of connectors fanning out of the source.</param>
    [Theory]
    [InlineData(EndMarkerStyle.None, 1)]
    [InlineData(EndMarkerStyle.OpenChevron, 1)]
    [InlineData(EndMarkerStyle.OpenChevron, 4)]
    [InlineData(EndMarkerStyle.HollowTriangle, 1)]
    [InlineData(EndMarkerStyle.HollowTriangle, 2)]
    [InlineData(EndMarkerStyle.FilledDiamond, 1)]
    [InlineData(EndMarkerStyle.FilledDiamond, 4)]
    [InlineData(EndMarkerStyle.FilledArrow, 3)]
    public void BrandesKopfPlacer_Apply_LayerSpacingBelowFloor_ClampsToRoutingFloor(EndMarkerStyle style, int edgeCount)
    {
        // Arrange: one source fanning out to edgeCount targets, every connector ending in the marker
        var approach = LayeredLayoutAlgorithm.ResolveEndApproach(style);
        var nodes = new List<LayerNode> { new(60, 40) };
        var edges = new List<LayerEdge>();
        for (var i = 1; i <= edgeCount; i++)
        {
            nodes.Add(new LayerNode(60, 40));
            edges.Add(new LayerEdge(0, i, TargetApproach: approach));
        }

        // Act
        var graph = BuildPlacedGraph(nodes, edges, 0.0);

        // Assert: the corridor is the slot floor, raised to the marker approach where that fits the
        // historical corridor; the slot pitch (16) and clearance (10) are never reduced
        var slots = (edgeCount - 1) * LayeredLayoutMetrics.EdgeSpacing;
        var slotFloor = (2.0 * LayeredLayoutMetrics.ConnectorClearance) + slots;
        var approachFloor = Math.Min(
            LayeredLayoutMetrics.ConnectorClearance + slots + Math.Max(LayeredLayoutMetrics.ConnectorClearance, approach),
            LayeredLayoutMetrics.DefaultLayerSpacing);
        var floor = Math.Max(slotFloor, approachFloor);
        Assert.Equal(floor, Corridor(graph, 0), 6);

        // The last run of the outermost slot is at least the standard clearance, and at least the marker
        // approach whenever the approach fits the historical corridor
        var lastRunOfOuterSlot = floor - LayeredLayoutMetrics.ConnectorClearance - slots;
        Assert.True(lastRunOfOuterSlot >= LayeredLayoutMetrics.ConnectorClearance - 1e-9);
        if (LayeredLayoutMetrics.ConnectorClearance + slots + approach <= LayeredLayoutMetrics.DefaultLayerSpacing)
        {
            Assert.True(lastRunOfOuterSlot >= approach - 1e-9, "the corridor is shorter than the end-marker approach");
        }
    }

    /// <summary>
    ///     End-label space is held inside the corridor: with a small layer spacing the corridor is the routing
    ///     floor plus the label space (not the spacing, and not 70 plus the labels).
    /// </summary>
    [Fact]
    public void BrandesKopfPlacer_Apply_EndLabelsWithSmallLayerSpacing_CorridorIsFloorPlusLabels()
    {
        // Arrange: one labeled connector
        var graph = BuildPlacedGraph([new(60, 40), new(60, 40)], [new(0, 1, SourceLabelAlong: 30, TargetLabelAlong: 20)], 0.0);

        // Assert: 2 * clearance + 30 + 20
        Assert.Equal(70.0, Corridor(graph, 0), 6);

        // With a spacing that exceeds that, the spacing itself is the corridor
        var wide = BuildPlacedGraph([new(60, 40), new(60, 40)], [new(0, 1, SourceLabelAlong: 30, TargetLabelAlong: 20)], 90.0);
        Assert.Equal(90.0, Corridor(wide, 0), 6);
    }

    /// <summary>Returns the corridor width between column <paramref name="layer"/> and the next column.</summary>
    private static double Corridor(LayeredGraph graph, int layer) =>
        graph.ColumnX[layer + 1] - graph.ColumnX[layer] - graph.MaxColWidth[layer];

    /// <summary>Runs the stages up to and including placement and returns the graph.</summary>
    /// <param name="nodes">Input nodes.</param>
    /// <param name="edges">Input edges.</param>
    /// <param name="layerSpacing">The requested layer spacing.</param>
    /// <returns>The graph after the Brandes-Köpf placement stage.</returns>
    private static LayeredGraph BuildPlacedGraph(
        List<LayerNode> nodes,
        List<LayerEdge> edges,
        double layerSpacing = LayeredLayoutMetrics.DefaultLayerSpacing)
    {
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right) { LayerSpacing = layerSpacing };
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        new LongEdgeSplitter().Apply(graph);
        new CrossingMinimizer().Apply(graph);
        new BrandesKopfPlacer().Apply(graph);
        return graph;
    }
}
