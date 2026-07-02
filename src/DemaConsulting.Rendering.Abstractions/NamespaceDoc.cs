// <copyright file="NamespaceDoc.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Abstractions;

/// <summary>
/// The service-provider interface (SPI) tier: the pluggable <see cref="ILayoutAlgorithm"/> and
/// <see cref="IRenderer"/> contracts, their registries, and the <see cref="Theme"/> that sit between
/// the diagram model and the concrete layout and render implementations.
/// </summary>
/// <remarks>
/// <para>
/// It defines the <see cref="ILayoutAlgorithm"/> contract (turn an unplaced <c>LayoutGraph</c> into
/// a placed <c>LayoutTree</c>) and the <see cref="IRenderer"/> contract (draw a <c>LayoutTree</c> to
/// an output format), the <see cref="LayoutAlgorithmRegistry"/> and <see cref="RendererRegistry"/>
/// that resolve them by identifier, and the <see cref="Theme"/> (with the built-in
/// <see cref="Themes"/>) and render options that style output. Implement these contracts to add a
/// new layout algorithm or renderer additively — the bundled ones live in
/// <c>DemaConsulting.Rendering.Layout</c>, <c>DemaConsulting.Rendering.Svg</c>, and
/// <c>DemaConsulting.Rendering.Skia</c>.
/// </para>
/// </remarks>
internal static class NamespaceDoc
{
}
