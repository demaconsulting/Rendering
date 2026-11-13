// <copyright file="ConnectorRouterTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout;
using DemaConsulting.Rendering.Layout.Engine;
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
    ///     When only a small shared span sits at one end of a much longer face, the router slides the
    ///     long face's port into that span (no closer to either corner than the marker reach) so one
    ///     straight connector replaces the tiny jog that clearance-inset anchors would force.
    /// </summary>
    [Fact]
    public void ConnectorRouter_Route_SmallSharedSpanOnLongFace_SlidesPortIntoSpanAndGoesStraight()
    {
        // Arrange: a tall source box overlaps a short (20 px) target box only at the very top of the
        // source face; the target's usable span (inset by the margin) is 8..12.
        var from = Box(0, 0, 60, 100);
        var to = Box(200, 0, 60, 20);
        var boxes = new[] { from, to };
        var options = new ConnectorRouteOptions(Clearance: 12.0);

        // Act: route from the tall box into the short box across their facing left/right faces.
        var line = ConnectorRouter.Route(boxes, new Connection(from, to), options);

        // Assert: both ports line up inside the short face's usable span and clear of both corners
        // (the clearance inset of the long face would otherwise put the source port at y = 12 and force
        // a jog), giving a straight line.
        Assert.Equal(2, line.Waypoints.Count);
        var start = line.Waypoints[0];
        var end = line.Waypoints[^1];
        Assert.Equal(from.X + from.Width, start.X, 6);
        Assert.Equal(to.X, end.X, 6);
        Assert.Equal(start.Y, end.Y, 6);
        Assert.InRange(start.Y, 8.0, 12.0);
        AssertPortClearOfCorners(from, start);
        AssertPortClearOfCorners(to, end);
    }

    /// <summary>
    ///     A face shorter than twice the corner margin cannot hold a slid port clear of both corners, so
    ///     it collapses to its centre: the port is not slid and the connector accepts a jog.
    /// </summary>
    [Fact]
    public void ConnectorRouter_Route_FaceShorterThanTwoMargins_KeepsCentre()
    {
        // Arrange: a tall source box and a 10 px target box (its centre, y = 5, is 5 from the source corner).
        var from = Box(0, 0, 60, 100);
        var to = Box(200, 0, 60, 10);
        var options = new ConnectorRouteOptions(Clearance: 12.0);

        // Act
        var line = ConnectorRouter.Route([from, to], new Connection(from, to), options);

        // Assert: the short face keeps its centered anchor; the long face's port is not pulled into the
        // margin, so the route is jogged rather than letting the arrowhead overhang the source's corner.
        var w = line.Waypoints;
        Assert.Equal(to.Y + (to.Height / 2.0), w[^1].Y, 6);
        Assert.True(w.Count > 2, "The short face's centre is nearer the long face's corner than the marker reach, so a jog stays.");
        AssertPortClearOfCorners(from, w[0]);
    }

    /// <summary>
    ///     When the shared span near a corner of a long face is wide enough for a straight connector, the
    ///     overlap-centered anchor is still kept at least the configured clearance away from the corner.
    /// </summary>
    [Fact]
    public void ConnectorRouter_Route_SharedSpanNearCornerOfLongFace_UsesClearanceInset()
    {
        // Arrange: a tall source box overlaps a taller-than-clearance target box only at its very top.
        var from = Box(0, 0, 60, 100);
        var to = Box(200, 0, 60, 24);
        var boxes = new[] { from, to };
        var options = new ConnectorRouteOptions(Clearance: 12.0);

        // Act
        var line = ConnectorRouter.Route(boxes, new Connection(from, to), options);

        // Assert: the overlap centre (y = 12) is exactly the clearance inset, so the connector is
        // straight at the clearance inset of the long source face.
        Assert.Equal(2, line.Waypoints.Count);
        Assert.Equal(from.Y + options.Clearance, line.Waypoints[0].Y, 6);
        Assert.Equal(from.Y + options.Clearance, line.Waypoints[^1].Y, 6);
    }

    /// <summary>
    ///     When a face is too short to keep the configured clearance from both ends, anchor selection
    ///     falls back to that face's own center instead of violating the margin.
    /// </summary>
    [Fact]
    public void ConnectorRouter_Route_ShortFaceForMargin_UsesFaceCenter()
    {
        // Arrange: the source face is shorter than twice the requested clearance.
        var from = Box(0, 0, 60, 10);
        var to = Box(200, 0, 60, 100);
        var boxes = new[] { from, to };
        var options = new ConnectorRouteOptions(Clearance: 12.0);

        // Act: route from the short box into the tall box.
        var line = ConnectorRouter.Route(boxes, new Connection(from, to), options);

        // Assert: the short source face anchors at its own midpoint.
        var start = line.Waypoints[0];
        Assert.Equal(from.X + from.Width, start.X, 6);
        Assert.Equal(from.Y + (from.Height / 2.0), start.Y, 6);
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
    ///     The generic face-end clearance composes with a shape's own narrower connectable extent, so a
    ///     folder top-face anchor stays inside the usable body-top span rather than overriding it.
    /// </summary>
    [Fact]
    public void ConnectorRouter_Route_FolderTopFace_ComposesClearanceWithReducedExtent()
    {
        // Arrange: the source overlaps a point already right of the tab, but still inside the reduced
        // top-face span that should receive the extra clearance inset.
        var from = Box(50, 0, 30, 30);
        var to = Box(0, 120, 140, 90, "Utilities", BoxShape.Folder, folderTabWidth: 60.0, folderTabHeight: 24.0);
        var boxes = new[] { from, to };
        var options = new ConnectorRouteOptions(Clearance: 12.0);

        // Act: route directly into the folder from above.
        var line = ConnectorRouter.Route(boxes, new Connection(from, to), options);

        // Assert: the anchor respects both the folder extent ([61, 140]) and the added clearance,
        // producing the first usable point at X = 61 + 12 = 73 on the recessed body top.
        var end = line.Waypoints[^1];
        Assert.Equal(to.X + 73.0, end.X, 6);
        Assert.Equal(to.Y + 24.0, end.Y, 6);
    }

    /// <summary>
    ///     When only the folder-tab width is supplied, the router still applies its generic tab-height
    ///     fallback so the anchor remains off the tab and lands on the recessed body top.
    /// </summary>
    [Fact]
    public void Route_FolderTopFace_WidthOnlyHint_UsesFallbackTabHeight()
    {
        // Arrange: the source overlaps only the explicit tab width while the height must come from the fallback.
        var from = Box(40, 0, 40, 40);
        var to = Box(0, 120, 140, 90, "Utilities", BoxShape.Folder, folderTabWidth: 60.0);
        var boxes = new[] { from, to };
        var expectedTabHeight = BoxMetrics.FolderTabHeight(Themes.Light);

        // Act: route directly into the folder from above.
        var line = ConnectorRouter.Route(boxes, new Connection(from, to), new ConnectorRouteOptions());

        // Assert: the target anchor still avoids the tab and projects onto the recessed body top.
        var end = line.Waypoints[^1];
        Assert.True(end.X > to.X + 60.0, "Target anchor should remain to the right of the explicit tab width.");
        Assert.Equal(to.Y + expectedTabHeight, end.Y, 6);
    }

    /// <summary>
    ///     When only the folder-tab height is supplied, the router still applies its generic tab-width
    ///     fallback so the top-face anchor avoids the drawn tab and projects onto the recessed body top.
    /// </summary>
    [Fact]
    public void Route_FolderTopFace_HeightOnlyHint_UsesFallbackTabWidth()
    {
        // Arrange: the source overlaps the fallback tab region while the width must come from the fallback formula.
        var from = Box(40, 0, 40, 40);
        var to = Box(0, 120, 140, 90, "Utilities", BoxShape.Folder, folderTabHeight: 24.0);
        var boxes = new[] { from, to };
        var expectedTabWidth = Math.Min(
            to.Width * NotationMetrics.FolderTabMaxWidthFraction,
            Math.Max(
                NotationMetrics.FolderTabMinWidth,
                (to.Label?.Length ?? 4) * Themes.Light.FontSizeBody * NotationMetrics.FolderLabelCharWidthFactor +
                (2.0 * Themes.Light.LabelPadding)));

        // Act: route directly into the folder from above.
        var line = ConnectorRouter.Route(boxes, new Connection(from, to), new ConnectorRouteOptions());

        // Assert: the target anchor stays to the right of the fallback tab and reaches the recessed body top.
        var end = line.Waypoints[^1];
        Assert.True(end.X > to.X + expectedTabWidth, "Target anchor should remain to the right of the fallback tab width.");
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
    ///     A note's top face excludes the portion consumed by the folded-corner cut, so a connector
    ///     approaching from above clamps to the first usable point left of the fold instead of anchoring
    ///     in the cut-off triangle.
    /// </summary>
    [Fact]
    public void Route_NoteTopFace_FoldExcludedFromConnectableExtent()
    {
        // Arrange: the source sits above the note and overlaps only the folded-corner region horizontally.
        // A 140x90 note folds min(140, 90) * 0.25 = 22.5, capped at NoteFoldMaxSize (16).
        var from = Box(130, 0, 40, 40);
        var to = Box(0, 120, 140, 90, "Design Note", BoxShape.Note);
        var boxes = new[] { from, to };

        // Act: route directly into the note from above.
        var line = ConnectorRouter.Route(boxes, new Connection(from, to), new ConnectorRouteOptions());

        // Assert: the target anchor lands left of the fold and directly on the bounding-box top edge
        // (no projection offset — the safe zone already lies on the real outline).
        var end = line.Waypoints[^1];
        Assert.True(
            end.X < to.X + to.Width - NotationMetrics.NoteFoldMaxSize,
            "Target anchor should clamp to the usable top-face extent left of the fold.");
        Assert.Equal(to.Y, end.Y, 6);
    }

    /// <summary>
    ///     A note's right face excludes the portion consumed by the folded-corner cut (the topmost
    ///     strip), so a connector approaching from the right clamps below the fold instead of anchoring
    ///     in the cut-off triangle.
    /// </summary>
    [Fact]
    public void Route_NoteRightFace_FoldExcludedFromConnectableExtent()
    {
        // Arrange: the source sits to the right of the note, level with the folded-corner region.
        var from = Box(200, 120, 40, 20);
        var to = Box(0, 120, 140, 90, "Design Note", BoxShape.Note);
        var boxes = new[] { from, to };

        // Act: route directly into the note from the right.
        var line = ConnectorRouter.Route(boxes, new Connection(from, to), new ConnectorRouteOptions());

        // Assert: the target anchor lands below the fold and directly on the bounding-box right edge.
        var end = line.Waypoints[^1];
        Assert.True(
            end.Y > to.Y + NotationMetrics.NoteFoldMaxSize,
            "Target anchor should clamp to the usable right-face extent below the fold.");
        Assert.Equal(to.X + to.Width, end.X, 6);
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
    ///     Both endpoint boxes are ordinary obstacles, yet the connector still reaches their boundary
    ///     anchors cleanly: the underlying orthogonal edge router steps each anchor off its face with a
    ///     perpendicular approach stub that clears the endpoint before pathfinding, then reattaches the
    ///     true anchor when assembling the final path.
    /// </summary>
    [Fact]
    public void Route_EndpointBoxes_ReachBoundaryAnchorsViaApproachStub()
    {
        // Arrange: two large adjacent boxes with only a narrow gap between their facing edges. The
        // source anchor sits on the source's right face and the target anchor on the target's left
        // face; both anchors lie on box boundaries that only route cleanly via each endpoint's
        // approach stub.
        var from = Box(0, 0, 120, 120);
        var to = Box(140, 0, 120, 120);
        var boxes = new[] { from, to };

        // Act
        var line = ConnectorRouter.Route(boxes, new Connection(from, to), new ConnectorRouteOptions());

        // Assert: a clean, straight orthogonal hop between the facing faces
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
    ///     Reproduces the true DictionaryMark rendering defect (a flat, length-independent soft-obstacle
    ///     penalty in the underlying orthogonal router's A* search): a small source box sits just below
    ///     a much wider target box across a narrow vertical gap, so every connector leaving the source's
    ///     top face is stepped off to the identical Y line and the sparse narrow-gap grid offers only one
    ///     natural corridor row. Three connections fan out from that shared source face to three widely
    ///     spaced anchors spread across the target's shared face. Before the length-proportional fix, the
    ///     interior horizontal corridor segments of connectors 1 and 2 collapsed onto the exact same grid
    ///     row and overlapped for hundreds of pixels because the flat penalty underpriced a long overlap
    ///     relative to a lane-change detour; after the fix each interior segment must resolve to a
    ///     distinct row (or overlap for only a trivial span) instead.
    /// </summary>
    [Fact]
    public void Route_ThreeConnectorsNarrowGap_InteriorCorridorsDoNotOverlapAlongLength()
    {
        // Arrange: geometry taken directly from the reported DictionaryMark defect repro — a small
        // source box directly below (with a 48px gap) a much wider target box.
        var source = Box(12, 1291, 232, 318, "OtsDependencies");
        var target = Box(166, 36, 1208, 1207, "DictionaryMark");
        var boxes = new[] { source, target };

        // Three connections between the same pair of boxes fan out across both shared faces.
        var connections = new[]
        {
            new Connection(source, target, EndMarkerStyle.FilledDiamond),
            new Connection(source, target, EndMarkerStyle.FilledDiamond),
            new Connection(source, target, EndMarkerStyle.FilledDiamond),
        };

        // Act
        var lines = ConnectorRouter.Route(boxes, connections, new ConnectorRouteOptions());

        // Assert: every connector still forms a valid orthogonal path.
        Assert.All(lines, line => AssertAllSegmentsOrthogonal(line.Waypoints));

        // Assert: no two distinct connectors' interior segments ride the same grid line for an
        // extended span (the visually merged trunk this test guards against).
        AssertNoCollinearOverlapAcrossLines(lines);
    }

    /// <summary>
    ///     Variation of <see cref="Route_ThreeConnectorsNarrowGap_InteriorCorridorsDoNotOverlapAlongLength"/>
    ///     with more fan-out edges (five instead of three) from the same narrow-gap source face, to
    ///     confirm the fix generalizes beyond the exact reported connector count.
    /// </summary>
    [Fact]
    public void Route_FiveConnectorsNarrowGap_InteriorCorridorsDoNotOverlapAlongLength()
    {
        // Arrange: same narrow-gap geometry as the primary repro, but with five fan-out connections.
        var source = Box(12, 1291, 232, 318, "OtsDependencies");
        var target = Box(166, 36, 1208, 1207, "DictionaryMark");
        var boxes = new[] { source, target };

        var connections = Enumerable.Range(0, 5)
            .Select(_ => new Connection(source, target, EndMarkerStyle.FilledDiamond))
            .ToArray();

        // Act
        var lines = ConnectorRouter.Route(boxes, connections, new ConnectorRouteOptions());

        // Assert
        Assert.All(lines, line => AssertAllSegmentsOrthogonal(line.Waypoints));
        AssertNoCollinearOverlapAcrossLines(lines);
    }

    /// <summary>
    ///     Fan-in variation of <see cref="Route_ThreeConnectorsNarrowGap_InteriorCorridorsDoNotOverlapAlongLength"/>:
    ///     three distinct small source boxes, each across the same narrow gap from the shared wide target,
    ///     converge on the same target face. This mirrors the reported defect direction (many-to-one)
    ///     with genuinely separate source boxes rather than repeated connections between the same pair.
    /// </summary>
    [Fact]
    public void Route_ThreeSourceBoxesNarrowGap_InteriorCorridorsDoNotOverlapAlongLength()
    {
        // Arrange: three narrow source boxes side by side below the same narrow gap, all facing the
        // same much wider target box above them.
        var sourceA = Box(12, 1291, 60, 318, "A");
        var sourceB = Box(92, 1291, 60, 318, "B");
        var sourceC = Box(172, 1291, 60, 318, "C");
        var target = Box(166, 36, 1208, 1207, "DictionaryMark");
        var boxes = new[] { sourceA, sourceB, sourceC, target };

        var connections = new[]
        {
            new Connection(sourceA, target, EndMarkerStyle.FilledDiamond),
            new Connection(sourceB, target, EndMarkerStyle.FilledDiamond),
            new Connection(sourceC, target, EndMarkerStyle.FilledDiamond),
        };

        // Act
        var lines = ConnectorRouter.Route(boxes, connections, new ConnectorRouteOptions());

        // Assert
        Assert.All(lines, line => AssertAllSegmentsOrthogonal(line.Waypoints));
        AssertNoCollinearOverlapAcrossLines(lines);
    }

    /// <summary>
    ///     Reproduces the SysML2Tools rendering defect: nine parallel, unmerged edges from a small
    ///     source box converge on the left face of a much taller multi-row-compartment target box.
    ///     Before the fix, routing each connection excluded <em>both</em> connection endpoints from the
    ///     hard-obstacle set for the whole path (not just the immediate approach stub), so a later
    ///     connector squeezed by earlier already-routed connectors' soft obstacles was free to detour
    ///     straight through its own target box's interior — visually cutting across the compartment's
    ///     row text — before curving back out to its real anchor. After the fix, every box (including a
    ///     connection's own endpoints) is a hard obstacle for the rest of the path; only the short
    ///     perpendicular docking stub the underlying router already adds is exempt.
    /// </summary>
    [Fact]
    public void Route_NineParallelEdgesIntoCompartmentBox_DoNotCrossTargetInterior()
    {
        // Arrange: geometry mirroring the reported bug — a small port box to the left of a much taller
        // box with a nine-row ports compartment, joined by nine distinct unmerged edges (mirroring
        // CoreOptions.MergeParallelEdges = false).
        var eePort = Box(12, 12, 130, 50, "EEPort");
        var lbo = Box(166, 12, 130, 242, "LBO");
        var boxes = new[] { eePort, lbo };

        var connections = Enumerable.Range(0, 9)
            .Select(_ => new Connection(eePort, lbo, EndMarkerStyle.FilledDiamond))
            .ToArray();

        // Act
        var lines = ConnectorRouter.Route(boxes, connections, new ConnectorRouteOptions());

        // Assert: every routed connector reaches LBO's face without any segment cutting through
        // either box's own strict interior (including its own endpoints).
        var lboRect = new Rect(lbo.X, lbo.Y, lbo.Width, lbo.Height);
        var eePortRect = new Rect(eePort.X, eePort.Y, eePort.Width, eePort.Height);
        Assert.All(lines, line =>
        {
            AssertNoSegmentCrossesObstacle(line.Waypoints, lboRect);
            AssertNoSegmentCrossesObstacle(line.Waypoints, eePortRect);
        });
    }

    /// <summary>
    ///     Regression test for the "coiled/looping" connector-router robustness bug: nine parallel,
    ///     unmerged edges from a small source box into a much taller multi-row-compartment target box,
    ///     separated by a narrow gap, must all route as clean fans that never stray outside the two
    ///     boxes' own bounding envelope. Before the fix, <c>OrthogonalEdgeRouter</c>'s soft-obstacle
    ///     lane candidates marched outward without bound as each successive connector became a soft
    ///     obstacle for the next (see <c>OrthogonalEdgeRouter.AddSoftCandidate</c>), eventually landing
    ///     a lane behind the source box's own footprint — producing a path that leaves toward the gap,
    ///     reverses back underneath the source box, then recovers. The existing
    ///     <see cref="Route_NineParallelEdgesIntoCompartmentBox_DoNotCrossTargetInterior"/> test uses the
    ///     same geometry but only asserts no segment cuts through either box's interior, which this
    ///     defect does not trigger (the loop-back travels behind, not through, the source box), so it
    ///     does not catch this bug. This test's bounding-envelope assertion does.
    /// </summary>
    [Fact]
    public void Route_NineParallelEdgesIntoCompartmentBox_DoNotLoopBehindEitherBox()
    {
        // Arrange: same geometry as Route_NineParallelEdgesIntoCompartmentBox_DoNotCrossTargetInterior
        // — a small port box beside a much taller multi-row-compartment box, joined by nine distinct
        // unmerged edges.
        var eePort = Box(12, 12, 130, 50, "EEPort");
        var lbo = Box(166, 12, 130, 242, "LBO");
        var boxes = new[] { eePort, lbo };

        var connections = Enumerable.Range(0, 9)
            .Select(_ => new Connection(eePort, lbo, EndMarkerStyle.FilledDiamond))
            .ToArray();

        // Act
        var lines = ConnectorRouter.Route(boxes, connections, new ConnectorRouteOptions());

        // Assert: no waypoint of any routed connector strays outside the bounding envelope of the two
        // connected boxes (expanded by a generous allowance for the approach stub) — this is what
        // "looping back behind a box already left" violates, and what a clean fan-out never needs.
        AssertWaypointsWithinBoxPairEnvelope(lines, eePort, lbo);
        Assert.All(lines, line => AssertAllSegmentsOrthogonal(line.Waypoints));
        AssertNoTransversalCrossingAcrossLines(lines);
    }

    /// <summary>
    ///     Asserts that every waypoint of every routed line lies within the bounding envelope of the two
    ///     connected boxes, expanded by a generous fixed allowance for the router's own perpendicular
    ///     approach stub (see <c>OrthogonalEdgeRouter.StepOff</c>). A route that needs to travel further
    ///     than this outside the pair's own footprint has looped back behind a box it already left,
    ///     rather than forming a clean fan between the two boxes.
    /// </summary>
    private static void AssertWaypointsWithinBoxPairEnvelope(
        IReadOnlyList<LayoutLine> lines,
        LayoutBox a,
        LayoutBox b)
    {
        const double allowance = 20.0;
        var envelope = new Rect(
            Math.Min(a.X, b.X) - allowance,
            Math.Min(a.Y, b.Y) - allowance,
            Math.Max(a.X + a.Width, b.X + b.Width) - Math.Min(a.X, b.X) + (2 * allowance),
            Math.Max(a.Y + a.Height, b.Y + b.Height) - Math.Min(a.Y, b.Y) + (2 * allowance));

        Assert.All(lines, line => Assert.All(line.Waypoints, p => AssertPointInsideRect(envelope, p)));
    }

    /// <summary>Asserts that a point lies within (or on the boundary of) the given rectangle.</summary>
    private static void AssertPointInsideRect(Rect r, Point2D p) =>
        Assert.True(
            p.X >= r.X && p.X <= r.X + r.Width && p.Y >= r.Y && p.Y <= r.Y + r.Height,
            $"Point ({p.X},{p.Y}) lies outside the expected bounding envelope " +
            $"({r.X},{r.Y})-({r.X + r.Width},{r.Y + r.Height}).");

    /// <summary>
    ///     Asserts that no two of the given lines' <em>interior</em> segments — those excluding each
    ///     line's first and last segment, which are the endpoint-adjacent approach legs the router's own
    ///     <c>AddLineObstacles</c> design intentionally lets several connectors share (see its remarks in
    ///     <c>ConnectorRouter.cs</c>) — are collinear and overlap for more than a trivial span. Two
    ///     horizontal segments are collinear when they share the same Y (within a tight tolerance); two
    ///     vertical segments are collinear when they share the same X. The overlap span is the length of
    ///     their shared range along the collinear axis; only spans exceeding <paramref name="maxOverlap"/>
    ///     are flagged, so incidental few-pixel touches (e.g. two corridors merely crossing the same grid
    ///     line for an instant) are not falsely reported.
    /// </summary>
    private static void AssertNoCollinearOverlapAcrossLines(IReadOnlyList<LayoutLine> lines, double maxOverlap = 5.0)
    {
        // Collect each line's interior segments (excluding the first and last, matching
        // AddLineObstacles' own endpoint-adjacent exclusion).
        var interiorSegments = new List<(int LineIndex, Point2D A, Point2D B)>();
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var waypoints = lines[lineIndex].Waypoints;
            for (var i = 1; i < waypoints.Count - 2; i++)
            {
                interiorSegments.Add((lineIndex, waypoints[i], waypoints[i + 1]));
            }
        }

        for (var x = 0; x < interiorSegments.Count; x++)
        {
            for (var y = x + 1; y < interiorSegments.Count; y++)
            {
                var (lineX, ax, bx) = interiorSegments[x];
                var (lineY, ay, by) = interiorSegments[y];
                if (lineX == lineY)
                {
                    // Only cross-connector overlaps are of interest here.
                    continue;
                }

                var overlap = CollinearOverlapLength(ax, bx, ay, by);
                Assert.True(
                    overlap <= maxOverlap,
                    $"Interior segments of connectors {lineX} and {lineY} are collinear and overlap for " +
                    $"{overlap:F1}px (({ax.X},{ax.Y})-({bx.X},{bx.Y}) vs ({ay.X},{ay.Y})-({by.X},{by.Y})), " +
                    "exceeding the trivial-overlap threshold.");
            }
        }
    }

    /// <summary>
    ///     Returns the length of the collinear overlap between two axis-aligned segments, or 0 when they
    ///     are not both horizontal at the same Y, not both vertical at the same X, or do not overlap.
    /// </summary>
    private static double CollinearOverlapLength(Point2D a1, Point2D b1, Point2D a2, Point2D b2)
    {
        const double tolerance = 1e-6;

        var horizontal1 = Math.Abs(a1.Y - b1.Y) < tolerance;
        var horizontal2 = Math.Abs(a2.Y - b2.Y) < tolerance;
        if (horizontal1 && horizontal2 && Math.Abs(a1.Y - a2.Y) < tolerance)
        {
            var lo = Math.Max(Math.Min(a1.X, b1.X), Math.Min(a2.X, b2.X));
            var hi = Math.Min(Math.Max(a1.X, b1.X), Math.Max(a2.X, b2.X));
            return Math.Max(0.0, hi - lo);
        }

        var vertical1 = Math.Abs(a1.X - b1.X) < tolerance;
        var vertical2 = Math.Abs(a2.X - b2.X) < tolerance;
        if (vertical1 && vertical2 && Math.Abs(a1.X - a2.X) < tolerance)
        {
            var lo = Math.Max(Math.Min(a1.Y, b1.Y), Math.Min(a2.Y, b2.Y));
            var hi = Math.Min(Math.Max(a1.Y, b1.Y), Math.Max(a2.Y, b2.Y));
            return Math.Max(0.0, hi - lo);
        }

        return 0.0;
    }

    /// <summary>
    ///     Asserts that no two of the given lines' <em>interior</em> segments (same endpoint-adjacent
    ///     exclusion as <see cref="AssertNoCollinearOverlapAcrossLines"/>) transversally cross one
    ///     another — i.e. one is horizontal, the other vertical, and they intersect strictly inside both
    ///     segments' interiors, not merely at a shared endpoint. This is the direct "no two distinct
    ///     connectors' paths cross" check: unlike <see cref="AssertNoCollinearOverlapAcrossLines"/>
    ///     (which only flags two <em>parallel</em> segments riding the same lane for an extended span),
    ///     a transversal crossing is a genuine perpendicular "X" intersection between two different
    ///     connectors, which is visually a crossing regardless of how short the shared point is.
    /// </summary>
    private static void AssertNoTransversalCrossingAcrossLines(IReadOnlyList<LayoutLine> lines)
    {
        // Collect each line's interior segments (excluding the first and last, matching
        // AddLineObstacles' own endpoint-adjacent exclusion and AssertNoCollinearOverlapAcrossLines'
        // convention above).
        var interiorSegments = new List<(int LineIndex, Point2D A, Point2D B)>();
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var waypoints = lines[lineIndex].Waypoints;
            for (var i = 1; i < waypoints.Count - 2; i++)
            {
                interiorSegments.Add((lineIndex, waypoints[i], waypoints[i + 1]));
            }
        }

        for (var x = 0; x < interiorSegments.Count; x++)
        {
            for (var y = x + 1; y < interiorSegments.Count; y++)
            {
                var (lineX, ax, bx) = interiorSegments[x];
                var (lineY, ay, by) = interiorSegments[y];
                if (lineX == lineY)
                {
                    // Only cross-connector crossings are of interest here.
                    continue;
                }

                Assert.False(
                    SegmentsCrossTransversally(ax, bx, ay, by),
                    $"Interior segments of connectors {lineX} and {lineY} cross transversally " +
                    $"(({ax.X},{ax.Y})-({bx.X},{bx.Y}) vs ({ay.X},{ay.Y})-({by.X},{by.Y})).");
            }
        }
    }

    /// <summary>
    ///     Returns <see langword="true"/> when one of the two axis-aligned segments is horizontal, the
    ///     other vertical, and they intersect strictly inside both segments' interiors (not merely at a
    ///     shared endpoint or a collinear touch). Uses the same tight tolerance convention as
    ///     <see cref="CollinearOverlapLength"/>/<see cref="SegmentCrossesRect"/> elsewhere in this file.
    /// </summary>
    private static bool SegmentsCrossTransversally(Point2D a1, Point2D b1, Point2D a2, Point2D b2)
    {
        const double tolerance = 1e-6;

        var horizontal1 = Math.Abs(a1.Y - b1.Y) < tolerance;
        var vertical1 = Math.Abs(a1.X - b1.X) < tolerance;
        var horizontal2 = Math.Abs(a2.Y - b2.Y) < tolerance;
        var vertical2 = Math.Abs(a2.X - b2.X) < tolerance;

        // Only interested in one horizontal + one vertical segment; two parallel segments (both
        // horizontal or both vertical) can never cross transversally.
        Point2D hA, hB, vA, vB;
        if (horizontal1 && vertical2)
        {
            (hA, hB, vA, vB) = (a1, b1, a2, b2);
        }
        else if (vertical1 && horizontal2)
        {
            (hA, hB, vA, vB) = (a2, b2, a1, b1);
        }
        else
        {
            return false;
        }

        var y = hA.Y;
        var xLo = Math.Min(hA.X, hB.X);
        var xHi = Math.Max(hA.X, hB.X);
        var x = vA.X;
        var yLo = Math.Min(vA.Y, vB.Y);
        var yHi = Math.Max(vA.Y, vB.Y);

        return x > xLo + tolerance && x < xHi - tolerance &&
               y > yLo + tolerance && y < yHi - tolerance;
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
    ///     The internal per-connection-obstacle batch entry point lets a connector whose endpoint is
    ///     nested in a container cross that container's wall: with the enclosing container omitted from
    ///     the connector's obstacle set, the route from the nested box straight out to a far box is a
    ///     clean orthogonal line that crosses the container's boundary.
    /// </summary>
    [Fact]
    public void RouteBatch_EndpointAncestorOmittedFromObstacles_RouteCrossesAncestorWall()
    {
        // Arrange: container C (0..200 x 0..200) holding nested a, and a far target b outside it.
        var container = Box(0, 0, 200, 200, "C");
        var nested = Box(40, 80, 60, 40, "a");
        var far = Box(400, 80, 60, 40, "b");
        var connections = new[] { new Connection(nested, far, EndMarkerStyle.FilledArrow) };

        // Act: the obstacle set holds the target and a sibling but not the endpoint's ancestor container.
        var lines = ConnectorRouter.RouteBatch(
            connections,
            new ConnectorRouteOptions(),
            _ => new[] { nested, far });

        // Assert: the line starts on the nested box, ends on the target, and crosses the container wall.
        var line = Assert.Single(lines);
        Assert.Equal(nested.X + nested.Width, line.Waypoints[0].X, 3);
        Assert.Equal(far.X, line.Waypoints[^1].X, 3);
        Assert.Contains(
            Enumerable.Range(0, line.Waypoints.Count - 1),
            i => Math.Min(line.Waypoints[i].X, line.Waypoints[i + 1].X) < container.X + container.Width &&
                 Math.Max(line.Waypoints[i].X, line.Waypoints[i + 1].X) > container.X + container.Width);
        Assert.Equal(EndMarkerStyle.FilledArrow, line.TargetEnd);
    }

    /// <summary>
    ///     Each connection in a batch is avoided against its own obstacle set: an unrelated box present in
    ///     a connection's set is steered around, while the same box absent from another connection's set
    ///     leaves that connection's straight route untouched.
    /// </summary>
    [Fact]
    public void RouteBatch_PerConnectionObstacles_AreHonoredIndependently()
    {
        // Arrange: two parallel source/target pairs at different heights, and a blocker sitting only on
        // the first pair's straight path.
        var from1 = Box(0, 0, 60, 60);
        var to1 = Box(400, 0, 60, 60);
        var from2 = Box(0, 300, 60, 60);
        var to2 = Box(400, 300, 60, 60);
        var blocker = Box(200, -20, 40, 100);
        var connections = new[] { new Connection(from1, to1), new Connection(from2, to2) };

        // Act: only the first connection knows about the blocker.
        var lines = ConnectorRouter.RouteBatch(
            connections,
            new ConnectorRouteOptions(),
            index => index == 0 ? new[] { from1, to1, blocker } : new[] { from2, to2 });

        // Assert: the first route avoids the blocker; the second stays a straight line.
        AssertNoSegmentCrossesObstacle(lines[0].Waypoints, new Rect(blocker.X, blocker.Y, blocker.Width, blocker.Height));
        Assert.Equal(2, lines[1].Waypoints.Count);
    }

    /// <summary>
    ///     The public batch overload is unchanged by the internal entry point: it produces exactly the
    ///     routes the internal entry point yields when every connection shares the same obstacle list.
    /// </summary>
    [Fact]
    public void Route_BatchOverload_MatchesInternalBatchWithSharedObstacles()
    {
        // Arrange
        var from = Box(0, 0, 60, 60);
        var middle = Box(150, -20, 60, 100);
        var to = Box(300, 0, 60, 60);
        var boxes = new[] { from, middle, to };
        var connections = new[] { new Connection(from, to), new Connection(to, from) };

        // Act
        var publicLines = ConnectorRouter.Route(boxes, connections, new ConnectorRouteOptions());
        var internalLines = ConnectorRouter.RouteBatch(connections, new ConnectorRouteOptions(), _ => boxes);

        // Assert
        Assert.Equal(publicLines.Count, internalLines.Count);
        for (var i = 0; i < publicLines.Count; i++)
        {
            Assert.Equal(publicLines[i].Waypoints, internalLines[i].Waypoints);
        }
    }

    /// <summary>
    ///     A fixed (already occupied) anchor on a box face makes a batch anchor that would land on top of it
    ///     move along the face to a free spot, while an anchor already clear of it stays where it was.
    /// </summary>
    [Fact]
    public void RouteBatch_FixedAnchorOnFace_MovesBatchAnchorClearOfIt()
    {
        // Arrange: a -> b with the natural anchor at the middle of a's right face, which is occupied.
        var a = Box(0, 0, 60, 60);
        var b = Box(300, 0, 60, 60);
        var connections = new[] { new Connection(a, b) };
        var occupied = new[] { new ConnectorRouter.FixedAnchor(a, PortSide.Right, 30.0) };

        // Act
        var unconstrained = ConnectorRouter.RouteBatch(connections, new ConnectorRouteOptions(), _ => new[] { a, b });
        var moved = ConnectorRouter.RouteBatch(connections, new ConnectorRouteOptions(), _ => new[] { a, b }, occupied);

        // Assert: unconstrained anchors mid-face; with the occupied anchor it sits at least 16px away.
        Assert.Equal(30.0, unconstrained[0].Waypoints[0].Y, 3);
        Assert.True(Math.Abs(moved[0].Waypoints[0].Y - 30.0) >= 16.0);
    }

    /// <summary>
    ///     A point on a box face resolves to that face with its local along-coordinate; a point away from
    ///     every face does not resolve.
    /// </summary>
    [Fact]
    public void TryResolveFaceAnchor_PointOnFace_ResolvesFaceAndAlong()
    {
        // Arrange
        var box = Box(100, 50, 80, 40);

        // Act / Assert
        Assert.True(ConnectorRouter.TryResolveFaceAnchor(box, new Point2D(180, 62), out var right));
        Assert.Equal(PortSide.Right, right.Side);
        Assert.Equal(12.0, right.Along, 3);
        Assert.True(ConnectorRouter.TryResolveFaceAnchor(box, new Point2D(130, 50), out var top));
        Assert.Equal(PortSide.Top, top.Side);
        Assert.Equal(30.0, top.Along, 3);
        Assert.False(ConnectorRouter.TryResolveFaceAnchor(box, new Point2D(140, 70), out _));
    }

    // ---------------------------------------------------------------------------------------------
    // Port sliding (straight-lane alignment), minimum end legs and the cross-hierarchy profile
    // ---------------------------------------------------------------------------------------------

    /// <summary>The length of the base canvas used by <see cref="OrientedBox"/>.</summary>
    private const double OrientedCanvas = 500.0;

    /// <summary>
    ///     Places a box described in a left-to-right frame (flow along +x, across along +y) into the
    ///     requested orientation, so one scenario exercises all four facing-face combinations.
    /// </summary>
    private static LayoutBox OrientedBox(string orientation, double along, double across, double length, double thickness) => orientation switch
    {
        "LeftToRight" => Box(along, across, length, thickness),
        "RightToLeft" => Box(OrientedCanvas - along - length, across, length, thickness),
        "TopToBottom" => Box(across, along, thickness, length),
        "BottomToTop" => Box(across, OrientedCanvas - along - length, thickness, length),
        _ => throw new ArgumentOutOfRangeException(nameof(orientation)),
    };

    /// <summary>Returns true when the point lies on the outline of the box.</summary>
    private static bool OnOutline(LayoutBox box, Point2D p)
    {
        const double tolerance = 1e-6;
        var onVertical = (Math.Abs(p.X - box.X) < tolerance || Math.Abs(p.X - (box.X + box.Width)) < tolerance)
                         && p.Y >= box.Y - tolerance && p.Y <= box.Y + box.Height + tolerance;
        var onHorizontal = (Math.Abs(p.Y - box.Y) < tolerance || Math.Abs(p.Y - (box.Y + box.Height)) < tolerance)
                           && p.X >= box.X - tolerance && p.X <= box.X + box.Width + tolerance;
        return onVertical || onHorizontal;
    }

    /// <summary>Returns the length of the segment starting at waypoint <paramref name="i"/>.</summary>
    private static double LegLength(IReadOnlyList<Point2D> waypoints, int i) =>
        Math.Abs(waypoints[i + 1].X - waypoints[i].X) + Math.Abs(waypoints[i + 1].Y - waypoints[i].Y);

    /// <summary>
    ///     When a free lane (just clear of the intervening box) lies inside both facing faces' spans, the
    ///     router slides both ports onto that lane and draws one straight segment — in every one of the four
    ///     facing orientations — instead of stepping off the face centers and jogging around the obstacle.
    /// </summary>
    /// <param name="orientation">The facing-face orientation under test.</param>
    [Theory]
    [InlineData("LeftToRight")]
    [InlineData("RightToLeft")]
    [InlineData("TopToBottom")]
    [InlineData("BottomToTop")]
    public void Route_LaneWithinBothFaceSpans_ProducesSingleStraightSegment(string orientation)
    {
        // Arrange: faces span 0..50 across the flow; the blocker's far edge + clearance (12) puts the
        // first free lane at 32, inside both faces, while the face centers (25) are blocked.
        var from = OrientedBox(orientation, 0, 0, 100, 50);
        var to = OrientedBox(orientation, 400, 0, 100, 50);
        var blocker = OrientedBox(orientation, 200, -40, 100, 60);
        var options = new ConnectorRouteOptions(Clearance: 12.0);

        // Act
        var line = ConnectorRouter.Route([from, to, blocker], new Connection(from, to), options);

        // Assert: a single straight segment between the facing faces that keeps the blocker's clearance.
        Assert.Equal(2, line.Waypoints.Count);
        var start = line.Waypoints[0];
        var end = line.Waypoints[^1];
        Assert.True(OnOutline(from, start), "The line must start on the source box.");
        Assert.True(OnOutline(to, end), "The line must end on the target box.");
        Assert.True(Math.Abs(start.X - end.X) < 1e-6 || Math.Abs(start.Y - end.Y) < 1e-6, "The line must be straight.");
        var padded = new Rect(blocker.X - 12.0, blocker.Y - 12.0, blocker.Width + 24.0, blocker.Height + 24.0);
        Assert.False(SegmentCrossesRect(start, end, padded), "The lane must keep the blocker's clearance.");
    }

    /// <summary>
    ///     When the free lane lies inside only the source face's span, the route is no longer the
    ///     four-bend jog (down, across, up, in) it was with fixed anchors: sliding the source port onto the
    ///     lane leaves at most two bends.
    /// </summary>
    [Fact]
    public void Route_LaneWithinOnlyOneFaceSpan_UsesAtMostTwoBends()
    {
        // Arrange: a tall source and a short target; the lane (blocker edge + 12 = 42) is inside the
        // source face (0..100) but past the target face (0..20).
        var from = Box(0, 0, 100, 100);
        var to = Box(400, 0, 100, 20);
        var blocker = Box(200, -40, 100, 70);

        // Act
        var line = ConnectorRouter.Route([from, to, blocker], new Connection(from, to), new ConnectorRouteOptions());

        // Assert: at most two bends (four waypoints), the target port unchanged on its own face.
        Assert.InRange(line.Waypoints.Count, 2, 4);
        AssertAllSegmentsOrthogonal(line.Waypoints);
        Assert.True(OnOutline(to, line.Waypoints[^1]));
        Assert.InRange(line.Waypoints[^1].Y, to.Y, to.Y + to.Height);
    }

    /// <summary>
    ///     When no lane fits either face (the blocker spans both faces entirely) a jog is unavoidable, and
    ///     it keeps a straight first leg of at least the connector stub and a final approach long enough
    ///     for the end marker plus the bend radius.
    /// </summary>
    /// <param name="end">The target end-marker style.</param>
    /// <param name="minimumLastLeg">The smallest acceptable final straight leg.</param>
    [Theory]
    [InlineData(EndMarkerStyle.FilledArrow, 15.0)]
    [InlineData(EndMarkerStyle.FilledDiamond, 19.0)]
    public void Route_LaneOutsideBothFaceSpans_KeepsJogWithMinimumLegs(EndMarkerStyle end, double minimumLastLeg)
    {
        // Arrange
        var from = Box(0, 0, 100, 50);
        var to = Box(400, 0, 100, 50);
        var blocker = Box(200, -100, 100, 200);

        // Act
        var line = ConnectorRouter.Route([from, to, blocker], new Connection(from, to, end), new ConnectorRouteOptions());

        // Assert: a real jog (more than one bend) with long enough end legs; no box is crossed.
        var w = line.Waypoints;
        Assert.True(w.Count > 3, "A jog is unavoidable here.");
        AssertAllSegmentsOrthogonal(w);
        Assert.True(LegLength(w, 0) >= 8.0, $"First leg {LegLength(w, 0)} is shorter than the connector stub.");
        Assert.True(LegLength(w, w.Count - 2) >= minimumLastLeg, $"Final leg {LegLength(w, w.Count - 2)} is shorter than the marker plus radius.");
        AssertNoSegmentCrossesObstacle(w, new Rect(blocker.X, blocker.Y, blocker.Width, blocker.Height));
    }

    /// <summary>
    ///     A gap too narrow to hold both legs still yields a route that never crosses a box, with the
    ///     legacy half-gap legs on either side of the jog.
    /// </summary>
    [Fact]
    public void Route_NarrowGapJog_StillReturnsRouteAndNeverCrossesBoxes()
    {
        // Arrange: a 20 px gap and no vertical overlap, so a jog inside the gap is required.
        var from = Box(0, 0, 40, 40);
        var to = Box(60, 70, 40, 40);

        // Act
        var line = ConnectorRouter.Route([from, to], new Connection(from, to, EndMarkerStyle.FilledArrow), new ConnectorRouteOptions());

        // Assert
        var w = line.Waypoints;
        AssertAllSegmentsOrthogonal(w);
        AssertNoSegmentCrossesObstacle(w, new Rect(from.X, from.Y, from.Width, from.Height));
        AssertNoSegmentCrossesObstacle(w, new Rect(to.X, to.Y, to.Width, to.Height));
        Assert.True(LegLength(w, w.Count - 2) >= 10.0 - 1e-6, "The final leg keeps the legacy half-gap length.");
    }

    /// <summary>
    ///     A slid port keeps the arrowhead inside its face: a lane that leaves the port closer to a corner
    ///     than the marker's reach (stroke-scaled half-width plus the border half-stroke) is refused, so
    ///     that connector keeps its jog and default anchors, while a lane that leaves the full reach is
    ///     drawn straight. Whatever the topology, both ports stay at least the reach from every corner.
    /// </summary>
    /// <param name="blockerBottom">The bottom edge of the blocker, which sets the lane at bottom + 12.</param>
    /// <param name="expectStraight">Whether a single straight segment is expected.</param>
    [Theory]
    [InlineData(28.0, true)]
    [InlineData(30.0, true)]
    [InlineData(32.5, false)]
    [InlineData(36.0, false)]
    public void Route_SlideMargin_PortKeepsDecorationInsideFace(double blockerBottom, bool expectStraight)
    {
        // Arrange: faces span 0..50; the lane (bottom + 12) is 40 or 42 (8 or more from the corner) or
        // 44.5 / 48 (5.5 / 2 from it, where the arrowhead would overhang the corner).
        var from = Box(0, 0, 100, 50);
        var to = Box(400, 0, 100, 50);
        var blocker = Box(200, -40, 100, blockerBottom + 40);

        // Act
        var line = ConnectorRouter.Route([from, to, blocker], new Connection(from, to), new ConnectorRouteOptions());

        // Assert
        var w = line.Waypoints;
        AssertPortClearOfCorners(from, w[0]);
        AssertPortClearOfCorners(to, w[^1]);
        if (expectStraight)
        {
            Assert.Equal(2, w.Count);
            Assert.Equal(blockerBottom + 12.0, w[0].Y, 6);
        }
        else
        {
            Assert.True(w.Count > 2, "A port closer to the corner than the marker reach is refused, so the jog stays.");
        }
    }

    /// <summary>
    ///     The distance, in logical pixels, an arrowhead (with its outline stroke) plus the box border's
    ///     half stroke reaches to either side of a connector's end, computed here from the theme and
    ///     notation metrics independently of the router's own margin constant.
    /// </summary>
    private static double MarkerReach =>
        (Themes.Light.StrokeWidth * (NotationMetrics.EndMarkerHalfWidth + (Themes.Light.StrokeWidth / 2.0))) + (Themes.Light.StrokeWidth / 2.0);

    /// <summary>
    ///     Asserts that <paramref name="port"/>, lying on a face of <paramref name="box"/>, is at least
    ///     <see cref="MarkerReach"/> from both ends of that face, so the arrowhead's wings stay inside it.
    /// </summary>
    /// <param name="box">The box owning the port.</param>
    /// <param name="port">The port (a waypoint on the box outline).</param>
    private static void AssertPortClearOfCorners(LayoutBox box, Point2D port)
    {
        const double tolerance = 1e-6;
        var distance = DistanceToFaceEnds(box, port);
        Assert.True(distance >= MarkerReach - tolerance, $"Port {port} is {distance} from the face end, nearer than the marker reach {MarkerReach}.");
    }

    /// <summary>
    ///     Three connectors sharing one source face are each straightened onto their own target's row when
    ///     that is possible, yet their ports stay distinct, in the original order, and at least the batch
    ///     separation apart.
    /// </summary>
    [Fact]
    public void Route_SharedFace_SlideKeepsDistinctOrderedPorts()
    {
        // Arrange: one tall source face feeding three stacked targets whose rows differ from the evenly
        // spread default ports (12, 100, 188).
        var from = Box(0, 0, 100, 200);
        var t1 = Box(400, 0, 100, 40);
        var t2 = Box(400, 80, 100, 40);
        var t3 = Box(400, 160, 100, 40);
        var connections = new[] { new Connection(from, t1), new Connection(from, t2), new Connection(from, t3) };

        // Act
        var lines = ConnectorRouter.Route([from, t1, t2, t3], connections, new ConnectorRouteOptions());

        // Assert: distinct, ordered, well separated source ports; every connector straight.
        var ys = lines.Select(l => l.Waypoints[0].Y).ToList();
        Assert.True(ys[0] + 16.0 <= ys[1] && ys[1] + 16.0 <= ys[2], $"Ports {string.Join(",", ys)} are not ordered and separated.");
        Assert.All(lines, l => Assert.Equal(2, l.Waypoints.Count));
    }

    /// <summary>
    ///     A port is never slid where it would crowd or hop over a neighbor on the same face: the first
    ///     connector's lane would sit within the neighbor's separation, so it keeps its original port.
    /// </summary>
    [Fact]
    public void Route_SharedFace_SlideRefusedWhenItWouldCrowdNeighbor()
    {
        // Arrange: two connectors on one source face (default ports 12 and 88); the first target's row
        // (80..90) lies beyond the first port's slide window, right beside the second connector's port.
        var from = Box(0, 0, 100, 100);
        var t1 = Box(400, 75, 100, 20);
        var t2 = Box(400, 150, 100, 20);
        var connections = new[] { new Connection(from, t1), new Connection(from, t2) };

        // Act
        var lines = ConnectorRouter.Route([from, t1, t2], connections, new ConnectorRouteOptions());

        // Assert: the first port stays on the original anchor and the two ports stay at least 16 apart.
        Assert.Equal(12.0, lines[0].Waypoints[0].Y, 6);
        Assert.True(Math.Abs(lines[0].Waypoints[0].Y - lines[1].Waypoints[0].Y) >= 16.0);
    }

    /// <summary>
    ///     The single-connection overload and the batch overload share the port-sliding stage, so they
    ///     produce identical waypoints for a slid route.
    /// </summary>
    [Fact]
    public void Route_SingleOverload_MatchesBatchForSlidRoute()
    {
        // Arrange
        var from = Box(0, 0, 100, 50);
        var to = Box(400, 0, 100, 50);
        var blocker = Box(200, -40, 100, 60);
        var boxes = new[] { from, to, blocker };
        var connection = new Connection(from, to, EndMarkerStyle.FilledArrow);
        var options = new ConnectorRouteOptions();

        // Act
        var single = ConnectorRouter.Route(boxes, connection, options);
        var batch = ConnectorRouter.Route(boxes, [connection], options);

        // Assert
        Assert.Equal(single.Waypoints, batch[0].Waypoints);
        Assert.Equal(2, single.Waypoints.Count);
    }

    /// <summary>
    ///     A rounded box's slid port stays inside the face's connectable extent (between the corner arcs):
    ///     a lane that falls in the arc zone is not used.
    /// </summary>
    [Fact]
    public void Route_RoundedFaces_SlideStaysWithinConnectableExtent()
    {
        // Arrange: rounded faces (radius 10) are connectable over 10..40; the free lane is at 44.5.
        var from = Box(0, 0, 100, 50, shape: BoxShape.RoundedRectangle, roundedCornerRadius: 10.0);
        var to = Box(400, 0, 100, 50, shape: BoxShape.RoundedRectangle, roundedCornerRadius: 10.0);
        var blocker = Box(200, -40, 100, 72.5);

        // Act
        var line = ConnectorRouter.Route([from, to, blocker], new Connection(from, to), new ConnectorRouteOptions());

        // Assert: both ends lie within 10..40 of their faces, never in the corner arcs.
        Assert.InRange(line.Waypoints[0].Y, 10.0, 40.0);
        Assert.InRange(line.Waypoints[^1].Y, 10.0, 40.0);
    }

    /// <summary>
    ///     Under the cross-hierarchy profile, connectors keep a comfortable first and final straight leg
    ///     (at least twice the clearance) instead of bending the moment they clear the stub.
    /// </summary>
    [Fact]
    public void RouteBatch_ProfileOn_FirstAndLastLegAtLeastComfort()
    {
        // Arrange: a wide gap and no vertical overlap, so a jog is required.
        var from = Box(0, 0, 60, 40);
        var to = Box(400, 100, 60, 40);
        var connection = new Connection(from, to, EndMarkerStyle.FilledArrow);
        var profile = new ConnectorRouter.RoutingProfile(_ => []);

        // Act
        var lines = ConnectorRouter.RouteBatch([connection], new ConnectorRouteOptions(), _ => [from, to], profile: profile);

        // Assert
        var w = lines[0].Waypoints;
        Assert.True(LegLength(w, 0) >= 24.0, $"First leg {LegLength(w, 0)} is too short.");
        Assert.True(LegLength(w, w.Count - 2) >= 24.0, $"Final leg {LegLength(w, w.Count - 2)} is too short.");
    }

    /// <summary>
    ///     Container walls supplied to the batch router are crossed at right angles but never run along or
    ///     bent against within the keep-out margin (twice the clearance).
    /// </summary>
    [Fact]
    public void RouteBatch_WallsSupplied_NoRunWithinWallMargin_AndCrossesPerpendicularly()
    {
        // Arrange: a jog between the two walls x = 100 and x = 260; lanes must keep 24 px from each.
        var from = Box(0, 100, 60, 40);
        var to = Box(400, 0, 60, 40);
        var connection = new Connection(from, to, EndMarkerStyle.FilledArrow);
        IReadOnlyList<RouterWall> walls =
        [
            new RouterWall(IsVertical: true, 100.0, 0.0, 200.0, OutwardIsPositive: true),
            new RouterWall(IsVertical: true, 260.0, 0.0, 200.0, OutwardIsPositive: false),
        ];
        var profile = new ConnectorRouter.RoutingProfile(_ => walls);

        // Act
        var lines = ConnectorRouter.RouteBatch([connection], new ConnectorRouteOptions(), _ => [from, to], profile: profile);

        // Assert: every vertical run is at least 24 px from both walls.
        var w = lines[0].Waypoints;
        for (var i = 0; i < w.Count - 1; i++)
        {
            if (Math.Abs(w[i].X - w[i + 1].X) < 1e-6)
            {
                Assert.True(Math.Abs(w[i].X - 100.0) >= 24.0 - 1e-6, $"Run at x={w[i].X} hugs the wall at 100.");
                Assert.True(Math.Abs(w[i].X - 260.0) >= 24.0 - 1e-6, $"Run at x={w[i].X} hugs the wall at 260.");
            }
        }
    }

    /// <summary>
    ///     A wall supplied for an unrelated container refuses a long straight lane at the bare obstacle
    ///     clearance beneath it: where a lane at least twice the clearance below the border still lies
    ///     inside both faces' usable spans the connector stays straight on that lane, and where it does not
    ///     the connector takes a clean Z (interior segments and the run beside the wall at least twice the
    ///     clearance), with both ports clear of the corners.
    /// </summary>
    /// <param name="faceHeight">The height of the source and sink boxes (faces centered on y = 150.5).</param>
    /// <param name="expectStraight">Whether the lane at the wall's keep-out edge lies inside both faces.</param>
    [Theory]
    [InlineData(90.0, true)]
    [InlineData(50.0, false)]
    public void RouteBatch_UnrelatedWall_StraightLaneInsideKeepOutIsRefused(double faceHeight, bool expectStraight)
    {
        // Arrange: an unrelated container (x 300..500, bottom at y = 158) between the two boxes; its
        // border keep-out reaches y = 182. The faces span 150.5 +/- faceHeight / 2.
        var from = Box(72, 150.5 - (faceHeight / 2.0), 100, faceHeight);
        var to = Box(560, 150.5 - (faceHeight / 2.0), 100, faceHeight);
        var container = Box(300, 0, 200, 158);
        IReadOnlyList<RouterWall> walls =
        [
            new RouterWall(IsVertical: true, 300.0, 0.0, 158.0, OutwardIsPositive: false),
            new RouterWall(IsVertical: true, 500.0, 0.0, 158.0, OutwardIsPositive: true),
            new RouterWall(IsVertical: false, 158.0, 300.0, 500.0, OutwardIsPositive: true),
            new RouterWall(IsVertical: false, 0.0, 300.0, 500.0, OutwardIsPositive: false),
        ];
        var profile = new ConnectorRouter.RoutingProfile(_ => walls);

        // Act
        var lines = ConnectorRouter.RouteBatch([new Connection(from, to, EndMarkerStyle.FilledArrow)], new ConnectorRouteOptions(), _ => [from, to, container], profile: profile);

        // Assert: no run beside the container is nearer than twice the clearance to its bottom border;
        // ports clear of the corners; no tiny interior segment; straight only when the lane fits the faces.
        var w = lines[0].Waypoints;
        AssertAllSegmentsOrthogonal(w);
        AssertPortClearOfCorners(from, w[0]);
        AssertPortClearOfCorners(to, w[^1]);
        AssertAllInteriorSegmentsAtLeast(w, 24.0);
        for (var i = 0; i < w.Count - 1; i++)
        {
            var isRunBesideContainer = Math.Abs(w[i].Y - w[i + 1].Y) < 1e-6 && Math.Max(w[i].X, w[i + 1].X) > 300.0 && Math.Min(w[i].X, w[i + 1].X) < 500.0;
            if (isRunBesideContainer)
            {
                Assert.True(Math.Abs(w[i].Y - 158.0) >= 24.0 - 1e-6, $"A run at y={w[i].Y} hugs the container border at 158.");
            }
        }

        Assert.Equal(expectStraight, w.Count == 2);
        if (!expectStraight)
        {
            Assert.True(w.Count <= 6, "A clean Z has at most four bends.");
        }
    }

    /// <summary>
    ///     Under the profile no interior segment is shorter than twice the clearance and no run doubles
    ///     back, and every port keeps the marker's reach from the face ends, however narrowly two facing
    ///     boxes overlap: the sweep slides the lower box across the upper one in 1 px steps.
    /// </summary>
    [Fact]
    public void RouteBatch_Profile_NarrowOverlapSweep_NoTinyJogsAndPortsClearOfCorners()
    {
        // Arrange
        var profile = new ConnectorRouter.RoutingProfile(_ => []);
        var failures = new List<string>();

        for (var offset = -110; offset <= 119; offset++)
        {
            var from = Box(200, 0, 120, 50);
            var to = Box(200 + offset, 200, 120, 50);

            // Act
            var lines = ConnectorRouter.RouteBatch([new Connection(from, to, EndMarkerStyle.FilledArrow)], new ConnectorRouteOptions(), _ => [from, to], profile: profile);

            // Assert (collected so one run reports every offending offset)
            var w = lines[0].Waypoints;
            for (var i = 1; i <= w.Count - 3; i++)
            {
                if (LegLength(w, i) < 24.0 - 1e-6)
                {
                    failures.Add($"offset {offset}: interior segment {i} is {LegLength(w, i)} long");
                }
            }

            for (var i = 0; i < w.Count - 2; i++)
            {
                var firstAlongX = Math.Abs(w[i].Y - w[i + 1].Y) < 1e-6;
                var secondAlongX = Math.Abs(w[i + 1].Y - w[i + 2].Y) < 1e-6;
                var dot = ((w[i + 1].X - w[i].X) * (w[i + 2].X - w[i + 1].X)) + ((w[i + 1].Y - w[i].Y) * (w[i + 2].Y - w[i + 1].Y));
                if (firstAlongX == secondAlongX && dot < 0.0)
                {
                    failures.Add($"offset {offset}: the route doubles back at waypoint {i + 1}");
                }
            }

            if (DistanceToFaceEnds(from, w[0]) < MarkerReach - 1e-6 || DistanceToFaceEnds(to, w[^1]) < MarkerReach - 1e-6)
            {
                failures.Add($"offset {offset}: a port is nearer a corner than the marker reach");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>Asserts that every segment between two bends (neither the first nor the last) is at least <paramref name="minimum"/> long.</summary>
    /// <param name="w">The waypoints.</param>
    /// <param name="minimum">The shortest acceptable interior segment.</param>
    private static void AssertAllInteriorSegmentsAtLeast(IReadOnlyList<Point2D> w, double minimum)
    {
        for (var i = 1; i <= w.Count - 3; i++)
        {
            Assert.True(LegLength(w, i) >= minimum - 1e-6, $"Interior segment {i} is {LegLength(w, i)} long, shorter than {minimum}.");
        }
    }

    /// <summary>Returns how far <paramref name="port"/> lies from the nearer end of the face of <paramref name="box"/> it sits on.</summary>
    /// <param name="box">The box owning the port.</param>
    /// <param name="port">The port (a waypoint on the box outline).</param>
    private static double DistanceToFaceEnds(LayoutBox box, Point2D port)
    {
        const double tolerance = 1e-6;
        var onVerticalFace = Math.Abs(port.X - box.X) < tolerance || Math.Abs(port.X - (box.X + box.Width)) < tolerance;
        var lo = onVerticalFace ? box.Y : box.X;
        var hi = onVerticalFace ? box.Y + box.Height : box.X + box.Width;
        var along = onVerticalFace ? port.Y : port.X;
        return Math.Min(along - lo, hi - along);
    }

    /// <summary>
    ///     A connector whose natural first approach lies right under a container title band chooses the
    ///     adjacent face and reaches its target with a single bend instead of the four-bend side approach.
    /// </summary>
    [Fact]
    public void RouteBatch_ShieldedTopFace_ChoosesAdjacentFaceWithOneBend()
    {
        // Arrange: the target's top is shielded by a title band and tab (obstacles) right above it.
        var from = Box(52, 340, 120, 50);
        var to = Box(159, 572, 120, 50);
        var title = Box(164, 492, 110, 24);
        var tab = Box(127, 492, 83, 24);
        var connection = new Connection(from, to, EndMarkerStyle.FilledArrow);
        var profile = new ConnectorRouter.RoutingProfile(_ => []);

        // Act
        var lines = ConnectorRouter.RouteBatch([connection], new ConnectorRouteOptions(), _ => [from, to, title, tab], profile: profile);

        // Assert: one bend, arriving on the target's left face.
        var w = lines[0].Waypoints;
        Assert.True(w.Count <= 3, $"Expected one bend, got {w.Count - 2}.");
        Assert.Equal(to.X, w[^1].X, 6);
    }

    /// <summary>
    ///     A later connector never crosses within the end zone of an earlier connector's arrowhead: any
    ///     crossing of the two lines lies at least 24 px from the arrowhead tip.
    /// </summary>
    [Fact]
    public void RouteBatch_PeerEndZone_LaterConnectorNeverCrossesWithinZoneOfEarlierArrowhead()
    {
        // Arrange: connector 0 runs straight into B's left face; connector 1 must pass between the two
        // columns to reach D below and to the right.
        var a = Box(0, 40, 60, 40);
        var b = Box(300, 40, 60, 40);
        var c = Box(0, 0, 60, 20);
        var d = Box(400, 140, 60, 40);
        var connections = new[]
        {
            new Connection(a, b, EndMarkerStyle.FilledArrow),
            new Connection(c, d, EndMarkerStyle.FilledArrow),
        };
        var profile = new ConnectorRouter.RoutingProfile(_ => []);

        // Act
        var lines = ConnectorRouter.RouteBatch(connections, new ConnectorRouteOptions(), _ => [a, b, c, d], profile: profile);

        // Assert: crossings, if any, keep 24 px from connector 0's arrowhead tip.
        var tip = lines[0].Waypoints[^1];
        var other = lines[1].Waypoints;
        for (var i = 0; i < other.Count - 1; i++)
        {
            for (var j = 0; j < lines[0].Waypoints.Count - 1; j++)
            {
                if (SegmentsCrossTransversally(other[i], other[i + 1], lines[0].Waypoints[j], lines[0].Waypoints[j + 1]))
                {
                    var crossing = Math.Abs(other[i].X - other[i + 1].X) < 1e-6
                        ? new Point2D(other[i].X, lines[0].Waypoints[j].Y)
                        : new Point2D(lines[0].Waypoints[j].X, other[i].Y);
                    Assert.True(
                        Math.Abs(crossing.X - tip.X) + Math.Abs(crossing.Y - tip.Y) >= 24.0,
                        $"Crossing at ({crossing.X},{crossing.Y}) is within the arrowhead zone.");
                }
            }
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
