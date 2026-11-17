// <copyright file="DirectionScenes.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Graph builders for the flow-direction and edge-label regression tests: the General View shape of a
///     SysML v2 consumer (package folders at the root holding definitions, cross-hierarchy edges, keyword
///     labeled edges, and isolated unconnected definitions).
/// </summary>
internal static class DirectionScenes
{
    /// <summary>The four flow directions every scene is checked in.</summary>
    public static readonly LayoutFlowDirection[] AllDirections =
    [
        LayoutFlowDirection.Down,
        LayoutFlowDirection.Up,
        LayoutFlowDirection.Left,
        LayoutFlowDirection.Right,
    ];

    /// <summary>
    ///     A flat graph of one connected specialization tree plus five isolated definitions, laid out with
    ///     the <c>layered</c> algorithm.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    /// <returns>The graph.</returns>
    public static LayoutGraph FlatIsolated(LayoutFlowDirection direction)
    {
        var graph = Root("layered", direction);
        var vehicle = Definition(graph, "vehicle", "Vehicle");
        var engine = Definition(graph, "engine", "Engine");
        var wheel = Definition(graph, "wheel", "Wheel");
        Specialize(graph, "e1", engine, vehicle);
        Specialize(graph, "e2", wheel, vehicle);
        for (var i = 0; i < 5; i++)
        {
            Definition(graph, $"isolated{i}", $"Isolated{i}");
        }

        return graph;
    }

    /// <summary>
    ///     A package folder holding a connected specialization tree plus isolated definitions, beside
    ///     isolated and connected definitions at the root, laid out with the <c>hierarchical</c> algorithm.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    /// <returns>The graph.</returns>
    public static LayoutGraph ContainerIsolated(LayoutFlowDirection direction)
    {
        var graph = Root("hierarchical", direction);
        var package = Folder(graph, "pkg", "Vehicles");
        var vehicle = Definition(package.Children, "vehicle", "Vehicle");
        var engine = Definition(package.Children, "engine", "Engine");
        var wheel = Definition(package.Children, "wheel", "Wheel");
        Specialize(package.Children, "e1", engine, vehicle);
        Specialize(package.Children, "e2", wheel, vehicle);
        for (var i = 0; i < 4; i++)
        {
            Definition(package.Children, $"inner{i}", $"Inner{i}");
            Definition(graph, $"root{i}", $"Root{i}");
        }

        var rootA = Definition(graph, "rootA", "RootConn1");
        var rootB = Definition(graph, "rootB", "RootConn2");
        Specialize(graph, "e3", rootA, rootB);
        return graph;
    }

    /// <summary>
    ///     The SysML General View shape: three package folders with definitions and unconnected
    ///     definitions inside, isolated definitions at the root, cross-hierarchy usage and <c>«satisfy»</c>
    ///     edges between definitions in different folders, and <c>«import»*</c> edges between folders.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    /// <returns>The graph.</returns>
    public static LayoutGraph GeneralView(LayoutFlowDirection direction)
    {
        var graph = Root("hierarchical", direction);
        var vehicles = Folder(graph, "vehicles", "Vehicles");
        var parts = Folder(graph, "parts", "Parts");
        var requirements = Folder(graph, "requirements", "Requirements");

        var vehicle = Definition(vehicles.Children, "vehicle", "Vehicle");
        var car = Definition(vehicles.Children, "car", "Car");
        var truck = Definition(vehicles.Children, "truck", "Truck");
        Definition(vehicles.Children, "unused1", "Unused1");
        Definition(vehicles.Children, "unused2", "Unused2");
        var engine = Definition(parts.Children, "engine", "Engine");
        var wheel = Definition(parts.Children, "wheel", "Wheel");
        Definition(parts.Children, "unused3", "Unused3");
        var massReq = Definition(requirements.Children, "massReq", "MassReq", "requirement def");
        Definition(requirements.Children, "unused4", "Unused4", "requirement def");

        Specialize(vehicles.Children, "s1", car, vehicle);
        Specialize(vehicles.Children, "s2", truck, vehicle);
        Reference(graph, "u1", vehicle, engine, null);
        Reference(graph, "u2", vehicle, wheel, null);
        Reference(graph, "satisfy", massReq, vehicle, "«satisfy»");
        Reference(graph, "import1", requirements, vehicles, "«import»*");
        Reference(graph, "import2", vehicles, parts, "«import»*");
        for (var i = 0; i < 3; i++)
        {
            Definition(graph, $"orphan{i}", $"Orphan{i}");
        }

        return graph;
    }

    /// <summary>
    ///     Four package folders joined by dashed <c>«import»*</c> edges, one of them between root-level
    ///     folders only (no nested endpoints).
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    /// <returns>The graph.</returns>
    public static LayoutGraph RootFolders(LayoutFlowDirection direction)
    {
        var graph = Root("hierarchical", direction);
        var core = Folder(graph, "core", "Core");
        var parts = Folder(graph, "parts", "Parts");
        var requirements = Folder(graph, "requirements", "Requirements");
        var quantities = Folder(graph, "isq", "ISQ");
        var vehicle = Definition(core.Children, "vehicle", "Vehicle");
        var engine = Definition(core.Children, "engine", "Engine");
        var wheel = Definition(parts.Children, "wheel", "Wheel");
        var axle = Definition(parts.Children, "axle", "Axle");
        Definition(requirements.Children, "massReq", "MassReq", "requirement def");
        Definition(quantities.Children, "mass", "Mass", "attribute def");
        Specialize(core.Children, "s1", engine, vehicle);
        Specialize(parts.Children, "s2", wheel, axle);
        Reference(graph, "i1", core, parts, "«import»*");
        Reference(graph, "i2", requirements, core, "«import»*");
        Reference(graph, "i3", parts, quantities, "«import»");
        Reference(graph, "i4", requirements, quantities, "«import»*");
        Reference(graph, "i5", core, quantities, "«import»*");
        return graph;
    }

    /// <summary>
    ///     A flat graph of four requirements pointing at one definition with alternating
    ///     <c>«satisfy»</c> and <c>«verify»</c> keyword-labeled dashed edges, so four arrowheads land on one
    ///     node face.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    /// <returns>The graph.</returns>
    public static LayoutGraph SatisfyVerify(LayoutFlowDirection direction)
    {
        var graph = Root("layered", direction);
        var target = Definition(graph, "vehicle", "Vehicle");
        string[] keywords = ["«satisfy»", "«verify»", "«satisfy»", "«verify»"];
        for (var i = 0; i < keywords.Length; i++)
        {
            var requirement = Definition(graph, $"req{i}", $"Req{i}", "requirement def");
            Reference(graph, $"e{i}", requirement, target, keywords[i]);
        }

        return graph;
    }

    /// <summary>
    ///     A requirements-and-design view joined only by keyword-labeled dashed edges: <c>«import»*</c>
    ///     between folders and <c>«satisfy»</c>/<c>«verify»</c> edges from definitions in two other folders
    ///     that land on one definition. The connectors run around the outer edge of the diagram, so their
    ///     labels sit at the canvas edge (where the consumer found <c>«import»*</c> clipped).
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    /// <returns>The graph.</returns>
    public static LayoutGraph KeywordLabels(LayoutFlowDirection direction)
    {
        var graph = Root("hierarchical", direction);
        var vehicles = Folder(graph, "vehicles", "Vehicles");
        var requirements = Folder(graph, "requirements", "Requirements");
        var verification = Folder(graph, "verification", "Verification");
        var library = Folder(graph, "library", "Library");

        var vehicle = Definition(vehicles.Children, "vehicle", "Vehicle");
        var engine = Definition(vehicles.Children, "engine", "Engine");
        Specialize(vehicles.Children, "s1", engine, vehicle);
        var massReq = Definition(requirements.Children, "massReq", "MassReq", "requirement def");
        var speedReq = Definition(requirements.Children, "speedReq", "SpeedReq", "requirement def");
        Specialize(requirements.Children, "s2", speedReq, massReq);
        var massTest = Definition(verification.Children, "massTest", "MassTest", "verification def");
        Definition(library.Children, "mass", "Mass", "attribute def");

        Reference(graph, "satisfyMass", massReq, vehicle, "«satisfy»");
        Reference(graph, "satisfySpeed", speedReq, vehicle, "«satisfy»");
        Reference(graph, "verifyMass", massTest, vehicle, "«verify»");
        Reference(graph, "importLibrary", vehicles, library, "«import»*");
        Reference(graph, "importRequirements", requirements, library, "«import»*");
        Reference(graph, "importVerification", verification, vehicles, "«import»*");
        return graph;
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

    private static LayoutGraphNode Folder(LayoutGraph graph, string id, string label)
    {
        var node = graph.AddNode(id, 10, 10);
        node.Label = label;
        node.Shape = BoxShape.Folder;
        node.Appearance = new BoxAppearance(FolderTitle: FolderTitlePlacement.Tab);
        return node;
    }

    private static void Specialize(LayoutGraph graph, string id, LayoutGraphNode specific, LayoutGraphNode general)
    {
        var edge = graph.AddEdge(id, specific, general);
        edge.TargetEnd = EndMarkerStyle.HollowTriangle;
    }

    private static void Reference(LayoutGraph graph, string id, LayoutGraphNode source, LayoutGraphNode target, string? label)
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
