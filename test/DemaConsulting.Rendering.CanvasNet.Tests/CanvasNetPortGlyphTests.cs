// <copyright file="CanvasNetPortGlyphTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.CanvasNet;

namespace DemaConsulting.Rendering.CanvasNet.Tests;

/// <summary>
///     Pixel tests for the CanvasNet (PNG) rendering of the SysML-style port glyphs: the legacy filled square
///     is unchanged, an outlined glyph has a background-colored interior with arrow ink inside, a dashed
///     outline has gaps, a proxy port is a filled circle, and an outward label lies outside the owning box.
/// </summary>
public sealed class CanvasNetPortGlyphTests
{
    private static Surface Render(params LayoutNode[] nodes) =>
        PngRenderer.RenderToSurface(new LayoutTree(300, 200, nodes), new RenderOptions(Themes.Light));

    private static LayoutPort Port(PortSide side, PortAppearance? appearance, string? label = null) =>
        new(100, 80, side, label, Appearance: appearance);

    private static bool IsInk(Rgba32 color) => color.A > 32 && (color.R + color.G + color.B) < 300;

    /// <summary>Any visibly painted pixel, including the light anti-aliasing of a one-pixel stroke.</summary>
    private static bool IsMark(Rgba32 color) => color.A > 32 && (color.R + color.G + color.B) < 650;

    private static bool IsBackground(Rgba32 color) => color.R > 250 && color.G > 250 && color.B > 250;

    private static int CountInk(Surface surface, int x0, int y0, int x1, int y1)
    {
        var count = 0;
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (IsMark(surface[x, y]))
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>The legacy port is the 8 px filled square: ink across its whole footprint, nothing just outside it.</summary>
    [Fact]
    public void PngLegacyPort_IsAFilledEightPixelSquare()
    {
        // Act
        using var surface = Render(Port(PortSide.Left, null));

        // Assert
        Assert.True(IsInk(surface[100, 80]));
        Assert.True(IsInk(surface[97, 77]));
        Assert.True(IsInk(surface[102, 82]));
        Assert.False(IsInk(surface[100, 90]));
        Assert.False(IsInk(surface[88, 80]));
    }

    /// <summary>An outlined glyph has a background-colored interior (away from the arrow) and an ink outline.</summary>
    [Fact]
    public void PngOutlinedPort_HasABackgroundInteriorAndAnInkOutline()
    {
        // Act
        using var surface = Render(Port(PortSide.Left, new PortAppearance(PortDirection.None, PortGlyph.Outlined)));

        // Assert: interior centre is background, the outline is ink.
        Assert.True(IsBackground(surface[100, 80]), "interior must be the background color");
        Assert.True(IsInk(surface[100, 74]) || IsInk(surface[100, 75]), "top outline ink");
        Assert.True(IsInk(surface[94, 80]) || IsInk(surface[95, 80]), "left outline ink");
    }

    /// <summary>A direction arrow puts ink inside the glyph where there is none without it.</summary>
    /// <param name="direction">The arrow direction.</param>
    [Theory]
    [InlineData(PortDirection.In)]
    [InlineData(PortDirection.Out)]
    [InlineData(PortDirection.InOut)]
    public void PngOutlinedPort_DirectionArrow_PutsInkInsideTheGlyph(PortDirection direction)
    {
        // Act
        using var withArrow = Render(Port(PortSide.Left, new PortAppearance(direction)));
        using var withoutArrow = Render(Port(PortSide.Left, new PortAppearance(PortDirection.None, PortGlyph.Outlined)));

        // Assert: the interior (inside the 1 px outline) holds ink only with an arrow.
        Assert.Equal(0, CountInk(withoutArrow, 96, 77, 104, 83));
        Assert.True(CountInk(withArrow, 96, 77, 104, 83) >= 6, "the arrow must paint ink inside the glyph");
    }

    /// <summary>
    ///     The arrow's tip is on the interior side for <c>in</c> and on the exterior side for <c>out</c>:
    ///     the ink centroid inside the glyph is shifted accordingly.
    /// </summary>
    [Fact]
    public void PngOutlinedPort_InAndOutArrows_PointOppositeWays()
    {
        // Act
        using var inArrow = Render(Port(PortSide.Left, new PortAppearance(PortDirection.In)));
        using var outArrow = Render(Port(PortSide.Left, new PortAppearance(PortDirection.Out)));

        // Assert: head ink (the wide part of the arrow) sits right of centre for a left port's "in" arrow
        // (into the part, +x) and left of centre for "out".
        var inRight = CountInk(inArrow, 101, 77, 104, 83);
        var inLeft = CountInk(inArrow, 96, 77, 99, 83);
        var outRight = CountInk(outArrow, 101, 77, 104, 83);
        var outLeft = CountInk(outArrow, 96, 77, 99, 83);
        Assert.True(inRight > inLeft, $"in arrow: right {inRight} vs left {inLeft}");
        Assert.True(outLeft > outRight, $"out arrow: left {outLeft} vs right {outRight}");
    }

    /// <summary>A dashed outline has less ink along the top edge than a solid one.</summary>
    [Fact]
    public void PngDashedPort_HasLessOutlineInkThanSolid()
    {
        // Act
        using var dashed = Render(Port(PortSide.Left, new PortAppearance(OutlineStyle: LineStyle.Dashed)));
        using var solid = Render(Port(PortSide.Left, new PortAppearance(PortDirection.None, PortGlyph.Outlined)));

        // Assert: count the whole outline ring.
        var dashedInk = CountInk(dashed, 93, 73, 107, 87);
        var solidInk = CountInk(solid, 93, 73, 107, 87);
        Assert.True(solidInk > 20);
        Assert.True(dashedInk < solidInk * 0.9, $"dashed {dashedInk} vs solid {solidInk}");
        Assert.True(dashedInk > solidInk * 0.3, "the dashed outline must still be drawn");
    }

    /// <summary>A proxy port is a filled circle: ink at the centre, none at the bounding-square corner.</summary>
    [Fact]
    public void PngProxyPort_IsAFilledCircle()
    {
        // Act
        using var surface = Render(Port(PortSide.Left, new PortAppearance(Glyph: PortGlyph.Proxy)));

        // Assert
        Assert.True(IsInk(surface[100, 80]));
        Assert.True(IsInk(surface[103, 80]));
        Assert.False(IsInk(surface[104, 76]), "the corner of the bounding square is outside the circle");
        Assert.False(IsInk(surface[100, 88]));
    }

    /// <summary>An outward label has ink outside the owning box and none inside it.</summary>
    /// <param name="side">The port side.</param>
    [Theory]
    [InlineData(PortSide.Left)]
    [InlineData(PortSide.Right)]
    [InlineData(PortSide.Top)]
    [InlineData(PortSide.Bottom)]
    public void PngOutwardLabel_HasInkOutsideTheBoxAndNoneInsideIt(PortSide side)
    {
        // Arrange
        var box = new LayoutBox(100, 80, 100, 60, null, 0, BoxShape.Rectangle, [], []);
        var (cx, cy) = side switch
        {
            PortSide.Left => (100.0, 110.0),
            PortSide.Right => (200.0, 110.0),
            PortSide.Top => (150.0, 80.0),
            _ => (150.0, 140.0),
        };
        var port = new LayoutPort(cx, cy, side, "speed : Real", Appearance: new PortAppearance(LabelPlacement: PortLabelPlacement.Outward));

        // Act
        using var surface = Render(box, port);

        // Assert
        var bounds = NotationMetrics.PortOutwardLabelBounds(port, "speed : Real", Themes.Light);
        var ink = CountInk(
            surface,
            (int)Math.Max(0, bounds.X - bounds.HalfWidth),
            (int)Math.Max(0, bounds.Y - bounds.HalfHeight),
            (int)Math.Min(surface.Width - 1, bounds.X + bounds.HalfWidth),
            (int)Math.Min(surface.Height - 1, bounds.Y + bounds.HalfHeight));
        Assert.True(ink >= 15, $"the outward label must have ink in its bounds (found {ink})");

        // The box interior (excluding its 1.5 px border and the glyph area) holds no ink: the label is outside.
        Assert.Equal(0, CountInk(surface, 110, 90, 190, 130));
    }
}
