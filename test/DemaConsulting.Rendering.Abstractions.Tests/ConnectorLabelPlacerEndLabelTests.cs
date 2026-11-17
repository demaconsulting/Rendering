// <copyright file="ConnectorLabelPlacerEndLabelTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Abstractions.Tests;

/// <summary>
///     Tests for <see cref="ConnectorLabelPlacer.PlaceEndLabels"/>: connection end labels (role name,
///     multiplicity) sit near their own end of a connector, on the correct side, clear of the box at that
///     end, of the end marker, and of every other placed label.
/// </summary>
public sealed class ConnectorLabelPlacerEndLabelTests
{
    private const double FontSize = 12.0;

    private static readonly double Scale = NotationMetrics.MarkerScale(Themes.Light);

    private static LayoutLine Horizontal(
        double x0,
        double x1,
        double y,
        LineEndLabel? source,
        LineEndLabel? target,
        EndMarkerStyle targetEnd = EndMarkerStyle.None) =>
        new([new Point2D(x0, y), new Point2D(x1, y)], EndMarkerStyle.None, targetEnd, LineStyle.Solid, null, source, target);

    private static LayoutLine Vertical(double x, double y0, double y1, LineEndLabel? source, LineEndLabel? target) =>
        new([new Point2D(x, y0), new Point2D(x, y1)], EndMarkerStyle.None, EndMarkerStyle.None, LineStyle.Solid, null, source, target);

    private static EndLabelPlacement Find(
        IReadOnlyList<EndLabelPlacement> placements,
        LineEnd end,
        EndLabelSlot slot) =>
        Assert.Single(placements, p => p.End == end && p.Slot == slot);

    private static bool Overlaps(EndLabelPlacement a, EndLabelPlacement b) =>
        Math.Abs(a.X - b.X) < a.HalfWidth + b.HalfWidth && Math.Abs(a.Y - b.Y) < a.HalfHeight + b.HalfHeight;

    /// <summary>Gap, in logical pixels, the placer's label box keeps around its text (<c>ConnectorLabelPlacer.Gap</c>).</summary>
    private const double BoxGap = 2.0;

    /// <summary>Minimum clearance, in logical pixels, an end label's text keeps from anything drawn near it.</summary>
    private const double MinClearance = 4.0;

    private static readonly PortAppearance OutlinedIn = new(PortDirection.In, LabelPlacement: PortLabelPlacement.Outward);

    /// <summary>The text rectangle (label box less the placer's built-in gap) as left, top, right, bottom.</summary>
    private static (double L, double T, double R, double B) TextRect(EndLabelPlacement p) =>
        (p.X - p.HalfWidth + BoxGap, p.Y - p.HalfHeight + BoxGap, p.X + p.HalfWidth - BoxGap, p.Y + p.HalfHeight - BoxGap);

    /// <summary>The clear distance between two rectangles (negative when they overlap).</summary>
    private static double RectGap((double L, double T, double R, double B) a, (double L, double T, double R, double B) b) =>
        Math.Max(Math.Max(b.L - a.R, a.L - b.R), Math.Max(b.T - a.B, a.T - b.B));

    /// <summary>
    ///     A target marker end on a port centre is drawn retracted by the glyph half size, so the label
    ///     keeps at least 4 px from the retracted marker's painted extent, not from the port centre.
    /// </summary>
    [Fact]
    public void PlaceEndLabels_VerticalTargetEndOnPort_KeepsFourPixelsFromRetractedMarker()
    {
        // Arrange: a downward line whose target end sits on an outlined port centre at (100, 360).
        var port = new LayoutPort(100, 360, PortSide.Top, null, Appearance: OutlinedIn);
        var line = new LayoutLine(
            [new Point2D(100, 40), new Point2D(100, 360)],
            EndMarkerStyle.None,
            EndMarkerStyle.OpenChevron,
            LineStyle.Solid,
            null,
            null,
            new LineEndLabel("sink", "1..*"));

        // Act
        var result = ConnectorLabelPlacer.PlaceEndLabels([line], FontSize, [], Scale, null, [port]);

        // Assert: the marker is painted from the retracted tip (360 - glyph half size) back along the line.
        var retraction = NotationMetrics.PortGlyphHalfSizeFor(OutlinedIn);
        var tip = 360 - retraction;
        var across = (NotationMetrics.AcrossHalfWidth(EndMarkerStyle.OpenChevron) * Scale) + (NotationMetrics.MarkerStrokeWidth(Themes.Light) / 2.0);
        var marker = (L: 100 - across, T: tip - (NotationMetrics.AlongLineLength(EndMarkerStyle.OpenChevron) * Scale), R: 100 + across, B: tip);
        Assert.Equal(2, result.Count);
        foreach (var placement in result)
        {
            var gap = RectGap(TextRect(placement), marker);
            Assert.True(gap >= MinClearance - 1e-6, $"'{placement.Text}' is only {gap:0.##} px from the retracted marker");
        }
    }

    /// <summary>
    ///     On a vertical run both slots keep at least 4 px between their text and the connector they sit
    ///     beside.
    /// </summary>
    [Fact]
    public void PlaceEndLabels_VerticalRun_SecondaryKeepsFourPixelsFromConnector()
    {
        // Arrange
        var line = Vertical(100, 40, 360, new LineEndLabel("owner", "1"), null);

        // Act
        var result = ConnectorLabelPlacer.PlaceEndLabels([line], FontSize, [], Scale);

        // Assert
        var primary = TextRect(Find(result, LineEnd.Source, EndLabelSlot.Primary));
        var secondary = TextRect(Find(result, LineEnd.Source, EndLabelSlot.Secondary));
        Assert.True(primary.L - 100 >= MinClearance - 1e-6, $"primary text is {primary.L - 100:0.##} px from the connector");
        Assert.True(100 - secondary.R >= MinClearance - 1e-6, $"secondary text is {100 - secondary.R:0.##} px from the connector");
    }

    /// <summary>
    ///     An occupied rectangle that touches the nominal slot is treated as 4 px of clearance wide: the
    ///     placed label's text keeps at least 4 px from it.
    /// </summary>
    [Fact]
    public void PlaceEndLabels_OccupiedRectangle_KeepsFourPixelsOfClearance()
    {
        // Arrange: find the nominal slot, then occupy the rectangle that touches its right edge.
        var line = Horizontal(40, 360, 100, new LineEndLabel("owner", null), null);
        var nominal = Assert.Single(ConnectorLabelPlacer.PlaceEndLabels([line], FontSize, [], Scale));
        var occupied = new LabelPlacement(nominal.X + nominal.HalfWidth + 10, nominal.Y, 10, nominal.HalfHeight);

        // Act
        var placed = Assert.Single(ConnectorLabelPlacer.PlaceEndLabels([line], FontSize, [], Scale, [occupied]));

        // Assert
        var rect = (L: occupied.X - occupied.HalfWidth, T: occupied.Y - occupied.HalfHeight, R: occupied.X + occupied.HalfWidth, B: occupied.Y + occupied.HalfHeight);
        var gap = RectGap(TextRect(placed), rect);
        Assert.True(gap >= MinClearance - 1e-6, $"the label text is only {gap:0.##} px from the occupied rectangle");
    }

    /// <summary>
    ///     An end label stacked beside a port glyph and a port label keeps 4 px from both.
    /// </summary>
    [Fact]
    public void PlaceEndLabels_StackedEndAndPortLabel_KeepFourPixelGap()
    {
        // Arrange: a downward line ending on a port with an outward label beside it.
        var port = new LayoutPort(100, 360, PortSide.Top, "in : Fuel", Appearance: OutlinedIn);
        var line = new LayoutLine(
            [new Point2D(100, 40), new Point2D(100, 360)],
            EndMarkerStyle.None,
            EndMarkerStyle.OpenChevron,
            LineStyle.Solid,
            null,
            null,
            new LineEndLabel("feeds", "0..* {ordered}"));
        var label = NotationMetrics.PortOutwardLabelBounds(port, "in : Fuel", Themes.Light);
        var glyph = NotationMetrics.PortGlyphBounds(port);

        // Act
        var result = ConnectorLabelPlacer.PlaceEndLabels([line], FontSize, [], Scale, [label, glyph], [port]);

        // Assert
        Assert.Equal(2, result.Count);
        foreach (var placement in result)
        {
            foreach (var other in new[] { label, glyph })
            {
                var rect = (L: other.X - other.HalfWidth, T: other.Y - other.HalfHeight, R: other.X + other.HalfWidth, B: other.Y + other.HalfHeight);
                var gap = RectGap(TextRect(placement), rect);
                Assert.True(gap >= MinClearance - 1e-6, $"'{placement.Text}' is only {gap:0.##} px from a port rectangle");
            }
        }
    }

    /// <summary>
    ///     Without ports the placement is the nominal one: just beyond the marker along the run and
    ///     <c>half height + 2</c> px beside the line.
    /// </summary>
    [Fact]
    public void PlaceEndLabels_NoPorts_PositionsUnchanged()
    {
        // Arrange
        var line = Horizontal(40, 360, 100, new LineEndLabel("owner", "1"), null);

        // Act
        var result = ConnectorLabelPlacer.PlaceEndLabels([line], FontSize, [], Scale, null, null);

        // Assert: the label box starts 4 px beyond the (marker-less) end point; 2 px line clearance.
        var primary = Find(result, LineEnd.Source, EndLabelSlot.Primary);
        Assert.Equal(40 + 4 + primary.HalfWidth, primary.X, 6);
        Assert.Equal(100 - primary.HalfHeight - 2.0, primary.Y, 6);
    }

    /// <summary>With no end labels the result is empty.</summary>
    [Fact]
    public void PlaceEndLabels_NoEndLabels_ReturnsEmpty()
    {
        // Arrange
        var line = Horizontal(0, 200, 50, null, null);

        // Act
        var result = ConnectorLabelPlacer.PlaceEndLabels([line], FontSize, [], Scale);

        // Assert
        Assert.Empty(result);
    }

    /// <summary>
    ///     On a horizontal end run the primary label (role name) sits above the line and the secondary
    ///     (multiplicity) below it, each near its own end.
    /// </summary>
    [Fact]
    public void PlaceEndLabels_HorizontalLine_PrimaryAboveSecondaryBelowNearEachEnd()
    {
        // Arrange
        var line = Horizontal(40, 360, 100, new LineEndLabel("owner", "1"), new LineEndLabel("parts", "0..*"));

        // Act
        var result = ConnectorLabelPlacer.PlaceEndLabels([line], FontSize, [], Scale);

        // Assert
        Assert.Equal(4, result.Count);
        var sourcePrimary = Find(result, LineEnd.Source, EndLabelSlot.Primary);
        var sourceSecondary = Find(result, LineEnd.Source, EndLabelSlot.Secondary);
        var targetPrimary = Find(result, LineEnd.Target, EndLabelSlot.Primary);
        var targetSecondary = Find(result, LineEnd.Target, EndLabelSlot.Secondary);
        Assert.True(sourcePrimary.Y < 100 && targetPrimary.Y < 100, "primary labels sit above the line");
        Assert.True(sourceSecondary.Y > 100 && targetSecondary.Y > 100, "secondary labels sit below the line");
        Assert.True(sourcePrimary.X < 200 && sourceSecondary.X < 200, "source labels sit in the first half");
        Assert.True(targetPrimary.X > 200 && targetSecondary.X > 200, "target labels sit in the second half");
        Assert.Equal("owner", sourcePrimary.Text);
        Assert.Equal("0..*", targetSecondary.Text);
    }

    /// <summary>
    ///     On a vertical end run the primary label sits to the right of the line and the secondary to the
    ///     left.
    /// </summary>
    [Fact]
    public void PlaceEndLabels_VerticalLine_PrimaryRightSecondaryLeft()
    {
        // Arrange
        var line = Vertical(100, 40, 360, new LineEndLabel("owner", "1"), null);

        // Act
        var result = ConnectorLabelPlacer.PlaceEndLabels([line], FontSize, [], Scale);

        // Assert
        var primary = Find(result, LineEnd.Source, EndLabelSlot.Primary);
        var secondary = Find(result, LineEnd.Source, EndLabelSlot.Secondary);
        Assert.True(primary.X - primary.HalfWidth >= 100, "primary label lies entirely right of the line");
        Assert.True(secondary.X + secondary.HalfWidth <= 100, "secondary label lies entirely left of the line");
        Assert.True(primary.Y < 200 && secondary.Y < 200, "labels sit near the source end");
    }

    /// <summary>A reversed (right-to-left) line keeps the primary label above the line.</summary>
    [Fact]
    public void PlaceEndLabels_RightToLeftLine_PrimaryStillAbove()
    {
        // Arrange: the line runs from x = 360 to x = 40.
        var line = Horizontal(360, 40, 100, new LineEndLabel("owner", "1"), new LineEndLabel("parts", "0..*"));

        // Act
        var result = ConnectorLabelPlacer.PlaceEndLabels([line], FontSize, [], Scale);

        // Assert
        Assert.True(Find(result, LineEnd.Source, EndLabelSlot.Primary).Y < 100);
        Assert.True(Find(result, LineEnd.Target, EndLabelSlot.Primary).Y < 100);
        Assert.True(Find(result, LineEnd.Source, EndLabelSlot.Primary).X > 200, "the source end is on the right");
        Assert.True(Find(result, LineEnd.Target, EndLabelSlot.Primary).X < 200, "the target end is on the left");
    }

    /// <summary>A label never overlaps the leaf box the line starts on.</summary>
    [Fact]
    public void PlaceEndLabels_LabelsStayClearOfTheBoxAtTheEnd()
    {
        // Arrange: a box ending exactly where the line starts; a label above the line near the start
        // would sit on the box's right edge if it were centered on the end point.
        var box = new LayoutBox(0, 60, 100, 80, "A", 0, BoxShape.Rectangle, [], []);
        var line = Horizontal(100, 360, 100, new LineEndLabel("owner", "1"), null);

        // Act
        var result = ConnectorLabelPlacer.PlaceEndLabels([line], FontSize, [box], Scale);

        // Assert
        foreach (var placement in result)
        {
            Assert.True(placement.X - placement.HalfWidth >= 100 - 1e-6, $"{placement.Text} overlaps the box");
        }
    }

    /// <summary>A label stays clear of the target end marker's footprint.</summary>
    [Fact]
    public void PlaceEndLabels_LabelsStayClearOfTheEndMarker()
    {
        // Arrange
        var line = Horizontal(40, 360, 100, null, new LineEndLabel("parts", "0..*"), EndMarkerStyle.FilledDiamond);

        // Act
        var result = ConnectorLabelPlacer.PlaceEndLabels([line], FontSize, [], Scale);

        // Assert: the marker occupies the last AlongLineLength * scale pixels of the line.
        var markerStart = 360 - (NotationMetrics.AlongLineLength(EndMarkerStyle.FilledDiamond) * Scale);
        foreach (var placement in result)
        {
            Assert.True(
                placement.X + placement.HalfWidth <= markerStart + 1e-6,
                $"{placement.Text} runs into the end marker");
        }
    }

    /// <summary>End labels never overlap each other, the midpoint placements, or other occupied rectangles.</summary>
    [Fact]
    public void PlaceEndLabels_LabelsDoNotOverlapEachOtherOrOccupiedRectangles()
    {
        // Arrange: two stacked parallel lines with labels at both ends plus a midpoint label.
        var upper = Horizontal(40, 360, 100, new LineEndLabel("a", "1"), new LineEndLabel("b", "0..*"));
        var lower = Horizontal(40, 360, 124, new LineEndLabel("c", "1"), new LineEndLabel("d", "0..*"));
        var occupied = new LabelPlacement(200, 100, 40, 9);

        // Act
        var result = ConnectorLabelPlacer.PlaceEndLabels([upper, lower], FontSize, [], Scale, [occupied]);

        // Assert
        Assert.Equal(8, result.Count);
        for (var i = 0; i < result.Count; i++)
        {
            for (var j = i + 1; j < result.Count; j++)
            {
                Assert.False(Overlaps(result[i], result[j]), $"{result[i].Text} overlaps {result[j].Text}");
            }

            Assert.False(
                Math.Abs(result[i].X - occupied.X) < result[i].HalfWidth + occupied.HalfWidth &&
                Math.Abs(result[i].Y - occupied.Y) < result[i].HalfHeight + occupied.HalfHeight,
                $"{result[i].Text} overlaps the occupied rectangle");
        }
    }

    /// <summary>The same input always yields the same placements.</summary>
    [Fact]
    public void PlaceEndLabels_IsDeterministic()
    {
        // Arrange
        var line = Horizontal(40, 360, 100, new LineEndLabel("owner", "1"), new LineEndLabel("parts", "0..*"));

        // Act
        var first = ConnectorLabelPlacer.PlaceEndLabels([line], FontSize, [], Scale);
        var second = ConnectorLabelPlacer.PlaceEndLabels([line], FontSize, [], Scale);

        // Assert
        Assert.Equal(first, second);
    }

    /// <summary>
    ///     A label wider than the end run is still placed (never dropped or thrown), at the nominal position,
    ///     when nothing clears.
    /// </summary>
    [Fact]
    public void PlaceEndLabels_LabelWiderThanTheRun_StillPlacedAtTheNominalPosition()
    {
        // Arrange: a 30 px run and a 60 px wide label.
        var line = Horizontal(0, 30, 100, new LineEndLabel("a very long role name", null), null);

        // Act
        var result = ConnectorLabelPlacer.PlaceEndLabels([line], FontSize, [], Scale);

        // Assert
        var placement = Assert.Single(result);
        Assert.True(placement.Y < 100);
        Assert.True(placement.X > 0);
    }

    /// <summary>A blank label slot yields no placement.</summary>
    [Fact]
    public void PlaceEndLabels_NullOrEmptySlots_AreSkipped()
    {
        // Arrange
        var line = Horizontal(40, 360, 100, new LineEndLabel(null, string.Empty), new LineEndLabel("only", null));

        // Act
        var result = ConnectorLabelPlacer.PlaceEndLabels([line], FontSize, [], Scale);

        // Assert
        var placement = Assert.Single(result);
        Assert.Equal("only", placement.Text);
    }

    /// <summary>Null arguments are rejected.</summary>
    [Fact]
    public void PlaceEndLabels_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => ConnectorLabelPlacer.PlaceEndLabels(null!, FontSize, [], Scale));
        Assert.Throws<ArgumentNullException>(() => ConnectorLabelPlacer.PlaceEndLabels([], FontSize, null!, Scale));
    }

    /// <summary>
    ///     End labels do not change the midpoint placements: a line's midpoint placement is identical with
    ///     and without end labels.
    /// </summary>
    [Fact]
    public void Place_MidpointPlacements_UnchangedByEndLabels()
    {
        // Arrange
        var plain = Horizontal(40, 360, 100, null, null) with { MidpointLabel = "mid" };
        var labeled = plain with { SourceEndLabel = new LineEndLabel("a", "1"), TargetEndLabel = new LineEndLabel("b") };

        // Act
        var without = ConnectorLabelPlacer.Place([plain], FontSize, [], Scale);
        var with = ConnectorLabelPlacer.Place([labeled], FontSize, [], Scale);

        // Assert
        Assert.Equal(without[plain], with[labeled]);
    }

    /// <summary>
    ///     The occupied rectangles of a port are its glyph and, for an outward label, the label rectangle
    ///     padded along the text, so an end label keeps a gap from both.
    /// </summary>
    [Fact]
    public void PortOccupiedRects_OutwardLabeledPort_ReturnsGlyphAndPaddedLabel()
    {
        // Arrange
        var port = new LayoutPort(100, 50, PortSide.Right, "state : Mode", Appearance: OutlinedIn);
        var plain = new LayoutPort(200, 50, PortSide.Left, null);

        // Act
        var rects = ConnectorLabelPlacer.PortOccupiedRects([port, plain], Themes.Light);

        // Assert: glyph + padded label for the outward port, glyph only for the plain one.
        Assert.Equal(3, rects.Count);
        Assert.Equal(NotationMetrics.PortGlyphBounds(port), rects[0]);
        var label = NotationMetrics.PortOutwardLabelBounds(port, "state : Mode", Themes.Light);
        Assert.Equal(label.HalfWidth + 5.0, rects[1].HalfWidth, 6);
        Assert.Equal(label.X, rects[1].X, 6);
        Assert.Equal(NotationMetrics.PortGlyphBounds(plain), rects[2]);
    }

    /// <summary>
    ///     <see cref="ConnectorLabelPlacer.ResolveLabels"/> returns exactly the placements the renderers
    ///     compute by hand: midpoint labels first, then end labels that yield to the midpoints and to every
    ///     port rectangle.
    /// </summary>
    [Fact]
    public void ResolveLabels_PortsAndLabels_MatchesManualComposition()
    {
        // Arrange: a labeled connector ending on an outward-labeled port.
        var port = new LayoutPort(360, 100, PortSide.Right, "in : Fuel", Appearance: OutlinedIn);
        var line = Horizontal(40, 360, 100, new LineEndLabel("src", "1"), new LineEndLabel("dst", "0..*"), EndMarkerStyle.OpenChevron)
            with
        { MidpointLabel = "mid" };
        var box = new LayoutBox(0, 80, 40, 40, "A", 0, BoxShape.Rectangle, [], []);

        // Act
        var resolved = ConnectorLabelPlacer.ResolveLabels([line], [box], [port], Themes.Light);

        // Assert
        var midpoints = ConnectorLabelPlacer.Place([line], FontSize, [box], Scale);
        var rects = ConnectorLabelPlacer.PortOccupiedRects([port], Themes.Light);
        var ends = ConnectorLabelPlacer.PlaceEndLabels([line], FontSize, [box], Scale, midpoints.Values.Concat(rects), [port]);
        Assert.Equal(midpoints[line], resolved.Midpoints[line]);
        Assert.Equal(ends, resolved.EndLabels);
        Assert.Equal(rects, resolved.PortRects);
    }

    /// <summary>Content entirely inside the canvas needs no translation.</summary>
    [Fact]
    public void MeasureLeadingOverhang_ContentInside_ReturnsZero()
    {
        // Arrange
        var line = Horizontal(60, 360, 100, null, null, EndMarkerStyle.OpenChevron) with { MidpointLabel = "mid" };
        var box = new LayoutBox(40, 80, 20, 40, "A", 0, BoxShape.Rectangle, [], []);
        var layout = ConnectorLabelPlacer.ResolveLabels([line], [box], [], Themes.Light);

        // Act
        var overhang = ConnectorLabelPlacer.MeasureLeadingOverhang([box], [line], [], layout, Themes.Light);

        // Assert
        Assert.Equal((0.0, 0.0), overhang);
    }

    /// <summary>
    ///     A label, an outward port label, and an end marker past the left and top edges produce the overhang
    ///     of the furthest one, measured with the supplied theme (a larger font overhangs further).
    /// </summary>
    [Fact]
    public void MeasureLeadingOverhang_LabelsPastOrigin_ReturnsOverhangForTheTheme()
    {
        // Arrange: a vertical line at x = 8 with a midpoint label centered on it, and an outward-labeled port.
        var line = Vertical(8, 30, 250, null, null) with { MidpointLabel = "keyword label" };
        var port = new LayoutPort(20, 60, PortSide.Left, "port : Type", Appearance: OutlinedIn);
        var large = Themes.Light with { FontSizeBody = 24.0, LabelPadding = 12.0 };

        // Act
        var small = ConnectorLabelPlacer.MeasureLeadingOverhang([], [line], [port], ConnectorLabelPlacer.ResolveLabels([line], [], [port], Themes.Light), Themes.Light);
        var big = ConnectorLabelPlacer.MeasureLeadingOverhang([], [line], [port], ConnectorLabelPlacer.ResolveLabels([line], [], [port], large), large);

        // Assert
        Assert.True(small.X > 0.0);
        Assert.True(big.X > small.X);
    }

    /// <summary>Null arguments to the overhang measurement are rejected.</summary>
    [Fact]
    public void MeasureLeadingOverhang_NullArguments_Throw()
    {
        var layout = ConnectorLabelPlacer.ResolveLabels([], [], [], Themes.Light);
        Assert.Throws<ArgumentNullException>(() => ConnectorLabelPlacer.MeasureLeadingOverhang(null!, [], [], layout, Themes.Light));
        Assert.Throws<ArgumentNullException>(() => ConnectorLabelPlacer.MeasureLeadingOverhang([], null!, [], layout, Themes.Light));
        Assert.Throws<ArgumentNullException>(() => ConnectorLabelPlacer.MeasureLeadingOverhang([], [], null!, layout, Themes.Light));
        Assert.Throws<ArgumentNullException>(() => ConnectorLabelPlacer.MeasureLeadingOverhang([], [], [], layout, null!));
    }

    /// <summary>Null arguments to the shared resolver and the port rectangles are rejected.</summary>
    [Fact]
    public void ResolveLabels_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => ConnectorLabelPlacer.ResolveLabels(null!, [], [], Themes.Light));
        Assert.Throws<ArgumentNullException>(() => ConnectorLabelPlacer.ResolveLabels([], null!, [], Themes.Light));
        Assert.Throws<ArgumentNullException>(() => ConnectorLabelPlacer.ResolveLabels([], [], null!, Themes.Light));
        Assert.Throws<ArgumentNullException>(() => ConnectorLabelPlacer.ResolveLabels([], [], [], null!));
        Assert.Throws<ArgumentNullException>(() => ConnectorLabelPlacer.PortOccupiedRects(null!, Themes.Light));
        Assert.Throws<ArgumentNullException>(() => ConnectorLabelPlacer.PortOccupiedRects([], null!));
    }
}
