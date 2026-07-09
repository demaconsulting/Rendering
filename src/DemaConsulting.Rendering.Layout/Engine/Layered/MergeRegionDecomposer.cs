// <copyright file="MergeRegionDecomposer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Layout.Engine;
using static DemaConsulting.Rendering.Layout.Engine.Layered.LayeredLayoutMetrics;

namespace DemaConsulting.Rendering.Layout.Engine.Layered;

/// <summary>
/// The per-scope geometry a <see cref="MergeRegionDecomposer"/> pass projects out of a fully-placed
/// <see cref="RecursiveLayoutResult"/>: the outermost scope's composed boxes (with every nested level
/// composed into its container box), its final connector lines, its boundary-port anchors, and the
/// canvas size.
/// </summary>
/// <param name="Boxes">
/// The outermost scope's composed boxes, index-aligned with the region root level's
/// <see cref="MergeRegionLevel.Nodes"/> (which is the scope graph's node order), each container box
/// carrying its recursively projected interior.
/// </param>
/// <param name="IndexOf">Map from each root-level direct-member node to its positional index in <see cref="Boxes"/>.</param>
/// <param name="Lines">
/// Every connector line of the whole region in absolute coordinates: ordinary interior edges at every
/// depth, each external approach routed to its boundary port's single shared anchor, and each internal
/// delegation routed from that same anchor into the container interior.
/// </param>
/// <param name="Ports">One <see cref="LayoutPort"/> per boundary port, on its container's boundary, carrying both labels.</param>
/// <param name="Width">The region canvas width in logical pixels.</param>
/// <param name="Height">The region canvas height in logical pixels.</param>
internal sealed record DecomposedRegion(
    LayoutBox[] Boxes,
    Dictionary<LayoutGraphNode, int> IndexOf,
    IReadOnlyList<LayoutLine> Lines,
    IReadOnlyList<LayoutPort> Ports,
    double Width,
    double Height);

/// <summary>
/// Projects a fully-placed <see cref="RecursiveLayoutResult"/> back into per-scope
/// <see cref="LayoutBox"/>/<see cref="LayoutLine"/>/<see cref="LayoutPort"/> geometry at every nesting
/// level — the decomposition half of the ELK-style recursive hierarchy handling that replaces the old
/// post-hoc boundary-port reconciliation.
/// </summary>
/// <remarks>
///     <para>
///     The combined pass laid every nesting level out in one coordinated placement, so this stage never
///     re-routes or re-derives geometry: each real edge's connector is the
///     <see cref="LayeredCorridorRouter"/>-produced polyline read straight from
///     <see cref="LayeredGraph.Waypoints"/>, and each interior node is translated into its container's
///     placed box by the same offset math <see cref="HierarchicalLayoutAlgorithm"/> composes with
///     (<see cref="HierarchicalLayoutAlgorithm.ContainerPadding"/> plus the title/content offset).
///     </para>
///     <para>
///     A boundary port resolves to exactly one physical anchor on its container's boundary: the placed
///     endpoint of the router polyline that lands on that face — the container-link polyline of the
///     port's external-face crossing when it has an in-scope approacher, or the parent port's delegation
///     polyline when the port is a further link in a delegation chain. Every external approach edge is
///     joined orthogonally to that anchor by concatenating its router polyline with the shared
///     container-link polyline (so a fan-in of many approaches converges through the router's own
///     orthogonal channels, never a straight diagonal), and every internal delegation edge is joined to
///     the same anchor by prepending it with at most one orthogonal corner. No connector endpoint is
///     patched onto a mean-of-targets and no diagonal shortcut is ever introduced.
///     </para>
/// </remarks>
internal static class MergeRegionDecomposer
{
    /// <summary>Tolerance, in logical pixels, for treating two placed points as coincident.</summary>
    private const double PointTolerance = 1e-6;

    /// <summary>Clearance, in logical pixels, bounding a boundary-port anchor's label width.</summary>
    private const double PortLabelClearance = 4.0;

    /// <summary>
    /// Decomposes <paramref name="result"/> into the outermost scope's composed boxes, connector lines,
    /// boundary-port anchors, and canvas size.
    /// </summary>
    /// <param name="result">The fully-placed recursive layout result to project.</param>
    /// <param name="direction">The region's flow direction, used to pick each boundary face.</param>
    /// <param name="rootTemplates">
    /// The outermost scope's already-composed, already-styled boxes (the leaf pass's placed boxes with
    /// each container's recursively laid-out interior composed in), index-aligned with the region root
    /// level's nodes; used only as a styling and interior-structure template — every placed coordinate is
    /// taken from the combined pass, not from these boxes.
    /// </param>
    /// <returns>The projected per-scope geometry for the outermost scope.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> or <paramref name="rootTemplates"/> is <see langword="null"/>.</exception>
    public static DecomposedRegion Decompose(
        RecursiveLayoutResult result,
        LayoutDirection direction,
        IReadOnlyList<LayoutBox> rootTemplates)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(rootTemplates);

        var boundaryFace = BoundaryPortResolver.FaceForDirection(direction);

        // Pass 1: resolve every boundary port's single shared anchor from the placed router polylines.
        var anchors = new Dictionary<LayoutGraphPort, Point2D>();
        ResolveAnchors(result, result.Region.Root, offsetX: 0.0, offsetY: 0.0, anchors);

        // Pass 2: project every level's boxes, lines, and ports into absolute geometry.
        var lines = new List<LayoutLine>();
        var ports = new List<LayoutPort>();
        var (boxes, indexOf) = ProjectLevel(
            result,
            result.Region.Root,
            offsetX: 0.0,
            offsetY: 0.0,
            rootTemplates,
            boundaryFace,
            anchors,
            lines,
            ports);

        var (width, height) = LevelFootprint(result.Levels[result.Region.Root].Graph, direction);
        return new DecomposedRegion(boxes, indexOf, lines, ports, width, height);
    }

    /// <summary>
    /// Recursively resolves each boundary port's single shared anchor: the endpoint of the router
    /// polyline that lands on the port's container face, recorded in absolute coordinates.
    /// </summary>
    /// <param name="result">The fully-placed recursive layout result.</param>
    /// <param name="level">The nesting level being scanned.</param>
    /// <param name="offsetX">The X offset mapping this level's local coordinates into absolute space.</param>
    /// <param name="offsetY">The Y offset mapping this level's local coordinates into absolute space.</param>
    /// <param name="anchors">The accumulating per-port anchor lookup, mutated in place.</param>
    private static void ResolveAnchors(
        RecursiveLayoutResult result,
        MergeRegionLevel level,
        double offsetX,
        double offsetY,
        Dictionary<LayoutGraphPort, Point2D> anchors)
    {
        var levelGraph = result.Levels[level];
        var waypoints = WaypointsByOriginalEdge(levelGraph.Graph, offsetX, offsetY);

        for (var e = 0; e < levelGraph.EdgeRoles.Count; e++)
        {
            var role = levelGraph.EdgeRoles[e];
            var polyline = waypoints[e];
            if (polyline is null || polyline.Count == 0)
            {
                continue;
            }

            switch (role.Kind)
            {
                case LevelEdgeKind.ContainerLink:
                    // The external-face crossing's synthetic dummy-to-container polyline lands on the
                    // container face: its endpoint is the port's single shared anchor.
                    anchors[role.Boundary!.Port] = polyline[^1];
                    break;

                case LevelEdgeKind.InternalDelegation
                    when OtherEndpoint(role.Edge!, role.Boundary!.Port) is LayoutGraphPort nested:
                    // A chain link: the parent port's delegation polyline lands on the nested container's
                    // face, so its endpoint is that nested port's single shared anchor.
                    anchors[nested] = polyline[^1];
                    break;

                default:
                    break;
            }
        }

        foreach (var (_, _, child) in level.Children)
        {
            var childOffset = ChildOffset(result, level, child, offsetX, offsetY);
            ResolveAnchors(result, child, childOffset.X, childOffset.Y, anchors);
        }
    }

    /// <summary>
    /// Recursively projects one nesting level into absolute boxes, appending its (and every descendant
    /// level's) connector lines and boundary-port anchors to the shared <paramref name="lines"/> and
    /// <paramref name="ports"/> accumulators.
    /// </summary>
    /// <param name="result">The fully-placed recursive layout result.</param>
    /// <param name="level">The nesting level to project.</param>
    /// <param name="offsetX">The X offset mapping this level's local coordinates into absolute space.</param>
    /// <param name="offsetY">The Y offset mapping this level's local coordinates into absolute space.</param>
    /// <param name="templates">The styling/interior template boxes, index-aligned with this level's nodes.</param>
    /// <param name="boundaryFace">The container boundary face every boundary port of this region sits on.</param>
    /// <param name="anchors">The resolved per-port shared anchors.</param>
    /// <param name="lines">The accumulating connector-line list, mutated in place.</param>
    /// <param name="ports">The accumulating boundary-port anchor list, mutated in place.</param>
    /// <returns>This level's composed boxes and the node-to-index map for the outermost scope.</returns>
    private static (LayoutBox[] Boxes, Dictionary<LayoutGraphNode, int> IndexOf) ProjectLevel(
        RecursiveLayoutResult result,
        MergeRegionLevel level,
        double offsetX,
        double offsetY,
        IReadOnlyList<LayoutBox> templates,
        PortSide boundaryFace,
        Dictionary<LayoutGraphPort, Point2D> anchors,
        List<LayoutLine> lines,
        List<LayoutPort> ports)
    {
        var levelGraph = result.Levels[level];
        var graph = levelGraph.Graph;
        var count = level.Nodes.Count;
        var boxes = new LayoutBox[count];
        var indexOf = new Dictionary<LayoutGraphNode, int>(count);

        var childByNodeIndex = new Dictionary<int, MergeRegionLevel>();
        foreach (var (_, nodeIndex, child) in level.Children)
        {
            childByNodeIndex[nodeIndex] = child;
        }

        for (var i = 0; i < count; i++)
        {
            var node = level.Nodes[i];
            indexOf[node] = i;
            var template = templates[i];
            var (effWidth, effHeight) = ResolveSize(level, node);
            var placedX = graph.AugX[i] + offsetX;
            var placedY = graph.AugY[i] + offsetY;

            if (childByNodeIndex.TryGetValue(i, out var childLevel))
            {
                // A boundary-port container: place its box from the combined pass and recurse into its
                // child level for its interior, translated into the box's padded interior.
                var box = template with { X = placedX, Y = placedY, Width = effWidth, Height = effHeight, Children = [] };
                var titleHeight = HierarchicalLayoutAlgorithm.ResolveContentOffsetHeight(node);
                var childOffsetX = placedX + HierarchicalLayoutAlgorithm.ContainerPadding;
                var childOffsetY = placedY + HierarchicalLayoutAlgorithm.ContainerPadding + titleHeight;
                var childTemplates = template.Children.OfType<LayoutBox>().ToList();
                var (childBoxes, _) = ProjectLevel(
                    result,
                    childLevel,
                    childOffsetX,
                    childOffsetY,
                    childTemplates,
                    boundaryFace,
                    anchors,
                    lines,
                    ports);
                boxes[i] = box with { Children = childBoxes.Cast<LayoutNode>().ToList() };
            }
            else
            {
                // A leaf or non-boundary container: the combined pass placed its box; rigidly shift its
                // styled template (and any nested interior) from the template origin to the placed
                // origin, preserving the interior structure.
                boxes[i] = (LayoutBox)HierarchicalLayoutAlgorithm.Translate(template, placedX - template.X, placedY - template.Y);
            }
        }

        ProjectEdges(result, level, offsetX, offsetY, boundaryFace, anchors, lines);
        ProjectPorts(level, boxes, boundaryFace, anchors, ports);

        return (boxes, indexOf);
    }

    /// <summary>
    /// Projects one level's connector lines into <paramref name="lines"/>: ordinary interior edges,
    /// each external approach concatenated with its shared container-link onto the boundary anchor, and
    /// each internal delegation prepended orthogonally from that same anchor.
    /// </summary>
    /// <param name="result">The fully-placed recursive layout result.</param>
    /// <param name="level">The level whose edges are projected.</param>
    /// <param name="offsetX">The X offset mapping this level's local coordinates into absolute space.</param>
    /// <param name="offsetY">The Y offset mapping this level's local coordinates into absolute space.</param>
    /// <param name="boundaryFace">The container boundary face boundary ports sit on.</param>
    /// <param name="anchors">The resolved per-port shared anchors.</param>
    /// <param name="lines">The accumulating connector-line list, mutated in place.</param>
    private static void ProjectEdges(
        RecursiveLayoutResult result,
        MergeRegionLevel level,
        double offsetX,
        double offsetY,
        PortSide boundaryFace,
        Dictionary<LayoutGraphPort, Point2D> anchors,
        List<LayoutLine> lines)
    {
        var levelGraph = result.Levels[level];
        var graph = levelGraph.Graph;
        var waypoints = WaypointsByOriginalEdge(graph, offsetX, offsetY);

        // The shared container-link polyline per boundary port (external approaches concatenate onto it).
        var containerLinks = new Dictionary<LayoutGraphPort, IReadOnlyList<Point2D>>();
        for (var e = 0; e < levelGraph.EdgeRoles.Count; e++)
        {
            if (levelGraph.EdgeRoles[e].Kind == LevelEdgeKind.ContainerLink && waypoints[e] is { } link)
            {
                containerLinks[levelGraph.EdgeRoles[e].Boundary!.Port] = link;
            }
        }

        for (var e = 0; e < levelGraph.EdgeRoles.Count; e++)
        {
            var role = levelGraph.EdgeRoles[e];
            var polyline = waypoints[e];
            if (polyline is null || polyline.Count == 0)
            {
                continue;
            }

            switch (role.Kind)
            {
                case LevelEdgeKind.Ordinary:
                    lines.Add(StyledLine(polyline, role.Edge!));
                    break;

                case LevelEdgeKind.ExternalApproach:
                    var full = containerLinks.TryGetValue(role.Boundary!.Port, out var containerLink)
                        ? Concatenate(polyline, containerLink)
                        : polyline;
                    lines.Add(StyledLine(full, role.Edge!));
                    break;

                case LevelEdgeKind.InternalDelegation when anchors.TryGetValue(role.Boundary!.Port, out var anchor):
                    lines.Add(StyledLine(PrependAnchor(anchor, polyline, boundaryFace), role.Edge!));
                    break;

                case LevelEdgeKind.ContainerLink:
                    // Consumed by the external approach concatenation; carries no visible connector.
                    break;

                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Emits one <see cref="LayoutPort"/> per boundary port discovered directly in <paramref name="level"/>,
    /// positioned on its container box's boundary and carrying both labels.
    /// </summary>
    /// <param name="level">The level whose directly-discovered boundary ports are emitted.</param>
    /// <param name="boxes">This level's composed boxes, index-aligned with its nodes.</param>
    /// <param name="boundaryFace">The container boundary face boundary ports sit on.</param>
    /// <param name="anchors">The resolved per-port shared anchors.</param>
    /// <param name="ports">The accumulating boundary-port anchor list, mutated in place.</param>
    private static void ProjectPorts(
        MergeRegionLevel level,
        LayoutBox[] boxes,
        PortSide boundaryFace,
        Dictionary<LayoutGraphPort, Point2D> anchors,
        List<LayoutPort> ports)
    {
        foreach (var boundary in level.BoundaryPorts)
        {
            if (!anchors.TryGetValue(boundary.Port, out var anchor) ||
                !level.NodeIndex.TryGetValue(boundary.Container, out var containerIndex))
            {
                continue;
            }

            var containerBox = boxes[containerIndex];
            var maxLabelWidth = Math.Max(0.0, (containerBox.Width / 2.0) - PortLabelClearance);
            ports.Add(new LayoutPort(
                anchor.X,
                anchor.Y,
                boundaryFace,
                boundary.Port.ExternalLabel,
                boundary.Port.InternalLabel,
                maxLabelWidth,
                SourcePort: boundary.Port));
        }
    }

    /// <summary>
    /// Builds the per-original-edge routed polylines of <paramref name="graph"/>, each translated by the
    /// level offset and reversed when the acyclic edge was a reversed back edge, indexed by input-edge
    /// index (so it aligns with the level's <see cref="LevelLayeredGraph.EdgeRoles"/>).
    /// </summary>
    /// <param name="graph">The placed level graph whose routed polylines are recovered.</param>
    /// <param name="offsetX">The X offset applied to every waypoint.</param>
    /// <param name="offsetY">The Y offset applied to every waypoint.</param>
    /// <returns>One translated polyline per input edge (a slot is <see langword="null"/> when the edge had no routed polyline).</returns>
    private static IReadOnlyList<Point2D>?[] WaypointsByOriginalEdge(LayeredGraph graph, double offsetX, double offsetY)
    {
        var byEdge = new IReadOnlyList<Point2D>?[graph.Edges.Count];
        for (var k = 0; k < graph.Acyclic.Count; k++)
        {
            var originalEdge = graph.AcyclicOriginalIndex[k];
            if (originalEdge < 0 || originalEdge >= byEdge.Length)
            {
                continue;
            }

            var polyline = graph.Waypoints[k];
            var reversed = graph.AcyclicReversed.Length > k && graph.AcyclicReversed[k];
            var translated = new List<Point2D>(polyline.Count);
            for (var p = 0; p < polyline.Count; p++)
            {
                var point = reversed ? polyline[polyline.Count - 1 - p] : polyline[p];
                translated.Add(new Point2D(point.X + offsetX, point.Y + offsetY));
            }

            byEdge[originalEdge] = translated;
        }

        return byEdge;
    }

    /// <summary>Builds a <see cref="LayoutLine"/> from a polyline, styled from its originating input edge.</summary>
    /// <param name="waypoints">The line's absolute waypoints.</param>
    /// <param name="edge">The originating input edge supplying the target marker, stroke style, and label.</param>
    /// <returns>The styled connector line.</returns>
    private static LayoutLine StyledLine(IReadOnlyList<Point2D> waypoints, LayoutGraphEdge edge) =>
        new(waypoints, EndMarkerStyle.None, edge.TargetEnd, edge.LineStyle, edge.Label);

    /// <summary>
    /// Concatenates two orthogonal polylines end-to-start, dropping the duplicated shared point so the
    /// join stays a single continuous orthogonal path.
    /// </summary>
    /// <param name="first">The leading polyline (its endpoint is the shared join point).</param>
    /// <param name="second">The trailing polyline (its start is the shared join point).</param>
    /// <returns>The concatenated polyline.</returns>
    private static List<Point2D> Concatenate(IReadOnlyList<Point2D> first, IReadOnlyList<Point2D> second)
    {
        var result = new List<Point2D>(first.Count + second.Count);
        result.AddRange(first);
        var start = result.Count > 0 && second.Count > 0 && SamePoint(result[^1], second[0]) ? 1 : 0;
        for (var i = start; i < second.Count; i++)
        {
            result.Add(second[i]);
        }

        return result;
    }

    /// <summary>
    /// Prepends a boundary anchor to a delegation polyline, inserting at most one orthogonal corner so
    /// the connector reaches the anchor without a diagonal segment.
    /// </summary>
    /// <param name="anchor">The single shared boundary anchor the connector must start from.</param>
    /// <param name="polyline">The router-produced delegation polyline (starting just inside the container).</param>
    /// <param name="boundaryFace">The container face the anchor sits on, deciding the corner orientation.</param>
    /// <returns>The delegation polyline with the anchor (and any orthogonal corner) prepended.</returns>
    private static List<Point2D> PrependAnchor(Point2D anchor, IReadOnlyList<Point2D> polyline, PortSide boundaryFace)
    {
        var result = new List<Point2D> { anchor };
        var start = polyline[0];
        var alignedX = Math.Abs(anchor.X - start.X) <= PointTolerance;
        var alignedY = Math.Abs(anchor.Y - start.Y) <= PointTolerance;
        if (!alignedX && !alignedY)
        {
            // Enter perpendicular to the face: a horizontal face turns vertically first, a vertical face
            // turns horizontally first, so both the anchor segment and the corner segment stay axis-aligned.
            var corner = boundaryFace is PortSide.Left or PortSide.Right
                ? new Point2D(start.X, anchor.Y)
                : new Point2D(anchor.X, start.Y);
            result.Add(corner);
        }

        var skipFirst = SamePoint(result[^1], start) ? 1 : 0;
        for (var i = skipFirst; i < polyline.Count; i++)
        {
            result.Add(polyline[i]);
        }

        return result;
    }

    /// <summary>Returns the endpoint of <paramref name="edge"/> that is not <paramref name="port"/>.</summary>
    /// <param name="edge">The delegation edge.</param>
    /// <param name="port">The boundary port whose opposite endpoint is required.</param>
    /// <returns>The edge endpoint opposite <paramref name="port"/>.</returns>
    private static ILayoutConnectable OtherEndpoint(LayoutGraphEdge edge, LayoutGraphPort port) =>
        ReferenceEquals(edge.Target, port) ? edge.Source : edge.Target;

    /// <summary>
    /// Computes the absolute offset that maps a boundary container's child-level local coordinates into
    /// the container's placed, padded (and title-offset) interior.
    /// </summary>
    /// <param name="result">The fully-placed recursive layout result.</param>
    /// <param name="level">The parent level owning the container.</param>
    /// <param name="child">The child level whose offset is computed.</param>
    /// <param name="offsetX">The parent level's own X offset.</param>
    /// <param name="offsetY">The parent level's own Y offset.</param>
    /// <returns>The absolute offset of the child level's local origin.</returns>
    private static (double X, double Y) ChildOffset(
        RecursiveLayoutResult result,
        MergeRegionLevel level,
        MergeRegionLevel child,
        double offsetX,
        double offsetY)
    {
        var nodeIndex = level.Children.First(entry => ReferenceEquals(entry.Child, child)).NodeIndex;
        var container = level.Nodes[nodeIndex];
        var graph = result.Levels[level].Graph;
        var titleHeight = HierarchicalLayoutAlgorithm.ResolveContentOffsetHeight(container);
        return (
            graph.AugX[nodeIndex] + offsetX + ContainerPaddingValue,
            graph.AugY[nodeIndex] + offsetY + ContainerPaddingValue + titleHeight);
    }

    /// <summary>Convenience accessor for the shared container padding constant.</summary>
    private static double ContainerPaddingValue => HierarchicalLayoutAlgorithm.ContainerPadding;

    /// <summary>
    /// Resolves the effective bounding-box size of <paramref name="node"/> for <paramref name="level"/>,
    /// falling back to the node's own dimensions when it is absent from the level's effective-size lookup.
    /// </summary>
    /// <param name="level">The level whose effective-size lookup is consulted.</param>
    /// <param name="node">The node whose size is required.</param>
    /// <returns>The effective width and height the node was placed with.</returns>
    private static (double Width, double Height) ResolveSize(MergeRegionLevel level, LayoutGraphNode node) =>
        level.EffectiveSize.TryGetValue(node, out var size) ? size : (node.Width, node.Height);

    /// <summary>
    /// Computes a level's placed footprint the same way <see cref="InterconnectionLayoutEngine"/> derives
    /// a leaf pass's canvas: the along-axis extent from the column geometry and the cross-axis extent
    /// from the placed node coordinates, both padded.
    /// </summary>
    /// <param name="graph">The placed level graph.</param>
    /// <param name="direction">The flow direction the level was laid out along.</param>
    /// <returns>The level's total width and height in logical pixels.</returns>
    internal static (double Width, double Height) LevelFootprint(LayeredGraph graph, LayoutDirection direction)
    {
        if (graph.ColumnX.Length == 0)
        {
            return (2.0 * Padding, 2.0 * Padding);
        }

        var lastLayer = graph.ColumnX.Length - 1;
        var alongTotal = graph.ColumnX[lastLayer] + graph.MaxColWidth[lastLayer] + Padding;
        var transposed = direction is LayoutDirection.Down or LayoutDirection.Up;
        var crossTotal = Padding;
        for (var i = 0; i < graph.Nodes.Count; i++)
        {
            // Down/Up directions leave graph.Nodes axis-swapped (see AxisTransform.NormalizeInputAxes),
            // so the cross-axis extent must come from RealWidth/RealHeight (the caller's true,
            // never-swapped dimensions), not Width/Height, to match how InterconnectionLayoutEngine.Place
            // computes the same footprint from its own un-swapped `nodes` parameter.
            var crossFar = transposed
                ? graph.AugX[i] + graph.Nodes[i].RealWidth
                : graph.AugY[i] + graph.Nodes[i].RealHeight;
            crossTotal = Math.Max(crossTotal, crossFar + Padding);
        }

        return transposed ? (crossTotal, alongTotal) : (alongTotal, crossTotal);
    }

    /// <summary>Returns whether two points coincide within <see cref="PointTolerance"/>.</summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns><see langword="true"/> when the points coincide.</returns>
    private static bool SamePoint(Point2D a, Point2D b) =>
        Math.Abs(a.X - b.X) <= PointTolerance && Math.Abs(a.Y - b.Y) <= PointTolerance;
}
