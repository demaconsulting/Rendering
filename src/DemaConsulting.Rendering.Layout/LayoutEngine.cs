// <copyright file="LayoutEngine.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout;

/// <summary>
/// The batteries-included layout facade: a one-call entry point that lays out a <see cref="LayoutGraph"/>
/// with the algorithm the graph (or options) declares, resolving it against the bundled algorithms. It
/// turns "lay out my graph with whatever algorithm it declares" into a single line, correctly handling
/// both flat and nested (compound) graphs without the caller having to assemble a registry or choose an
/// engine.
/// </summary>
/// <remarks>
///     <para>
///     <strong>Algorithm resolution.</strong> The algorithm identifier is taken from an explicit
///     <see cref="CoreOptions.Algorithm"/> set directly on the graph itself — for example
///     <c>graph.Set(CoreOptions.Algorithm, "layered")</c> — falling back to
///     <see cref="DefaultAlgorithmId"/> only when the graph carries no explicit value. There is a single
///     place to configure layout: the graph itself, since <see cref="LayoutGraph"/> is an
///     <see cref="IPropertyHolder"/>. This deliberately avoids a second, free-standing options object at
///     this entry point that could disagree with the graph's own settings; callers configure every
///     <see cref="CoreOptions"/> property (algorithm, direction, edge routing, spacing, and so on) the
///     same way, by calling <see cref="IPropertyHolder.Set{TValue}(LayoutProperty{TValue}, TValue)"/> on
///     the graph (or, for a nested container's own scope, on its <see cref="LayoutGraphNode.Children"/>
///     graph). The resolved algorithm identifier is looked up in the registry and applied.
///     </para>
///     <para>
///     <strong>Why the default is <c>hierarchical</c>, not <c>layered</c>.</strong> When no algorithm is
///     declared, the facade defaults to <see cref="HierarchicalLayoutAlgorithm"/> (<c>hierarchical</c>)
///     rather than the layered algorithm so the single entry point correctly lays out
///     <em>both</em> flat and nested graphs. This is safe precisely because the hierarchical engine
///     guarantees flat-graph equivalence: for a graph with no container nodes it returns output
///     byte-for-byte identical to the selected leaf algorithm (default <c>layered</c>) applied directly.
///     A flat graph therefore lays out exactly as the layered algorithm would, while a nested graph is
///     composed correctly — with no decision required from the caller. Note that
///     <see cref="CoreOptions.Algorithm"/> has its own property default of <c>layered</c>; the facade
///     intentionally consults an <em>explicit</em> graph setting only (via
///     <see cref="IPropertyHolder.TryGet{TValue}(LayoutProperty{TValue}, out TValue)"/>) so that an
///     unset graph falls through to <see cref="DefaultAlgorithmId"/> instead of the property default.
///     </para>
///     <para>
///     The single-argument overload resolves against a shared default registry of the bundled algorithms
///     (see <see cref="LayoutAlgorithms.CreateDefaultRegistry"/>). Because the bundled algorithms are
///     stateless, that shared registry is safe to read concurrently. Callers that want to add or replace
///     algorithms — for example to register a custom <see cref="ILayoutAlgorithm"/> — pass their own
///     registry to the two-argument overload.
///     </para>
/// </remarks>
/// <example>
///     <code>
///     using System.IO;
///     using DemaConsulting.Rendering;
///     using DemaConsulting.Rendering.Abstractions;
///     using DemaConsulting.Rendering.Layout;
///     using DemaConsulting.Rendering.Svg;
///
///     // 1. Describe the diagram as a graph of sized boxes and directed edges.
///     var graph = new LayoutGraph();
///     var a = graph.AddNode("a", 80, 40);
///     var b = graph.AddNode("b", 80, 40);
///     var c = graph.AddNode("c", 80, 40);
///     graph.AddEdge("a-b", a, b);
///     graph.AddEdge("b-c", b, c);
///
///     // 2. Lay it out with whatever algorithm the graph declares (default: hierarchical,
///     //    which is byte-identical to layered for this flat graph). Configure the graph itself
///     //    (e.g. graph.Set(CoreOptions.Algorithm, "layered")) to change that.
///     var tree = LayoutEngine.Layout(graph);
///
///     // 3. Render the placed tree to SVG.
///     using var output = File.Create("diagram.svg");
///     new SvgRenderer().Render(tree, new RenderOptions(Themes.Light), output);
///     </code>
/// </example>
public static class LayoutEngine
{
    /// <summary>
    /// Identifier of the algorithm used when the graph declares none: <c>hierarchical</c>. It lays out
    /// flat and nested graphs uniformly, matching the layered algorithm byte-for-byte on flat graphs
    /// while composing nested graphs correctly.
    /// </summary>
    public const string DefaultAlgorithmId = HierarchicalLayoutAlgorithm.AlgorithmId;

    /// <summary>
    /// Shared registry of the bundled algorithms used by the single-argument overload. The bundled
    /// algorithms are stateless, so a single shared instance is safe to read (resolve) concurrently.
    /// </summary>
    private static readonly LayoutAlgorithmRegistry DefaultRegistry =
        LayoutAlgorithms.CreateDefaultRegistry();

    /// <summary>
    /// Lays out <paramref name="graph"/> with the algorithm it declares (see the resolution rules on
    /// <see cref="LayoutEngine"/>), resolving it from the bundled algorithms.
    /// </summary>
    /// <param name="graph">
    /// The unplaced input graph, flat or nested. Configure it directly (via
    /// <see cref="IPropertyHolder.Set{TValue}(LayoutProperty{TValue}, TValue)"/>) with any
    /// <see cref="CoreOptions"/> the layout should honor, including
    /// <see cref="CoreOptions.Algorithm"/> to select the algorithm explicitly; otherwise
    /// <see cref="DefaultAlgorithmId"/> applies.
    /// </param>
    /// <returns>A fully-placed <see cref="LayoutTree"/> with absolute coordinates and routed edges.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="graph"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when the declared algorithm identifier is not one of the bundled algorithms.
    /// </exception>
    public static LayoutTree Layout(LayoutGraph graph) =>
        Layout(graph, DefaultRegistry);

    /// <summary>
    /// Lays out <paramref name="graph"/> with the algorithm it declares (see the resolution rules on
    /// <see cref="LayoutEngine"/>), resolving it from a caller-supplied <paramref name="registry"/>.
    /// </summary>
    /// <param name="graph">
    /// The unplaced input graph, flat or nested. Configure it directly (via
    /// <see cref="IPropertyHolder.Set{TValue}(LayoutProperty{TValue}, TValue)"/>) with any
    /// <see cref="CoreOptions"/> the layout should honor, including
    /// <see cref="CoreOptions.Algorithm"/> to select the algorithm explicitly; otherwise
    /// <see cref="DefaultAlgorithmId"/> applies.
    /// </param>
    /// <param name="registry">
    /// Provider of the algorithms to resolve against. Use
    /// <see cref="LayoutAlgorithms.CreateDefaultRegistry"/> as a starting point and register any custom
    /// algorithms; the resolved identifier must be present in it.
    /// </param>
    /// <returns>A fully-placed <see cref="LayoutTree"/> with absolute coordinates and routed edges.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="graph"/> or <paramref name="registry"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when the declared algorithm identifier is not registered in <paramref name="registry"/>.
    /// </exception>
    public static LayoutTree Layout(LayoutGraph graph, LayoutAlgorithmRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(registry);

        var algorithmId = ResolveAlgorithm(graph);

        // The graph carries every explicit setting a leaf algorithm needs; seed the cascade with an
        // empty LayoutOptions rather than accepting a caller-supplied one, so the graph itself is the
        // single place options are configured (see the "Algorithm resolution" remarks above).
        return registry.Resolve(algorithmId).Apply(graph, new LayoutOptions());
    }

    /// <summary>
    /// Resolves the algorithm identifier from an explicit <see cref="CoreOptions.Algorithm"/> set on the
    /// graph, falling back to <see cref="DefaultAlgorithmId"/> when the graph declares none.
    /// </summary>
    /// <param name="graph">The graph whose explicit algorithm declaration is consulted.</param>
    /// <returns>The algorithm identifier to resolve and apply.</returns>
    private static string ResolveAlgorithm(LayoutGraph graph) =>
        graph.TryGet(CoreOptions.Algorithm, out var fromGraph) ? fromGraph : DefaultAlgorithmId;
}
