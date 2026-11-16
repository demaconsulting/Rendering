// <copyright file="CanvasNetNotationPrimitiveTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.CanvasNet;

namespace DemaConsulting.Rendering.CanvasNet.Tests;

/// <summary>
///     Pixel and geometry tests for the CanvasNet (PNG) rendering of the notation primitives: the
///     dotted-triangle and circled-plus end markers at both line ends, dashed outlines, name and
///     compartment-title text styles, folder name-in-tab, corner icons, thick-bar and crossed-circle
///     badges, dashed bands, rounded lifelines, and connector labels that avoid boxes. The raster renderer
///     draws markers at the same size as the SVG renderer (notation size times the theme stroke width,
///     <see cref="NotationMetrics.MarkerScale(Theme)"/>), so geometry is asserted against
///     <see cref="NotationMetrics"/> scaled by <c>K</c>.
/// </summary>
public sealed class CanvasNetNotationPrimitiveTests
{
    private static Surface Render(params LayoutNode[] nodes) =>
        PngRenderer.RenderToSurface(new LayoutTree(300, 200, nodes), new RenderOptions(Themes.Light));

    /// <summary>Notation-to-pixel marker scale of the default (light) theme at scale 1.</summary>
    private static readonly double K = NotationMetrics.MarkerScale(Themes.Light);

    private static bool IsInk(Rgba32 color) => color.A > 32 && (color.R + color.G + color.B) < 600;

    private static bool InkAt(Surface surface, double x, double y) => IsInk(surface[(int)Math.Floor(x), (int)Math.Floor(y)]);

    private static bool InkNear(Surface surface, double x, double y)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (InkAt(surface, x + dx, y + dy))
                {
                    return true;
                }
            }
        }

        return false;
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

    /// <summary>Counts the gaps (ink to no-ink transitions) along a stroke two pixels thick centered on row <paramref name="y"/>.</summary>
    private static int CountGaps(Surface surface, int y, int x0, int x1)
    {
        var gaps = 0;
        var previous = true;
        for (var x = x0; x <= x1; x++)
        {
            var ink = IsInk(surface[x, y]) || IsInk(surface[x, y - 1]);
            if (previous && !ink)
            {
                gaps++;
            }

            previous = ink;
        }

        return gaps;
    }

    private static double InkCentroidX(Surface surface, int x0, int y0, int x1, int y1)
    {
        double sum = 0;
        var count = 0;
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (IsInk(surface[x, y]))
                {
                    sum += x;
                    count++;
                }
            }
        }

        Assert.True(count > 0, "Expected some ink.");
        return sum / count;
    }

    private static LayoutLine Horizontal(EndMarkerStyle source, EndMarkerStyle target, double x0 = 10, double x1 = 150) =>
        new([new Point2D(x0, 50), new Point2D(x1, 50)], source, target, LineStyle.Solid, null);

    private static LayoutBox Box(BoxShape shape, BoxAppearance? appearance, string? label = null, IReadOnlyList<LayoutCompartment>? compartments = null, string? keyword = null) =>
        new(20, 20, 200, 100, label, 0, shape, compartments ?? [], [], Keyword: keyword, Appearance: appearance);

    // ── End markers ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     The two-dots marker has ink at both dot centers (one on each side of the line, behind the
    ///     triangle base), which a plain hollow triangle does not, and its triangle tip lands exactly where
    ///     the plain triangle's does.
    /// </summary>
    [Fact]
    public void PngTwoDots_InkAtBothDotCenters_TriangleTipUnchanged()
    {
        using var dotted = Render(Horizontal(EndMarkerStyle.None, EndMarkerStyle.HollowTriangleTwoDots));
        using var plain = Render(Horizontal(EndMarkerStyle.None, EndMarkerStyle.HollowTriangle));

        foreach (var dot in NotationMetrics.DotCenters(EndMarkerStyle.HollowTriangleTwoDots))
        {
            Assert.True(InkAt(dotted, 150 - (dot.Along * K), 50 + (dot.Across * K)), $"Expected ink at dot ({dot.Along}, {dot.Across}).");
            Assert.False(InkAt(plain, 150 - (dot.Along * K), 50 + (dot.Across * K)), "The plain triangle must have no ink at the dot centre.");
        }

        Assert.Equal(RightmostInkColumn(plain), RightmostInkColumn(dotted));
    }

    /// <summary>
    ///     The four-dots marker has ink at all four centers of its 2x2 grid.
    /// </summary>
    [Fact]
    public void PngFourDots_InkAtAllFourDotCenters()
    {
        using var surface = Render(Horizontal(EndMarkerStyle.None, EndMarkerStyle.HollowTriangleFourDots));

        var dots = NotationMetrics.DotCenters(EndMarkerStyle.HollowTriangleFourDots);
        Assert.Equal(4, dots.Count);
        foreach (var dot in dots)
        {
            Assert.True(InkAt(surface, 150 - (dot.Along * K), 50 + (dot.Across * K)), $"Expected ink at dot ({dot.Along}, {dot.Across}).");
        }
    }

    /// <summary>
    ///     The circled plus has ink on its ring and on both plus arms, an empty (background) interior off
    ///     the arms, and its far circle edge on the line endpoint.
    /// </summary>
    [Fact]
    public void PngCircledPlus_RingAndArmsHaveInkAndInteriorIsEmpty()
    {
        using var surface = Render(Horizontal(EndMarkerStyle.None, EndMarkerStyle.CircledPlus));

        var r = NotationMetrics.CircledPlusRadius * K;
        var centreX = 150 - r - (NotationMetrics.TipRetraction(EndMarkerStyle.CircledPlus, Themes.Light) * K);
        Assert.True(InkNear(surface, centreX, 50 - r), "Expected ink at the top of the ring.");
        Assert.True(InkNear(surface, centreX, 50 + r), "Expected ink at the bottom of the ring.");
        Assert.True(InkNear(surface, centreX, 50 + 3), "Expected ink on the vertical plus arm.");
        Assert.True(InkNear(surface, 150 - 1, 50), "Expected ink where the circle's far edge meets the endpoint.");
        Assert.False(InkAt(surface, centreX - 2, 50 - 2), "The circle interior off the arms must be background.");
        Assert.False(InkAt(surface, centreX + 2, 50 + 2), "The circle interior off the arms must be background.");
    }

    /// <summary>
    ///     A circled plus at the source end sits on the line side of the start point, mirrored from the
    ///     target-end case, with nothing drawn beyond the start point.
    /// </summary>
    [Fact]
    public void PngCircledPlus_AtSourceEnd_SitsOnLineSideOfStartPoint()
    {
        using var surface = Render(Horizontal(EndMarkerStyle.CircledPlus, EndMarkerStyle.None, 50, 190));

        var r = NotationMetrics.CircledPlusRadius * K;
        Assert.True(InkNear(surface, 50 + r, 50 - r), "Expected ink at the top of the ring.");
        Assert.True(InkNear(surface, 50 + r, 50 + r), "Expected ink at the bottom of the ring.");
        Assert.Equal(0, CountInk(surface, 0, 30, 47, 70));
    }

    /// <summary>
    ///     A dotted triangle at the source end has its dots on the line side of the triangle base, so the
    ///     triangle apex is the leftmost ink.
    /// </summary>
    [Fact]
    public void PngTwoDots_AtSourceEnd_DotsFollowTriangleBaseAlongLine()
    {
        using var surface = Render(Horizontal(EndMarkerStyle.HollowTriangleTwoDots, EndMarkerStyle.None, 50, 190));

        foreach (var dot in NotationMetrics.DotCenters(EndMarkerStyle.HollowTriangleTwoDots))
        {
            Assert.True(InkAt(surface, 50 + (dot.Along * K), 50 + (dot.Across * K)), $"Expected ink at mirrored dot ({dot.Along}, {dot.Across}).");
        }
    }

    /// <summary>
    ///     The redefinition crossbar is drawn BEHIND the triangle base: there is a clear gap between the
    ///     base and the bar, the bar spans the base height, and nothing sits at the bar position of the
    ///     old geometry (against the apex).
    /// </summary>
    [Fact]
    public void PngCrossbar_BarSitsBehindBaseWithGap()
    {
        using var surface = Render(Horizontal(EndMarkerStyle.None, EndMarkerStyle.HollowTriangleCrossbar));

        var baseX = 150 - (NotationMetrics.EndMarkerRefX * K);
        var barX = 150 - (NotationMetrics.CrossbarAlong * K);
        var halfSpan = NotationMetrics.CrossbarHalfLength * K;

        // The bar has ink above and below the line, at its full half span.
        Assert.True(InkNear(surface, barX, 50 - halfSpan + 1), "Expected ink at the top of the bar.");
        Assert.True(InkNear(surface, barX, 50 + halfSpan - 1), "Expected ink at the bottom of the bar.");

        // Between the base and the bar (above the line) there is a clear gap.
        var gapX = (baseX + barX) / 2.0;
        Assert.False(InkAt(surface, gapX, 50 - 4), "Expected a clear gap between the triangle base and the bar.");
        Assert.False(InkAt(surface, gapX, 50 + 4), "Expected a clear gap between the triangle base and the bar.");

        // Nothing is drawn behind the bar except the line itself.
        Assert.Equal(0, CountInk(surface, 0, 40, (int)Math.Floor(barX - (NotationMetrics.CrossbarStrokeRoom * K) - 2), 46));
    }

    /// <summary>
    ///     A crossbar marker at the source end sits on the line side of the start point with the bar
    ///     following the triangle base along the line.
    /// </summary>
    [Fact]
    public void PngCrossbar_AtSourceEnd_BarFollowsBaseAlongLine()
    {
        using var surface = Render(Horizontal(EndMarkerStyle.HollowTriangleCrossbar, EndMarkerStyle.None, 50, 190));

        var barX = 50 + (NotationMetrics.CrossbarAlong * K);
        var halfSpan = NotationMetrics.CrossbarHalfLength * K;
        Assert.True(InkNear(surface, barX, 50 - halfSpan + 1), "Expected ink at the top of the bar.");
        Assert.True(InkNear(surface, barX, 50 + halfSpan - 1), "Expected ink at the bottom of the bar.");

        // Nothing is drawn beyond the start point except the triangle apex and its stroke miter.
        Assert.Equal(0, CountInk(surface, 0, 30, 40, 70));
    }

    /// <summary>
    ///     A rounded corner next to a target end marker is clamped to the marker's rendered along-line
    ///     length: with a large corner radius and a final leg only two pixels longer than the rendered
    ///     chevron, the bend stays nearly sharp at the original corner, whereas the same path without
    ///     markers is rounded with the full available radius and leaves no ink at the corner.
    /// </summary>
    [Fact]
    public void PngLineCornerClamp_UsesRenderedMarkerLength()
    {
        var theme = Themes.Light with { LineCornerRadius = 12.0 };
        var rendered = NotationMetrics.RenderedAlongLineLength(EndMarkerStyle.OpenChevron, theme);
        var leg = rendered + 2.0;
        Point2D[] waypoints = [new(10, 10), new(100, 10), new(100, 10 + leg)];

        using var clamped = PngRenderer.RenderToSurface(
            new LayoutTree(300, 200, [new LayoutLine(waypoints, EndMarkerStyle.None, EndMarkerStyle.OpenChevron, LineStyle.Solid, null)]),
            new RenderOptions(theme));
        using var unmarked = PngRenderer.RenderToSurface(
            new LayoutTree(300, 200, [new LayoutLine(waypoints, EndMarkerStyle.None, EndMarkerStyle.None, LineStyle.Solid, null)]),
            new RenderOptions(theme));

        // Clamped radius is 2: the bend hugs the original corner.
        Assert.True(InkNear(clamped, 100, 10), "Expected ink at the original corner when the radius is clamped.");

        // Control: without a marker the radius is leg / 2 = 8.5, so the corner is cut well away from the bend.
        Assert.False(InkNear(unmarked, 100, 10), "Expected no ink at the original corner when the full radius applies.");
    }

    private static int RightmostInkColumn(Surface surface)
    {
        var rightmost = -1;
        for (var x = 100; x <= 170; x++)
        {
            if (CountInk(surface, x, 40, x, 60) > 0)
            {
                rightmost = x;
            }
        }

        return rightmost;
    }

    // ── Outline style ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     A dashed outline has a repeating gap pattern along the top edge of every box shape, which a
    ///     solid outline does not.
    /// </summary>
    [Theory]
    [InlineData(BoxShape.Rectangle)]
    [InlineData(BoxShape.RoundedRectangle)]
    [InlineData(BoxShape.Folder)]
    [InlineData(BoxShape.Note)]
    public void PngBoxOutline_Dashed_HasGapsAlongTopEdgeAndSolidHasNone(BoxShape shape)
    {
        var box = Box(shape, new BoxAppearance(LineStyle.Dashed));
        var solid = Box(shape, null);
        var theme = Themes.Light;

        // The edge row to scan: the top edge, or for a folder the body's top edge to the right of the tab.
        var y = shape == BoxShape.Folder ? (int)Math.Round(20 + BoxMetrics.FolderTabHeight(theme)) : 20;
        var x0 = shape == BoxShape.Folder ? (int)(20 + BoxMetrics.ResolveFolderTabWidth(box, theme)) + 6 : 40;
        var x1 = shape == BoxShape.Note ? 170 : 200;

        using var dashed = Render(box);
        using var plain = Render(solid);

        Assert.True(CountGaps(dashed, y, x0, x1) >= 5, "Expected a repeating gap pattern on the dashed top edge.");
        Assert.Equal(0, CountGaps(plain, y, x0, x1));
    }

    /// <summary>
    ///     A dashed band has gaps along its top edge; a solid band does not.
    /// </summary>
    [Fact]
    public void PngBand_Dashed_HasGapsAlongTopEdge()
    {
        using var dashed = Render(new LayoutBand(20, 20, 200, 80, BandOrientation.Horizontal, null, [], LineStyle.Dashed));
        using var solid = Render(new LayoutBand(20, 20, 200, 80, BandOrientation.Horizontal, null, []));

        Assert.True(CountGaps(dashed, 20, 30, 210) >= 5);
        Assert.Equal(0, CountGaps(solid, 20, 30, 210));
    }

    // ── Text styles ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     A bold name has measurably more ink than a regular name of the same text, and the default
    ///     appearance renders the legacy bold name pixel-for-pixel.
    /// </summary>
    [Fact]
    public void PngBoxName_RegularHasLessInkThanBold_DefaultIsBold()
    {
        using var regular = Render(Box(BoxShape.Rectangle, new BoxAppearance(NameStyle: TextStyle.Regular), "Namespace"));
        using var bold = Render(Box(BoxShape.Rectangle, new BoxAppearance(NameStyle: TextStyle.Bold), "Namespace"));
        using var legacy = Render(Box(BoxShape.Rectangle, null, "Namespace"));

        var regularInk = CountInk(regular, 24, 24, 216, 60);
        var boldInk = CountInk(bold, 24, 24, 216, 60);
        var legacyInk = CountInk(legacy, 24, 24, 216, 60);

        Assert.True(regularInk > 0);
        Assert.True(regularInk < boldInk, $"Expected regular ({regularInk}) to have less ink than bold ({boldInk}).");
        Assert.Equal(boldInk, legacyInk);
    }

    /// <summary>
    ///     An italic name slants right: the ink of its upper half sits further right, relative to its lower
    ///     half, than the upright name's.
    /// </summary>
    [Fact]
    public void PngBoxName_Italic_SlantsRelativeToRegular()
    {
        using var regular = Render(Box(BoxShape.Rectangle, new BoxAppearance(NameStyle: TextStyle.Regular), "Namespace"));
        using var italic = Render(Box(BoxShape.Rectangle, new BoxAppearance(NameStyle: TextStyle.Italic), "Namespace"));

        // The name block is 14 px tall starting just below the top inset of the box.
        var regularSkew = InkCentroidX(regular, 24, 26, 216, 33) - InkCentroidX(regular, 24, 34, 216, 41);
        var italicSkew = InkCentroidX(italic, 24, 26, 216, 33) - InkCentroidX(italic, 24, 34, 216, 41);

        Assert.True(italicSkew > regularSkew + 0.4, $"Expected italic skew ({italicSkew:F2}) to exceed regular skew ({regularSkew:F2}).");
    }

    /// <summary>
    ///     A centered compartment title's ink is centered in the box; the legacy left-aligned title's ink is
    ///     in the left part of the box.
    /// </summary>
    [Fact]
    public void PngCompartmentTitle_Centered_InkCenteredInContentArea()
    {
        var centered = new LayoutCompartment("attributes", [], TextAlign.Center, TextStyle.Italic);
        var legacy = new LayoutCompartment("attributes", []);
        using var centeredSurface = Render(Box(BoxShape.Rectangle, null, compartments: [centered]));
        using var legacySurface = Render(Box(BoxShape.Rectangle, null, compartments: [legacy]));

        var centeredX = InkCentroidX(centeredSurface, 22, 24, 218, 42);
        var legacyX = InkCentroidX(legacySurface, 22, 24, 218, 42);

        Assert.InRange(centeredX, 120 - 6, 120 + 6);
        Assert.True(legacyX < 80);
    }

    // ── Folder name in tab ─────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     With the tab placement the folder's name is inked inside the tab and nothing is drawn in the
    ///     body's title band; by default the name is in the body and the tab is empty.
    /// </summary>
    [Fact]
    public void PngFolderNameInTab_InkInTabAndNoneInBodyBand()
    {
        var theme = Themes.Light;
        var tabbed = Box(BoxShape.Folder, new BoxAppearance(NameStyle: TextStyle.Regular, FolderTitle: FolderTitlePlacement.Tab), "Package");
        var bodied = Box(BoxShape.Folder, null, "Package");
        var tabWidth = (int)BoxMetrics.ResolveFolderTabWidth(tabbed, theme);
        var tabHeight = (int)BoxMetrics.ResolveFolderTabHeight(tabbed, theme);

        using var inTab = Render(tabbed);
        using var inBody = Render(bodied);

        Assert.True(CountInk(inTab, 23, 22, 20 + tabWidth - 3, 20 + tabHeight - 3) > 20, "Expected the name inked in the tab.");
        Assert.Equal(0, CountInk(inTab, 30, 20 + tabHeight + 4, 210, 20 + tabHeight + 30));
        Assert.Equal(0, CountInk(inBody, 23, 22, 20 + (int)BoxMetrics.ResolveFolderTabWidth(bodied, theme) - 3, 20 + tabHeight - 3));
        Assert.True(CountInk(inBody, 30, 20 + tabHeight + 4, 210, 20 + tabHeight + 30) > 20, "Expected the name inked in the body.");
    }

    // ── Corner icon ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     The send icon has an intact left edge; the accept icon's left edge is notched inward at its
    ///     mid-height, so the pixel on the send icon's left edge is empty for accept.
    /// </summary>
    [Fact]
    public void PngCornerIcon_SendHasLeftEdgeAcceptHasNotch()
    {
        var originX = 20 + NotationMetrics.CornerIconInset;
        var originY = 20 + NotationMetrics.CornerIconInset;
        var midY = originY + (NotationMetrics.CornerIconHeight / 2.0);

        using var send = Render(Box(BoxShape.Rectangle, new BoxAppearance(CornerIcon: BoxCornerIcon.Send)));
        using var accept = Render(Box(BoxShape.Rectangle, new BoxAppearance(CornerIcon: BoxCornerIcon.Accept)));
        using var none = Render(Box(BoxShape.Rectangle, null));

        Assert.True(InkNear(send, originX, midY), "Expected the send icon's left edge.");
        Assert.False(InkAt(accept, originX, midY), "Expected the accept icon's left edge to be notched.");
        Assert.True(InkNear(accept, originX + NotationMetrics.CornerIconNotchDepth, midY), "Expected ink at the notch tip.");
        Assert.False(InkNear(none, originX, midY), "A box without an icon has no ink there.");
    }

    // ── Badges ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     The thick horizontal bar is a filled rectangle of the badge size by the default thickness, and an
    ///     explicit thickness overrides it.
    /// </summary>
    [Fact]
    public void PngBadgeThickHorizontalBar_FillsSizeByThickness()
    {
        using var defaultBar = Render(new LayoutBadge(100, 50, 60, BadgeShape.ThickHorizontalBar, null));
        using var fatBar = Render(new LayoutBadge(100, 50, 60, BadgeShape.ThickHorizontalBar, null, Thickness: 10));

        Assert.True(InkAt(defaultBar, 72, 50) && InkAt(defaultBar, 127, 50));
        Assert.False(InkAt(defaultBar, 68, 50) || InkAt(defaultBar, 132, 50));
        Assert.True(InkAt(defaultBar, 100, 48) && InkAt(defaultBar, 100, 51));
        Assert.False(InkAt(defaultBar, 100, 45) || InkAt(defaultBar, 100, 55));
        Assert.True(InkAt(fatBar, 100, 46) && InkAt(fatBar, 100, 54));
        Assert.False(InkAt(fatBar, 100, 43) || InkAt(fatBar, 100, 57));
    }

    /// <summary>
    ///     The thick vertical bar is a filled rectangle of the default thickness by the badge size.
    /// </summary>
    [Fact]
    public void PngBadgeThickVerticalBar_FillsThicknessBySize()
    {
        using var surface = Render(new LayoutBadge(100, 80, 60, BadgeShape.ThickVerticalBar, null));

        Assert.True(InkAt(surface, 100, 52) && InkAt(surface, 100, 107));
        Assert.False(InkAt(surface, 100, 48) || InkAt(surface, 100, 112));
        Assert.True(InkAt(surface, 98, 80) && InkAt(surface, 101, 80));
        Assert.False(InkAt(surface, 95, 80) || InkAt(surface, 105, 80));
    }

    /// <summary>
    ///     The crossed circle has ink on its ring, at the centre where the arms cross and along the four
    ///     diagonal arms, with an empty interior between the arms.
    /// </summary>
    [Fact]
    public void PngBadgeCrossedCircle_RingAndDiagonalArmsHaveInk()
    {
        using var surface = Render(new LayoutBadge(100, 50, 24, BadgeShape.CrossedCircle, null));

        var arm = 12 * NotationMetrics.CrossedCircleArmFraction;
        Assert.True(InkNear(surface, 112, 50) && InkNear(surface, 88, 50));
        Assert.True(InkAt(surface, 100, 50));
        Assert.True(InkNear(surface, 100 + (arm / 2), 50 + (arm / 2)));
        Assert.True(InkNear(surface, 100 - (arm / 2), 50 + (arm / 2)));
        Assert.False(InkAt(surface, 106, 50), "The interior between the arms must be background.");
    }

    // ── Lifeline ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     A rounded lifeline header has an empty top-left corner pixel where the sharp header has ink.
    /// </summary>
    [Fact]
    public void PngLifeline_RoundedHeader_CutsCornerAndSharpDoesNot()
    {
        using var rounded = Render(new LayoutLifeline(100, 10, 120, "a : A", 80, 30, null, BoxShape.RoundedRectangle));
        using var sharp = Render(new LayoutLifeline(100, 10, 120, "a : A", 80, 30));

        Assert.False(InkNear(rounded, 60, 10), "A rounded header must not ink its square corner.");
        Assert.True(InkNear(sharp, 60, 10), "A sharp header inks its corner.");
    }

    /// <summary>
    ///     A lifeline keyword adds a second text row inside the header, above the label.
    /// </summary>
    [Fact]
    public void PngLifeline_Keyword_AddsTextRowAboveLabel()
    {
        var theme = Themes.Light;
        var height = BoxMetrics.LifelineHeaderHeight(theme, hasKeyword: true);
        using var withKeyword = Render(new LayoutLifeline(100, 10, 150, "label", 120, height, "part"));
        using var without = Render(new LayoutLifeline(100, 10, 150, "label", 120, BoxMetrics.LifelineHeaderHeight(theme, false)));

        Assert.Equal(2, TextBands(withKeyword, 70, 130, 12, 10 + (int)height - 2));
        Assert.Equal(1, TextBands(without, 70, 130, 12, 10 + (int)BoxMetrics.LifelineHeaderHeight(theme, false) - 2));
    }

    /// <summary>
    ///     A dashed header appearance gives the lifeline header a dashed top edge.
    /// </summary>
    [Fact]
    public void PngLifeline_DashedHeaderAppearance_HasGapsAlongTopEdge()
    {
        var appearance = new BoxAppearance(LineStyle.Dashed);
        using var dashed = Render(new LayoutLifeline(100, 10, 120, "a", 100, 30, null, BoxShape.Rectangle, appearance));

        Assert.True(CountGaps(dashed, 10, 52, 148) >= 4);
    }

    private static int TextBands(Surface surface, int x0, int x1, int y0, int y1)
    {
        var bands = 0;
        var inBand = false;
        for (var y = y0; y <= y1; y++)
        {
            var any = CountInk(surface, x0, y, x1, y) > 0;
            if (any && !inBand)
            {
                bands++;
            }

            inBand = any;
        }

        return bands;
    }

    // ── Connector labels avoid boxes ───────────────────────────────────────────────────────────

    /// <summary>
    ///     A connector label that would sit on a small leaf box is placed clear, so the box border under
    ///     the label's would-be position is still inked.
    /// </summary>
    [Fact]
    public void PngLineLabel_MidpointOnLeafBox_DoesNotEraseBoxBorder()
    {
        var line = new LayoutLine(
            [new Point2D(0, 100), new Point2D(300, 100)],
            EndMarkerStyle.None,
            EndMarkerStyle.None,
            LineStyle.Solid,
            "[guard]");
        var box = new LayoutBox(140, 94, 20, 12, null, 0, BoxShape.Rectangle, [], []);

        using var surface = Render(box, line);

        // The box's top border, inside the label's legacy midpoint position, must survive.
        Assert.True(InkNear(surface, 150, 94), "Expected the box border to remain visible.");
    }
}
