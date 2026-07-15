// <copyright file="GalleryCatalog.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Gallery;

/// <summary>
///     One generated showcase image: its stable output filename (folder-prefixed with its owning
///     group's folder) plus the alt text and caption used in the browsable <c>README.md</c> pages.
/// </summary>
/// <param name="FileName">Stable output filename (for example <c>flow-pipeline/layered-pipeline.svg</c>).</param>
/// <param name="Alt">Alt text for the Markdown image link.</param>
/// <param name="Caption">One-line caption shown beneath the image link in the index.</param>
/// <param name="IsTopIndexHighlight">
///     Whether this image is one of a small, hand-picked set shown on the top-level index's "taste of
///     what's inside" section. Opt-in by design: an image only ever appears on the top-level index if a
///     person deliberately chose it (with this same, real caption, not a generic placeholder), so a
///     deliberate contrast/baseline image can never surface there bare and out of context by accident.
/// </param>
internal sealed record GalleryImage(
    string FileName,
    string Alt,
    string Caption,
    bool IsTopIndexHighlight = false);

/// <summary>
///     A titled section of related showcase images with a short introduction, rendered as one section
///     of a group's <c>README.md</c> page.
/// </summary>
/// <param name="Title">Section heading.</param>
/// <param name="Intro">One-paragraph description of what the section demonstrates.</param>
/// <param name="Images">The images belonging to the section, in display order.</param>
internal sealed record GallerySection(string Title, string Intro, IReadOnlyList<GalleryImage> Images);

/// <summary>
///     A topology-based group of related sections, rendered as its own browsable
///     <c>docs/gallery/&lt;Folder&gt;/README.md</c> page and summarized on the top-level index.
/// </summary>
/// <param name="Folder">The group's output subfolder under <c>docs/gallery</c> (for example <c>flow-pipeline</c>).</param>
/// <param name="Title">Group heading, shown on both the top-level index and the group's own page.</param>
/// <param name="Intro">One-paragraph description of what the group demonstrates, shown on its own page.</param>
/// <param name="ShortSummary">
///     A single short phrase describing the group, shown in the top-level index's group table (keep this
///     brief — it is a table cell, not a paragraph).
/// </param>
/// <param name="Sections">The group's sections, in display order.</param>
internal sealed record GalleryGroup(
    string Folder,
    string Title,
    string Intro,
    string ShortSummary,
    IReadOnlyList<GallerySection> Sections);

/// <summary>
///     The single source of truth for the gallery showcase: the stable output filenames and the
///     structure of the browsable index. The showcase facts render to these filenames and the index
///     generator lays the same entries out as Markdown, so the two never drift apart.
/// </summary>
internal static class GalleryCatalog
{
    // Stable output filenames. Facts and the index both reference these constants so a rename is a
    // single edit that keeps the committed artifacts and the index in lockstep. Each is prefixed with
    // its owning group's folder so the generated file lands directly in the group's own subfolder.
    public const string LayeredPipelineSvg = "flow-pipeline/layered-pipeline.svg";
    public const string DirectionRightSvg = "flow-pipeline/direction-right.svg";
    public const string DirectionDownSvg = "flow-pipeline/direction-down.svg";
    public const string MixedDirectionNestedSvg = "flow-pipeline/mixed-direction-nested.svg";
    public const string OrthogonalObstacleSvg = "flow-pipeline/orthogonal-obstacle.svg";
    public const string LayeredPipelinePng = "flow-pipeline/layered-pipeline.png";

    public const string ContainmentPackedSvg = "connectivity-and-clusters/containment-packed.svg";
    public const string LayeredRegressionBaselineSvg =
        "connectivity-and-clusters/layered-regression-baseline.svg";
    public const string AutoClusterPlusIsolatedSvg =
        "connectivity-and-clusters/auto-cluster-plus-isolated.svg";
    public const string AutoMultipleDisconnectedClustersSvg =
        "connectivity-and-clusters/auto-multiple-disconnected-clusters.svg";
    public const string LayeredMultipleDisconnectedClustersSvg =
        "connectivity-and-clusters/layered-multiple-disconnected-clusters.svg";
    public const string AutoAllIsolatedSvg = "connectivity-and-clusters/auto-all-isolated.svg";
    public const string ContainmentManySmallWideBoxesSvg =
        "connectivity-and-clusters/containment-many-small-wide-boxes.svg";

    public const string HierarchicalNestedSvg = "nested-hierarchy/hierarchical-nested.svg";
    public const string HierarchicalNestedPng = "nested-hierarchy/hierarchical-nested.png";
    public const string AutoNestedRoutesHierarchicalSvg =
        "nested-hierarchy/auto-nested-routes-hierarchical.svg";
    public const string AutoDeepNestedMixedConnectivitySvg =
        "nested-hierarchy/auto-deep-nested-mixed-connectivity.svg";
    public const string BoundaryPortsShowcaseHorizontalSvg =
        "nested-hierarchy/boundary-ports-showcase-horizontal.svg";
    public const string BoundaryPortsShowcaseHorizontalPng =
        "nested-hierarchy/boundary-ports-showcase-horizontal.png";
    public const string BoundaryPortsShowcaseVerticalSvg =
        "nested-hierarchy/boundary-ports-showcase-vertical.svg";
    public const string BoundaryPortsShowcaseVerticalPng =
        "nested-hierarchy/boundary-ports-showcase-vertical.png";
    public const string BoundaryPortsShowcaseDeepChainSvg =
        "nested-hierarchy/boundary-ports-showcase-deep-chain.svg";
    public const string BoundaryPortsShowcaseDeepChainPng =
        "nested-hierarchy/boundary-ports-showcase-deep-chain.png";

    public const string ParallelEdgesIntoCompartmentBoxSvg =
        "parallel-edges-and-ports/parallel-edges-into-compartment-box.svg";
    public const string ParallelEdgesIntoCompartmentBoxSideBySideSvg =
        "custom-rendering/parallel-edges-into-compartment-box-side-by-side.svg";
    public const string ParallelEdgesPreservedSvg =
        "parallel-edges-and-ports/parallel-edges-preserved.svg";
    public const string ParallelEdgesPreservedPng =
        "parallel-edges-and-ports/parallel-edges-preserved.png";
    public const string ParallelEdgesPreservedVerticalSvg =
        "parallel-edges-and-ports/parallel-edges-preserved-vertical.svg";
    public const string ParallelEdgesPreservedVerticalPng =
        "parallel-edges-and-ports/parallel-edges-preserved-vertical.png";
    public const string ParallelEdgesMergedSvg = "parallel-edges-and-ports/parallel-edges-merged.svg";
    public const string ParallelEdgesMergedPng = "parallel-edges-and-ports/parallel-edges-merged.png";
    public const string PortsShowcaseHorizontalSvg =
        "parallel-edges-and-ports/ports-showcase-horizontal.svg";
    public const string PortsShowcaseHorizontalPng =
        "parallel-edges-and-ports/ports-showcase-horizontal.png";
    public const string PortsShowcaseVerticalSvg =
        "parallel-edges-and-ports/ports-showcase-vertical.svg";
    public const string PortsShowcaseVerticalPng =
        "parallel-edges-and-ports/ports-showcase-vertical.png";
    public const string PortsShowcaseMultiConnectorHorizontalSvg =
        "parallel-edges-and-ports/ports-showcase-multi-connector-horizontal.svg";
    public const string PortsShowcaseMultiConnectorHorizontalPng =
        "parallel-edges-and-ports/ports-showcase-multi-connector-horizontal.png";
    public const string PortsShowcaseMultiConnectorVerticalSvg =
        "parallel-edges-and-ports/ports-showcase-multi-connector-vertical.svg";
    public const string PortsShowcaseMultiConnectorVerticalPng =
        "parallel-edges-and-ports/ports-showcase-multi-connector-vertical.png";
    public const string PortsShowcaseUnlabeledFanOutSvg =
        "parallel-edges-and-ports/ports-showcase-unlabeled-fan-out.svg";
    public const string PortsShowcaseUnlabeledFanOutPng =
        "parallel-edges-and-ports/ports-showcase-unlabeled-fan-out.png";
    public const string ContainmentParallelEdgesSideBySideSvg =
        "parallel-edges-and-ports/containment-parallel-edges-side-by-side.svg";
    public const string HierarchicalParallelEdgesSideBySideSvg =
        "parallel-edges-and-ports/hierarchical-parallel-edges-side-by-side.svg";

    public const string BoxAppearanceSvg = "appearance-and-themes/box-appearance.svg";
    public const string FolderTopFaceAnchorSvg = "appearance-and-themes/folder-top-face-anchor.svg";
    public const string ShapeGallerySvg = "appearance-and-themes/shape-gallery.svg";
    public const string ThemeLightPng = "appearance-and-themes/theme-light.png";
    public const string ThemeDarkPng = "appearance-and-themes/theme-dark.png";
    public const string ThemePrintPng = "appearance-and-themes/theme-print.png";

    public const string IsolatedNodeLayerGapSvg = "layout-regressions/isolated-node-layer-gap.svg";

    /// <summary>Shared "Layout algorithms" section intro, reused by every topology-based group.</summary>
    private const string LayoutAlgorithmsIntro =
        "The bundled algorithms, each laying out the same kind of graph in its own style. Select one "
        + "with the algorithm option and let the engine place the boxes and route any edges.";

    /// <summary>Shared "Raster output" section intro, reused by every group with a PNG showcase.</summary>
    private const string RasterOutputIntro =
        "One of the layout-algorithm diagrams above is rendered again here through the SkiaSharp "
        + "raster path to PNG with the same dark theme, proving multi-format output.";

    /// <summary>Shared "The auto meta-algorithm" section intro, reused by every group showcasing it.</summary>
    private const string AutoMetaAlgorithmIntro =
        "The bundled \"auto\" algorithm splits the input graph into its connected top-level "
        + "components, routes each component to whichever bundled leaf algorithm best suits its shape "
        + "— layered for a connected cluster, hierarchical for any component holding a container node, "
        + "containment for the shared bucket of childless, edgeless singletons — lays each piece out "
        + "independently, and packs the results into one combined canvas.";

    /// <summary>Shared "Layout regressions" intro, reused by both its section and group forms.</summary>
    private const string LayoutRegressionsIntro =
        "Small graphs that reproduce and pin down a specific layout bug once fixed, kept as permanent "
        + "visual and numeric evidence that it stays fixed.";

    /// <summary>Gets the browsable topology-based groups of the gallery, in display order.</summary>
    public static IReadOnlyList<GalleryGroup> Groups { get; } =
    [
        new GalleryGroup(
            "flow-pipeline",
            "Flow pipeline",
            "A single connected component with a directed left-to-right or top-to-bottom flow — what "
            + "a pipeline or flow diagram looks like once laid out and routed.",
            "A single connected, directed flow — pipelines and direction changes",
            [
                new GallerySection(
                    "Layout algorithms",
                    LayoutAlgorithmsIntro,
                    [
                        new GalleryImage(
                            LayeredPipelineSvg,
                            "Layered pipeline diagram",
                            "A directed pipeline laid out left to right by the layered algorithm.",
                            IsTopIndexHighlight: true),
                    ]),
                new GallerySection(
                    "Flow direction",
                    "The same directed graph laid out in two flow directions, selected with the "
                    + "direction option, plus a nested container overriding its own direction "
                    + "independently of its parent. A rightward flow arranges the layers left-to-right "
                    + "for block and pipeline diagrams; a downward flow arranges them top-to-bottom for "
                    + "action flows and state machines, swapping each node's width and height so layer "
                    + "spacing follows node height.",
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
                            "A container's own direction override is honored independently of its "
                            + "parent: the outer flow runs left-to-right while the nested container "
                            + "runs top-to-bottom.",
                            IsTopIndexHighlight: true),
                    ]),
                new GallerySection(
                    "Edge routing",
                    "Orthogonal connectors step around the boxes between their endpoints instead of "
                    + "cutting through them.",
                    [
                        new GalleryImage(
                            OrthogonalObstacleSvg,
                            "Orthogonal edge routed around an obstacle",
                            "A connector routed orthogonally around an intervening container box."),
                    ]),
                new GallerySection(
                    "Raster output",
                    RasterOutputIntro,
                    [
                        new GalleryImage(
                            LayeredPipelinePng,
                            "Layered pipeline diagram as PNG",
                            "The layered pipeline rendered to a raster PNG image."),
                    ]),
            ]),
        new GalleryGroup(
            "connectivity-and-clusters",
            "Connectivity and clusters",
            "Compares a normal, fully-connected graph against graphs with little or no connectivity "
            + "between nodes — isolated singletons and/or multiple unrelated components — across the "
            + "containment, auto, and layered algorithms.",
            "Connected vs. disconnected input and clusters",
            [
                new GallerySection(
                    "Layout algorithms",
                    LayoutAlgorithmsIntro,
                    [
                        new GalleryImage(
                            ContainmentPackedSvg,
                            "Containment packed diagram",
                            "Sibling boxes packed compactly by the containment algorithm."),
                    ]),
                new GallerySection(
                    "Baseline: a fully-connected graph, for contrast",
                    "Before the disconnected-graph comparisons in the next section, this baseline shows "
                    + "the same kind of small pipeline fully connected, laid out directly by the "
                    + "layered algorithm — the normal, connected case the \"auto\" meta-algorithm "
                    + "examples below are deliberately contrasted against.",
                    [
                        new GalleryImage(
                            LayeredRegressionBaselineSvg,
                            "A small, fully-connected pipeline laid out by the layered algorithm",
                            "A baseline, fully-connected graph laid out directly by the layered "
                            + "algorithm, for visual contrast with the disconnected-graph diagrams in "
                            + "the next section."),
                    ]),
                new GallerySection(
                    "The auto meta-algorithm",
                    AutoMetaAlgorithmIntro,
                    [
                        new GalleryImage(
                            AutoClusterPlusIsolatedSvg,
                            "One connected cluster packed alongside three isolated singleton boxes",
                            "The \"auto\" algorithm routes the two-node cluster through the layered "
                            + "algorithm and gathers the three unrelated singletons into one shared "
                            + "bucket routed through the containment algorithm, then packs both pieces "
                            + "into one canvas."),
                        new GalleryImage(
                            AutoMultipleDisconnectedClustersSvg,
                            "Three disconnected two-node clusters, each routed and packed independently",
                            "The same three-cluster graph routed through \"auto\": each cluster is its "
                            + "own connected component, so each is laid out by the layered algorithm "
                            + "independently and the three results are packed into one combined "
                            + "canvas.",
                            IsTopIndexHighlight: true),
                        new GalleryImage(
                            LayeredMultipleDisconnectedClustersSvg,
                            "The same three clusters laid out directly by the layered algorithm",
                            "The companion direct-\"layered\" sibling of the diagram above: the same "
                            + "disconnected graph, laid out by the layered algorithm's own internal "
                            + "component packing rather than \"auto\"'s per-component routing, for "
                            + "comparison."),
                        new GalleryImage(
                            AutoAllIsolatedSvg,
                            "Five entirely isolated boxes packed by the auto algorithm",
                            "A graph of nothing but childless, edgeless singleton nodes: \"auto\" "
                            + "gathers every one of them into the shared bucket and routes the whole "
                            + "graph through the containment algorithm unchanged, taking its zero-copy "
                            + "fast path."),
                    ]),
                new GallerySection(
                    "Containment packing heuristics",
                    "The containment algorithm derives its row-wrapping content-width budget from the "
                    + "packed boxes' shape, combining an area-based estimate with a column-count-based "
                    + "one so both a few large boxes and many small ones wrap into a balanced block.",
                    [
                        new GalleryImage(
                            ContainmentManySmallWideBoxesSvg,
                            "Twelve small, wide boxes packed into a balanced multi-column block",
                            "Twelve identically-sized, wide boxes: the column-count-based content-width "
                            + "candidate keeps the containment algorithm from packing them into one "
                            + "long, narrow column, wrapping them into a balanced grid of columns "
                            + "instead.",
                            IsTopIndexHighlight: true),
                    ]),
            ]),
        new GalleryGroup(
            "nested-hierarchy",
            "Nested hierarchy",
            "Parent/child containment and boundary-port delegation into nested children — what a "
            + "container-with-children graph looks like once laid out and routed.",
            "Parent/child containment and boundary-port delegation",
            [
                new GallerySection(
                    "Layout algorithms",
                    LayoutAlgorithmsIntro,
                    [
                        new GalleryImage(
                            HierarchicalNestedSvg,
                            "Hierarchical nested diagram",
                            "A container node holding a nested child graph, with a cross-container "
                            + "edge."),
                    ]),
                new GallerySection(
                    "Raster output",
                    RasterOutputIntro,
                    [
                        new GalleryImage(
                            HierarchicalNestedPng,
                            "Hierarchical nested diagram as PNG",
                            "The hierarchical nested diagram rendered to a raster PNG image."),
                    ]),
                new GallerySection(
                    "The auto meta-algorithm",
                    AutoMetaAlgorithmIntro,
                    [
                        new GalleryImage(
                            AutoNestedRoutesHierarchicalSvg,
                            "A nested container routed to hierarchical alongside an unrelated isolated "
                            + "box",
                            "\"auto\" routes any component containing a container node through the "
                            + "hierarchical algorithm regardless of its size, while the unrelated "
                            + "isolated sibling is packed alongside it through the shared containment "
                            + "bucket."),
                        new GalleryImage(
                            AutoDeepNestedMixedConnectivitySvg,
                            "Three-level nested container mixing connected pairs and singletons",
                            "Every nested scope inherits \"auto\" without re-declaring it, and each one "
                            + "is independently re-classified there: the connected pair routes through "
                            + "layered and the singleton is packed alongside it through containment, at "
                            + "every level of nesting — not just the root."),
                    ]),
                new GallerySection(
                    "Boundary and delegation ports",
                    "The hierarchical engine's support for boundary (delegation) ports: a container may "
                    + "expose a named port carrying BOTH an external and an internal label at one "
                    + "shared physical anchor on its boundary. An external approach edge from a sibling "
                    + "reaches the anchor from outside, while one or more internal delegation edges "
                    + "relay the connection inward to the container's nested children. The container "
                    + "and its children are laid out in one combined recursive pass, and every "
                    + "converging edge — external approach and internal delegation alike — is routed "
                    + "through the orthogonal corridor router onto that single shared anchor, with the "
                    + "external label reading outward and the internal label reading inward.",
                    [
                        new GalleryImage(
                            BoundaryPortsShowcaseHorizontalSvg,
                            "A sibling joined to a container's boundary port delegating to two children",
                            "A rightward-flowing container exposes one boundary port on its left face "
                            + "carrying both a 'command' external label (reading outward) and a "
                            + "'dispatch' internal label (reading inward) at the same shared anchor. "
                            + "The external approach edge from the sibling and both internal delegation "
                            + "edges (internal fan-out to two nested children) are routed orthogonally "
                            + "onto that one anchor."),
                        new GalleryImage(
                            BoundaryPortsShowcaseVerticalSvg,
                            "Two siblings joined to a container's top-face boundary port and one child",
                            "The companion downward-flowing case: the boundary port anchors on the "
                            + "container's top face, with external fan-out (two sibling approach edges) "
                            + "both routed orthogonally onto the one shared anchor, which then "
                            + "delegates inward to the single nested child."),
                        new GalleryImage(
                            BoundaryPortsShowcaseDeepChainSvg,
                            "A boundary port delegating through a nested boundary port to a leaf",
                            "A three-level delegation chain: a sibling approaches an outer container's "
                            + "boundary port, which delegates inward to a nested container's own "
                            + "boundary port, which delegates again to the innermost leaf. Both "
                            + "boundary crossings carry an outward external and an inward internal "
                            + "label, and the whole chain is routed orthogonally in one combined "
                            + "recursive pass with no diagonal shortcut at either boundary.",
                            IsTopIndexHighlight: true),
                    ]),
            ]),
        new GalleryGroup(
            "parallel-edges-and-ports",
            "Parallel edges and ports",
            "Dense parallel connections between a small number of boxes, including named/boundary "
            + "ports and edge-count-aware gap widening — the same small-box-count-many-edges shape "
            + "compared across algorithms.",
            "Many edges between few boxes, named/boundary ports",
            [
                new GallerySection(
                    "Parallel-edge routing regressions",
                    "Regression coverage specific to routing many parallel connectors around and "
                    + "between boxes: a small graph, laid out by the containment algorithm, that "
                    + "reproduces a specific connector-routing bug once fixed, kept as permanent "
                    + "visual and numeric evidence that it stays fixed.",
                    [
                        new GalleryImage(
                            ParallelEdgesIntoCompartmentBoxSvg,
                            "Nine edges docking on a compartment box without crossing its interior",
                            "Regression coverage for the parallel-edges-into-compartment-box fix: nine "
                            + "unmerged edges from a small Source box converge on a taller Target box's "
                            + "nine-row compartment. ConnectorRouter now treats every box, including a "
                            + "connection's own endpoints, as a hard obstacle for the whole route (not "
                            + "just the final docking stub), so a connector squeezed by other "
                            + "already-routed connectors can no longer detour straight through its own "
                            + "target box's interior.",
                            IsTopIndexHighlight: true),
                    ]),
                new GallerySection(
                    "Parallel edges and named ports",
                    "The layered algorithm's Phase 1 flat-graph support for multiple parallel "
                    + "connectors between the same two boxes, and for named ports attached to a "
                    + "specific, labelled location on a node's boundary. Parallel edges either collapse "
                    + "to one rendered connector (the default) or each keep their own "
                    + "independently-routed line, selected with the MergeParallelEdges option; each "
                    + "node's ContentInset margins are auto-computed from its ports' measured label "
                    + "widths so port text never overlaps the box's own content.",
                    [
                        new GalleryImage(
                            ParallelEdgesPreservedSvg,
                            "Three parallel connectors between the same two boxes, independently "
                            + "routed",
                            "MergeParallelEdges set to false: all three parallel connectors survive, "
                            + "each with its own label."),
                        new GalleryImage(
                            ParallelEdgesPreservedVerticalSvg,
                            "The same three parallel connectors on a downward-flowing pair of boxes",
                            "The companion vertical-flow case: with a downward Direction the three "
                            + "parallel connectors anchor on the boxes' top and bottom faces instead of "
                            + "their left and right faces, and each box's WIDTH (not height) auto-grows "
                            + "to fit the widened lane spacing, since PortDistributor spreads anchors "
                            + "on a top/bottom face horizontally."),
                        new GalleryImage(
                            ParallelEdgesMergedSvg,
                            "The same three parallel connectors collapsed to a single line",
                            "The default MergeParallelEdges (true): the three parallel connectors "
                            + "collapse to a single rendered line, and its midpoint label is omitted "
                            + "entirely (not any single surviving connector's label) since a reader "
                            + "could not tell which of the three collapsed connectors a kept label "
                            + "would have belonged to."),
                        new GalleryImage(
                            PortsShowcaseHorizontalSvg,
                            "A hub node with a named port on each of its left and right sides",
                            "Left/right named ports on a rightward-flowing hub node; the long left-side "
                            + "incoming label auto-computes a widened ContentInsetLeft margin, measured "
                            + "with the Skia-backed text measurer."),
                        new GalleryImage(
                            PortsShowcaseVerticalSvg,
                            "A hub node with a named port on each of its top and bottom sides",
                            "The companion top/bottom case: a downward-flowing hub node, whose ports "
                            + "anchor on its top and bottom faces instead."),
                        new GalleryImage(
                            PortsShowcaseMultiConnectorHorizontalSvg,
                            "A hub node with two named ports on each of its left and right sides",
                            "Same-face crowding with two independently-labelled ports per side (one "
                            + "deliberately long): PortDistributor spreads both anchors on each face "
                            + "without collapsing them onto one row, and the hub's title stays clear of "
                            + "both stacked rows on either side."),
                        new GalleryImage(
                            PortsShowcaseMultiConnectorVerticalSvg,
                            "A hub node with two named ports on each of its top and bottom sides",
                            "The companion top/bottom case: two ports per face spread horizontally "
                            + "instead of vertically, proving the same crowding and title-collision "
                            + "protection when PortDistributor works along the cross axis of a "
                            + "downward flow."),
                        new GalleryImage(
                            PortsShowcaseUnlabeledFanOutSvg,
                            "A titled hub node with several unlabeled ports fanning out to four other "
                            + "boxes",
                            "A face with several ports carrying no label or text at all still grows the "
                            + "box tall enough to keep them from bunching together — the growth floor "
                            + "applies unconditionally to any face with two or more anchors, not only "
                            + "labeled ones."),
                    ]),
                new GallerySection(
                    "Edge-count gap widening",
                    "When many parallel connectors fan through the single gap between two side-by-side "
                    + "boxes, that gap is widened in proportion to the connector count so each "
                    + "connector gets its own orthogonal routing lane instead of being crushed into one "
                    + "narrow channel — the same corridor-width reservation the layered pipeline "
                    + "already makes between its columns, now applied to the containment packer's "
                    + "same-row pairs and to the hierarchical engine's side-by-side sibling containers.",
                    [
                        new GalleryImage(
                            ContainmentParallelEdgesSideBySideSvg,
                            "Eight edges fanning through a widened containment gap",
                            "The containment algorithm packs two tall, compartment-bearing peer boxes "
                            + "side by side and widens the horizontal gap between them in proportion to "
                            + "the eight parallel connectors routed through it, so the connectors fan "
                            + "cleanly into distinct lanes rather than crowding a fixed-width channel."),
                        new GalleryImage(
                            HierarchicalParallelEdgesSideBySideSvg,
                            "Eight cross-container edges through a widened sibling gap",
                            "The hierarchical engine places two peer containers side by side and widens "
                            + "the gap between them for the eight cross-container connectors that fan "
                            + "child-to-child through it — spacing the per-scope leaf algorithm alone "
                            + "cannot reserve, because those edges never appear in the sized view it "
                            + "lays out."),
                    ]),
            ]),
        new GalleryGroup(
            "appearance-and-themes",
            "Appearance and themes",
            "Not a topology grouping — box shape/keyword/compartments, shape-aware connector "
            + "anchoring, and theme showcases, organized pragmatically by appearance rather than by "
            + "topology, unlike the rest of this gallery.",
            "Box shapes, keywords, compartments, and built-in themes",
            [
                new GallerySection(
                    "Box appearance",
                    "A node's Shape, Keyword, and Compartments properties select the box outline, an "
                    + "italicized keyword line, and labelled feature sections, all through the plain "
                    + "input graph model — no downstream renderer-specific code required. This is "
                    + "generic block-diagram notation; SysML is just one modeling language that uses "
                    + "it.",
                    [
                        new GalleryImage(
                            BoxAppearanceSvg,
                            "A folder container with two boxes carrying a keyword, one also "
                            + "compartmented, joined by an edge",
                            "A folder container holding two boxes with a keyword line — one also with "
                            + "a labelled compartment — joined by a decorated edge."),
                    ]),
                new GallerySection(
                    "Shape-aware connectors",
                    "A box's Shape can make its true outline diverge from its plain bounding rectangle "
                    + "— a folder's tab, a note's folded corner, a rounded rectangle's corners. The "
                    + "router keeps connectors off those non-connectable regions and projects each "
                    + "anchor down to the shape's actual drawn outline, so every connector visibly "
                    + "touches the shape it targets.",
                    [
                        new GalleryImage(
                            FolderTopFaceAnchorSvg,
                            "An external node connected into a folder top face, clear of the tab",
                            "An edge approaching a folder container from above: the connector avoids "
                            + "the tab and anchors on the folder's recessed top edge instead of "
                            + "floating above it."),
                        new GalleryImage(
                            ShapeGallerySvg,
                            "One of each container shape side by side, each holding content",
                            "Every Shape value side by side, each with content appropriate to it: "
                            + "rectangle and rounded-rectangle boxes with a keyword and a compartment, "
                            + "a folder holding a nested child, and a note holding free-form text — "
                            + "every shape reserves enough space so its content never overlaps the tab "
                            + "or the folded corner.",
                            IsTopIndexHighlight: true),
                    ]),
                new GallerySection(
                    "Themes",
                    "One representative diagram rendered with each of the three built-in themes, "
                    + "showing how the theme controls colours, stroke, and corner style without "
                    + "touching the layout. These are rendered through the raster path to PNG so each "
                    + "carries a solid theme background.",
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
            ]),
        new GalleryGroup(
            "layout-regressions",
            "Layout regressions",
            "Kept as its own group per its organizing principle: bug history, not topology. "
            + LayoutRegressionsIntro,
            "Permanent visual proof that fixed layout bugs stay fixed",
            [
                new GallerySection(
                    "Isolated-node layer-gap fix",
                    LayoutRegressionsIntro,
                    [
                        new GalleryImage(
                            IsolatedNodeLayerGapSvg,
                            "A hub column with an isolated node beside a routing-corridor dummy",
                            "Regression coverage for the isolated-node layer-gap fix: a genuinely "
                            + "isolated node (Isolated, zero edges) shares its layer with a dummy bend "
                            + "point created by an unrelated long edge (LongSource to LongTarget). The "
                            + "crossing minimizer now clusters isolated nodes at the end of the layer's "
                            + "order, and coordinate assignment squeezes any resulting gap down to the "
                            + "standard node spacing, instead of inheriting the dummy's unrelated "
                            + "port-alignment floor as an inflated gap."),
                    ]),
            ]),
        new GalleryGroup(
            "custom-rendering",
            "Custom rendering",
            "Every other group in this gallery shows what the layout engine itself produces: describe "
            + "a graph, call LayoutEngine.Layout, render the result. This group is the deliberate "
            + "exception. Its diagrams are hand-built LayoutTrees with boxes placed at explicit, "
            + "hardcoded coordinates, rendered by calling ConnectorRouter and the renderers directly — "
            + "no layout algorithm is involved at all. This is a legitimate, fully public capability "
            + "(every type used here is public), so it earns its own honestly-labelled home rather than "
            + "being mixed into a showcase of algorithm output. Expect rough edges: nothing here "
            + "benefited from any algorithm's spacing or routing intelligence, and a person taking this "
            + "path on their own diagrams should expect the same trade-off.",
            "Hand-built LayoutTrees, bypassing the layout engine entirely",
            [
                new GallerySection(
                    "Direct ConnectorRouter regression coverage",
                    "Regression coverage that requires an exact, hardcoded box arrangement no layout "
                    + "algorithm currently produces, kept as permanent visual and numeric evidence "
                    + "that a specific connector-routing bug stays fixed regardless of which algorithm "
                    + "(or future heuristic change) might eventually produce a similar arrangement.",
                    [
                        new GalleryImage(
                            ParallelEdgesIntoCompartmentBoxSideBySideSvg,
                            "Nine edges sharing a narrow inter-box gap",
                            "Regression coverage for the OrthogonalEdgeRouter narrow-gap tangling fix: "
                            + "a small Source box placed explicitly beside a taller, nine-row-compartment "
                            + "Target box with a narrow gap between them, joined by nine hand-routed "
                            + "connectors (bypassing any layout algorithm, so this exact geometry stays "
                            + "covered regardless of which algorithm or future heuristic change might "
                            + "produce it). Before the fix, each successive parallel connector's own "
                            + "soft-obstacle avoidance offset its candidate lane a little further from "
                            + "the shared gap with no ceiling, eventually looping a route back behind "
                            + "the Source box it had already left; the router now clamps soft-obstacle "
                            + "lane candidates to the box pair's own envelope and penalizes (without "
                            + "blocking) any move that still leaves it. The router also now prices a "
                            + "connector transversally crossing another already-routed connector's "
                            + "trunk (OrthogonalEdgeRouter's SoftObstacleCrossingCost), so a connector "
                            + "with a genuinely bounded-cost non-crossing alternative takes it; this gap "
                            + "is saturated enough (nine connectors, one alternate lane) that one "
                            + "connector's own approach stub still crosses two others' trunks here, "
                            + "because that connector is routed before the two it later crosses and so "
                            + "cannot see them as obstacles yet — a known single-pass routing-order "
                            + "limitation, not a pricing gap, left as documented residual behavior "
                            + "rather than force a much wider routing-order change for this one "
                            + "saturated diagram."),
                    ]),
            ]),
    ];
}
