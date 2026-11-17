// <copyright file="DiamondShapeAnchorTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests for <see cref="BoxShape.Diamond"/> outline anchoring (Rendering#58): a connector to a diamond
///     starts or ends on the diamond's outline and leaves it along the axis, instead of on the bounding box
///     with a hook.
/// </summary>
public sealed class DiamondShapeAnchorTests
{
    private const double Tolerance = 0.6;

    /// <summary>
    ///     The Rendering#58 reproduction: a diamond decision with two branch targets laid out downward.
    ///     Both connectors start on the diamond's outline, leave it vertically, and have a clean first run
    ///     measured from the outline.
    /// </summary>
    [Fact]
    public void LayeredLayout_DiamondSourceTwoTargets_Down_FirstRunStartsOnOutlineAndIsClean()
    {
        // Arrange
        var graph = DecisionGraph(LayoutFlowDirection.Down, branches: 2);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert
        var diamond = DiamondBox(tree);
        var lines = DiagramQuality.Lines(tree);
        Assert.Equal(2, lines.Count);
        Assert.All(lines, line =>
        {
            AssertOnOutline(diamond, line.Waypoints[0]);
            Assert.Equal(line.Waypoints[0].X, line.Waypoints[1].X, 6);
            Assert.True(
                DiagramQuality.FirstRun(line.Waypoints) >= Themes.Light.ConnectorStub - 1e-6,
                "first run measured from the outline must keep the connector stub");
        });
    }

    /// <summary>
    ///     A single edge anchors exactly on the diamond's vertex, matching a plain rectangle of the same
    ///     size (depth zero).
    /// </summary>
    [Fact]
    public void LayeredLayout_DiamondSingleEdge_AnchorsOnTheVertex()
    {
        // Arrange
        var graph = DecisionGraph(LayoutFlowDirection.Down, branches: 1);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: the start is the bottom vertex of the diamond.
        var diamond = DiamondBox(tree);
        var line = Assert.Single(DiagramQuality.Lines(tree));
        Assert.Equal(diamond.X + (diamond.Width / 2.0), line.Waypoints[0].X, 6);
        Assert.Equal(diamond.Y + diamond.Height, line.Waypoints[0].Y, 6);
    }

    /// <summary>
    ///     Every flow direction starts on the outline and leaves perpendicular to the face.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    /// <param name="size">The diamond edge length.</param>
    [Theory]
    [InlineData(LayoutFlowDirection.Down, 32)]
    [InlineData(LayoutFlowDirection.Down, 64)]
    [InlineData(LayoutFlowDirection.Up, 40)]
    [InlineData(LayoutFlowDirection.Right, 40)]
    [InlineData(LayoutFlowDirection.Right, 56)]
    [InlineData(LayoutFlowDirection.Left, 40)]
    public void LayeredLayout_DiamondSource_AnyDirection_StartsOnOutlinePerpendicular(
        LayoutFlowDirection direction,
        int size)
    {
        // Arrange
        var graph = DecisionGraph(direction, branches: 3, size: size);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert
        var diamond = DiamondBox(tree);
        var vertical = direction is LayoutFlowDirection.Down or LayoutFlowDirection.Up;
        foreach (var line in DiagramQuality.Lines(tree))
        {
            AssertOnOutline(diamond, line.Waypoints[0]);
            var first = line.Waypoints[0];
            var second = line.Waypoints[1];
            Assert.True(
                vertical ? Math.Abs(first.X - second.X) < 1e-6 : Math.Abs(first.Y - second.Y) < 1e-6,
                "the first segment must leave the outline perpendicular to the face");
        }
    }

    /// <summary>
    ///     A diamond as a merge target: every connector ends on the outline with an axis-aligned final run
    ///     long enough for its marker.
    /// </summary>
    [Fact]
    public void LayeredLayout_DiamondTarget_TwoSources_EndsOnOutlineWithCleanFinalRun()
    {
        // Arrange
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        var a = graph.AddNode("a", 100, 40);
        var b = graph.AddNode("b", 100, 40);
        var merge = graph.AddNode("merge", 40, 40);
        merge.Shape = BoxShape.Diamond;
        graph.AddEdge("ea", a, merge).TargetEnd = EndMarkerStyle.FilledArrow;
        graph.AddEdge("eb", b, merge).TargetEnd = EndMarkerStyle.FilledArrow;

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert
        var diamond = DiamondBox(tree);
        foreach (var line in DiagramQuality.Lines(tree))
        {
            AssertOnOutline(diamond, line.Waypoints[^1]);
            Assert.True(DiagramQuality.LastSegmentAxisAligned(line.Waypoints));
            Assert.True(DiagramQuality.LastRun(line.Waypoints) >= DiagramQuality.MarkerLength(EndMarkerStyle.FilledArrow));
        }
    }

    /// <summary>
    ///     A decision with branches on three faces keeps every anchor on the outline and distinct.
    /// </summary>
    [Fact]
    public void LayeredLayout_DiamondDecisionThreeBranches_AnchorsDistinctAndOnOutline()
    {
        // Arrange
        var graph = DecisionGraph(LayoutFlowDirection.Down, branches: 3);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert
        var diamond = DiamondBox(tree);
        var starts = DiagramQuality.Lines(tree).Select(line => line.Waypoints[0]).ToList();
        Assert.All(starts, start => AssertOnOutline(diamond, start));
        Assert.Equal(3, starts.Select(start => Math.Round(start.X, 3)).Distinct().Count());
    }

    /// <summary>
    ///     <see cref="ConnectorRouter"/> anchors a connector to a placed diamond on its outline.
    /// </summary>
    [Fact]
    public void ConnectorRouter_Route_DiamondTarget_AnchorsOnOutline()
    {
        // Arrange
        var source = new LayoutBox(80, 0, 100, 40, "src", Depth: 0, BoxShape.Rectangle, [], []);
        var diamond = new LayoutBox(100, 140, 60, 60, "dec", Depth: 0, BoxShape.Diamond, [], []);
        var connection = new Connection(source, diamond, EndMarkerStyle.FilledArrow);

        // Act
        var line = ConnectorRouter.Route([source, diamond], connection, new ConnectorRouteOptions());

        // Assert
        AssertOnOutline(diamond, line.Waypoints[^1]);
        Assert.True(DiagramQuality.LastSegmentAxisAligned(line.Waypoints));
    }

    /// <summary>
    ///     A diamond source routed across containers by the hierarchical algorithm keeps its anchor on the
    ///     outline.
    /// </summary>
    [Fact]
    public void HierarchicalLayout_DiamondInsideContainer_CrossContainerEdgeStartsOnOutline()
    {
        // Arrange: the decision lives in a container, the target outside.
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        var group = graph.AddNode("group", 120, 80);
        group.Label = "Group";
        var decision = group.Children.AddNode("decision", 40, 40);
        decision.Shape = BoxShape.Diamond;
        var target = graph.AddNode("target", 100, 40);
        graph.AddEdge("e", decision, target).TargetEnd = EndMarkerStyle.FilledArrow;

        // Act
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert
        var diamond = AllBoxes(tree.Nodes).Single(box => box.Shape == BoxShape.Diamond);
        var line = Assert.Single(DiagramQuality.Lines(tree));
        AssertOnOutline(diamond, line.Waypoints[0]);
    }

    /// <summary>
    ///     The diamond geometry exposes the full face as connectable and projects an along-face coordinate
    ///     onto the sloped outline: zero at the vertex, growing linearly to the corner.
    /// </summary>
    /// <param name="width">The box width.</param>
    /// <param name="height">The box height.</param>
    [Theory]
    [InlineData(40, 40)]
    [InlineData(60, 40)]
    [InlineData(40, 80)]
    public void DiamondGeometry_ProjectToSurface_IsZeroAtVertexAndLinearToTheCorner(int width, int height)
    {
        // Arrange
        var box = new LayoutBox(0, 0, width, height, null, Depth: 0, BoxShape.Diamond, [], []);
        var geometry = ConnectorRouter.ResolveShapeGeometry(box);

        // Act / Assert: top/bottom faces use the height/width slope, left/right the width/height slope.
        Assert.Equal(0.0, geometry.ProjectToSurface(PortSide.Top, width / 2.0), 9);
        Assert.Equal(height / 4.0, geometry.ProjectToSurface(PortSide.Top, width / 4.0), 9);
        Assert.Equal(height / 2.0, geometry.ProjectToSurface(PortSide.Top, 0.0), 9);
        Assert.Equal(height / 2.0, geometry.ProjectToSurface(PortSide.Bottom, width), 9);
        Assert.Equal(0.0, geometry.ProjectToSurface(PortSide.Left, height / 2.0), 9);
        Assert.Equal(width / 4.0, geometry.ProjectToSurface(PortSide.Right, height / 4.0), 9);
        Assert.Equal(width / 2.0, geometry.ProjectToSurface(PortSide.Right, height), 9);

        foreach (var side in new[] { PortSide.Top, PortSide.Bottom, PortSide.Left, PortSide.Right })
        {
            var extent = Assert.Single(geometry.GetConnectableExtents(side));
            var length = side is PortSide.Left or PortSide.Right ? height : width;
            Assert.Equal(0.0, extent.Lo, 9);
            Assert.Equal(length, extent.Hi, 9);
        }
    }

    /// <summary>
    ///     The containment and auto algorithms carry the diamond shape through to the placed box.
    /// </summary>
    [Fact]
    public void ContainmentAndAutoLayout_DiamondNode_PassesShapeThrough()
    {
        // Arrange
        foreach (var algorithm in new[] { "containment", "auto" })
        {
            var graph = new LayoutGraph();
            graph.Set(CoreOptions.Algorithm, algorithm);
            var node = graph.AddNode("d", 40, 40);
            node.Shape = BoxShape.Diamond;

            // Act
            var tree = LayoutEngine.Layout(graph);

            // Assert
            var box = Assert.Single(AllBoxes(tree.Nodes));
            Assert.Equal(BoxShape.Diamond, box.Shape);
        }
    }

    /// <summary>
    ///     Several connectors ending on one diamond face keep their end markers a marker width plus 4 px
    ///     apart in every flow direction and for every marker style, so arrowheads never touch.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    /// <param name="style">The end-marker style.</param>
    [Theory]
    [InlineData(LayoutFlowDirection.Down, EndMarkerStyle.FilledArrow)]
    [InlineData(LayoutFlowDirection.Down, EndMarkerStyle.HollowTriangle)]
    [InlineData(LayoutFlowDirection.Down, EndMarkerStyle.FilledDiamond)]
    [InlineData(LayoutFlowDirection.Down, EndMarkerStyle.Bar)]
    [InlineData(LayoutFlowDirection.Up, EndMarkerStyle.FilledArrow)]
    [InlineData(LayoutFlowDirection.Up, EndMarkerStyle.FilledDiamond)]
    [InlineData(LayoutFlowDirection.Right, EndMarkerStyle.FilledArrow)]
    [InlineData(LayoutFlowDirection.Right, EndMarkerStyle.Bar)]
    [InlineData(LayoutFlowDirection.Left, EndMarkerStyle.HollowTriangle)]
    [InlineData(LayoutFlowDirection.Left, EndMarkerStyle.FilledArrow)]
    public void LayeredLayout_DiamondTarget_ThreeSources_MarkerHeadsKeepClearOfEachOther(
        LayoutFlowDirection direction,
        EndMarkerStyle style)
    {
        // Arrange
        var graph = MergeGraph(direction, sources: 3, style);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert
        AssertMarkerHeadsKeepApart(tree, direction, style);
    }

    /// <summary>
    ///     A narrow (44 px) merge diamond with two sources also keeps its two heads apart; the face used to
    ///     give them a 12 px pitch.
    /// </summary>
    [Fact]
    public void LayeredLayout_DiamondTarget_TwoSources_MarkerHeadsKeepClear()
    {
        // Arrange
        var graph = MergeGraph(LayoutFlowDirection.Down, sources: 2, EndMarkerStyle.FilledArrow, size: 44);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert
        AssertMarkerHeadsKeepApart(tree, LayoutFlowDirection.Down, EndMarkerStyle.FilledArrow);
    }

    /// <summary>
    ///     A decision diamond's source ends carry no marker, so its three branches keep the existing
    ///     connector-clearance growth only (width 60, height unchanged), not a marker pitch.
    /// </summary>
    [Fact]
    public void LayeredLayout_DiamondDecisionThreeBranches_SourceEndsWithoutMarker_KeepClearanceGrowthOnly()
    {
        // Arrange
        var graph = DecisionGraph(LayoutFlowDirection.Down, branches: 3);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert: no marker pitch applies at a marker-less source end, so only the existing growth holds.
        var diamond = DiamondBox(tree);
        Assert.Equal(60.0, diamond.Width, 6);
        Assert.Equal(40.0, diamond.Height, 6);
    }

    /// <summary>
    ///     A single connector on a diamond face never grows the diamond and still ends on the vertex.
    /// </summary>
    [Fact]
    public void LayeredLayout_DiamondSingleEdge_KeepsRequestedSizeAndVertexAnchor()
    {
        // Arrange
        var graph = MergeGraph(LayoutFlowDirection.Down, sources: 1, EndMarkerStyle.FilledArrow, size: 44);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert
        var diamond = DiamondBox(tree);
        Assert.Equal(44.0, diamond.Width, 6);
        Assert.Equal(44.0, diamond.Height, 6);
        var line = Assert.Single(DiagramQuality.Lines(tree));
        Assert.Equal(diamond.X + (diamond.Width / 2.0), line.Waypoints[^1].X, 6);
        Assert.Equal(diamond.Y, line.Waypoints[^1].Y, 6);
    }

    /// <summary>
    ///     The growth is diamond-only: a rectangle with three sources keeps its 60 px clearance-based width
    ///     and its evenly spread anchors.
    /// </summary>
    [Fact]
    public void LayeredLayout_RectangleTarget_ThreeSources_SizeAndAnchorsUnchanged()
    {
        // Arrange
        var graph = MergeGraph(LayoutFlowDirection.Down, sources: 3, EndMarkerStyle.FilledArrow, size: 44, diamond: false);

        // Act
        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());

        // Assert
        var merge = AllBoxes(tree.Nodes).Single(box => box.Label is null && box.Width < 80);
        Assert.Equal(60.0, merge.Width, 6);
        var ends = DiagramQuality.Lines(tree).Select(line => line.Waypoints[^1].X).Order().ToList();
        var pitch = merge.Width / 3.0;
        Assert.Equal(merge.X + (pitch / 2.0), ends[0], 6);
        Assert.Equal(merge.X + (pitch * 2.5), ends[2], 6);
    }

    /// <summary>Builds a merge node (diamond unless <paramref name="diamond"/> is false) with several sources.</summary>
    private static LayoutGraph MergeGraph(
        LayoutFlowDirection direction,
        int sources,
        EndMarkerStyle style,
        double size = 44,
        bool diamond = true)
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, direction);
        var merge = graph.AddNode("merge", size, size);
        if (diamond)
        {
            merge.Shape = BoxShape.Diamond;
        }

        for (var i = 0; i < sources; i++)
        {
            var source = graph.AddNode($"s{i}", 100, 40);
            graph.AddEdge($"e{i}", source, merge).TargetEnd = style;
        }

        return graph;
    }

    /// <summary>
    ///     Asserts that adjacent end points of the lines ending on the single diamond keep a marker width
    ///     plus the 4 px gap along the face axis.
    /// </summary>
    private static void AssertMarkerHeadsKeepApart(LayoutTree tree, LayoutFlowDirection direction, EndMarkerStyle style)
    {
        var vertical = direction is LayoutFlowDirection.Down or LayoutFlowDirection.Up;
        var ends = DiagramQuality.Lines(tree)
            .Select(line => vertical ? line.Waypoints[^1].X : line.Waypoints[^1].Y)
            .Order()
            .ToList();
        Assert.True(ends.Count >= 2);
        var required = (2.0 * NotationMetrics.AcrossHalfWidth(style) * NotationMetrics.MarkerScale(Themes.Light))
            + NotationMetrics.MarkerStrokeWidth(Themes.Light) + 4.0;
        for (var i = 1; i < ends.Count; i++)
        {
            Assert.True(
                ends[i] - ends[i - 1] >= required - 1e-6,
                $"adjacent {style} heads are {ends[i] - ends[i - 1]:0.##} px apart, need {required:0.##}");
        }
    }

    /// <summary>Builds a decision diamond with <paramref name="branches"/> wide targets.</summary>
    private static LayoutGraph DecisionGraph(LayoutFlowDirection direction, int branches, double size = 40)
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, direction);
        var decision = graph.AddNode("decision", size, size);
        decision.Shape = BoxShape.Diamond;
        for (var i = 0; i < branches; i++)
        {
            var target = graph.AddNode($"t{i}", 100, 40);
            graph.AddEdge($"e{i}", decision, target).TargetEnd = EndMarkerStyle.FilledArrow;
        }

        return graph;
    }

    /// <summary>Returns the single diamond box of a tree.</summary>
    private static LayoutBox DiamondBox(LayoutTree tree) =>
        AllBoxes(tree.Nodes).Single(box => box.Shape == BoxShape.Diamond);

    /// <summary>Enumerates every box of the node list, recursing into children.</summary>
    private static IEnumerable<LayoutBox> AllBoxes(IEnumerable<LayoutNode> nodes)
    {
        foreach (var box in nodes.OfType<LayoutBox>())
        {
            yield return box;
            foreach (var child in AllBoxes(box.Children))
            {
                yield return child;
            }
        }
    }

    /// <summary>Asserts a point lies on the outline of the diamond inscribed in the box.</summary>
    private static void AssertOnOutline(LayoutBox diamond, Point2D point)
    {
        var norm = DiagramQuality.DiamondNorm(diamond, point);
        var distance = Math.Abs(norm - 1.0) * Math.Min(diamond.Width, diamond.Height) / 2.0;
        Assert.True(distance <= Tolerance, $"point {point} is {distance:0.###} px off the diamond outline");
    }
}
