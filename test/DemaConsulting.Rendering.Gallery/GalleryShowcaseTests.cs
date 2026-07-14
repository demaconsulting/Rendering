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
    ///     Renders the isolated-node layer-gap regression diagram to SVG, proving the crossing minimizer
    ///     and Brandes-Köpf coordinate assignment keep a genuinely isolated node at standard node
    ///     spacing from its layer neighbor even when an unrelated routing-corridor dummy shares its
    ///     layer.
    /// </summary>
    [Fact]
    public void Gallery_IsolatedNodeLayerGap_RendersSvg()
    {
        var graph = GalleryDiagrams.IsolatedNodeLayerGap();
        graph.Set(CoreOptions.Algorithm, "layered");

        GalleryWriter.Svg(
            GalleryCatalog.IsolatedNodeLayerGapSvg,
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
    ///     Renders the folder-top-face-anchor diagram to SVG, proving an edge approaching a folder
    ///     container from above anchors clear of the tab, on the folder's actual outline.
    /// </summary>
    [Fact]
    public void Gallery_FolderTopFaceAnchor_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.FolderTopFaceAnchorSvg,
            GalleryDiagrams.FolderTopFaceAnchor(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the shape-gallery diagram to SVG, proving every <see cref="BoxShape"/> value reserves
    ///     enough space for its own content (title area, compartments, or nested children) without that
    ///     content overlapping the shape's non-rectangular features.
    /// </summary>
    [Fact]
    public void Gallery_ShapeGallery_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.ShapeGallerySvg,
            GalleryDiagrams.ShapeGallery(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the parallel-edges-preserved diagram to SVG, proving that with
    ///     <see cref="CoreOptions.MergeParallelEdges"/> set to <see langword="false"/> every one of
    ///     three parallel connectors between the same two boxes survives as its own
    ///     independently-routed line.
    /// </summary>
    [Fact]
    public void Gallery_ParallelEdgesPreserved_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.ParallelEdgesPreservedSvg,
            GalleryDiagrams.ParallelEdgesPreserved(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the same three parallel connectors with the default
    ///     <see cref="CoreOptions.MergeParallelEdges"/> (<see langword="true"/>), proving only the
    ///     first survives — the companion comparison case to
    ///     <see cref="Gallery_ParallelEdgesPreserved_RendersSvg"/>.
    /// </summary>
    [Fact]
    public void Gallery_ParallelEdgesMerged_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.ParallelEdgesMergedSvg,
            GalleryDiagrams.ParallelEdgesMerged(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the vertical-flow parallel-edges-preserved diagram to SVG — the companion to
    ///     <see cref="Gallery_ParallelEdgesPreserved_RendersSvg"/> — proving that with a downward
    ///     <see cref="CoreOptions.Direction"/> the three parallel connectors anchor on the boxes' top
    ///     and bottom faces, and that each box's WIDTH (not height) auto-grows to fit the widened
    ///     lane spacing on that axis.
    /// </summary>
    [Fact]
    public void Gallery_ParallelEdgesPreservedVertical_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.ParallelEdgesPreservedVerticalSvg,
            GalleryDiagrams.ParallelEdgesPreservedVertical(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the horizontal ports showcase to SVG, proving named left/right ports on a
    ///     rightward-flowing node emit a <see cref="LayoutPort"/> glyph and inward-reading label, and
    ///     that a long left-side label auto-computes a widened <see cref="LayoutBox.ContentInsetLeft"/>
    ///     margin auto-computed by the built-in heuristic estimator.
    /// </summary>
    [Fact]
    public void Gallery_PortsShowcaseHorizontal_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.PortsShowcaseHorizontalSvg,
            GalleryDiagrams.PortsShowcaseHorizontal(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the vertical ports showcase to SVG — the companion to
    ///     <see cref="Gallery_PortsShowcaseHorizontal_RendersSvg"/> — proving named top/bottom ports on
    ///     a downward-flowing node emit a <see cref="LayoutPort"/> glyph and inward-reading label.
    /// </summary>
    [Fact]
    public void Gallery_PortsShowcaseVertical_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.PortsShowcaseVerticalSvg,
            GalleryDiagrams.PortsShowcaseVertical(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the multi-connector horizontal ports showcase to SVG, proving two independently
    ///     labelled <see cref="LayoutGraphPort"/>s sharing a single left or right face spread evenly
    ///     without collapsing onto one row, and that the hub's title stays clear of both stacked port
    ///     rows on either side.
    /// </summary>
    [Fact]
    public void Gallery_PortsShowcaseMultiConnectorHorizontal_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.PortsShowcaseMultiConnectorHorizontalSvg,
            GalleryDiagrams.PortsShowcaseMultiConnectorHorizontal(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the multi-connector vertical ports showcase to SVG — the companion to
    ///     <see cref="Gallery_PortsShowcaseMultiConnectorHorizontal_RendersSvg"/> — proving the same
    ///     same-face crowding and title-collision protection when PortDistributor spreads anchors
    ///     horizontally along a top/bottom face instead of vertically along a left/right one.
    /// </summary>
    [Fact]
    public void Gallery_PortsShowcaseMultiConnectorVertical_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.PortsShowcaseMultiConnectorVerticalSvg,
            GalleryDiagrams.PortsShowcaseMultiConnectorVertical(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the unlabeled-port-fan-out showcase to SVG, proving a titled hub node with several
    ///     unlabeled ports on one face still grows tall enough to spread them apart instead of bunching
    ///     them together — regression coverage for the reported "Motherboard" clustering bug.
    /// </summary>
    [Fact]
    public void Gallery_PortsShowcaseUnlabeledFanOut_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.PortsShowcaseUnlabeledFanOutSvg,
            GalleryDiagrams.PortsShowcaseUnlabeledFanOut(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the horizontal boundary-ports showcase to SVG, proving a container's boundary
    ///     (delegation) port emits one shared <see cref="LayoutPort"/> anchor carrying both an outward
    ///     external label and an inward internal label, with the sibling's external approach edge and
    ///     both internal delegation edges (internal fan-out) reaching that one anchor.
    /// </summary>
    [Fact]
    public void Gallery_BoundaryPortsShowcaseHorizontal_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.BoundaryPortsShowcaseHorizontalSvg,
            GalleryDiagrams.BoundaryPortsShowcaseHorizontal(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the vertical boundary-ports showcase to SVG — the companion to
    ///     <see cref="Gallery_BoundaryPortsShowcaseHorizontal_RendersSvg"/> — proving a downward-flowing
    ///     container's top-face boundary port routes external fan-out (two sibling approach edges)
    ///     orthogonally onto one shared anchor that then delegates inward to the nested child.
    /// </summary>
    [Fact]
    public void Gallery_BoundaryPortsShowcaseVertical_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.BoundaryPortsShowcaseVerticalSvg,
            GalleryDiagrams.BoundaryPortsShowcaseVertical(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the three-level boundary-ports delegation chain to SVG — proving an outer container's
    ///     boundary port delegates inward to a nested container's own boundary port, which delegates
    ///     again to the innermost leaf, so the whole chain routes as one unbroken orthogonal path
    ///     through the recursive engine's single combined pass across both boundary crossings.
    /// </summary>
    [Fact]
    public void Gallery_BoundaryPortsShowcaseDeepChain_RendersSvg()
    {
        GalleryWriter.Svg(
            GalleryCatalog.BoundaryPortsShowcaseDeepChainSvg,
            GalleryDiagrams.BoundaryPortsShowcaseDeepChain(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the horizontal ports showcase to PNG, proving the raster path handles named
    ///     left/right ports.
    /// </summary>
    [Fact]
    public void Gallery_PortsShowcaseHorizontal_RendersPng()
    {
        GalleryWriter.Png(
            GalleryCatalog.PortsShowcaseHorizontalPng,
            GalleryDiagrams.PortsShowcaseHorizontal(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the vertical ports showcase to PNG, proving the raster path handles named
    ///     top/bottom ports.
    /// </summary>
    [Fact]
    public void Gallery_PortsShowcaseVertical_RendersPng()
    {
        GalleryWriter.Png(
            GalleryCatalog.PortsShowcaseVerticalPng,
            GalleryDiagrams.PortsShowcaseVertical(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the multi-connector horizontal ports showcase to PNG, proving the raster path
    ///     handles two labelled ports sharing a single left or right face.
    /// </summary>
    [Fact]
    public void Gallery_PortsShowcaseMultiConnectorHorizontal_RendersPng()
    {
        GalleryWriter.Png(
            GalleryCatalog.PortsShowcaseMultiConnectorHorizontalPng,
            GalleryDiagrams.PortsShowcaseMultiConnectorHorizontal(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the multi-connector vertical ports showcase to PNG, proving the raster path
    ///     handles two labelled ports sharing a single top or bottom face.
    /// </summary>
    [Fact]
    public void Gallery_PortsShowcaseMultiConnectorVertical_RendersPng()
    {
        GalleryWriter.Png(
            GalleryCatalog.PortsShowcaseMultiConnectorVerticalPng,
            GalleryDiagrams.PortsShowcaseMultiConnectorVertical(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the unlabeled-port-fan-out showcase to PNG, proving the raster path also grows a
    ///     titled hub with several unlabeled ports tall enough to spread them apart.
    /// </summary>
    [Fact]
    public void Gallery_PortsShowcaseUnlabeledFanOut_RendersPng()
    {
        GalleryWriter.Png(
            GalleryCatalog.PortsShowcaseUnlabeledFanOutPng,
            GalleryDiagrams.PortsShowcaseUnlabeledFanOut(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the parallel-edges-merged diagram to PNG, proving the raster path handles merged
    ///     parallel connectors.
    /// </summary>
    [Fact]
    public void Gallery_ParallelEdgesMerged_RendersPng()
    {
        GalleryWriter.Png(
            GalleryCatalog.ParallelEdgesMergedPng,
            GalleryDiagrams.ParallelEdgesMerged(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the parallel-edges-preserved diagram to PNG, proving the raster path handles
    ///     independently-routed parallel connectors.
    /// </summary>
    [Fact]
    public void Gallery_ParallelEdgesPreserved_RendersPng()
    {
        GalleryWriter.Png(
            GalleryCatalog.ParallelEdgesPreservedPng,
            GalleryDiagrams.ParallelEdgesPreserved(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the vertical-flow parallel-edges-preserved diagram to PNG, proving the raster path
    ///     handles the top/bottom-anchored companion case.
    /// </summary>
    [Fact]
    public void Gallery_ParallelEdgesPreservedVertical_RendersPng()
    {
        GalleryWriter.Png(
            GalleryCatalog.ParallelEdgesPreservedVerticalPng,
            GalleryDiagrams.ParallelEdgesPreservedVertical(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the horizontal boundary-ports showcase to PNG, proving the raster path handles a
    ///     container's shared boundary (delegation) port anchor.
    /// </summary>
    [Fact]
    public void Gallery_BoundaryPortsShowcaseHorizontal_RendersPng()
    {
        GalleryWriter.Png(
            GalleryCatalog.BoundaryPortsShowcaseHorizontalPng,
            GalleryDiagrams.BoundaryPortsShowcaseHorizontal(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the vertical boundary-ports showcase to PNG, proving the raster path handles the
    ///     downward-flowing companion case's top-face boundary port.
    /// </summary>
    [Fact]
    public void Gallery_BoundaryPortsShowcaseVertical_RendersPng()
    {
        GalleryWriter.Png(
            GalleryCatalog.BoundaryPortsShowcaseVerticalPng,
            GalleryDiagrams.BoundaryPortsShowcaseVertical(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the three-level boundary-ports delegation chain to PNG, proving the raster path
    ///     handles a chain of two boundary crossings routed in one combined recursive pass.
    /// </summary>
    [Fact]
    public void Gallery_BoundaryPortsShowcaseDeepChain_RendersPng()
    {
        GalleryWriter.Png(
            GalleryCatalog.BoundaryPortsShowcaseDeepChainPng,
            GalleryDiagrams.BoundaryPortsShowcaseDeepChain(),
            Themes.Dark);
    }
}
