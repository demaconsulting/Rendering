// <copyright file="NamespaceDoc.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Skia;

/// <summary>
/// The raster renderer tier: draws a placed <c>LayoutTree</c> (from the
/// <c>DemaConsulting.Rendering</c> model, laid out by <c>DemaConsulting.Rendering.Layout</c>) to
/// bitmap image formats (PNG, JPEG, WEBP) using SkiaSharp.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SkiaRasterRenderer"/> is the abstract SkiaSharp rasterizer shared by the concrete
/// formats — <see cref="PngRenderer"/> (lossless PNG), <see cref="JpegRenderer"/> (JPEG), and
/// <see cref="WebpRenderer"/> (WEBP) — each implementing the
/// <c>DemaConsulting.Rendering.Abstractions.IRenderer</c> contract. This is the final stage of the
/// diagram pipeline for raster output; use <c>DemaConsulting.Rendering.Svg</c> instead for scalable
/// vector output.
/// </para>
/// </remarks>
internal static class NamespaceDoc
{
}
