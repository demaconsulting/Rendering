// <copyright file="GalleryShowcaseTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout;

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
    ///     Renders the many-edges-into-one-short-box diagram (issue #53) to SVG and asserts the layout invariant
    ///     the image demonstrates: on every face of the 130x50 definition box, the anchors of the ten typing
    ///     edges are at least 16 px apart (<c>MinFixedAnchorSeparation</c>), the overflow having wrapped onto
    ///     the top and bottom faces, and every edge still lands on the definition's boundary.
    /// </summary>
    [Fact]
    public void Gallery_ManyEdgesIntoShortBox_RendersSvgWithSeparatedAnchors()
    {
        // Arrange
        var graph = GalleryDiagrams.ManyEdgesIntoShortBox();
        graph.Set(CoreOptions.Algorithm, "auto");

        // Act: render the committed image, and lay the same graph out again to inspect the anchors.
        GalleryWriter.Svg(
            GalleryCatalog.ManyEdgesIntoShortBoxSvg,
            graph,
            Themes.Dark);
        var tree = LayoutEngine.Layout(graph);

        // Assert
        var definition = FindBox(tree.Nodes, "Definition");
        var faces = new Dictionary<string, List<double>>();
        var lines = new List<LayoutLine>();
        CollectEdgeLines(tree.Nodes, lines);
        Assert.Equal(10, lines.Count);
        foreach (var line in lines)
        {
            var end = line.Waypoints[^1];
            var (face, along) = ClassifyAnchor(definition, end);
            Assert.True(face.Length > 0, $"A typing edge ends at {end}, not on the definition's boundary.");
            if (!faces.TryGetValue(face, out var coords))
            {
                coords = [];
                faces[face] = coords;
            }

            coords.Add(along);
        }

        Assert.True(faces["west"].Count <= 3, "The 50 px west face holds at most three anchors 16 px apart.");
        foreach (var (face, coords) in faces)
        {
            coords.Sort();
            for (var i = 1; i < coords.Count; i++)
            {
                Assert.True(coords[i] - coords[i - 1] >= 16.0 - 1e-6, $"{face}: anchors {coords[i - 1]} and {coords[i]} are too close.");
            }
        }
    }

    /// <summary>Returns the face name and along-face coordinate of a point on the box boundary, or an empty face name.</summary>
    private static (string Face, double Along) ClassifyAnchor(LayoutBox box, Point2D point)
    {
        if (Math.Abs(point.X - box.X) < 0.01)
        {
            return ("west", point.Y);
        }

        if (Math.Abs(point.X - (box.X + box.Width)) < 0.01)
        {
            return ("east", point.Y);
        }

        if (Math.Abs(point.Y - box.Y) < 0.01)
        {
            return ("north", point.X);
        }

        return Math.Abs(point.Y - (box.Y + box.Height)) < 0.01 ? ("south", point.X) : (string.Empty, 0.0);
    }

    private static LayoutBox FindBox(IEnumerable<LayoutNode> nodes, string label) =>
        FindBoxOrNull(nodes, label) ?? throw new InvalidOperationException($"No box labeled '{label}'.");

    private static LayoutBox? FindBoxOrNull(IEnumerable<LayoutNode> nodes, string label)
    {
        foreach (var box in nodes.OfType<LayoutBox>())
        {
            if (box.Label == label)
            {
                return box;
            }

            var nested = FindBoxOrNull(box.Children, label);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void CollectEdgeLines(IEnumerable<LayoutNode> nodes, List<LayoutLine> lines)
    {
        foreach (var node in nodes)
        {
            if (node is LayoutLine line)
            {
                lines.Add(line);
            }
            else if (node is LayoutBox box)
            {
                CollectEdgeLines(box.Children, lines);
            }
        }
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
    ///     Proves the obstacle-routing diagram's connectors are clean and keep every arrowhead inside its
    ///     face: Left-to-Obstacle and Obstacle-to-Right are single straight segments, while the faces of
    ///     Source and Sink hold no lane that is both 24 px under the Obstacle and clear of the corners, so
    ///     Source-to-Sink is a clean Z (no tiny jog, ports at least the marker's reach from the corners,
    ///     its long run at least 24 px under the Obstacle's border, not a detour).
    /// </summary>
    [Fact]
    public void Gallery_OrthogonalObstacle_ConnectorsAreCleanAndCornerSafe()
    {
        // Arrange
        var graph = GalleryDiagrams.OrthogonalObstacle();

        // Act
        var tree = DemaConsulting.Rendering.Layout.LayoutEngine.Layout(graph);

        // Assert: three connectors; the two container-to-container ones are straight.
        var lines = CollectLines(tree.Nodes).ToList();
        Assert.Equal(3, lines.Count);
        var boxes = CollectBoxes(tree.Nodes).ToList();
        var obstacle = boxes.Single(b => b.Label == "Obstacle");
        var source = boxes.Single(b => b.Label == "Source");
        var sink = boxes.Single(b => b.Label == "Sink");
        var straight = lines.Where(l => l.Waypoints.Count == 2).ToList();
        Assert.Equal(2, straight.Count);

        // The Source -> Sink connector is the routed one: a clean Z of at most four bends.
        var routed = Assert.Single(lines, l => l.Waypoints.Count > 2);
        var w = routed.Waypoints;
        Assert.True(w.Count <= 6, $"Source -> Sink needs at most four bends, got {w.Count - 2}.");

        // Every interior segment is at least 24 px, the final leg holds the arrowhead plus the bend radius.
        for (var i = 1; i <= w.Count - 3; i++)
        {
            Assert.True(Length(w, i) >= 24.0 - 1e-6, $"Interior segment {i} is {Length(w, i)} long, a tiny jog.");
        }

        Assert.True(Length(w, w.Count - 2) >= NotationMetrics.AlongLineLength(routed.TargetEnd) + Themes.Dark.LineCornerRadius, "The final leg is shorter than the arrowhead plus the bend radius.");

        // Both ports keep the arrowhead (stroke-scaled half-width plus the border's half stroke) inside the face.
        var reach = (Themes.Dark.StrokeWidth * (NotationMetrics.EndMarkerHalfWidth + (Themes.Dark.StrokeWidth / 2.0))) + (Themes.Dark.StrokeWidth / 2.0);
        Assert.True(Math.Min(w[0].Y - source.Y, source.Y + source.Height - w[0].Y) >= reach - 1e-6, "The Source port is nearer a corner than the arrowhead reach.");
        Assert.True(Math.Min(w[^1].Y - sink.Y, sink.Y + sink.Height - w[^1].Y) >= reach - 1e-6, "The Sink port is nearer a corner than the arrowhead reach.");

        // Every horizontal run beside the Obstacle is at least 24 px (twice the clearance) from its bottom border.
        for (var i = 0; i < w.Count - 1; i++)
        {
            var horizontal = Math.Abs(w[i].Y - w[i + 1].Y) < 1e-6;
            var beside = Math.Max(w[i].X, w[i + 1].X) > obstacle.X && Math.Min(w[i].X, w[i + 1].X) < obstacle.X + obstacle.Width;
            if (horizontal && beside)
            {
                Assert.True(Math.Abs(w[i].Y - (obstacle.Y + obstacle.Height)) >= 24.0 - 1e-6, $"The run at y={w[i].Y} hugs the Obstacle border.");
            }
        }

        // Not a detour.
        var length = Enumerable.Range(0, w.Count - 1).Sum(i => Length(w, i));
        var manhattan = Math.Abs(w[^1].X - w[0].X) + Math.Abs(w[^1].Y - w[0].Y);
        Assert.True(length <= (1.25 * manhattan) + 0.5, $"Source -> Sink is a detour ({length} vs {manhattan}).");
    }

    /// <summary>Returns the length of the segment starting at waypoint <paramref name="i"/>.</summary>
    private static double Length(IReadOnlyList<Point2D> waypoints, int i) =>
        Math.Abs(waypoints[i + 1].X - waypoints[i].X) + Math.Abs(waypoints[i + 1].Y - waypoints[i].Y);

    /// <summary>Enumerates every box in the node list, recursing into box children.</summary>
    private static IEnumerable<LayoutBox> CollectBoxes(IEnumerable<LayoutNode> nodes)
    {
        foreach (var box in nodes.OfType<LayoutBox>())
        {
            yield return box;
            foreach (var nested in CollectBoxes(box.Children))
            {
                yield return nested;
            }
        }
    }

    /// <summary>Enumerates every line in the node list, recursing into box children.</summary>
    private static IEnumerable<LayoutLine> CollectLines(IEnumerable<LayoutNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is LayoutLine line)
            {
                yield return line;
            }
            else if (node is LayoutBox box)
            {
                foreach (var nested in CollectLines(box.Children))
                {
                    yield return nested;
                }
            }
        }
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
    ///     Renders the layered pipeline diagram to PNG, proving the CanvasNet raster path produces a
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
    ///     Renders the layered pipeline diagram to JPEG, proving the CanvasNet raster path's
    ///     <see cref="DemaConsulting.Rendering.CanvasNet.JpegRenderer"/> produces valid, non-empty
    ///     JPEG output end-to-end alongside the existing PNG and SVG showcase coverage.
    /// </summary>
    [Fact]
    public void Gallery_LayeredPipeline_RendersJpeg()
    {
        // Arrange
        var graph = GalleryDiagrams.LayeredPipeline();
        graph.Set(CoreOptions.Algorithm, "layered");

        // Act / Assert
        GalleryWriter.Jpeg(
            GalleryCatalog.LayeredPipelineJpeg,
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
    ///     labeled <see cref="LayoutGraphPort"/>s sharing a single left or right face spread evenly
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
    ///     handles two labeled ports sharing a single left or right face.
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
    ///     handles two labeled ports sharing a single top or bottom face.
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
    ///     Renders a three-level-deep nested container to SVG, where the middle and innermost scopes
    ///     each mix a connected pair with an unrelated singleton and neither re-declares
    ///     <see cref="CoreOptions.Algorithm"/>. Proves "auto" is re-evaluated at every scope it cascades
    ///     to (routing each scope's connected pair through layered and its singleton through
    ///     containment independently) rather than being resolved once at the root and applied
    ///     uniformly to every descendant scope. Regression coverage for a bug where a nested scope that
    ///     inherited "auto" without re-declaring it either threw <see cref="KeyNotFoundException"/> or,
    ///     under an earlier incomplete fix, was silently flattened to a single fixed leaf algorithm.
    /// </summary>
    [Fact]
    public void Gallery_AutoDeepNestedMixedConnectivity_RendersSvg()
    {
        // Arrange
        var graph = GalleryDiagrams.AutoDeepNestedMixedConnectivity();
        graph.Set(CoreOptions.Algorithm, "auto");

        // Act / Assert
        GalleryWriter.Svg(
            GalleryCatalog.AutoDeepNestedMixedConnectivitySvg,
            graph,
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the cross-hierarchy edges diagram to SVG with the "auto" root algorithm, proving
    ///     root-declared edges between nested nodes (and nested containers) are kept and drawn between
    ///     the actual nested boxes.
    /// </summary>
    [Fact]
    public void Gallery_CrossHierarchyEdges_RendersSvg()
    {
        // Arrange
        var graph = GalleryDiagrams.CrossHierarchyEdges();
        graph.Set(CoreOptions.Algorithm, "auto");

        // Act / Assert
        GalleryWriter.Svg(
            GalleryCatalog.CrossHierarchyEdgesSvg,
            graph,
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the downward-flowing cross-hierarchy edges diagram to SVG with the "hierarchical" root
    ///     algorithm, proving edges between leaves and nested nodes route cleanly through container title
    ///     bands and walls.
    /// </summary>
    [Fact]
    public void Gallery_CrossHierarchyEdgesHierarchical_RendersSvg()
    {
        // Arrange
        var graph = GalleryDiagrams.CrossHierarchyEdgesHierarchical();
        graph.Set(CoreOptions.Algorithm, "hierarchical");

        // Act / Assert
        GalleryWriter.Svg(
            GalleryCatalog.CrossHierarchyEdgesHierarchicalSvg,
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

    /// <summary>
    ///     Renders the SysML relationship end-marker sheet to SVG, proving every new end marker (hollow
    ///     triangle with two or four dots, circled plus) draws at both a target and a source line end.
    /// </summary>
    [Fact]
    public void Gallery_SysmlEndMarkers_RendersSvg()
    {
        // Act / Assert
        GalleryWriter.Svg(
            GalleryCatalog.SysmlEndMarkersSvg,
            GallerySysmlNotation.EndMarkers(Themes.Light),
            Themes.Light);
    }

    /// <summary>
    ///     Renders the SysML relationship end-marker sheet to PNG through the raster renderer.
    /// </summary>
    [Fact]
    public void Gallery_SysmlEndMarkers_RendersPng()
    {
        // Act / Assert
        GalleryWriter.Png(
            GalleryCatalog.SysmlEndMarkersPng,
            GallerySysmlNotation.EndMarkers(Themes.Light),
            Themes.Light);
    }

    /// <summary>
    ///     Renders the notation-primitives sheet to SVG, proving dashed outlines, text styles, centered
    ///     compartment titles, name-in-tab folders, thick bars, the terminate badge, corner icons,
    ///     dashed bands and rounded lifelines all render.
    /// </summary>
    [Fact]
    public void Gallery_SysmlNotationPrimitives_RendersSvg()
    {
        // Act / Assert
        GalleryWriter.Svg(
            GalleryCatalog.SysmlNotationPrimitivesSvg,
            GallerySysmlNotation.NotationPrimitives(Themes.Light),
            Themes.Light);
    }

    /// <summary>
    ///     Renders the notation-primitives sheet to PNG through the raster renderer.
    /// </summary>
    [Fact]
    public void Gallery_SysmlNotationPrimitives_RendersPng()
    {
        // Act / Assert
        GalleryWriter.Png(
            GalleryCatalog.SysmlNotationPrimitivesPng,
            GallerySysmlNotation.NotationPrimitives(Themes.Light),
            Themes.Light);
    }

    /// <summary>
    ///     Renders the graph-driven box-appearance diagram to SVG, proving a node's
    ///     <see cref="LayoutGraphNode.Appearance"/> reaches the placed boxes through the hierarchical
    ///     algorithm and that an in-tab folder title reserves no body title band.
    /// </summary>
    [Fact]
    public void Gallery_BoxAppearanceGraph_RendersSvg()
    {
        // Arrange
        var graph = GallerySysmlNotation.BoxAppearanceGraph();
        graph.Set(CoreOptions.Algorithm, "hierarchical");

        // Act / Assert
        GalleryWriter.Svg(
            GalleryCatalog.BoxAppearanceGraphSvg,
            graph,
            Themes.Dark);
    }

    /// <summary>
    ///     Renders the back-edge entry-approach state machine to SVG, proving the reversed edges end in a
    ///     wrap-around corridor long enough for the open chevron they carry.
    /// </summary>
    [Fact]
    public void Gallery_BackEdgeEntryApproach_RendersSvg()
    {
        // Act / Assert
        GalleryWriter.Svg(
            GalleryCatalog.BackEdgeEntryApproachSvg,
            GallerySysmlNotation.BackEdgeEntryApproach(),
            Themes.Light);
    }

    /// <summary>
    ///     Renders the horizontal SysML port-glyph interconnection to SVG and PNG, proving the appearance
    ///     and end labels reach the placed ports and lines and that no label overlaps a box, another label,
    ///     or a connector.
    /// </summary>
    [Fact]
    public void Gallery_SysmlPortsHorizontal_RendersSvgAndPngWithoutLabelOverlaps()
    {
        // Act
        GalleryWriter.Svg(GalleryCatalog.PortsSysmlGlyphsHorizontalSvg, GallerySysmlPorts.FuelSystem(LayoutFlowDirection.Right), Themes.Light);
        GalleryWriter.Png(GalleryCatalog.PortsSysmlGlyphsHorizontalPng, GallerySysmlPorts.FuelSystem(LayoutFlowDirection.Right), Themes.Light);

        // Assert
        AssertNoLabelOverlaps(LayoutEngine.Layout(GallerySysmlPorts.FuelSystem(LayoutFlowDirection.Right)), expectedOutwardPorts: 8);
    }

    /// <summary>
    ///     Renders the downward SysML port-glyph interconnection to SVG and PNG with the same label checks.
    /// </summary>
    [Fact]
    public void Gallery_SysmlPortsVertical_RendersSvgAndPngWithoutLabelOverlaps()
    {
        // Act
        GalleryWriter.Svg(GalleryCatalog.PortsSysmlGlyphsVerticalSvg, GallerySysmlPorts.FuelSystem(LayoutFlowDirection.Down), Themes.Light);
        GalleryWriter.Png(GalleryCatalog.PortsSysmlGlyphsVerticalPng, GallerySysmlPorts.FuelSystem(LayoutFlowDirection.Down), Themes.Light);

        // Assert
        AssertNoLabelOverlaps(LayoutEngine.Layout(GallerySysmlPorts.FuelSystem(LayoutFlowDirection.Down)), expectedOutwardPorts: 8);
    }

    /// <summary>
    ///     Renders the connection end-label ownership diagram to SVG and PNG, proving every end label is
    ///     placed clear of boxes, other labels, and connectors.
    /// </summary>
    [Fact]
    public void Gallery_ConnectionEndLabels_RendersSvgAndPngWithoutLabelOverlaps()
    {
        // Act
        GalleryWriter.Svg(GalleryCatalog.ConnectionEndLabelsSvg, GallerySysmlPorts.ConnectionEnds(), Themes.Light);
        GalleryWriter.Png(GalleryCatalog.ConnectionEndLabelsPng, GallerySysmlPorts.ConnectionEnds(), Themes.Light);

        // Assert
        AssertNoLabelOverlaps(LayoutEngine.Layout(GallerySysmlPorts.ConnectionEnds()), expectedOutwardPorts: 0);
    }

    /// <summary>
    ///     Renders the SysML port-glyph matrix (a hand-built sheet) to SVG and PNG.
    /// </summary>
    [Fact]
    public void Gallery_SysmlPortGlyphMatrix_RendersSvgAndPng()
    {
        // Act / Assert
        GalleryWriter.Svg(GalleryCatalog.SysmlPortGlyphMatrixSvg, GallerySysmlPorts.GlyphMatrix(Themes.Light), Themes.Light);
        GalleryWriter.Png(GalleryCatalog.SysmlPortGlyphMatrixPng, GallerySysmlPorts.GlyphMatrix(Themes.Light), Themes.Light);
    }

    /// <summary>
    ///     Renders the Rendering#57 regression scene, proving every connector's final run is straight and
    ///     no connector has a perpendicular step shorter than the marker.
    /// </summary>
    [Fact]
    public void Gallery_Regression57_SharedFaceStraightRuns_AllFinalRunsStraight()
    {
        // Act
        GalleryWriter.Svg(GalleryCatalog.SharedFaceStraightRunsSvg, GallerySysmlPorts.SharedFaceStraightRuns(), Themes.Light);
        GalleryWriter.Png(GalleryCatalog.SharedFaceStraightRunsPng, GallerySysmlPorts.SharedFaceStraightRuns(), Themes.Light);

        // Assert
        var tree = LayoutEngine.Layout(GallerySysmlPorts.SharedFaceStraightRuns());
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();
        Assert.Equal(5, lines.Count);
        Assert.All(lines, line => Assert.Equal(2, line.Waypoints.Count));
    }

    /// <summary>
    ///     Renders the Rendering#58 regression scene to SVG and PNG, proving every connector end at a
    ///     diamond lies on the diamond outline and leaves it perpendicular to the face.
    /// </summary>
    [Fact]
    public void Gallery_Regression58_DiamondOutlineAnchors_EndsOnOutline()
    {
        // Act
        GalleryWriter.Svg(GalleryCatalog.DiamondOutlineAnchorsSvg, GallerySysmlPorts.DiamondOutlineAnchors(), Themes.Light);
        GalleryWriter.Png(GalleryCatalog.DiamondOutlineAnchorsPng, GallerySysmlPorts.DiamondOutlineAnchors(), Themes.Light);

        // Assert
        var tree = LayoutEngine.Layout(GallerySysmlPorts.DiamondOutlineAnchors());
        var diamonds = tree.Nodes.OfType<LayoutBox>().Where(box => box.Shape == BoxShape.Diamond).ToList();
        Assert.Equal(2, diamonds.Count);
        foreach (var line in tree.Nodes.OfType<LayoutLine>())
        {
            foreach (var diamond in diamonds)
            {
                foreach (var (point, next) in new[] { (line.Waypoints[0], line.Waypoints[1]), (line.Waypoints[^1], line.Waypoints[^2]) })
                {
                    var norm = (Math.Abs(point.X - (diamond.X + (diamond.Width / 2.0))) / (diamond.Width / 2.0))
                        + (Math.Abs(point.Y - (diamond.Y + (diamond.Height / 2.0))) / (diamond.Height / 2.0));
                    if (Math.Abs(norm - 1.0) * (diamond.Width / 2.0) <= 0.6)
                    {
                        Assert.True(Math.Abs(point.X - next.X) < 1e-6, "a connector must leave a diamond outline along the axis");
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Asserts that no outward port label, end label, or connector overlaps another: outward labels
    ///     overlap no box, other port label, or connector segment; end labels (placed against the port
    ///     rectangles) overlap no box, glyph, port label, other end label, or connector segment other than
    ///     the end segment they sit beside.
    /// </summary>
    private static void AssertNoLabelOverlaps(LayoutTree tree, int expectedOutwardPorts)
    {
        var boxes = tree.Nodes.OfType<LayoutBox>().ToList();
        var lines = tree.Nodes.OfType<LayoutLine>().ToList();
        var ports = tree.Nodes.OfType<LayoutPort>().ToList();
        var portLabels = ports.Where(NotationMetrics.IsOutwardLabel)
            .Select(port => NotationMetrics.PortOutwardLabelBounds(port, port.ExternalLabel!, Themes.Light))
            .ToList();
        Assert.True(portLabels.Count >= expectedOutwardPorts, $"expected at least {expectedOutwardPorts} outward port labels, found {portLabels.Count}");

        static bool Overlap(LabelPlacement a, LabelPlacement b) =>
            Math.Abs(a.X - b.X) < a.HalfWidth + b.HalfWidth && Math.Abs(a.Y - b.Y) < a.HalfHeight + b.HalfHeight;

        static bool OverlapsBox(LabelPlacement a, LayoutBox box) =>
            a.X - a.HalfWidth < box.X + box.Width && a.X + a.HalfWidth > box.X && a.Y - a.HalfHeight < box.Y + box.Height && a.Y + a.HalfHeight > box.Y;

        static bool Crosses(LabelPlacement a, Point2D p, Point2D q) =>
            a.X - a.HalfWidth <= Math.Max(p.X, q.X) && a.X + a.HalfWidth >= Math.Min(p.X, q.X) &&
            a.Y - a.HalfHeight <= Math.Max(p.Y, q.Y) && a.Y + a.HalfHeight >= Math.Min(p.Y, q.Y);

        for (var i = 0; i < portLabels.Count; i++)
        {
            Assert.DoesNotContain(boxes, box => OverlapsBox(portLabels[i], box));
            for (var j = i + 1; j < portLabels.Count; j++)
            {
                Assert.False(Overlap(portLabels[i], portLabels[j]), "two outward port labels overlap");
            }

            foreach (var line in lines)
            {
                for (var s = 0; s + 1 < line.Waypoints.Count; s++)
                {
                    Assert.False(Crosses(portLabels[i], line.Waypoints[s], line.Waypoints[s + 1]), "an outward port label is crossed by a connector");
                }
            }
        }

        var occupied = portLabels.Concat(ports.Select(NotationMetrics.PortGlyphBounds)).ToList();
        var endLabels = ConnectorLabelPlacer.PlaceEndLabels(lines, Themes.Light.FontSizeBody, boxes, NotationMetrics.MarkerScale(Themes.Light), occupied);
        for (var i = 0; i < endLabels.Count; i++)
        {
            var label = new LabelPlacement(endLabels[i].X, endLabels[i].Y, endLabels[i].HalfWidth, endLabels[i].HalfHeight);
            Assert.DoesNotContain(boxes, box => OverlapsBox(label, box));
            Assert.DoesNotContain(occupied, rect => Overlap(label, rect));
            for (var j = i + 1; j < endLabels.Count; j++)
            {
                Assert.False(
                    Overlap(label, new LabelPlacement(endLabels[j].X, endLabels[j].Y, endLabels[j].HalfWidth, endLabels[j].HalfHeight)),
                    $"end labels '{endLabels[i].Text}' and '{endLabels[j].Text}' overlap");
            }

            foreach (var line in lines)
            {
                for (var s = 0; s + 1 < line.Waypoints.Count; s++)
                {
                    var isOwnEndSegment = ReferenceEquals(line, endLabels[i].Line)
                        && (endLabels[i].End == LineEnd.Source ? s == 0 : s == line.Waypoints.Count - 2);
                    if (!isOwnEndSegment)
                    {
                        Assert.False(Crosses(label, line.Waypoints[s], line.Waypoints[s + 1]), $"end label '{endLabels[i].Text}' is crossed by a connector");
                    }
                }
            }
        }
    }
}
