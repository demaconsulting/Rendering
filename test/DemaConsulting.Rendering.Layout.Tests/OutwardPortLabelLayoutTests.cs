// <copyright file="OutwardPortLabelLayoutTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests that a laid-out diagram with outward port labels (<c>name : Type</c> outside the owning box)
///     leaves room for them: no label overlaps a box, another port label, a connector segment, or an end
///     label, and a layout without any outward label is unchanged.
/// </summary>
public sealed class OutwardPortLabelLayoutTests
{
    private static readonly PortAppearance Outward = new(PortDirection.In, LabelPlacement: PortLabelPlacement.Outward);

    /// <summary>Builds a three-part connection scene with labeled outward ports and end labels.</summary>
    private static LayoutGraph Scene(LayoutFlowDirection direction, bool outward, bool endLabels)
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "layered");
        graph.Set(CoreOptions.Direction, direction);
        var sensor = graph.AddNode("sensor", 120, 60);
        sensor.Label = "Sensor";
        var controller = graph.AddNode("controller", 120, 60);
        controller.Label = "Controller";
        var actuator = graph.AddNode("actuator", 120, 60);
        actuator.Label = "Actuator";

        LayoutGraphPort Port(LayoutGraphNode node, string id, string label)
        {
            var port = node.Ports.AddPort(id);
            port.ExternalLabel = label;
            port.Appearance = outward ? Outward : null;
            return port;
        }

        var reading = Port(sensor, "reading", "reading : Real");
        var input = Port(controller, "input", "input : Real");
        var command = Port(controller, "command", "command : Level");
        var drive = Port(actuator, "drive", "drive : Level");
        var first = graph.AddEdge("e1", reading, input);
        first.TargetEnd = EndMarkerStyle.OpenChevron;
        var second = graph.AddEdge("e2", command, drive);
        second.TargetEnd = EndMarkerStyle.OpenChevron;
        if (endLabels)
        {
            first.SourceEndLabel = new LineEndLabel("source", "1");
            first.TargetEndLabel = new LineEndLabel("target", "0..*");
        }

        return graph;
    }

    /// <summary>Right and downward flows keep every outward label clear of boxes, labels, and connectors.</summary>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [InlineData(LayoutFlowDirection.Right)]
    [InlineData(LayoutFlowDirection.Down)]
    public void LayeredLayout_OutwardPortLabels_OverlapNothing(LayoutFlowDirection direction)
    {
        // Act
        var tree = LayoutEngine.Layout(Scene(direction, outward: true, endLabels: true));

        // Assert
        var ports = tree.Nodes.OfType<LayoutPort>().Where(NotationMetrics.IsOutwardLabel).ToList();
        Assert.Equal(4, ports.Count);
        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();
        var rects = ports.Select(port => (Port: port, Bounds: NotationMetrics.PortOutwardLabelBounds(port, port.ExternalLabel!, Themes.Light))).ToList();

        foreach (var (port, bounds) in rects)
        {
            foreach (var box in boxes)
            {
                Assert.False(Overlaps(bounds, box.X, box.Y, box.X + box.Width, box.Y + box.Height), $"'{port.ExternalLabel}' overlaps box '{box.Label}'");
            }

            foreach (var (otherPort, otherBounds) in rects.Where(other => !ReferenceEquals(other.Port, port)))
            {
                Assert.False(
                    Overlaps(bounds, otherBounds.X - otherBounds.HalfWidth, otherBounds.Y - otherBounds.HalfHeight, otherBounds.X + otherBounds.HalfWidth, otherBounds.Y + otherBounds.HalfHeight),
                    $"'{port.ExternalLabel}' overlaps '{otherPort.ExternalLabel}'");
            }

            foreach (var line in lines)
            {
                for (var s = 0; s + 1 < line.Waypoints.Count; s++)
                {
                    Assert.False(
                        Crosses(bounds, line.Waypoints[s], line.Waypoints[s + 1]),
                        $"'{port.ExternalLabel}' is crossed by a connector segment");
                }
            }
        }

        // End labels placed against the port rectangles also clear them.
        var occupied = rects.Select(r => r.Bounds).Concat(ports.Select(NotationMetrics.PortGlyphBounds)).ToList();
        var endLabels = ConnectorLabelPlacer.PlaceEndLabels(lines, Themes.Light.FontSizeBody, boxes, NotationMetrics.MarkerScale(Themes.Light), occupied);
        foreach (var end in endLabels)
        {
            foreach (var rect in occupied)
            {
                Assert.False(
                    Math.Abs(end.X - rect.X) < end.HalfWidth + rect.HalfWidth && Math.Abs(end.Y - rect.Y) < end.HalfHeight + rect.HalfHeight,
                    $"end label '{end.Text}' overlaps a port glyph or label");
            }
        }
    }

    /// <summary>
    ///     A graph with no outward label is laid out exactly as before: giving ports an appearance that keeps
    ///     the labels inward changes no box position.
    /// </summary>
    [Fact]
    public void LayeredLayout_NoOutwardLabels_BoxesIdenticalWithAndWithoutAnInwardAppearance()
    {
        // Arrange
        var plain = Scene(LayoutFlowDirection.Right, outward: false, endLabels: false);
        var inward = Scene(LayoutFlowDirection.Right, outward: false, endLabels: false);
        foreach (var port in inward.Nodes.SelectMany(node => node.Ports.Ports))
        {
            port.Appearance = new PortAppearance(LabelPlacement: PortLabelPlacement.Inward);
        }

        // Act
        var a = LayoutEngine.Layout(plain).Nodes.OfType<LayoutBox>().ToList();
        var b = LayoutEngine.Layout(inward).Nodes.OfType<LayoutBox>().ToList();

        // Assert
        Assert.Equal(a.Count, b.Count);
        for (var i = 0; i < a.Count; i++)
        {
            Assert.Equal((a[i].X, a[i].Y, a[i].Width, a[i].Height), (b[i].X, b[i].Y, b[i].Width, b[i].Height));
        }
    }

    /// <summary>
    ///     Reserving room for outward labels widens the layout compared with the same scene with inward labels
    ///     only along the flow axis; the corridor between the parts grows by the outward label's width.
    /// </summary>
    [Fact]
    public void LayeredLayout_OutwardLabels_WidenTheCorridorBetweenParts()
    {
        // Act
        var outward = LayoutEngine.Layout(Scene(LayoutFlowDirection.Right, outward: true, endLabels: false));
        var inward = LayoutEngine.Layout(Scene(LayoutFlowDirection.Right, outward: false, endLabels: false));

        // Assert: the gap between Sensor and Controller is larger when the labels sit outside.
        var gapOutward = Gap(outward, "Sensor", "Controller");
        var gapInward = Gap(inward, "Sensor", "Controller");
        Assert.True(gapOutward > gapInward + 20, $"outward gap {gapOutward} vs inward gap {gapInward}");
    }

    private static double Gap(LayoutTree tree, string left, string right)
    {
        var l = tree.Nodes.OfType<LayoutBox>().Single(box => box.Label == left);
        var r = tree.Nodes.OfType<LayoutBox>().Single(box => box.Label == right);
        return r.X - (l.X + l.Width);
    }

    private static bool Overlaps(LabelPlacement bounds, double left, double top, double right, double bottom) =>
        bounds.X - bounds.HalfWidth < right && bounds.X + bounds.HalfWidth > left &&
        bounds.Y - bounds.HalfHeight < bottom && bounds.Y + bounds.HalfHeight > top;

    private static bool Crosses(LabelPlacement bounds, Point2D a, Point2D b) =>
        bounds.X - bounds.HalfWidth <= Math.Max(a.X, b.X) && bounds.X + bounds.HalfWidth >= Math.Min(a.X, b.X) &&
        bounds.Y - bounds.HalfHeight <= Math.Max(a.Y, b.Y) && bounds.Y + bounds.HalfHeight >= Math.Min(a.Y, b.Y);
}
