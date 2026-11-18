// <copyright file="OrderRankPlumbingTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests for the internal order-rank hint (<see cref="HierarchicalLayoutAlgorithm.OrderRanks"/>) that
///     lets the hierarchical engine steer the order of nodes a leaf algorithm places without reordering or
///     mutating the caller's graph: each bundled algorithm honors the hint, and the hint's node keys are
///     translated whenever an algorithm builds a copy of the graph.
/// </summary>
public sealed class OrderRankPlumbingTests
{
    /// <summary>
    ///     Proves the layered algorithm places unconstrained siblings in rank order rather than insertion
    ///     order.
    /// </summary>
    [Fact]
    public void LayeredLayoutAlgorithm_OrderRanks_OrdersSiblingsByRank()
    {
        // Arrange: a fork p -> {q, r, s}; the ranks reverse the insertion order of the leaves.
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Right);
        var p = Add(graph, "p");
        var q = Add(graph, "q");
        var r = Add(graph, "r");
        var s = Add(graph, "s");
        graph.AddEdge("p-q", p, q);
        graph.AddEdge("p-r", p, r);
        graph.AddEdge("p-s", p, s);
        var options = WithRanks(new Dictionary<LayoutGraphNode, double> { [s] = 0, [r] = 1, [q] = 2 });

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, options);

        // Assert: s above r above q
        Assert.True(Top(tree, "s") < Top(tree, "r"));
        Assert.True(Top(tree, "r") < Top(tree, "q"));
    }

    /// <summary>
    ///     Proves a layered layout with no hint keeps the insertion order (the control for the test above).
    /// </summary>
    [Fact]
    public void LayeredLayoutAlgorithm_NoOrderRanks_KeepsInsertionOrder()
    {
        // Arrange
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Right);
        var p = Add(graph, "p");
        var q = Add(graph, "q");
        var r = Add(graph, "r");
        graph.AddEdge("p-q", p, q);
        graph.AddEdge("p-r", p, r);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert
        Assert.True(Top(tree, "q") < Top(tree, "r"));
    }

    /// <summary>
    ///     Proves the containment algorithm packs its boxes in rank order while still emitting one box per
    ///     input node in input order.
    /// </summary>
    [Fact]
    public void ContainmentLayoutAlgorithm_OrderRanks_PacksInRankOrderKeepingInputBoxOrder()
    {
        // Arrange: three peers; ranks pack n2 first, then n0, then n1.
        var graph = new LayoutGraph();
        var n0 = Add(graph, "n0");
        var n1 = Add(graph, "n1");
        var n2 = Add(graph, "n2");
        var options = WithRanks(new Dictionary<LayoutGraphNode, double> { [n2] = 0, [n0] = 1, [n1] = 2 });

        // Act
        var tree = new ContainmentLayoutAlgorithm().ApplyCore(graph, options);

        // Assert: boxes stay in input order, but the pack order follows the ranks
        Assert.Equal(["n0", "n1", "n2"], tree.Nodes.OfType<LayoutBox>().Select(b => b.Label).ToList());
        Assert.True(Precedes(tree, "n2", "n0"));
        Assert.True(Precedes(tree, "n0", "n1"));
    }

    /// <summary>
    ///     Proves the containment algorithm keeps its legacy packing when no hint is supplied.
    /// </summary>
    [Fact]
    public void ContainmentLayoutAlgorithm_NoOrderRanks_PacksInInputOrder()
    {
        // Arrange
        var graph = new LayoutGraph();
        Add(graph, "n0");
        Add(graph, "n1");
        Add(graph, "n2");

        // Act
        var tree = new ContainmentLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert
        Assert.True(Precedes(tree, "n0", "n1"));
        Assert.True(Precedes(tree, "n1", "n2"));
    }

    /// <summary>
    ///     Proves the auto algorithm's single-group fast path (the original graph is delegated unchanged)
    ///     honors the hint.
    /// </summary>
    [Fact]
    public void AutoLayoutAlgorithm_OrderRanks_SingleGroupFastPath_HonorsRanks()
    {
        // Arrange: one connected group, so the graph is delegated unchanged to the layered algorithm.
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Right);
        var p = Add(graph, "p");
        var q = Add(graph, "q");
        var r = Add(graph, "r");
        graph.AddEdge("p-q", p, q);
        graph.AddEdge("p-r", p, r);
        var options = WithRanks(new Dictionary<LayoutGraphNode, double> { [r] = 0, [q] = 1 });

        // Act
        var tree = new AutoLayoutAlgorithm().ApplyCore(graph, options);

        // Assert
        Assert.True(Top(tree, "r") < Top(tree, "q"));
    }

    /// <summary>
    ///     Proves the auto algorithm's multi-group path orders the packed components, the members of each
    ///     component, and the singleton bucket by rank, translating the hint's keys onto the node copies it
    ///     lays out (a rank keyed by an original node must still steer its copy).
    /// </summary>
    [Fact]
    public void AutoLayoutAlgorithm_OrderRanks_MultiGroupRoot_OrdersComponentsMembersAndSingletons()
    {
        // Arrange: two connected components (a -> b) and (c -> d) plus two singleton boxes.
        var graph = BuildMultiGroupGraph(out var nodes);
        var ranked = new Dictionary<LayoutGraphNode, double>
        {
            [nodes["c"]] = 0,
            [nodes["d"]] = 1,
            [nodes["a"]] = 2,
            [nodes["b"]] = 3,
            [nodes["s2"]] = 4,
            [nodes["s1"]] = 5,
        };

        // Act
        var baseline = new AutoLayoutAlgorithm().ApplyCore(BuildMultiGroupGraph(out _), new LayoutOptions());
        var tree = new AutoLayoutAlgorithm().ApplyCore(graph, WithRanks(ranked));

        // Assert: the unranked control packs a-b before c-d and s1 before s2; the ranks reverse both
        Assert.True(Precedes(baseline, "a", "c"));
        Assert.True(Precedes(baseline, "s1", "s2"));
        Assert.True(Precedes(tree, "c", "a"));
        Assert.True(Precedes(tree, "s2", "s1"));
        Assert.Equal(["a", "b", "c", "d", "s1", "s2"], tree.Nodes.OfType<LayoutBox>().Select(b => b.Label).ToList());
    }

    /// <summary>
    ///     Proves the auto algorithm orders the members of a split-off layered component by rank.
    /// </summary>
    [Fact]
    public void AutoLayoutAlgorithm_OrderRanks_MultiGroupRoot_OrdersMembersOfComponent()
    {
        // Arrange: a fork p -> {q, r} next to an unrelated pair, so the graph is split and copied.
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Right);
        var p = Add(graph, "p");
        var q = Add(graph, "q");
        var r = Add(graph, "r");
        graph.AddEdge("p-q", p, q);
        graph.AddEdge("p-r", p, r);
        var x = Add(graph, "x");
        var y = Add(graph, "y");
        graph.AddEdge("x-y", x, y);
        var options = WithRanks(new Dictionary<LayoutGraphNode, double> { [p] = 0, [r] = 1, [q] = 2, [x] = 3, [y] = 4 });

        // Act
        var tree = new AutoLayoutAlgorithm().ApplyCore(graph, options);

        // Assert: r sits above q
        Assert.True(Top(tree, "r") < Top(tree, "q"));
    }

    /// <summary>
    ///     Proves the auto algorithm translates the hint's keys for nodes nested inside a container of a
    ///     split-off group: the container's children are ordered by the rank of their original nodes.
    /// </summary>
    [Fact]
    public void AutoLayoutAlgorithm_OrderRanks_MultiGroupRoot_TranslatesKeysOfNestedNodes()
    {
        // Arrange: a container with two unconnected children beside an unrelated singleton, so the
        // container group is copied into its own sub-graph.
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Right);
        var folder = Add(graph, "folder");
        var x = Add(folder.Children, "x");
        var y = Add(folder.Children, "y");
        Add(graph, "solo");
        var options = WithRanks(new Dictionary<LayoutGraphNode, double> { [y] = 0, [x] = 1 });

        // Act
        var tree = new AutoLayoutAlgorithm().ApplyCore(graph, options);

        // Assert: inside the folder box, y precedes x
        var folderBox = tree.Nodes.OfType<LayoutBox>().Single(b => b.Label == "folder");
        var inner = new LayoutTree(folderBox.Width, folderBox.Height, [.. folderBox.Children]);
        Assert.True(Precedes(inner, "y", "x"));
    }

    /// <summary>
    ///     Proves the hierarchical engine translates the hint onto its sized view, so the leaf algorithm
    ///     orders a scope that contains a container by the ranks of the original nodes.
    /// </summary>
    [Fact]
    public void HierarchicalLayoutAlgorithm_OrderRanks_TranslatedThroughSizedView()
    {
        // Arrange: container -> {a, b} at the root scope; ranks put b above a.
        var graph = new LayoutGraph();
        var container = Add(graph, "container");
        Add(container.Children, "child");
        var a = Add(graph, "a");
        var b = Add(graph, "b");
        graph.AddEdge("c-a", container, a);
        graph.AddEdge("c-b", container, b);
        var options = LayoutOptions.ForAlgorithm("layered");
        options.Set(CoreOptions.Direction, LayoutFlowDirection.Right);
        options.Set(HierarchicalLayoutAlgorithm.OrderRanks, Ranks(new Dictionary<LayoutGraphNode, double> { [b] = 0, [a] = 1 }));

        // Act
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, options);

        // Assert
        Assert.True(Top(tree, "b") < Top(tree, "a"));
    }

    /// <summary>
    ///     Proves the hint is inherited by nested scopes: the children of a container are ordered by their
    ///     original nodes' ranks.
    /// </summary>
    [Fact]
    public void HierarchicalLayoutAlgorithm_OrderRanks_InheritedByNestedScope()
    {
        // Arrange: a container whose interior is a fork p -> {q, r}; the ranks put r above q.
        var graph = new LayoutGraph();
        var container = Add(graph, "container");
        var p = Add(container.Children, "p");
        var q = Add(container.Children, "q");
        var r = Add(container.Children, "r");
        container.Children.AddEdge("p-q", p, q);
        container.Children.AddEdge("p-r", p, r);
        var options = LayoutOptions.ForAlgorithm("layered");
        options.Set(CoreOptions.Direction, LayoutFlowDirection.Right);
        options.Set(HierarchicalLayoutAlgorithm.OrderRanks, Ranks(new Dictionary<LayoutGraphNode, double> { [r] = 0, [q] = 1 }));

        // Act
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, options);

        // Assert
        var containerBox = tree.Nodes.OfType<LayoutBox>().Single(b => b.Label == "container");
        var inner = new LayoutTree(containerBox.Width, containerBox.Height, [.. containerBox.Children]);
        Assert.True(Top(inner, "r") < Top(inner, "q"));
    }

    /// <summary>
    ///     Proves ordering through the hint never mutates the caller's graph or nodes: the hint is carried
    ///     in the layout options only.
    /// </summary>
    [Fact]
    public void HierarchicalLayoutAlgorithm_OrderRanks_DoesNotMutateCallerGraph()
    {
        // Arrange
        var graph = new LayoutGraph();
        var container = Add(graph, "container");
        Add(container.Children, "child");
        var a = Add(graph, "a");
        graph.AddEdge("c-a", container, a);
        var options = LayoutOptions.ForAlgorithm("layered");
        options.Set(HierarchicalLayoutAlgorithm.OrderRanks, Ranks(new Dictionary<LayoutGraphNode, double> { [a] = 0 }));

        // Act
        new HierarchicalLayoutAlgorithm().ApplyCore(graph, options);

        // Assert: the graph, its nodes, and its edges are untouched and carry no hint
        Assert.Equal(["container", "a"], graph.Nodes.Select(n => n.Id).ToList());
        Assert.Single(graph.Edges);
        Assert.False(graph.Contains(HierarchicalLayoutAlgorithm.OrderRanks));
        Assert.All(graph.Nodes, n => Assert.False(n.Contains(HierarchicalLayoutAlgorithm.OrderRanks)));
    }

    /// <summary>
    ///     Proves a hint that names no node of the scope (or an absent hint) leaves a hierarchical layout
    ///     identical to the layout without any hint.
    /// </summary>
    [Fact]
    public void HierarchicalLayoutAlgorithm_OrderRanks_UnrelatedKeys_IdenticalToUnordered()
    {
        // Arrange: two equal graphs; the hint of one holds only a foreign node.
        LayoutGraph Build()
        {
            var graph = new LayoutGraph();
            var container = Add(graph, "container");
            Add(container.Children, "child");
            var a = Add(graph, "a");
            var b = Add(graph, "b");
            graph.AddEdge("c-a", container, a);
            graph.AddEdge("c-b", container, b);
            return graph;
        }

        var hinted = LayoutOptions.ForAlgorithm("layered");
        hinted.Set(
            HierarchicalLayoutAlgorithm.OrderRanks,
            Ranks(new Dictionary<LayoutGraphNode, double> { [new LayoutGraph().AddNode("foreign", 10, 10)] = 0 }));

        // Act
        var withoutHint = new HierarchicalLayoutAlgorithm().ApplyCore(Build(), LayoutOptions.ForAlgorithm("layered"));
        var withHint = new HierarchicalLayoutAlgorithm().ApplyCore(Build(), hinted);

        // Assert
        var expected = withoutHint.Nodes.OfType<LayoutBox>().Select(b => (b.Label, b.X, b.Y)).ToList();
        var actual = withHint.Nodes.OfType<LayoutBox>().Select(b => (b.Label, b.X, b.Y)).ToList();
        Assert.Equal(expected, actual);
        Assert.Equal(withoutHint.Width, withHint.Width);
        Assert.Equal(withoutHint.Height, withHint.Height);
    }

    /// <summary>Builds a root graph of two connected components and two singleton boxes, outputting its nodes by id.</summary>
    private static LayoutGraph BuildMultiGroupGraph(out Dictionary<string, LayoutGraphNode> nodes)
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Right);
        nodes = [];
        foreach (var id in new[] { "a", "b", "c", "d", "s1", "s2" })
        {
            nodes[id] = Add(graph, id);
        }

        graph.AddEdge("a-b", nodes["a"], nodes["b"]);
        graph.AddEdge("c-d", nodes["c"], nodes["d"]);
        return graph;
    }

    /// <summary>Adds a labeled 80 by 40 node whose label equals its id.</summary>
    private static LayoutGraphNode Add(LayoutGraph graph, string id)
    {
        var node = graph.AddNode(id, 80, 40);
        node.Label = id;
        return node;
    }

    /// <summary>Wraps a rank dictionary as the typed hint value.</summary>
    private static IReadOnlyDictionary<LayoutGraphNode, double>? Ranks(Dictionary<LayoutGraphNode, double> ranks) => ranks;

    /// <summary>Builds options carrying the order-rank hint.</summary>
    private static LayoutOptions WithRanks(Dictionary<LayoutGraphNode, double> ranks)
    {
        var options = new LayoutOptions();
        options.Set(HierarchicalLayoutAlgorithm.OrderRanks, Ranks(ranks));
        return options;
    }

    /// <summary>Finds the top-left corner of the box labeled <paramref name="label"/> in <paramref name="tree"/>.</summary>
    private static (double Y, double X) Corner(LayoutTree tree, string label)
    {
        var box = tree.Nodes.OfType<LayoutBox>().Single(b => b.Label == label);
        return (box.Y, box.X);
    }

    /// <summary>Gets the top edge of the box labeled <paramref name="label"/>.</summary>
    private static double Top(LayoutTree tree, string label) => Corner(tree, label).Y;

    /// <summary>
    ///     Determines whether <paramref name="first"/> precedes <paramref name="second"/> in packing order:
    ///     higher on the canvas, or on the same row and further left.
    /// </summary>
    private static bool Precedes(LayoutTree tree, string first, string second)
    {
        var a = Corner(tree, first);
        var b = Corner(tree, second);
        return a.Y < b.Y - 0.5 || (Math.Abs(a.Y - b.Y) <= 0.5 && a.X < b.X);
    }
}
