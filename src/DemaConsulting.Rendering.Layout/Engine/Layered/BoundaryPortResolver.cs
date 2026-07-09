// <copyright file="BoundaryPortResolver.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Layout.Engine.Layered;

/// <summary>
/// Maps a scope's flow direction to the container boundary face a delegation (boundary) port sits on,
/// the one shared reference the recursive hierarchy handling uses to place a boundary anchor and to
/// join its external approach and internal delegation across the container boundary.
/// </summary>
/// <remarks>
///     The production placement of boundary ports is the single combined pass:
///     <see cref="MergeRegionGraphAssembler"/> assembles the hierarchy-aware graph,
///     <see cref="LayeredLayoutPipeline.RunRecursive"/> lays every nesting level out in one coordinated
///     placement, and <see cref="MergeRegionDecomposer"/> projects the placed result back into per-scope
///     geometry. This type retains only the direction-to-face mapping that both the decomposer's anchor
///     placement and its orthogonal boundary joins share.
/// </remarks>
internal static class BoundaryPortResolver
{
    /// <summary>
    /// Maps a flow direction to the container boundary face a delegation port sits on: the face the flow
    /// enters from, so an external approach and an internal delegation meet head-on across the boundary.
    /// </summary>
    /// <param name="direction">The scope's flow direction.</param>
    /// <returns>The boundary face a delegation anchor is placed on.</returns>
    internal static PortSide FaceForDirection(LayoutDirection direction) => direction switch
    {
        LayoutDirection.Left => PortSide.Right,
        LayoutDirection.Down => PortSide.Top,
        LayoutDirection.Up => PortSide.Bottom,
        _ => PortSide.Left,
    };
}
