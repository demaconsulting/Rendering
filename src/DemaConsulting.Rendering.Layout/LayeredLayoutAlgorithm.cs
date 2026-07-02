// <copyright file="LayeredLayoutAlgorithm.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout.Engine;

namespace DemaConsulting.Rendering.Layout;

/// <summary>
/// The bundled ELK-style layered layout algorithm: arranges the input graph into Sugiyama layers and
/// routes edges orthogonally, producing a placed <see cref="LayoutTree"/> of boxes and connectors.
/// This is the reference <see cref="ILayoutAlgorithm"/> implementation; it wraps the reusable layered
/// pipeline under <c>Engine/Layered/</c>.
/// </summary>
public sealed class LayeredLayoutAlgorithm : ILayoutAlgorithm
{
    /// <summary>
    /// The stable algorithm identifier <c>"layered"</c> under which this algorithm is selected and
    /// registered. Pass it to <see cref="LayoutOptions.ForAlgorithm(string)"/> or
    /// <see cref="CoreOptions.Algorithm"/> instead of hardcoding the literal string.
    /// </summary>
    public const string AlgorithmId = "layered";

    /// <inheritdoc/>
    public string Id => AlgorithmId;

    /// <inheritdoc/>
    public LayoutTree Apply(LayoutGraph graph, LayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(options);

        var graphNodes = graph.Nodes;
        var count = graphNodes.Count;

        // Map each input node to a positional index the layered engine works in terms of.
        var indexOf = new Dictionary<LayoutGraphNode, int>(count);
        var engineNodes = new LayerNode[count];
        for (var i = 0; i < count; i++)
        {
            var node = graphNodes[i];
            indexOf[node] = i;
            engineNodes[i] = new LayerNode(node.Width, node.Height);
        }

        // Map edges to index pairs, dropping any that reference nodes outside this graph.
        var engineEdges = new List<LayerEdge>(graph.Edges.Count);
        foreach (var edge in graph.Edges)
        {
            if (indexOf.TryGetValue(edge.Source, out var s) &&
                indexOf.TryGetValue(edge.Target, out var t))
            {
                engineEdges.Add(new LayerEdge(s, t));
            }
        }

        var result = InterconnectionLayoutEngine.Place(engineNodes, engineEdges);

        var nodes = new List<LayoutNode>(count + graph.Edges.Count);

        // Emit one placed box per input node, preserving input order.
        for (var i = 0; i < count; i++)
        {
            var rect = result.Rects[i];
            nodes.Add(new LayoutBox(
                rect.X,
                rect.Y,
                rect.Width,
                rect.Height,
                graphNodes[i].Label,
                Depth: 0,
                BoxShape.Rectangle,
                Compartments: [],
                Children: []));
        }

        // Build a (source, target) -> polyline lookup from the acyclic edge set the engine routed.
        var routes = new Dictionary<(int Source, int Target), IReadOnlyList<Point2D>>();
        for (var k = 0; k < result.AcyclicEdges.Count; k++)
        {
            var acyclic = result.AcyclicEdges[k];
            routes[(acyclic.Source, acyclic.Target)] = result.ConnectorWaypoints[k];
        }

        // Emit one connector per input edge, recovering its route (reversing a reversed back edge).
        foreach (var edge in graph.Edges)
        {
            if (!indexOf.TryGetValue(edge.Source, out var s) ||
                !indexOf.TryGetValue(edge.Target, out var t))
            {
                continue;
            }

            var waypoints = ResolveRoute(routes, s, t, result.Rects);
            nodes.Add(new LayoutLine(
                waypoints,
                EndMarkerStyle.None,
                edge.TargetEnd,
                edge.LineStyle,
                edge.Label));
        }

        return new LayoutTree(result.TotalWidth, result.TotalHeight, nodes);
    }

    private static IReadOnlyList<Point2D> ResolveRoute(
        Dictionary<(int Source, int Target), IReadOnlyList<Point2D>> routes,
        int source,
        int target,
        IReadOnlyList<Rect> rects)
    {
        if (routes.TryGetValue((source, target), out var forward))
        {
            return forward;
        }

        // The cycle-breaking stage may have reversed this edge; recover it by reversing the polyline.
        if (routes.TryGetValue((target, source), out var reversed))
        {
            var flipped = new List<Point2D>(reversed);
            flipped.Reverse();
            return flipped;
        }

        // Self-loops and duplicate edges are dropped by the engine; fall back to a straight segment
        // between the two node centres so the connector is still drawn.
        return [Centre(rects[source]), Centre(rects[target])];
    }

    private static Point2D Centre(Rect rect) =>
        new(rect.X + (rect.Width / 2.0), rect.Y + (rect.Height / 2.0));
}
