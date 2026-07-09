// <copyright file="HierarchyMergeRegionBuilderTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Tests.Engine.Layered;

/// <summary>
///     Tests for <see cref="HierarchyMergeRegionBuilder"/>: structural detection of boundary
///     (delegation) ports per scope, the empty-result gate for boundary-port-free scopes, and the
///     transitive union-of-chains collection across a whole hierarchy.
/// </summary>
public sealed class HierarchyMergeRegionBuilderTests
{
    /// <summary>
    ///     A scope with no ports at all yields no boundary ports, so callers can gate all new recursive
    ///     behavior behind a non-empty result and keep boundary-port-free graphs on their old path.
    /// </summary>
    [Fact]
    public void Collect_NoPorts_ReturnsEmpty()
    {
        // Arrange: a container with a plain child and no ports.
        var scope = new LayoutGraph();
        var b = scope.AddNode("B", 10, 10);
        b.Children.AddNode("C", 80, 40);

        // Act
        var boundaries = HierarchyMergeRegionBuilder.Collect(scope);

        // Assert
        Assert.Empty(boundaries);
    }

    /// <summary>
    ///     A plain (same-scope) port whose edge lives in the owner's own scope is <em>not</em> a
    ///     boundary port: only a port with an edge one level down in the owner's children is detected.
    /// </summary>
    [Fact]
    public void Collect_SameScopePort_NotDetectedAsBoundary()
    {
        // Arrange: node B has a port whose only edge is in B's own scope (B->port style, same scope).
        var scope = new LayoutGraph();
        var a = scope.AddNode("A", 80, 40);
        var b = scope.AddNode("B", 10, 10);
        var port = b.Ports.AddPort("p");

        // A plain same-scope port edge, plus a child so B is a container (still no inward edge to p).
        scope.AddEdge("a-p", a, port);
        b.Children.AddNode("C", 80, 40);

        // Act
        var boundaries = HierarchyMergeRegionBuilder.Collect(scope);

        // Assert: no inward delegation edge references the port, so it is not a boundary port.
        Assert.Empty(boundaries);
    }

    /// <summary>
    ///     A container port with an inward delegation edge in its own children is detected as a
    ///     boundary port, capturing both its external approach edges and its internal delegation edges.
    /// </summary>
    [Fact]
    public void Collect_DelegationPort_DetectedWithExternalAndInternalEdges()
    {
        // Arrange: B owns port P; external edge A->P in scope; internal edge P->C in B's children.
        var scope = new LayoutGraph();
        var a = scope.AddNode("A", 80, 40);
        var b = scope.AddNode("B", 10, 10);
        var port = b.Ports.AddPort("p");
        var c = b.Children.AddNode("C", 80, 40);
        var external = scope.AddEdge("a-p", a, port);
        var internalEdge = b.Children.AddEdge("p-c", port, c);

        // Act
        var boundaries = HierarchyMergeRegionBuilder.Collect(scope);

        // Assert
        var boundary = Assert.Single(boundaries);
        Assert.Same(port, boundary.Port);
        Assert.Same(b, boundary.Container);
        Assert.Same(external, Assert.Single(boundary.ExternalEdges));
        Assert.Same(internalEdge, Assert.Single(boundary.InternalEdges));
    }

    /// <summary>
    ///     Two independent boundary ports on one container are both detected in a single scope pass, in
    ///     deterministic port-insertion order.
    /// </summary>
    [Fact]
    public void Collect_TwoIndependentPorts_DetectsBoth()
    {
        // Arrange: container B owns two delegation ports, each with its own inward edge.
        var scope = new LayoutGraph();
        var b = scope.AddNode("B", 10, 10);
        var p = b.Ports.AddPort("p");
        var q = b.Ports.AddPort("q");
        var c1 = b.Children.AddNode("C1", 80, 40);
        var c2 = b.Children.AddNode("C2", 80, 40);
        b.Children.AddEdge("p-c1", p, c1);
        b.Children.AddEdge("q-c2", q, c2);

        // Act
        var boundaries = HierarchyMergeRegionBuilder.Collect(scope);

        // Assert: both ports, in insertion order.
        Assert.Equal(2, boundaries.Count);
        Assert.Same(p, boundaries[0].Port);
        Assert.Same(q, boundaries[1].Port);
    }

    /// <summary>
    ///     The transitive collection walks every nesting level so a three-level delegation chain — a
    ///     top port delegating to a mid container's port delegating to a leaf — is reported as two
    ///     boundary ports (the top and mid ports) across two scopes, proving the merge region is
    ///     depth-unbounded by construction with no fixed two-level cap.
    /// </summary>
    [Fact]
    public void CollectRecursive_ThreeLevelChain_ReportsEveryLevel()
    {
        // Arrange: root scope has container B1 (port p1) -> B2 (port p2) -> leaf C.
        //   external:  A -> p1                (root scope)
        //   delegate:  p1 -> p2               (B1.Children)   [p2 is B2's boundary port]
        //   delegate:  p2 -> C                (B2.Children)
        var root = new LayoutGraph();
        var a = root.AddNode("A", 80, 40);
        var b1 = root.AddNode("B1", 10, 10);
        var p1 = b1.Ports.AddPort("p1");

        var b2 = b1.Children.AddNode("B2", 10, 10);
        var p2 = b2.Ports.AddPort("p2");
        root.AddEdge("a-p1", a, p1);
        b1.Children.AddEdge("p1-p2", p1, p2);

        var c = b2.Children.AddNode("C", 80, 40);
        b2.Children.AddEdge("p2-c", p2, c);

        // Act
        var all = HierarchyMergeRegionBuilder.CollectRecursive(root);

        // Assert: p1 detected in the root scope, p2 detected in B1's child scope.
        Assert.Equal(2, all.Count);
        Assert.Same(root, all[0].Scope);
        Assert.Same(p1, all[0].Boundary.Port);
        Assert.Same(b1.Children, all[1].Scope);
        Assert.Same(p2, all[1].Boundary.Port);
    }

    /// <summary>
    ///     A leaf (childless) node that owns ports never yields a boundary port, because it has no
    ///     child scope to delegate into.
    /// </summary>
    [Fact]
    public void Collect_PortOnLeafNode_NotDetected()
    {
        // Arrange: a plain node with a port but no children.
        var scope = new LayoutGraph();
        var node = scope.AddNode("N", 80, 40);
        node.Ports.AddPort("p");

        // Act
        var boundaries = HierarchyMergeRegionBuilder.Collect(scope);

        // Assert
        Assert.Empty(boundaries);
    }
}
