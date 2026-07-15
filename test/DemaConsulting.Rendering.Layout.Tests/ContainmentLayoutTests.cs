// <copyright file="ContainmentLayoutTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Layout;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests for <see cref="ContainmentLayout"/> public containment-packing of model boxes.
/// </summary>
public sealed class ContainmentLayoutTests
{
    /// <summary>
    ///     Creates a plain rectangular <see cref="LayoutBox"/> of the given size (placed at the origin,
    ///     which the packer overwrites) with no compartments or children.
    /// </summary>
    private static LayoutBox Box(double width, double height, string? label = null) =>
        new(0, 0, width, height, label, 0, BoxShape.Rectangle, [], []);

    /// <summary>
    ///     The packed children are returned in the same order they were supplied, laid out left to right
    ///     along a row when they all fit the content width.
    /// </summary>
    [Fact]
    public void Pack_ItemsFitInRow_PreservesOrderLeftToRight()
    {
        // Arrange: three labelled boxes that together fit within a 300px content width
        var children = new[] { Box(60, 40, "first"), Box(60, 40, "second"), Box(60, 40, "third") };

        // Act: pack them into a single row
        var result = ContainmentLayout.Pack(children, new ContainmentOptions(MaxContentWidth: 300));

        // Assert: order is preserved and X increases strictly left to right on a shared row
        Assert.Equal(["first", "second", "third"], result.Children.Select(c => c.Label));
        Assert.True(result.Children[0].X < result.Children[1].X);
        Assert.True(result.Children[1].X < result.Children[2].X);
        Assert.Equal(result.Children[0].Y, result.Children[1].Y);
        Assert.Equal(result.Children[1].Y, result.Children[2].Y);
    }

    /// <summary>
    ///     For a mixed-size set that spans multiple rows, no two packed children overlap.
    /// </summary>
    [Fact]
    public void Pack_MixedSizes_ProducesNoOverlaps()
    {
        // Arrange: a varied mix of sizes that forces several rows within the width budget
        var children = new[]
        {
            Box(60, 30), Box(120, 20), Box(40, 50),
            Box(90, 25), Box(70, 40), Box(50, 30),
            Box(110, 35), Box(30, 20),
        };

        // Act
        var result = ContainmentLayout.Pack(children, new ContainmentOptions(MaxContentWidth: 250));

        // Assert: every pair of placed boxes is disjoint
        for (var i = 0; i < result.Children.Count; i++)
        {
            for (var j = i + 1; j < result.Children.Count; j++)
            {
                Assert.False(Overlaps(result.Children[i], result.Children[j]),
                    $"Boxes {i} and {j} overlap.");
            }
        }
    }

    /// <summary>
    ///     Every packed child lies fully within the reported region bounds.
    /// </summary>
    [Fact]
    public void Pack_MixedSizes_AllChildrenWithinRegion()
    {
        // Arrange: a mix of sizes
        var children = new[]
        {
            Box(60, 30), Box(120, 20), Box(40, 50),
            Box(90, 25), Box(70, 40),
        };

        // Act
        var result = ContainmentLayout.Pack(children, new ContainmentOptions(MaxContentWidth: 200));

        // Assert: each box is contained within [0, Width] x [0, Height]
        foreach (var child in result.Children)
        {
            Assert.True(child.X >= 0);
            Assert.True(child.Y >= 0);
            Assert.True(child.X + child.Width <= result.Width + 1e-9);
            Assert.True(child.Y + child.Height <= result.Height + 1e-9);
        }
    }

    /// <summary>
    ///     A child that would exceed the maximum content width wraps onto a new row below the current one.
    /// </summary>
    [Fact]
    public void Pack_ChildExceedsWidth_WrapsToNewRow()
    {
        // Arrange: three 80-wide boxes; a 200px content width fits only two per row
        var children = new[] { Box(80, 20), Box(80, 20), Box(80, 20) };

        // Act
        var result = ContainmentLayout.Pack(children, new ContainmentOptions(MaxContentWidth: 200));

        // Assert: the first two share a row and the third wraps to a lower row at the left origin
        Assert.Equal(result.Children[0].Y, result.Children[1].Y);
        Assert.True(result.Children[2].Y > result.Children[0].Y);
        Assert.Equal(result.Children[0].X, result.Children[2].X);
    }

    /// <summary>
    ///     A child wider than the content width is placed alone and the region widens to contain it.
    /// </summary>
    [Fact]
    public void Pack_OversizedChild_PlacedAloneAndRegionWidens()
    {
        // Arrange: a 300-wide box with only 100px of content width available
        var children = new[] { Box(50, 20), Box(300, 20) };

        // Act
        var result = ContainmentLayout.Pack(children, new ContainmentOptions(MaxContentWidth: 100));

        // Assert: the oversized box wrapped to its own row and the region widened to enclose it
        Assert.True(result.Children[1].Y > result.Children[0].Y);
        Assert.True(result.Width >= 300.0 + result.Children[1].X);
        Assert.True(result.Children[1].X + result.Children[1].Width <= result.Width + 1e-9);
    }

    /// <summary>
    ///     Packing no children yields a region consisting solely of padding, with no children returned.
    /// </summary>
    [Fact]
    public void Pack_EmptyInput_ReturnsPaddingOnlyRegion()
    {
        // Act: pack an empty list with a padding of 15
        var result = ContainmentLayout.Pack([], new ContainmentOptions(MaxContentWidth: 100, Padding: 15));

        // Assert: no children and a region of exactly 2*padding on each axis
        Assert.Empty(result.Children);
        Assert.Equal(30.0, result.Width);
        Assert.Equal(30.0, result.Height);
    }

    /// <summary>
    ///     Non-position fields of each child (label, depth, shape, compartments, children, keyword) survive
    ///     the pack unchanged; only X and Y are updated.
    /// </summary>
    [Fact]
    public void Pack_PreservesNonPositionFields()
    {
        // Arrange: a richly populated box with a nested child and compartments
        var nested = Box(10, 10, "inner");
        var compartments = new[] { new LayoutCompartment("attributes", ["speed: Real"]) };
        var original = new LayoutBox(
            999, 999, 90, 50, "Engine", 3, BoxShape.Folder, compartments, new[] { nested }, "part def");

        // Act
        var result = ContainmentLayout.Pack(new[] { original }, new ContainmentOptions(MaxContentWidth: 200, Padding: 12));

        // Assert: position was updated to the padding origin while every other field is unchanged
        var placed = Assert.Single(result.Children);
        Assert.Equal(12.0, placed.X);
        Assert.Equal(12.0, placed.Y);
        Assert.Equal(90, placed.Width);
        Assert.Equal(50, placed.Height);
        Assert.Equal("Engine", placed.Label);
        Assert.Equal(3, placed.Depth);
        Assert.Equal(BoxShape.Folder, placed.Shape);
        Assert.Same(compartments, placed.Compartments);
        Assert.Equal("part def", placed.Keyword);
        Assert.Same(nested, Assert.Single(placed.Children));
    }

    /// <summary>
    ///     The options record supplies sensible gap and padding defaults so a caller need only specify the
    ///     wrap width.
    /// </summary>
    [Fact]
    public void ContainmentOptions_Defaults_AreSensibleGapsAndPadding()
    {
        // Act: construct options specifying only the required content width
        var options = new ContainmentOptions(MaxContentWidth: 200);

        // Assert: the documented defaults apply
        Assert.Equal(8.0, options.HorizontalGap);
        Assert.Equal(8.0, options.VerticalGap);
        Assert.Equal(12.0, options.Padding);
    }

    /// <summary>
    ///     A null children list is rejected with an argument-null error.
    /// </summary>
    [Fact]
    public void Pack_NullChildren_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(
            () => ContainmentLayout.Pack(null!, new ContainmentOptions(MaxContentWidth: 100)));
    }

    /// <summary>
    ///     A null options argument is rejected with an argument-null error.
    /// </summary>
    [Fact]
    public void Pack_NullOptions_Throws()
    {
        // Arrange
        var children = new[] { Box(60, 40) };

        // Act / Assert
        Assert.Throws<ArgumentNullException>(
            () => ContainmentLayout.Pack(children, null!));
    }

    /// <summary>
    ///     A null element within the children list is rejected with an argument-null error.
    /// </summary>
    [Fact]
    public void Pack_NullChildElement_Throws()
    {
        // Arrange: a list containing a null box
        var children = new LayoutBox[] { Box(60, 40), null! };

        // Act / Assert
        Assert.Throws<ArgumentNullException>(
            () => ContainmentLayout.Pack(children, new ContainmentOptions(MaxContentWidth: 100)));
    }

    /// <summary>
    ///     Supplying an <see cref="ContainmentOptions.EdgeCounts"/> entry for an adjacent same-row pair
    ///     widens exactly that horizontal gap to the connector-corridor width, pushing the second box
    ///     right while leaving the first at the origin.
    /// </summary>
    [Fact]
    public void Pack_EdgeCounts_WidensIndicatedRowGap()
    {
        // Arrange: two boxes that share a row, with eight connectors between the (0, 1) pair
        var children = new[] { Box(60, 40, "first"), Box(60, 40, "second") };
        var edgeCounts = new Dictionary<(int First, int Second), int> { [(0, 1)] = 8 };

        // Act
        var result = ContainmentLayout.Pack(
            children, new ContainmentOptions(MaxContentWidth: 400, EdgeCounts: edgeCounts));

        // Assert: the pair stays on one row and the gap between them equals the corridor width
        // (2*10 + 7*16 = 132) rather than the default 8px horizontal gap.
        Assert.Equal(result.Children[0].Y, result.Children[1].Y);
        var gap = result.Children[1].X - (result.Children[0].X + result.Children[0].Width);
        Assert.Equal(132.0, gap);
    }

    /// <summary>
    ///     Omitting <see cref="ContainmentOptions.EdgeCounts"/> leaves placement at the default
    ///     horizontal gap, byte-identical to the pre-existing no-counts behaviour.
    /// </summary>
    [Fact]
    public void Pack_WithoutEdgeCounts_UsesDefaultGap()
    {
        // Arrange: the same two boxes, but no edge counts supplied
        var children = new[] { Box(60, 40, "first"), Box(60, 40, "second") };

        // Act
        var result = ContainmentLayout.Pack(children, new ContainmentOptions(MaxContentWidth: 400));

        // Assert: the boxes are separated only by the default 8px horizontal gap
        var gap = result.Children[1].X - (result.Children[0].X + result.Children[0].Width);
        Assert.Equal(8.0, gap);
    }

    /// <summary>
    ///     Determines whether two boxes overlap with a positive-area intersection.
    /// </summary>
    private static bool Overlaps(LayoutBox a, LayoutBox b)
    {
        return a.X < b.X + b.Width &&
               b.X < a.X + a.Width &&
               a.Y < b.Y + b.Height &&
               b.Y < a.Y + a.Height;
    }
}
