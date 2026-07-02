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
/// Set a well-known option by passing the key together with a value of its type to
/// <see cref="IPropertyHolder.Set{TValue}(LayoutProperty{TValue}, TValue)"/> on any scope — a graph, a
/// node, an edge, or a free-standing <see cref="LayoutOptions"/>.
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
/// </example>
public static class CoreOptions
{
    /// <summary>
    /// Identifier of the layout algorithm to apply. Resolved against the registered algorithms by a
    /// caller or layout service. Prefer a bundled algorithm-id constant —
    /// <c>LayeredLayoutAlgorithm.AlgorithmId</c> (<c>"layered"</c>),
    /// <c>ContainmentLayoutAlgorithm.AlgorithmId</c> (<c>"containment"</c>), or
    /// <c>HierarchicalLayoutAlgorithm.AlgorithmId</c> (<c>"hierarchical"</c>) — over a hardcoded string
    /// so callers never have to spell the identifier by hand.
    /// </summary>
    public static readonly LayoutProperty<string> Algorithm =
        new("rendering.algorithm", "layered");

    /// <summary>
    /// How a hierarchical layout engine treats a container node's nested children, mirroring ELK's
    /// <c>elk.hierarchyHandling</c>. Carried on any scope (graph, node, or a free-standing
    /// <see cref="LayoutOptions"/>) so hierarchy handling can be selected per scope just like the
    /// algorithm. The value defaults to <see cref="Rendering.HierarchyHandling.SeparateChildren"/>,
    /// which is the only mode honored by the bundled hierarchical engine today: each container is laid
    /// out in its own coordinate space and sized to fit its children. The vocabulary grows additively as
    /// new hierarchy-handling modes are implemented.
    /// </summary>
    public static readonly LayoutProperty<HierarchyHandling> HierarchyHandling =
        new("rendering.hierarchyhandling", Rendering.HierarchyHandling.SeparateChildren);

    /// <summary>
    /// Primary flow direction for layered algorithms. Advisory in the bundled <c>layered</c>
    /// algorithm today, which lays out left-to-right; honoring other directions is a future,
    /// additive enhancement.
    /// </summary>
    public static readonly LayoutProperty<LayoutFlowDirection> Direction =
        new("rendering.direction", LayoutFlowDirection.Right);

    /// <summary>
    /// Routing style applied to connectors, mirroring ELK's <c>elk.edgeRouting</c>. Carried on any
    /// scope (graph, node, edge, or a free-standing <see cref="LayoutOptions"/>) so routing can be
    /// selected per scope just like the algorithm. The bundled routing orchestration reads this key
    /// to choose the router; the value defaults to <see cref="EdgeRouting.Orthogonal"/>, the only
    /// shipped style, and the vocabulary grows additively as new routers are implemented.
    /// </summary>
    public static readonly LayoutProperty<EdgeRouting> EdgeRouting =
        new("rendering.edgerouting", Rendering.EdgeRouting.Orthogonal);

    /// <summary>
    /// Advisory: desired spacing, in logical pixels, between adjacent nodes within a layer. Accepted
    /// but not yet honored by the bundled layered algorithm, which uses fixed engine metrics.
    /// </summary>
    public static readonly LayoutProperty<double> NodeSpacing =
        new("rendering.spacing.node", 20.0);

    /// <summary>
    /// Advisory: desired spacing, in logical pixels, between adjacent layers. Accepted but not yet
    /// honored by the bundled layered algorithm, which uses fixed engine metrics.
    /// </summary>
    public static readonly LayoutProperty<double> LayerSpacing =
        new("rendering.spacing.layer", 40.0);
}
