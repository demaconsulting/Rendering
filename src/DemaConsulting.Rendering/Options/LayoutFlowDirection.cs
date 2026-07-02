// <copyright file="LayoutFlowDirection.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// The primary flow direction a layered layout algorithm arranges nodes along: successive layers
/// progress in this direction.
/// </summary>
public enum LayoutFlowDirection
{
    /// <summary>Layers progress left-to-right (the default for most block diagrams).</summary>
    Right = 0,

    /// <summary>Layers progress right-to-left.</summary>
    Left = 1,

    /// <summary>Layers progress top-to-bottom.</summary>
    Down = 2,

    /// <summary>Layers progress bottom-to-top.</summary>
    Up = 3,
}
