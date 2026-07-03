// <copyright file="PortDistributorTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Layout.Engine;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Tests.Engine.Layered;

/// <summary>
///     Tests for <see cref="PortDistributor"/> covering that ports lie within node faces and that a
///     source and target port Y is recorded for every augmented sub-edge.
/// </summary>
public sealed class PortDistributorTests
{
    /// <summary>
    ///     A single edge's source and target ports lie within the faces of their respective nodes.
    /// </summary>
    [Fact]
    public void PortDistributor_Apply_SingleEdge_PortsLieWithinNodeFaces()
    {
        // Arrange / Act: distribute ports for a single 0->1 edge.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40) };
        var graph = BuildPortedGraph(nodes, new List<LayerEdge> { new(0, 1) });

        var src = graph.AugEdges[0].Source;
        var tgt = graph.AugEdges[0].Target;

        // Assert: the source port is on the source node's face and the target port on the target's.
        Assert.InRange(graph.AugPortYSrc[0], graph.AugY[src], graph.AugY[src] + nodes[src].Height);
        Assert.InRange(graph.AugPortYTgt[0], graph.AugY[tgt], graph.AugY[tgt] + nodes[tgt].Height);
    }

    /// <summary>
    ///     A diamond graph yields one source port Y and one target port Y per augmented sub-edge,
    ///     each a finite value.
    /// </summary>
    [Fact]
    public void PortDistributor_Apply_AssignsPortYForEverySubEdge()
    {
        // Arrange / Act: distribute ports for a four-node diamond.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40), new(60, 40) };
        var graph = BuildPortedGraph(nodes, new List<LayerEdge> { new(0, 1), new(0, 2), new(1, 3), new(2, 3) });

        // Assert: one source and one target port per sub-edge.
        Assert.Equal(graph.AugEdges.Count, graph.AugPortYSrc.Length);
        Assert.Equal(graph.AugEdges.Count, graph.AugPortYTgt.Length);
        Assert.All(graph.AugPortYSrc, y => Assert.True(double.IsFinite(y)));
        Assert.All(graph.AugPortYTgt, y => Assert.True(double.IsFinite(y)));
    }

    /// <summary>
    ///     A node whose face is smaller than twice the connector clearance still yields ports that lie
    ///     within the node face, without throwing. Regression guard for the inverted-clamp crash: a
    ///     10-tall face cannot hold the full clearance inset on both edges, which previously drove
    ///     <c>Math.Clamp</c> with <c>min &gt; max</c>.
    /// </summary>
    [Fact]
    public void PortDistributor_Apply_SmallFace_PortsLieWithinNodeFacesWithoutThrowing()
    {
        // Arrange / Act: distribute ports for a fan-out from a face far shorter than the clearance band.
        var nodes = new List<LayerNode> { new(60, 10), new(60, 10), new(60, 10) };
        var graph = BuildPortedGraph(nodes, new List<LayerEdge> { new(0, 1), new(0, 2) });

        // Assert: every recorded port is finite and sits within its owning node's face.
        Assert.Equal(graph.AugEdges.Count, graph.AugPortYSrc.Length);
        Assert.All(graph.AugPortYSrc, y => Assert.True(double.IsFinite(y)));
        Assert.All(graph.AugPortYTgt, y => Assert.True(double.IsFinite(y)));
        for (var ei = 0; ei < graph.AugEdges.Count; ei++)
        {
            var src = graph.AugEdges[ei].Source;
            var tgt = graph.AugEdges[ei].Target;
            Assert.InRange(graph.AugPortYSrc[ei], graph.AugY[src], graph.AugY[src] + nodes[src].Height);
            Assert.InRange(graph.AugPortYTgt[ei], graph.AugY[tgt], graph.AugY[tgt] + nodes[tgt].Height);
        }
    }

    /// <summary>Runs the stages up to and including port distribution and returns the graph.</summary>
    /// <param name="nodes">Input nodes.</param>
    /// <param name="edges">Input edges.</param>
    /// <returns>The graph after the port-distribution stage.</returns>
    private static LayeredGraph BuildPortedGraph(List<LayerNode> nodes, List<LayerEdge> edges)
    {
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        new LongEdgeSplitter().Apply(graph);
        new CrossingMinimizer().Apply(graph);
        new BrandesKopfPlacer().Apply(graph);
        new PortDistributor().Apply(graph);
        return graph;
    }
}
