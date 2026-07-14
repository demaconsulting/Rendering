// <copyright file="LayoutAlgorithmBase.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Abstractions;

/// <summary>
/// A pluggable layout algorithm: the high-level, ELK-inspired extension point. An algorithm consumes
/// an unplaced <see cref="LayoutGraph"/> and produces a placed <see cref="LayoutTree"/> ready for an
/// <see cref="IRenderer"/>. Additional diagram families (tree, force, packing, and so on) are
/// introduced purely additively by deriving from this class and registering the implementation; no
/// existing contract changes.
/// </summary>
/// <remarks>
///     <para>
///     <strong>Single configuration surface.</strong> A graph is an <see cref="IPropertyHolder"/>, so
///     every <see cref="CoreOptions"/> a layout should honor — including which algorithm applies — is
///     configured directly on the graph (or, for a nested container's own scope, on its
///     <see cref="LayoutGraphNode.Children"/> graph). <see cref="Apply(LayoutGraph)"/> is therefore the
///     only public entry point and takes no separate options argument: there is exactly one way to
///     configure a run, so a caller can never supply settings that contradict the graph's own.
///     </para>
///     <para>
///     <strong>Extensibility without a second public surface.</strong> A hierarchical or composite
///     algorithm still needs to recurse into a nested scope with that scope's own cascaded
///     (parent-resolved) options — for example a hierarchical algorithm resolving a container's
///     sub-layout. That recursion is exposed only to other algorithm implementations, via
///     the <c>protected internal</c> <see cref="ApplyCore(LayoutGraph, LayoutOptions)"/> extension
///     point: accessible to a derived class (the <c>protected</c> half) and, through
///     <c>InternalsVisibleTo</c>, to the bundled algorithms in
///     <c>DemaConsulting.Rendering.Layout</c> (the <c>internal</c> half), but never to an external
///     caller, who can only reach the sealed, single-argument <see cref="Apply(LayoutGraph)"/>.
///     </para>
/// </remarks>
public abstract class LayoutAlgorithmBase
{
    /// <summary>
    /// Gets the stable identifier used to select this algorithm (for example <c>layered</c>). Matches
    /// the value read from <see cref="CoreOptions.Algorithm"/>.
    /// </summary>
    public abstract string Id { get; }

    /// <summary>
    /// Computes a placed layout for <paramref name="graph"/>. Every setting the layout should honor
    /// (algorithm, direction, edge routing, spacing, and so on) is read directly from
    /// <paramref name="graph"/> itself, since it is an <see cref="IPropertyHolder"/>; this is the only
    /// way to configure a run, so there is no second options argument to disagree with the graph.
    /// </summary>
    /// <param name="graph">The unplaced input graph.</param>
    /// <returns>A fully-placed <see cref="LayoutTree"/> with absolute coordinates and routed edges.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="graph"/> is <see langword="null"/>.</exception>
    public LayoutTree Apply(LayoutGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return ApplyCore(graph, new LayoutOptions());
    }

    /// <summary>
    /// Computes a placed layout for <paramref name="graph"/> using <paramref name="options"/> as the
    /// cascaded fallback for any setting the graph itself does not explicitly declare. This extension
    /// point exists purely so a composite algorithm (for example a hierarchical algorithm)
    /// can recurse into a nested scope with that scope's already-resolved effective options; it is not
    /// part of the public contract, so an external caller cannot use it to supply settings that
    /// contradict the graph's own.
    /// </summary>
    /// <param name="graph">The unplaced input graph, or nested scope, to lay out.</param>
    /// <param name="options">
    /// The cascaded fallback for any setting <paramref name="graph"/> does not explicitly declare.
    /// Properties this algorithm does not understand are ignored, so a caller may pass options intended
    /// for another algorithm without error.
    /// </param>
    /// <returns>A fully-placed <see cref="LayoutTree"/> with absolute coordinates and routed edges.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="graph"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    protected internal abstract LayoutTree ApplyCore(LayoutGraph graph, LayoutOptions options);
}
