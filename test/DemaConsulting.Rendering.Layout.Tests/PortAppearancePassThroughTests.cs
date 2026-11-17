// <copyright file="PortAppearancePassThroughTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests that the <see cref="PortAppearance"/> of a <see cref="LayoutGraphPort"/> reaches every
///     <see cref="LayoutPort"/> the layout algorithms emit for it, and that a port without an appearance
///     stays the legacy look.
/// </summary>
public sealed class PortAppearancePassThroughTests
{
    private static readonly PortAppearance Appearance =
        new(PortDirection.InOut, PortGlyph.Outlined, LineStyle.Dashed, PortLabelPlacement.Outward);

    private static LayoutGraph PortedGraph(string algorithm, PortAppearance? appearance)
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, algorithm);
        var a = graph.AddNode("a", 120, 60);
        a.Label = "A";
        var source = a.Ports.AddPort("out1");
        source.ExternalLabel = "out1 : Real";
        source.Appearance = appearance;
        var b = graph.AddNode("b", 120, 60);
        b.Label = "B";
        var target = b.Ports.AddPort("in1");
        target.ExternalLabel = "in1 : Real";
        target.Appearance = appearance;
        graph.AddEdge("e", source, target).TargetEnd = EndMarkerStyle.OpenChevron;
        return graph;
    }

    /// <summary>The layered algorithm copies the appearance onto the source and the target port.</summary>
    [Fact]
    public void LayeredLayout_PortAppearance_ReachesBothPlacedPorts()
    {
        // Act
        var tree = LayoutEngine.Layout(PortedGraph("layered", Appearance));

        // Assert
        var ports = tree.Nodes.OfType<LayoutPort>().ToList();
        Assert.Equal(2, ports.Count);
        Assert.All(ports, port => Assert.Equal(Appearance, port.Appearance));
    }

    /// <summary>A port without an appearance stays the legacy look.</summary>
    [Fact]
    public void LayeredLayout_PortWithoutAppearance_StaysNull()
    {
        // Act
        var tree = LayoutEngine.Layout(PortedGraph("layered", null));

        // Assert
        Assert.All(tree.Nodes.OfType<LayoutPort>(), port => Assert.Null(port.Appearance));
    }

    /// <summary>The hierarchical algorithm copies the appearance on a leaf-scope edge.</summary>
    [Fact]
    public void HierarchicalLayout_LeafPortAppearance_ReachesPlacedPorts()
    {
        // Act
        var tree = LayoutEngine.Layout(PortedGraph("hierarchical", Appearance));

        // Assert
        var ports = tree.Nodes.OfType<LayoutPort>().ToList();
        Assert.Equal(2, ports.Count);
        Assert.All(ports, port => Assert.Equal(Appearance, port.Appearance));
    }

    /// <summary>The auto algorithm carries the appearance through its cloned sub-graphs.</summary>
    [Fact]
    public void AutoLayout_PortAppearance_ReachesPlacedPorts()
    {
        // Act
        var tree = LayoutEngine.Layout(PortedGraph("auto", Appearance));

        // Assert
        var ports = tree.Nodes.OfType<LayoutPort>().ToList();
        Assert.Equal(2, ports.Count);
        Assert.All(ports, port => Assert.Equal(Appearance, port.Appearance));
    }

    /// <summary>
    ///     A boundary (delegation) port keeps its appearance when the combined pass projects it back onto the
    ///     container boundary.
    /// </summary>
    [Fact]
    public void HierarchicalLayout_BoundaryPortAppearance_ReachesTheSharedAnchor()
    {
        // Arrange: container B owns boundary port P (both labels, an appearance); A -> P outside, P -> C inside.
        var graph = new LayoutGraph();
        var a = graph.AddNode("A", 80, 40);
        a.Label = "A";
        var b = graph.AddNode("B", 10, 10);
        b.Label = "B";
        var port = b.Ports.AddPort("p1");
        port.ExternalLabel = "PWR_OUT";
        port.InternalLabel = "PWR_IN";
        port.Appearance = Appearance;
        var c = b.Children.AddNode("C", 80, 40);
        c.Label = "C";
        graph.AddEdge("a-to-b", a, port);
        b.Children.AddEdge("b-to-c", port, c);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().ApplyCore(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert
        var emitted = Assert.Single(tree.Nodes.OfType<LayoutPort>());
        Assert.Equal(Appearance, emitted.Appearance);
    }

    /// <summary>
    ///     An outward-label port is never squeezed to half its box's width: the label lies outside the box, so
    ///     the maximum label width is unconstrained.
    /// </summary>
    [Fact]
    public void LayeredLayout_OutwardLabelPort_HasUnconstrainedLabelWidth()
    {
        // Act
        var tree = LayoutEngine.Layout(PortedGraph("layered", Appearance));

        // Assert
        Assert.All(tree.Nodes.OfType<LayoutPort>(), port => Assert.True(double.IsPositiveInfinity(port.MaxLabelWidth)));
    }

    /// <summary>
    ///     An outward-label port reserves no label-driven content inset inside its box: its label is outside, so
    ///     the insets equal those of the same port without any label, and are smaller than for an inward label.
    /// </summary>
    [Fact]
    public void LayeredLayout_OutwardLabelPort_ReservesNoLabelInset()
    {
        // Arrange
        var unlabeled = PortedGraph("layered", Appearance);
        foreach (var port in unlabeled.Nodes.SelectMany(node => node.Ports.Ports))
        {
            port.ExternalLabel = null;
        }

        // Act
        var outward = LayoutEngine.Layout(PortedGraph("layered", Appearance));
        var none = LayoutEngine.Layout(unlabeled);
        var inward = LayoutEngine.Layout(PortedGraph("layered", Appearance with { LabelPlacement = PortLabelPlacement.Inward }));

        // Assert
        var outwardBox = outward.Nodes.OfType<LayoutBox>().First(box => box.Label == "A");
        var noneBox = none.Nodes.OfType<LayoutBox>().First(box => box.Label == "A");
        var inwardBox = inward.Nodes.OfType<LayoutBox>().First(box => box.Label == "A");
        Assert.Equal(noneBox.ContentInsetRight, outwardBox.ContentInsetRight);
        Assert.Equal(noneBox.ContentInsetLeft, outwardBox.ContentInsetLeft);
        Assert.True(inwardBox.ContentInsetRight > outwardBox.ContentInsetRight || inwardBox.ContentInsetLeft > outwardBox.ContentInsetLeft);
    }
}
