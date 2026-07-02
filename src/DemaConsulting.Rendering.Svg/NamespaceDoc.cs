// <copyright file="NamespaceDoc.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Svg;

/// <summary>
/// The SVG renderer tier: draws a placed <c>LayoutTree</c> (from the <c>DemaConsulting.Rendering</c>
/// model, laid out by <c>DemaConsulting.Rendering.Layout</c>) to a scalable SVG document.
/// </summary>
/// <remarks>
/// <para>
/// The single public type, <see cref="SvgRenderer"/>, implements the
/// <c>DemaConsulting.Rendering.Abstractions.IRenderer</c> contract with zero external dependencies:
/// it is pure and stateless, emitting UTF-8 SVG 1.1 for each node and connector styled by the
/// supplied <c>Theme</c>. This is the final stage of the diagram pipeline for vector output.
/// </para>
/// </remarks>
internal static class NamespaceDoc
{
}
