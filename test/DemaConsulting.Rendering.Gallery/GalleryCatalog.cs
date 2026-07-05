// <copyright file="GalleryCatalog.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Gallery;

/// <summary>
///     One generated showcase image: its stable output filename plus the alt text and caption used in
///     the browsable <c>gallery.md</c> index.
/// </summary>
/// <param name="FileName">Stable output filename (for example <c>layered-pipeline.svg</c>).</param>
/// <param name="Alt">Alt text for the Markdown image link.</param>
/// <param name="Caption">One-line caption shown beneath the image link in the index.</param>
internal sealed record GalleryImage(string FileName, string Alt, string Caption);

/// <summary>
///     A titled group of related showcase images with a short introduction, rendered as one section of
///     the <c>gallery.md</c> index.
/// </summary>
/// <param name="Title">Section heading.</param>
/// <param name="Intro">One-paragraph description of what the section demonstrates.</param>
/// <param name="Images">The images belonging to the section, in display order.</param>
internal sealed record GallerySection(string Title, string Intro, IReadOnlyList<GalleryImage> Images);

/// <summary>
///     The single source of truth for the gallery showcase: the stable output filenames and the
///     structure of the browsable index. The showcase facts render to these filenames and the index
///     generator lays the same entries out as Markdown, so the two never drift apart.
/// </summary>
internal static class GalleryCatalog
{
    // Stable output filenames. Facts and the index both reference these constants so a rename is a
    // single edit that keeps the committed artifacts and the index in lockstep.
    public const string LayeredPipelineSvg = "layered-pipeline.svg";
    public const string ContainmentPackedSvg = "containment-packed.svg";
    public const string HierarchicalNestedSvg = "hierarchical-nested.svg";
    public const string OrthogonalObstacleSvg = "orthogonal-obstacle.svg";
    public const string DirectionRightSvg = "direction-right.svg";
    public const string DirectionDownSvg = "direction-down.svg";
    public const string MixedDirectionNestedSvg = "mixed-direction-nested.svg";
    public const string ThemeLightPng = "theme-light.png";
    public const string ThemeDarkPng = "theme-dark.png";
    public const string ThemePrintPng = "theme-print.png";
    public const string LayeredPipelinePng = "layered-pipeline.png";
    public const string HierarchicalNestedPng = "hierarchical-nested.png";
    public const string BoxAppearanceSvg = "box-appearance.svg";
    public const string FolderTopFaceAnchorSvg = "folder-top-face-anchor.svg";

    /// <summary>Gets the browsable sections of the gallery, in display order.</summary>
    public static IReadOnlyList<GallerySection> Sections { get; } =
    [
        new GallerySection(
            "Layout algorithms",
            "The bundled algorithms, each laying out the same kind of node-and-edge graph in its own "
            + "style. Select one with the algorithm option and let the engine place the boxes and route "
            + "the edges.",
            [
                new GalleryImage(
                    LayeredPipelineSvg,
                    "Layered pipeline diagram",
                    "A directed pipeline laid out left to right by the layered algorithm."),
                new GalleryImage(
                    ContainmentPackedSvg,
                    "Containment packed diagram",
                    "Sibling boxes packed compactly by the containment algorithm."),
                new GalleryImage(
                    HierarchicalNestedSvg,
                    "Hierarchical nested diagram",
                    "A container node holding a nested child graph, with a cross-container edge."),
            ]),
        new GallerySection(
            "Flow direction",
            "The same directed graph laid out in two flow directions, selected with the direction "
            + "option, plus a nested container overriding its own direction independently of its parent. "
            + "A rightward flow arranges the layers left-to-right for block and pipeline diagrams; a "
            + "downward flow arranges them top-to-bottom for action flows and state machines, swapping "
            + "each node's width and height so layer spacing follows node height.",
            [
                new GalleryImage(
                    DirectionRightSvg,
                    "Directed flow laid out left to right",
                    "The default rightward direction: layers progress left-to-right."),
                new GalleryImage(
                    DirectionDownSvg,
                    "The same directed flow laid out top to bottom",
                    "The downward direction: the same graph's layers progress top-to-bottom."),
                new GalleryImage(
                    MixedDirectionNestedSvg,
                    "A nested container flowing downward inside an outer rightward flow",
                    "A container's own direction override is honored independently of its parent: the "
                    + "outer flow runs left-to-right while the nested container runs top-to-bottom."),
            ]),
        new GallerySection(
            "Edge routing",
            "Orthogonal connectors step around the boxes between their endpoints instead of cutting "
            + "through them.",
            [
                new GalleryImage(
                    OrthogonalObstacleSvg,
                    "Orthogonal edge routed around an obstacle",
                    "A connector routed orthogonally around an intervening container box."),
            ]),
        new GallerySection(
            "Themes",
            "One representative diagram rendered with each of the three built-in themes, showing how the "
            + "theme controls colours, stroke, and corner style without touching the layout. These are "
            + "rendered through the raster path to PNG so each carries a solid theme background.",
            [
                new GalleryImage(
                    ThemeLightPng,
                    "Representative diagram in the light theme",
                    "The light theme, suited to on-screen viewing."),
                new GalleryImage(
                    ThemeDarkPng,
                    "Representative diagram in the dark theme",
                    "The dark theme, suited to dark-mode viewing."),
                new GalleryImage(
                    ThemePrintPng,
                    "Representative diagram in the print theme",
                    "The print theme, optimised for black-and-white output."),
            ]),
        new GallerySection(
            "Raster output",
            "The layout-algorithm diagrams above are rendered as SVG with the dark theme; here the same "
            + "two diagrams are rendered through the SkiaSharp raster path to PNG, proving multi-format "
            + "output.",
            [
                new GalleryImage(
                    LayeredPipelinePng,
                    "Layered pipeline diagram as PNG",
                    "The layered pipeline rendered to a raster PNG image."),
                new GalleryImage(
                    HierarchicalNestedPng,
                    "Hierarchical nested diagram as PNG",
                    "The hierarchical nested diagram rendered to a raster PNG image."),
            ]),
        new GallerySection(
            "Box appearance",
            "A node's Shape, Keyword, and Compartments properties select the box outline, an "
            + "italicized keyword line, and labelled feature sections, all through the plain input "
            + "graph model — no downstream renderer-specific code required. This is generic block-"
            + "diagram notation; SysML is just one modeling language that uses it.",
            [
                new GalleryImage(
                    BoxAppearanceSvg,
                    "A folder container with two boxes carrying a keyword, one also compartmented, joined by an edge",
                    "A folder container holding two boxes with a keyword line — one also with a labelled "
                    + "compartment — joined by a decorated edge."),
            ]),
        new GallerySection(
            "Shape-aware connectors",
            "A box's Shape can make its true outline diverge from its plain bounding rectangle — a "
            + "folder's tab, a note's folded corner, a rounded rectangle's corners. The router keeps "
            + "connectors off those non-connectable regions and projects each anchor down to the shape's "
            + "actual drawn outline, so every connector visibly touches the shape it targets.",
            [
                new GalleryImage(
                    FolderTopFaceAnchorSvg,
                    "An external node connected into a folder top face, clear of the tab",
                    "A cross-container edge approaching a folder container from above: the connector "
                    + "avoids the tab and anchors on the folder's recessed top edge instead of floating "
                    + "above it."),
            ]),
    ];
}
