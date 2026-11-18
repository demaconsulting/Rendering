// <copyright file="EmptySpaceMeterTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Layout.Engine;

namespace DemaConsulting.Rendering.Layout.Tests.Engine;

/// <summary>
///     Tests for <see cref="EmptySpaceMeter"/>: the largest empty square of a drawing on the occupancy grid,
///     where leaf boxes and dilated connectors occupy cells, container outlines are transparent, and the
///     passing limit is a multiple of the mean leaf block height.
/// </summary>
public sealed class EmptySpaceMeterTests
{
    /// <summary>A tree with no node leaves the whole canvas empty: its smaller side is the square.</summary>
    [Fact]
    public void EmptySpaceMeter_LargestEmptySquare_EmptyTree_IsSmallerCanvasSide()
    {
        // Arrange
        var tree = new LayoutTree(200.0, 120.0, []);

        // Act
        var square = EmptySpaceMeter.LargestEmptySquare(tree);

        // Assert
        Assert.Equal(120.0, square, 6);
    }

    /// <summary>A leaf box in the middle of the canvas splits the empty space around it.</summary>
    [Fact]
    public void EmptySpaceMeter_LargestEmptySquare_CentralLeafBox_ReducesSquare()
    {
        // Arrange: a 40 x 40 box in the middle of a 200 x 200 canvas
        var box = new LayoutBox(80.0, 80.0, 40.0, 40.0, "box", 0, BoxShape.Rectangle, [], []);
        var tree = new LayoutTree(200.0, 200.0, [box]);

        // Act
        var square = EmptySpaceMeter.LargestEmptySquare(tree);

        // Assert: the widest strip beside the box is 80 px wide
        Assert.Equal(80.0, square, 6);
    }

    /// <summary>A container outline is transparent: only the leaf box inside it occupies cells.</summary>
    [Fact]
    public void EmptySpaceMeter_LargestEmptySquare_ContainerOutline_IsTransparent()
    {
        // Arrange: a 200 x 200 container holding one small leaf in a corner
        var leaf = new LayoutBox(10.0, 10.0, 20.0, 20.0, "leaf", 1, BoxShape.Rectangle, [], []);
        var container = new LayoutBox(0.0, 0.0, 200.0, 200.0, "container", 0, BoxShape.Rectangle, [], [leaf]);
        var tree = new LayoutTree(200.0, 200.0, [container]);

        // Act
        var square = EmptySpaceMeter.LargestEmptySquare(tree);

        // Assert: the free square is nearly the whole container
        Assert.True(square >= 160.0, $"the square {square} is smaller than expected");
    }

    /// <summary>A long connector across the canvas halves the largest empty square.</summary>
    [Fact]
    public void EmptySpaceMeter_LargestEmptySquare_ConnectorAcrossCanvas_SplitsSpace()
    {
        // Arrange: a horizontal connector through the middle of a 200 x 200 canvas
        var line = new LayoutLine(
            [new Point2D(0.0, 100.0), new Point2D(200.0, 100.0)],
            EndMarkerStyle.None,
            EndMarkerStyle.None,
            LineStyle.Solid,
            null);
        var tree = new LayoutTree(200.0, 200.0, [line]);

        // Act
        var square = EmptySpaceMeter.LargestEmptySquare(tree);

        // Assert: each half is about 92 px tall (the connector is dilated by one cell on each side)
        Assert.InRange(square, 88.0, 100.0);
    }

    /// <summary>The passing square is three times the mean leaf block height.</summary>
    [Fact]
    public void EmptySpaceMeter_PassingSquare_LeafBoxes_IsThreeTimesMeanHeight()
    {
        // Arrange: two leaves 40 and 60 px tall
        var first = new LayoutBox(0.0, 0.0, 50.0, 40.0, "a", 0, BoxShape.Rectangle, [], []);
        var second = new LayoutBox(100.0, 0.0, 50.0, 60.0, "b", 0, BoxShape.Rectangle, [], []);
        var tree = new LayoutTree(200.0, 100.0, [first, second]);

        // Act
        var square = EmptySpaceMeter.PassingSquare(tree);

        // Assert
        Assert.Equal(150.0, square, 6);
    }

    /// <summary>A tree without leaf boxes has no passing square.</summary>
    [Fact]
    public void EmptySpaceMeter_PassingSquare_NoLeafBox_IsZero()
    {
        // Arrange
        var tree = new LayoutTree(100.0, 100.0, []);

        // Act
        var square = EmptySpaceMeter.PassingSquare(tree);

        // Assert
        Assert.Equal(0.0, square, 6);
    }
}
