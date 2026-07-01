// <copyright file="CoreOptions.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// Well-known <see cref="LayoutProperty{T}"/> keys understood by the core layout pipeline. This is
/// the ELK-style option catalog: the set is expected to grow over time, and adding a key is a purely
/// additive change. Keys marked <em>advisory</em> are accepted today but not yet honored by the
/// bundled algorithms; they default harmlessly until an algorithm implements them.
/// </summary>
public static class CoreOptions
{
    /// <summary>
    /// Identifier of the layout algorithm to apply (for example <c>layered</c>). Resolved against the
    /// registered algorithms by a caller or layout service.
    /// </summary>
    public static readonly LayoutProperty<string> Algorithm =
        new("rendering.algorithm", "layered");

    /// <summary>
    /// Primary flow direction for layered algorithms. Advisory in the bundled <c>layered</c>
    /// algorithm today, which lays out left-to-right; honoring other directions is a future,
    /// additive enhancement.
    /// </summary>
    public static readonly LayoutProperty<LayoutFlowDirection> Direction =
        new("rendering.direction", LayoutFlowDirection.Right);

    /// <summary>
    /// Advisory: desired spacing, in logical pixels, between adjacent nodes within a layer. Accepted
    /// but not yet honored by the bundled layered algorithm, which uses fixed engine metrics.
    /// </summary>
    public static readonly LayoutProperty<double> NodeSpacing =
        new("rendering.spacing.node", 20.0);

    /// <summary>
    /// Advisory: desired spacing, in logical pixels, between adjacent layers. Accepted but not yet
    /// honored by the bundled layered algorithm, which uses fixed engine metrics.
    /// </summary>
    public static readonly LayoutProperty<double> LayerSpacing =
        new("rendering.spacing.layer", 40.0);
}
