// <copyright file="GalleryDiagrams.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout;

namespace DemaConsulting.Rendering.Gallery;

/// <summary>
///     Builds the stable, deterministic input graphs the gallery showcase renders. Every diagram is
///     described purely through the public <see cref="LayoutGraph"/> model so the committed images do
///     not churn between runs.
/// </summary>
internal static class GalleryDiagrams
{
    /// <summary>
    ///     A directed processing pipeline with a side branch, suited to the layered algorithm which
    ///     places it in left-to-right layers and routes the edges between them.
    /// </summary>
    /// <returns>A flat graph of labelled boxes joined by arrowed edges.</returns>
    public static LayoutGraph LayeredPipeline()
    {
        var graph = new LayoutGraph();
        var ingest = AddLabelled(graph, "ingest", "Ingest");
        var validate = AddLabelled(graph, "validate", "Validate");
        var transform = AddLabelled(graph, "transform", "Transform");
        var render = AddLabelled(graph, "render", "Render");
        var publish = AddLabelled(graph, "publish", "Publish");
        var report = AddLabelled(graph, "report", "Report");

        Connect(graph, "ingest-validate", ingest, validate);
        Connect(graph, "validate-transform", validate, transform);
        Connect(graph, "transform-render", transform, render);
        Connect(graph, "render-publish", render, publish);
        Connect(graph, "validate-report", validate, report);

        return graph;
    }

    /// <summary>
    ///     Regression coverage (and visual evidence) for the isolated-node layer-gap fix: a genuinely
    ///     isolated real node (zero incident edges) shares layer 0 with an unrelated node whose single
    ///     outgoing edge targets the bottom-most node of a tall next layer, pulling its own vertical
    ///     position far down to straighten that edge. Before the fix, the crossing minimizer's
    ///     barycenter sort fell back to the isolated node's arbitrary insertion index, which could sort
    ///     it directly between an ordinary node and the pulled-down node; the layered algorithm's
    ///     Brandes-Köpf coordinate assignment could then leave the isolated node's own position
    ///     untouched while its neighbor was dragged far away, producing a gap many times larger than the
    ///     standard node spacing.
    /// </summary>
    /// <returns>
    ///     A layered graph whose first layer holds an ordinary fan-out source (<c>entry</c>), one
    ///     genuinely isolated node (<c>isolated</c>, added between <c>entry</c> and <c>farSource</c> so
    ///     its insertion index sits between them), and an unrelated source (<c>farSource</c>) whose
    ///     single edge targets the bottom-most node of a tall five-node hub column, pulling
    ///     <c>farSource</c>'s own position far down within layer 0.</returns>
    public static LayoutGraph IsolatedNodeLayerGap()
    {
        var graph = new LayoutGraph();

        // Layer 0: an ordinary fan-out source, an unrelated source whose single outgoing edge targets
        // the bottom-most node of a tall next layer (pulling its own vertical position far down to
        // straighten that edge), and one genuinely isolated node with zero edges inserted between them
        // by insertion order — the pre-fix fallback sort key for a neighbor-less node — so it can land
        // sandwiched between an ordinary node and the pulled-down node.
        var entry = AddLabelled(graph, "entry", "Entry");
        AddLabelled(graph, "isolated", "Isolated");
        var farSource = AddLabelled(graph, "farSource", "FarSource");

        // Layer 1 (the "hub column"): a tall stack of ordinary nodes fed by entry, so the coordinate
        // assignment pass spreads them across a wide vertical range.
        var hubA = AddLabelled(graph, "hubA", "HubA");
        var hubB = AddLabelled(graph, "hubB", "HubB");
        var hubC = AddLabelled(graph, "hubC", "HubC");
        var hubD = AddLabelled(graph, "hubD", "HubD");
        var hubE = AddLabelled(graph, "hubE", "HubE");

        // Layer 2: the ordinary hub target.
        var sink = AddLabelled(graph, "sink", "Sink");

        Connect(graph, "entry-hubA", entry, hubA);
        Connect(graph, "entry-hubB", entry, hubB);
        Connect(graph, "entry-hubC", entry, hubC);
        Connect(graph, "entry-hubD", entry, hubD);
        Connect(graph, "entry-hubE", entry, hubE);
        Connect(graph, "hubA-sink", hubA, sink);
        Connect(graph, "hubB-sink", hubB, sink);
        Connect(graph, "hubC-sink", hubC, sink);
        Connect(graph, "hubD-sink", hubD, sink);
        Connect(graph, "hubE-sink", hubE, sink);

        // The unrelated edge: entirely disconnected from the hub fan-out, but it targets the
        // bottom-most hub node, so vertical alignment pulls farSource's own layer-0 position far down
        // to straighten it — exercising the same "inherited unrelated floor" mechanism the fix targets.
        Connect(graph, "farSource-hubE", farSource, hubE);

        return graph;
    }

    /// <summary>
    ///     A set of differently-sized sibling boxes with no edges, suited to the containment algorithm
    ///     which shelf-packs them left-to-right and wraps them into a compact block. The varied widths
    ///     make the row packing visible, distinguishing it from the layered algorithm's connectivity-driven
    ///     layers.
    /// </summary>
    /// <returns>A flat graph of labelled boxes of varied widths for packing.</returns>
    public static LayoutGraph ContainmentPacked()
    {
        var graph = new LayoutGraph();

        // Labelled peers of varied widths; with no edges, the containment algorithm packs them by
        // reading order into rows that wrap into a tidy rectangle.
        (string Label, double Width)[] boxes =
        [
            ("Core", 90),
            ("Model", 150),
            ("Layout", 110),
            ("Svg", 70),
            ("Skia", 80),
            ("Abstractions", 200),
            ("Themes", 120),
            ("Options", 130),
            ("Engine", 110),
            ("Registry", 140),
            ("Renderer", 150),
            ("Graph", 90),
        ];

        for (var i = 0; i < boxes.Length; i++)
        {
            var node = graph.AddNode($"box{i}", boxes[i].Width, 50);
            node.Label = boxes[i].Label;
        }

        return graph;
    }

    /// <summary>
    ///     Regression coverage (and visual evidence) for the parallel-edges-into-compartment-box fix: a
    ///     small source box and a much taller target box with a nine-row compartment, joined by nine
    ///     distinct unmerged edges (mirroring <see cref="CoreOptions.MergeParallelEdges"/> set to
    ///     <see langword="false"/>). Before the fix, <c>ConnectorRouter</c> excluded a connection's own
    ///     endpoint boxes from the hard-obstacle set for its <em>entire</em> route rather than just its
    ///     final docking stub, so a later connector squeezed by earlier already-routed connectors' soft
    ///     obstacles could detour straight through its own target box's interior — cutting across the
    ///     compartment's row text — before curving back out to its real anchor. After the fix, every box
    ///     (including a connection's own endpoints) is a hard obstacle for the rest of the path.
    /// </summary>
    /// <returns>
    ///     A flat graph suited to the containment algorithm: a small <c>Source</c> box and a taller
    ///     <c>Target</c> box carrying a nine-row compartment, joined by nine distinct edges.
    /// </returns>
    public static LayoutGraph ParallelEdgesIntoCompartmentBox()
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.MergeParallelEdges, false);

        var source = graph.AddNode("source", 130, 50);
        source.Label = "Source";

        var target = graph.AddNode("target", 130, 242);
        target.Label = "Target";
        target.Compartments =
        [
            new LayoutCompartment(
                "rows",
                ["row1", "row2", "row3", "row4", "row5", "row6", "row7", "row8", "row9"]),
        ];

        for (var i = 0; i < 9; i++)
        {
            var edge = graph.AddEdge($"source-target-{i}", source, target);
            edge.TargetEnd = EndMarkerStyle.FilledDiamond;
        }

        return graph;
    }

    /// <summary>
    ///     Permanent visual regression coverage for the connector-router "coiled/looping" robustness
    ///     fix (<c>OrthogonalEdgeRouter</c>'s soft-obstacle envelope clamp and envelope-departure cost),
    ///     built with explicit, hardcoded box positions rather than any layout algorithm. This deliberately
    ///     forces the exact geometry that exposed the bug — a small <c>Source</c> box beside a much
    ///     taller, nine-row-compartment <c>Target</c> box, separated by a narrow gap, joined by nine
    ///     distinct unmerged edges — regardless of which layout algorithm (or future heuristic change)
    ///     happens to produce a side-by-side arrangement for a similar graph. The companion
    ///     <see cref="ParallelEdgesIntoCompartmentBox"/> diagram exercises the same defect's vertical-stack
    ///     arrangement through the containment algorithm; this one exists so the narrow-gap, side-by-side
    ///     arrangement itself remains covered even if no algorithm currently produces it. Because it
    ///     bypasses <see cref="LayoutEngine.Layout(LayoutGraph)"/> entirely, it belongs only to the
    ///     <c>custom-rendering</c> gallery group, never to a group showcasing algorithm output.
    /// </summary>
    /// <param name="theme">The theme the diagram will be rendered with, used to size the target box's compartment.</param>
    /// <returns>
    ///     A hand-built <see cref="LayoutTree"/> with the two boxes placed explicitly and the nine
    ///     connectors already routed via <see cref="ConnectorRouter.Route(IReadOnlyList{LayoutBox},IReadOnlyList{Connection},ConnectorRouteOptions)"/>.
    /// </returns>
    public static LayoutTree ParallelEdgesIntoCompartmentBoxSideBySide(Theme theme)
    {
        var rowsCompartment = new LayoutCompartment(
            "rows",
            ["row1", "row2", "row3", "row4", "row5", "row6", "row7", "row8", "row9"]);
        var titledHeight = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: false);

        var source = new LayoutBox(12, 12, 130, 50, "Source", 0, BoxShape.Rectangle, [], []);
        var target = new LayoutBox(
            166,
            12,
            130,
            titledHeight + TitledCompartmentHeight(theme, rowsCompartment),
            "Target",
            0,
            BoxShape.Rectangle,
            [rowsCompartment],
            []);
        var boxes = new[] { source, target };

        var connections = Enumerable.Range(0, 9)
            .Select(_ => new Connection(source, target, EndMarkerStyle.FilledDiamond))
            .ToArray();
        var lines = ConnectorRouter.Route(boxes, connections, new ConnectorRouteOptions());

        var nodes = new List<LayoutNode>();
        nodes.AddRange(boxes);
        nodes.AddRange(lines);

        var width = target.X + target.Width + 12;
        var height = Math.Max(source.Y + source.Height, target.Y + target.Height) + 12;
        return new LayoutTree(width, height, nodes);
    }

    /// <summary>
    ///     A container node holding a nested child graph, plus an external leaf joined to a child by a
    ///     cross-container edge — the canonical hierarchical (nested) diagram.
    /// </summary>
    /// <returns>A two-level compound graph.</returns>
    public static LayoutGraph HierarchicalNested()
    {
        var graph = new LayoutGraph();

        var service = graph.AddNode("service", 10, 10);
        service.Label = "Service";
        var api = AddLabelled(service.Children, "api", "API");
        var worker = AddLabelled(service.Children, "worker", "Worker");
        var store = AddLabelled(service.Children, "store", "Store");
        Connect(service.Children, "api-worker", api, worker);
        Connect(service.Children, "worker-store", worker, store);

        var client = AddLabelled(graph, "client", "Client");

        // A cross-container edge added at the lowest common ancestor (the root) referencing a
        // descendant node inside the container.
        Connect(graph, "client-api", client, api);

        return graph;
    }

    /// <summary>
    ///     Three sibling containers packed in a row with a cross-container edge from a child of the first
    ///     to a child of the third, forcing the orthogonal router to step around the middle container.
    /// </summary>
    /// <returns>A compound graph whose cross-container edge must avoid an intervening obstacle.</returns>
    public static LayoutGraph OrthogonalObstacle()
    {
        var graph = new LayoutGraph();

        var left = graph.AddNode("left", 10, 10);
        left.Label = "Left";
        var source = AddLabelled(left.Children, "source", "Source");

        var middle = graph.AddNode("middle", 10, 10);
        middle.Label = "Obstacle";
        AddLabelled(middle.Children, "blocker", "Blocker");

        var right = graph.AddNode("right", 10, 10);
        right.Label = "Right";
        var sink = AddLabelled(right.Children, "sink", "Sink");

        Connect(graph, "source-sink", source, sink);

        return graph;
    }

    /// <summary>
    ///     A compact nested diagram used to compare the built-in themes: a container with two children
    ///     and an edge exercises depth-based fills, stroke colour, and corner rounding.
    /// </summary>
    /// <returns>A two-level compound graph for theme comparison.</returns>
    public static LayoutGraph ThemeShowcase()
    {
        var graph = new LayoutGraph();

        var module = graph.AddNode("module", 10, 10);
        module.Label = "Module";
        var input = AddLabelled(module.Children, "input", "Input");
        var output = AddLabelled(module.Children, "output", "Output");
        Connect(module.Children, "input-output", input, output);

        var sink = AddLabelled(graph, "consumer", "Consumer");
        Connect(graph, "output-consumer", output, sink);

        return graph;
    }

    /// <summary>
    ///     A compact directed flow — a start, two sequential steps, a branch, and an end — used to show
    ///     the same graph laid out in two flow directions. Read left-to-right it is a pipeline; read
    ///     top-to-bottom it is an action flow, which is exactly what the direction option selects.
    /// </summary>
    /// <returns>A flat directed graph suited to a direction comparison.</returns>
    public static LayoutGraph DirectionShowcase()
    {
        var graph = new LayoutGraph();
        var start = AddLabelled(graph, "start", "Start");
        var collect = AddLabelled(graph, "collect", "Collect");
        var analyze = AddLabelled(graph, "analyze", "Analyze");
        var report = AddLabelled(graph, "report", "Report");
        var archive = AddLabelled(graph, "archive", "Archive");

        Connect(graph, "start-collect", start, collect);
        Connect(graph, "collect-analyze", collect, analyze);
        Connect(graph, "analyze-report", analyze, report);
        Connect(graph, "analyze-archive", analyze, archive);

        return graph;
    }

    /// <summary>
    ///     A container node whose own child graph overrides the flow direction to Down, nested inside an
    ///     outer graph that uses the default rightward direction. Direct edges connect the outer intake
    ///     and archive leaves straight to the container itself (not to its nested descendants), so the
    ///     root scope has real connectivity of its own and is genuinely laid out left-to-right, while the
    ///     container's nested chain is laid out top-to-bottom. This demonstrates that a container's own
    ///     <see cref="CoreOptions.Direction"/> override is honored independently of its parent's flow
    ///     direction.
    /// </summary>
    /// <returns>A two-level compound graph mixing flow directions across nesting levels.</returns>
    public static LayoutGraph MixedDirectionNested()
    {
        var graph = new LayoutGraph();

        var intake = AddLabelled(graph, "intake", "Intake");

        var pipeline = graph.AddNode("pipeline", 10, 10);
        pipeline.Label = "Pipeline";
        pipeline.Children.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        var validate = AddLabelled(pipeline.Children, "validate", "Validate");
        var transform = AddLabelled(pipeline.Children, "transform", "Transform");
        var publish = AddLabelled(pipeline.Children, "publish", "Publish");
        Connect(pipeline.Children, "validate-transform", validate, transform);
        Connect(pipeline.Children, "transform-publish", transform, publish);

        var archive = AddLabelled(graph, "archive", "Archive");

        // Direct root-level edges (both endpoints are direct members of the root scope, not nested
        // descendants) so the outer scope has real connectivity and is genuinely laid out left-to-right,
        // rather than falling back to disconnected-singleton packing.
        Connect(graph, "intake-pipeline", intake, pipeline);
        Connect(graph, "pipeline-archive", pipeline, archive);

        return graph;
    }

    /// <summary>
    ///     A folder container holding two boxes with a keyword line, one also compartmented, joined by a decorated edge —
    ///     a block-diagram notation used by SysML and similar modeling languages, but expressed purely
    ///     through the generic <see cref="LayoutGraphNode.Shape"/>, <see cref="LayoutGraphNode.Keyword"/>,
    ///     and <see cref="LayoutGraphNode.Compartments"/> properties on the input graph model, with no
    ///     SysML-specific code anywhere in the rendering pipeline.
    /// </summary>
    /// <remarks>
    ///     A leaf algorithm places a box at exactly the width and height its node declares — it never
    ///     grows a box to fit a keyword line or compartment rows — so the caller must size each node
    ///     tall enough to hold its title area (<see cref="BoxMetrics.TitleAreaHeight"/>) plus every
    ///     compartment's own rows, exactly as a caller such as a SysML general-view layout strategy does.
    /// </remarks>
    /// <returns>A two-level compound graph with a folder container and boxes carrying a keyword and compartments.</returns>
    public static LayoutGraph BoxAppearance()
    {
        var theme = Themes.Dark;
        var titleHeight = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true);

        var graph = new LayoutGraph();

        var pkg = graph.AddNode("pkg", 10, 10);
        pkg.Label = "Powertrain";
        pkg.Shape = BoxShape.Folder;
        pkg.Keyword = "package";

        // The "ports" compartment adds a title row plus one row per port, sized from the same theme
        // metrics the renderer uses, so the compartment text never overflows the box.
        var portsCompartment = new LayoutCompartment("ports", ["intake : FluidPort", "exhaust : FluidPort"]);
        var compartmentHeight = theme.LabelPadding + theme.FontSizeBody + theme.LabelPadding // title row
            + (portsCompartment.Rows.Count * (theme.LabelPadding + theme.FontSizeBody)) // data rows
            + theme.LabelPadding; // bottom gap

        var engine = pkg.Children.AddNode("engine", 160, titleHeight + compartmentHeight);
        engine.Label = "Engine";
        engine.Keyword = "part def";
        engine.Compartments = [portsCompartment];

        var motor = pkg.Children.AddNode("motor", 160, titleHeight);
        motor.Label = "ElectricMotor";
        motor.Keyword = "part def";

        var edge = pkg.Children.AddEdge("motor-engine", motor, engine);
        edge.TargetEnd = EndMarkerStyle.HollowTriangle;

        return graph;
    }

    /// <summary>
    ///     An edge landing on a <see cref="BoxShape.Folder"/> container's top face, demonstrating
    ///     shape-aware connector anchoring: the router keeps the connector off the folder's tab (the
    ///     small raised label strip at the top-left) and projects the anchor down to the folder's
    ///     actual recessed outline instead of the plain bounding rectangle, so the line visibly touches
    ///     the drawn shape rather than floating above it.
    /// </summary>
    /// <remarks>
    ///     The edge connects directly to the folder node itself (a direct root member, not a
    ///     descendant), so it belongs to the root's own leaf view and its ranking honors the downward
    ///     flow direction below, reliably placing <c>Client</c> above the folder — the one relationship
    ///     where the tab's presence actually matters. A cross-container edge into a descendant would be
    ///     excluded from that view and ranked only by insertion order, not by flow direction.
    /// </remarks>
    /// <returns>A compound graph with an external node connected into a folder container from above.</returns>
    public static LayoutGraph FolderTopFaceAnchor()
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        var pkg = graph.AddNode("utilities", 10, 10);
        pkg.Label = "Utilities";
        pkg.Shape = BoxShape.Folder;
        pkg.Keyword = "package";

        var globMatcher = AddLabelled(pkg.Children, "glob-matcher", "GlobMatcher");
        var pathHelpers = AddLabelled(pkg.Children, "path-helpers", "PathHelpers");
        Connect(pkg.Children, "glob-matcher-path-helpers", globMatcher, pathHelpers);

        var client = AddLabelled(graph, "client", "Client");

        // Connect directly to the folder node itself (a direct root member, not a descendant), so this
        // edge belongs to the root's own leaf view and its ranking honors the Direction override above,
        // reliably placing Client above the folder. A cross-container edge into a descendant would be
        // excluded from that view and ranked only by insertion order, not by Direction.
        Connect(graph, "client-utilities", client, pkg);

        return graph;
    }

    /// <summary>
    ///     One sibling of each <see cref="BoxShape"/> value, side by side, each carrying content
    ///     appropriate to that shape: a plain rectangle and a rounded rectangle each with a keyword line
    ///     and a labelled compartment, a folder holding a nested child box, and a note holding an
    ///     untitled compartment of free-form text. Demonstrates that every shape reserves enough space
    ///     for its own content (title area, compartments, or nested children) without the content
    ///     overlapping the shape's non-rectangular features (the folder's tab, the note's folded
    ///     corner).
    /// </summary>
    /// <returns>A flat graph of four sibling containers, one per <see cref="BoxShape"/> value.</returns>
    public static LayoutGraph ShapeGallery()
    {
        var theme = Themes.Dark;
        var graph = new LayoutGraph();

        var titledKeywordHeight = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: true);
        var titledOnlyHeight = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: false);

        // A plain rectangle with a keyword line and a labelled compartment.
        var attributesCompartment = new LayoutCompartment("attributes", ["value : Real", "unit : String"]);
        var sensor = graph.AddNode("sensor", 160, titledKeywordHeight + TitledCompartmentHeight(theme, attributesCompartment));
        sensor.Label = "Sensor";
        sensor.Keyword = "part def";
        sensor.Compartments = [attributesCompartment];

        // A rounded rectangle with a keyword line and a labelled compartment.
        var portsCompartment = new LayoutCompartment("ports", ["cmd : Signal"]);
        var controller = graph.AddNode("controller", 160, titledKeywordHeight + TitledCompartmentHeight(theme, portsCompartment));
        controller.Label = "Controller";
        controller.Keyword = "part def";
        controller.Shape = BoxShape.RoundedRectangle;
        controller.RoundedCornerRadius = 14.0;
        controller.Compartments = [portsCompartment];

        // A folder holding a single nested child box.
        var utilities = graph.AddNode("utilities", 160, titledKeywordHeight + 50 + (2 * 12));
        utilities.Label = "Utilities";
        utilities.Keyword = "package";
        utilities.Shape = BoxShape.Folder;
        AddLabelled(utilities.Children, "path-helpers", "PathHelpers");

        // A note holding an untitled compartment of free-form text, exercising the folded-corner
        // routing fix: the compartment divider and text sit clear of the diagonal fold.
        var noteBody = new LayoutCompartment(null, ["Values expressed in SI units", "unless stated otherwise."]);
        var note = graph.AddNode("note", 200, titledOnlyHeight + UntitledCompartmentHeight(theme, noteBody));
        note.Label = "Note";
        note.Shape = BoxShape.Note;
        note.Compartments = [noteBody];

        return graph;
    }

    /// <summary>
    ///     Two boxes joined by three distinct parallel connectors, each with its own label, laid out
    ///     with <see cref="CoreOptions.MergeParallelEdges"/> set to <see langword="false"/> so every
    ///     parallel edge survives as its own independently-routed connector.
    /// </summary>
    /// <returns>A two-node graph with three parallel edges and merging disabled.</returns>
    public static LayoutGraph ParallelEdgesPreserved()
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.MergeParallelEdges, false);

        var producer = AddLabelled(graph, "producer", "Producer");
        var consumer = AddLabelled(graph, "consumer", "Consumer");

        Connect(graph, "primary", producer, consumer, "primary");
        Connect(graph, "retry", producer, consumer, "retry");
        Connect(graph, "audit", producer, consumer, "audit");

        return graph;
    }

    /// <summary>
    ///     The same two boxes and three parallel connectors as <see cref="ParallelEdgesPreserved"/>, but
    ///     laid out with the default <see cref="CoreOptions.MergeParallelEdges"/> (<see langword="true"/>),
    ///     so exactly one connector (the first) survives — the companion comparison case.
    /// </summary>
    /// <returns>A two-node graph with three parallel edges and the default merge behavior.</returns>
    public static LayoutGraph ParallelEdgesMerged()
    {
        var graph = new LayoutGraph();

        var producer = AddLabelled(graph, "producer", "Producer");
        var consumer = AddLabelled(graph, "consumer", "Consumer");

        Connect(graph, "primary", producer, consumer, "primary");
        Connect(graph, "retry", producer, consumer, "retry");
        Connect(graph, "audit", producer, consumer, "audit");

        return graph;
    }

    /// <summary>
    ///     The same three parallel labeled connectors as <see cref="ParallelEdgesPreserved"/>, but laid
    ///     out with a downward <see cref="CoreOptions.Direction"/> so the connectors attach to the
    ///     top and bottom box faces instead of the left and right faces — the companion vertical-flow
    ///     case, following the same horizontal/vertical pairing convention as
    ///     <see cref="PortsShowcaseHorizontal"/>/<see cref="PortsShowcaseVertical"/>. Proves that box
    ///     WIDTH (not height) auto-grows to fit the widened parallel-edge lane spacing when the shared
    ///     face is Top/Bottom, since <c>PortDistributor</c> spreads anchors on that face horizontally.
    /// </summary>
    /// <returns>A two-node graph with three parallel edges, merging disabled, and a downward flow.</returns>
    public static LayoutGraph ParallelEdgesPreservedVertical()
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.MergeParallelEdges, false);
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        var producer = AddLabelled(graph, "producer", "Producer");
        var consumer = AddLabelled(graph, "consumer", "Consumer");

        Connect(graph, "primary", producer, consumer, "primary");
        Connect(graph, "retry", producer, consumer, "retry");
        Connect(graph, "audit", producer, consumer, "audit");

        return graph;
    }

    /// <summary>
    ///     A three-node left-to-right chain exercising named <see cref="LayoutGraphPort"/>s on the
    ///     left and right sides of the middle node, including a deliberately long left-side label
    ///     demonstrating the auto-computed <see cref="LayoutBox.ContentInsetLeft"/> reserved margin.
    /// </summary>
    /// <remarks>
    ///     A port's rendered side is derived purely from where the layered algorithm's own routing
    ///     anchors the connector on the owning node's placed rectangle (Phase 1 deliberately has no
    ///     <c>Side</c> property to request a side directly — see <see cref="LayoutGraphPort"/>). Under
    ///     the default rightward <see cref="CoreOptions.Direction"/>, the layer-progression axis is
    ///     horizontal, so every inter-layer connector anchors on a left or right face; a companion
    ///     <see cref="PortsShowcaseVertical"/> uses a downward flow to exercise the top and bottom
    ///     faces the same way.
    /// </remarks>
    /// <returns>A three-node graph with left/right ports on its middle node.</returns>
    public static LayoutGraph PortsShowcaseHorizontal()
    {
        var graph = new LayoutGraph();

        var upstream = AddLabelled(graph, "upstream", "Upstream");
        var hub = AddLabelled(graph, "hub", "Hub");
        var downstream = AddLabelled(graph, "downstream", "Downstream");

        // Left side (incoming): a deliberately long label to demonstrate the auto-computed inset.
        var inPort = hub.Ports.AddPort("in");
        inPort.ExternalLabel = "a rather long incoming data label";
        Connect(graph, "upstream-hub", upstream, inPort, null);

        // Right side (outgoing).
        var outPort = hub.Ports.AddPort("out");
        outPort.ExternalLabel = "out";
        Connect(graph, "hub-downstream", outPort, downstream, null);

        return graph;
    }

    /// <summary>
    ///     A three-node top-to-bottom chain exercising named <see cref="LayoutGraphPort"/>s on the top
    ///     and bottom sides of the middle node — the companion to
    ///     <see cref="PortsShowcaseHorizontal"/>, using a downward <see cref="CoreOptions.Direction"/>
    ///     so the layer-progression axis (and therefore every inter-layer connector's anchor face) is
    ///     vertical instead of horizontal.
    /// </summary>
    /// <returns>A three-node graph with top/bottom ports on its middle node.</returns>
    public static LayoutGraph PortsShowcaseVertical()
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        var monitor = AddLabelled(graph, "monitor", "Monitor");
        var hub = AddLabelled(graph, "hub", "Hub");
        var control = AddLabelled(graph, "control", "Control");

        // Top side (incoming).
        var statusPort = hub.Ports.AddPort("status");
        statusPort.ExternalLabel = "status";
        Connect(graph, "monitor-hub", monitor, statusPort, null);

        // Bottom side (outgoing).
        var ctrlPort = hub.Ports.AddPort("ctrl");
        ctrlPort.ExternalLabel = "ctrl";
        Connect(graph, "hub-control", ctrlPort, control, null);

        return graph;
    }

    /// <summary>
    ///     A five-node left-to-right chain exercising <em>two</em> named <see cref="LayoutGraphPort"/>s
    ///     on each of the middle node's left and right sides — the multi-connector companion to
    ///     <see cref="PortsShowcaseHorizontal"/>'s single-port-per-side case, demonstrating
    ///     same-face crowding: two independently-labelled ports sharing one face, one of them
    ///     deliberately long, so the box's <see cref="LayoutBox.ContentInsetLeft"/>/Right auto-grow
    ///     from the widest same-side label and PortDistributor's even spacing between the two anchors
    ///     never collapses onto a single row.
    /// </summary>
    /// <remarks>
    ///     Also exercises this session's title-vs-side-port collision protection with a genuinely
    ///     crowded face (not just the single-port case): the hub's title must stay clear of both
    ///     stacked port rows on either side, at every combination of label length.
    /// </remarks>
    /// <returns>A five-node graph with two left ports and two right ports on its middle node.</returns>
    public static LayoutGraph PortsShowcaseMultiConnectorHorizontal()
    {
        var graph = new LayoutGraph();

        var sensorA = AddLabelled(graph, "sensor-a", "SensorA");
        var sensorB = AddLabelled(graph, "sensor-b", "SensorB");
        var hub = AddLabelled(graph, "hub", "Hub");
        var actuatorA = AddLabelled(graph, "actuator-a", "ActuatorA");
        var actuatorB = AddLabelled(graph, "actuator-b", "ActuatorB");

        // Left side (incoming): two stacked ports, one with a deliberately long label.
        var telemetryPort = hub.Ports.AddPort("telemetry");
        telemetryPort.ExternalLabel = "a rather long telemetry feed";
        Connect(graph, "sensorA-hub", sensorA, telemetryPort, null);

        var heartbeatPort = hub.Ports.AddPort("heartbeat");
        heartbeatPort.ExternalLabel = "heartbeat";
        Connect(graph, "sensorB-hub", sensorB, heartbeatPort, null);

        // Right side (outgoing): two more stacked ports, again of differing label length.
        var primaryDrivePort = hub.Ports.AddPort("primary-drive");
        primaryDrivePort.ExternalLabel = "primary drive command";
        Connect(graph, "hub-actuatorA", primaryDrivePort, actuatorA, null);

        var diagnosticsPort = hub.Ports.AddPort("diagnostics");
        diagnosticsPort.ExternalLabel = "diag";
        Connect(graph, "hub-actuatorB", diagnosticsPort, actuatorB, null);

        return graph;
    }

    /// <summary>
    ///     A five-node top-to-bottom chain exercising <em>two</em> named <see cref="LayoutGraphPort"/>s
    ///     on each of the middle node's top and bottom sides — the vertical companion to
    ///     <see cref="PortsShowcaseMultiConnectorHorizontal"/>, proving the same same-face crowding and
    ///     title-collision protection when PortDistributor spreads anchors horizontally along a
    ///     top/bottom face instead of vertically along a left/right one.
    /// </summary>
    /// <returns>A five-node graph with two top ports and two bottom ports on its middle node.</returns>
    public static LayoutGraph PortsShowcaseMultiConnectorVertical()
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        var monitorA = AddLabelled(graph, "monitor-a", "MonitorA");
        var monitorB = AddLabelled(graph, "monitor-b", "MonitorB");
        var hub = AddLabelled(graph, "hub", "Hub");
        var controlA = AddLabelled(graph, "control-a", "ControlA");
        var controlB = AddLabelled(graph, "control-b", "ControlB");

        // Top side (incoming): two side-by-side ports, one with a deliberately long label.
        var statusPort = hub.Ports.AddPort("status");
        statusPort.ExternalLabel = "a rather long status feed";
        Connect(graph, "monitorA-hub", monitorA, statusPort, null);

        var alertPort = hub.Ports.AddPort("alert");
        alertPort.ExternalLabel = "alert";
        Connect(graph, "monitorB-hub", monitorB, alertPort, null);

        // Bottom side (outgoing): two more side-by-side ports.
        var ctrlPrimaryPort = hub.Ports.AddPort("ctrl-primary");
        ctrlPrimaryPort.ExternalLabel = "primary control command";
        Connect(graph, "hub-controlA", ctrlPrimaryPort, controlA, null);

        var ctrlBackupPort = hub.Ports.AddPort("ctrl-backup");
        ctrlBackupPort.ExternalLabel = "backup";
        Connect(graph, "hub-controlB", ctrlBackupPort, controlB, null);

        return graph;
    }

    /// <summary>
    ///     A titled hub node with several <em>unlabeled</em> ports fanning out to unevenly-spaced
    ///     targets — regression coverage (and visual evidence) for the growth-floor fix ensuring a face
    ///     with 2+ anchors still reserves adequate per-port clearance even when none of them carry any
    ///     text at all. Mirrors the externally reported "Motherboard" bug: a box with a pile of plain,
    ///     unlabeled connectors used to never grow past its caller-supplied size to give them room,
    ///     visually bunching several connector exit points close together near the face's center.
    /// </summary>
    /// <returns>A six-node graph whose central hub has four unlabeled outgoing ports (a fifth node,
    /// <c>memory</c>, hangs off <c>cpu</c> rather than the hub directly).</returns>
    public static LayoutGraph PortsShowcaseUnlabeledFanOut()
    {
        var graph = new LayoutGraph();

        var board = graph.AddNode("board", 140, 50);
        board.Label = "board : Motherboard";

        var cpu = AddLabelled(graph, "cpu", "cpu : Cpu");
        var memory = AddLabelled(graph, "memory", "memory : Ram");
        var storage = AddLabelled(graph, "storage", "storage : Ssd");
        var network = AddLabelled(graph, "network", "network : NetworkCard");
        var graphics = AddLabelled(graph, "graphics", "graphics : Gpu");

        var cpuPort = board.Ports.AddPort("cpu-link");
        Connect(graph, "board-cpu", cpuPort, cpu, null);
        Connect(graph, "cpu-memory", cpu, memory, null);

        var storagePort = board.Ports.AddPort("storage-link");
        Connect(graph, "board-storage", storagePort, storage, null);

        var networkPort = board.Ports.AddPort("network-link");
        Connect(graph, "board-network", networkPort, network, null);

        var graphicsPort = board.Ports.AddPort("graphics-link");
        Connect(graph, "board-graphics", graphicsPort, graphics, null);

        return graph;
    }

    /// <summary>
    ///     A sibling node joined to a container's <em>boundary (delegation) port</em>, which in turn
    ///     delegates inward to two nested children — the canonical boundary-port diagram, laid out
    ///     left-to-right.
    /// </summary>
    /// <remarks>
    ///     A boundary port is a <see cref="LayoutGraphPort"/> that carries <em>both</em> an
    ///     <see cref="LayoutGraphPort.ExternalLabel"/> and an <see cref="LayoutGraphPort.InternalLabel"/>
    ///     and is referenced by an edge <em>inside</em> its owning container's child scope (the inward
    ///     delegation edge is the structural signal that marks it as a boundary port). The hierarchical
    ///     engine lays the container and its children out in one combined recursive pass and routes the
    ///     external approach edge and every internal delegation edge through its orthogonal corridor
    ///     router onto one shared physical anchor on the container boundary, carrying both labels: the
    ///     external label reads outward (away from the container) and the internal label reads inward
    ///     (into the container's interior). This diagram also exercises internal <em>fan-out</em>: the
    ///     single port delegates to two distinct nested children, both connectors routed onto the one
    ///     shared anchor.
    /// </remarks>
    /// <returns>A compound graph whose container exposes one left-face boundary port with internal fan-out.</returns>
    public static LayoutGraph BoundaryPortsShowcaseHorizontal()
    {
        var graph = new LayoutGraph();

        var sensor = AddLabelled(graph, "sensor", "Sensor");

        // The container starts small; the hierarchical engine grows it to fit its nested children.
        var controller = graph.AddNode("controller", 10, 10);
        controller.Label = "Controller";

        // One boundary port carrying BOTH labels: the external label reads outward, the internal label
        // reads inward, at the same shared physical anchor on the container boundary.
        var command = controller.Ports.AddPort("command");
        command.ExternalLabel = "command";
        command.InternalLabel = "dispatch";

        var driver = AddLabelled(controller.Children, "driver", "Driver");
        var logger = AddLabelled(controller.Children, "logger", "Logger");

        // The external approach edge lives in the root scope, joining the sibling to the boundary port.
        Connect(graph, "sensor-command", sensor, command, null);

        // The internal delegation edges live inside the container's own child scope, relaying the
        // boundary port inward to two children (internal fan-out onto the one shared anchor).
        Connect(controller.Children, "command-driver", command, driver, null);
        Connect(controller.Children, "command-logger", command, logger, null);

        return graph;
    }

    /// <summary>
    ///     The companion to <see cref="BoundaryPortsShowcaseHorizontal"/>, using a downward
    ///     <see cref="CoreOptions.Direction"/> so the boundary port anchors on the container's top face
    ///     instead of its left face, and exercising external <em>fan-out</em>: two sibling nodes both
    ///     approach the single boundary port, which delegates inward to one nested child.
    /// </summary>
    /// <returns>A compound graph whose downward-flowing container exposes one top-face boundary port with external fan-out.</returns>
    public static LayoutGraph BoundaryPortsShowcaseVertical()
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        var monitor = AddLabelled(graph, "monitor", "Monitor");
        var operatorConsole = AddLabelled(graph, "operator", "Operator");

        var controller = graph.AddNode("controller", 10, 10);
        controller.Label = "Controller";

        var command = controller.Ports.AddPort("command");
        command.ExternalLabel = "command";
        command.InternalLabel = "dispatch";

        var driver = AddLabelled(controller.Children, "driver", "Driver");

        // Two external approach edges (external fan-out) both reach the one shared top-face anchor.
        Connect(graph, "monitor-command", monitor, command, null);
        Connect(graph, "operator-command", operatorConsole, command, null);

        // A single internal delegation edge reaches inward to the nested child.
        Connect(controller.Children, "command-driver", command, driver, null);

        return graph;
    }

    /// <summary>
    ///     A three-level boundary-port <em>delegation chain</em>: a sibling approaches an outer
    ///     container's boundary port, which delegates inward to a nested container's own boundary port,
    ///     which in turn delegates inward again to a leaf child at the innermost level — two boundary
    ///     crossing points stacked in one recursive descent.
    /// </summary>
    /// <remarks>
    ///     This exercises the recursive hierarchical engine at depth three. Each of the two boundary
    ///     ports (the outer <c>system</c> container's and the nested <c>subsystem</c> container's)
    ///     carries <em>both</em> a <see cref="LayoutGraphPort.ExternalLabel"/> reading outward and a
    ///     <see cref="LayoutGraphPort.InternalLabel"/> reading inward, resolving to one shared physical
    ///     anchor on its own container boundary. The single external approach and every delegation edge
    ///     — across both boundary crossings — are routed through the orthogonal corridor router in the
    ///     one combined pass, so the whole chain reads as an unbroken orthogonal path from the outermost
    ///     sibling down to the innermost leaf with no diagonal shortcut at either boundary.
    /// </remarks>
    /// <returns>A compound graph whose boundary port delegates through a nested container's own boundary port to a leaf.</returns>
    public static LayoutGraph BoundaryPortsShowcaseDeepChain()
    {
        var graph = new LayoutGraph();

        var source = AddLabelled(graph, "source", "Source");

        // Outer container; the engine grows it to fit its nested subsystem.
        var system = graph.AddNode("system", 10, 10);
        system.Label = "System";

        // The outer boundary port: external label reads outward, internal label reads inward, at one
        // shared anchor on the System boundary.
        var request = system.Ports.AddPort("request");
        request.ExternalLabel = "request";
        request.InternalLabel = "route";

        // Nested container inside System; it too exposes its own boundary port.
        var subsystem = system.Children.AddNode("subsystem", 10, 10);
        subsystem.Label = "Subsystem";

        // The inner boundary port: its external face is approached by System's own delegation edge, and
        // its internal face delegates inward again to the innermost leaf.
        var relay = subsystem.Ports.AddPort("relay");
        relay.ExternalLabel = "relay";
        relay.InternalLabel = "handle";

        var core = AddLabelled(subsystem.Children, "core", "Core");

        // The external approach edge lives in the root scope, joining the sibling to the outer port.
        Connect(graph, "source-request", source, request, null);

        // The first delegation edge lives inside System's own child scope, relaying the outer boundary
        // port inward to the nested container's boundary port (crossing point one).
        Connect(system.Children, "request-relay", request, relay, null);

        // The second delegation edge lives inside Subsystem's own child scope, relaying the inner
        // boundary port inward to the innermost leaf child (crossing point two).
        Connect(subsystem.Children, "relay-core", relay, core, null);

        return graph;
    }

    /// <summary>
    ///     A small, fully-connected directed pipeline laid out via the layered algorithm, kept as a
    ///     visual baseline for comparison against the disconnected-graph diagrams in the same section:
    ///     this graph has a single connected component, so it never engages the layered pipeline's
    ///     internal component-packing path.
    /// </summary>
    /// <returns>A flat, fully-connected graph of four labelled boxes.</returns>
    public static LayoutGraph LayeredRegressionBaseline()
    {
        var graph = new LayoutGraph();
        var start = AddLabelled(graph, "start", "Start");
        var middle = AddLabelled(graph, "middle", "Middle");
        var branch = AddLabelled(graph, "branch", "Branch");
        var end = AddLabelled(graph, "end", "End");

        Connect(graph, "start-middle", start, middle);
        Connect(graph, "middle-branch", middle, branch);
        Connect(graph, "middle-end", middle, end);

        return graph;
    }

    /// <summary>
    ///     One small connected cluster plus several unrelated singleton nodes, suited to the
    ///     <c>"auto"</c> meta-algorithm: the cluster routes through the layered algorithm while the
    ///     singletons are gathered into one shared bucket routed through the containment algorithm, then
    ///     both pieces are packed into a single combined canvas.
    /// </summary>
    /// <returns>A graph mixing one two-node cluster with three isolated singleton nodes.</returns>
    public static LayoutGraph AutoClusterPlusIsolated()
    {
        var graph = new LayoutGraph();
        var a = AddLabelled(graph, "a", "A");
        var b = AddLabelled(graph, "b", "B");
        Connect(graph, "a-b", a, b);

        AddLabelled(graph, "solo1", "Solo1");
        AddLabelled(graph, "solo2", "Solo2");
        AddLabelled(graph, "solo3", "Solo3");

        return graph;
    }

    /// <summary>
    ///     Three independent two-node clusters with no edges between them, used both as the
    ///     <c>"auto"</c> multi-component showcase (each cluster routed and packed independently) and,
    ///     directly, as the <c>"layered"</c> sibling showing the same disconnected graph handled by the
    ///     layered algorithm's own internal component packing.
    /// </summary>
    /// <returns>A graph of three disconnected two-node clusters.</returns>
    public static LayoutGraph MultipleDisconnectedClusters()
    {
        var graph = new LayoutGraph();
        var a1 = AddLabelled(graph, "a1", "A1");
        var a2 = AddLabelled(graph, "a2", "A2");
        Connect(graph, "a1-a2", a1, a2);

        var b1 = AddLabelled(graph, "b1", "B1");
        var b2 = AddLabelled(graph, "b2", "B2");
        Connect(graph, "b1-b2", b1, b2);

        var c1 = AddLabelled(graph, "c1", "C1");
        var c2 = AddLabelled(graph, "c2", "C2");
        Connect(graph, "c1-c2", c1, c2);

        return graph;
    }

    /// <summary>
    ///     A graph of entirely isolated nodes (no edges, no children), suited to the <c>"auto"</c>
    ///     meta-algorithm's all-singleton fast path: every node is gathered into the shared bucket and
    ///     routed through the containment algorithm, unchanged, with no splitting or packing needed.
    /// </summary>
    /// <returns>A flat graph of five isolated labelled boxes.</returns>
    public static LayoutGraph AutoAllIsolated()
    {
        var graph = new LayoutGraph();
        AddLabelled(graph, "n1", "N1");
        AddLabelled(graph, "n2", "N2");
        AddLabelled(graph, "n3", "N3");
        AddLabelled(graph, "n4", "N4");
        AddLabelled(graph, "n5", "N5");

        return graph;
    }

    /// <summary>
    ///     A nested container plus an unrelated isolated singleton, suited to the <c>"auto"</c>
    ///     meta-algorithm's routing rule that any component containing a container node is routed
    ///     through the hierarchical algorithm regardless of its size, while the unrelated singleton is
    ///     packed alongside it through the shared containment bucket.
    /// </summary>
    /// <returns>A graph mixing one two-level compound container with one isolated leaf node.</returns>
    public static LayoutGraph AutoNestedRoutesHierarchical()
    {
        var graph = new LayoutGraph();

        var group = graph.AddNode("group", 10, 10);
        group.Label = "Group";
        var inner1 = AddLabelled(group.Children, "inner1", "Inner1");
        var inner2 = AddLabelled(group.Children, "inner2", "Inner2");
        Connect(group.Children, "inner1-inner2", inner1, inner2);

        AddLabelled(graph, "solo", "Solo");

        return graph;
    }

    /// <summary>
    ///     A three-level-deep compound container whose intermediate and innermost scopes each mix a
    ///     connected pair with an unrelated singleton, none of them re-declaring
    ///     <see cref="CoreOptions.Algorithm"/>. Every nested scope inherits <c>"auto"</c> from the root
    ///     graph and must be independently re-classified there — proving <c>"auto"</c> is re-evaluated
    ///     at each level it cascades to (routing the connected pair through layered and the singleton
    ///     through containment at every scope), rather than being resolved once at the root and then
    ///     applied uniformly to every descendant scope.
    /// </summary>
    /// <returns>A graph with a two-level nested container plus an unrelated root-level singleton.</returns>
    public static LayoutGraph AutoDeepNestedMixedConnectivity()
    {
        var graph = new LayoutGraph();

        // Level 1: the outer container.
        var outer = graph.AddNode("outer", 10, 10);
        outer.Label = "Outer";

        // Level 2 (inside outer.Children): a connected pair, an unrelated singleton, and another
        // container going one level deeper still.
        var mid1 = AddLabelled(outer.Children, "mid1", "Mid1");
        var mid2 = AddLabelled(outer.Children, "mid2", "Mid2");
        Connect(outer.Children, "mid1-mid2", mid1, mid2);
        AddLabelled(outer.Children, "midSolo", "MidSolo");

        var deepContainer = outer.Children.AddNode("deepContainer", 10, 10);
        deepContainer.Label = "DeepContainer";

        // Level 3 (inside deepContainer.Children): again a connected pair plus an unrelated singleton.
        var d1 = AddLabelled(deepContainer.Children, "d1", "D1");
        var d2 = AddLabelled(deepContainer.Children, "d2", "D2");
        Connect(deepContainer.Children, "d1-d2", d1, d2);
        AddLabelled(deepContainer.Children, "dSolo", "DSolo");

        // An unrelated root-level singleton forces the genuine multi-group split path at the root too.
        AddLabelled(graph, "rootSolo", "RootSolo");

        return graph;
    }

    /// <summary>
    ///     Twelve small, wide sibling boxes with no edges, suited to the containment algorithm's
    ///     column-count-based content-width candidate: without it, the area-based width estimate alone
    ///     would under-size the packing budget for this shape and pack the boxes into a single narrow
    ///     column instead of a balanced multi-column block.
    /// </summary>
    /// <returns>A flat graph of twelve labelled boxes, each 160 by 40 logical pixels.</returns>
    public static LayoutGraph ContainmentManySmallWideBoxes()
    {
        var graph = new LayoutGraph();
        for (var i = 0; i < 12; i++)
        {
            var node = graph.AddNode($"box{i}", 160, 40);
            node.Label = $"Box{i}";
        }

        return graph;
    }

    /// <summary>
    ///     Coverage (and visual evidence) for the containment algorithm's edge-count-aware horizontal
    ///     gap widening: two equally-sized peer boxes, each carrying a nine-row compartment, joined by
    ///     eight distinct unmerged edges and packed <em>side by side</em> by the containment algorithm.
    ///     The boxes are deliberately tall and moderately wide so the algorithm's content-width budget
    ///     places them on the same row (rather than wrapping them into a vertical stack as the shorter
    ///     <see cref="ParallelEdgesIntoCompartmentBox"/> source/target pair does); the eight parallel
    ///     connectors must therefore fan through the single horizontal gap between them, which the packer
    ///     now widens in proportion to the edge count so the connectors get distinct routing lanes.
    /// </summary>
    /// <remarks>
    ///     Unlike <see cref="ParallelEdgesIntoCompartmentBoxSideBySide"/> — a hand-built
    ///     <see cref="LayoutTree"/> with explicitly-placed boxes that never invokes any layout algorithm
    ///     — this diagram is a pure <see cref="LayoutGraph"/> laid out by the real containment algorithm,
    ///     so it exercises the packer's same-row gap-widening path end to end. The box sizes were
    ///     empirically confirmed to pack side by side: two 100-wide boxes this tall satisfy the packer's
    ///     <c>2 * width + gap &lt;= contentWidth</c> same-row budget.
    /// </remarks>
    /// <returns>
    ///     A flat graph suited to the containment algorithm: two tall, compartment-bearing peer boxes
    ///     joined by eight distinct parallel edges, sized to pack side by side.
    /// </returns>
    public static LayoutGraph ContainmentParallelEdgesSideBySide()
    {
        var theme = Themes.Dark;
        var titledHeight = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: false);

        string[] rows = ["row1", "row2", "row3", "row4", "row5", "row6", "row7", "row8", "row9"];
        var boxHeight = titledHeight + TitledCompartmentHeight(theme, new LayoutCompartment("rows", rows));

        var graph = new LayoutGraph();
        graph.Set(CoreOptions.MergeParallelEdges, false);

        var left = graph.AddNode("left", 100, boxHeight);
        left.Label = "Left";
        left.Compartments = [new LayoutCompartment("rows", rows)];

        var right = graph.AddNode("right", 100, boxHeight);
        right.Label = "Right";
        right.Compartments = [new LayoutCompartment("rows", rows)];

        for (var i = 0; i < 8; i++)
        {
            var edge = graph.AddEdge($"left-right-{i}", left, right);
            edge.TargetEnd = EndMarkerStyle.FilledDiamond;
        }

        return graph;
    }

    /// <summary>
    ///     Coverage (and visual evidence) for the hierarchical algorithm's edge-count-aware
    ///     cross-container gap widening: two peer container boxes, each holding one compartment-bearing
    ///     child, placed side by side by the default leaf algorithm and joined by eight distinct
    ///     unmerged cross-container edges running child-to-child. The eight connectors must fan through
    ///     the single gap between the two containers; the hierarchical engine now widens that gap in
    ///     proportion to the cross-container edge count (a spacing the leaf algorithm alone cannot
    ///     reserve, because those edges never appear in the per-scope sized view it lays out) so the
    ///     connectors get distinct routing lanes.
    /// </summary>
    /// <remarks>
    ///     The children are tall and narrow so the leaf algorithm's disconnected-component packing
    ///     places the two containers on the same row (side by side) rather than stacking them; the box
    ///     sizes were empirically confirmed to produce a side-by-side arrangement whose gap widens from
    ///     the un-widened baseline once the edge count exceeds one. This graph uses no boundary ports, so
    ///     it exercises the no-boundary-port placement path the widening pass operates on.
    /// </remarks>
    /// <returns>
    ///     A two-level compound graph: two peer containers, each with one compartment-bearing child,
    ///     joined by eight parallel cross-container edges, sized to lay out side by side.
    /// </returns>
    public static LayoutGraph HierarchicalParallelEdgesSideBySide()
    {
        var theme = Themes.Dark;
        var titledHeight = BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword: false);

        string[] rows = ["row1", "row2", "row3", "row4", "row5", "row6", "row7", "row8", "row9"];
        var childHeight = titledHeight + TitledCompartmentHeight(theme, new LayoutCompartment("rows", rows));

        var graph = new LayoutGraph();
        graph.Set(CoreOptions.MergeParallelEdges, false);

        var left = graph.AddNode("left", 10, 10);
        left.Label = "Left";
        var leftInner = left.Children.AddNode("leftInner", 90, childHeight);
        leftInner.Label = "LeftInner";
        leftInner.Compartments = [new LayoutCompartment("rows", rows)];

        var right = graph.AddNode("right", 10, 10);
        right.Label = "Right";
        var rightInner = right.Children.AddNode("rightInner", 90, childHeight);
        rightInner.Label = "RightInner";
        rightInner.Compartments = [new LayoutCompartment("rows", rows)];

        for (var i = 0; i < 8; i++)
        {
            var edge = graph.AddEdge($"leftInner-rightInner-{i}", leftInner, rightInner);
            edge.TargetEnd = EndMarkerStyle.FilledDiamond;
        }

        return graph;
    }

    /// <summary>Computes the height of a titled compartment (a title row plus one row per line, plus the
    ///     leading title-area gap and the trailing bottom gap), matching the renderer's own layout
    ///     formula so the compartment content never overflows the box.
    /// </summary>
    private static double TitledCompartmentHeight(Theme theme, LayoutCompartment compartment) =>
        theme.LabelPadding + theme.FontSizeBody + theme.LabelPadding // title row
        + (compartment.Rows.Count * (theme.LabelPadding + theme.FontSizeBody)) // data rows
        + theme.LabelPadding; // bottom gap

    /// <summary>
    ///     Computes the height of an untitled compartment (one row per line, no title row), matching the
    ///     renderer's own layout formula so the compartment content never overflows the box.
    /// </summary>
    private static double UntitledCompartmentHeight(Theme theme, LayoutCompartment compartment) =>
        (compartment.Rows.Count * (theme.LabelPadding + theme.FontSizeBody)) // data rows
        + theme.LabelPadding; // bottom gap

    /// <summary>Adds a labelled leaf node of the standard showcase size to the given graph.</summary>
    private static LayoutGraphNode AddLabelled(LayoutGraph graph, string id, string label)
    {
        var node = graph.AddNode(id, 120, 50);
        node.Label = label;
        return node;
    }

    /// <summary>Adds a directed edge drawn with a filled arrowhead at its target end.</summary>
    private static void Connect(LayoutGraph graph, string id, LayoutGraphNode source, LayoutGraphNode target)
    {
        var edge = graph.AddEdge(id, source, target);
        edge.TargetEnd = EndMarkerStyle.FilledArrow;
    }

    /// <summary>
    ///     Adds a directed edge drawn with a filled arrowhead at its target end, between any two
    ///     <see cref="ILayoutConnectable"/> endpoints (a node or one of its named ports), with an
    ///     optional connector label.
    /// </summary>
    private static void Connect(
        LayoutGraph graph,
        string id,
        ILayoutConnectable source,
        ILayoutConnectable target,
        string? label)
    {
        var edge = graph.AddEdge(id, source, target);
        edge.TargetEnd = EndMarkerStyle.FilledArrow;
        edge.Label = label;
    }
}
