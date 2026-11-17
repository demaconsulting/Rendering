// <copyright file="DiamondBoxMetricsTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Abstractions.Tests;

/// <summary>
///     Tests for the shared <see cref="BoxShape.Diamond"/> outline geometry in
///     <see cref="NotationMetrics"/>, which both renderers draw so their outlines cannot drift apart.
/// </summary>
public sealed class DiamondBoxMetricsTests
{
    /// <summary>
    ///     The diamond vertices are the midpoints of the four bounding-box edges, in the order top, right,
    ///     bottom, left.
    /// </summary>
    [Fact]
    public void NotationMetrics_DiamondBoxVertices_AreTheBoxEdgeMidpointsTopRightBottomLeft()
    {
        // Act
        var vertices = NotationMetrics.DiamondBoxVertices(20, 30, 200, 100);

        // Assert
        Assert.Equal(
            [new Point2D(120, 30), new Point2D(220, 80), new Point2D(120, 130), new Point2D(20, 80)],
            vertices);
    }

    /// <summary>
    ///     A degenerate (zero-size) box collapses all four vertices onto the box point instead of throwing.
    /// </summary>
    [Fact]
    public void NotationMetrics_DiamondBoxVertices_ZeroSizeBox_CollapsesToThePoint()
    {
        // Act
        var vertices = NotationMetrics.DiamondBoxVertices(5, 6, 0, 0);

        // Assert
        Assert.Equal(4, vertices.Count);
        Assert.All(vertices, vertex => Assert.Equal(new Point2D(5, 6), vertex));
    }

    /// <summary>
    ///     A diamond has no tab or fold, so its title cursor starts exactly where a rectangle's does.
    /// </summary>
    [Fact]
    public void BoxMetrics_TitleCursorTop_Diamond_MatchesARectangle()
    {
        // Arrange
        var diamond = new LayoutBox(10, 20, 100, 100, "d", Depth: 0, BoxShape.Diamond, [], []);
        var rectangle = diamond with { Shape = BoxShape.Rectangle };

        // Act / Assert
        Assert.Equal(
            BoxMetrics.TitleCursorTop(rectangle, Themes.Light),
            BoxMetrics.TitleCursorTop(diamond, Themes.Light));
    }
}
