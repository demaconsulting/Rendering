// <copyright file="ConnectorLabelPlacerObstacleTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Abstractions.Tests;

/// <summary>
///     Tests for the box-aware <see cref="ConnectorLabelPlacer.Place(IEnumerable{LayoutLine}, double, IEnumerable{LayoutBox})"/>
///     overload: leaf boxes reject any overlap, container boxes reject only a straddled border, sliding
///     along a segment clears a blocker, and the obstacle-free placement is the fallback.
/// </summary>
public sealed class ConnectorLabelPlacerObstacleTests
{
    private const double FontSize = 12.0;

    private static LayoutLine Horizontal(string label, double length = 200.0, double y = 0.0) =>
        new(
            [new Point2D(0, y), new Point2D(length, y)],
            EndMarkerStyle.None,
            EndMarkerStyle.None,
            LineStyle.Solid,
            label);

    private static LayoutBox Leaf(double x, double y, double width, double height) =>
        new(x, y, width, height, null, 0, BoxShape.Rectangle, [], []);

    private static LayoutBox Container(double x, double y, double width, double height) =>
        new(x, y, width, height, null, 0, BoxShape.Rectangle, [], [Leaf(x + 2, y + 2, 4, 4)]);

    private static (double Left, double Top, double Right, double Bottom) Rect(LabelPlacement placement) =>
        (placement.X - placement.HalfWidth, placement.Y - placement.HalfHeight, placement.X + placement.HalfWidth, placement.Y + placement.HalfHeight);

    private static bool Overlaps((double Left, double Top, double Right, double Bottom) a, LayoutBox box) =>
        a.Left < box.X + box.Width && a.Right > box.X && a.Top < box.Y + box.Height && a.Bottom > box.Y;

    private static bool Inside((double Left, double Top, double Right, double Bottom) a, LayoutBox box) =>
        a.Left >= box.X && a.Right <= box.X + box.Width && a.Top >= box.Y && a.Bottom <= box.Y + box.Height;

    /// <summary>
    ///     With no obstacle boxes the box-aware overload places every label exactly where the two-argument
    ///     overload does, including colliding labels.
    /// </summary>
    [Fact]
    public void PlaceWithObstacles_NoObstacles_MatchesLegacyOverload()
    {
        var a = Horizontal("[atFloor]");
        var b = Horizontal("[timeout]");
        var c = Horizontal("[idle]", 80, 30);

        var legacy = ConnectorLabelPlacer.Place([a, b, c], FontSize);
        var boxAware = ConnectorLabelPlacer.Place([a, b, c], FontSize, []);

        Assert.Equal(legacy.Count, boxAware.Count);
        foreach (var line in new[] { a, b, c })
        {
            Assert.Equal(legacy[line], boxAware[line]);
        }
    }

    /// <summary>
    ///     A label whose midpoint lands on a leaf box is slid along its segment until it no longer
    ///     overlaps the box, so its opaque background never paints over the box border.
    /// </summary>
    [Fact]
    public void PlaceWithObstacles_LeafBoxOnMidpoint_SlidesLabelClear()
    {
        var line = Horizontal("[guard]");
        var box = Leaf(90, -20, 20, 40);

        var placement = ConnectorLabelPlacer.Place([line], FontSize, [box])[line];

        Assert.False(Overlaps(Rect(placement), box));
        Assert.Equal(0.0, placement.Y, 6);
        Assert.NotEqual(100.0, placement.X);
    }

    /// <summary>
    ///     A label that lies entirely inside a container box is allowed to stay there.
    /// </summary>
    [Fact]
    public void PlaceWithObstacles_LabelFullyInsideContainer_IsAllowed()
    {
        var line = Horizontal("[guard]");
        var container = Container(0, -50, 200, 100);

        var placement = ConnectorLabelPlacer.Place([line], FontSize, [container])[line];

        Assert.Equal(100.0, placement.X, 6);
        Assert.Equal(0.0, placement.Y, 6);
    }

    /// <summary>
    ///     A label that would straddle a container's border is moved until it lies fully inside or fully
    ///     outside the container.
    /// </summary>
    [Fact]
    public void PlaceWithObstacles_LabelStraddlingContainerBorder_IsRejected()
    {
        var line = Horizontal("[guard]");
        var container = Container(100, -50, 100, 100);

        var placement = ConnectorLabelPlacer.Place([line], FontSize, [container])[line];
        var rect = Rect(placement);

        Assert.True(Inside(rect, container) || !Overlaps(rect, container));
    }

    /// <summary>
    ///     When no candidate can clear an obstacle, the obstacle-free placement is used, so a label is
    ///     still placed and never lost.
    /// </summary>
    [Fact]
    public void PlaceWithObstacles_EverythingBlocked_FallsBackToObstacleFreePlacement()
    {
        var line = Horizontal("[guard]");
        var blanket = Leaf(-500, -500, 1200, 1000);

        var legacy = ConnectorLabelPlacer.Place([line], FontSize)[line];
        var placement = ConnectorLabelPlacer.Place([line], FontSize, [blanket])[line];

        Assert.Equal(legacy, placement);
    }

    /// <summary>
    ///     A slide along a decorated line never moves the label into the footprint of the end decoration,
    ///     so the label cannot hide the arrowhead.
    /// </summary>
    [Fact]
    public void PlaceWithObstacles_SlideAvoidsEndMarkerZone()
    {
        var line = new LayoutLine(
            [new Point2D(50, 0), new Point2D(50, 60)],
            EndMarkerStyle.None,
            EndMarkerStyle.OpenChevron,
            LineStyle.Solid,
            "ab");
        var blocker = Leaf(30, 25, 40, 10);

        var placement = ConnectorLabelPlacer.Place([line], FontSize, [blocker])[line];
        var rect = Rect(placement);

        var markerZoneTop = 60.0 - NotationMetrics.RenderedAlongLineLength(EndMarkerStyle.OpenChevron, Themes.Light);
        var overlapsMarkerZone = rect.Right > 44.0 && rect.Left < 56.0 && rect.Bottom > markerZoneTop && rect.Top < 60.0;
        Assert.False(Overlaps(rect, blocker));
        Assert.False(overlapsMarkerZone);
    }

    /// <summary>
    ///     The end-decoration clearance is derived from the supplied marker scale (the theme stroke width),
    ///     not a hard-coded constant: a thicker-stroke theme reserves a proportionally longer zone.
    /// </summary>
    [Fact]
    public void PlaceWithObstacles_SlideAvoidsEndMarkerZone_UsesSuppliedMarkerScale()
    {
        var line = new LayoutLine(
            [new Point2D(50, 0), new Point2D(50, 120)],
            EndMarkerStyle.None,
            EndMarkerStyle.OpenChevron,
            LineStyle.Solid,
            "ab");

        // The blocker leaves clear space only below y = 76: inside a 3x marker zone (starting at 90)
        // but beyond the zone a 1.5x scale would assume (starting at 105 for a label that fits).
        var blocker = Leaf(30, 0, 40, 76);
        var theme = Themes.Light with { StrokeWidth = 3.0 };

        var placement = ConnectorLabelPlacer.Place([line], FontSize, [blocker], NotationMetrics.MarkerScale(theme))[line];
        var rect = Rect(placement);

        var markerZoneTop = 120.0 - NotationMetrics.RenderedAlongLineLength(EndMarkerStyle.OpenChevron, theme);
        Assert.False(rect.Bottom > markerZoneTop && rect.Top < 120.0, $"Label {rect} overlaps the 3x marker zone starting at {markerZoneTop}.");
    }

    /// <summary>
    ///     The end decoration's footprint blocks the label's candidate positions, not only its slides: a
    ///     label on a short run whose midpoint would sit on the arrowhead is moved clear of it, at either
    ///     end of the line, even when there are no obstacle boxes.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Place_LabelOnShortRun_StaysClearOfEndMarkerFootprint(bool atTarget)
    {
        var points = atTarget
            ? new[] { new Point2D(0, 0), new Point2D(80, 0) }
            : new[] { new Point2D(80, 0), new Point2D(0, 0) };
        var line = new LayoutLine(
            points,
            atTarget ? EndMarkerStyle.None : EndMarkerStyle.FilledArrow,
            atTarget ? EndMarkerStyle.FilledArrow : EndMarkerStyle.None,
            LineStyle.Solid,
            "primary");

        var placement = ConnectorLabelPlacer.Place([line], FontSize, [])[line];
        var rect = Rect(placement);

        // The marker sits at x = 80 in both cases (the source case reverses the line) and occupies its
        // rendered length back along the line, so the label must not reach into it.
        var markerLength = NotationMetrics.RenderedAlongLineLength(EndMarkerStyle.FilledArrow, Themes.Light);
        Assert.True(rect.Right <= 80.0 - markerLength + 1e-9, $"Label {rect} overlaps the marker footprint.");
        Assert.True(rect.Left >= -1e-9 || rect.Right <= 80.0, $"Label {rect} left the line.");
    }

    /// <summary>
    ///     A label whose midpoint already clears the end decoration keeps its midpoint, so decorated lines
    ///     with ample room are placed exactly as before.
    /// </summary>
    [Fact]
    public void Place_LabelClearOfEndMarker_KeepsMidpoint()
    {
        var line = new LayoutLine(
            [new Point2D(0, 0), new Point2D(300, 0)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            "primary");

        var placement = ConnectorLabelPlacer.Place([line], FontSize, [])[line];

        Assert.Equal(150.0, placement.X, 1e-9);
        Assert.Equal(0.0, placement.Y, 1e-9);
    }

    /// <summary>
    ///     A label that cannot clear the end decoration on a run too short for both stays on its own line
    ///     (it is not nudged off to another row) and keeps its midpoint.
    /// </summary>
    [Fact]
    public void Place_RunTooShortForLabelAndMarker_KeepsLabelOnItsLine()
    {
        var line = new LayoutLine(
            [new Point2D(0, 0), new Point2D(40, 0)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            "primary");

        var placement = ConnectorLabelPlacer.Place([line], FontSize, [])[line];

        Assert.Equal(0.0, placement.Y, 1e-9);
        Assert.Equal(20.0, placement.X, 1e-9);
    }

    /// <summary>
    ///     A null obstacle sequence or line sequence is rejected.
    /// </summary>
    [Fact]
    public void PlaceWithObstacles_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => ConnectorLabelPlacer.Place([Horizontal("a")], FontSize, null!));
        Assert.Throws<ArgumentNullException>(() => ConnectorLabelPlacer.Place(null!, FontSize, []));
    }

    /// <summary>
    ///     Placement with obstacles is deterministic: the same input gives the same result twice.
    /// </summary>
    [Fact]
    public void PlaceWithObstacles_SameInput_IsDeterministic()
    {
        var line = Horizontal("[guard]");
        var box = Leaf(90, -20, 20, 40);

        var first = ConnectorLabelPlacer.Place([line], FontSize, [box])[line];
        var second = ConnectorLabelPlacer.Place([line], FontSize, [box])[line];

        Assert.Equal(first, second);
    }
}
