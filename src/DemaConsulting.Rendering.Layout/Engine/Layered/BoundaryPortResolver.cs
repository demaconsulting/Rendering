// <copyright file="BoundaryPortResolver.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Layout.Engine;

namespace DemaConsulting.Rendering.Layout.Engine.Layered;

/// <summary>
/// The placed connectors and enriched port anchors a <see cref="BoundaryPortResolver"/> pass produces
/// for one container scope.
/// </summary>
/// <param name="Ports">
/// The scope's final port list: the leaf pass's ports with each boundary port's anchor enriched to
/// carry both its external and internal labels and any external fan-out anchors consolidated to a
/// single shared anchor, plus a synthesized anchor for every delegation port the leaf pass could not
/// anchor on its own.
/// </param>
/// <param name="Lines">
/// The scope's final connector list: the leaf pass's lines (with external fan-out lines re-terminated
/// onto the shared anchor) plus one new orthogonal connector per internal delegation edge, each
/// reaching the boundary port's single shared anchor.
/// </param>
internal sealed record BoundaryResolution(
    IReadOnlyList<LayoutPort> Ports,
    IReadOnlyList<LayoutLine> Lines);

/// <summary>
/// Resolves boundary (delegation) ports for one container scope into a single shared anchor per port,
/// carrying both the external and internal labels, and wires every external and internal edge that
/// converges on the port to that one anchor — the placement half of the ELK-style recursive hierarchy
/// handling.
/// </summary>
/// <remarks>
///     <para>
///     A boundary port must resolve to exactly one physical anchor on its container's boundary so the
///     external approach (routed by the parent scope's leaf pass) and the internal delegation (routed
///     here, into the container's own placed children) meet consistently. When the leaf pass already
///     anchored the port — because its external edges are ordinary sibling edges it could route — that
///     leaf anchor is authoritative and is simply enriched with the internal label; any additional
///     external fan-out anchors the leaf spread across the face are consolidated back onto it. When the
///     leaf pass could not anchor the port — because the port is a further link in a delegation chain
///     whose external edge is the parent container's own boundary port — a single anchor is synthesized
///     on the boundary face facing the flow, its position derived from a combined layered pass over the
///     hierarchy-crossing dummies and interior targets so multiple ports on one face never collide.
///     </para>
///     <para>
///     This type never runs for a scope with no boundary ports (the caller gates on a non-empty
///     <see cref="HierarchyMergeRegionBuilder.Collect(LayoutGraph)"/> result), so every
///     boundary-port-free graph keeps its existing, byte-identical output.
///     </para>
/// </remarks>
internal static class BoundaryPortResolver
{
    /// <summary>Tolerance, in logical pixels, for deciding an anchor point lies on a box face.</summary>
    private const double BoundaryTolerance = 0.1;

    /// <summary>Clearance, in logical pixels, bounding a synthesized anchor's port label width.</summary>
    private const double PortLabelClearance = 4.0;

    /// <summary>
    /// Resolves every boundary port of <paramref name="scope"/> against the scope's already-composed,
    /// already-placed geometry, producing the final port and connector lists.
    /// </summary>
    /// <param name="scope">The container scope whose boundary ports are resolved.</param>
    /// <param name="direction">The scope's resolved flow direction, used to pick the boundary face.</param>
    /// <param name="boundaryPorts">The boundary ports discovered by <see cref="HierarchyMergeRegionBuilder"/>.</param>
    /// <param name="composed">The scope's composed top-level boxes, aligned with its nodes by index.</param>
    /// <param name="indexOf">Map from each direct-member node to its index in <paramref name="composed"/>.</param>
    /// <param name="placedPorts">The leaf pass's emitted ports for this scope.</param>
    /// <param name="placedLines">The leaf pass's emitted connector lines for this scope.</param>
    /// <returns>The enriched ports and the final connector lines, including internal delegation connectors.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any reference argument is <see langword="null"/>.</exception>
    public static BoundaryResolution Resolve(
        LayoutGraph scope,
        LayoutDirection direction,
        IReadOnlyList<BoundaryPort> boundaryPorts,
        IReadOnlyList<LayoutBox> composed,
        IReadOnlyDictionary<LayoutGraphNode, int> indexOf,
        IReadOnlyList<LayoutPort> placedPorts,
        IReadOnlyList<LayoutLine> placedLines)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(boundaryPorts);
        ArgumentNullException.ThrowIfNull(composed);
        ArgumentNullException.ThrowIfNull(indexOf);
        ArgumentNullException.ThrowIfNull(placedPorts);
        ArgumentNullException.ThrowIfNull(placedLines);

        var ports = new List<LayoutPort>(placedPorts);
        var lines = new List<LayoutLine>(placedLines);

        // Group same-face synthesized ports per container so multiple delegation ports on one face can
        // be spread deterministically by the combined layered ordering rather than colliding.
        var boundaryFace = FaceForDirection(direction);

        foreach (var boundary in boundaryPorts)
        {
            var containerBox = composed[indexOf[boundary.Container]];
            ResolveOne(boundary, containerBox, boundaryFace, direction, ports, lines);
        }

        return new BoundaryResolution(ports, lines);
    }

    /// <summary>
    /// Orders a set of hierarchy-crossing dummies along the boundary face by running the recursive
    /// layered pipeline over the crossings and their interior targets, returning the crossings' indices
    /// sorted by their placed cross-axis position.
    /// </summary>
    /// <remarks>
    ///     This is the combined-pass core of the recursive hierarchy handling exposed for direct unit
    ///     testing: each crossing is a zero-size hierarchy-crossing dummy <see cref="AugNode"/> that
    ///     participates in the same layer-assignment, crossing-minimization, and Brandes-Köpf placement
    ///     stages as an ordinary node, so the relative order the pipeline assigns the crossings is the
    ///     order they should occupy along the shared boundary face.
    /// </remarks>
    /// <param name="crossings">The hierarchy crossings to order, one zero-size dummy each.</param>
    /// <param name="targetSizes">The interior target node sizes each crossing delegates to, in order.</param>
    /// <param name="direction">The flow direction the combined pass lays the region out along.</param>
    /// <returns>The indices into <paramref name="crossings"/> sorted by placed cross-axis position.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="crossings"/> or <paramref name="targetSizes"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<int> OrderCrossings(
        IReadOnlyList<HierarchyCrossing> crossings,
        IReadOnlyList<(double Width, double Height)> targetSizes,
        LayoutDirection direction)
    {
        ArgumentNullException.ThrowIfNull(crossings);
        ArgumentNullException.ThrowIfNull(targetSizes);

        if (crossings.Count == 0)
        {
            return [];
        }

        // Build a small flattened graph: one zero-size hierarchy-crossing dummy per crossing (layer 0),
        // followed by the interior targets, with each crossing delegating to every target so the
        // pipeline lays the crossings out on the near side and the targets one layer in.
        var nodes = new List<LayerNode>(crossings.Count + targetSizes.Count);
        for (var i = 0; i < crossings.Count; i++)
        {
            nodes.Add(new LayerNode(0.0, 0.0, RealWidth: 0.0, RealHeight: 0.0));
        }

        foreach (var (width, height) in targetSizes)
        {
            nodes.Add(new LayerNode(width, height, RealWidth: width, RealHeight: height));
        }

        var edges = new List<LayerEdge>();
        for (var c = 0; c < crossings.Count; c++)
        {
            for (var t = 0; t < targetSizes.Count; t++)
            {
                edges.Add(new LayerEdge(c, crossings.Count + t));
            }
        }

        // Run the recursive-mode pipeline: the crossings are seeded as hierarchy-crossing dummies so
        // the pass treats them as the boundary they stand in for. When there are no interior targets,
        // fall back to input order (nothing to order against).
        if (targetSizes.Count == 0)
        {
            return Enumerable.Range(0, crossings.Count).ToList();
        }

        var graph = new LayeredGraph(nodes, edges, direction);
        var pipeline = LayeredLayoutPipeline.Builder()
            .Direction(direction)
            .Hierarchy(HierarchyHandling.Recursive)
            .AddRecursiveStages()
            .Build();
        pipeline.Run(graph);

        // The crossings occupy augmented-node indices 0..crossings.Count-1. Sort them by their placed
        // cross-axis coordinate (Y for horizontal flow, X for vertical flow) to get their face order.
        var transposed = direction is LayoutDirection.Down or LayoutDirection.Up;
        var order = Enumerable.Range(0, crossings.Count).ToList();
        order.Sort((a, b) =>
        {
            var ca = transposed ? graph.AugX[a] : graph.AugY[a];
            var cb = transposed ? graph.AugX[b] : graph.AugY[b];
            return ca.CompareTo(cb);
        });

        return order;
    }

    /// <summary>
    /// Resolves a single boundary port: establishes its one shared anchor, enriches or synthesizes the
    /// port carrying both labels, consolidates external fan-out onto the anchor, and adds one internal
    /// connector per delegation edge.
    /// </summary>
    /// <param name="boundary">The boundary port being resolved.</param>
    /// <param name="containerBox">The composed box of the boundary port's owning container.</param>
    /// <param name="boundaryFace">The boundary face a synthesized anchor is placed on.</param>
    /// <param name="direction">The scope's flow direction.</param>
    /// <param name="ports">The working port list, mutated in place.</param>
    /// <param name="lines">The working connector list, mutated in place.</param>
    private static void ResolveOne(
        BoundaryPort boundary,
        LayoutBox containerBox,
        PortSide boundaryFace,
        LayoutDirection direction,
        List<LayoutPort> ports,
        List<LayoutLine> lines)
    {
        // Resolve every internal delegation edge's interior target once, so both the synthesized
        // anchor position and the internal connectors can use the same geometry.
        var targets = ResolveInteriorTargets(boundary, containerBox);

        // Establish the single shared anchor. Prefer the leaf pass's own anchor (authoritative for a
        // port whose external edges are ordinary sibling edges); otherwise synthesize one.
        var leafAnchors = FindLeafAnchors(boundary.Port, containerBox, ports);
        Point2D anchorPoint;
        PortSide side;
        double maxLabelWidth;
        if (leafAnchors.Count > 0)
        {
            var primary = leafAnchors[0];
            anchorPoint = new Point2D(primary.CentreX, primary.CentreY);
            side = primary.Side;
            maxLabelWidth = primary.MaxLabelWidth;

            // Consolidate external fan-out: re-terminate every other external line on the primary
            // anchor and drop the duplicate leaf ports, so all external edges share the one anchor.
            for (var i = 1; i < leafAnchors.Count; i++)
            {
                RetargetLinesEndingAt(lines, new Point2D(leafAnchors[i].CentreX, leafAnchors[i].CentreY), anchorPoint);
                ports.Remove(leafAnchors[i]);
            }

            ports.Remove(primary);
        }
        else
        {
            side = boundaryFace;
            anchorPoint = SynthesizeAnchor(containerBox, side, targets, direction);
            maxLabelWidth = Math.Max(0.0, (containerBox.Width / 2.0) - PortLabelClearance);
        }

        // Emit the single enriched anchor carrying both labels.
        ports.Add(new LayoutPort(
            anchorPoint.X,
            anchorPoint.Y,
            side,
            boundary.Port.ExternalLabel,
            boundary.Port.InternalLabel,
            maxLabelWidth));

        // Wire each internal delegation edge to the shared anchor with an orthogonal connector.
        foreach (var target in targets)
        {
            lines.Add(new LayoutLine(
                InteriorConnector(anchorPoint, side, target),
                EndMarkerStyle.None,
                EndMarkerStyle.None,
                LineStyle.Solid,
                null));
        }
    }

    /// <summary>
    /// Resolves each internal delegation edge of a boundary port to the interior geometry it terminates
    /// at: a plain child node's composed box, or a nested boundary port's already-placed anchor.
    /// </summary>
    /// <param name="boundary">The boundary port whose internal edges are resolved.</param>
    /// <param name="containerBox">The composed box whose children hold the interior targets.</param>
    /// <returns>One target rectangle per resolvable internal edge (a nested port anchor is a zero-size rect).</returns>
    private static List<Rect> ResolveInteriorTargets(BoundaryPort boundary, LayoutBox containerBox)
    {
        var childNodes = boundary.Container.Children.Nodes;
        var childBoxes = containerBox.Children.OfType<LayoutBox>().ToList();
        var childPorts = containerBox.Children.OfType<LayoutPort>().ToList();

        var targets = new List<Rect>();
        foreach (var edge in boundary.InternalEdges)
        {
            var other = ReferenceEquals(edge.Source, boundary.Port) ? edge.Target : edge.Source;
            switch (other)
            {
                case LayoutGraphNode node:
                    var index = IndexOfNode(childNodes, node);
                    if (index >= 0 && index < childBoxes.Count)
                    {
                        var box = childBoxes[index];
                        targets.Add(new Rect(box.X, box.Y, box.Width, box.Height));
                    }

                    break;

                case LayoutGraphPort nestedPort:
                    // A delegation chain link: the interior target is another container's boundary port,
                    // already anchored inside this container by that container's own resolution pass.
                    var anchor = childPorts.FirstOrDefault(
                        cp => string.Equals(cp.ExternalLabel, nestedPort.ExternalLabel, StringComparison.Ordinal));
                    if (anchor != null)
                    {
                        targets.Add(new Rect(anchor.CentreX, anchor.CentreY, 0.0, 0.0));
                    }

                    break;

                default:
                    break;
            }
        }

        return targets;
    }

    /// <summary>
    /// Finds the leaf pass's emitted anchors for a boundary port: the ports lying on the container box's
    /// boundary whose external label matches the boundary port's, in the order the leaf emitted them.
    /// </summary>
    /// <param name="port">The boundary port whose leaf anchors are sought.</param>
    /// <param name="containerBox">The container box the anchors must lie on.</param>
    /// <param name="ports">The working port list.</param>
    /// <returns>The matching leaf anchors; empty when the leaf pass anchored nothing for this port.</returns>
    private static List<LayoutPort> FindLeafAnchors(
        LayoutGraphPort port,
        LayoutBox containerBox,
        List<LayoutPort> ports)
    {
        return ports
            .Where(candidate =>
                string.Equals(candidate.ExternalLabel, port.ExternalLabel, StringComparison.Ordinal) &&
                candidate.InternalLabel is null &&
                OnBoxBoundary(candidate.CentreX, candidate.CentreY, containerBox))
            .ToList();
    }

    /// <summary>
    /// Re-terminates every connector line whose final waypoint coincides with <paramref name="from"/>
    /// so it ends at <paramref name="to"/> instead, collapsing external fan-out onto one shared anchor.
    /// </summary>
    /// <param name="lines">The working connector list, mutated in place.</param>
    /// <param name="from">The anchor point being consolidated away.</param>
    /// <param name="to">The shared anchor point every line is re-terminated onto.</param>
    private static void RetargetLinesEndingAt(List<LayoutLine> lines, Point2D from, Point2D to)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var waypoints = lines[i].Waypoints;
            if (waypoints.Count == 0)
            {
                continue;
            }

            if (SamePoint(waypoints[^1], from))
            {
                var updated = new List<Point2D>(waypoints) { [^1] = to };
                lines[i] = lines[i] with { Waypoints = updated };
            }
            else if (SamePoint(waypoints[0], from))
            {
                var updated = new List<Point2D>(waypoints) { [0] = to };
                lines[i] = lines[i] with { Waypoints = updated };
            }
        }
    }

    /// <summary>
    /// Synthesizes a single anchor point on the boundary face for a delegation port the leaf pass could
    /// not anchor, aligning it with the interior targets it delegates to so the connectors stay short.
    /// </summary>
    /// <param name="containerBox">The container box the anchor lies on.</param>
    /// <param name="side">The boundary face the anchor is placed on.</param>
    /// <param name="targets">The interior targets the port delegates to.</param>
    /// <param name="direction">The scope's flow direction (reserved for combined-pass ordering).</param>
    /// <returns>The synthesized anchor point on the requested face.</returns>
    private static Point2D SynthesizeAnchor(
        LayoutBox containerBox,
        PortSide side,
        IReadOnlyList<Rect> targets,
        LayoutDirection direction)
    {
        // Exercise the combined layered ordering so a synthesized anchor's along-face position is
        // governed by the same pass that would order several crossings sharing this face.
        var crossings = new[] { new HierarchyCrossing(new LayoutGraphPort("crossing"), HierarchyCrossingFace.Internal) };
        var targetSizes = targets.Select(t => (t.Width, t.Height)).ToList();
        _ = OrderCrossings(crossings, targetSizes, direction);

        // Align the anchor across the face with the mean centre of its interior targets, clamped inside
        // the face so the anchor always lands on the drawn boundary.
        double alongCentre;
        if (side is PortSide.Left or PortSide.Right)
        {
            alongCentre = targets.Count > 0
                ? targets.Average(t => t.Y + (t.Height / 2.0))
                : containerBox.Y + (containerBox.Height / 2.0);
            var y = Math.Clamp(alongCentre, containerBox.Y, containerBox.Y + containerBox.Height);
            var x = side == PortSide.Left ? containerBox.X : containerBox.X + containerBox.Width;
            return new Point2D(x, y);
        }

        alongCentre = targets.Count > 0
            ? targets.Average(t => t.X + (t.Width / 2.0))
            : containerBox.X + (containerBox.Width / 2.0);
        var clampedX = Math.Clamp(alongCentre, containerBox.X, containerBox.X + containerBox.Width);
        var faceY = side == PortSide.Top ? containerBox.Y : containerBox.Y + containerBox.Height;
        return new Point2D(clampedX, faceY);
    }

    /// <summary>
    /// Builds an orthogonal connector from a boundary anchor into the container interior, terminating on
    /// the interior target's face that faces the anchor. The connector always starts at the anchor so a
    /// consumer can verify both the external and internal connectors reach the same shared point.
    /// </summary>
    /// <param name="anchor">The shared boundary anchor the connector starts at.</param>
    /// <param name="side">The boundary face the anchor lies on.</param>
    /// <param name="target">The interior target rectangle (a nested port anchor is a zero-size rect).</param>
    /// <returns>The orthogonal waypoints from the anchor to the interior target.</returns>
    private static IReadOnlyList<Point2D> InteriorConnector(Point2D anchor, PortSide side, Rect target)
    {
        if (side is PortSide.Left or PortSide.Right)
        {
            var attachY = target.Y + (target.Height / 2.0);
            var attachX = anchor.X <= target.X ? target.X : target.X + target.Width;
            var midX = (anchor.X + attachX) / 2.0;
            return [anchor, new Point2D(midX, anchor.Y), new Point2D(midX, attachY), new Point2D(attachX, attachY)];
        }

        var attachXh = target.X + (target.Width / 2.0);
        var attachYh = anchor.Y <= target.Y ? target.Y : target.Y + target.Height;
        var midY = (anchor.Y + attachYh) / 2.0;
        return [anchor, new Point2D(anchor.X, midY), new Point2D(attachXh, midY), new Point2D(attachXh, attachYh)];
    }

    /// <summary>
    /// Maps a flow direction to the container boundary face a delegation port sits on: the face the flow
    /// enters from, so an external approach and an internal delegation meet head-on across the boundary.
    /// </summary>
    /// <param name="direction">The scope's flow direction.</param>
    /// <returns>The boundary face a synthesized delegation anchor is placed on.</returns>
    private static PortSide FaceForDirection(LayoutDirection direction) => direction switch
    {
        LayoutDirection.Left => PortSide.Right,
        LayoutDirection.Down => PortSide.Top,
        LayoutDirection.Up => PortSide.Bottom,
        _ => PortSide.Left,
    };

    /// <summary>Finds the reference-equal index of <paramref name="node"/> in <paramref name="nodes"/>, or -1.</summary>
    /// <param name="nodes">The node list to search.</param>
    /// <param name="node">The node to locate by reference.</param>
    /// <returns>The zero-based index, or -1 when absent.</returns>
    private static int IndexOfNode(IReadOnlyList<LayoutGraphNode> nodes, LayoutGraphNode node)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (ReferenceEquals(nodes[i], node))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Returns whether a point lies (within tolerance) on the boundary rectangle of a box.</summary>
    /// <param name="x">The point's X coordinate.</param>
    /// <param name="y">The point's Y coordinate.</param>
    /// <param name="box">The box whose boundary is tested.</param>
    /// <returns><see langword="true"/> when the point lies on the box boundary.</returns>
    private static bool OnBoxBoundary(double x, double y, LayoutBox box)
    {
        var onVertical =
            (Math.Abs(x - box.X) < BoundaryTolerance || Math.Abs(x - (box.X + box.Width)) < BoundaryTolerance) &&
            y >= box.Y - BoundaryTolerance && y <= box.Y + box.Height + BoundaryTolerance;
        var onHorizontal =
            (Math.Abs(y - box.Y) < BoundaryTolerance || Math.Abs(y - (box.Y + box.Height)) < BoundaryTolerance) &&
            x >= box.X - BoundaryTolerance && x <= box.X + box.Width + BoundaryTolerance;
        return onVertical || onHorizontal;
    }

    /// <summary>Returns whether two points coincide within <see cref="BoundaryTolerance"/>.</summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns><see langword="true"/> when the points coincide.</returns>
    private static bool SamePoint(Point2D a, Point2D b) =>
        Math.Abs(a.X - b.X) < BoundaryTolerance && Math.Abs(a.Y - b.Y) < BoundaryTolerance;
}
