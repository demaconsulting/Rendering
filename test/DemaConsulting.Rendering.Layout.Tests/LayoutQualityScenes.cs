// <copyright file="LayoutQualityScenes.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     A named graph of the layout-quality benchmark set.
/// </summary>
/// <param name="Name">The stable scene name (used for file names and baseline constants).</param>
/// <param name="Title">A short human-readable title.</param>
/// <param name="Build">Builds a fresh copy of the graph.</param>
internal sealed record QualityScene(string Name, string Title, Func<LayoutGraph> Build);

/// <summary>
///     Graph builders for the layout-quality regression tests and benchmark (Rendering#48 cross-hierarchy
///     crossing minimization, Rendering#60 layer spacing and compactness). Written against the public API
///     only, so the same scenes can be laid out by the baseline (main) and by the branch.
/// </summary>
internal static class LayoutQualityScenes
{
    /// <summary>
    ///     The six insertion orders of the three members <c>C</c>, <c>S</c>, <c>H</c> of the first folder of
    ///     the Rendering#48 repro scenes.
    /// </summary>
    public static readonly IReadOnlyList<string[]> InsertionOrders =
    [
        ["C", "S", "H"],
        ["C", "H", "S"],
        ["S", "C", "H"],
        ["S", "H", "C"],
        ["H", "C", "S"],
        ["H", "S", "C"],
    ];

    /// <summary>The four flow directions every scene may be checked in.</summary>
    public static readonly LayoutFlowDirection[] AllDirections =
    [
        LayoutFlowDirection.Right,
        LayoutFlowDirection.Left,
        LayoutFlowDirection.Up,
        LayoutFlowDirection.Down,
    ];

    /// <summary>The two root algorithms that route cross-hierarchy edges.</summary>
    public static readonly string[] RootAlgorithms = ["hierarchical", "auto"];

    /// <summary>Gets the benchmark scenes, in a stable order.</summary>
    public static IReadOnlyList<QualityScene> Benchmarks { get; } =
    [
        new("Repro2Right", "Rendering#48 repro 2 (Right, C S H order)", () => Repro2("hierarchical", LayoutFlowDirection.Right, ["C", "S", "H"])),
        new("Repro3Right", "Rendering#48 repro 3 with interior connector (Right, C S H order)", () => Repro3("hierarchical", LayoutFlowDirection.Right, ["C", "S", "H"])),
        new("NestedDepthRight", "Leaf partners across two nesting levels (Right)", () => NestedDepth("hierarchical", LayoutFlowDirection.Right, ["C", "S", "H"])),
        new("NestedPackagesRight", "Four packages with nested packages and labeled cross edges (Right)", () => NestedPackages(LayoutFlowDirection.Right)),
        new("NestedPackagesUp", "Four packages with nested packages and labeled cross edges (Up)", () => NestedPackages(LayoutFlowDirection.Up)),
        new("GeneralViewUp", "SysML general view with usages, typing and composition (Up)", () => GeneralViewUp(LayoutFlowDirection.Up)),
        new("DenseUsageTree", "Drone usage tree with shared and isolated definitions (Down)", DenseUsageTree),
        new("InterconnectionLabeledCorridors", "Interconnection with labeled corridors (Right)", () => Interconnection(null)),
        new("InterconnectionLayerSpacing40", "Interconnection with LayerSpacing 40 (Right)", () => Interconnection(40.0)),
        new("ActionFlow", "Action flow with decisions, fork/join and a back edge (Down)", ActionFlow),
        new("StateMachine", "State machine with back edges (Down)", StateMachine),
        new("AutoTallPlusIsolated", "Auto layout of a tall chain plus isolated definitions (Down)", AutoTallPlusIsolated),
    ];

    /// <summary>
    ///     The Rendering#48 second repro: folder <c>B</c> holds <c>C</c>, <c>S</c> (specializing <c>C</c>) and
    ///     <c>H</c>; folder <c>P</c> holds <c>E</c>; leaf <c>K</c> sits at the root. Edges <c>H→E</c>,
    ///     <c>E→K</c> and <c>C→K</c> cross the hierarchy, so the order of <c>B</c>'s members decides whether
    ///     connectors cross.
    /// </summary>
    /// <param name="root">The root algorithm id (<c>hierarchical</c> or <c>auto</c>).</param>
    /// <param name="direction">The flow direction.</param>
    /// <param name="order">The insertion order of <c>C</c>, <c>S</c> and <c>H</c> into folder <c>B</c>.</param>
    /// <returns>The graph.</returns>
    public static LayoutGraph Repro2(string root, LayoutFlowDirection direction, IReadOnlyList<string> order) =>
        Repro(root, direction, order, interior: false, interiorFirst: false);

    /// <summary>
    ///     The Rendering#48 third repro: <see cref="Repro2"/> plus an interior connector <c>M→E</c> inside
    ///     folder <c>P</c>, which the declaring scope's connector <c>H→E</c> must not cross.
    /// </summary>
    /// <param name="root">The root algorithm id (<c>hierarchical</c> or <c>auto</c>).</param>
    /// <param name="direction">The flow direction.</param>
    /// <param name="order">The insertion order of <c>C</c>, <c>S</c> and <c>H</c> into folder <c>B</c>.</param>
    /// <param name="interiorFirst">Whether <c>M</c> is inserted into folder <c>P</c> before <c>E</c>.</param>
    /// <returns>The graph.</returns>
    public static LayoutGraph Repro3(string root, LayoutFlowDirection direction, IReadOnlyList<string> order, bool interiorFirst = false) =>
        Repro(root, direction, order, interior: true, interiorFirst);

    /// <summary>
    ///     Leaf partners across two nesting levels: <c>B{C, S→C, H}</c> beside <c>P{Q{R{X, Y→X}}, E}</c> and a
    ///     root leaf <c>K</c>. <c>H→X</c> reaches a leaf three containers deep, <c>X→K</c> and <c>C→K</c> reach
    ///     the root; <c>E</c> is an unconnected neighbor of <c>Q</c>.
    /// </summary>
    /// <param name="root">The root algorithm id.</param>
    /// <param name="direction">The flow direction.</param>
    /// <param name="order">The insertion order of <c>C</c>, <c>S</c> and <c>H</c> into folder <c>B</c>.</param>
    /// <returns>The graph.</returns>
    public static LayoutGraph NestedDepth(string root, LayoutFlowDirection direction, IReadOnlyList<string> order)
    {
        var graph = Root(root, direction);
        var b = Folder(graph, "B", "B", "layered", tab: false);
        var p = Folder(graph, "P", "P", "layered", tab: false);
        var q = Folder(p.Children, "Q", "Q", "layered", tab: false);
        var r = Folder(q.Children, "R", "R", "layered", tab: false);
        var members = new Dictionary<string, LayoutGraphNode>();
        foreach (var name in order)
        {
            members[name] = Definition(b.Children, name, name);
        }

        Specialize(b.Children, "s_c", members["S"], members["C"]);
        Definition(p.Children, "E", "E");
        var x = Definition(r.Children, "X", "X");
        var y = Definition(r.Children, "Y", "Y");
        Specialize(r.Children, "y_x", y, x);
        var k = Definition(graph, "K", "K");
        Specialize(graph, "h_x", members["H"], x);
        Reference(graph, "x_k", x, k, null);
        Composite(graph, "c_k", members["C"], k);
        return graph;
    }

    /// <summary>
    ///     Four package folders with a nested package, labeled keyword edges between leaves of different
    ///     packages and between packages, and isolated definitions: the Rendering#48 repro scaled up.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    /// <returns>The graph.</returns>
    public static LayoutGraph NestedPackages(LayoutFlowDirection direction)
    {
        var graph = Root("hierarchical", direction);
        var design = Folder(graph, "design", "Design", "layered");
        var reqFolder = Folder(graph, "reqFolder", "Requirements", "layered");
        var tests = Folder(graph, "tests", "Verification", "layered");
        var lib = Folder(graph, "lib", "Library", "layered");
        var power = Folder(design.Children, "power", "Power", "layered");

        var vehicle = Definition(design.Children, "vehicle", "Vehicle");
        var chassis = Definition(design.Children, "chassis", "Chassis");
        var body = Definition(design.Children, "body", "Body");
        var battery = Definition(power.Children, "battery", "Battery");
        var charger = Definition(power.Children, "charger", "Charger");
        var inverter = Definition(power.Children, "inverter", "Inverter");
        var massReq = Definition(reqFolder.Children, "massReq", "MassReq", "requirement def");
        var rangeReq = Definition(reqFolder.Children, "rangeReq", "RangeReq", "requirement def");
        var safetyReq = Definition(reqFolder.Children, "safetyReq", "SafetyReq", "requirement def");
        var massTest = Definition(tests.Children, "massTest", "MassTest", "verification def");
        var rangeTest = Definition(tests.Children, "rangeTest", "RangeTest", "verification def");
        var mass = Definition(lib.Children, "mass", "Mass", "attribute def");
        var length = Definition(lib.Children, "length", "Length", "attribute def");

        Specialize(design.Children, "s1", chassis, vehicle);
        Specialize(design.Children, "s2", body, vehicle);
        Composite(power.Children, "c1", charger, battery);
        Composite(power.Children, "c2", inverter, battery);
        Specialize(reqFolder.Children, "s3", rangeReq, massReq);

        Reference(graph, "satisfyMass", massReq, vehicle, "«satisfy»");
        Reference(graph, "satisfyRange", rangeReq, battery, "«satisfy»");
        Reference(graph, "verifyMass", massTest, massReq, "«verify»");
        Reference(graph, "verifyRange", rangeTest, rangeReq, "«verify»");
        Reference(graph, "massType", battery, mass, null);
        Reference(graph, "lengthType", chassis, length, null);
        Reference(graph, "importLib", design, lib, "«import»*");
        Reference(graph, "importReqFolder", tests, reqFolder, "«import»*");
        Composite(graph, "owns", inverter, vehicle);
        Definition(reqFolder.Children, "unusedReq", "UnusedReq", "requirement def");
        Definition(tests.Children, "unusedTest", "UnusedTest", "verification def");
        Definition(graph, "orphan1", "Orphan1");
        Definition(graph, "orphan2", "Orphan2");
        _ = safetyReq;
        return graph;
    }

    /// <summary>
    ///     A SysML general view in the shape consumers produce: package folders (laid out under an
    ///     <c>auto</c> root, so each folder inherits <c>auto</c>) holding definitions and usages as nodes,
    ///     composite (filled diamond) and typing (hollow triangle with two dots) edges, many
    ///     cross-hierarchy edges, orphan definitions, and an insertion order that scatters connected items
    ///     rather than grouping them.
    /// </summary>
    /// <param name="direction">The flow direction (the benchmark uses <see cref="LayoutFlowDirection.Up"/>).</param>
    /// <param name="shuffleSeed">
    /// When set, the folders, the items inside them, and the edges are inserted in a pseudo-random order
    /// derived from the seed instead of the fixed scattered order, to probe insertion-order dependence.
    /// </param>
    /// <returns>The graph.</returns>
    public static LayoutGraph GeneralViewUp(LayoutFlowDirection direction, int? shuffleSeed = null)
    {
        var random = shuffleSeed is { } seed ? new Random(seed) : null;
        var graph = Root("auto", direction);

        (string Id, string Label)[] folderSpecs =
        [
            ("requirements", "Requirements"),
            ("usages", "Usages"),
            ("definitions", "Definitions"),
            ("library", "Library"),
        ];

        // Scattered insertion: related items are deliberately not adjacent.
        (string Folder, string Id, string Label, string Kind)[] itemSpecs =
        [
            ("definitions", "wheelDef", "Wheel", "part def"),
            ("usages", "rearWheel", "rearWheel : Wheel", "usage"),
            ("definitions", "unusedA", "UnusedA", "part def"),
            ("requirements", "rangeReq", "RangeReq", "requirement def"),
            ("definitions", "vehicleDef", "Vehicle", "part def"),
            ("usages", "battery1", "battery1 : Battery", "usage"),
            ("library", "power", "Power", "attribute def"),
            ("definitions", "batteryDef", "Battery", "part def"),
            ("usages", "vehicle1", "vehicle1 : Vehicle", "usage"),
            ("definitions", "truckDef", "Truck", "part def"),
            ("requirements", "massReq", "MassReq", "requirement def"),
            ("usages", "axle1", "axle1 : Axle", "usage"),
            ("definitions", "engineDef", "Engine", "part def"),
            ("library", "mass", "Mass", "attribute def"),
            ("usages", "chassis1", "chassis1 : Chassis", "usage"),
            ("definitions", "carDef", "Car", "part def"),
            ("requirements", "unusedReq", "UnusedReq", "requirement def"),
            ("usages", "frontWheel", "frontWheel : Wheel", "usage"),
            ("definitions", "axleDef", "Axle", "part def"),
            ("definitions", "unusedB", "UnusedB", "part def"),
            ("usages", "engine1", "engine1 : Engine", "usage"),
            ("definitions", "chassisDef", "Chassis", "part def"),
            (string.Empty, "orphan1", "Orphan1", "part def"),
            (string.Empty, "orphan2", "Orphan2", "part def"),
            (string.Empty, "orphan3", "Orphan3", "part def"),
        ];

        // Edge kinds: S specialization, C composite, T typing, R keyword reference.
        (string Folder, string Id, string Source, string Target, char Kind, string? Label)[] edgeSpecs =
        [
            ("definitions", "carIsVehicle", "carDef", "vehicleDef", 'S', null),
            ("usages", "rearWheelOfAxle", "rearWheel", "axle1", 'C', null),
            (string.Empty, "t6", "engine1", "engineDef", 'T', null),
            ("usages", "batteryOfVehicle", "battery1", "vehicle1", 'C', null),
            (string.Empty, "satisfyRange", "rangeReq", "battery1", 'R', "«satisfy»"),
            ("definitions", "truckIsVehicle", "truckDef", "vehicleDef", 'S', null),
            (string.Empty, "t3", "vehicle1", "vehicleDef", 'T', null),
            ("usages", "chassisOfVehicle", "chassis1", "vehicle1", 'C', null),
            (string.Empty, "t1", "rearWheel", "wheelDef", 'T', null),
            (string.Empty, "enginePower", "engineDef", "power", 'R', null),
            ("usages", "frontWheelOfAxle", "frontWheel", "axle1", 'C', null),
            (string.Empty, "t5", "axle1", "axleDef", 'T', null),
            (string.Empty, "satisfyMass", "massReq", "vehicle1", 'R', "«satisfy»"),
            ("usages", "engineOfVehicle", "engine1", "vehicle1", 'C', null),
            (string.Empty, "t2", "battery1", "batteryDef", 'T', null),
            (string.Empty, "batteryMass", "batteryDef", "mass", 'R', null),
            ("usages", "axleOfVehicle", "axle1", "vehicle1", 'C', null),
            (string.Empty, "t7", "chassis1", "chassisDef", 'T', null),
            (string.Empty, "t4", "frontWheel", "wheelDef", 'T', null),
            (string.Empty, "importLibrary", "requirements", "library", 'R', "«import»*"),
        ];

        if (random is not null)
        {
            Shuffle(folderSpecs, random);
            Shuffle(itemSpecs, random);
            Shuffle(edgeSpecs, random);
        }

        var nodes = new Dictionary<string, LayoutGraphNode>();
        foreach (var (id, label) in folderSpecs)
        {
            nodes[id] = Folder(graph, id, label, null);
        }

        foreach (var (folder, id, label, kind) in itemSpecs)
        {
            var target = folder.Length == 0 ? graph : nodes[folder].Children;
            nodes[id] = kind == "usage" ? Usage(target, id, label) : Definition(target, id, label, kind);
        }

        foreach (var (folder, id, source, target, kind, label) in edgeSpecs)
        {
            var scope = folder.Length == 0 ? graph : nodes[folder].Children;
            switch (kind)
            {
                case 'S':
                    Specialize(scope, id, nodes[source], nodes[target]);
                    break;
                case 'C':
                    Composite(scope, id, nodes[source], nodes[target]);
                    break;
                case 'T':
                    Typing(scope, id, nodes[source], nodes[target]);
                    break;
                default:
                    Reference(scope, id, nodes[source], nodes[target], label);
                    break;
            }
        }

        return graph;
    }
    /// <summary>
    ///     A drone-like usage tree: one owner with twelve composite usages (four of them sharing one
    ///     rotor definition, two sharing a battery definition), typing edges into a definitions folder, and
    ///     unused definitions with no connectors.
    /// </summary>
    /// <returns>The graph, laid out downward.</returns>
    public static LayoutGraph DenseUsageTree()
    {
        var graph = Root("auto", LayoutFlowDirection.Down);
        var drone = Folder(graph, "drone", "Drone", null);
        var definitions = Folder(graph, "definitions", "Definitions", null);

        var owner = Usage(drone.Children, "drone", "drone : Drone");
        (string Id, string Label, string Definition)[] parts =
        [
            ("rotor1", "rotor1 : Rotor", "rotorDef"),
            ("rotor2", "rotor2 : Rotor", "rotorDef"),
            ("rotor3", "rotor3 : Rotor", "rotorDef"),
            ("rotor4", "rotor4 : Rotor", "rotorDef"),
            ("battery1", "battery1 : Battery", "batteryDef"),
            ("battery2", "battery2 : Battery", "batteryDef"),
            ("flightController", "flightController : Controller", "controllerDef"),
            ("gps", "gps : GpsReceiver", "gpsDef"),
            ("camera", "camera : Camera", "cameraDef"),
            ("gimbal", "gimbal : Gimbal", "gimbalDef"),
            ("radio", "radio : Radio", "radioDef"),
            ("frame", "frame : Frame", "frameDef"),
        ];

        var defs = new Dictionary<string, LayoutGraphNode>();
        foreach (var (_, label, definition) in parts)
        {
            if (!defs.ContainsKey(definition))
            {
                var name = label.Substring(label.IndexOf(':') + 2);
                defs[definition] = Definition(definitions.Children, definition, name);
            }
        }

        var usageNodes = new List<(string Id, LayoutGraphNode Node, string Definition)>();
        foreach (var (id, label, definition) in parts)
        {
            usageNodes.Add((id, Usage(drone.Children, id, label), definition));
        }

        foreach (var (id, node, _) in usageNodes)
        {
            Composite(drone.Children, $"owns_{id}", node, owner);
        }

        foreach (var (id, node, definition) in usageNodes)
        {
            Typing(graph, $"typing_{id}", node, defs[definition]);
        }

        Typing(graph, "typing_drone", owner, Definition(definitions.Children, "droneDef", "Drone"));
        Definition(definitions.Children, "unusedA", "Payload");
        Definition(definitions.Children, "unusedB", "Tether");
        Definition(graph, "orphan1", "Orphan1");
        return graph;
    }

    /// <summary>
    ///     An interconnection of parts and ports with labeled connectors and end labels, laid out to the
    ///     right, so the corridors between layers must hold labels as well as routing lanes.
    /// </summary>
    /// <param name="layerSpacing">The <c>CoreOptions.LayerSpacing</c> to set on the graph, or <see langword="null"/> to leave it unset.</param>
    /// <returns>The graph.</returns>
    public static LayoutGraph Interconnection(double? layerSpacing)
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "layered");
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Right);
        if (layerSpacing is { } spacing)
        {
            graph.Set(CoreOptions.LayerSpacing, spacing);
        }

        var battery = Part(graph, "battery", "battery : Battery");
        var bus = Part(graph, "bus", "bus : PowerBus");
        var controller = Part(graph, "controller", "controller : MotorController");
        var motorA = Part(graph, "motorA", "motorA : Motor");
        var motorB = Part(graph, "motorB", "motorB : Motor");
        var flight = Part(graph, "flight", "flight : FlightComputer");
        var sensor = Part(graph, "sensor", "sensor : Imu");

        var batteryOut = AddPort(battery, "out", "out : Power", PortDirection.Out);
        var busIn = AddPort(bus, "in", "in : Power", PortDirection.In);
        var busOut1 = AddPort(bus, "out1", "out1 : Power", PortDirection.Out);
        var busOut2 = AddPort(bus, "out2", "out2 : Power", PortDirection.Out);
        var controllerPower = AddPort(controller, "power", "power : Power", PortDirection.In);
        var controllerDrive = AddPort(controller, "drive", "drive : Torque", PortDirection.Out);
        var controllerCmd = AddPort(controller, "cmd", "cmd : Command", PortDirection.In);
        var flightPower = AddPort(flight, "power", "power : Power", PortDirection.In);
        var flightCmd = AddPort(flight, "cmd", "cmd : Command", PortDirection.Out);
        var flightSense = AddPort(flight, "sense", "sense : Attitude", PortDirection.In);
        var motorAIn = AddPort(motorA, "in", "in : Torque", PortDirection.In);
        var motorBIn = AddPort(motorB, "in", "in : Torque", PortDirection.In);
        var sensorOut = AddPort(sensor, "out", "out : Attitude", PortDirection.Out);

        Connect(graph, "supply", batteryOut, busIn, "28 V", new LineEndLabel("source", "1"), new LineEndLabel("sink", "1..*"));
        Connect(graph, "toController", busOut1, controllerPower, "power", new LineEndLabel("feed", "1"), new LineEndLabel("load", "0..1"));
        Connect(graph, "toFlight", busOut2, flightPower, "power", null, null);
        Connect(graph, "command", flightCmd, controllerCmd, "command", new LineEndLabel("issuer", "1"), new LineEndLabel("executor", "1"));
        Connect(graph, "driveA", controllerDrive, motorAIn, "torque A", null, new LineEndLabel("motor", "1"));
        Connect(graph, "driveB", controllerDrive, motorBIn, "torque B", null, new LineEndLabel("motor", "1"));
        Connect(graph, "attitude", sensorOut, flightSense, "attitude", null, null);
        return graph;
    }

    /// <summary>
    ///     An activity flow laid out downward: a start, an action, a decision with guarded branches, a fork
    ///     and join pair, a merge, a back edge that loops to an earlier action, and an end.
    /// </summary>
    /// <returns>The graph.</returns>
    public static LayoutGraph ActionFlow()
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "layered");
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        var start = ActionNode(graph, "start", "start", 40, 28);
        var receive = ActionNode(graph, "receive", "Receive order", 140, 44);
        var validate = ActionNode(graph, "validate", "Validate order", 140, 44);
        var valid = Decision(graph, "valid", "valid?");
        var reject = ActionNode(graph, "reject", "Reject order", 140, 44);
        var fork = Bar(graph, "fork");
        var pick = ActionNode(graph, "pick", "Pick items", 140, 44);
        var pay = ActionNode(graph, "pay", "Charge payment", 140, 44);
        var join = Bar(graph, "join");
        var merge = Decision(graph, "merge", string.Empty);
        var ship = ActionNode(graph, "ship", "Ship order", 140, 44);
        var end = ActionNode(graph, "end", "end", 40, 28);

        Flow(graph, "f1", start, receive, null);
        Flow(graph, "f2", receive, validate, null);
        Flow(graph, "f3", validate, valid, null);
        Flow(graph, "f4", valid, fork, "[yes]");
        Flow(graph, "f5", valid, reject, "[no]");
        Flow(graph, "f6", fork, pick, null);
        Flow(graph, "f7", fork, pay, null);
        Flow(graph, "f8", pick, join, null);
        Flow(graph, "f9", pay, join, null);
        Flow(graph, "f10", join, merge, null);
        Flow(graph, "f11", reject, merge, null);
        Flow(graph, "f12", merge, ship, null);
        Flow(graph, "f13", ship, end, null);
        Flow(graph, "back", reject, receive, "[retry]");
        return graph;
    }

    /// <summary>
    ///     A state machine laid out downward whose reversed (back) edges end in wrap-around corridors and
    ///     whose every edge ends in an open chevron.
    /// </summary>
    /// <returns>The graph.</returns>
    public static LayoutGraph StateMachine()
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "layered");
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        graph.Set(CoreOptions.MergeParallelEdges, false);

        var idle = State(graph, "idle", "Idle");
        var closing = State(graph, "doorsClosing", "DoorsClosing");
        var moving = State(graph, "moving", "Moving");
        var arrived = State(graph, "atFloor", "AtFloor");
        var open = State(graph, "doorsOpen", "DoorsOpen");
        var waiting = State(graph, "waiting", "Waiting");

        Flow(graph, "callReceived", idle, closing, "callReceived");
        Flow(graph, "doorsClosed", closing, moving, "doorsClosed");
        Flow(graph, "arrive", moving, arrived, "atFloor");
        Flow(graph, "openDoors", arrived, open, "doorsAreOpen");
        Flow(graph, "linger", open, waiting, "linger");
        Flow(graph, "timeout", waiting, closing, "timeout");
        Flow(graph, "idleTimeout", waiting, idle, "idleTimeout");
        return graph;
    }

    /// <summary>
    ///     An <c>auto</c> root holding one tall connected chain (laid out downward) and six isolated
    ///     definitions, so the packer must place the isolated nodes beside or below the tall component.
    /// </summary>
    /// <returns>The graph.</returns>
    public static LayoutGraph AutoTallPlusIsolated()
    {
        var graph = Root("auto", LayoutFlowDirection.Down);
        LayoutGraphNode? previous = null;
        for (var i = 0; i < 6; i++)
        {
            var node = Definition(graph, $"chain{i}", $"Chain{i}");
            if (previous is not null)
            {
                Specialize(graph, $"link{i}", node, previous);
            }

            previous = node;
        }

        for (var i = 0; i < 6; i++)
        {
            Definition(graph, $"isolated{i}", $"Isolated{i}");
        }

        return graph;
    }

    /// <summary>
    ///     The Rendering#60 repro: a source feeding two targets, all 100 by 50, with no labels. With the
    ///     default layer spacing the canvas is 310 by 170 when laid out to the right.
    /// </summary>
    /// <param name="algorithm">The root algorithm id.</param>
    /// <param name="direction">The flow direction.</param>
    /// <param name="layerSpacing">The layer spacing to set, or <see langword="null"/> to leave it unset.</param>
    /// <returns>The graph.</returns>
    public static LayoutGraph FanOutPair(string algorithm, LayoutFlowDirection direction, double? layerSpacing)
    {
        var graph = Root(algorithm, direction);
        if (layerSpacing is { } spacing)
        {
            graph.Set(CoreOptions.LayerSpacing, spacing);
        }

        var a = graph.AddNode("a", 100, 50);
        a.Label = "a";
        var b = graph.AddNode("b", 100, 50);
        b.Label = "b";
        var c = graph.AddNode("c", 100, 50);
        c.Label = "c";
        graph.AddEdge("a_b", a, b).TargetEnd = EndMarkerStyle.OpenChevron;
        graph.AddEdge("a_c", a, c).TargetEnd = EndMarkerStyle.OpenChevron;
        return graph;
    }

    private static LayoutGraph Repro(
        string root,
        LayoutFlowDirection direction,
        IReadOnlyList<string> order,
        bool interior,
        bool interiorFirst)
    {
        var graph = Root(root, direction);
        var b = Folder(graph, "B", "B", "layered", tab: false);
        var p = Folder(graph, "P", "P", "layered", tab: false);
        var members = new Dictionary<string, LayoutGraphNode>();
        foreach (var name in order)
        {
            members[name] = Definition(b.Children, name, name);
        }

        Specialize(b.Children, "s_c", members["S"], members["C"]);
        LayoutGraphNode e;
        if (interior && interiorFirst)
        {
            var m = Definition(p.Children, "M", "M");
            e = Definition(p.Children, "E", "E");
            p.Children.AddEdge("m_e", m, e);
        }
        else
        {
            e = Definition(p.Children, "E", "E");
            if (interior)
            {
                var m = Definition(p.Children, "M", "M");
                p.Children.AddEdge("m_e", m, e);
            }
        }

        var k = Definition(graph, "K", "K");
        Specialize(graph, "h_e", members["H"], e);
        graph.AddEdge("e_k", e, k);
        Composite(graph, "c_k", members["C"], k);
        return graph;
    }

    private static void Shuffle<T>(T[] items, Random random)
    {
        for (var i = items.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    private static LayoutGraph Root(string algorithm, LayoutFlowDirection direction)
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, algorithm);
        graph.Set(CoreOptions.Direction, direction);
        return graph;
    }

    private static LayoutGraphNode Definition(LayoutGraph graph, string id, string label, string keyword = "part def")
    {
        var node = graph.AddNode(id, 140, 56);
        node.Label = label;
        node.Keyword = keyword;
        return node;
    }

    private static LayoutGraphNode Usage(LayoutGraph graph, string id, string label)
    {
        var node = graph.AddNode(id, 170, 56);
        node.Label = label;
        node.Keyword = "part";
        node.Shape = BoxShape.RoundedRectangle;
        return node;
    }

    private static LayoutGraphNode Folder(LayoutGraph graph, string id, string label, string? childAlgorithm, bool tab = true)
    {
        var node = graph.AddNode(id, 10, 10);
        node.Label = label;
        node.Shape = BoxShape.Folder;
        if (tab)
        {
            node.Appearance = new BoxAppearance(FolderTitle: FolderTitlePlacement.Tab);
        }

        if (childAlgorithm is not null)
        {
            node.Children.Set(CoreOptions.Algorithm, childAlgorithm);
        }
        return node;
    }

    private static LayoutGraphNode Part(LayoutGraph graph, string id, string label)
    {
        var node = graph.AddNode(id, 170, 80);
        node.Label = label;
        node.Keyword = "part";
        return node;
    }

    private static LayoutGraphPort AddPort(LayoutGraphNode node, string id, string label, PortDirection direction)
    {
        var port = node.Ports.AddPort(id);
        port.ExternalLabel = label;
        port.Appearance = new PortAppearance(direction, LabelPlacement: PortLabelPlacement.Outward);
        return port;
    }

    private static void Connect(
        LayoutGraph graph,
        string id,
        ILayoutConnectable source,
        ILayoutConnectable target,
        string label,
        LineEndLabel? sourceEnd,
        LineEndLabel? targetEnd)
    {
        var edge = graph.AddEdge(id, source, target);
        edge.TargetEnd = EndMarkerStyle.OpenChevron;
        edge.Label = label;
        edge.SourceEndLabel = sourceEnd;
        edge.TargetEndLabel = targetEnd;
    }

    private static LayoutGraphNode ActionNode(LayoutGraph graph, string id, string label, double width, double height)
    {
        var node = graph.AddNode(id, width, height);
        node.Label = label;
        node.Shape = BoxShape.RoundedRectangle;
        return node;
    }

    private static LayoutGraphNode Decision(LayoutGraph graph, string id, string label)
    {
        var node = graph.AddNode(id, 60, 60);
        node.Shape = BoxShape.Diamond;
        if (label.Length > 0)
        {
            node.Label = label;
        }
        return node;
    }

    private static LayoutGraphNode Bar(LayoutGraph graph, string id) => graph.AddNode(id, 120, 8);

    private static LayoutGraphNode State(LayoutGraph graph, string id, string label)
    {
        var node = graph.AddNode(id, 130, 44);
        node.Label = label;
        node.Shape = BoxShape.RoundedRectangle;
        node.RoundedCornerRadius = 12.0;
        return node;
    }

    private static void Flow(LayoutGraph graph, string id, LayoutGraphNode source, LayoutGraphNode target, string? label)
    {
        var edge = graph.AddEdge(id, source, target);
        edge.TargetEnd = EndMarkerStyle.OpenChevron;
        edge.Label = label;
    }

    private static void Specialize(LayoutGraph graph, string id, LayoutGraphNode specific, LayoutGraphNode general) =>
        graph.AddEdge(id, specific, general).TargetEnd = EndMarkerStyle.HollowTriangle;

    private static void Composite(LayoutGraph graph, string id, LayoutGraphNode part, LayoutGraphNode owner) =>
        graph.AddEdge(id, part, owner).TargetEnd = EndMarkerStyle.FilledDiamond;

    private static void Typing(LayoutGraph graph, string id, LayoutGraphNode usage, LayoutGraphNode definition)
    {
        var edge = graph.AddEdge(id, usage, definition);
        edge.TargetEnd = EndMarkerStyle.HollowTriangleTwoDots;
        edge.LineStyle = LineStyle.Dashed;
    }

    private static void Reference(LayoutGraph graph, string id, ILayoutConnectable source, ILayoutConnectable target, string? label)
    {
        var edge = graph.AddEdge(id, source, target);
        edge.TargetEnd = EndMarkerStyle.OpenChevron;
        if (label is not null)
        {
            edge.LineStyle = LineStyle.Dashed;
            edge.Label = label;
        }
    }
}
