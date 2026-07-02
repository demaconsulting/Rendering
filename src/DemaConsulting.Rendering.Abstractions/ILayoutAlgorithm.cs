// <copyright file="ILayoutAlgorithm.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Abstractions;

/// <summary>
/// A pluggable layout algorithm: the high-level, ELK-inspired extension point. An algorithm consumes
/// an unplaced <see cref="LayoutGraph"/> plus <see cref="LayoutOptions"/> and produces a placed
/// <see cref="LayoutTree"/> ready for an <see cref="IRenderer"/>. Additional diagram families (tree,
/// force, packing, and so on) are introduced purely additively by implementing this interface and
/// registering the implementation; no existing contract changes.
/// </summary>
public interface ILayoutAlgorithm
{
    /// <summary>
    /// Gets the stable identifier used to select this algorithm (for example <c>layered</c>). Matches
    /// the value read from <see cref="CoreOptions.Algorithm"/>.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Computes a placed layout for <paramref name="graph"/> using the supplied options.
    /// </summary>
    /// <param name="graph">The unplaced input graph.</param>
    /// <param name="options">
    /// Configuration for this run. Properties the algorithm does not understand are ignored, so
    /// callers may pass options intended for other algorithms without error.
    /// </param>
    /// <returns>A fully-placed <see cref="LayoutTree"/> with absolute coordinates and routed edges.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="graph"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    LayoutTree Apply(LayoutGraph graph, LayoutOptions options);
}
