// <copyright file="GalleryCatalog.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Gallery;

/// <summary>
///     One generated showcase image: its stable output filename plus the alt text and caption used in
///     the browsable <c>README.md</c> index.
/// </summary>
/// <param name="FileName">Stable output filename (for example <c>layered-pipeline.svg</c>).</param>
/// <param name="Alt">Alt text for the Markdown image link.</param>
/// <param name="Caption">One-line caption shown beneath the image link in the index.</param>
internal sealed record GalleryImage(string FileName, string Alt, string Caption);

/// <summary>
///     A titled group of related showcase images with a short introduction, rendered as one section of
///     the <c>README.md</c> index.
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
    public const string IsolatedNodeLayerGapSvg = "isolated-node-layer-gap.svg";
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
    public const string ShapeGallerySvg = "shape-gallery.svg";
    public const string ParallelEdgesMergedSvg = "parallel-edges-merged.svg";
    public const string ParallelEdgesPreservedSvg = "parallel-edges-preserved.svg";
    public const string ParallelEdgesPreservedVerticalSvg = "parallel-edges-preserved-vertical.svg";
    public const string PortsShowcaseHorizontalSvg = "ports-showcase-horizontal.svg";
    public const string PortsShowcaseVerticalSvg = "ports-showcase-vertical.svg";
    public const string PortsShowcaseMultiConnectorHorizontalSvg = "ports-showcase-multi-connector-horizontal.svg";
    public const string PortsShowcaseMultiConnectorVerticalSvg = "ports-showcase-multi-connector-vertical.svg";
    public const string PortsShowcaseUnlabeledFanOutSvg = "ports-showcase-unlabeled-fan-out.svg";
    public const string BoundaryPortsShowcaseHorizontalSvg = "boundary-ports-showcase-horizontal.svg";
    public const string BoundaryPortsShowcaseVerticalSvg = "boundary-ports-showcase-vertical.svg";
    public const string BoundaryPortsShowcaseDeepChainSvg = "boundary-ports-showcase-deep-chain.svg";
    public const string PortsShowcaseHorizontalPng = "ports-showcase-horizontal.png";
    public const string PortsShowcaseVerticalPng = "ports-showcase-vertical.png";
    public const string PortsShowcaseMultiConnectorHorizontalPng = "ports-showcase-multi-connector-horizontal.png";
    public const string PortsShowcaseMultiConnectorVerticalPng = "ports-showcase-multi-connector-vertical.png";
    public const string PortsShowcaseUnlabeledFanOutPng = "ports-showcase-unlabeled-fan-out.png";
    public const string ParallelEdgesMergedPng = "parallel-edges-merged.png";
    public const string ParallelEdgesPreservedPng = "parallel-edges-preserved.png";
    public const string ParallelEdgesPreservedVerticalPng = "parallel-edges-preserved-vertical.png";
    public const string BoundaryPortsShowcaseHorizontalPng = "boundary-ports-showcase-horizontal.png";
    public const string BoundaryPortsShowcaseVerticalPng = "boundary-ports-showcase-vertical.png";
    public const string BoundaryPortsShowcaseDeepChainPng = "boundary-ports-showcase-deep-chain.png";

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
            "Layout regressions",
            "Small graphs that reproduce and pin down a specific layout bug once fixed, kept as "
            + "permanent visual and numeric evidence that it stays fixed.",
            [
                new GalleryImage(
                    IsolatedNodeLayerGapSvg,
                    "A hub column with an isolated node beside a routing-corridor dummy",
                    "Regression coverage for the isolated-node layer-gap fix: a genuinely isolated "
                    + "node (Isolated, zero edges) shares its layer with a dummy bend point created by "
                    + "an unrelated long edge (LongSource to LongTarget). The crossing minimizer now "
                    + "clusters isolated nodes at the end of the layer's order, and coordinate "
                    + "assignment squeezes any resulting gap down to the standard node spacing, instead "
                    + "of inheriting the dummy's unrelated port-alignment floor as an inflated gap."),
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
                    "An edge approaching a folder container from above: the connector avoids the tab and "
                    + "anchors on the folder's recessed top edge instead of floating above it."),
                new GalleryImage(
                    ShapeGallerySvg,
                    "One of each container shape side by side, each holding content",
                    "Every Shape value side by side, each with content appropriate to it: rectangle and "
                    + "rounded-rectangle boxes with a keyword and a compartment, a folder holding a "
                    + "nested child, and a note holding free-form text — every shape reserves enough "
                    + "space so its content never overlaps the tab or the folded corner."),
            ]),
        new GallerySection(
            "Parallel edges and named ports",
            "The layered algorithm's Phase 1 flat-graph support for multiple parallel connectors "
            + "between the same two boxes, and for named ports attached to a specific, labelled "
            + "location on a node's boundary. Parallel edges either collapse to one rendered connector "
            + "(the default) or each keep their own independently-routed line, selected with the "
            + "MergeParallelEdges option; each node's ContentInset margins are auto-computed from its "
            + "ports' measured label widths so port text never overlaps the box's own content.",
            [
                new GalleryImage(
                    ParallelEdgesPreservedSvg,
                    "Three parallel connectors between the same two boxes, independently routed",
                    "MergeParallelEdges set to false: all three parallel connectors survive, each with "
                    + "its own label."),
                new GalleryImage(
                    ParallelEdgesPreservedVerticalSvg,
                    "The same three parallel connectors on a downward-flowing pair of boxes",
                    "The companion vertical-flow case: with a downward Direction the three parallel "
                    + "connectors anchor on the boxes' top and bottom faces instead of their left and "
                    + "right faces, and each box's WIDTH (not height) auto-grows to fit the widened "
                    + "lane spacing, since PortDistributor spreads anchors on a top/bottom face "
                    + "horizontally."),
                new GalleryImage(
                    ParallelEdgesMergedSvg,
                    "The same three parallel connectors collapsed to a single line",
                    "The default MergeParallelEdges (true): the three parallel connectors collapse to "
                    + "a single rendered line, and its midpoint label is omitted entirely (not any "
                    + "single surviving connector's label) since a reader could not tell which of the "
                    + "three collapsed connectors a kept label would have belonged to."),
                new GalleryImage(
                    PortsShowcaseHorizontalSvg,
                    "A hub node with a named port on each of its left and right sides",
                    "Left/right named ports on a rightward-flowing hub node; the long left-side "
                    + "incoming label auto-computes a widened ContentInsetLeft margin, measured with "
                    + "the Skia-backed text measurer."),
                new GalleryImage(
                    PortsShowcaseVerticalSvg,
                    "A hub node with a named port on each of its top and bottom sides",
                    "The companion top/bottom case: a downward-flowing hub node, whose ports anchor on "
                    + "its top and bottom faces instead."),
                new GalleryImage(
                    PortsShowcaseMultiConnectorHorizontalSvg,
                    "A hub node with two named ports on each of its left and right sides",
                    "Same-face crowding with two independently-labelled ports per side (one "
                    + "deliberately long): PortDistributor spreads both anchors on each face without "
                    + "collapsing them onto one row, and the hub's title stays clear of both stacked "
                    + "rows on either side."),
                new GalleryImage(
                    PortsShowcaseMultiConnectorVerticalSvg,
                    "A hub node with two named ports on each of its top and bottom sides",
                    "The companion top/bottom case: two ports per face spread horizontally instead of "
                    + "vertically, proving the same crowding and title-collision protection when "
                    + "PortDistributor works along the cross axis of a downward flow."),
                new GalleryImage(
                    PortsShowcaseUnlabeledFanOutSvg,
                    "A titled hub node with several unlabeled ports fanning out to four other boxes",
                    "A face with several ports carrying no label or text at all still grows the box "
                    + "tall enough to keep them from bunching together — the growth floor applies "
                    + "unconditionally to any face with two or more anchors, not only labeled ones."),
            ]),
        new GallerySection(
            "Boundary and delegation ports",
            "The hierarchical engine's support for boundary (delegation) ports: a container may expose "
            + "a named port carrying BOTH an external and an internal label at one shared physical "
            + "anchor on its boundary. An external approach edge from a sibling reaches the anchor from "
            + "outside, while one or more internal delegation edges relay the connection inward to the "
            + "container's nested children. The container and its children are laid out in one combined "
            + "recursive pass, and every converging edge — external approach and internal delegation "
            + "alike — is routed through the orthogonal corridor router onto that single shared anchor, "
            + "with the external label reading outward and the internal label reading inward.",
            [
                new GalleryImage(
                    BoundaryPortsShowcaseHorizontalSvg,
                    "A sibling joined to a container's boundary port delegating to two children",
                    "A rightward-flowing container exposes one boundary port on its left face carrying "
                    + "both a 'command' external label (reading outward) and a 'dispatch' internal "
                    + "label (reading inward) at the same shared anchor. The external approach edge from "
                    + "the sibling and both internal delegation edges (internal fan-out to two nested "
                    + "children) are routed orthogonally onto that one anchor."),
                new GalleryImage(
                    BoundaryPortsShowcaseVerticalSvg,
                    "Two siblings joined to a container's top-face boundary port and one child",
                    "The companion downward-flowing case: the boundary port anchors on the container's "
                    + "top face, with external fan-out (two sibling approach edges) both routed "
                    + "orthogonally onto the one shared anchor, which then delegates inward to the "
                    + "single nested child."),
                new GalleryImage(
                    BoundaryPortsShowcaseDeepChainSvg,
                    "A boundary port delegating through a nested boundary port to a leaf",
                    "A three-level delegation chain: a sibling approaches an outer container's boundary "
                    + "port, which delegates inward to a nested container's own boundary port, which "
                    + "delegates again to the innermost leaf. Both boundary crossings carry an outward "
                    + "external and an inward internal label, and the whole chain is routed orthogonally "
                    + "in one combined recursive pass with no diagonal shortcut at either boundary."),
            ]),
    ];
}
