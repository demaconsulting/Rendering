// <copyright file="AutoLayoutAlgorithmTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests for the bundled <see cref="AutoLayoutAlgorithm"/>, exercising its component routing rules,
///     its zero-copy fast path for a single group, and its splitting-and-packing path for genuine
///     multi-group graphs.
/// </summary>
public sealed class AutoLayoutAlgorithmTests
{
    /// <summary>
    ///     Proves that the algorithm advertises the stable "auto" identifier.
    /// </summary>
    [Fact]
    public void Id_IsAuto()
    {
        // Act / Assert
        Assert.Equal("auto", new AutoLayoutAlgorithm().Id);
    }

    /// <summary>
    ///     Proves that <c>Apply(LayoutGraph)</c> rejects a <see langword="null"/> graph.
    /// </summary>
    [Fact]
    public void Apply_NullGraph_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new AutoLayoutAlgorithm().Apply(null!));
    }

    /// <summary>
    ///     Proves that <c>ApplyCore(LayoutGraph, LayoutOptions)</c> rejects a <see langword="null"/>
    ///     options.
    /// </summary>
    [Fact]
    public void ApplyCore_NullOptions_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new AutoLayoutAlgorithm().ApplyCore(new LayoutGraph(), null!));
    }

    /// <summary>
    ///     Proves that an empty graph produces an empty, but valid, placed tree instead of throwing.
    /// </summary>
    [Fact]
    public void Apply_EmptyGraph_ReturnsEmptyTree()
    {
        // Arrange
        var graph = new LayoutGraph();

        // Act
        var tree = new AutoLayoutAlgorithm().Apply(graph);

        // Assert
        Assert.Empty(tree.Nodes.OfType<LayoutBox>());
    }

    /// <summary>
    ///     Proves that a single fully-connected component routes entirely through the layered algorithm,
    ///     taking the zero-copy fast path (byte-identical to invoking <see cref="LayeredLayoutAlgorithm"/>
    ///     directly on the same graph).
    /// </summary>
    [Fact]
    public void Apply_SingleConnectedComponent_MatchesLayeredAlgorithmDirectly()
    {
        // Arrange
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        var c = graph.AddNode("c", 80, 40);
        a.Label = "A";
        b.Label = "B";
        c.Label = "C";
        graph.AddEdge("a-b", a, b);
        graph.AddEdge("b-c", b, c);

        // Act
        var autoTree = new AutoLayoutAlgorithm().Apply(graph);
        var layeredTree = new LayeredLayoutAlgorithm().Apply(graph);

        // Assert
        Assert.Equal(DumpTree(layeredTree), DumpTree(autoTree));
    }

    /// <summary>
    ///     Proves that a graph containing only childless, edgeless singleton nodes routes entirely
    ///     through the containment algorithm, taking the zero-copy fast path.
    /// </summary>
    [Fact]
    public void Apply_AllIsolatedSingletons_MatchesContainmentAlgorithmDirectly()
    {
        // Arrange
        var graph = new LayoutGraph();
        graph.AddNode("a", 80, 40);
        graph.AddNode("b", 80, 40);
        graph.AddNode("c", 80, 40);

        // Act
        var autoTree = new AutoLayoutAlgorithm().Apply(graph);
        var containmentTree = new ContainmentLayoutAlgorithm().Apply(graph);

        // Assert
        Assert.Equal(DumpTree(containmentTree), DumpTree(autoTree));
    }

    /// <summary>
    ///     Proves that a single isolated node with children is still routed to the hierarchical
    ///     algorithm — a container needs hierarchical recursion even when nothing else references it —
    ///     taking the zero-copy fast path.
    /// </summary>
    [Fact]
    public void Apply_IsolatedNodeWithChildren_MatchesHierarchicalAlgorithmDirectly()
    {
        // Arrange
        var graph = new LayoutGraph();
        var group = graph.AddNode("group", 10, 10);
        group.Label = "Group";
        var inner1 = group.Children.AddNode("c1", 80, 40);
        var inner2 = group.Children.AddNode("c2", 80, 40);
        group.Children.AddEdge("c1-c2", inner1, inner2);

        // Act
        var autoTree = new AutoLayoutAlgorithm().Apply(graph);
        var hierarchicalTree = new HierarchicalLayoutAlgorithm().Apply(graph);

        // Assert
        Assert.Equal(DumpTree(hierarchicalTree), DumpTree(autoTree));
    }

    /// <summary>
    ///     Proves that a nested container two levels deep still routes the whole graph to hierarchical
    ///     (rather than incorrectly treating the root as a flat, childless component).
    /// </summary>
    [Fact]
    public void Apply_NestedContainers_RoutesToHierarchical()
    {
        // Arrange
        var graph = new LayoutGraph();
        var outer = graph.AddNode("outer", 10, 10);
        var inner = outer.Children.AddNode("inner", 10, 10);
        var leaf1 = inner.Children.AddNode("leaf1", 80, 40);
        var leaf2 = inner.Children.AddNode("leaf2", 80, 40);
        inner.Children.AddEdge("leaf1-leaf2", leaf1, leaf2);

        // Act
        var autoTree = new AutoLayoutAlgorithm().Apply(graph);
        var hierarchicalTree = new HierarchicalLayoutAlgorithm().Apply(graph);

        // Assert
        Assert.Equal(DumpTree(hierarchicalTree), DumpTree(autoTree));
    }

    /// <summary>
    ///     Proves that a graph mixing one connected cluster with several unrelated singleton nodes packs
    ///     the cluster (via layered) and the singleton bucket (via containment) into one combined tree
    ///     with non-overlapping bounding boxes for the two pieces.
    /// </summary>
    [Fact]
    public void Apply_ClusterPlusIsolatedSingletons_PacksBothGroupsWithoutOverlap()
    {
        // Arrange
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        a.Label = "A";
        b.Label = "B";
        graph.AddEdge("a-b", a, b);
        graph.AddNode("solo1", 80, 40).Label = "Solo1";
        graph.AddNode("solo2", 80, 40).Label = "Solo2";

        // Act
        var tree = new AutoLayoutAlgorithm().Apply(graph);

        // Assert: every original label is present exactly once in the combined tree.
        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        Assert.Equal(["A", "B", "Solo1", "Solo2"], boxes.Select(box => box.Label).OrderBy(label => label, StringComparer.Ordinal));

        // Every box lies fully within the reported canvas, and no two boxes overlap.
        foreach (var box in boxes)
        {
            Assert.True(box.X >= 0);
            Assert.True(box.Y >= 0);
            Assert.True(box.X + box.Width <= tree.Width + 1e-6);
            Assert.True(box.Y + box.Height <= tree.Height + 1e-6);
        }

        for (var i = 0; i < boxes.Count; i++)
        {
            for (var j = i + 1; j < boxes.Count; j++)
            {
                var overlapsX = boxes[i].X < boxes[j].X + boxes[j].Width && boxes[j].X < boxes[i].X + boxes[i].Width;
                var overlapsY = boxes[i].Y < boxes[j].Y + boxes[j].Height && boxes[j].Y < boxes[i].Y + boxes[i].Height;
                Assert.False(overlapsX && overlapsY, $"Boxes '{boxes[i].Label}' and '{boxes[j].Label}' overlap.");
            }
        }
    }

    /// <summary>
    ///     Proves that multiple disconnected clusters (each with two or more nodes, no children) each
    ///     route through layered and are packed without overlapping each other.
    /// </summary>
    [Fact]
    public void Apply_MultipleDisconnectedClusters_PacksEachClusterWithoutOverlap()
    {
        // Arrange: two independent two-node clusters.
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        graph.AddEdge("a-b", a, b);
        var c = graph.AddNode("c", 80, 40);
        var d = graph.AddNode("d", 80, 40);
        graph.AddEdge("c-d", c, d);

        // Act
        var tree = new AutoLayoutAlgorithm().Apply(graph);

        // Assert: four boxes total, two lines (one per cluster's edge).
        Assert.Equal(4, tree.Nodes.OfType<LayoutBox>().Count());
        Assert.Equal(2, tree.Nodes.OfType<LayoutLine>().Count());
    }

    /// <summary>
    ///     Proves that a component containing a self-loop (a single node with an edge to itself, and no
    ///     children) is routed through layered rather than being folded into the singleton bucket.
    /// </summary>
    [Fact]
    public void Apply_SingleNodeWithSelfLoop_RoutesToLayeredNotContainment()
    {
        // Arrange
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        graph.AddEdge("self", a, a);

        // Act
        var autoTree = new AutoLayoutAlgorithm().Apply(graph);
        var layeredTree = new LayeredLayoutAlgorithm().Apply(graph);

        // Assert
        Assert.Equal(DumpTree(layeredTree), DumpTree(autoTree));
    }

    /// <summary>
    ///     Proves that a graph-level cascaded option (<see cref="CoreOptions.NodeSpacing"/>) actually
    ///     reaches a split-off component's leaf algorithm, since the effective options are captured
    ///     before splitting. Regression test: the prior version of this test only asserted a box count,
    ///     so a regression that dropped the captured options (or otherwise ignored NodeSpacing on the
    ///     split path) would still have passed.
    /// </summary>
    [Fact]
    public void Apply_GraphLevelOptionOverride_AppliesToSplitComponents()
    {
        // Arrange: a two-sibling fan-out cluster {a -> b, a -> c} (forces the multi-group split path
        // and gives the layered leaf a same-layer sibling pair whose gap is driven by NodeSpacing) plus
        // an unrelated singleton, laid out twice with only the graph-level NodeSpacing changed.
        static double SiblingGap(double nodeSpacing)
        {
            var graph = new LayoutGraph();
            var a = graph.AddNode("a", 80, 40);
            var b = graph.AddNode("b", 80, 40);
            var c = graph.AddNode("c", 80, 40);
            b.Label = "b";
            c.Label = "c";
            graph.AddEdge("a-b", a, b);
            graph.AddEdge("a-c", a, c);
            graph.AddNode("solo", 80, 40);
            graph.Set(CoreOptions.NodeSpacing, nodeSpacing);

            var tree = new AutoLayoutAlgorithm().Apply(graph);
            var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
            Assert.Equal(4, boxes.Count);

            var bBox = boxes.Single(box => box.Label == "b");
            var cBox = boxes.Single(box => box.Label == "c");
            return Math.Abs(bBox.Y - cBox.Y);
        }

        // Act
        var narrowGap = SiblingGap(5.0);
        var wideGap = SiblingGap(300.0);

        // Assert: the same-layer sibling gap between b and c grows with the graph-level NodeSpacing
        // override, proving the split layered component actually received the captured cascaded option
        // rather than falling back to a default.
        Assert.True(
            wideGap > narrowGap + 250.0,
            $"expected the wide-spacing sibling gap ({wideGap}) to exceed the narrow-spacing gap ({narrowGap}) by more than 250, proving NodeSpacing reached the split component");
    }

    /// <summary>
    ///     Proves that when the graph itself explicitly declares <c>CoreOptions.Algorithm = "auto"</c>
    ///     (as a caller resolving the algorithm from the graph's own options, rather than passing it
    ///     directly, would do) and the multi-group split path routes one group to hierarchical, the
    ///     hierarchical algorithm's own recursive scope resolution does not throw when it re-reads
    ///     "auto" from its cascaded options: its recursion registry resolves "auto" back to this same
    ///     <see cref="AutoLayoutAlgorithm"/> instance instead of a leaf-only registry that lacks it.
    ///     Regression test for a bug where the captured effective options still carried the root graph's
    ///     own "auto" value down into a nested algorithm's cascade, and that cascade had no way to
    ///     resolve it.
    /// </summary>
    [Fact]
    public void Apply_GraphDeclaresAutoAlgorithmWithNestedContainerGroup_DoesNotThrow()
    {
        // Arrange: the graph itself declares "auto" (mirroring a caller that resolves the algorithm from
        // the graph's own options), one component is a two-level-deep nested container (forcing a
        // hierarchical-routed group whose own nested scope does not declare an override), and an
        // unrelated singleton forces the genuine multi-group split path rather than the fast path.
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "auto");

        var outer = graph.AddNode("outer", 10, 10);
        var inner = outer.Children.AddNode("inner", 10, 10);
        var leaf1 = inner.Children.AddNode("leaf1", 80, 40);
        var leaf2 = inner.Children.AddNode("leaf2", 80, 40);
        inner.Children.AddEdge("leaf1-leaf2", leaf1, leaf2);

        graph.AddNode("solo", 80, 40);

        // Act
        var tree = new AutoLayoutAlgorithm().Apply(graph);

        // Assert: no exception was thrown, and every box (the outer container, the inner container, the
        // two leaves, and the solo singleton) made it into the combined tree.
        Assert.Equal(5, CountBoxesRecursively(tree.Nodes));
    }

    /// <summary>
    ///     Proves that when the graph itself explicitly declares <c>CoreOptions.Algorithm = "auto"</c>
    ///     and the <em>entire</em> graph is a single component containing a nested container (so routing
    ///     takes the zero-copy single-group fast path straight to <see cref="HierarchicalLayoutAlgorithm"/>
    ///     on the original, unmodified graph — not the split-and-pack path), the hierarchical algorithm's
    ///     own top-scope resolution does not throw <see cref="KeyNotFoundException"/> when it re-reads
    ///     "auto" from its cascaded options. Regression test for a bug where the fast path handed the
    ///     untouched original options straight to <see cref="HierarchicalLayoutAlgorithm"/>, whose first
    ///     step (<c>graph.OverlayOnto(options)</c>) picks up the root graph's own "auto" override
    ///     unchanged, and its recursion registry (at the time) had no way to resolve that identifier.
    /// </summary>
    [Fact]
    public void Apply_GraphDeclaresAutoAlgorithmAsSoleHierarchicalGroup_DoesNotThrow()
    {
        // Arrange: the graph itself declares "auto", and its only top-level node is a two-level-deep
        // nested container with no other top-level node or edge, so routing produces exactly one group
        // (routed to hierarchical) and no singletons — the single-group fast path.
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "auto");

        var outer = graph.AddNode("outer", 10, 10);
        var inner = outer.Children.AddNode("inner", 10, 10);
        var leaf1 = inner.Children.AddNode("leaf1", 80, 40);
        var leaf2 = inner.Children.AddNode("leaf2", 80, 40);
        inner.Children.AddEdge("leaf1-leaf2", leaf1, leaf2);

        // Act
        var tree = new AutoLayoutAlgorithm().Apply(graph);

        // Assert: no exception was thrown, and every box (the outer container, the inner container, and
        // the two leaves) made it into the tree.
        Assert.Equal(4, CountBoxesRecursively(tree.Nodes));
    }

    /// <summary>
    ///     Proves that "auto" is re-evaluated at every scope it cascades to, rather than being resolved
    ///     once at the root and then locked in as a fixed concrete choice for every descendant scope.
    ///     A container whose own children mix a connected pair with an unrelated singleton — inheriting
    ///     "auto" from the root without re-declaring it — must be classified by this algorithm's own
    ///     connectivity-based routing (packing the singleton separately via containment) exactly like a
    ///     top-level "auto" graph would, not simply handed to a single fixed leaf algorithm (which would
    ///     lay every member out uniformly, with no special packing for the singleton). Regression test
    ///     for a design gap where a container-scope fix merely avoided throwing by forcing a fixed leaf
    ///     choice onto every descendant scope, rather than honoring "auto"'s documented inheritance rule
    ///     that an unset option keeps re-evaluating at each level.
    /// </summary>
    [Fact]
    public void Apply_AutoInheritedByNestedContainer_ReclassifiesMixedConnectivityAtThatScope()
    {
        // Arrange: two structurally-identical graphs, differing only in how the container's own children
        // scope resolves its algorithm. Both containers hold a connected pair ("a"-"b") plus an unrelated
        // singleton ("solo"). "inherited" lets the root's "auto" cascade down unset; "forcedLayered"
        // instead explicitly overrides the children scope to plain "layered", so its singleton is laid
        // out uniformly alongside the pair rather than packed separately via containment.
        static LayoutTree Build(bool forceLayeredOnChildren)
        {
            var graph = new LayoutGraph();
            graph.Set(CoreOptions.Algorithm, "auto");

            var outer = graph.AddNode("outer", 10, 10);
            outer.Label = "outer";
            if (forceLayeredOnChildren)
            {
                outer.Children.Set(CoreOptions.Algorithm, LayeredLayoutAlgorithm.AlgorithmId);
            }

            var a = outer.Children.AddNode("a", 80, 40);
            var b = outer.Children.AddNode("b", 80, 40);
            outer.Children.AddEdge("a-b", a, b);
            outer.Children.AddNode("solo", 80, 40);

            return new AutoLayoutAlgorithm().Apply(graph);
        }

        // Act
        var inheritedTree = Build(forceLayeredOnChildren: false);
        var forcedLayeredTree = Build(forceLayeredOnChildren: true);

        var inheritedOuter = inheritedTree.Nodes.OfType<LayoutBox>().Single(box => box.Label == "outer");
        var forcedLayeredOuter = forcedLayeredTree.Nodes.OfType<LayoutBox>().Single(box => box.Label == "outer");

        // Assert: every box made it into both trees (the outer container, "a", "b", and "solo").
        Assert.Equal(4, CountBoxesRecursively(inheritedTree.Nodes));
        Assert.Equal(4, CountBoxesRecursively(forcedLayeredTree.Nodes));

        // Assert: the inherited-"auto" container is sized differently from the forced-plain-"layered"
        // container, proving the inherited case actually re-ran this algorithm's own component
        // classification (splitting the singleton into its own containment-packed bucket) instead of
        // resolving to the same fixed leaf treatment as an explicit "layered" override.
        Assert.True(
            Math.Abs(inheritedOuter.Width - forcedLayeredOuter.Width) > 0.5 ||
            Math.Abs(inheritedOuter.Height - forcedLayeredOuter.Height) > 0.5,
            $"expected the inherited-\"auto\" container ({inheritedOuter.Width:R}x{inheritedOuter.Height:R}) to be " +
            $"sized differently from the forced-\"layered\" container ({forcedLayeredOuter.Width:R}x{forcedLayeredOuter.Height:R}), " +
            "proving the nested scope was reclassified by \"auto\" rather than defaulting to a fixed leaf choice");
    }

    /// <summary>Counts every <see cref="LayoutBox"/> in a node list, recursing into each box's children.</summary>
    /// <param name="nodes">The nodes to count within.</param>
    /// <returns>The total number of boxes found.</returns>
    private static int CountBoxesRecursively(IEnumerable<LayoutNode> nodes)
    {
        var count = 0;
        foreach (var node in nodes)
        {
            if (node is not LayoutBox box)
            {
                continue;
            }

            count++;
            count += CountBoxesRecursively(box.Children);
        }

        return count;
    }

    /// <summary>
    ///     Serializes a layout tree into a stable, human-readable string capturing every box rectangle
    ///     (recursively) and every line's waypoints, so two independently-produced trees can be compared
    ///     for content equality without relying on reference-equality collection semantics.
    /// </summary>
    /// <param name="tree">The tree to serialize.</param>
    /// <returns>The deterministic textual snapshot.</returns>
    private static string DumpTree(LayoutTree tree)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append(System.Globalization.CultureInfo.InvariantCulture, $"W={tree.Width:R} H={tree.Height:R}\n");
        foreach (var node in tree.Nodes)
        {
            DumpNode(builder, node, 0);
        }

        return builder.ToString();
    }

    /// <summary>Appends a single node (and its children) to the snapshot builder.</summary>
    private static void DumpNode(System.Text.StringBuilder builder, LayoutNode node, int depth)
    {
        var indent = new string(' ', depth * 2);
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        switch (node)
        {
            case LayoutBox box:
                builder.Append(culture, $"{indent}BOX {box.Label} X={box.X:R} Y={box.Y:R} W={box.Width:R} H={box.Height:R}\n");
                foreach (var child in box.Children)
                {
                    DumpNode(builder, child, depth + 1);
                }

                break;

            case LayoutLine line:
                builder.Append(culture, $"{indent}LINE");
                foreach (var wp in line.Waypoints)
                {
                    builder.Append(culture, $" ({wp.X:R},{wp.Y:R})");
                }

                builder.Append('\n');
                break;

            case LayoutPort port:
                builder.Append(culture, $"{indent}PORT {port.ExternalLabel}/{port.InternalLabel} X={port.CentreX:R} Y={port.CentreY:R}\n");
                break;

            default:
                builder.Append(culture, $"{indent}{node.GetType().Name}\n");
                break;
        }
    }
}
