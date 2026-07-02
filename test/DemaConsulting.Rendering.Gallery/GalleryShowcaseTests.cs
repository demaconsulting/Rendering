// <copyright file="GalleryShowcaseTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Gallery;

/// <summary>
///     The gallery showcase: each fact renders one curated diagram to a stable filename and asserts the
///     produced image is valid. The set spans the breadth of the library — every bundled algorithm,
///     orthogonal edge routing around an obstacle, all three themes, and both the SVG and raster output
///     paths — so a normal test run doubles as a real end-to-end rendering smoke test. Output goes to a
///     throwaway directory unless <c>RENDERING_GALLERY_DIR</c> selects the committed showcase folder.
/// </summary>
public sealed class GalleryShowcaseTests
{
    /// <summary>
    ///     Renders the layered pipeline diagram to SVG, proving the layered algorithm and SVG renderer
    ///     produce a well-formed document.
    /// </summary>
    [Fact]
    public void Gallery_LayeredPipeline_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.LayeredPipelineSvg,
            GalleryDiagrams.LayeredPipeline(),
            LayoutOptions.ForAlgorithm("layered"),
            Themes.Light);
    }

    /// <summary>
    ///     Renders the containment-packed diagram to SVG, proving the containment algorithm packs
    ///     sibling boxes into a valid document.
    /// </summary>
    [Fact]
    public void Gallery_ContainmentPacked_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.ContainmentPackedSvg,
            GalleryDiagrams.ContainmentPacked(),
            LayoutOptions.ForAlgorithm("containment"),
            Themes.Light);
    }

    /// <summary>
    ///     Renders the hierarchical nested diagram to SVG, proving the hierarchical engine composes a
    ///     container node's child graph and a cross-container edge into a valid document.
    /// </summary>
    [Fact]
    public void Gallery_HierarchicalNested_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.HierarchicalNestedSvg,
            GalleryDiagrams.HierarchicalNested(),
            new LayoutOptions(),
            Themes.Light);
    }

    /// <summary>
    ///     Renders the obstacle-routing diagram to SVG, proving a cross-container edge is routed
    ///     orthogonally around an intervening container.
    /// </summary>
    [Fact]
    public void Gallery_OrthogonalObstacle_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.OrthogonalObstacleSvg,
            GalleryDiagrams.OrthogonalObstacle(),
            new LayoutOptions(),
            Themes.Light);
    }

    /// <summary>
    ///     Renders the representative diagram with the light theme.
    /// </summary>
    [Fact]
    public void Gallery_ThemeLight_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.ThemeLightSvg,
            GalleryDiagrams.ThemeShowcase(),
            new LayoutOptions(),
            Themes.Light);
    }

    /// <summary>
    ///     Renders the representative diagram with the dark theme.
    /// </summary>
    [Fact]
    public void Gallery_ThemeDark_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.ThemeDarkSvg,
            GalleryDiagrams.ThemeShowcase(),
            new LayoutOptions(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the representative diagram with the print theme.
    /// </summary>
    [Fact]
    public void Gallery_ThemePrint_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.ThemePrintSvg,
            GalleryDiagrams.ThemeShowcase(),
            new LayoutOptions(),
            Themes.Print);
    }

    /// <summary>
    ///     Renders the layered pipeline diagram to PNG, proving the SkiaSharp raster path produces a
    ///     valid, decodable image.
    /// </summary>
    [Fact]
    public void Gallery_LayeredPipeline_RendersPng()
    {
        GalleryWriter.Png(
            GalleryCatalog.LayeredPipelinePng,
            GalleryDiagrams.LayeredPipeline(),
            LayoutOptions.ForAlgorithm("layered"),
            Themes.Light);
    }

    /// <summary>
    ///     Renders the hierarchical nested diagram to PNG, proving the raster path handles composed
    ///     nested layouts.
    /// </summary>
    [Fact]
    public void Gallery_HierarchicalNested_RendersPng()
    {
        GalleryWriter.Png(
            GalleryCatalog.HierarchicalNestedPng,
            GalleryDiagrams.HierarchicalNested(),
            new LayoutOptions(),
            Themes.Light);
    }
}
