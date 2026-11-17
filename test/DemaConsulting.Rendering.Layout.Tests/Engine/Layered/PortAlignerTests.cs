// <copyright file="PortAlignerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout.Engine;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Tests.Engine.Layered;

/// <summary>
///     Stage-level tests for <see cref="PortAligner"/> (Rendering#57), in the pipeline's abstract axes:
///     it slides the ports of an edge onto one coordinate when both faces allow it, and otherwise leaves
///     the even spacing alone.
/// </summary>
public sealed class PortAlignerTests
{
    /// <summary>Two sources whose face spans cover the wide target's port positions end up straight.</summary>
    [Fact]
    public void PortAligner_Apply_TwoSourcesOneWideTarget_PortsAlign()
    {
        // Arrange: the spacing-only ports are a few pixels apart.
        var nodes = new List<LayerNode> { new(50, 80), new(50, 80), new(50, 200) };
        var graph = BuildGraph(nodes, [new(0, 2), new(1, 2)]);
        var before = Gaps(graph);

        // Act
        new PortAligner().Apply(graph);

        // Assert: some pair was misaligned before, every pair is aligned afterwards.
        Assert.Contains(before, gap => gap > 1e-6);
        Assert.All(Gaps(graph), gap => Assert.True(gap < 1e-6, $"residual gap {gap}"));
    }

    /// <summary>A graph whose ports are already aligned is left bit-identical.</summary>
    [Fact]
    public void PortAligner_Apply_SingleEdgeAlreadyAligned_IsNoOp()
    {
        // Arrange: two equal nodes centre-aligned by the placer.
        var graph = BuildGraph([new(60, 40), new(60, 40)], [new(0, 1)]);
        var src = graph.AugPortYSrc.ToArray();
        var tgt = graph.AugPortYTgt.ToArray();

        // Act
        new PortAligner().Apply(graph);

        // Assert
        Assert.Equal(src, graph.AugPortYSrc);
        Assert.Equal(tgt, graph.AugPortYTgt);
    }

    /// <summary>With <see cref="LayeredGraph.AlignPorts"/> off the stage changes nothing.</summary>
    [Fact]
    public void PortAligner_Apply_AlignPortsDisabled_LeavesPortsUntouched()
    {
        // Arrange
        var graph = BuildGraph([new(50, 80), new(50, 80), new(50, 200)], [new(0, 2), new(1, 2)]);
        graph.AlignPorts = false;
        var src = graph.AugPortYSrc.ToArray();
        var tgt = graph.AugPortYTgt.ToArray();

        // Act
        new PortAligner().Apply(graph);

        // Assert
        Assert.Equal(src, graph.AugPortYSrc);
        Assert.Equal(tgt, graph.AugPortYTgt);
    }

    /// <summary>
    ///     Slid ports keep their order and stay at least the edge spacing apart and clear of the face
    ///     corners.
    /// </summary>
    [Fact]
    public void PortAligner_Apply_ThreeSourcesOneTarget_KeepsOrderSeparationAndCornerClearance()
    {
        // Arrange
        var nodes = new List<LayerNode> { new(50, 70), new(50, 70), new(50, 70), new(50, 240) };
        var graph = BuildGraph(nodes, [new(0, 3), new(1, 3), new(2, 3)]);
        var orderBefore = Order(graph.AugPortYTgt);

        // Act
        new PortAligner().Apply(graph);

        // Assert
        Assert.Equal(orderBefore, Order(graph.AugPortYTgt));
        var ports = graph.AugPortYTgt.OrderBy(y => y).ToList();
        for (var i = 1; i < ports.Count; i++)
        {
            Assert.True(ports[i] - ports[i - 1] >= LayeredLayoutMetrics.EdgeSpacing - 1e-6);
        }

        var target = graph.AugEdges[0].Target;
        Assert.True(ports[0] >= graph.AugY[target] + LayeredLayoutMetrics.ConnectorClearance - 1e-6);
        Assert.True(ports[^1] <= graph.AugY[target] + nodes[target].Height - LayeredLayoutMetrics.ConnectorClearance + 1e-6);
    }

    /// <summary>
    ///     A long-edge dummy is never moved: the real end slides onto the dummy's coordinate when its face
    ///     allows it, and the dummy's own ports stay on its coordinate.
    /// </summary>
    [Fact]
    public void PortAligner_Apply_DummyEnd_IsPinnedAndNotMoved()
    {
        // Arrange: a -> b -> c plus a long edge a -> c (one dummy in the middle layer).
        var nodes = new List<LayerNode> { new(100, 50), new(100, 50), new(220, 50) };
        var graph = BuildGraph(nodes, [new(0, 1), new(1, 2), new(0, 2)]);
        var yBefore = graph.AugY.ToArray();

        // Act
        new PortAligner().Apply(graph);

        // Assert: the stage never writes AugY, and every port at a dummy equals that dummy's Y.
        Assert.Equal(yBefore, graph.AugY);
        for (var ei = 0; ei < graph.AugEdges.Count; ei++)
        {
            var edge = graph.AugEdges[ei];
            if (graph.AugNodes[edge.Source].IsDummy)
            {
                Assert.Equal(graph.AugY[edge.Source], graph.AugPortYSrc[ei]);
            }

            if (graph.AugNodes[edge.Target].IsDummy)
            {
                Assert.Equal(graph.AugY[edge.Target], graph.AugPortYTgt[ei]);
            }
        }
    }

    /// <summary>
    ///     A folder target's port stays on the connectable extent (right of the tab) after sliding.
    /// </summary>
    [Fact]
    public void PortAligner_Apply_FolderTargetTopFace_Down_PortStaysRightOfTab()
    {
        // Arrange: two narrow sources into a folder whose tab covers the left of the top face.
        var nodes = new List<LayerNode>
        {
            new(40, 40),
            new(40, 40),
            new(240, 90, BoxShape.Folder, FolderTabWidth: 100.0, FolderTabHeight: 24.0, Label: "F", RealWidth: 240, RealHeight: 90),
        };
        var graph = BuildGraph(nodes, [new(0, 2), new(1, 2)], LayoutDirection.Down);
        var target = graph.AugEdges[0].Target;

        // Act
        new PortAligner().Apply(graph);

        // Assert: every target port lies beyond the tab (plus margin) within the face.
        Assert.All(graph.AugPortYTgt, port =>
        {
            var local = port - graph.AugY[target];
            Assert.True(local >= 100.0, $"port {local} is inside the folder tab");
            Assert.True(local <= 240.0);
        });
    }

    /// <summary>
    ///     When both faces are too far apart to meet, a residual step shorter than the minimum is pushed
    ///     out to the minimum (or left if already clear), never left as a sub-marker step.
    /// </summary>
    [Fact]
    public void PortAligner_Apply_DisjointFaces_NoStepBetweenZeroAndMinimum()
    {
        // Arrange: tall source column and short target so some pairs cannot meet.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40), new(60, 400) };
        var graph = BuildGraph(nodes, [new(0, 3), new(1, 3), new(2, 3)]);

        // Act
        new PortAligner().Apply(graph);

        // Assert
        Assert.All(Gaps(graph), gap => Assert.True(gap < 1e-6 || gap >= PortAligner.MinPortStep - 1e-6, $"gap {gap}"));
    }

    /// <summary>Runs the pipeline up to and including port distribution and returns the graph.</summary>
    private static LayeredGraph BuildGraph(
        List<LayerNode> nodes,
        List<LayerEdge> edges,
        LayoutDirection direction = LayoutDirection.Right)
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

    /// <summary>Returns the absolute source/target port gap of every sub-edge.</summary>
    private static List<double> Gaps(LayeredGraph graph) =>
        graph.AugEdges.Select((_, i) => Math.Abs(graph.AugPortYSrc[i] - graph.AugPortYTgt[i])).ToList();

    /// <summary>Returns the indices of <paramref name="values"/> ordered by value.</summary>
    private static List<int> Order(double[] values) =>
        Enumerable.Range(0, values.Length).OrderBy(i => values[i]).ThenBy(i => i).ToList();
}
