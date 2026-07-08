// <copyright file="CoreOptions.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// Well-known <see cref="LayoutProperty{T}"/> keys understood by the core layout pipeline. This is
/// the ELK-style option catalog: the set is expected to grow over time, and adding a key is a purely
/// additive change. Keys marked <em>advisory</em> are accepted today but not yet honored by the
/// bundled algorithms; they default harmlessly until an algorithm implements them.
/// </summary>
/// <remarks>
/// <para>
/// Set a well-known option by passing the key together with a value of its type to
/// <see cref="IPropertyHolder.Set{TValue}(LayoutProperty{TValue}, TValue)"/> on any scope — a graph, a
/// node, an edge, or a free-standing <see cref="LayoutOptions"/>.
/// </para>
/// <para>
/// <strong>Cascading.</strong> Every property below is resolved <em>per scope</em> by nearest-ancestor
/// override: a scope that sets its own explicit value wins, both for itself and for every descendant
/// scope that sets nothing; a scope with no explicit value inherits the nearest ancestor scope's
/// resolved value; and only when no scope in the chain declares one does the property's own documented
/// default apply. Consumers such as the bundled hierarchical layout engine build this per-scope
/// resolution with <see cref="PropertyHolder.OverlayOnto"/>, the generic primitive that merges a
/// parent scope's already-resolved snapshot with a scope's own overrides.
/// </para>
/// </remarks>
/// <example>
/// Setting well-known options at two different scopes:
/// <code>
/// // Graph scope: route every edge in this graph orthogonally.
/// var graph = new LayoutGraph();
/// graph.Set(CoreOptions.EdgeRouting, EdgeRouting.Orthogonal);
///
/// // Options scope: select the layered algorithm and flow its layers downward.
/// // Prefer the algorithm-id constant over a hardcoded string.
/// var options = LayoutOptions.ForAlgorithm(LayeredLayoutAlgorithm.AlgorithmId);
/// options.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
/// </code>
/// Three-level cascading of a single property (here <see cref="Direction"/>), showing both
/// nearest-ancestor inheritance and a deeper override taking precedence:
/// <code>
/// var root = new LayoutGraph();
/// var mid = root.AddNode("mid", 10, 10);
/// mid.Children.Set(CoreOptions.Direction, LayoutFlowDirection.Down); // mid-tree override
/// var leaf = mid.Children.AddNode("leaf", 10, 10);
/// leaf.Children.AddNode("a", 80, 40);
/// leaf.Children.AddNode("b", 80, 40);        // leaf's own Children set nothing: inherits Down from mid
///
/// var deeper = mid.Children.AddNode("deeper", 10, 10);
/// deeper.Children.Set(CoreOptions.Direction, LayoutFlowDirection.Right); // deeper override wins locally
/// </code>
/// </example>
public static class CoreOptions
{
    /// <summary>
    /// Identifier of the layout algorithm to apply. Resolved against the registered algorithms by a
    /// caller or layout service. Prefer a bundled algorithm-id constant —
    /// <c>LayeredLayoutAlgorithm.AlgorithmId</c> (<c>"layered"</c>),
    /// <c>ContainmentLayoutAlgorithm.AlgorithmId</c> (<c>"containment"</c>), or
    /// <c>HierarchicalLayoutAlgorithm.AlgorithmId</c> (<c>"hierarchical"</c>) — over a hardcoded string
    /// so callers never have to spell the identifier by hand. Cascades per scope: a container node may
    /// override it for itself and its descendants (by convention, set directly on the container node),
    /// falling back to the nearest ancestor's resolved algorithm, and ultimately to this property's own
    /// default when nothing in the chain declares one.
    /// </summary>
    public static readonly LayoutProperty<string> Algorithm =
        new("rendering.algorithm", "layered");

    /// <summary>
    /// How a hierarchical layout engine treats a container node's nested children, mirroring ELK's
    /// <c>elk.hierarchyHandling</c>. Carried on any scope (graph, node, or a free-standing
    /// <see cref="LayoutOptions"/>) so hierarchy handling can be selected per scope just like the
    /// algorithm, cascading by nearest-ancestor override the same way every other property in this
    /// catalog does. The value defaults to <see cref="Rendering.HierarchyHandling.SeparateChildren"/>,
    /// which is the only mode honored by the bundled hierarchical engine today: each container is laid
    /// out in its own coordinate space and sized to fit its children. The vocabulary grows additively as
    /// new hierarchy-handling modes are implemented.
    /// </summary>
    public static readonly LayoutProperty<HierarchyHandling> HierarchyHandling =
        new("rendering.hierarchyhandling", Rendering.HierarchyHandling.SeparateChildren);

    /// <summary>
    /// Primary flow direction for layered algorithms: the direction successive layers progress in.
    /// Honored by the bundled <c>layered</c> algorithm — <see cref="LayoutFlowDirection.Right"/> and
    /// <see cref="LayoutFlowDirection.Left"/> flow left-to-right and right-to-left, while
    /// <see cref="LayoutFlowDirection.Down"/> and <see cref="LayoutFlowDirection.Up"/> flow
    /// top-to-bottom and bottom-to-top (swapping each node's width and height before layering so layer
    /// spacing is driven by node height). Cascades per scope: a container's nested content inherits the
    /// nearest ancestor scope's resolved direction unless the container's own scope declares an
    /// explicit override (by convention, set on the container node's
    /// <see cref="LayoutGraphNode.Children"/> graph), falling back to
    /// <see cref="LayoutFlowDirection.Right"/> only when no scope in the chain declares one.
    /// </summary>
    public static readonly LayoutProperty<LayoutFlowDirection> Direction =
        new("rendering.direction", LayoutFlowDirection.Right);

    /// <summary>
    /// Routing style applied to connectors, mirroring ELK's <c>elk.edgeRouting</c>. Carried on any
    /// scope (graph, node, edge, or a free-standing <see cref="LayoutOptions"/>) so routing can be
    /// selected per scope just like the algorithm. The bundled routing orchestration reads this key
    /// to choose the router, cascading per scope by nearest-ancestor override the same way every other
    /// property in this catalog does; the value defaults to <see cref="EdgeRouting.Orthogonal"/>, the
    /// only shipped style, and the vocabulary grows additively as new routers are implemented.
    /// </summary>
    public static readonly LayoutProperty<EdgeRouting> EdgeRouting =
        new("rendering.edgerouting", Rendering.EdgeRouting.Orthogonal);

    /// <summary>
    /// Minimum spacing, in logical pixels, between adjacent nodes within a layer, mirroring ELK's
    /// <c>spacing.nodeNode</c>. As with ELK's option, this is a floor enforced by the layout engine's
    /// compaction step, not an exact/desired gap — nodes may end up further apart when other placement
    /// constraints require it. Honored by the bundled <c>layered</c> algorithm. The default,
    /// <c>30.0</c>, matches both ELK's own default and the value the engine used internally before this
    /// property was honored, so a graph that never sets this property renders identically to before.
    /// Cascades per scope by nearest-ancestor override, consistent with every other property in this
    /// catalog.
    /// </summary>
    public static readonly LayoutProperty<double> NodeSpacing =
        new("rendering.spacing.node", 30.0);

    /// <summary>
    /// Advisory: minimum spacing, in logical pixels, between adjacent layers, mirroring ELK's
    /// <c>spacing.nodeNodeBetweenLayers</c>. As with ELK's option, this is a floor, not an exact/desired
    /// gap — the bundled layered algorithm's connector routing may already require a larger corridor
    /// width than this value, in which case the routing-derived minimum takes precedence. Accepted but
    /// not yet honored by the bundled layered algorithm, which uses a fixed engine constant instead.
    /// Would cascade per scope by nearest-ancestor override, consistent with every other property in
    /// this catalog, once an algorithm honors it.
    /// </summary>
    public static readonly LayoutProperty<double> LayerSpacing =
        new("rendering.spacing.layer", 40.0);

    /// <summary>
    /// Whether parallel edges (two or more input edges sharing the same source and target) are
    /// merged into a single rendered connector. ELK has no directly equivalent option; this is a
    /// bespoke addition needed because the bundled layout engines historically collapsed parallel
    /// edges into one routed line as a side effect of their internal cycle-breaking deduplication,
    /// without exposing any way to opt out. The default, <see langword="true"/>, preserves that
    /// existing behavior exactly: only the first of any group of parallel edges between the same
    /// two endpoints is emitted, with its own label. Set to <see langword="false"/> to instead
    /// retain every parallel edge instance as its own independently-routed connector with its own
    /// label — self-loops (an edge whose source and target are the same node) are always dropped
    /// regardless of this setting, since no rendering exists for them. Honored by the bundled
    /// <c>layered</c> algorithm's cycle-breaking pipeline stage (which retains or discards duplicate
    /// directed pairs accordingly) and by its connector-route lookup and final line-emission step.
    /// Cascades per scope by nearest-ancestor override,
    /// consistent with every other property in this catalog.
    /// </summary>
    public static readonly LayoutProperty<bool> MergeParallelEdges =
        new("rendering.mergeparalleledges", true);

    /// <summary>
    /// Assumed font size, in logical pixels, used by the bundled <c>layered</c> algorithm when it
    /// measures port label text to compute a node's <see cref="LayoutBox.ContentInsetLeft"/>,
    /// <see cref="LayoutBox.ContentInsetRight"/>, <see cref="LayoutBox.ContentInsetTop"/>, and
    /// <see cref="LayoutBox.ContentInsetBottom"/> reserved margins during layout — before any
    /// renderer or theme is involved. The default, <c>12.0</c>, matches
    /// <c>Theme.FontSizeBody</c>'s own built-in default, so a caller that never customizes either
    /// value gets layout-time measurements consistent with what the default theme actually draws.
    /// Cascades per scope by nearest-ancestor override, consistent with every other property in
    /// this catalog.
    /// </summary>
    public static readonly LayoutProperty<double> AssumedFontSize =
        new("rendering.assumedfontsize", 12.0);

    /// <summary>
    /// Optional <see cref="ITextMeasurer"/> the bundled <c>layered</c> algorithm uses to measure
    /// port label text widths when computing <see cref="LayoutBox.ContentInsetLeft"/> and
    /// <see cref="LayoutBox.ContentInsetRight"/>. <see langword="null"/> (the default) selects the
    /// engine's own dependency-free heuristic estimator, which approximates advance width from
    /// character count alone; supply a real font-metric-backed implementation (for example a
    /// SkiaSharp-backed measurer) here to get pixel-accurate reserved margins that match what a
    /// renderer will actually draw. Cascades per scope by nearest-ancestor override, consistent
    /// with every other property in this catalog.
    /// </summary>
    public static readonly LayoutProperty<ITextMeasurer?> TextMeasurer =
        new("rendering.textmeasurer", null);
}
