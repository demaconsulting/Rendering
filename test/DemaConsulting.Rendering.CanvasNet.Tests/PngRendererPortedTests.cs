// <copyright file="PngRendererPortedTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.CanvasNet;

namespace DemaConsulting.Rendering.CanvasNet.Tests;

/// <summary>
///     Tests for the PNG renderer.
/// </summary>
public sealed class PngRendererPortedTests
{
    /// <summary>
    ///     Renders a <see cref="LayoutTree"/> into a CanvasNet surface so tests can inspect exact pixels.
    /// </summary>
    /// <param name="layout">Layout tree to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    /// <returns>Rendered surface. Caller must dispose it.</returns>
    private static Surface RenderToSurface(LayoutTree layout, RenderOptions options) => PngRenderer.RenderToSurface(layout, options);

    /// <summary>
    ///     Parses a CSS hex color string such as <c>#RRGGBB</c> or <c>#AARRGGBB</c>.
    /// </summary>
    /// <param name="hex">Hex color string to parse.</param>
    /// <returns>Parsed color.</returns>
    private static Rgba32 ParseHex(string hex) => Rgba32.Parse(hex);

    /// <summary>
    ///     Returns whether each channel of <paramref name="actual"/> is within
    ///     <paramref name="tolerance"/> of <paramref name="expected"/>.
    /// </summary>
    /// <param name="expected">Expected color.</param>
    /// <param name="actual">Actual sampled color.</param>
    /// <param name="tolerance">Maximum per-channel delta.</param>
    /// <returns><see langword="true"/> when the colors are sufficiently close.</returns>
    private static bool ColorNear(Rgba32 expected, Rgba32 actual, int tolerance = 2) =>
        Math.Abs(expected.R - actual.R) <= tolerance &&
        Math.Abs(expected.G - actual.G) <= tolerance &&
        Math.Abs(expected.B - actual.B) <= tolerance;

    /// <summary>
    ///     Render with an empty <see cref="LayoutTree"/> produces a non-empty output stream whose first
    ///     four bytes are the PNG signature bytes, confirming that a valid PNG is produced for a minimal
    ///     empty layout.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_EmptyTree_WritesPngSignature()
    {
        // Arrange
        var renderer = new PngRenderer();
        var layout = new LayoutTree(0, 0, []);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        Assert.True(output.Length > 4);
        output.Position = 0;
        var header = new byte[4];
        _ = output.Read(header, 0, 4);
        Assert.Equal(0x89, header[0]);
        Assert.Equal(0x50, header[1]);
        Assert.Equal(0x4E, header[2]);
        Assert.Equal(0x47, header[3]);
    }

    /// <summary>
    ///     Render with a <see cref="LayoutTree"/> containing one <see cref="LayoutBox"/> produces a
    ///     non-empty output stream, confirming that box rendering does not throw and produces valid PNG.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_SingleBox_ProducesNonEmptyOutput()
    {
        // Arrange
        var renderer = new PngRenderer();
        var box = new LayoutBox(10, 10, 100, 50, "TestBox", 0, BoxShape.Rectangle, [], []);
        var layout = new LayoutTree(200, 100, [box]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        Assert.True(output.Length > 4);
        output.Position = 0;
        var header = new byte[4];
        _ = output.Read(header, 0, 4);
        Assert.Equal(0x89, header[0]);
        Assert.Equal(0x50, header[1]);
    }

    /// <summary>
    ///     Render a <see cref="LayoutBox"/> at depth 0 and sample a pixel at the box center. The pixel
    ///     color must match the depth-0 fill color from <see cref="Themes.Light"/>, confirming that
    ///     boxes are filled with the correct theme color.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_SingleBox_FillColorMatchesTheme()
    {
        // Arrange
        var box = new LayoutBox(10, 10, 100, 60, null, 0, BoxShape.Rectangle, [], []);
        var layout = new LayoutTree(200, 100, [box]);
        var options = new RenderOptions(Themes.Light);

        // Act
        using var surface = RenderToSurface(layout, options);

        // Assert
        var expected = ParseHex(Themes.Light.DepthFillColors[0]);
        var actual = surface[60, 40];
        Assert.True(ColorNear(expected, actual), $"Expected {expected} ≈ {actual}");
    }

    /// <summary>
    ///     Render a <see cref="LayoutBox"/> at depth 1 and sample a pixel at the box center. The pixel
    ///     color must match the depth-1 fill color from <see cref="Themes.Light"/>, confirming that
    ///     depth-based fill colors are applied correctly.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_SingleBox_DepthOneUsesSecondColor()
    {
        // Arrange
        var box = new LayoutBox(10, 10, 100, 60, null, 1, BoxShape.Rectangle, [], []);
        var layout = new LayoutTree(200, 100, [box]);
        var options = new RenderOptions(Themes.Light);

        // Act
        using var surface = RenderToSurface(layout, options);

        // Assert
        var expected = ParseHex(Themes.Light.DepthFillColors[1]);
        var actual = surface[60, 40];
        Assert.True(ColorNear(expected, actual), $"Expected {expected} ≈ {actual}");
    }

    /// <summary>
    ///     Render an empty <see cref="LayoutTree"/> with a given theme and sample the pixel at (0, 0).
    ///     The background fill must equal that theme's background color, confirming the canvas is
    ///     initialized from <see cref="RenderOptions.Theme"/> rather than a hardcoded white.
    /// </summary>
    /// <param name="themeName">Named theme to render with.</param>
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void PngRenderer_Render_BackgroundIsThemeBackground(string themeName)
    {
        // Arrange
        var theme = themeName == "Dark" ? Themes.Dark : Themes.Light;
        var layout = new LayoutTree(100, 100, []);
        var options = new RenderOptions(theme);

        // Act
        using var surface = RenderToSurface(layout, options);

        // Assert
        var expected = ParseHex(theme.BackgroundColor);
        var actual = surface[0, 0];
        Assert.True(ColorNear(expected, actual), $"Expected theme background {expected} ≈ {actual}");
    }

    /// <summary>
    ///     Render a horizontal <see cref="LayoutLine"/> and sample a pixel on the line. The sampled
    ///     pixel color must approximate the theme stroke color, confirming that lines are drawn with
    ///     the correct stroke color.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_SingleLine_PixelOnLineIsStrokeColor()
    {
        // Arrange
        var line = new LayoutLine(
            [new Point2D(10, 50), new Point2D(190, 50)],
            EndMarkerStyle.None,
            EndMarkerStyle.None,
            LineStyle.Solid,
            null);
        var layout = new LayoutTree(200, 100, [line]);
        var options = new RenderOptions(Themes.Light);

        // Act
        using var surface = RenderToSurface(layout, options);

        // Assert
        var strokeColor = ParseHex(Themes.Light.StrokeColor);
        var actual = surface[100, 50];
        Assert.True(ColorNear(strokeColor, actual, tolerance: 80), $"Expected stroke {strokeColor} ≈ {actual}");
    }

    /// <summary>
    ///     Render a <see cref="LayoutPort"/> and sample a pixel at the port center. The pixel must
    ///     approximate the theme stroke color, confirming ports are rendered as filled squares.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_SinglePort_CenterPixelIsStrokeColor()
    {
        // Arrange
        var port = new LayoutPort(50, 50, PortSide.Right, null);
        var layout = new LayoutTree(200, 100, [port]);
        var options = new RenderOptions(Themes.Light);

        // Act
        using var surface = RenderToSurface(layout, options);

        // Assert
        var strokeColor = ParseHex(Themes.Light.StrokeColor);
        var actual = surface[50, 50];
        Assert.True(ColorNear(strokeColor, actual, tolerance: 10), $"Expected stroke {strokeColor} ≈ {actual}");
    }

    /// <summary>
    ///     Render a <see cref="LayoutActivation"/> bar and sample a pixel in its interior. The pixel
    ///     must be white, confirming activation bars are filled with white.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_SingleActivation_CenterPixelIsWhite()
    {
        // Arrange
        var activation = new LayoutActivation(100, 20, 80);
        var layout = new LayoutTree(200, 100, [activation]);
        var options = new RenderOptions(Themes.Light);

        // Act
        using var surface = RenderToSurface(layout, options);

        // Assert
        var actual = surface[100, 50];
        Assert.True(ColorNear(new Rgba32(255, 255, 255, 255), actual), $"Expected white ≈ {actual}");
    }

    /// <summary>
    ///     Render a <see cref="LayoutBand"/> and scan its top border row. At least one pixel along the
    ///     band's top edge must approximate the theme stroke color, confirming the swim-lane band render
    ///     path executes and draws its border.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_SingleBand_BorderIsStrokeColor()
    {
        // Arrange
        var band = new LayoutBand(20, 20, 160, 60, BandOrientation.Horizontal, null, []);
        var layout = new LayoutTree(200, 100, [band]);
        var options = new RenderOptions(Themes.Light);

        // Act
        using var surface = RenderToSurface(layout, options);

        // Assert
        var strokeColor = ParseHex(Themes.Light.StrokeColor);
        var found = false;
        for (var x = 20; x <= 180 && !found; x++)
        {
            if (ColorNear(strokeColor, surface[x, 20], tolerance: 80))
            {
                found = true;
            }
        }

        Assert.True(found, "Expected the band's top border to be drawn in the stroke color");
    }

    /// <summary>
    ///     Render a <see cref="LayoutLifeline"/> and sample the stem pixel. The pixel at the center X
    ///     midway down the stem must approximate the stroke color, confirming the stem is drawn.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_SingleLifeline_StemPixelIsStrokeColor()
    {
        // Arrange
        var lifeline = new LayoutLifeline(100, 10, 200, ":Actor", 80, 40);
        var layout = new LayoutTree(300, 300, [lifeline]);
        var options = new RenderOptions(Themes.Light);

        // Act
        using var surface = RenderToSurface(layout, options);

        // Assert
        var strokeColor = ParseHex(Themes.Light.StrokeColor);
        var foundStroke = false;
        for (var y = 55; y < 90; y += 3)
        {
            var actual = surface[100, y];
            if (ColorNear(strokeColor, actual, tolerance: 80))
            {
                foundStroke = true;
                break;
            }
        }

        Assert.True(foundStroke, "Expected to find stroke-colored pixel on lifeline stem");
    }

    /// <summary>
    ///     Render a <see cref="LayoutGrid"/> with a header row and sample the header cell pixel. The
    ///     pixel must match the depth-1 fill color, confirming header rows use the secondary fill color.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_SingleGrid_HeaderFillMatchesTheme()
    {
        // Arrange
        var headerRow = new LayoutGridRow(true, [new LayoutGridCell(100, 30, "Name", TextAlign.Left, 1)]);
        var grid = new LayoutGrid(10, 10, [headerRow]);
        var layout = new LayoutTree(200, 100, [grid]);
        var options = new RenderOptions(Themes.Light);

        // Act
        using var surface = RenderToSurface(layout, options);

        // Assert
        var expected = ParseHex(Themes.Light.DepthFillColors[1]);
        var actual = surface[60, 25];
        Assert.True(ColorNear(expected, actual), $"Expected {expected} ≈ {actual}");
    }

    /// <summary>
    ///     Render a <see cref="LayoutBadge"/> with <see cref="BadgeShape.FilledCircle"/> and sample the
    ///     badge center pixel. The pixel must approximate the theme stroke color.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_SingleBadge_FilledCircle_CenterPixelIsStrokeColor()
    {
        // Arrange
        var badge = new LayoutBadge(50, 50, 20, BadgeShape.FilledCircle, null);
        var layout = new LayoutTree(200, 100, [badge]);
        var options = new RenderOptions(Themes.Light);

        // Act
        using var surface = RenderToSurface(layout, options);

        // Assert
        var strokeColor = ParseHex(Themes.Light.StrokeColor);
        var actual = surface[50, 50];
        Assert.True(ColorNear(strokeColor, actual, tolerance: 10), $"Expected stroke {strokeColor} ≈ {actual}");
    }

    /// <summary>
    ///     Render a <see cref="LayoutLine"/> with an open-with-crossbar target arrowhead produces a
    ///     non-empty output stream beginning with the PNG signature bytes, confirming that the
    ///     open-with-crossbar arrowhead style renders without error.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_DrawArrowhead_OpenWithCrossbar_ProducesNonEmptyOutput()
    {
        // Arrange
        var renderer = new PngRenderer();
        var line = new LayoutLine(
            [new Point2D(10, 50), new Point2D(190, 50)],
            EndMarkerStyle.None,
            EndMarkerStyle.HollowTriangleCrossbar,
            LineStyle.Solid,
            null);
        var layout = new LayoutTree(200, 100, [line]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        Assert.True(output.Length > 4);
        output.Position = 0;
        var header = new byte[4];
        _ = output.Read(header, 0, 4);
        Assert.Equal(0x89, header[0]);
        Assert.Equal(0x50, header[1]);
        Assert.Equal(0x4E, header[2]);
        Assert.Equal(0x47, header[3]);
    }

    /// <summary>
    ///     Render a tree whose labels contain XML-special characters (<c>&lt; &gt; &amp; " '</c>)
    ///     completes without throwing and produces a valid PNG, confirming the raster path treats
    ///     label text as literal glyphs rather than markup.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_LabelWithXmlSpecialCharacters_ProducesValidPng()
    {
        // Arrange
        const string special = "A < B & C > D \" E ' F";
        var renderer = new PngRenderer();
        var box = new LayoutBox(10, 10, 200, 60, special, 0, BoxShape.Rectangle, [], []);
        var label = new LayoutLabel(20, 40, 200, special, TextAlign.Left, FontWeight.Regular, FontStyle.Normal, 12.0);
        var layout = new LayoutTree(300, 200, [box, label]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        Assert.True(output.Length > 4);
        output.Position = 0;
        var header = new byte[4];
        _ = output.Read(header, 0, 4);
        Assert.Equal(0x89, header[0]);
        Assert.Equal(0x50, header[1]);
        Assert.Equal(0x4E, header[2]);
        Assert.Equal(0x47, header[3]);
    }

    /// <summary>
    ///     Render a moderately deeply nested box tree (50 nesting levels) completes without a stack
    ///     overflow and produces a valid PNG, confirming the recursive renderer is robust at realistic
    ///     nesting depths.
    /// </summary>
    [Fact]
    public void PngRenderer_Render_DeeplyNestedBoxes_DoesNotStackOverflow()
    {
        // Arrange
        const int depth = 50;
        var renderer = new PngRenderer();
        LayoutNode node = new LayoutBox(0, 0, 10, 10, "leaf", depth, BoxShape.Rectangle, [], []);
        for (var d = depth - 1; d >= 0; d--)
        {
            node = new LayoutBox(0, 0, 10 + d, 10 + d, $"n{d}", d, BoxShape.Rectangle, [], [node]);
        }

        var layout = new LayoutTree(200, 200, [node]);
        var options = new RenderOptions(Themes.Light);
        using var output = new MemoryStream();

        // Act
        renderer.Render(layout, options, output);

        // Assert
        Assert.True(output.Length > 4);
        output.Position = 0;
        var header = new byte[4];
        _ = output.Read(header, 0, 4);
        Assert.Equal(0x89, header[0]);
        Assert.Equal(0x50, header[1]);
        Assert.Equal(0x4E, header[2]);
        Assert.Equal(0x47, header[3]);
    }
}
