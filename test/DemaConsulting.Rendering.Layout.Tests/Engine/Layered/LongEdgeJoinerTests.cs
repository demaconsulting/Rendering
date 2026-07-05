// <copyright file="LongEdgeJoinerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Layout.Engine;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Tests.Engine.Layered;

/// <summary>
///     Tests for <see cref="LongEdgeJoiner"/> covering production of one polyline per original edge
///     and concatenation of a long edge's sub-edge bend points.
/// </summary>
public sealed class LongEdgeJoinerTests
{
    /// <summary>
    ///     A single short edge yields one waypoint polyline of exactly two points (its source and
    ///     target ports, with no bends).
    /// </summary>
    [Fact]
    public void LongEdgeJoiner_Apply_SingleEdge_ProducesWaypointsPerOriginalEdge()
    {
        // Arrange / Act: join a single 0->1 edge.
        var graph = BuildJoinedGraph(
            [new(60, 40), new(60, 40)],
            [new(0, 1)]);

        // Assert: one polyline of two points.
        Assert.Single(graph.Waypoints);
        Assert.Equal(2, graph.Waypoints[0].Count);
    }

    /// <summary>
    ///     A span-three edge's polyline begins at the source's right face and ends at the target's
    ///     left face, and its point count equals its sub-edges' bend points plus the two endpoints.
    /// </summary>
    [Fact]
    public void LongEdgeJoiner_Apply_LongEdge_ConcatenatesSubEdgeBendPoints()
    {
        // Arrange: a chain plus a span-three edge 0->3 (original edge index 3).
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40), new(60, 40) };
        var graph = BuildJoinedGraph(nodes, [new(0, 1), new(1, 2), new(2, 3), new(0, 3)]);

        const int origIdx = 3;
        var polyline = graph.Waypoints[origIdx];

        // Sub-edge bend points that make up the long edge.
        var bendTotal = 0;
        for (var ei = 0; ei < graph.AugEdges.Count; ei++)
        {
            if (graph.AugEdges[ei].OrigEdgeIndex == origIdx)
            {
                bendTotal += graph.AugBendPoints[ei].Count;
            }
        }

        // Assert: one polyline per original edge, anchored to the boxes, count = endpoints + bends.
        Assert.Equal(4, graph.Waypoints.Count);
        Assert.Equal(graph.AugX[0] + nodes[0].Width, polyline[0].X);
        Assert.Equal(graph.AugX[3], polyline[^1].X);
        Assert.Equal(bendTotal + 2, polyline.Count);
    }

    /// <summary>Runs the stages up to and including long-edge joining and returns the graph.</summary>
    /// <param name="nodes">Input nodes.</param>
    /// <param name="edges">Input edges.</param>
    /// <returns>The graph after the long-edge-joining stage.</returns>
    private static LayeredGraph BuildJoinedGraph(List<LayerNode> nodes, List<LayerEdge> edges) =>
        BuildJoinedGraph(nodes, edges, LayoutDirection.Right);

    /// <summary>Runs the stages up to and including long-edge joining and returns the graph.</summary>
    /// <param name="nodes">Input nodes.</param>
    /// <param name="edges">Input edges.</param>
    /// <param name="direction">The flow direction under test.</param>
    /// <returns>The graph after the long-edge-joining stage.</returns>
    private static LayeredGraph BuildJoinedGraph(List<LayerNode> nodes, List<LayerEdge> edges, LayoutDirection direction)
    {
        var graph = new LayeredGraph(nodes, edges, direction);
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        new LongEdgeSplitter().Apply(graph);
        new CrossingMinimizer().Apply(graph);
        new BrandesKopfPlacer().Apply(graph);
        new PortDistributor().Apply(graph);
        new LayeredCorridorRouter().Apply(graph);
        new LongEdgeJoiner().Apply(graph);
        return graph;
    }

    /// <summary>
    ///     Under a <see cref="LayoutDirection.Down"/> flow the target's resolved real face is
    ///     <see cref="PortSide.Top"/>, so a <see cref="BoxShape.Folder"/> target's assembled endpoint is
    ///     projected inward by the folder-tab height, touching the recessed body top rather than the
    ///     bounding-box edge.
    /// </summary>
    [Fact]
    public void LongEdgeJoiner_Apply_FolderTargetTopFace_Down_ProjectsInwardByTabHeight()
    {
        // Arrange: a source node feeding a Folder-shaped target under a Down flow.
        var nodes = new List<LayerNode>
        {
            new(60, 40),
            new(140, 90, BoxShape.Folder, FolderTabWidth: 60.0, FolderTabHeight: 24.0, Label: "Utilities", RealWidth: 140, RealHeight: 90),
        };
        var graph = BuildJoinedGraph(nodes, [new(0, 1)], LayoutDirection.Down);

        var tgt = graph.AugEdges[0].Target;
        var polyline = graph.Waypoints[0];

        // Assert: the target endpoint's X (the perpendicular axis for a Down flow) is recessed by the
        // 24-unit tab height rather than sitting on the plain bounding-box edge.
        Assert.Equal(graph.AugX[tgt] + 24.0, polyline[^1].X, 6);
    }

    /// <summary>
    ///     Under an <see cref="LayoutDirection.Up"/> flow the source's resolved real face is
    ///     <see cref="PortSide.Top"/>, so a <see cref="BoxShape.Folder"/> source's assembled endpoint is
    ///     projected inward by the folder-tab height.
    /// </summary>
    [Fact]
    public void LongEdgeJoiner_Apply_FolderSourceTopFace_Up_ProjectsInwardByTabHeight()
    {
        // Arrange: a Folder-shaped source node under an Up flow.
        var nodes = new List<LayerNode>
        {
            new(140, 90, BoxShape.Folder, FolderTabWidth: 60.0, FolderTabHeight: 24.0, Label: "Utilities", RealWidth: 140, RealHeight: 90),
            new(60, 40),
        };
        var graph = BuildJoinedGraph(nodes, [new(0, 1)], LayoutDirection.Up);

        var src = graph.AugEdges[0].Source;
        var polyline = graph.Waypoints[0];

        // Assert: the source endpoint's X is recessed inward (toward the node) by the tab height.
        Assert.Equal(graph.AugX[src] + nodes[src].Width - 24.0, polyline[0].X, 6);
    }

    /// <summary>
    ///     Under a <see cref="LayoutDirection.Right"/> flow the source's resolved real face is
    ///     <see cref="PortSide.Right"/>, which <see cref="BoxShape.Folder"/> never restricts or
    ///     projects (only the Top face carries a tab), so the source endpoint is unaffected — matching
    ///     the plain-rectangle formula exactly.
    /// </summary>
    [Fact]
    public void LongEdgeJoiner_Apply_FolderNode_Right_NonTopFaceUnaffected()
    {
        // Arrange: a Folder-shaped source node under the default Right flow (source face = Right).
        var nodes = new List<LayerNode>
        {
            new(140, 90, BoxShape.Folder, FolderTabWidth: 60.0, FolderTabHeight: 24.0, Label: "Utilities", RealWidth: 140, RealHeight: 90),
            new(60, 40),
        };
        var graph = BuildJoinedGraph(nodes, [new(0, 1)]);

        var src = graph.AugEdges[0].Source;
        var polyline = graph.Waypoints[0];

        // Assert: the source endpoint's X exactly matches the non-projected right-face formula.
        Assert.Equal(graph.AugX[src] + nodes[src].Width, polyline[0].X, 6);
    }

    /// <summary>
    ///     Under a <see cref="LayoutDirection.Left"/> flow the target's resolved real face is
    ///     <see cref="PortSide.Right"/>, which <see cref="BoxShape.Folder"/> never restricts or
    ///     projects, so the target endpoint is unaffected — matching the plain-rectangle formula
    ///     exactly.
    /// </summary>
    [Fact]
    public void LongEdgeJoiner_Apply_FolderNode_Left_NonTopFaceUnaffected()
    {
        // Arrange: a Folder-shaped target node under a Left flow (target face = Right).
        var nodes = new List<LayerNode>
        {
            new(60, 40),
            new(140, 90, BoxShape.Folder, FolderTabWidth: 60.0, FolderTabHeight: 24.0, Label: "Utilities", RealWidth: 140, RealHeight: 90),
        };
        var graph = BuildJoinedGraph(nodes, [new(0, 1)], LayoutDirection.Left);

        var tgt = graph.AugEdges[0].Target;
        var polyline = graph.Waypoints[0];

        // Assert: the target endpoint's X exactly matches the non-projected right-face formula.
        Assert.Equal(graph.AugX[tgt], polyline[^1].X, 6);
    }

    /// <summary>
    ///     A plain <see cref="BoxShape.Rectangle"/> node's assembled endpoint is byte-identical to the
    ///     pre-shape-awareness formula: regression guard proving the
    ///     <c>ShapeAnchorSupport.IsPlainRectangle</c> fast path skips geometry resolution entirely (not
    ///     just producing a zero offset) for the default (and by far most common) shape.
    /// </summary>
    [Fact]
    public void LongEdgeJoiner_Apply_RectangleNode_MatchesPlainFormula()
    {
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40) };
        var graph = BuildJoinedGraph(nodes, [new(0, 1)]);

        var src = graph.AugEdges[0].Source;
        var tgt = graph.AugEdges[0].Target;
        var polyline = graph.Waypoints[0];

        Assert.Equal(graph.AugX[src] + graph.AugNodes[src].Width, polyline[0].X, 9);
        Assert.Equal(graph.AugX[tgt], polyline[^1].X, 9);
    }
}
