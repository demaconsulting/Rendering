// <copyright file="CanvasFitterTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout.Engine;

namespace DemaConsulting.Rendering.Layout.Tests.Engine;

/// <summary>
///     Tests for <see cref="CanvasFitter"/>: content already inside the canvas is left alone (the same
///     tree instance), a connector or label reaching past the origin translates the whole tree, and one
///     reaching past the far edges widens the canvas.
/// </summary>
public sealed class CanvasFitterTests
{
    /// <summary>The body font size the layout assumes.</summary>
    private const double FontSize = 12.0;

    /// <summary>A tree whose connectors and labels are all inside its canvas is returned unchanged.</summary>
    [Fact]
    public void CanvasFitter_Fit_ContentInsideCanvas_ReturnsSameTree()
    {
        // Arrange
        var tree = TwoBoxTree(waypointsX: 100.0, label: null);

        // Act
        var fitted = CanvasFitter.Fit(tree, FontSize);

        // Assert
        Assert.Same(tree, fitted);
    }

    /// <summary>
    ///     A connector bend at a negative X translates every node right so the bend is a margin inside the
    ///     canvas, and the canvas widens by the same amount; the boxes keep their relative positions.
    /// </summary>
    [Fact]
    public void CanvasFitter_Fit_WaypointLeftOfOrigin_TranslatesTreeAndWidensCanvas()
    {
        // Arrange: the connector detours to x = -16.
        var tree = TwoBoxTree(waypointsX: -16.0, label: null);

        // Act
        var fitted = CanvasFitter.Fit(tree, FontSize);

        // Assert
        var shift = CanvasFitter.FitMargin + 16.0;
        Assert.Equal(tree.Width + shift, fitted.Width, 6);
        Assert.Equal(tree.Height, fitted.Height, 6);
        var boxes = fitted.Nodes.OfType<LayoutBox>().ToList();
        Assert.Equal(20.0 + shift, boxes[0].X, 6);
        Assert.Equal(220.0 + shift, boxes[1].X, 6);
        var line = fitted.Nodes.OfType<LayoutLine>().Single();
        Assert.Equal(CanvasFitter.FitMargin, line.Waypoints.Min(p => p.X), 6);
        LayoutGeometryAssert.AssertWithinCanvas(fitted);
    }

    /// <summary>
    ///     A label centered on a vertical run near the left edge extends past the origin; the fitter brings it
    ///     fully inside the canvas.
    /// </summary>
    [Fact]
    public void CanvasFitter_Fit_LabelLeftOfOrigin_BringsLabelInsideCanvas()
    {
        // Arrange: a vertical run at x = 10 with a wide keyword label centered on it.
        var line = new LayoutLine(
            [new Point2D(10, 20), new Point2D(10, 300)],
            EndMarkerStyle.None,
            EndMarkerStyle.OpenChevron,
            LineStyle.Dashed,
            "«import»*");
        var tree = new LayoutTree(200, 320, [line]);
        Assert.Contains(LayoutGeometryAssert.LabelRects(tree), l => l.Left < 0.0);

        // Act
        var fitted = CanvasFitter.Fit(tree, FontSize);

        // Assert
        Assert.NotSame(tree, fitted);
        Assert.Contains(LayoutGeometryAssert.LabelRects(fitted), l => l.Text == "«import»*");
        LayoutGeometryAssert.AssertWithinCanvas(fitted);
    }

    /// <summary>A connector running past the declared width and height widens and heightens the canvas.</summary>
    [Fact]
    public void CanvasFitter_Fit_WaypointBeyondFarEdges_GrowsCanvasWithoutTranslating()
    {
        // Arrange: the canvas is 300 x 100 but the connector reaches (340, 130).
        var line = new LayoutLine(
            [new Point2D(120, 50), new Point2D(340, 50), new Point2D(340, 130)],
            EndMarkerStyle.None,
            EndMarkerStyle.OpenChevron,
            LineStyle.Solid,
            null);
        var tree = new LayoutTree(300, 100, [Box(20, 20, "A"), line]);

        // Act
        var fitted = CanvasFitter.Fit(tree, FontSize);

        // Assert
        Assert.Equal(340 + CanvasFitter.FitMargin, fitted.Width, 6);
        Assert.Equal(130 + CanvasFitter.FitMargin, fitted.Height, 6);
        Assert.Equal(20.0, fitted.Nodes.OfType<LayoutBox>().Single().X, 6);
    }

    /// <summary>
    ///     An outward port label hanging left of the origin (its port glyph is inside the canvas) translates
    ///     the tree so the label is inside, even though no connector or connector label is outside.
    /// </summary>
    [Fact]
    public void CanvasFitter_Fit_OutwardPortLabelLeftOfOrigin_TranslatesTreeToHoldLabel()
    {
        // Arrange: a port on the left face at x = 30 whose outward label extends far to its left.
        var port = new LayoutPort(
            30,
            50,
            PortSide.Left,
            "aVeryLongPortLabel : SomeType",
            Appearance: new PortAppearance(PortDirection.In, LabelPlacement: PortLabelPlacement.Outward));
        var line = new LayoutLine([new Point2D(30, 50), new Point2D(30, 150)], EndMarkerStyle.None, EndMarkerStyle.OpenChevron, LineStyle.Solid, null);
        var tree = new LayoutTree(300, 200, [Box(30, 20, "A"), line, port]);
        var label = NotationMetrics.PortOutwardLabelBounds(port, port.ExternalLabel!, Themes.Light);
        Assert.True(label.X - label.HalfWidth < 0.0, "scenario must put the port label left of the canvas");

        // Act
        var fitted = CanvasFitter.Fit(tree, FontSize);

        // Assert: the outward label and the glyph are now inside the canvas.
        var movedPort = fitted.Nodes.OfType<LayoutPort>().Single();
        var movedLabel = NotationMetrics.PortOutwardLabelBounds(movedPort, movedPort.ExternalLabel!, Themes.Light);
        Assert.Equal(CanvasFitter.FitMargin, movedLabel.X - movedLabel.HalfWidth, 6);
        var glyph = NotationMetrics.PortGlyphBounds(movedPort);
        Assert.True(glyph.X - glyph.HalfWidth >= 0.0 && glyph.X + glyph.HalfWidth <= fitted.Width);
    }

    /// <summary>A port glyph poking past the top-left corner brings the whole tree inside the canvas.</summary>
    [Fact]
    public void CanvasFitter_Fit_PortGlyphAboveOrigin_TranslatesTree()
    {
        // Arrange: a port glyph centered at y = 2 (half of it above the canvas) on a top face.
        var port = new LayoutPort(60, 2, PortSide.Top, null);
        var line = new LayoutLine([new Point2D(60, 2), new Point2D(60, 90)], EndMarkerStyle.None, EndMarkerStyle.None, LineStyle.Solid, null);
        var tree = new LayoutTree(200, 150, [Box(30, 2, "A"), line, port]);

        // Act
        var fitted = CanvasFitter.Fit(tree, FontSize);

        // Assert
        var glyph = NotationMetrics.PortGlyphBounds(fitted.Nodes.OfType<LayoutPort>().Single());
        Assert.True(glyph.Y - glyph.HalfHeight >= 0.0);
        Assert.NotSame(tree, fitted);
    }

    /// <summary>A tree holding a node type the packer cannot translate is returned unchanged.</summary>
    [Fact]
    public void CanvasFitter_Fit_UnrecognizedNode_ReturnsSameTree()
    {
        // Arrange
        var line = new LayoutLine([new Point2D(-20, 50), new Point2D(100, 50)], EndMarkerStyle.None, EndMarkerStyle.None, LineStyle.Solid, null);
        var tree = new LayoutTree(300, 100, [line, new UnknownNode()]);

        // Act
        var fitted = CanvasFitter.Fit(tree, FontSize);

        // Assert
        Assert.Same(tree, fitted);
    }

    /// <summary>A null tree is rejected.</summary>
    [Fact]
    public void CanvasFitter_Fit_NullTree_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => CanvasFitter.Fit(null!, FontSize));
    }

    /// <summary>A tree whose content already has exactly the padding on every side is returned unchanged.</summary>
    [Fact]
    public void CanvasFitter_FitWithPadding_ContentHuggedByPadding_ReturnsSameTree()
    {
        // Arrange: one 100 x 60 box with 20 px on every side
        var tree = new LayoutTree(140, 100, [Box(20, 20, "A")]);

        // Act / Assert
        Assert.Same(tree, CanvasFitter.Fit(tree, FontSize, 20.0));
    }

    /// <summary>
    ///     A canvas with a surplus ring around the content is shrunk, the content moved to one padding from
    ///     the origin; the content keeps its relative geometry.
    /// </summary>
    [Fact]
    public void CanvasFitter_FitWithPadding_SurplusRing_ShrinksCanvasAndTranslatesContent()
    {
        // Arrange: the box sits at (50, 70) in a 300 x 300 canvas
        var tree = new LayoutTree(300, 300, [Box(50, 70, "A")]);

        // Act
        var fitted = CanvasFitter.Fit(tree, FontSize, 20.0);

        // Assert
        Assert.Equal(140.0, fitted.Width, 6);
        Assert.Equal(100.0, fitted.Height, 6);
        var box = fitted.Nodes.OfType<LayoutBox>().Single();
        Assert.Equal(20.0, box.X, 6);
        Assert.Equal(20.0, box.Y, 6);
    }

    /// <summary>
    ///     Content that sits against the origin with a double margin at the far edge (what the reflection of a
    ///     Left or Up flow produces) is moved to one padding from every edge without changing the canvas size.
    /// </summary>
    [Fact]
    public void CanvasFitter_FitWithPadding_ContentOffCenter_CentersWithoutChangingCanvasSize()
    {
        // Arrange: margins 0 left/top and 40 right/bottom
        var tree = new LayoutTree(140, 100, [Box(0, 0, "A")]);

        // Act
        var fitted = CanvasFitter.Fit(tree, FontSize, 20.0);

        // Assert
        Assert.Equal(tree.Width, fitted.Width, 6);
        Assert.Equal(tree.Height, fitted.Height, 6);
        var box = fitted.Nodes.OfType<LayoutBox>().Single();
        Assert.Equal(20.0, box.X, 6);
        Assert.Equal(20.0, box.Y, 6);
    }

    /// <summary>
    ///     A connector label that reaches into the padding band (inside the canvas, so the plain fit leaves it
    ///     alone) is counted as content: the canvas keeps the padding beyond it.
    /// </summary>
    [Fact]
    public void CanvasFitter_FitWithPadding_LabelInsidePaddingBand_KeepsPaddingBeyondLabel()
    {
        // Arrange: a vertical run at x = 20 with a wide keyword label centered on it reaches left of x = 20
        var line = new LayoutLine(
            [new Point2D(20, 20), new Point2D(20, 300)],
            EndMarkerStyle.None,
            EndMarkerStyle.OpenChevron,
            LineStyle.Dashed,
            "«import»*");
        var tree = new LayoutTree(200, 320, [line]);

        // Act
        var fitted = CanvasFitter.Fit(tree, FontSize, 20.0);

        // Assert
        Assert.Equal(20.0, CanvasFitter.MeasureMargin(fitted, FontSize)!.Value, 1e-6);
    }

    /// <summary>The smallest margin of a tree is the nearest distance from its content to a canvas edge.</summary>
    [Fact]
    public void CanvasFitter_MeasureMargin_Boxes_ReturnsSmallestMargin()
    {
        // Arrange: margins 20 left/top, 40 right/bottom
        var tree = new LayoutTree(180, 140, [Box(20, 20, "A")]);

        // Act / Assert
        Assert.Equal(20.0, CanvasFitter.MeasureMargin(tree, FontSize)!.Value, 6);
        Assert.Null(CanvasFitter.MeasureMargin(new LayoutTree(50, 50, []), FontSize));
    }

    /// <summary>A tree holding a node type the packer cannot translate is returned unchanged by the padded fit too.</summary>
    [Fact]
    public void CanvasFitter_FitWithPadding_UnrecognizedNode_ReturnsSameTree()
    {
        // Arrange
        var tree = new LayoutTree(300, 100, [Box(0, 0, "A"), new UnknownNode()]);

        // Act / Assert
        Assert.Same(tree, CanvasFitter.Fit(tree, FontSize, 20.0));
    }

    /// <summary>
    ///     Trimming moves the content to the origin and sizes the canvas to exactly the content: the two boxes
    ///     of the fixture span 20..320 by 20..180, so the trimmed canvas is 300 by 160 with the first box at (0, 0).
    /// </summary>
    [Fact]
    public void CanvasFitter_TrimToContent_Boxes_StartsAtOriginAndHugsContent()
    {
        // Arrange
        var tree = TwoBoxTree(waypointsX: 100.0, label: null);

        // Act
        var trimmed = CanvasFitter.TrimToContent(tree, FontSize);

        // Assert
        Assert.Equal(300.0, trimmed.Width, 6);
        Assert.Equal(160.0, trimmed.Height, 6);
        var boxes = trimmed.Nodes.OfType<LayoutBox>().ToList();
        Assert.Equal(0.0, boxes[0].X, 6);
        Assert.Equal(0.0, boxes[0].Y, 6);
        Assert.Equal(200.0, boxes[1].X, 6);
        Assert.Equal(100.0, boxes[1].Y, 6);
        var line = trimmed.Nodes.OfType<LayoutLine>().Single();
        Assert.Equal(new Point2D(100, 30), line.Waypoints[0]);
    }

    /// <summary>A connector bend left of the boxes counts as content, and a bend inset keeps a wider margin from it.</summary>
    [Fact]
    public void CanvasFitter_TrimToContent_BendLeftOfBoxes_IsContentAndHonorsBendInset()
    {
        // Arrange: the connector detours to x = 2, left of the first box (x = 20)
        var tree = TwoBoxTree(waypointsX: 2.0, label: null);

        // Act
        var plain = CanvasFitter.TrimToContent(tree, FontSize);
        var inset = CanvasFitter.TrimToContent(tree, FontSize, bendInset: 12.0);

        // Assert: the bend is the leftmost content; the inset keeps 12 further out
        Assert.Equal(0.0, plain.Nodes.OfType<LayoutLine>().Single().Waypoints[1].X, 6);
        Assert.Equal(12.0, inset.Nodes.OfType<LayoutLine>().Single().Waypoints[1].X, 6);
        Assert.Equal(inset.Width, plain.Width + 12.0, 6);
    }

    /// <summary>A tree with no content is returned unchanged.</summary>
    [Fact]
    public void CanvasFitter_TrimToContent_EmptyTree_ReturnsSameTree()
    {
        // Arrange
        var tree = new LayoutTree(50, 50, []);

        // Act / Assert
        Assert.Same(tree, CanvasFitter.TrimToContent(tree, FontSize));
    }

    /// <summary>A tree already hugging its content at the origin is returned unchanged.</summary>
    [Fact]
    public void CanvasFitter_TrimToContent_AlreadyTrimmed_ReturnsSameTree()
    {
        // Arrange
        var tree = new LayoutTree(100, 60, [Box(0, 0, "A")]);

        // Act / Assert
        Assert.Same(tree, CanvasFitter.TrimToContent(tree, FontSize));
    }

    /// <summary>A null tree is rejected.</summary>
    [Fact]
    public void CanvasFitter_TrimToContent_NullTree_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => CanvasFitter.TrimToContent(null!, FontSize));
    }

    private static LayoutTree TwoBoxTree(double waypointsX, string? label)
    {
        var line = new LayoutLine(
            [new Point2D(120, 50), new Point2D(waypointsX, 50), new Point2D(waypointsX, 150), new Point2D(220, 150)],
            EndMarkerStyle.None,
            EndMarkerStyle.OpenChevron,
            LineStyle.Dashed,
            label);
        return new LayoutTree(360, 200, [Box(20, 20, "A"), Box(220, 120, "B"), line]);
    }

    private static LayoutBox Box(double x, double y, string label) =>
        new(x, y, 100, 60, label, 0, BoxShape.Rectangle, [], []);

    private sealed record UnknownNode : LayoutNode;
}
