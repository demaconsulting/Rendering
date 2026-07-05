// <copyright file="PortDistributorTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;
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
        var graph = BuildPortedGraph(nodes, [new(0, 1)]);

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
        var graph = BuildPortedGraph(nodes, [new(0, 1), new(0, 2), new(1, 3), new(2, 3)]);

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
        var graph = BuildPortedGraph(nodes, [new(0, 1), new(0, 2)]);

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
    private static LayeredGraph BuildPortedGraph(List<LayerNode> nodes, List<LayerEdge> edges) =>
        BuildPortedGraph(nodes, edges, LayoutDirection.Right);

    /// <summary>Runs the stages up to and including port distribution and returns the graph.</summary>
    /// <param name="nodes">Input nodes.</param>
    /// <param name="edges">Input edges.</param>
    /// <param name="direction">The flow direction under test.</param>
    /// <returns>The graph after the port-distribution stage.</returns>
    private static LayeredGraph BuildPortedGraph(List<LayerNode> nodes, List<LayerEdge> edges, LayoutDirection direction)
    {
        var graph = new LayeredGraph(nodes, edges, direction);
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        new LongEdgeSplitter().Apply(graph);
        new CrossingMinimizer().Apply(graph);
        new BrandesKopfPlacer().Apply(graph);
        new PortDistributor().Apply(graph);
        return graph;
    }

    /// <summary>
    ///     A <see cref="BoxShape.Folder"/> target node whose tab occupies part of the direction's
    ///     resolved target face (the real <see cref="PortSide.Top"/> face for
    ///     <see cref="LayoutDirection.Down"/>) has its port band restricted to the tab-excluded portion
    ///     of that face, instead of the plain full-span band a rectangle would use.
    /// </summary>
    [Fact]
    public void PortDistributor_Apply_FolderTargetTopFace_Down_PortExcludesTabRegion()
    {
        // Arrange: a single edge into a Folder-shaped target under a Down (top-to-bottom) flow, whose
        // resolved target face is the real Top face.
        var nodes = new List<LayerNode>
        {
            new(60, 40),
            new(140, 90, BoxShape.Folder, FolderTabWidth: 60.0, FolderTabHeight: 24.0, Label: "Utilities", RealWidth: 140, RealHeight: 90),
        };
        var graph = BuildPortedGraph(nodes, [new(0, 1)], LayoutDirection.Down);

        var tgt = graph.AugEdges[0].Target;
        var localX = graph.AugPortYTgt[0] - graph.AugY[tgt];

        // Assert: the port lands to the right of the tab (plus its small anti-shoulder margin), never
        // inside the tab-excluded strip a plain rectangle's full-span band would have allowed.
        Assert.True(localX > 60.0, $"Expected port beyond the 60-wide tab, got local X {localX}.");
    }

    /// <summary>
    ///     A <see cref="BoxShape.Note"/> node's fold-excluded strip on the real
    ///     <see cref="PortSide.Right"/> face (resolved for a <see cref="LayoutDirection.Right"/> source)
    ///     is excluded from the port band, matching <see cref="ConnectorRouter"/>'s own exclusion rule.
    /// </summary>
    [Fact]
    public void PortDistributor_Apply_NoteSourceRightFace_Right_PortExcludesFoldRegion()
    {
        // Arrange: a single edge out of a Note-shaped source under a Right flow, whose resolved source
        // face is the real Right face. A 140x90 note folds min(140, 90) * 0.25 = 22.5, capped at
        // NotationMetrics.NoteFoldMaxSize (16).
        var nodes = new List<LayerNode>
        {
            new(140, 90, BoxShape.Note, RealWidth: 140, RealHeight: 90),
            new(60, 40),
        };
        var graph = BuildPortedGraph(nodes, [new(0, 1)]);

        var src = graph.AugEdges[0].Source;
        var localY = graph.AugPortYSrc[0] - graph.AugY[src];

        // Assert: the port lands below the folded-corner strip, never within it.
        Assert.True(
            localY > NotationMetrics.NoteFoldMaxSize,
            $"Expected port below the fold ({NotationMetrics.NoteFoldMaxSize}), got local Y {localY}.");
    }

    /// <summary>
    ///     A plain <see cref="BoxShape.Rectangle"/> node's port distribution is byte-identical to the
    ///     pre-shape-awareness full-span formula: regression guard proving the
    ///     <c>ShapeAnchorSupport.IsPlainRectangle</c> fast path never engages the new shape-aware code
    ///     for the default (and by far most common) shape.
    /// </summary>
    [Fact]
    public void PortDistributor_Apply_RectangleNode_MatchesPlainFullSpanFormula()
    {
        // Arrange: a fan-in of three edges onto a plain-rectangle target.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40), new(60, 90) };
        var graph = BuildPortedGraph(nodes, [new(0, 3), new(1, 3), new(2, 3)]);

        var tgt = graph.AugEdges[0].Target;
        const double clearance = LayeredLayoutMetrics.ConnectorClearance;
        var inset = Math.Min(clearance, nodes[tgt].Height / 2.0);
        var usable = nodes[tgt].Height - (2.0 * inset);

        // Assert: each recorded port Y matches the original even-spacing formula exactly.
        var sorted = graph.AugEdges
            .Select((e, i) => (Edge: e, Index: i))
            .Where(x => x.Edge.Target == tgt)
            .OrderBy(x => graph.AugY[x.Edge.Source] + (nodes[x.Edge.Source].Height / 2.0))
            .ThenBy(x => x.Index)
            .Select(x => x.Index)
            .ToList();
        for (var k = 0; k < sorted.Count; k++)
        {
            var expected = graph.AugY[tgt] + inset + (k * usable / (sorted.Count - 1));
            Assert.Equal(expected, graph.AugPortYTgt[sorted[k]], 9);
        }
    }
}
