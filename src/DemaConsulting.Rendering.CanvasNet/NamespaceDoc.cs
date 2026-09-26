// <copyright file="NamespaceDoc.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.CanvasNet;

/// <summary>
/// The raster renderer tier: draws a placed <c>LayoutTree</c> (from the
/// <c>DemaConsulting.Rendering</c> model, laid out by <c>DemaConsulting.Rendering.Layout</c>) to
/// bitmap image formats (PNG and JPEG) using CanvasNet.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CanvasNetRasterRenderer"/> is the abstract CanvasNet rasterizer shared by the
/// concrete formats — <see cref="PngRenderer"/> (lossless PNG) and <see cref="JpegRenderer"/>
/// (JPEG) — each implementing the <c>DemaConsulting.Rendering.Abstractions.IRenderer</c>
/// contract. This is the final stage of the diagram pipeline for raster output; use
/// <c>DemaConsulting.Rendering.Svg</c> instead for scalable vector output.
/// </para>
/// </remarks>
internal static class NamespaceDoc
{
}
