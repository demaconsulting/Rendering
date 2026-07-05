// <copyright file="ConnectorRouter.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;
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
/// overlap of the boxes on the shared edge. When a face is long enough, that overlap-based coordinate
/// is kept at least <see cref="ConnectorRouteOptions.Clearance"/> away from both ends of the face so
/// a slight shared span near one corner does not pin the connector visually against that corner. Each
/// face is then interpreted through the box shape's own routing geometry: only its connectable
/// sub-ranges may be used, an unusable natural face falls back to the next-best adjacent face, and the
/// final anchor is projected inward from the bounding box to the real outline when the shape is
/// recessed there (for example the body top of a folder below its tab). Connectors therefore leave and
/// arrive on the sides the two boxes actually present to each other, without wrapping back across a
/// wide endpoint box or anchoring on a shape's non-connectable outline detail. The chosen side is
/// passed to the underlying router so the connector exits and enters perpendicular to the edge.
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

            var (sourceAlong, sourceSide, targetAlong, targetSide) = FacingAnchors(connection.From, connection.To, options.Clearance);
            anchors[i] = new FaceAnchors(sourceAlong, sourceSide, targetAlong, targetSide);
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
        var (sourceAlong, sourceSide, targetAlong, targetSide) = FacingAnchors(connection.From, connection.To, options.Clearance);

        return RouteWithAnchors(
            boxes,
            connection,
            options,
            new FaceAnchors(sourceAlong, sourceSide, targetAlong, targetSide),
            []);
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
        var source = ResolveAnchorPoint(from, anchors.SourceSide, anchors.SourceAlong, options.Clearance);
        var target = ResolveAnchorPoint(to, anchors.TargetSide, anchors.TargetAlong, options.Clearance);

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
            options.EdgeRouting, source, target, obstacles, options.Clearance, anchors.SourceSide, anchors.TargetSide, extraSoftObstacles);

        return new LayoutLine(
            Waypoints: waypoints,
            SourceEnd: EndMarkerStyle.None,
            TargetEnd: connection.TargetEnd,
            LineStyle: connection.LineStyle,
            MidpointLabel: connection.Label);
    }

    /// <summary>
    /// Appends a thin rectangle obstacle for every interior orthogonal segment of
    /// <paramref name="waypoints"/> to <paramref name="obstacles"/>, so a subsequently routed
    /// connector treats this already-routed line as something to steer clear of (by the router's usual
    /// obstacle clearance) rather than freely overlapping it.
    /// </summary>
    /// <remarks>
    /// The first and last segments are intentionally excluded. Those short endpoint-adjacent approach
    /// legs are the one place where several connectors may legitimately share the same corridor while
    /// still reaching a common face cleanly; penalizing them as soft obstacles is what causes the
    /// redundant leave-and-return detours this method now avoids.
    /// </remarks>
    private static void AddLineObstacles(IReadOnlyList<Point2D> waypoints, List<Rect> obstacles)
    {
        // A hairline thickness is enough: the router already keeps a clearance gap between paths and
        // any obstacle, so a wide slab here would only double up on that margin unnecessarily.
        const double halfThickness = 1.0;

        for (var i = 0; i < waypoints.Count - 1; i++)
        {
            if (i == 0 || i == waypoints.Count - 2)
            {
                continue;
            }

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
    /// The chosen source and target face coordinates and box sides for one connection, prior to
    /// obstacle-avoiding path computation. The along-axis coordinates are stored in the owning box's
    /// local face coordinate system (0 at the face start, increasing toward the face end) so shared-face
    /// redistribution can work directly against shape-aware connectable extents.
    /// </summary>
    private readonly record struct FaceAnchors(double SourceAlong, PortSide SourceSide, double TargetAlong, PortSide TargetSide);

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
    /// connector whose anchor is moved to share its face fairly with others. The redistribution spans
    /// the union of the face's connectable extents rather than the full bounding-box edge.
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
            var geometry = ResolveShapeGeometry(box);
            var usableExtents = BuildUsableExtents(geometry.GetConnectableExtents(side), clearance);
            if (usableExtents.Count == 0)
            {
                continue;
            }

            var usableLength = TotalExtentLength(usableExtents);
            var ordered = slots.OrderBy(s => s.Counterpart).ThenBy(s => s.ConnectionIndex).ToList();
            var count = ordered.Count;

            for (var k = 0; k < count; k++)
            {
                var distance = count == 1 ? usableLength / 2.0 : k * usableLength / (count - 1);
                var along = CoordinateAtDistance(usableExtents, distance);
                var slot = ordered[k];
                var current = anchors[slot.ConnectionIndex];
                anchors[slot.ConnectionIndex] = slot.IsSource
                    ? current with { SourceAlong = along }
                    : current with { TargetAlong = along };
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
    /// Insets each connectable face segment by up to <paramref name="clearance"/> at both ends so
    /// redistributed anchors keep the same breathing room from the face edges that plain rectangular
    /// distribution already used. When that inset would collapse every segment to a point, the original
    /// extents are used instead so a reduced-but-still-usable face does not become artificially empty.
    /// </summary>
    private static IReadOnlyList<(double Lo, double Hi)> BuildUsableExtents(
        IReadOnlyList<(double Lo, double Hi)> extents,
        double clearance)
    {
        var insetExtents = new List<(double Lo, double Hi)>(extents.Count);
        foreach (var (lo, hi) in extents)
        {
            if (hi < lo)
            {
                continue;
            }

            var inset = Math.Min(clearance, (hi - lo) / 2.0);
            insetExtents.Add((lo + inset, hi - inset));
        }

        if (TotalExtentLength(insetExtents) > 1e-9)
        {
            return insetExtents;
        }

        var fallback = new List<(double Lo, double Hi)>(extents.Count);
        foreach (var (lo, hi) in extents)
        {
            if (hi >= lo)
            {
                fallback.Add((lo, hi));
            }
        }

        return fallback;
    }

    /// <summary>
    /// Returns the total length covered by a set of local face extents.
    /// </summary>
    private static double TotalExtentLength(IReadOnlyList<(double Lo, double Hi)> extents)
    {
        var total = 0.0;
        foreach (var (lo, hi) in extents)
        {
            total += Math.Max(0.0, hi - lo);
        }

        return total;
    }

    /// <summary>
    /// Returns the local face coordinate lying <paramref name="distance"/> units along the concatenated
    /// union of <paramref name="extents"/>.
    /// </summary>
    private static double CoordinateAtDistance(IReadOnlyList<(double Lo, double Hi)> extents, double distance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(distance);

        var remaining = distance;
        for (var i = 0; i < extents.Count; i++)
        {
            var (lo, hi) = extents[i];
            var length = Math.Max(0.0, hi - lo);
            if (remaining <= length || i == extents.Count - 1)
            {
                return lo + Math.Min(remaining, length);
            }

            remaining -= length;
        }

        return 0.0;
    }

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
    /// Small inset applied immediately to the right of a folder tab so a top-face anchor never lands
    /// exactly on the tab's vertical shoulder.
    /// </summary>
    private const double FolderTopFaceMargin = 1.0;

    /// <summary>
    /// Describes the parts of a box face that connectors may use and how a point chosen on the bounding
    /// box projects inward to the shape's real outline.
    /// </summary>
    /// <remarks>
    /// Current shipped shapes use constant per-face projections, but <see cref="ProjectToSurface"/>
    /// intentionally receives the along-face coordinate so a future shape can vary its projection across
    /// the face (for example a sloped or curved shoulder) without changing the router's calling
    /// contract.
    /// </remarks>
    private interface IBoxShapeGeometry
    {
        /// <summary>
        /// Returns the local along-face sub-ranges, in logical pixels, where connectors may anchor on
        /// <paramref name="side"/>. Coordinates are measured from the face start (top-to-bottom on left
        /// and right faces; left-to-right on top and bottom faces). An empty list means the face is
        /// unusable for anchoring.
        /// </summary>
        IReadOnlyList<(double Lo, double Hi)> GetConnectableExtents(PortSide side);

        /// <summary>
        /// Returns the inward perpendicular offset, in logical pixels, from the bounding-box face on
        /// <paramref name="side"/> to the real shape outline at the given local along-face coordinate.
        /// A value of zero means the bounding box already lies on the drawn outline.
        /// </summary>
        double ProjectToSurface(PortSide side, double alongAxisCoordinate);
    }

    /// <summary>
    /// Shared base for the shipped box-shape geometries.
    /// </summary>
    private abstract class BoxShapeGeometryBase : IBoxShapeGeometry
    {
        /// <summary>
        /// Gets the width of the owning box, used when resolving top and bottom face extents.
        /// </summary>
        protected double Width { get; }

        /// <summary>
        /// Gets the height of the owning box, used when resolving left and right face extents.
        /// </summary>
        protected double Height { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BoxShapeGeometryBase"/> class.
        /// </summary>
        protected BoxShapeGeometryBase(LayoutBox box)
        {
            Width = box.Width;
            Height = box.Height;
        }

        /// <inheritdoc/>
        public abstract IReadOnlyList<(double Lo, double Hi)> GetConnectableExtents(PortSide side);

        /// <inheritdoc/>
        public abstract double ProjectToSurface(PortSide side, double alongAxisCoordinate);

        /// <summary>
        /// Returns a single full-length extent for <paramref name="side"/>.
        /// </summary>
        protected IReadOnlyList<(double Lo, double Hi)> FullExtent(PortSide side) =>
            [(0.0, BoxFaceLength(side))];

        /// <summary>
        /// Returns the local length of <paramref name="side"/>.
        /// </summary>
        protected double BoxFaceLength(PortSide side) =>
            side is PortSide.Left or PortSide.Right ? Height : Width;

        /// <summary>
        /// Builds a single connectable segment inset equally from both ends of the given face length.
        /// If the inset consumes the whole face, the remaining usable anchor point collapses to the face
        /// midpoint instead of becoming invalid.
        /// </summary>
        protected static IReadOnlyList<(double Lo, double Hi)> InsetExtent(double faceLength, double inset)
        {
            var boundedInset = Math.Min(Math.Max(0.0, inset), faceLength / 2.0);
            return [(boundedInset, faceLength - boundedInset)];
        }
    }

    /// <summary>
    /// Rectangle geometry: every face is fully usable and the bounding box already lies on the drawn
    /// outline.
    /// </summary>
    private sealed class RectangleGeometry : BoxShapeGeometryBase
    {
        public RectangleGeometry(LayoutBox box)
            : base(box)
        {
        }

        public override IReadOnlyList<(double Lo, double Hi)> GetConnectableExtents(PortSide side) => FullExtent(side);

        public override double ProjectToSurface(PortSide side, double alongAxisCoordinate) => 0.0;
    }

    /// <summary>
    /// Rounded-rectangle geometry: each face is usable only between the two corner arcs.
    /// </summary>
    private sealed class RoundedRectangleGeometry : BoxShapeGeometryBase
    {
        private readonly double _cornerRadius;

        public RoundedRectangleGeometry(LayoutBox box)
            : base(box)
        {
            _cornerRadius = Math.Max(0.0, box.RoundedCornerRadius ?? 0.0);
        }

        public override IReadOnlyList<(double Lo, double Hi)> GetConnectableExtents(PortSide side) =>
            InsetExtent(BoxFaceLength(side), _cornerRadius);

        public override double ProjectToSurface(PortSide side, double alongAxisCoordinate) => 0.0;
    }

    /// <summary>
    /// Small inset applied immediately beside a note's folded corner so a face anchor never lands
    /// exactly on the fold's edge.
    /// </summary>
    private const double NoteFoldMargin = 1.0;

    /// <summary>
    /// Note geometry: the top-right corner is cut by a diagonal fold, so the affected portions of the
    /// top face (near the right edge) and the right face (near the top edge) are excluded from the
    /// connectable extent. The remaining extents lie exactly on the bounding box, matching the
    /// rounded-rectangle pattern, so no surface projection offset is needed.
    /// </summary>
    private sealed class NoteGeometry : BoxShapeGeometryBase
    {
        private readonly double _fold;

        public NoteGeometry(LayoutBox box)
            : base(box)
        {
            _fold = Math.Min(Math.Min(box.Width, box.Height) * NotationMetrics.NoteFoldFraction, NotationMetrics.NoteFoldMaxSize);
        }

        public override IReadOnlyList<(double Lo, double Hi)> GetConnectableExtents(PortSide side)
        {
            if (_fold <= 0.0)
            {
                return FullExtent(side);
            }

            switch (side)
            {
                case PortSide.Top:
                    // The fold cuts diagonally from (Width - fold, 0) to (Width, fold); only the
                    // portion left of the fold's near edge still touches the real top outline.
                    var topEnd = Math.Max(0.0, Width - _fold - NoteFoldMargin);
                    return topEnd <= 0.0 ? [] : [(0.0, topEnd)];

                case PortSide.Right:
                    // The same diagonal cut removes the topmost portion of the right face.
                    var rightStart = Math.Min(Height, _fold + NoteFoldMargin);
                    return rightStart >= Height ? [] : [(rightStart, Height)];

                default:
                    return FullExtent(side);
            }
        }

        public override double ProjectToSurface(PortSide side, double alongAxisCoordinate) => 0.0;
    }

    /// <summary>
    /// Folder geometry: the top-left tab occupies part of the top bounding edge, so that portion is
    /// excluded from the connectable extent and the remaining top-face anchors project down to the
    /// body's recessed top edge.
    /// </summary>
    private sealed class FolderGeometry : BoxShapeGeometryBase
    {
        private readonly double _tabHeight;
        private readonly double _tabWidth;

        public FolderGeometry(LayoutBox box)
            : base(box)
        {
            _tabHeight = ResolveFolderTabHeight(box);
            _tabWidth = ResolveFolderTabWidth(box);
        }

        /// <summary>
        /// Resolves the folder-tab width used by routing, honoring a caller-supplied hint and
        /// otherwise falling back to the router's generic folder-tab width formula.
        /// </summary>
        /// <param name="box">Box whose folder-tab width is being resolved.</param>
        /// <returns>The non-negative folder-tab width, in logical pixels.</returns>
        private static double ResolveFolderTabWidth(LayoutBox box)
        {
            if (box.FolderTabWidth.HasValue)
            {
                return Math.Max(0.0, box.FolderTabWidth.Value);
            }

            return Math.Min(
                box.Width * NotationMetrics.FolderTabMaxWidthFraction,
                Math.Max(
                    NotationMetrics.FolderTabMinWidth,
                    (box.Label?.Length ?? 4) * Themes.Light.FontSizeBody *
                    NotationMetrics.FolderLabelCharWidthFactor +
                    (2.0 * Themes.Light.LabelPadding)));
        }

        /// <summary>
        /// Resolves the folder-tab height used by routing, honoring a caller-supplied hint and
        /// otherwise falling back to the router's generic folder-tab height formula.
        /// </summary>
        /// <param name="box">Box whose folder-tab height is being resolved.</param>
        /// <returns>The non-negative folder-tab height, in logical pixels.</returns>
        private static double ResolveFolderTabHeight(LayoutBox box) =>
            box.FolderTabHeight.HasValue
                ? Math.Max(0.0, box.FolderTabHeight.Value)
                : BoxMetrics.FolderTabHeight(Themes.Light);

        public override IReadOnlyList<(double Lo, double Hi)> GetConnectableExtents(PortSide side)
        {
            if (side is not PortSide.Top)
            {
                return FullExtent(side);
            }

            if (_tabWidth <= 0.0 || _tabHeight <= 0.0)
            {
                return FullExtent(side);
            }

            var topStart = Math.Min(Width, _tabWidth + FolderTopFaceMargin);
            return topStart >= Width
                ? []
                : [(topStart, Width)];
        }

        public override double ProjectToSurface(PortSide side, double alongAxisCoordinate) =>
            side is PortSide.Top ? _tabHeight : 0.0;
    }

    /// <summary>
    /// Resolves the shape geometry object used to interpret a box's connectable face extents and real
    /// outline projection.
    /// </summary>
    private static IBoxShapeGeometry ResolveShapeGeometry(LayoutBox box) => box.Shape switch
    {
        BoxShape.Folder => new FolderGeometry(box),
        BoxShape.RoundedRectangle => new RoundedRectangleGeometry(box),
        BoxShape.Note => new NoteGeometry(box),
        _ => new RectangleGeometry(box),
    };

    /// <summary>
    /// Returns the local face coordinate to use on <paramref name="side"/> after clamping the naive
    /// along-face position to the nearest connectable shape extent and, when possible, insetting that
    /// extent by <paramref name="clearance"/> at both ends.
    /// </summary>
    private static double ClampToConnectableExtent(LayoutBox box, PortSide side, double alongAxisCoordinate, double clearance)
    {
        var geometry = ResolveShapeGeometry(box);
        var extents = BuildUsableExtents(geometry.GetConnectableExtents(side), clearance);
        if (extents.Count == 0)
        {
            return FaceLength(box, side) / 2.0;
        }

        var best = alongAxisCoordinate;
        var bestDistance = double.PositiveInfinity;
        foreach (var (lo, hi) in extents)
        {
            var clamped = Math.Clamp(alongAxisCoordinate, lo, hi);
            var distance = Math.Abs(clamped - alongAxisCoordinate);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = clamped;
            }
        }

        return best;
    }

    /// <summary>
    /// Resolves the final anchor point on a box: clamp the local face coordinate into the nearest
    /// connectable extent (respecting the requested end clearance whenever that extent is long enough),
    /// then project it inward from the bounding box to the real drawn outline.
    /// </summary>
    private static Point2D ResolveAnchorPoint(LayoutBox box, PortSide side, double alongAxisCoordinate, double clearance)
    {
        var geometry = ResolveShapeGeometry(box);
        var clamped = ClampToConnectableExtent(box, side, alongAxisCoordinate, clearance);
        var offset = Math.Max(0.0, geometry.ProjectToSurface(side, clamped));

        return side switch
        {
            PortSide.Left => new Point2D(box.X + offset, box.Y + clamped),
            PortSide.Right => new Point2D(box.X + box.Width - offset, box.Y + clamped),
            PortSide.Top => new Point2D(box.X + clamped, box.Y + offset),
            _ => new Point2D(box.X + clamped, box.Y + box.Height - offset),
        };
    }

    /// <summary>
    /// Returns the local length of a box face.
    /// </summary>
    private static double FaceLength(LayoutBox box, PortSide side) =>
        side is PortSide.Left or PortSide.Right ? box.Height : box.Width;

    /// <summary>
    /// Returns the faces to try for one endpoint, ordered from the natural face chosen by box
    /// separation, then the adjacent face that still points most toward the other box on the minor
    /// axis, then the other adjacent face, and finally the opposite face as a last resort.
    /// </summary>
    private static IReadOnlyList<PortSide> PreferredFaceOrder(LayoutBox box, LayoutBox other, PortSide naturalSide)
    {
        var boxCentreX = box.X + (box.Width / 2.0);
        var boxCentreY = box.Y + (box.Height / 2.0);
        var otherCentreX = other.X + (other.Width / 2.0);
        var otherCentreY = other.Y + (other.Height / 2.0);

        return naturalSide switch
        {
            PortSide.Left => otherCentreY <= boxCentreY
                ? [PortSide.Left, PortSide.Top, PortSide.Bottom, PortSide.Right]
                : [PortSide.Left, PortSide.Bottom, PortSide.Top, PortSide.Right],
            PortSide.Right => otherCentreY <= boxCentreY
                ? [PortSide.Right, PortSide.Top, PortSide.Bottom, PortSide.Left]
                : [PortSide.Right, PortSide.Bottom, PortSide.Top, PortSide.Left],
            PortSide.Top => otherCentreX <= boxCentreX
                ? [PortSide.Top, PortSide.Left, PortSide.Right, PortSide.Bottom]
                : [PortSide.Top, PortSide.Right, PortSide.Left, PortSide.Bottom],
            _ => otherCentreX <= boxCentreX
                ? [PortSide.Bottom, PortSide.Left, PortSide.Right, PortSide.Top]
                : [PortSide.Bottom, PortSide.Right, PortSide.Left, PortSide.Top],
        };
    }

    /// <summary>
    /// Chooses the first usable face from <see cref="PreferredFaceOrder"/>; when every face reports an
    /// empty extent, the natural face is returned so routing still has a deterministic fallback.
    /// </summary>
    private static PortSide ChooseUsableFace(LayoutBox box, LayoutBox other, PortSide naturalSide)
    {
        var geometry = ResolveShapeGeometry(box);
        foreach (var side in PreferredFaceOrder(box, other, naturalSide))
        {
            if (geometry.GetConnectableExtents(side).Count > 0)
            {
                return side;
            }
        }

        return naturalSide;
    }

    /// <summary>
    /// Converts an absolute axis coordinate on one of <paramref name="box"/>'s faces into the owning
    /// face's local coordinate system.
    /// </summary>
    private static double ToLocalAlong(LayoutBox box, PortSide side, double absoluteCoordinate) =>
        side is PortSide.Left or PortSide.Right
            ? absoluteCoordinate - box.Y
            : absoluteCoordinate - box.X;

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
    /// relative placement. The natural side is the face on the axis along which the boxes are more
    /// separated; when that face has no connectable extent, the router falls back to the adjacent face
    /// that still points most toward the other box on the minor axis, then the other adjacent face, and
    /// finally the opposite face. The along-face coordinate is still aligned to the overlap of the two
    /// boxes on that face axis when they overlap, or to this box's own face centre when they do not,
    /// but is also kept at least <paramref name="clearance"/> away from the ends of any face long
    /// enough to allow that margin before any shape-specific clamping is applied.
    /// </summary>
    /// <param name="from">The source box.</param>
    /// <param name="to">The target box.</param>
    /// <param name="clearance">
    /// Minimum inset to keep from the ends of a sufficiently long face before the coordinate is
    /// clamped to any shape-specific connectable extent.
    /// </param>
    /// <returns>
    /// The source and target local face coordinates, together with the chosen source and target sides.
    /// </returns>
    private static (double SourceAlong, PortSide SourceSide, double TargetAlong, PortSide TargetSide) FacingAnchors(
        LayoutBox from, LayoutBox to, double clearance)
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
            var naturalFromSide = fromIsLeft ? PortSide.Right : PortSide.Left;
            var naturalToSide = fromIsLeft ? PortSide.Left : PortSide.Right;
            var sourceSide = ChooseUsableFace(from, to, naturalFromSide);
            var targetSide = ChooseUsableFace(to, from, naturalToSide);
            var sourceCoordinate = NaiveAnchorCoordinate(from, to, sourceSide, clearance);
            var targetCoordinate = NaiveAnchorCoordinate(to, from, targetSide, clearance);
            return (ToLocalAlong(from, sourceSide, sourceCoordinate), sourceSide, ToLocalAlong(to, targetSide, targetCoordinate), targetSide);
        }

        // Top/bottom relationship: the upper box anchors its bottom face, the lower box its top face.
        var fromIsAbove = from.Y + (from.Height / 2.0) <= to.Y + (to.Height / 2.0);
        var naturalSourceSide = fromIsAbove ? PortSide.Bottom : PortSide.Top;
        var naturalTargetSide = fromIsAbove ? PortSide.Top : PortSide.Bottom;
        var sourceTopBottomSide = ChooseUsableFace(from, to, naturalSourceSide);
        var targetTopBottomSide = ChooseUsableFace(to, from, naturalTargetSide);
        var sourceCoordinateOnFallback = NaiveAnchorCoordinate(from, to, sourceTopBottomSide, clearance);
        var targetCoordinateOnFallback = NaiveAnchorCoordinate(to, from, targetTopBottomSide, clearance);
        return (ToLocalAlong(from, sourceTopBottomSide, sourceCoordinateOnFallback), sourceTopBottomSide, ToLocalAlong(to, targetTopBottomSide, targetCoordinateOnFallback), targetTopBottomSide);
    }

    /// <summary>
    /// Returns the naive absolute coordinate on <paramref name="box"/>'s chosen face before shape
    /// extents are applied: the overlap centre on the face axis when the two boxes overlap there, or
    /// this box's own face centre when they do not, with an added inward clamp that keeps sufficiently
    /// long faces away from their corners by <paramref name="clearance"/>.
    /// </summary>
    private static double NaiveAnchorCoordinate(LayoutBox box, LayoutBox other, PortSide side, double clearance) =>
        side is PortSide.Left or PortSide.Right
            ? AnchorCoordinate(box.Y, box.Y + box.Height, other.Y, other.Y + other.Height, clearance)
            : AnchorCoordinate(box.X, box.X + box.Width, other.X, other.X + other.Width, clearance);

    /// <summary>
    /// Returns the anchor coordinate for a box face spanning [<paramref name="lo"/>, <paramref name="hi"/>]
    /// against the facing box's span [<paramref name="otherLo"/>, <paramref name="otherHi"/>] on the
    /// same axis. The starting point is the centre of the two spans' overlap when they overlap
    /// (keeping both ends aligned on a single coordinate for a short, straight hop across the shared
    /// span), or this box's own centre when they do not. When the face is longer than twice
    /// <paramref name="clearance"/>, that coordinate is then clamped inward so it stays at least that
    /// far from either end of the face; otherwise the face centre is used directly instead of
    /// violating the requested margin.
    /// </summary>
    private static double AnchorCoordinate(double lo, double hi, double otherLo, double otherHi, double clearance)
    {
        var faceCentre = (lo + hi) / 2.0;
        var inset = Math.Max(0.0, clearance);
        if ((hi - lo) <= 2.0 * inset)
        {
            return faceCentre;
        }

        var overlapLo = Math.Max(lo, otherLo);
        var overlapHi = Math.Min(hi, otherHi);
        var coordinate = overlapLo <= overlapHi ? (overlapLo + overlapHi) / 2.0 : faceCentre;
        return Math.Clamp(coordinate, lo + inset, hi - inset);
    }
}
