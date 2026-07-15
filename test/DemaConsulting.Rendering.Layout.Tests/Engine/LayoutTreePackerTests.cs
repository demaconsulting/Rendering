// <copyright file="LayoutTreePackerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Layout.Engine;

namespace DemaConsulting.Rendering.Layout.Tests.Engine;

/// <summary>
///     Tests for <see cref="LayoutTreePacker"/>, the shelf-packer that merges several independently
///     placed <see cref="LayoutTree"/>s into one combined tree.
/// </summary>
public sealed class LayoutTreePackerTests
{
    /// <summary>
    ///     Proves that <c>Pack</c> rejects a <see langword="null"/> <c>trees</c> argument.
    /// </summary>
    [Fact]
    public void Pack_NullTrees_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => LayoutTreePacker.Pack(null!, 10.0, 4.0 / 3.0));
    }

    /// <summary>
    ///     Proves that packing an empty list of trees yields a degenerate, empty tree rather than
    ///     throwing.
    /// </summary>
    [Fact]
    public void Pack_EmptyList_ReturnsEmptyTree()
    {
        // Act
        var packed = LayoutTreePacker.Pack([], 10.0, 4.0 / 3.0);

        // Assert
        Assert.Empty(packed.Nodes);
        Assert.Equal(0.0, packed.Width);
        Assert.Equal(0.0, packed.Height);
    }

    /// <summary>
    ///     Proves that packing a single tree preserves its content (same width/height, and equivalent
    ///     nodes translated by a no-op (0, 0) offset) rather than returning a different shape — the
    ///     coordinates are unchanged, but the tree instance itself is rebuilt so its node types are
    ///     still validated against the packer's closed set (see
    ///     <see cref="Pack_SingleTreeWithUnrecognizedNodeType_ThrowsNotSupportedException"/>).
    /// </summary>
    [Fact]
    public void Pack_SingleTree_ReturnsItUnchanged()
    {
        // Arrange
        var box = new LayoutBox(5, 5, 80, 40, "A", 0, BoxShape.Rectangle, [], []);
        var tree = new LayoutTree(90, 50, [box]);

        // Act
        var packed = LayoutTreePacker.Pack([tree], 10.0, 4.0 / 3.0);

        // Assert
        Assert.Equal(tree.Width, packed.Width);
        Assert.Equal(tree.Height, packed.Height);
        var packedBox = Assert.Single(packed.Nodes.OfType<LayoutBox>());
        Assert.Equal(box.X, packedBox.X);
        Assert.Equal(box.Y, packedBox.Y);
        Assert.Equal(box.Label, packedBox.Label);
    }

    /// <summary>
    ///     Proves that packing two trees places the second one shifted to the right of the first (no
    ///     overlap), and that the combined canvas encloses both.
    /// </summary>
    [Fact]
    public void Pack_TwoTrees_PlacesSecondBesideFirstWithoutOverlap()
    {
        // Arrange
        var boxA = new LayoutBox(0, 0, 100, 50, "A", 0, BoxShape.Rectangle, [], []);
        var treeA = new LayoutTree(100, 50, [boxA]);
        var boxB = new LayoutBox(0, 0, 60, 30, "B", 0, BoxShape.Rectangle, [], []);
        var treeB = new LayoutTree(60, 30, [boxB]);

        // Act
        var packed = LayoutTreePacker.Pack([treeA, treeB], 10.0, 100.0);

        // Assert: both boxes present, translated, and non-overlapping.
        var boxes = packed.Nodes.OfType<LayoutBox>().ToList();
        Assert.Equal(2, boxes.Count);
        var packedA = boxes.Single(b => b.Label == "A");
        var packedB = boxes.Single(b => b.Label == "B");

        // Same-shelf placement (wide aspect target): B sits to the right of A with the requested gap.
        Assert.Equal(0.0, packedA.X);
        Assert.Equal(0.0, packedA.Y);
        Assert.Equal(packedA.X + packedA.Width + 10.0, packedB.X);
        Assert.Equal(0.0, packedB.Y);

        // The combined canvas encloses both boxes.
        Assert.True(packed.Width >= packedB.X + packedB.Width);
        Assert.True(packed.Height >= Math.Max(packedA.Height, packedB.Height));
    }

    /// <summary>
    ///     Proves that a wide sub-tree that would overflow the target row width still gets its own
    ///     shelf beneath the previous row, rather than overlapping it.
    /// </summary>
    [Fact]
    public void Pack_ManyTrees_WrapsOntoNewShelfWithoutOverlap()
    {
        // Arrange: five identical square trees and a tight aspect ratio, forcing shelf wrapping.
        var trees = Enumerable.Range(0, 5)
            .Select(i =>
            {
                var box = new LayoutBox(0, 0, 50, 50, $"n{i}", 0, BoxShape.Rectangle, [], []);
                return new LayoutTree(50, 50, [box]);
            })
            .ToList();

        // Act
        var packed = LayoutTreePacker.Pack(trees, 5.0, 1.0);

        // Assert: every box lies within the canvas and no two boxes overlap.
        var boxes = packed.Nodes.OfType<LayoutBox>().ToList();
        Assert.Equal(5, boxes.Count);
        foreach (var box in boxes)
        {
            Assert.True(box.X >= 0);
            Assert.True(box.Y >= 0);
            Assert.True(box.X + box.Width <= packed.Width + 1e-9);
            Assert.True(box.Y + box.Height <= packed.Height + 1e-9);
        }

        for (var i = 0; i < boxes.Count; i++)
        {
            for (var j = i + 1; j < boxes.Count; j++)
            {
                var overlapsX = boxes[i].X < boxes[j].X + boxes[j].Width && boxes[j].X < boxes[i].X + boxes[i].Width;
                var overlapsY = boxes[i].Y < boxes[j].Y + boxes[j].Height && boxes[j].Y < boxes[i].Y + boxes[i].Height;
                Assert.False(overlapsX && overlapsY);
            }
        }

        // At least two distinct rows were needed (shelf wrapping actually occurred).
        Assert.True(boxes.Select(b => b.Y).Distinct().Count() > 1);
    }

    /// <summary>
    ///     Proves that a box's nested <see cref="LayoutBox.Children"/> are translated recursively along
    ///     with their parent, keeping their relative position intact.
    /// </summary>
    [Fact]
    public void Pack_BoxWithNestedChildren_TranslatesChildrenRecursively()
    {
        // Arrange
        var child = new LayoutBox(12, 12, 40, 20, "child", 1, BoxShape.Rectangle, [], []);
        var boxA = new LayoutBox(0, 0, 100, 50, "A", 0, BoxShape.Rectangle, [], []);
        var treeA = new LayoutTree(100, 50, [boxA]);
        var boxB = new LayoutBox(0, 0, 80, 60, "B", 0, BoxShape.Rectangle, [], [child]);
        var treeB = new LayoutTree(80, 60, [boxB]);

        // Act
        var packed = LayoutTreePacker.Pack([treeA, treeB], 10.0, 100.0);

        // Assert: the packed B box's child is offset by the same amount as B itself, preserving the
        // original 12/12 relative offset from B's own origin.
        var packedB = packed.Nodes.OfType<LayoutBox>().Single(b => b.Label == "B");
        var packedChild = Assert.Single(packedB.Children.OfType<LayoutBox>());
        Assert.Equal(packedB.X + 12, packedChild.X);
        Assert.Equal(packedB.Y + 12, packedChild.Y);
    }

    /// <summary>
    ///     Proves that a <see cref="LayoutLine"/>'s waypoints are all translated by the same offset as
    ///     the sub-tree it belongs to.
    /// </summary>
    [Fact]
    public void Pack_TreeWithLine_TranslatesEveryWaypoint()
    {
        // Arrange
        var boxA = new LayoutBox(0, 0, 100, 50, "A", 0, BoxShape.Rectangle, [], []);
        var treeA = new LayoutTree(100, 50, [boxA]);
        var line = new LayoutLine(
            [new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 20)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            null);
        var boxB = new LayoutBox(0, 0, 60, 30, "B", 0, BoxShape.Rectangle, [], []);
        var treeB = new LayoutTree(60, 30, [boxB, line]);

        // Act
        var packed = LayoutTreePacker.Pack([treeA, treeB], 10.0, 100.0);

        // Assert: every waypoint of the line is shifted by the same offset the second tree received.
        var packedB = packed.Nodes.OfType<LayoutBox>().Single(b => b.Label == "B");
        var offsetX = packedB.X;
        var offsetY = packedB.Y;
        var packedLine = Assert.Single(packed.Nodes.OfType<LayoutLine>());
        Assert.Equal(
            [new Point2D(offsetX, offsetY), new Point2D(offsetX + 10, offsetY), new Point2D(offsetX + 10, offsetY + 20)],
            packedLine.Waypoints);
    }

    /// <summary>
    ///     Proves that a <see cref="LayoutPort"/>'s centre is translated by the same offset as its
    ///     owning sub-tree.
    /// </summary>
    [Fact]
    public void Pack_TreeWithPort_TranslatesCentre()
    {
        // Arrange
        var boxA = new LayoutBox(0, 0, 100, 50, "A", 0, BoxShape.Rectangle, [], []);
        var treeA = new LayoutTree(100, 50, [boxA]);
        var port = new LayoutPort(60, 15, PortSide.Right, "p");
        var boxB = new LayoutBox(0, 0, 60, 30, "B", 0, BoxShape.Rectangle, [], []);
        var treeB = new LayoutTree(60, 30, [boxB, port]);

        // Act
        var packed = LayoutTreePacker.Pack([treeA, treeB], 10.0, 100.0);

        // Assert
        var packedB = packed.Nodes.OfType<LayoutBox>().Single(b => b.Label == "B");
        var packedPort = Assert.Single(packed.Nodes.OfType<LayoutPort>());
        Assert.Equal(packedB.X + 60, packedPort.CentreX);
        Assert.Equal(packedB.Y + 15, packedPort.CentreY);
    }

    /// <summary>
    ///     Proves that warnings from every packed sub-tree are preserved in the combined tree.
    /// </summary>
    [Fact]
    public void Pack_TreesWithWarnings_PreservesAllWarnings()
    {
        // Arrange
        var boxA = new LayoutBox(0, 0, 100, 50, "A", 0, BoxShape.Rectangle, [], []);
        var treeA = new LayoutTree(100, 50, [boxA]) { Warnings = ["warning-a"] };
        var boxB = new LayoutBox(0, 0, 60, 30, "B", 0, BoxShape.Rectangle, [], []);
        var treeB = new LayoutTree(60, 30, [boxB]) { Warnings = ["warning-b1", "warning-b2"] };

        // Act
        var packed = LayoutTreePacker.Pack([treeA, treeB], 10.0, 100.0);

        // Assert
        Assert.Equal(["warning-a", "warning-b1", "warning-b2"], packed.Warnings);
    }

    /// <summary>
    ///     Proves that translating an unrecognized <see cref="LayoutNode"/> subtype throws
    ///     <see cref="NotSupportedException"/> rather than silently leaving it at the wrong position.
    /// </summary>
    [Fact]
    public void Pack_UnrecognizedNodeType_ThrowsNotSupportedException()
    {
        // Arrange
        var boxA = new LayoutBox(0, 0, 100, 50, "A", 0, BoxShape.Rectangle, [], []);
        var treeA = new LayoutTree(100, 50, [boxA]);
        var treeB = new LayoutTree(60, 30, [new UnrecognizedLayoutNode()]);

        // Act / Assert
        Assert.Throws<NotSupportedException>(() => LayoutTreePacker.Pack([treeA, treeB], 10.0, 100.0));
    }

    /// <summary>
    ///     Proves that a <em>singleton</em> tree containing an unrecognized <see cref="LayoutNode"/>
    ///     subtype also throws <see cref="NotSupportedException"/>: the single-tree fast path must not
    ///     bypass the same closed-set validation the multi-tree path enforces.
    /// </summary>
    [Fact]
    public void Pack_SingleTreeWithUnrecognizedNodeType_ThrowsNotSupportedException()
    {
        // Arrange
        var tree = new LayoutTree(60, 30, [new UnrecognizedLayoutNode()]);

        // Act / Assert
        Assert.Throws<NotSupportedException>(() => LayoutTreePacker.Pack([tree], 10.0, 100.0));
    }

    /// <summary>A minimal <see cref="LayoutNode"/> stub outside the packer's known, closed set.</summary>
    private sealed record UnrecognizedLayoutNode : LayoutNode;
}
