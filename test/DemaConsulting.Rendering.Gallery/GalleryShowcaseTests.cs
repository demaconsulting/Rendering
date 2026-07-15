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
        // Arrange
        var graph = GalleryDiagrams.LayeredPipeline();
        graph.Set(CoreOptions.Algorithm, "layered");

        // Act / Assert
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
        // Arrange
        var graph = GalleryDiagrams.IsolatedNodeLayerGap();
        graph.Set(CoreOptions.Algorithm, "layered");

        // Act / Assert
        GalleryWriter.Svg(
            GalleryCatalog.IsolatedNodeLayerGapSvg,
            graph,
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the parallel-edges-into-compartment-box regression diagram to SVG, proving
    ///     <c>ConnectorRouter</c> keeps nine parallel connectors converging on a compartment box outside
    ///     its interior instead of cutting across its rows.
    /// </summary>
    [Fact]
    public void Gallery_ParallelEdgesIntoCompartmentBox_RendersSvg()
    {
        // Arrange
        var graph = GalleryDiagrams.ParallelEdgesIntoCompartmentBox();
        graph.Set(CoreOptions.Algorithm, "containment");

        // Act / Assert
        GalleryWriter.Svg(
            GalleryCatalog.ParallelEdgesIntoCompartmentBoxSvg,
            graph,
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the parallel-edges-into-compartment-box-side-by-side regression diagram to SVG,
    ///     proving <c>ConnectorRouter</c> clamps soft-obstacle lane candidates to the Source/Target box
    ///     pair's own envelope (and penalizes, without blocking, any move that still leaves it) so nine
    ///     parallel connectors routed across a narrow inter-box gap never loop a route back behind the
    ///     box it already left.
    /// </summary>
    [Fact]
    public void Gallery_ParallelEdgesIntoCompartmentBoxSideBySide_RendersSvg()
    {
        // Act / Assert
        GalleryWriter.Svg(
            GalleryCatalog.ParallelEdgesIntoCompartmentBoxSideBySideSvg,
            GalleryDiagrams.ParallelEdgesIntoCompartmentBoxSideBySide(Themes.Dark),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the containment-packed diagram to SVG, proving the containment algorithm packs
    ///     sibling boxes into a valid document.
    /// </summary>
    [Fact]
    public void Gallery_ContainmentPacked_RendersSvg()
    {
        // Arrange
        var graph = GalleryDiagrams.ContainmentPacked();
        graph.Set(CoreOptions.Algorithm, "containment");

        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Arrange
        var graph = GalleryDiagrams.DirectionShowcase();
        graph.Set(CoreOptions.Algorithm, "layered");
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Right);

        // Act / Assert
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
        // Arrange
        var graph = GalleryDiagrams.DirectionShowcase();
        graph.Set(CoreOptions.Algorithm, "layered");
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Arrange
        var graph = GalleryDiagrams.LayeredPipeline();
        graph.Set(CoreOptions.Algorithm, "layered");

        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
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
        // Act / Assert
        GalleryWriter.Png(
            GalleryCatalog.BoundaryPortsShowcaseDeepChainPng,
            GalleryDiagrams.BoundaryPortsShowcaseDeepChain(),
            Themes.Dark);
    }

    /// <summary>
    ///     Renders a small, fully-connected pipeline laid out by the layered algorithm to SVG, kept as
    ///     a visual baseline for comparison against the disconnected-graph diagrams in the same
    ///     "auto" meta-algorithm section.
    /// </summary>
    [Fact]
    public void Gallery_LayeredRegressionBaseline_RendersSvg()
    {
        // Arrange
        var graph = GalleryDiagrams.LayeredRegressionBaseline();
        graph.Set(CoreOptions.Algorithm, "layered");

        // Act / Assert
        GalleryWriter.Svg(
            GalleryCatalog.LayeredRegressionBaselineSvg,
            graph,
            Themes.Dark);
    }

    /// <summary>
    ///     Renders one connected cluster packed alongside three isolated singleton boxes to SVG,
    ///     proving the "auto" meta-algorithm routes the cluster through the layered algorithm and
    ///     gathers the singletons into one shared bucket routed through the containment algorithm,
    ///     then packs both pieces into one canvas.
    /// </summary>
    [Fact]
    public void Gallery_AutoClusterPlusIsolated_RendersSvg()
    {
        // Arrange
        var graph = GalleryDiagrams.AutoClusterPlusIsolated();
        graph.Set(CoreOptions.Algorithm, "auto");

        // Act / Assert
        GalleryWriter.Svg(
            GalleryCatalog.AutoClusterPlusIsolatedSvg,
            graph,
            Themes.Dark);
    }

    /// <summary>
    ///     Renders three disconnected two-node clusters, each routed and packed independently, to SVG,
    ///     proving the "auto" meta-algorithm treats each connected component as its own layered-routed
    ///     piece before packing all three results into one combined canvas.
    /// </summary>
    [Fact]
    public void Gallery_AutoMultipleDisconnectedClusters_RendersSvg()
    {
        // Arrange
        var graph = GalleryDiagrams.MultipleDisconnectedClusters();
        graph.Set(CoreOptions.Algorithm, "auto");

        // Act / Assert
        GalleryWriter.Svg(
            GalleryCatalog.AutoMultipleDisconnectedClustersSvg,
            graph,
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the same three disconnected clusters laid out directly by the layered algorithm to
    ///     SVG — the companion direct-"layered" sibling of
    ///     <see cref="Gallery_AutoMultipleDisconnectedClusters_RendersSvg"/>, proving the layered
    ///     algorithm's own internal component packing handles the same disconnected graph.
    /// </summary>
    [Fact]
    public void Gallery_LayeredMultipleDisconnectedClusters_RendersSvg()
    {
        // Arrange
        var graph = GalleryDiagrams.MultipleDisconnectedClusters();
        graph.Set(CoreOptions.Algorithm, "layered");

        // Act / Assert
        GalleryWriter.Svg(
            GalleryCatalog.LayeredMultipleDisconnectedClustersSvg,
            graph,
            Themes.Dark);
    }

    /// <summary>
    ///     Renders five entirely isolated boxes packed by the "auto" algorithm to SVG, proving that a
    ///     graph of nothing but childless, edgeless singletons is gathered into the shared bucket and
    ///     routed through the containment algorithm unchanged, taking its zero-copy fast path.
    /// </summary>
    [Fact]
    public void Gallery_AutoAllIsolated_RendersSvg()
    {
        // Arrange
        var graph = GalleryDiagrams.AutoAllIsolated();
        graph.Set(CoreOptions.Algorithm, "auto");

        // Act / Assert
        GalleryWriter.Svg(
            GalleryCatalog.AutoAllIsolatedSvg,
            graph,
            Themes.Dark);
    }

    /// <summary>
    ///     Renders a nested container routed to hierarchical alongside an unrelated isolated box to
    ///     SVG, proving the "auto" meta-algorithm routes any component containing a container node
    ///     through the hierarchical algorithm regardless of its size, while the unrelated isolated
    ///     sibling is packed alongside it through the shared containment bucket.
    /// </summary>
    [Fact]
    public void Gallery_AutoNestedRoutesHierarchical_RendersSvg()
    {
        // Arrange
        var graph = GalleryDiagrams.AutoNestedRoutesHierarchical();
        graph.Set(CoreOptions.Algorithm, "auto");

        // Act / Assert
        GalleryWriter.Svg(
            GalleryCatalog.AutoNestedRoutesHierarchicalSvg,
            graph,
            Themes.Dark);
    }

    /// <summary>
    ///     Renders twelve small, wide boxes packed by the containment algorithm to SVG, proving the
    ///     column-count-based content-width candidate keeps the algorithm from packing them into one
    ///     long, narrow column, wrapping them into a balanced grid of columns instead.
    /// </summary>
    [Fact]
    public void Gallery_ContainmentManySmallWideBoxes_RendersSvg()
    {
        // Arrange
        var graph = GalleryDiagrams.ContainmentManySmallWideBoxes();
        graph.Set(CoreOptions.Algorithm, "containment");

        // Act / Assert
        GalleryWriter.Svg(
            GalleryCatalog.ContainmentManySmallWideBoxesSvg,
            graph,
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the containment edge-count gap-widening diagram to SVG, proving the containment
    ///     algorithm packs two tall peer boxes side by side and widens the horizontal gap between them in
    ///     proportion to the eight parallel connectors routed through it, so the connectors fan into
    ///     distinct lanes.
    /// </summary>
    [Fact]
    public void Gallery_ContainmentParallelEdgesSideBySide_RendersSvg()
    {
        // Arrange
        var graph = GalleryDiagrams.ContainmentParallelEdgesSideBySide();
        graph.Set(CoreOptions.Algorithm, "containment");

        // Act / Assert
        GalleryWriter.Svg(
            GalleryCatalog.ContainmentParallelEdgesSideBySideSvg,
            graph,
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the hierarchical cross-container edge-count gap-widening diagram to SVG, proving the
    ///     hierarchical engine places two peer containers side by side and widens the gap between them
    ///     for the eight cross-container connectors that fan child-to-child through it.
    /// </summary>
    [Fact]
    public void Gallery_HierarchicalParallelEdgesSideBySide_RendersSvg()
    {
        // Arrange
        var graph = GalleryDiagrams.HierarchicalParallelEdgesSideBySide();
        graph.Set(CoreOptions.Algorithm, "hierarchical");

        // Act / Assert
        GalleryWriter.Svg(
            GalleryCatalog.HierarchicalParallelEdgesSideBySideSvg,
            graph,
            Themes.Dark);
    }
}
