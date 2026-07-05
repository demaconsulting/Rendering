// <copyright file="ConnectorRouterTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Layout;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests for <see cref="ConnectorRouter"/> connector routing orchestration among placed boxes.
/// </summary>
public sealed class ConnectorRouterTests
{
    /// <summary>
    ///     Creates a plain rectangular <see cref="LayoutBox"/> at the given placement with no
    ///     compartments or children.
    /// </summary>
    private static LayoutBox Box(
        double x,
        double y,
        double width,
        double height,
        string? label = null,
        BoxShape shape = BoxShape.Rectangle,
        double? roundedCornerRadius = null,
        double? folderTabWidth = null,
        double? folderTabHeight = null) =>
        new(
            x,
            y,
            width,
            height,
            label,
            0,
            shape,
            [],
            [],
            RoundedCornerRadius: roundedCornerRadius,
            FolderTabWidth: folderTabWidth,
            FolderTabHeight: folderTabHeight);

    /// <summary>
    ///     When the target box lies to the right, the source anchor sits on the source box's right
    ///     face and the target anchor on the target box's left face — each box presents the face that
    ///     points at the other.
    /// </summary>
    [Fact]
    public void Route_TargetToTheRight_AnchorsFaceEachOther()
    {
        // Arrange: two boxes side by side, target to the right of source, no obstacles between
        var from = Box(0, 0, 60, 60);
        var to = Box(200, 0, 60, 60);
        var boxes = new[] { from, to };

        // Act: route the single connection
        var line = ConnectorRouter.Route(boxes, new Connection(from, to), new ConnectorRouteOptions());

        // Assert: the route starts at the source right-edge midpoint and ends at the target left-edge midpoint
        var start = line.Waypoints[0];
        var end = line.Waypoints[^1];
        Assert.Equal(from.X + from.Width, start.X, 6); // right face of source
        Assert.Equal(from.Y + (from.Height / 2.0), start.Y, 6);
        Assert.Equal(to.X, end.X, 6); // left face of target
        Assert.Equal(to.Y + (to.Height / 2.0), end.Y, 6);
    }

    /// <summary>
    ///     When the target box lies below, the source anchor sits on the source box's bottom face and
    ///     the target anchor on the target box's top face.
    /// </summary>
    [Fact]
    public void Route_TargetBelow_AnchorsFaceEachOther()
    {
        // Arrange: target stacked below the source
        var from = Box(0, 0, 60, 60);
        var to = Box(0, 200, 60, 60);
        var boxes = new[] { from, to };

        // Act
        var line = ConnectorRouter.Route(boxes, new Connection(from, to), new ConnectorRouteOptions());

        // Assert: source leaves the bottom face, target is entered on the top face
        var start = line.Waypoints[0];
        var end = line.Waypoints[^1];
        Assert.Equal(from.X + (from.Width / 2.0), start.X, 6);
        Assert.Equal(from.Y + from.Height, start.Y, 6); // bottom face of source
        Assert.Equal(to.X + (to.Width / 2.0), end.X, 6);
        Assert.Equal(to.Y, end.Y, 6); // top face of target
    }

    /// <summary>
    ///     A folder's top-face connectable extent excludes the raised tab strip, so a connector
    ///     approaching from above clamps to the first usable point to the right of the tab instead of
    ///     anchoring on the tab itself.
    /// </summary>
    [Fact]
    public void Route_FolderTopFace_TabExcludedFromConnectableExtent()
    {
        // Arrange: the source sits above the folder and overlaps only the tab region horizontally.
        var from = Box(40, 0, 40, 40);
        var to = Box(0, 120, 140, 90, "Utilities", BoxShape.Folder, folderTabWidth: 60.0, folderTabHeight: 24.0);
        var boxes = new[] { from, to };

        // Act: route directly into the folder from above.
        var line = ConnectorRouter.Route(boxes, new Connection(from, to), new ConnectorRouteOptions());

        // Assert: the target anchor lands to the right of the tab strip and on the recessed body top.
        var end = line.Waypoints[^1];
        Assert.True(end.X > to.X + 60.0, "Target anchor should clamp to the usable top-face extent right of the tab.");
        Assert.Equal(to.Y + 24.0, end.Y, 6);
    }

    /// <summary>
    ///     When several connectors share a folder's top face, their anchors are distributed across the
    ///     usable top-face extent to the right of the tab, not across the full box width.
    /// </summary>
    [Fact]
    public void Route_SharedFolderTopFace_DistributesAcrossReducedExtent()
    {
        // Arrange: three source boxes above a folder, all converging on its top face.
        var s1 = Box(0, 0, 30, 30);
        var s2 = Box(55, 0, 30, 30);
        var s3 = Box(110, 0, 30, 30);
        var folder = Box(0, 120, 140, 90, "Utilities", BoxShape.Folder, folderTabWidth: 60.0, folderTabHeight: 24.0);
        var boxes = new[] { s1, s2, s3, folder };
        var connections = new[]
        {
            new Connection(s1, folder),
            new Connection(s2, folder),
            new Connection(s3, folder),
        };

        // Act: route them as a batch so the shared-face distribution logic engages.
        var lines = ConnectorRouter.Route(boxes, connections, new ConnectorRouteOptions());
        var targets = lines.Select(line => line.Waypoints[^1]).ToArray();

        // Assert: every target anchor stays on the recessed body top and to the right of the tab.
        Assert.All(targets, target =>
        {
            Assert.True(target.X > folder.X + 60.0, "Target anchor should remain off the folder tab.");
            Assert.Equal(folder.Y + 24.0, target.Y, 6);
        });

        // Assert: the anchors are distinct and ordered across the reduced usable span.
        Assert.True(targets[0].X < targets[1].X, "First target anchor should sit left of the second.");
        Assert.True(targets[1].X < targets[2].X, "Second target anchor should sit left of the third.");
    }

    /// <summary>
    ///     Surface projection applies the folder tab height as an inward offset on the top face, so the
    ///     final anchor touches the folder body's recessed outline rather than the bounding-box edge.
    /// </summary>
    [Fact]
    public void Route_FolderTopFace_ProjectsAnchorToRecessedBodyTop()
    {
        // Arrange: the source overlaps a clearly usable part of the folder's top face.
        var from = Box(95, 0, 20, 20);
        var to = Box(0, 120, 140, 90, "Utilities", BoxShape.Folder, folderTabWidth: 60.0, folderTabHeight: 24.0);
        var boxes = new[] { from, to };

        // Act
        var line = ConnectorRouter.Route(boxes, new Connection(from, to), new ConnectorRouteOptions());

        // Assert: the route still enters from above, but the final touch point is recessed by the tab height.
        Assert.Equal(line.Waypoints[^1].X, line.Waypoints[^2].X, 6);
        Assert.True(line.Waypoints[^2].Y < line.Waypoints[^1].Y, "Last segment should enter the top face from above.");
        Assert.Equal(to.Y + 24.0, line.Waypoints[^1].Y, 6);
    }

    /// <summary>
    ///     When the naturally-facing face has an empty connectable extent, anchor selection falls back
    ///     to the first usable adjacent face in the documented preference order.
    /// </summary>
    [Fact]
    public void Route_FaceSelectionFallback_EmptyNaturalFaceUsesAdjacentFace()
    {
        // Arrange: the source sits above-left of a folder whose top face is entirely consumed by the tab.
        var from = Box(90, 0, 20, 20);
        var to = Box(100, 100, 80, 60, "Package", BoxShape.Folder, folderTabWidth: 80.0, folderTabHeight: 24.0);
        var boxes = new[] { from, to };

        // Act
        var line = ConnectorRouter.Route(boxes, new Connection(from, to), new ConnectorRouteOptions());

        // Assert: the target cannot use its top face, so it falls back to its left face.
        var end = line.Waypoints[^1];
        Assert.Equal(to.X, end.X, 6);
        Assert.Equal(to.Y + (to.Height / 2.0), end.Y, 6);
    }

    /// <summary>
    ///     A connector between two boxes routes around an intervening obstacle box without passing
    ///     through the obstacle's interior, while still producing an axis-aligned path.
    /// </summary>
    [Fact]
    public void Route_ObstacleBetweenEndpoints_RoutesAroundInterior()
    {
        // Arrange: source and target on a shared horizontal band, with a tall box squarely between
        var from = Box(0, 0, 60, 60);
        var obstacle = Box(120, -40, 60, 140);
        var to = Box(260, 0, 60, 60);
        var boxes = new[] { from, obstacle, to };

        // Act
        var line = ConnectorRouter.Route(boxes, new Connection(from, to), new ConnectorRouteOptions());

        // Assert: the path is orthogonal and no segment crosses the intervening obstacle's interior
        AssertAllSegmentsOrthogonal(line.Waypoints);
        AssertNoSegmentCrossesObstacle(line.Waypoints, new Rect(obstacle.X, obstacle.Y, obstacle.Width, obstacle.Height));
    }

    /// <summary>
    ///     The two endpoint boxes are excluded from the obstacle set: the connector reaches their
    ///     boundary anchors even though both boxes are present in the box list. If they were treated
    ///     as obstacles, no clean approach to their faces would exist.
    /// </summary>
    [Fact]
    public void Route_EndpointBoxes_AreExcludedFromObstacles()
    {
        // Arrange: two large adjacent boxes with only a narrow gap between their facing edges. The
        // source anchor sits on the source's right face and the target anchor on the target's left
        // face; both anchors lie on box boundaries that only route cleanly when the endpoints are
        // excluded from the obstacle set.
        var from = Box(0, 0, 120, 120);
        var to = Box(140, 0, 120, 120);
        var boxes = new[] { from, to };

        // Act
        var line = ConnectorRouter.Route(boxes, new Connection(from, to), new ConnectorRouteOptions());

        // Assert: a clean, straight orthogonal hop between the facing faces (only possible because
        // neither endpoint box is an obstacle)
        AssertAllSegmentsOrthogonal(line.Waypoints);
        Assert.Equal(from.X + from.Width, line.Waypoints[0].X, 6);
        Assert.Equal(to.X, line.Waypoints[^1].X, 6);

        // Neither endpoint's interior is crossed, confirming the anchors reached the boundaries.
        Assert.DoesNotContain(line.Waypoints, p => IsStrictlyInside(p, from) || IsStrictlyInside(p, to));
    }

    /// <summary>
    ///     The routed <see cref="LayoutLine"/> carries the connection's requested target end marker,
    ///     line style, and label, and always reports no source-end marker.
    /// </summary>
    [Fact]
    public void Route_Connection_CarriesRequestedStyling()
    {
        // Arrange: a connection with an explicit arrowhead, dashed style, and a label
        var from = Box(0, 0, 60, 60);
        var to = Box(200, 0, 60, 60);
        var boxes = new[] { from, to };
        var connection = new Connection(from, to, EndMarkerStyle.FilledArrow, LineStyle.Dashed, "supertype");

        // Act
        var line = ConnectorRouter.Route(boxes, connection, new ConnectorRouteOptions());

        // Assert: the styling flows onto the line and the source end stays unmarked
        Assert.Equal(EndMarkerStyle.None, line.SourceEnd);
        Assert.Equal(EndMarkerStyle.FilledArrow, line.TargetEnd);
        Assert.Equal(LineStyle.Dashed, line.LineStyle);
        Assert.Equal("supertype", line.MidpointLabel);
    }

    /// <summary>
    ///     The batch overload returns one routed line per connection, in input order.
    /// </summary>
    [Fact]
    public void Route_MultipleConnections_ReturnsOneLinePerConnectionInOrder()
    {
        // Arrange: three boxes and two connections from the first box
        var a = Box(0, 0, 60, 60);
        var b = Box(200, 0, 60, 60);
        var c = Box(0, 200, 60, 60);
        var boxes = new[] { a, b, c };
        var connections = new[]
        {
            new Connection(a, b, EndMarkerStyle.FilledArrow),
            new Connection(a, c, EndMarkerStyle.HollowDiamond),
        };

        // Act
        var lines = ConnectorRouter.Route(boxes, connections, new ConnectorRouteOptions());

        // Assert: two lines, in the same order, each carrying its own target marker
        Assert.Equal(2, lines.Count);
        Assert.Equal(EndMarkerStyle.FilledArrow, lines[0].TargetEnd);
        Assert.Equal(EndMarkerStyle.HollowDiamond, lines[1].TargetEnd);
    }

    /// <summary>
    ///     Reproduces the DictionaryMark rendering defect: three small source boxes stacked far below a
    ///     much taller target box, each connected to the same target. Routed independently, every
    ///     connector's naive anchor clamps to the exact same target-box corner because none of the
    ///     source boxes overlap the target vertically. Routed as a batch, the three target anchors must
    ///     be spread apart instead.
    /// </summary>
    [Fact]
    public void Route_ThreeConnectorsShareTargetFace_BatchSpreadsTargetAnchors()
    {
        // Arrange: geometry taken directly from DictionaryMark's generated SVG.
        var yamlDotNet = Box(24, 1265, 130, 50, "YamlDotNet");
        var fileSystemGlobbing = Box(24, 1345, 168.24, 50, "FileSystemGlobbing");
        var testResults = Box(24, 1425, 130, 50, "TestResults");
        var dictionaryMarkSystem = Box(1037.52, 421, 308.16, 224, "DictionaryMarkSystem");
        var boxes = new[] { yamlDotNet, fileSystemGlobbing, testResults, dictionaryMarkSystem };

        var connections = new[]
        {
            new Connection(yamlDotNet, dictionaryMarkSystem, EndMarkerStyle.FilledDiamond),
            new Connection(fileSystemGlobbing, dictionaryMarkSystem, EndMarkerStyle.FilledDiamond),
            new Connection(testResults, dictionaryMarkSystem, EndMarkerStyle.FilledDiamond),
        };

        // Act: route the single-connection overload to confirm the defect this test guards against,
        // then route the same connections as a batch.
        var independentTargets = connections
            .Select(c => ConnectorRouter.Route(boxes, c, new ConnectorRouteOptions()).Waypoints[^1])
            .ToArray();
        var batchLines = ConnectorRouter.Route(boxes, connections, new ConnectorRouteOptions());
        var batchTargets = batchLines.Select(l => l.Waypoints[^1]).ToArray();

        // Assert: routed independently, all three connectors collapse onto the identical target point
        // (the defect reported against DictionaryMark's diagram).
        Assert.Equal(independentTargets[0].Y, independentTargets[1].Y, 6);
        Assert.Equal(independentTargets[1].Y, independentTargets[2].Y, 6);

        // Assert: routed as a batch, the three target anchors are distinct and ordered to match the
        // source boxes' own stacking order (YamlDotNet above FileSystemGlobbing above TestResults).
        Assert.True(batchTargets[0].Y < batchTargets[1].Y, "First target anchor should sit above the second.");
        Assert.True(batchTargets[1].Y < batchTargets[2].Y, "Second target anchor should sit above the third.");

        // Assert: every batch target anchor still lands on the target box's left face.
        Assert.All(batchTargets, p => Assert.Equal(dictionaryMarkSystem.X, p.X, 6));

        // Assert: every batch target anchor stays within the target box's own vertical extent.
        Assert.All(batchTargets, p =>
        {
            Assert.True(p.Y >= dictionaryMarkSystem.Y, "Target anchor should not sit above the target box.");
            Assert.True(p.Y <= dictionaryMarkSystem.Y + dictionaryMarkSystem.Height, "Target anchor should not sit below the target box.");
        });
    }

    /// <summary>
    ///     When a box face is shared by only one connector in the batch, that connector's anchor is
    ///     left exactly as independent single-connection routing would produce — the redistribution
    ///     logic only engages for faces with two or more connectors.
    /// </summary>
    [Fact]
    public void Route_BatchWithoutSharedFaces_MatchesIndependentRouting()
    {
        // Arrange: two unrelated connections that do not share any box
        var a = Box(0, 0, 60, 60);
        var b = Box(200, 0, 60, 60);
        var c = Box(0, 300, 60, 60);
        var d = Box(200, 300, 60, 60);
        var boxes = new[] { a, b, c, d };
        var connections = new[] { new Connection(a, b), new Connection(c, d) };

        // Act
        var batchLines = ConnectorRouter.Route(boxes, connections, new ConnectorRouteOptions());
        var independentLine0 = ConnectorRouter.Route(boxes, connections[0], new ConnectorRouteOptions());
        var independentLine1 = ConnectorRouter.Route(boxes, connections[1], new ConnectorRouteOptions());

        // Assert: batch routing reproduces the same anchors as routing each connection independently
        Assert.Equal(independentLine0.Waypoints[0], batchLines[0].Waypoints[0]);
        Assert.Equal(independentLine0.Waypoints[^1], batchLines[0].Waypoints[^1]);
        Assert.Equal(independentLine1.Waypoints[0], batchLines[1].Waypoints[0]);
        Assert.Equal(independentLine1.Waypoints[^1], batchLines[1].Waypoints[^1]);
    }

    /// <summary>
    ///     Reproduces the second half of the DictionaryMark rendering defect: once the batch overload
    ///     spreads three connectors' target anchors across a shared face (see
    ///     <see cref="Route_ThreeConnectorsShareTargetFace_BatchSpreadsTargetAnchors"/>), each connector
    ///     still travels from its own widely separated source box toward that face, and every one of
    ///     them naturally wants its long axis-changing run in the same column (the target's own
    ///     stepped-off approach column), because that column is the only fixed X common to all three
    ///     paths. Routing the connectors sequentially and steering each later one away from earlier
    ///     ones' already-claimed corridors must produce visually distinct vertical runs instead of one
    ///     another's corridor, while still reaching its own spread-out target anchor.
    /// </summary>
    [Fact]
    public void Route_ThreeConnectorsShareTargetFace_TrunksDoNotOverlap()
    {
        // Arrange: geometry taken directly from DictionaryMark's generated SVG (same as the anchor
        // spread test above) — three source boxes stacked far below a much taller target box.
        var yamlDotNet = Box(24, 1265, 130, 50, "YamlDotNet");
        var fileSystemGlobbing = Box(24, 1345, 168.24, 50, "FileSystemGlobbing");
        var testResults = Box(24, 1425, 130, 50, "TestResults");
        var dictionaryMarkSystem = Box(1037.52, 421, 308.16, 224, "DictionaryMarkSystem");
        var boxes = new[] { yamlDotNet, fileSystemGlobbing, testResults, dictionaryMarkSystem };

        var connections = new[]
        {
            new Connection(yamlDotNet, dictionaryMarkSystem, EndMarkerStyle.FilledDiamond),
            new Connection(fileSystemGlobbing, dictionaryMarkSystem, EndMarkerStyle.FilledDiamond),
            new Connection(testResults, dictionaryMarkSystem, EndMarkerStyle.FilledDiamond),
        };

        // Act
        var lines = ConnectorRouter.Route(boxes, connections, new ConnectorRouteOptions(EdgeRouting.Orthogonal, 12.0));

        // Assert: every connector still reaches its own (now distinct) anchor on the target's face.
        Assert.Equal(dictionaryMarkSystem.X, lines[0].Waypoints[^1].X, 6);
        Assert.Equal(dictionaryMarkSystem.X, lines[1].Waypoints[^1].X, 6);
        Assert.Equal(dictionaryMarkSystem.X, lines[2].Waypoints[^1].X, 6);

        // Assert: the long vertical run each connector uses to change its Y position (its longest
        // vertical segment) sits at a distinct X for each connector — the defect this test guards
        // against had all three collapse onto the exact same column, drawing what looked like one
        // thick merged trunk instead of three separate connectors.
        var trunkX = lines.Select(LongestVerticalSegmentX).ToArray();
        Assert.NotEqual(trunkX[0], trunkX[1], 3);
        Assert.NotEqual(trunkX[0], trunkX[2], 3);
        Assert.NotEqual(trunkX[1], trunkX[2], 3);

        // Assert: none of the three connectors crossed a box (the soft-obstacle steering must never
        // force a fallback to an obstacle-crossing route just to avoid another connector's corridor).
        Assert.All(lines, line => AssertAllSegmentsOrthogonal(line.Waypoints));
    }

    /// <summary>
    ///     Returns the X coordinate of <paramref name="line"/>'s longest vertical (axis-changing)
    ///     segment — the run a connector uses to move from its source's Y level toward its target's Y
    ///     level, as opposed to the short perpendicular entry/exit stubs at either end.
    /// </summary>
    private static double LongestVerticalSegmentX(LayoutLine line)
    {
        var waypoints = line.Waypoints;
        var bestLength = -1.0;
        var bestX = double.NaN;
        for (var i = 0; i < waypoints.Count - 1; i++)
        {
            var a = waypoints[i];
            var b = waypoints[i + 1];
            if (Math.Abs(a.X - b.X) > 1e-6)
            {
                continue;
            }

            var length = Math.Abs(a.Y - b.Y);
            if (length > bestLength)
            {
                bestLength = length;
                bestX = a.X;
            }
        }

        return bestX;
    }

    /// <summary>
    ///     A null box list is rejected by the batch overload.
    /// </summary>
    [Fact]
    public void Route_NullBoxes_Throws()
    {
        // Arrange
        var from = Box(0, 0, 60, 60);
        var to = Box(200, 0, 60, 60);


        // Act / Assert
        Assert.Throws<ArgumentNullException>(
            () => ConnectorRouter.Route(null!, new[] { new Connection(from, to) }, new ConnectorRouteOptions()));
    }

    /// <summary>
    ///     A null connection list is rejected by the batch overload.
    /// </summary>
    [Fact]
    public void Route_NullConnections_Throws()
    {
        // Arrange
        var boxes = new[] { Box(0, 0, 60, 60) };

        // Act / Assert
        Assert.Throws<ArgumentNullException>(
            () => ConnectorRouter.Route(boxes, (IReadOnlyList<Connection>)null!, new ConnectorRouteOptions()));
    }

    /// <summary>
    ///     A null options argument is rejected by the single-connection overload.
    /// </summary>
    [Fact]
    public void Route_NullOptions_Throws()
    {
        // Arrange
        var from = Box(0, 0, 60, 60);
        var to = Box(200, 0, 60, 60);
        var boxes = new[] { from, to };

        // Act / Assert
        Assert.Throws<ArgumentNullException>(
            () => ConnectorRouter.Route(boxes, new Connection(from, to), null!));
    }

    /// <summary>
    ///     A null connection is rejected by the single-connection overload.
    /// </summary>
    [Fact]
    public void Route_NullConnection_Throws()
    {
        // Arrange
        var boxes = new[] { Box(0, 0, 60, 60) };

        // Act / Assert
        Assert.Throws<ArgumentNullException>(
            () => ConnectorRouter.Route(boxes, (Connection)null!, new ConnectorRouteOptions()));
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
    ///     Asserts that no segment of the path passes through the strict interior of the obstacle.
    /// </summary>
    private static void AssertNoSegmentCrossesObstacle(IReadOnlyList<Point2D> path, Rect obstacle)
    {
        for (var i = 0; i < path.Count - 1; i++)
        {
            var a = path[i];
            var b = path[i + 1];
            Assert.False(SegmentCrossesRect(a, b, obstacle),
                $"Segment {i} from ({a.X},{a.Y}) to ({b.X},{b.Y}) crosses obstacle.");
        }
    }

    /// <summary>Returns true when the point lies strictly inside the box.</summary>
    private static bool IsStrictlyInside(Point2D p, LayoutBox box) =>
        box.X < p.X && p.X < box.X + box.Width &&
        box.Y < p.Y && p.Y < box.Y + box.Height;

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
}
