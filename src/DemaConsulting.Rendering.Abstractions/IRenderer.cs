// <copyright file="IRenderer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Abstractions;

/// <summary>
/// Low-level renderer: converts one <see cref="LayoutTree"/> to one output stream.
/// Implementations must be pure, stateless, and must not perform filesystem access.
/// </summary>
public interface IRenderer
{
    /// <summary>Gets the MIME media type produced by this renderer.</summary>
    string MediaType { get; }

    /// <summary>Gets the default file extension (including leading dot) produced by this renderer.</summary>
    string DefaultExtension { get; }

    /// <summary>
    /// Gets every file extension (each including a leading dot, lower-case) this renderer produces.
    /// The list always contains <see cref="DefaultExtension"/> and lets consumers register and
    /// resolve a renderer by output filename (for example a renderer that emits both
    /// <c>.jpg</c> and <c>.jpeg</c>).
    /// </summary>
    IReadOnlyList<string> FileExtensions { get; }

    /// <summary>
    /// Renders the layout tree and writes the output to <paramref name="output"/>.
    /// </summary>
    /// <param name="layout">The layout tree describing all nodes to render.</param>
    /// <param name="options">Render options including theme and scale.</param>
    /// <param name="output">Destination stream that receives all rendered bytes.</param>
    void Render(LayoutTree layout, RenderOptions options, Stream output);
}
