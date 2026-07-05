// <copyright file="ShapeAnchorSupport.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>
namespace DemaConsulting.Rendering.Layout.Engine.Layered;

/// <summary>
/// Small helper bridging the layered pipeline's abstract, direction-agnostic axes to
/// <see cref="ConnectorRouter"/>'s (internal) shape-geometry types, so <see cref="PortDistributor"/>
/// and <see cref="LongEdgeJoiner"/> can restrict a shaped node's connector ports to its usable
/// connectable face extents and project the resulting endpoint inward to the shape's real outline,
/// matching <see cref="ConnectorRouter"/>'s own shape-geometry rules for cross-container edges.
/// </summary>
/// <remarks>
/// This is the one place the <c>LayeredPipeline</c> unit depends on the <c>ConnectorRouter</c> unit;
/// see <c>docs/design/rendering-layout/engine/layered-pipeline.md</c>'s "Layered Pipeline
/// Dependencies" section for the documented exception.
/// </remarks>
internal static class ShapeAnchorSupport
{
    /// <summary>
    /// Resolves the real (screen-space) box face that a source or target port on an abstract,
    /// direction-agnostic node maps to for the given flow direction.
    /// </summary>
    /// <param name="direction">The layout's requested flow direction.</param>
    /// <param name="isSource">
    /// <see langword="true"/> to resolve the source-side (abstract right/along-far) face;
    /// <see langword="false"/> to resolve the target-side (abstract left/along-near) face.
    /// </param>
    /// <returns>The real <see cref="PortSide"/> the abstract face maps to.</returns>
    /// <remarks>
    /// Verified by algebraic trace of <see cref="AxisTransform"/>'s per-direction point mapping (see
    /// the shape-aware-anchors planning report): the abstract cross-axis coordinate is never
    /// reflected, so a local face coordinate carries over unchanged in all four cases, and the
    /// direction-to-face table is:
    /// <list type="bullet">
    /// <item><see cref="LayoutDirection.Right"/>: source → <see cref="PortSide.Right"/>, target →
    /// <see cref="PortSide.Left"/>.</item>
    /// <item><see cref="LayoutDirection.Left"/>: source → <see cref="PortSide.Left"/>, target →
    /// <see cref="PortSide.Right"/>.</item>
    /// <item><see cref="LayoutDirection.Down"/>: source → <see cref="PortSide.Bottom"/>, target →
    /// <see cref="PortSide.Top"/>.</item>
    /// <item><see cref="LayoutDirection.Up"/>: source → <see cref="PortSide.Top"/>, target →
    /// <see cref="PortSide.Bottom"/>.</item>
    /// </list>
    /// </remarks>
    public static PortSide ResolveRealFace(LayoutDirection direction, bool isSource) => direction switch
    {
        LayoutDirection.Right => isSource ? PortSide.Right : PortSide.Left,
        LayoutDirection.Left => isSource ? PortSide.Left : PortSide.Right,
        LayoutDirection.Down => isSource ? PortSide.Bottom : PortSide.Top,
        LayoutDirection.Up => isSource ? PortSide.Top : PortSide.Bottom,
        _ => isSource ? PortSide.Right : PortSide.Left,
    };

    /// <summary>
    /// Builds a throwaway <see cref="LayoutBox"/> carrying just enough of <paramref name="node"/>'s
    /// shape metadata to pass into <see cref="ConnectorRouter"/>'s (internal) shape-geometry
    /// resolution. The box's position is irrelevant (always <c>0, 0</c>) because only face-local
    /// extents and projections are read from the resolved geometry.
    /// </summary>
    /// <param name="node">The real (non-dummy) node whose shape metadata should be adapted.</param>
    /// <returns>A minimal <see cref="LayoutBox"/> suitable for <c>ConnectorRouter.ResolveShapeGeometry</c>.</returns>
    public static LayoutBox BuildAdapterBox(LayerNode node) => new(
        X: 0.0,
        Y: 0.0,
        Width: node.RealWidth,
        Height: node.RealHeight,
        Label: node.Label,
        Depth: 0,
        Shape: node.Shape,
        Compartments: [],
        Children: [],
        RoundedCornerRadius: node.RoundedCornerRadius,
        FolderTabWidth: node.FolderTabWidth,
        FolderTabHeight: node.FolderTabHeight);

    /// <summary>
    /// Fast-path guard used by both <see cref="PortDistributor"/> and <see cref="LongEdgeJoiner"/> to
    /// skip all shape-aware logic and fall through to the original, byte-identical arithmetic for the
    /// common (and default) case of a plain rectangle.
    /// </summary>
    /// <param name="node">The node to test.</param>
    /// <returns><see langword="true"/> if <paramref name="node"/> is a plain rectangle.</returns>
    public static bool IsPlainRectangle(LayerNode node) => node.Shape == BoxShape.Rectangle;
}
