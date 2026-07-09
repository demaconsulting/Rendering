// <copyright file="MergeRegionGraphAssemblerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Tests.Engine.Layered;

/// <summary>
///     Tests for <see cref="MergeRegionGraphAssembler"/>: the structural assembly of a merge region into
///     nested <see cref="MergeRegionLevel"/>s, enforcing full leaf-level flattening (a container's entire
///     interior participates, not a boundary skeleton) and arbitrary delegation-chain depth.
/// </summary>
public sealed class MergeRegionGraphAssemblerTests
{
    /// <summary>
    ///     A single boundary-port container produces exactly one nested child level, and that level
    ///     retains the container's full interior: every interior node in <see cref="MergeRegionLevel.Nodes"/>
    ///     and every ordinary interior edge in <see cref="MergeRegionLevel.Edges"/>, with the delegation
    ///     edge captured as the child level's incoming boundary crossing.
    /// </summary>
    [Fact]
    public void MergeRegionGraphAssembler_Assemble_SingleLevelBoundaryPort_ProducesOneChildLevel()
    {
        // Arrange: scope { A, B(port p) }, external A->p; B.Children { C, D } with delegation p->C and
        // an ordinary interior edge C->D.
        var scope = new LayoutGraph();
        var a = scope.AddNode("A", 80, 40);
        var b = scope.AddNode("B", 10, 10);
        var p = b.Ports.AddPort("p");
        var c = b.Children.AddNode("C", 80, 40);
        var d = b.Children.AddNode("D", 80, 40);
        scope.AddEdge("a-p", a, p);
        var delegation = b.Children.AddEdge("p-c", p, c);
        var interior = b.Children.AddEdge("c-d", c, d);
        var boundaries = HierarchyMergeRegionBuilder.Collect(scope);

        // Act
        var region = MergeRegionGraphAssembler.Assemble(scope, boundaries, EmptySize());

        // Assert: exactly one child level for the single boundary-port container B.
        var child = Assert.Single(region.Root.Children);
        Assert.Same(b, child.Container);
        Assert.Same(b.Children, child.Child.Scope);

        // Assert: full interior node set is retained (both C and D, not a boundary skeleton).
        Assert.Equal(new[] { c, d }, child.Child.Nodes);

        // Assert: the ordinary interior edge is retained; the delegation edge is not an ordinary edge.
        Assert.Contains(interior, child.Child.Edges);
        Assert.DoesNotContain(delegation, child.Child.Edges);

        // Assert: the delegation edge is captured as the child level's incoming boundary crossing.
        Assert.NotNull(child.Child.IncomingBoundary);
        Assert.Same(p, child.Child.IncomingBoundary!.Port);
        Assert.Contains(delegation, child.Child.IncomingBoundary.InternalEdges);
    }

    /// <summary>
    ///     A three-level delegation chain — container A's boundary port delegates to nested container B's
    ///     boundary port, which delegates to nested container C's boundary port, which delegates to a leaf
    ///     child — assembles three nested <see cref="MergeRegionLevel"/>s, each retaining its own interior
    ///     nodes and edges, proving the assembler recurses to arbitrary depth.
    /// </summary>
    [Fact]
    public void MergeRegionGraphAssembler_Assemble_ThreeLevelChain_RecursesToDepthThree()
    {
        // Arrange: root { Outer, A(port pa) }; external Outer->pa.
        var root = new LayoutGraph();
        var outer = root.AddNode("Outer", 80, 40);
        var a = root.AddNode("A", 10, 10);
        var pa = a.Ports.AddPort("pa");
        root.AddEdge("outer-pa", outer, pa);

        // A.Children { B(port pb), M1 }; delegation pa->pb; ordinary interior M1 alongside the container.
        var b = a.Children.AddNode("B", 10, 10);
        var pb = b.Ports.AddPort("pb");
        var m1 = a.Children.AddNode("M1", 30, 30);
        a.Children.AddEdge("pa-pb", pa, pb);

        // B.Children { C(port pc), M2 }; delegation pb->pc; ordinary interior M2.
        var c = b.Children.AddNode("C", 10, 10);
        var pc = c.Ports.AddPort("pc");
        var m2 = b.Children.AddNode("M2", 30, 30);
        b.Children.AddEdge("pb-pc", pb, pc);

        // C.Children { Leaf, M3 }; delegation pc->Leaf; ordinary interior M3.
        var leaf = c.Children.AddNode("Leaf", 80, 40);
        var m3 = c.Children.AddNode("M3", 30, 30);
        c.Children.AddEdge("pc-leaf", pc, leaf);

        var boundaries = HierarchyMergeRegionBuilder.Collect(root);

        // Act
        var region = MergeRegionGraphAssembler.Assemble(root, boundaries, EmptySize());

        // Assert: level 1 (root) retains its own interior and nests container A.
        var level1 = region.Root;
        Assert.Same(root, level1.Scope);
        var childA = Assert.Single(level1.Children);
        Assert.Same(a, childA.Container);

        // Assert: level 2 (A.Children) retains ordinary interior node M1 and nests container B.
        var level2 = childA.Child;
        Assert.Same(a.Children, level2.Scope);
        Assert.Contains(m1, level2.Nodes);
        var childB = Assert.Single(level2.Children);
        Assert.Same(b, childB.Container);

        // Assert: level 3 (B.Children) retains ordinary interior node M2 and nests container C.
        var level3 = childB.Child;
        Assert.Same(b.Children, level3.Scope);
        Assert.Contains(m2, level3.Nodes);
        var childC = Assert.Single(level3.Children);
        Assert.Same(c, childC.Container);

        // Assert: level 4 (C.Children) is the innermost — it retains the leaf and interior M3 and nests
        // no further container, confirming exactly three delegation levels below the root scope.
        var level4 = childC.Child;
        Assert.Same(c.Children, level4.Scope);
        Assert.Contains(leaf, level4.Nodes);
        Assert.Contains(m3, level4.Nodes);
        Assert.Empty(level4.Children);
    }

    /// <summary>
    ///     An ordinary (non-boundary) interior node of a merge-region container appears in the assembled
    ///     level's <see cref="MergeRegionLevel.Nodes"/> list — the full-flattening guardrail that the
    ///     interior is not filtered down to a boundary-port skeleton subset.
    /// </summary>
    [Fact]
    public void MergeRegionGraphAssembler_Assemble_NonBoundaryInteriorNode_IncludedInFullFlattening()
    {
        // Arrange: scope { A, B(port p) }, external A->p; B.Children { Target, Bystander } where the
        // delegation p->Target reaches Target and Bystander is an ordinary interior node touched by no
        // boundary crossing at all.
        var scope = new LayoutGraph();
        var a = scope.AddNode("A", 80, 40);
        var b = scope.AddNode("B", 10, 10);
        var p = b.Ports.AddPort("p");
        var target = b.Children.AddNode("Target", 80, 40);
        var bystander = b.Children.AddNode("Bystander", 60, 30);
        scope.AddEdge("a-p", a, p);
        b.Children.AddEdge("p-target", p, target);
        var boundaries = HierarchyMergeRegionBuilder.Collect(scope);

        // Act
        var region = MergeRegionGraphAssembler.Assemble(scope, boundaries, EmptySize());

        // Assert: the ordinary, boundary-unrelated interior node is present in the flattened level.
        var child = Assert.Single(region.Root.Children);
        Assert.Contains(bystander, child.Child.Nodes);
        Assert.True(child.Child.NodeIndex.ContainsKey(bystander));
    }

    /// <summary>
    ///     Creates an empty effective-size lookup, so every node falls back to its own declared
    ///     dimensions during assembly.
    /// </summary>
    /// <returns>An empty node-to-size lookup.</returns>
    private static IReadOnlyDictionary<LayoutGraphNode, (double Width, double Height)> EmptySize()
    {
        return new Dictionary<LayoutGraphNode, (double Width, double Height)>();
    }
}
