// <copyright file="LayoutAlgorithmRegistry.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Collections.ObjectModel;

namespace DemaConsulting.Rendering.Abstractions;

/// <summary>
/// A lookup of <see cref="ILayoutAlgorithm"/> implementations keyed by <see cref="ILayoutAlgorithm.Id"/>.
/// Consumers register the algorithms they wish to make available (for example the bundled
/// <c>layered</c> algorithm) and resolve one by identifier at layout time. This is the ELK-style
/// algorithm-provider service; it is not thread-safe for concurrent registration.
/// </summary>
public sealed class LayoutAlgorithmRegistry
{
    private readonly Dictionary<string, ILayoutAlgorithm> _algorithms =
        new(StringComparer.Ordinal);

    /// <summary>Gets the identifiers of all registered algorithms.</summary>
    public ReadOnlyCollection<string> Ids => new([.. _algorithms.Keys]);

    /// <summary>
    /// Registers an algorithm, replacing any previously-registered algorithm with the same identifier.
    /// </summary>
    /// <param name="algorithm">The algorithm to register.</param>
    /// <returns>This registry, to support fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="algorithm"/> is <see langword="null"/>.</exception>
    public LayoutAlgorithmRegistry Register(ILayoutAlgorithm algorithm)
    {
        ArgumentNullException.ThrowIfNull(algorithm);
        _algorithms[algorithm.Id] = algorithm;
        return this;
    }

    /// <summary>Determines whether an algorithm with the given identifier is registered.</summary>
    /// <param name="id">The algorithm identifier.</param>
    /// <returns><see langword="true"/> when a matching algorithm is registered; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is <see langword="null"/>.</exception>
    public bool Contains(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _algorithms.ContainsKey(id);
    }

    /// <summary>Attempts to resolve the algorithm registered under <paramref name="id"/>.</summary>
    /// <param name="id">The algorithm identifier.</param>
    /// <param name="algorithm">When this method returns <see langword="true"/>, the resolved algorithm.</param>
    /// <returns><see langword="true"/> when a matching algorithm is registered; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is <see langword="null"/>.</exception>
    public bool TryResolve(string id, out ILayoutAlgorithm? algorithm)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _algorithms.TryGetValue(id, out algorithm);
    }

    /// <summary>Resolves the algorithm registered under <paramref name="id"/>.</summary>
    /// <param name="id">The algorithm identifier.</param>
    /// <returns>The resolved algorithm.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no algorithm is registered under <paramref name="id"/>.</exception>
    public ILayoutAlgorithm Resolve(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _algorithms.TryGetValue(id, out var algorithm)
            ? algorithm
            : throw new KeyNotFoundException($"No layout algorithm is registered with id '{id}'.");
    }
}
