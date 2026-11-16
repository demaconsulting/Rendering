// <copyright file="NotationPrimitivePassThroughTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests that a <see cref="LayoutGraphNode.Appearance"/> reaches the placed <see cref="LayoutBox"/>
///     through every bundled layout algorithm, that a folder whose name is drawn in the tab reserves no
///     body title band, and that the connector router reserves space for the new end markers.
/// </summary>
public sealed class NotationPrimitivePassThroughTests
{
    private static readonly BoxAppearance Dashed = new(LineStyle.Dashed, NameStyle: TextStyle.Regular);

    private static LayoutBox OnlyBox(LayoutTree tree) => tree.Nodes.OfType<LayoutBox>().Single();

    private static LayoutBox FindBox(IEnumerable<LayoutNode> nodes, string label)
    {
        foreach (var node in nodes)
        {
            if (node is not LayoutBox box)
            {
                continue;
            }

            if (box.Label == label)
            {
                return box;
            }

            var inner = FindBoxOrNull(box.Children, label);
            if (inner is not null)
            {
                return inner;
            }
        }

        throw new InvalidOperationException($"No box labeled '{label}'.");
    }

    private static LayoutBox? FindBoxOrNull(IEnumerable<LayoutNode> nodes, string label)
    {
        try
        {
            return FindBox(nodes, label);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static LayoutGraph SingleNodeGraph(BoxAppearance? appearance)
    {
        var graph = new LayoutGraph();
        var node = graph.AddNode("n", 100, 50);
        node.Label = "N";
        node.Appearance = appearance;
        return graph;
    }

    /// <summary>The layered algorithm copies a node's appearance onto its placed box.</summary>
    [Fact]
    public void LayeredLayout_NodeAppearance_ReachesPlacedBox()
    {
        var tree = new LayeredLayoutAlgorithm().ApplyCore(SingleNodeGraph(Dashed), new LayoutOptions());

        Assert.Same(Dashed, OnlyBox(tree).Appearance);
    }

    /// <summary>A node without an appearance stays without one through the layered algorithm.</summary>
    [Fact]
    public void LayeredLayout_NodeWithoutAppearance_StaysNull()
    {
        var tree = new LayeredLayoutAlgorithm().ApplyCore(SingleNodeGraph(null), new LayoutOptions());

        Assert.Null(OnlyBox(tree).Appearance);
    }

    /// <summary>The containment algorithm copies a node's appearance onto its placed box.</summary>
    [Fact]
    public void ContainmentLayout_NodeAppearance_ReachesPlacedBox()
    {
        var tree = new ContainmentLayoutAlgorithm().ApplyCore(SingleNodeGraph(Dashed), new LayoutOptions());

        Assert.Same(Dashed, OnlyBox(tree).Appearance);
    }

    /// <summary>
    ///     The hierarchical algorithm copies a container's and a nested leaf's appearance onto their
    ///     placed boxes.
    /// </summary>
    [Fact]
    public void HierarchicalLayout_ContainerAndLeafAppearance_ReachPlacedBoxes()
    {
        var graph = new LayoutGraph();
        var container = graph.AddNode("c", 10, 10);
        container.Label = "Container";
        container.Appearance = Dashed;
        var leaf = container.Children.AddNode("l", 80, 40);
        leaf.Label = "Leaf";
        var tabbed = new BoxAppearance(FolderTitle: FolderTitlePlacement.Tab);
        leaf.Appearance = tabbed;

        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

        Assert.Same(Dashed, FindBox(tree.Nodes, "Container").Appearance);
        Assert.Same(tabbed, FindBox(tree.Nodes, "Leaf").Appearance);
    }

    /// <summary>
    ///     The auto algorithm preserves a node's appearance when it clones the graph into per-component
    ///     sub-graphs (a connected pair routed to the layered algorithm and an isolated node routed to the
    ///     shared singleton bucket).
    /// </summary>
    [Fact]
    public void AutoLayout_NodeAppearance_SurvivesClone()
    {
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        a.Label = "A";
        a.Appearance = Dashed;
        var b = graph.AddNode("b", 80, 40);
        b.Label = "B";
        graph.AddEdge("e", a, b);
        var c = graph.AddNode("c", 80, 40);
        c.Label = "C";
        c.Appearance = Dashed;

        var tree = new AutoLayoutAlgorithm().Apply(graph);

        Assert.Same(Dashed, FindBox(tree.Nodes, "A").Appearance);
        Assert.Null(FindBox(tree.Nodes, "B").Appearance);
        Assert.Same(Dashed, FindBox(tree.Nodes, "C").Appearance);
    }

    /// <summary>
    ///     A folder whose name is drawn in the tab has no title in its body, so the layered algorithm does
    ///     not widen the top content inset to keep a top port's label clear of the body title: its top
    ///     inset is smaller than that of the same folder with a body title.
    /// </summary>
    [Fact]
    public void LayeredLayout_FolderNameInTabWithTopPort_ReservesNoTitleInset()
    {
        static LayoutBox PlacePortedFolder(BoxAppearance? appearance)
        {
            var graph = new LayoutGraph();
            graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
            var folder = graph.AddNode("pkg", 160, 120);
            folder.Label = "Package";
            folder.Shape = BoxShape.Folder;
            folder.Appearance = appearance;
            var port = folder.Ports.AddPort("p");
            port.ExternalLabel = "request";
            var other = graph.AddNode("other", 80, 40);
            other.Label = "Other";
            graph.AddEdge("e", other, port);

            var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());
            return tree.Nodes.OfType<LayoutBox>().Single(box => box.Label == "Package");
        }

        var inTab = PlacePortedFolder(new BoxAppearance(FolderTitle: FolderTitlePlacement.Tab));
        var inBody = PlacePortedFolder(null);

        Assert.True(inBody.ContentInsetTop > 0.0, "A body-titled folder with a top port reserves its title band.");
        Assert.True(inTab.ContentInsetTop < inBody.ContentInsetTop);
    }

    private static LayoutBox PlaceKeywordPortedFolder(
        BoxAppearance? appearance, string? keyword, bool topPort, double height = 120)
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        var folder = graph.AddNode("pkg", 160, height);
        folder.Label = "Package";
        folder.Keyword = keyword;
        folder.Shape = BoxShape.Folder;
        folder.Appearance = appearance;
        var port = folder.Ports.AddPort("p");
        port.ExternalLabel = "request";
        var other = graph.AddNode("other", 80, 40);
        other.Label = "Other";
        if (topPort)
        {
            graph.AddEdge("e", other, port);
        }
        else
        {
            graph.AddEdge("e", port, other);
        }

        var tree = new LayeredLayoutAlgorithm().ApplyCore(graph, new LayoutOptions());
        return tree.Nodes.OfType<LayoutBox>().Single(box => box.Label == "Package");
    }

    /// <summary>
    ///     A name-in-tab folder that still draws a keyword line in its body keeps the title clearance for
    ///     a top port label: its top inset equals the body-titled folder's and exceeds the keyword-less one.
    /// </summary>
    [Fact]
    public void LayeredLayout_FolderNameInTabWithKeywordAndTopPort_ReservesTitleInset()
    {
        var tab = new BoxAppearance(FolderTitle: FolderTitlePlacement.Tab);
        var inTabKeyword = PlaceKeywordPortedFolder(tab, "package", topPort: true);
        var inTabPlain = PlaceKeywordPortedFolder(tab, null, topPort: true);
        var inBody = PlaceKeywordPortedFolder(null, "package", topPort: true);

        Assert.True(inTabKeyword.ContentInsetTop > inTabPlain.ContentInsetTop);
        Assert.Equal(inBody.ContentInsetTop, inTabKeyword.ContentInsetTop, 1e-9);
    }

    /// <summary>
    ///     As above for a bottom port: the keyword line keeps the bottom content inset reserved.
    /// </summary>
    [Fact]
    public void LayeredLayout_FolderNameInTabWithKeywordAndBottomPort_ReservesTitleInset()
    {
        var tab = new BoxAppearance(FolderTitle: FolderTitlePlacement.Tab);
        var inTabKeyword = PlaceKeywordPortedFolder(tab, "package", topPort: false);
        var inTabPlain = PlaceKeywordPortedFolder(tab, null, topPort: false);
        var inBody = PlaceKeywordPortedFolder(null, "package", topPort: false);

        Assert.True(inTabKeyword.ContentInsetBottom > inTabPlain.ContentInsetBottom);
        Assert.Equal(inBody.ContentInsetBottom, inTabKeyword.ContentInsetBottom, 1e-9);
    }

    /// <summary>
    ///     The port-driven growth path also sees the keyword line: with a small caller-set height the
    ///     keyword-bearing name-in-tab folder grows at least as much as the body-titled folder.
    /// </summary>
    [Fact]
    public void LayeredLayout_FolderNameInTabWithKeywordAndSmallHeight_GrowsLikeBodyTitled()
    {
        var tab = new BoxAppearance(FolderTitle: FolderTitlePlacement.Tab);
        var inTabKeyword = PlaceKeywordPortedFolder(tab, "package", topPort: true, height: 20);
        var inTabPlain = PlaceKeywordPortedFolder(tab, null, topPort: true, height: 20);
        var inBody = PlaceKeywordPortedFolder(null, "package", topPort: true, height: 20);

        Assert.True(inTabKeyword.Height >= inBody.Height - 1e-9);
        Assert.True(inTabKeyword.Height >= inTabPlain.Height);
    }

    private static LayoutGraph FolderWithChild(
        BoxAppearance? appearance, double? titleHeight = null, string? keyword = null)
    {
        var graph = new LayoutGraph();
        var folder = graph.AddNode("pkg", 10, 10);
        folder.Label = "Package";
        folder.Keyword = keyword;
        folder.Shape = BoxShape.Folder;
        folder.Appearance = appearance;
        folder.TitleHeight = titleHeight;
        var child = folder.Children.AddNode("child", 80, 40);
        child.Label = "Child";
        return graph;
    }

    /// <summary>
    ///     A folder whose name is drawn in the tab reserves no body title band, so its child sits higher
    ///     (by exactly the default container title height) than inside a body-titled folder.
    /// </summary>
    [Fact]
    public void HierarchicalLayout_FolderNameInTab_ReservesNoBodyTitleBand()
    {
        var options = LayoutOptions.ForAlgorithm("layered");
        var inTab = new HierarchicalLayoutAlgorithm().ApplyCore(
            FolderWithChild(new BoxAppearance(FolderTitle: FolderTitlePlacement.Tab)), options);
        var inBody = new HierarchicalLayoutAlgorithm().ApplyCore(FolderWithChild(null), options);

        var tabbedOffset = FindBox(inTab.Nodes, "Child").Y - FindBox(inTab.Nodes, "Package").Y;
        var bodiedOffset = FindBox(inBody.Nodes, "Child").Y - FindBox(inBody.Nodes, "Package").Y;

        Assert.True(tabbedOffset < bodiedOffset);
        // The body title band is the hierarchical algorithm's default container title height (24 px).
        Assert.Equal(24.0, bodiedOffset - tabbedOffset, 1e-9);
    }

    /// <summary>
    ///     A name-in-tab folder that still draws a keyword line in its body keeps the default title reserve,
    ///     so its child clears the keyword line exactly as in a body-titled folder.
    /// </summary>
    [Fact]
    public void HierarchicalLayout_FolderNameInTabWithKeyword_ChildClearsKeywordLine()
    {
        var options = LayoutOptions.ForAlgorithm("layered");
        var tabbed = new BoxAppearance(FolderTitle: FolderTitlePlacement.Tab);
        var inTab = new HierarchicalLayoutAlgorithm().ApplyCore(FolderWithChild(tabbed, keyword: "package"), options);
        var inBody = new HierarchicalLayoutAlgorithm().ApplyCore(FolderWithChild(null, keyword: "package"), options);

        var folder = FindBox(inTab.Nodes, "Package");
        var child = FindBox(inTab.Nodes, "Child");
        var tabbedOffset = child.Y - folder.Y;
        var bodiedOffset = FindBox(inBody.Nodes, "Child").Y - FindBox(inBody.Nodes, "Package").Y;

        var keywordBottom = BoxMetrics.FolderTabHeight(Themes.Light) + BoxMetrics.TitleAreaHeight(Themes.Light, false, true);
        Assert.True(tabbedOffset >= keywordBottom, "The child must clear the tab and the keyword line.");
        Assert.Equal(bodiedOffset, tabbedOffset, 1e-9);
    }

    /// <summary>
    ///     An explicit title height keeps its reserve even for a name-in-tab folder, so a caller can still
    ///     reserve a keyword line.
    /// </summary>
    [Fact]
    public void HierarchicalLayout_FolderNameInTabWithExplicitTitleHeight_KeepsReserve()
    {
        var options = LayoutOptions.ForAlgorithm("layered");
        var tabbed = new BoxAppearance(FolderTitle: FolderTitlePlacement.Tab);
        var withExplicit = new HierarchicalLayoutAlgorithm().ApplyCore(FolderWithChild(tabbed, 30.0), options);
        var inBodyExplicit = new HierarchicalLayoutAlgorithm().ApplyCore(FolderWithChild(null, 30.0), options);

        var tabbedOffset = FindBox(withExplicit.Nodes, "Child").Y - FindBox(withExplicit.Nodes, "Package").Y;
        var bodiedOffset = FindBox(inBodyExplicit.Nodes, "Child").Y - FindBox(inBodyExplicit.Nodes, "Package").Y;

        Assert.Equal(bodiedOffset, tabbedOffset, 1e-9);
    }

    /// <summary>
    ///     The name-in-tab folder's tab, used by the router as the non-connectable strip, is sized to the
    ///     measured name rather than the historical label-length estimate.
    /// </summary>
    [Fact]
    public void FolderGeometry_NameInTab_UsesMeasuredTabWidth()
    {
        const string Name = "AVeryLongPackageNameThatNeedsAWideTab";
        var tabbed = new LayoutBox(
            0, 0, 800, 100, Name, 0, BoxShape.Folder, [], [],
            Appearance: new BoxAppearance(FolderTitle: FolderTitlePlacement.Tab));
        var bodied = tabbed with { Appearance = null };

        var tabbedWidth = ConnectorRouter.FolderGeometry.ResolveFolderTabWidth(tabbed);
        var bodiedWidth = ConnectorRouter.FolderGeometry.ResolveFolderTabWidth(bodied);

        Assert.Equal(BoxMetrics.ResolveFolderTabWidth(tabbed, Themes.Light), tabbedWidth, 1e-9);
        Assert.Equal(BoxMetrics.ResolveFolderTabWidth(bodied, Themes.Light), bodiedWidth, 1e-9);
        Assert.NotEqual(tabbedWidth, bodiedWidth);
    }

    /// <summary>
    ///     Every legacy end-marker style keeps the historical 8 px slid-port corner margin; the wider
    ///     circled plus needs 10 px.
    /// </summary>
    [Fact]
    public void ConnectorRouter_PortCornerMarginFor_LegacyIsEightAndCircledPlusIsTen()
    {
        foreach (var style in new[]
        {
            EndMarkerStyle.None,
            EndMarkerStyle.OpenChevron,
            EndMarkerStyle.HollowTriangle,
            EndMarkerStyle.HollowTriangleCrossbar,
            EndMarkerStyle.FilledArrow,
            EndMarkerStyle.HollowDiamond,
            EndMarkerStyle.FilledDiamond,
            EndMarkerStyle.Circle,
            EndMarkerStyle.Bar,
            EndMarkerStyle.HollowTriangleTwoDots,
            EndMarkerStyle.HollowTriangleFourDots,
        })
        {
            Assert.Equal(8.0, ConnectorRouter.PortCornerMarginFor(style));
        }

        Assert.Equal(10.0, ConnectorRouter.PortCornerMarginFor(EndMarkerStyle.CircledPlus));
    }

    /// <summary>
    ///     The router reserves a final straight leg at least as long as the marker as it is RENDERED (the
    ///     notation length times the theme stroke width, the same size both renderers draw) plus the bend
    ///     radius, so the decoration sits on a clean run in SVG and PNG alike.
    /// </summary>
    [Theory]
    [InlineData(EndMarkerStyle.OpenChevron)]
    [InlineData(EndMarkerStyle.HollowTriangle)]
    [InlineData(EndMarkerStyle.HollowTriangleCrossbar)]
    [InlineData(EndMarkerStyle.HollowDiamond)]
    [InlineData(EndMarkerStyle.HollowTriangleTwoDots)]
    [InlineData(EndMarkerStyle.HollowTriangleFourDots)]
    [InlineData(EndMarkerStyle.CircledPlus)]
    public void ConnectorRouter_NewEndMarker_ReservesCleanEndLeg(EndMarkerStyle style)
    {
        var from = new LayoutBox(0, 0, 80, 40, "A", 0, BoxShape.Rectangle, [], []);
        var to = new LayoutBox(220, 120, 80, 40, "B", 0, BoxShape.Rectangle, [], []);

        var line = ConnectorRouter.Route([from, to], new Connection(from, to, style), new ConnectorRouteOptions());

        var last = line.Waypoints[^1];
        var previous = line.Waypoints[^2];
        var finalLeg = Math.Abs(last.X - previous.X) + Math.Abs(last.Y - previous.Y);
        var rendered = NotationMetrics.RenderedAlongLineLength(style, Themes.Light);
        Assert.True(
            finalLeg >= rendered + Themes.Light.LineCornerRadius - 1e-6,
            $"Final leg {finalLeg} shorter than rendered marker {rendered} plus corner radius.");
    }
}
