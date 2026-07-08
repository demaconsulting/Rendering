// <copyright file="ConnectorLabelPlacerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Abstractions.Tests;

/// <summary>
///     Tests for <see cref="ConnectorLabelPlacer"/>.
/// </summary>
public sealed class ConnectorLabelPlacerTests
{
    /// <summary>A line without a label is omitted from the result.</summary>
    [Fact]
    public void Place_LineWithoutLabel_IsOmitted()
    {
        // Arrange
        var line = new LayoutLine(
            [new Point2D(0, 0), new Point2D(100, 0)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            MidpointLabel: null);

        // Act
        var result = ConnectorLabelPlacer.Place([line], fontSize: 12);

        // Assert
        Assert.Empty(result);
    }

    /// <summary>A single labelled line is placed at the midpoint of its longest segment.</summary>
    [Fact]
    public void Place_SingleLine_UsesLongestSegmentMidpoint()
    {
        // Arrange: a short vertical stub then a long horizontal run; the label should land on the run.
        var line = new LayoutLine(
            [new Point2D(0, 0), new Point2D(0, 10), new Point2D(200, 10)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            MidpointLabel: "[guard]");

        // Act
        var result = ConnectorLabelPlacer.Place([line], fontSize: 12);

        // Assert
        var (x, y) = result[line];
        Assert.Equal(100, x, precision: 3);
        Assert.Equal(10, y, precision: 3);
    }

    /// <summary>Two labels whose preferred positions coincide are separated so they do not overlap.</summary>
    [Fact]
    public void Place_CollidingLabels_AreSeparated()
    {
        // Arrange: two lines whose longest-segment midpoints are the same point.
        var a = new LayoutLine(
            [new Point2D(0, 0), new Point2D(200, 0)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            MidpointLabel: "[atFloor]");
        var b = new LayoutLine(
            [new Point2D(0, 0), new Point2D(200, 0)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            MidpointLabel: "[timeout]");

        // Act
        var result = ConnectorLabelPlacer.Place([a, b], fontSize: 12);

        // Assert: the first keeps the preferred midpoint; the second is nudged away vertically.
        var posA = result[a];
        var posB = result[b];
        Assert.Equal(100, posA.X, precision: 3);
        Assert.Equal(0, posA.Y, precision: 3);
        Assert.NotEqual(posB.Y, posA.Y, precision: 3);
    }

    /// <summary>
    ///     A placed label's <see cref="LabelPlacement.HalfWidth"/>/<see cref="LabelPlacement.HalfHeight"/>
    ///     are both positive, so a renderer can compute the label's full bounding-box extent (not just
    ///     its centre) to grow a canvas/bitmap around it.
    /// </summary>
    [Fact]
    public void Place_SingleLine_ExposesPositiveHalfWidthAndHalfHeight()
    {
        var line = new LayoutLine(
            [new Point2D(0, 0), new Point2D(200, 0)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            MidpointLabel: "[guard]");

        var result = ConnectorLabelPlacer.Place([line], fontSize: 12);

        var placement = result[line];
        Assert.True(placement.HalfWidth > 0);
        Assert.True(placement.HalfHeight > 0);
    }

    /// <summary>
    ///     A longer label produces a larger <see cref="LabelPlacement.HalfWidth"/>, confirming the
    ///     exposed size actually reflects the label's estimated text width rather than a fixed
    ///     placeholder.
    /// </summary>
    [Fact]
    public void Place_LongerLabel_HasLargerHalfWidth()
    {
        var shortLine = new LayoutLine(
            [new Point2D(0, 0), new Point2D(200, 0)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            MidpointLabel: "a");
        var longLine = new LayoutLine(
            [new Point2D(0, 100), new Point2D(200, 100)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            MidpointLabel: "a much longer label string");

        var result = ConnectorLabelPlacer.Place([shortLine, longLine], fontSize: 12);

        Assert.True(result[longLine].HalfWidth > result[shortLine].HalfWidth);
    }

    /// <summary>
    ///     Proves that <see cref="ConnectorLabelPlacer.EstimateLabelHeight"/> returns the full (not
    ///     half) label bounding-box height matching the formula <see cref="ConnectorLabelPlacer.Place"/>
    ///     uses internally
    ///     for <c>halfHeight</c> (<c>fontSize * 1.3 + 2 * Gap</c>, doubled), so other layout stages can
    ///     size themselves against the exact same value this placer uses when testing for overlap.
    /// </summary>
    [Fact]
    public void EstimateLabelHeight_MatchesPlaceHalfHeightDoubled()
    {
        const double fontSize = 12.0;
        var line = new LayoutLine(
            [new Point2D(0, 0), new Point2D(200, 0)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            MidpointLabel: "label");

        var result = ConnectorLabelPlacer.Place([line], fontSize);
        var expectedHeight = result[line].HalfHeight * 2.0;

        Assert.Equal(expectedHeight, ConnectorLabelPlacer.EstimateLabelHeight(fontSize), 6);
    }

    /// <summary>
    ///     Proves that <see cref="ConnectorLabelPlacer.EstimateLabelHeight"/> grows monotonically with
    ///     font size.
    /// </summary>
    [Fact]
    public void EstimateLabelHeight_IsMonotonicInFontSize()
    {
        var small = ConnectorLabelPlacer.EstimateLabelHeight(10.0);
        var large = ConnectorLabelPlacer.EstimateLabelHeight(20.0);

        Assert.True(large > small);
    }
}
