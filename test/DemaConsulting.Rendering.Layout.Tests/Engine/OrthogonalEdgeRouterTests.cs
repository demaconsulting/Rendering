// <copyright file="OrthogonalEdgeRouterTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Layout.Engine;

namespace DemaConsulting.Rendering.Layout.Tests.Engine;

/// <summary>
///     Tests for <see cref="OrthogonalEdgeRouter"/> orthogonal edge routing.
/// </summary>
public sealed class OrthogonalEdgeRouterTests
{
    /// <summary>
    ///     Two ends on the same side that are level along the face (two boxes with aligned left edges joined
    ///     left to left) step off their faces and wrap around, instead of collapsing to a straight segment lying
    ///     on the faces themselves (issue #53: a connector overflowed to the same side of two aligned boxes).
    /// </summary>
    [Fact]
    public void RouteWithStatus_SameSideAlignedEnds_StepOffInsteadOfRunningAlongFaces()
    {
        // Arrange: two boxes with their left edges at x = 12, anchors on those left faces.
        var upper = new Rect(12, 12, 130, 50);
        var lower = new Rect(12, 86, 130, 242);
        var source = new Point2D(12, 37);
        var target = new Point2D(12, 207);

        // Act
        var result = OrthogonalEdgeRouter.RouteWithStatus(source, target, [upper, lower], 12, PortSide.Left, PortSide.Left);

        // Assert: the route leaves to the left of both faces and comes back, rather than running along x = 12.
        Assert.False(result.Crossed);
        Assert.True(result.Waypoints.Count >= 4, "Expected a C-shaped route with stubs off both faces.");
        Assert.True(result.Waypoints[1].X < 12.0, "The first leg must leave the left face, not run along it.");
        Assert.True(result.Waypoints[^2].X < 12.0, "The last leg must enter the left face from outside.");
    }

    /// <summary>
    ///     A budget that is already exhausted stops the search before it starts: the connector takes the
    ///     elbow fallback (flagged as crossing) and the budget counts it.
    /// </summary>
    [Fact]
    public void RouteWithStatus_ExhaustedBudget_FallsBackToElbow()
    {
        // Arrange: an obstacle between the ends and a budget already past its hard limit
        var source = new Point2D(0, 50);
        var target = new Point2D(200, 100);
        var obstacles = new[] { new Rect(80, 0, 40, 160) };
        var budget = new RoutingWorkBudget(new RoutingWorkLimits(Soft: 10, Hard: 20));
        budget.Charge(20);

        // Act
        var result = OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, 10, budget: budget);

        // Assert: an orthogonal elbow between the ends, flagged as a fallback and counted by the budget
        Assert.True(result.Crossed);
        AssertEndpoints(result.Waypoints, source, target);
        AssertAllSegmentsOrthogonal(result.Waypoints);
        Assert.Equal(1, budget.ElbowRoutes);
        Assert.Equal(RoutingTier.Minimal, budget.LowestTier);
    }

    /// <summary>
    ///     A budget that runs out in the middle of a search stops it at the same point on every run (the
    ///     limit counts work, never time), so the fallback route is reproducible.
    /// </summary>
    [Fact]
    public void RouteWithStatus_BudgetRunsOutDuringSearch_FallsBackIdenticallyOnEveryRun()
    {
        // Arrange: a wide field of obstacles and a hard limit far below the work its search takes
        var source = new Point2D(0, 0);
        var target = new Point2D(600, 600);
        var obstacles = Enumerable.Range(0, 12).Select(i => new Rect(40 + (i * 45), 20 + ((i % 4) * 130), 30, 400)).ToArray();

        IReadOnlyList<Point2D> Run(out RoutingWorkBudget spent)
        {
            spent = new RoutingWorkBudget(new RoutingWorkLimits(Soft: 100, Hard: 200));
            return OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, 10, budget: spent).Waypoints;
        }

        // Act
        var first = Run(out var firstBudget);
        var second = Run(out var secondBudget);

        // Assert: both runs stopped at the limit with the same elbow, having spent the same work
        Assert.Equal(first, second);
        Assert.Equal(RoutingTier.Minimal, firstBudget.LowestTier);
        Assert.Equal(firstBudget.Spent, secondBudget.Spent);
        AssertAllSegmentsOrthogonal(first);
    }

    /// <summary>
    ///     However small the hard limit, the work spent overshoots it by less than one charge chunk (64
    ///     expansions, each with four connectivity steps) plus nothing for the grid build, which is clamped
    ///     to the work that is left, and the fallback is the same on every run.
    /// </summary>
    /// <param name="hard">The hard work limit.</param>
    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(10L)]
    [InlineData(100L)]
    [InlineData(1_000L)]
    [InlineData(5_000L)]
    [InlineData(10_000L)]
    public void RouteWithStatus_HardLimitMidSearch_OvershootsByLessThanOneChunk(long hard)
    {
        // Arrange: a field of 1,600 pillars crossed corner to corner, whose search spends
        // about 15,000 units unbudgeted, so every limit below cuts it off
        var source = new Point2D(10, 10);
        var target = new Point2D(790, 790);
        var obstacles = Enumerable.Range(0, 1600).Select(i => new Rect(40 + ((i % 40) * 20), 40 + ((i / 40) * 20), 10, 10)).ToArray();

        IReadOnlyList<Point2D> Run(out RoutingWorkBudget spent)
        {
            spent = new RoutingWorkBudget(new RoutingWorkLimits(Soft: hard / 2, Hard: hard));
            return OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, 4, budget: spent).Waypoints;
        }

        // Act
        var first = Run(out var firstBudget);
        var second = Run(out var secondBudget);

        // Assert: stopped within one chunk of the limit (64 * (1 + 4) = 320 units), identically
        Assert.True(firstBudget.Spent <= hard + 320, $"Spent {firstBudget.Spent} units against a hard limit of {hard}.");
        Assert.Equal(RoutingTier.Minimal, firstBudget.Tier);
        Assert.Equal(first, second);
        Assert.Equal(firstBudget.Spent, secondBudget.Spent);
        AssertAllSegmentsOrthogonal(first);
    }

    /// <summary>
    ///     A budget whose token is canceled stops the search it serves with an <see cref="OperationCanceledException"/>.
    /// </summary>
    [Fact]
    public void RouteWithStatus_CanceledBudget_StopsTheSearch()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        source.Cancel();
        var budget = new RoutingWorkBudget(new RoutingWorkLimits(Soft: long.MaxValue / 4, Hard: long.MaxValue / 2), source.Token);

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() =>
            OrthogonalEdgeRouter.RouteWithStatus(new Point2D(0, 50), new Point2D(300, 50), [new Rect(100, 0, 40, 100)], 10, budget: budget));
    }

    /// <summary>
    ///     A grid whose tables take more than the retention limit (32 MB) is released after its search, not
    ///     kept alive on the thread (a 450 by 450 node grid kept for the life of a thread took over 40 MB).
    /// </summary>
    [Fact]
    public void RouteWithStatus_LargeGrid_ScratchIsNotRetained()
    {
        OnFreshThread(() =>
        {
            // Act
            RouteAcrossGrid(LargeGridObstacles);

            // Assert
            Assert.Equal(0L, OrthogonalEdgeRouter.RetainedScratchBytes);
        });
    }

    /// <summary>
    ///     A small grid's tables are kept for reuse, within the retention limit.
    /// </summary>
    [Fact]
    public void RouteWithStatus_SmallGrid_ScratchIsRetainedWithinLimit()
    {
        OnFreshThread(() =>
        {
            // Act
            RouteAcrossGrid(SmallGridObstacles);

            // Assert
            var retained = OrthogonalEdgeRouter.RetainedScratchBytes;
            Assert.True(retained > 0, "No scratch was kept for reuse.");
            Assert.True(retained <= RetainedByteLimit, $"{retained} bytes are kept.");
        });
    }

    /// <summary>
    ///     A search after a large one finds exactly the route it finds on a fresh thread: dropping the large
    ///     scratch leaves nothing behind that changes a result.
    /// </summary>
    [Fact]
    public void RouteWithStatus_SmallGridAfterLargeGrid_RouteIsUnchanged()
    {
        // Arrange: the route of the small grid on a thread that has searched nothing else
        IReadOnlyList<Point2D>? fresh = null;
        OnFreshThread(() => fresh = RouteAcrossGrid(SmallGridObstacles));

        OnFreshThread(() =>
        {
            // Act: the same search after a large one
            RouteAcrossGrid(LargeGridObstacles);
            var after = RouteAcrossGrid(SmallGridObstacles);

            // Assert
            Assert.Equal(fresh, after);
        });
    }

    /// <summary>The most scratch, in bytes, the router keeps per thread (32 MB).</summary>
    private const long RetainedByteLimit = 32L * 1024 * 1024;

    /// <summary>Obstacles whose grid lines make a grid of several hundred thousand nodes (over 100 MB of tables).</summary>
    private static readonly Rect[] LargeGridObstacles =
        [.. Enumerable.Range(0, 500).Select(i => new Rect(100 + (i * 30), 100 + (i * 30), 10, 10))];

    /// <summary>A handful of obstacles whose grid is small.</summary>
    private static readonly Rect[] SmallGridObstacles = [new Rect(100, 0, 40, 100), new Rect(180, 20, 40, 120)];

    /// <summary>Routes a connector from one corner of the field of <paramref name="obstacles"/> to a point near it.</summary>
    /// <param name="obstacles">The obstacles.</param>
    /// <returns>The waypoints.</returns>
    private static IReadOnlyList<Point2D> RouteAcrossGrid(Rect[] obstacles) =>
        OrthogonalEdgeRouter.RouteWithStatus(new Point2D(0, 50), new Point2D(300, 50), obstacles, 10).Waypoints;

    /// <summary>
    ///     Runs <paramref name="body"/> on a thread of its own, so the thread-static scratch the router keeps
    ///     is not shared with the other tests, and rethrows what it threw.
    /// </summary>
    /// <param name="body">The test body.</param>
    private static void OnFreshThread(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    /// <summary>
    ///     A budget with room to spare leaves the route exactly as it is without a budget, and counts the
    ///     work it took.
    /// </summary>
    [Fact]
    public void RouteWithStatus_GenerousBudget_RouteEqualsUnbudgetedRoute()
    {
        // Arrange
        var source = new Point2D(0, 50);
        var target = new Point2D(300, 50);
        var obstacles = new[] { new Rect(100, 0, 40, 100), new Rect(180, 20, 40, 120) };
        var budget = new RoutingWorkBudget(new RoutingWorkLimits(Soft: long.MaxValue / 4, Hard: long.MaxValue / 2));

        // Act
        var unbudgeted = OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, 10);
        var budgeted = OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, 10, budget: budget);

        // Assert
        Assert.Equal(unbudgeted.Waypoints, budgeted.Waypoints);
        Assert.False(budgeted.Crossed);
        Assert.True(budget.Spent > 0);
        Assert.Equal(RoutingTier.Full, budget.LowestTier);
    }

    /// <summary>
    ///     At the reduced effort a connector is routed in a window around its two ends: the route is still
    ///     orthogonal and clear of every obstacle, including those the window leaves out.
    /// </summary>
    [Fact]
    public void RouteWithStatus_ReducedBudget_RoutesAroundObstaclesInWindow()
    {
        // Arrange: obstacles between the ends and one far outside the window around them
        var source = new Point2D(0, 50);
        var target = new Point2D(300, 50);
        var obstacles = new[] { new Rect(100, -50, 40, 200), new Rect(180, -40, 40, 200), new Rect(5000, 5000, 100, 100) };
        var budget = new RoutingWorkBudget(new RoutingWorkLimits(Soft: long.MaxValue / 4, Hard: long.MaxValue / 2, ReducedWeight: 1));
        budget.NoteScopeWeight(1);

        // Act
        var result = OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, 10, budget: budget);

        // Assert
        Assert.Equal(RoutingTier.Reduced, budget.Tier);
        Assert.False(result.Crossed);
        AssertEndpoints(result.Waypoints, source, target);
        AssertAllSegmentsOrthogonal(result.Waypoints);
        AssertNoSegmentCrossesObstacle(result.Waypoints, obstacles);
    }

    /// <summary>
    ///     A route whose ends the window cannot join (a wall of obstacles wider than the window) is searched
    ///     again over the whole canvas, so the reduced effort never turns a routable connector into a fallback.
    /// </summary>
    [Fact]
    public void RouteWithStatus_ReducedBudget_WindowTooSmall_SearchesWholeCanvas()
    {
        // Arrange: a long wall between the ends, reaching far beyond the window around them
        var source = new Point2D(0, 0);
        var target = new Point2D(100, 0);
        var obstacles = new[] { new Rect(40, -2000, 20, 2150) };
        var budget = new RoutingWorkBudget(new RoutingWorkLimits(Soft: long.MaxValue / 4, Hard: long.MaxValue / 2, ReducedWeight: 1));
        budget.NoteScopeWeight(1);

        // Act
        var result = OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, 10, budget: budget);

        // Assert: the route goes round the wall below its end
        Assert.False(result.Crossed);
        AssertEndpoints(result.Waypoints, source, target);
        AssertNoSegmentCrossesObstacle(result.Waypoints, obstacles);
    }

    /// <summary>
    ///     At the reduced effort a connector among hundreds of already-routed lines (soft obstacles) is still
    ///     routed orthogonally, clear of every hard obstacle, identically on every run, and for less work than
    ///     the full effort spends on the same scene: the soft lanes of the grid are limited to the nearest
    ///     perpendicular lines instead of one line per soft rectangle.
    /// </summary>
    [Fact]
    public void RouteWithStatus_ReducedBudget_HundredsOfSoftRects_RoutesValidlyForLessWork()
    {
        // Arrange: a wall between the ends and 300 thin soft rectangles (tall and wide) across the canvas
        var source = new Point2D(0, 100);
        var target = new Point2D(1200, 100);
        var obstacles = new[] { new Rect(560, 20, 40, 160) };
        var soft = new List<Rect>();
        for (var i = 0; i < 150; i++)
        {
            soft.Add(new Rect(20 + (i * 7), 10 + ((i % 5) * 30), 2, 70));
            soft.Add(new Rect(10 + (i * 8), 15 + ((i % 7) * 25), 60, 2));
        }

        RoutingWorkBudget Reduced()
        {
            var reduced = new RoutingWorkBudget(new RoutingWorkLimits(Soft: long.MaxValue / 4, Hard: long.MaxValue / 2, ReducedWeight: 1));
            reduced.NoteScopeWeight(1);
            return reduced;
        }

        var first = Reduced();
        var second = Reduced();
        var full = new RoutingWorkBudget(new RoutingWorkLimits(Soft: long.MaxValue / 4, Hard: long.MaxValue / 2));

        // Act
        var result = OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, 10, softObstacles: soft, budget: first);
        var again = OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, 10, softObstacles: soft, budget: second);
        _ = OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, 10, softObstacles: soft, budget: full);

        // Assert: a valid route, the same twice, found for less work than the full effort spends
        Assert.Equal(RoutingTier.Reduced, first.Tier);
        Assert.False(result.Crossed);
        AssertEndpoints(result.Waypoints, source, target);
        AssertAllSegmentsOrthogonal(result.Waypoints);
        AssertNoSegmentCrossesObstacle(result.Waypoints, obstacles);
        Assert.Equal(result.Waypoints, again.Waypoints);
        Assert.Equal(first.Spent, second.Spent);
        Assert.True(first.Spent < full.Spent, $"The reduced effort spent {first.Spent} units, the full effort {full.Spent}.");
    }

    /// <summary>
    ///     The soft-lane limits belong to the reduced effort alone: with the same hundreds of soft rectangles
    ///     a budget at the full effort routes exactly as no budget does.
    /// </summary>
    [Fact]
    public void RouteWithStatus_FullBudget_HundredsOfSoftRects_RouteEqualsUnbudgetedRoute()
    {
        // Arrange
        var source = new Point2D(0, 100);
        var target = new Point2D(600, 100);
        var obstacles = new[] { new Rect(260, 20, 40, 160) };
        var soft = new List<Rect>();
        for (var i = 0; i < 100; i++)
        {
            soft.Add(new Rect(20 + (i * 5), 10 + ((i % 5) * 30), 2, 70));
            soft.Add(new Rect(10 + (i * 5), 15 + ((i % 7) * 25), 60, 2));
        }

        var budget = new RoutingWorkBudget(new RoutingWorkLimits(Soft: long.MaxValue / 4, Hard: long.MaxValue / 2));

        // Act
        var unbudgeted = OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, 10, softObstacles: soft);
        var budgeted = OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, 10, softObstacles: soft, budget: budget);

        // Assert
        Assert.Equal(RoutingTier.Full, budget.LowestTier);
        Assert.Equal(unbudgeted.Waypoints, budgeted.Waypoints);
    }

    /// <summary>
    ///     A route whose ends the window cannot join is searched over the whole canvas, still with the
    ///     reduced soft lanes, and still finds the way round a wall among hundreds of soft rectangles.
    /// </summary>
    [Fact]
    public void RouteWithStatus_ReducedBudget_HundredsOfSoftRects_WindowTooSmall_SearchesWholeCanvas()
    {
        // Arrange: a long wall between the ends and 200 soft rectangles near it
        var source = new Point2D(0, 0);
        var target = new Point2D(100, 0);
        var obstacles = new[] { new Rect(40, -2000, 20, 2150) };
        var soft = Enumerable.Range(0, 200).Select(i => new Rect(-20 + (i * 2), -40 + ((i % 9) * 10), 2, 30)).ToList();
        var budget = new RoutingWorkBudget(new RoutingWorkLimits(Soft: long.MaxValue / 4, Hard: long.MaxValue / 2, ReducedWeight: 1));
        budget.NoteScopeWeight(1);

        // Act
        var result = OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, 10, softObstacles: soft, budget: budget);

        // Assert: the route goes round the wall below its end
        Assert.False(result.Crossed);
        AssertEndpoints(result.Waypoints, source, target);
        AssertAllSegmentsOrthogonal(result.Waypoints);
        AssertNoSegmentCrossesObstacle(result.Waypoints, obstacles);
    }

    /// <summary>
    ///     A route with no obstacles still produces a valid orthogonal path from source to target.
    /// </summary>
    [Fact]
    public void Route_NoObstacles_ProducesOrthogonalPath()
    {
        // Act: route between two diagonal points with no obstacles
        var path = OrthogonalEdgeRouter.Route(new Point2D(0, 0), new Point2D(100, 80), [], clearance: 10);

        // Assert: path starts at source, ends at target, and every segment is axis-aligned
        AssertEndpoints(path, new Point2D(0, 0), new Point2D(100, 80));
        AssertAllSegmentsOrthogonal(path);
    }

    /// <summary>
    ///     With an obstacle directly between source and target, the route avoids the obstacle interior.
    /// </summary>
    [Fact]
    public void Route_ObstacleBetween_RoutesAround()
    {
        // Arrange: an obstacle squarely between the horizontal line from source to target
        var source = new Point2D(0, 50);
        var target = new Point2D(200, 50);
        var obstacles = new[] { new Rect(80, 0, 40, 100) };

        // Act
        var path = OrthogonalEdgeRouter.Route(source, target, obstacles, clearance: 10);

        // Assert: valid orthogonal path that does not cross the obstacle interior
        AssertEndpoints(path, source, target);
        AssertAllSegmentsOrthogonal(path);
        AssertNoSegmentCrossesObstacle(path, obstacles);
    }

    /// <summary>
    ///     With multiple staggered obstacles, the route remains orthogonal and obstacle-free.
    /// </summary>
    [Fact]
    public void Route_MultipleObstacles_RemainsValid()
    {
        // Arrange: several obstacles forming a partial maze between source and target
        var source = new Point2D(0, 0);
        var target = new Point2D(300, 200);
        var obstacles = new[]
        {
            new Rect(60, -20, 40, 160),
            new Rect(160, 60, 40, 200),
            new Rect(220, 0, 40, 120),
        };

        // Act
        var path = OrthogonalEdgeRouter.Route(source, target, obstacles, clearance: 12);

        // Assert
        AssertEndpoints(path, source, target);
        AssertAllSegmentsOrthogonal(path);
        AssertNoSegmentCrossesObstacle(path, obstacles);
    }

    /// <summary>
    ///     Horizontally aligned endpoints with no obstacle produce a single straight segment.
    /// </summary>
    [Fact]
    public void Route_AlignedEndpoints_ProducesStraightLine()
    {
        // Act: source and target share a Y coordinate with no obstacles
        var path = OrthogonalEdgeRouter.Route(new Point2D(0, 30), new Point2D(150, 30), [], clearance: 10);

        // Assert: a simple two-point straight segment
        Assert.Equal(2, path.Count);
        AssertEndpoints(path, new Point2D(0, 30), new Point2D(150, 30));
    }

    /// <summary>
    ///     When a source side is given, the route leaves the source with a perpendicular stub: the
    ///     first segment runs in the side's outward direction.
    /// </summary>
    [Fact]
    public void Route_WithSourceSide_LeavesPerpendicular()
    {
        // Arrange: source on the right side of its box, target up and to the right
        var source = new Point2D(100, 100);
        var target = new Point2D(200, 20);

        // Act: the source anchor is on the Right side, so the first move must go right (+x)
        var path = OrthogonalEdgeRouter.Route(source, target, [], clearance: 10, sourceSide: PortSide.Right);

        // Assert: first segment is horizontal and heads to the right (outward from the Right side)
        Assert.True(path.Count >= 2);
        Assert.Equal(source.X, path[0].X, 6);
        Assert.Equal(source.Y, path[0].Y, 6);
        Assert.Equal(path[0].Y, path[1].Y, 6); // horizontal first segment
        Assert.True(path[1].X > path[0].X, "First segment should leave the Right side going right.");
        AssertAllSegmentsOrthogonal(path);
    }

    /// <summary>
    ///     When a target side is given, the route enters the target with a perpendicular stub: the
    ///     last segment runs into the side's inward direction.
    /// </summary>
    [Fact]
    public void Route_WithTargetSide_EntersPerpendicular()
    {
        // Arrange: target on the top side of its box, source below-left
        var source = new Point2D(20, 200);
        var target = new Point2D(150, 100);

        // Act: the target anchor is on the Top side, so the last move must arrive going down (+y)
        var path = OrthogonalEdgeRouter.Route(source, target, [], clearance: 10, targetSide: PortSide.Top);

        // Assert: last segment is vertical and arrives from above (entering the Top side)
        Assert.Equal(target.X, path[^1].X, 6);
        Assert.Equal(target.Y, path[^1].Y, 6);
        Assert.Equal(path[^1].X, path[^2].X, 6); // vertical last segment
        Assert.True(path[^2].Y < path[^1].Y, "Last segment should enter the Top side from above.");
        AssertAllSegmentsOrthogonal(path);
    }

    /// <summary>
    ///     A clean route (no blocking obstacle) reports it did not cross via RouteWithStatus.
    /// </summary>
    [Fact]
    public void RouteWithStatus_NoBlockingObstacle_ReportsNotCrossed()
    {
        // Act: route around a single obstacle that a channel exists past
        var result = OrthogonalEdgeRouter.RouteWithStatus(
            new Point2D(0, 50), new Point2D(200, 50), [new Rect(80, 0, 40, 100)], clearance: 10);

        // Assert: a valid orthogonal route was found, so Crossed is false
        Assert.False(result.Crossed);
        AssertAllSegmentsOrthogonal(result.Waypoints);
    }

    /// <summary>
    ///     An obstacle squarely between the endpoints is routed around (not crossed), demonstrating
    ///     the clearance-retry robustness.
    /// </summary>
    [Fact]
    public void RouteWithStatus_ObstacleBetween_RoutesAroundWithoutCrossing()
    {
        // Arrange: an obstacle blocking the straight path but with room to route around
        var obstacles = new[] { new Rect(40, 40, 40, 40) };

        // Act
        var result = OrthogonalEdgeRouter.RouteWithStatus(
            new Point2D(0, 60), new Point2D(120, 60), obstacles, clearance: 8);

        // Assert: routed cleanly (no crossing) and no segment passes through the obstacle interior
        Assert.False(result.Crossed);
        AssertNoSegmentCrossesObstacle(result.Waypoints, obstacles);
    }

    /// <summary>
    ///     When the target lies inside an obstacle (no obstacle-free approach exists), the router
    ///     reports that it had to cross.
    /// </summary>
    [Fact]
    public void RouteWithStatus_TargetEnclosedByObstacle_ReportsCrossed()
    {
        // Arrange: an obstacle that fully encloses the target point
        var obstacles = new[] { new Rect(50, 0, 200, 100) };

        // Act: target (100, 50) is strictly inside the obstacle
        var result = OrthogonalEdgeRouter.RouteWithStatus(
            new Point2D(0, 50), new Point2D(100, 50), obstacles, clearance: 10);

        // Assert: no clean path exists, so Crossed is reported true
        Assert.True(result.Crossed);
    }

    /// <summary>
    ///     A highway cost band makes one detour cheaper, so the router prefers routing through the band
    ///     rather than the equal-length alternative on the opposite side of the obstacle.
    /// </summary>
    [Fact]
    public void RouteWithStatus_HighwayBand_PrefersBandedDetour()
    {
        // Arrange: an obstacle blocks the straight line; a cheaper band lies on the +y (downward) side
        var source = new Point2D(0, 0);
        var target = new Point2D(200, 0);
        var obstacles = new[] { new Rect(80, -30, 40, 60) };
        var bands = new[] { new CostBand(IsHorizontal: true, Start: 40, End: 80, Multiplier: 0.6) };

        // Act: route with the discounted band biasing the detour
        var result = OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, clearance: 10, costBands: bands);

        // Assert: the route dips downward into the cheaper band instead of detouring up
        Assert.False(result.Crossed);
        Assert.Contains(result.Waypoints, p => p.Y > 0);
    }

    /// <summary>
    ///     A clean route keeps the requested clearance from obstacles it passes, rather than grazing
    ///     their edges.
    /// </summary>
    [Fact]
    public void RouteWithStatus_CleanRoute_KeepsClearanceFromObstacles()
    {
        // Arrange: source sits just to the right of an obstacle; a straight drop would graze it.
        var obstacle = new Rect(0, 40, 60, 80);
        var obstacles = new[] { obstacle };

        // Act: route from above-right of the obstacle to below it.
        var result = OrthogonalEdgeRouter.RouteWithStatus(
            new Point2D(62, 0), new Point2D(30, 200), obstacles, clearance: 10);

        // Assert: routed cleanly and every segment stays at least (nearly) the clearance away.
        Assert.False(result.Crossed);
        for (var i = 0; i < result.Waypoints.Count - 1; i++)
        {
            Assert.True(
                SegmentDistanceToRect(result.Waypoints[i], result.Waypoints[i + 1], obstacle) > 10.0 - 1e-6,
                $"Segment {i} runs closer than the clearance to the obstacle.");
        }
    }

    /// <summary>
    ///     A route steered by soft-obstacle penalties does not publish a redundant leave-and-return
    ///     waypoint loop that revisits the same point before continuing.
    /// </summary>
    [Fact]
    public void RouteWithStatus_SoftObstacleDetour_DoesNotRevisitWaypoint()
    {
        // Arrange: a vertical route whose preferred corridor is marked as a soft obstacle, representing
        // an already-routed connector that a later connector should prefer to steer around without
        // publishing a redundant excursion that leaves and returns to the same point.
        var source = new Point2D(100, 0);
        var target = new Point2D(100, 200);
        var softObstacles = new[] { new Rect(99, 20, 2, 160) };

        // Act
        var result = OrthogonalEdgeRouter.RouteWithStatus(
            source,
            target,
            obstacles: [],
            clearance: 10,
            sourceSide: PortSide.Bottom,
            targetSide: PortSide.Top,
            softObstacles: softObstacles);

        // Assert: the path is valid and never visits, leaves, and later revisits the same point.
        Assert.False(result.Crossed);
        AssertAllSegmentsOrthogonal(result.Waypoints);
        AssertNoWaypointRevisit(result.Waypoints);
    }

    /// <summary>
    ///     Pins the length-proportional soft-obstacle cost fix directly: when a soft obstacle occupies
    ///     the entire natural straight corridor for an extended span (hundreds of pixels), the router
    ///     must prefer a bounded-cost detour to an alternate lane rather than riding the long overlap.
    ///     Before the fix, the flat per-move <c>SoftObstaclePenalty</c> underpriced a long overlap
    ///     relative to the (roughly fixed) cost of a one-lane detour, so the search always kept the
    ///     overlap regardless of its length; this reproduces that exact mechanism with a synthetic soft
    ///     obstacle standing in for an already-routed connector's corridor.
    /// </summary>
    [Fact]
    public void RouteWithStatus_LongSoftObstacleOverlap_PrefersAlternateLane()
    {
        // Arrange: a straight horizontal route whose entire natural corridor (y = 0) is claimed by a
        // long soft obstacle for most of its length; an alternate lane is reachable at y = +/-11 (the
        // soft obstacle's own clearance-offset edge, added to the grid by BuildAxis) for a bounded
        // detour cost (two turns plus a short perpendicular jog).
        var source = new Point2D(0, 0);
        var target = new Point2D(500, 0);
        var softObstacles = new[] { new Rect(50, -1, 400, 2) };

        // Act
        var result = OrthogonalEdgeRouter.RouteWithStatus(
            source, target, obstacles: [], clearance: 10, softObstacles: softObstacles);

        // Assert: a valid orthogonal route was found (soft obstacles never hard-block).
        Assert.False(result.Crossed);
        AssertAllSegmentsOrthogonal(result.Waypoints);

        // Assert: no segment of the route rides the long soft obstacle for more than a trivial span —
        // the router must have detoured to the alternate lane instead of eating the full 400px overlap.
        var longestOverlap = LongestOverlapWithRect(result.Waypoints, softObstacles[0]);
        Assert.True(
            longestOverlap < 20.0,
            $"Route rides the soft obstacle for {longestOverlap:F1}px; expected a detour to an alternate lane.");
    }

    /// <summary>
    ///     Companion to <see cref="RouteWithStatus_LongSoftObstacleOverlap_PrefersAlternateLane"/>:
    ///     confirms the fix's proportional cost does not over-correct into penalizing short, incidental
    ///     soft-obstacle overlaps into pointless detours — a handful of overlapping pixels must still be
    ///     cheaper than the bounded detour cost, preserving the router's tolerance for legitimate shared
    ///     corridors near a common face.
    /// </summary>
    [Fact]
    public void RouteWithStatus_ShortSoftObstacleOverlap_KeepsStraightRoute()
    {
        // Arrange: the same straight corridor, but the soft obstacle now spans only a few pixels near
        // the midpoint — an incidental overlap rather than a long shared trunk.
        var source = new Point2D(0, 0);
        var target = new Point2D(500, 0);
        var softObstacles = new[] { new Rect(248, -1, 4, 2) };

        // Act
        var result = OrthogonalEdgeRouter.RouteWithStatus(
            source, target, obstacles: [], clearance: 10, softObstacles: softObstacles);

        // Assert: still a valid, non-crossing route.
        Assert.False(result.Crossed);
        AssertAllSegmentsOrthogonal(result.Waypoints);

        // Assert: the route stays on the direct y = 0 corridor rather than detouring for a trivial
        // overlap — every waypoint remains on the straight line.
        Assert.All(result.Waypoints, p => Assert.Equal(0.0, p.Y, 6));
    }

    /// <summary>
    ///     Direct, low-level regression test for the connector-router "coiled/looping" robustness bug
    ///     (see <c>OrthogonalEdgeRouter.AddSoftCandidate</c> and
    ///     <c>OrthogonalEdgeRouter.EnvelopeDepartureCost</c>), isolated from
    ///     <c>ConnectorRouter</c>'s anchor-distribution logic by calling <see cref="OrthogonalEdgeRouter.RouteWithStatus"/>
    ///     directly. Several soft obstacles are pre-seeded at ever-increasing offsets outside the true
    ///     source/target gap, simulating the "stair-stepping" candidate lane a stack of already-routed
    ///     parallel connectors produced before the fix — each successive connector's own soft-obstacle
    ///     avoidance offset its candidate lane a little further out, with no ceiling, until a lane landed
    ///     behind (outside the X-span of) the source box's own footprint. Before the fix, the unbounded
    ///     soft-obstacle-derived grid candidates let A* pick one of these behind-the-box lanes; after the
    ///     fix, the candidates are clamped to the source/target envelope, and any residual temptation to
    ///     leave it is priced by <c>EnvelopeDepartureCost</c>, so the route stays within the gap.
    /// </summary>
    [Fact]
    public void RouteWithStatus_SaturatedNarrowGapSoftObstacles_StaysWithinEnvelope()
    {
        // Arrange: a narrow 24px gap between two boxes (source right edge at X=142, target left edge at
        // X=166, mirroring the reported repro geometry), with three already-routed parallel connectors'
        // interior segments pre-seeded as soft obstacles: one occupying the gap's natural shared lane,
        // and two marching progressively further left — the second sitting right at the source box's
        // own edge, the third already behind (inside the X-span of) the source box.
        var source = new Point2D(142, 43.5);
        var target = new Point2D(166, 187.5);
        var softObstacles = new[]
        {
            new Rect(153, 20, 2, 200),
            new Rect(141, 20, 2, 200),
            new Rect(129, 20, 2, 200),
        };

        // Act
        var result = OrthogonalEdgeRouter.RouteWithStatus(
            source, target, obstacles: [], clearance: 12, softObstacles: softObstacles);

        // Assert: a valid route was found and every waypoint stays within the source/target X envelope
        // (a small allowance covers the router's own approach stub) — no waypoint lands at or behind
        // X=141/129, which sit outside the [142,166] gap the two endpoints actually define.
        Assert.False(result.Crossed);
        AssertAllSegmentsOrthogonal(result.Waypoints);
        const double allowance = 4.0;
        Assert.All(
            result.Waypoints,
            p => Assert.True(
                p.X >= 142 - allowance && p.X <= 166 + allowance,
                $"Waypoint ({p.X},{p.Y}) lies outside the source/target X envelope [142,166]."));
    }

    /// <summary>
    ///     Direct, low-level regression test for the connector-to-connector transversal-crossing fix
    ///     (see <c>OrthogonalEdgeRouter.SoftObstacleCrossingCount</c>/<c>SoftObstacleCrossingCost</c>),
    ///     isolated from <c>ConnectorRouter</c>'s anchor-distribution logic by calling
    ///     <see cref="OrthogonalEdgeRouter.RouteWithStatus"/> directly. A single vertical-oriented soft
    ///     obstacle (thin in X, tall in Y — the shape <c>ConnectorRouter.AddLineObstacles</c> builds for
    ///     an already-routed connector's vertical trunk segment) is seeded so that it crosses one of two
    ///     otherwise equally-cheap single-turn "L" routes between the source and target, leaving the
    ///     other entirely clear — a genuinely bounded-cost, no-extra-turn alternative. Before the fix,
    ///     <c>SoftObstacleOverlapLength</c> priced this perpendicular crossing identically to a trivial
    ///     few-pixel parallel overlap (bounded by the obstacle's own ~2px thin dimension, regardless of
    ///     the crossing move's own length), so the two L-routes tied on cost and the crossing one could
    ///     be (and was) picked, cutting straight across another connector's trunk; after the fix, the
    ///     new flat <c>SoftObstacleCrossingCost</c> breaks the tie in favor of the non-crossing route.
    /// </summary>
    [Fact]
    public void RouteWithStatus_VerticalOrientedSoftObstacleAcrossOneLRoute_PrefersNonCrossingAlternative()
    {
        // Arrange: source and target are diagonal from one another, so a bounded-cost, single-turn
        // "L" route exists in two equally-Manhattan-length flavors — turn early (go vertical first,
        // then horizontal) or turn late (go horizontal first, then vertical) — that differ only in
        // which one crosses the seeded obstacle. A single vertical-oriented soft obstacle (thin in X,
        // tall in Y — the shape <c>ConnectorRouter.AddLineObstacles</c> builds for an already-routed
        // connector's vertical trunk) sits at x = 100 spanning y in [-1, 19], squarely across the
        // "turn late" route's y = 0 leg, but entirely clear of the "turn early" route's y = 50 leg. Both
        // L-shaped routes cost the same but for the new crossing term, so the fix alone — with no
        // length- or turn-penalty advantage needed — decides which one A* prefers.
        var source = new Point2D(0, 0);
        var target = new Point2D(200, 50);
        var softObstacles = new[] { new Rect(100, -1, 2, 20) };

        // Act
        var result = OrthogonalEdgeRouter.RouteWithStatus(
            source, target, obstacles: [], clearance: 10, softObstacles: softObstacles);

        // Assert: a valid orthogonal route was found (soft obstacles never hard-block).
        Assert.False(result.Crossed);
        AssertAllSegmentsOrthogonal(result.Waypoints);

        // Assert: no segment of the route transversally crosses the vertical-oriented obstacle — i.e.
        // no horizontal segment spans fully from strictly left of x = [100,102] to strictly right of
        // it while sitting at a y within the obstacle's [-1,19] span. This is the concrete check the
        // fix exists to satisfy: the two L-shaped routes are otherwise equally cheap, so the crossing
        // route must be avoided purely because of the new crossing cost.
        for (var i = 0; i + 1 < result.Waypoints.Count; i++)
        {
            var p1 = result.Waypoints[i];
            var p2 = result.Waypoints[i + 1];
            if (Math.Abs(p1.Y - p2.Y) > 1e-9)
            {
                // Vertical segment: never transversally crosses another vertical-oriented obstacle.
                continue;
            }

            var y = p1.Y;
            var xLo = Math.Min(p1.X, p2.X);
            var xHi = Math.Max(p1.X, p2.X);
            var crosses = y > -1 && y < 19 && xLo < 100 && xHi > 102;
            Assert.False(
                crosses,
                $"Segment ({p1.X},{p1.Y})-({p2.X},{p2.Y}) transversally crosses the seeded vertical " +
                "soft obstacle instead of taking the equally-cheap non-crossing alternative.");
        }
    }

    /// <summary>
    ///     A null source anchor is rejected.
    /// </summary>
    [Fact]
    public void RouteWithStatus_NullSource_Throws()
    {
        // Act / Assert: a null source is rejected before routing
        Assert.Throws<ArgumentNullException>(
            () => OrthogonalEdgeRouter.RouteWithStatus(null!, new Point2D(100, 80), [], clearance: 10));
    }

    /// <summary>
    ///     A null target anchor is rejected.
    /// </summary>
    [Fact]
    public void RouteWithStatus_NullTarget_Throws()
    {
        // Act / Assert: a null target is rejected before routing
        Assert.Throws<ArgumentNullException>(
            () => OrthogonalEdgeRouter.RouteWithStatus(new Point2D(0, 0), null!, [], clearance: 10));
    }

    /// <summary>
    ///     A null obstacle list is rejected.
    /// </summary>
    [Fact]
    public void RouteWithStatus_NullObstacles_Throws()
    {
        // Act / Assert: a null obstacle list is rejected before routing
        Assert.Throws<ArgumentNullException>(
            () => OrthogonalEdgeRouter.RouteWithStatus(new Point2D(0, 0), new Point2D(100, 80), null!, clearance: 10));
    }

    /// <summary>
    ///     With a target leg hint the final bend is placed at least that far from the target face; without
    ///     it the legacy route bends right at the end of the target stub.
    /// </summary>
    [Fact]
    public void RouteWithStatus_TargetLeg_PlacesBendAtLeastLegFromFace()
    {
        // Arrange: an obstacle forces the route under it; the legacy final bend sits at the stub end (x = 380).
        var source = new Point2D(100, 100);
        var target = new Point2D(400, 20);
        var obstacles = new[] { new Rect(130, -100, 200, 250) };
        var hints = new RouteHints { TargetLeg = 40.0 };

        // Act
        var legacy = OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, 12, PortSide.Right, PortSide.Left);
        var hinted = OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, 12, PortSide.Right, PortSide.Left, hints: hints);

        // Assert
        Assert.False(hinted.Crossed);
        Assert.Equal(20.0, LastLeg(legacy.Waypoints), 6);
        Assert.True(LastLeg(hinted.Waypoints) >= 40.0 - 1e-6, $"Final leg {LastLeg(hinted.Waypoints)} is shorter than the hinted leg.");
        AssertAllSegmentsOrthogonal(hinted.Waypoints);
        AssertNoSegmentCrossesObstacle(hinted.Waypoints, obstacles);
    }

    /// <summary>
    ///     Zero-valued hints add no cost term and no grid line, so the route is exactly the legacy route.
    /// </summary>
    [Fact]
    public void RouteWithStatus_LegsZero_MatchesLegacyRoute()
    {
        // Arrange
        var source = new Point2D(100, 100);
        var target = new Point2D(400, 20);
        var obstacles = new[] { new Rect(130, -100, 200, 250) };

        // Act
        var legacy = OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, 12, PortSide.Right, PortSide.Left);
        var zero = OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, 12, PortSide.Right, PortSide.Left, hints: new RouteHints());

        // Assert
        Assert.Equal(legacy.Waypoints, zero.Waypoints);
        Assert.Equal(legacy.Cost, zero.Cost, 6);
    }

    /// <summary>
    ///     The result reports the search cost (path length for a straight run, more once the route must
    ///     detour) and the clearance level at which the route was found (smaller when the source sits
    ///     closer to an obstacle than the requested clearance).
    /// </summary>
    [Fact]
    public void RouteWithStatus_ReportsCostAndClearanceUsed()
    {
        // Arrange: a free run, a run that must bend around a block, and a source tucked 2 px from a box.
        var source = new Point2D(0, 0);
        var target = new Point2D(200, 0);
        var block = new[] { new Rect(80, -30, 40, 60) };
        var tucked = new[] { new Rect(-60, -20, 60, 40) };

        // Act
        var free = OrthogonalEdgeRouter.RouteWithStatus(source, target, [], 12);
        var detour = OrthogonalEdgeRouter.RouteWithStatus(source, target, block, 12);
        var close = OrthogonalEdgeRouter.RouteWithStatus(new Point2D(2, 0), target, tucked, 12);

        // Assert
        Assert.Equal(200.0, free.Cost, 6);
        Assert.Equal(12.0, free.ClearanceUsed, 6);
        Assert.True(detour.Cost > free.Cost);
        Assert.Equal(12.0, detour.ClearanceUsed, 6);
        Assert.False(close.Crossed);
        Assert.True(close.ClearanceUsed < 12.0, "Only a smaller clearance fits a source 2 px from the obstacle.");
    }

    /// <summary>
    ///     A port range lets the router pick the ports itself: with a free lane inside both faces' spans it
    ///     slides both ports onto the lane and returns one straight segment.
    /// </summary>
    [Fact]
    public void RoutePorts_LaneWithinBothRanges_SlidesPortsToStraightLane()
    {
        // Arrange: faces span 0..50 across; the block's bottom (20) + clearance (12) leaves the lane at 32.
        var obstacles = new[] { new Rect(200, -40, 100, 60) };
        var source = new PortRange { Side = PortSide.Right, Face = 100, Preferred = 25, Intervals = [(5, 45)] };
        var target = new PortRange { Side = PortSide.Left, Face = 400, Preferred = 25, Intervals = [(5, 45)], Extra = [32.0] };

        // Act
        var result = OrthogonalEdgeRouter.RoutePorts(source, target, obstacles, 12);

        // Assert
        Assert.False(result.Crossed);
        Assert.Equal(2, result.Waypoints.Count);
        Assert.Equal(32.0, result.Waypoints[0].Y, 6);
        Assert.Equal(32.0, result.Waypoints[1].Y, 6);
    }

    /// <summary>
    ///     A vertical wall hint blocks runs parallel to the wall inside twice the clearance, yet lets a
    ///     horizontal run cross it: a gap beside the wall that the plain route threads is refused, and the
    ///     route goes around the barrier instead, crossing the wall on a horizontal run with every vertical
    ///     run kept clear of the strip.
    /// </summary>
    [Fact]
    public void RouteWithStatus_VerticalWall_BlocksParallelRunInsideMarginButAllowsHorizontalCrossing()
    {
        // Arrange: two barriers leave a gap whose lanes (x = 112 / 118) lie within 24 px of the wall at
        // x = 100; going around the barrier's far end is the only way to keep clear of the wall.
        var source = new Point2D(0, 0);
        var target = new Point2D(230, 100);
        var obstacles = new[] { new Rect(-200, 40, 300, 20), new Rect(130, 40, 300, 20) };
        var hints = new RouteHints { Walls = [new RouterWall(IsVertical: true, 100.0, -300.0, 400.0)] };

        // Act
        var plain = OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, 12);
        var walled = OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, 12, hints: hints);

        // Assert: plain threads the gap beside the wall; the walled route keeps every vertical run 24 px
        // from the wall yet still crosses it horizontally.
        Assert.Contains(VerticalRunXs(plain.Waypoints), x => Math.Abs(x - 100.0) < 24.0);
        Assert.False(walled.Crossed);
        Assert.Equal(12.0, walled.ClearanceUsed, 6);
        Assert.All(VerticalRunXs(walled.Waypoints), x => Assert.True(Math.Abs(x - 100.0) >= 24.0 - 1e-6, $"Run at x={x} hugs the wall."));
        Assert.Contains(walled.Waypoints.Zip(walled.Waypoints.Skip(1)), s =>
            Math.Abs(s.First.Y - s.Second.Y) < 1e-6 && Math.Min(s.First.X, s.Second.X) < 100.0 && Math.Max(s.First.X, s.Second.X) > 100.0);
    }

    /// <summary>
    ///     With a crossing penalty the router prefers a detour that crosses no earlier connector over a
    ///     straight run that crosses two of them.
    /// </summary>
    [Fact]
    public void RouteWithStatus_CrossingPenalty_PrefersRouteWithFewerCrossings()
    {
        // Arrange: two vertical soft lines (earlier connectors) between source and target.
        var source = new Point2D(0, 0);
        var target = new Point2D(200, 0);
        var soft = new[] { new Rect(60, -30, 2, 60), new Rect(120, -30, 2, 60) };

        // Act
        var cheap = OrthogonalEdgeRouter.RouteWithStatus(source, target, [], 12, softObstacles: soft);
        var avoiding = OrthogonalEdgeRouter.RouteWithStatus(
            source, target, [], 12, softObstacles: soft, hints: new RouteHints { CrossingPenalty = 100.0 });

        // Assert
        Assert.Equal(2, cheap.Waypoints.Count);
        Assert.True(avoiding.Waypoints.Count > 2, "The penalized route detours around the lines.");
        Assert.DoesNotContain(
            avoiding.Waypoints.Zip(avoiding.Waypoints.Skip(1)),
            s => Math.Abs(s.First.Y - s.Second.Y) < 1e-6 && Math.Abs(s.First.Y) < 30.0
                 && Math.Min(s.First.X, s.Second.X) < 61.0 && Math.Max(s.First.X, s.Second.X) > 61.0);
    }

    /// <summary>
    ///     A comfort leg keeps the first bend at least that far from the source face; without the hint the
    ///     legacy route bends at the stub end (here the far lane is made costly by an earlier connector).
    /// </summary>
    [Fact]
    public void RouteWithStatus_ComfortLegZone_PlacesFirstBendAtLeastComfortFromFace()
    {
        // Arrange: a plain jog between two facing faces on a wide gap; a soft line sits on the far lane
        // (x = 380) so the legacy route bends right after the source stub (x = 120).
        var source = new Point2D(100, 100);
        var target = new Point2D(400, 20);
        var soft = new[] { new Rect(379, 0, 2, 120) };
        var hints = new RouteHints { SourceLeg = 24.0, SourceComfort = 36.0 };

        // Act
        var legacy = OrthogonalEdgeRouter.RouteWithStatus(source, target, [], 12, PortSide.Right, PortSide.Left, softObstacles: soft);
        var comfortable = OrthogonalEdgeRouter.RouteWithStatus(
            source, target, [], 12, PortSide.Right, PortSide.Left, softObstacles: soft, hints: hints);

        // Assert
        Assert.Equal(20.0, FirstLeg(legacy.Waypoints), 6);
        Assert.True(FirstLeg(comfortable.Waypoints) >= 36.0 - 1e-6, $"First leg {FirstLeg(comfortable.Waypoints)} is shorter than the comfort leg.");
    }

    /// <summary>
    ///     A minimum interior segment hint turns a tiny sideways jog into one of at least that length: two
    ///     facing faces whose usable spans do not overlap are joined by a Z whose middle (interior) run is
    ///     at least the minimum, by moving the port on the wider face away from the narrow one; without
    ///     the hint the router takes the shortest jog the ranges allow.
    /// </summary>
    [Fact]
    public void RoutePorts_MinInteriorSegment_FacesOverlapNarrowly_ReturnsZWithJogAtLeastMinimum()
    {
        // Arrange: the source's preferred port (x = 171) and the target's (x = 162.8) are 8.2 px apart, and the
        // faces' other usable spans (190..271 and 60..164) do not overlap, so the legacy route jogs by 8.2.
        var source = new PortRange { Side = PortSide.Bottom, Face = 70, Preferred = 171, Intervals = [(171, 171), (190, 271)], Extra = [190.0] };
        var target = new PortRange { Side = PortSide.Top, Face = 220, Preferred = 162.8, Intervals = [(60, 164)], Extra = [164.0] };

        // Act
        var legacy = OrthogonalEdgeRouter.RoutePorts(source, target, [], 12);
        var hinted = OrthogonalEdgeRouter.RoutePorts(source, target, [], 12, hints: new RouteHints { MinInteriorSegment = 24.0 });

        // Assert: the legacy jog is tiny, the hinted jog is at least 24 and the route is still clean.
        Assert.True(InteriorSegments(legacy.Waypoints).Min() < 24.0, $"Without the hint the shortest jog is tiny: {string.Join(" ", legacy.Waypoints)}.");
        Assert.False(hinted.Crossed);
        Assert.Equal(2, hinted.Waypoints.Count - 2);
        Assert.All(InteriorSegments(hinted.Waypoints), length => Assert.True(length >= 24.0 - 1e-6, $"Interior segment {length} is shorter than the minimum."));
        AssertAllSegmentsOrthogonal(hinted.Waypoints);
    }

    /// <summary>
    ///     The minimum interior segment is never met by doubling back: a run may not reverse along its own
    ///     axis (go left 24 px and return right) to reach the required length, for any offset between the
    ///     two faces.
    /// </summary>
    /// <param name="targetHi">The upper end of the narrow target face's usable span.</param>
    [Theory]
    [InlineData(150.0)]
    [InlineData(160.0)]
    [InlineData(164.0)]
    [InlineData(176.0)]
    public void RoutePorts_MinInteriorSegment_NoReversalToSatisfyMinimum(double targetHi)
    {
        // Arrange
        var source = new PortRange { Side = PortSide.Bottom, Face = 70, Preferred = 171, Intervals = [(171, 171), (190, 271)], Extra = [190.0] };
        var target = new PortRange { Side = PortSide.Top, Face = 220, Preferred = targetHi - 1.2, Intervals = [(targetHi - 104.0, targetHi)], Extra = [targetHi] };

        // Act
        var result = OrthogonalEdgeRouter.RoutePorts(source, target, [], 12, hints: new RouteHints { MinInteriorSegment = 24.0 });

        // Assert: no two consecutive segments run along the same axis in opposite directions, and every
        // interior segment meets the minimum.
        var w = result.Waypoints;
        for (var i = 0; i < w.Count - 2; i++)
        {
            var dot = ((w[i + 1].X - w[i].X) * (w[i + 2].X - w[i + 1].X)) + ((w[i + 1].Y - w[i].Y) * (w[i + 2].Y - w[i + 1].Y));
            Assert.False(dot < 0.0, $"The route doubles back at waypoint {i + 1}.");
        }

        Assert.All(InteriorSegments(w), length => Assert.True(length >= 24.0 - 1e-6, $"Interior segment {length} is shorter than the minimum: {string.Join(" ", w)} crossed={result.Crossed}."));
    }

    /// <summary>
    ///     When no route satisfies the minimum interior segment (here an unreachable length, so no grid
    ///     line allows it), the constraint is dropped and the unconstrained route is returned, never a
    ///     crossing fallback: the hint can only improve a route, not lose it.
    /// </summary>
    [Fact]
    public void RouteWithStatus_MinInteriorSegmentInfeasible_FallsBackToUnconstrainedRoute()
    {
        // Arrange: two facing faces offset by 10 px; the only route is a Z with a 10 px middle run.
        var source = new Point2D(100, 100);
        var target = new Point2D(110, 300);

        // Act
        var plain = OrthogonalEdgeRouter.RouteWithStatus(source, target, [], 12, PortSide.Bottom, PortSide.Top);
        var impossible = OrthogonalEdgeRouter.RouteWithStatus(
            source, target, [], 12, PortSide.Bottom, PortSide.Top, hints: new RouteHints { MinInteriorSegment = 1000.0 });

        // Assert
        Assert.False(impossible.Crossed);
        Assert.Equal(plain.Waypoints, impossible.Waypoints);
        AssertAllSegmentsOrthogonal(impossible.Waypoints);
    }

    /// <summary>Returns the length of every interior segment (between two bends) of the path.</summary>
    private static IEnumerable<double> InteriorSegments(IReadOnlyList<Point2D> path)
    {
        for (var i = 1; i <= path.Count - 3; i++)
        {
            yield return Math.Abs(path[i + 1].X - path[i].X) + Math.Abs(path[i + 1].Y - path[i].Y);
        }
    }

    /// <summary>Returns the length of the first segment of the path.</summary>
    private static double FirstLeg(IReadOnlyList<Point2D> path) =>
        Math.Abs(path[1].X - path[0].X) + Math.Abs(path[1].Y - path[0].Y);

    /// <summary>Returns the length of the last segment of the path.</summary>
    private static double LastLeg(IReadOnlyList<Point2D> path) =>
        Math.Abs(path[^1].X - path[^2].X) + Math.Abs(path[^1].Y - path[^2].Y);

    /// <summary>Returns the X coordinate of every vertical segment of the path.</summary>
    private static IEnumerable<double> VerticalRunXs(IReadOnlyList<Point2D> path) =>
        path.Zip(path.Skip(1)).Where(s => Math.Abs(s.First.X - s.Second.X) < 1e-6).Select(s => s.First.X);

    /// <summary>
    ///     Asserts that the path begins at the expected source and ends at the expected target.
    /// </summary>
    private static void AssertEndpoints(IReadOnlyList<Point2D> path, Point2D source, Point2D target)
    {
        Assert.True(path.Count >= 2);
        Assert.Equal(source.X, path[0].X, 6);
        Assert.Equal(source.Y, path[0].Y, 6);
        Assert.Equal(target.X, path[^1].X, 6);
        Assert.Equal(target.Y, path[^1].Y, 6);
    }

    /// <summary>
    ///     Asserts that every consecutive pair of waypoints forms a horizontal or vertical segment.
    /// </summary>
    private static void AssertAllSegmentsOrthogonal(IReadOnlyList<Point2D> path)
    {
        for (var i = 0; i < path.Count - 1; i++)
        {
            var a = path[i];
            var b = path[i + 1];
            var horizontal = Math.Abs(a.Y - b.Y) < 1e-6;
            var vertical = Math.Abs(a.X - b.X) < 1e-6;
            Assert.True(horizontal || vertical,
                $"Segment {i} from ({a.X},{a.Y}) to ({b.X},{b.Y}) is not orthogonal.");
        }
    }

    /// <summary>
    ///     Asserts that no segment of the path passes through the interior of any obstacle.
    /// </summary>
    private static void AssertNoSegmentCrossesObstacle(IReadOnlyList<Point2D> path, IReadOnlyList<Rect> obstacles)
    {
        for (var i = 0; i < path.Count - 1; i++)
        {
            var a = path[i];
            var b = path[i + 1];
            foreach (var r in obstacles)
            {
                Assert.False(SegmentCrossesRect(a, b, r),
                    $"Segment {i} from ({a.X},{a.Y}) to ({b.X},{b.Y}) crosses obstacle.");
            }
        }
    }

    /// <summary>
    ///     Asserts that the path never visits one point, leaves it, and later returns to the exact same
    ///     point.
    /// </summary>
    private static void AssertNoWaypointRevisit(IReadOnlyList<Point2D> path)
    {
        for (var i = 0; i < path.Count - 2; i++)
        {
            for (var j = i + 2; j < path.Count; j++)
            {
                Assert.False(
                    SamePoint(path[i], path[j]),
                    $"Waypoint ({path[i].X},{path[i].Y}) is revisited at positions {i} and {j}.");
            }
        }
    }

    /// <summary>
    ///     Returns true when the axis-aligned segment passes through the strict interior of the rect.
    /// </summary>
    private static bool SegmentCrossesRect(Point2D a, Point2D b, Rect r)
    {
        if (Math.Abs(a.Y - b.Y) < 1e-6)
        {
            // Horizontal segment
            var y = a.Y;
            var xa = Math.Min(a.X, b.X);
            var xb = Math.Max(a.X, b.X);
            return r.Y < y && y < r.Y + r.Height &&
                   Math.Max(xa, r.X) < Math.Min(xb, r.X + r.Width);
        }

        // Vertical segment
        var x = a.X;
        var ya = Math.Min(a.Y, b.Y);
        var yb = Math.Max(a.Y, b.Y);
        return r.X < x && x < r.X + r.Width &&
               Math.Max(ya, r.Y) < Math.Min(yb, r.Y + r.Height);
    }

    /// <summary>
    ///     Returns the Euclidean distance from an axis-aligned segment to an axis-aligned rectangle
    ///     (0 when they intersect).
    /// </summary>
    private static double SegmentDistanceToRect(Point2D a, Point2D b, Rect r)
    {
        var xlo = Math.Min(a.X, b.X);
        var xhi = Math.Max(a.X, b.X);
        var ylo = Math.Min(a.Y, b.Y);
        var yhi = Math.Max(a.Y, b.Y);

        var dx = Math.Max(0.0, Math.Max(r.X - xhi, xlo - (r.X + r.Width)));
        var dy = Math.Max(0.0, Math.Max(r.Y - yhi, ylo - (r.Y + r.Height)));
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    ///     Returns whether two waypoints identify the same geometric point.
    /// </summary>
    private static bool SamePoint(Point2D left, Point2D right) =>
        Math.Abs(left.X - right.X) < 1e-9 && Math.Abs(left.Y - right.Y) < 1e-9;

    /// <summary>
    ///     Returns the longest overlap, in pixels, between any segment of <paramref name="path"/> and
    ///     the interior of <paramref name="rect"/> along the segment's own axis (horizontal segments
    ///     are compared against the rect's X range when the segment's Y falls inside the rect's Y range,
    ///     and vice versa for vertical segments).
    /// </summary>
    private static double LongestOverlapWithRect(IReadOnlyList<Point2D> path, Rect rect)
    {
        var longest = 0.0;
        for (var i = 0; i < path.Count - 1; i++)
        {
            var a = path[i];
            var b = path[i + 1];

            if (Math.Abs(a.Y - b.Y) < 1e-6)
            {
                // Horizontal segment.
                if (a.Y <= rect.Y || a.Y >= rect.Y + rect.Height)
                {
                    continue;
                }

                var xa = Math.Min(a.X, b.X);
                var xb = Math.Max(a.X, b.X);
                var overlap = Math.Min(xb, rect.X + rect.Width) - Math.Max(xa, rect.X);
                longest = Math.Max(longest, Math.Max(0.0, overlap));
            }
            else
            {
                // Vertical segment.
                if (a.X <= rect.X || a.X >= rect.X + rect.Width)
                {
                    continue;
                }

                var ya = Math.Min(a.Y, b.Y);
                var yb = Math.Max(a.Y, b.Y);
                var overlap = Math.Min(yb, rect.Y + rect.Height) - Math.Max(ya, rect.Y);
                longest = Math.Max(longest, Math.Max(0.0, overlap));
            }
        }

        return longest;
    }
}
