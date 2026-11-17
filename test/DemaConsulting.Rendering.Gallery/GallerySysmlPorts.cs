// <copyright file="GallerySysmlPorts.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Gallery;

/// <summary>
///     Builds the gallery diagrams for the SysML-style port glyphs, connection end labels, and the
///     Rendering#57 / #58 regression scenes. The port and connection scenes are engine-driven
///     <see cref="LayoutGraph"/>s (they prove the appearance and end labels travel through the layout and
///     leave room for themselves); the glyph matrix is a hand-built <see cref="LayoutTree"/> (it belongs to
///     the <c>custom-rendering</c> group, which is the only group allowed to bypass the layout engine)
///     because the layered algorithm only puts ports on the faces the flow direction runs along.
/// </summary>
internal static class GallerySysmlPorts
{
    private static readonly PortAppearance In = new(PortDirection.In, LabelPlacement: PortLabelPlacement.Outward);
    private static readonly PortAppearance Out = new(PortDirection.Out, LabelPlacement: PortLabelPlacement.Outward);
    private static readonly PortAppearance InOut = new(PortDirection.InOut, LabelPlacement: PortLabelPlacement.Outward);
    private static readonly PortAppearance Reference = new(PortDirection.InOut, OutlineStyle: LineStyle.Dashed, LabelPlacement: PortLabelPlacement.Outward);
    private static readonly PortAppearance Proxy = new(Glyph: PortGlyph.Proxy, LabelPlacement: PortLabelPlacement.Outward);

    /// <summary>
    ///     A small fuel-system interconnection: a tank feeds a pump through an <c>out</c> to <c>in</c> port
    ///     pair, the pump feeds an engine, a controller drives the pump through a proxy port, and the
    ///     engine reports status to a monitor over an <c>inout</c> port and a dashed reference port. Every
    ///     port label reads <c>name : Type</c> outside its part, and the fuel connections carry a role name
    ///     and a multiplicity (one with an <c>ordered</c> adornment) at each end.
    /// </summary>
    /// <param name="direction">The flow direction: right (ports on left and right faces) or down.</param>
    /// <returns>The interconnection graph.</returns>
    public static LayoutGraph FuelSystem(LayoutFlowDirection direction)
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "layered");
        graph.Set(CoreOptions.Direction, direction);

        var tank = Part(graph, "tank", "fuelTank : FuelTank");
        var pump = Part(graph, "pump", "fuelPump : Pump");
        var engine = Part(graph, "engine", "engine : Engine");
        var controller = Part(graph, "controller", "controller : Controller");
        var monitor = Part(graph, "monitor", "monitor : Monitor");

        var tankOut = AddPort(tank, "out", "out : Fuel", Out);
        var pumpIn = AddPort(pump, "in", "in : Fuel", In);
        var pumpOut = AddPort(pump, "out", "out : Fuel", Out);
        var pumpCtrl = AddPort(pump, "ctrl", "ctrl : Cmd", Proxy);
        var engineIn = AddPort(engine, "in", "in : Fuel", In);
        var engineStatus = AddPort(engine, "state", "state : Mode", InOut);
        var monitorStatus = AddPort(monitor, "state", "state : Mode", Reference);
        var controllerCmd = AddPort(controller, "cmd", "cmd : Cmd", Out);

        var supply = Connect(graph, "supply", tankOut, pumpIn, EndMarkerStyle.OpenChevron);
        supply.SourceEndLabel = new LineEndLabel("source", "1");
        supply.TargetEndLabel = new LineEndLabel("sink", "1..*");

        var feed = Connect(graph, "feed", pumpOut, engineIn, EndMarkerStyle.OpenChevron);
        feed.SourceEndLabel = new LineEndLabel("pump", "1");
        feed.TargetEndLabel = new LineEndLabel("feeds", "0..* {ordered}");

        Connect(graph, "command", controllerCmd, pumpCtrl, EndMarkerStyle.OpenChevron);
        Connect(graph, "report", engineStatus, monitorStatus, EndMarkerStyle.None);
        return graph;
    }

    /// <summary>
    ///     An ownership diagram showing the connection end labels on their own: a vehicle composed of an
    ///     engine, a gearbox, and wheels, every composite connection carrying a role name above and a
    ///     multiplicity (with an adornment on one) below the line at each end, plus an association with a
    ///     midpoint label to prove end and midpoint labels coexist.
    /// </summary>
    /// <returns>The ownership graph, laid out to the right.</returns>
    public static LayoutGraph ConnectionEnds()
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "layered");
        var vehicle = Part(graph, "vehicle", "vehicle : Vehicle");
        var engine = Part(graph, "engine", "engine : Engine");
        var gearbox = Part(graph, "gearbox", "gearbox : Gearbox");
        var wheel = Part(graph, "wheel", "wheel : Wheel");
        var driver = Part(graph, "driver", "driver : Driver");

        // A composite connection draws the filled diamond at the owner (the whole), so each part is the
        // source and the vehicle the target; the part's role and multiplicity are the source end labels.
        var powertrain = Connect(graph, "powertrain", engine, vehicle, EndMarkerStyle.FilledDiamond);
        powertrain.SourceEndLabel = new LineEndLabel("engine", "1");
        powertrain.TargetEndLabel = new LineEndLabel("owner", "1");

        var transmission = Connect(graph, "transmission", gearbox, vehicle, EndMarkerStyle.FilledDiamond);
        transmission.SourceEndLabel = new LineEndLabel("gearbox", "0..1");
        transmission.TargetEndLabel = new LineEndLabel("owner", "1");

        var wheels = Connect(graph, "wheels", wheel, vehicle, EndMarkerStyle.FilledDiamond);
        wheels.SourceEndLabel = new LineEndLabel("wheels", "4 {ordered}");
        wheels.TargetEndLabel = new LineEndLabel("owner", "1");

        var drives = Connect(graph, "drives", driver, vehicle, EndMarkerStyle.OpenChevron);
        drives.SourceEndLabel = new LineEndLabel("driver", "0..*");
        drives.TargetEndLabel = new LineEndLabel("vehicle", "1"); return graph;
    }

    /// <summary>
    ///     The Rendering#57 regression scene: two sources feeding one wide target through hollow-triangle
    ///     (specialization) ends, and that target one of three feeding a still wider one, laid out downward. Before the fix the
    ///     connectors stepped 5 px sideways right before the marker; now every final run is straight.
    /// </summary>
    /// <returns>The two-component graph.</returns>
    public static LayoutGraph SharedFaceStraightRuns()
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "layered");
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        var shape = Plain(graph, "shape", "Shape", 320, 50);
        var vehicle = Plain(graph, "vehicle", "Vehicle", 200, 50);
        Specialize(graph, Plain(graph, "car", "Car", 80, 50), vehicle);
        Specialize(graph, Plain(graph, "truck", "Truck", 80, 50), vehicle);
        Specialize(graph, vehicle, shape);
        Specialize(graph, Plain(graph, "circle", "Circle", 70, 50), shape);
        Specialize(graph, Plain(graph, "square", "Square", 70, 50), shape);
        return graph;
    }

    /// <summary>
    ///     The Rendering#58 regression scene: an action flow with a decision diamond fanning to three
    ///     branches and a merge diamond joining them again, laid out downward. Every connector to or from a
    ///     diamond now starts or ends on the diamond's outline and leaves it along the axis, instead of
    ///     floating off the bounding box with a hook.
    /// </summary>
    /// <returns>The action-flow graph.</returns>
    public static LayoutGraph DiamondOutlineAnchors()
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "layered");
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        var start = Plain(graph, "start", "Start", 90, 40);
        var decide = graph.AddNode("decide", 44, 44);
        decide.Shape = BoxShape.Diamond;
        var merge = graph.AddNode("merge", 44, 44);
        merge.Shape = BoxShape.Diamond;
        var end = Plain(graph, "end", "End", 90, 40);

        graph.AddEdge("s-d", start, decide).TargetEnd = EndMarkerStyle.FilledArrow;
        foreach (var (id, label) in new[] { ("fast", "Fast path"), ("slow", "Slow path"), ("retry", "Retry") })
        {
            var branch = Plain(graph, id, label, 100, 40);
            graph.AddEdge($"d-{id}", decide, branch).TargetEnd = EndMarkerStyle.FilledArrow;
            graph.AddEdge($"{id}-m", branch, merge).TargetEnd = EndMarkerStyle.FilledArrow;
        }

        graph.AddEdge("m-e", merge, end).TargetEnd = EndMarkerStyle.FilledArrow;
        return graph;
    }

    /// <summary>
    ///     A hand-built sheet of every port glyph: each of the three directions (<c>in</c>, <c>out</c>,
    ///     <c>inout</c>), a dashed reference port, a proxy port, and the legacy filled square, each on a box
    ///     with one port on every face and an outward <c>name : Type</c> label, so the arrow orientation per
    ///     face can be checked at a glance.
    /// </summary>
    /// <param name="theme">The theme the sheet will be rendered with.</param>
    /// <returns>The hand-built tree.</returns>
    public static LayoutTree GlyphMatrix(Theme theme)
    {
        (string Title, PortAppearance? Appearance)[] cells =
        [
            ("in: arrow points into the part", In),
            ("out: arrow points out of the part", Out),
            ("inout: double-headed arrow", InOut),
            ("reference port: dashed outline", Reference),
            ("proxy port: filled circle", Proxy),
            ("legacy: no appearance", null),
        ];

        const double ColumnPitch = 270.0;
        const double RowPitch = 190.0;
        const double BoxWidth = 120.0;
        const double BoxHeight = 70.0;

        var nodes = new List<LayoutNode>
        {
            Caption(20, 22, 700, "SysML v2 port glyphs on every face", theme.FontSizeTitle, bold: true),
        };

        for (var i = 0; i < cells.Length; i++)
        {
            var (title, appearance) = cells[i];
            var originX = 85.0 + ((i % 3) * ColumnPitch);
            var originY = 90.0 + ((i / 3) * RowPitch);

            nodes.Add(Caption(originX - 40, originY - 44, 250, title, theme.FontSizeBody, bold: false));
            nodes.Add(new LayoutBox(originX, originY, BoxWidth, BoxHeight, "part", 0, BoxShape.Rectangle, [], [], CenterTitle: true));
            foreach (var (side, label, x, y) in new[]
            {
                (PortSide.Left, "left : T", originX, originY + (BoxHeight / 2.0)),
                (PortSide.Right, "right : T", originX + BoxWidth, originY + (BoxHeight / 2.0)),
                (PortSide.Top, "top : T", originX + (BoxWidth / 2.0), originY),
                (PortSide.Bottom, "bottom : T", originX + (BoxWidth / 2.0), originY + BoxHeight),
            })
            {
                // The legacy cell has no outward labels: its inward labels would fill the empty box.
                nodes.Add(new LayoutPort(x, y, side, appearance is null ? null : label, Appearance: appearance));
            }
        }

        return new LayoutTree((3 * ColumnPitch) + 40, (2 * RowPitch) + 20, nodes);
    }

    private static LayoutGraphNode Part(LayoutGraph graph, string id, string label)
    {
        var node = graph.AddNode(id, 150, 70);
        node.Label = label;
        return node;
    }

    private static LayoutGraphNode Plain(LayoutGraph graph, string id, string label, double width, double height)
    {
        var node = graph.AddNode(id, width, height);
        node.Label = label;
        return node;
    }

    private static LayoutGraphPort AddPort(LayoutGraphNode node, string id, string label, PortAppearance appearance)
    {
        var port = node.Ports.AddPort(id);
        port.ExternalLabel = label;
        port.Appearance = appearance;
        return port;
    }

    private static LayoutGraphEdge Connect(LayoutGraph graph, string id, ILayoutConnectable source, ILayoutConnectable target, EndMarkerStyle targetEnd)
    {
        var edge = graph.AddEdge(id, source, target);
        edge.TargetEnd = targetEnd;
        return edge;
    }

    private static void Specialize(LayoutGraph graph, LayoutGraphNode specific, LayoutGraphNode general) =>
        graph.AddEdge($"{specific.Id}-{general.Id}", specific, general).TargetEnd = EndMarkerStyle.HollowTriangle;

    private static LayoutLabel Caption(double x, double y, double maxWidth, string text, double fontSize, bool bold) =>
        new(
            x,
            y,
            maxWidth,
            text,
            TextAlign.Left,
            bold ? FontWeight.Bold : FontWeight.Regular,
            FontStyle.Normal,
            fontSize);
}
