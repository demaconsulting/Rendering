// <copyright file="ComponentPackerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Layout.Engine;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Tests.Engine.Layered;

/// <summary>
///     Tests for <see cref="ComponentPacker"/> covering connected-component detection, non-overlapping
///     packing of disconnected components, the single-component pass-through, the empty/null guards,
///     deterministic ordering, and translation of edge waypoints with their owning component.
/// </summary>
public sealed class ComponentPackerTests
{
    /// <summary>Logical pixel size used for every test node.</summary>
    private const double NodeWidth = 60.0;

    /// <summary>Logical pixel height used for every test node.</summary>
    private const double NodeHeight = 40.0;

    /// <summary>
    ///     A connected core (chain 0-&gt;1-&gt;2) plus a disconnected node keeps the core as one
    ///     component: the core's internal arrangement matches laying the core out on its own.
    /// </summary>
    [Fact]
    public void ComponentPacker_Apply_ConnectedCore_StaysOneComponent()
    {
        // Arrange: a connected chain {0,1,2} and an isolated node 3.
        var nodes = new List<LayerNode> { new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight) };
        var edges = new List<LayerEdge> { new(0, 1), new(1, 2) };
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);

        // Act: pack the disconnected graph.
        ComponentPacker.WithDefaultStages().Apply(graph);

        // Reference: lay the connected core out on its own and normalize to its bounding box.
        var coreNodes = new List<LayerNode> { new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight) };
        var coreEdges = new List<LayerEdge> { new(0, 1), new(1, 2) };
        var core = RunDefaultStages(coreNodes, coreEdges);
        var coreMinX = new[] { core.AugX[0], core.AugX[1], core.AugX[2] }.Min();
        var coreMinY = new[] { core.AugY[0], core.AugY[1], core.AugY[2] }.Min();

        // The packed core is component 0 (offset 0,0), so its coordinates are already normalized.
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(core.AugX[i] - coreMinX, graph.AugX[i], 6);
            Assert.Equal(core.AugY[i] - coreMinY, graph.AugY[i], 6);
        }
    }

    /// <summary>
    ///     Three disconnected nodes become three components placed in distinct, non-overlapping boxes.
    /// </summary>
    [Fact]
    public void ComponentPacker_Apply_DisconnectedSingletons_PackSeparately()
    {
        // Arrange: three isolated nodes, no edges.
        var nodes = new List<LayerNode> { new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight) };
        var graph = new LayeredGraph(nodes, [], LayoutDirection.Right);

        // Act.
        ComponentPacker.WithDefaultStages().Apply(graph);

        // Assert: no two node boxes overlap.
        for (var a = 0; a < nodes.Count; a++)
        {
            for (var b = a + 1; b < nodes.Count; b++)
            {
                Assert.False(
                    BoxesOverlap(graph, a, b),
                    $"nodes {a} and {b} should not overlap");
            }
        }
    }

    /// <summary>
    ///     A single connected component is a transparent pass-through: the packed output equals the
    ///     output of running the same inner stages directly on the graph.
    /// </summary>
    [Fact]
    public void ComponentPacker_Apply_SingleComponent_EqualsDefaultPipeline()
    {
        // Arrange: a diamond (one connected component).
        var nodes = new List<LayerNode> { new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight) };
        var edges = new List<LayerEdge> { new(0, 1), new(0, 2), new(1, 3), new(2, 3) };

        var packed = new LayeredGraph(CloneNodes(nodes), CloneEdges(edges), LayoutDirection.Right);
        var reference = RunDefaultStages(CloneNodes(nodes), CloneEdges(edges));

        // Act.
        ComponentPacker.WithDefaultStages().Apply(packed);

        // Assert: node coordinates and routed waypoints match the default pipeline exactly.
        for (var i = 0; i < nodes.Count; i++)
        {
            Assert.Equal(reference.AugX[i], packed.AugX[i]);
            Assert.Equal(reference.AugY[i], packed.AugY[i]);
        }

        Assert.Equal(reference.Waypoints.Count, packed.Waypoints.Count);
        for (var k = 0; k < reference.Waypoints.Count; k++)
        {
            Assert.Equal(reference.Waypoints[k].Count, packed.Waypoints[k].Count);
            for (var p = 0; p < reference.Waypoints[k].Count; p++)
            {
                Assert.Equal(reference.Waypoints[k][p].X, packed.Waypoints[k][p].X);
                Assert.Equal(reference.Waypoints[k][p].Y, packed.Waypoints[k][p].Y);
            }
        }
    }

    /// <summary>
    ///     Regression test for a bug where a component's per-connected-component child graph (built by
    ///     <see cref="ComponentPacker"/> when the input graph splits into 2+ connected components) was
    ///     constructed inline and silently forgot to copy the parent's <see cref="LayeredGraph.MergeParallelEdges"/>
    ///     (and <see cref="LayeredGraph.NodeSpacing"/>) override, so each component's child graph
    ///     silently reverted to the <see cref="LayeredGraph"/> defaults regardless of what the caller
    ///     requested on the parent. With <c>MergeParallelEdges = false</c> requested on the parent, two
    ///     parallel edges within one component used to collapse down to one acyclic edge inside that
    ///     component's child graph (as if merging had been requested), silently dropping the second
    ///     edge's route entirely and leaving it to a crude fallback line at the outer
    ///     <c>LayeredLayoutAlgorithm</c> layer. Proves both parallel edges now survive as their own
    ///     acyclic entries even when a second, entirely disconnected component forces the multi-component
    ///     packing path.
    /// </summary>
    [Fact]
    public void ComponentPacker_Apply_MergeParallelEdgesFalse_MultiComponentGraph_RetainsEveryParallelEdge()
    {
        // Arrange: two nodes {0,1} joined by two parallel edges, plus an entirely disconnected pair
        // {2,3} forming a second connected component, so the multi-component packing path runs.
        var nodes = new List<LayerNode>
        {
            new(NodeWidth, NodeHeight),
            new(NodeWidth, NodeHeight),
            new(NodeWidth, NodeHeight),
            new(NodeWidth, NodeHeight),
        };
        var edges = new List<LayerEdge> { new(0, 1), new(0, 1), new(2, 3) };
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right) { MergeParallelEdges = false };

        // Act.
        ComponentPacker.WithDefaultStages().Apply(graph);

        // Assert: both parallel edges between nodes 0 and 1 survived as their own acyclic entries
        // (original edge indices 0 and 1), each with a route computed — not collapsed to one.
        var parallelEdgeOriginalIndices = graph.AcyclicOriginalIndex
            .Where(idx => idx is 0 or 1)
            .OrderBy(idx => idx)
            .ToList();
        Assert.Equal([0, 1], parallelEdgeOriginalIndices);
    }

    /// <summary>An empty graph is laid out as a no-op without throwing.</summary>
    [Fact]
    public void ComponentPacker_Apply_EmptyGraph_IsNoOp()
    {
        // Arrange: a graph with no nodes or edges.
        var graph = new LayeredGraph([], [], LayoutDirection.Right);

        // Act.
        ComponentPacker.WithDefaultStages().Apply(graph);

        // Assert: nothing was produced.
        Assert.Empty(graph.AugX);
        Assert.Empty(graph.AugY);
        Assert.Empty(graph.Waypoints);
    }

    /// <summary>A null graph throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void ComponentPacker_Apply_NullGraph_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ComponentPacker.WithDefaultStages().Apply(null!));
    }

    /// <summary>
    ///     Composing <see cref="ComponentPacker"/> as an inner stage of a <see cref="LayeredLayoutPipeline"/>
    ///     for a Down/Up graph does not double-swap the input node axes. Regression test: before
    ///     <see cref="AxisTransform.NormalizeInputAxes"/> was made idempotent via
    ///     <see cref="LayeredGraph.InputAxesNormalized"/>, the pipeline's own normalization call followed by
    ///     <see cref="ComponentPacker.Apply"/>'s internal call swapped the same graph's node axes twice,
    ///     undoing the swap and handing the direction-agnostic stages the wrong along/cross extents.
    /// </summary>
    [Fact]
    public void ComponentPacker_Apply_ComposedInDownPipeline_DoesNotDoubleSwapAxes()
    {
        // Arrange: a tall/wide node whose Down-direction along-extent (height) differs sharply from its
        // Right-direction along-extent (width), so a double-swap would be detectable via node placement.
        var wideNode = new LayerNode(200.0, 20.0);
        var nodes = new List<LayerNode> { wideNode, wideNode };
        var edges = new List<LayerEdge> { new(0, 1) };

        // Reference: run ComponentPacker directly (single normalization) for a Down graph.
        var direct = new LayeredGraph(CloneNodes(nodes), CloneEdges(edges), LayoutDirection.Down);
        ComponentPacker.WithDefaultStages().Apply(direct);

        // Act: compose ComponentPacker as the sole stage of a pipeline, which normalizes the graph's axes
        // itself before running its stage list — exercising the double-normalization seam.
        var composed = new LayeredGraph(CloneNodes(nodes), CloneEdges(edges), LayoutDirection.Down);
        var pipeline = LayeredLayoutPipeline.Builder()
            .Direction(LayoutDirection.Down)
            .AddStage(ComponentPacker.WithDefaultStages())
            .Build();
        pipeline.Run(composed);

        // Assert: both paths normalize the axes exactly once, so the packed node layout matches.
        for (var i = 0; i < nodes.Count; i++)
        {
            Assert.Equal(direct.AugX[i], composed.AugX[i], 6);
            Assert.Equal(direct.AugY[i], composed.AugY[i], 6);
        }
    }

    /// <summary>
    ///     Repeated layouts of the same disconnected graph produce identical coordinates, confirming a
    ///     deterministic component order.
    /// </summary>
    [Fact]
    public void ComponentPacker_Apply_ComponentOrder_IsDeterministic()
    {
        // Arrange: two components — an edge {0,1} and an isolated node 2.
        static LayeredGraph Build()
        {
            var nodes = new List<LayerNode> { new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight) };
            var edges = new List<LayerEdge> { new(0, 1) };
            return new LayeredGraph(nodes, edges, LayoutDirection.Right);
        }

        var first = Build();
        var second = Build();

        // Act.
        ComponentPacker.WithDefaultStages().Apply(first);
        ComponentPacker.WithDefaultStages().Apply(second);

        // Assert: identical placements.
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(first.AugX[i], second.AugX[i]);
            Assert.Equal(first.AugY[i], second.AugY[i]);
        }
    }

    /// <summary>
    ///     Each edge's routed waypoints are translated with its component so the endpoints stay on the
    ///     boxes of the offset component, not at the local origin.
    /// </summary>
    [Fact]
    public void ComponentPacker_Apply_Waypoints_TranslatedWithComponent()
    {
        // Arrange: two components, each with one internal edge: {0->1} and {2->3}.
        var nodes = new List<LayerNode> { new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight) };
        var edges = new List<LayerEdge> { new(0, 1), new(2, 3) };
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);

        // Act.
        ComponentPacker.WithDefaultStages().Apply(graph);

        // Assert: every edge's first/last waypoint lies on its source/target box (within tolerance).
        const double eps = 3.0;
        AssertWaypointOnBox(graph, edge: 0, point: 0, node: 0, eps);
        AssertWaypointOnBox(graph, edge: 0, point: -1, node: 1, eps);
        AssertWaypointOnBox(graph, edge: 1, point: 0, node: 2, eps);
        AssertWaypointOnBox(graph, edge: 1, point: -1, node: 3, eps);
    }

    /// <summary>
    ///     A single connected component containing a parallel edge and a self-loop lays out without
    ///     throwing. The pipeline drops the self-loop and de-duplicates the parallel pair, so the
    ///     produced Waypoints are per-acyclic-edge and index-aligned with <see cref="LayeredGraph.Acyclic"/>
    ///     — the same contract as the default pipeline — and every input edge resolves by its endpoints.
    /// </summary>
    [Fact]
    public void ComponentPacker_Apply_SingleComponent_ParallelAndSelfEdges_ProducesAlignedWaypoints()
    {
        // Arrange: one connected component {0,1,2} with a parallel 0->1 pair, a 1->1 self-loop, and 1->2.
        var nodes = new List<LayerNode> { new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight) };
        var edges = new List<LayerEdge> { new(0, 1), new(0, 1), new(1, 1), new(1, 2) };
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);

        // Act: must not throw despite the parallel pair and self-loop.
        ComponentPacker.WithDefaultStages().Apply(graph);

        // Assert: Waypoints are index-aligned with the acyclic edge set (the routing contract).
        Assert.Equal(graph.Acyclic.Count, graph.Waypoints.Count);

        // Assert: the self-loop is dropped and the parallel pair collapses to a single acyclic edge.
        Assert.DoesNotContain(graph.Acyclic, e => e.Source == e.Target);
        Assert.Equal(2, graph.Acyclic.Count);
        Assert.Contains(graph.Acyclic, e => e.Source == 0 && e.Target == 1);
        Assert.Contains(graph.Acyclic, e => e.Source == 1 && e.Target == 2);

        // Assert: every non-self input edge (including the duplicate) resolves to a routed polyline whose
        // endpoints lie on the correct boxes.
        var routed = BuildRouted(graph);
        const double eps = 3.0;
        AssertEndpointsOnBoxes(graph, routed[(0, 1)], 0, 1, eps);
        AssertEndpointsOnBoxes(graph, routed[(1, 2)], 1, 2, eps);
    }

    /// <summary>
    ///     Two connected components, each containing a parallel edge and a self-loop, lay out without
    ///     throwing (the multi-component merge path). The merged graph exposes one Waypoint per acyclic
    ///     edge, index-aligned with <see cref="LayeredGraph.Acyclic"/>, and each edge's polyline is
    ///     translated onto its own component's (offset) boxes.
    /// </summary>
    [Fact]
    public void ComponentPacker_Apply_MultiComponent_ParallelAndSelfEdges_MergesAlignedWaypoints()
    {
        // Arrange: components {0,1} and {2,3}, each with a parallel pair and a self-loop.
        var nodes = new List<LayerNode> { new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight) };
        var edges = new List<LayerEdge> { new(0, 1), new(0, 1), new(0, 0), new(2, 3), new(2, 3), new(3, 3) };
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);

        // Act: must not throw despite parallel pairs and self-loops spanning two components.
        ComponentPacker.WithDefaultStages().Apply(graph);

        // Assert: Waypoints are index-aligned with the acyclic edge set (same contract as single-component).
        Assert.Equal(graph.Acyclic.Count, graph.Waypoints.Count);

        // Assert: self-loops dropped, parallel pairs collapsed — one acyclic edge per component.
        Assert.DoesNotContain(graph.Acyclic, e => e.Source == e.Target);
        Assert.Equal(2, graph.Acyclic.Count);
        Assert.Contains(graph.Acyclic, e => e.Source == 0 && e.Target == 1);
        Assert.Contains(graph.Acyclic, e => e.Source == 2 && e.Target == 3);

        // Assert: each edge's polyline endpoints lie on its own component's (offset) boxes.
        var routed = BuildRouted(graph);
        const double eps = 3.0;
        AssertEndpointsOnBoxes(graph, routed[(0, 1)], 0, 1, eps);
        AssertEndpointsOnBoxes(graph, routed[(2, 3)], 2, 3, eps);
    }

    /// <summary>
    ///     Two disconnected components, each containing a short back-edge cycle, propagate the parent
    ///     graph's <see cref="LayeredGraph.BackEdgeEntryApproach"/> to every packed child component.
    ///     Regression test: previously <c>LayoutComponent</c> built each child <see cref="LayeredGraph"/>
    ///     without copying the parent's <c>BackEdgeEntryApproach</c>, so a caller-configured clearance
    ///     silently reverted to the class default for any multi-component (packed) graph.
    /// </summary>
    [Fact]
    public void ComponentPacker_Apply_MultiComponent_PropagatesBackEdgeEntryApproach()
    {
        // Arrange: two 3-node triangle components, each with a cycle (0->1, 1->2, 2->0) so CycleBreaker
        // reverses the closing edge into a long back edge (skipping a layer via a dummy node), whose
        // routed corridor width is governed by BackEdgeEntryApproach.
        static LayeredGraph Build(double backEdgeEntryApproach)
        {
            var nodes = new List<LayerNode>
            {
                new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight),
                new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight), new(NodeWidth, NodeHeight),
            };
            var edges = new List<LayerEdge> { new(0, 1), new(1, 2), new(2, 0), new(3, 4), new(4, 5), new(5, 3) };
            return new LayeredGraph(nodes, edges, LayoutDirection.Right) { BackEdgeEntryApproach = backEdgeEntryApproach };
        }

        var small = Build(LayeredLayoutMetrics.ConnectorClearance);
        var large = Build(LayeredLayoutMetrics.ConnectorClearance * 10.0);

        // Act.
        ComponentPacker.WithDefaultStages().Apply(small);
        ComponentPacker.WithDefaultStages().Apply(large);

        // Assert: each reversed (back) edge's first routed bend point is pushed further out under the
        // larger BackEdgeEntryApproach, proving each packed child component honored the parent graph's
        // configured clearance instead of always reverting to the LayeredGraph default.
        Assert.True(MaxBackEdgeBendX(large) > MaxBackEdgeBendX(small));
    }

    /// <summary>Finds the furthest first-bend-point X coordinate among a graph's reversed (back) edges.</summary>
    /// <param name="graph">The laid-out graph.</param>
    /// <returns>The maximum X coordinate of the first bend point across all reversed edges.</returns>
    private static double MaxBackEdgeBendX(LayeredGraph graph)
    {
        var best = double.NegativeInfinity;
        for (var i = 0; i < graph.AcyclicReversed.Length; i++)
        {
            if (!graph.AcyclicReversed[i])
            {
                continue;
            }

            var waypoints = graph.Waypoints[i];
            if (waypoints.Count < 2)
            {
                continue;
            }

            best = Math.Max(best, waypoints[1].X);
        }

        return best;
    }

    /// <summary>Builds a <c>(source, target)</c> to polyline lookup over a graph's acyclic edge set.</summary>
    /// <param name="graph">The laid-out graph.</param>
    /// <returns>A dictionary keyed by each acyclic edge's endpoints.</returns>
    private static Dictionary<(int Source, int Target), IReadOnlyList<Point2D>> BuildRouted(LayeredGraph graph)
    {
        var routed = new Dictionary<(int Source, int Target), IReadOnlyList<Point2D>>();
        for (var k = 0; k < graph.Acyclic.Count; k++)
        {
            routed[(graph.Acyclic[k].Source, graph.Acyclic[k].Target)] = graph.Waypoints[k];
        }

        return routed;
    }

    /// <summary>Asserts that a polyline's first/last waypoints lie on its source/target boxes.</summary>
    /// <param name="graph">The laid-out graph.</param>
    /// <param name="waypoints">The routed polyline.</param>
    /// <param name="source">Source node index.</param>
    /// <param name="target">Target node index.</param>
    /// <param name="eps">Tolerance in logical pixels.</param>
    private static void AssertEndpointsOnBoxes(LayeredGraph graph, IReadOnlyList<Point2D> waypoints, int source, int target, double eps)
    {
        var first = waypoints[0];
        var last = waypoints[^1];
        Assert.InRange(first.X, graph.AugX[source] - eps, graph.AugX[source] + graph.Nodes[source].Width + eps);
        Assert.InRange(first.Y, graph.AugY[source] - eps, graph.AugY[source] + graph.Nodes[source].Height + eps);
        Assert.InRange(last.X, graph.AugX[target] - eps, graph.AugX[target] + graph.Nodes[target].Width + eps);
        Assert.InRange(last.Y, graph.AugY[target] - eps, graph.AugY[target] + graph.Nodes[target].Height + eps);
    }

    /// <summary>Runs the default ELK-layered stage sequence directly on a fresh graph.</summary>
    /// <param name="nodes">Input nodes.</param>
    /// <param name="edges">Input edges.</param>
    /// <returns>The graph after the full default stage sequence.</returns>
    private static LayeredGraph RunDefaultStages(List<LayerNode> nodes, List<LayerEdge> edges)
    {
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        new LongEdgeSplitter().Apply(graph);
        new CrossingMinimizer().Apply(graph);
        new BrandesKopfPlacer().Apply(graph);
        new PortDistributor().Apply(graph);
        new LayeredCorridorRouter().Apply(graph);
        new LongEdgeJoiner().Apply(graph);
        new AxisTransform().Apply(graph);
        return graph;
    }

    /// <summary>Returns a fresh copy of a node list.</summary>
    /// <param name="nodes">Nodes to copy.</param>
    /// <returns>A new list with the same node values.</returns>
    private static List<LayerNode> CloneNodes(List<LayerNode> nodes) => [.. nodes];

    /// <summary>Returns a fresh copy of an edge list.</summary>
    /// <param name="edges">Edges to copy.</param>
    /// <returns>A new list with the same edge values.</returns>
    private static List<LayerEdge> CloneEdges(List<LayerEdge> edges) => [.. edges];

    /// <summary>Determines whether the boxes of two real nodes overlap.</summary>
    /// <param name="graph">The laid-out graph.</param>
    /// <param name="a">First node index.</param>
    /// <param name="b">Second node index.</param>
    /// <returns><see langword="true"/> when the two boxes intersect.</returns>
    private static bool BoxesOverlap(LayeredGraph graph, int a, int b)
    {
        var ax = graph.AugX[a];
        var ay = graph.AugY[a];
        var bx = graph.AugX[b];
        var by = graph.AugY[b];
        var overlapX = ax < bx + graph.Nodes[b].Width && bx < ax + graph.Nodes[a].Width;
        var overlapY = ay < by + graph.Nodes[b].Height && by < ay + graph.Nodes[a].Height;
        return overlapX && overlapY;
    }

    /// <summary>Asserts that a given waypoint of an edge lies on a node's box, within a tolerance.</summary>
    /// <param name="graph">The laid-out graph.</param>
    /// <param name="edge">Edge index (original edge order).</param>
    /// <param name="point">Waypoint index; a negative value counts from the end.</param>
    /// <param name="node">The node whose box the waypoint should lie on.</param>
    /// <param name="eps">Tolerance in logical pixels.</param>
    private static void AssertWaypointOnBox(LayeredGraph graph, int edge, int point, int node, double eps)
    {
        var waypoints = graph.Waypoints[edge];
        var p = point < 0 ? waypoints[waypoints.Count + point] : waypoints[point];
        var x = graph.AugX[node];
        var y = graph.AugY[node];
        Assert.InRange(p.X, x - eps, x + graph.Nodes[node].Width + eps);
        Assert.InRange(p.Y, y - eps, y + graph.Nodes[node].Height + eps);
    }
}
