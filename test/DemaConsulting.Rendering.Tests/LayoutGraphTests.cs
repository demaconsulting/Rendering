// <copyright file="LayoutGraphTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Tests;

/// <summary>
///     Tests for the input <see cref="LayoutGraph"/> model and its element factory methods.
/// </summary>
public class LayoutGraphTests
{
    /// <summary>
    ///     Proves that AddNode appends a node and returns it with the requested identity and size.
    /// </summary>
    [Fact]
    public void AddNode_AppendsNodeAndReturnsIt()
    {
        var graph = new LayoutGraph();

        var node = graph.AddNode("a", 80, 40);

        Assert.Single(graph.Nodes);
        Assert.Equal("a", node.Id);
        Assert.Equal(80, node.Width);
        Assert.Equal(40, node.Height);
    }

    /// <summary>
    ///     Proves that AddEdge appends a directed edge referencing the given endpoints.
    /// </summary>
    [Fact]
    public void AddEdge_AppendsEdgeWithEndpoints()
    {
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 1, 1);
        var b = graph.AddNode("b", 1, 1);

        var edge = graph.AddEdge("e", a, b);

        Assert.Single(graph.Edges);
        Assert.Same(a, edge.Source);
        Assert.Same(b, edge.Target);
    }

    /// <summary>
    ///     Proves that a per-element property override is stored on the node itself.
    /// </summary>
    [Fact]
    public void Node_CarriesPerElementProperties()
    {
        var graph = new LayoutGraph();
        var node = graph.AddNode("a", 1, 1);

        node.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        Assert.Equal(LayoutFlowDirection.Down, node.Get(CoreOptions.Direction));
    }
}
