// <copyright file="ContainmentPackerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Layout.Engine;

namespace DemaConsulting.Rendering.Layout.Tests.Engine;

/// <summary>
///     Tests for <see cref="ContainmentPacker"/> shelf bin-packing.
/// </summary>
public sealed class ContainmentPackerTests
{
    /// <summary>
    ///     Packing an empty list returns a region consisting only of padding and no rectangles.
    /// </summary>
    [Fact]
    public void Pack_EmptyList_ReturnsPaddingOnlyRegion()
    {
        // Act: pack no items with padding 10
        var result = ContainmentPacker.Pack([], maxContentWidth: 100, horizontalGap: 5, verticalGap: 5, padding: 10);

        // Assert: region is 2*padding on each axis with no rectangles
        Assert.Empty(result.Rects);
        Assert.Equal(20.0, result.Width);
        Assert.Equal(20.0, result.Height);
    }

    /// <summary>
    ///     A single item is positioned at the padding origin and the region fits it exactly.
    /// </summary>
    [Fact]
    public void Pack_SingleItem_PositionsAtPaddingOrigin()
    {
        // Arrange: one 40x20 item
        var items = new[] { new PackItem(40, 20) };

        // Act
        var result = ContainmentPacker.Pack(items, maxContentWidth: 200, horizontalGap: 5, verticalGap: 5, padding: 10);

        // Assert: positioned at (10, 10); region = item + 2*padding
        Assert.Single(result.Rects);
        Assert.Equal(10.0, result.Rects[0].X);
        Assert.Equal(10.0, result.Rects[0].Y);
        Assert.Equal(60.0, result.Width);
        Assert.Equal(40.0, result.Height);
    }

    /// <summary>
    ///     A null items argument throws ArgumentNullException rather than failing partway through the
    ///     packing algorithm with a less specific exception.
    /// </summary>
    [Fact]
    public void Pack_NullItems_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(
            () => ContainmentPacker.Pack(null!, maxContentWidth: 200, horizontalGap: 5, verticalGap: 5, padding: 10));
    }

    /// <summary>
    ///     Items that fit within the max content width are placed on a single row sharing a Y.
    /// </summary>
    [Fact]
    public void Pack_ItemsFitInRow_ShareSameRow()
    {
        // Arrange: three 30-wide items; max content width 200 fits all in one row
        var items = new[] { new PackItem(30, 20), new PackItem(30, 20), new PackItem(30, 20) };

        // Act
        var result = ContainmentPacker.Pack(items, maxContentWidth: 200, horizontalGap: 5, verticalGap: 5, padding: 10);

        // Assert: all three share the same top Y (single row)
        Assert.Equal(result.Rects[0].Y, result.Rects[1].Y);
        Assert.Equal(result.Rects[1].Y, result.Rects[2].Y);

        // And X positions increase left-to-right with the horizontal gap
        Assert.Equal(10.0, result.Rects[0].X);
        Assert.Equal(45.0, result.Rects[1].X);
        Assert.Equal(80.0, result.Rects[2].X);
    }

    /// <summary>
    ///     Items exceeding the max content width wrap to a new row positioned below the first.
    /// </summary>
    [Fact]
    public void Pack_ItemsExceedWidth_WrapToNewRow()
    {
        // Arrange: three 80-wide items; max content width 200 fits only two per row
        var items = new[] { new PackItem(80, 20), new PackItem(80, 20), new PackItem(80, 20) };

        // Act
        var result = ContainmentPacker.Pack(items, maxContentWidth: 200, horizontalGap: 5, verticalGap: 5, padding: 10);

        // Assert: first two on row 0, third wraps to row 1 with a greater Y
        Assert.Equal(result.Rects[0].Y, result.Rects[1].Y);
        Assert.True(result.Rects[2].Y > result.Rects[0].Y);

        // Third item starts a new row at the left padding origin
        Assert.Equal(10.0, result.Rects[2].X);
    }

    /// <summary>
    ///     For a mixed-size set, no two packed rectangles overlap.
    /// </summary>
    [Fact]
    public void Pack_MixedSizes_ProducesNoOverlaps()
    {
        // Arrange: a varied mix of sizes that forces multiple rows
        var items = new[]
        {
            new PackItem(60, 30), new PackItem(120, 20), new PackItem(40, 50),
            new PackItem(90, 25), new PackItem(70, 40), new PackItem(50, 30),
            new PackItem(110, 35), new PackItem(30, 20),
        };

        // Act
        var result = ContainmentPacker.Pack(items, maxContentWidth: 250, horizontalGap: 8, verticalGap: 8, padding: 12);

        // Assert: every pair of rectangles is disjoint
        for (var i = 0; i < result.Rects.Count; i++)
        {
            for (var j = i + 1; j < result.Rects.Count; j++)
            {
                Assert.False(Overlaps(result.Rects[i], result.Rects[j]),
                    $"Rectangles {i} and {j} overlap.");
            }
        }
    }

    /// <summary>
    ///     Every packed rectangle lies fully within the reported region bounds.
    /// </summary>
    [Fact]
    public void Pack_MixedSizes_AllRectsWithinBounds()
    {
        // Arrange: a varied mix of sizes
        var items = new[]
        {
            new PackItem(60, 30), new PackItem(120, 20), new PackItem(40, 50),
            new PackItem(90, 25), new PackItem(70, 40),
        };

        // Act
        var result = ContainmentPacker.Pack(items, maxContentWidth: 200, horizontalGap: 8, verticalGap: 8, padding: 12);

        // Assert: each rectangle is contained within [0, Width] x [0, Height]
        foreach (var r in result.Rects)
        {
            Assert.True(r.X >= 0);
            Assert.True(r.Y >= 0);
            Assert.True(r.X + r.Width <= result.Width + 1e-9);
            Assert.True(r.Y + r.Height <= result.Height + 1e-9);
        }
    }

    /// <summary>
    ///     An item wider than the content width is placed alone and the region widens to fit it.
    /// </summary>
    [Fact]
    public void Pack_ItemWiderThanContentWidth_PlacedAloneAndRegionWidens()
    {
        // Arrange: a 300-wide item with only 100 content width available
        var items = new[] { new PackItem(50, 20), new PackItem(300, 20) };

        // Act
        var result = ContainmentPacker.Pack(items, maxContentWidth: 100, horizontalGap: 5, verticalGap: 5, padding: 10);

        // Assert: the oversized item wrapped to its own row and the region widened to contain it
        Assert.True(result.Rects[1].Y > result.Rects[0].Y);
        Assert.True(result.Width >= 320.0);
    }

    /// <summary>
    ///     A per-pair edge count widens only the one horizontal gap between the two adjacent same-row
    ///     items it names, spreading them apart by the connector-corridor width for that fan.
    /// </summary>
    [Fact]
    public void Pack_SameRowEdgeCount_WidensHorizontalGap()
    {
        // Arrange: two 40-wide items that share a row, with eight connectors between them
        var items = new[] { new PackItem(40, 20), new PackItem(40, 20) };
        var edgeCounts = new Dictionary<(int First, int Second), int> { [(0, 1)] = 8 };

        // Act: pack with and without the edge-count lookup
        var widened = ContainmentPacker.Pack(
            items, maxContentWidth: 400, horizontalGap: 5, verticalGap: 5, padding: 10, edgeCounts);
        var plain = ContainmentPacker.Pack(
            items, maxContentWidth: 400, horizontalGap: 5, verticalGap: 5, padding: 10);

        // Assert: the second item is pushed right by the corridor width (2*10 + 7*16 = 132) beyond the
        // base gap, so its X sits at padding + width + 132 rather than padding + width + baseGap.
        Assert.Equal(10.0, widened.Rects[0].X);
        Assert.Equal(10.0 + 40.0 + 132.0, widened.Rects[1].X);

        // And the widened placement is strictly further right than the un-widened one.
        Assert.True(widened.Rects[1].X > plain.Rects[1].X);
    }

    /// <summary>
    ///     Supplying a <see langword="null"/> edge-count lookup produces byte-identical placement to a
    ///     caller that supplied no lookup at all, guarding the backward-compatible default path.
    /// </summary>
    [Fact]
    public void Pack_NullEdgeCounts_ByteIdenticalToNoCounts()
    {
        // Arrange: a mix that forces two rows so both axes are exercised
        var items = new[]
        {
            new PackItem(60, 30), new PackItem(120, 20), new PackItem(40, 50), new PackItem(90, 25),
        };

        // Act: one call passes null explicitly, the other omits the argument
        var withNull = ContainmentPacker.Pack(
            items, maxContentWidth: 200, horizontalGap: 8, verticalGap: 8, padding: 12, edgeCounts: null);
        var withoutArg = ContainmentPacker.Pack(
            items, maxContentWidth: 200, horizontalGap: 8, verticalGap: 8, padding: 12);

        // Assert: identical region and identical per-rectangle placement
        Assert.Equal(withoutArg.Width, withNull.Width);
        Assert.Equal(withoutArg.Height, withNull.Height);
        for (var i = 0; i < withoutArg.Rects.Count; i++)
        {
            Assert.Equal(withoutArg.Rects[i].X, withNull.Rects[i].X);
            Assert.Equal(withoutArg.Rects[i].Y, withNull.Rects[i].Y);
        }
    }

    /// <summary>
    ///     An edge count for a pair that the un-widened wrap decision splits across two rows never
    ///     widens anything: the pair is no longer same-row, so placement matches the no-counts output.
    /// </summary>
    [Fact]
    public void Pack_DifferentRowPair_Unaffected()
    {
        // Arrange: two 80-wide items with only 100 content width, so the second wraps to a new row
        var items = new[] { new PackItem(80, 20), new PackItem(80, 20) };
        var edgeCounts = new Dictionary<(int First, int Second), int> { [(0, 1)] = 8 };

        // Act: the (0, 1) pair spans two rows, so its count must not apply
        var widened = ContainmentPacker.Pack(
            items, maxContentWidth: 100, horizontalGap: 5, verticalGap: 5, padding: 10, edgeCounts);
        var plain = ContainmentPacker.Pack(
            items, maxContentWidth: 100, horizontalGap: 5, verticalGap: 5, padding: 10);

        // Assert: the wrapped item starts at the left origin in both cases, unmoved by the edge count
        Assert.True(widened.Rects[1].Y > widened.Rects[0].Y);
        Assert.Equal(plain.Rects[1].X, widened.Rects[1].X);
        Assert.Equal(plain.Rects[1].Y, widened.Rects[1].Y);
        Assert.Equal(plain.Width, widened.Width);
    }

    /// <summary>
    ///     Determines whether two rectangles overlap with a positive-area intersection.
    /// </summary>
    private static bool Overlaps(PackedRect a, PackedRect b)
    {
        return a.X < b.X + b.Width &&
               b.X < a.X + a.Width &&
               a.Y < b.Y + b.Height &&
               b.Y < a.Y + a.Height;
    }
}
