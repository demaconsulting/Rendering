// <copyright file="PortGlyphMetricsTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Abstractions.Tests;

/// <summary>
///     Tests for the shared port glyph geometry in <see cref="NotationMetrics"/>: glyph sizes, direction
///     arrow orientation on every side, and the outward label anchor and bounds. Both renderers draw
///     through these helpers, so they cannot drift apart.
/// </summary>
public sealed class PortGlyphMetricsTests
{
    private static readonly PortSide[] Sides = [PortSide.Left, PortSide.Right, PortSide.Top, PortSide.Bottom];

    /// <summary>The legacy glyph keeps the legacy half size, so every existing marker shortening is unchanged.</summary>
    [Fact]
    public void NotationMetrics_PortGlyphHalfSizeFor_NullAndFilled_AreTheLegacyHalfSize()
    {
        Assert.Equal(NotationMetrics.PortHalfSize, NotationMetrics.PortGlyphHalfSizeFor(null));
        Assert.Equal(NotationMetrics.PortHalfSize, NotationMetrics.PortGlyphHalfSizeFor(new PortAppearance()));
        Assert.Equal(NotationMetrics.PortHalfSize, NotationMetrics.PortGlyphHalfSizeFor(new PortAppearance(Glyph: PortGlyph.Filled)));
    }

    /// <summary>The outlined glyph is 12 px, the proxy circle has a 5 px radius, and Auto resolves through the appearance.</summary>
    [Fact]
    public void NotationMetrics_PortGlyphHalfSizeFor_OutlinedAndProxy_UseTheirOwnSizes()
    {
        Assert.Equal(6.0, NotationMetrics.PortGlyphHalfSize);
        Assert.Equal(5.0, NotationMetrics.PortProxyRadius);
        Assert.Equal(6.0, NotationMetrics.PortGlyphHalfSizeFor(new PortAppearance(PortDirection.In)));
        Assert.Equal(6.0, NotationMetrics.PortGlyphHalfSizeFor(new PortAppearance(Glyph: PortGlyph.Outlined)));
        Assert.Equal(6.0, NotationMetrics.PortGlyphHalfSizeFor(new PortAppearance(OutlineStyle: LineStyle.Dashed)));
        Assert.Equal(5.0, NotationMetrics.PortGlyphHalfSizeFor(new PortAppearance(Glyph: PortGlyph.Proxy)));
    }

    /// <summary>No direction yields no arrow segments.</summary>
    [Fact]
    public void NotationMetrics_PortArrowSegments_None_IsEmpty()
    {
        foreach (var side in Sides)
        {
            Assert.Empty(NotationMetrics.PortArrowSegments(PortDirection.None, side));
        }
    }

    /// <summary>
    ///     An <c>in</c> arrow points into the owning part and an <c>out</c> arrow points away from it, on every
    ///     side: the arrow tip lies on the interior (resp. exterior) side of the glyph centre.
    /// </summary>
    /// <param name="side">The port side.</param>
    /// <param name="intoX">Expected sign of the into-the-part direction on X.</param>
    /// <param name="intoY">Expected sign of the into-the-part direction on Y.</param>
    [Theory]
    [InlineData(PortSide.Left, 1, 0)]
    [InlineData(PortSide.Right, -1, 0)]
    [InlineData(PortSide.Top, 0, 1)]
    [InlineData(PortSide.Bottom, 0, -1)]
    public void NotationMetrics_PortArrowSegments_InAndOut_PointIntoAndAwayFromThePart(PortSide side, int intoX, int intoY)
    {
        // Act
        var inTip = TipExtent(NotationMetrics.PortArrowSegments(PortDirection.In, side), intoX, intoY);
        var outTip = TipExtent(NotationMetrics.PortArrowSegments(PortDirection.Out, side), intoX, intoY);

        // Assert: the farthest extent along the into-direction is positive for in, negative for out.
        Assert.True(inTip.Max > 0 && inTip.Min > -NotationMetrics.PortGlyphHalfSize, "in arrow reaches into the part");
        Assert.True(outTip.Min < 0 && outTip.Max < NotationMetrics.PortGlyphHalfSize, "out arrow reaches out of the part");
        Assert.Equal(-inTip.Min, outTip.Max, 9);
        Assert.Equal(-inTip.Max, outTip.Min, 9);
    }

    /// <summary>An <c>inout</c> arrow is symmetric about the glyph centre along the arrow axis.</summary>
    [Fact]
    public void NotationMetrics_PortArrowSegments_InOut_IsSymmetricAndDoubleHeaded()
    {
        foreach (var side in Sides)
        {
            // Arrange
            var horizontal = side is PortSide.Left or PortSide.Right;

            // Act
            var segments = NotationMetrics.PortArrowSegments(PortDirection.InOut, side);
            var extent = TipExtent(segments, horizontal ? 1 : 0, horizontal ? 0 : 1);

            // Assert: shaft plus a head at each end, symmetric about the centre.
            Assert.Equal(3, segments.Count);
            Assert.Equal(-extent.Min, extent.Max, 9);
            Assert.Equal(
                0.0,
                segments.Sum(segment => segment.Sum(point => horizontal ? point.Y : point.X)),
                9);
        }
    }

    /// <summary>Every arrow segment stays inside the outlined glyph square.</summary>
    [Fact]
    public void NotationMetrics_PortArrowSegments_StayInsideTheGlyph()
    {
        foreach (var direction in new[] { PortDirection.In, PortDirection.Out, PortDirection.InOut })
        {
            foreach (var side in Sides)
            {
                foreach (var point in NotationMetrics.PortArrowSegments(direction, side).SelectMany(segment => segment))
                {
                    Assert.True(Math.Abs(point.X) < NotationMetrics.PortGlyphHalfSize, $"{direction}/{side}: {point}");
                    Assert.True(Math.Abs(point.Y) < NotationMetrics.PortGlyphHalfSize, $"{direction}/{side}: {point}");
                }
            }
        }
    }

    /// <summary>
    ///     An outward label lies fully outside the owning box on every side and never covers the connector
    ///     axis through the port.
    /// </summary>
    /// <param name="side">The port side.</param>
    [Theory]
    [InlineData(PortSide.Left)]
    [InlineData(PortSide.Right)]
    [InlineData(PortSide.Top)]
    [InlineData(PortSide.Bottom)]
    public void NotationMetrics_PortOutwardLabelBounds_LieOutsideTheBoxAndClearOfTheConnector(PortSide side)
    {
        // Arrange: a 100 x 60 box at (100, 100) with a port at the middle of the chosen face.
        var box = new Rect(100, 100, 100, 60);
        var (cx, cy) = side switch
        {
            PortSide.Left => (box.X, box.Y + 30),
            PortSide.Right => (box.X + box.Width, box.Y + 30),
            PortSide.Top => (box.X + 50, box.Y),
            _ => (box.X + 50, box.Y + box.Height),
        };
        var port = new LayoutPort(
            cx,
            cy,
            side,
            "speed : Real",
            Appearance: new PortAppearance(PortDirection.In, LabelPlacement: PortLabelPlacement.Outward));

        // Act
        var bounds = NotationMetrics.PortOutwardLabelBounds(port, "speed : Real", Themes.Light);

        // Assert: no overlap with the box interior ...
        var overlapsBox = bounds.X - bounds.HalfWidth < box.X + box.Width && bounds.X + bounds.HalfWidth > box.X &&
                          bounds.Y - bounds.HalfHeight < box.Y + box.Height && bounds.Y + bounds.HalfHeight > box.Y;
        Assert.False(overlapsBox, "the label must be outside the owning box");

        // ... and the connector axis through the port (a ray leaving the face) is not covered.
        var crossesAxis = side is PortSide.Left or PortSide.Right
            ? bounds.Y - bounds.HalfHeight < cy && bounds.Y + bounds.HalfHeight > cy
            : bounds.X - bounds.HalfWidth < cx && bounds.X + bounds.HalfWidth > cx;
        Assert.False(crossesAxis, "the label must not sit on the connector");

        // The label starts beyond the glyph.
        var glyph = NotationMetrics.PortGlyphHalfSizeFor(port.Appearance);
        var gap = side switch
        {
            PortSide.Left => box.X - (bounds.X + bounds.HalfWidth),
            PortSide.Right => bounds.X - bounds.HalfWidth - (box.X + box.Width),
            PortSide.Top => box.Y - (bounds.Y + bounds.HalfHeight),
            _ => bounds.Y - bounds.HalfHeight - (box.Y + box.Height),
        };
        Assert.True(gap >= glyph - 1e-6, $"gap {gap} must clear the glyph half size {glyph}");
    }

    /// <summary>The outward label anchor and bounds agree: the anchor lies inside the bounds.</summary>
    [Fact]
    public void NotationMetrics_PortOutwardLabelAnchor_LiesInsideTheBounds()
    {
        foreach (var side in Sides)
        {
            // Arrange
            var port = new LayoutPort(
                200,
                150,
                side,
                "p",
                Appearance: new PortAppearance(LabelPlacement: PortLabelPlacement.Outward));

            // Act
            var anchor = NotationMetrics.PortOutwardLabelAnchor(port, Themes.Light);
            var bounds = NotationMetrics.PortOutwardLabelBounds(port, "p", Themes.Light);

            // Assert
            Assert.InRange(anchor.X, bounds.X - bounds.HalfWidth - 1e-6, bounds.X + bounds.HalfWidth + 1e-6);
            Assert.InRange(anchor.Y, bounds.Y - bounds.HalfHeight - 1e-6, bounds.Y + bounds.HalfHeight + 1e-6);
        }
    }

    /// <summary>A label is outward only when asked for and present.</summary>
    [Fact]
    public void NotationMetrics_IsOutwardLabel_RequiresOutwardPlacementAndALabel()
    {
        var outward = new PortAppearance(LabelPlacement: PortLabelPlacement.Outward);
        Assert.True(NotationMetrics.IsOutwardLabel(new LayoutPort(0, 0, PortSide.Left, "p", Appearance: outward)));
        Assert.False(NotationMetrics.IsOutwardLabel(new LayoutPort(0, 0, PortSide.Left, null, Appearance: outward)));
        Assert.False(NotationMetrics.IsOutwardLabel(new LayoutPort(0, 0, PortSide.Left, "p")));
        Assert.False(NotationMetrics.IsOutwardLabel(new LayoutPort(0, 0, PortSide.Left, "p", Appearance: new PortAppearance())));
    }

    /// <summary>The glyph bounds are the square (or circle bounding box) around the port centre.</summary>
    [Fact]
    public void NotationMetrics_PortGlyphBounds_AreCenteredOnThePortWithTheGlyphHalfSize()
    {
        // Arrange
        var legacy = new LayoutPort(30, 40, PortSide.Left, null);
        var outlined = legacy with { Appearance = new PortAppearance(PortDirection.In) };

        // Act
        var legacyBounds = NotationMetrics.PortGlyphBounds(legacy);
        var outlinedBounds = NotationMetrics.PortGlyphBounds(outlined);

        // Assert
        Assert.Equal(new LabelPlacement(30, 40, NotationMetrics.PortHalfSize, NotationMetrics.PortHalfSize), legacyBounds);
        Assert.Equal(new LabelPlacement(30, 40, 6, 6), outlinedBounds);
    }

    /// <summary>
    ///     A marker end on a port centre retracts by the glyph's own half size (legacy square, outlined,
    ///     proxy), and by nothing when the end is off a port, has no marker, the segment is too short, or
    ///     there are no ports.
    /// </summary>
    [Fact]
    public void NotationMetrics_PortEndRetraction_OnPortWithMarker_IsTheGlyphHalfSizeElseZero()
    {
        // Arrange
        var legacy = new LayoutPort(100, 100, PortSide.Top, null);
        var outlined = new LayoutPort(200, 100, PortSide.Top, null, Appearance: new PortAppearance(PortDirection.In));
        var proxy = new LayoutPort(300, 100, PortSide.Top, null, Appearance: new PortAppearance(Glyph: PortGlyph.Proxy));
        LayoutPort[] ports = [legacy, outlined, proxy];

        // Act / Assert
        Assert.Equal(NotationMetrics.PortHalfSize, NotationMetrics.PortEndRetraction(new Point2D(100, 100), new Point2D(100, 40), EndMarkerStyle.OpenChevron, ports));
        Assert.Equal(NotationMetrics.PortGlyphHalfSize, NotationMetrics.PortEndRetraction(new Point2D(200.3, 99.8), new Point2D(200, 40), EndMarkerStyle.FilledArrow, ports));
        Assert.Equal(NotationMetrics.PortProxyRadius, NotationMetrics.PortEndRetraction(new Point2D(300, 100), new Point2D(300, 40), EndMarkerStyle.FilledDiamond, ports));
        Assert.Equal(0.0, NotationMetrics.PortEndRetraction(new Point2D(150, 100), new Point2D(150, 40), EndMarkerStyle.OpenChevron, ports));
        Assert.Equal(0.0, NotationMetrics.PortEndRetraction(new Point2D(200, 100), new Point2D(200, 40), EndMarkerStyle.None, ports));
        Assert.Equal(0.0, NotationMetrics.PortEndRetraction(new Point2D(200, 100), new Point2D(200, 95), EndMarkerStyle.OpenChevron, ports));
        Assert.Equal(0.0, NotationMetrics.PortEndRetraction(new Point2D(200, 100), new Point2D(200, 40), EndMarkerStyle.OpenChevron, null));
    }

    private static (double Min, double Max) TipExtent(IReadOnlyList<IReadOnlyList<Point2D>> segments, int axisX, int axisY)
    {
        var values = segments.SelectMany(segment => segment).Select(point => (point.X * axisX) + (point.Y * axisY)).ToList();
        return (values.Min(), values.Max());
    }
}
