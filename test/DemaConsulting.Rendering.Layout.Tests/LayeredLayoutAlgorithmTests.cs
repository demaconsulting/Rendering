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

    /// <summary>
    ///     Proves that a null options argument is rejected.
    /// </summary>
    [Fact]
    public void Apply_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new LayeredLayoutAlgorithm().Apply(new LayoutGraph(), null!));
    }

    /// <summary>
    ///     Proves that selecting a downward flow direction lays the chain out top-to-bottom: boxes are
    ///     stacked in strictly increasing Y (rather than the default left-to-right increasing X), and
    ///     the canvas is taller than it is wide.
    /// </summary>
    [Fact]
    public void Apply_DownDirection_FlowsTopToBottom()
    {
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        var c = graph.AddNode("c", 80, 40);
        graph.AddEdge("e1", a, b);
        graph.AddEdge("e2", b, c);

        var options = new LayoutOptions();
        options.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        var tree = new LayeredLayoutAlgorithm().Apply(graph, options);

        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        Assert.Equal(3, boxes.Count);

        // A downward chain stacks its boxes vertically: strictly increasing Y.
        Assert.True(boxes[0].Y < boxes[1].Y);
        Assert.True(boxes[1].Y < boxes[2].Y);

        // A three-deep top-to-bottom flow of short boxes is taller than it is wide.
        Assert.True(tree.Height > tree.Width);
    }

    /// <summary>
    ///     Proves that the downward flow is a genuinely different layout from the default rightward
    ///     flow — a regression guard against the option being silently ignored (which would return the
    ///     identical left-to-right coordinates for both directions).
    /// </summary>
    [Fact]
    public void Apply_DownDirection_DiffersFromRight()
    {
        var algorithm = new LayeredLayoutAlgorithm();

        var right = algorithm.Apply(BuildChain(), new LayoutOptions());

        var downOptions = new LayoutOptions();
        downOptions.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        var down = algorithm.Apply(BuildChain(), downOptions);

        // Rightward is wide-and-short; downward is tall-and-narrow. They must not be identical.
        Assert.True(right.Width > right.Height);
        Assert.True(down.Height > down.Width);

        var rightBoxes = right.Nodes.OfType<LayoutBox>().ToList();
        var downBoxes = down.Nodes.OfType<LayoutBox>().ToList();
        Assert.True(rightBoxes[0].X < rightBoxes[2].X);
        Assert.True(downBoxes[0].Y < downBoxes[2].Y);
    }

    /// <summary>
    ///     Proves that the flow direction is honored when carried on the graph scope, mirroring how the
    ///     algorithm resolves its other well-known options (graph scope takes precedence over options).
    /// </summary>
    [Fact]
    public void Apply_DownDirectionOnGraphScope_IsHonored()
    {
        var graph = BuildChain();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        var tree = new LayeredLayoutAlgorithm().Apply(graph, new LayoutOptions());

        Assert.True(tree.Height > tree.Width);
    }

    /// <summary>
    ///     Proves that the default (unset) direction lays the graph out left-to-right, so existing
    ///     callers that never set the option are unaffected.
    /// </summary>
    [Fact]
    public void Apply_DefaultDirection_FlowsLeftToRight()
    {
        var tree = new LayeredLayoutAlgorithm().Apply(BuildChain(), new LayoutOptions());

        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        Assert.True(boxes[0].X < boxes[1].X);
        Assert.True(boxes[1].X < boxes[2].X);
        Assert.True(tree.Width > tree.Height);
    }

    /// <summary>Builds the standard three-node chain graph used by the direction tests.</summary>
    private static LayoutGraph BuildChain()
    {
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        var c = graph.AddNode("c", 80, 40);
        graph.AddEdge("e1", a, b);
        graph.AddEdge("e2", b, c);
        return graph;
    }

    /// <summary>
    ///     Proves that the hierarchical input-model capability is behavior-preserving: a flat graph
    ///     whose nodes declare no children lays out exactly as before, because the layered algorithm
    ///     reads only the top-level nodes and edges and ignores nesting.
    /// </summary>
    [Fact]
    public void Apply_FlatGraphWithNoChildren_PlacesTopLevelStructureUnchanged()
    {
        // Arrange: build a flat chain graph and confirm none of its nodes are containers
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        var c = graph.AddNode("c", 80, 40);
        graph.AddEdge("e1", a, b);
        graph.AddEdge("e2", b, c);
        Assert.All(new[] { a, b, c }, node => Assert.False(node.HasChildren));

        // Act: lay the flat graph out with the bundled algorithm
        var tree = new LayeredLayoutAlgorithm().Apply(graph, new LayoutOptions());

        // Assert: the placed structure matches the classic flat result (one box per node,
        // one connector per edge, left-to-right chain ordering)
        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();
        Assert.Equal(3, boxes.Count);
        Assert.Equal(2, lines.Count);
        Assert.True(boxes[0].X < boxes[1].X);
        Assert.True(boxes[1].X < boxes[2].X);
    }

    /// <summary>
    ///     Proves that a chain of nodes whose cross-extent is smaller than the port clearance band lays
    ///     out without throwing in every flow direction. Regression guard for the inverted-clamp crash
    ///     in the port distributor: under <see cref="LayoutFlowDirection.Down"/>/<see cref="LayoutFlowDirection.Up"/>
    ///     the port face is sized by the node <em>width</em> (axis swap), so narrow boxes (10/20/30 wide)
    ///     previously produced a <c>min &gt; max</c> range in <c>Math.Clamp</c> and threw an opaque
    ///     <see cref="ArgumentException"/> from deep in the pipeline.
    /// </summary>
    /// <param name="direction">The flow direction under test.</param>
    [Theory]
    [InlineData(LayoutFlowDirection.Right)]
    [InlineData(LayoutFlowDirection.Left)]
    [InlineData(LayoutFlowDirection.Down)]
    [InlineData(LayoutFlowDirection.Up)]
    public void Apply_SmallNodeChain_PlacesWithoutThrowingInEveryDirection(LayoutFlowDirection direction)
    {
        // Arrange: a chain of nodes far narrower than the port-clearance band (10/20/30 wide, 40 tall).
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 10, 40);
        var b = graph.AddNode("b", 20, 40);
        var c = graph.AddNode("c", 30, 40);
        graph.AddEdge("ab", a, b);
        graph.AddEdge("bc", b, c);

        var options = LayoutOptions.ForAlgorithm("layered");
        options.Set(CoreOptions.Direction, direction);

        // Act: laying out must not throw regardless of direction.
        var tree = new LayeredLayoutAlgorithm().Apply(graph, options);

        // Assert: a valid placed tree with one box per node and one connector per edge.
        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();
        Assert.Equal(3, boxes.Count);
        Assert.Equal(2, lines.Count);
        Assert.True(tree.Width > 0);
        Assert.True(tree.Height > 0);
        Assert.All(lines, line => Assert.True(line.Waypoints.Count >= 2));
    }
}
