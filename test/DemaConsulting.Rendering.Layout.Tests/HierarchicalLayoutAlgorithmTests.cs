// <copyright file="HierarchicalLayoutAlgorithmTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

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
    ///     Proves that a container's own <see cref="LayoutGraphNode.TitleHeight"/> override replaces the
    ///     engine's generic default title-band height, both in the container's effective size and in
    ///     the vertical offset applied to its nested children — so a caller can match a specific theme's
    ///     actual title-area height (for example when the container also carries a keyword line) instead
    ///     of being limited to the engine's generic default band.
    /// </summary>
    [Fact]
    public void Apply_ContainerWithTitleHeightOverride_ReplacesDefaultTitleBand()
    {
        // Arrange: two otherwise-identical single-child containers, one with a TitleHeight override.
        var defaultGraph = new LayoutGraph();
        var defaultGroup = defaultGraph.AddNode("group", 10, 10);
        defaultGroup.Label = "Group";
        defaultGroup.Children.AddNode("child", 80, 40);

        var overrideGraph = new LayoutGraph();
        var overrideGroup = overrideGraph.AddNode("group", 10, 10);
        overrideGroup.Label = "Group";
        overrideGroup.TitleHeight = 100.0;
        overrideGroup.Children.AddNode("child", 80, 40);

        // Act
        var defaultTree = new HierarchicalLayoutAlgorithm().Apply(defaultGraph, LayoutOptions.ForAlgorithm("layered"));
        var overrideTree = new HierarchicalLayoutAlgorithm().Apply(overrideGraph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: the override container is exactly (100 - 24) taller than the default container, and
        // its nested child is offset down by that same difference.
        var defaultContainer = Assert.Single(defaultTree.Nodes.OfType<LayoutBox>());
        var overrideContainer = Assert.Single(overrideTree.Nodes.OfType<LayoutBox>());
        Assert.Equal(defaultContainer.Height + 76.0, overrideContainer.Height, precision: 6);

        var defaultChild = Assert.Single(defaultContainer.Children.OfType<LayoutBox>());
        var overrideChild = Assert.Single(overrideContainer.Children.OfType<LayoutBox>());
        var defaultOffsetFromTop = defaultChild.Y - defaultContainer.Y;
        var overrideOffsetFromTop = overrideChild.Y - overrideContainer.Y;
        Assert.Equal(defaultOffsetFromTop + 76.0, overrideOffsetFromTop, precision: 6);
    }

    /// <summary>
    ///     Proves that a container's own <see cref="LayoutGraphNode.TitleHeight"/> override applies only
    ///     while it carries a <see cref="LayoutGraphNode.Label"/>: an unlabelled container reserves no
    ///     title band regardless of the override, matching the engine's existing label-gated behavior.
    /// </summary>
    [Fact]
    public void Apply_UnlabelledContainerWithTitleHeightOverride_ReservesNoTitleBand()
    {
        // Arrange: two otherwise-identical unlabelled single-child containers; only one sets an
        // (ignored) TitleHeight override.
        var plainGraph = new LayoutGraph();
        var plainGroup = plainGraph.AddNode("group", 10, 10);
        plainGroup.Children.AddNode("child", 80, 40);

        var overrideGraph = new LayoutGraph();
        var overrideGroup = overrideGraph.AddNode("group", 10, 10);
        overrideGroup.TitleHeight = 100.0;
        overrideGroup.Children.AddNode("child", 80, 40);

        // Act
        var plainTree = new HierarchicalLayoutAlgorithm().Apply(plainGraph, LayoutOptions.ForAlgorithm("layered"));
        var overrideTree = new HierarchicalLayoutAlgorithm().Apply(overrideGraph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: both containers are sized identically, since neither reserves a title band.
        var plainContainer = Assert.Single(plainTree.Nodes.OfType<LayoutBox>());
        var overrideContainer = Assert.Single(overrideTree.Nodes.OfType<LayoutBox>());
        Assert.Equal(plainContainer.Height, overrideContainer.Height, precision: 6);
    }

    /// <summary>
    ///     Proves that a <see cref="BoxShape.Folder"/> container reserves additional space above its
    ///     nested children for the folder tab, beyond the ordinary title band, so children are never
    ///     placed underneath the recessed keyword/label text (which the renderer draws below the tab).
    /// </summary>
    [Fact]
    public void Apply_FolderContainer_ReservesTabHeightAboveChildren()
    {
        // Arrange: two otherwise-identical labelled single-child containers, one shaped as a folder
        // with an explicit tab height.
        var rectangleGraph = new LayoutGraph();
        var rectangleGroup = rectangleGraph.AddNode("group", 10, 10);
        rectangleGroup.Label = "Group";
        rectangleGroup.Children.AddNode("child", 80, 40);

        var folderGraph = new LayoutGraph();
        var folderGroup = folderGraph.AddNode("group", 10, 10);
        folderGroup.Label = "Group";
        folderGroup.Shape = BoxShape.Folder;
        folderGroup.FolderTabHeight = 24.0;
        folderGroup.Children.AddNode("child", 80, 40);

        // Act
        var rectangleTree = new HierarchicalLayoutAlgorithm().Apply(rectangleGraph, LayoutOptions.ForAlgorithm("layered"));
        var folderTree = new HierarchicalLayoutAlgorithm().Apply(folderGraph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: the folder container is exactly tab-height taller, and its child is offset down by
        // that same additional amount, so the child never overlaps the recessed title area.
        var rectangleContainer = Assert.Single(rectangleTree.Nodes.OfType<LayoutBox>());
        var folderContainer = Assert.Single(folderTree.Nodes.OfType<LayoutBox>());
        Assert.Equal(rectangleContainer.Height + 24.0, folderContainer.Height, precision: 6);

        var rectangleChild = Assert.Single(rectangleContainer.Children.OfType<LayoutBox>());
        var folderChild = Assert.Single(folderContainer.Children.OfType<LayoutBox>());
        var rectangleOffsetFromTop = rectangleChild.Y - rectangleContainer.Y;
        var folderOffsetFromTop = folderChild.Y - folderContainer.Y;
        Assert.Equal(rectangleOffsetFromTop + 24.0, folderOffsetFromTop, precision: 6);
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
    ///     graph for that scope. This exercises the generalized cascading mechanism (<see cref="PropertyHolder.OverlayOnto"/>):
    ///     the container's own <see cref="LayoutGraphNode.Children"/> graph override is overlaid onto its
    ///     parent scope's resolved options to produce the effective snapshot passed to the leaf algorithm.
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
    ///     Proves that a <see cref="CoreOptions.Direction"/> override set two levels up cascades through
    ///     an intermediate container that sets nothing of its own, reaching a third-level leaf chain.
    ///     This is the multi-level cascade the single-level regression test above cannot exercise.
    /// </summary>
    [Fact]
    public void Apply_ThreeLevelDirectionCascade_InheritsThroughUnsetMiddleLevel()
    {
        // Arrange: root (unset) -> level1 container (Children.Set(Direction, Down)) -> level2 container
        // (unset) -> level3 leaf chain.
        var graph = new LayoutGraph();
        var level1 = graph.AddNode("level1", 10, 10);
        level1.Children.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        var level2 = level1.Children.AddNode("level2", 10, 10);
        var leafA = level2.Children.AddNode("leafA", 80, 40);
        var leafB = level2.Children.AddNode("leafB", 80, 40);
        var leafC = level2.Children.AddNode("leafC", 80, 40);
        level2.Children.AddEdge("a-b", leafA, leafB);
        level2.Children.AddEdge("b-c", leafB, leafC);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: level3 leaves stack vertically, proving Down cascaded through the unset level2 scope.
        var outerBox = Assert.Single(tree.Nodes.OfType<LayoutBox>());
        var middleBox = Assert.Single(outerBox.Children.OfType<LayoutBox>());
        var leafBoxes = middleBox.Children.OfType<LayoutBox>().ToList();
        Assert.Equal(3, leafBoxes.Count);
        Assert.True(leafBoxes[0].Y < leafBoxes[1].Y);
        Assert.True(leafBoxes[1].Y < leafBoxes[2].Y);
    }

    /// <summary>
    ///     Proves that a deeper, explicit <see cref="CoreOptions.Direction"/> override takes precedence
    ///     over an ancestor's own override, rather than the ancestor's value winning because it is set
    ///     first or higher in the tree. Nearest-ancestor-override-wins, not first-set-wins.
    /// </summary>
    [Fact]
    public void Apply_ThreeLevelDirectionCascade_MidLevelOverrideTakesPrecedence()
    {
        // Arrange: same shape as the inherit-through-unset test, but level2 sets its own explicit
        // Direction override (Right), which must win over level1's inherited Down for level2's children.
        var graph = new LayoutGraph();
        var level1 = graph.AddNode("level1", 10, 10);
        level1.Children.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        var level2 = level1.Children.AddNode("level2", 10, 10);
        level2.Children.Set(CoreOptions.Direction, LayoutFlowDirection.Right);
        var leafA = level2.Children.AddNode("leafA", 80, 40);
        var leafB = level2.Children.AddNode("leafB", 80, 40);
        var leafC = level2.Children.AddNode("leafC", 80, 40);
        level2.Children.AddEdge("a-b", leafA, leafB);
        level2.Children.AddEdge("b-c", leafB, leafC);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: level3 leaves flow horizontally, not vertically, proving level2's own Right override
        // took precedence over level1's inherited Down.
        var outerBox = Assert.Single(tree.Nodes.OfType<LayoutBox>());
        var middleBox = Assert.Single(outerBox.Children.OfType<LayoutBox>());
        var leafBoxes = middleBox.Children.OfType<LayoutBox>().ToList();
        Assert.Equal(3, leafBoxes.Count);
        Assert.True(leafBoxes[0].X < leafBoxes[1].X);
        Assert.True(leafBoxes[1].X < leafBoxes[2].X);
    }

    /// <summary>
    ///     Proves that the per-scope cascaded effective options snapshot — built by overlaying each
    ///     scope's own overrides via <see cref="PropertyHolder.OverlayOnto"/> — actually reaches every
    ///     leaf-algorithm invocation, at three levels of nesting, using a recording test double that
    ///     captures the exact <see cref="LayoutOptions"/> instance each scope was placed with. Because
    ///     <see cref="EdgeRouting"/> declares only one member (<see cref="EdgeRouting.Orthogonal"/>)
    ///     today, a resolved value alone cannot distinguish "inherited" from "never set"; this test pairs
    ///     the real <see cref="CoreOptions.EdgeRouting"/> cascade with an arbitrary custom marker property
    ///     set at the same scopes, which does have distinguishable values, to prove both that
    ///     <see cref="CoreOptions.EdgeRouting"/> is carried through every level's effective snapshot and
    ///     that a deeper scope's own override of another property wins over an ancestor's, at the exact
    ///     effective-options instance each leaf-algorithm call receives.
    /// </summary>
    [Fact]
    public void Apply_ThreeLevelEdgeRoutingCascade_ReachesEveryLeafAlgorithmCall()
    {
        // Arrange: root -> level1 container (Children overrides EdgeRouting and a custom Marker) ->
        // level2 container (Children overrides only the Marker, not EdgeRouting) -> level3 leaves. A
        // recording algorithm captures the graph/options pair passed to every leaf-algorithm invocation.
        var marker = new LayoutProperty<string?>("test.cascade.marker", null);
        var recorder = new RecordingLayoutAlgorithm(new LayeredLayoutAlgorithm());
        var registry = new LayoutAlgorithmRegistry()
            .Register(recorder)
            .Register(new ContainmentLayoutAlgorithm());

        var graph = new LayoutGraph();
        var level1 = graph.AddNode("level1", 10, 10);
        level1.Children.Set(CoreOptions.EdgeRouting, EdgeRouting.Orthogonal);
        level1.Children.Set(marker, "level1");
        var level2 = level1.Children.AddNode("level2", 10, 10);
        level2.Children.Set(marker, "level2");
        var leafA = level2.Children.AddNode("leafA", 80, 40);
        var leafB = level2.Children.AddNode("leafB", 80, 40);
        level2.Children.AddEdge("a-b", leafA, leafB);

        // Act
        _ = new HierarchicalLayoutAlgorithm(registry).Apply(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: the leaf-scope call (level2's own children graph, the flat fast path) received the
        // EdgeRouting override cascaded down from level1, and its own Marker override, not level1's.
        var leafScopeCall = Assert.Single(recorder.Calls, call => ReferenceEquals(call.Graph, level2.Children));
        Assert.Equal(EdgeRouting.Orthogonal, leafScopeCall.Options.Get(CoreOptions.EdgeRouting));
        Assert.Equal("level2", leafScopeCall.Options.Get(marker));

        // The level1-scope call (placing level2's box within level1's sized view) saw level1's own Marker
        // value, proving each scope's effective snapshot is distinct rather than one shared reference.
        var level1ScopeCall = Assert.Single(recorder.Calls, call => call.Options.Get(marker) == "level1");
        Assert.Equal(EdgeRouting.Orthogonal, level1ScopeCall.Options.Get(CoreOptions.EdgeRouting));
    }

    /// <summary>
    ///     Proves that <see cref="ContainmentLayoutAlgorithm"/>'s cross-container-edge routing (owned by
    ///     <see cref="HierarchicalLayoutAlgorithm"/>) uses the cascaded scope's <see cref="CoreOptions.EdgeRouting"/>
    ///     rather than the root options, by overriding it away from the root's own value at the scope that
    ///     owns the cross-container edge and confirming the routed line still appears (the recording
    ///     algorithm on its own only observes leaf-algorithm calls, not the cross-container routing path,
    ///     so this test exercises the composed public behavior directly instead).
    /// </summary>
    [Fact]
    public void Apply_CrossContainerEdge_HonorsScopeEdgeRoutingOverride()
    {
        // Arrange: a scope whose own graph overrides EdgeRouting away from the root options' value.
        var graph = new LayoutGraph();
        var a = graph.AddNode("A", 10, 10);
        var b = graph.AddNode("B", 10, 10);
        var aChild = a.Children.AddNode("a-child", 60, 40);
        var bChild = b.Children.AddNode("b-child", 60, 40);
        graph.Set(CoreOptions.EdgeRouting, EdgeRouting.Orthogonal);
        graph.AddEdge("cross", aChild, bChild);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("containment"));

        // Assert: the cross-container edge is still routed (the scope's own EdgeRouting override,
        // cascaded via the effective snapshot, was consulted rather than being silently ignored).
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());
        Assert.True(line.Waypoints.Count >= 2);
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
    ///     Proves that a box which is both a plain root-level sibling (connected to several other
    ///     root-level boxes by ordinary edges the leaf algorithm would normally route locally) and the
    ///     endpoint of a genuine cross-container edge from inside a separate container has every one of
    ///     its anchors allocated by a single coordinated pass, not two independent ones. Regression: the
    ///     leaf algorithm's own locally-routed anchors and this scope's cross-container router's anchor
    ///     were previously computed by two separate <see cref="ConnectorRouter"/> batches, each
    ///     unaware of the other, so an anchor from one batch could land at the exact same point as an
    ///     anchor from the other — exactly what happened in a real SysML general-view diagram where a
    ///     package-external specialization edge collided with several sibling boxes' membership edges on
    ///     the same shared target face.
    /// </summary>
    [Fact]
    public void Apply_BoxWithBothLeafAndCrossContainerEdges_SpreadsSharedFaceAnchorsTogether()
    {
        // Arrange: "target" is a small root-level box; "a" is a much wider box packed directly below
        // it, and "pkg" (a container whose child "typed" is the real edge endpoint, after promotion to
        // the cross-container batch) is packed in a further row, also directly below "target" and also
        // wide enough to span its full width. Both "a" and "pkg" fully contain target's horizontal
        // extent, so ConnectorRouter's face-anchor calculation (which centres on the *overlap* of the
        // two boxes, or the target's own centre when they don't overlap at all) resolves to target's
        // own bottom-face centre for BOTH edges independently. Before the fix, "a-target" was routed
        // alone in the leaf algorithm's own batch and "typed-target" was routed alone in this scope's
        // separate cross-container batch, so each computed that identical centred point with no
        // awareness of the other, landing both anchors on the exact same pixel. This mirrors the real
        // DictionaryMark diagram where a package-external specialization edge collided with a sibling
        // membership edge on the same shared target face.
        var graph = new LayoutGraph();
        var target = graph.AddNode("target", 40, 40);
        var a = graph.AddNode("a", 300, 40);

        var pkg = graph.AddNode("pkg", 10, 10);
        var typed = pkg.Children.AddNode("typed", 60, 40);

        graph.AddEdge("a-target", a, target);
        graph.AddEdge("typed-target", typed, target);

        // Act: the "containment" leaf algorithm packs boxes deterministically and routes its own edges
        // with ConnectorRouter, so the root scope's leaf-routed batch and its cross-container batch are
        // both driven by the same underlying router — exactly like the real diagram's regression.
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("containment"));

        // Assert: the two connectors landing on "target" reach distinct anchor points.
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();
        Assert.Equal(2, lines.Count);
        Assert.NotEqual(lines[0].Waypoints[^1], lines[1].Waypoints[^1]);
    }

    /// <summary>
    ///     Proves that a same-scope port edge — one whose direct-member endpoints are both literal,
    ///     non-nested members of the scope — is routed by the leaf algorithm exactly as it would be in
    ///     a flat graph, even though the scope also contains an unrelated container node elsewhere.
    ///     Regression: the algorithm's edge classification used to unconditionally skip (silently
    ///     drop) any edge touching a <see cref="LayoutGraphPort"/> the instant the scope contained any
    ///     container node at all, regardless of whether the port edge actually crossed a container
    ///     boundary.
    /// </summary>
    [Fact]
    public void Apply_SameScopePortEdge_WithUnrelatedContainerElsewhere_RoutesLikePortEdge()
    {
        // Arrange: two root-level siblings joined by a port-to-node edge, plus an unrelated container
        // elsewhere in the scope with its own child and no edges of its own.
        var graph = new LayoutGraph();
        var source = graph.AddNode("source", 80, 40);
        source.Label = "Source";
        var target = graph.AddNode("target", 80, 40);
        target.Label = "Target";
        var port = source.Ports.AddPort("out1");
        port.ExternalLabel = "output";
        graph.AddEdge("e1", port, target);

        var unrelated = graph.AddNode("unrelated", 10, 10);
        unrelated.Label = "Unrelated";
        unrelated.Children.AddNode("unrelated-child", 60, 40);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, new LayoutOptions());

        // Assert: exactly one LayoutPort is emitted, carrying the port's external label, and exactly
        // one LayoutLine connects the two boxes — the connector is routed, not dropped, even though
        // the scope contains an (unrelated) container.
        var port1 = Assert.Single(tree.Nodes.OfType<LayoutPort>());
        Assert.Equal("output", port1.ExternalLabel);
        Assert.Single(tree.Nodes.OfType<LayoutLine>());

        // The port anchor lies exactly on the source box's boundary, matching how a flat (no
        // container) graph with the same port edge would be routed by the leaf algorithm directly.
        var sourceBox = tree.Nodes.OfType<LayoutBox>().Single(box => box.Label == "Source");
        Assert.True(
            OnBoxBoundary(port1.CentreX, port1.CentreY, sourceBox),
            "The port anchor does not lie on the source box's boundary.");
    }

    /// <summary>
    ///     Proves the Stage 1 acceptance scenario: a container <c>B</c> owns a boundary (delegation)
    ///     port <c>P</c> carrying both an external and an internal label, an external edge approaches
    ///     <c>P</c> from a sibling node <c>A</c> in the parent scope, and an internal delegation edge
    ///     routes <c>P</c> to a child <c>C</c> inside <c>B</c>. The engine must emit exactly one
    ///     <see cref="LayoutPort"/> for <c>P</c> — carrying both labels on one shared physical anchor on
    ///     <c>B</c>'s boundary — and both the external approach connector and the internal delegation
    ///     connector must reach that one anchor.
    /// </summary>
    [Fact]
    public void Apply_BoundaryPortWithExternalAndInternalEdges_EmitsOneSharedAnchorCarryingBothLabels()
    {
        // Arrange: sibling A and container B are peers at the root; B owns boundary port P with both
        // labels; the external edge A->P lives in the root scope; the internal delegation edge P->C
        // lives inside B's own children.
        var graph = new LayoutGraph();
        var a = graph.AddNode("A", 80, 40);
        a.Label = "A";
        var b = graph.AddNode("B", 10, 10);
        b.Label = "B";
        var port = b.Ports.AddPort("p1");
        port.ExternalLabel = "PWR_OUT";
        port.InternalLabel = "PWR_IN";
        var c = b.Children.AddNode("C", 80, 40);
        c.Label = "C";

        graph.AddEdge("a-to-b", a, port);
        b.Children.AddEdge("b-to-c", port, c);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: exactly one LayoutPort, carrying BOTH labels, on B's boundary.
        var emitted = Assert.Single(tree.Nodes.OfType<LayoutPort>());
        Assert.Equal("PWR_OUT", emitted.ExternalLabel);
        Assert.Equal("PWR_IN", emitted.InternalLabel);

        var containerBox = tree.Nodes.OfType<LayoutBox>().Single(box => box.Label == "B");
        Assert.True(
            OnBoxBoundary(emitted.CentreX, emitted.CentreY, containerBox),
            "The shared boundary-port anchor does not lie on container B's boundary.");

        // Both connectors reach the single shared anchor: the external approach terminates on it, and
        // the internal delegation connector starts from it.
        var anchor = new Point2D(emitted.CentreX, emitted.CentreY);
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();
        Assert.Contains(
            lines,
            line => line.Waypoints.Count > 0 &&
                (SamePoint(line.Waypoints[0], anchor) || SamePoint(line.Waypoints[^1], anchor)));

        // The internal delegation connector must reach into container B's interior toward child C.
        var childBox = containerBox.Children.OfType<LayoutBox>().Single(box => box.Label == "C");
        Assert.Contains(
            lines,
            line => line.Waypoints.Count > 0 &&
                (SamePoint(line.Waypoints[0], anchor) || SamePoint(line.Waypoints[^1], anchor)) &&
                line.Waypoints.Any(wp =>
                    wp.X >= childBox.X - 1.0 && wp.X <= childBox.X + childBox.Width + 1.0 &&
                    wp.Y >= childBox.Y - 1.0 && wp.Y <= childBox.Y + childBox.Height + 1.0));
    }

    /// <summary>
    ///     Proves boundary-port fan-out: a single boundary port <c>P</c> with <em>two</em> external
    ///     approach edges (from siblings <c>A1</c> and <c>A2</c>) and <em>two</em> internal delegation
    ///     edges (to children <c>C1</c> and <c>C2</c>) still resolves to exactly one shared anchor
    ///     carrying both labels, with every external approach and every internal delegation reaching
    ///     that one anchor.
    /// </summary>
    [Fact]
    public void Apply_BoundaryPortFanOut_ResolvesToOneSharedAnchor()
    {
        // Arrange: two siblings feed one boundary port that delegates to two children.
        var graph = new LayoutGraph();
        var a1 = graph.AddNode("A1", 80, 40);
        var a2 = graph.AddNode("A2", 80, 40);
        var b = graph.AddNode("B", 10, 10);
        b.Label = "B";
        var port = b.Ports.AddPort("p1");
        port.ExternalLabel = "PWR_OUT";
        port.InternalLabel = "PWR_IN";
        var c1 = b.Children.AddNode("C1", 80, 40);
        var c2 = b.Children.AddNode("C2", 80, 40);

        graph.AddEdge("a1-b", a1, port);
        graph.AddEdge("a2-b", a2, port);
        b.Children.AddEdge("b-c1", port, c1);
        b.Children.AddEdge("b-c2", port, c2);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: exactly one LayoutPort carrying both labels despite the fan-out.
        var emitted = Assert.Single(tree.Nodes.OfType<LayoutPort>());
        Assert.Equal("PWR_OUT", emitted.ExternalLabel);
        Assert.Equal("PWR_IN", emitted.InternalLabel);

        var containerBox = tree.Nodes.OfType<LayoutBox>().Single(box => box.Label == "B");
        Assert.True(
            OnBoxBoundary(emitted.CentreX, emitted.CentreY, containerBox),
            "The shared fan-out anchor does not lie on container B's boundary.");

        // At least the two internal delegation connectors must start/end at the shared anchor.
        var anchor = new Point2D(emitted.CentreX, emitted.CentreY);
        var reachingAnchor = tree.Nodes.OfType<LayoutLine>().Count(
            line => line.Waypoints.Count > 0 &&
                (SamePoint(line.Waypoints[0], anchor) || SamePoint(line.Waypoints[^1], anchor)));
        Assert.True(reachingAnchor >= 2, "Fewer than the two internal delegation connectors reach the shared anchor.");
    }

    /// <summary>
    ///     Proves two independent boundary ports on one container are merged in a single combined pass:
    ///     container <c>B</c> owns two delegation ports <c>P</c> and <c>Q</c>, each with its own
    ///     external approach and internal delegation, and the engine emits exactly two
    ///     <see cref="LayoutPort"/> anchors — one per port — each carrying its own pair of labels.
    /// </summary>
    [Fact]
    public void Apply_TwoIndependentBoundaryPortsOnOneContainer_EmitsTwoAnchors()
    {
        // Arrange: container B owns two independent boundary ports.
        var graph = new LayoutGraph();
        var a1 = graph.AddNode("A1", 80, 40);
        var a2 = graph.AddNode("A2", 80, 40);
        var b = graph.AddNode("B", 10, 10);
        b.Label = "B";
        var p = b.Ports.AddPort("p");
        p.ExternalLabel = "P_OUT";
        p.InternalLabel = "P_IN";
        var q = b.Ports.AddPort("q");
        q.ExternalLabel = "Q_OUT";
        q.InternalLabel = "Q_IN";
        var c1 = b.Children.AddNode("C1", 80, 40);
        var c2 = b.Children.AddNode("C2", 80, 40);

        graph.AddEdge("a1-p", a1, p);
        graph.AddEdge("a2-q", a2, q);
        b.Children.AddEdge("p-c1", p, c1);
        b.Children.AddEdge("q-c2", q, c2);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: two distinct anchors, each carrying its own labels.
        var emitted = tree.Nodes.OfType<LayoutPort>().ToList();
        Assert.Equal(2, emitted.Count);
        Assert.Contains(emitted, port => port.ExternalLabel == "P_OUT" && port.InternalLabel == "P_IN");
        Assert.Contains(emitted, port => port.ExternalLabel == "Q_OUT" && port.InternalLabel == "Q_IN");

        var containerBox = tree.Nodes.OfType<LayoutBox>().Single(box => box.Label == "B");
        foreach (var port in emitted)
        {
            Assert.True(
                OnBoxBoundary(port.CentreX, port.CentreY, containerBox),
                "A boundary-port anchor does not lie on container B's boundary.");
        }
    }

    /// <summary>
    ///     Proves that a port edge which genuinely crosses a container boundary in a shape Stage 1 does
    ///     <em>not</em> support — a named port owned by a plain (non-container) node, with an edge
    ///     straight to a leaf nested inside a <em>different</em> container — still throws a clear
    ///     <see cref="NotSupportedException"/> rather than being silently dropped or mis-routed. This
    ///     shape is not a delegation port: the port's owner has no child scope to delegate into, so the
    ///     boundary-port merge mechanism never detects it, and the edge falls through to the box-only
    ///     cross-container router which has no port concept.
    /// </summary>
    [Fact]
    public void Apply_PortOnNonContainerCrossingIntoDifferentContainer_Throws()
    {
        // Arrange: a root-level plain node with a named port, and a separate container with a nested
        // child. An edge from the plain node's port directly to the nested child is a genuine
        // boundary-crossing port edge added at the root (their lowest common ancestor) that Stage 1's
        // delegation mechanism deliberately does not cover.
        var graph = new LayoutGraph();
        var outer = graph.AddNode("outer", 80, 40);
        var port = outer.Ports.AddPort("out1");

        var container = graph.AddNode("container", 10, 10);
        var nested = container.Children.AddNode("nested", 60, 40);

        graph.AddEdge("cross", port, nested);

        // Act / Assert
        var ex = Assert.Throws<NotSupportedException>(
            () => new HierarchicalLayoutAlgorithm().Apply(graph, new LayoutOptions()));
        Assert.Contains("container boundary", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a <see cref="LayoutPort"/> emitted by a nested scope's own leaf pass is
    ///     correctly translated into the ancestor container's absolute coordinates when composed,
    ///     rather than left at its local (pre-translation) position. Closes a latent gap in the
    ///     algorithm's node-translation helper, which previously had no case for
    ///     <see cref="LayoutPort"/> and left it untranslated.
    /// </summary>
    [Fact]
    public void Apply_NestedContainerPortEdge_TranslatesPortIntoAncestorCoordinates()
    {
        // Arrange: a container whose own children graph has a port-to-node edge (exercised via the
        // flat fast path one level down), composed into a root that also has an unrelated sibling
        // container.
        var graph = new LayoutGraph();
        var group = graph.AddNode("group", 10, 10);
        group.Label = "Group";
        var c1 = group.Children.AddNode("c1", 80, 40);
        var c2 = group.Children.AddNode("c2", 80, 40);
        var port = c1.Ports.AddPort("out1");
        port.ExternalLabel = "internal";
        group.Children.AddEdge("c1-c2", port, c2);

        var sibling = graph.AddNode("sibling", 10, 10);
        sibling.Label = "Sibling";
        sibling.Children.AddNode("sibling-child", 60, 40);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: the "group" container box holds the emitted LayoutPort among its (translated)
        // children, positioned within the composed container box's absolute bounds.
        var groupBox = tree.Nodes.OfType<LayoutBox>().Single(box => box.Label == "Group");
        var port1 = Assert.Single(groupBox.Children.OfType<LayoutPort>());
        Assert.InRange(port1.CentreX, groupBox.X, groupBox.X + groupBox.Width);
        Assert.InRange(port1.CentreY, groupBox.Y, groupBox.Y + groupBox.Height);
    }

    /// <summary>
    ///     Returns true when two points coincide within a small tolerance.
    /// </summary>
    private static bool SamePoint(Point2D a, Point2D b)
    {
        const double tolerance = 1e-6;
        return Math.Abs(a.X - b.X) < tolerance && Math.Abs(a.Y - b.Y) < tolerance;
    }

    /// <summary>
    ///     Returns true when the point lies (within a small tolerance) on the boundary of the box.
    /// </summary>
    private static bool OnBoxBoundary(double x, double y, LayoutBox box)
    {
        const double tolerance = 1e-6;
        var onVerticalEdge =
            (Math.Abs(x - box.X) < tolerance || Math.Abs(x - (box.X + box.Width)) < tolerance) &&
            y >= box.Y - tolerance && y <= box.Y + box.Height + tolerance;
        var onHorizontalEdge =
            (Math.Abs(y - box.Y) < tolerance || Math.Abs(y - (box.Y + box.Height)) < tolerance) &&
            x >= box.X - tolerance && x <= box.X + box.Width + tolerance;
        return onVerticalEdge || onHorizontalEdge;
    }

    /// <summary>
    ///     Proves that a container node's <see cref="LayoutGraphNode.Shape"/>,
    ///     <see cref="LayoutGraphNode.Keyword"/>, and folder-geometry hints, and a nested leaf's
    ///     <see cref="LayoutGraphNode.Compartments"/> and rounded-corner hint, all survive the
    ///     hierarchical engine's sized-view and composition round-trip unchanged.
    /// </summary>
    [Fact]
    public void Apply_NestedGraph_PropagatesContainerAndLeafShapeKeywordCompartments()
    {
        // Arrange: a "package" folder container holding a "part def" leaf with a ports compartment.
        var graph = new LayoutGraph();
        var pkg = graph.AddNode("pkg", 10, 10);
        pkg.Label = "Powertrain";
        pkg.Shape = BoxShape.Folder;
        pkg.Keyword = "package";
        pkg.FolderTabWidth = 82.0;
        pkg.FolderTabHeight = 24.0;

        var engine = pkg.Children.AddNode("engine", 120, 80);
        engine.Label = "Engine";
        engine.Shape = BoxShape.RoundedRectangle;
        engine.Keyword = "part def";
        engine.Compartments = [new LayoutCompartment("ports", ["intake : FluidPort"])];
        engine.RoundedCornerRadius = 14.0;

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert
        var folder = Assert.Single(tree.Nodes.OfType<LayoutBox>());
        Assert.Equal(BoxShape.Folder, folder.Shape);
        Assert.Equal("package", folder.Keyword);
        Assert.Equal(82.0, folder.FolderTabWidth);
        Assert.Equal(24.0, folder.FolderTabHeight);

        var part = Assert.Single(folder.Children.OfType<LayoutBox>());
        Assert.Equal(BoxShape.RoundedRectangle, part.Shape);
        Assert.Equal("part def", part.Keyword);
        Assert.Equal(engine.Compartments, part.Compartments);
        Assert.Equal(14.0, part.RoundedCornerRadius);
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

    /// <summary>
    ///     A leaf-algorithm test double that records the exact <see cref="LayoutGraph"/> and
    ///     <see cref="LayoutOptions"/> instance every <see cref="Apply"/> call receives, then delegates
    ///     to a real inner algorithm so the composed layout remains valid. It advertises the inner
    ///     algorithm's own <see cref="Id"/> so it can be registered in its place, transparently observing
    ///     every scope <see cref="HierarchicalLayoutAlgorithm"/> places with that algorithm identifier.
    /// </summary>
    private sealed class RecordingLayoutAlgorithm(ILayoutAlgorithm inner) : ILayoutAlgorithm
    {
        /// <summary>The graph/options pair captured from every <see cref="Apply"/> invocation, in call order.</summary>
        public List<(LayoutGraph Graph, LayoutOptions Options)> Calls { get; } = [];

        /// <inheritdoc/>
        public string Id => inner.Id;

        /// <inheritdoc/>
        public LayoutTree Apply(LayoutGraph graph, LayoutOptions options)
        {
            Calls.Add((graph, options));
            return inner.Apply(graph, options);
        }
    }
}
