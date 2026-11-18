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
/// The source box, used to resolve the connector's departure anchor and face; pass the exact
/// <see cref="LayoutBox"/> reference that appears in the box list. It still counts as a hard obstacle
/// for the rest of the route — the router steps the anchor off this box's face before obstacle
/// avoidance begins, so the connector can still leave cleanly.
/// </param>
/// <param name="To">
/// The target box, used to resolve the connector's arrival anchor and face; the connector's target end
/// marker is drawn where the route meets this box. It still counts as a hard obstacle for the rest of
/// the route — the router steps the anchor off this box's face before obstacle avoidance begins, so the
/// connector can still arrive cleanly.
/// </param>
/// <param name="TargetEnd">End-marker style drawn at the target (arrival) end of the connector.</param>
/// <param name="LineStyle">Stroke style (solid, dashed, dotted) applied to the routed connector.</param>
/// <param name="Label">Optional midpoint label carried onto the routed line; <see langword="null"/> for an unlabelled connector.</param>
/// <param name="SourceEndLabel">Optional end labels (role name, multiplicity) carried onto the routed line's source end; <see langword="null"/> for none.</param>
/// <param name="TargetEndLabel">Optional end labels carried onto the routed line's target end; <see langword="null"/> for none.</param>
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
    string? Label = null,
    LineEndLabel? SourceEndLabel = null,
    LineEndLabel? TargetEndLabel = null);

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
/// Every box on the canvas, including a connection's own two endpoints, becomes a <see cref="Rect"/>
/// obstacle the route steers around by <see cref="ConnectorRouteOptions.Clearance"/>; a connector still
/// leaves and enters the boxes it joins cleanly because each anchor is stepped off its box with a
/// perpendicular stub longer than the clearance before the obstacle-avoiding search begins.
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
/// A connection's endpoints may be boxes nested inside container boxes (their coordinates are absolute
/// like every other box). The public overloads treat every box passed in as a hard obstacle; an
/// internal batch entry point lets the hierarchical engine give each connector its own obstacle set,
/// with that connector's endpoint-ancestor containers expanded into their title bands and children so
/// the connector can cross those containers' walls without crossing any unrelated box.
/// </para>
/// <para>
/// A first route is computed from those anchors exactly as above. When it is defective (a jogged route
/// between two facing faces; in the cross-hierarchy profile also a detour, a short leg, a wall-hugging
/// run or a crossing of another connector's end zone) the connection is searched again with the ports
/// left to the router: each port may slide along its face (never into a corner, never onto a part of
/// the face the shape forbids, and never crowding or hopping over another connector's port on that face)
/// so a lane that fits both faces becomes one straight connector, and a jog that cannot be avoided keeps
/// a first leg of at least the connector stub and a final leg of at least the end marker plus the bend
/// radius. The cross-hierarchy profile additionally keeps container walls clear, tries the adjacent faces
/// and keeps clear of the other connectors' end zones. Connectors that need no search are unchanged.
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
    /// All placed boxes on the canvas, including each connection's own two endpoints. Every box acts
    /// as a hard obstacle for the routes; a connection's own endpoints still permit a clean
    /// leave/enter because anchors are stepped off their box before obstacle avoidance begins.
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
        return RouteBatch(connections, options, _ => boxes);
    }

    /// <summary>
    /// Routes a single <paramref name="connection"/> among the placed <paramref name="boxes"/>.
    /// </summary>
    /// <param name="boxes">
    /// All placed boxes on the canvas, including the connection's own two endpoints. Every box acts as
    /// a hard obstacle for this route; the connection's own endpoints still permit a clean leave/enter
    /// because anchors are stepped off their box before obstacle avoidance begins.
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

        // A single connection is routed as a one-element batch so it shares the batch's anchor choice,
        // port sliding and end-leg stages exactly: with nothing else to share a face with, the batch
        // path degenerates to the facing anchors of this one pair of boxes, so the result is the same as
        // routing the connection independently.
        return RouteBatch([connection], options, _ => boxes)[0];
    }

    /// <summary>
    /// Routes every connection in <paramref name="connections"/> in one batch, exactly like the public
    /// batch <see cref="Route(IReadOnlyList{LayoutBox},IReadOnlyList{Connection},ConnectorRouteOptions)"/>
    /// overload, except that each connection is routed against its own hard-obstacle set supplied by
    /// <paramref name="obstaclesFor"/> rather than one shared box list. The hierarchical engine uses this
    /// to route a connector whose endpoints are nested inside containers: the endpoints' ancestor
    /// containers are replaced, for that connector only, by their title bands and children, so the
    /// connector may cross those containers' walls while every unrelated box stays a hard obstacle.
    /// </summary>
    /// <param name="connections">The connectors to route; endpoints may be nested boxes.</param>
    /// <param name="options">Routing options, including the routing style and obstacle clearance.</param>
    /// <param name="obstaclesFor">
    /// Returns the hard-obstacle boxes for the connection at the given index (its own endpoints included).
    /// </param>
    /// <param name="fixedAnchors">
    /// Anchor points already occupied on box faces by connectors routed elsewhere (for example the
    /// internal connectors of a container whose nested box is one of this batch's endpoints). A batch
    /// anchor that would land too close to one is moved along its face to a free spot. May be
    /// <see langword="null"/> or empty.
    /// </param>
    /// <param name="penalizeLongEndSegments">
    /// When <see langword="true"/>, the long runs of an already-routed connector's first and last
    /// segments (beyond a short approach corridor next to each endpoint) are also treated as soft
    /// obstacles for the connectors routed after it, so connectors with nested endpoints do not run
    /// collinearly along one another's final approach.
    /// </param>
    /// <param name="profile">
    /// Optional cross-hierarchy routing profile (container wall keep-out strips). When supplied, a
    /// connector whose first route is defective also has its faces re-chosen, keeps comfort legs at both
    /// ends, avoids the end zones of the other connectors, and prefers not to cross them.
    /// </param>
    /// <returns>One routed <see cref="LayoutLine"/> per connection, in input order.</returns>
    internal static IReadOnlyList<LayoutLine> RouteBatch(
        IReadOnlyList<Connection> connections,
        ConnectorRouteOptions options,
        Func<int, IReadOnlyList<LayoutBox>> obstaclesFor,
        IReadOnlyList<FixedAnchor>? fixedAnchors = null,
        bool penalizeLongEndSegments = false,
        RoutingProfile? profile = null)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(obstaclesFor);

        var lines = RouteBatchCore(connections, options, obstaclesFor, fixedAnchors, penalizeLongEndSegments, profile, null);

        // A batch whose connectors run squeezed beside one another is routed once more with every routed
        // line a thicker obstacle (so the later connectors keep a full lane's width from it); the second
        // result replaces the first only when it is no worse in crossings, hairpins and bends and has
        // fewer close parallel runs.
        if (profile is null || connections.Count < 2)
        {
            return lines;
        }

        var first = BatchQuality(lines);
        if (first.Summary.ParallelLength <= 0.0)
        {
            return lines;
        }

        var wide = RouteBatchCore(connections, options, obstaclesFor, fixedAnchors, penalizeLongEndSegments, profile, LaneHalfThickness);
        var second = BatchQuality(wide);
        return second.Crossings <= first.Crossings
            && second.Summary.Hairpins <= first.Summary.Hairpins
            && second.Summary.Bends <= first.Summary.Bends
            && second.Summary.ParallelLength < first.Summary.ParallelLength
            ? wide
            : lines;
    }

    /// <summary>
    /// The half thickness of the line obstacles of a batch routed with full lane width: with the router's
    /// obstacle clearance on top it keeps a later connector at least <see cref="RouteQuality.CloseParallelGap"/>
    /// from an earlier one.
    /// </summary>
    private const double LaneHalfThickness = 4.0;

    /// <summary>Returns the crossings among, and the route quality of, a batch of routed lines.</summary>
    private static (int Crossings, RouteQuality.Summary Summary) BatchQuality(IReadOnlyList<LayoutLine> lines)
    {
        var crossings = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            crossings += CountCrossedLines(lines[i].Waypoints, [.. lines.Skip(i + 1)]);
        }

        return (crossings, RouteQuality.Measure([.. lines.Select(line => line.Waypoints)]));
    }

    /// <summary>
    /// Routes one batch (see <see cref="RouteBatch"/>), every routed line an obstacle of
    /// <paramref name="laneHalfThickness"/> (hairline when <see langword="null"/>) for the connectors
    /// routed after it.
    /// </summary>
    private static List<LayoutLine> RouteBatchCore(
        IReadOnlyList<Connection> connections,
        ConnectorRouteOptions options,
        Func<int, IReadOnlyList<LayoutBox>> obstaclesFor,
        IReadOnlyList<FixedAnchor>? fixedAnchors,
        bool penalizeLongEndSegments,
        RoutingProfile? profile,
        double? laneHalfThickness)
    {
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

        var occupied = fixedAnchors ?? [];
        DistributeSharedFaceAnchors(connections, anchors, options.Clearance, occupied);

        // The end zones of every connector (the stretch just outside each end's face) are reserved
        // against the other connectors in the cross-hierarchy profile, so no connector crosses another's
        // arrowhead approach or first leg.
        var endZones = new Rect[connections.Count][];
        if (profile is not null)
        {
            for (var i = 0; i < connections.Count; i++)
            {
                endZones[i] = EndZones(connections[i], anchors[i], options);
            }
        }

        // Route one connector at a time, growing a soft-obstacle set with each already-routed line (as
        // a thin rectangle per segment) so later connectors prefer a free lane over one already used by
        // an earlier connector. These are soft (cost-penalized), not hard, obstacles: several connectors
        // converging on the same box face legitimately share their final short approach corridor, and
        // hard-blocking it would make that shared face unreachable for every connector after the first
        // (see AddLineObstacles remarks).
        var lines = new List<LayoutLine>(connections.Count);
        var routedLineObstacles = new List<Rect>();
        for (var index = 0; index < connections.Count; index++)
        {
            var line = RouteConnection(
                index, connections, options, obstaclesFor(index), anchors, occupied, routedLineObstacles, profile, endZones);
            lines.Add(line);
            AddLineObstacles(line.Waypoints, routedLineObstacles, penalizeLongEndSegments ? options.Clearance : 0.0, laneHalfThickness);
        }

        // The lines that already exist around a connection's endpoints (interior connectors of the
        // containers it enters) were not known while the batch was routed, so a connector may have been
        // routed straight across one; those connectors get a second look now that every line is known.
        // A connector routed before the lines that now surround it may also have ended up with a hairpin,
        // many bends, or a run squeezed beside another connector; those get a second look as well.
        if (profile is not null)
        {
            var existingLinesFor = profile.ExistingLinesFor;
            var endInset = penalizeLongEndSegments ? options.Clearance : 0.0;
            if (existingLinesFor is not null)
            {
                RerouteBatch(connections, options, obstaclesFor, anchors, occupied, profile, endZones, lines, existingLinesFor, endInset, tidy: false);
            }

            RerouteBatch(connections, options, obstaclesFor, anchors, occupied, profile, endZones, lines, existingLinesFor ?? (_ => []), endInset, tidy: true);
        }

        return lines;
    }

    /// <summary>
    /// Re-routes, one at a time, the connectors of an already-routed batch that a second look can improve,
    /// searching each again over the finished batch: every other connector of the batch and the nearby
    /// existing lines (see <see cref="RoutingProfile.ExistingLinesFor"/>) are costs, so the new route is
    /// judged against the lines as they really are. Every other connector stays as it was.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Without <paramref name="tidy"/> the pass looks at each connector that crosses a line existing
    ///     around its endpoints and keeps the new route only when it crosses strictly fewer lines, so the
    ///     pass can only reduce the crossings of the batch.
    ///     </para>
    ///     <para>
    ///     With <paramref name="tidy"/> it looks at each connector with a hairpin, many bends, or a run
    ///     squeezed beside another connector, and keeps the new route when it crosses no more lines.
    ///     </para>
    ///     <para>
    ///     Either way a new route replaces the old one only when its visual cost (see
    ///     <see cref="IsTidyReplacement"/>: length, bends, hairpins, close parallel runs, and the price of
    ///     every crossing) is lower, so a crossing is never traded for a long wrap around a container.
    ///     </para>
    /// </remarks>
    private static void RerouteBatch(
        IReadOnlyList<Connection> connections,
        ConnectorRouteOptions options,
        Func<int, IReadOnlyList<LayoutBox>> obstaclesFor,
        FaceAnchors[] anchors,
        IReadOnlyList<FixedAnchor> occupied,
        RoutingProfile profile,
        Rect[][] endZones,
        List<LayoutLine> lines,
        Func<int, IReadOnlyList<LayoutLine>> existingLinesFor,
        double endInset,
        bool tidy)
    {
        var reach = ExistingLineReachFactor * options.Clearance;
        for (var index = 0; index < lines.Count; index++)
        {
            var existing = existingLinesFor(index);
            var current = lines[index].Waypoints;
            if (!tidy && (existing.Count == 0 || CountCrossedLines(current, existing, limit: 1) == 0))
            {
                continue;
            }

            // Every other connector of the batch, as soft obstacles for the search (only those near the
            // current route, to keep the search's grid small) and as lines to count crossings against.
            var others = new List<Rect>();
            var otherLines = new List<LayoutLine>(lines.Count);
            for (var j = 0; j < lines.Count; j++)
            {
                if (j != index)
                {
                    AddLineObstacles(lines[j].Waypoints, others, endInset, tidy ? LaneHalfThickness : null);
                    otherLines.Add(lines[j]);
                }
            }

            List<LayoutLine> against = [.. existing, .. otherLines];
            if (tidy && RouteQuality.HairpinCount(current) == 0 && RouteQuality.BendCount(current) < TidyBendThreshold
                && !against.Any(line => RouteQuality.CloseParallelLength(current, line.Waypoints) > 0.0))
            {
                continue;
            }

            var existingRects = ExistingLineRects(LinesNearPath(existing, current, reach));
            List<Rect> soft = [.. others.Where(r => IsRectNearPath(r, current, reach)), .. existingRects];
            var refine = new RefineContext(existingRects, [.. others, .. existingRects], tidy);
            var better = SearchBetterRoute(
                index, connections, options, obstaclesFor(index), anchors, occupied, soft, profile, profile.WallsFor(index), endZones,
                new RouteResult(current, false), refine);

            // The search only ranked rectangles near the current route; confirm with the exact lines that
            // the new route really crosses fewer of them (or no more, when tidying) than the one it replaces,
            // and that it does not buy that with a visibly worse route (a long detour, extra bends, a
            // hairpin, a run squeezed beside another connector).
            if (better is not { } accepted)
            {
                continue;
            }

            var currentCrossings = CountCrossedLines(current, existing) + CountCrossedLines(current, otherLines);
            var acceptedCrossings = CountCrossedLines(accepted.Waypoints, existing) + CountCrossedLines(accepted.Waypoints, otherLines);
            if (tidy ? acceptedCrossings > currentCrossings : acceptedCrossings >= currentCrossings)
            {
                continue;
            }

            if (!IsTidyReplacement(current, accepted.Waypoints, currentCrossings, acceptedCrossings, against, options.Clearance))
            {
                continue;
            }

            lines[index] = lines[index] with { Waypoints = accepted.Waypoints };
            anchors[index] = accepted.Anchors;
            endZones[index] = EndZonesOfRoute(connections[index], accepted.Anchors, accepted.Waypoints, options);
        }
    }

    /// <summary>A route with at least this many bends (or a hairpin, or a run closer than <see cref="RouteQuality.CloseParallelGap"/> to another connector) is looked at again by the tidying pass.</summary>
    private const int TidyBendThreshold = 6;

    /// <summary>
    /// What the re-routing pass hands the route search: the rectangles of the existing lines
    /// near the route (which also earn a crossing penalty in the search), and every rectangle crossings are
    /// counted against (the existing lines plus every other connector of the batch).
    /// </summary>
    /// <param name="ExistingRects">The rectangles of the nearby existing lines.</param>
    /// <param name="CrossingRects">Every rectangle a candidate's crossings are counted against.</param>
    /// <param name="AllowEqualCrossings">
    /// Whether a candidate that crosses as many lines as the route it would replace is acceptable (the
    /// tidying pass), instead of only one that crosses fewer.
    /// </param>
    private sealed record RefineContext(IReadOnlyList<Rect> ExistingRects, IReadOnlyList<Rect> CrossingRects, bool AllowEqualCrossings = false);

    /// <summary>
    /// Builds the thin soft-obstacle rectangles of <paramref name="existingLines"/> (see
    /// <see cref="RoutingProfile.ExistingLinesFor"/>). Every segment counts, including the end runs and
    /// all but the last <see cref="ExistingLineEndInset"/> pixels beside each end, so a route cannot slip
    /// across such a line next to the point it ends on.
    /// </summary>
    /// <param name="existingLines">The lines that already exist around a connection.</param>
    /// <returns>The rectangles, empty when there is no line.</returns>
    private static List<Rect> ExistingLineRects(IReadOnlyList<LayoutLine> existingLines)
    {
        var rects = new List<Rect>();
        foreach (var existingLine in existingLines)
        {
            AddLineObstacles(existingLine.Waypoints, rects, ExistingLineEndInset);
        }

        return rects;
    }

    /// <summary>
    /// Counts the transversal crossings between the path <paramref name="w"/> and <paramref name="lines"/>
    /// (a horizontal segment through a vertical one, strictly inside both). Used as a cheap pre-test on
    /// every connection that has lines around its endpoints — so only one that really crosses a line pays
    /// for the soft-obstacle search — and as the exact final check of a searched route.
    /// </summary>
    /// <param name="w">The path.</param>
    /// <param name="lines">The lines to cross-check against.</param>
    /// <param name="limit">Stops counting once this many crossings are found (for a mere "any" test pass 1).</param>
    private static int CountCrossedLines(IReadOnlyList<Point2D> w, IReadOnlyList<LayoutLine> lines, int limit = int.MaxValue)
    {
        const double tolerance = 1e-6;
        var count = 0;
        foreach (var o in lines.Select(line => line.Waypoints))
        {
            for (var j = 0; j < o.Count - 1; j++)
            {
                var oHorizontal = Math.Abs(o[j].Y - o[j + 1].Y) < tolerance;
                var oVertical = Math.Abs(o[j].X - o[j + 1].X) < tolerance;
                for (var i = 0; i < w.Count - 1; i++)
                {
                    var wHorizontal = Math.Abs(w[i].Y - w[i + 1].Y) < tolerance;
                    var wVertical = Math.Abs(w[i].X - w[i + 1].X) < tolerance;
                    if (!((wHorizontal && oVertical) || (wVertical && oHorizontal)))
                    {
                        continue;
                    }

                    var (hA, hB, vA, vB) = wHorizontal ? (w[i], w[i + 1], o[j], o[j + 1]) : (o[j], o[j + 1], w[i], w[i + 1]);
                    if (vA.X > Math.Min(hA.X, hB.X) + tolerance && vA.X < Math.Max(hA.X, hB.X) - tolerance
                        && hA.Y > Math.Min(vA.Y, vB.Y) + tolerance && hA.Y < Math.Max(vA.Y, vB.Y) - tolerance
                        && ++count >= limit)
                    {
                        return count;
                    }
                }
            }
        }

        return count;
    }

    /// <summary>
    /// The price, in logical pixels of connector length, of one more bend (see <see cref="RouteCost"/>).
    /// </summary>
    private const double BendCost = 40.0;

    /// <summary>The price, in logical pixels of connector length, of one more hairpin (a leg running back against its own direction).</summary>
    private const double HairpinCost = 80.0;

    /// <summary>The price of one logical pixel of run closer than <see cref="RouteQuality.CloseParallelGap"/> to another connector.</summary>
    private const double CloseParallelCost = 4.0;

    /// <summary>
    /// A run closer than this, in logical pixels, to a run of another connector (without lying on it) cannot
    /// be told apart from it: a replacement route never introduces one.
    /// </summary>
    private const double NearlyCoincidentGap = 4.0;

    /// <summary>
    /// How much longer than the route it replaces a connector may become, as a factor (plus two clearances),
    /// when the replacement removes a crossing: a crossing is worth a modest detour, never a wrap around a box.
    /// </summary>
    private const double MaxDetourFactor = 1.25;

    /// <summary>How many more bends than the route it replaces a connector may have when the replacement removes a crossing.</summary>
    private const int MaxExtraBends = 2;

    /// <summary>
    /// Returns whether <paramref name="candidate"/> is a tidy replacement of <paramref name="current"/>. It
    /// never introduces a run nearly on top of another connector and never adds a hairpin. When it crosses
    /// fewer lines it must also be no more than <see cref="MaxDetourFactor"/> times as long (plus two
    /// clearances) with at most <see cref="MaxExtraBends"/> more bends; when it crosses as many it must have a
    /// lower visual cost: length, bends, hairpins and closeness-weighted parallel runs.
    /// </summary>
    internal static bool IsTidyReplacement(
        IReadOnlyList<Point2D> current,
        IReadOnlyList<Point2D> candidate,
        int currentCrossings,
        int candidateCrossings,
        IReadOnlyList<LayoutLine> others,
        double clearance)
    {
        if (others.Sum(line => RouteQuality.CloseParallelLength(candidate, line.Waypoints, NearlyCoincidentGap))
                > others.Sum(line => RouteQuality.CloseParallelLength(current, line.Waypoints, NearlyCoincidentGap))
            || RouteQuality.HairpinCount(candidate) > RouteQuality.HairpinCount(current))
        {
            return false;
        }

        return candidateCrossings < currentCrossings
            ? RouteQuality.PathLength(candidate) <= (MaxDetourFactor * RouteQuality.PathLength(current)) + (2.0 * clearance)
                && RouteQuality.BendCount(candidate) <= RouteQuality.BendCount(current) + MaxExtraBends
            : RouteQuality.BendCount(candidate) <= RouteQuality.BendCount(current) && RouteCost(candidate, others) < RouteCost(current, others);
    }

    /// <summary>Returns the visual cost of a route: length plus the price of its bends, hairpins and close parallel runs.</summary>
    private static double RouteCost(IReadOnlyList<Point2D> w, IReadOnlyList<LayoutLine> others) =>
        RouteQuality.PathLength(w)
        + (RouteQuality.BendCount(w) * BendCost)
        + (RouteQuality.HairpinCount(w) * HairpinCost)
        + (others.Sum(line => RouteQuality.WeightedCloseParallelLength(w, line.Waypoints)) * CloseParallelCost);

    /// <summary>
    /// Returns the lines of <paramref name="lines"/> that come within <paramref name="reach"/> logical
    /// pixels of the path <paramref name="w"/> (the ones it crosses, and the ones running beside it). Only
    /// these are worth the cost of being soft obstacles of the route search: an alternative to the path
    /// stays near it, and the exact final check against every line guards the rest.
    /// </summary>
    private static List<LayoutLine> LinesNearPath(IReadOnlyList<LayoutLine> lines, IReadOnlyList<Point2D> w, double reach) =>
        [.. lines.Where(line => IsNearPath(line.Waypoints, w, reach))];

    /// <summary>Returns whether <paramref name="line"/> lies within <paramref name="reach"/> of any segment of <paramref name="w"/>.</summary>
    private static bool IsNearPath(IReadOnlyList<Point2D> line, IReadOnlyList<Point2D> w, double reach)
    {
        for (var j = 0; j < line.Count - 1; j++)
        {
            if (IsBoundsNearPath(
                    Math.Min(line[j].X, line[j + 1].X),
                    Math.Min(line[j].Y, line[j + 1].Y),
                    Math.Max(line[j].X, line[j + 1].X),
                    Math.Max(line[j].Y, line[j + 1].Y),
                    w,
                    reach))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the rectangle <paramref name="r"/> lies within <paramref name="reach"/> of any segment of <paramref name="w"/>.</summary>
    private static bool IsRectNearPath(Rect r, IReadOnlyList<Point2D> w, double reach) =>
        IsBoundsNearPath(r.X, r.Y, r.X + r.Width, r.Y + r.Height, w, reach);

    /// <summary>Returns whether the box (<paramref name="loX"/>, <paramref name="loY"/>)–(<paramref name="hiX"/>, <paramref name="hiY"/>) inflated by <paramref name="reach"/> meets any segment of <paramref name="w"/>.</summary>
    private static bool IsBoundsNearPath(double loX, double loY, double hiX, double hiY, IReadOnlyList<Point2D> w, double reach)
    {
        for (var i = 0; i < w.Count - 1; i++)
        {
            if (Math.Min(w[i].X, w[i + 1].X) <= hiX + reach && Math.Max(w[i].X, w[i + 1].X) >= loX - reach
                && Math.Min(w[i].Y, w[i + 1].Y) <= hiY + reach && Math.Max(w[i].Y, w[i + 1].Y) >= loY - reach)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Counts the transversal crossings between the path <paramref name="w"/> and the thin line
    /// rectangles in <paramref name="lines"/> (as built by <see cref="AddLineObstacles"/>): a horizontal
    /// segment crossing a vertical rectangle or a vertical segment crossing a horizontal one. Segments
    /// that run along a line are not crossings.
    /// </summary>
    private static int CountLineCrossings(IReadOnlyList<Point2D> w, IReadOnlyList<Rect> lines)
    {
        var count = 0;
        for (var i = 0; i < w.Count - 1; i++)
        {
            var a = w[i];
            var b = w[i + 1];
            var horizontal = Math.Abs(a.Y - b.Y) < 1e-9;
            var lo = horizontal ? Math.Min(a.X, b.X) : Math.Min(a.Y, b.Y);
            var hi = horizontal ? Math.Max(a.X, b.X) : Math.Max(a.Y, b.Y);
            foreach (var r in lines)
            {
                if (horizontal)
                {
                    if (r.Height > r.Width && r.Y < a.Y && a.Y < r.Y + r.Height
                        && Math.Min(hi, r.X + r.Width) - Math.Max(lo, r.X) > 0.0)
                    {
                        count++;
                    }
                }
                else if (r.Width > r.Height && r.X < a.X && a.X < r.X + r.Width
                    && Math.Min(hi, r.Y + r.Height) - Math.Max(lo, r.Y) > 0.0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Routes the connection at <paramref name="index"/> between its chosen anchors and, when that first
    /// route is defective (a jog that sliding the ports along their faces would remove, or — in the
    /// cross-hierarchy profile — a short leg, a wall-hugging run, a detour, or a crossing of another
    /// connector's end zone), searches for a better one by letting the router choose the ports itself and
    /// try alternative faces. The accepted ports are written back to <paramref name="anchors"/> so the
    /// connectors routed after this one see the occupied faces.
    /// </summary>
    private static LayoutLine RouteConnection(
        int index,
        IReadOnlyList<Connection> connections,
        ConnectorRouteOptions options,
        IReadOnlyList<LayoutBox> boxes,
        FaceAnchors[] anchors,
        IReadOnlyList<FixedAnchor> occupied,
        IReadOnlyList<Rect> routedLineObstacles,
        RoutingProfile? profile,
        Rect[][] endZones)
    {
        var connection = connections[index];
        var initial = RouteWithAnchors(boxes, connection, options, anchors[index], routedLineObstacles);
        var waypoints = initial.Waypoints;

        var walls = profile?.WallsFor(index);
        if (IsDefective(connection, anchors[index], initial, options, profile, walls, endZones, index))
        {
            var better = SearchBetterRoute(
                index, connections, options, boxes, anchors, occupied, routedLineObstacles, profile, walls, endZones, initial);
            if (better is { } accepted)
            {
                waypoints = accepted.Waypoints;
                anchors[index] = accepted.Anchors;
            }
        }

        if (profile is not null)
        {
            endZones[index] = EndZonesOfRoute(connection, anchors[index], waypoints, options);
        }

        return new LayoutLine(
            Waypoints: waypoints,
            SourceEnd: EndMarkerStyle.None,
            TargetEnd: connection.TargetEnd,
            LineStyle: connection.LineStyle,
            MidpointLabel: connection.Label,
            SourceEndLabel: connection.SourceEndLabel,
            TargetEndLabel: connection.TargetEndLabel);
    }

    /// <summary>
    /// Builds the obstacle set for <paramref name="connection"/> — every box on the canvas, including
    /// this connection's own two endpoints, plus any already routed <paramref name="extraSoftObstacles"/>
    /// treated as soft (cost-penalized, never hard-blocking) obstacles — and routes it between the
    /// already chosen <paramref name="anchors"/>.
    /// </summary>
    private static RouteResult RouteWithAnchors(
        IReadOnlyList<LayoutBox> boxes,
        Connection connection,
        ConnectorRouteOptions options,
        FaceAnchors anchors,
        IReadOnlyList<Rect> extraSoftObstacles)
    {
        var from = connection.From;
        var to = connection.To;
        var source = ResolveAnchorPoint(from, anchors.SourceSide, anchors.SourceAlong, options.Clearance, anchors.SourceSlid);
        var target = ResolveAnchorPoint(to, anchors.TargetSide, anchors.TargetAlong, options.Clearance, anchors.TargetSlid);

        // The hard obstacle set is every box on the canvas, including this connection's own endpoints.
        // The connector still leaves and enters the boxes it joins cleanly: OrthogonalEdgeRouter steps
        // off each anchor with a perpendicular stub longer than the obstacle clearance before searching
        // for a path, so the free-space search always starts and ends safely outside its own box's
        // inflated obstacle footprint. Treating the endpoints as real obstacles for the *rest* of the
        // route is what stops a path from cutting across its own target box's interior (e.g. through a
        // compartment's content) when steering around other already-routed connectors.
        var obstacles = new List<Rect>(boxes.Count);
        foreach (var box in boxes)
        {
            obstacles.Add(new Rect(box.X, box.Y, box.Width, box.Height));
        }

        return RouteWaypoints(
            options.EdgeRouting, source, target, obstacles, options.Clearance, anchors.SourceSide, anchors.TargetSide, extraSoftObstacles);
    }

    /// <summary>
    /// Routing profile applied to connectors whose endpoints are nested inside containers (the
    /// hierarchical engine's cross-hierarchy connectors). It supplies each connector's container wall
    /// keep-out strips and switches on the stricter end-leg, face-choice, end-zone and crossing rules.
    /// </summary>
    /// <param name="WallsFor">Returns the wall keep-out strips applying to the connection at the given index.</param>
    /// <param name="ExistingLinesFor">
    /// Optional. Returns the lines that already exist in the containers enclosing the connection's two
    /// endpoints (their interior connectors, routed by the nested scopes before this batch). They are not
    /// part of the batch and never move; each is a crossing cost for the connection at the given index.
    /// The batch is routed without them first; a connector that then crosses one is searched again over the
    /// finished batch (see <see cref="RerouteBatch"/>) and replaced by a route that crosses fewer
    /// lines when one exists — a crossing is accepted only when no alternative exists.
    /// <see langword="null"/> (the default), or an empty list for a connection, leaves that connection's
    /// routing exactly as without the cost.
    /// </param>
    internal sealed record RoutingProfile(
        Func<int, IReadOnlyList<RouterWall>> WallsFor,
        Func<int, IReadOnlyList<LayoutLine>>? ExistingLinesFor = null);

    /// <summary>
    /// Smallest inset kept between a port that was slid along its face and the face's corner or
    /// connectable-extent end. It is derived from the rendered marker, not chosen: the renderers draw
    /// end markers with <c>markerUnits=strokeWidth</c>, so the widest marker (the diamond) reaches
    /// <c>StrokeWidth × (DiamondHalfWidth + StrokeWidth / 2)</c> = 7.125 px to either side of the
    /// line (an arrowhead: 6.375 px) once its own outline stroke is included, and the box border adds
    /// half its stroke width (0.75 px); rounded up that is 8 px. A slid port therefore never leaves
    /// the arrowhead overhanging a corner. Deliberately smaller than the connector clearance, which
    /// still positions not-slid (default) anchors. (A bar or circle marker is not a connector end the
    /// router places, so it needs no separate allowance.)
    /// </summary>
    private static readonly double PortCornerMargin = PortCornerMarginFor(EndMarkerStyle.None);

    /// <summary>
    /// Returns the smallest inset kept between a slid port and its face's corner for a connector end
    /// drawn with <paramref name="style"/>: the <see cref="PortCornerMargin"/> derivation applied to
    /// the wider of the legacy widest arrowhead (the diamond) and, for the circled plus, its circle
    /// radius. Every legacy style (including the bar and circle, which the router has never allowed
    /// for separately) keeps the historical 8 px; the wider circled plus gets 10 px so it never
    /// overhangs a corner.
    /// </summary>
    /// <param name="style">The end-marker style at the port.</param>
    /// <returns>The margin, in logical pixels.</returns>
    internal static double PortCornerMarginFor(EndMarkerStyle style)
    {
        var halfWidth = Math.Max(NotationMetrics.EndMarkerHalfWidth, NotationMetrics.DiamondHalfWidth);
        if (style == EndMarkerStyle.CircledPlus)
        {
            halfWidth = Math.Max(halfWidth, NotationMetrics.AcrossHalfWidth(style));
        }

        return Math.Ceiling(
            (Themes.Light.StrokeWidth * (halfWidth + (Themes.Light.StrokeWidth / 2.0)))
            + (Themes.Light.StrokeWidth / 2.0));
    }

    /// <summary>Extra cost charged against a face pair other than the natural one, to avoid gratuitous face changes.</summary>
    private const double AlternativeFacePenalty = 5.0;

    /// <summary>Cost charged for crossing an already-routed connector at right angles (cross-hierarchy profile).</summary>
    private const double ProfileCrossingPenalty = 30.0;

    /// <summary>
    /// Extra cost charged, in the existing-line aware search, for crossing a line that already exists around
    /// the connection (see <see cref="RoutingProfile.ExistingLinesFor"/>) at right angles, on top of the
    /// ordinary soft cost and <see cref="ProfileCrossingPenalty"/>: high enough that the search takes a detour
    /// of a few bends or a different port rather than cut across an interior connector of an endpoint
    /// container, while a route that cannot avoid one still crosses it.
    /// </summary>
    private const double ExistingLineCrossingPenalty = 150.0;

    /// <summary>
    /// The sliver, in logical pixels, trimmed off each end of an existing line when its crossing
    /// rectangles are built: effectively none, so the whole line (end runs included) counts — it only has
    /// to be positive for <see cref="AddLineObstacles"/> to include the end segments.
    /// </summary>
    private const double ExistingLineEndInset = 1e-3;

    /// <summary>
    /// How far, as a multiple of the obstacle clearance, an existing line may lie from a connection's
    /// first route and still be counted against when the route search ranks its candidates.
    /// </summary>
    private const double ExistingLineReachFactor = 4.0;

    /// <summary>
    /// Minimum straight leg wanted at the source end of a connector: the theme's connector stub.
    /// </summary>
    private static double RequiredStartLeg => Themes.Light.ConnectorStub;

    /// <summary>
    /// Minimum straight leg wanted at the target end of a connector before its final bend: the end
    /// marker's RENDERED along-line length (the notation length times the shared marker scale both
    /// renderers draw it at) plus the bend radius plus a small margin, so the decoration sits on a
    /// clean straight run in SVG and PNG alike.
    /// </summary>
    private static double RequiredEndLeg(EndMarkerStyle end) =>
        NotationMetrics.RenderedAlongLineLength(end, Themes.Light) + Themes.Light.LineCornerRadius + Themes.Light.CleanLegMargin;

    /// <summary>Returns the quality hints for routing <paramref name="connection"/> under the given profile.</summary>
    private static RouteHints BuildHints(
        Connection connection,
        ConnectorRouteOptions options,
        RoutingProfile? profile,
        IReadOnlyList<RouterWall>? walls)
    {
        var endLeg = RequiredEndLeg(connection.TargetEnd);
        if (profile is null)
        {
            return new RouteHints { SourceLeg = RequiredStartLeg, TargetLeg = endLeg };
        }

        var clearance = options.Clearance;
        return new RouteHints
        {
            SourceLeg = 2.0 * clearance,
            TargetLeg = Math.Max(2.0 * clearance, endLeg),
            SourceComfort = 3.0 * clearance,
            TargetComfort = 3.0 * clearance,
            Walls = walls,
            CrossingPenalty = ProfileCrossingPenalty,
            MinInteriorSegment = 2.0 * clearance,
        };
    }

    /// <summary>Returns the length of the segment starting at waypoint <paramref name="i"/>.</summary>
    private static double SegmentLength(IReadOnlyList<Point2D> waypoints, int i) =>
        Math.Abs(waypoints[i + 1].X - waypoints[i].X) + Math.Abs(waypoints[i + 1].Y - waypoints[i].Y);

    /// <summary>
    /// Returns whether <paramref name="first"/> and <paramref name="second"/> are the two faces of
    /// opposite, facing sides (right-to-left, left-to-right, bottom-to-top or top-to-bottom).
    /// </summary>
    private static bool AreOppositeFaces(PortSide first, PortSide second) =>
        (first, second) is (PortSide.Right, PortSide.Left) or (PortSide.Left, PortSide.Right)
            or (PortSide.Bottom, PortSide.Top) or (PortSide.Top, PortSide.Bottom);

    /// <summary>
    /// Decides whether a first route is worth searching a better port/face combination for. Outside the
    /// cross-hierarchy profile only a jogged route between two facing, opposite faces qualifies (it may be
    /// straightened by sliding the ports). In the profile, any route with three or more bends, a leg
    /// shorter than the comfort minimum, a tiny interior jog, a run along a container wall, a crossing of
    /// another connector's end zone, or an obstacle-crossing fallback qualifies.
    /// </summary>
    private static bool IsDefective(
        Connection connection,
        FaceAnchors anchors,
        RouteResult route,
        ConnectorRouteOptions options,
        RoutingProfile? profile,
        IReadOnlyList<RouterWall>? walls,
        Rect[][] endZones,
        int index)
    {
        var w = route.Waypoints;
        var bends = w.Count - 2;
        if (profile is null)
        {
            return !route.Crossed && bends >= 2 && AreOppositeFaces(anchors.SourceSide, anchors.TargetSide);
        }

        if (route.Crossed || bends >= 3)
        {
            return true;
        }

        var clearance = options.Clearance;
        const double tolerance = 1e-6;
        if (bends >= 1
            && (SegmentLength(w, 0) < (2.0 * clearance) - tolerance
                || SegmentLength(w, w.Count - 2) < Math.Max(2.0 * clearance, RequiredEndLeg(connection.TargetEnd)) - tolerance))
        {
            return true;
        }

        for (var i = 1; i <= w.Count - 3; i++)
        {
            if (SegmentLength(w, i) < (2.0 * clearance) - tolerance)
            {
                return true;
            }
        }

        return HasWallViolation(w, walls, clearance) || CrossesPeerEndZone(w, endZones, index, clearance);
    }

    /// <summary>
    /// Returns whether any segment of <paramref name="w"/> runs along, or bends inside, the keep-out strip
    /// of a wall. The short step-off stubs at the two ends are exempt, so a port close to its own
    /// container's wall is not penalized for leaving its face.
    /// </summary>
    internal static bool HasWallViolation(IReadOnlyList<Point2D> w, IReadOnlyList<RouterWall>? walls, double clearance)
    {
        if (walls is null || walls.Count == 0)
        {
            return false;
        }

        var margin = 2.0 * clearance;
        var stub = clearance + 8.0;
        foreach (var wall in walls)
        {
            for (var i = 0; i < w.Count - 1; i++)
            {
                var vertical = Math.Abs(w[i].X - w[i + 1].X) < 1e-9;
                if (vertical != wall.IsVertical)
                {
                    continue;
                }

                var isEndSegment = i == 0 || i == w.Count - 2;
                if (isEndSegment && SegmentLength(w, i) <= stub + 1e-6)
                {
                    continue;
                }

                var coordinate = vertical ? w[i].X : w[i].Y;
                var lo = vertical ? Math.Min(w[i].Y, w[i + 1].Y) : Math.Min(w[i].X, w[i + 1].X);
                var hi = vertical ? Math.Max(w[i].Y, w[i + 1].Y) : Math.Max(w[i].X, w[i + 1].X);
                if (Math.Abs(coordinate - wall.Coordinate) < margin - 1e-6 && Math.Max(lo, wall.Lo) < Math.Min(hi, wall.Hi))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether any segment of <paramref name="w"/> passes within <paramref name="clearance"/> of
    /// another connector's reserved end zone.
    /// </summary>
    private static bool CrossesPeerEndZone(IReadOnlyList<Point2D> w, Rect[][] endZones, int index, double clearance)
    {
        for (var j = 0; j < endZones.Length; j++)
        {
            if (j == index || endZones[j] is not { } zones)
            {
                continue;
            }

            foreach (var zone in zones)
            {
                for (var i = 0; i < w.Count - 1; i++)
                {
                    if (SegmentWithin(w[i], w[i + 1], zone, clearance))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether an axis-aligned segment passes strictly within <paramref name="clearance"/> of a rectangle.</summary>
    private static bool SegmentWithin(Point2D a, Point2D b, Rect r, double clearance)
    {
        if (Math.Abs(a.Y - b.Y) < 1e-9)
        {
            return r.Y - clearance < a.Y && a.Y < r.Y + r.Height + clearance
                && Math.Max(Math.Min(a.X, b.X), r.X - clearance) < Math.Min(Math.Max(a.X, b.X), r.X + r.Width + clearance);
        }

        return r.X - clearance < a.X && a.X < r.X + r.Width + clearance
            && Math.Max(Math.Min(a.Y, b.Y), r.Y - clearance) < Math.Min(Math.Max(a.Y, b.Y), r.Y + r.Height + clearance);
    }

    /// <summary>
    /// Returns the rectangle reserving <paramref name="length"/> logical pixels straight out of
    /// <paramref name="side"/> from <paramref name="point"/>: a hairline for a plain end, and a strip
    /// <paramref name="thickness"/> wide (centered on the connector) for an end carrying an arrowhead, so
    /// other connectors keep a little further from the arrowhead's approach.
    /// </summary>
    private static Rect EndZone(Point2D point, PortSide side, double length, double thickness) => side switch
    {
        PortSide.Right => new Rect(point.X, point.Y - (thickness / 2.0), length, thickness),
        PortSide.Left => new Rect(point.X - length, point.Y - (thickness / 2.0), length, thickness),
        PortSide.Top => new Rect(point.X - (thickness / 2.0), point.Y - length, thickness, length),
        _ => new Rect(point.X - (thickness / 2.0), point.Y, thickness, length),
    };

    /// <summary>
    /// The two reserved end zones (source, target) of a not-yet-routed connection at its planned anchors.
    /// </summary>
    private static Rect[] EndZones(Connection connection, FaceAnchors anchors, ConnectorRouteOptions options)
    {
        var source = ResolveAnchorPoint(connection.From, anchors.SourceSide, anchors.SourceAlong, options.Clearance, anchors.SourceSlid);
        var target = ResolveAnchorPoint(connection.To, anchors.TargetSide, anchors.TargetAlong, options.Clearance, anchors.TargetSlid);
        return ZonesAt(connection, source, anchors.SourceSide, target, anchors.TargetSide, options);
    }

    /// <summary>The reserved end zones (source, target) of a routed connection, at its actual end points.</summary>
    private static Rect[] EndZonesOfRoute(
        Connection connection,
        FaceAnchors anchors,
        IReadOnlyList<Point2D> waypoints,
        ConnectorRouteOptions options) =>
        ZonesAt(connection, waypoints[0], anchors.SourceSide, waypoints[^1], anchors.TargetSide, options);

    /// <summary>
    /// Builds the reserved zones for the two ends: the source end is a hairline, and the target end a
    /// strip one clearance wide when it carries an arrowhead.
    /// </summary>
    private static Rect[] ZonesAt(
        Connection connection,
        Point2D source,
        PortSide sourceSide,
        Point2D target,
        PortSide targetSide,
        ConnectorRouteOptions options)
    {
        var length = 2.0 * options.Clearance;
        var thickness = NotationMetrics.AlongLineLength(connection.TargetEnd) > 0.0 ? options.Clearance : 0.0;
        return
        [
            EndZone(source, sourceSide, length, 0.0),
            EndZone(target, targetSide, Math.Max(length, RequiredEndLeg(connection.TargetEnd)), thickness),
        ];
    }

    /// <summary>One source-face / target-face combination to try.</summary>
    private sealed record FacePair(PortSide SourceSide, PortSide TargetSide, bool IsNatural);

    /// <summary>
    /// Lists the face combinations worth trying for a connection: the one already chosen, and — when
    /// <paramref name="alternatives"/> is set — the faces adjacent to each chosen face that still point
    /// toward the other box.
    /// </summary>
    private static List<FacePair> CandidateFacePairs(Connection connection, FaceAnchors anchors, bool alternatives)
    {
        var pairs = new List<FacePair> { new(anchors.SourceSide, anchors.TargetSide, true) };
        if (!alternatives)
        {
            return pairs;
        }

        var sourceFaces = TowardFaces(connection.From, connection.To, anchors.SourceSide);
        var targetFaces = TowardFaces(connection.To, connection.From, anchors.TargetSide);
        foreach (var source in sourceFaces)
        {
            foreach (var target in targetFaces)
            {
                if (source != anchors.SourceSide || target != anchors.TargetSide)
                {
                    pairs.Add(new FacePair(source, target, false));
                }
            }
        }

        return pairs;
    }

    /// <summary>
    /// The usable faces of <paramref name="box"/> to consider toward <paramref name="other"/>: the current
    /// face and the adjacent face that points most toward the other box (never the far-side faces).
    /// </summary>
    private static List<PortSide> TowardFaces(LayoutBox box, LayoutBox other, PortSide current)
    {
        var geometry = ResolveShapeGeometry(box);
        var order = PreferredFaceOrder(box, other, current);
        var faces = new List<PortSide>();
        for (var i = 0; i < 2; i++)
        {
            if (geometry.GetConnectableExtents(order[i]).Count > 0)
            {
                faces.Add(order[i]);
            }
        }

        return faces;
    }

    /// <summary>The accepted outcome of a port/face search: the route and the anchors it ended on.</summary>
    private readonly record struct SearchedRoute(IReadOnlyList<Point2D> Waypoints, FaceAnchors Anchors);

    /// <summary>
    /// Searches for a better route than <paramref name="initial"/> by letting the router pick the ports
    /// along each face (so a lane that fits both faces becomes one straight connector) and, in the
    /// cross-hierarchy profile, by trying the adjacent faces too. Returns <see langword="null"/> when
    /// nothing beats <paramref name="initial"/>.
    /// </summary>
    private static SearchedRoute? SearchBetterRoute(
        int index,
        IReadOnlyList<Connection> connections,
        ConnectorRouteOptions options,
        IReadOnlyList<LayoutBox> boxes,
        FaceAnchors[] anchors,
        IReadOnlyList<FixedAnchor> occupied,
        IReadOnlyList<Rect> softObstacles,
        RoutingProfile? profile,
        IReadOnlyList<RouterWall>? walls,
        Rect[][] endZones,
        RouteResult initial,
        RefineContext? refine = null)
    {
        var connection = connections[index];
        var current = anchors[index];
        var clearance = options.Clearance;

        var obstacles = new List<Rect>(boxes.Count);
        foreach (var box in boxes)
        {
            obstacles.Add(new Rect(box.X, box.Y, box.Width, box.Height));
        }

        if (profile is not null)
        {
            for (var j = 0; j < endZones.Length; j++)
            {
                if (j != index && endZones[j] is { } zones)
                {
                    obstacles.AddRange(zones);
                }
            }
        }

        // In the existing-line avoidance pass the existing lines also carry a crossing penalty in the
        // search, and the number of crossings — of those and of the other connectors of the batch — is the
        // first criterion of the ranking (fewest wins); the clearance level and the search cost only rank
        // candidates that cross equally often. Outside that pass every count is zero, so the ranking is
        // exactly the one without this criterion.
        var hints = BuildHints(connection, options, profile, walls);
        if (refine is not null)
        {
            hints = hints with { ExistingLines = refine.ExistingRects, ExistingLineCrossingPenalty = ExistingLineCrossingPenalty };
        }

        var edgeRouting = options.EdgeRouting;
        if (edgeRouting != EdgeRouting.Orthogonal)
        {
            return null;
        }

        FacePair? bestPair = null;
        RouteResult? best = null;
        var bestScore = double.PositiveInfinity;
        var bestCrossings = int.MaxValue;
        foreach (var pair in CandidateFacePairs(connection, current, alternatives: profile is not null))
        {
            var sourceRange = BuildPortRange(connection.From, pair.SourceSide, connection.To, index, true, connections, anchors, occupied, clearance);
            var targetRange = BuildPortRange(connection.To, pair.TargetSide, connection.From, index, false, connections, anchors, occupied, clearance);
            if (sourceRange is null || targetRange is null)
            {
                continue;
            }

            (sourceRange, targetRange) = AddSharedLanes(sourceRange, targetRange);
            var result = OrthogonalEdgeRouter.RoutePorts(sourceRange, targetRange, obstacles, clearance, null, softObstacles, hints);
            if (result.Crossed || HasReversal(result.Waypoints) || OverlapsRoutedLine(result.Waypoints, softObstacles, refine is null ? 0.0 : NearlyCoincidentGap))
            {
                continue;
            }

            var crossings = refine is null ? 0 : CountLineCrossings(result.Waypoints, refine.CrossingRects);
            var score = result.Cost + (pair.IsNatural ? 0.0 : AlternativeFacePenalty);
            var better = best is null
                || crossings < bestCrossings
                || (crossings == bestCrossings
                    && (result.ClearanceUsed > best.Value.ClearanceUsed + 1e-9
                        || (Math.Abs(result.ClearanceUsed - best.Value.ClearanceUsed) <= 1e-9 && score < bestScore)));
            if (better)
            {
                best = result;
                bestPair = pair;
                bestScore = score;
                bestCrossings = crossings;
            }
        }

        if (best is not { } chosen || bestPair is null)
        {
            return null;
        }

        // The avoidance pass keeps a result only when it crosses strictly fewer lines than the route it
        // replaces (the tidying pass: no more), at the full clearance.
        if (refine is not null
            && ((refine.AllowEqualCrossings
                    ? bestCrossings > CountLineCrossings(initial.Waypoints, refine.CrossingRects)
                    : bestCrossings >= CountLineCrossings(initial.Waypoints, refine.CrossingRects))
                || chosen.ClearanceUsed + 1e-9 < clearance))
        {
            return null;
        }

        // The first route's clearance level is the floor: a search result that only exists at a tighter
        // clearance than the plain route found is not an improvement.
        if (!initial.Crossed && chosen.ClearanceUsed + 1e-9 < initial.ClearanceUsed)
        {
            return null;
        }

        if (profile is null && !IsStraighterThanInitial(initial, chosen))
        {
            return null;
        }

        return new SearchedRoute(chosen.Waypoints, AnchorsOfRoute(connection, current, bestPair, chosen.Waypoints, clearance));
    }

    /// <summary>
    /// Returns whether <paramref name="waypoints"/> doubles back on itself: two consecutive segments on
    /// the same line running in opposite directions (a visible U-turn).
    /// </summary>
    private static bool HasReversal(IReadOnlyList<Point2D> waypoints)
    {
        for (var i = 1; i < waypoints.Count - 1; i++)
        {
            var a = waypoints[i - 1];
            var b = waypoints[i];
            var c = waypoints[i + 1];
            var sameX = Math.Abs(a.X - b.X) < 1e-9 && Math.Abs(b.X - c.X) < 1e-9;
            var sameY = Math.Abs(a.Y - b.Y) < 1e-9 && Math.Abs(b.Y - c.Y) < 1e-9;
            if ((sameX && (b.Y - a.Y) * (c.Y - b.Y) < 0.0) || (sameY && (b.X - a.X) * (c.X - b.X) < 0.0))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether any segment of <paramref name="waypoints"/> runs along an already-routed
    /// connector (one of the thin <paramref name="routed"/> rectangles) for more than a trivial stretch.
    /// </summary>
    private static bool OverlapsRoutedLine(IReadOnlyList<Point2D> waypoints, IReadOnlyList<Rect> routed, double tolerance = 0.0)
    {
        const double trivialOverlap = 5.0;
        for (var i = 0; i < waypoints.Count - 1; i++)
        {
            var a = waypoints[i];
            var b = waypoints[i + 1];
            var horizontal = Math.Abs(a.Y - b.Y) < 1e-9;
            foreach (var r in routed)
            {
                if (horizontal)
                {
                    if (r.Width > r.Height && r.Y - tolerance < a.Y && a.Y < r.Y + r.Height + tolerance
                        && Math.Min(Math.Max(a.X, b.X), r.X + r.Width) - Math.Max(Math.Min(a.X, b.X), r.X) > trivialOverlap)
                    {
                        return true;
                    }
                }
                else if (r.Height > r.Width && r.X - tolerance < a.X && a.X < r.X + r.Width + tolerance
                    && Math.Min(Math.Max(a.Y, b.Y), r.Y + r.Height) - Math.Max(Math.Min(a.Y, b.Y), r.Y) > trivialOverlap)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Outside the cross-hierarchy profile a searched route replaces the plain one only when sliding the
    /// ports removed bends from it — the stage exists to straighten a jog, never to re-route a connector
    /// that cannot be straightened.
    /// </summary>
    private static bool IsStraighterThanInitial(RouteResult initial, RouteResult searched) =>
        searched.Waypoints.Count < initial.Waypoints.Count;

    /// <summary>
    /// Converts a searched route's end points back into the per-face anchors recorded for the batch,
    /// flagging an end as slid when it no longer sits where the plain anchor would have put it.
    /// </summary>
    private static FaceAnchors AnchorsOfRoute(
        Connection connection,
        FaceAnchors previous,
        FacePair pair,
        IReadOnlyList<Point2D> waypoints,
        double clearance)
    {
        var sourceAlong = ToLocalAlong(connection.From, pair.SourceSide, AlongCoordinate(pair.SourceSide, waypoints[0]));
        var targetAlong = ToLocalAlong(connection.To, pair.TargetSide, AlongCoordinate(pair.TargetSide, waypoints[^1]));
        var plainSource = ResolveAnchorPoint(connection.From, previous.SourceSide, previous.SourceAlong, clearance, previous.SourceSlid);
        var plainTarget = ResolveAnchorPoint(connection.To, previous.TargetSide, previous.TargetAlong, clearance, previous.TargetSlid);
        var sourceSlid = pair.SourceSide != previous.SourceSide || Math.Abs(AlongCoordinate(pair.SourceSide, plainSource) - AlongCoordinate(pair.SourceSide, waypoints[0])) > 1e-6;
        var targetSlid = pair.TargetSide != previous.TargetSide || Math.Abs(AlongCoordinate(pair.TargetSide, plainTarget) - AlongCoordinate(pair.TargetSide, waypoints[^1])) > 1e-6;
        return new FaceAnchors(
            sourceAlong,
            pair.SourceSide,
            targetAlong,
            pair.TargetSide,
            sourceSlid || previous.SourceSlid,
            targetSlid || previous.TargetSlid);
    }

    /// <summary>The coordinate of <paramref name="point"/> along a face on <paramref name="side"/>.</summary>
    private static double AlongCoordinate(PortSide side, Point2D point) =>
        side is PortSide.Left or PortSide.Right ? point.Y : point.X;

    /// <summary>
    /// Builds the permitted-port description for one end of a connection on <paramref name="side"/>:
    /// the usable extents of the face (inset by <see cref="PortCornerMargin"/>), kept clear of the other
    /// connectors' ports on that face (by the batch's usual separation, never closer than they already
    /// are, and without hopping over a neighbor), plus the end's current or natural position as the
    /// preferred port.
    /// </summary>
    /// <returns>The range, or <see langword="null"/> when the face has no connectable extent.</returns>
    private static PortRange? BuildPortRange(
        LayoutBox box,
        PortSide side,
        LayoutBox other,
        int index,
        bool isSource,
        IReadOnlyList<Connection> connections,
        FaceAnchors[] anchors,
        IReadOnlyList<FixedAnchor> occupied,
        double clearance)
    {
        var geometry = ResolveShapeGeometry(box);
        var extents = geometry.GetConnectableExtents(side);
        if (extents.Count == 0)
        {
            return null;
        }

        var usable = SlideExtents(extents, isSource ? PortCornerMargin : PortCornerMarginFor(connections[index].TargetEnd));
        var current = anchors[index];
        var isCurrentFace = side == (isSource ? current.SourceSide : current.TargetSide);
        double local;
        var slid = false;
        if (isCurrentFace)
        {
            local = isSource ? current.SourceAlong : current.TargetAlong;
            slid = isSource ? current.SourceSlid : current.TargetSlid;
        }
        else
        {
            local = ToLocalAlong(box, side, NaiveAnchorCoordinate(box, other, side, clearance));
        }

        var preferredPoint = ResolveAnchorPoint(box, side, local, clearance, slid);
        var preferredAbsolute = AlongCoordinate(side, preferredPoint);
        var preferredLocal = ToLocalAlong(box, side, preferredAbsolute);
        var face = side is PortSide.Left or PortSide.Right ? preferredPoint.X : preferredPoint.Y;

        var neighbors = FaceNeighbors(box, side, index, connections, anchors, occupied, clearance);
        var intervals = isCurrentFace
            ? WindowBetweenNeighbors(usable, preferredLocal, neighbors)
            : ExcludeNeighborWindows(usable, neighbors);
        intervals.Add((preferredLocal, preferredLocal));

        var offset = side is PortSide.Left or PortSide.Right ? box.Y : box.X;
        return new PortRange
        {
            Side = side,
            Face = face,
            Preferred = preferredAbsolute,
            Intervals = intervals.Select(i => (i.Lo + offset, i.Hi + offset)).ToList(),
        };
    }

    /// <summary>
    /// Insets each connectable face segment by <paramref name="margin"/> at both ends to give the
    /// range a port may slide over. A segment too short to keep that margin from both ends collapses to
    /// its midpoint, so a short face keeps its centered anchor instead of letting a port crowd a corner.
    /// </summary>
    private static List<(double Lo, double Hi)> SlideExtents(IReadOnlyList<(double Lo, double Hi)> extents, double margin)
    {
        var slide = new List<(double Lo, double Hi)>(extents.Count);
        foreach (var (lo, hi) in extents)
        {
            if (hi < lo)
            {
                continue;
            }

            if (hi - lo >= 2.0 * margin)
            {
                slide.Add((lo + margin, hi - margin));
            }
            else
            {
                var middle = (lo + hi) / 2.0;
                slide.Add((middle, middle));
            }
        }

        return slide;
    }

    /// <summary>
    /// The local along-face positions of every other connector end claiming the same face of
    /// <paramref name="box"/>: other connections in the batch (at their planned or final anchors) and any
    /// fixed, already-occupied anchors.
    /// </summary>
    private static List<double> FaceNeighbors(
        LayoutBox box,
        PortSide side,
        int index,
        IReadOnlyList<Connection> connections,
        FaceAnchors[] anchors,
        IReadOnlyList<FixedAnchor> occupied,
        double clearance)
    {
        var neighbors = new List<double>();
        for (var j = 0; j < connections.Count; j++)
        {
            if (j == index)
            {
                continue;
            }

            var a = anchors[j];
            if (ReferenceEquals(connections[j].From, box) && a.SourceSide == side)
            {
                neighbors.Add(a.SourceSlid ? a.SourceAlong : ClampToConnectableExtent(box, side, a.SourceAlong, clearance));
            }

            if (ReferenceEquals(connections[j].To, box) && a.TargetSide == side)
            {
                neighbors.Add(a.TargetSlid ? a.TargetAlong : ClampToConnectableExtent(box, side, a.TargetAlong, clearance));
            }
        }

        foreach (var fixedAnchor in occupied)
        {
            if (ReferenceEquals(fixedAnchor.Box, box) && fixedAnchor.Side == side)
            {
                neighbors.Add(fixedAnchor.Along);
            }
        }

        return neighbors;
    }

    /// <summary>
    /// Restricts <paramref name="usable"/> to the stretch around <paramref name="position"/> that lies
    /// between its nearest neighbors, each kept at least the smaller of the batch separation and its
    /// current distance away — so a port may slide freely without crowding or hopping over a neighbor.
    /// </summary>
    private static List<(double Lo, double Hi)> WindowBetweenNeighbors(
        IReadOnlyList<(double Lo, double Hi)> usable,
        double position,
        IReadOnlyList<double> neighbors)
    {
        var lo = double.NegativeInfinity;
        var hi = double.PositiveInfinity;
        foreach (var n in neighbors)
        {
            if (n <= position)
            {
                lo = Math.Max(lo, n + Math.Min(MinFixedAnchorSeparation, position - n));
            }
            else
            {
                hi = Math.Min(hi, n - Math.Min(MinFixedAnchorSeparation, n - position));
            }
        }

        var window = new List<(double Lo, double Hi)>();
        foreach (var (a, b) in usable)
        {
            var from = Math.Max(a, lo);
            var to = Math.Min(b, hi);
            if (to >= from)
            {
                window.Add((from, to));
            }
        }

        return window;
    }

    /// <summary>
    /// Removes from <paramref name="usable"/> the stretch within the batch separation of every neighbor,
    /// for a face the connector is newly moving onto.
    /// </summary>
    private static List<(double Lo, double Hi)> ExcludeNeighborWindows(
        IReadOnlyList<(double Lo, double Hi)> usable,
        IReadOnlyList<double> neighbors)
    {
        var pieces = usable.ToList();
        foreach (var n in neighbors)
        {
            var next = new List<(double Lo, double Hi)>();
            foreach (var (a, b) in pieces)
            {
                if (n + MinFixedAnchorSeparation <= a || n - MinFixedAnchorSeparation >= b)
                {
                    next.Add((a, b));
                    continue;
                }

                if (n - MinFixedAnchorSeparation >= a)
                {
                    next.Add((a, n - MinFixedAnchorSeparation));
                }

                if (n + MinFixedAnchorSeparation <= b)
                {
                    next.Add((n + MinFixedAnchorSeparation, b));
                }
            }

            pieces = next;
        }

        return pieces;
    }

    /// <summary>
    /// When both ends slide along the same axis (facing opposite faces), adds the middle of every stretch
    /// where their permitted intervals overlap as a candidate port for both, so a straight lane through the
    /// overlap is always available even where no obstacle edge falls inside it.
    /// </summary>
    private static (PortRange Source, PortRange Target) AddSharedLanes(PortRange source, PortRange target)
    {
        if (source.NormalIsX != target.NormalIsX)
        {
            return (source, target);
        }

        var shared = new List<double>();
        foreach (var (a, b) in source.Intervals)
        {
            foreach (var (c, d) in target.Intervals)
            {
                var lo = Math.Max(a, c);
                var hi = Math.Min(b, d);
                if (hi - lo > 1e-6)
                {
                    shared.Add((lo + hi) / 2.0);
                }
            }
        }

        return (source with { Extra = shared }, target with { Extra = shared });
    }

    /// <summary>
    /// A hairline thickness is enough for the first routing of a batch: the router already keeps a clearance
    /// gap between paths and any obstacle, so a wide slab would only double up on that margin.
    /// </summary>
    private const double HairlineHalfThickness = 1.0;

    /// <summary>
    /// Appends a thin rectangle obstacle for every interior orthogonal segment of
    /// <paramref name="waypoints"/> to <paramref name="obstacles"/>, so a subsequently routed
    /// connector treats this already-routed line as something to steer clear of (by the router's usual
    /// obstacle clearance) rather than freely overlapping it.
    /// </summary>
    /// <remarks>
    /// By default the first and last segments are intentionally excluded. Those short endpoint-adjacent
    /// approach legs are the one place where several connectors may legitimately share the same
    /// corridor while still reaching a common face cleanly; penalizing them as soft obstacles is what
    /// causes the redundant leave-and-return detours this method avoids. A positive
    /// <paramref name="endInset"/> (used for connectors with nested endpoints, whose end legs can be
    /// long straight runs) includes those end segments too, but leaves the last
    /// <paramref name="endInset"/> logical pixels next to each endpoint free so the shared approach
    /// corridor stays usable.
    /// </remarks>
    private static void AddLineObstacles(IReadOnlyList<Point2D> waypoints, List<Rect> obstacles, double endInset = 0.0, double? thickness = null)
    {
        var halfThickness = thickness ?? HairlineHalfThickness;
        for (var i = 0; i < waypoints.Count - 1; i++)
        {
            var a = waypoints[i];
            var b = waypoints[i + 1];

            var isFirst = i == 0;
            var isLast = i == waypoints.Count - 2;
            if (isFirst || isLast)
            {
                if (endInset <= 0.0)
                {
                    continue;
                }

                var length = Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y);
                var trimStart = isFirst ? endInset : 0.0;
                var trimEnd = isLast ? endInset : 0.0;
                if (length <= trimStart + trimEnd + 1e-6)
                {
                    continue;
                }

                var ux = Math.Sign(b.X - a.X);
                var uy = Math.Sign(b.Y - a.Y);
                a = new Point2D(a.X + (ux * trimStart), a.Y + (uy * trimStart));
                b = new Point2D(b.X - (ux * trimEnd), b.Y - (uy * trimEnd));
            }

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
    private readonly record struct FaceAnchors(
        double SourceAlong,
        PortSide SourceSide,
        double TargetAlong,
        PortSide TargetSide,
        bool SourceSlid = false,
        bool TargetSlid = false);

    /// <summary>
    /// One connector's claim on a shared box face: which connection it belongs to, whether it is the
    /// connection's source or target end, and the position (its counterpart box's centre on the face's
    /// axis) used to order claims before spreading them evenly across the face. <c>Reverse</c> marks a claim
    /// that overflowed from a face with a positive outward normal (right or bottom) to an adjacent face:
    /// among claims with equal counterparts it takes the reverse connection order, so the connectors that
    /// wrap around the corner nest instead of crossing one another.
    /// </summary>
    private readonly record struct FaceSlot(int ConnectionIndex, bool IsSource, double Counterpart, bool Reverse = false);

    /// <summary>
    /// Redistributes anchors for any box face claimed by more than one connector in this batch (or that
    /// already carries an occupied anchor), so connectors landing on one face are always at least
    /// <see cref="MinFixedAnchorSeparation"/> apart instead of each independently clamping to the same
    /// boundary point (see <see cref="FacingAnchors"/>) or being crowded together.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each face is first spread inside its connectable extent inset by the connector clearance (the
    /// historical behavior). If that region cannot hold the face's connectors the required distance apart —
    /// including the anchors already occupied there — the wider marker-aware corner-margin region
    /// (<see cref="PortCornerMargin"/>, or <see cref="PortCornerMarginFor"/> for a wider target marker) is
    /// used instead, and the anchors outside the clearance inset are flagged as slid so later stages keep
    /// them there.
    /// </para>
    /// <para>
    /// When even that region is full, the connectors nearest the middle of the face (ordered by where their
    /// counterpart sits) keep it and the overflow from each end moves to the adjacent face on that side,
    /// where it takes a fresh part in the next pass (at most <see cref="MaxAnchorRedistributionPasses"/>
    /// passes, in deterministic order; a connector end never returns to a face it has already left). If
    /// every candidate face is full the face is spread as evenly as it allows (best effort, never an error).
    /// </para>
    /// </remarks>
    /// <param name="connections">The connections being routed, in the same order as <paramref name="anchors"/>.</param>
    /// <param name="anchors">
    /// The naive per-connection anchors computed by <see cref="FacingAnchors"/>; updated in place for any
    /// connector whose anchor (or face) is moved to share its face fairly with others. The redistribution
    /// spans the union of the face's connectable extents rather than the full bounding-box edge.
    /// </param>
    /// <param name="clearance">Minimum inset kept between a redistributed anchor and the ends of the face.</param>
    /// <param name="fixedAnchors">
    /// Anchor points already occupied on box faces; a face carrying one never keeps a batch anchor closer
    /// than <see cref="MinFixedAnchorSeparation"/> to it.
    /// </param>
    private static void DistributeSharedFaceAnchors(
        IReadOnlyList<Connection> connections,
        FaceAnchors[] anchors,
        double clearance,
        IReadOnlyList<FixedAnchor> fixedAnchors)
    {
        // The face each connector end started on (to detect an overflowed end) and every face it has been on
        // (so an overflowing end never returns to a face it left).
        var origin = new Dictionary<(int Index, bool IsSource), PortSide>();
        var visited = new HashSet<(int Index, bool IsSource, PortSide Side)>();
        for (var i = 0; i < connections.Count; i++)
        {
            origin[(i, true)] = anchors[i].SourceSide;
            origin[(i, false)] = anchors[i].TargetSide;
            visited.Add((i, true, anchors[i].SourceSide));
            visited.Add((i, false, anchors[i].TargetSide));
        }

        for (var pass = 0; pass < MaxAnchorRedistributionPasses; pass++)
        {
            // The last pass places every connector where it is (best effort) rather than moving any.
            var allowOverflow = pass < MaxAnchorRedistributionPasses - 1;
            if (!AssignFaceAnchors(connections, anchors, clearance, fixedAnchors, origin, visited, allowOverflow))
            {
                return;
            }
        }
    }

    /// <summary>Maximum number of overflow passes made by <see cref="DistributeSharedFaceAnchors"/>.</summary>
    private const int MaxAnchorRedistributionPasses = 4;

    /// <summary>
    /// One pass of <see cref="DistributeSharedFaceAnchors"/>: groups the connector ends by face, places each
    /// shared face's ends at least <see cref="MinFixedAnchorSeparation"/> apart and, when
    /// <paramref name="allowOverflow"/> is set, moves the ends a full face cannot hold to an adjacent face.
    /// </summary>
    /// <returns><see langword="true"/> when at least one connector end moved to another face.</returns>
    private static bool AssignFaceAnchors(
        IReadOnlyList<Connection> connections,
        FaceAnchors[] anchors,
        double clearance,
        IReadOnlyList<FixedAnchor> fixedAnchors,
        Dictionary<(int Index, bool IsSource), PortSide> origin,
        HashSet<(int Index, bool IsSource, PortSide Side)> visited,
        bool allowOverflow)
    {
        var groups = new Dictionary<(LayoutBox Box, PortSide Side), List<FaceSlot>>(FaceKeyComparer.Instance);

        // A claim that overflowed from a right or bottom face keeps the reverse connection order on its new
        // face, so the connectors wrapping around the corner nest instead of crossing.
        bool Overflowed(int index, bool isSource, PortSide current) =>
            origin[(index, isSource)] != current && origin[(index, isSource)] is PortSide.Right or PortSide.Bottom;

        for (var i = 0; i < connections.Count; i++)
        {
            var connection = connections[i];
            var faceAnchors = anchors[i];

            AddSlot(
                groups, connection.From, faceAnchors.SourceSide, i, isSource: true, CounterpartCentre(connection.To, faceAnchors.SourceSide),
                Overflowed(i, true, faceAnchors.SourceSide));
            AddSlot(
                groups, connection.To, faceAnchors.TargetSide, i, isSource: false, CounterpartCentre(connection.From, faceAnchors.TargetSide),
                Overflowed(i, false, faceAnchors.TargetSide));
        }

        var moved = false;
        foreach (var (key, slots) in groups)
        {
            var (box, side) = key;
            var occupied = fixedAnchors
                .Where(f => ReferenceEquals(f.Box, box) && f.Side == side)
                .Select(f => f.Along)
                .ToList();

            if (slots.Count < 2 && occupied.Count == 0)
            {
                // A face claimed by a single connector already has the best anchor FacingAnchors could
                // compute for that pair of boxes; leave it untouched.
                continue;
            }

            var extents = ResolveShapeGeometry(box).GetConnectableExtents(side);
            var usable = BuildUsableExtents(extents, clearance);
            if (usable.Count == 0)
            {
                continue;
            }

            var ordered = slots
                .OrderBy(s => s.Counterpart)
                .ThenBy(s => s.Reverse ? -s.ConnectionIndex : s.ConnectionIndex)
                .ToList();
            var margin = SlideExtents(extents, FaceMargin(ordered, connections));
            var (region, free, capacity) = ChooseFaceRegion(usable, margin, occupied, ordered.Count);

            var stayers = ordered;
            if (allowOverflow && ordered.Count > capacity)
            {
                (stayers, var overflowed) = MoveOverflowToAdjacentFaces(
                    box, side, ordered, capacity, connections, anchors, clearance, visited);
                moved |= overflowed;
            }

            if (stayers.Count > 0)
            {
                PlaceOnFace(stayers, anchors, region, free, usable, occupied);
            }
        }

        return moved;
    }

    /// <summary>
    /// The corner margin used for a face's connectors: the legacy <see cref="PortCornerMargin"/>, widened to
    /// <see cref="PortCornerMarginFor"/> the end marker of any connector that ends (targets) on the face.
    /// </summary>
    private static double FaceMargin(IReadOnlyList<FaceSlot> slots, IReadOnlyList<Connection> connections)
    {
        var margin = PortCornerMargin;
        foreach (var slot in slots)
        {
            if (!slot.IsSource)
            {
                margin = Math.Max(margin, PortCornerMarginFor(connections[slot.ConnectionIndex].TargetEnd));
            }
        }

        return margin;
    }

    /// <summary>
    /// Chooses the region of a face the connectors are placed in: the clearance-inset
    /// <paramref name="usable"/> extents when they can hold <paramref name="count"/> anchors the required
    /// distance apart (kept clear of the <paramref name="occupied"/> anchors), otherwise the wider
    /// <paramref name="margin"/> extents; when neither can, the one holding more.
    /// </summary>
    /// <returns>The chosen extents, the part of them clear of occupied anchors, and how many anchors that part holds.</returns>
    private static (IReadOnlyList<(double Lo, double Hi)> Region, List<(double Lo, double Hi)> Free, int Capacity) ChooseFaceRegion(
        IReadOnlyList<(double Lo, double Hi)> usable,
        IReadOnlyList<(double Lo, double Hi)> margin,
        List<double> occupied,
        int count)
    {
        var usableFree = ExcludeNeighborWindows(usable, occupied);
        var usableCapacity = PackPositions(usableFree, int.MaxValue).Count;
        if (usableCapacity >= count)
        {
            return (usable, usableFree, usableCapacity);
        }

        var marginFree = ExcludeNeighborWindows(margin, occupied);
        var marginCapacity = PackPositions(marginFree, int.MaxValue).Count;
        return marginCapacity >= usableCapacity
            ? (margin, marginFree, marginCapacity)
            : (usable, usableFree, usableCapacity);
    }

    /// <summary>
    /// Moves the connectors a full face cannot hold to an adjacent face. Connectors are peeled from the ends
    /// of the ordering (the ones whose counterpart sits farthest from the face's middle first), and only
    /// toward the side their counterpart lies on: the lower end goes to the adjacent face on the lower side
    /// (top or left), the upper end to the one on the upper side (bottom or right), falling back to the other
    /// adjacent face. The connectors nearest the middle keep the face. A connector whose adjacent faces are
    /// unusable or already visited stays on this face.
    /// </summary>
    /// <returns>The connectors that remain on the face (in order) and whether any moved.</returns>
    private static (List<FaceSlot> Stayers, bool Moved) MoveOverflowToAdjacentFaces(
        LayoutBox box,
        PortSide side,
        List<FaceSlot> ordered,
        int capacity,
        IReadOnlyList<Connection> connections,
        FaceAnchors[] anchors,
        double clearance,
        HashSet<(int Index, bool IsSource, PortSide Side)> visited)
    {
        var keep = Math.Max(capacity, 0);
        var geometry = ResolveShapeGeometry(box);
        var (lowerFace, upperFace) = side is PortSide.Left or PortSide.Right
            ? (PortSide.Top, PortSide.Bottom)
            : (PortSide.Left, PortSide.Right);
        var faceMiddle = CounterpartCentre(box, side);

        // The face a claim moves to when peeled from the front (its counterpart on the lower side) or the
        // back (the upper side) of the ordering; null when it cannot move that way.
        PortSide? OverflowFace(FaceSlot slot, bool fromFront)
        {
            var lowerSide = slot.Counterpart <= faceMiddle;
            if (lowerSide != fromFront)
            {
                return null;
            }

            return new[] { fromFront ? lowerFace : upperFace, fromFront ? upperFace : lowerFace }
                .Where(face => geometry.GetConnectableExtents(face).Count > 0 && !visited.Contains((slot.ConnectionIndex, slot.IsSource, face)))
                .Cast<PortSide?>()
                .FirstOrDefault();
        }

        var peeled = new List<(FaceSlot Slot, PortSide Face)>();
        var front = 0;
        var back = ordered.Count;
        while (back - front > keep)
        {
            var frontFace = OverflowFace(ordered[front], fromFront: true);
            var backFace = OverflowFace(ordered[back - 1], fromFront: false);
            if (frontFace is null && backFace is null)
            {
                break;
            }

            var peelFront = backFace is null
                || (frontFace is not null
                    && Math.Abs(ordered[front].Counterpart - faceMiddle) >= Math.Abs(ordered[back - 1].Counterpart - faceMiddle));
            if (peelFront)
            {
                peeled.Add((ordered[front], frontFace!.Value));
                front++;
            }
            else
            {
                back--;
                peeled.Add((ordered[back], backFace!.Value));
            }
        }

        var moved = false;
        foreach (var (slot, face) in peeled)
        {
            var connection = connections[slot.ConnectionIndex];
            var counterpart = slot.IsSource ? connection.To : connection.From;
            var along = ToLocalAlong(box, face, NaiveAnchorCoordinate(box, counterpart, face, clearance));
            var current = anchors[slot.ConnectionIndex];
            anchors[slot.ConnectionIndex] = slot.IsSource
                ? current with { SourceSide = face, SourceAlong = along, SourceSlid = false }
                : current with { TargetSide = face, TargetAlong = along, TargetSlid = false };
            visited.Add((slot.ConnectionIndex, slot.IsSource, face));
            moved = true;
        }

        return (ordered.GetRange(front, back - front), moved);
    }

    /// <summary>
    /// Places the connectors of one face, in order, inside <paramref name="free"/> (the part of
    /// <paramref name="region"/> clear of the occupied anchors). Without occupied anchors they are spread
    /// evenly along the region (the historical behavior); with occupied anchors each keeps the position
    /// nearest its current anchor that is clear of every anchor already placed, so an anchor that is already
    /// clear does not move. If the connectors do not fit the required distance apart, they are packed from the
    /// start of the free space; if they still do not fit (a full face with nowhere to overflow), they are
    /// spread evenly across the region as a best effort. Anchors outside the clearance-inset
    /// <paramref name="usable"/> extents are flagged as slid.
    /// </summary>
    private static void PlaceOnFace(
        List<FaceSlot> stayers,
        FaceAnchors[] anchors,
        IReadOnlyList<(double Lo, double Hi)> region,
        List<(double Lo, double Hi)> free,
        IReadOnlyList<(double Lo, double Hi)> usable,
        List<double> occupied)
    {
        var count = stayers.Count;
        double[]? positions = null;

        if (occupied.Count > 0 || count == 1)
        {
            positions = PlaceNearPreferred(stayers, anchors, free, occupied);
        }
        else if (TotalExtentLength(free) > 0.0)
        {
            var length = TotalExtentLength(free);
            var evenly = new double[count];
            for (var k = 0; k < count; k++)
            {
                evenly[k] = CoordinateAtDistance(free, k * length / (count - 1));
            }

            if (MinSpacing(evenly) >= MinFixedAnchorSeparation - 1e-9)
            {
                positions = evenly;
            }
        }

        if (positions is null)
        {
            var packed = PackPositions(free, count);
            positions = packed.Count == count ? [.. packed] : null;
        }

        if (positions is null)
        {
            // Over capacity with nowhere to overflow: spread as evenly as the region allows.
            var length = TotalExtentLength(region);
            positions = new double[count];
            for (var k = 0; k < count; k++)
            {
                positions[k] = count == 1 ? CoordinateAtDistance(region, length / 2.0) : CoordinateAtDistance(region, k * length / (count - 1));
            }
        }

        for (var k = 0; k < count; k++)
        {
            var slot = stayers[k];
            var slid = !WithinExtents(positions[k], usable);
            var current = anchors[slot.ConnectionIndex];
            anchors[slot.ConnectionIndex] = slot.IsSource
                ? current with { SourceAlong = positions[k], SourceSlid = slid }
                : current with { TargetAlong = positions[k], TargetSlid = slid };
        }
    }

    /// <summary>
    /// Places each connector, in order, at the position inside <paramref name="free"/> nearest its current
    /// anchor that is at least <see cref="MinFixedAnchorSeparation"/> from every anchor placed so far
    /// (the occupied ones included). Returns <see langword="null"/> when some connector has no such position.
    /// </summary>
    private static double[]? PlaceNearPreferred(
        List<FaceSlot> stayers,
        FaceAnchors[] anchors,
        List<(double Lo, double Hi)> free,
        List<double> occupied)
    {
        var placed = new List<double>(occupied);
        var positions = new double[stayers.Count];
        for (var k = 0; k < stayers.Count; k++)
        {
            var slot = stayers[k];
            var current = anchors[slot.ConnectionIndex];
            var preferred = slot.IsSource ? current.SourceAlong : current.TargetAlong;
            var candidates = new List<double>();
            foreach (var (lo, hi) in free)
            {
                candidates.Add(Math.Clamp(preferred, lo, hi));
                candidates.Add(lo);
                candidates.Add(hi);
            }

            foreach (var other in placed)
            {
                candidates.Add(other - MinFixedAnchorSeparation);
                candidates.Add(other + MinFixedAnchorSeparation);
            }

            double? best = null;
            foreach (var candidate in candidates)
            {
                if (!WithinExtents(candidate, free)
                    || MinDistance(candidate, placed) < MinFixedAnchorSeparation - 1e-9)
                {
                    continue;
                }

                if (best is null || Math.Abs(candidate - preferred) < Math.Abs(best.Value - preferred) - 1e-9)
                {
                    best = candidate;
                }
            }

            if (best is null)
            {
                return null;
            }

            positions[k] = best.Value;
            placed.Add(best.Value);
        }

        return positions;
    }

    /// <summary>
    /// Returns up to <paramref name="max"/> positions packed <see cref="MinFixedAnchorSeparation"/> apart,
    /// from the start of each of the (ascending) free intervals: the most anchors the intervals can hold.
    /// </summary>
    private static List<double> PackPositions(IReadOnlyList<(double Lo, double Hi)> free, int max)
    {
        var positions = new List<double>();
        var last = double.NegativeInfinity;
        foreach (var (lo, hi) in free.OrderBy(interval => interval.Lo))
        {
            var position = Math.Max(lo, last + MinFixedAnchorSeparation);
            while (position <= hi + 1e-9 && positions.Count < max)
            {
                positions.Add(Math.Min(position, hi));
                last = position;
                position += MinFixedAnchorSeparation;
            }
        }

        return positions;
    }

    /// <summary>Returns whether <paramref name="value"/> lies inside any of the closed <paramref name="extents"/>.</summary>
    private static bool WithinExtents(double value, IReadOnlyList<(double Lo, double Hi)> extents)
    {
        foreach (var (lo, hi) in extents)
        {
            if (value >= lo - 1e-9 && value <= hi + 1e-9)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the smallest gap between consecutive values of an ascending array (infinity for fewer than two).</summary>
    private static double MinSpacing(double[] values)
    {
        var min = double.PositiveInfinity;
        for (var i = 1; i < values.Length; i++)
        {
            min = Math.Min(min, values[i] - values[i - 1]);
        }

        return min;
    }

    /// <summary>
    /// An anchor point already occupied on a box face by a connector that is routed elsewhere, in the
    /// box's local face coordinate system (0 at the face start). Used so a batch connector never lands on
    /// top of an existing connector's end point.
    /// </summary>
    internal readonly record struct FixedAnchor(LayoutBox Box, PortSide Side, double Along);

    /// <summary>
    /// Minimum separation, in logical pixels, kept along a face between any two connector ends on it
    /// (batch anchors, and batch anchors versus anchors that are already occupied on the face).
    /// </summary>
    private const double MinFixedAnchorSeparation = 16.0;

    /// <summary>
    /// Resolves whether <paramref name="point"/> lies on the bounding-box boundary of
    /// <paramref name="box"/> and, when it does, describes it as an occupied face anchor.
    /// </summary>
    /// <param name="box">The box whose faces are tested.</param>
    /// <param name="point">The connector end point to classify.</param>
    /// <param name="anchor">The resulting occupied anchor, when the point is on a face.</param>
    /// <returns><see langword="true"/> when the point sits on one of the box's four faces.</returns>
    internal static bool TryResolveFaceAnchor(LayoutBox box, Point2D point, out FixedAnchor anchor)
    {
        const double tolerance = 0.5;
        var withinX = point.X >= box.X - tolerance && point.X <= box.X + box.Width + tolerance;
        var withinY = point.Y >= box.Y - tolerance && point.Y <= box.Y + box.Height + tolerance;

        PortSide? side = null;
        if (withinY && Math.Abs(point.X - box.X) <= tolerance)
        {
            side = PortSide.Left;
        }
        else if (withinY && Math.Abs(point.X - (box.X + box.Width)) <= tolerance)
        {
            side = PortSide.Right;
        }
        else if (withinX && Math.Abs(point.Y - box.Y) <= tolerance)
        {
            side = PortSide.Top;
        }
        else if (withinX && Math.Abs(point.Y - (box.Y + box.Height)) <= tolerance)
        {
            side = PortSide.Bottom;
        }

        if (side is null)
        {
            anchor = default;
            return false;
        }

        var absolute = side is PortSide.Left or PortSide.Right ? point.Y : point.X;
        anchor = new FixedAnchor(box, side.Value, ToLocalAlong(box, side.Value, absolute));
        return true;
    }

    /// <summary>Returns the smallest absolute distance from <paramref name="value"/> to any of <paramref name="others"/>.</summary>
    private static double MinDistance(double value, List<double> others)
    {
        var min = double.PositiveInfinity;
        foreach (var other in others)
        {
            min = Math.Min(min, Math.Abs(value - other));
        }

        return min;
    }

    /// <summary>Records one connector's claim on a shared box face for later distribution.</summary>
    private static void AddSlot(
        Dictionary<(LayoutBox Box, PortSide Side), List<FaceSlot>> groups,
        LayoutBox box,
        PortSide side,
        int connectionIndex,
        bool isSource,
        double counterpartCentre,
        bool reverse = false)
    {
        var key = (box, side);
        if (!groups.TryGetValue(key, out var slots))
        {
            slots = [];
            groups[key] = slots;
        }

        slots.Add(new FaceSlot(connectionIndex, isSource, counterpartCentre, reverse));
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
    /// <remarks>
    /// Also consumed internally by the <c>LayeredPipeline</c> unit's <c>PortDistributor</c> stage so
    /// shaped nodes routed through <c>LayeredLayoutAlgorithm</c> get the same connectable-extent
    /// restriction as <see cref="ConnectorRouter"/>-routed edges.
    /// </remarks>
    internal static IReadOnlyList<(double Lo, double Hi)> BuildUsableExtents(
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
    /// <remarks>
    /// Also consumed internally by the <c>LayeredPipeline</c> unit's <c>PortDistributor</c> stage.
    /// </remarks>
    internal static double TotalExtentLength(IReadOnlyList<(double Lo, double Hi)> extents)
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
    /// <remarks>
    /// Also consumed internally by the <c>LayeredPipeline</c> unit's <c>PortDistributor</c> stage.
    /// </remarks>
    internal static double CoordinateAtDistance(IReadOnlyList<(double Lo, double Hi)> extents, double distance)
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
            (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Box) * 397) ^ (int)obj.Side;
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
    /// Most shipped shapes use constant per-face projections, but <see cref="ProjectToSurface"/>
    /// intentionally receives the along-face coordinate so a shape can vary its projection across
    /// the face — <see cref="DiamondGeometry"/> does, with its sloped outline — without changing the
    /// router's calling contract.
    /// </remarks>
    /// <remarks>
    /// Also consumed internally by the <c>LayeredPipeline</c> unit's <c>PortDistributor</c> and
    /// <c>LongEdgeJoiner</c> stages so shaped nodes routed through <c>LayeredLayoutAlgorithm</c> get the
    /// same connectable-extent restriction and inward surface projection as
    /// <see cref="ConnectorRouter"/>-routed edges.
    /// </remarks>
    internal interface IBoxShapeGeometry
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
    internal abstract class BoxShapeGeometryBase : IBoxShapeGeometry
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
    internal sealed class RectangleGeometry : BoxShapeGeometryBase
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
    internal sealed class RoundedRectangleGeometry : BoxShapeGeometryBase
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
    internal sealed class NoteGeometry : BoxShapeGeometryBase
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
    internal sealed class FolderGeometry : BoxShapeGeometryBase
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
        internal static double ResolveFolderTabWidth(LayoutBox box) =>
            BoxMetrics.ResolveFolderTabWidth(box, Themes.Light);

        /// <summary>
        /// Resolves the folder-tab height used by routing, honoring a caller-supplied hint and
        /// otherwise falling back to the router's generic folder-tab height formula.
        /// </summary>
        /// <param name="box">Box whose folder-tab height is being resolved.</param>
        /// <returns>The non-negative folder-tab height, in logical pixels.</returns>
        internal static double ResolveFolderTabHeight(LayoutBox box) =>
            BoxMetrics.ResolveFolderTabHeight(box, Themes.Light);

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
    /// Diamond geometry: the shape is the rhombus through the four edge midpoints of the bounding box, so
    /// every face is connectable in full but the outline recedes linearly from the bounding-box face
    /// towards the vertex at the face midpoint. <see cref="ProjectToSurface"/> returns that distance — zero
    /// at the vertex and <c>depth / 2</c> at the corner — so a connector touches the drawn outline instead
    /// of floating off the bounding box (Rendering#58).
    /// </summary>
    internal sealed class DiamondGeometry : BoxShapeGeometryBase
    {
        public DiamondGeometry(LayoutBox box)
            : base(box)
        {
        }

        public override IReadOnlyList<(double Lo, double Hi)> GetConnectableExtents(PortSide side) => FullExtent(side);

        public override double ProjectToSurface(PortSide side, double alongAxisCoordinate)
        {
            // Top/bottom faces run along the width and recede over half the height; left/right faces run
            // along the height and recede over half the width. The offset is the proportional distance of
            // the coordinate from the face midpoint, clamped to the face so a coordinate marginally
            // outside it never yields a negative or oversized offset.
            var faceLength = BoxFaceLength(side);
            if (faceLength <= 0.0)
            {
                return 0.0;
            }

            var depth = side is PortSide.Left or PortSide.Right ? Width : Height;
            var fromMiddle = Math.Min(Math.Abs(alongAxisCoordinate - (faceLength / 2.0)), faceLength / 2.0);
            return (depth / faceLength) * fromMiddle;
        }
    }

    /// <summary>
    /// Resolves the shape geometry object used to interpret a box's connectable face extents and real
    /// outline projection.
    /// </summary>
    /// <remarks>
    /// Also consumed internally by the <c>LayeredPipeline</c> unit's <c>PortDistributor</c> and
    /// <c>LongEdgeJoiner</c> stages via <see cref="Engine.Layered.ShapeAnchorSupport.BuildAdapterBox"/>.
    /// </remarks>
    internal static IBoxShapeGeometry ResolveShapeGeometry(LayoutBox box) => box.Shape switch
    {
        BoxShape.Folder => new FolderGeometry(box),
        BoxShape.RoundedRectangle => new RoundedRectangleGeometry(box),
        BoxShape.Note => new NoteGeometry(box),
        BoxShape.Diamond => new DiamondGeometry(box),
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
    /// connectable extent (respecting the requested end clearance whenever that extent is long enough,
    /// or the smaller <see cref="PortCornerMargin"/> for a port the router slid along its face), then
    /// project it inward from the bounding box to the real drawn outline.
    /// </summary>
    private static Point2D ResolveAnchorPoint(LayoutBox box, PortSide side, double alongAxisCoordinate, double clearance, bool slid = false)
    {
        var geometry = ResolveShapeGeometry(box);
        var clamped = ClampToConnectableExtent(box, side, alongAxisCoordinate, slid ? PortCornerMargin : clearance);
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
    private static RouteResult RouteWaypoints(
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
                    source, target, obstacles, clearance, sourceSide, targetSide, costBands: null, softObstacles),
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
