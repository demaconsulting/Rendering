// <copyright file="RenderOptions.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Abstractions;

/// <summary>
/// Options that control the rendering of a <see cref="DemaConsulting.Rendering.LayoutTree"/>.
/// </summary>
/// <remarks>
/// Only <paramref name="Theme"/> is required. <paramref name="Scale"/>, <paramref name="Dpi"/> and
/// <paramref name="DepthLimit"/> are optional (defaults <c>1.0</c>, <c>96</c> and <c>0</c>), so the
/// usual form is simply <c>new RenderOptions(theme)</c>; supply the later arguments only to override a
/// default.
/// </remarks>
/// <param name="Theme">Visual theme (colors, fonts, line metrics).</param>
/// <param name="Scale">Uniform scale factor applied to all coordinates. Default is 1.0.</param>
/// <param name="Dpi">Output resolution in dots per inch. Default is 96.</param>
/// <param name="DepthLimit">Maximum nesting depth to render. 0 means unlimited.</param>
public sealed record RenderOptions(
    Theme Theme,
    double Scale = 1.0,
    double Dpi = 96.0,
    int DepthLimit = 0);
