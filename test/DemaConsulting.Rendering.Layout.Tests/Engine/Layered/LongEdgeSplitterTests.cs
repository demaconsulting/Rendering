// <copyright file="LongEdgeSplitterTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Layout.Engine;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Tests.Engine.Layered;

/// <summary>
///     Tests for <see cref="LongEdgeSplitter"/> covering that unit-span edges add no dummy nodes
///     and that a long edge inserts one dummy node per intermediate layer.
/// </summary>
public sealed class LongEdgeSplitterTests
{
    /// <summary>
    ///     A chain of unit-span edges (0-&gt;1-&gt;2) produces an augmented node list equal in
    ///     count to the input node list, with no dummy nodes.
    /// </summary>
    [Fact]
    public void LongEdgeSplitter_Apply_SpanOneEdge_AddsNoDummyNodes()
    {
        // Arrange: a three-node chain (all edges span one layer).
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(1, 2) };
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);

        // Act: run the prerequisite stages, then split long edges.
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        new LongEdgeSplitter().Apply(graph);

        // Assert: no dummy nodes were added.
        Assert.Equal(graph.N, graph.AugNodes.Count);
        Assert.DoesNotContain(graph.AugNodes, a => a.IsDummy);
    }

    /// <summary>
    ///     A span-three edge (0-&gt;3 over the chain 0-&gt;1-&gt;2-&gt;3) is split with one dummy
    ///     node in each of the two intermediate layers.
    /// </summary>
    [Fact]
    public void LongEdgeSplitter_Apply_LongEdge_InsertsDummyNodesPerIntermediateLayer()
    {
        // Arrange: a chain plus a span-three edge 0->3.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40), new(60, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(1, 2), new(2, 3), new(0, 3) };
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);

        // Act: run the prerequisite stages, then split long edges.
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        new LongEdgeSplitter().Apply(graph);

        // Assert: exactly two dummy nodes were inserted (layers 1 and 2).
        Assert.Equal(graph.N + 2, graph.AugNodes.Count);
        Assert.Equal(2, graph.AugNodes.Count(a => a.IsDummy));
    }

    /// <summary>
    ///     A pre-seeded hierarchy-crossing dummy (a real node carrying a non-null
    ///     <see cref="AugNode.Crossing"/> tag) is a zero-size terminal hop across a container boundary,
    ///     so long-edge splitting carries its tag forward unchanged and never rebuilds it into an
    ///     ordinary node — the consumer guard for the recursive pipeline's crossing dummies.
    /// </summary>
    [Fact]
    public void LongEdgeSplitter_Apply_CrossingTaggedNode_PreservesTagAndIsNotSplit()
    {
        // Arrange: a two-node graph (feeder -> crossing dummy) whose target node is pre-seeded as a
        // hierarchy-crossing dummy, exactly as the recursive pipeline tags a boundary-crossing node.
        var port = new LayoutGraph().AddNode("Container", 10, 10).Ports.AddPort("p");
        var nodes = new List<LayerNode> { new(60, 40), new(0, 0, RealWidth: 0, RealHeight: 0) };
        var edges = new List<LayerEdge> { new(0, 1) };
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        graph.AugNodes =
        [
            new AugNode(nodes[0].Width, nodes[0].Height, graph.NodeLayers[0]),
            new AugNode(0, 0, graph.NodeLayers[1], Crossing: new HierarchyCrossing(port, HierarchyCrossingFace.Internal)),
        ];

        // Act: split long edges with the crossing tag already present.
        new LongEdgeSplitter().Apply(graph);

        // Assert: the crossing tag survived the augmented-node rebuild unchanged, and the tagged node was
        // not turned into a long-edge dummy nor duplicated (no split of the terminal crossing hop).
        Assert.NotNull(graph.AugNodes[1].Crossing);
        Assert.Equal(HierarchyCrossingFace.Internal, graph.AugNodes[1].Crossing!.Value.Face);
        Assert.Same(port, graph.AugNodes[1].Crossing!.Value.Port);
        Assert.False(graph.AugNodes[1].IsDummy);
        Assert.DoesNotContain(graph.AugNodes, a => a.IsDummy);
    }
}
