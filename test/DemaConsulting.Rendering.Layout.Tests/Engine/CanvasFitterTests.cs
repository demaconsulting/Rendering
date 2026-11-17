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
