// <copyright file="SvgRendererPortedTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Text;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Svg;

namespace DemaConsulting.Rendering.Svg.Tests;

/// <summary>
///     Tests for the SVG renderer.
/// </summary>
public sealed class SvgRendererPortedTests
{
    /// <summary>
    ///     Render with an empty LayoutTree produces a non-empty output stream whose content
    ///     contains the SVG root element, confirming basic SVG document generation.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_EmptyTree_ProducesSvgDocument()
    {
        // Arrange: a renderer with an empty LayoutTree and default options
        var renderer = new SvgRenderer();
        var layout = new LayoutTree(400, 300, []);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act: render the empty tree
        renderer.Render(layout, options, output);

        // Assert: output is non-empty and contains the SVG root element
        Assert.True(output.Length > 0);
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains("<svg", svgText, StringComparison.Ordinal);
        Assert.Contains("</svg>", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render with a LayoutTree containing one LayoutBox produces SVG output that
    ///     contains a rect element, confirming that boxes are translated to SVG rectangles.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleBox_ProducesRectElement()
    {
        // Arrange: a renderer with a tree containing one LayoutBox
        var renderer = new SvgRenderer();
        var box = new LayoutBox(10, 10, 100, 50, "MyBox", 0, BoxShape.Rectangle, [], []);
        var layout = new LayoutTree(200, 100, [box]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act: render the tree with one box
        renderer.Render(layout, options, output);

        // Assert: output contains a rect element
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains("<rect", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render with a LayoutTree containing one LayoutLabel produces SVG output that
    ///     contains a text element, confirming that labels are translated to SVG text nodes.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleLabel_ProducesTextElement()
    {
        // Arrange: a renderer with a tree containing one LayoutLabel
        var renderer = new SvgRenderer();
        var label = new LayoutLabel(50, 75, 200, "Hello World", TextAlign.Center, FontWeight.Regular, FontStyle.Normal, 12.0);
        var layout = new LayoutTree(200, 100, [label]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act: render the tree with one label
        renderer.Render(layout, options, output);

        // Assert: output contains a text element
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains("<text", svgText, StringComparison.Ordinal);
        Assert.Contains("Hello World", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render with a LayoutTree containing one LayoutLine produces SVG output that
    ///     contains a path element, confirming that lines are translated to SVG paths.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleLine_ProducesPathElement()
    {
        // Arrange: a renderer with a tree containing one LayoutLine
        var renderer = new SvgRenderer();
        var line = new LayoutLine(
            [new Point2D(10, 10), new Point2D(90, 90)],
            EndMarkerStyle.None,
            EndMarkerStyle.HollowTriangle,
            LineStyle.Solid,
            null);
        var layout = new LayoutTree(200, 100, [line]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act: render the tree with one line
        renderer.Render(layout, options, output);

        // Assert: output contains a path element
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains("<path", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutLine with 3 waypoints and a positive LineCornerRadius theme
    ///     produces SVG output containing an arc command (" A ") in the path data,
    ///     confirming that corner rounding generates arc segments.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleLine_WithCornerRadius_ProducesArcInPath()
    {
        // Arrange: a line with an interior bend that triggers arc generation
        var renderer = new SvgRenderer();
        var line = new LayoutLine(
            [new Point2D(10, 10), new Point2D(10, 50), new Point2D(90, 50)],
            EndMarkerStyle.None,
            EndMarkerStyle.None,
            LineStyle.Solid,
            null);
        var layout = new LayoutTree(200, 100, [line]);
        var options = new RenderOptions(Themes.Light); // LineCornerRadius = 4.0
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert: arc command is present in path data
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains(" A ", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a dashed LayoutLine produces SVG output containing the stroke-dasharray
    ///     attribute, confirming that dashed line style is mapped to SVG dash patterns.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleLine_Dashed_ProducesDashArray()
    {
        // Arrange: a dashed line
        var renderer = new SvgRenderer();
        var line = new LayoutLine(
            [new Point2D(10, 10), new Point2D(90, 10)],
            EndMarkerStyle.None,
            EndMarkerStyle.None,
            LineStyle.Dashed,
            null);
        var layout = new LayoutTree(200, 100, [line]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains("stroke-dasharray", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutLine with a HollowTriangle target arrowhead produces SVG output containing
    ///     a marker-end attribute, confirming arrowhead markers are referenced correctly.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleLine_WithHollowTriangleArrowhead_ProducesMarkerEnd()
    {
        // Arrange: a line with a hollow-triangle arrowhead at the target
        var renderer = new SvgRenderer();
        var line = new LayoutLine(
            [new Point2D(10, 10), new Point2D(90, 10)],
            EndMarkerStyle.None,
            EndMarkerStyle.HollowTriangle,
            LineStyle.Solid,
            null);
        var layout = new LayoutTree(200, 100, [line]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains("marker-end", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutLine with a Diamond source arrowhead produces SVG output containing
    ///     the line-end-hollow-diamond marker id, confirming diamond markers are defined and referenced.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleLine_WithDiamondArrowhead_ProducesDiamondMarker()
    {
        // Arrange: a line with Diamond arrowhead at the source
        var renderer = new SvgRenderer();
        var line = new LayoutLine(
            [new Point2D(10, 10), new Point2D(90, 10)],
            EndMarkerStyle.HollowDiamond,
            EndMarkerStyle.None,
            LineStyle.Solid,
            null);
        var layout = new LayoutTree(200, 100, [line]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert: the connector path references the hollow-diamond marker on its source end
        // (not merely that the marker id appears in <defs>).
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains("marker-start=\"url(#line-end-hollow-diamond)\"", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutBox with a LayoutCompartment produces SVG output containing a
    ///     line element (compartment divider) and compartment row text, confirming that
    ///     compartment rendering is complete.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_BoxWithCompartment_ProducesLineAndText()
    {
        // Arrange: a box with one compartment that has a body row
        var renderer = new SvgRenderer();
        var compartment = new LayoutCompartment(null, ["+ radius : Real"]);
        var box = new LayoutBox(10, 10, 150, 80, "MyBlock", 0, BoxShape.Rectangle, [compartment], []);
        var layout = new LayoutTree(200, 120, [box]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert: divider line and compartment row text are both present
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains("<line", svgText, StringComparison.Ordinal);
        Assert.Contains("+ radius : Real", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutBox with RoundedRectangle shape produces SVG output containing an
    ///     rx attribute, confirming that rounded corners are applied via the rx/ry attributes.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_BoxRoundedRectangle_ProducesRxAttribute()
    {
        // Arrange: a rounded-rectangle box
        var renderer = new SvgRenderer();
        var box = new LayoutBox(10, 10, 100, 50, "Rounded", 0, BoxShape.RoundedRectangle, [], []);
        var layout = new LayoutTree(200, 100, [box]);
        var options = new RenderOptions(Themes.Light); // LineCornerRadius = 4.0
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains("rx=\"", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutPort produces SVG output containing a rect element,
    ///     confirming that ports are rendered as filled squares.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SinglePort_ProducesRect()
    {
        // Arrange: a port on the right side
        var renderer = new SvgRenderer();
        var port = new LayoutPort(100, 50, PortSide.Right, "p1");
        var layout = new LayoutTree(200, 100, [port]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains("<rect", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a port's label reads inward (into the box interior) rather than outward: for
    ///     a left-side port the label's text x-coordinate is greater than the port's own x-coordinate
    ///     (toward the box's right/interior), and for a right-side port the label's x-coordinate is
    ///     less than the port's own x-coordinate (toward the box's left/interior).
    /// </summary>
    [Theory]
    [InlineData(PortSide.Left, true)]
    [InlineData(PortSide.Right, false)]
    public void SvgRenderer_RenderPort_LeftRightLabel_ReadsInward(PortSide side, bool labelXGreaterThanPortX)
    {
        // Arrange
        var renderer = new SvgRenderer();
        var port = new LayoutPort(100, 50, side, "label");
        var layout = new LayoutTree(200, 100, [port]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert: locate the <text> element's x attribute and compare against the port's own
        // (unscaled) x position at the default scale of 1.0.
        output.Position = 0;
        var svgText = ReadAllText(output);
        var match = System.Text.RegularExpressions.Regex.Match(svgText, """<text x="([\-0-9.]+)""");
        Assert.True(match.Success);
        var labelX = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

        if (labelXGreaterThanPortX)
        {
            Assert.True(labelX > port.CentreX);
        }
        else
        {
            Assert.True(labelX < port.CentreX);
        }
    }

    /// <summary>
    ///     Proves that a port's label reads inward for the top/bottom sides too: a top-side port's
    ///     label y-coordinate is greater than the port's own y (downward, into the box below), and a
    ///     bottom-side port's label y-coordinate is less than the port's own y (upward, into the box
    ///     above).
    /// </summary>
    [Theory]
    [InlineData(PortSide.Top, true)]
    [InlineData(PortSide.Bottom, false)]
    public void SvgRenderer_RenderPort_TopBottomLabel_ReadsInward(PortSide side, bool labelYGreaterThanPortY)
    {
        // Arrange
        var renderer = new SvgRenderer();
        var port = new LayoutPort(100, 50, side, "label");
        var layout = new LayoutTree(200, 100, [port]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = ReadAllText(output);
        var match = System.Text.RegularExpressions.Regex.Match(svgText, """<text x="[\-0-9.]+" y="([\-0-9.]+)""");
        Assert.True(match.Success);
        var labelY = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

        if (labelYGreaterThanPortY)
        {
            Assert.True(labelY > port.CentreY);
        }
        else
        {
            Assert.True(labelY < port.CentreY);
        }
    }

    /// <summary>
    ///     Proves that a box's non-zero <see cref="LayoutBox.ContentInsetLeft"/> pushes its
    ///     compartment row text further right than an otherwise-identical box with no content
    ///     insets, confirming the renderer reads the inset rather than a fixed label-padding-only
    ///     offset.
    /// </summary>
    [Fact]
    public void SvgRenderer_RenderBoxCompartments_ContentInsetLeft_ShiftsRowTextRight()
    {
        // Arrange: two identical boxes with a compartment row, one with a left content inset.
        var renderer = new SvgRenderer();
        var compartments = new[] { new LayoutCompartment(null, ["row text"]) };
        var plain = new LayoutBox(0, 0, 200, 100, "Title", 0, BoxShape.Rectangle, compartments, []);
        var inset = plain with { ContentInsetLeft = 50.0 };
        var options = new RenderOptions(Themes.Light);

        using var plainOutput = new MemoryStream();
        renderer.Render(new LayoutTree(200, 100, [plain]), options, plainOutput);
        plainOutput.Position = 0;
        var plainSvg = ReadAllText(plainOutput);

        using var insetOutput = new MemoryStream();
        renderer.Render(new LayoutTree(200, 100, [inset]), options, insetOutput);
        insetOutput.Position = 0;
        var insetSvg = ReadAllText(insetOutput);

        // Assert: the inset box's row <text> x-coordinate is further right than the plain box's.
        var plainX = ExtractLastTextX(plainSvg);
        var insetX = ExtractLastTextX(insetSvg);
        Assert.True(insetX > plainX);
    }

    /// <summary>Extracts the x attribute of the last &lt;text&gt; element in the given SVG markup.</summary>
    private static double ExtractLastTextX(string svg)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(svg, """<text x="([\-0-9.]+)""");
        Assert.NotEmpty(matches);
        return double.Parse(matches[^1].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Proves that when 3+ parallel labeled connectors force the label placer to nudge labels
    ///     downward to avoid collisions, the SVG renderer grows the canvas (rather than sizing it
    ///     from the pre-label-placement box/routing geometry alone) so every label stays fully
    ///     within the final rendered viewBox — none are clipped off-canvas.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_ManyCollidingConnectorLabels_AllLabelsWithinViewBox()
    {
        // Arrange: three horizontal lines close enough together (4px apart) that their preferred
        // midpoint labels collide and must be nudged apart, using a deliberately small declared
        // canvas height (60) that is too small to fit the nudged labels without growing.
        var renderer = new SvgRenderer();
        var lineA = new LayoutLine([new Point2D(0, 20), new Point2D(200, 20)], EndMarkerStyle.None, EndMarkerStyle.FilledArrow, LineStyle.Solid, "primary");
        var lineB = new LayoutLine([new Point2D(0, 24), new Point2D(200, 24)], EndMarkerStyle.None, EndMarkerStyle.FilledArrow, LineStyle.Solid, "retry");
        var lineC = new LayoutLine([new Point2D(0, 28), new Point2D(200, 28)], EndMarkerStyle.None, EndMarkerStyle.FilledArrow, LineStyle.Solid, "audit");
        var layout = new LayoutTree(220, 60, [lineA, lineB, lineC]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert: every <text> element (the 3 midpoint labels) lies within [0, width] x [0, height]
        // of the rendered viewBox, which must have grown past the declared 220x60 to include them.
        output.Position = 0;
        var svgText = ReadAllText(output);
        var (width, height) = ExtractSvgSize(svgText);
        var (xs, ys) = ExtractAllTextPositions(svgText);
        Assert.Equal(3, xs.Count);
        for (var i = 0; i < xs.Count; i++)
        {
            Assert.InRange(xs[i], 0.0, width);
            Assert.InRange(ys[i], 0.0, height);
        }

        // The canvas must actually have grown past the small declared height to accommodate the
        // stacked labels — otherwise this test would not be exercising the fix.
        Assert.True(height > 60.0, $"Expected canvas height to grow past 60, was {height}.");
    }

    /// <summary>Extracts the <c>width</c>/<c>height</c> attribute values of the root <c>&lt;svg&gt;</c> element.</summary>
    private static (double Width, double Height) ExtractSvgSize(string svg)
    {
        var match = System.Text.RegularExpressions.Regex.Match(svg, """<svg[^>]*\swidth="([\-0-9.]+)"\s+height="([\-0-9.]+)""");
        Assert.True(match.Success, $"Expected to find an <svg> root element with width/height in: {svg}");
        return (
            double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
            double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Extracts every <c>&lt;text&gt;</c> element's x/y attribute values, in document order.</summary>
    private static (List<double> Xs, List<double> Ys) ExtractAllTextPositions(string svg)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(svg, """<text x="([\-0-9.]+)" y="([\-0-9.]+)""");
        var xs = matches.Select(m => double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        var ys = matches.Select(m => double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        return (xs, ys);
    }

    /// <summary>
    ///     Proves that a box title centers on the inset-adjusted content area, not the full box
    ///     width: when <see cref="LayoutBox.ContentInsetLeft"/> exceeds
    ///     <see cref="LayoutBox.ContentInsetRight"/>, the title's rendered x-coordinate shifts right
    ///     of true box-center (toward the smaller/un-inset side), and vice versa.
    /// </summary>
    [Fact]
    public void SvgRenderer_RenderBoxTitle_AsymmetricContentInsets_ShiftsTitleOffBoxCenter()
    {
        // Arrange: a box with a large left inset and no right inset (mirrors a box with a long
        // left-port label and a short/absent right-port label).
        var renderer = new SvgRenderer();
        var box = new LayoutBox(0, 0, 200, 100, "Hub", 0, BoxShape.Rectangle, [], [], ContentInsetLeft: 60.0, ContentInsetRight: 0.0);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(new LayoutTree(200, 100, [box]), options, output);

        // Assert: the title's x-coordinate is to the right of the true (un-inset) box center, since
        // the content area is shifted right by the larger left inset.
        output.Position = 0;
        var svgText = ReadAllText(output);
        var titleX = ExtractLastTextX(svgText);
        var trueBoxCenterX = box.X + (box.Width / 2.0);
        Assert.True(titleX > trueBoxCenterX, $"Expected title x ({titleX}) to be right of true box center ({trueBoxCenterX}).");
    }

    /// <summary>
    ///     Proves that a long port label bounded by a finite <see cref="LayoutPort.MaxLabelWidth"/> is
    ///     squeezed to fit (via the same <c>textLength</c> mechanism used for box titles) rather than
    ///     rendering at its full natural width and overlapping the opposite port's label region.
    /// </summary>
    [Fact]
    public void SvgRenderer_RenderPort_LongLabelWithMaxLabelWidth_AppliesTextLengthConstraint()
    {
        // Arrange: a left port with a deliberately long label bounded to a narrow MaxLabelWidth, and
        // a right port with a short label and no meaningful bound (mirrors a box's two opposite ports).
        var renderer = new SvgRenderer();
        var leftPort = new LayoutPort(10, 50, PortSide.Left, "a rather long incoming data label", MaxLabelWidth: 40.0);
        var rightPort = new LayoutPort(190, 50, PortSide.Right, "out");
        var layout = new LayoutTree(200, 100, [leftPort, rightPort]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert: the long label's <text> element carries a textLength constraint (squeezed),
        // while the short "out" label does not need one.
        output.Position = 0;
        var svgText = ReadAllText(output);
        var longLabelMatch = System.Text.RegularExpressions.Regex.Match(svgText, """<text[^>]*>a rather long incoming data label<""");
        Assert.True(longLabelMatch.Success, $"Expected to find the long port label's <text> element in: {svgText}");
        Assert.Contains("textLength=", longLabelMatch.Value, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutBadge with FilledCircle shape produces SVG output containing a
    ///     circle element, confirming that filled-circle badges are rendered as SVG circles.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleBadge_FilledCircle_ProducesCircle()
    {
        // Arrange: a filled-circle badge
        var renderer = new SvgRenderer();
        var badge = new LayoutBadge(50, 50, 12, BadgeShape.FilledCircle, "I");
        var layout = new LayoutTree(200, 100, [badge]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains("<circle", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutBadge with Bullseye shape produces SVG output containing two circle
    ///     elements, confirming that the bullseye ring is rendered as concentric circles.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleBadge_Bullseye_ProducesConcentricCircles()
    {
        // Arrange: a bullseye badge
        var renderer = new SvgRenderer();
        var badge = new LayoutBadge(50, 50, 12, BadgeShape.Bullseye, "I");
        var layout = new LayoutTree(200, 100, [badge]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var body = BodyAfterDefs(ReadAllText(output));
        Assert.True(
            body.Split("<circle", StringSplitOptions.None).Length - 1 >= 2,
            "Expected the bullseye badge to render two circle elements.");
    }

    /// <summary>
    ///     Render a LayoutBadge with Diamond shape produces SVG output containing a polygon element,
    ///     confirming that the badge renders as a diamond outline.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleBadge_Diamond_ProducesPolygon()
    {
        // Arrange: a diamond badge
        var renderer = new SvgRenderer();
        var badge = new LayoutBadge(50, 50, 12, BadgeShape.Diamond, "I");
        var layout = new LayoutTree(200, 100, [badge]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var body = BodyAfterDefs(ReadAllText(output));
        Assert.Contains("<polygon", body, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutBadge with HorizontalBar shape produces SVG output containing a line
    ///     element, confirming that the badge renders as a horizontal bar.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleBadge_HorizontalBar_ProducesLine()
    {
        // Arrange: a horizontal-bar badge
        var renderer = new SvgRenderer();
        var badge = new LayoutBadge(50, 50, 12, BadgeShape.HorizontalBar, "I");
        var layout = new LayoutTree(200, 100, [badge]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var body = BodyAfterDefs(ReadAllText(output));
        Assert.Contains("<line", body, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutBadge with VerticalBar shape produces SVG output containing a line
    ///     element, confirming that the badge renders as a vertical bar.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleBadge_VerticalBar_ProducesLine()
    {
        // Arrange: a vertical-bar badge
        var renderer = new SvgRenderer();
        var badge = new LayoutBadge(50, 50, 12, BadgeShape.VerticalBar, "I");
        var layout = new LayoutTree(200, 100, [badge]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var body = BodyAfterDefs(ReadAllText(output));
        Assert.Contains("<line", body, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutBand produces SVG output containing a rect element,
    ///     confirming that swim-lane bands are rendered as rectangles.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleBand_ProducesRect()
    {
        // Arrange: a horizontal swim-lane band
        var renderer = new SvgRenderer();
        var band = new LayoutBand(10, 10, 300, 100, BandOrientation.Horizontal, "Lane A", []);
        var layout = new LayoutTree(400, 200, [band]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains("<rect", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutLifeline produces SVG output containing both a rect element
    ///     (the header box) and a line element (the dashed stem), confirming that both
    ///     components of a lifeline are rendered.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleLifeline_ProducesRectAndLine()
    {
        // Arrange: a lifeline with a header box and a stem
        var renderer = new SvgRenderer();
        var lifeline = new LayoutLifeline(100, 10, 300, ":Actor", 80, 30);
        var layout = new LayoutTree(300, 400, [lifeline]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains("<rect", svgText, StringComparison.Ordinal);
        Assert.Contains("<line", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutActivation produces SVG output containing a rect element,
    ///     confirming that activation bars are rendered as narrow rectangles.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleActivation_ProducesRect()
    {
        // Arrange: a narrow activation bar
        var renderer = new SvgRenderer();
        var activation = new LayoutActivation(100, 50, 200);
        var layout = new LayoutTree(300, 400, [activation]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains("<rect", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutGrid produces SVG output containing at least one rect element,
    ///     confirming that grid cells are rendered as bordered rectangles.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleGrid_ProducesRects()
    {
        // Arrange: a 1x2 grid with one header and one body row
        var renderer = new SvgRenderer();
        var headerRow = new LayoutGridRow(true, [new LayoutGridCell(100, 24, "Name", TextAlign.Left, 1)]);
        var bodyRow = new LayoutGridRow(false, [new LayoutGridCell(100, 24, "Alice", TextAlign.Left, 1)]);
        var grid = new LayoutGrid(10, 10, [headerRow, bodyRow]);
        var layout = new LayoutTree(200, 100, [grid]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains("<rect", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutLabel with FontWeight.Bold produces SVG output containing
    ///     font-weight="bold", confirming that bold labels apply the bold font weight attribute.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_LabelWithBold_ProducesBoldAttribute()
    {
        // Arrange: a label with bold weight
        var renderer = new SvgRenderer();
        var label = new LayoutLabel(50, 50, 200, "Bold Text", TextAlign.Left, FontWeight.Bold, FontStyle.Normal, 14.0);
        var layout = new LayoutTree(300, 100, [label]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains("font-weight=\"bold\"", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutLabel with FontStyle.Italic produces SVG output containing
    ///     font-style="italic", confirming that italic labels apply the italic font style attribute.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_LabelWithItalic_ProducesItalicAttribute()
    {
        // Arrange: a label with italic style
        var renderer = new SvgRenderer();
        var label = new LayoutLabel(50, 50, 200, "Italic Text", TextAlign.Left, FontWeight.Regular, FontStyle.Italic, 14.0);
        var layout = new LayoutTree(300, 100, [label]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains("font-style=\"italic\"", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutLine with a non-null MidpointLabel produces SVG output containing
    ///     a text element, confirming that midpoint labels are rendered over the line.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_LineWithMidpointLabel_ProducesTextElement()
    {
        // Arrange: a line with a midpoint label
        var renderer = new SvgRenderer();
        var line = new LayoutLine(
            [new Point2D(10, 50), new Point2D(190, 50)],
            EndMarkerStyle.None,
            EndMarkerStyle.None,
            LineStyle.Solid,
            "uses");
        var layout = new LayoutTree(200, 100, [line]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains("<text", svgText, StringComparison.Ordinal);
        Assert.Contains("uses", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a LayoutLine with an OpenWithCrossbar target arrowhead produces SVG output
    ///     containing the line-end-hollow-triangle-crossbar marker id, confirming the open-crossbar marker
    ///     is defined in the defs block and referenced by the path element.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleLine_WithOpenCrossbarArrowhead_ProducesOpenCrossbarMarker()
    {
        // Arrange: a line with OpenWithCrossbar arrowhead at the target
        var renderer = new SvgRenderer();
        var line = new LayoutLine(
            [new Point2D(10, 10), new Point2D(90, 10)],
            EndMarkerStyle.None,
            EndMarkerStyle.HollowTriangleCrossbar,
            LineStyle.Solid,
            null);
        var layout = new LayoutTree(200, 100, [line]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert: the connector path references the hollow-triangle-crossbar marker on its target end
        // (not merely that the marker id appears in <defs>).
        output.Position = 0;
        var svgText = ReadAllText(output);
        Assert.Contains("marker-end=\"url(#line-end-hollow-triangle-crossbar)\"", svgText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Render a tree whose box label and standalone label both contain XML-special characters
    ///     (<c>&lt; &gt; &amp; " '</c>) produces well-formed SVG in which the reserved markup
    ///     characters are escaped, confirming that label text can never break the SVG document.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_LabelWithXmlSpecialCharacters_ProducesWellFormedEscapedSvg()
    {
        // Arrange: a tree whose box label and standalone label both contain XML-special characters.
        const string special = "A < B & C > D \" E ' F";
        var renderer = new SvgRenderer();
        var box = new LayoutBox(10, 10, 200, 60, special, 0, BoxShape.Rectangle, [], []);
        var label = new LayoutLabel(20, 40, 200, special, TextAlign.Left, FontWeight.Regular, FontStyle.Normal, 12.0);
        var layout = new LayoutTree(300, 200, [box, label]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act: render the tree.
        renderer.Render(layout, options, output);

        // Assert: the output parses as well-formed XML and the reserved characters are escaped.
        output.Position = 0;
        var svgText = ReadAllText(output);
        var document = System.Xml.Linq.XDocument.Parse(svgText); // throws if the SVG is not well-formed
        Assert.Contains("&lt;", svgText, StringComparison.Ordinal);
        Assert.Contains("&gt;", svgText, StringComparison.Ordinal);
        Assert.Contains("&amp;", svgText, StringComparison.Ordinal);

        // Assert: after parsing, a text element's content round-trips back to the original label,
        // proving the special characters were escaped rather than emitted raw.
        var textValues = document.Descendants()
            .Where(e => e.Name.LocalName == "text")
            .Select(e => e.Value)
            .ToList();
        Assert.Contains(textValues, v => v.Contains(special, StringComparison.Ordinal));
    }

    /// <summary>Reads the whole stream as UTF-8 text, disposing the reader while leaving the stream open.</summary>
    /// <param name="stream">The stream to read from its current position.</param>
    /// <returns>The decoded text.</returns>
    private static string ReadAllText(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
        return reader.ReadToEnd();
    }

    /// <summary>
    ///     Returns the SVG body content that follows the shared <c>&lt;defs&gt;</c> marker-definitions
    ///     block, so element-shape assertions (circle/polygon/line, etc.) verify the actual badge
    ///     rendering rather than incidentally matching the same element types used by end markers.
    /// </summary>
    /// <param name="svgText">The full rendered SVG document text.</param>
    /// <returns>The substring of <paramref name="svgText"/> after the closing <c>&lt;/defs&gt;</c> tag.</returns>
    private static string BodyAfterDefs(string svgText)
    {
        var index = svgText.IndexOf("</defs>", StringComparison.Ordinal);
        return index < 0 ? svgText : svgText[(index + "</defs>".Length)..];
    }
}
