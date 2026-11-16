// <copyright file="NotationPrimitiveMetricsTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Abstractions.Tests;

/// <summary>
///     Tests for the notation-primitive additions to <see cref="NotationMetrics"/>: the dotted hollow
///     triangles, the circled plus, the along-line and across-line extents, the thick-bar and
///     crossed-circle badge constants, and the send/accept corner-icon polygons.
/// </summary>
public sealed class NotationPrimitiveMetricsTests
{
    private const double Tolerance = 1e-9;

    /// <summary>
    ///     The dotted-triangle marker lengths are the triangle length plus the dot extension, and the tip
    ///     anchor shifts by the same extension so the triangle lands where a plain hollow triangle does.
    /// </summary>
    [Fact]
    public void DottedTriangles_HaveCanonicalValues()
    {
        Assert.Equal(NotationMetrics.DotBaseGap + (2.0 * NotationMetrics.DotRadius), NotationMetrics.TwoDotsExtension, Tolerance);
        Assert.Equal(NotationMetrics.TwoDotsExtension + NotationMetrics.DotColumnSpacing, NotationMetrics.FourDotsExtension, Tolerance);
        Assert.Equal(NotationMetrics.EndMarkerLength + NotationMetrics.TwoDotsExtension, NotationMetrics.TwoDotsMarkerLength, Tolerance);
        Assert.Equal(NotationMetrics.EndMarkerLength + NotationMetrics.FourDotsExtension, NotationMetrics.FourDotsMarkerLength, Tolerance);
        Assert.Equal(NotationMetrics.EndMarkerRefX + NotationMetrics.TwoDotsExtension, NotationMetrics.TwoDotsRefX, Tolerance);
        Assert.Equal(NotationMetrics.EndMarkerRefX + NotationMetrics.FourDotsExtension, NotationMetrics.FourDotsRefX, Tolerance);
    }

    /// <summary>
    ///     The two-dots marker has one dot on each side of the line, behind the triangle base and clear of
    ///     both the base and the line stroke.
    /// </summary>
    [Fact]
    public void DotCenters_TwoDots_StraddleLineBehindBase()
    {
        var dots = NotationMetrics.DotCenters(EndMarkerStyle.HollowTriangleTwoDots);

        Assert.Equal(2, dots.Count);
        Assert.Equal(dots[0].Along, dots[1].Along, Tolerance);
        Assert.Equal(-dots[0].Across, dots[1].Across, Tolerance);
        Assert.All(dots, dot => Assert.True(dot.Along - NotationMetrics.DotRadius > NotationMetrics.EndMarkerRefX));
        Assert.All(dots, dot => Assert.True(Math.Abs(dot.Across) - NotationMetrics.DotRadius > 0.75));
    }

    /// <summary>
    ///     The four-dots marker is a 2x2 grid: two columns, each with a dot on either side of the line,
    ///     the second column further behind the base.
    /// </summary>
    [Fact]
    public void DotCenters_FourDots_FormTwoByTwoGrid()
    {
        var dots = NotationMetrics.DotCenters(EndMarkerStyle.HollowTriangleFourDots);

        Assert.Equal(4, dots.Count);
        var columns = dots.Select(dot => Math.Round(dot.Along, 6)).Distinct().OrderBy(along => along).ToList();
        Assert.Equal(2, columns.Count);
        Assert.Equal(NotationMetrics.DotColumnSpacing, columns[1] - columns[0], 1e-6);
        Assert.Equal(2, dots.Count(dot => dot.Across < 0));
        Assert.Equal(2, dots.Count(dot => dot.Across > 0));
    }

    /// <summary>
    ///     Styles without dots yield no dot centers.
    /// </summary>
    [Fact]
    public void DotCenters_NonDottedStyles_AreEmpty()
    {
        Assert.Empty(NotationMetrics.DotCenters(EndMarkerStyle.HollowTriangle));
        Assert.Empty(NotationMetrics.DotCenters(EndMarkerStyle.CircledPlus));
        Assert.Empty(NotationMetrics.DotCenters(EndMarkerStyle.None));
    }

    /// <summary>
    ///     The dotted variants keep the triangle anchored exactly as the plain hollow triangle does; only
    ///     the box around it widens.
    /// </summary>
    [Fact]
    public void TriangleFamilyBox_DottedVariants_KeepTriangleTipAnchored()
    {
        var plain = NotationMetrics.TriangleFamilyBox(EndMarkerStyle.HollowTriangle);
        var two = NotationMetrics.TriangleFamilyBox(EndMarkerStyle.HollowTriangleTwoDots);
        var four = NotationMetrics.TriangleFamilyBox(EndMarkerStyle.HollowTriangleFourDots);

        Assert.Equal((NotationMetrics.EndMarkerLength, NotationMetrics.EndMarkerRefX), plain);
        Assert.Equal(NotationMetrics.TwoDotsMarkerLength - NotationMetrics.TwoDotsRefX, plain.Length - plain.RefX, Tolerance);
        Assert.Equal(NotationMetrics.FourDotsMarkerLength - NotationMetrics.FourDotsRefX, plain.Length - plain.RefX, Tolerance);
        Assert.Equal((NotationMetrics.TwoDotsMarkerLength, NotationMetrics.TwoDotsRefX), two);
        Assert.Equal((NotationMetrics.FourDotsMarkerLength, NotationMetrics.FourDotsRefX), four);
    }

    /// <summary>
    ///     The circled-plus marker sits entirely behind the endpoint with the far edge of its circle on the
    ///     endpoint, and its plus arms span the circle diameter.
    /// </summary>
    [Fact]
    public void CircledPlus_HasCanonicalValues()
    {
        Assert.Equal(NotationMetrics.CircledPlusMarkerBox / 2.0, NotationMetrics.CircledPlusCenter, Tolerance);
        Assert.Equal(NotationMetrics.CircledPlusCenter + NotationMetrics.CircledPlusRadius, NotationMetrics.CircledPlusRefX, Tolerance);
        Assert.Equal(NotationMetrics.CircledPlusRadius, NotationMetrics.CircledPlusArmHalfLength, Tolerance);
        Assert.True(NotationMetrics.CircledPlusRefX <= NotationMetrics.CircledPlusMarkerBox);
    }

    /// <summary>
    ///     The along-line length of the new styles is their marker box, so the router's end-leg and the
    ///     renderers' corner clamp reserve space for them; every legacy arm is unchanged.
    /// </summary>
    [Fact]
    public void AlongLineLength_NewStyles_MatchMarkerBoxesAndLegacyUnchanged()
    {
        Assert.Equal(NotationMetrics.TwoDotsMarkerLength, NotationMetrics.AlongLineLength(EndMarkerStyle.HollowTriangleTwoDots));
        Assert.Equal(NotationMetrics.FourDotsMarkerLength, NotationMetrics.AlongLineLength(EndMarkerStyle.HollowTriangleFourDots));
        Assert.Equal(NotationMetrics.CircledPlusMarkerBox, NotationMetrics.AlongLineLength(EndMarkerStyle.CircledPlus));

        Assert.Equal(0.0, NotationMetrics.AlongLineLength(EndMarkerStyle.None));
        Assert.Equal(NotationMetrics.EndMarkerLength, NotationMetrics.AlongLineLength(EndMarkerStyle.OpenChevron));
        Assert.Equal(NotationMetrics.EndMarkerLength, NotationMetrics.AlongLineLength(EndMarkerStyle.HollowTriangle));
        Assert.Equal(NotationMetrics.EndMarkerLength, NotationMetrics.AlongLineLength(EndMarkerStyle.HollowTriangleCrossbar));
        Assert.Equal(NotationMetrics.EndMarkerLength, NotationMetrics.AlongLineLength(EndMarkerStyle.FilledArrow));
        Assert.Equal(NotationMetrics.DiamondLength, NotationMetrics.AlongLineLength(EndMarkerStyle.HollowDiamond));
        Assert.Equal(NotationMetrics.DiamondLength, NotationMetrics.AlongLineLength(EndMarkerStyle.FilledDiamond));
        Assert.Equal(NotationMetrics.CircleMarkerBox, NotationMetrics.AlongLineLength(EndMarkerStyle.Circle));
        Assert.Equal(NotationMetrics.BarAlong, NotationMetrics.AlongLineLength(EndMarkerStyle.Bar));
    }

    /// <summary>
    ///     The across-line half width is the widest point of each glyph: triangle family, diamond,
    ///     circle, bar and circled plus.
    /// </summary>
    [Fact]
    public void AcrossHalfWidth_EveryStyle_MatchesWidestPoint()
    {
        Assert.Equal(0.0, NotationMetrics.AcrossHalfWidth(EndMarkerStyle.None));
        foreach (var triangle in new[]
        {
            EndMarkerStyle.OpenChevron,
            EndMarkerStyle.HollowTriangle,
            EndMarkerStyle.HollowTriangleCrossbar,
            EndMarkerStyle.FilledArrow,
            EndMarkerStyle.HollowTriangleTwoDots,
            EndMarkerStyle.HollowTriangleFourDots,
        })
        {
            Assert.Equal(NotationMetrics.EndMarkerHalfWidth, NotationMetrics.AcrossHalfWidth(triangle));
        }

        Assert.Equal(NotationMetrics.DiamondHalfWidth, NotationMetrics.AcrossHalfWidth(EndMarkerStyle.HollowDiamond));
        Assert.Equal(NotationMetrics.DiamondHalfWidth, NotationMetrics.AcrossHalfWidth(EndMarkerStyle.FilledDiamond));
        Assert.Equal(NotationMetrics.CircleRadius, NotationMetrics.AcrossHalfWidth(EndMarkerStyle.Circle));
        Assert.Equal(NotationMetrics.BarHalf, NotationMetrics.AcrossHalfWidth(EndMarkerStyle.Bar));
        Assert.Equal(NotationMetrics.CircledPlusRadius, NotationMetrics.AcrossHalfWidth(EndMarkerStyle.CircledPlus));
    }

    /// <summary>
    ///     The dots of the dotted markers stay inside the marker's across-line extent.
    /// </summary>
    [Fact]
    public void DotCenters_StayInsideMarkerBox()
    {
        foreach (var style in new[] { EndMarkerStyle.HollowTriangleTwoDots, EndMarkerStyle.HollowTriangleFourDots })
        {
            var (length, refX) = NotationMetrics.TriangleFamilyBox(style);
            foreach (var dot in NotationMetrics.DotCenters(style))
            {
                var boxX = refX - dot.Along;
                Assert.InRange(boxX - NotationMetrics.DotRadius, 0.0, length);
                Assert.InRange(boxX + NotationMetrics.DotRadius, 0.0, length);
                Assert.True(Math.Abs(dot.Across) + NotationMetrics.DotRadius <= NotationMetrics.EndMarkerHalfWidth + Tolerance);
            }
        }
    }

    /// <summary>
    ///     The rendered along-line length scales the notation length by the theme stroke width, matching
    ///     the SVG marker-unit convention, and is zero without a marker.
    /// </summary>
    [Fact]
    public void RenderedAlongLineLength_ScalesNotationLengthByStrokeWidth()
    {
        var theme = Themes.Light;

        Assert.Equal(NotationMetrics.EndMarkerLength * theme.StrokeWidth, NotationMetrics.RenderedAlongLineLength(EndMarkerStyle.OpenChevron, theme), Tolerance);
        Assert.Equal(NotationMetrics.CircledPlusMarkerBox * theme.StrokeWidth, NotationMetrics.RenderedAlongLineLength(EndMarkerStyle.CircledPlus, theme), Tolerance);
        Assert.Equal(0.0, NotationMetrics.RenderedAlongLineLength(EndMarkerStyle.None, theme));
    }

    /// <summary>
    ///     A null theme is rejected by <see cref="NotationMetrics.RenderedAlongLineLength"/>.
    /// </summary>
    [Fact]
    public void RenderedAlongLineLength_NullTheme_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => NotationMetrics.RenderedAlongLineLength(EndMarkerStyle.OpenChevron, null!));
    }

    /// <summary>
    ///     The thick-bar thickness and crossed-circle arm fraction have their documented values: the arm
    ///     fraction is cos 45 degrees so the four arms end exactly on the circle.
    /// </summary>
    [Fact]
    public void ThickBarAndCrossedCircle_HaveCanonicalValues()
    {
        Assert.Equal(4.0, NotationMetrics.ThickBarThickness);
        Assert.Equal(Math.Cos(Math.PI / 4.0), NotationMetrics.CrossedCircleArmFraction, Tolerance);

        var arm = NotationMetrics.CrossedCircleArmFraction;
        Assert.Equal(1.0, Math.Sqrt((arm * arm) + (arm * arm)), Tolerance);
    }

    /// <summary>
    ///     The send icon is a convex pentagon pointing right and stays inside its documented box.
    /// </summary>
    [Fact]
    public void CornerIconVertices_Send_IsConvexPentagonInsideIconBox()
    {
        var vertices = NotationMetrics.CornerIconVertices(BoxCornerIcon.Send);

        Assert.Equal(5, vertices.Count);
        Assert.All(vertices, v => Assert.InRange(v.Along, 0.0, NotationMetrics.CornerIconWidth));
        Assert.All(vertices, v => Assert.InRange(v.Across, 0.0, NotationMetrics.CornerIconHeight));
        var point = vertices.OrderByDescending(v => v.Along).First();
        Assert.Equal(NotationMetrics.CornerIconWidth, point.Along);
        Assert.Equal(NotationMetrics.CornerIconHeight / 2.0, point.Across);
        Assert.Equal(0.0, vertices.Min(v => v.Along));
    }

    /// <summary>
    ///     The accept icon is the send pentagon with a triangular notch cut into its left edge, so it has
    ///     one more vertex and its leftmost point at the notch is inset from the icon's left edge.
    /// </summary>
    [Fact]
    public void CornerIconVertices_Accept_HasNotchInLeftEdge()
    {
        var send = NotationMetrics.CornerIconVertices(BoxCornerIcon.Send);
        var accept = NotationMetrics.CornerIconVertices(BoxCornerIcon.Accept);

        Assert.Equal(send.Count + 1, accept.Count);
        var notch = accept[accept.Count - 1];
        Assert.Equal(NotationMetrics.CornerIconNotchDepth, notch.Along);
        Assert.Equal(NotationMetrics.CornerIconHeight / 2.0, notch.Across);
    }

    /// <summary>
    ///     No corner icon yields no vertices.
    /// </summary>
    [Fact]
    public void CornerIconVertices_None_IsEmpty()
    {
        Assert.Empty(NotationMetrics.CornerIconVertices(BoxCornerIcon.None));
    }
}
