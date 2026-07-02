// <copyright file="LayoutOptions.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// A free-standing, shareable <see cref="IPropertyHolder"/> used to pass configuration to a layout
/// algorithm or renderer independently of any graph element. Well-known keys are declared on
/// <see cref="CoreOptions"/>; callers may also set any custom <see cref="LayoutProperty{T}"/>.
/// </summary>
public sealed class LayoutOptions : PropertyHolder
{
    /// <summary>
    /// Creates a <see cref="LayoutOptions"/> pre-set with the given layout algorithm identifier.
    /// </summary>
    /// <param name="algorithmId">
    /// Identifier of the layout algorithm to run (see <see cref="CoreOptions.Algorithm"/>). Prefer a
    /// bundled algorithm-id constant — <c>LayeredLayoutAlgorithm.AlgorithmId</c>,
    /// <c>ContainmentLayoutAlgorithm.AlgorithmId</c>, or <c>HierarchicalLayoutAlgorithm.AlgorithmId</c> —
    /// over a hardcoded string such as <c>"hierarchical"</c>.
    /// </param>
    /// <returns>A new options instance with <see cref="CoreOptions.Algorithm"/> set.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="algorithmId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="algorithmId"/> is empty.</exception>
    public static LayoutOptions ForAlgorithm(string algorithmId)
    {
        ArgumentException.ThrowIfNullOrEmpty(algorithmId);
        var options = new LayoutOptions();
        options.Set(CoreOptions.Algorithm, algorithmId);
        return options;
    }
}
