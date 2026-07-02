// <copyright file="NamespaceDoc.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Layout;

/// <summary>
/// The layout tier and primary entry point for turning a diagram into placed geometry: the bundled
/// layout algorithms, the one-call <see cref="LayoutEngine"/> facade, and the connector-routing and
/// containment helpers.
/// </summary>
/// <remarks>
/// <para>
/// Given an unplaced <c>LayoutGraph</c> from the <c>DemaConsulting.Rendering</c> model, the
/// algorithms here produce a fully-placed <c>LayoutTree</c> ready to render.
/// </para>
/// <para>
/// Begin with the one-call facade <see cref="LayoutEngine"/>:
/// <c>LayoutEngine.Layout(graph, options)</c> resolves whichever algorithm the graph (or options)
/// declares and lays out flat and nested graphs uniformly. The bundled algorithms are
/// <see cref="LayeredLayoutAlgorithm"/> (<c>layered</c> — ELK-style Sugiyama layering),
/// <see cref="ContainmentLayoutAlgorithm"/> (<c>containment</c> — grouped/packed placement), and
/// <see cref="HierarchicalLayoutAlgorithm"/> (<c>hierarchical</c> — the recursive engine that
/// composes nested graphs while matching the leaf algorithm byte-for-byte on flat graphs); use
/// <see cref="LayoutAlgorithms"/> to build a registry of them. Supporting helpers include
/// <see cref="ConnectorRouter"/> for orthogonal edge routing among placed boxes and
/// <see cref="ContainmentLayout"/> for containment packing. Once you have a <c>LayoutTree</c>,
/// render it with <c>DemaConsulting.Rendering.Svg</c> or <c>DemaConsulting.Rendering.Skia</c>.
/// </para>
/// </remarks>
internal static class NamespaceDoc
{
}
