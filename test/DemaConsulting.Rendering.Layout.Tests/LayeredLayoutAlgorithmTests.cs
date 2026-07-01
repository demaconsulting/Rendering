// <copyright file="LayeredLayoutAlgorithmTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests for the bundled <see cref="LayeredLayoutAlgorithm"/>, exercising the full
///     graph-to-placed-tree path.
/// </summary>
public class LayeredLayoutAlgorithmTests
{
    /// <summary>
    ///     Proves that the algorithm advertises the stable "layered" identifier.
    /// </summary>
    [Fact]
    public void Id_IsLayered()
    {
        Assert.Equal("layered", new LayeredLayoutAlgorithm().Id);
    }

    /// <summary>
    ///     Proves that a simple chain graph is placed into left-to-right layers with one box per node
    ///     and one routed connector per edge.
    /// </summary>
    [Fact]
    public void Apply_ChainGraph_PlacesLayeredBoxesAndRoutesEdges()
    {
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        var c = graph.AddNode("c", 80, 40);
        graph.AddEdge("e1", a, b);
        graph.AddEdge("e2", b, c);

        var tree = new LayeredLayoutAlgorithm().Apply(graph, new LayoutOptions());

        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();

        Assert.Equal(3, boxes.Count);
        Assert.Equal(2, lines.Count);
        Assert.True(tree.Width > 0);
        Assert.True(tree.Height > 0);

        // Boxes are emitted in input order; a chain lays out with strictly increasing X.
        Assert.True(boxes[0].X < boxes[1].X);
        Assert.True(boxes[1].X < boxes[2].X);

        // Every connector has at least a start and an end waypoint.
        Assert.All(lines, line => Assert.True(line.Waypoints.Count >= 2));
    }

    /// <summary>
    ///     Proves that an empty graph produces an empty, positively-sized canvas.
    /// </summary>
    [Fact]
    public void Apply_EmptyGraph_ReturnsEmptyCanvas()
    {
        var tree = new LayeredLayoutAlgorithm().Apply(new LayoutGraph(), new LayoutOptions());

        Assert.Empty(tree.Nodes);
        Assert.True(tree.Width > 0);
        Assert.True(tree.Height > 0);
    }

    /// <summary>
    ///     Proves that a null graph argument is rejected.
    /// </summary>
    [Fact]
    public void Apply_NullGraph_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new LayeredLayoutAlgorithm().Apply(null!, new LayoutOptions()));
    }
}
