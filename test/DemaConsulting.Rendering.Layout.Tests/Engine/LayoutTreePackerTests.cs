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

    /// <summary>
    ///     Proves free-space packing fills the hole beside a tall piece: a tall piece followed by small ones
    ///     that the shelf packer would wrap onto a row below it is arranged in a canvas strictly smaller than
    ///     the shelf canvas, and the small pieces sit beside the tall one.
    /// </summary>
    [Fact]
    public void Arrange_SmallPiecesAfterTallPiece_FillsHoleBesideTallPiece()
    {
        // Arrange: shelf (aspect 1) puts the tall piece alone on shelf one, the small pieces below it
        (double, double)[] sizes = [(100, 300), (100, 60), (100, 60), (100, 60)];

        // Act
        var arrangement = LayoutTreePacker.Arrange(sizes, 10.0, 1.0);

        // Assert: smaller than the shelf arrangement, and some small piece starts beside the tall one
        Assert.True(arrangement.Area < LayoutTreePacker.ArrangeShelf(sizes, 10.0, 1.0).Area - 1e-6, $"area {arrangement.Area}");
        Assert.Contains(Enumerable.Range(1, 3), index => arrangement.X[index] >= 110.0 - 1e-9 && arrangement.Y[index] < 300.0);
        AssertNoOverlap(sizes, arrangement, 10.0);
    }

    /// <summary>
    ///     Proves the arrangement is never a larger canvas than the shelf arrangement, over a range of
    ///     piece mixes and aspect ratios, and never overlaps or breaks the spacing.
    /// </summary>
    /// <param name="seed">The seed of the pseudo-random piece sizes.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Arrange_RandomPieces_NeverLargerThanShelfAndNeverOverlaps(int seed)
    {
        // Arrange
        var random = new Random(seed);
        var sizes = Enumerable.Range(0, 3 + (seed * 2))
            .Select(_ => ((double)random.Next(30, 260), (double)random.Next(30, 320)))
            .ToArray();

        foreach (var aspect in new[] { 1.0, 4.0 / 3.0, 3.0 })
        {
            // Act
            var arrangement = LayoutTreePacker.Arrange(sizes, 24.0, aspect);
            var shelf = LayoutTreePacker.ArrangeShelf(sizes, 24.0, aspect);

            // Assert
            Assert.True(arrangement.Area <= shelf.Area + 1e-6, $"seed {seed} aspect {aspect}");
            AssertNoOverlap(sizes, arrangement, 24.0);
        }
    }

    /// <summary>Proves the arrangement is deterministic: the same input twice gives identical offsets.</summary>
    [Fact]
    public void Arrange_SameInputTwice_IsIdentical()
    {
        // Arrange
        (double, double)[] sizes = [(100, 300), (100, 60), (140, 60), (80, 90), (200, 40)];

        // Act
        var first = LayoutTreePacker.Arrange(sizes, 10.0, 4.0 / 3.0);
        var second = LayoutTreePacker.Arrange(sizes, 10.0, 4.0 / 3.0);

        // Assert
        Assert.Equal(first.X, second.X);
        Assert.Equal(first.Y, second.Y);
        Assert.Equal(first.Width, second.Width);
        Assert.Equal(first.Height, second.Height);
    }

    /// <summary>
    ///     Proves that when no free-space arrangement has a strictly smaller canvas, the shelf arrangement is
    ///     returned exactly: two pieces on one shelf keep their shelf offsets.
    /// </summary>
    [Fact]
    public void Arrange_NoGain_KeepsShelfArrangement()
    {
        // Arrange
        (double, double)[] sizes = [(100, 50), (60, 30)];

        // Act
        var arrangement = LayoutTreePacker.Arrange(sizes, 10.0, 100.0);

        // Assert: the shelf arrangement — both on the first row
        Assert.Equal([0.0, 110.0], arrangement.X);
        Assert.Equal([0.0, 0.0], arrangement.Y);
        Assert.Equal(170.0, arrangement.Width);
        Assert.Equal(50.0, arrangement.Height);
    }

    /// <summary>
    ///     Proves a free-space arrangement whose aspect-penalized score ties the shelf score is accepted when
    ///     its canvas area is strictly smaller ("score no worse"): a 400-wide piece fixes the canvas width of
    ///     both arrangements, so a wide canvas scores width²/2 either way, yet the free-space packing is
    ///     shorter.
    /// </summary>
    [Fact]
    public void Arrange_TiedScoreWithSmallerArea_ChoosesFreeSpace()
    {
        // Arrange: shelf (aspect 1) gives 400 x 170 (rows: 400x20 / 150x100+150x30 / 150x30+150x30); the
        // free-space packing stacks the three small pieces beside the tall one: 400 x 140
        (double, double)[] sizes = [(400, 20), (150, 100), (150, 30), (150, 30), (150, 30)];
        var shelf = LayoutTreePacker.ArrangeShelf(sizes, 10.0, 1.0);

        // Act
        var arrangement = LayoutTreePacker.Arrange(sizes, 10.0, 1.0);

        // Assert: both canvases are wider than 2:1 with equal width (equal penalized score), and the
        // free-space canvas is strictly smaller
        Assert.Equal(shelf.Width, arrangement.Width, 6);
        Assert.True(shelf.Width / shelf.Height > 2.0);
        Assert.True(arrangement.Width / arrangement.Height > 2.0);
        Assert.True(arrangement.Area < shelf.Area - 1e-6, $"area {arrangement.Area} vs shelf {shelf.Area}");
        AssertNoOverlap(sizes, arrangement, 10.0);
    }

    /// <summary>
    ///     Proves a free-space arrangement that ties the shelf in both score and area is not taken: equal
    ///     canvases keep the shelf offsets exactly.
    /// </summary>
    [Fact]
    public void Arrange_TiedScoreAndArea_KeepsShelfArrangement()
    {
        // Arrange: two equal pieces side by side; any free-space packing is the same canvas
        (double, double)[] sizes = [(400, 20), (400, 20)];
        var shelf = LayoutTreePacker.ArrangeShelf(sizes, 10.0, 1.0);

        // Act
        var arrangement = LayoutTreePacker.Arrange(sizes, 10.0, 1.0);

        // Assert
        Assert.Equal(shelf.X, arrangement.X);
        Assert.Equal(shelf.Y, arrangement.Y);
        Assert.Equal(shelf.Width, arrangement.Width);
        Assert.Equal(shelf.Height, arrangement.Height);
    }

    /// <summary>Proves empty and single-piece inputs are unchanged.</summary>
    [Fact]
    public void Arrange_EmptyAndSingle_AreUnchanged()
    {
        // Act
        var empty = LayoutTreePacker.Arrange([], 10.0, 1.0);
        var single = LayoutTreePacker.Arrange([(80.0, 40.0)], 10.0, 1.0);

        // Assert
        Assert.Empty(empty.X);
        Assert.Equal(0.0, empty.Area);
        Assert.Equal([0.0], single.X);
        Assert.Equal([0.0], single.Y);
        Assert.Equal(80.0, single.Width);
        Assert.Equal(40.0, single.Height);
    }

    /// <summary>
    ///     Proves <c>Pack</c> uses the free-space arrangement: the small trees land beside the tall one and
    ///     every node is translated with its tree.
    /// </summary>
    [Fact]
    public void Pack_TallTreePlusSmallTrees_TucksSmallTreesBesideTallTree()
    {
        // Arrange
        var trees = new List<LayoutTree>
        {
            new(100, 300, [new LayoutBox(0, 0, 100, 300, "tall", 0, BoxShape.Rectangle, [], [])]),
            new(100, 60, [new LayoutBox(0, 0, 100, 60, "s1", 0, BoxShape.Rectangle, [], [])]),
            new(100, 60, [new LayoutBox(0, 0, 100, 60, "s2", 0, BoxShape.Rectangle, [], [])]),
            new(100, 60, [new LayoutBox(0, 0, 100, 60, "s3", 0, BoxShape.Rectangle, [], [])]),
        };

        // Act
        var packed = LayoutTreePacker.Pack(trees, 10.0, 1.0);

        // Assert
        var boxes = packed.Nodes.OfType<LayoutBox>().ToDictionary(box => box.Label!);
        Assert.Equal(["tall", "s1", "s2", "s3"], packed.Nodes.OfType<LayoutBox>().Select(box => box.Label));
        Assert.True(packed.Width * packed.Height < 210.0 * 370.0);
        Assert.True(boxes["s1"].X >= boxes["tall"].X + boxes["tall"].Width + 10.0 - 1e-9 || boxes["s1"].Y >= 310.0 - 1e-9);
    }

    /// <summary>
    ///     Proves <c>TuckIntoHoles</c> places a piece into an empty region of the host canvas, leaving the host
    ///     canvas unchanged, keeping the spacing from the host content and the margin from the canvas edge.
    /// </summary>
    [Fact]
    public void TuckIntoHoles_PieceFitsBesideHostContent_PlacesInsideCanvasKeepingSpacing()
    {
        // Arrange: the host box fills the right part of a 400 x 300 canvas, a 100 x 50 piece fits left of it
        var host = new LayoutTree(400, 300, [new LayoutBox(220, 20, 160, 260, "H", 0, BoxShape.Rectangle, [], [])]);
        var piece = new LayoutTree(100, 50, [new LayoutBox(0, 0, 100, 50, "P", 0, BoxShape.Rectangle, [], [])]);

        // Act
        var tucked = LayoutTreePacker.TuckIntoHoles(host, [piece], 24.0, 4.0 / 3.0, 20.0, 12.0);

        // Assert
        Assert.NotNull(tucked);
        Assert.Equal(400.0, tucked.Width);
        Assert.Equal(300.0, tucked.Height);
        var placed = tucked.Nodes.OfType<LayoutBox>().Single(box => box.Label == "P");
        Assert.True(placed.X >= 20.0 - 1e-9 && placed.Y >= 20.0 - 1e-9);
        Assert.True(placed.X + placed.Width <= 220.0 - 24.0 + 1e-9, $"too close to the host box: right edge {placed.X + placed.Width}");
        Assert.True(placed.Y + placed.Height <= 280.0 + 1e-9);
    }

    /// <summary>Proves <c>TuckIntoHoles</c> returns <see langword="null"/> when the piece fits nowhere.</summary>
    [Fact]
    public void TuckIntoHoles_PieceFitsNowhere_ReturnsNull()
    {
        // Arrange: the host content fills the canvas
        var host = new LayoutTree(200, 100, [new LayoutBox(20, 20, 160, 60, "H", 0, BoxShape.Rectangle, [], [])]);
        var piece = new LayoutTree(100, 50, [new LayoutBox(0, 0, 100, 50, "P", 0, BoxShape.Rectangle, [], [])]);

        // Act / Assert
        Assert.Null(LayoutTreePacker.TuckIntoHoles(host, [piece], 24.0, 4.0 / 3.0, 20.0, 12.0));
    }

    /// <summary>Proves <c>TuckIntoHoles</c> with no pieces returns <see langword="null"/>.</summary>
    [Fact]
    public void TuckIntoHoles_NoPieces_ReturnsNull()
    {
        // Arrange
        var host = new LayoutTree(200, 100, []);

        // Act / Assert
        Assert.Null(LayoutTreePacker.TuckIntoHoles(host, [], 24.0, 4.0 / 3.0, 20.0, 12.0));
    }

    /// <summary>Proves the outer margin of <c>Pack</c> offsets every node and widens the canvas on both sides.</summary>
    [Fact]
    public void Pack_WithMargin_OffsetsContentAndGrowsCanvasOnEverySide()
    {
        // Arrange
        var a = new LayoutTree(100, 50, [new LayoutBox(0, 0, 100, 50, "A", 0, BoxShape.Rectangle, [], [])]);
        var b = new LayoutTree(60, 30, [new LayoutBox(0, 0, 60, 30, "B", 0, BoxShape.Rectangle, [], [])]);

        // Act
        var plain = LayoutTreePacker.Pack([a, b], 10.0, 100.0);
        var margined = LayoutTreePacker.Pack([a, b], 10.0, 100.0, margin: 20.0);

        // Assert
        Assert.Equal(plain.Width + 40.0, margined.Width, 6);
        Assert.Equal(plain.Height + 40.0, margined.Height, 6);
        var boxA = margined.Nodes.OfType<LayoutBox>().Single(box => box.Label == "A");
        Assert.Equal(20.0, boxA.X, 6);
        Assert.Equal(20.0, boxA.Y, 6);
    }

    /// <summary>Asserts no two pieces overlap or come closer than the spacing, and that all are inside the canvas.</summary>
    private static void AssertNoOverlap(IReadOnlyList<(double Width, double Height)> sizes, LayoutTreePacker.Arrangement arrangement, double spacing)
    {
        for (var i = 0; i < sizes.Count; i++)
        {
            Assert.True(arrangement.X[i] >= -1e-9 && arrangement.Y[i] >= -1e-9);
            Assert.True(arrangement.X[i] + sizes[i].Width <= arrangement.Width + 1e-6);
            Assert.True(arrangement.Y[i] + sizes[i].Height <= arrangement.Height + 1e-6);
            for (var j = i + 1; j < sizes.Count; j++)
            {
                var apartX = arrangement.X[i] + sizes[i].Width + spacing <= arrangement.X[j] + 1e-6
                    || arrangement.X[j] + sizes[j].Width + spacing <= arrangement.X[i] + 1e-6;
                var apartY = arrangement.Y[i] + sizes[i].Height + spacing <= arrangement.Y[j] + 1e-6
                    || arrangement.Y[j] + sizes[j].Height + spacing <= arrangement.Y[i] + 1e-6;
                Assert.True(apartX || apartY, $"pieces {i} and {j} overlap or are closer than the spacing");
            }
        }
    }

    /// <summary>A minimal <see cref="LayoutNode"/> stub outside the packer's known, closed set.</summary>
    private sealed record UnrecognizedLayoutNode : LayoutNode;
}
