// <copyright file="ConnectorRouter.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Layout.Engine;

namespace DemaConsulting.Rendering.Layout;

/// <summary>
/// Describes one connector to be routed between two already-placed boxes, together with the styling
/// carried onto the resulting <see cref="LayoutLine"/>.
/// </summary>
/// <param name="From">
/// The source box. Its instance identity is used to exclude it from the obstacle set while routing,
/// so pass the exact <see cref="LayoutBox"/> reference that appears in the box list.
/// </param>
/// <param name="To">
/// The target box. Its instance identity is used to exclude it from the obstacle set while routing;
/// the connector's target end marker is drawn where the route meets this box.
/// </param>
/// <param name="TargetEnd">End-marker style drawn at the target (arrival) end of the connector.</param>
/// <param name="LineStyle">Stroke style (solid, dashed, dotted) applied to the routed connector.</param>
/// <param name="Label">Optional midpoint label carried onto the routed line; <see langword="null"/> for an unlabelled connector.</param>
/// <remarks>
/// A <see cref="Connection"/> carries no geometry of its own: source and target anchors are chosen by
/// <see cref="ConnectorRouter"/> from the boxes' current placement. The record intentionally models
/// only general diagram concepts (two endpoints plus line styling) and holds no domain-specific data.
/// </remarks>
public sealed record Connection(
    LayoutBox From,
    LayoutBox To,
    EndMarkerStyle TargetEnd = EndMarkerStyle.None,
    LineStyle LineStyle = LineStyle.Solid,
    string? Label = null);

/// <summary>
/// Options controlling how <see cref="ConnectorRouter"/> routes connectors among placed boxes.
/// </summary>
/// <param name="EdgeRouting">
/// Routing style to apply, mirroring ELK's <c>elk.edgeRouting</c>. Defaults to
/// <see cref="Rendering.EdgeRouting.Orthogonal"/>, the only shipped style; the switch grows additively
/// as new routers are implemented.
/// </param>
/// <param name="Clearance">
/// Minimum gap, in logical pixels, kept between routed segments and the boxes they steer around.
/// Defaults to <c>12.0</c>. Callers can override this to reproduce a specific spacing (for example a
/// downstream adapter matching its historical output).
/// </param>
/// <remarks>
/// The default value mirrors the property default on <see cref="CoreOptions.EdgeRouting"/>, so a
/// caller that has not selected a routing style gets orthogonal routing either way.
/// </remarks>
public sealed record ConnectorRouteOptions(
    EdgeRouting EdgeRouting = EdgeRouting.Orthogonal,
    double Clearance = 12.0);

/// <summary>
/// Routes connectors among a set of already-placed boxes, producing one <see cref="LayoutLine"/> per
/// connection. This is the public routing-orchestration entry point: it picks boundary anchors facing
/// the other endpoint, builds the obstacle set from the remaining boxes, and defers the per-connector
/// path to the router selected by <see cref="ConnectorRouteOptions.EdgeRouting"/>.
/// </summary>
/// <remarks>
/// <para>
/// The orchestration is deliberately model-agnostic: boxes are matched to their connections by
/// instance identity, and no domain concept (names, kinds, qualified references) enters the routing.
/// For each connection the two endpoint boxes are excluded from the obstacle set — a connector must
/// be free to leave and enter the boxes it joins — while every other box becomes a <see cref="Rect"/>
/// obstacle the route steers around by <see cref="ConnectorRouteOptions.Clearance"/>.
/// </para>
/// <para>
/// Source and target anchors are chosen on the box faces that front each other, based on the boxes'
/// relative placement rather than the direction to a possibly far-off centre, and are aligned to the
/// overlap of the boxes on the shared edge. Connectors therefore leave and arrive on the sides the two
/// boxes actually present to each other, without wrapping back across a wide endpoint box. The chosen
/// side is passed to the underlying router so the connector exits and enters perpendicular to the edge.
/// </para>
/// <para>
/// Today the only supported style is <see cref="Rendering.EdgeRouting.Orthogonal"/>, realized by the
/// library's internal orthogonal edge router. The dispatch is a single-arm switch structured so new
/// routing styles slot in additively without changing this contract.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Two already-placed boxes and an intervening one that the connector must avoid.
/// var a = new LayoutBox(0, 0, 80, 40, "A", 0, BoxShape.Rectangle, [], []);
/// var mid = new LayoutBox(140, -10, 60, 80, "M", 0, BoxShape.Rectangle, [], []);
/// var b = new LayoutBox(260, 0, 80, 40, "B", 0, BoxShape.Rectangle, [], []);
/// var boxes = new[] { a, mid, b };
///
/// // Route A -> B (avoiding M) and A -> M, carrying the requested styling.
/// var connections = new[]
/// {
///     new Connection(a, b, EndMarkerStyle.FilledArrow),
///     new Connection(a, mid, EndMarkerStyle.HollowDiamond, LineStyle.Dashed, "owns"),
/// };
///
/// var lines = ConnectorRouter.Route(boxes, connections, new ConnectorRouteOptions());
///
/// // Drop the placed boxes and the routed connectors into a LayoutTree for a renderer.
/// var nodes = new List&lt;LayoutNode&gt;();
/// nodes.AddRange(boxes);
/// nodes.AddRange(lines);
/// var tree = new LayoutTree(360, 80, nodes);
/// </code>
/// </example>
public static class ConnectorRouter
{
    /// <summary>
    /// Routes every connection in <paramref name="connections"/> among the placed
    /// <paramref name="boxes"/>, returning one routed <see cref="LayoutLine"/> per connection in the
    /// same order.
    /// </summary>
    /// <param name="boxes">
    /// All placed boxes on the canvas. Boxes other than a connection's two endpoints act as obstacles
    /// for that connection's route.
    /// </param>
    /// <param name="connections">The connectors to route, each naming a source and target box.</param>
    /// <param name="options">Routing options, including the routing style and obstacle clearance.</param>
    /// <returns>One routed <see cref="LayoutLine"/> per connection, in input order.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="boxes"/>, <paramref name="connections"/>, <paramref name="options"/>,
    /// or any connection (or its <see cref="Connection.From"/> / <see cref="Connection.To"/>) is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when <see cref="ConnectorRouteOptions.EdgeRouting"/> names a style that has no shipped
    /// router.
    /// </exception>
    /// <remarks>
    /// Prefer this overload over calling the single-connection <see cref="Route(IReadOnlyList{LayoutBox},Connection,ConnectorRouteOptions)"/>
    /// once per connector whenever several connectors may land on the same box face (for example many
    /// cross-package edges converging on one shared box). <see cref="FacingAnchors"/> picks each
    /// connector's anchor independently from its own pair of boxes; when two boxes do not overlap on the
    /// face's axis, that per-pair calculation can clamp to the same boundary point for every connector
    /// sharing the face, making them visually collapse into one another. Routing as a batch lets this
    /// method detect a face shared by more than one connector and spread their anchors evenly across it
    /// (see <see cref="DistributeSharedFaceAnchors"/>) before any obstacle-avoiding path is computed. It
    /// also routes connectors one at a time, adding each already-routed line's own path as a soft
    /// obstacle for the ones that follow, so parallel connectors bound for nearby anchors fan out into
    /// separate corridors instead of overlapping along a shared trunk.
    /// </remarks>
    public static IReadOnlyList<LayoutLine> Route(
        IReadOnlyList<LayoutBox> boxes,
        IReadOnlyList<Connection> connections,
        ConnectorRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(options);

        var anchors = new FaceAnchors[connections.Count];
        for (var i = 0; i < connections.Count; i++)
        {
            var connection = connections[i];
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentNullException.ThrowIfNull(connection.From);
            ArgumentNullException.ThrowIfNull(connection.To);

            var (source, sourceSide, target, targetSide) = FacingAnchors(connection.From, connection.To);
            anchors[i] = new FaceAnchors(source, sourceSide, target, targetSide);
        }

        DistributeSharedFaceAnchors(connections, anchors, options.Clearance);

        // Route one connector at a time, growing a soft-obstacle set with each already-routed line (as
        // a thin rectangle per segment) so later connectors prefer a free lane over one already used by
        // an earlier connector. These are soft (cost-penalized), not hard, obstacles: several connectors
        // converging on the same box face legitimately share their final short approach corridor, and
        // hard-blocking it would make that shared face unreachable for every connector after the first
        // (see AddLineObstacles remarks).
        var lines = new List<LayoutLine>(connections.Count);
        var routedLineObstacles = new List<Rect>();
        foreach (var t in anchors)
        {
            var index = lines.Count;
            var line = RouteWithAnchors(boxes, connections[index], options, t, routedLineObstacles);
            lines.Add(line);
            AddLineObstacles(line.Waypoints, routedLineObstacles);
        }

        return lines;
    }

    /// <summary>
    /// Routes a single <paramref name="connection"/> among the placed <paramref name="boxes"/>.
    /// </summary>
    /// <param name="boxes">
    /// All placed boxes on the canvas. Every box except the connection's two endpoints acts as an
    /// obstacle for this route.
    /// </param>
    /// <param name="connection">The connector to route.</param>
    /// <param name="options">Routing options, including the routing style and obstacle clearance.</param>
    /// <returns>The routed connector as a <see cref="LayoutLine"/> carrying the connection's styling.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="boxes"/>, <paramref name="connection"/>, <paramref name="options"/>,
    /// or the connection's <see cref="Connection.From"/> / <see cref="Connection.To"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when <see cref="ConnectorRouteOptions.EdgeRouting"/> names a style that has no shipped
    /// router.
    /// </exception>
    /// <remarks>
    /// This overload considers only <paramref name="connection"/>'s own two boxes when picking anchors,
    /// so it cannot spread anchors across a box face that also receives other connectors routed by
    /// separate calls, nor steer around any other connector's path. When routing several connectors that
    /// may share a target (or source) box, or may run parallel to one another, prefer the batch
    /// <see cref="Route(IReadOnlyList{LayoutBox},IReadOnlyList{Connection},ConnectorRouteOptions)"/>
    /// overload instead.
    /// </remarks>
    public static LayoutLine Route(
        IReadOnlyList<LayoutBox> boxes,
        Connection connection,
        ConnectorRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(connection.From);
        ArgumentNullException.ThrowIfNull(connection.To);

        // Pick anchors on the faces the two boxes actually present to each other, based on their
        // relative placement. Using the direction to the other box's centre misfires for wide boxes
        // (whose centre can sit far past the near endpoint), forcing the connector to wrap back across a
        // box; deriving the facing sides from the box rectangles avoids that.
        var (source, sourceSide, target, targetSide) = FacingAnchors(connection.From, connection.To);

        return RouteWithAnchors(boxes, connection, options, new FaceAnchors(source, sourceSide, target, targetSide), []);
    }

    /// <summary>
    /// Builds the obstacle set for <paramref name="connection"/> — every other box, plus any already
    /// routed <paramref name="extraSoftObstacles"/> treated as soft (cost-penalized, never hard-blocking)
    /// obstacles — and routes it between the already chosen <paramref name="anchors"/>.
    /// </summary>
    private static LayoutLine RouteWithAnchors(
        IReadOnlyList<LayoutBox> boxes,
        Connection connection,
        ConnectorRouteOptions options,
        FaceAnchors anchors,
        IReadOnlyList<Rect> extraSoftObstacles)
    {
        var from = connection.From;
        var to = connection.To;

        // The hard obstacle set is every box except this connection's own endpoints, matched by
        // instance identity. The connector must be free to leave and enter the boxes it joins.
        var obstacles = new List<Rect>(boxes.Count);
        foreach (var box in boxes)
        {
            if (ReferenceEquals(box, from) || ReferenceEquals(box, to))
            {
                continue;
            }

            obstacles.Add(new Rect(box.X, box.Y, box.Width, box.Height));
        }

        var waypoints = RouteWaypoints(
            options.EdgeRouting, anchors.Source, anchors.Target, obstacles, options.Clearance, anchors.SourceSide, anchors.TargetSide, extraSoftObstacles);

        return new LayoutLine(
            Waypoints: waypoints,
            SourceEnd: EndMarkerStyle.None,
            TargetEnd: connection.TargetEnd,
            LineStyle: connection.LineStyle,
            MidpointLabel: connection.Label);
    }

    /// <summary>
    /// Appends a thin rectangle obstacle for every orthogonal segment of <paramref name="waypoints"/> to
    /// <paramref name="obstacles"/>, so a subsequently routed connector treats this already-routed line
    /// as something to steer clear of (by the router's usual obstacle clearance) rather than freely
    /// overlapping it.
    /// </summary>
    private static void AddLineObstacles(IReadOnlyList<Point2D> waypoints, List<Rect> obstacles)
    {
        // A hairline thickness is enough: the router already keeps a clearance gap between paths and
        // any obstacle, so a wide slab here would only double up on that margin unnecessarily.
        const double halfThickness = 1.0;

        for (var i = 0; i < waypoints.Count - 1; i++)
        {
            var a = waypoints[i];
            var b = waypoints[i + 1];

            if (Math.Abs(a.Y - b.Y) < 1e-6)
            {
                // Horizontal segment.
                var x = Math.Min(a.X, b.X);
                var width = Math.Abs(b.X - a.X);
                if (width < 1e-6)
                {
                    continue;
                }

                obstacles.Add(new Rect(x, a.Y - halfThickness, width, 2.0 * halfThickness));
            }
            else if (Math.Abs(a.X - b.X) < 1e-6)
            {
                // Vertical segment.
                var y = Math.Min(a.Y, b.Y);
                var height = Math.Abs(b.Y - a.Y);
                if (height < 1e-6)
                {
                    continue;
                }

                obstacles.Add(new Rect(a.X - halfThickness, y, 2.0 * halfThickness, height));
            }
        }
    }

    /// <summary>
    /// The chosen source and target anchors and box sides for one connection, prior to obstacle-avoiding
    /// path computation.
    /// </summary>
    private readonly record struct FaceAnchors(Point2D Source, PortSide SourceSide, Point2D Target, PortSide TargetSide);

    /// <summary>
    /// One connector's claim on a shared box face: which connection it belongs to, whether it is the
    /// connection's source or target end, and the position (its counterpart box's centre on the face's
    /// axis) used to order claims before spreading them evenly across the face.
    /// </summary>
    private readonly record struct FaceSlot(int ConnectionIndex, bool IsSource, double Counterpart);

    /// <summary>
    /// Redistributes anchors for any box face claimed by more than one connector in this batch, so
    /// co-terminating connectors land at evenly spaced points along the face instead of each
    /// independently clamping to the same boundary point (see <see cref="FacingAnchors"/>).
    /// </summary>
    /// <param name="connections">The connections being routed, in the same order as <paramref name="anchors"/>.</param>
    /// <param name="anchors">
    /// The naive per-connection anchors computed by <see cref="FacingAnchors"/>; updated in place for any
    /// connector whose anchor is moved to share its face fairly with others.
    /// </param>
    /// <param name="clearance">Minimum inset kept between a redistributed anchor and the ends of the face.</param>
    private static void DistributeSharedFaceAnchors(
        IReadOnlyList<Connection> connections,
        FaceAnchors[] anchors,
        double clearance)
    {
        var groups = new Dictionary<(LayoutBox Box, PortSide Side), List<FaceSlot>>(FaceKeyComparer.Instance);

        for (var i = 0; i < connections.Count; i++)
        {
            var connection = connections[i];
            var faceAnchors = anchors[i];

            AddSlot(groups, connection.From, faceAnchors.SourceSide, i, isSource: true, CounterpartCentre(connection.To, faceAnchors.SourceSide));
            AddSlot(groups, connection.To, faceAnchors.TargetSide, i, isSource: false, CounterpartCentre(connection.From, faceAnchors.TargetSide));
        }

        foreach (var (key, slots) in groups)
        {
            if (slots.Count < 2)
            {
                // A face claimed by a single connector already has the best anchor FacingAnchors could
                // compute for that pair of boxes; leave it untouched.
                continue;
            }

            var (box, side) = key;
            var isVerticalFace = side is PortSide.Left or PortSide.Right;
            var faceLo = isVerticalFace ? box.Y : box.X;
            var faceExtent = isVerticalFace ? box.Height : box.Width;
            var fixedCoord = side switch
            {
                PortSide.Left => box.X,
                PortSide.Right => box.X + box.Width,
                PortSide.Top => box.Y,
                _ => box.Y + box.Height,
            };

            // Cap the inset so the usable span never inverts for a face too small to hold the full
            // clearance on both ends; degrade gracefully by collapsing ports toward the centre instead.
            var inset = Math.Min(clearance, faceExtent / 2.0);
            var usable = faceExtent - (2.0 * inset);
            var ordered = slots.OrderBy(s => s.Counterpart).ThenBy(s => s.ConnectionIndex).ToList();
            var count = ordered.Count;

            for (var k = 0; k < count; k++)
            {
                var along = faceLo + inset + (count == 1 ? usable / 2.0 : k * usable / (count - 1));
                var point = isVerticalFace ? new Point2D(fixedCoord, along) : new Point2D(along, fixedCoord);

                var slot = ordered[k];
                var current = anchors[slot.ConnectionIndex];
                anchors[slot.ConnectionIndex] = slot.IsSource
                    ? current with { Source = point }
                    : current with { Target = point };
            }
        }
    }

    /// <summary>Records one connector's claim on a shared box face for later distribution.</summary>
    private static void AddSlot(
        Dictionary<(LayoutBox Box, PortSide Side), List<FaceSlot>> groups,
        LayoutBox box,
        PortSide side,
        int connectionIndex,
        bool isSource,
        double counterpartCentre)
    {
        var key = (box, side);
        if (!groups.TryGetValue(key, out var slots))
        {
            slots = [];
            groups[key] = slots;
        }

        slots.Add(new FaceSlot(connectionIndex, isSource, counterpartCentre));
    }

    /// <summary>
    /// The centre of <paramref name="box"/> on the axis perpendicular to <paramref name="faceSide"/>,
    /// used to order connectors sharing a face by where their counterpart box sits.
    /// </summary>
    private static double CounterpartCentre(LayoutBox box, PortSide faceSide) =>
        faceSide is PortSide.Left or PortSide.Right
            ? box.Y + (box.Height / 2.0)
            : box.X + (box.Width / 2.0);

    /// <summary>
    /// Matches box faces by the source box's instance identity rather than <see cref="LayoutBox"/>'s
    /// record value-equality, so two distinct boxes that happen to share the same geometry are not
    /// merged into a single face group.
    /// </summary>
    private sealed class FaceKeyComparer : IEqualityComparer<(LayoutBox Box, PortSide Side)>
    {
        public static readonly FaceKeyComparer Instance = new();

        public bool Equals((LayoutBox Box, PortSide Side) x, (LayoutBox Box, PortSide Side) y) =>
            ReferenceEquals(x.Box, y.Box) && x.Side == y.Side;

        public int GetHashCode((LayoutBox Box, PortSide Side) obj) =>
            HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Box), obj.Side);
    }

    /// <summary>
    /// Dispatches to the router realizing the requested <paramref name="edgeRouting"/> style.
    /// </summary>
    private static IReadOnlyList<Point2D> RouteWaypoints(
        EdgeRouting edgeRouting,
        Point2D source,
        Point2D target,
        IReadOnlyList<Rect> obstacles,
        double clearance,
        PortSide sourceSide,
        PortSide targetSide,
        IReadOnlyList<Rect>? softObstacles = null) => edgeRouting switch
        {
            EdgeRouting.Orthogonal =>
                OrthogonalEdgeRouter.RouteWithStatus(
                    source, target, obstacles, clearance, sourceSide, targetSide, costBands: null, softObstacles).Waypoints,
            _ => throw new NotSupportedException($"Edge routing style '{edgeRouting}' has no shipped router."),
        };

    /// <summary>
    /// Chooses boundary anchors on the two box faces that front each other, based on the boxes'
    /// relative placement. The connector leaves and enters on the sides actually facing the other box —
    /// the axis along which the boxes are more separated — and each anchor is aligned to the overlap of
    /// the boxes on the shared edge when they overlap, or to each box's own face centre when they don't,
    /// so the route stays short and every anchor sits at a natural point on its own box regardless of
    /// how far away the other box sits.
    /// </summary>
    /// <param name="from">The source box.</param>
    /// <param name="to">The target box.</param>
    /// <returns>The source anchor and side, and the target anchor and side.</returns>
    private static (Point2D Source, PortSide SourceSide, Point2D Target, PortSide TargetSide) FacingAnchors(
        LayoutBox from, LayoutBox to)
    {
        // Signed separation on each axis: positive when the boxes clear each other on that axis,
        // negative when they overlap. Anchor on the axis with the greater separation so the connector
        // spans the real gap between the boxes.
        var horizontalGap = Math.Max(to.X - (from.X + from.Width), from.X - (to.X + to.Width));
        var verticalGap = Math.Max(to.Y - (from.Y + from.Height), from.Y - (to.Y + to.Height));

        if (horizontalGap >= verticalGap)
        {
            // Left/right relationship: the left box anchors its right face, the right box its left face.
            var fromIsLeft = from.X + (from.Width / 2.0) <= to.X + (to.Width / 2.0);
            var fromY = AnchorCoordinate(from.Y, from.Y + from.Height, to.Y, to.Y + to.Height);
            var toY = AnchorCoordinate(to.Y, to.Y + to.Height, from.Y, from.Y + from.Height);
            return fromIsLeft
                ? (new Point2D(from.X + from.Width, fromY), PortSide.Right,
                   new Point2D(to.X, toY), PortSide.Left)
                : (new Point2D(from.X, fromY), PortSide.Left,
                   new Point2D(to.X + to.Width, toY), PortSide.Right);
        }

        // Top/bottom relationship: the upper box anchors its bottom face, the lower box its top face.
        var fromIsAbove = from.Y + (from.Height / 2.0) <= to.Y + (to.Height / 2.0);
        var fromX = AnchorCoordinate(from.X, from.X + from.Width, to.X, to.X + to.Width);
        var toX = AnchorCoordinate(to.X, to.X + to.Width, from.X, from.X + from.Width);
        return fromIsAbove
            ? (new Point2D(fromX, from.Y + from.Height), PortSide.Bottom,
               new Point2D(toX, to.Y), PortSide.Top)
            : (new Point2D(fromX, from.Y), PortSide.Top,
               new Point2D(toX, to.Y + to.Height), PortSide.Bottom);
    }

    /// <summary>
    /// Returns the anchor coordinate for a box face spanning [<paramref name="lo"/>, <paramref name="hi"/>]
    /// against the facing box's span [<paramref name="otherLo"/>, <paramref name="otherHi"/>] on the same
    /// axis: the centre of their overlap when the spans overlap (keeping both ends aligned on a single
    /// coordinate for a short, straight hop across the shared span), or this box's own centre when they
    /// don't overlap at all. The "own centre" fallback matters because a shared "gap midpoint" between
    /// two far-apart, differently sized boxes can fall entirely outside one (or both) box's own span;
    /// clamping that midpoint into the box's range would then pin the anchor to whichever edge happens to
    /// be nearest, rather than the natural middle of the face every other connector uses.
    /// </summary>
    private static double AnchorCoordinate(double lo, double hi, double otherLo, double otherHi)
    {
        var overlapLo = Math.Max(lo, otherLo);
        var overlapHi = Math.Min(hi, otherHi);
        return overlapLo <= overlapHi ? (overlapLo + overlapHi) / 2.0 : (lo + hi) / 2.0;
    }
}
