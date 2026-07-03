// <copyright file="GalleryDiagrams.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

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
}
