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
}
