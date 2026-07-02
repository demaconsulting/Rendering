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
        // Arrange: create an empty graph
        var graph = new LayoutGraph();

        // Act: add a sized node
        var node = graph.AddNode("a", 80, 40);

        // Assert: the node is appended and carries the requested identity and size
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
        // Arrange: create a graph with two nodes
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 1, 1);
        var b = graph.AddNode("b", 1, 1);

        // Act: connect the two nodes with an edge
        var edge = graph.AddEdge("e", a, b);

        // Assert: the edge is appended and references both endpoints
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
        // Arrange: create a graph with a single node
        var graph = new LayoutGraph();
        var node = graph.AddNode("a", 1, 1);

        // Act: attach a per-node property override
        node.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        // Assert: the override is stored on the node
        Assert.Equal(LayoutFlowDirection.Down, node.Get(CoreOptions.Direction));
    }

    /// <summary>
    ///     Proves that AddNode rejects a null identifier.
    /// </summary>
    [Fact]
    public void AddNode_NullId_ThrowsArgumentNullException()
    {
        // Arrange: create an empty graph
        var graph = new LayoutGraph();

        // Act / Assert: a null id is rejected
        Assert.Throws<ArgumentNullException>(() => graph.AddNode(null!, 1, 1));
    }

    /// <summary>
    ///     Proves that AddNode rejects an empty identifier.
    /// </summary>
    [Fact]
    public void AddNode_EmptyId_ThrowsArgumentException()
    {
        // Arrange: create an empty graph
        var graph = new LayoutGraph();

        // Act / Assert: an empty id is rejected
        Assert.Throws<ArgumentException>(() => graph.AddNode(string.Empty, 1, 1));
    }

    /// <summary>
    ///     Proves that AddNode enforces node-identifier uniqueness within the graph.
    /// </summary>
    [Fact]
    public void AddNode_DuplicateId_ThrowsArgumentException()
    {
        // Arrange: create a graph that already contains a node "a"
        var graph = new LayoutGraph();
        graph.AddNode("a", 1, 1);

        // Act / Assert: a second node with the same id is rejected and not appended
        Assert.Throws<ArgumentException>(() => graph.AddNode("a", 2, 2));
        Assert.Single(graph.Nodes);
    }

    /// <summary>
    ///     Proves that AddEdge rejects a null identifier.
    /// </summary>
    [Fact]
    public void AddEdge_NullId_ThrowsArgumentNullException()
    {
        // Arrange: create a graph with two nodes
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 1, 1);
        var b = graph.AddNode("b", 1, 1);

        // Act / Assert: a null id is rejected
        Assert.Throws<ArgumentNullException>(() => graph.AddEdge(null!, a, b));
    }

    /// <summary>
    ///     Proves that AddEdge rejects a null endpoint.
    /// </summary>
    [Fact]
    public void AddEdge_NullSource_ThrowsArgumentNullException()
    {
        // Arrange: create a graph with a single target node
        var graph = new LayoutGraph();
        var b = graph.AddNode("b", 1, 1);

        // Act / Assert: a null source endpoint is rejected
        Assert.Throws<ArgumentNullException>(() => graph.AddEdge("e", null!, b));
    }

    /// <summary>
    ///     Proves that AddEdge enforces edge-identifier uniqueness within the graph.
    /// </summary>
    [Fact]
    public void AddEdge_DuplicateId_ThrowsArgumentException()
    {
        // Arrange: create a graph with two nodes and an edge "e"
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 1, 1);
        var b = graph.AddNode("b", 1, 1);
        graph.AddEdge("e", a, b);

        // Act / Assert: a second edge with the same id is rejected and not appended
        Assert.Throws<ArgumentException>(() => graph.AddEdge("e", b, a));
        Assert.Single(graph.Edges);
    }

    /// <summary>
    ///     Proves that a leaf node reports it is not a container and materializes no child subgraph.
    /// </summary>
    [Fact]
    public void LayoutGraphNode_HasChildren_LeafNode_ReturnsFalse()
    {
        // Arrange: create a graph with a single, unnested node
        var graph = new LayoutGraph();
        var leaf = graph.AddNode("leaf", 80, 40);

        // Act: query the container flag without ever accessing Children
        var isContainer = leaf.HasChildren;

        // Assert: the leaf is not a container (and no child subgraph was allocated to answer)
        Assert.False(isContainer);
    }

    /// <summary>
    ///     Proves that a container node holds its nested child nodes and contained edges through its
    ///     own child subgraph and reports itself as a container.
    /// </summary>
    [Fact]
    public void LayoutGraphNode_Children_ContainerNode_HoldsChildNodesAndEdges()
    {
        // Arrange: create a container node at the root scope
        var graph = new LayoutGraph();
        var group = graph.AddNode("group", 200, 120);

        // Act: populate the container's child subgraph with two nodes and an intra-container edge
        var inner1 = group.Children.AddNode("child1", 80, 40);
        var inner2 = group.Children.AddNode("child2", 80, 40);
        var innerEdge = group.Children.AddEdge("inner-edge", inner1, inner2);

        // Assert: the node is now a container whose children and contained edge are retrievable
        Assert.True(group.HasChildren);
        Assert.Equal(2, group.Children.Nodes.Count);
        Assert.Single(group.Children.Edges);
        Assert.Same(inner1, innerEdge.Source);
        Assert.Same(inner2, innerEdge.Target);
    }

    /// <summary>
    ///     Proves that identifiers are scoped per container: the same identifier may be reused in a
    ///     different scope without conflict.
    /// </summary>
    [Fact]
    public void LayoutGraph_AddNode_ChildScope_AllowsIdReuseAcrossScopes()
    {
        // Arrange: create two container nodes that each open their own identifier scope
        var graph = new LayoutGraph();
        var left = graph.AddNode("left", 100, 100);
        var right = graph.AddNode("right", 100, 100);

        // Act: add a node named "x" inside each container, reusing the same identifier per scope
        var leftX = left.Children.AddNode("x", 40, 40);
        var rightX = right.Children.AddNode("x", 40, 40);

        // Assert: both scopes accept the shared identifier and hold distinct node instances
        Assert.Equal("x", leftX.Id);
        Assert.Equal("x", rightX.Id);
        Assert.NotSame(leftX, rightX);
    }

    /// <summary>
    ///     Proves that identifier uniqueness is enforced within a single container scope.
    /// </summary>
    [Fact]
    public void LayoutGraph_AddNode_ChildScope_DuplicateId_ThrowsArgumentException()
    {
        // Arrange: create a container that already holds a child node "dup"
        var graph = new LayoutGraph();
        var container = graph.AddNode("container", 100, 100);
        container.Children.AddNode("dup", 40, 40);

        // Act / Assert: a second child with the same id in the same scope is rejected
        Assert.Throws<ArgumentException>(() => container.Children.AddNode("dup", 50, 50));
        Assert.Single(container.Children.Nodes);
    }

    /// <summary>
    ///     Proves that a cross-container edge referencing a descendant node is constructible at an
    ///     ancestor (here the root) scope.
    /// </summary>
    [Fact]
    public void LayoutGraphEdge_CrossContainer_ReferencingDescendant_ConstructibleAtRoot()
    {
        // Arrange: a root graph with a leaf node and a container holding a descendant node
        var graph = new LayoutGraph();
        var outside = graph.AddNode("outside", 80, 40);
        var group = graph.AddNode("group", 200, 120);
        var inner = group.Children.AddNode("inner", 80, 40);

        // Act: add an edge at the root (the lowest common ancestor) that spans into the container
        var cross = graph.AddEdge("cross-edge", outside, inner);

        // Assert: the edge lives in the ancestor container yet references the descendant endpoints
        Assert.Single(graph.Edges);
        Assert.Same(outside, cross.Source);
        Assert.Same(inner, cross.Target);
        Assert.DoesNotContain(inner, graph.Nodes);
    }

    /// <summary>
    ///     Proves that an edge identifier used in one container scope may be reused in a different
    ///     scope, confirming edge identifiers are scoped per container just like node identifiers.
    /// </summary>
    [Fact]
    public void LayoutGraph_AddEdge_ChildScope_AllowsEdgeIdReuseAcrossScopes()
    {
        // Arrange: two sibling containers, each with two nodes to connect
        var graph = new LayoutGraph();
        var left = graph.AddNode("left", 100, 100);
        var right = graph.AddNode("right", 100, 100);
        var la = left.Children.AddNode("a", 40, 40);
        var lb = left.Children.AddNode("b", 40, 40);
        var ra = right.Children.AddNode("a", 40, 40);
        var rb = right.Children.AddNode("b", 40, 40);

        // Act: add an edge with the same id "e" inside each container scope
        var leftEdge = left.Children.AddEdge("e", la, lb);
        var rightEdge = right.Children.AddEdge("e", ra, rb);

        // Assert: both are accepted as distinct edges in their own scopes
        Assert.Same(la, leftEdge.Source);
        Assert.Same(rb, rightEdge.Target);
        Assert.NotSame(leftEdge, rightEdge);
        Assert.Single(left.Children.Edges);
        Assert.Single(right.Children.Edges);
    }

    /// <summary>
    ///     Proves that a cross-container edge between nodes living in two different sibling
    ///     containers is constructible at their lowest common ancestor (the root).
    /// </summary>
    [Fact]
    public void LayoutGraphEdge_CrossContainer_BetweenSiblingContainers_ConstructibleAtRoot()
    {
        // Arrange: two sibling containers, each holding one descendant node
        var graph = new LayoutGraph();
        var groupA = graph.AddNode("groupA", 120, 100);
        var groupB = graph.AddNode("groupB", 120, 100);
        var innerA = groupA.Children.AddNode("innerA", 40, 40);
        var innerB = groupB.Children.AddNode("innerB", 40, 40);

        // Act: add an edge at the root (the LCA of the two siblings) spanning both containers
        var cross = graph.AddEdge("sibling-cross", innerA, innerB);

        // Assert: the edge lives at the root yet references descendants of different siblings
        Assert.Single(graph.Edges);
        Assert.Same(innerA, cross.Source);
        Assert.Same(innerB, cross.Target);
        Assert.DoesNotContain(innerA, graph.Nodes);
        Assert.DoesNotContain(innerB, graph.Nodes);
    }
}
