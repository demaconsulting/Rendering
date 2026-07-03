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
    /// Advisory: desired spacing, in logical pixels, between adjacent nodes within a layer. Accepted
    /// but not yet honored by the bundled layered algorithm, which uses fixed engine metrics. Would
    /// cascade per scope by nearest-ancestor override, consistent with every other property in this
    /// catalog, once an algorithm honors it.
    /// </summary>
    public static readonly LayoutProperty<double> NodeSpacing =
        new("rendering.spacing.node", 20.0);

    /// <summary>
    /// Advisory: desired spacing, in logical pixels, between adjacent layers. Accepted but not yet
    /// honored by the bundled layered algorithm, which uses fixed engine metrics. Would cascade per
    /// scope by nearest-ancestor override, consistent with every other property in this catalog, once
    /// an algorithm honors it.
    /// </summary>
    public static readonly LayoutProperty<double> LayerSpacing =
        new("rendering.spacing.layer", 40.0);
}
