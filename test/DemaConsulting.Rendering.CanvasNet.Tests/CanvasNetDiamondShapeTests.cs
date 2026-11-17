// <copyright file="CanvasNetDiamondShapeTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.CanvasNet;

namespace DemaConsulting.Rendering.CanvasNet.Tests;

/// <summary>
///     Pixel tests for the CanvasNet (PNG) rendering of <see cref="BoxShape.Diamond"/>: stroke ink on
///     the four vertices, nothing at the bounding-box corners, a depth-filled interior, and a dash
///     pattern for a dashed appearance.
/// </summary>
public sealed class CanvasNetDiamondShapeTests
{
    private static Surface Render(LayoutBox box) =>
        PngRenderer.RenderToSurface(new LayoutTree(300, 200, [box]), new RenderOptions(Themes.Light));

    private static LayoutBox Diamond(BoxAppearance? appearance = null) =>
        new(20, 20, 200, 100, null, Depth: 1, BoxShape.Diamond, [], [], Appearance: appearance);

    private static bool IsInk(Rgba32 color) => color.A > 32 && (color.R + color.G + color.B) < 300;

    private static bool InkNear(Surface surface, int x, int y)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (IsInk(surface[x + dx, y + dy]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The stroke reaches all four vertices (the box-edge midpoints).</summary>
    [Fact]
    public void PngDiamond_HasInkAtTheFourVertices()
    {
        // Act
        using var surface = Render(Diamond());

        // Assert
        Assert.True(InkNear(surface, 120, 21), "top vertex");
        Assert.True(InkNear(surface, 219, 70), "right vertex");
        Assert.True(InkNear(surface, 120, 119), "bottom vertex");
        Assert.True(InkNear(surface, 21, 70), "left vertex");
    }

    /// <summary>The bounding-box corners are outside the diamond: they stay background-colored.</summary>
    [Fact]
    public void PngDiamond_BoundingBoxCornersAreEmpty()
    {
        // Act
        using var surface = Render(Diamond());

        // Assert: a pixel just inside each corner of the bounding box is untouched.
        foreach (var (x, y) in new[] { (24, 24), (216, 24), (24, 116), (216, 116) })
        {
            var color = surface[x, y];
            Assert.False(IsInk(color), $"corner ({x},{y}) has ink");
            Assert.True(color.R > 250 && color.G > 250 && color.B > 250, $"corner ({x},{y}) is not background");
        }
    }

    /// <summary>The interior is filled with the box's depth fill, which differs from the background.</summary>
    [Fact]
    public void PngDiamond_InteriorIsFilledWithTheDepthFill()
    {
        // Act
        using var surface = Render(Diamond());

        // Assert
        var center = surface[120, 70];
        var fill = Themes.Light.DepthFillColors[1];
        Assert.Equal(Convert.ToInt32(fill.Substring(1, 2), 16), center.R);
        Assert.Equal(Convert.ToInt32(fill.Substring(3, 2), 16), center.G);
        Assert.Equal(Convert.ToInt32(fill.Substring(5, 2), 16), center.B);
    }

    /// <summary>
    ///     A dashed appearance removes a visible share of the ink along the diamond's slanted edge compared
    ///     with the solid outline (the dash pattern leaves a third of the edge empty).
    /// </summary>
    [Fact]
    public void PngDiamond_Dashed_HasLessInkAlongTheEdgeThanSolid()
    {
        // Arrange / Act
        using var dashed = Render(Diamond(new BoxAppearance(LineStyle.Dashed)));
        using var solid = Render(Diamond());

        // Assert: the upper-left quadrant holds only the slanted edge of the outline.
        var solidInk = CountInk(solid, 23, 23, 117, 67);
        var dashedInk = CountInk(dashed, 23, 23, 117, 67);
        Assert.True(solidInk > 50, "the solid edge must have ink");
        Assert.True(dashedInk < solidInk * 0.85, $"dashed ink {dashedInk} vs solid ink {solidInk}");
        Assert.True(dashedInk > solidInk * 0.4, "the dashed edge must still be drawn");
    }

    private static int CountInk(Surface surface, int x0, int y0, int x1, int y1)
    {
        var count = 0;
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (IsInk(surface[x, y]))
                {
                    count++;
                }
            }
        }

        return count;
    }
}
