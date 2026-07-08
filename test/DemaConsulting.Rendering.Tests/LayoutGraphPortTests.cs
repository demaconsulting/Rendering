// <copyright file="LayoutGraphPortTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Tests;

/// <summary>
///     Tests for the <see cref="LayoutGraphPort"/>/<see cref="LayoutGraphPortCollection"/> port model
///     and its lazy allocation on <see cref="LayoutGraphNode"/>.
/// </summary>
public sealed class LayoutGraphPortTests
{
    /// <summary>
    ///     Proves that a node with no ports ever accessed reports <see cref="LayoutGraphNode.HasPorts"/>
    ///     as <see langword="false"/> without allocating a port collection.
    /// </summary>
    [Fact]
    public void LayoutGraphNode_HasPorts_NoPortsAdded_ReturnsFalse()
    {
        var graph = new LayoutGraph();
        var node = graph.AddNode("a", 80, 40);

        Assert.False(node.HasPorts);
    }

    /// <summary>
    ///     Proves that AddPort appends a port and returns it with the requested identity, and that the
    ///     owning node now reports <see cref="LayoutGraphNode.HasPorts"/> as <see langword="true"/>.
    /// </summary>
    [Fact]
    public void Ports_AddPort_AppendsPortAndReturnsIt()
    {
        var graph = new LayoutGraph();
        var node = graph.AddNode("a", 80, 40);

        var port = node.Ports.AddPort("p1");

        Assert.True(node.HasPorts);
        Assert.Single(node.Ports.Ports);
        Assert.Equal("p1", port.Id);
        Assert.Same(port, node.Ports.Ports[0]);
    }

    /// <summary>
    ///     Proves that <see cref="LayoutGraphPort.ExternalLabel"/> and
    ///     <see cref="LayoutGraphPort.InternalLabel"/> default to <see langword="null"/> and are
    ///     independently settable.
    /// </summary>
    [Fact]
    public void Port_Labels_DefaultNullAndIndependentlySettable()
    {
        var graph = new LayoutGraph();
        var node = graph.AddNode("a", 80, 40);
        var port = node.Ports.AddPort("p1");

        Assert.Null(port.ExternalLabel);
        Assert.Null(port.InternalLabel);

        port.ExternalLabel = "out";
        Assert.Equal("out", port.ExternalLabel);
        Assert.Null(port.InternalLabel);

        port.InternalLabel = "in";
        Assert.Equal("out", port.ExternalLabel);
        Assert.Equal("in", port.InternalLabel);
    }

    /// <summary>
    ///     Proves that AddPort enforces port-identifier uniqueness within the owning node's own
    ///     collection.
    /// </summary>
    [Fact]
    public void Ports_AddPort_DuplicateId_ThrowsArgumentException()
    {
        var graph = new LayoutGraph();
        var node = graph.AddNode("a", 80, 40);
        node.Ports.AddPort("p1");

        Assert.Throws<ArgumentException>(() => node.Ports.AddPort("p1"));
        Assert.Single(node.Ports.Ports);
    }

    /// <summary>
    ///     Proves that the same port identifier may be reused across two different nodes' own port
    ///     collections without conflict, mirroring node/edge identifier scoping.
    /// </summary>
    [Fact]
    public void Ports_AddPort_SameIdOnDifferentNodes_Allowed()
    {
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);

        var portA = a.Ports.AddPort("p1");
        var portB = b.Ports.AddPort("p1");

        Assert.NotSame(portA, portB);
    }

    /// <summary>
    ///     Proves that AddPort rejects a null identifier.
    /// </summary>
    [Fact]
    public void Ports_AddPort_NullId_ThrowsArgumentNullException()
    {
        var graph = new LayoutGraph();
        var node = graph.AddNode("a", 80, 40);

        Assert.Throws<ArgumentNullException>(() => node.Ports.AddPort(null!));
    }

    /// <summary>
    ///     Proves that AddPort rejects an empty identifier.
    /// </summary>
    [Fact]
    public void Ports_AddPort_EmptyId_ThrowsArgumentException()
    {
        var graph = new LayoutGraph();
        var node = graph.AddNode("a", 80, 40);

        Assert.Throws<ArgumentException>(() => node.Ports.AddPort(string.Empty));
    }

    /// <summary>
    ///     Proves that a <see cref="LayoutGraphEdge"/> may connect a port to a plain node, and that
    ///     both <see cref="LayoutGraphNode"/> and <see cref="LayoutGraphPort"/> satisfy
    ///     <see cref="ILayoutConnectable"/> so <see cref="LayoutGraph.AddEdge"/> accepts either.
    /// </summary>
    [Fact]
    public void AddEdge_PortToNode_ConnectsPortAsSource()
    {
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        var port = a.Ports.AddPort("out1");

        var edge = graph.AddEdge("e1", port, b);

        Assert.Same(port, edge.Source);
        Assert.Same(b, edge.Target);
        Assert.IsAssignableFrom<ILayoutConnectable>(port);
        Assert.IsAssignableFrom<ILayoutConnectable>(a);
    }
}
