// <copyright file="RendererRegistry.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Collections.ObjectModel;

namespace DemaConsulting.Rendering.Abstractions;

/// <summary>
/// A lookup of <see cref="IRenderer"/> implementations keyed by <see cref="IRenderer.MediaType"/>.
/// Consumers register the renderers they wish to make available (for example the bundled SVG and PNG
/// renderers) and resolve one by media type at render time. It is not thread-safe for concurrent
/// registration.
/// </summary>
public sealed class RendererRegistry
{
    private readonly Dictionary<string, IRenderer> _renderers =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the media types of all registered renderers.</summary>
    public ReadOnlyCollection<string> MediaTypes => new([.. _renderers.Keys]);

    /// <summary>
    /// Registers a renderer, replacing any previously-registered renderer with the same media type.
    /// </summary>
    /// <param name="renderer">The renderer to register.</param>
    /// <returns>This registry, to support fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderer"/> is <see langword="null"/>.</exception>
    public RendererRegistry Register(IRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderers[renderer.MediaType] = renderer;
        return this;
    }

    /// <summary>Determines whether a renderer for the given media type is registered.</summary>
    /// <param name="mediaType">The renderer media type.</param>
    /// <returns><see langword="true"/> when a matching renderer is registered; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mediaType"/> is <see langword="null"/>.</exception>
    public bool Contains(string mediaType)
    {
        ArgumentNullException.ThrowIfNull(mediaType);
        return _renderers.ContainsKey(mediaType);
    }

    /// <summary>Attempts to resolve the renderer registered for <paramref name="mediaType"/>.</summary>
    /// <param name="mediaType">The renderer media type.</param>
    /// <param name="renderer">When this method returns <see langword="true"/>, the resolved renderer.</param>
    /// <returns><see langword="true"/> when a matching renderer is registered; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mediaType"/> is <see langword="null"/>.</exception>
    public bool TryResolve(string mediaType, out IRenderer? renderer)
    {
        ArgumentNullException.ThrowIfNull(mediaType);
        return _renderers.TryGetValue(mediaType, out renderer);
    }

    /// <summary>Resolves the renderer registered for <paramref name="mediaType"/>.</summary>
    /// <param name="mediaType">The renderer media type.</param>
    /// <returns>The resolved renderer.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mediaType"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no renderer is registered for <paramref name="mediaType"/>.</exception>
    public IRenderer Resolve(string mediaType)
    {
        ArgumentNullException.ThrowIfNull(mediaType);
        return _renderers.TryGetValue(mediaType, out var renderer)
            ? renderer
            : throw new KeyNotFoundException($"No renderer is registered for media type '{mediaType}'.");
    }
}
