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
    /// Gets the thin rectangles (one per segment, as built for soft obstacles) of lines that already
    /// exist around the connector and are not part of its batch — the interior connectors of the
    /// containers enclosing its endpoints — or <see langword="null"/> for none. They must also be
    /// passed as soft obstacles; this only adds the <see cref="ExistingLineCrossingPenalty"/> to a
    /// right-angle crossing of one of them, on top of the ordinary soft cost.
    /// </summary>
    public IReadOnlyList<Rect>? ExistingLines { get; init; }

    /// <summary>
    /// Gets the extra cost charged each time the route crosses one of the <see cref="ExistingLines"/>
    /// at right angles; ignored when there are none.
    /// </summary>
    public double ExistingLineCrossingPenalty { get; init; }

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
    /// <param name="budget">Optional work budget; when it runs out the search stops and the elbow fallback is returned.</param>
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
        RouteHints? hints = null,
        RoutingWorkBudget? budget = null)
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
            hints,
            budget);
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
    /// <param name="budget">Optional work budget; when it runs out the search stops and the elbow fallback is returned.</param>
    /// <returns>The waypoints (starting and ending on the chosen ports), crossing flag, cost and clearance.</returns>
    public static RouteResult RoutePorts(
        PortRange source,
        PortRange target,
        IReadOnlyList<Rect> obstacles,
        double clearance,
        IReadOnlyList<CostBand>? costBands = null,
        IReadOnlyList<Rect>? softObstacles = null,
        RouteHints? hints = null,
        RoutingWorkBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(obstacles);

        return Solve(source, target, obstacles, clearance, costBands, softObstacles, hints, budget);
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
        RouteHints? hints,
        RoutingWorkBudget? budget)
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

        // Two ends on the SAME side do not face each other, so the midline cap above is meaningless for
        // them: with both anchors level along the face (for example two boxes with aligned left edges
        // joined left to left) it collapses both stubs to zero, and the only "route" left is a straight
        // segment lying on the faces themselves. Give such an end its full stub so the connector steps
        // off and wraps around instead.
        if (source.Side is not null && source.Side == target.Side)
        {
            sourceStub = sourceStub < 1e-9 ? stub : sourceStub;
            targetStub = targetStub < 1e-9 ? stub : targetStub;
        }

        var routeSource = StepOff(sourceReference, source.Side, sourceStub);
        var routeTarget = StepOff(targetReference, target.Side, targetStub);

        // A route computed at the reduced effort first searches only a window around its two ends: the
        // grid then holds the obstacles of that window rather than of the whole canvas, which is what
        // keeps the cost of one search from growing with the size of the scope. A connector the window
        // cannot hold takes the whole-canvas search below.
        var reducedBudget = budget is { Tier: RoutingTier.Reduced } ? budget : null;
        if (reducedBudget is not null && hints is null)
        {
            var windowed = TrySolveInWindow(
                source, target, obstacles, clearance, costBands, softObstacles, (routeSource, routeTarget), (sourceStub, targetStub), reducedBudget, (sourceReference, targetReference));
            if (windowed is not null)
            {
                return windowed.Value;
            }
        }

        // At the reduced effort the soft lanes the grid offers are limited to the ones that matter near the
        // connector (see SoftLanes); the full effort gives the search every lane.
        var lanes = reducedBudget is not null ? SoftLanes.Reduced(softObstacles, routeSource, routeTarget) : SoftLanes.Full(softObstacles);

        // Try to find an obstacle-free orthogonal path, preferring the largest clearance that works.
        // With a minimum interior segment hint, a first pass requires the route at every clearance
        // level to honor it; only when no level yields a route is the requirement dropped, so the
        // hint can never turn a routable connector into a crossing fallback.
        var minSegmentPasses = hints is { MinInteriorSegment: > 0.0 } ? new[] { true, false } : new[] { false };
        foreach (var enforceMinSegment in minSegmentPasses)
        {
            foreach (var c in ClearanceLevels(clearance))
            {
                // An exhausted budget searches no further: every remaining connector takes the elbow below.
                if (budget is { Tier: RoutingTier.Minimal })
                {
                    break;
                }

                var attempt = TrySolveAtClearance(
                    source, target, obstacles, c, costBands, lanes, hints, enforceMinSegment, (routeSource, routeTarget), (sourceStub, targetStub), budget);
                if (attempt is not null)
                {
                    return attempt.Value;
                }
            }
        }

        // No clean path at any clearance: fall back to the least-bad L-shape (it may cross a box).
        if (budget is { Tier: RoutingTier.Minimal })
        {
            budget.RecordElbowRoute();
        }

        var fallback = BuildObstacleAwareFallback(routeSource, routeTarget, obstacles);
        return new RouteResult(Finalize(sourceReference, targetReference, source.Side, target.Side, fallback), Crossed: true);
    }

    /// <summary>
    /// Searches for a route inside a window around the two ends of the connector, with the obstacles
    /// outside the window left out and the space outside it walled off by four obstacles, so the grid (and
    /// therefore the search) is as big as the neighborhood of the connector instead of the canvas.
    /// </summary>
    /// <returns>The route found at the largest clearance that has one, or <see langword="null"/> when the window holds none.</returns>
    private static RouteResult? TrySolveInWindow(
        PortRange source,
        PortRange target,
        IReadOnlyList<Rect> obstacles,
        double clearance,
        IReadOnlyList<CostBand>? costBands,
        IReadOnlyList<Rect>? softObstacles,
        (Point2D Source, Point2D Target) route,
        (double Source, double Target) stubs,
        RoutingWorkBudget budget,
        (Point2D Source, Point2D Target) anchors)
    {
        // The window: the box around both anchors and both step-off points, grown by a margin that is a
        // fraction of its size (and at least the least margin), so a route may bend around what it meets.
        var left = Math.Min(Math.Min(route.Source.X, route.Target.X), Math.Min(anchors.Source.X, anchors.Target.X));
        var right = Math.Max(Math.Max(route.Source.X, route.Target.X), Math.Max(anchors.Source.X, anchors.Target.X));
        var top = Math.Min(Math.Min(route.Source.Y, route.Target.Y), Math.Min(anchors.Source.Y, anchors.Target.Y));
        var bottom = Math.Max(Math.Max(route.Source.Y, route.Target.Y), Math.Max(anchors.Source.Y, anchors.Target.Y));
        var margin = Math.Max(WindowMinMargin, WindowMarginFraction * Math.Max(right - left, bottom - top));
        left -= margin;
        top -= margin;
        right += margin;
        bottom += margin;

        // Only what reaches into the window can matter inside it
        var windowed = obstacles.Where(obstacle => Overlaps(obstacle, left - clearance, top - clearance, right + clearance, bottom + clearance)).ToList();

        // The space outside the window is closed off, so a route never leaves it for ground whose obstacles were left out.
        // The top and bottom walls extend beyond the side walls so no seam is left between two walls for a route to follow.
        windowed.Add(new Rect(left - WindowWallDepth, top - WindowWallDepth, WindowWallDepth, (bottom - top) + (2.0 * WindowWallDepth)));
        windowed.Add(new Rect(right, top - WindowWallDepth, WindowWallDepth, (bottom - top) + (2.0 * WindowWallDepth)));
        windowed.Add(new Rect(left - WindowWallDepth, top - WindowWallDepth, (right - left) + (2.0 * WindowWallDepth), WindowWallDepth));
        windowed.Add(new Rect(left - WindowWallDepth, bottom, (right - left) + (2.0 * WindowWallDepth), WindowWallDepth));

        var windowedSoft = softObstacles?.Where(soft => Overlaps(soft, left, top, right, bottom)).ToList();
        var lanes = SoftLanes.Reduced(windowedSoft, route.Source, route.Target);
        foreach (var c in ClearanceLevels(clearance))
        {
            if (budget.Tier == RoutingTier.Minimal)
            {
                break;
            }

            var attempt = TrySolveAtClearance(source, target, windowed, c, costBands, lanes, null, false, route, stubs, budget);

            // A route that left the window found a seam in its walls (never expected); it is not trusted
            if (attempt is not null && attempt.Value.Waypoints.All(p => p.X >= left && p.X <= right && p.Y >= top && p.Y <= bottom))
            {
                return attempt;
            }
        }

        return null;
    }

    /// <summary>Returns whether <paramref name="r"/> reaches into the box with the given edges.</summary>
    private static bool Overlaps(Rect r, double left, double top, double right, double bottom) =>
        r.X < right && r.X + r.Width > left && r.Y < bottom && r.Y + r.Height > top;

    /// <summary>The least margin, in logical pixels, a search window extends beyond the two ends of a connector.</summary>
    private const double WindowMinMargin = 120.0;

    /// <summary>The fraction of the larger extent of the two ends a search window extends beyond them.</summary>
    private const double WindowMarginFraction = 0.25;

    /// <summary>The depth, in logical pixels, of the walls that close off the space outside a search window.</summary>
    private const double WindowWallDepth = 1_000_000.0;

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
        SoftLanes lanes,
        RouteHints? hints,
        bool enforceMinSegment,
        (Point2D Source, Point2D Target) route,
        (double Source, double Target) stubs,
        RoutingWorkBudget? budget)
    {
        var xs = new SortedSet<double> { route.Source.X, route.Target.X };
        var ys = new SortedSet<double> { route.Source.Y, route.Target.Y };
        var extras = hints is null ? null : BuildExtras(source, target, hints, c, xs, ys, enforceMinSegment);
        AddPortLines(source, xs, ys);
        AddPortLines(target, xs, ys);

        var xAxis = BuildAxis(xs, obstacles, c, horizontal: true, lanes);
        var yAxis = BuildAxis(ys, obstacles, c, horizontal: false, lanes);

        var starts = BuildTerminals(source, stubs.Source, xAxis, yAxis);
        var goals = BuildTerminals(target, stubs.Target, xAxis, yAxis);

        // A range whose permitted intervals hold no grid line (every port of the face excluded) offers no terminal.
        if (starts.Count == 0 || goals.Count == 0)
        {
            return null;
        }

        extras?.SetTerminals(starts, goals);

        var found = AStar(xAxis, yAxis, starts, goals, obstacles, c, costBands, lanes.Obstacles, extras, budget);
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
    /// clearance, plus the same offset for the soft obstacles of <paramref name="lanes"/> so the search has a
    /// candidate lane on either side of an already-routed line to prefer when one is available.
    /// </summary>
    private static double[] BuildAxis(
        SortedSet<double> set,
        IReadOnlyList<Rect> obstacles,
        double clearance,
        bool horizontal,
        SoftLanes lanes)
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

        if (lanes.AxisRects is not null)
        {
            foreach (var r in lanes.AxisRects)
            {
                // A lane beside a soft rectangle only helps a route running along it: a line of the other
                // direction (a horizontal rectangle for the x axis) is skipped when the lanes are limited.
                if (lanes.PerpendicularOnly && (horizontal ? r.Width > r.Height : r.Height > r.Width))
                {
                    continue;
                }

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
    /// The soft obstacles of one search and the grid lines they contribute. The search always weighs
    /// every soft obstacle in its cost; what differs by effort is which of them also add grid lines (the
    /// candidate lanes beside an already-routed line). Every line a soft obstacle adds is an alternative
    /// lane only, so leaving some out never makes a connector impossible to route: the lines of the hard obstacles
    /// and of the two ends always remain.
    /// </summary>
    /// <param name="Obstacles">The soft obstacles the search's cost weighs; <see langword="null"/> for none.</param>
    /// <param name="AxisRects">The soft obstacles that add grid lines; <see langword="null"/> for none.</param>
    /// <param name="PerpendicularOnly">Whether only a rectangle perpendicular to an axis adds a line to it.</param>
    private readonly record struct SoftLanes(IReadOnlyList<Rect>? Obstacles, IReadOnlyList<Rect>? AxisRects, bool PerpendicularOnly)
    {
        /// <summary>
        /// The most soft rectangles a search at the reduced effort adds grid lines for: each adds up to four
        /// lines to the grid, and a grid line per soft rectangle of a scope with hundreds of routed lines
        /// made one window grid twenty times larger than the hard obstacles in it require.
        /// </summary>
        internal const int ReducedLimit = 128;

        /// <summary>The lanes of the full effort: every soft obstacle adds its lines to both axes.</summary>
        /// <param name="soft">The soft obstacles.</param>
        /// <returns>The lanes.</returns>
        internal static SoftLanes Full(IReadOnlyList<Rect>? soft) => new(soft, soft, false);

        /// <summary>
        /// The lanes of the reduced effort: only the <see cref="ReducedLimit"/> soft rectangles nearest the
        /// middle of the connector add lines, and a rectangle only to the axis it is perpendicular to (a tall
        /// one gives x lines, a wide one y lines). The choice depends on the input alone: ties in the
        /// distance keep the order of <paramref name="soft"/>.
        /// </summary>
        /// <param name="soft">The soft obstacles.</param>
        /// <param name="from">The step-off point of one end of the connector.</param>
        /// <param name="to">The step-off point of the other end.</param>
        /// <returns>The lanes.</returns>
        internal static SoftLanes Reduced(IReadOnlyList<Rect>? soft, Point2D from, Point2D to)
        {
            if (soft is null || soft.Count <= ReducedLimit)
            {
                return new SoftLanes(soft, soft, true);
            }

            var midX = (from.X + to.X) / 2.0;
            var midY = (from.Y + to.Y) / 2.0;
            var nearest = soft
                .OrderBy(r => Math.Abs(r.X + (r.Width / 2.0) - midX) + Math.Abs(r.Y + (r.Height / 2.0) - midY))
                .Take(ReducedLimit)
                .ToList();
            return new SoftLanes(soft, nearest, true);
        }
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
        public SearchExtras(
            IReadOnlyList<ZoneSpec> zones,
            IReadOnlyList<WallSpec> walls,
            double crossingPenalty,
            double clearance,
            double minSegment,
            IReadOnlyList<Rect>? existingLines = null,
            double existingLinePenalty = 0.0)
        {
            Zones = zones;
            Walls = walls;
            CrossingPenalty = crossingPenalty;
            MinSegment = minSegment;
            ExistingLines = existingLines is { Count: > 0 } && existingLinePenalty > 0.0 ? existingLines : null;
            ExistingLinePenalty = existingLinePenalty;
            _rewardDistance = WallDistanceRewardScale * clearance;
        }

        /// <summary>Gets the minimum interior segment length enforced by the search; zero for none.</summary>
        public double MinSegment { get; }

        /// <summary>Gets the rectangles of pre-existing lines charged an extra crossing penalty, or <see langword="null"/>.</summary>
        public IReadOnlyList<Rect>? ExistingLines { get; }

        /// <summary>Gets the extra cost of crossing one of <see cref="ExistingLines"/>.</summary>
        public double ExistingLinePenalty { get; }

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

        return new SearchExtras(
            zones,
            walls,
            hints.CrossingPenalty,
            clearance,
            enforceMinSegment ? hints.MinInteriorSegment : 0.0,
            hints.ExistingLines,
            hints.ExistingLineCrossingPenalty);
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
        SearchExtras? extras = null,
        RoutingWorkBudget? budget = null) =>
        new GridSearch(xs, ys, starts, goals, obstacles, clearance, costBands, softObstacles ?? [], extras, budget).Run();

    /// <summary>
    /// Reusable per-thread working storage of the grid searches: the edge-cost memo, the node and state
    /// tables and the connectivity queue. Entries are validated with a per-search stamp instead of being
    /// cleared, so starting a search costs nothing proportional to the grid, and the arrays are kept (and
    /// grown only when a larger grid shows up) instead of being reallocated for each of the thousands of
    /// searches one layout runs.
    /// </summary>
    /// <remarks>
    /// The scratch holds no result: every entry a search reads is one that search wrote under its own
    /// stamp, so reuse cannot change what a search computes and the routes stay a pure function of the
    /// inputs. It is thread-local, so concurrent layouts never share it.
    /// </remarks>
    private sealed class SearchScratch
    {
        /// <summary>The stamp after which the scratch is replaced, keeping the packed marks within an int.</summary>
        private const int StampLimit = 1 << 27;

        /// <summary>The most memory, in bytes, a thread keeps its scratch for reuse by later searches.</summary>
        private const long RetainedByteLimit = 32L * 1024 * 1024;

        /// <summary>Extra state slots reserved for run-pending states beyond the dense ones.</summary>
        private const int InitialOverflow = 1024;

        /// <summary>The thread's retained scratch, if any.</summary>
        [ThreadStatic]
        private static SearchScratch? s_retained;

        /// <summary>The stamp of the current search; never 0, which marks an untouched entry.</summary>
        public int Stamp;

        /// <summary>Per edge: the search stamp shifted left by two, or-ed with the edge status.</summary>
        public int[] EdgeMark = [];

        /// <summary>Per priced edge: the length term (length, cost-band and wall-distance multipliers).</summary>
        public double[] EdgeStep = [];

        /// <summary>Per priced edge: the soft-obstacle cost.</summary>
        public double[] EdgeSoft = [];

        /// <summary>Per priced edge: the hint-driven extra cost.</summary>
        public double[] EdgeExtra = [];

        /// <summary>Per edge: 1 when a hard obstacle (inflated by the clearance) blocks it.</summary>
        public byte[] ObstacleBlocked = [];

        /// <summary>Per edge: the length by which it overlaps the soft obstacles.</summary>
        public double[] SoftOverlap = [];

        /// <summary>Per edge: the number of existing lines it crosses at a right angle.</summary>
        public int[] ExistingCrossings = [];

        /// <summary>Per node: the cached heuristic value.</summary>
        public double[] Heuristic = [];

        /// <summary>Per node: the stamp under which <see cref="Heuristic"/> is valid.</summary>
        public int[] HeuristicMark = [];

        /// <summary>Per node: the index of the first goal terminal on it.</summary>
        public int[] GoalAt = [];

        /// <summary>Per node: the stamp under which <see cref="GoalAt"/> is valid.</summary>
        public int[] GoalMark = [];

        /// <summary>Per node: the stamp under which the connectivity sweep has reached it.</summary>
        public int[] SeenMark = [];

        /// <summary>Per node: the stamp under which it is a start terminal's node.</summary>
        public int[] StartMark = [];

        /// <summary>The breadth-first queue of the connectivity sweep.</summary>
        public int[] Queue = [];

        /// <summary>Per state: the best cost found.</summary>
        public double[] Best = [];

        /// <summary>Per state: the state it was reached from, or -1 for a start.</summary>
        public int[] From = [];

        /// <summary>Per state: the search stamp shifted left by one, or-ed with 1 once the state was expanded at its best cost.</summary>
        public int[] StateMark = [];

        /// <summary>Per run-pending state: the node and direction slot it sits on.</summary>
        public int[] OverflowNodeSlot = [];

        /// <summary>Per run-pending state: the grid index where its pending run began.</summary>
        public int[] OverflowRun = [];

        /// <summary>Maps a run-pending state key to its state id.</summary>
        public Dictionary<long, int> OverflowIds { get; } = [];

        /// <summary>Gets or sets the number of run-pending states of the current search.</summary>
        public int OverflowCount { get; set; }

        /// <summary>Gets the number of nodes the node tables hold.</summary>
        private int NodeCapacity { get; set; }

        /// <summary>
        /// Gets the approximate size in bytes of the scratch's tables, from their real lengths (the state
        /// and overflow tables may have grown beyond what the grid first needed).
        /// </summary>
        private long ApproximateBytes =>
            ((long)(EdgeMark.Length + ExistingCrossings.Length) * sizeof(int))
            + ((long)(EdgeStep.Length + EdgeSoft.Length + EdgeExtra.Length + SoftOverlap.Length) * sizeof(double))
            + ObstacleBlocked.Length
            + ((long)Heuristic.Length * sizeof(double))
            + ((long)(HeuristicMark.Length + GoalAt.Length + GoalMark.Length + SeenMark.Length + StartMark.Length + Queue.Length) * sizeof(int))
            + ((long)Best.Length * (sizeof(double) + (2 * sizeof(int))))
            + ((long)(OverflowNodeSlot.Length + OverflowRun.Length) * sizeof(int));

        /// <summary>
        /// Returns the calling thread's scratch prepared for a new search over a grid of
        /// <paramref name="nodes"/> nodes.
        /// </summary>
        /// <param name="nodes">The number of grid nodes.</param>
        /// <returns>A scratch whose <see cref="Stamp"/> is unique to the new search.</returns>
        public static SearchScratch Begin(int nodes)
        {
            var scratch = s_retained;

            // A scratch that grew past the byte limit during its last search, or that has run out of stamps, is replaced
            if (scratch is null || scratch.Stamp >= StampLimit || scratch.ApproximateBytes > RetainedByteLimit)
            {
                scratch = new SearchScratch();
            }

            // Grow the node and state tables only when this grid is larger than any seen so far
            if (nodes > scratch.NodeCapacity)
            {
                scratch.Allocate(nodes);
            }

            scratch.Stamp++;
            if (scratch.OverflowCount > 0)
            {
                scratch.OverflowIds.Clear();
                scratch.OverflowCount = 0;
            }

            // A grid too large to keep (by the memory it takes, not its node count) is released after its search
            s_retained = scratch.ApproximateBytes <= RetainedByteLimit ? scratch : null;
            return scratch;
        }

        /// <summary>
        /// Drops the calling thread's retained scratch when <paramref name="scratch"/> is it and its tables
        /// grew past the byte limit during the search that just ended.
        /// </summary>
        /// <param name="scratch">The scratch the search used.</param>
        public static void ReleaseIfOversize(SearchScratch scratch)
        {
            if (ReferenceEquals(s_retained, scratch) && scratch.ApproximateBytes > RetainedByteLimit)
            {
                s_retained = null;
            }
        }

        /// <summary>Makes room for a state id, doubling the state tables.</summary>
        /// <param name="id">The state id that must fit.</param>
        public void EnsureStateCapacity(int id)
        {
            if (id < Best.Length)
            {
                return;
            }

            var capacity = Math.Max(id + 1, Best.Length * 2);
            Array.Resize(ref Best, capacity);
            Array.Resize(ref From, capacity);
            Array.Resize(ref StateMark, capacity);
        }

        /// <summary>Makes room for a run-pending state record, doubling its tables.</summary>
        /// <param name="index">The overflow index that must fit.</param>
        public void EnsureOverflowCapacity(int index)
        {
            if (index < OverflowRun.Length)
            {
                return;
            }

            var capacity = Math.Max(index + 1, OverflowRun.Length * 2);
            Array.Resize(ref OverflowNodeSlot, capacity);
            Array.Resize(ref OverflowRun, capacity);
        }

        /// <summary>Allocates fresh, unstamped tables for a grid of <paramref name="nodes"/> nodes.</summary>
        private void Allocate(int nodes)
        {
            NodeCapacity = nodes;
            EdgeMark = new int[2 * nodes];
            EdgeStep = new double[2 * nodes];
            EdgeSoft = new double[2 * nodes];
            EdgeExtra = new double[2 * nodes];
            ObstacleBlocked = new byte[2 * nodes];
            SoftOverlap = new double[2 * nodes];
            ExistingCrossings = new int[2 * nodes];
            Heuristic = new double[nodes];
            HeuristicMark = new int[nodes];
            GoalAt = new int[nodes];
            GoalMark = new int[nodes];
            SeenMark = new int[nodes];
            StartMark = new int[nodes];
            Queue = new int[nodes];
            var states = (GridSearch.DirectionSlots * nodes) + InitialOverflow;
            Best = new double[states];
            From = new int[states];
            StateMark = new int[states];
            OverflowNodeSlot = new int[InitialOverflow];
            OverflowRun = new int[InitialOverflow];
        }
    }

    /// <summary>
    /// One A* search over a routing grid, together with the memo of per-edge costs it (and the connectivity
    /// pre-check) share.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A grid edge between two adjacent nodes is blocked, or priced, the same way whichever direction and
    /// whichever search state it is crossed in: the obstacle, wall and soft-obstacle terms depend on the
    /// segment only, and the turn penalty (the one state-dependent term) is added by the caller. Each edge
    /// is therefore evaluated at most once per search, lazily, instead of once per expansion of each of
    /// its two end nodes in each of the search's states. The cost components are cached separately, never
    /// as a pre-summed term, and combined in the original order, so every route is bit-for-bit the one
    /// the uncached search found.
    /// </para>
    /// <para>
    /// The search states live in dense tables addressed by node and direction; only the rare states that
    /// carry a pending short run (a minimum interior segment is enforced) use a dictionary.
    /// </para>
    /// </remarks>
    private sealed class GridSearch
    {
        /// <summary>
        /// The number of direction slots per node in the dense state table: none, horizontal forward and
        /// backward, vertical forward and backward, and the terminal done state.
        /// </summary>
        public const int DirectionSlots = 6;

        /// <summary>Turn penalty in pixels; comparable to a short straight run so a detour that removes a bend is preferred only when not much longer.</summary>
        private const double TurnPenalty = 20.0;

        /// <summary>
        /// Soft-obstacle cost is split into a small flat base (so a trivial, few-pixel overlap stays cheap
        /// enough that a shared approach corridor is never worth a pointless detour) plus a per-unit-length
        /// term proportional to how much of the candidate move actually overlaps the soft obstacle. A flat,
        /// length-independent penalty was tried first and found insufficient: on a sparse narrow-gap grid a
        /// connector's entire multi-hundred-pixel corridor can collapse into a single grid move, so a flat
        /// cost priced a 500px visual overlap identically to a trivial one, cheaper than the roughly fixed
        /// cost of detouring to a nearby alternate lane, so A* always kept the long overlap no matter how
        /// visually merged the result looked. Scaling the cost with the overlap length keeps short,
        /// incidental overlaps (and the legitimate endpoint-adjacent approach corridors that never become
        /// soft obstacles in the first place; see <c>AddLineObstacles</c> in <c>ConnectorRouter</c>) cheap,
        /// while making an extended overlap cost substantially more than a bounded-cost lane change, so the
        /// search prefers the detour once the overlap grows long enough to matter.
        /// </summary>
        private const double SoftObstacleBaseCost = 10.0;

        /// <summary>The soft-obstacle cost per pixel of overlap; see <see cref="SoftObstacleBaseCost"/>.</summary>
        private const double SoftObstaclePerUnitLengthCost = 1.0;

        /// <summary>Edge status: blocked by an obstacle or a wall strip.</summary>
        private const int EdgeBlocked = 1;

        /// <summary>Edge status: free, cost components not yet evaluated.</summary>
        private const int EdgeFree = 2;

        /// <summary>Edge status: free, cost components cached.</summary>
        private const int EdgePriced = 3;

        /// <summary>The connectivity sweep is still spreading from the goals.</summary>
        private const int ProbeRunning = 0;

        /// <summary>The connectivity sweep reached a start: the goals are reachable.</summary>
        private const int ProbeConnected = 1;

        /// <summary>The connectivity sweep ran out of nodes without reaching a start: the goals are walled in.</summary>
        private const int ProbeDisconnected = 2;

        /// <summary>The nodes the connectivity sweep visits per state the search expands.</summary>
        private const int ProbeStepsPerExpansion = 4;

        /// <summary>The grid-build work (clearing and rasterizing) is charged to the budget as one unit per this many nodes.</summary>
        private const int GridBuildWorkDivisor = 4;

        /// <summary>
        /// The number of expansions (a power of two) after which a running search charges its work to the
        /// budget, so the work spent never exceeds the hard limit by more than
        /// <c>(ChargeChunkSize - 1) * (1 + ProbeStepsPerExpansion)</c> units plus the clamped grid build.
        /// </summary>
        private const int ChargeChunkSize = 64;

        /// <summary>The x step of each of the four neighbors, in the fixed order right, left, down, up.</summary>
        private static readonly int[] StepI = [1, -1, 0, 0];

        /// <summary>The y step of each of the four neighbors, in the fixed order right, left, down, up.</summary>
        private static readonly int[] StepJ = [0, 0, 1, -1];

        /// <summary>The direction slots of the horizontal and vertical moves when travel senses are told apart.</summary>
        private static readonly int[] SensedSlots = [1, 2, 3, 4];

        /// <summary>The direction slots of the horizontal and vertical moves when travel senses are not told apart.</summary>
        private static readonly int[] SharedSenseSlots = [1, 1, 3, 3];

        /// <summary>Bits of a run-pending state key holding the run's start index.</summary>
        private const int RunBits = 24;

        /// <summary>Direction slot of the start state (no move yet).</summary>
        private const int SlotNone = 0;

        /// <summary>Direction slot of the terminal done state.</summary>
        private const int SlotDone = 5;

        /// <summary>The grid's x lines.</summary>
        private readonly double[] _xs;

        /// <summary>The grid's y lines.</summary>
        private readonly double[] _ys;

        /// <summary>The start terminals.</summary>
        private readonly IReadOnlyList<Terminal> _starts;

        /// <summary>The goal terminals.</summary>
        private readonly IReadOnlyList<Terminal> _goals;

        /// <summary>The hard obstacles.</summary>
        private readonly IReadOnlyList<Rect> _obstacles;

        /// <summary>The clearance of the level searched.</summary>
        private readonly double _clearance;

        /// <summary>The cost bands, or <see langword="null"/>.</summary>
        private readonly IReadOnlyList<CostBand>? _costBands;

        /// <summary>The soft obstacles (empty when there are none).</summary>
        private readonly IReadOnlyList<Rect> _softObstacles;

        /// <summary>The hint-driven terms, or <see langword="null"/>.</summary>
        private readonly SearchExtras? _extras;

        /// <summary>The number of x lines.</summary>
        private readonly int _nx;

        /// <summary>The number of y lines.</summary>
        private readonly int _ny;

        /// <summary>The number of nodes.</summary>
        private readonly int _nodes;

        /// <summary>The minimum interior segment enforced; zero for none.</summary>
        private readonly double _minSegment;

        /// <summary>The reusable tables, stamped for this search.</summary>
        private readonly SearchScratch _scratch;

        /// <summary>The stamp of this search.</summary>
        private readonly int _stamp;

        /// <summary>The direction slot reached by each of the four moves.</summary>
        private readonly int[] _neighborSlots;

        /// <summary>Whether the connectivity sweep is running, found a start, or ran dry.</summary>
        private int _probeState;

        /// <summary>The next node of the connectivity sweep's queue to visit.</summary>
        private int _probeHead;

        /// <summary>The end of the connectivity sweep's queue.</summary>
        private int _probeTail;

        /// <summary>The nodes the connectivity sweep has visited, charged to the budget.</summary>
        private long _probeSteps;

        /// <summary>The expansions and connectivity sweep steps already charged to the budget.</summary>
        private long _charged;

        /// <summary>The work budget the search is charged to, or <see langword="null"/> for none.</summary>
        private readonly RoutingWorkBudget? _budget;

        /// <summary>Whether the soft-obstacle terms have been rasterized for this search.</summary>
        private bool _softRasterized;

        /// <summary>Initializes a search over the given grid.</summary>
        public GridSearch(
            double[] xs,
            double[] ys,
            IReadOnlyList<Terminal> starts,
            IReadOnlyList<Terminal> goals,
            IReadOnlyList<Rect> obstacles,
            double clearance,
            IReadOnlyList<CostBand>? costBands,
            IReadOnlyList<Rect> softObstacles,
            SearchExtras? extras,
            RoutingWorkBudget? budget)
        {
            _budget = budget;
            _xs = xs;
            _ys = ys;
            _starts = starts;
            _goals = goals;
            _obstacles = obstacles;
            _clearance = clearance;
            _costBands = costBands;
            _softObstacles = softObstacles;
            _extras = extras;
            _nx = xs.Length;
            _ny = ys.Length;
            _nodes = _nx * _ny;
            _minSegment = extras?.MinSegment ?? 0.0;
            _neighborSlots = _minSegment > 0.0 ? SensedSlots : SharedSenseSlots;
            _scratch = SearchScratch.Begin(_nodes);
            _stamp = _scratch.Stamp;
        }

        /// <summary>
        /// Runs the search, returning the grid points from the chosen start to the chosen goal with the
        /// path cost, or a <see langword="null"/> path when no obstacle-free path exists.
        /// </summary>
        public (List<Point2D>? Path, double Cost) Run()
        {
            var s = _scratch;

            // A search over a single goal with no extra cost ends the moment its goal node is first
            // dequeued (the legacy behavior); several goals, or goals with an extra cost, need a
            // terminal "done" state carrying the goal's extra cost so the cheapest total wins.
            var useDone = _goals.Count > 1 || _goals[0].Extra > 0.0;

            // Index the goal terminals by node; the first goal on a node wins, as in a linear scan.
            for (var k = 0; k < _goals.Count; k++)
            {
                var node = (_goals[k].I * _ny) + _goals[k].J;
                if (s.GoalMark[node] != _stamp)
                {
                    s.GoalMark[node] = _stamp;
                    s.GoalAt[node] = k;
                }
            }

            // The grid build is charged up to the hard limit and no further, and a budget that has no work
            // left needs no search at all: the connector falls back to the elbow
            if (_budget is not null)
            {
                _budget.Charge(Math.Min(_nodes / GridBuildWorkDivisor, _budget.RemainingHard));
                if (_budget.Tier == RoutingTier.Minimal)
                {
                    return (null, 0.0);
                }
            }

            RasterizeObstacles();

            // The expansions (and connectivity sweep steps) are charged to the budget in chunks while the
            // search runs, and the rest however the search ends
            long expansions = 0;
            try
            {
                return RunLoop(useDone, ref expansions);
            }
            finally
            {
                _budget?.Charge(expansions + _probeSteps - _charged);
                SearchScratch.ReleaseIfOversize(_scratch);
            }
        }

        /// <summary>
        /// Charges the work done since the last charge to the budget, and reports whether the search may
        /// go on.
        /// </summary>
        /// <param name="expansions">The number of states expanded so far.</param>
        /// <returns><see langword="true"/> when the budget's hard limit is reached and the search must stop.</returns>
        private bool ChargeProgress(long expansions)
        {
            var budget = _budget!;
            var total = expansions + _probeSteps;
            budget.Charge(total - _charged);
            _charged = total;
            return budget.Tier == RoutingTier.Minimal;
        }

        /// <summary>The A* loop proper over the grid.</summary>
        /// <param name="useDone">Whether goals carry a terminal done state.</param>
        /// <param name="expansions">Receives the number of states expanded.</param>
        private (List<Point2D>? Path, double Cost) RunLoop(bool useDone, ref long expansions)
        {
            var s = _scratch;

            // A search whose goals cannot be reached over the grid at all (the free grid edges, ignoring
            // costs, turns and the minimum interior segment, which can only remove moves) expands every
            // state reachable from the starts before it fails, by far the most expensive way to find that
            // out. A connectivity sweep that spreads from the goals alongside the search finds it out in the
            // time it takes to exhaust the goals' side of the grid (a handful of nodes when a goal is walled
            // in), and never disagrees with the search: every move the search may make is a free grid edge
            // the sweep follows too, and a free edge is free in both directions.
            SeedProbe();

            // Seed the open set with the start terminals, each at its port-displacement cost.
            var open = new MinHeap<int, double>();
            foreach (var start in _starts)
            {
                var startId = ((start.I * _ny) + start.J) * DirectionSlots;
                if (IsValid(startId) && s.Best[startId] <= start.Extra)
                {
                    continue;
                }

                Record(startId, start.Extra, -1);
                open.Enqueue(startId, start.Extra + HeuristicAt(start.I, start.J));
            }

            while (open.Count > 0)
            {
                var currentId = open.Dequeue();
                // A queue entry whose state was already expanded at its best cost is a stale duplicate: its
                // expansion would find every neighbor already at the cost it would offer.
                if ((s.StateMark[currentId] & 1) != 0)
                {
                    continue;
                }

                s.StateMark[currentId] |= 1;
                expansions++;

                // Advance the connectivity sweep alongside the search: a goal walled off from every start
                // ends the search now instead of after the whole reachable grid has been expanded.
                if (_probeState == ProbeRunning)
                {
                    AdvanceProbe(ProbeStepsPerExpansion);
                    if (_probeState == ProbeDisconnected)
                    {
                        return (null, 0.0);
                    }
                }

                // Charge the work in chunks and stop the moment the budget's hard limit is reached: the
                // connector falls back to the elbow, and the work spent overshoots the limit by one chunk at most
                if ((expansions & (ChargeChunkSize - 1)) == 0 && _budget is not null && ChargeProgress(expansions))
                {
                    return (null, 0.0);
                }

                var (node, slot, run) = Decode(currentId);
                var (ci, cj) = (node / _ny, node % _ny);
                var cd = SlotDirection(slot);
                var g = s.Best[currentId];

                if (cd == Dir.Done)
                {
                    return (Reconstruct(currentId), g);
                }

                if (s.GoalMark[node] == _stamp)
                {
                    var goal = _goals[s.GoalAt[node]];
                    if (!EndsOnShortInteriorRun(goal, cd, run))
                    {
                        if (!useDone)
                        {
                            return (Reconstruct(currentId), g);
                        }

                        var doneId = (node * DirectionSlots) + SlotDone;
                        var total = g + goal.Extra;
                        if (!IsValid(doneId) || total < s.Best[doneId])
                        {
                            Record(doneId, total, currentId);
                            open.Enqueue(doneId, total);
                        }
                    }
                }

                // The axis of the stub this search leaves from (start state only, and only when a minimum
                // interior segment is enforced): a first move across that axis starts an interior run.
                var startAxis = _minSegment > 0.0 && cd == Dir.None ? StartStubAxis(_starts, ci, cj) : Dir.None;
                var currentSign = SlotSign(slot);

                // The four neighbors in a fixed order: right, left, down, up.
                for (var k = 0; k < 4; k++)
                {
                    var horizontal = k < 2;
                    var ni = ci + StepI[k];
                    var nj = cj + StepJ[k];
                    if (ni < 0 || ni >= _nx || nj < 0 || nj >= _ny)
                    {
                        continue;
                    }

                    // With a minimum interior segment, a turn may only complete a segment that is long
                    // enough (a segment that continues the stub at either end is part of the end leg, whose
                    // length the end-leg zones govern, so it is exempt) and a move may never double back
                    // along its own axis.
                    var nd = horizontal ? Dir.Horizontal : Dir.Vertical;
                    var turning = cd != Dir.None && cd != nd;
                    var beginsRun = turning || (startAxis != Dir.None && startAxis != nd);
                    var sign = _minSegment > 0.0 ? StepI[k] + StepJ[k] : 0;
                    if (_minSegment > 0.0 && ((turning && run >= 0) || (cd == nd && currentSign == -sign)))
                    {
                        continue;
                    }

                    // Skip moves over a blocked edge, then price the free ones from the shared memo.
                    var edge = horizontal ? (Math.Min(ci, ni) * _ny) + cj : _nodes + (ci * _ny) + Math.Min(cj, nj);
                    var mark = s.EdgeMark[edge];
                    if ((mark >> 2) != _stamp || (mark & 3) == EdgeFree)
                    {
                        if (!PriceEdge(edge, Math.Min(ci, ni), Math.Min(cj, nj), Math.Max(ci, ni), Math.Max(cj, nj), horizontal))
                        {
                            continue;
                        }
                    }
                    else if ((mark & 3) == EdgeBlocked)
                    {
                        continue;
                    }

                    var turnCost = turning ? TurnPenalty : 0.0;
                    var tentative = g + s.EdgeStep[edge] + turnCost + s.EdgeSoft[edge] + s.EdgeExtra[edge];

                    var neighborNode = (ni * _ny) + nj;
                    var neighborSlot = _neighborSlots[k];

                    // Without a minimum interior segment no state carries a pending run: the state is dense
                    var neighborId = _minSegment > 0.0
                        ? StateId(neighborNode, neighborSlot, NextRun(ci, cj, run, ni, nj, nd, beginsRun))
                        : (neighborNode * DirectionSlots) + neighborSlot;
                    if (IsValid(neighborId) && tentative >= s.Best[neighborId])
                    {
                        continue;
                    }

                    Record(neighborId, tentative, currentId);
                    open.Enqueue(neighborId, tentative + HeuristicAt(ni, nj));
                }
            }

            return (null, 0.0);
        }

        /// <summary>
        /// Starts the connectivity sweep: marks the start nodes and queues the goal nodes, from which the
        /// sweep spreads over the free edges of the grid (the hard blocks only: obstacles inflated by the
        /// clearance, wall strips), ignoring every cost, turn and minimum-segment rule. It is a relaxation
        /// of the A* search, so a search whose sweep runs dry can only fail.
        /// </summary>
        private void SeedProbe()
        {
            var s = _scratch;
            foreach (var start in _starts)
            {
                s.StartMark[(start.I * _ny) + start.J] = _stamp;
            }

            _probeHead = 0;
            _probeTail = 0;
            foreach (var goal in _goals)
            {
                var id = (goal.I * _ny) + goal.J;
                if (s.SeenMark[id] != _stamp)
                {
                    s.SeenMark[id] = _stamp;
                    s.Queue[_probeTail++] = id;
                }
            }

            _probeState = ProbeRunning;
        }

        /// <summary>
        /// Visits up to <paramref name="steps"/> more nodes of the connectivity sweep, settling
        /// <see cref="_probeState"/> as soon as the sweep reaches a start node or runs out of nodes.
        /// </summary>
        /// <param name="steps">The most nodes to visit.</param>
        private void AdvanceProbe(int steps)
        {
            var s = _scratch;
            for (var step = 0; step < steps; step++)
            {
                if (_probeHead == _probeTail)
                {
                    _probeState = ProbeDisconnected;
                    return;
                }

                var id = s.Queue[_probeHead++];
                _probeSteps++;
                if (s.StartMark[id] == _stamp)
                {
                    _probeState = ProbeConnected;
                    return;
                }

                var ci = id / _ny;
                var cj = id % _ny;
                for (var k = 0; k < 4; k++)
                {
                    var horizontal = k < 2;
                    var ni = ci + StepI[k];
                    var nj = cj + StepJ[k];
                    if (ni < 0 || ni >= _nx || nj < 0 || nj >= _ny)
                    {
                        continue;
                    }

                    var next = (ni * _ny) + nj;
                    if (s.SeenMark[next] == _stamp)
                    {
                        continue;
                    }

                    var edge = horizontal ? (Math.Min(ci, ni) * _ny) + cj : _nodes + (ci * _ny) + Math.Min(cj, nj);
                    if (EdgeIsBlocked(edge, Math.Min(ci, ni), Math.Min(cj, nj), Math.Max(ci, ni), Math.Max(cj, nj)))
                    {
                        continue;
                    }

                    s.SeenMark[next] = _stamp;
                    s.Queue[_probeTail++] = next;
                }
            }
        }

        /// <summary>
        /// Marks every grid edge a hard obstacle (inflated by the clearance) blocks. Each obstacle visits
        /// only the edges around it, found by binary search on the sorted grid lines, instead of every edge
        /// testing every obstacle; each candidate edge is tested with the original strict-inequality test, so
        /// the marks are exactly those the per-edge scan would give.
        /// </summary>
        private void RasterizeObstacles()
        {
            var blocked = _scratch.ObstacleBlocked;
            Array.Clear(blocked, 0, 2 * _nodes);
            var clearance = _clearance;
            foreach (var r in _obstacles)
            {
                var x0 = r.X - clearance;
                var x1 = r.X + r.Width + clearance;
                var y0 = r.Y - clearance;
                var y1 = r.Y + r.Height + clearance;

                // Horizontal edges (i, j)-(i + 1, j) on a row strictly inside the obstacle's y range
                var iFrom = Math.Max(0, LowerBound(_xs, x0) - 1);
                var iTo = Math.Min(_nx - 2, LowerBound(_xs, x1));
                var jFrom = Math.Max(0, LowerBound(_ys, y0));
                var jTo = Math.Min(_ny - 1, LowerBound(_ys, y1));
                for (var i = iFrom; i <= iTo; i++)
                {
                    for (var j = jFrom; j <= jTo; j++)
                    {
                        var y = _ys[j];
                        if (y0 < y && y < y1 && Math.Max(_xs[i], x0) < Math.Min(_xs[i + 1], x1))
                        {
                            blocked[(i * _ny) + j] = 1;
                        }
                    }
                }

                // Vertical edges (i, j)-(i, j + 1) on a column strictly inside the obstacle's x range
                iFrom = Math.Max(0, LowerBound(_xs, x0));
                iTo = Math.Min(_nx - 1, LowerBound(_xs, x1));
                jFrom = Math.Max(0, LowerBound(_ys, y0) - 1);
                jTo = Math.Min(_ny - 2, LowerBound(_ys, y1));
                for (var i = iFrom; i <= iTo; i++)
                {
                    var x = _xs[i];
                    if (!(x0 < x && x < x1))
                    {
                        continue;
                    }

                    for (var j = jFrom; j <= jTo; j++)
                    {
                        if (Math.Max(_ys[j], y0) < Math.Min(_ys[j + 1], y1))
                        {
                            blocked[_nodes + (i * _ny) + j] = 1;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Accumulates, per grid edge, the length by which it overlaps the soft obstacles (summed in the
        /// obstacles' order, as the per-edge scan sums them) and the number of existing lines it crosses at
        /// a right angle. Like <see cref="RasterizeObstacles"/>, each rectangle visits only the edges around
        /// it and every candidate is tested with the original expressions.
        /// </summary>
        private void RasterizeSoftTerms()
        {
            _softRasterized = true;
            var s = _scratch;
            if (_softObstacles.Count > 0)
            {
                Array.Clear(s.SoftOverlap, 0, 2 * _nodes);
                foreach (var r in _softObstacles)
                {
                    var right = r.X + r.Width;
                    var bottom = r.Y + r.Height;
                    var iFrom = Math.Max(0, LowerBound(_xs, r.X) - 1);
                    var iTo = Math.Min(_nx - 2, LowerBound(_xs, right));
                    var jFrom = Math.Max(0, LowerBound(_ys, r.Y));
                    var jTo = Math.Min(_ny - 1, LowerBound(_ys, bottom));
                    for (var i = iFrom; i <= iTo; i++)
                    {
                        for (var j = jFrom; j <= jTo; j++)
                        {
                            var y = _ys[j];
                            if (r.Y < y && y < bottom)
                            {
                                var overlap = Math.Min(_xs[i + 1], right) - Math.Max(_xs[i], r.X);
                                if (overlap > 0.0)
                                {
                                    s.SoftOverlap[(i * _ny) + j] += overlap;
                                }
                            }
                        }
                    }

                    iFrom = Math.Max(0, LowerBound(_xs, r.X));
                    iTo = Math.Min(_nx - 1, LowerBound(_xs, right));
                    jFrom = Math.Max(0, LowerBound(_ys, r.Y) - 1);
                    jTo = Math.Min(_ny - 2, LowerBound(_ys, bottom));
                    for (var i = iFrom; i <= iTo; i++)
                    {
                        var x = _xs[i];
                        if (!(r.X < x && x < right))
                        {
                            continue;
                        }

                        for (var j = jFrom; j <= jTo; j++)
                        {
                            var overlap = Math.Min(_ys[j + 1], bottom) - Math.Max(_ys[j], r.Y);
                            if (overlap > 0.0)
                            {
                                s.SoftOverlap[_nodes + (i * _ny) + j] += overlap;
                            }
                        }
                    }
                }
            }

            var existing = _extras?.ExistingLines;
            if (existing is null)
            {
                return;
            }

            Array.Clear(s.ExistingCrossings, 0, 2 * _nodes);
            foreach (var r in existing)
            {
                var right = r.X + r.Width;
                var bottom = r.Y + r.Height;
                if (r.Height > r.Width)
                {
                    // A vertical line is crossed by horizontal edges
                    var iFrom = Math.Max(0, LowerBound(_xs, r.X) - 1);
                    var iTo = Math.Min(_nx - 2, LowerBound(_xs, right));
                    var jFrom = Math.Max(0, LowerBound(_ys, r.Y));
                    var jTo = Math.Min(_ny - 1, LowerBound(_ys, bottom));
                    for (var i = iFrom; i <= iTo; i++)
                    {
                        for (var j = jFrom; j <= jTo; j++)
                        {
                            var y = _ys[j];
                            if (r.Y < y && y < bottom && Math.Min(_xs[i + 1], right) - Math.Max(_xs[i], r.X) > 0.0)
                            {
                                s.ExistingCrossings[(i * _ny) + j]++;
                            }
                        }
                    }
                }

                if (r.Width > r.Height)
                {
                    // A horizontal line is crossed by vertical edges
                    var iFrom = Math.Max(0, LowerBound(_xs, r.X));
                    var iTo = Math.Min(_nx - 1, LowerBound(_xs, right));
                    var jFrom = Math.Max(0, LowerBound(_ys, r.Y) - 1);
                    var jTo = Math.Min(_ny - 2, LowerBound(_ys, bottom));
                    for (var i = iFrom; i <= iTo; i++)
                    {
                        var x = _xs[i];
                        if (!(r.X < x && x < right))
                        {
                            continue;
                        }

                        for (var j = jFrom; j <= jTo; j++)
                        {
                            if (Math.Min(_ys[j + 1], bottom) - Math.Max(_ys[j], r.Y) > 0.0)
                            {
                                s.ExistingCrossings[_nodes + (i * _ny) + j]++;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Returns the index of the first line in the ascending <paramref name="lines"/> that is not below <paramref name="value"/>.</summary>
        private static int LowerBound(double[] lines, double value)
        {
            var lo = 0;
            var hi = lines.Length;
            while (lo < hi)
            {
                var mid = (lo + hi) >> 1;
                if (lines[mid] < value)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            return lo;
        }

        /// <summary>
        /// Returns whether the grid edge is blocked (by an obstacle inflated by the clearance, or by a wall
        /// strip), evaluating and memoizing it on first use.
        /// </summary>
        /// <param name="edge">The edge's memo index.</param>
        /// <param name="i1">The lower x index of its end nodes.</param>
        /// <param name="j1">The lower y index of its end nodes.</param>
        /// <param name="i2">The higher x index of its end nodes.</param>
        /// <param name="j2">The higher y index of its end nodes.</param>
        /// <returns><see langword="true"/> when the edge may not be used.</returns>
        private bool EdgeIsBlocked(int edge, int i1, int j1, int i2, int j2)
        {
            var s = _scratch;
            var mark = s.EdgeMark[edge];
            if ((mark >> 2) == _stamp)
            {
                return (mark & 3) == EdgeBlocked;
            }

            // Skip edges whose segment passes within the clearance of an obstacle, or runs along a wall strip.
            var blocked = s.ObstacleBlocked[edge] != 0
                || (_extras is not null && _extras.WallBlocked(_xs, _ys, i1, j1, i2, j2));
            s.EdgeMark[edge] = (_stamp << 2) | (blocked ? EdgeBlocked : EdgeFree);
            return blocked;
        }

        /// <summary>
        /// Ensures the cost components of a free grid edge are cached, evaluating them on first use.
        /// </summary>
        /// <param name="edge">The edge's memo index.</param>
        /// <param name="i1">The lower x index of its end nodes.</param>
        /// <param name="j1">The lower y index of its end nodes.</param>
        /// <param name="i2">The higher x index of its end nodes.</param>
        /// <param name="j2">The higher y index of its end nodes.</param>
        /// <param name="horizontal">Whether the edge runs along the x axis.</param>
        /// <returns><see langword="false"/> when the edge is blocked; otherwise its components are cached.</returns>
        private bool PriceEdge(int edge, int i1, int j1, int i2, int j2, bool horizontal)
        {
            var s = _scratch;
            if (EdgeIsBlocked(edge, i1, j1, i2, j2))
            {
                return false;
            }

            if ((s.EdgeMark[edge] & 3) == EdgePriced)
            {
                return true;
            }

            // The soft-obstacle terms of all edges are rasterized once, on the first edge priced
            if (!_softRasterized)
            {
                RasterizeSoftTerms();
            }

            var stepLength = horizontal
                ? Math.Abs(_xs[i2] - _xs[i1])
                : Math.Abs(_ys[j2] - _ys[j1]);
            var bandMultiplier = SegmentCostMultiplier(_xs, _ys, i1, j1, i2, j2, _costBands);
            var channelMultiplier = _extras is null ? 1.0 : _extras.WallDistanceMultiplier(_xs, _ys, i1, j1, i2, j2);
            var softOverlap = _softObstacles.Count > 0 ? s.SoftOverlap[edge] : 0.0;
            var softCost = softOverlap > 0.0
                ? SoftObstacleBaseCost + (softOverlap * SoftObstaclePerUnitLengthCost)
                : 0.0;
            var extraCost = 0.0;
            if (_extras is not null)
            {
                extraCost += _extras.ZonePenalty(_xs, _ys, i1, j1, !horizontal);
                if (_extras.CrossingPenalty > 0.0 && softOverlap > 0.0)
                {
                    extraCost += _extras.CrossingPenalty * SoftCrossingCount(_xs, _ys, i1, j1, i2, j2, _softObstacles);
                }

                if (_extras.ExistingLines is not null)
                {
                    extraCost += _extras.ExistingLinePenalty * s.ExistingCrossings[edge];
                }
            }

            s.EdgeStep[edge] = stepLength * bandMultiplier * channelMultiplier;
            s.EdgeSoft[edge] = softCost;
            s.EdgeExtra[edge] = extraCost;
            s.EdgeMark[edge] = (_stamp << 2) | EdgePriced;
            return true;
        }

        /// <summary>Manhattan-distance heuristic from a grid node to the nearest goal terminal, cached per node.</summary>
        private double HeuristicAt(int i, int j)
        {
            var s = _scratch;
            var node = (i * _ny) + j;
            if (s.HeuristicMark[node] == _stamp)
            {
                return s.Heuristic[node];
            }

            var min = double.PositiveInfinity;
            foreach (var goal in _goals)
            {
                min = Math.Min(min, Math.Abs(_xs[i] - _xs[goal.I]) + Math.Abs(_ys[j] - _ys[goal.J]));
            }

            s.Heuristic[node] = min;
            s.HeuristicMark[node] = _stamp;
            return min;
        }

        /// <summary>Returns whether the state was reached during this search.</summary>
        private bool IsValid(int id) => (_scratch.StateMark[id] >> 1) == _stamp;

        /// <summary>Records a (better) cost for a state and the state it came from, clearing its expanded flag.</summary>
        private void Record(int id, double cost, int from)
        {
            _scratch.Best[id] = cost;
            _scratch.From[id] = from;
            _scratch.StateMark[id] = _stamp << 1;
        }

        /// <summary>
        /// Returns the id of a state: dense (node and direction slot) when no short run is pending, else
        /// looked up, or created, in the run-pending table.
        /// </summary>
        private int StateId(int node, int slot, int run)
        {
            var nodeSlot = (node * DirectionSlots) + slot;
            if (run < 0)
            {
                return nodeSlot;
            }

            var s = _scratch;
            var key = ((long)nodeSlot << RunBits) | (uint)run;
            if (s.OverflowIds.TryGetValue(key, out var id))
            {
                return id;
            }

            var index = s.OverflowCount++;
            id = (_nodes * DirectionSlots) + index;
            s.EnsureStateCapacity(id);
            s.EnsureOverflowCapacity(index);
            s.OverflowNodeSlot[index] = nodeSlot;
            s.OverflowRun[index] = run;
            s.StateMark[id] = 0;
            s.OverflowIds[key] = id;
            return id;
        }

        /// <summary>Splits a state id into its node, direction slot and pending run (-1 for none).</summary>
        private (int Node, int Slot, int Run) Decode(int id)
        {
            var dense = _nodes * DirectionSlots;
            if (id < dense)
            {
                return (id / DirectionSlots, id % DirectionSlots, -1);
            }

            var index = id - dense;
            var nodeSlot = _scratch.OverflowNodeSlot[index];
            return (nodeSlot / DirectionSlots, nodeSlot % DirectionSlots, _scratch.OverflowRun[index]);
        }

        /// <summary>Returns the axis of travel a direction slot stands for.</summary>
        private static Dir SlotDirection(int slot) => slot switch
        {
            SlotNone => Dir.None,
            SlotDone => Dir.Done,
            1 or 2 => Dir.Horizontal,
            _ => Dir.Vertical,
        };

        /// <summary>Returns the sign of travel of a direction slot: 1 forward, -1 backward, 0 for the start and done slots.</summary>
        private int SlotSign(int slot)
        {
            // Only a search enforcing a minimum interior segment tells the two travel senses apart
            if (_minSegment <= 0.0 || slot is SlotNone or SlotDone)
            {
                return 0;
            }

            return (slot & 1) == 1 ? 1 : -1;
        }

        /// <summary>
        /// Returns the run component of the state reached by a move: the grid index (along the move's axis)
        /// where the run being drawn began while it is still shorter than the minimum interior segment, or
        /// -1 once it is long enough, when no minimum is enforced, or when the run continues a stub (the end
        /// leg). A run begins where the path turns, or where its first move leaves across the start stub.
        /// </summary>
        private int NextRun(int ci, int cj, int run, int ni, int nj, Dir nd, bool beginsRun)
        {
            if (_minSegment <= 0.0 || (!beginsRun && run < 0))
            {
                return -1;
            }

            var horizontal = nd == Dir.Horizontal;
            var axis = horizontal ? _xs : _ys;
            var runEnd = horizontal ? ni : nj;
            var runStart = run;
            if (beginsRun)
            {
                runStart = horizontal ? ci : cj;
            }

            return Math.Abs(axis[runEnd] - axis[runStart]) >= _minSegment - 1e-9 ? -1 : runStart;
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
        /// Returns whether finishing at <paramref name="goal"/> after a move along <paramref name="direction"/>
        /// with the pending run <paramref name="run"/> would leave a too-short interior run: the run being drawn
        /// is still shorter than the minimum and crosses the goal's stub axis, so the stub (not the run) would
        /// be the final leg. A run that continues the stub is part of the end leg and is not an interior run.
        /// </summary>
        private static bool EndsOnShortInteriorRun(Terminal goal, Dir direction, int run) =>
            run >= 0 && goal.StubAxis != Dir.None && goal.StubAxis != direction;

        /// <summary>Reconstructs the grid-point path by walking the came-from chain back to the start.</summary>
        private List<Point2D> Reconstruct(int goalId)
        {
            var points = new List<Point2D>();
            var id = goalId;
            while (id >= 0)
            {
                var (node, slot, _) = Decode(id);
                if (slot != SlotDone)
                {
                    points.Add(new Point2D(_xs[node / _ny], _ys[node % _ny]));
                }

                id = _scratch.From[id];
            }

            points.Reverse();
            return points;
        }
    }

    /// <summary>
    /// Counts the soft (already-routed) segments the move crosses at a right angle, as opposed to
    /// running along them.
    /// </summary>
    internal static int SoftCrossingCount(
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

    /// <summary>
    /// Returns the cheapest cost multiplier for a segment by testing whether its midpoint lies inside
    /// any cost band; a highway band (multiplier &lt; 1) makes bundled runs cheaper so the router
    /// prefers them. When no band applies the cost is neutral (1.0).
    /// </summary>
    internal static double SegmentCostMultiplier(
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
    internal static bool SegmentBlocked(
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
    internal static double SoftObstacleOverlapLength(
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
