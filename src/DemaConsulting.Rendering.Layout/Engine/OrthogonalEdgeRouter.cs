// <copyright file="OrthogonalEdgeRouter.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Layout.Engine;

/// <summary>
/// The outcome of a routing request: the computed waypoints and whether the route had to cross an
/// obstacle (i.e. no obstacle-free orthogonal path could be found).
/// </summary>
/// <param name="Waypoints">Ordered orthogonal waypoints from source to target.</param>
/// <param name="Crossed">
/// <see langword="true"/> when the router fell back to a path that may cross a box; this indicates a
/// degenerate (over-dense or overlapping) placement worth surfacing as a layout warning.
/// </param>
/// <param name="Cost">
/// The search cost of the returned path (path length, turn penalties, soft-obstacle and zone penalties),
/// letting a caller compare alternative routes produced by the same cost function. Zero for a
/// <paramref name="Crossed"/> fallback, whose cost is not meaningful.
/// </param>
/// <param name="ClearanceUsed">
/// The obstacle clearance, in logical pixels, at which the path was found (the requested clearance or
/// one of the progressively smaller fallback levels); zero for a <paramref name="Crossed"/> fallback.
/// </param>
internal readonly record struct RouteResult(
    IReadOnlyList<Point2D> Waypoints,
    bool Crossed,
    double Cost = 0.0,
    double ClearanceUsed = 0.0);

/// <summary>
/// A thin, direction-selective keep-out strip along one wall of a container box: a connector may cross
/// the wall (at right angles) but its segments may not run parallel to the wall, nor bend, within the
/// strip next to it. Used so connectors that pass through an enclosing container's border neither hug
/// that border nor cut across it at a shallow position.
/// </summary>
/// <param name="IsVertical">
/// <see langword="true"/> for a vertical wall (a left or right border, at X = <paramref name="Coordinate"/>),
/// <see langword="false"/> for a horizontal wall (a top or bottom border, at Y = <paramref name="Coordinate"/>).
/// </param>
/// <param name="Coordinate">The wall's position on its perpendicular axis, in logical pixels.</param>
/// <param name="Lo">Lower bound of the wall's extent along its own axis.</param>
/// <param name="Hi">Upper bound of the wall's extent along its own axis.</param>
/// <param name="OutwardIsPositive">
/// <see langword="true"/> when the container lies on the lower-coordinate side of the wall (a right or
/// bottom border), so the space outside it is on the higher-coordinate side. Used to recognize a corridor
/// between two containers (the outward side of one wall facing the outward side of the next).
/// </param>
internal readonly record struct RouterWall(bool IsVertical, double Coordinate, double Lo, double Hi, bool OutwardIsPositive = false);

/// <summary>
/// Optional quality hints that turn on the router's additional (soft) cost terms. A route computed
/// without hints is byte-for-byte the legacy route.
/// </summary>
internal sealed record RouteHints
{
    /// <summary>
    /// Gets the minimum straight leg, in logical pixels, wanted at the source end before the first
    /// bend; a bend placed closer than this to the source face is charged a soft penalty.
    /// </summary>
    public double SourceLeg { get; init; }

    /// <summary>
    /// Gets the minimum straight leg wanted at the target end before the final approach bends
    /// (typically end-marker length + bend radius + margin).
    /// </summary>
    public double TargetLeg { get; init; }

    /// <summary>
    /// Gets the comfortable leg at the source end; a bend between <see cref="SourceLeg"/> and this
    /// distance is charged a smaller soft penalty. Values below <see cref="SourceLeg"/> disable the tier.
    /// </summary>
    public double SourceComfort { get; init; }

    /// <summary>Gets the comfortable leg at the target end; see <see cref="SourceComfort"/>.</summary>
    public double TargetComfort { get; init; }

    /// <summary>
    /// Gets the container wall keep-out strips, or <see langword="null"/> for none. The strip width is
    /// twice the (current) obstacle clearance.
    /// </summary>
    public IReadOnlyList<RouterWall>? Walls { get; init; }

    /// <summary>
    /// Gets the extra cost charged each time the route crosses an already-routed (soft) connector at
    /// right angles; zero leaves a crossing as cheap as it always was.
    /// </summary>
    public double CrossingPenalty { get; init; }

    /// <summary>
    /// Gets the shortest interior segment (one between two bends) the route may contain; zero allows any.
    /// The first and the last segment are exempt (the end-leg zones govern them) and a segment may never
    /// reverse direction to satisfy the minimum. It is a hard constraint on a first pass over every
    /// clearance level, and is dropped only when that pass finds no route, so the router is never worse
    /// off than without it.
    /// </summary>
    public double MinInteriorSegment { get; init; }
}

/// <summary>
/// Describes where along one box face a connector may attach, so the router can choose the port
/// itself (sliding it along the face to line up with a free lane) instead of being handed a fixed
/// anchor.
/// </summary>
/// <remarks>
/// A range with <see cref="Side"/> set names the face (and the normal coordinate of its outline), the
/// permitted along-face intervals, a preferred position, and optional extra coordinates worth a grid
/// line. A range with no side wraps a single fixed point (used by the legacy fixed-anchor entry point).
/// </remarks>
internal sealed record PortRange
{
    /// <summary>Gets the face the port sits on, or <see langword="null"/> for a fixed, side-less point.</summary>
    public PortSide? Side { get; init; }

    /// <summary>
    /// Gets the coordinate of the face outline on the axis normal to the face (X for a left or right
    /// face, Y for a top or bottom face), after any shape projection.
    /// </summary>
    public double Face { get; init; }

    /// <summary>Gets the preferred along-face coordinate (absolute), used as the tie-break position.</summary>
    public double Preferred { get; init; }

    /// <summary>Gets the permitted absolute along-face intervals.</summary>
    public IReadOnlyList<(double Lo, double Hi)> Intervals { get; init; } = [];

    /// <summary>Gets extra absolute along-face coordinates that should become grid lines (candidate ports).</summary>
    public IReadOnlyList<double> Extra { get; init; } = [];

    /// <summary>Gets the fixed point for a side-less range.</summary>
    public Point2D? FixedPoint { get; init; }

    /// <summary>Creates a range wrapping one fixed anchor point.</summary>
    /// <param name="anchor">The anchor point.</param>
    /// <param name="side">The box side the anchor sits on, or <see langword="null"/> when unknown.</param>
    /// <returns>A range that admits exactly <paramref name="anchor"/>.</returns>
    public static PortRange Single(Point2D anchor, PortSide? side)
    {
        if (side is null)
        {
            return new PortRange { FixedPoint = anchor };
        }

        var horizontalNormal = side is PortSide.Left or PortSide.Right;
        var along = horizontalNormal ? anchor.Y : anchor.X;
        return new PortRange
        {
            Side = side,
            Face = horizontalNormal ? anchor.X : anchor.Y,
            Preferred = along,
            Intervals = [(along, along)],
        };
    }

    /// <summary>Gets whether the face normal is horizontal (a left or right face).</summary>
    public bool NormalIsX => Side is PortSide.Left or PortSide.Right;

    /// <summary>Returns the anchor point on the face at the given along-face coordinate.</summary>
    /// <param name="along">The absolute along-face coordinate.</param>
    /// <returns>The anchor point.</returns>
    public Point2D PointAt(double along) =>
        FixedPoint ?? (NormalIsX ? new Point2D(Face, along) : new Point2D(along, Face));
}

/// <summary>
/// A cost band: a region of the canvas where routing is cheaper (or dearer) than normal, used to bias
/// the router toward bundling wires along committed highways.
/// </summary>
/// <param name="IsHorizontal">True when the band spans a horizontal stripe (Y range), false for vertical (X range).</param>
/// <param name="Start">Lower bound of the band on its perpendicular axis, in logical pixels.</param>
/// <param name="End">Upper bound of the band on its perpendicular axis, in logical pixels.</param>
/// <param name="Multiplier">Cost factor applied to segments inside the band (0.6 cheaper, 1.0 neutral).</param>
internal readonly record struct CostBand(bool IsHorizontal, double Start, double End, double Multiplier);

/// <summary>
/// Routes orthogonal (right-angle) connector lines between two points while avoiding a set of
/// rectangular obstacles.
/// </summary>
/// <remarks>
/// <para>
/// The router builds a sparse "Hanan-style" routing grid whose vertical lines are the source and
/// target X coordinates plus each obstacle's left/right edges offset outward by a clearance, and
/// whose horizontal lines are the analogous Y coordinates. It then runs an A* search over the grid,
/// preferring straight runs via a turn penalty. Because the grid lines include the exact source and
/// target coordinates, the returned path starts at the source and ends at the target exactly, and
/// every segment is strictly horizontal or vertical.
/// </para>
/// <para>
/// The caller must exclude the boxes that own the source and target anchors from
/// <c>obstacles</c>; otherwise the very first segment would be blocked by the source's own box.
/// When no obstacle-free path exists, the router falls back to a simple L-shaped route so that a
/// result is always returned.
/// </para>
/// <para>
/// Optional <see cref="RouteHints"/> add soft end-leg zones (bends too close to a face are charged),
/// direction-selective wall keep-out strips, a crossing penalty and a hard minimum interior segment
/// (so no tiny jog is drawn and no run doubles back), and <see cref="RoutePorts"/> lets the
/// router choose where along each face the connector attaches. Without hints the route is the legacy
/// route.
/// </para>
/// </remarks>
internal static class OrthogonalEdgeRouter
{
    /// <summary>
    /// One node of the A* search: a grid position, the axis of the last move, and (only when a minimum
    /// interior segment is enforced) the pending-run marker and the direction sign of the last move.
    /// </summary>
    /// <param name="I">X grid index.</param>
    /// <param name="J">Y grid index.</param>
    /// <param name="D">Axis of the last move.</param>
    /// <param name="Run">
    /// The grid index along the last move's axis where the current run began while it is still shorter
    /// than the minimum interior segment, or -1 when the run is long enough or exempt.
    /// </param>
    /// <param name="Sign">The sign of the last move along its axis; 0 when no minimum is enforced.</param>
    private readonly record struct SearchState(int I, int J, Dir D, int Run, int Sign);

    /// <summary>Direction of travel along a segment, used for turn-penalty accounting.</summary>
    private enum Dir
    {
        /// <summary>No prior direction (search start).</summary>
        None,

        /// <summary>Horizontal travel.</summary>
        Horizontal,

        /// <summary>Vertical travel.</summary>
        Vertical,

        /// <summary>Terminal state reached when a goal node's own extra cost has been added.</summary>
        Done,
    }

    /// <summary>
    /// Computes an orthogonal route from <paramref name="source"/> to <paramref name="target"/>
    /// avoiding the interiors of the given obstacles.
    /// </summary>
    /// <param name="source">Start point (typically an anchor on the source box boundary).</param>
    /// <param name="target">End point (typically an anchor on the target box boundary).</param>
    /// <param name="obstacles">
    /// Rectangles to route around, excluding the boxes that own the source and target anchors.
    /// </param>
    /// <param name="clearance">Minimum gap kept between routed segments and obstacles.</param>
    /// <param name="sourceSide">
    /// Optional box side the source anchor sits on. When given, the route leaves the source with a
    /// short stub perpendicular to that side before routing freely, so connectors exit boxes cleanly.
    /// </param>
    /// <param name="targetSide">Optional box side the target anchor sits on; see <paramref name="sourceSide"/>.</param>
    /// <param name="costBands">Optional cost bands biasing the route toward highway corridors; null leaves cost neutral.</param>
    /// <param name="softObstacles">
    /// Optional rectangles (typically already-routed connector segments) that the route should prefer
    /// to avoid but is never hard-blocked by.
    /// </param>
    /// <param name="hints">Optional quality hints (see <see cref="RouteHints"/>); <see langword="null"/> gives the legacy route.</param>
    /// <returns>
    /// An ordered list of waypoints beginning with <paramref name="source"/> and ending with
    /// <paramref name="target"/>. Consecutive waypoints always share an X or a Y coordinate.
    /// </returns>
    public static IReadOnlyList<Point2D> Route(
        Point2D source,
        Point2D target,
        IReadOnlyList<Rect> obstacles,
        double clearance,
        PortSide? sourceSide = null,
        PortSide? targetSide = null,
        IReadOnlyList<CostBand>? costBands = null,
        IReadOnlyList<Rect>? softObstacles = null,
        RouteHints? hints = null) =>
        RouteWithStatus(source, target, obstacles, clearance, sourceSide, targetSide, costBands, softObstacles, hints).Waypoints;

    /// <summary>
    /// Computes an orthogonal route and reports whether it had to cross an obstacle. The route is
    /// attempted with progressively smaller clearances; only when no obstacle-free orthogonal path
    /// exists at any clearance does it fall back to a (possibly crossing) L-shape, in which case
    /// <see cref="RouteResult.Crossed"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="source">Start point (typically an anchor on the source box boundary).</param>
    /// <param name="target">End point (typically an anchor on the target box boundary).</param>
    /// <param name="obstacles">Rectangles to route around, excluding the source and target boxes.</param>
    /// <param name="clearance">Preferred gap between routed segments and obstacles.</param>
    /// <param name="sourceSide">Optional box side the source anchor sits on (adds a perpendicular stub).</param>
    /// <param name="targetSide">Optional box side the target anchor sits on (adds a perpendicular stub).</param>
    /// <param name="costBands">Optional cost bands biasing the route toward highway corridors; null leaves cost neutral.</param>
    /// <param name="softObstacles">
    /// Optional rectangles (typically already-routed connector segments) that the route should prefer
    /// to avoid but is never hard-blocked by. Unlike <paramref name="obstacles"/>, a segment may still
    /// cross a soft obstacle when doing so is the only (or cheapest) way to reach the target; this keeps
    /// a shared target/source face reachable even when every other connector converging on it already
    /// occupies the same short approach corridor.
    /// </param>
    /// <param name="hints">
    /// Optional quality hints (minimum end legs, wall keep-out strips, crossing penalty). When
    /// <see langword="null"/> the route is the legacy route.
    /// </param>
    /// <returns>
    /// The waypoints, a flag indicating whether the route crosses an obstacle, and the cost and
    /// clearance level at which the route was found.
    /// </returns>
    public static RouteResult RouteWithStatus(
        Point2D source,
        Point2D target,
        IReadOnlyList<Rect> obstacles,
        double clearance,
        PortSide? sourceSide = null,
        PortSide? targetSide = null,
        IReadOnlyList<CostBand>? costBands = null,
        IReadOnlyList<Rect>? softObstacles = null,
        RouteHints? hints = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(obstacles);

        return Solve(
            PortRange.Single(source, sourceSide),
            PortRange.Single(target, targetSide),
            obstacles,
            clearance,
            costBands,
            softObstacles,
            hints);
    }

    /// <summary>
    /// Computes an orthogonal route between two <em>port ranges</em>: the router chooses where along
    /// each face the connector attaches (any grid line inside the permitted intervals, plus the
    /// preferred position), so it can slide a port to line up with a free lane and draw one straight
    /// connector where fixed anchors would force a jog. Otherwise identical to
    /// <see cref="RouteWithStatus"/>.
    /// </summary>
    /// <param name="source">The source port range (must name a side).</param>
    /// <param name="target">The target port range (must name a side).</param>
    /// <param name="obstacles">Rectangles to route around, including the source and target boxes.</param>
    /// <param name="clearance">Preferred gap between routed segments and obstacles.</param>
    /// <param name="costBands">Optional cost bands biasing the route toward highway corridors.</param>
    /// <param name="softObstacles">Optional rectangles the route prefers to avoid but may cross.</param>
    /// <param name="hints">Optional quality hints; see <see cref="RouteHints"/>.</param>
    /// <returns>The waypoints (starting and ending on the chosen ports), crossing flag, cost and clearance.</returns>
    public static RouteResult RoutePorts(
        PortRange source,
        PortRange target,
        IReadOnlyList<Rect> obstacles,
        double clearance,
        IReadOnlyList<CostBand>? costBands = null,
        IReadOnlyList<Rect>? softObstacles = null,
        RouteHints? hints = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(obstacles);

        return Solve(source, target, obstacles, clearance, costBands, softObstacles, hints);
    }

    /// <summary>
    /// Shared implementation of <see cref="RouteWithStatus"/> and <see cref="RoutePorts"/>: steps off each
    /// end with a perpendicular stub, then searches the routing grid (preferring the largest clearance)
    /// from every candidate source port to every candidate target port.
    /// </summary>
    private static RouteResult Solve(
        PortRange source,
        PortRange target,
        IReadOnlyList<Rect> obstacles,
        double clearance,
        IReadOnlyList<CostBand>? costBands,
        IReadOnlyList<Rect>? softObstacles,
        RouteHints? hints)
    {
        // Step off each anchor's box edge with a perpendicular stub so connectors enter and leave
        // boxes at right angles instead of sliding along the edge. The stub is capped so that two
        // stubs facing each other across a small gap meet at the midline instead of overshooting
        // (which would force a back-and-forth jog right at the arrowhead).
        // FUTURE (Phase 14c cleanup): replace this magic 8.0 with theme.ConnectorApproachZone.
        var stub = clearance + 8.0;
        var sourceReference = source.PointAt(source.Preferred);
        var targetReference = target.PointAt(target.Preferred);
        var sourceStub = StubLength(sourceReference, source.Side, targetReference, stub);
        var targetStub = StubLength(targetReference, target.Side, sourceReference, stub);
        var routeSource = StepOff(sourceReference, source.Side, sourceStub);
        var routeTarget = StepOff(targetReference, target.Side, targetStub);

        // Try to find an obstacle-free orthogonal path, preferring the largest clearance that works.
        // With a minimum interior segment hint, a first pass requires the route at every clearance
        // level to honor it; only when no level yields a route is the requirement dropped, so the
        // hint can never turn a routable connector into a crossing fallback.
        var minSegmentPasses = hints is { MinInteriorSegment: > 0.0 } ? new[] { true, false } : new[] { false };
        foreach (var enforceMinSegment in minSegmentPasses)
        {
            foreach (var c in ClearanceLevels(clearance))
            {
                var attempt = TrySolveAtClearance(
                    source, target, obstacles, c, costBands, softObstacles, hints, enforceMinSegment, (routeSource, routeTarget), (sourceStub, targetStub));
                if (attempt is not null)
                {
                    return attempt.Value;
                }
            }
        }

        // No clean path at any clearance: fall back to the least-bad L-shape (it may cross a box).
        var fallback = BuildObstacleAwareFallback(routeSource, routeTarget, obstacles);
        return new RouteResult(Finalize(sourceReference, targetReference, source.Side, target.Side, fallback), Crossed: true);
    }

    /// <summary>
    /// Builds the routing grid for one clearance level and searches it from every candidate source port
    /// to every candidate target port.
    /// </summary>
    /// <returns>The route found at <paramref name="c"/>, or <see langword="null"/> when there is none.</returns>
    private static RouteResult? TrySolveAtClearance(
        PortRange source,
        PortRange target,
        IReadOnlyList<Rect> obstacles,
        double c,
        IReadOnlyList<CostBand>? costBands,
        IReadOnlyList<Rect>? softObstacles,
        RouteHints? hints,
        bool enforceMinSegment,
        (Point2D Source, Point2D Target) route,
        (double Source, double Target) stubs)
    {
        var xs = new SortedSet<double> { route.Source.X, route.Target.X };
        var ys = new SortedSet<double> { route.Source.Y, route.Target.Y };
        var extras = hints is null ? null : BuildExtras(source, target, hints, c, xs, ys, enforceMinSegment);
        AddPortLines(source, xs, ys);
        AddPortLines(target, xs, ys);

        var xAxis = BuildAxis(xs, obstacles, c, horizontal: true, softObstacles);
        var yAxis = BuildAxis(ys, obstacles, c, horizontal: false, softObstacles);

        var starts = BuildTerminals(source, stubs.Source, xAxis, yAxis);
        var goals = BuildTerminals(target, stubs.Target, xAxis, yAxis);
        extras?.SetTerminals(starts, goals);

        var found = AStar(xAxis, yAxis, starts, goals, obstacles, c, costBands, softObstacles, extras);
        if (found.Path is null)
        {
            return null;
        }

        var first = found.Path[0];
        var last = found.Path[^1];
        var sourceAnchor = starts.First(t => Math.Abs(t.StubEnd.X - first.X) < 1e-9 && Math.Abs(t.StubEnd.Y - first.Y) < 1e-9).Anchor;
        var targetAnchor = goals.First(t => Math.Abs(t.StubEnd.X - last.X) < 1e-9 && Math.Abs(t.StubEnd.Y - last.Y) < 1e-9).Anchor;
        return new RouteResult(
            Finalize(sourceAnchor, targetAnchor, source.Side, target.Side, found.Path),
            Crossed: false,
            found.Cost,
            c);
    }

    /// <summary>
    /// Yields the clearances to attempt, from the requested value down to zero, so the router prefers
    /// a spacious route but still hugs box edges (clearance 0) rather than crossing them.
    /// </summary>
    private static IEnumerable<double> ClearanceLevels(double clearance)
    {
        var seen = new HashSet<double>();
        foreach (var c in new[] { clearance, clearance / 2.0, clearance / 4.0, 0.0 })
        {
            var v = Math.Max(0.0, c);
            if (seen.Add(v))
            {
                yield return v;
            }
        }
    }

    /// <summary>
    /// Re-attaches the original anchor points outside their stubs and simplifies the path.
    /// </summary>
    private static IReadOnlyList<Point2D> Finalize(
        Point2D source,
        Point2D target,
        PortSide? sourceSide,
        PortSide? targetSide,
        IReadOnlyList<Point2D> path)
    {
        var full = new List<Point2D>();
        if (sourceSide is not null)
        {
            full.Add(source);
        }

        full.AddRange(path);

        if (targetSide is not null)
        {
            full.Add(target);
        }

        return RemovePointRevisits(Simplify(full));
    }

    /// <summary>
    /// Returns the point offset from <paramref name="anchor"/> by <paramref name="distance"/> in the
    /// outward-normal direction of <paramref name="side"/>, or the anchor unchanged when no side.
    /// </summary>
    private static Point2D StepOff(Point2D anchor, PortSide? side, double distance) => side switch
    {
        PortSide.Top => new Point2D(anchor.X, anchor.Y - distance),
        PortSide.Bottom => new Point2D(anchor.X, anchor.Y + distance),
        PortSide.Left => new Point2D(anchor.X - distance, anchor.Y),
        PortSide.Right => new Point2D(anchor.X + distance, anchor.Y),
        _ => anchor,
    };

    /// <summary>
    /// Returns the stub length to step off <paramref name="anchor"/>'s edge: the base length, but
    /// capped to half the distance to <paramref name="other"/> measured along the side's outward
    /// normal when <paramref name="other"/> lies in that direction. This keeps two stubs that face
    /// each other across a narrow gap from overshooting past the midline (which produces a visible
    /// reversal at the connector's end). When the projection is exactly zero (ports at the same
    /// coordinate on facing sides, e.g. touching-edge boxes), the stub collapses to zero so the
    /// connector routes directly between the ports without stepping into the opposite box.
    /// </summary>
    private static double StubLength(Point2D anchor, PortSide? side, Point2D other, double baseStub)
    {
        var projection = side switch
        {
            PortSide.Top => anchor.Y - other.Y,
            PortSide.Bottom => other.Y - anchor.Y,
            PortSide.Left => anchor.X - other.X,
            PortSide.Right => other.X - anchor.X,
            _ => double.PositiveInfinity,
        };

        return projection >= 0 ? Math.Min(baseStub, projection / 2.0) : baseStub;
    }

    /// <summary>
    /// Completes the sorted, de-duplicated set of grid coordinates for one axis (already holding the
    /// endpoint and any quality-hint lines): each obstacle's near/far edge offset outward by the
    /// clearance, plus the same offset for any <paramref name="softObstacles"/> so the search has a
    /// candidate lane on either side of an already-routed line to prefer when one is available.
    /// </summary>
    private static double[] BuildAxis(
        SortedSet<double> set,
        IReadOnlyList<Rect> obstacles,
        double clearance,
        bool horizontal,
        IReadOnlyList<Rect>? softObstacles = null)
    {
        foreach (var r in obstacles)
        {
            if (horizontal)
            {
                set.Add(r.X - clearance);
                set.Add(r.X + r.Width + clearance);
            }
            else
            {
                set.Add(r.Y - clearance);
                set.Add(r.Y + r.Height + clearance);
            }
        }

        if (softObstacles is not null)
        {
            foreach (var r in softObstacles)
            {
                if (horizontal)
                {
                    set.Add(r.X - clearance);
                    set.Add(r.X + r.Width + clearance);
                }
                else
                {
                    set.Add(r.Y - clearance);
                    set.Add(r.Y + r.Height + clearance);
                }
            }
        }

        return [.. set];
    }

    /// <summary>
    /// Adds the grid lines a port range can attach on: its preferred position and any extra candidate
    /// coordinates, on the axis running along the face.
    /// </summary>
    private static void AddPortLines(PortRange range, SortedSet<double> xs, SortedSet<double> ys)
    {
        if (range.Side is null)
        {
            return;
        }

        var along = range.NormalIsX ? ys : xs;
        along.Add(range.Preferred);
        foreach (var coordinate in range.Extra)
        {
            along.Add(coordinate);
        }
    }

    /// <summary>
    /// One candidate attachment of a connector: the anchor on the box face, the grid node at the end
    /// of its stub, and a small extra cost that makes the search prefer the range's preferred position
    /// when everything else is equal.
    /// </summary>
    private readonly record struct Terminal(Point2D Anchor, Point2D StubEnd, int I, int J, double Extra)
    {
        /// <summary>
        /// Gets the axis of the stub joining the anchor to the stub end, or <see cref="Dir.None"/> when the
        /// stub has no length (a fixed point or touching faces).
        /// </summary>
        public Dir StubAxis
        {
            get
            {
                if (Math.Abs(Anchor.X - StubEnd.X) < 1e-9 && Math.Abs(Anchor.Y - StubEnd.Y) < 1e-9)
                {
                    return Dir.None;
                }

                return Math.Abs(Anchor.X - StubEnd.X) < 1e-9 ? Dir.Vertical : Dir.Horizontal;
            }
        }
    }

    /// <summary>
    /// Enumerates the candidate terminals of a port range: every grid line along the face that lies
    /// within one of the range's permitted intervals (always including the preferred position of a
    /// fixed range).
    /// </summary>
    private static List<Terminal> BuildTerminals(PortRange range, double stubLength, double[] xs, double[] ys)
    {
        var terminals = new List<Terminal>();
        if (range.Side is null)
        {
            var fixedPoint = range.FixedPoint!;
            terminals.Add(new Terminal(fixedPoint, fixedPoint, IndexOf(xs, fixedPoint.X), IndexOf(ys, fixedPoint.Y), 0.0));
            return terminals;
        }

        var alongAxis = range.NormalIsX ? ys : xs;
        var candidates = alongAxis.Where(along => InAnyInterval(range.Intervals, along)).ToList();

        // A very long face crossed by many obstacle edges would otherwise offer hundreds of candidate
        // ports; keep the ones nearest the preferred position (and any explicitly requested lane).
        if (candidates.Count > MaxPortCandidates)
        {
            candidates = candidates
                .OrderBy(along => range.Extra.Contains(along) ? -1.0 : Math.Abs(along - range.Preferred))
                .Take(MaxPortCandidates)
                .ToList();
        }

        foreach (var along in candidates)
        {
            var anchor = range.PointAt(along);
            var stepOff = StepOff(anchor, range.Side, stubLength);
            terminals.Add(new Terminal(
                anchor,
                stepOff,
                IndexOf(xs, stepOff.X),
                IndexOf(ys, stepOff.Y),
                PortDisplacementCost * Math.Abs(along - range.Preferred)));
        }

        return terminals;
    }

    /// <summary>Most candidate ports tried along one face.</summary>
    private const int MaxPortCandidates = 40;

    /// <summary>Returns whether <paramref name="value"/> lies inside any of the closed intervals.</summary>
    private static bool InAnyInterval(IReadOnlyList<(double Lo, double Hi)> intervals, double value)
    {
        foreach (var (lo, hi) in intervals)
        {
            if (value >= lo - 1e-9 && value <= hi + 1e-9)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Cost per pixel of sliding a port away from its preferred position. Slightly above one, so a port
    /// only moves when that removes bends (sliding purely to shorten the path never pays for itself) and
    /// the not-moved, centered anchor wins every tie.
    /// </summary>
    private const double PortDisplacementCost = 1.1;

    /// <summary>
    /// Returns the index of the grid line equal to <paramref name="value"/>. The value is always
    /// present because the axis was built to include it.
    /// </summary>
    private static int IndexOf(double[] axis, double value)
    {
        for (var i = 0; i < axis.Length; i++)
        {
            if (Math.Abs(axis[i] - value) < 1e-9)
            {
                return i;
            }
        }

        // Should never happen: endpoint coordinates are always added to the axis.
        return 0;
    }

    /// <summary>Soft penalty charged for a bend inside an end's minimum leg zone.</summary>
    private const double EndLegPenalty = 100.0;

    /// <summary>Soft penalty charged for a bend inside an end's comfort leg zone (beyond the minimum).</summary>
    private const double ComfortLegPenalty = 30.0;

    /// <summary>Wall keep-out strip width as a multiple of the current obstacle clearance.</summary>
    private const double WallMarginFactor = 2.0;

    /// <summary>Largest fraction of a move's length that keeping away from the walls can discount.</summary>
    private const double WallDistanceRewardFraction = 0.05;

    /// <summary>Distance from a wall, as a multiple of the clearance, beyond which no further reward is earned.</summary>
    private const double WallDistanceRewardScale = 5.0;

    /// <summary>One end's leg zone: lateral moves close to the face are charged a soft penalty.</summary>
    private sealed record ZoneSpec(bool NormalIsX, double Face, int Outward, double Leg, double Comfort);

    /// <summary>A wall keep-out strip with its margin at the current clearance level.</summary>
    private sealed class WallSpec
    {
        public WallSpec(RouterWall wall, double margin)
        {
            IsVertical = wall.IsVertical;
            Coordinate = wall.Coordinate;
            Lo = wall.Lo;
            Hi = wall.Hi;
            OutwardIsPositive = wall.OutwardIsPositive;
            Margin = margin;
        }

        public bool IsVertical { get; }

        public double Coordinate { get; }

        public double Lo { get; }

        public double Hi { get; }

        public bool OutwardIsPositive { get; }

        public double Margin { get; }
    }

    /// <summary>The extra, hint-driven search terms resolved for one clearance level.</summary>
    private sealed class SearchExtras
    {
        public SearchExtras(IReadOnlyList<ZoneSpec> zones, IReadOnlyList<WallSpec> walls, double crossingPenalty, double clearance, double minSegment)
        {
            Zones = zones;
            Walls = walls;
            CrossingPenalty = crossingPenalty;
            MinSegment = minSegment;
            _rewardDistance = WallDistanceRewardScale * clearance;
        }

        /// <summary>Gets the minimum interior segment length enforced by the search; zero for none.</summary>
        public double MinSegment { get; }

        public IReadOnlyList<ZoneSpec> Zones { get; }

        public IReadOnlyList<WallSpec> Walls { get; }

        public double CrossingPenalty { get; }

        private readonly double _rewardDistance;

        /// <summary>
        /// Records the connector's own step-off nodes. A move leaving or reaching one of them is exempt
        /// from the wall strips, so a port sitting close to its own container's wall can still get going.
        /// </summary>
        public void SetTerminals(IReadOnlyList<Terminal> starts, IReadOnlyList<Terminal> goals)
        {
            foreach (var terminal in starts.Concat(goals))
            {
                _terminalNodes.Add((terminal.I, terminal.J));
            }
        }

        private readonly HashSet<(int I, int J)> _terminalNodes = [];

        /// <summary>
        /// Returns whether the move runs parallel to, or bends inside, a wall strip. A move across a
        /// wall (perpendicular to it) is never blocked, and neither is a move touching one of the
        /// connector's own step-off nodes.
        /// </summary>
        public bool WallBlocked(double[] xs, double[] ys, int i1, int j1, int i2, int j2)
        {
            if (_terminalNodes.Contains((i1, j1)) || _terminalNodes.Contains((i2, j2)))
            {
                return false;
            }

            var vertical = i1 == i2;
            var coordinate = vertical ? xs[i1] : ys[j1];
            var lo = vertical ? Math.Min(ys[j1], ys[j2]) : Math.Min(xs[i1], xs[i2]);
            var hi = vertical ? Math.Max(ys[j1], ys[j2]) : Math.Max(xs[i1], xs[i2]);
            foreach (var wall in Walls)
            {
                if (wall.IsVertical == vertical
                    && Math.Abs(coordinate - wall.Coordinate) < wall.Margin
                    && Math.Max(lo, wall.Lo) < Math.Min(hi, wall.Hi))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns a cost multiplier slightly below one for a move running parallel to nearby walls,
        /// the further it stays from the nearest one (up to a few clearances), so otherwise equally good
        /// lanes resolve to the middle of a corridor instead of hugging a border.
        /// </summary>
        public double WallDistanceMultiplier(double[] xs, double[] ys, int i1, int j1, int i2, int j2)
        {
            if (Walls.Count == 0 || _rewardDistance <= 0.0)
            {
                return 1.0;
            }

            var vertical = i1 == i2;
            var coordinate = vertical ? xs[i1] : ys[j1];
            var lo = vertical ? Math.Min(ys[j1], ys[j2]) : Math.Min(xs[i1], xs[i2]);
            var hi = vertical ? Math.Max(ys[j1], ys[j2]) : Math.Max(xs[i1], xs[i2]);
            var nearest = double.PositiveInfinity;
            foreach (var wall in Walls)
            {
                if (wall.IsVertical == vertical && Math.Max(lo, wall.Lo) < Math.Min(hi, wall.Hi))
                {
                    nearest = Math.Min(nearest, Math.Abs(coordinate - wall.Coordinate));
                }
            }

            if (double.IsInfinity(nearest))
            {
                return 1.0;
            }

            return 1.0 - (WallDistanceRewardFraction * Math.Min(1.0, nearest / _rewardDistance));
        }

        /// <summary>
        /// Returns the soft penalty for a move that is lateral to an end's face normal and lies within
        /// that end's leg zone (a bend too close to the face).
        /// </summary>
        public double ZonePenalty(double[] xs, double[] ys, int i1, int j1, bool vertical)
        {
            var penalty = 0.0;
            foreach (var zone in Zones)
            {
                // A move is lateral to the end when it runs along the face (perpendicular to its normal).
                if (zone.NormalIsX != vertical)
                {
                    continue;
                }

                var distance = ((zone.NormalIsX ? xs[i1] : ys[j1]) - zone.Face) * zone.Outward;
                if (distance <= 1e-9)
                {
                    continue;
                }

                if (distance < zone.Leg - 1e-6)
                {
                    penalty += EndLegPenalty;
                }
                else if (distance < zone.Comfort - 1e-6)
                {
                    penalty += ComfortLegPenalty;
                }
            }

            return penalty;
        }
    }

    /// <summary>
    /// Resolves the hint-driven search terms for one clearance level and registers the extra grid lines
    /// they need (leg-zone boundaries, wall strip edges and corridor centers) on the axis sets.
    /// </summary>
    private static SearchExtras BuildExtras(
        PortRange source,
        PortRange target,
        RouteHints hints,
        double clearance,
        SortedSet<double> xs,
        SortedSet<double> ys,
        bool enforceMinSegment)
    {
        var zones = new List<ZoneSpec>();
        AddZone(source, hints.SourceLeg, hints.SourceComfort, zones, xs, ys);
        AddZone(target, hints.TargetLeg, hints.TargetComfort, zones, xs, ys);

        var walls = new List<WallSpec>();
        var margin = WallMarginFactor * clearance;
        if (hints.Walls is not null && margin > 0.0)
        {
            foreach (var wall in hints.Walls)
            {
                walls.Add(new WallSpec(wall, margin));
                var axis = wall.IsVertical ? xs : ys;
                axis.Add(wall.Coordinate - margin);
                axis.Add(wall.Coordinate + margin);
            }

            // A lane centered in the corridor between two containers (the outside of one wall facing the
            // outside of the next) is worth a grid line of its own.
            foreach (var wall in walls)
            {
                if (!wall.OutwardIsPositive)
                {
                    continue;
                }

                WallSpec? next = null;
                foreach (var other in walls)
                {
                    if (other.IsVertical == wall.IsVertical
                        && !other.OutwardIsPositive
                        && other.Coordinate > wall.Coordinate + (2.0 * margin)
                        && Math.Max(wall.Lo, other.Lo) < Math.Min(wall.Hi, other.Hi)
                        && (next is null || other.Coordinate < next.Coordinate))
                    {
                        next = other;
                    }
                }

                if (next is not null)
                {
                    (wall.IsVertical ? xs : ys).Add((wall.Coordinate + next.Coordinate) / 2.0);
                }
            }
        }

        return new SearchExtras(zones, walls, hints.CrossingPenalty, clearance, enforceMinSegment ? hints.MinInteriorSegment : 0.0);
    }

    /// <summary>Registers one end's leg zone and the grid lines at its boundaries.</summary>
    private static void AddZone(
        PortRange range,
        double leg,
        double comfort,
        List<ZoneSpec> zones,
        SortedSet<double> xs,
        SortedSet<double> ys)
    {
        if (range.Side is null || (leg <= 0.0 && comfort <= 0.0))
        {
            return;
        }

        var outward = range.Side is PortSide.Right or PortSide.Bottom ? 1 : -1;
        var axis = range.NormalIsX ? xs : ys;
        if (leg > 0.0)
        {
            axis.Add(range.Face + (outward * leg));
        }

        if (comfort > leg)
        {
            axis.Add(range.Face + (outward * comfort));
        }

        zones.Add(new ZoneSpec(range.NormalIsX, range.Face, outward, leg, comfort));
    }

    /// <summary>
    /// Runs an A* search over the grid from any of the <paramref name="starts"/> to any of the
    /// <paramref name="goals"/>, returning the sequence of grid points from the chosen start to the
    /// chosen goal together with its cost, or a <see langword="null"/> path when no obstacle-free path
    /// exists.
    /// </summary>
    private static (List<Point2D>? Path, double Cost) AStar(
        double[] xs,
        double[] ys,
        IReadOnlyList<Terminal> starts,
        IReadOnlyList<Terminal> goals,
        IReadOnlyList<Rect> obstacles,
        double clearance,
        IReadOnlyList<CostBand>? costBands,
        IReadOnlyList<Rect>? softObstacles = null,
        SearchExtras? extras = null)
    {
        var nx = xs.Length;
        var ny = ys.Length;

        // A search over a single goal with no extra cost ends the moment its goal node is first
        // dequeued (the legacy behavior); several goals, or goals with an extra cost, need a
        // terminal "done" state carrying the goal's extra cost so the cheapest total wins.
        var useDone = goals.Count > 1 || goals[0].Extra > 0.0;

        // Visited cost keyed by the full search state so straight-through and turning arrivals differ
        // and, when a minimum interior segment is enforced, so a short run still being measured is told
        // apart from a long enough one.
        var minSegment = extras?.MinSegment ?? 0.0;
        var best = new Dictionary<SearchState, double>();
        var cameFrom = new Dictionary<SearchState, SearchState>();
        var open = new MinHeap<SearchState, double>();

        foreach (var start in starts)
        {
            var startState = new SearchState(start.I, start.J, Dir.None, -1, 0);
            if (best.TryGetValue(startState, out var existingStart) && existingStart <= start.Extra)
            {
                continue;
            }

            best[startState] = start.Extra;
            open.Enqueue(startState, start.Extra + Heuristic(xs, ys, start.I, start.J, goals));
        }

        // Turn penalty expressed in pixels; comparable to a short straight run so detours that
        // remove a bend are preferred only when not much longer.
        const double TurnPenalty = 20.0;

        // Soft-obstacle cost is split into a small flat base (so a trivial, few-pixel overlap stays
        // cheap enough that a shared approach corridor is never worth a pointless detour) plus a
        // per-unit-length term proportional to how much of the candidate move actually overlaps the
        // soft obstacle. A flat, length-independent penalty was tried first and found insufficient: on
        // a sparse narrow-gap grid a connector's entire multi-hundred-pixel corridor can collapse into
        // a single grid move, so a flat cost priced a 500px visual overlap identically to a trivial
        // one — cheaper than the roughly fixed cost of detouring to a nearby alternate lane, so A*
        // always kept the long overlap no matter how visually merged the result looked. Scaling the
        // cost with the overlap length keeps short, incidental overlaps (and the legitimate
        // endpoint-adjacent approach corridors that never become soft obstacles in the first place —
        // see <c>AddLineObstacles</c> in <c>ConnectorRouter</c>) cheap, while making an extended
        // overlap cost substantially more than a bounded-cost lane change, so the search prefers the
        // detour once the overlap grows long enough to matter.
        const double SoftObstacleBaseCost = 10.0;
        const double SoftObstaclePerUnitLengthCost = 1.0;

        // Resolved once outside the neighbor loop so a null softObstacles argument does not allocate a
        // fresh empty array on every A* iteration.
        var resolvedSoftObstacles = softObstacles ?? [];

        while (open.Count > 0)
        {
            var current = open.Dequeue();
            var (ci, cj, cd) = (current.I, current.J, current.D);
            var g = best[current];

            if (cd == Dir.Done)
            {
                return (Reconstruct(xs, ys, cameFrom, current), g);
            }

            var goal = FindGoal(goals, ci, cj);
            if (goal is not null && !EndsOnShortInteriorRun(goal.Value, current))
            {
                if (!useDone)
                {
                    return (Reconstruct(xs, ys, cameFrom, current), g);
                }

                var doneState = new SearchState(ci, cj, Dir.Done, -1, 0);
                var total = g + goal.Value.Extra;
                if (!best.TryGetValue(doneState, out var existingDone) || total < existingDone)
                {
                    best[doneState] = total;
                    cameFrom[doneState] = current;
                    open.Enqueue(doneState, total);
                }
            }

            // The axis of the stub this search leaves from (start state only, and only when a minimum
            // interior segment is enforced): a first move across that axis starts an interior run.
            var startAxis = minSegment > 0.0 && cd == Dir.None ? StartStubAxis(starts, ci, cj) : Dir.None;

            foreach (var (ni, nj, nd) in Neighbors(ci, cj, nx, ny))
            {
                // With a minimum interior segment, a turn may only complete a segment that is long
                // enough (a segment that continues the stub at either end is part of the end leg, whose
                // length the end-leg zones govern, so it is exempt) and a move may never double back
                // along its own axis.
                var turning = cd != Dir.None && cd != nd;
                var beginsRun = turning || (startAxis != Dir.None && startAxis != nd);
                var sign = minSegment > 0.0 ? MoveSign(xs, ys, ci, cj, ni, nj, nd) : 0;
                if (minSegment > 0.0 && ((turning && current.Run >= 0) || (cd == nd && current.Sign == -sign)))
                {
                    continue;
                }

                // Skip moves whose segment passes within the clearance of an obstacle.
                if (SegmentBlocked(xs, ys, ci, cj, ni, nj, obstacles, clearance))
                {
                    continue;
                }

                if (extras is not null && extras.WallBlocked(xs, ys, ci, cj, ni, nj))
                {
                    continue;
                }

                var stepLength = nd == Dir.Horizontal
                    ? Math.Abs(xs[ni] - xs[ci])
                    : Math.Abs(ys[nj] - ys[cj]);
                var bandMultiplier = SegmentCostMultiplier(xs, ys, ci, cj, ni, nj, costBands);
                var channelMultiplier = extras is null ? 1.0 : extras.WallDistanceMultiplier(xs, ys, ci, cj, ni, nj);
                var turnCost = turning ? TurnPenalty : 0.0;
                var softOverlap = SoftObstacleOverlapLength(xs, ys, ci, cj, ni, nj, resolvedSoftObstacles);
                var softCost = softOverlap > 0.0
                    ? SoftObstacleBaseCost + (softOverlap * SoftObstaclePerUnitLengthCost)
                    : 0.0;
                var extraCost = 0.0;
                if (extras is not null)
                {
                    extraCost += extras.ZonePenalty(xs, ys, ci, cj, nd == Dir.Vertical);
                    if (extras.CrossingPenalty > 0.0 && softOverlap > 0.0)
                    {
                        extraCost += extras.CrossingPenalty * SoftCrossingCount(xs, ys, ci, cj, ni, nj, resolvedSoftObstacles);
                    }
                }

                var tentative = g + (stepLength * bandMultiplier * channelMultiplier) + turnCost + softCost + extraCost;

                var neighborState = new SearchState(ni, nj, nd, NextRun(xs, ys, current, ni, nj, nd, beginsRun, minSegment), sign);
                if (best.TryGetValue(neighborState, out var existing) && tentative >= existing)
                {
                    continue;
                }

                best[neighborState] = tentative;
                cameFrom[neighborState] = current;
                var f = tentative + Heuristic(xs, ys, ni, nj, goals);
                open.Enqueue(neighborState, f);
            }
        }

        return (null, 0.0);
    }

    /// <summary>
    /// Returns the sign (-1, 0 or 1) of a grid move along its axis: the direction of travel, used to
    /// forbid a reversal.
    /// </summary>
    private static int MoveSign(double[] xs, double[] ys, int i1, int j1, int i2, int j2, Dir direction) =>
        direction == Dir.Horizontal ? Math.Sign(xs[i2] - xs[i1]) : Math.Sign(ys[j2] - ys[j1]);

    /// <summary>
    /// Returns the run component of the state reached by a move: the grid index (along the move's axis)
    /// where the run being drawn began while it is still shorter than <paramref name="minSegment"/>, or
    /// -1 once it is long enough, when no minimum is enforced, or when the run continues a stub (the end
    /// leg). A run begins where the path turns, or where its first move leaves across the start stub.
    /// </summary>
    private static int NextRun(
        double[] xs,
        double[] ys,
        SearchState current,
        int ni,
        int nj,
        Dir nd,
        bool beginsRun,
        double minSegment)
    {
        if (minSegment <= 0.0 || (!beginsRun && current.Run < 0))
        {
            return -1;
        }

        var horizontal = nd == Dir.Horizontal;
        var axis = horizontal ? xs : ys;
        var runEnd = horizontal ? ni : nj;
        var runStart = current.Run;
        if (beginsRun)
        {
            runStart = horizontal ? current.I : current.J;
        }

        return Math.Abs(axis[runEnd] - axis[runStart]) >= minSegment - 1e-9 ? -1 : runStart;
    }

    /// <summary>Returns the axis of the stub of the start terminal on grid node (<paramref name="i"/>, <paramref name="j"/>).</summary>
    private static Dir StartStubAxis(IReadOnlyList<Terminal> starts, int i, int j)
    {
        foreach (var start in starts)
        {
            if (start.I == i && start.J == j)
            {
                return start.StubAxis;
            }
        }

        return Dir.None;
    }

    /// <summary>
    /// Returns whether finishing at <paramref name="goal"/> in <paramref name="state"/> would leave a
    /// too-short interior run: the run being drawn is still shorter than the minimum and crosses the goal's
    /// stub axis, so the stub (not the run) would be the final leg. A run that continues the stub is part
    /// of the end leg and is not an interior run.
    /// </summary>
    private static bool EndsOnShortInteriorRun(Terminal goal, SearchState state) =>
        state.Run >= 0 && goal.StubAxis != Dir.None && goal.StubAxis != state.D;

    /// <summary>Returns the goal terminal sitting on grid node (<paramref name="i"/>, <paramref name="j"/>), if any.</summary>
    private static Terminal? FindGoal(IReadOnlyList<Terminal> goals, int i, int j)
    {
        foreach (var goal in goals)
        {
            if (goal.I == i && goal.J == j)
            {
                return goal;
            }
        }

        return null;
    }

    /// <summary>
    /// Counts the soft (already-routed) segments the move crosses at a right angle, as opposed to
    /// running along them.
    /// </summary>
    private static int SoftCrossingCount(
        double[] xs,
        double[] ys,
        int i1,
        int j1,
        int i2,
        int j2,
        IReadOnlyList<Rect> softObstacles)
    {
        var count = 0;
        if (j1 == j2)
        {
            var y = ys[j1];
            var xa = Math.Min(xs[i1], xs[i2]);
            var xb = Math.Max(xs[i1], xs[i2]);
            foreach (var r in softObstacles)
            {
                if (r.Height > r.Width && r.Y < y && y < r.Y + r.Height
                    && Math.Min(xb, r.X + r.Width) - Math.Max(xa, r.X) > 0.0)
                {
                    count++;
                }
            }
        }
        else
        {
            var x = xs[i1];
            var ya = Math.Min(ys[j1], ys[j2]);
            var yb = Math.Max(ys[j1], ys[j2]);
            foreach (var r in softObstacles)
            {
                if (r.Width > r.Height && r.X < x && x < r.X + r.Width
                    && Math.Min(yb, r.Y + r.Height) - Math.Max(ya, r.Y) > 0.0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>Enumerates the four grid neighbors of a node along with the travel direction.</summary>
    private static IEnumerable<(int I, int J, Dir D)> Neighbors(int i, int j, int nx, int ny)
    {
        if (i + 1 < nx)
        {
            yield return (i + 1, j, Dir.Horizontal);
        }

        if (i - 1 >= 0)
        {
            yield return (i - 1, j, Dir.Horizontal);
        }

        if (j + 1 < ny)
        {
            yield return (i, j + 1, Dir.Vertical);
        }

        if (j - 1 >= 0)
        {
            yield return (i, j - 1, Dir.Vertical);
        }
    }

    /// <summary>Manhattan-distance heuristic from a grid node to the nearest goal terminal.</summary>
    private static double Heuristic(double[] xs, double[] ys, int i, int j, IReadOnlyList<Terminal> goals)
    {
        var min = double.PositiveInfinity;
        foreach (var goal in goals)
        {
            min = Math.Min(min, Math.Abs(xs[i] - xs[goal.I]) + Math.Abs(ys[j] - ys[goal.J]));
        }

        return min;
    }

    /// <summary>
    /// Returns the cheapest cost multiplier for a segment by testing whether its midpoint lies inside
    /// any cost band; a highway band (multiplier &lt; 1) makes bundled runs cheaper so the router
    /// prefers them. When no band applies the cost is neutral (1.0).
    /// </summary>
    private static double SegmentCostMultiplier(
        double[] xs,
        double[] ys,
        int i1,
        int j1,
        int i2,
        int j2,
        IReadOnlyList<CostBand>? bands)
    {
        if (bands is null || bands.Count == 0)
        {
            return 1.0;
        }

        // Sample the segment midpoint; bands are stripes, so a point test is enough to classify it.
        var mx = (xs[i1] + xs[i2]) / 2.0;
        var my = (ys[j1] + ys[j2]) / 2.0;
        var best = 1.0;
        foreach (var band in bands)
        {
            var coord = band.IsHorizontal ? my : mx;
            if (coord >= band.Start && coord <= band.End)
            {
                best = Math.Min(best, band.Multiplier);
            }
        }

        return best;
    }

    /// <summary>
    /// Determines whether the straight grid segment between two adjacent nodes passes within
    /// <paramref name="clearance"/> of any obstacle (the obstacle rectangles are inflated by the
    /// clearance and tested with strict inequalities, so a segment exactly one clearance away is
    /// allowed).
    /// </summary>
    private static bool SegmentBlocked(
        double[] xs,
        double[] ys,
        int i1,
        int j1,
        int i2,
        int j2,
        IReadOnlyList<Rect> obstacles,
        double clearance)
    {
        if (j1 == j2)
        {
            // Horizontal segment at y = ys[j1] spanning the two x grid lines.
            var y = ys[j1];
            var xa = Math.Min(xs[i1], xs[i2]);
            var xb = Math.Max(xs[i1], xs[i2]);
            foreach (var r in obstacles)
            {
                if (r.Y - clearance < y && y < r.Y + r.Height + clearance &&
                    Math.Max(xa, r.X - clearance) < Math.Min(xb, r.X + r.Width + clearance))
                {
                    return true;
                }
            }
        }
        else
        {
            // Vertical segment at x = xs[i1] spanning the two y grid lines.
            var x = xs[i1];
            var ya = Math.Min(ys[j1], ys[j2]);
            var yb = Math.Max(ys[j1], ys[j2]);
            foreach (var r in obstacles)
            {
                if (r.X - clearance < x && x < r.X + r.Width + clearance &&
                    Math.Max(ya, r.Y - clearance) < Math.Min(yb, r.Y + r.Height + clearance))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the total length by which the straight grid segment between two adjacent nodes overlaps
    /// the interiors of <paramref name="softObstacles"/> (summed across every soft obstacle it
    /// intersects), or 0 when it does not overlap any. Mirrors <see cref="SegmentBlocked"/>'s
    /// horizontal/vertical branch logic and its clearance-0.0 "just touching interior" semantics
    /// (soft obstacles are never inflated by clearance, unlike hard <c>obstacles</c>), but reports the
    /// overlapping span instead of a boolean so the caller can price the move proportionally to how
    /// much of it is actually shared with an already-routed line.
    /// </summary>
    private static double SoftObstacleOverlapLength(
        double[] xs,
        double[] ys,
        int i1,
        int j1,
        int i2,
        int j2,
        IReadOnlyList<Rect> softObstacles)
    {
        var total = 0.0;

        if (j1 == j2)
        {
            // Horizontal segment at y = ys[j1] spanning the two x grid lines.
            var y = ys[j1];
            var xa = Math.Min(xs[i1], xs[i2]);
            var xb = Math.Max(xs[i1], xs[i2]);
            foreach (var r in softObstacles)
            {
                if (r.Y < y && y < r.Y + r.Height)
                {
                    var overlap = Math.Min(xb, r.X + r.Width) - Math.Max(xa, r.X);
                    if (overlap > 0.0)
                    {
                        total += overlap;
                    }
                }
            }
        }
        else
        {
            // Vertical segment at x = xs[i1] spanning the two y grid lines.
            var x = xs[i1];
            var ya = Math.Min(ys[j1], ys[j2]);
            var yb = Math.Max(ys[j1], ys[j2]);
            foreach (var r in softObstacles)
            {
                if (r.X < x && x < r.X + r.Width)
                {
                    var overlap = Math.Min(yb, r.Y + r.Height) - Math.Max(ya, r.Y);
                    if (overlap > 0.0)
                    {
                        total += overlap;
                    }
                }
            }
        }

        return total;
    }

    /// <summary>Reconstructs the grid-point path by walking the came-from chain back to the start.</summary>
    private static List<Point2D> Reconstruct(
        double[] xs,
        double[] ys,
        Dictionary<SearchState, SearchState> cameFrom,
        SearchState goal)
    {
        var points = new List<Point2D>();
        var cursor = goal;
        while (true)
        {
            if (cursor.D != Dir.Done)
            {
                points.Add(new Point2D(xs[cursor.I], ys[cursor.J]));
            }

            if (!cameFrom.TryGetValue(cursor, out var prev))
            {
                break;
            }

            cursor = prev;
        }

        points.Reverse();
        return points;
    }

    /// <summary>Collapses consecutive collinear waypoints into single straight segments.</summary>
    private static IReadOnlyList<Point2D> Simplify(List<Point2D> points)
    {
        if (points.Count <= 2)
        {
            return points;
        }

        var result = new List<Point2D> { points[0] };
        for (var k = 1; k < points.Count - 1; k++)
        {
            var prev = result[^1];
            var cur = points[k];
            var next = points[k + 1];

            // Drop exact duplicates of the previous point (stubs can introduce these).
            if (Math.Abs(prev.X - cur.X) < 1e-9 && Math.Abs(prev.Y - cur.Y) < 1e-9)
            {
                continue;
            }

            // Drop the middle point only when prev→cur→next is collinear AND monotonic (same
            // direction). A direction reversal (U-turn) on the same axis must be preserved, e.g. a
            // perpendicular stub that briefly overshoots before entering a box.
            var collinearX = Math.Abs(prev.X - cur.X) < 1e-9 && Math.Abs(cur.X - next.X) < 1e-9 &&
                             (cur.Y - prev.Y) * (next.Y - cur.Y) >= 0;
            var collinearY = Math.Abs(prev.Y - cur.Y) < 1e-9 && Math.Abs(cur.Y - next.Y) < 1e-9 &&
                             (cur.X - prev.X) * (next.X - cur.X) >= 0;
            if (!collinearX && !collinearY)
            {
                result.Add(cur);
            }
        }

        // Append the final point unless it duplicates the current last point.
        if (Math.Abs(result[^1].X - points[^1].X) >= 1e-9 || Math.Abs(result[^1].Y - points[^1].Y) >= 1e-9)
        {
            result.Add(points[^1]);
        }

        return result;
    }

    /// <summary>
    /// Removes any exact leave-and-return excursion that departs a waypoint and later comes back to the
    /// same point before continuing onward. This is a defensive cleanup only: the primary fix for the
    /// shared-face detour regression is to stop contributing endpoint-adjacent soft obstacles that lure
    /// the search into such loops. The cleanup remains valuable as a last line of defense so a future
    /// caller cannot reintroduce a visibly redundant revisit sequence into the published route.
    /// </summary>
    private static IReadOnlyList<Point2D> RemovePointRevisits(IReadOnlyList<Point2D> points)
    {
        if (points.Count <= 3)
        {
            return points;
        }

        var result = new List<Point2D>(points);
        var changed = true;
        while (changed)
        {
            changed = false;
            for (var i = 0; i < result.Count - 2; i++)
            {
                var removedCycle = false;
                for (var j = i + 2; j < result.Count; j++)
                {
                    if (!SamePoint(result[i], result[j]))
                    {
                        continue;
                    }

                    result.RemoveRange(i + 1, j - i);
                    changed = true;
                    removedCycle = true;
                    break;
                }

                if (removedCycle)
                {
                    break;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Returns whether two waypoints represent the same geometric point.
    /// </summary>
    private static bool SamePoint(Point2D left, Point2D right) =>
        Math.Abs(left.X - right.X) < 1e-9 && Math.Abs(left.Y - right.Y) < 1e-9;

    /// <summary>
    /// Builds the least-bad L-shaped fallback route used when A* cannot find an obstacle-free path:
    /// it tries the horizontal-first and vertical-first elbows and returns whichever crosses fewer
    /// obstacles.
    /// </summary>
    private static IReadOnlyList<Point2D> BuildObstacleAwareFallback(
        Point2D source,
        Point2D target,
        IReadOnlyList<Rect> obstacles)
    {
        // Aligned endpoints need only a straight segment.
        if (Math.Abs(source.X - target.X) < 1e-9 || Math.Abs(source.Y - target.Y) < 1e-9)
        {
            return [source, target];
        }

        // Two candidate elbows: horizontal-first and vertical-first.
        var horizontalFirst = new List<Point2D> { source, new(target.X, source.Y), target };
        var verticalFirst = new List<Point2D> { source, new(source.X, target.Y), target };

        var hCrossings = CountCrossings(horizontalFirst, obstacles);
        var vCrossings = CountCrossings(verticalFirst, obstacles);

        return hCrossings <= vCrossings ? horizontalFirst : verticalFirst;
    }

    /// <summary>Counts how many (segment, obstacle) pairs along a path cross an obstacle interior.</summary>
    private static int CountCrossings(IReadOnlyList<Point2D> path, IReadOnlyList<Rect> obstacles)
    {
        var count = 0;
        for (var i = 0; i < path.Count - 1; i++)
        {
            var a = path[i];
            var b = path[i + 1];
            count += obstacles.Count(r => SegmentCrossesRect(a, b, r));
        }

        return count;
    }

    /// <summary>Returns true when an axis-aligned segment passes through a rectangle's strict interior.</summary>
    private static bool SegmentCrossesRect(Point2D a, Point2D b, Rect r)
    {
        if (Math.Abs(a.Y - b.Y) < 1e-9)
        {
            var y = a.Y;
            var xa = Math.Min(a.X, b.X);
            var xb = Math.Max(a.X, b.X);
            return r.Y < y && y < r.Y + r.Height && Math.Max(xa, r.X) < Math.Min(xb, r.X + r.Width);
        }

        var x = a.X;
        var ya = Math.Min(a.Y, b.Y);
        var yb = Math.Max(a.Y, b.Y);
        return r.X < x && x < r.X + r.Width && Math.Max(ya, r.Y) < Math.Min(yb, r.Y + r.Height);
    }
}
