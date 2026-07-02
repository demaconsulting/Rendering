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
///     <strong>Algorithm resolution.</strong> The algorithm identifier is taken, in order of
///     precedence, from an explicit <see cref="CoreOptions.Algorithm"/> set on the
///     <em>graph</em> itself, then from an explicit <see cref="CoreOptions.Algorithm"/> set on the
///     supplied <em>options</em>, and finally — when neither carries an explicit value — from
///     <see cref="DefaultAlgorithmId"/>. The graph takes precedence because, in the ELK-style model,
///     layout options are naturally attached to the graph being laid out; the free-standing
///     <see cref="LayoutOptions"/> acts as a fallback. The resolved identifier is looked up in the
///     registry and applied.
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
///     intentionally consults <em>explicit</em> settings only (via
///     <see cref="IPropertyHolder.TryGet{TValue}(LayoutProperty{TValue}, out TValue)"/>) so that an
///     unset graph and options fall through to <see cref="DefaultAlgorithmId"/> instead of the
///     property default.
///     </para>
///     <para>
///     The parameterless overload resolves against a shared default registry of the bundled algorithms
///     (see <see cref="LayoutAlgorithms.CreateDefaultRegistry"/>). Because the bundled algorithms are
///     stateless, that shared registry is safe to read concurrently. Callers that want to add or replace
///     algorithms — for example to register a custom <see cref="ILayoutAlgorithm"/> — pass their own
///     registry to the three-argument overload.
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
///     //    which is byte-identical to layered for this flat graph).
///     var tree = LayoutEngine.Layout(graph, new LayoutOptions());
///
///     // 3. Render the placed tree to SVG.
///     using var output = File.Create("diagram.svg");
///     new SvgRenderer().Render(tree, new RenderOptions(Themes.Light), output);
///     </code>
/// </example>
public static class LayoutEngine
{
    /// <summary>
    /// Identifier of the algorithm used when neither the graph nor the options declares one:
    /// <c>hierarchical</c>. It lays out flat and nested graphs uniformly, matching the layered algorithm
    /// byte-for-byte on flat graphs while composing nested graphs correctly.
    /// </summary>
    public const string DefaultAlgorithmId = HierarchicalLayoutAlgorithm.AlgorithmId;

    /// <summary>
    /// Shared registry of the bundled algorithms used by the parameterless overload. The bundled
    /// algorithms are stateless, so a single shared instance is safe to read (resolve) concurrently.
    /// </summary>
    private static readonly LayoutAlgorithmRegistry DefaultRegistry =
        LayoutAlgorithms.CreateDefaultRegistry();

    /// <summary>
    /// Lays out <paramref name="graph"/> with the algorithm it declares (see the resolution rules on
    /// <see cref="LayoutEngine"/>), resolving it from the bundled algorithms.
    /// </summary>
    /// <param name="graph">The unplaced input graph, flat or nested.</param>
    /// <param name="options">
    /// Layout options carried into the resolved algorithm. May declare
    /// <see cref="CoreOptions.Algorithm"/> to select the algorithm explicitly; otherwise the graph's
    /// declaration, then <see cref="DefaultAlgorithmId"/>, applies.
    /// </param>
    /// <returns>A fully-placed <see cref="LayoutTree"/> with absolute coordinates and routed edges.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="graph"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when the declared algorithm identifier is not one of the bundled algorithms.
    /// </exception>
    public static LayoutTree Layout(LayoutGraph graph, LayoutOptions options) =>
        Layout(graph, options, DefaultRegistry);

    /// <summary>
    /// Lays out <paramref name="graph"/> with the algorithm it declares (see the resolution rules on
    /// <see cref="LayoutEngine"/>), resolving it from a caller-supplied <paramref name="registry"/>.
    /// </summary>
    /// <param name="graph">The unplaced input graph, flat or nested.</param>
    /// <param name="options">
    /// Layout options carried into the resolved algorithm. May declare
    /// <see cref="CoreOptions.Algorithm"/> to select the algorithm explicitly; otherwise the graph's
    /// declaration, then <see cref="DefaultAlgorithmId"/>, applies.
    /// </param>
    /// <param name="registry">
    /// Provider of the algorithms to resolve against. Use
    /// <see cref="LayoutAlgorithms.CreateDefaultRegistry"/> as a starting point and register any custom
    /// algorithms; the resolved identifier must be present in it.
    /// </param>
    /// <returns>A fully-placed <see cref="LayoutTree"/> with absolute coordinates and routed edges.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="graph"/>, <paramref name="options"/>, or <paramref name="registry"/>
    /// is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when the declared algorithm identifier is not registered in <paramref name="registry"/>.
    /// </exception>
    public static LayoutTree Layout(LayoutGraph graph, LayoutOptions options, LayoutAlgorithmRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(registry);

        var algorithmId = ResolveAlgorithm(graph, options);
        return registry.Resolve(algorithmId).Apply(graph, options);
    }

    /// <summary>
    /// Resolves the algorithm identifier from an explicit <see cref="CoreOptions.Algorithm"/> on the
    /// graph, then on the options, falling back to <see cref="DefaultAlgorithmId"/> when neither is set.
    /// </summary>
    /// <param name="graph">The graph whose explicit algorithm declaration takes precedence.</param>
    /// <param name="options">The options consulted when the graph declares no algorithm.</param>
    /// <returns>The algorithm identifier to resolve and apply.</returns>
    private static string ResolveAlgorithm(LayoutGraph graph, LayoutOptions options)
    {
        if (graph.TryGet(CoreOptions.Algorithm, out var fromGraph))
        {
            return fromGraph;
        }

        return options.TryGet(CoreOptions.Algorithm, out var fromOptions)
            ? fromOptions
            : DefaultAlgorithmId;
    }
}
