// <copyright file="BoundaryPortResolverTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Tests.Engine.Layered;

/// <summary>
///     Tests for <see cref="BoundaryPortResolver.FaceForDirection"/>: the direction-to-boundary-face
///     mapping the recursive hierarchy handling shares between its anchor placement and its orthogonal
///     boundary joins. The former reconciliation entry points (<c>Resolve</c>, <c>OrderCrossings</c>,
///     and their helpers) were removed when the single combined pass
///     (<see cref="MergeRegionGraphAssembler"/> → <see cref="LayeredLayoutPipeline.RunRecursive"/> →
///     <see cref="MergeRegionDecomposer"/>) replaced post-hoc reconciliation, so their tests were
///     retired with them; the end-to-end behavior they covered is now proven by the
///     <c>MergeRegionDecomposer</c> and <c>HierarchicalLayoutAlgorithm</c> boundary-port tests.
/// </summary>
public sealed class BoundaryPortResolverTests
{
    /// <summary>
    ///     A rightward flow enters a container from its left face, so a delegation anchor sits on the
    ///     left boundary where the external approach and internal delegation meet head-on.
    /// </summary>
    [Fact]
    public void FaceForDirection_Right_ReturnsLeftFace()
    {
        // Act
        var face = BoundaryPortResolver.FaceForDirection(LayoutDirection.Right);

        // Assert
        Assert.Equal(PortSide.Left, face);
    }

    /// <summary>A leftward flow enters from the right face.</summary>
    [Fact]
    public void FaceForDirection_Left_ReturnsRightFace()
    {
        // Act
        var face = BoundaryPortResolver.FaceForDirection(LayoutDirection.Left);

        // Assert
        Assert.Equal(PortSide.Right, face);
    }

    /// <summary>A downward flow enters from the top face.</summary>
    [Fact]
    public void FaceForDirection_Down_ReturnsTopFace()
    {
        // Act
        var face = BoundaryPortResolver.FaceForDirection(LayoutDirection.Down);

        // Assert
        Assert.Equal(PortSide.Top, face);
    }

    /// <summary>An upward flow enters from the bottom face.</summary>
    [Fact]
    public void FaceForDirection_Up_ReturnsBottomFace()
    {
        // Act
        var face = BoundaryPortResolver.FaceForDirection(LayoutDirection.Up);

        // Assert
        Assert.Equal(PortSide.Bottom, face);
    }
}
