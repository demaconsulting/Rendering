// <copyright file="EndLabelPassThroughTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests that the connection end labels of a <see cref="LayoutGraphEdge"/> reach the placed
///     <see cref="LayoutLine"/> unchanged through every bundled layout algorithm, and stay with their
///     semantic end of the connector even when the layered algorithm reverses a back edge for layering.
/// </summary>
public sealed class EndLabelPassThroughTests
{
    private static readonly LineEndLabel Source = new("owner", "1");
    private static readonly LineEndLabel Target = new("parts", "0..*");

    private static LayoutGraph TwoNodeGraph(string algorithm, out LayoutGraphEdge edge)
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, algorithm);
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        edge = graph.AddEdge("e", a, b);
        edge.SourceEndLabel = Source;
        edge.TargetEndLabel = Target;
        return graph;
    }

    /// <summary>The layered algorithm copies both end labels onto the line.</summary>
    [Fact]
    public void LayeredLayout_EdgeEndLabels_ReachPlacedLine()
    {
        // Arrange
        var graph = TwoNodeGraph("layered", out _);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());
        Assert.Equal(Source, line.SourceEndLabel);
        Assert.Equal(Target, line.TargetEndLabel);
    }

    /// <summary>An edge without end labels yields a line without them.</summary>
    [Fact]
    public void LayeredLayout_EdgeWithoutEndLabels_StaysNull()
    {
        // Arrange
        var graph = TwoNodeGraph("layered", out var edge);
        edge.SourceEndLabel = null;
        edge.TargetEndLabel = null;

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());
        Assert.Null(line.SourceEndLabel);
        Assert.Null(line.TargetEndLabel);
    }

    /// <summary>The hierarchical algorithm copies end labels on a leaf-scope edge.</summary>
    [Fact]
    public void HierarchicalLayout_LeafEdgeEndLabels_ReachPlacedLine()
    {
        // Arrange
        var graph = TwoNodeGraph("hierarchical", out _);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());
        Assert.Equal(Source, line.SourceEndLabel);
        Assert.Equal(Target, line.TargetEndLabel);
    }

    /// <summary>
    ///     The hierarchical algorithm copies end labels onto the sized view it lays a scope out through; a
    ///     container node in the same scope as the labeled leaf edge forces that view.
    /// </summary>
    [Fact]
    public void HierarchicalLayout_SizedViewEdgeEndLabels_ReachPlacedLine()
    {
        // Arrange
        var graph = TwoNodeGraph("hierarchical", out _);
        var container = graph.AddNode("g", 100, 60);
        container.Label = "G";
        container.Children.AddNode("x", 80, 40);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());
        Assert.Equal(Source, line.SourceEndLabel);
        Assert.Equal(Target, line.TargetEndLabel);
    }
    /// <summary>The hierarchical algorithm copies end labels on a cross-container edge.</summary>
    [Fact]
    public void HierarchicalLayout_CrossContainerEdgeEndLabels_ReachPlacedLine()
    {
        // Arrange
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "hierarchical");
        var g1 = graph.AddNode("g1", 100, 60);
        g1.Label = "G1";
        var g2 = graph.AddNode("g2", 100, 60);
        g2.Label = "G2";
        var a = g1.Children.AddNode("a", 80, 40);
        var b = g2.Children.AddNode("b", 80, 40);
        var edge = graph.AddEdge("e", a, b);
        edge.SourceEndLabel = Source;
        edge.TargetEndLabel = Target;

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        var line = AllLines(tree.Nodes).Single();
        Assert.Equal(Source, line.SourceEndLabel);
        Assert.Equal(Target, line.TargetEndLabel);
    }

    /// <summary>The containment algorithm copies end labels onto the routed connector.</summary>
    [Fact]
    public void ContainmentLayout_EdgeEndLabels_ReachPlacedLine()
    {
        // Arrange
        var graph = TwoNodeGraph("containment", out _);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());
        Assert.Equal(Source, line.SourceEndLabel);
        Assert.Equal(Target, line.TargetEndLabel);
    }

    /// <summary>
    ///     The auto algorithm carries end labels through its cloned sub-graphs. The isolated third node makes
    ///     the graph a genuine multi-group case, so the component split really copies the edge.
    /// </summary>
    [Fact]
    public void AutoLayout_EdgeEndLabels_SurviveClone()
    {
        // Arrange
        var graph = TwoNodeGraph("auto", out _);
        graph.AddNode("isolated", 80, 40);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());
        Assert.Equal(Source, line.SourceEndLabel);
        Assert.Equal(Target, line.TargetEndLabel);
    }

    /// <summary>
    ///     The router carries a connection's end labels onto the routed line.
    /// </summary>
    [Fact]
    public void ConnectorRouter_Connection_EndLabelsReachRoutedLine()
    {
        // Arrange
        var a = new LayoutBox(0, 0, 80, 40, "a", 0, BoxShape.Rectangle, [], []);
        var b = new LayoutBox(200, 0, 80, 40, "b", 0, BoxShape.Rectangle, [], []);
        var connection = new Connection(a, b, EndMarkerStyle.None, LineStyle.Solid, null, Source, Target);

        // Act
        var line = ConnectorRouter.Route([a, b], connection, new ConnectorRouteOptions());

        // Assert
        Assert.Equal(Source, line.SourceEndLabel);
        Assert.Equal(Target, line.TargetEndLabel);
    }

    /// <summary>
    ///     When the layered algorithm reverses a back edge for layering, the labels stay with the semantic
    ///     ends: the source label is at the line's first waypoint, which is on the semantic source box.
    /// </summary>
    [Fact]
    public void LayeredLayout_BackEdge_EndLabelsStayWithTheirSemanticEnds()
    {
        // Arrange: a -> b -> c and a back edge c -> a whose ends are labeled.
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "layered");
        var a = graph.AddNode("a", 80, 40);
        a.Label = "A";
        var b = graph.AddNode("b", 80, 40);
        b.Label = "B";
        var c = graph.AddNode("c", 80, 40);
        c.Label = "C";
        graph.AddEdge("ab", a, b);
        graph.AddEdge("bc", b, c);
        var back = graph.AddEdge("ca", c, a);
        var sourceLabel = new LineEndLabel("fromC", "1");
        var targetLabel = new LineEndLabel("toA", "0..1");
        back.SourceEndLabel = sourceLabel;
        back.TargetEndLabel = targetLabel;

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert: the labeled line starts on C and ends on A.
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>(), l => l.SourceEndLabel is not null);
        Assert.Equal(sourceLabel, line.SourceEndLabel);
        Assert.Equal(targetLabel, line.TargetEndLabel);
        var boxes = tree.Nodes.OfType<LayoutBox>().ToDictionary(box => box.Label!);
        Assert.True(Touches(boxes["C"], line.Waypoints[0]), "the first waypoint must be on the semantic source");
        Assert.True(Touches(boxes["A"], line.Waypoints[^1]), "the last waypoint must be on the semantic target");
    }

    /// <summary>
    ///     Parallel edges merged into one line drop their midpoint label and their end labels together,
    ///     because one line cannot honestly carry the ends of several edges.
    /// </summary>
    [Fact]
    public void LayeredLayout_MergedParallelEdges_DropEndLabels()
    {
        // Arrange
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "layered");
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        graph.AddEdge("e1", a, b).SourceEndLabel = Source;
        graph.AddEdge("e2", a, b).SourceEndLabel = Target;

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        var line = Assert.Single(tree.Nodes.OfType<LayoutLine>());
        Assert.Null(line.SourceEndLabel);
        Assert.Null(line.TargetEndLabel);
    }

    private static bool Touches(LayoutBox box, Point2D point) =>
        point.X >= box.X - 1 && point.X <= box.X + box.Width + 1 && point.Y >= box.Y - 1 && point.Y <= box.Y + box.Height + 1;

    private static IEnumerable<LayoutLine> AllLines(IEnumerable<LayoutNode> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case LayoutLine line:
                    yield return line;
                    break;
                case LayoutBox box:
                    foreach (var inner in AllLines(box.Children))
                    {
                        yield return inner;
                    }

                    break;
            }
        }
    }
}
