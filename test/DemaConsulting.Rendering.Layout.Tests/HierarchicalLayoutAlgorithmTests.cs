// <copyright file="HierarchicalLayoutAlgorithmTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests for the bundled <see cref="HierarchicalLayoutAlgorithm"/>, exercising the recursive
///     compound-graph path (per-scope algorithm selection, container sizing, absolute composition, and
///     cross-container edge routing) and — critically — the flat-graph equivalence guarantee that a
///     graph with no container nodes produces output byte-for-byte identical to the selected leaf
///     algorithm applied directly.
/// </summary>
public sealed class HierarchicalLayoutAlgorithmTests
{
    /// <summary>
    ///     Proves that the algorithm advertises the stable "hierarchical" identifier.
    /// </summary>
    [Fact]
    public void Id_IsHierarchical()
    {
        // Act / Assert
        Assert.Equal("hierarchical", new HierarchicalLayoutAlgorithm().Id);
    }

    /// <summary>
    ///     Proves the flat-graph equivalence guarantee for the layered leaf algorithm: across many
    ///     pseudo-randomly generated flat graphs, the hierarchical engine returns exactly what the
    ///     layered algorithm returns, bit-for-bit.
    /// </summary>
    [Fact]
    public void Apply_FlatRandomGraphs_MatchLayeredAlgorithmExactly()
    {
        for (var seed = 0; seed < 400; seed++)
        {
            // Arrange: an identical flat graph and options for both algorithms
            var graph = BuildRandomFlatGraph(seed);
            var options = LayoutOptions.ForAlgorithm("layered");

            // Act
            var expected = new LayeredLayoutAlgorithm().Apply(graph, options);
            var actual = new HierarchicalLayoutAlgorithm().Apply(graph, options);

            // Assert
            AssertTreesIdentical($"layered seed {seed}", expected, actual);
        }
    }

    /// <summary>
    ///     Proves the flat-graph equivalence guarantee for the containment leaf algorithm: a flat graph
    ///     run through the hierarchical engine with the containment algorithm selected is identical to
    ///     the containment algorithm applied directly.
    /// </summary>
    [Fact]
    public void Apply_FlatRandomGraphs_MatchContainmentAlgorithmExactly()
    {
        for (var seed = 0; seed < 150; seed++)
        {
            // Arrange
            var graph = BuildRandomFlatGraph(seed);
            var options = LayoutOptions.ForAlgorithm("containment");

            // Act
            var expected = new ContainmentLayoutAlgorithm().Apply(graph, options);
            var actual = new HierarchicalLayoutAlgorithm().Apply(graph, options);

            // Assert
            AssertTreesIdentical($"containment seed {seed}", expected, actual);
        }
    }

    /// <summary>
    ///     Proves that a container node is sized to enclose its children and its recursively laid-out
    ///     children are nested at absolute coordinates entirely within the container's bounds.
    /// </summary>
    [Fact]
    public void Apply_TwoLevelNesting_SizesContainerAndNestsChildrenAbsolutely()
    {
        // Arrange: a labelled container holding three leaves and an intra edge, plus a sibling leaf
        var graph = new LayoutGraph();
        var group = graph.AddNode("group", 10, 10);
        group.Label = "Group";
        var c1 = group.Children.AddNode("c1", 80, 40);
        var c2 = group.Children.AddNode("c2", 80, 40);
        var c3 = group.Children.AddNode("c3", 80, 40);
        group.Children.AddEdge("c1-c2", c1, c2);
        group.Children.AddEdge("c2-c3", c2, c3);
        graph.AddNode("outside", 80, 40);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: the container box is present and carries nested boxes
        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        Assert.Equal(2, boxes.Count);
        var containerBox = Assert.Single(boxes, box => box.Children.Count > 0);
        var nestedBoxes = containerBox.Children.OfType<LayoutBox>().ToList();
        Assert.Equal(3, nestedBoxes.Count);

        // Every nested box lies fully within the container's bounds.
        foreach (var nested in nestedBoxes)
        {
            Assert.True(nested.X >= containerBox.X - 1e-9);
            Assert.True(nested.Y >= containerBox.Y - 1e-9);
            Assert.True(nested.X + nested.Width <= containerBox.X + containerBox.Width + 1e-9);
            Assert.True(nested.Y + nested.Height <= containerBox.Y + containerBox.Height + 1e-9);
        }

        // The reported canvas encloses every top-level box.
        foreach (var box in boxes)
        {
            Assert.True(box.X + box.Width <= tree.Width + 1e-9);
            Assert.True(box.Y + box.Height <= tree.Height + 1e-9);
        }
    }

    /// <summary>
    ///     Proves that a containment-packed root can hold a container whose children are laid out with a
    ///     per-node layered override, composing without error.
    /// </summary>
    [Fact]
    public void Apply_ContainmentRootWithLayeredContainer_Composes()
    {
        // Arrange: a containment root with a container that overrides its algorithm to "layered"
        var graph = new LayoutGraph();
        var group = graph.AddNode("group", 10, 10);
        group.Set(CoreOptions.Algorithm, "layered");
        var a = group.Children.AddNode("a", 80, 40);
        var b = group.Children.AddNode("b", 80, 40);
        var c = group.Children.AddNode("c", 80, 40);
        group.Children.AddEdge("a-b", a, b);
        group.Children.AddEdge("b-c", b, c);
        graph.AddNode("peer", 80, 40);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("containment"));

        // Assert: the container was composed with nested children
        var containerBox = Assert.Single(tree.Nodes.OfType<LayoutBox>(), box => box.Children.Count > 0);
        Assert.Equal(3, containerBox.Children.OfType<LayoutBox>().Count());
    }

    /// <summary>
    ///     Proves the reverse composition: a layered root holding a container whose children are packed
    ///     with a per-node containment override.
    /// </summary>
    [Fact]
    public void Apply_LayeredRootWithContainmentContainer_Composes()
    {
        // Arrange: a layered root with a container that overrides its algorithm to "containment"
        var graph = new LayoutGraph();
        var group = graph.AddNode("group", 10, 10);
        group.Set(CoreOptions.Algorithm, "containment");
        var a = group.Children.AddNode("a", 80, 40);
        var b = group.Children.AddNode("b", 80, 40);
        var c = group.Children.AddNode("c", 80, 40);
        group.Children.AddEdge("a-b", a, b);
        group.Children.AddEdge("b-c", b, c);
        graph.AddNode("peer", 80, 40);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert
        var containerBox = Assert.Single(tree.Nodes.OfType<LayoutBox>(), box => box.Children.Count > 0);
        Assert.Equal(3, containerBox.Children.OfType<LayoutBox>().Count());
    }

    /// <summary>
    ///     Proves that a container scope's own <see cref="CoreOptions.Direction"/> override is honored by
    ///     the leaf algorithm laying out its children, even though the engine builds a fresh sized-view
    ///     graph for that scope. Regression test: previously <c>BuildSizedView</c> did not propagate the
    ///     scope's direction override onto the sized view, so a nested container's chain fell back to the
    ///     parent options' direction (or the leaf default) instead of the override set directly on the
    ///     container's children graph.
    /// </summary>
    [Fact]
    public void Apply_ContainerWithDirectionOverride_HonorsNestedDirection()
    {
        // Arrange: a labelled container whose children graph overrides Direction to Down, while the
        // top-level options select the (default) Right direction.
        var graph = new LayoutGraph();
        var group = graph.AddNode("group", 10, 10);
        group.Label = "Group";
        group.Children.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        var a = group.Children.AddNode("a", 80, 40);
        var b = group.Children.AddNode("b", 80, 40);
        var c = group.Children.AddNode("c", 80, 40);
        group.Children.AddEdge("a-b", a, b);
        group.Children.AddEdge("b-c", b, c);
        graph.AddNode("outside", 80, 40);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: the nested chain stacks vertically (strictly increasing Y), proving the container's own
        // Down override was honored rather than falling back to the top-level Right default.
        var containerBox = Assert.Single(tree.Nodes.OfType<LayoutBox>(), box => box.Children.Count > 0);
        var nestedBoxes = containerBox.Children.OfType<LayoutBox>().ToList();
        Assert.Equal(3, nestedBoxes.Count);
        Assert.True(nestedBoxes[0].Y < nestedBoxes[1].Y);
        Assert.True(nestedBoxes[1].Y < nestedBoxes[2].Y);
    }

    /// <summary>
    ///     Proves that a cross-container edge — one whose endpoints live inside different sibling
    ///     containers — is routed at the owning scope around the intervening container rather than
    ///     through its interior.
    /// </summary>
    [Fact]
    public void Apply_CrossContainerEdge_RoutesAroundInterveningContainer()
    {
        // Arrange: three sibling containers packed by a containment root, with a cross-container edge
        // from a child of the first to a child of the third added at the root (their LCA).
        var graph = new LayoutGraph();
        var a = graph.AddNode("A", 10, 10);
        var mid = graph.AddNode("MID", 10, 10);
        var b = graph.AddNode("B", 10, 10);
        var aChild = a.Children.AddNode("a-child", 60, 40);
        mid.Children.AddNode("mid-child", 60, 40);
        var bChild = b.Children.AddNode("b-child", 60, 40);
        graph.AddEdge("cross", aChild, bChild);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("containment"));

        // Assert: exactly one routed line exists at the root (the cross-container edge)
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());
        Assert.True(line.Waypoints.Count >= 2);

        // The MID container box is the second placed box (nodes are emitted in input order).
        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        Assert.Equal(3, boxes.Count);
        var midBox = boxes[1];
        var obstacle = new Rect(midBox.X, midBox.Y, midBox.Width, midBox.Height);

        // No routed segment passes through the intervening container's interior.
        for (var i = 0; i < line.Waypoints.Count - 1; i++)
        {
            Assert.False(
                SegmentCrossesRect(line.Waypoints[i], line.Waypoints[i + 1], obstacle),
                $"Segment {i} crosses the intervening container box.");
        }
    }

    /// <summary>
    ///     Proves that a cross-container edge to a WIDE container placed above a small sibling does not
    ///     wrap back across an endpoint box. Regression: anchoring a connector by the direction to the
    ///     other box's centre chose the wrong side for wide boxes (whose centre sits far past the near
    ///     edge), forcing the route to double back across its own source box.
    /// </summary>
    [Fact]
    public void Apply_CrossContainerEdge_ToWideContainer_DoesNotCrossEndpointBoxes()
    {
        // Arrange: a wide container (three children laid out in a row) plus a small sibling leaf, joined
        // by a cross-container edge from the leaf to a child inside the container.
        var graph = new LayoutGraph();
        var service = graph.AddNode("service", 10, 10);
        var api = service.Children.AddNode("api", 120, 50);
        var worker = service.Children.AddNode("worker", 120, 50);
        var store = service.Children.AddNode("store", 120, 50);
        service.Children.AddEdge("api-worker", api, worker);
        service.Children.AddEdge("worker-store", worker, store);
        var client = graph.AddNode("client", 120, 50);
        graph.AddEdge("client-api", client, api);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, new LayoutOptions());

        // Assert: the single top-level cross-container line does not pass through the interior of either
        // endpoint container box (the wide container or the leaf).
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());
        foreach (var box in tree.Nodes.OfType<LayoutBox>())
        {
            var rect = new Rect(box.X, box.Y, box.Width, box.Height);
            for (var i = 0; i < line.Waypoints.Count - 1; i++)
            {
                Assert.False(
                    SegmentCrossesRect(line.Waypoints[i], line.Waypoints[i + 1], rect),
                    $"Cross-container segment {i} crosses the '{box.Label}' box interior.");
            }
        }
    }

    /// <summary>
    ///     Proves that three levels of nesting compose so a box contains a box that contains a box.
    /// </summary>
    [Fact]
    public void Apply_ThreeLevelNesting_Succeeds()
    {
        // Arrange: root -> container A -> container B -> leaf
        var graph = new LayoutGraph();
        var a = graph.AddNode("A", 10, 10);
        var b = a.Children.AddNode("B", 10, 10);
        b.Children.AddNode("leaf", 80, 40);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: the nesting depth is reflected in the composed box tree
        var outer = Assert.Single(tree.Nodes.OfType<LayoutBox>());
        var middle = Assert.Single(outer.Children.OfType<LayoutBox>());
        var inner = Assert.Single(middle.Children.OfType<LayoutBox>());
        Assert.Empty(inner.Children);
    }

    /// <summary>
    ///     Proves that the engine never mutates the caller's input graph: after laying out a compound
    ///     graph, every original node — the container placeholder and its leaf children — retains the
    ///     exact width and height it was given, because effective container sizing is done over an
    ///     internal sized view rather than by writing back to the caller's nodes.
    /// </summary>
    [Fact]
    public void Apply_CompoundGraph_DoesNotMutateInputNodeSizes()
    {
        // Arrange: a labelled container (small placeholder size) holding two differently sized leaves
        var graph = new LayoutGraph();
        var group = graph.AddNode("group", 10, 10);
        group.Label = "Group";
        var c1 = group.Children.AddNode("c1", 80, 40);
        var c2 = group.Children.AddNode("c2", 123, 57);
        group.Children.AddEdge("c1-c2", c1, c2);
        var outside = graph.AddNode("outside", 64, 48);

        // Act
        _ = new HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: every original node keeps its exact input dimensions (bit-for-bit)
        AssertExact("group.Width", 10, group.Width);
        AssertExact("group.Height", 10, group.Height);
        AssertExact("c1.Width", 80, c1.Width);
        AssertExact("c1.Height", 40, c1.Height);
        AssertExact("c2.Width", 123, c2.Width);
        AssertExact("c2.Height", 57, c2.Height);
        AssertExact("outside.Width", 64, outside.Width);
        AssertExact("outside.Height", 48, outside.Height);
    }

    /// <summary>
    ///     Proves that a flat empty graph is delegated to the layered algorithm, yielding an empty
    ///     placed tree with a positive-size canvas.
    /// </summary>
    [Fact]
    public void Apply_EmptyGraph_ReturnsEmptyCanvas()
    {
        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(new LayoutGraph(), LayoutOptions.ForAlgorithm("layered"));

        // Assert
        Assert.Empty(tree.Nodes);
        Assert.True(tree.Width > 0);
        Assert.True(tree.Height > 0);
    }

    /// <summary>
    ///     Proves that a node whose child graph was materialized but never populated is treated as a
    ///     leaf, keeping the whole graph on the flat fast path.
    /// </summary>
    [Fact]
    public void Apply_ContainerWithEmptyChildren_TreatedAsLeaf()
    {
        // Arrange: a graph with a single node whose empty child graph has been materialized
        var graph = new LayoutGraph();
        var node = graph.AddNode("n", 80, 40);
        _ = node.Children; // materialize but do not populate
        Assert.False(node.HasChildren);
        var options = LayoutOptions.ForAlgorithm("layered");

        // Act
        var expected = new LayeredLayoutAlgorithm().Apply(graph, options);
        var actual = new HierarchicalLayoutAlgorithm().Apply(graph, options);

        // Assert: identical to the layered algorithm, and the single box carries no children
        AssertTreesIdentical("empty-children", expected, actual);
        var box = Assert.Single(actual.Nodes.OfType<LayoutBox>());
        Assert.Empty(box.Children);
    }

    /// <summary>
    ///     Proves that a null graph argument is rejected.
    /// </summary>
    [Fact]
    public void Apply_NullGraph_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(
            () => new HierarchicalLayoutAlgorithm().Apply(null!, new LayoutOptions()));
    }

    /// <summary>
    ///     Proves that a null options argument is rejected.
    /// </summary>
    [Fact]
    public void Apply_NullOptions_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(
            () => new HierarchicalLayoutAlgorithm().Apply(new LayoutGraph(), null!));
    }

    /// <summary>
    ///     Proves that a null registry argument is rejected by the injecting constructor.
    /// </summary>
    [Fact]
    public void Constructor_NullRegistry_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new HierarchicalLayoutAlgorithm(null!));
    }

    /// <summary>
    ///     Proves that naming this engine's own "hierarchical" identifier as the scope algorithm — the
    ///     documented default id — does not fail to resolve, but degrades to the default leaf algorithm
    ///     (layered) and yields output identical to the default (unset) path.
    /// </summary>
    [Fact]
    public void Apply_ExplicitHierarchicalOptions_MatchesDefaultLeafExactly()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            // Arrange: the same flat graph selected explicitly as "hierarchical" and (implicitly) layered
            var graph = BuildRandomFlatGraph(seed);

            // Act
            var explicitHierarchical = new HierarchicalLayoutAlgorithm()
                .Apply(graph, LayoutOptions.ForAlgorithm(HierarchicalLayoutAlgorithm.AlgorithmId));
            var layered = new LayeredLayoutAlgorithm()
                .Apply(graph, LayoutOptions.ForAlgorithm("layered"));

            // Assert: explicit "hierarchical" resolves to the layered leaf, bit-for-bit
            AssertTreesIdentical($"explicit-hierarchical seed {seed}", layered, explicitHierarchical);
        }
    }

    /// <summary>
    ///     Proves that a container node explicitly set to the "hierarchical" algorithm composes without a
    ///     resolution failure, placing that scope with the default leaf algorithm.
    /// </summary>
    [Fact]
    public void Apply_ContainerNodeSetHierarchical_Composes()
    {
        // Arrange: a layered root holding a container that overrides its algorithm to "hierarchical"
        var graph = new LayoutGraph();
        var group = graph.AddNode("group", 10, 10);
        group.Set(CoreOptions.Algorithm, HierarchicalLayoutAlgorithm.AlgorithmId);
        var a = group.Children.AddNode("a", 80, 40);
        var b = group.Children.AddNode("b", 80, 40);
        group.Children.AddEdge("a-b", a, b);
        graph.AddNode("peer", 80, 40);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: the container was composed with its two nested children
        var containerBox = Assert.Single(tree.Nodes.OfType<LayoutBox>(), box => box.Children.Count > 0);
        Assert.Equal(2, containerBox.Children.OfType<LayoutBox>().Count());
    }

    /// <summary>
    ///     Proves that a graph whose root carries an explicit "hierarchical" algorithm override lays out
    ///     without a resolution failure.
    /// </summary>
    [Fact]
    public void Apply_GraphSetHierarchical_DoesNotThrow()
    {
        // Arrange: a compound graph whose root explicitly selects "hierarchical"
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, HierarchicalLayoutAlgorithm.AlgorithmId);
        var group = graph.AddNode("group", 10, 10);
        group.Children.AddNode("child", 80, 40);
        graph.AddNode("peer", 80, 40);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, new LayoutOptions());

        // Assert: the container was composed with its nested child
        var containerBox = Assert.Single(tree.Nodes.OfType<LayoutBox>(), box => box.Children.Count > 0);
        Assert.Single(containerBox.Children.OfType<LayoutBox>());
    }

    /// <summary>
    ///     Deterministically builds a flat (non-nested) layout graph for the given seed, with random
    ///     node sizes and arbitrary edges (including self-loops, parallel edges, and cycles).
    /// </summary>
    private static LayoutGraph BuildRandomFlatGraph(int seed)
    {
        var rng = new Random(seed);
        var graph = new LayoutGraph();

        var count = rng.Next(0, 16);
        var nodes = new LayoutGraphNode[count];
        for (var i = 0; i < count; i++)
        {
            var node = graph.AddNode($"n{i}", rng.Next(40, 240), rng.Next(30, 120));

            // Give some nodes labels so the equivalence check also covers label propagation.
            if (rng.Next(2) == 0)
            {
                node.Label = $"N{i}";
            }

            nodes[i] = node;
        }

        if (count > 0)
        {
            var edgeCount = rng.Next(0, (count * 2) + 1);
            for (var e = 0; e < edgeCount; e++)
            {
                var source = nodes[rng.Next(count)];
                var target = nodes[rng.Next(count)];
                graph.AddEdge($"e{e}", source, target);
            }
        }

        return graph;
    }

    /// <summary>
    ///     Deep-compares two layout trees for exact (bit-level) equality of every geometric field,
    ///     node kind, box attribute, and line attribute. No numeric tolerance is allowed.
    /// </summary>
    private static void AssertTreesIdentical(string context, LayoutTree expected, LayoutTree actual)
    {
        AssertExact($"{context}: Width", expected.Width, actual.Width);
        AssertExact($"{context}: Height", expected.Height, actual.Height);

        Assert.Equal(expected.Nodes.Count, actual.Nodes.Count);
        for (var i = 0; i < expected.Nodes.Count; i++)
        {
            var expectedNode = expected.Nodes[i];
            var actualNode = actual.Nodes[i];
            Assert.Equal(expectedNode.GetType(), actualNode.GetType());

            switch (expectedNode)
            {
                case LayoutBox expectedBox:
                    AssertBoxesIdentical($"{context}: Nodes[{i}]", expectedBox, (LayoutBox)actualNode);
                    break;
                case LayoutLine expectedLine:
                    AssertLinesIdentical($"{context}: Nodes[{i}]", expectedLine, (LayoutLine)actualNode);
                    break;
                default:
                    Assert.Fail($"{context}: Nodes[{i}] has an unexpected node type {expectedNode.GetType()}.");
                    break;
            }
        }
    }

    /// <summary>Deep-compares two boxes for exact equality of every geometric and display field.</summary>
    private static void AssertBoxesIdentical(string context, LayoutBox expected, LayoutBox actual)
    {
        AssertExact($"{context}.X", expected.X, actual.X);
        AssertExact($"{context}.Y", expected.Y, actual.Y);
        AssertExact($"{context}.Width", expected.Width, actual.Width);
        AssertExact($"{context}.Height", expected.Height, actual.Height);
        Assert.Equal(expected.Label, actual.Label);
        Assert.Equal(expected.Depth, actual.Depth);
        Assert.Equal(expected.Shape, actual.Shape);
        Assert.Equal(expected.Children.Count, actual.Children.Count);
    }

    /// <summary>Deep-compares two lines for exact equality of waypoints and styling.</summary>
    private static void AssertLinesIdentical(string context, LayoutLine expected, LayoutLine actual)
    {
        Assert.Equal(expected.Waypoints.Count, actual.Waypoints.Count);
        for (var w = 0; w < expected.Waypoints.Count; w++)
        {
            AssertExact($"{context}.Waypoints[{w}].X", expected.Waypoints[w].X, actual.Waypoints[w].X);
            AssertExact($"{context}.Waypoints[{w}].Y", expected.Waypoints[w].Y, actual.Waypoints[w].Y);
        }

        Assert.Equal(expected.SourceEnd, actual.SourceEnd);
        Assert.Equal(expected.TargetEnd, actual.TargetEnd);
        Assert.Equal(expected.LineStyle, actual.LineStyle);
        Assert.Equal(expected.MidpointLabel, actual.MidpointLabel);
    }

    /// <summary>Asserts that two doubles are identical at the bit level (no tolerance).</summary>
    private static void AssertExact(string context, double expected, double actual)
    {
        Assert.True(
            BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual),
            $"{context}: expected {expected:R} but got {actual:R}");
    }

    /// <summary>
    ///     Returns true when the axis-aligned segment passes through the strict interior of the rect.
    /// </summary>
    private static bool SegmentCrossesRect(Point2D a, Point2D b, Rect r)
    {
        if (Math.Abs(a.Y - b.Y) < 1e-6)
        {
            // Horizontal segment
            var y = a.Y;
            var xa = Math.Min(a.X, b.X);
            var xb = Math.Max(a.X, b.X);
            return r.Y < y && y < r.Y + r.Height &&
                   Math.Max(xa, r.X) < Math.Min(xb, r.X + r.Width);
        }

        // Vertical segment
        var x = a.X;
        var ya = Math.Min(a.Y, b.Y);
        var yb = Math.Max(a.Y, b.Y);
        return r.X < x && x < r.X + r.Width &&
               Math.Max(ya, r.Y) < Math.Min(yb, r.Y + r.Height);
    }
}
