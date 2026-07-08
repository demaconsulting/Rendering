// <copyright file="GalleryDiagrams.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;

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
