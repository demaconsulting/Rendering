// <copyright file="LayeredLayoutAlgorithmTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests for the bundled <see cref="LayeredLayoutAlgorithm"/>, exercising the full
///     graph-to-placed-tree path.
/// </summary>
public class LayeredLayoutAlgorithmTests
{
    /// <summary>
    ///     Proves that the algorithm advertises the stable "layered" identifier.
    /// </summary>
    [Fact]
    public void Id_IsLayered()
    {
        Assert.Equal("layered", new LayeredLayoutAlgorithm().Id);
    }

    /// <summary>
    ///     Proves that a simple chain graph is placed into left-to-right layers with one box per node
    ///     and one routed connector per edge.
    /// </summary>
    [Fact]
    public void Apply_ChainGraph_PlacesLayeredBoxesAndRoutesEdges()
    {
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        var c = graph.AddNode("c", 80, 40);
        graph.AddEdge("e1", a, b);
        graph.AddEdge("e2", b, c);

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();

        Assert.Equal(3, boxes.Count);
        Assert.Equal(2, lines.Count);
        Assert.True(tree.Width > 0);
        Assert.True(tree.Height > 0);

        // Boxes are emitted in input order; a chain lays out with strictly increasing X.
        Assert.True(boxes[0].X < boxes[1].X);
        Assert.True(boxes[1].X < boxes[2].X);

        // Every connector has at least a start and an end waypoint.
        Assert.All(lines, line => Assert.True(line.Waypoints.Count >= 2));
    }

    /// <summary>
    ///     Proves that a node's <see cref="LayoutGraphNode.Shape"/>, <see cref="LayoutGraphNode.Keyword"/>,
    ///     <see cref="LayoutGraphNode.Compartments"/>, and optional shape-geometry hints flow through to
    ///     the placed <see cref="LayoutBox"/> unchanged, so a caller can select both the visible outline
    ///     and any resolved routing/rendering geometry purely through the input graph model.
    /// </summary>
    [Fact]
    public void Apply_NodeWithShapeKeywordAndCompartments_PropagatesToPlacedBox()
    {
        var graph = new LayoutGraph();
        var node = graph.AddNode("part", 120, 80);
        node.Label = "Engine";
        node.Shape = BoxShape.RoundedRectangle;
        node.Keyword = "part def";
        node.Compartments = [new LayoutCompartment("ports", ["intake : FluidPort", "exhaust : FluidPort"])];
        node.RoundedCornerRadius = 14.0;
        node.FolderTabWidth = 70.0;
        node.FolderTabHeight = 22.0;

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        var box = Assert.Single(tree.Nodes.OfType<LayoutBox>());
        Assert.Equal(BoxShape.RoundedRectangle, box.Shape);
        Assert.Equal("part def", box.Keyword);
        Assert.Equal(node.Compartments, box.Compartments);
        Assert.Equal(14.0, box.RoundedCornerRadius);
        Assert.Equal(70.0, box.FolderTabWidth);
        Assert.Equal(22.0, box.FolderTabHeight);
    }

    /// <summary>
    ///     Proves that an unset <see cref="LayoutGraphNode.Shape"/>, <see cref="LayoutGraphNode.Keyword"/>,
    ///     and <see cref="LayoutGraphNode.Compartments"/> default to a plain rectangle with no keyword or
    ///     compartments, preserving the algorithm's prior behavior for callers that never set them.
    /// </summary>
    [Fact]
    public void Apply_NodeWithNoShapeKeywordOrCompartments_DefaultsToPlainRectangle()
    {
        var graph = new LayoutGraph();
        graph.AddNode("plain", 80, 40);

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        var box = Assert.Single(tree.Nodes.OfType<LayoutBox>());
        Assert.Equal(BoxShape.Rectangle, box.Shape);
        Assert.Null(box.Keyword);
        Assert.Empty(box.Compartments);
    }

    /// <summary>
    ///     Proves that an empty graph produces an empty, positively-sized canvas.
    /// </summary>
    [Fact]
    public void Apply_EmptyGraph_ReturnsEmptyCanvas()
    {
        var tree = new LayeredLayoutAlgorithm().ApplyCore(new LayoutGraph(), new LayoutOptions());

        Assert.Empty(tree.Nodes);
        Assert.True(tree.Width > 0);
        Assert.True(tree.Height > 0);
    }

    /// <summary>
    ///     Proves that a null graph argument is rejected.
    /// </summary>
    [Fact]
    public void Apply_NullGraph_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new LayeredLayoutAlgorithm().ApplyCore(null!, new LayoutOptions()));
    }

    /// <summary>
    ///     Proves that a null options argument is rejected.
    /// </summary>
    [Fact]
    public void Apply_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new LayeredLayoutAlgorithm().ApplyCore(new LayoutGraph(), null!));
    }

    /// <summary>
    ///     Proves that selecting a downward flow direction lays the chain out top-to-bottom: boxes are
    ///     stacked in strictly increasing Y (rather than the default left-to-right increasing X), and
    ///     the canvas is taller than it is wide.
    /// </summary>
    [Fact]
    public void Apply_DownDirection_FlowsTopToBottom()
    {
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        var c = graph.AddNode("c", 80, 40);
        graph.AddEdge("e1", a, b);
        graph.AddEdge("e2", b, c);

        var options = new LayoutOptions();
        options.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, options);

        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        Assert.Equal(3, boxes.Count);

        // A downward chain stacks its boxes vertically: strictly increasing Y.
        Assert.True(boxes[0].Y < boxes[1].Y);
        Assert.True(boxes[1].Y < boxes[2].Y);

        // A three-deep top-to-bottom flow of short boxes is taller than it is wide.
        Assert.True(tree.Height > tree.Width);
    }

    /// <summary>
    ///     Proves that the downward flow is a genuinely different layout from the default rightward
    ///     flow — a regression guard against the option being silently ignored (which would return the
    ///     identical left-to-right coordinates for both directions).
    /// </summary>
    [Fact]
    public void Apply_DownDirection_DiffersFromRight()
    {
        var algorithm = new LayeredLayoutAlgorithm();

        var right = algorithm.ApplyCore(BuildChain(), new LayoutOptions());

        var downOptions = new LayoutOptions();
        downOptions.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        var down = algorithm.ApplyCore(BuildChain(), downOptions);

        // Rightward is wide-and-short; downward is tall-and-narrow. They must not be identical.
        Assert.True(right.Width > right.Height);
        Assert.True(down.Height > down.Width);

        var rightBoxes = right.Nodes.OfType<LayoutBox>().ToList();
        var downBoxes = down.Nodes.OfType<LayoutBox>().ToList();
        Assert.True(rightBoxes[0].X < rightBoxes[2].X);
        Assert.True(downBoxes[0].Y < downBoxes[2].Y);
    }

    /// <summary>
    ///     Proves that the flow direction is honored when carried on the graph scope, mirroring how the
    ///     algorithm resolves its other well-known options (graph scope takes precedence over options).
    /// </summary>
    [Fact]
    public void Apply_DownDirectionOnGraphScope_IsHonored()
    {
        var graph = BuildChain();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        Assert.True(tree.Height > tree.Width);
    }

    /// <summary>
    ///     Proves that the default (unset) direction lays the graph out left-to-right, so existing
    ///     callers that never set the option are unaffected.
    /// </summary>
    [Fact]
    public void Apply_DefaultDirection_FlowsLeftToRight()
    {
        var tree = new LayeredLayoutAlgorithm().ApplyCore(BuildChain(), new LayoutOptions());

        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        Assert.True(boxes[0].X < boxes[1].X);
        Assert.True(boxes[1].X < boxes[2].X);
        Assert.True(tree.Width > tree.Height);
    }

    /// <summary>Builds the standard three-node chain graph used by the direction tests.</summary>
    private static LayoutGraph BuildChain()
    {
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        var c = graph.AddNode("c", 80, 40);
        graph.AddEdge("e1", a, b);
        graph.AddEdge("e2", b, c);
        return graph;
    }

    /// <summary>
    ///     Proves that the hierarchical input-model capability is behavior-preserving: a flat graph
    ///     whose nodes declare no children lays out exactly as before, because the layered algorithm
    ///     reads only the top-level nodes and edges and ignores nesting.
    /// </summary>
    [Fact]
    public void Apply_FlatGraphWithNoChildren_PlacesTopLevelStructureUnchanged()
    {
        // Arrange: build a flat chain graph and confirm none of its nodes are containers
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        var c = graph.AddNode("c", 80, 40);
        graph.AddEdge("e1", a, b);
        graph.AddEdge("e2", b, c);
        Assert.All(new[] { a, b, c }, node => Assert.False(node.HasChildren));

        // Act: lay the flat graph out with the bundled algorithm
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: the placed structure matches the classic flat result (one box per node,
        // one connector per edge, left-to-right chain ordering)
        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();
        Assert.Equal(3, boxes.Count);
        Assert.Equal(2, lines.Count);
        Assert.True(boxes[0].X < boxes[1].X);
        Assert.True(boxes[1].X < boxes[2].X);
    }

    /// <summary>
    ///     Proves that a chain of nodes whose cross-extent is smaller than the port clearance band lays
    ///     out without throwing in every flow direction. Regression guard for the inverted-clamp crash
    ///     in the port distributor: under <see cref="LayoutFlowDirection.Down"/>/<see cref="LayoutFlowDirection.Up"/>
    ///     the port face is sized by the node <em>width</em> (axis swap), so narrow boxes (10/20/30 wide)
    ///     previously produced a <c>min &gt; max</c> range in <c>Math.Clamp</c> and threw an opaque
    ///     <see cref="ArgumentException"/> from deep in the pipeline.
    /// </summary>
    /// <param name="direction">The flow direction under test.</param>
    [Theory]
    [InlineData(LayoutFlowDirection.Right)]
    [InlineData(LayoutFlowDirection.Left)]
    [InlineData(LayoutFlowDirection.Down)]
    [InlineData(LayoutFlowDirection.Up)]
    public void Apply_SmallNodeChain_PlacesWithoutThrowingInEveryDirection(LayoutFlowDirection direction)
    {
        // Arrange: a chain of nodes far narrower than the port-clearance band (10/20/30 wide, 40 tall).
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 10, 40);
        var b = graph.AddNode("b", 20, 40);
        var c = graph.AddNode("c", 30, 40);
        graph.AddEdge("ab", a, b);
        graph.AddEdge("bc", b, c);

        var options = LayoutOptions.ForAlgorithm("layered");
        options.Set(CoreOptions.Direction, direction);

        // Act: laying out must not throw regardless of direction.
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, options);

        // Assert: a valid placed tree with one box per node and one connector per edge.
        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();
        Assert.Equal(3, boxes.Count);
        Assert.Equal(2, lines.Count);
        Assert.True(tree.Width > 0);
        Assert.True(tree.Height > 0);
        Assert.All(lines, line => Assert.True(line.Waypoints.Count >= 2));
    }

    /// <summary>
    ///     Under a <see cref="LayoutFlowDirection.Down"/> flow the target's resolved real face is
    ///     <see cref="PortSide.Top"/>, so a <see cref="BoxShape.Folder"/>-shaped target's routed
    ///     connector lands on the recessed body top (box.Y + folder-tab height) rather than the plain
    ///     bounding-box top edge — proving <see cref="LayeredLayoutAlgorithm"/>'s own routing pipeline
    ///     now gets the same anchor projection <see cref="ConnectorRouter"/> already applies.
    /// </summary>
    [Fact]
    public void Apply_DownDirection_FolderTarget_ProjectsEndpointToRecessedTop()
    {
        var graph = new LayoutGraph();
        var source = graph.AddNode("client", 80, 40);
        var target = graph.AddNode("pkg", 140, 90);
        target.Label = "Utilities";
        target.Shape = BoxShape.Folder;
        target.FolderTabWidth = 60.0;
        target.FolderTabHeight = 24.0;
        graph.AddEdge("uses", source, target);

        var options = new LayoutOptions();
        options.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, options);

        var targetBox = tree.Nodes.OfType<LayoutBox>().Single(b => b.Label == "Utilities");
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());

        // Assert: the connector's final waypoint touches the recessed body top, not the bounding-box edge.
        Assert.Equal(targetBox.Y + 24.0, line.Waypoints[^1].Y, 6);
    }

    /// <summary>
    ///     Under an <see cref="LayoutFlowDirection.Up"/> flow the source's resolved real face is
    ///     <see cref="PortSide.Top"/>, so a <see cref="BoxShape.Folder"/>-shaped source's routed
    ///     connector departs from the recessed body top rather than the plain bounding-box top edge.
    /// </summary>
    [Fact]
    public void Apply_UpDirection_FolderSource_ProjectsEndpointToRecessedTop()
    {
        var graph = new LayoutGraph();
        var source = graph.AddNode("pkg", 140, 90);
        source.Label = "Utilities";
        source.Shape = BoxShape.Folder;
        source.FolderTabWidth = 60.0;
        source.FolderTabHeight = 24.0;
        var target = graph.AddNode("client", 80, 40);
        graph.AddEdge("uses", source, target);

        var options = new LayoutOptions();
        options.Set(CoreOptions.Direction, LayoutFlowDirection.Up);

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, options);

        var sourceBox = tree.Nodes.OfType<LayoutBox>().Single(b => b.Label == "Utilities");
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());

        // Assert: the connector's first waypoint touches the recessed body top, not the bounding-box edge.
        Assert.Equal(sourceBox.Y + 24.0, line.Waypoints[0].Y, 6);
    }

    /// <summary>
    ///     Under a <see cref="LayoutFlowDirection.Right"/> flow the target's resolved real face is
    ///     <see cref="PortSide.Left"/>, which <see cref="BoxShape.Folder"/> never restricts or projects
    ///     (only the real Top face carries a tab), so the connector lands on the plain bounding-box left
    ///     edge exactly as it would for a rectangle — the folder tab must never spuriously affect a face
    ///     other than the real Top face.
    /// </summary>
    [Fact]
    public void Apply_RightDirection_FolderTarget_LeftFaceUnaffectedByTab()
    {
        var graph = new LayoutGraph();
        var source = graph.AddNode("client", 40, 20);
        var target = graph.AddNode("pkg", 140, 90);
        target.Label = "Utilities";
        target.Shape = BoxShape.Folder;
        target.FolderTabWidth = 60.0;
        target.FolderTabHeight = 24.0;
        graph.AddEdge("uses", source, target);

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        var targetBox = tree.Nodes.OfType<LayoutBox>().Single(b => b.Label == "Utilities");
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());

        // Assert: the connector's final waypoint sits exactly on the plain bounding-box left edge.
        Assert.Equal(targetBox.X, line.Waypoints[^1].X, 6);
    }

    /// <summary>
    ///     Under a <see cref="LayoutFlowDirection.Left"/> flow the target's resolved real face is
    ///     <see cref="PortSide.Right"/>, which <see cref="BoxShape.Folder"/> never restricts or
    ///     projects, so the connector lands on the plain bounding-box right edge exactly as it would for
    ///     a rectangle.
    /// </summary>
    [Fact]
    public void Apply_LeftDirection_FolderTarget_RightFaceUnaffectedByTab()
    {
        var graph = new LayoutGraph();
        var source = graph.AddNode("client", 40, 20);
        var target = graph.AddNode("pkg", 140, 90);
        target.Label = "Utilities";
        target.Shape = BoxShape.Folder;
        target.FolderTabWidth = 60.0;
        target.FolderTabHeight = 24.0;
        graph.AddEdge("uses", source, target);

        var options = new LayoutOptions();
        options.Set(CoreOptions.Direction, LayoutFlowDirection.Left);

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, options);

        var targetBox = tree.Nodes.OfType<LayoutBox>().Single(b => b.Label == "Utilities");
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());

        // Assert: the connector's final waypoint sits exactly on the plain bounding-box right edge.
        Assert.Equal(targetBox.X + targetBox.Width, line.Waypoints[^1].X, 6);
    }

    /// <summary>
    ///     A <see cref="BoxShape.Note"/>-shaped target's fold-excluded strip on its real Top face
    ///     (resolved for a <see cref="LayoutFlowDirection.Down"/> flow) is excluded from the connector's
    ///     landing zone, matching <see cref="ConnectorRouter"/>'s own exclusion rule; no surface
    ///     projection applies (a note's fold sits exactly on the bounding-box edge).
    /// </summary>
    [Fact]
    public void Apply_DownDirection_NoteTarget_ExcludesFoldFromLandingZone()
    {
        var graph = new LayoutGraph();
        var source = graph.AddNode("author", 80, 40);
        var target = graph.AddNode("note", 140, 90);
        target.Label = "Design Note";
        target.Shape = BoxShape.Note;
        graph.AddEdge("writes", source, target);

        var options = new LayoutOptions();
        options.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, options);

        var targetBox = tree.Nodes.OfType<LayoutBox>().Single(b => b.Label == "Design Note");
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());
        var end = line.Waypoints[^1];

        // Assert: the connector lands on the plain top edge (no projection offset for Note) but strictly
        // left of the fold-excluded strip near the right edge. A 140x90 note folds
        // min(140, 90) * 0.25 = 22.5, capped at NotationMetrics.NoteFoldMaxSize (16).
        Assert.Equal(targetBox.Y, end.Y, 6);
        Assert.True(
            end.X < targetBox.X + targetBox.Width - NotationMetrics.NoteFoldMaxSize,
            "Target anchor should land left of the fold-excluded strip.");
    }

    /// <summary>
    ///     A plain <see cref="BoxShape.Rectangle"/> chain's placement and routing are byte-for-byte
    ///     unchanged from the pre-shape-awareness behavior: regression guard proving the new shape-aware
    ///     code paths never engage for the default (and by far most common) shape. Uses the same chain
    ///     graph as <see cref="Apply_ChainGraph_PlacesLayeredBoxesAndRoutesEdges"/>.
    /// </summary>
    [Fact]
    public void Apply_RectangleChain_MatchesPreShapeAwarenessOutput()
    {
        var tree = new LayeredLayoutAlgorithm().ApplyCore(BuildChain(), new LayoutOptions());

        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();

        Assert.Equal(3, boxes.Count);
        Assert.Equal(2, lines.Count);

        // Pinned expected values for the 80x40 three-node chain (a->b->c) under the default Right flow.
        Assert.Equal(20.0, boxes[0].X, 6);
        Assert.Equal(20.0, boxes[0].Y, 6);
        Assert.Equal(170.0, boxes[1].X, 6);
        Assert.Equal(20.0, boxes[1].Y, 6);
        Assert.Equal(320.0, boxes[2].X, 6);
        Assert.Equal(20.0, boxes[2].Y, 6);
        Assert.Equal(100.0, lines[0].Waypoints[0].X, 6);
        Assert.Equal(40.0, lines[0].Waypoints[0].Y, 6);
        Assert.Equal(170.0, lines[0].Waypoints[^1].X, 6);
        Assert.Equal(40.0, lines[0].Waypoints[^1].Y, 6);
    }

    /// <summary>
    ///     Proves that the default (unset) <see cref="CoreOptions.NodeSpacing"/> reproduces the engine's
    ///     original fixed 30.0 constant exactly — a regression pin against the option's introduction
    ///     silently changing default output for callers that never set it. Uses a two-child fan-out
    ///     (both children the same size) so the sibling gap is driven purely by node spacing; the
    ///     Brandes-Köpf balanced-layout averaging this pinned value reflects is an internal detail this
    ///     test does not need to reproduce by hand, only pin against regressions.
    /// </summary>
    [Fact]
    public void Apply_DefaultNodeSpacing_MatchesPriorEngineBehavior()
    {
        var tree = new LayeredLayoutAlgorithm().ApplyCore(BuildFanOut(), new LayoutOptions());

        Assert.Equal(-5.0, SiblingGap(tree), 6);
    }

    /// <summary>
    ///     Proves that a larger <see cref="CoreOptions.NodeSpacing"/> strictly widens the gap between
    ///     siblings stacked in the same layer — a regression guard against the option being silently
    ///     ignored (which would return the same gap regardless of the requested value).
    /// </summary>
    [Fact]
    public void Apply_LargerNodeSpacing_WidensGapBetweenSiblings()
    {
        var smallOptions = new LayoutOptions();
        smallOptions.Set(CoreOptions.NodeSpacing, 40.0);
        var smallGap = SiblingGap(new LayeredLayoutAlgorithm().ApplyCore(BuildFanOut(), smallOptions));

        var largeOptions = new LayoutOptions();
        largeOptions.Set(CoreOptions.NodeSpacing, 100.0);
        var largeGap = SiblingGap(new LayeredLayoutAlgorithm().ApplyCore(BuildFanOut(), largeOptions));

        Assert.True(largeGap > smallGap);
    }

    /// <summary>
    ///     Proves that node spacing is honored when carried on the graph scope, mirroring how the
    ///     algorithm resolves its other well-known options: an explicit value on the graph takes
    ///     precedence over a conflicting value on the options.
    /// </summary>
    [Fact]
    public void Apply_NodeSpacingOnGraphScope_TakesPrecedenceOverOptions()
    {
        var graphWithOverride = BuildFanOut();
        graphWithOverride.Set(CoreOptions.NodeSpacing, 100.0);
        var options = new LayoutOptions();
        options.Set(CoreOptions.NodeSpacing, 40.0);
        var overriddenGap = SiblingGap(new LayeredLayoutAlgorithm().ApplyCore(graphWithOverride, options));

        var graphOnly = BuildFanOut();
        graphOnly.Set(CoreOptions.NodeSpacing, 100.0);
        var graphOnlyGap = SiblingGap(new LayeredLayoutAlgorithm().ApplyCore(graphOnly, new LayoutOptions()));

        var optionsOnlyGap = SiblingGap(new LayeredLayoutAlgorithm().ApplyCore(BuildFanOut(), options));

        // The graph's 100.0 wins over the options' 40.0, matching a graph-only resolution...
        Assert.Equal(graphOnlyGap, overriddenGap, 6);

        // ...and differs from what the options' 40.0 alone would have produced.
        Assert.NotEqual(optionsOnlyGap, overriddenGap);
    }

    /// <summary>Gets the vertical gap between the two stacked sibling boxes in a <see cref="BuildFanOut"/> tree.</summary>
    private static double SiblingGap(LayoutTree tree)
    {
        var boxes = tree.Nodes.OfType<LayoutBox>().OrderBy(b => b.Y).ToList();
        var sibling1 = boxes[1];
        var sibling2 = boxes[2];
        return sibling2.Y - (sibling1.Y + sibling1.Height);
    }

    /// <summary>Builds a three-node fan-out graph: one source with two same-sized children.</summary>
    private static LayoutGraph BuildFanOut()
    {
        var graph = new LayoutGraph();
        var source = graph.AddNode("source", 80, 40);
        var child1 = graph.AddNode("child1", 80, 40);
        var child2 = graph.AddNode("child2", 80, 40);
        graph.AddEdge("e1", source, child1);
        graph.AddEdge("e2", source, child2);
        return graph;
    }

    /// <summary>
    ///     Proves that with the default <see cref="CoreOptions.MergeParallelEdges"/> (<see langword="true"/>),
    ///     three parallel edges between the same two nodes collapse into exactly one rendered
    ///     <see cref="LayoutLine"/>, fixing the pre-existing latent bug where every original edge
    ///     emitted its own stacked line.
    /// </summary>
    [Fact]
    public void Apply_ParallelEdges_MergeParallelEdgesDefaultTrue_EmitsExactlyOneLine()
    {
        // Arrange: two nodes joined by three distinct parallel edges.
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        graph.AddEdge("e1", a, b);
        graph.AddEdge("e2", a, b);
        graph.AddEdge("e3", a, b);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: only one connector survives.
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();
        Assert.Single(lines);
    }

    /// <summary>
    ///     Proves that with <see cref="CoreOptions.MergeParallelEdges"/> set to <see langword="false"/>,
    ///     each of three parallel edges between the same two nodes is retained as its own
    ///     independently-routed <see cref="LayoutLine"/>.
    /// </summary>
    [Fact]
    public void Apply_ParallelEdges_MergeParallelEdgesFalse_RetainsEveryEdge()
    {
        // Arrange: two nodes joined by three distinct parallel edges, each with its own label.
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        graph.AddEdge("e1", a, b).Label = "first";
        graph.AddEdge("e2", a, b).Label = "second";
        graph.AddEdge("e3", a, b).Label = "third";

        var options = new LayoutOptions();
        options.Set(CoreOptions.MergeParallelEdges, false);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, options);

        // Assert: all three connectors survive, each keeping its own label.
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();
        Assert.Equal(3, lines.Count);
        Assert.Equal(["first", "second", "third"], lines.Select(l => l.MidpointLabel).OrderBy(l => l).ToList());
    }

    /// <summary>
    ///     Proves that with the default <see cref="CoreOptions.MergeParallelEdges"/> (<see langword="true"/>),
    ///     when 2+ raw parallel edges collapse into a single rendered <see cref="LayoutLine"/>, the
    ///     midpoint label is omitted entirely — never "first survivor wins" — even when every
    ///     collapsed edge happened to carry an identical label.
    /// </summary>
    [Fact]
    public void Apply_ParallelEdges_MergeParallelEdgesDefaultTrue_OmitsLabelOnCollapse()
    {
        // Arrange: two nodes joined by three labeled parallel edges.
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        graph.AddEdge("e1", a, b).Label = "primary";
        graph.AddEdge("e2", a, b).Label = "retry";
        graph.AddEdge("e3", a, b).Label = "audit";

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: exactly one line survives, and its label is omitted (not "primary", the first
        // edge's label).
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());
        Assert.Null(line.MidpointLabel);
    }

    /// <summary>
    ///     Proves that a genuinely single edge between two nodes (nothing collapsed) keeps its own
    ///     label even when <see cref="CoreOptions.MergeParallelEdges"/> is <see langword="true"/>.
    /// </summary>
    [Fact]
    public void Apply_SingleEdge_MergeParallelEdgesDefaultTrue_KeepsOwnLabel()
    {
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        graph.AddEdge("e1", a, b).Label = "only";

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());
        Assert.Equal("only", line.MidpointLabel);
    }

    /// <summary>
    ///     Regression test for a bug where two edges declared in opposite directions between the same
    ///     node pair (e.g. A-&gt;B and B-&gt;A), with the default <see cref="CoreOptions.MergeParallelEdges"/>
    ///     (<see langword="true"/>), collapsed inconsistently: the engine's cycle-breaking stage
    ///     normalizes direction (reversing cycle-causing back edges) before deciding which edges
    ///     collapse together, so it correctly recognized the two edges as parallel and dropped one —
    ///     but the emission loop's own duplicate rule compared the raw, un-normalized declared
    ///     direction, so it did not recognize them as a collapsed pair and still tried to emit the
    ///     dropped edge. That edge had no route computed for it, so it fell back to a raw straight
    ///     line between the two node centres — cutting through both boxes instead of a proper
    ///     orthogonal connector. Proves exactly one properly-anchored line survives instead.
    /// </summary>
    [Fact]
    public void Apply_OppositeDirectionParallelEdges_MergeParallelEdgesDefaultTrue_CollapsesToOneProperlyRoutedLine()
    {
        // Arrange: two nodes joined by edges declared in opposite directions.
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        a.Label = "a";
        var b = graph.AddNode("b", 80, 40);
        b.Label = "b";
        graph.AddEdge("e1", a, b);
        graph.AddEdge("e2", b, a);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: only one connector survives, and it is a genuinely-anchored route (not the
        // centre-to-centre fallback used for an unresolved/dropped edge).
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());
        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        var boxA = boxes.Single(box => box.Label == "a");
        var boxB = boxes.Single(box => box.Label == "b");
        var centreA = new Point2D(boxA.X + (boxA.Width / 2.0), boxA.Y + (boxA.Height / 2.0));
        var centreB = new Point2D(boxB.X + (boxB.Width / 2.0), boxB.Y + (boxB.Height / 2.0));
        Assert.NotEqual(centreA, line.Waypoints[0]);
        Assert.NotEqual(centreB, line.Waypoints[^1]);
    }

    /// <summary>
    ///     Regression test for a bug where <see cref="CoreOptions.MergeParallelEdges"/> set to
    ///     <see langword="false"/> was silently ignored for any node pair that ended up inside a
    ///     connected component alongside 2+ total components: the internal engine's per-component
    ///     packing stage built each component's child graph without propagating the caller's
    ///     <c>MergeParallelEdges</c> override, so it silently reverted to the engine's own default
    ///     (<see langword="true"/>) and collapsed the parallel edges anyway — leaving one of them with
    ///     no computed route, which fell back to a crude straight line between the two node centres.
    ///     Proves both parallel edges now survive as their own properly-routed, independently-labeled
    ///     connectors even when an unrelated, entirely disconnected second component is also present.
    /// </summary>
    [Fact]
    public void Apply_MergeParallelEdgesFalse_MultiComponentGraph_RetainsEveryParallelEdge()
    {
        // Arrange: two nodes {a,b} joined by two parallel edges (mirroring a "connect" +
        // "dependency" pair between the same two boxes), plus an entirely unrelated, disconnected
        // pair {c,d} forming a second connected component so the multi-component packing path runs.
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.MergeParallelEdges, false);

        var a = graph.AddNode("a", 80, 40);
        a.Label = "a";
        var b = graph.AddNode("b", 80, 40);
        b.Label = "b";
        graph.AddEdge("connect", a, b).Label = "connect";
        graph.AddEdge("dependency", a, b).Label = "dependency";

        var c = graph.AddNode("c", 80, 40);
        var d = graph.AddNode("d", 80, 40);
        graph.AddEdge("cd", c, d);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: both parallel edges between a and b survive, each keeping its own label, and
        // neither degenerates to the centre-to-centre fallback used for an unresolved/dropped edge.
        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        var boxA = boxes.Single(box => box.Label == "a");
        var boxB = boxes.Single(box => box.Label == "b");
        var centreA = new Point2D(boxA.X + (boxA.Width / 2.0), boxA.Y + (boxA.Height / 2.0));
        var centreB = new Point2D(boxB.X + (boxB.Width / 2.0), boxB.Y + (boxB.Height / 2.0));

        var abLines = tree.Nodes.OfType<LayoutLine>()
            .Where(l => l.MidpointLabel is "connect" or "dependency")
            .ToList();
        Assert.Equal(2, abLines.Count);
        Assert.Equal(["connect", "dependency"], abLines.Select(l => l.MidpointLabel).OrderBy(l => l).ToList());

        foreach (var line in abLines)
        {
            Assert.NotEqual(centreA, line.Waypoints[0]);
            Assert.NotEqual(centreB, line.Waypoints[^1]);
        }
    }

    /// <summary>
    ///     Proves that a per-graph <see cref="CoreOptions.MergeParallelEdges"/> override wins over an
    ///     options-scope value, consistent with every other resolved option in this algorithm.
    /// </summary>
    [Fact]
    public void Apply_MergeParallelEdges_GraphOverridesOptions()
    {
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        graph.AddEdge("e1", a, b);
        graph.AddEdge("e2", a, b);
        graph.Set(CoreOptions.MergeParallelEdges, false);

        var options = new LayoutOptions();
        options.Set(CoreOptions.MergeParallelEdges, true);

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, options);

        Assert.Equal(2, tree.Nodes.OfType<LayoutLine>().Count());
    }

    /// <summary>
    ///     Proves that an edge whose endpoint is a named <see cref="LayoutGraphPort"/> emits a
    ///     <see cref="LayoutPort"/> anchored at the routed connector's endpoint waypoint, carrying the
    ///     port's <see cref="LayoutGraphPort.ExternalLabel"/> as its label.
    /// </summary>
    [Fact]
    public void Apply_EdgeWithPortEndpoint_EmitsLayoutPortWithExternalLabel()
    {
        // Arrange: a source node with a named, labeled port feeding a plain target node.
        var graph = new LayoutGraph();
        var source = graph.AddNode("source", 80, 40);
        var target = graph.AddNode("target", 80, 40);
        var port = source.Ports.AddPort("out1");
        port.ExternalLabel = "output";
        graph.AddEdge("e1", port, target);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: exactly one port is emitted, carrying the external label.
        var ports = tree.Nodes.OfType<LayoutPort>().ToList();
        var port1 = Assert.Single(ports);
        Assert.Equal("output", port1.ExternalLabel);

        // The port anchor lies exactly on the source box's boundary (its right face, since the
        // port is the edge's source feeding rightward toward the target).
        var sourceBox = tree.Nodes.OfType<LayoutBox>().OrderBy(b => b.X).First();
        Assert.Equal(sourceBox.X + sourceBox.Width, port1.CentreX, 6);
    }

    /// <summary>
    ///     Proves that a node with a labeled port on its left side gets a non-zero
    ///     <see cref="LayoutBox.ContentInsetLeft"/> sized to fit the label, while a node with no ports
    ///     gets a zero inset on every side.
    /// </summary>
    [Fact]
    public void Apply_NodeWithLeftPort_ComputesNonZeroContentInsetLeft()
    {
        // Arrange: a target node with a long-labeled port on its left (incoming) side.
        var graph = new LayoutGraph();
        var source = graph.AddNode("source", 80, 40);
        var target = graph.AddNode("target", 80, 40);
        var port = target.Ports.AddPort("in1");
        port.ExternalLabel = "a rather long incoming label";
        graph.AddEdge("e1", source, port);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: the target box (which receives the port) has a positive left inset, while its
        // other three sides (and the port-free source box) stay zero.
        var boxes = tree.Nodes.OfType<LayoutBox>().OrderBy(b => b.X).ToList();
        var sourceBox = boxes[0];
        var targetBox = boxes[1];
        Assert.True(targetBox.ContentInsetLeft > 0);
        Assert.Equal(0.0, targetBox.ContentInsetTop);
        Assert.Equal(0.0, targetBox.ContentInsetBottom);

        Assert.Equal(0.0, sourceBox.ContentInsetLeft);
        Assert.Equal(0.0, sourceBox.ContentInsetRight);
        Assert.Equal(0.0, sourceBox.ContentInsetTop);
        Assert.Equal(0.0, sourceBox.ContentInsetBottom);
    }

    /// <summary>
    ///     Proves that <see cref="LayoutPort.MaxLabelWidth"/> is computed from the owning box's
    ///     (post-auto-grow) width — roughly half the box's inner width — rather than left unconstrained,
    ///     so a renderer can squeeze an overlong port label instead of letting it overlap the opposite
    ///     port's label region.
    /// </summary>
    [Fact]
    public void Apply_EdgeWithPortEndpoint_ComputesFiniteMaxLabelWidth()
    {
        var graph = new LayoutGraph();
        var source = graph.AddNode("source", 200, 60);
        var target = graph.AddNode("target", 200, 60);
        var port = source.Ports.AddPort("out1");
        port.ExternalLabel = "output";
        graph.AddEdge("e1", port, target);

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        var port1 = Assert.Single(tree.Nodes.OfType<LayoutPort>());
        Assert.False(double.IsPositiveInfinity(port1.MaxLabelWidth));

        var sourceBox = tree.Nodes.OfType<LayoutBox>().OrderBy(b => b.X).First();
        Assert.True(port1.MaxLabelWidth <= sourceBox.Width / 2.0);
    }

    /// <summary>
    ///     Proves that a node with a long left-port <see cref="LayoutGraphPort.ExternalLabel"/> is
    ///     auto-grown wide enough that <see cref="LayoutPort.MaxLabelWidth"/> (half the placed width
    ///     minus <c>PortLabelClearance</c>) is never smaller than the label's own measured width — i.e.
    ///     the box that already grew to reserve <see cref="LayoutBox.ContentInsetLeft"/> for the label
    ///     no longer separately squeezes that same label via <c>textLength</c>/<c>lengthAdjust</c>.
    /// </summary>
    [Fact]
    public void Apply_NodeWithLongLeftPortLabel_GrowsWidthEnoughToAvoidSqueeze()
    {
        // Arrange: mirrors the ports-showcase-horizontal gallery repro — a small caller-supplied
        // target box receiving a long-labeled left (incoming) port.
        const string label = "a rather long incoming data label";
        var graph = new LayoutGraph();
        var source = graph.AddNode("source", 80, 40);
        var target = graph.AddNode("target", 80, 40);
        var port = target.Ports.AddPort("in1");
        port.ExternalLabel = label;
        graph.AddEdge("e1", source, port);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: the target box grew past its caller-supplied 80px width to accommodate the floor.
        var targetBox = tree.Nodes.OfType<LayoutBox>().Single(b => b.Label == null && b.ContentInsetLeft > 0);
        Assert.True(targetBox.Width > 80.0, $"Expected target width to grow past 80, was {targetBox.Width}.");

        // Assert: the emitted port's MaxLabelWidth is no longer a squeeze — it is at least as wide as
        // the label's own measured width, using the same internal estimator production code uses.
        const double assumedFontSize = 12.0; // CoreOptions.AssumedFontSize default
        var measuredLabelWidth = PortLabelWidthEstimator.MeasureWidth(label, assumedFontSize);
        var port1 = Assert.Single(tree.Nodes.OfType<LayoutPort>());
        Assert.True(
            port1.MaxLabelWidth >= measuredLabelWidth,
            $"Expected MaxLabelWidth >= {measuredLabelWidth} (no squeeze), was {port1.MaxLabelWidth}.");
    }

    /// <summary>
    ///     Proves that a node with long labeled ports on <em>both</em> its left and right sides grows
    ///     wide enough that both sides independently satisfy the no-squeeze floor, confirming the
    ///     <c>Math.Max(minWidth, 2 * insetLeft)</c> / <c>Math.Max(minWidth, 2 * insetRight)</c>
    ///     composition does not let one side's floor starve the other.
    /// </summary>
    [Fact]
    public void Apply_NodeWithLongLeftAndRightPortLabels_BothSidesAvoidSqueeze()
    {
        const string leftLabel = "a rather long incoming data label";
        const string rightLabel = "an equally long outgoing result label";
        var graph = new LayoutGraph();
        var source = graph.AddNode("source", 80, 40);
        var hub = graph.AddNode("hub", 80, 40);
        var target = graph.AddNode("target", 80, 40);
        var inPort = hub.Ports.AddPort("in1");
        inPort.ExternalLabel = leftLabel;
        var outPort = hub.Ports.AddPort("out1");
        outPort.ExternalLabel = rightLabel;
        graph.AddEdge("e1", source, inPort);
        graph.AddEdge("e2", outPort, target);

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        var hubBox = tree.Nodes.OfType<LayoutBox>().Single(b => b.ContentInsetLeft > 0 && b.ContentInsetRight > 0);
        Assert.True(hubBox.Width > 80.0, $"Expected hub width to grow past 80, was {hubBox.Width}.");

        const double assumedFontSize = 12.0; // CoreOptions.AssumedFontSize default
        var measuredLeftWidth = PortLabelWidthEstimator.MeasureWidth(leftLabel, assumedFontSize);
        var measuredRightWidth = PortLabelWidthEstimator.MeasureWidth(rightLabel, assumedFontSize);

        var ports = tree.Nodes.OfType<LayoutPort>().ToList();
        var inLayoutPort = Assert.Single(ports, p => p.ExternalLabel == leftLabel);
        var outLayoutPort = Assert.Single(ports, p => p.ExternalLabel == rightLabel);
        Assert.True(
            inLayoutPort.MaxLabelWidth >= measuredLeftWidth,
            $"Expected left MaxLabelWidth >= {measuredLeftWidth}, was {inLayoutPort.MaxLabelWidth}.");
        Assert.True(
            outLayoutPort.MaxLabelWidth >= measuredRightWidth,
            $"Expected right MaxLabelWidth >= {measuredRightWidth}, was {outLayoutPort.MaxLabelWidth}.");
    }

    /// <summary>
    ///     Proves that a titled node with two labeled ports stacked on its left (or right) face reserves
    ///     enough bottom clearance for the bottom-most port's downward-shifted label
    ///     (<c>SvgRenderer.EmitPortLabel</c> and the Skia renderer's equivalent draw a Left/Right port's
    ///     <see cref="LayoutGraphPort.ExternalLabel"/> at <c>CentreY + FontSizeBody / 2</c>, not centered
    ///     on the port row). Regression coverage for the gallery bug where "Hub"'s bottom-most labeled
    ///     port ("heartbeat"/"diag") rendered its label past the box's own bottom border: the growth
    ///     floor's per-port compensation for this downward shift was a flat addend divided across every
    ///     port's own equal-height slice (PortDistributor.DistributePorts centers each port within its
    ///     own equal-height slice of the face), so it was fully absorbed only when a face carried
    ///     exactly one port — a face with 2+ labeled ports lost most of that compensation to the other
    ///     slices. Asserts the bottom-most port's own margin to the box's bottom edge covers both the
    ///     downward shift itself and the shifted label's own half-height (2 * FontSizeBody / 2 =
    ///     assumedFontSize), matching the margin the single-port case already achieves comfortably.
    /// </summary>
    [Fact]
    public void Apply_TitledNodeWithTwoLeftPortLabels_BottomPortRetainsDownwardShiftMargin()
    {
        const double assumedFontSize = 12.0; // CoreOptions.AssumedFontSize default
        var graph = new LayoutGraph();
        var sourceA = graph.AddNode("sourceA", 80, 30);
        var sourceB = graph.AddNode("sourceB", 80, 30);
        var target = graph.AddNode("target", 80, 30);
        var hub = graph.AddNode("hub", 200, 60);
        hub.Label = "Hub";
        var topLeftPort = hub.Ports.AddPort("telemetry");
        topLeftPort.ExternalLabel = "telemetry";
        var bottomLeftPort = hub.Ports.AddPort("heartbeat");
        bottomLeftPort.ExternalLabel = "heartbeat";
        graph.AddEdge("e1", sourceA, topLeftPort);
        graph.AddEdge("e2", sourceB, bottomLeftPort);
        graph.AddEdge("e3", hub, target);

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        var hubBox = tree.Nodes.OfType<LayoutBox>().Single(b => b.Label == "Hub");
        var ports = tree.Nodes.OfType<LayoutPort>()
            .Where(p => p.Side == PortSide.Left)
            .OrderBy(p => p.CentreY)
            .ToList();
        Assert.Equal(2, ports.Count);

        var boxBottom = hubBox.Y + hubBox.Height;
        var bottomPortMargin = boxBottom - ports[^1].CentreY;
        Assert.True(
            bottomPortMargin >= assumedFontSize,
            $"Expected bottom-most left port's margin to box bottom ({bottomPortMargin}) to be at least "
            + $"{assumedFontSize} — enough room for SvgRenderer.EmitPortLabel's own "
            + "CentreY + FontSizeBody / 2 downward shift plus the shifted label's own half-height.");
    }

    /// <summary>
    ///     Proves that a node with several <em>unlabeled</em> outgoing edges fanning out from one face
    ///     still grows tall enough to give each connector a minimum clearance slice, even though none
    ///     of the edges or ports carry any label. Regression coverage for the reported "Motherboard"
    ///     bug: a face with 2+ anchors used to skip the entire growth floor whenever
    ///     <c>!faceInfo.AnyLabeled &amp;&amp; !sidePortLabeled</c>, so a box with a pile of unlabeled
    ///     ports never grew to give them room — <c>PortDistributor.DistributePorts</c> still
    ///     spread the anchors proportionally, but within whatever (possibly tiny) caller-supplied
    ///     height happened to already exist, visually bunching several connector exit points close
    ///     together. Asserts the box grows past its small caller-supplied height to at least
    ///     <c>2 * ConnectorClearance</c> per anchor (mirroring the labeled-face floor's baseline term),
    ///     and that adjacent anchor spacing on the fanned-out face is at least that same clearance.
    /// </summary>
    [Fact]
    public void Apply_NodeWithSeveralUnlabeledOutgoingEdges_TooSmall_AutoGrowsHeight()
    {
        var graph = new LayoutGraph();
        var board = graph.AddNode("board", 140, 20);
        var cpu = graph.AddNode("cpu", 80, 30);
        cpu.Label = "cpu";
        var storage = graph.AddNode("storage", 80, 30);
        storage.Label = "storage";
        var network = graph.AddNode("network", 80, 30);
        network.Label = "network";
        var graphics = graph.AddNode("graphics", 80, 30);
        graphics.Label = "graphics";
        graph.AddEdge("e1", board, cpu);
        graph.AddEdge("e2", board, storage);
        graph.AddEdge("e3", board, network);
        graph.AddEdge("e4", board, graphics);

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        var boardBox = tree.Nodes.OfType<LayoutBox>().Single(b => b.Label == null);
        var expectedFloor = 2.0 * LayeredLayoutMetrics.ConnectorClearance * 4;
        Assert.True(
            boardBox.Height >= expectedFloor,
            $"Expected board height to grow to at least {expectedFloor} (2 * ConnectorClearance * "
            + $"Total) for its 4 unlabeled outgoing anchors, was {boardBox.Height}.");

        // Assert: the fix's user-visible goal — the 4 anchors on the board's own face are actually
        // spread apart by at least one ConnectorClearance each, not merely bunched within a taller box.
        // (All 4 lines in this graph originate from board, so no extra source-filtering is needed.)
        var anchorYs = tree.Nodes.OfType<LayoutLine>()
            .Select(l => l.Waypoints[0].Y)
            .OrderBy(y => y)
            .ToList();
        Assert.Equal(4, anchorYs.Count);
        for (var i = 1; i < anchorYs.Count; i++)
        {
            Assert.True(
                anchorYs[i] - anchorYs[i - 1] >= LayeredLayoutMetrics.ConnectorClearance - 1e-6,
                $"Expected adjacent unlabeled anchors to be spaced at least {LayeredLayoutMetrics.ConnectorClearance}px "
                + $"apart, was {anchorYs[i] - anchorYs[i - 1]}.");
        }
    }

    /// <summary>
    ///     Top/Bottom-face mirror of <see cref="Apply_NodeWithSeveralUnlabeledOutgoingEdges_TooSmall_AutoGrowsHeight"/>:
    ///     proves the growth-floor fix applies along the width axis too. The Left/Right branch's
    ///     unlabeled-face floor was fixed to scale by <c>faceInfo.Total</c>
    ///     (<c>clearanceBasedHeight</c>), but the Top/Bottom branch's width floor was initially left
    ///     using the old flat <c>2 * ConnectorClearance</c> term regardless of port count — reproducing
    ///     the exact same bunching bug rotated 90 degrees for several unlabeled ports fanning out from
    ///     a node's bottom face (forced here via <see cref="LayoutFlowDirection.Down"/>). Asserts the
    ///     board grows wide enough to give each of 4 unlabeled anchors at least
    ///     <c>2 * ConnectorClearance</c> of its own width.
    /// </summary>
    [Fact]
    public void Apply_NodeWithSeveralUnlabeledOutgoingEdges_TooNarrow_AutoGrowsWidth()
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        var board = graph.AddNode("board", 20, 20);
        var cpu = graph.AddNode("cpu", 30, 30);
        cpu.Label = "cpu";
        var storage = graph.AddNode("storage", 30, 30);
        storage.Label = "storage";
        var network = graph.AddNode("network", 30, 30);
        network.Label = "network";
        var graphics = graph.AddNode("graphics", 30, 30);
        graphics.Label = "graphics";
        graph.AddEdge("e1", board, cpu);
        graph.AddEdge("e2", board, storage);
        graph.AddEdge("e3", board, network);
        graph.AddEdge("e4", board, graphics);

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        var boardBox = tree.Nodes.OfType<LayoutBox>().Single(b => b.Label == null);
        var expectedFloor = 2.0 * LayeredLayoutMetrics.ConnectorClearance * 4;
        Assert.True(
            boardBox.Width >= expectedFloor,
            $"Expected board width to grow to at least {expectedFloor} (2 * ConnectorClearance * "
            + $"Total) for its 4 unlabeled outgoing anchors on the Bottom face, was {boardBox.Width}.");

        // Assert: the fix's user-visible goal — the 4 anchors on the board's own face are actually
        // spread apart by at least one ConnectorClearance each, not merely bunched within a wider box.
        // (All 4 lines in this graph originate from board, so no extra source-filtering is needed.)
        var anchorXs = tree.Nodes.OfType<LayoutLine>()
            .Select(l => l.Waypoints[0].X)
            .OrderBy(x => x)
            .ToList();
        Assert.Equal(4, anchorXs.Count);
        for (var i = 1; i < anchorXs.Count; i++)
        {
            Assert.True(
                anchorXs[i] - anchorXs[i - 1] >= LayeredLayoutMetrics.ConnectorClearance - 1e-6,
                $"Expected adjacent unlabeled anchors to be spaced at least {LayeredLayoutMetrics.ConnectorClearance}px "
                + $"apart, was {anchorXs[i] - anchorXs[i - 1]}.");
        }
    }

    /// <summary>
    ///     Proves that a titled node with several <em>unlabeled</em> named ports on one face still
    ///     excludes the title band from port placement — even though nothing rendered at those anchors
    ///     could ever collide with the title text, <c>PortDistributor.TitleReserveFor</c> keys
    ///     its exclusion off <c>LayerNode.TitleReserveTop</c>, which is populated whenever the
    ///     node <see cref="LayoutGraphNode.HasPorts"/> — regardless of whether any individual port
    ///     carries an <see cref="LayoutGraphPort.ExternalLabel"/> — so a titled box's unlabeled ports
    ///     are placed no differently than labeled ones with respect to the title. Combines this with
    ///     the growth-floor regression above: uses 6 unlabeled ports (rather than 4) so the
    ///     <c>2 * ConnectorClearance</c>-per-port floor (120) clearly exceeds the height a titled box
    ///     would already reach from its title band alone — otherwise the growth assertion would pass
    ///     even without the fix, since a title reserves height on its own regardless of port count.
    /// </summary>
    [Fact]
    public void Apply_TitledNodeWithSeveralUnlabeledPorts_PortsAvoidTitleBandAndAutoGrowsHeight()
    {
        const int portCount = 6;
        var graph = new LayoutGraph();
        var board = graph.AddNode("board", 140, 20);
        board.Label = "board : Motherboard";

        for (var k = 0; k < portCount; k++)
        {
            var target = graph.AddNode($"target{k}", 80, 30);
            var port = board.Ports.AddPort($"p{k}"); // no ExternalLabel set — carries no text at all
            graph.AddEdge($"e{k}", port, target);
        }

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        var boardBox = tree.Nodes.OfType<LayoutBox>().Single(b => b.Label == "board : Motherboard");
        var expectedFloor = 2.0 * LayeredLayoutMetrics.ConnectorClearance * portCount;
        Assert.True(
            boardBox.Height >= expectedFloor,
            $"Expected board height to grow to at least {expectedFloor} (2 * ConnectorClearance * "
            + $"Total) for its {portCount} unlabeled ports, was {boardBox.Height}.");

        var layoutPorts = tree.Nodes.OfType<LayoutPort>().Where(p => p.Side == PortSide.Right).ToList();
        Assert.Equal(portCount, layoutPorts.Count);

        // The title band sits at the very top of the box; every port must be placed strictly below it
        // (using a generous lower bound of one title-sized band, matching ResolveTitleReserveTop's
        // "roughly double assumedFontSize" comment) even though none of them carry any text.
        const double assumedFontSize = 12.0; // CoreOptions.AssumedFontSize default
        var titleBandBottom = boardBox.Y + (2.0 * assumedFontSize);
        Assert.All(
            layoutPorts,
            p => Assert.True(
                p.CentreY > titleBandBottom,
                $"Expected port at CentreY {p.CentreY} to sit below the title band (bottom "
                + $"{titleBandBottom}), even though it carries no label."));
    }

    /// <summary>
    ///     Proves that a caller-supplied node size already large enough to fit its title and port
    ///     insets is left completely unchanged — the auto-grow floor never shrinks a node, and it does
    ///     not "helpfully" enlarge a node that does not need it.
    /// </summary>
    [Fact]
    public void Apply_NodeAlreadyLargeEnough_SizeUnchanged()
    {
        var graph = new LayoutGraph();
        var node = graph.AddNode("roomy", 400, 300);
        node.Label = "Roomy";

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        var box = Assert.Single(tree.Nodes.OfType<LayoutBox>());
        Assert.Equal(400.0, box.Width, 3);
        Assert.Equal(300.0, box.Height, 3);
    }

    /// <summary>
    ///     Proves that a node whose caller-supplied size is too small to fit its title plus its top and
    ///     bottom port-driven content insets simultaneously is auto-grown taller (never below the
    ///     caller-supplied size) so the title and port labels do not overlap.
    /// </summary>
    [Fact]
    public void Apply_NodeWithTopAndBottomPorts_TooSmall_AutoGrowsHeight()
    {
        // Arrange: a small node with both a top and a bottom port, mirroring the gallery's
        // PortsShowcaseVertical "hub" node (small caller-supplied 120x50 size, title + 2 flat
        // vertical insets demand more height than that).
        var graph = new LayoutGraph();
        var above = graph.AddNode("above", 80, 30);
        var hub = graph.AddNode("hub", 120, 50);
        hub.Label = "Hub";
        var below = graph.AddNode("below", 80, 30);
        var topPort = hub.Ports.AddPort("status");
        topPort.ExternalLabel = "status";
        var bottomPort = hub.Ports.AddPort("ctrl");
        bottomPort.ExternalLabel = "ctrl";
        graph.AddEdge("e1", above, topPort);
        graph.AddEdge("e2", bottomPort, below);

        var options = new LayoutOptions();
        options.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, options);

        // Assert: the hub box grew taller than its caller-supplied 50px height.
        var hubBox = tree.Nodes.OfType<LayoutBox>().Single(b => b.Label == "Hub");
        Assert.True(hubBox.Height > 50.0, $"Expected hub height to grow past 50, was {hubBox.Height}.");
        Assert.True(hubBox.Width >= 120.0);

        // Assert: the top content inset itself (not merely the box's overall height) is deep enough
        // that a renderer's title start position clears the top port's own label. A renderer draws
        // the top port's label at a position derived purely from the port's glyph/font size (roughly
        // PortHalfSize + LabelPadding + FontSizeBody + FontSizeBody/2 below the box's top edge,
        // independent of box height), so only ContentInsetTop can create real clearance.
        const double assumedFontSize = 12.0; // CoreOptions.AssumedFontSize default
        const double portLabelClearance = 4.0; // matches NotationMetrics.PortHalfSize by design
        var requiredInsetTop = (2.0 * assumedFontSize) + (2.0 * portLabelClearance);
        Assert.True(
            hubBox.ContentInsetTop >= requiredInsetTop,
            $"Expected ContentInsetTop >= {requiredInsetTop} to clear the top port's label, was {hubBox.ContentInsetTop}.");
    }

    /// <summary>
    ///     Proves that when auto-growing a node to fit its title/port insets, the packing/spacing stage
    ///     re-runs with the grown size so the grown node never overlaps an already-placed sibling.
    /// </summary>
    [Fact]
    public void Apply_AutoGrownNode_DoesNotOverlapSiblings()
    {
        var graph = new LayoutGraph();
        var above = graph.AddNode("above", 80, 30);
        var hub = graph.AddNode("hub", 120, 50);
        hub.Label = "Hub";
        var below = graph.AddNode("below", 80, 30);
        var sibling = graph.AddNode("sibling", 80, 30);
        var topPort = hub.Ports.AddPort("status");
        topPort.ExternalLabel = "status";
        var bottomPort = hub.Ports.AddPort("ctrl");
        bottomPort.ExternalLabel = "ctrl";
        graph.AddEdge("e1", above, topPort);
        graph.AddEdge("e2", bottomPort, below);
        graph.AddEdge("e3", above, sibling);

        var options = new LayoutOptions();
        options.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, options);

        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        for (var i = 0; i < boxes.Count; i++)
        {
            for (var j = i + 1; j < boxes.Count; j++)
            {
                Assert.False(Overlaps(boxes[i], boxes[j]), $"{boxes[i].Label} overlaps {boxes[j].Label}");
            }
        }
    }

    /// <summary>
    ///     Proves that when no node needs auto-growth (the common case), the layout is unaffected by
    ///     the Fix 5 two-pass mechanism: a diagram with generously-sized nodes and no ports produces
    ///     the same box placements whether or not the growth check ran.
    /// </summary>
    [Fact]
    public void Apply_NoNodeNeedsGrowth_PassTwoSkipped_LayoutUnaffected()
    {
        var graph = BuildFanOut();

        var tree1 = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());
        var tree2 = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        var boxes1 = tree1.Nodes.OfType<LayoutBox>().ToList();
        var boxes2 = tree2.Nodes.OfType<LayoutBox>().ToList();
        Assert.Equal(boxes1.Count, boxes2.Count);
        for (var i = 0; i < boxes1.Count; i++)
        {
            Assert.Equal(boxes1[i].X, boxes2[i].X, 6);
            Assert.Equal(boxes1[i].Y, boxes2[i].Y, 6);
            Assert.Equal(boxes1[i].Width, boxes2[i].Width, 6);
            Assert.Equal(boxes1[i].Height, boxes2[i].Height, 6);
        }
    }

    /// <summary>Returns whether two placed boxes' rectangles overlap (touching edges are not an overlap).</summary>
    private static bool Overlaps(LayoutBox a, LayoutBox b) =>
        a.X < b.X + b.Width && a.X + a.Width > b.X && a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;

    /// <summary>
    ///     Proves that 3 parallel labeled connectors between the same two nodes (mirroring the
    ///     gallery's <c>ParallelEdgesPreserved</c> diagram) are spaced far enough apart that each
    ///     label's natural midpoint lands directly on its own line — no perpendicular nudge is needed
    ///     — instead of the labels colliding and being displaced far from their own connector.
    /// </summary>
    [Fact]
    public void Apply_ThreeParallelLabeledEdges_LabelsLandOnTheirOwnLine()
    {
        // Arrange: two boxes joined by three distinct parallel edges, each with its own label, with
        // MergeParallelEdges disabled so all three survive as independently-routed connectors —
        // mirrors the gallery's ParallelEdgesPreserved diagram exactly (caller-supplied 120x50 boxes).
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.MergeParallelEdges, false);
        var producer = graph.AddNode("producer", 120, 50);
        producer.Label = "Producer";
        var consumer = graph.AddNode("consumer", 120, 50);
        consumer.Label = "Consumer";
        graph.AddEdge("primary", producer, consumer).Label = "primary";
        graph.AddEdge("retry", producer, consumer).Label = "retry";
        graph.AddEdge("audit", producer, consumer).Label = "audit";

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        var lines = tree.Nodes.OfType<LayoutLine>().Where(l => l.MidpointLabel != null).ToList();
        Assert.Equal(3, lines.Count);

        const double assumedFontSize = 12.0; // CoreOptions.AssumedFontSize default
        var placements = ConnectorLabelPlacer.Place(lines, assumedFontSize);

        // Assert: every labeled line's chosen label Y coordinate matches its own line's (straight,
        // horizontal) Y coordinate closely — proving the first-pass (no-nudge) placement succeeded
        // for all three labels instead of one colliding and being pushed far away from its own line.
        foreach (var line in lines)
        {
            Assert.True(placements.TryGetValue(line, out var placement), $"Expected a placement for label '{line.MidpointLabel}'.");
            var lineY = line.Waypoints[0].Y;
            Assert.Equal(lineY, placement.Y, 3);
        }

        // Assert: the three lines' Y coordinates are themselves distinct (still parallel, non-overlapping
        // lanes) and, per the fix, spaced at least a full label-height apart.
        var lineYs = lines.Select(l => l.Waypoints[0].Y).OrderBy(y => y).ToList();
        var labelHeight = ConnectorLabelPlacer.EstimateLabelHeight(assumedFontSize);
        for (var i = 1; i < lineYs.Count; i++)
        {
            Assert.True(
                lineYs[i] - lineYs[i - 1] >= labelHeight - 1e-6,
                $"Expected adjacent parallel lanes to be spaced at least {labelHeight}px apart, was {lineYs[i] - lineYs[i - 1]}.");
        }
    }

    /// <summary>
    ///     Vertical-flow (<see cref="LayoutFlowDirection.Down"/>) mirror of
    ///     <see cref="Apply_ThreeParallelLabeledEdges_LabelsLandOnTheirOwnLine"/>: proves that 3
    ///     parallel labeled connectors attaching to Top/Bottom box faces (where
    ///     <c>PortDistributor</c> spreads anchors horizontally, not vertically) grow the box's WIDTH
    ///     — not its height — to fit the widened lane spacing, and that each label's natural midpoint
    ///     lands directly on its own line's X coordinate with no perpendicular-nudge displacement.
    /// </summary>
    [Fact]
    public void Apply_ThreeParallelLabeledEdges_Down_BoxWidthGrowsAndLabelsLandOnTheirOwnLine()
    {
        // Arrange: two boxes joined by three distinct parallel edges, each with its own label, with
        // MergeParallelEdges disabled so all three survive as independently-routed connectors —
        // the same scenario as the horizontal test above, but with Direction = Down so the shared
        // face is Top/Bottom instead of Left/Right (caller-supplied 120x50 boxes).
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.MergeParallelEdges, false);
        var producer = graph.AddNode("producer", 120, 50);
        producer.Label = "Producer";
        var consumer = graph.AddNode("consumer", 120, 50);
        consumer.Label = "Consumer";
        graph.AddEdge("primary", producer, consumer).Label = "primary";
        graph.AddEdge("retry", producer, consumer).Label = "retry";
        graph.AddEdge("audit", producer, consumer).Label = "audit";

        var options = new LayoutOptions();
        options.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, options);

        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        Assert.Equal(2, boxes.Count);

        // Assert: box WIDTH auto-grew past the caller-supplied 120 to fit the widened lane spacing —
        // the geometrically correct axis for a Top/Bottom face, where PortDistributor spreads anchors
        // horizontally.
        foreach (var box in boxes)
        {
            Assert.True(box.Width > 120.0, $"Expected box '{box.Label}' width to auto-grow past 120, was {box.Width}.");
        }

        var lines = tree.Nodes.OfType<LayoutLine>().Where(l => l.MidpointLabel != null).ToList();
        Assert.Equal(3, lines.Count);

        const double assumedFontSize = 12.0; // CoreOptions.AssumedFontSize default
        var placements = ConnectorLabelPlacer.Place(lines, assumedFontSize);

        // Assert: every labeled line's chosen label X coordinate matches its own line's (straight,
        // vertical) X coordinate closely — proving the first-pass (no-nudge) placement succeeded for
        // all three labels instead of one colliding and being pushed far away from its own line.
        foreach (var line in lines)
        {
            Assert.True(placements.TryGetValue(line, out var placement), $"Expected a placement for label '{line.MidpointLabel}'.");
            var lineX = line.Waypoints[0].X;
            Assert.Equal(lineX, placement.X, 3);
        }

        // Assert: the three lines' X coordinates are themselves distinct (still parallel,
        // non-overlapping lanes) and, per the fix, spaced at least the widest label's own width apart.
        var lineXs = lines.Select(l => l.Waypoints[0].X).OrderBy(x => x).ToList();
        var maxLabelWidth = lines.Max(l => ConnectorLabelPlacer.EstimateLabelWidth(l.MidpointLabel!, assumedFontSize));
        for (var i = 1; i < lineXs.Count; i++)
        {
            Assert.True(
                lineXs[i] - lineXs[i - 1] >= maxLabelWidth - 1e-6,
                $"Expected adjacent parallel lanes to be spaced at least {maxLabelWidth}px apart, was {lineXs[i] - lineXs[i - 1]}.");
        }
    }
}


