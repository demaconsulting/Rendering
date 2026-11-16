// <copyright file="BackEdgeEntryApproachTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout.Engine;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests for the back-edge entry approach: the final straight leg of a reversed (back) edge is long
///     enough for the end decoration it carries plus the rounded corner before it, undecorated graphs keep
///     the historical approach, and connector labels never cross a node border.
/// </summary>
public sealed class BackEdgeEntryApproachTests
{
    private const double OpenChevronApproach = (NotationMetrics.EndMarkerLength * 1.5) + 4.0 + 1.0;

    /// <summary>
    ///     Builds the six-state, seven-transition state machine used as the elevator repro: two reversed
    ///     (back) edges (<c>timeout</c>, <c>idleTimeout</c>) among forward transitions.
    /// </summary>
    private static LayoutGraph Elevator(EndMarkerStyle end, LayoutFlowDirection direction = LayoutFlowDirection.Down)
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "layered");
        graph.Set(CoreOptions.Direction, direction);
        graph.Set(CoreOptions.MergeParallelEdges, false);

        LayoutGraphNode State(string id)
        {
            var node = graph.AddNode(id, 130, 44);
            node.Label = id;
            node.Shape = BoxShape.RoundedRectangle;
            return node;
        }

        void Transition(string id, LayoutGraphNode source, LayoutGraphNode target)
        {
            var edge = graph.AddEdge(id, source, target);
            edge.TargetEnd = end;
            edge.Label = id;
        }

        var idle = State("Idle");
        var closing = State("DoorsClosing");
        var moving = State("Moving");
        var arrived = State("AtFloor");
        var open = State("DoorsOpen");
        var waiting = State("Waiting");

        Transition("callReceived", idle, closing);
        Transition("doorsClosed", closing, moving);
        Transition("atFloor", moving, arrived);
        Transition("doorsAreOpen", arrived, open);
        Transition("linger", open, waiting);
        Transition("timeout", waiting, closing);
        Transition("idleTimeout", waiting, idle);
        return graph;
    }

    private static double FinalLeg(LayoutLine line)
    {
        var last = line.Waypoints[^1];
        var previous = line.Waypoints[^2];
        return Math.Abs(last.X - previous.X) + Math.Abs(last.Y - previous.Y);
    }

    private static Dictionary<string, LayoutLine> LinesByLabel(LayoutTree tree) =>
        tree.Nodes.OfType<LayoutLine>().ToDictionary(line => line.MidpointLabel!);

    /// <summary>
    ///     With open-chevron ends, the final leg of every connector (forward and reversed) covers the
    ///     rendered chevron plus the corner radius plus the clean-leg margin.
    /// </summary>
    [Fact]
    public void LayeredLayout_BackEdgeWithOpenChevron_FinalLegCoversChevronAndCorner()
    {
        var tree = LayoutEngine.Layout(Elevator(EndMarkerStyle.OpenChevron));

        var lines = LinesByLabel(tree);
        Assert.Equal(7, lines.Count);
        foreach (var (label, line) in lines)
        {
            Assert.True(FinalLeg(line) >= OpenChevronApproach - 1e-6, $"'{label}' final leg {FinalLeg(line)} < {OpenChevronApproach}.");
        }
    }

    /// <summary>
    ///     Forward edges keep their long final legs: the back-edge approach only ever lengthens the
    ///     wrap-around corridor of reversed edges.
    /// </summary>
    [Fact]
    public void LayeredLayout_BackEdgeWithOpenChevron_ForwardLegsUnchanged()
    {
        var decorated = LinesByLabel(LayoutEngine.Layout(Elevator(EndMarkerStyle.OpenChevron)));
        var undecorated = LinesByLabel(LayoutEngine.Layout(Elevator(EndMarkerStyle.None)));

        foreach (var label in new[] { "callReceived", "doorsClosed", "atFloor", "doorsAreOpen", "linger" })
        {
            Assert.Equal(FinalLeg(undecorated[label]), FinalLeg(decorated[label]), 1e-6);
        }
    }

    /// <summary>
    ///     Without an end decoration the reversed edges keep exactly the historical approach, the
    ///     connector clearance.
    /// </summary>
    [Fact]
    public void LayeredLayout_UndecoratedBackEdge_KeepsLegacyApproach()
    {
        var lines = LinesByLabel(LayoutEngine.Layout(Elevator(EndMarkerStyle.None)));

        Assert.Equal(LayeredLayoutMetrics.ConnectorClearance, FinalLeg(lines["timeout"]), 1e-6);
        Assert.Equal(LayeredLayoutMetrics.ConnectorClearance, FinalLeg(lines["idleTimeout"]), 1e-6);
    }

    /// <summary>
    ///     The longer approach holds for every flow direction.
    /// </summary>
    [Theory]
    [InlineData(LayoutFlowDirection.Down)]
    [InlineData(LayoutFlowDirection.Up)]
    [InlineData(LayoutFlowDirection.Right)]
    [InlineData(LayoutFlowDirection.Left)]
    public void LayeredLayout_BackEdgeWithOpenChevron_AllDirectionsKeepCleanFinalLeg(LayoutFlowDirection direction)
    {
        var lines = LinesByLabel(LayoutEngine.Layout(Elevator(EndMarkerStyle.OpenChevron, direction)));

        Assert.True(FinalLeg(lines["timeout"]) >= OpenChevronApproach - 1e-6);
        Assert.True(FinalLeg(lines["idleTimeout"]) >= OpenChevronApproach - 1e-6);
    }

    /// <summary>
    ///     Each end-marker style sizes the approach from its own rendered length.
    /// </summary>
    [Theory]
    [InlineData(EndMarkerStyle.FilledArrow)]
    [InlineData(EndMarkerStyle.HollowTriangle)]
    [InlineData(EndMarkerStyle.HollowDiamond)]
    [InlineData(EndMarkerStyle.HollowTriangleFourDots)]
    [InlineData(EndMarkerStyle.CircledPlus)]
    public void LayeredLayout_BackEdgeWithAnyMarker_FinalLegCoversRenderedMarker(EndMarkerStyle style)
    {
        var lines = LinesByLabel(LayoutEngine.Layout(Elevator(style)));

        var needed = NotationMetrics.RenderedAlongLineLength(style, Themes.Light)
            + Themes.Light.LineCornerRadius
            + Themes.Light.CleanLegMargin;
        Assert.True(FinalLeg(lines["timeout"]) >= needed - 1e-6);
        Assert.True(FinalLeg(lines["idleTimeout"]) >= needed - 1e-6);
    }

    /// <summary>
    ///     The derived approach is the connector clearance for undecorated edges and the rendered marker
    ///     plus corner radius plus margin for the widest decorated edge.
    /// </summary>
    [Fact]
    public void ResolveBackEdgeEntryApproach_DerivesFromWidestDecoration()
    {
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 10, 10);
        var b = graph.AddNode("b", 10, 10);
        var plain = graph.AddEdge("plain", a, b);
        var chevron = graph.AddEdge("chevron", a, b);
        chevron.TargetEnd = EndMarkerStyle.OpenChevron;
        var diamond = graph.AddEdge("diamond", a, b);
        diamond.TargetEnd = EndMarkerStyle.FilledDiamond;

        Assert.Equal(LayeredLayoutMetrics.ConnectorClearance, LayeredLayoutAlgorithm.ResolveBackEdgeEntryApproach([]));
        Assert.Equal(LayeredLayoutMetrics.ConnectorClearance, LayeredLayoutAlgorithm.ResolveBackEdgeEntryApproach([plain]));
        Assert.Equal(OpenChevronApproach, LayeredLayoutAlgorithm.ResolveBackEdgeEntryApproach([plain, chevron]), 1e-9);
        Assert.Equal(
            (NotationMetrics.DiamondLength * 1.5) + 5.0,
            LayeredLayoutAlgorithm.ResolveBackEdgeEntryApproach([plain, chevron, diamond]),
            1e-9);
    }

    private static (IReadOnlyList<LayerNode> Nodes, IReadOnlyList<LayerEdge> Edges) TwoLayerBackEdgeGraph() =>
        (
            [new LayerNode(80, 40), new LayerNode(80, 40), new LayerNode(80, 40)],
            [new LayerEdge(0, 1), new LayerEdge(1, 2), new LayerEdge(2, 0)]);

    /// <summary>
    ///     The engine's optional approach parameter defaults to the connector clearance, so omitting it and
    ///     passing the default give identical placements.
    /// </summary>
    [Fact]
    public void InterconnectionLayoutEngine_DefaultApproach_EqualsExplicitConnectorClearance()
    {
        var (nodes, edges) = TwoLayerBackEdgeGraph();

        var implicitDefault = InterconnectionLayoutEngine.Place(nodes, edges);
        var explicitDefault = InterconnectionLayoutEngine.Place(
            nodes,
            edges,
            LayoutDirection.Right,
            LayeredLayoutMetrics.NodeSpacing,
            true,
            LayeredLayoutMetrics.ConnectorClearance);

        Assert.Equal(implicitDefault.Rects, explicitDefault.Rects);
        for (var i = 0; i < implicitDefault.ConnectorWaypoints.Count; i++)
        {
            Assert.Equal(implicitDefault.ConnectorWaypoints[i], explicitDefault.ConnectorWaypoints[i]);
        }
    }

    /// <summary>
    ///     A larger approach pushes the reversed edge's wrap-around corridor outward, lengthening its final
    ///     straight run.
    /// </summary>
    [Fact]
    public void InterconnectionLayoutEngine_LargerApproach_LengthensBackEdgeFinalRun()
    {
        var (nodes, edges) = TwoLayerBackEdgeGraph();

        var shortApproach = InterconnectionLayoutEngine.Place(nodes, edges);
        var longApproach = InterconnectionLayoutEngine.Place(
            nodes, edges, LayoutDirection.Right, LayeredLayoutMetrics.NodeSpacing, true, 40.0);

        double LongestFinalRun(LayerResult result) =>
            result.ConnectorWaypoints.Max(w => Math.Abs(w[^1].X - w[^2].X) + Math.Abs(w[^1].Y - w[^2].Y));
        Assert.True(LongestFinalRun(longApproach) >= LongestFinalRun(shortApproach));
        Assert.True(longApproach.TotalWidth >= shortApproach.TotalWidth);
    }

    /// <summary>
    ///     No connector label of the decorated elevator crosses a leaf node's border: each placed label
    ///     rectangle is disjoint from every box.
    /// </summary>
    [Fact]
    public void LayeredLayout_ElevatorLabels_DoNotCrossNodeBorders()
    {
        var tree = LayoutEngine.Layout(Elevator(EndMarkerStyle.OpenChevron));
        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();

        var placements = ConnectorLabelPlacer.Place(lines, Themes.Light.FontSizeBody, boxes);

        Assert.Equal(lines.Count, placements.Count);
        foreach (var (line, placement) in placements)
        {
            foreach (var box in boxes)
            {
                var overlaps = placement.X - placement.HalfWidth < box.X + box.Width
                    && placement.X + placement.HalfWidth > box.X
                    && placement.Y - placement.HalfHeight < box.Y + box.Height
                    && placement.Y + placement.HalfHeight > box.Y;
                Assert.False(overlaps, $"Label '{line.MidpointLabel}' overlaps box '{box.Label}'.");
            }
        }
    }
}
