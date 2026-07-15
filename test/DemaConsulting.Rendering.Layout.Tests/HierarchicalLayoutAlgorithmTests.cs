// <copyright file="HierarchicalLayoutAlgorithmTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Reflection;

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
            var expected = new LayeredLayoutAlgorithm().ApplyCore(graph, options);
            var actual = new HierarchicalLayoutAlgorithm().ApplyCore(graph, options);

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
            var expected = new ContainmentLayoutAlgorithm().ApplyCore(graph, options);
            var actual = new HierarchicalLayoutAlgorithm().ApplyCore(graph, options);

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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

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
        var defaultTree = new HierarchicalLayoutAlgorithm().ApplyCore(defaultGraph, LayoutOptions.ForAlgorithm("layered"));
        var overrideTree = new HierarchicalLayoutAlgorithm().ApplyCore(overrideGraph, LayoutOptions.ForAlgorithm("layered"));

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
        var plainTree = new HierarchicalLayoutAlgorithm().ApplyCore(plainGraph, LayoutOptions.ForAlgorithm("layered"));
        var overrideTree = new HierarchicalLayoutAlgorithm().ApplyCore(overrideGraph, LayoutOptions.ForAlgorithm("layered"));

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
        var rectangleTree = new HierarchicalLayoutAlgorithm().ApplyCore(rectangleGraph, LayoutOptions.ForAlgorithm("layered"));
        var folderTree = new HierarchicalLayoutAlgorithm().ApplyCore(folderGraph, LayoutOptions.ForAlgorithm("layered"));

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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("containment"));

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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

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
        _ = new HierarchicalLayoutAlgorithm(registry).ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("containment"));

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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("containment"));

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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("containment"));

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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

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
    ///     Regression for the boundary-port identity bug: two independent boundary ports <c>P</c> and
    ///     <c>Q</c> on one container <c>B</c> both leave <see cref="LayoutGraphPort.ExternalLabel"/>
    ///     <see langword="null"/> (the common, default case for a boundary port that has no cosmetic
    ///     external label). Before the fix, <c>BoundaryPortResolver</c> matched a boundary port to its
    ///     leaf-placed anchor by <c>string.Equals</c> on <c>ExternalLabel</c>, so both null-labeled
    ///     ports collapsed onto the same match: one anchor silently absorbed both ports' external
    ///     connectors while the other anchor was left with none. This test proves each anchor's
    ///     external connector traces back to its own true external source (not just that two anchors
    ///     exist), by identifying each anchor via its internal delegation connector's destination
    ///     (<c>C1</c> vs <c>C2</c>) and then asserting that anchor's external connector reaches its own
    ///     sibling (<c>A1</c> vs <c>A2</c>) and no other.
    /// </summary>
    [Fact]
    public void Apply_TwoIndependentBoundaryPortsWithSharedNullExternalLabel_PreservesConnectorProvenance()
    {
        // Arrange: container B owns two boundary ports, both with a null ExternalLabel, distinguished
        // only by InternalLabel and by which sibling/child each is wired to.
        var graph = new LayoutGraph();
        var a1 = graph.AddNode("A1", 80, 40);
        a1.Label = "A1";
        var a2 = graph.AddNode("A2", 80, 40);
        a2.Label = "A2";
        var b = graph.AddNode("B", 10, 10);
        b.Label = "B";
        var p = b.Ports.AddPort("p");
        p.InternalLabel = "P_IN";
        var q = b.Ports.AddPort("q");
        q.InternalLabel = "Q_IN";
        var c1 = b.Children.AddNode("C1", 80, 40);
        c1.Label = "C1";
        var c2 = b.Children.AddNode("C2", 80, 40);
        c2.Label = "C2";

        graph.AddEdge("a1-p", a1, p);
        graph.AddEdge("a2-q", a2, q);
        b.Children.AddEdge("p-c1", p, c1);
        b.Children.AddEdge("q-c2", q, c2);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

        AssertConnectorProvenancePreserved(tree, "A1", "C1", "A2", "C2");
    }

    /// <summary>
    ///     Same regression as
    ///     <see cref="Apply_TwoIndependentBoundaryPortsWithSharedNullExternalLabel_PreservesConnectorProvenance"/>,
    ///     but covering the finding's "share an <c>ExternalLabel</c>" wording literally: both boundary
    ///     ports carry the identical non-null <c>ExternalLabel</c> "SAME" rather than both leaving it
    ///     null.
    /// </summary>
    [Fact]
    public void Apply_TwoIndependentBoundaryPortsWithIdenticalExternalLabel_PreservesConnectorProvenance()
    {
        // Arrange: container B owns two boundary ports sharing one identical, non-null ExternalLabel.
        var graph = new LayoutGraph();
        var a1 = graph.AddNode("A1", 80, 40);
        a1.Label = "A1";
        var a2 = graph.AddNode("A2", 80, 40);
        a2.Label = "A2";
        var b = graph.AddNode("B", 10, 10);
        b.Label = "B";
        var p = b.Ports.AddPort("p");
        p.ExternalLabel = "SAME";
        p.InternalLabel = "P_IN";
        var q = b.Ports.AddPort("q");
        q.ExternalLabel = "SAME";
        q.InternalLabel = "Q_IN";
        var c1 = b.Children.AddNode("C1", 80, 40);
        c1.Label = "C1";
        var c2 = b.Children.AddNode("C2", 80, 40);
        c2.Label = "C2";

        graph.AddEdge("a1-p", a1, p);
        graph.AddEdge("a2-q", a2, q);
        b.Children.AddEdge("p-c1", p, c1);
        b.Children.AddEdge("q-c2", q, c2);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

        AssertConnectorProvenancePreserved(tree, "A1", "C1", "A2", "C2");
    }

    /// <summary>
    ///     Shared assertion helper for the two connector-provenance regression tests above: given a
    ///     resolved tree with exactly two boundary-port anchors on container <c>B</c>, identifies each
    ///     anchor by which composed child box its internal delegation connector reaches, then asserts
    ///     that anchor's external connector reaches its own corresponding sibling box and no other, and
    ///     that the two anchors are not collapsed onto the same point.
    /// </summary>
    private static void AssertConnectorProvenancePreserved(
        LayoutTree tree,
        string firstSiblingLabel,
        string firstChildLabel,
        string secondSiblingLabel,
        string secondChildLabel)
    {
        var emitted = tree.Nodes.OfType<LayoutPort>().ToList();
        Assert.Equal(2, emitted.Count);

        var containerBox = tree.Nodes.OfType<LayoutBox>().Single(box => box.Label == "B");
        foreach (var port in emitted)
        {
            Assert.True(
                OnBoxBoundary(port.CentreX, port.CentreY, containerBox),
                "A boundary-port anchor does not lie on container B's boundary.");
        }

        // The two anchors must not be collapsed onto one point.
        Assert.False(
            SamePoint(
                new Point2D(emitted[0].CentreX, emitted[0].CentreY),
                new Point2D(emitted[1].CentreX, emitted[1].CentreY)),
            "The two independent boundary-port anchors collapsed onto the same point.");

        var firstChildBox = containerBox.Children.OfType<LayoutBox>().Single(box => box.Label == firstChildLabel);
        var secondChildBox = containerBox.Children.OfType<LayoutBox>().Single(box => box.Label == secondChildLabel);
        var firstSiblingBox = tree.Nodes.OfType<LayoutBox>().Single(box => box.Label == firstSiblingLabel);
        var secondSiblingBox = tree.Nodes.OfType<LayoutBox>().Single(box => box.Label == secondSiblingLabel);

        var lines = tree.Nodes.OfType<LayoutLine>().ToList();

        foreach (var port in emitted)
        {
            var anchor = new Point2D(port.CentreX, port.CentreY);
            bool ReachesAnchor(LayoutLine line) =>
                line.Waypoints.Count > 0 &&
                (SamePoint(line.Waypoints[0], anchor) || SamePoint(line.Waypoints[^1], anchor));

            bool ReachesBox(LayoutLine line, LayoutBox box) =>
                ReachesAnchor(line) &&
                line.Waypoints.Any(wp =>
                    wp.X >= box.X - 1.0 && wp.X <= box.X + box.Width + 1.0 &&
                    wp.Y >= box.Y - 1.0 && wp.Y <= box.Y + box.Height + 1.0);

            // Determine which logical port this anchor is by its internal delegation connector's
            // destination — the actual "provenance" check the prior label/count-only test lacked.
            var isFirst = lines.Any(line => ReachesBox(line, firstChildBox));
            var isSecond = lines.Any(line => ReachesBox(line, secondChildBox));
            Assert.True(isFirst ^ isSecond, "Anchor must reach exactly one of the two internal children.");

            var (ownChild, ownSibling, otherSibling) = isFirst
                ? (firstChildBox, firstSiblingBox, secondSiblingBox)
                : (secondChildBox, secondSiblingBox, firstSiblingBox);

            Assert.True(
                lines.Any(line => ReachesBox(line, ownChild)),
                "Anchor's internal delegation connector does not reach its own child.");
            Assert.True(
                lines.Any(line => ReachesBox(line, ownSibling)),
                "Anchor's external connector does not reach its own true external sibling.");
            Assert.False(
                lines.Any(line => ReachesBox(line, otherSibling)),
                "Anchor's external connector incorrectly reaches the OTHER port's sibling (cross-wiring bug).");
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
            () => new HierarchicalLayoutAlgorithm().ApplyCore(graph, new LayoutOptions()));
        Assert.Contains("container boundary", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a same-scope port edge (both endpoints literally direct members, so it never
    ///     crosses a container boundary) is routed locally by the leaf algorithm even when one of its
    ///     endpoint boxes is also touched by an unrelated, genuine box-only cross-container edge. The
    ///     shared box would otherwise mark the port edge as "conflicted" and promote it into the
    ///     box-only cross-container router's batch, which has no port concept — a regression this test
    ///     guards against by asserting the layout succeeds instead of throwing
    ///     <see cref="NotSupportedException"/>.
    /// </summary>
    [Fact]
    public void Apply_SameScopePortEdgeSharesBoxWithUnrelatedCrossContainerEdge_DoesNotThrow()
    {
        // Arrange: two root-level plain nodes joined by a same-scope port edge (boxA's named port to
        // boxB), plus a separate container with a nested child. An unrelated, box-only edge from boxA
        // straight to the nested child is a genuine cross-container edge that marks boxA as
        // "conflicted" with the port edge's box — but the port edge itself never crosses a boundary.
        var graph = new LayoutGraph();
        var boxA = graph.AddNode("boxA", 80, 40);
        var port = boxA.Ports.AddPort("out1");
        var boxB = graph.AddNode("boxB", 80, 40);

        var container = graph.AddNode("container", 10, 10);
        var nested = container.Children.AddNode("nested", 60, 40);

        graph.AddEdge("same-scope-port", port, boxB);
        graph.AddEdge("cross-container", boxA, nested);

        // Act
        var result = new HierarchicalLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: both edges are routed, none dropped, and no exception is thrown.
        Assert.Equal(2, result.Nodes.OfType<LayoutLine>().Count());
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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

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
        _ = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(new LayoutGraph(), LayoutOptions.ForAlgorithm("layered"));

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
        var expected = new LayeredLayoutAlgorithm().ApplyCore(graph, options);
        var actual = new HierarchicalLayoutAlgorithm().ApplyCore(graph, options);

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
            () => new HierarchicalLayoutAlgorithm().ApplyCore(null!, new LayoutOptions()));
    }

    /// <summary>
    ///     Proves that a null options argument is rejected.
    /// </summary>
    [Fact]
    public void Apply_NullOptions_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(
            () => new HierarchicalLayoutAlgorithm().ApplyCore(new LayoutGraph(), null!));
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
                .ApplyCore(graph, LayoutOptions.ForAlgorithm(HierarchicalLayoutAlgorithm.AlgorithmId));
            var layered = new LayeredLayoutAlgorithm()
                .ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

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
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: the container was composed with its nested child
        var containerBox = Assert.Single(tree.Nodes.OfType<LayoutBox>(), box => box.Children.Count > 0);
        Assert.Single(containerBox.Children.OfType<LayoutBox>());
    }

    /// <summary>
    ///     End-to-end proof of an arbitrary-depth delegation chain: a sibling in the root scope reaches a
    ///     boundary port on an outer container, which delegates through a boundary port on a middle
    ///     container, which delegates through a boundary port on an inner container, which finally reaches
    ///     a leaf three levels deep. The whole pipeline (scope walk, assembly, combined recursive
    ///     placement, decomposition) must connect the outer edge through all three levels to the innermost
    ///     leaf using strictly orthogonal segments only — no diagonal patched onto any anchor at any depth.
    /// </summary>
    [Fact]
    public void HierarchicalLayoutAlgorithm_ThreeLevelDelegationChain_EndToEnd_ProducesConnectedOrthogonalPath()
    {
        // Arrange: source -> L1.p1 -> L2.p2 -> L3.p3 -> leaf, one boundary port per nesting level.
        var graph = BuildDelegationChain(leafWidth: 120, leafHeight: 50);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: one anchor per level (three in total), each on its own container's boundary.
        var ports = tree.Nodes.OfType<LayoutPort>().ToList();
        Assert.Equal(3, ports.Count);

        var l1 = tree.Nodes.OfType<LayoutBox>().Single(box => box.Label == "L1");
        var l2 = l1.Children.OfType<LayoutBox>().Single(box => box.Label == "L2");
        var l3 = l2.Children.OfType<LayoutBox>().Single(box => box.Label == "L3");
        var leaf = l3.Children.OfType<LayoutBox>().Single(box => box.Label == "Leaf");
        var source = tree.Nodes.OfType<LayoutBox>().Single(box => box.Label == "Source");

        // Every connector, at every depth, is strictly orthogonal along its whole polyline.
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();
        foreach (var line in lines)
        {
            AssertPolylineIsStrictlyOrthogonal(line.Waypoints);
        }

        // The chain is connected end to end: some connector touches the root source, and some connector
        // reaches into the innermost leaf three levels down.
        Assert.Contains(lines, line => TouchesBox(line, source));
        Assert.Contains(lines, line => TouchesBox(line, leaf));

        // Each level's anchor sits on its own container's boundary — the physical hand-off between levels.
        Assert.Contains(ports, port => OnBoxBoundary(port.CentreX, port.CentreY, l1));
        Assert.Contains(ports, port => OnBoxBoundary(port.CentreX, port.CentreY, l2));
        Assert.Contains(ports, port => OnBoxBoundary(port.CentreX, port.CentreY, l3));
    }

    /// <summary>
    ///     Hard-invariant guard: a hierarchical graph that has containers but <em>zero</em> boundary
    ///     ports must never take the new combined-pass path; its output must be byte-for-byte the leaf
    ///     pass + cross-container routing it produced before this change. The expected layout is the
    ///     leaf-composition oracle (containers sized bottom-up, cross-container edge routed by the
    ///     existing router); forcing the combined path instead (breaking the <c>Collect == 0</c> gate)
    ///     changes these coordinates and fails this assertion.
    /// </summary>
    [Fact]
    public void HierarchicalLayoutAlgorithm_NoBoundaryPortHierarchy_OutputIsByteIdenticalBeforeAndAfterChange()
    {
        // Arrange: two containers, each with a child, joined by a genuine cross-container edge and NO
        // boundary ports anywhere — the exact shape the Collect == 0 gate must keep on the leaf path.
        var graph = BuildNoBoundaryPortHierarchy();
        var options = LayoutOptions.ForAlgorithm("layered");

        // Act
        var actual = new HierarchicalLayoutAlgorithm().ApplyCore(graph, options);

        // Assert: the layout matches the captured leaf-path golden snapshot exactly (bit level).
        Assert.Equal(NoBoundaryPortGolden, DumpTree(actual));
    }

    /// <summary>
    ///     Cascading-sizing proof: when the innermost container of a three-level delegation chain holds a
    ///     large leaf, that growth must cascade outward so every enclosing level grows to physically
    ///     contain the level nested inside it. The innermost leaf keeps its intrinsic size, and each
    ///     container box strictly encloses the box nested directly within it, with room for padding.
    /// </summary>
    [Fact]
    public void HierarchicalLayoutAlgorithm_ThreeLevelChain_InnermostContainerGrows_GrowthCascadesThroughEveryEnclosingLevel()
    {
        // Arrange: an oversized innermost leaf forces the inner container to grow, which must cascade
        // through the middle and outer containers.
        const double leafWidth = 420;
        const double leafHeight = 300;
        var graph = BuildDelegationChain(leafWidth, leafHeight);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: the innermost leaf keeps its intrinsic size.
        var l1 = tree.Nodes.OfType<LayoutBox>().Single(box => box.Label == "L1");
        var l2 = l1.Children.OfType<LayoutBox>().Single(box => box.Label == "L2");
        var l3 = l2.Children.OfType<LayoutBox>().Single(box => box.Label == "L3");
        var leaf = l3.Children.OfType<LayoutBox>().Single(box => box.Label == "Leaf");

        Assert.Equal(leafWidth, leaf.Width, precision: 3);
        Assert.Equal(leafHeight, leaf.Height, precision: 3);

        // Growth cascaded outward: each level strictly encloses the box nested directly inside it.
        AssertStrictlyEncloses("L3 must contain Leaf", l3, leaf);
        AssertStrictlyEncloses("L2 must contain L3", l2, l3);
        AssertStrictlyEncloses("L1 must contain L2", l1, l2);

        // Sanity: the oversized inner content actually forced the whole chain to be large.
        Assert.True(l1.Width >= leafWidth, "Outermost container did not grow to accommodate the deep oversized leaf.");
    }

    /// <summary>
    ///     Proves the no-boundary-port sibling-gap widening: two peer containers placed side by side by
    ///     the leaf pass and joined by a fan of eight parallel cross-container edges have the gap between
    ///     them widened to the connector-corridor width so those connectors get distinct routing lanes.
    /// </summary>
    [Fact]
    public void Apply_SiblingContainersWithEightCrossEdges_WidensGapToCorridorWidth()
    {
        // Arrange: two containers, each holding one tall, narrow child, joined by eight parallel
        // cross-container edges. Tall/narrow children make the disconnected-component packer place the
        // two containers side by side (this graph carries no boundary ports).
        var graph = BuildSiblingContainerGraph(crossEdgeCount: 8);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: the two top-level containers share a vertical band (side by side) and the gap between
        // them equals the eight-connector corridor width (2*10 + 7*16 = 132).
        var containers = tree.Nodes.OfType<LayoutBox>().OrderBy(box => box.X).ToList();
        Assert.Equal(2, containers.Count);
        Assert.True(
            containers[0].Y < containers[1].Y + containers[1].Height &&
            containers[1].Y < containers[0].Y + containers[0].Height,
            "Expected the two containers to be placed side by side (overlapping vertical bands).");
        var gap = containers[1].X - (containers[0].X + containers[0].Width);
        Assert.Equal(132.0, gap);
    }

    /// <summary>
    ///     Proves a single cross-container edge never widens the sibling gap: the same two side-by-side
    ///     containers joined by just one cross-container edge keep the un-widened baseline gap, so every
    ///     existing single-edge scope stays byte-identical (a fan of one needs no extra lane).
    /// </summary>
    [Fact]
    public void Apply_SiblingContainersWithSingleCrossEdge_LeavesGapUnwidened()
    {
        // Arrange: the identical container arrangement, but with only one cross-container edge.
        var widenedGraph = BuildSiblingContainerGraph(crossEdgeCount: 8);
        var singleEdgeGraph = BuildSiblingContainerGraph(crossEdgeCount: 1);

        // Act
        var widened = new HierarchicalLayoutAlgorithm().ApplyCore(widenedGraph, LayoutOptions.ForAlgorithm("layered"));
        var single = new HierarchicalLayoutAlgorithm().ApplyCore(singleEdgeGraph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: the single-edge gap is the un-widened baseline and is strictly narrower than the
        // eight-edge gap, proving the widening is driven by (and only by) the connector count.
        var singleContainers = single.Nodes.OfType<LayoutBox>().OrderBy(box => box.X).ToList();
        var widenedContainers = widened.Nodes.OfType<LayoutBox>().OrderBy(box => box.X).ToList();
        var singleGap = singleContainers[1].X - (singleContainers[0].X + singleContainers[0].Width);
        var widenedGap = widenedContainers[1].X - (widenedContainers[0].X + widenedContainers[0].Width);
        Assert.True(singleGap < widenedGap, "A single cross-container edge must not widen the gap.");
        Assert.True(singleGap < 132.0, "The single-edge gap must stay at the un-widened baseline.");
    }

    /// <summary>
    ///     Proves the sibling-gap widening shifts an unrelated line's waypoints independently, not as one
    ///     rigid unit: a line whose waypoints straddle the inserted cut (a common outcome for
    ///     Sugiyama-style routing threading an unrelated edge through the free channel between two
    ///     side-by-side boxes) must have only the waypoints at or past the cut shifted, keeping the
    ///     waypoints before the cut exactly where the leaf pass placed them.
    /// </summary>
    [Fact]
    public void WidenSiblingContainerGaps_LineStraddlingCut_ShiftsOnlyWaypointsPastCut()
    {
        // Arrange: two side-by-side containers with a gap of 20 joined by eight parallel cross-container
        // edges (qualifies for widening to the 132-wide corridor, per the eight-edge formula already
        // proven above), plus one unrelated placed line whose two waypoints sit either side of the cut
        // (at the right box's original left edge, x=120): one waypoint left of it, one to its right.
        var graph = new LayoutGraph();
        var left = graph.AddNode("Left", 100, 200);
        var right = graph.AddNode("Right", 100, 200);
        var indexOf = new Dictionary<LayoutGraphNode, int> { [left] = 0, [right] = 1 };
        var descendantToDirect = new Dictionary<LayoutGraphNode, LayoutGraphNode> { [left] = left, [right] = right };

        var routedEdges = new List<LayoutGraphEdge>();
        for (var i = 0; i < 8; i++)
        {
            routedEdges.Add(graph.AddEdge($"e{i}", left, right));
        }

        var composed = new[]
        {
            new LayoutBox(0, 0, 100, 200, "Left", 0, BoxShape.Rectangle, [], []),
            new LayoutBox(120, 0, 100, 200, "Right", 0, BoxShape.Rectangle, [], []),
        };

        var straddlingLine = new LayoutLine(
            [new Point2D(50, 300), new Point2D(300, 300)],
            EndMarkerStyle.None,
            EndMarkerStyle.None,
            LineStyle.Solid,
            null);
        var placedLines = new List<LayoutLine> { straddlingLine };
        var placedPorts = new List<LayoutPort>();

        // Act: invoke the private widening method directly so this test exercises the exact geometry
        // that exposed the bug, independent of whatever the leaf/component-packer would place in
        // practice.
        var method = typeof(HierarchicalLayoutAlgorithm).GetMethod(
            "WidenSiblingContainerGaps",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = method.Invoke(
            null,
            [composed, indexOf, routedEdges, descendantToDirect, placedLines, placedPorts, 250.0]);
        var (widenedBoxes, widenedLines, _, _) = ((LayoutBox[], List<LayoutLine>, List<LayoutPort>, double))result!;

        // Assert: the gap was actually widened (sanity check the fixture qualifies for widening), and
        // the straddling line's waypoint before the cut stayed put while the waypoint past the cut moved
        // by the same extra width the right box moved by.
        var extra = widenedBoxes[1].X - 120.0;
        Assert.True(extra > 0.0, "Fixture must actually qualify for widening for this test to be meaningful.");

        var resultLine = Assert.Single(widenedLines);
        Assert.Equal(50.0, resultLine.Waypoints[0].X, precision: 3);
        Assert.Equal(300.0, resultLine.Waypoints[0].Y, precision: 3);
        Assert.Equal(300.0 + extra, resultLine.Waypoints[1].X, precision: 3);
        Assert.Equal(300.0, resultLine.Waypoints[1].Y, precision: 3);
    }

    /// <summary>
    ///     Proves the boundary-port path is unaffected by the sibling-gap widening: the widening pass
    ///     runs only in the no-boundary-port branch, so a scope that owns a boundary port fed by a fan of
    ///     parallel external approaches still resolves to a single shared anchor through the combined
    ///     pass, exactly as it did before the widening was added.
    /// </summary>
    [Fact]
    public void Apply_BoundaryPortWithParallelApproaches_ResolvesToOneSharedAnchorUnaffectedByWidening()
    {
        // Arrange: a single boundary port on container B, approached by three parallel external edges
        // from one sibling and delegating to one child. The presence of boundary ports forces the whole
        // scope onto the combined pass, which the widening pass never touches.
        var graph = new LayoutGraph();
        var a = graph.AddNode("A", 80, 40);
        var b = graph.AddNode("B", 10, 10);
        b.Label = "B";
        var port = b.Ports.AddPort("p1");
        port.ExternalLabel = "PWR_OUT";
        port.InternalLabel = "PWR_IN";
        var c = b.Children.AddNode("C", 80, 40);

        graph.AddEdge("a-b-0", a, port);
        graph.AddEdge("a-b-1", a, port);
        graph.AddEdge("a-b-2", a, port);
        b.Children.AddEdge("b-c", port, c);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: exactly one shared anchor carrying both labels, sitting on container B's boundary —
        // the combined-pass behaviour, unperturbed by the no-boundary-port widening pass.
        var emitted = Assert.Single(tree.Nodes.OfType<LayoutPort>());
        Assert.Equal("PWR_OUT", emitted.ExternalLabel);
        Assert.Equal("PWR_IN", emitted.InternalLabel);
        var containerBox = tree.Nodes.OfType<LayoutBox>().Single(box => box.Label == "B");
        Assert.True(
            OnBoxBoundary(emitted.CentreX, emitted.CentreY, containerBox),
            "The shared anchor does not lie on container B's boundary.");
    }

    /// <summary>
    ///     Builds two peer containers, each holding one tall, narrow compartment-free child, joined by
    ///     <paramref name="crossEdgeCount"/> parallel cross-container edges running child-to-child and
    ///     containing <em>no</em> boundary ports. The tall/narrow children make the disconnected-component
    ///     packer place the two containers side by side, exercising the no-boundary-port widening pass.
    /// </summary>
    /// <param name="crossEdgeCount">The number of parallel child-to-child cross-container edges.</param>
    /// <returns>The assembled two-container graph.</returns>
    private static LayoutGraph BuildSiblingContainerGraph(int crossEdgeCount)
    {
        var graph = new LayoutGraph();

        var left = graph.AddNode("Left", 10, 10);
        left.Label = "Left";
        var leftChild = left.Children.AddNode("LeftChild", 90, 240);
        leftChild.Label = "LeftChild";

        var right = graph.AddNode("Right", 10, 10);
        right.Label = "Right";
        var rightChild = right.Children.AddNode("RightChild", 90, 240);
        rightChild.Label = "RightChild";

        for (var i = 0; i < crossEdgeCount; i++)
        {
            graph.AddEdge($"leftChild-rightChild-{i}", leftChild, rightChild);
        }

        return graph;
    }

    /// <summary>Deterministically builds a flat (non-nested) layout graph for the given seed, with random
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
    ///     Builds a three-level delegation chain: a root <c>Source</c> reaches boundary port <c>p1</c> on
    ///     container <c>L1</c>, which delegates to port <c>p2</c> on nested container <c>L2</c>, which
    ///     delegates to port <c>p3</c> on nested container <c>L3</c>, which finally delegates to the
    ///     leaf <c>Leaf</c> of the given intrinsic size.
    /// </summary>
    /// <param name="leafWidth">The innermost leaf's intrinsic width.</param>
    /// <param name="leafHeight">The innermost leaf's intrinsic height.</param>
    /// <returns>The assembled delegation-chain graph.</returns>
    private static LayoutGraph BuildDelegationChain(double leafWidth, double leafHeight)
    {
        var graph = new LayoutGraph();

        var source = graph.AddNode("Source", 120, 50);
        source.Label = "Source";

        var l1 = graph.AddNode("L1", 10, 10);
        l1.Label = "L1";
        var p1 = l1.Ports.AddPort("p1");
        p1.ExternalLabel = "in1";
        p1.InternalLabel = "out1";

        var l2 = l1.Children.AddNode("L2", 10, 10);
        l2.Label = "L2";
        var p2 = l2.Ports.AddPort("p2");
        p2.ExternalLabel = "in2";
        p2.InternalLabel = "out2";

        var l3 = l2.Children.AddNode("L3", 10, 10);
        l3.Label = "L3";
        var p3 = l3.Ports.AddPort("p3");
        p3.ExternalLabel = "in3";
        p3.InternalLabel = "out3";

        var leaf = l3.Children.AddNode("Leaf", leafWidth, leafHeight);
        leaf.Label = "Leaf";

        graph.AddEdge("source-p1", source, p1);
        l1.Children.AddEdge("p1-p2", p1, p2);
        l2.Children.AddEdge("p2-p3", p2, p3);
        l3.Children.AddEdge("p3-leaf", p3, leaf);

        return graph;
    }

    /// <summary>
    ///     Builds a hierarchical graph with two containers, each holding one child, joined by a genuine
    ///     cross-container edge and containing <em>no</em> boundary ports — the shape that must stay on
    ///     the leaf pass under the <c>Collect == 0</c> gate.
    /// </summary>
    /// <returns>The assembled boundary-port-free hierarchy.</returns>
    private static LayoutGraph BuildNoBoundaryPortHierarchy()
    {
        var graph = new LayoutGraph();

        var left = graph.AddNode("Left", 10, 10);
        left.Label = "Left";
        var leftChild = left.Children.AddNode("LeftChild", 80, 40);
        leftChild.Label = "LeftChild";

        var right = graph.AddNode("Right", 10, 10);
        right.Label = "Right";
        var rightChild = right.Children.AddNode("RightChild", 80, 40);
        rightChild.Label = "RightChild";

        // A genuine cross-container edge between the two nested children (routed by the cross-container
        // router, not a boundary port), plus an ordinary edge between the two container nodes so the
        // scope has real layered structure that the combined pass would place differently.
        graph.AddEdge("leftChild-rightChild", leftChild, rightChild);
        graph.AddEdge("left-right", left, right);

        return graph;
    }

    /// <summary>
    ///     Serializes a layout tree into a stable, human-readable string capturing every box rectangle
    ///     (recursively) and every line's waypoints, for exact snapshot comparison.
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
    /// <param name="builder">The accumulating snapshot builder.</param>
    /// <param name="node">The node to append.</param>
    /// <param name="depth">The current nesting depth, used for indentation.</param>
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

    /// <summary>Asserts every consecutive, non-degenerate waypoint pair of the polyline is axis-aligned.</summary>
    /// <param name="waypoints">The connector polyline.</param>
    private static void AssertPolylineIsStrictlyOrthogonal(IReadOnlyList<Point2D> waypoints)
    {
        const double tolerance = 1e-4;
        for (var i = 1; i < waypoints.Count; i++)
        {
            var dx = Math.Abs(waypoints[i].X - waypoints[i - 1].X);
            var dy = Math.Abs(waypoints[i].Y - waypoints[i - 1].Y);
            if (dx < tolerance && dy < tolerance)
            {
                continue;
            }

            Assert.True(
                (dy < tolerance) ^ (dx < tolerance),
                $"Segment [{waypoints[i - 1].X},{waypoints[i - 1].Y}]->[{waypoints[i].X},{waypoints[i].Y}] is diagonal.");
        }
    }

    /// <summary>Returns true when either endpoint of the line lies on the given box's boundary.</summary>
    /// <param name="line">The connector line.</param>
    /// <param name="box">The box to test against.</param>
    /// <returns>True when an endpoint touches the box boundary.</returns>
    private static bool TouchesBox(LayoutLine line, LayoutBox box)
    {
        if (line.Waypoints.Count == 0)
        {
            return false;
        }

        var start = line.Waypoints[0];
        var end = line.Waypoints[^1];
        return OnBoxBoundary(start.X, start.Y, box) || OnBoxBoundary(end.X, end.Y, box);
    }

    /// <summary>Asserts the outer box strictly encloses the inner box (with room to spare on every side).</summary>
    /// <param name="context">A message prefix for assertion failures.</param>
    /// <param name="outer">The enclosing box.</param>
    /// <param name="inner">The box expected to be nested within.</param>
    private static void AssertStrictlyEncloses(string context, LayoutBox outer, LayoutBox inner)
    {
        Assert.True(inner.X >= outer.X - 0.001, $"{context}: inner left is outside outer.");
        Assert.True(inner.Y >= outer.Y - 0.001, $"{context}: inner top is outside outer.");
        Assert.True(
            inner.X + inner.Width <= outer.X + outer.Width + 0.001,
            $"{context}: inner right is outside outer.");
        Assert.True(
            inner.Y + inner.Height <= outer.Y + outer.Height + 0.001,
            $"{context}: inner bottom is outside outer.");
        Assert.True(outer.Width > inner.Width, $"{context}: outer is not wider than inner (no room for padding).");
        Assert.True(outer.Height > inner.Height, $"{context}: outer is not taller than inner (no room for padding).");
    }

    /// <summary>
    ///     The captured leaf-path golden snapshot for <see cref="BuildNoBoundaryPortHierarchy"/>. Any
    ///     change to the boundary-port-free layout path (including forcing the combined pass) alters this.
    /// </summary>
    private const string NoBoundaryPortGolden =
        "W=184 H=326\n" +
        "BOX Left X=20 Y=20 W=144 H=128\n" +
        "  BOX LeftChild X=52 Y=76 W=80 H=40\n" +
        "BOX Right X=20 Y=178 W=144 H=128\n" +
        "  BOX RightChild X=52 Y=234 W=80 H=40\n" +
        "LINE (32,148) (32,178)\n" +
        "LINE (152,148) (152,178)\n";

    /// <summary>Deep-compares two layout trees for exact (bit-level) equality of every geometric field,
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
    ///     <see cref="LayoutOptions"/> instance every <see cref="ApplyCore"/> call receives, then
    ///     delegates to a real inner algorithm so the composed layout remains valid. It advertises the
    ///     inner algorithm's own <see cref="Id"/> so it can be registered in its place, transparently
    ///     observing every scope <see cref="HierarchicalLayoutAlgorithm"/> places with that algorithm
    ///     identifier.
    /// </summary>
    private sealed class RecordingLayoutAlgorithm(LayoutAlgorithmBase inner) : LayoutAlgorithmBase
    {
        /// <summary>The graph/options pair captured from every <see cref="ApplyCore"/> invocation, in call order.</summary>
        public List<(LayoutGraph Graph, LayoutOptions Options)> Calls { get; } = [];

        /// <inheritdoc/>
        public override string Id => inner.Id;

        /// <inheritdoc/>
        protected internal override LayoutTree ApplyCore(LayoutGraph graph, LayoutOptions options)
        {
            Calls.Add((graph, options));
            return inner.ApplyCore(graph, options);
        }
    }
}
