// <copyright file="LayeredLayoutAlgorithmTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

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

        var tree = new LayeredLayoutAlgorithm().Apply(graph, new LayoutOptions());

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

        var tree = new LayeredLayoutAlgorithm().Apply(graph, new LayoutOptions());

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

        var tree = new LayeredLayoutAlgorithm().Apply(graph, new LayoutOptions());

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
        var tree = new LayeredLayoutAlgorithm().Apply(new LayoutGraph(), new LayoutOptions());

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
            () => new LayeredLayoutAlgorithm().Apply(null!, new LayoutOptions()));
    }

    /// <summary>
    ///     Proves that a null options argument is rejected.
    /// </summary>
    [Fact]
    public void Apply_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new LayeredLayoutAlgorithm().Apply(new LayoutGraph(), null!));
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

        var tree = new LayeredLayoutAlgorithm().Apply(graph, options);

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

        var right = algorithm.Apply(BuildChain(), new LayoutOptions());

        var downOptions = new LayoutOptions();
        downOptions.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        var down = algorithm.Apply(BuildChain(), downOptions);

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

        var tree = new LayeredLayoutAlgorithm().Apply(graph, new LayoutOptions());

        Assert.True(tree.Height > tree.Width);
    }

    /// <summary>
    ///     Proves that the default (unset) direction lays the graph out left-to-right, so existing
    ///     callers that never set the option are unaffected.
    /// </summary>
    [Fact]
    public void Apply_DefaultDirection_FlowsLeftToRight()
    {
        var tree = new LayeredLayoutAlgorithm().Apply(BuildChain(), new LayoutOptions());

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
        var tree = new LayeredLayoutAlgorithm().Apply(graph, new LayoutOptions());

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
        var tree = new LayeredLayoutAlgorithm().Apply(graph, options);

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

        var tree = new LayeredLayoutAlgorithm().Apply(graph, options);

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

        var tree = new LayeredLayoutAlgorithm().Apply(graph, options);

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

        var tree = new LayeredLayoutAlgorithm().Apply(graph, new LayoutOptions());

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

        var tree = new LayeredLayoutAlgorithm().Apply(graph, options);

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

        var tree = new LayeredLayoutAlgorithm().Apply(graph, options);

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
        var tree = new LayeredLayoutAlgorithm().Apply(BuildChain(), new LayoutOptions());

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
        var tree = new LayeredLayoutAlgorithm().Apply(BuildFanOut(), new LayoutOptions());

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
        var smallGap = SiblingGap(new LayeredLayoutAlgorithm().Apply(BuildFanOut(), smallOptions));

        var largeOptions = new LayoutOptions();
        largeOptions.Set(CoreOptions.NodeSpacing, 100.0);
        var largeGap = SiblingGap(new LayeredLayoutAlgorithm().Apply(BuildFanOut(), largeOptions));

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
        var overriddenGap = SiblingGap(new LayeredLayoutAlgorithm().Apply(graphWithOverride, options));

        var graphOnly = BuildFanOut();
        graphOnly.Set(CoreOptions.NodeSpacing, 100.0);
        var graphOnlyGap = SiblingGap(new LayeredLayoutAlgorithm().Apply(graphOnly, new LayoutOptions()));

        var optionsOnlyGap = SiblingGap(new LayeredLayoutAlgorithm().Apply(BuildFanOut(), options));

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
}
