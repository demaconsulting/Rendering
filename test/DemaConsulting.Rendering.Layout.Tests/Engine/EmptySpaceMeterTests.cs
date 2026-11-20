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

    /// <summary>
    ///     The square table finds, for any rectangle filled into a grid, the same largest empty square as a
    ///     full pass over a copy of the filled grid (Rendering#75: the hole search of the tree packer relies
    ///     on it to skip that pass).
    /// </summary>
    [Fact]
    public void EmptySpaceMeter_SquareIndex_LargestWithFilled_EqualsFullPassOverFilledCopy()
    {
        // Arrange: random sparse grids of several shapes and many random filled rectangles
        var random = new Random(75);
        foreach (var (columns, rows, density) in new[] { (30, 20, 0.05), (17, 41, 0.15), (60, 60, 0.30), (1, 25, 0.10), (25, 1, 0.10) })
        {
            var grid = new bool[columns, rows];
            for (var column = 0; column < columns; column++)
            {
                for (var row = 0; row < rows; row++)
                {
                    grid[column, row] = random.NextDouble() < density;
                }
            }

            var index = new EmptySpaceMeter.SquareIndex(grid);
            Assert.Equal(EmptySpaceMeter.LargestEmptySquareCells(grid), index.Largest);

            for (var trial = 0; trial < 200; trial++)
            {
                var firstColumn = random.Next(columns);
                var lastColumn = Math.Min(columns - 1, firstColumn + random.Next(columns / 2 + 1));
                var firstRow = random.Next(rows);
                var lastRow = Math.Min(rows - 1, firstRow + random.Next(rows / 2 + 1));
                var filled = (bool[,])grid.Clone();
                for (var column = firstColumn; column <= lastColumn; column++)
                {
                    for (var row = firstRow; row <= lastRow; row++)
                    {
                        filled[column, row] = true;
                    }
                }

                // Act
                var expected = EmptySpaceMeter.LargestEmptySquareCells(filled);
                var actual = index.LargestWithFilled(firstColumn, lastColumn, firstRow, lastRow);

                // Assert: the exact side
                Assert.Equal(expected, actual);

                // A limit below the answer stops early with a side above the limit; one at or above it is exact
                if (expected > 0)
                {
                    Assert.True(index.LargestWithFilled(firstColumn, lastColumn, firstRow, lastRow, expected - 1) > expected - 1);
                    Assert.True(EmptySpaceMeter.LargestEmptySquareCells(filled, expected - 1) > expected - 1);
                }

                Assert.Equal(expected, index.LargestWithFilled(firstColumn, lastColumn, firstRow, lastRow, expected));
                Assert.Equal(expected, EmptySpaceMeter.LargestEmptySquareCells(filled, expected));
            }
        }
    }

    /// <summary>Filling nothing leaves the grid's own largest empty square.</summary>
    [Fact]
    public void EmptySpaceMeter_SquareIndex_LargestWithFilled_EmptyRange_IsGridLargest()
    {
        // Arrange
        var grid = new bool[10, 10];
        grid[5, 5] = true;
        var index = new EmptySpaceMeter.SquareIndex(grid);

        // Act / Assert
        Assert.Equal(index.Largest, index.LargestWithFilled(4, 3, 0, 9));
        Assert.Equal(index.Largest, index.LargestWithFilled(0, 9, 7, 6));
    }
}
