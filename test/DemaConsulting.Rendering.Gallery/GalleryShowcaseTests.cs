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
        var graph = GalleryDiagrams.LayeredPipeline();
        graph.Set(CoreOptions.Algorithm, "layered");

        GalleryWriter.Svg(
            GalleryCatalog.LayeredPipelineSvg,
            graph,
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the containment-packed diagram to SVG, proving the containment algorithm packs
    ///     sibling boxes into a valid document.
    /// </summary>
    [Fact]
    public void Gallery_ContainmentPacked_RendersSvg()
    {
        var graph = GalleryDiagrams.ContainmentPacked();
        graph.Set(CoreOptions.Algorithm, "containment");

        GalleryWriter.Svg(
            GalleryCatalog.ContainmentPackedSvg,
            graph,
            Themes.Dark);
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
            Themes.Dark);
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
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the direction showcase left-to-right, proving the layered algorithm honors the
    ///     default rightward flow direction.
    /// </summary>
    [Fact]
    public void Gallery_DirectionRight_RendersSvg()
    {
        var graph = GalleryDiagrams.DirectionShowcase();
        graph.Set(CoreOptions.Algorithm, "layered");
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Right);

        GalleryWriter.Svg(
            GalleryCatalog.DirectionRightSvg,
            graph,
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the same direction showcase top-to-bottom, proving the layered algorithm honors a
    ///     downward flow direction and produces a genuinely different, transposed layout.
    /// </summary>
    [Fact]
    public void Gallery_DirectionDown_RendersSvg()
    {
        var graph = GalleryDiagrams.DirectionShowcase();
        graph.Set(CoreOptions.Algorithm, "layered");
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        GalleryWriter.Svg(
            GalleryCatalog.DirectionDownSvg,
            graph,
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the mixed-direction nested diagram, proving a container's own
    ///     <see cref="CoreOptions.Direction"/> override is honored independently of the outer graph's
    ///     flow direction: the outer flow runs rightward while the nested container runs downward.
    /// </summary>
    [Fact]
    public void Gallery_MixedDirectionNested_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.MixedDirectionNestedSvg,
            GalleryDiagrams.MixedDirectionNested(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the representative diagram with the light theme to PNG, giving it a solid light
    ///     background.
    /// </summary>
    [Fact]
    public void Gallery_ThemeLight_RendersPng()
    {
        GalleryWriter.Png(
            GalleryCatalog.ThemeLightPng,
            GalleryDiagrams.ThemeShowcase(),
            Themes.Light);
    }

    /// <summary>
    ///     Renders the representative diagram with the dark theme to PNG, giving it a solid dark
    ///     background.
    /// </summary>
    [Fact]
    public void Gallery_ThemeDark_RendersPng()
    {
        GalleryWriter.Png(
            GalleryCatalog.ThemeDarkPng,
            GalleryDiagrams.ThemeShowcase(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the representative diagram with the print theme to PNG, giving it a solid light
    ///     background suited to black-and-white output.
    /// </summary>
    [Fact]
    public void Gallery_ThemePrint_RendersPng()
    {
        GalleryWriter.Png(
            GalleryCatalog.ThemePrintPng,
            GalleryDiagrams.ThemeShowcase(),
            Themes.Print);
    }

    /// <summary>
    ///     Renders the layered pipeline diagram to PNG, proving the SkiaSharp raster path produces a
    ///     valid, decodable image.
    /// </summary>
    [Fact]
    public void Gallery_LayeredPipeline_RendersPng()
    {
        var graph = GalleryDiagrams.LayeredPipeline();
        graph.Set(CoreOptions.Algorithm, "layered");

        GalleryWriter.Png(
            GalleryCatalog.LayeredPipelinePng,
            graph,
            Themes.Dark);
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
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the box-appearance showcase to SVG, proving a folder container's
    ///     <see cref="LayoutGraphNode.Shape"/> and <see cref="LayoutGraphNode.Keyword"/>, and a nested
    ///     box's <see cref="LayoutGraphNode.Keyword"/> and <see cref="LayoutGraphNode.Compartments"/>,
    ///     all render correctly when selected purely through the input graph model.
    /// </summary>
    [Fact]
    public void Gallery_BoxAppearance_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.BoxAppearanceSvg,
            GalleryDiagrams.BoxAppearance(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the folder-top-face-anchor diagram to SVG, proving a cross-container edge approaching
    ///     a folder container from above anchors clear of the tab, on the folder's actual outline.
    /// </summary>
    [Fact]
    public void Gallery_FolderTopFaceAnchor_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.FolderTopFaceAnchorSvg,
            GalleryDiagrams.FolderTopFaceAnchor(),
            Themes.Dark);
    }


}