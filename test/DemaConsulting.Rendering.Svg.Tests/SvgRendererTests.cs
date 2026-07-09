// <copyright file="SvgRendererTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Text;

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Svg.Tests;

/// <summary>
///     Smoke tests for <see cref="SvgRenderer"/>, proving a placed layout tree renders to SVG.
/// </summary>
public class SvgRendererTests
{
    /// <summary>
    ///     Proves that rendering a simple box produces a well-formed SVG document.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_SingleBox_ProducesSvgDocument()
    {
        // Arrange: a placed layout tree containing a single box
        var tree = new LayoutTree(100, 60, new LayoutNode[]
        {
            new LayoutBox(10, 10, 80, 40, "Box", 0, BoxShape.Rectangle, [], []),
        });
        var renderer = new SvgRenderer();
        using var stream = new MemoryStream();

        // Act: render the tree to an SVG stream
        renderer.Render(tree, new RenderOptions(Themes.Light), stream);

        // Assert: a well-formed SVG document is produced and the renderer advertises SVG metadata
        var svg = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("<svg", svg, StringComparison.Ordinal);
        Assert.Equal("image/svg+xml", renderer.MediaType);
        Assert.Equal(".svg", renderer.DefaultExtension);
    }

    /// <summary>
    ///     Proves that a <see cref="BoxShape.Folder"/> box renders its keyword/label title text
    ///     recessed below the tab (never floating in the empty tab notch above the box outline),
    ///     matching standard SysML/UML folder notation where the tab itself carries no text.
    /// </summary>
    [Fact]
    public void SvgRenderer_Render_FolderBox_TitleRecessedBelowTab()
    {
        // Arrange: a folder box wide enough that the tab does not span the full width, so any
        // title text still positioned at the tab's vertical band would be visibly outside the
        // tab's horizontal extent.
        var theme = Themes.Light;
        var folderBox = new LayoutBox(0, 0, 200, 80, "Widget", 0, BoxShape.Folder, [], [], Keyword: "part def");
        var renderer = new SvgRenderer();

        // Act: render the box and extract the keyword line's y-coordinate
        var folderSvg = RenderToString(renderer, folderBox, theme);
        var folderKeywordY = ExtractFirstTextY(folderSvg);
        var tabHeight = BoxMetrics.FolderTabHeight(theme);

        // Assert: the folder's keyword text sits at or below the tab's bottom edge, i.e. it is
        // recessed below the tab rather than floating in the empty notch above the box outline.
        // (Both boxes are leaf boxes, so their titles are centered vertically within their own
        // content height; the folder's centering window starts below the tab, so this invariant
        // holds regardless of that centering offset.)
        Assert.True(
            folderKeywordY >= folderBox.Y + tabHeight - 0.5,
            $"Expected folder keyword y ({folderKeywordY}) to be at or below the tab's bottom edge " +
            $"({folderBox.Y + tabHeight}).");
    }

    /// <summary>
    ///     Renders a single box within a minimal layout tree and returns the resulting SVG markup.
    /// </summary>
    private static string RenderToString(SvgRenderer renderer, LayoutBox box, Theme theme)
    {
        var tree = new LayoutTree(200, 100, [box]);
        using var stream = new MemoryStream();
        renderer.Render(tree, new RenderOptions(theme), stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    ///     Extracts the y-coordinate attribute value of the first <c>&lt;text&gt;</c> element in
    ///     the given SVG markup.
    /// </summary>
    private static double ExtractFirstTextY(string svg)
    {
        var match = System.Text.RegularExpressions.Regex.Match(svg, """<text x="[^"]*" y="([^"]*)""");
        Assert.True(match.Success, $"Expected to find a <text> element in: {svg}");
        return double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
