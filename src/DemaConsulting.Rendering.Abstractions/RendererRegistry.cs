// <copyright file="RendererRegistry.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Collections.ObjectModel;

namespace DemaConsulting.Rendering.Abstractions;

/// <summary>
/// A lookup of <see cref="IRenderer"/> implementations indexed by both <see cref="IRenderer.MediaType"/>
/// and every <see cref="IRenderer.FileExtensions">file extension</see> a renderer produces. Consumers
/// register the renderers they wish to make available (for example the bundled SVG, PNG, JPEG, and
/// WEBP renderers) and resolve one by media type or by output filename extension at render time. It is
/// not thread-safe for concurrent registration.
/// </summary>
public sealed class RendererRegistry
{
    private readonly Dictionary<string, IRenderer> _byMediaType =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IRenderer> _byExtension =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the media types of all registered renderers.</summary>
    public ReadOnlyCollection<string> MediaTypes => new([.. _byMediaType.Keys]);

    /// <summary>Gets the file extensions (each including a leading dot) of all registered renderers.</summary>
    public ReadOnlyCollection<string> FileExtensions => new([.. _byExtension.Keys]);

    /// <summary>
    /// Registers a renderer under its media type and each of its file extensions, replacing any
    /// previously-registered renderer that claimed the same media type or extension.
    /// </summary>
    /// <param name="renderer">The renderer to register.</param>
    /// <returns>This registry, to support fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderer"/> is <see langword="null"/>.</exception>
    public RendererRegistry Register(IRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _byMediaType[renderer.MediaType] = renderer;
        foreach (var extension in renderer.FileExtensions)
        {
            _byExtension[Normalize(extension)] = renderer;
        }

        return this;
    }

    /// <summary>Determines whether a renderer for the given media type is registered.</summary>
    /// <param name="mediaType">The renderer media type.</param>
    /// <returns><see langword="true"/> when a matching renderer is registered; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mediaType"/> is <see langword="null"/>.</exception>
    public bool Contains(string mediaType)
    {
        ArgumentNullException.ThrowIfNull(mediaType);
        return _byMediaType.ContainsKey(mediaType);
    }

    /// <summary>Determines whether a renderer for the given file extension is registered.</summary>
    /// <param name="extension">The file extension, with or without a leading dot (case-insensitive).</param>
    /// <returns><see langword="true"/> when a matching renderer is registered; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="extension"/> is <see langword="null"/>.</exception>
    public bool ContainsExtension(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        return _byExtension.ContainsKey(Normalize(extension));
    }

    /// <summary>Attempts to resolve the renderer registered for <paramref name="mediaType"/>.</summary>
    /// <param name="mediaType">The renderer media type.</param>
    /// <param name="renderer">When this method returns <see langword="true"/>, the resolved renderer.</param>
    /// <returns><see langword="true"/> when a matching renderer is registered; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mediaType"/> is <see langword="null"/>.</exception>
    public bool TryResolve(string mediaType, out IRenderer? renderer)
    {
        ArgumentNullException.ThrowIfNull(mediaType);
        return _byMediaType.TryGetValue(mediaType, out renderer);
    }

    /// <summary>Attempts to resolve the renderer registered for <paramref name="extension"/>.</summary>
    /// <param name="extension">The file extension, with or without a leading dot (case-insensitive).</param>
    /// <param name="renderer">When this method returns <see langword="true"/>, the resolved renderer.</param>
    /// <returns><see langword="true"/> when a matching renderer is registered; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="extension"/> is <see langword="null"/>.</exception>
    public bool TryResolveByExtension(string extension, out IRenderer? renderer)
    {
        ArgumentNullException.ThrowIfNull(extension);
        return _byExtension.TryGetValue(Normalize(extension), out renderer);
    }

    /// <summary>Resolves the renderer registered for <paramref name="mediaType"/>.</summary>
    /// <param name="mediaType">The renderer media type.</param>
    /// <returns>The resolved renderer.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mediaType"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no renderer is registered for <paramref name="mediaType"/>.</exception>
    public IRenderer Resolve(string mediaType)
    {
        ArgumentNullException.ThrowIfNull(mediaType);
        return _byMediaType.TryGetValue(mediaType, out var renderer)
            ? renderer
            : throw new KeyNotFoundException($"No renderer is registered for media type '{mediaType}'.");
    }

    /// <summary>Resolves the renderer registered for the given output file extension.</summary>
    /// <param name="extension">The file extension, with or without a leading dot (case-insensitive).</param>
    /// <returns>The resolved renderer.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="extension"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no renderer is registered for <paramref name="extension"/>.</exception>
    public IRenderer ResolveByExtension(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        var normalized = Normalize(extension);
        return _byExtension.TryGetValue(normalized, out var renderer)
            ? renderer
            : throw new KeyNotFoundException($"No renderer is registered for file extension '{normalized}'.");
    }

    private static string Normalize(string extension)
    {
        var trimmed = extension.Trim();
        return trimmed.StartsWith('.') ? trimmed.ToLowerInvariant() : "." + trimmed.ToLowerInvariant();
    }
}
