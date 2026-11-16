// <copyright file="SvgNotationPrimitiveTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Svg;

namespace DemaConsulting.Rendering.Svg.Tests;

/// <summary>
///     Tests for the SVG rendering of the notation primitives: the dotted-triangle and circled-plus end
///     markers (and the mirrored start variants), dashed outlines, name/keyword/compartment-title text
///     styles, folder name-in-tab, corner icons, thick-bar and crossed-circle badges, dashed bands,
///     rounded lifelines, and connector labels that avoid boxes.
/// </summary>
public sealed class SvgNotationPrimitiveTests
{
    private static string Render(params LayoutNode[] nodes) => Render(Themes.Light, 400, 300, nodes);

    private static string Render(Theme theme, double width, double height, params LayoutNode[] nodes)
    {
        using var output = new MemoryStream();
        new SvgRenderer().Render(new LayoutTree(width, height, nodes), new RenderOptions(theme), output);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static string Num(double value) =>
        Math.Round(value, 6).ToString(CultureInfo.InvariantCulture);

    private static string F(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private static LayoutLine HorizontalLine(EndMarkerStyle source, EndMarkerStyle target) =>
        new([new Point2D(10, 10), new Point2D(190, 10)], source, target, LineStyle.Solid, null);

    private static string MarkerBody(string svg, string id)
    {
        var start = svg.IndexOf($"<marker id=\"{id}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected marker '{id}' to be defined.");
        var end = svg.IndexOf("</marker>", start, StringComparison.Ordinal);
        return svg[start..end];
    }

    private static string TextElementFor(string svg, string text)
    {
        var match = Regex.Match(svg, $"<text [^>]*>{Regex.Escape(text)}</text>");
        Assert.True(match.Success, $"Expected a <text> element containing '{text}'.");
        return match.Value;
    }

    private static LayoutBox Box(BoxShape shape, BoxAppearance? appearance, string? label = "Name", string? keyword = null, IReadOnlyList<LayoutCompartment>? compartments = null) =>
        new(20, 20, 200, 120, label, 0, shape, compartments ?? [], [], Keyword: keyword, Appearance: appearance);

    // ── End markers ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     The two-dots marker is a closed hollow triangle plus exactly two dot circles, with the marker
    ///     box and tip anchor derived from <see cref="NotationMetrics"/>.
    /// </summary>
    [Fact]
    public void EndMarkerTwoDots_DefinesTriangleAndTwoDotsFromNotationMetrics()
    {
        var svg = Render(HorizontalLine(EndMarkerStyle.None, EndMarkerStyle.HollowTriangleTwoDots));

        var body = MarkerBody(svg, "line-end-hollow-triangle-two-dots");
        Assert.Contains("<polygon", body, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(body, "<circle").Count);
        Assert.Contains(
            $"markerWidth=\"{Num(NotationMetrics.TwoDotsMarkerLength)}\" markerHeight=\"{Num(NotationMetrics.EndMarkerWidth)}\" refX=\"{Num(NotationMetrics.TwoDotsRefX)}\" refY=\"{Num(NotationMetrics.EndMarkerHalfWidth)}\"",
            body,
            StringComparison.Ordinal);
        Assert.Contains("marker-end=\"url(#line-end-hollow-triangle-two-dots)\"", svg, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The redefinition marker is a hollow triangle plus one crossbar BEHIND the triangle base (the
    ///     bar is on the line side of the base, with a gap), in a marker box widened by
    ///     <see cref="NotationMetrics.CrossbarExtension"/> whose tip anchor shifts by the same amount.
    /// </summary>
    [Fact]
    public void EndMarkerCrossbar_BarSitsBehindTriangleBase()
    {
        var svg = Render(HorizontalLine(EndMarkerStyle.None, EndMarkerStyle.HollowTriangleCrossbar));

        var body = MarkerBody(svg, "line-end-hollow-triangle-crossbar");
        Assert.Contains(
            $"markerWidth=\"{Num(NotationMetrics.CrossbarMarkerLength)}\" markerHeight=\"{Num(NotationMetrics.EndMarkerWidth)}\" refX=\"{Num(NotationMetrics.CrossbarRefX)}\" refY=\"{Num(NotationMetrics.EndMarkerHalfWidth)}\"",
            body,
            StringComparison.Ordinal);

        // The triangle base is at box x = CrossbarExtension (apex at box x = CrossbarMarkerLength).
        Assert.Contains(
            $"points=\"{Num(NotationMetrics.CrossbarExtension)} 0, {Num(NotationMetrics.CrossbarMarkerLength)} {Num(NotationMetrics.EndMarkerHalfWidth)}, {Num(NotationMetrics.CrossbarExtension)} {Num(NotationMetrics.EndMarkerWidth)}\"",
            body,
            StringComparison.Ordinal);

        // The bar is a vertical line behind the base: its box x is below the base's box x by the gap.
        var bar = Regex.Match(body, "<line x1=\"([^\"]+)\" y1=\"([^\"]+)\" x2=\"([^\"]+)\" y2=\"([^\"]+)\"");
        Assert.True(bar.Success, "Expected a crossbar <line>.");
        var barX = double.Parse(bar.Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.Equal(barX, double.Parse(bar.Groups[3].Value, CultureInfo.InvariantCulture), 6);
        Assert.Equal(NotationMetrics.CrossbarExtension - NotationMetrics.CrossbarBaseGap, barX, 6);
        Assert.Equal(0.0, double.Parse(bar.Groups[2].Value, CultureInfo.InvariantCulture), 6);
        Assert.Equal(NotationMetrics.EndMarkerWidth, double.Parse(bar.Groups[4].Value, CultureInfo.InvariantCulture), 6);
        Assert.True(barX < NotationMetrics.CrossbarExtension, "The bar must lie behind the triangle base.");
    }

    /// <summary>
    ///     The final-corner clamp keeps the rounded corner out of the end marker as it is RENDERED (the
    ///     notation length times the stroke width), not the notation length: a 17 px final approach
    ///     before a 15 px rendered chevron leaves only 2 px for the bend radius.
    /// </summary>
    [Fact]
    public void LineCornerClamp_UsesRenderedMarkerLength()
    {
        var line = new LayoutLine(
            [new Point2D(10, 10), new Point2D(60, 10), new Point2D(60, 27)],
            EndMarkerStyle.None,
            EndMarkerStyle.OpenChevron,
            LineStyle.Solid,
            null);

        var svg = Render(line);

        var rendered = NotationMetrics.RenderedAlongLineLength(EndMarkerStyle.OpenChevron, Themes.Light);
        var radius = 17.0 - rendered;
        Assert.Contains($" A {F(radius)} {F(radius)} 0 0 1 ", svg, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The four-dots marker has four dot circles forming a 2x2 grid: two distinct columns and two
    ///     distinct rows.
    /// </summary>
    [Fact]
    public void EndMarkerFourDots_HasTwoByTwoDotGrid()
    {
        var svg = Render(HorizontalLine(EndMarkerStyle.None, EndMarkerStyle.HollowTriangleFourDots));

        var body = MarkerBody(svg, "line-end-hollow-triangle-four-dots");
        var dots = Regex.Matches(body, "<circle cx=\"([^\"]+)\" cy=\"([^\"]+)\"");
        Assert.Equal(4, dots.Count);
        Assert.Equal(2, dots.Cast<Match>().Select(m => m.Groups[1].Value).Distinct().Count());
        Assert.Equal(2, dots.Cast<Match>().Select(m => m.Groups[2].Value).Distinct().Count());
        Assert.Contains(
            $"markerWidth=\"{Num(NotationMetrics.FourDotsMarkerLength)}\"",
            body,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     The circled-plus marker is a background-filled circle plus two plus arms spanning its diameter,
    ///     with the far edge of the circle on the line endpoint.
    /// </summary>
    [Fact]
    public void EndMarkerCircledPlus_DefinesCircleAndTwoPlusArms()
    {
        var svg = Render(HorizontalLine(EndMarkerStyle.None, EndMarkerStyle.CircledPlus));

        var body = MarkerBody(svg, "line-end-circled-plus");
        Assert.Single(Regex.Matches(body, "<circle"));
        Assert.Equal(2, Regex.Matches(body, "<line").Count);
        Assert.Contains($"r=\"{Num(NotationMetrics.CircledPlusRadius)}\"", body, StringComparison.Ordinal);
        Assert.Contains($"fill=\"{Themes.Light.BackgroundColor}\"", body, StringComparison.Ordinal);
        Assert.Contains(
            $"markerWidth=\"{Num(NotationMetrics.CircledPlusMarkerBox)}\" markerHeight=\"{Num(NotationMetrics.CircledPlusMarkerBox)}\" refX=\"{Num(NotationMetrics.CircledPlusRefX)}\" refY=\"{Num(NotationMetrics.CircledPlusCenter)}\"",
            body,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     The new marker definitions are written only when a line uses them; a tree that uses none has
    ///     exactly the historical eight definitions and the label filter.
    /// </summary>
    [Fact]
    public void EndMarkerDefs_NewMarkersUnused_AreNotEmitted()
    {
        var svg = Render(HorizontalLine(EndMarkerStyle.None, EndMarkerStyle.HollowTriangle));

        Assert.DoesNotContain("two-dots", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("four-dots", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("circled-plus", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("line-start-", svg, StringComparison.Ordinal);
        Assert.Equal(8, Regex.Matches(svg, "<marker ").Count);
        Assert.Contains("<filter id=\"label-bg\"", svg, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Each new marker is defined once even when several lines use it at either end.
    /// </summary>
    [Fact]
    public void EndMarkerDefs_NewMarkerUsedTwice_IsDefinedOnce()
    {
        var svg = Render(
            HorizontalLine(EndMarkerStyle.None, EndMarkerStyle.CircledPlus),
            HorizontalLine(EndMarkerStyle.None, EndMarkerStyle.CircledPlus));

        Assert.Single(Regex.Matches(svg, "<marker id=\"line-end-circled-plus\""));
    }

    /// <summary>
    ///     A new marker at the source end references a mirrored start variant whose tip anchor sits at the
    ///     near end of the marker box and whose glyph is wrapped in a mirror transform, because a
    ///     <c>marker-start</c> marker's x axis points into the line.
    /// </summary>
    [Fact]
    public void EndMarkerSource_CircledPlus_UsesMirroredStartVariant()
    {
        var svg = Render(HorizontalLine(EndMarkerStyle.CircledPlus, EndMarkerStyle.None));

        Assert.Contains("marker-start=\"url(#line-start-circled-plus)\"", svg, StringComparison.Ordinal);
        var body = MarkerBody(svg, "line-start-circled-plus");
        var mirroredRefX = NotationMetrics.CircledPlusMarkerBox - NotationMetrics.CircledPlusRefX;
        Assert.Contains($"refX=\"{Num(mirroredRefX)}\"", body, StringComparison.Ordinal);
        Assert.Contains($"matrix(-1 0 0 1 {Num(NotationMetrics.CircledPlusMarkerBox)} 0)", body, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A dotted marker at the source end likewise gets a mirrored start variant, and its end variant is
    ///     not written unless some line uses it at a target end.
    /// </summary>
    [Fact]
    public void EndMarkerSource_FourDots_UsesMirroredStartVariantOnly()
    {
        var svg = Render(HorizontalLine(EndMarkerStyle.HollowTriangleFourDots, EndMarkerStyle.None));

        var body = MarkerBody(svg, "line-start-hollow-triangle-four-dots");
        var mirroredRefX = NotationMetrics.FourDotsMarkerLength - NotationMetrics.FourDotsRefX;
        Assert.Contains($"refX=\"{Num(mirroredRefX)}\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"line-end-hollow-triangle-four-dots\"", svg, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A legacy marker at the source end references a mirrored start variant too, so source-end
    ///     decorations point out of the line start exactly like their target-end counterparts.
    /// </summary>
    [Fact]
    public void EndMarkerSource_LegacyDiamond_UsesMirroredStartVariant()
    {
        var svg = Render(HorizontalLine(EndMarkerStyle.FilledDiamond, EndMarkerStyle.None));

        Assert.Contains("marker-start=\"url(#line-start-filled-diamond)\"", svg, StringComparison.Ordinal);
        var body = MarkerBody(svg, "line-start-filled-diamond");
        Assert.Contains($"refX=\"{Num(NotationMetrics.DiamondLength - NotationMetrics.DiamondRefX)}\"", body, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The new markers work at both ends of one line.
    /// </summary>
    [Fact]
    public void EndMarkerBothEnds_NewMarkers_ReferenceStartAndEndVariants()
    {
        var svg = Render(HorizontalLine(EndMarkerStyle.CircledPlus, EndMarkerStyle.HollowTriangleTwoDots));

        Assert.Contains("marker-start=\"url(#line-start-circled-plus)\"", svg, StringComparison.Ordinal);
        Assert.Contains("marker-end=\"url(#line-end-hollow-triangle-two-dots)\"", svg, StringComparison.Ordinal);
    }

    // ── Outline style ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     A dashed or dotted outline emits the matching <c>stroke-dasharray</c> on the outline element of
    ///     every box shape.
    /// </summary>
    [Theory]
    [InlineData(BoxShape.Rectangle, LineStyle.Dashed, "6 3")]
    [InlineData(BoxShape.RoundedRectangle, LineStyle.Dashed, "6 3")]
    [InlineData(BoxShape.Folder, LineStyle.Dashed, "6 3")]
    [InlineData(BoxShape.Note, LineStyle.Dashed, "6 3")]
    [InlineData(BoxShape.Rectangle, LineStyle.Dotted, "2 2")]
    [InlineData(BoxShape.Folder, LineStyle.Dotted, "2 2")]
    public void BoxOutline_DashedOrDotted_EmitsDashArrayForEveryShape(BoxShape shape, LineStyle style, string expected)
    {
        var svg = Render(Box(shape, new BoxAppearance(style), label: null));

        Assert.Contains($"stroke-dasharray=\"{expected}\"", svg, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The Note outline dashes both its body path and its fold path.
    /// </summary>
    [Fact]
    public void BoxOutline_DashedNote_DashesBodyAndFold()
    {
        var svg = Render(Box(BoxShape.Note, new BoxAppearance(LineStyle.Dashed), label: null));

        Assert.Equal(2, Regex.Matches(svg, "stroke-dasharray=\"6 3\"").Count);
    }

    /// <summary>
    ///     A solid or absent appearance emits no dash array on the outline.
    /// </summary>
    [Fact]
    public void BoxOutline_SolidOrNoAppearance_EmitsNoDashArray()
    {
        Assert.DoesNotContain("stroke-dasharray", Render(Box(BoxShape.Rectangle, null, label: null)), StringComparison.Ordinal);
        Assert.DoesNotContain("stroke-dasharray", Render(Box(BoxShape.RoundedRectangle, new BoxAppearance(), label: null)), StringComparison.Ordinal);
    }

    // ── Name / keyword styles ──────────────────────────────────────────────────────────────────

    /// <summary>
    ///     The name's weight and style follow the appearance: regular has neither attribute, italic only
    ///     the style, bold italic both.
    /// </summary>
    [Fact]
    public void BoxName_Styles_EmitMatchingFontAttributes()
    {
        var regular = TextElementFor(Render(Box(BoxShape.Rectangle, new BoxAppearance(NameStyle: TextStyle.Regular))), "Name");
        var italic = TextElementFor(Render(Box(BoxShape.Rectangle, new BoxAppearance(NameStyle: TextStyle.Italic))), "Name");
        var boldItalic = TextElementFor(Render(Box(BoxShape.Rectangle, new BoxAppearance(NameStyle: TextStyle.BoldItalic))), "Name");

        Assert.DoesNotContain("font-weight", regular, StringComparison.Ordinal);
        Assert.DoesNotContain("font-style", regular, StringComparison.Ordinal);
        Assert.DoesNotContain("font-weight", italic, StringComparison.Ordinal);
        Assert.Contains("font-style=\"italic\"", italic, StringComparison.Ordinal);
        Assert.Contains("font-weight=\"bold\"", boldItalic, StringComparison.Ordinal);
        Assert.Contains("font-style=\"italic\"", boldItalic, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A box without an appearance renders its name exactly as before: bold, upright.
    /// </summary>
    [Fact]
    public void BoxName_DefaultAppearance_EmitsLegacyBoldMarkup()
    {
        var withoutAppearance = TextElementFor(Render(Box(BoxShape.Rectangle, null)), "Name");
        var defaultAppearance = TextElementFor(Render(Box(BoxShape.Rectangle, new BoxAppearance())), "Name");

        Assert.Contains("font-weight=\"bold\"", withoutAppearance, StringComparison.Ordinal);
        Assert.DoesNotContain("font-style", withoutAppearance, StringComparison.Ordinal);
        Assert.Equal(withoutAppearance, defaultAppearance);
    }

    /// <summary>
    ///     The keyword is italic by default and upright when the appearance asks for a regular keyword.
    /// </summary>
    [Fact]
    public void BoxKeyword_DefaultItalicAndRegularOverride()
    {
        var legacy = TextElementFor(Render(Box(BoxShape.Rectangle, null, keyword: "part def")), "«part def»");
        var regular = TextElementFor(Render(Box(BoxShape.Rectangle, new BoxAppearance(KeywordStyle: TextStyle.Regular), keyword: "part def")), "«part def»");
        var bold = TextElementFor(Render(Box(BoxShape.Rectangle, new BoxAppearance(KeywordStyle: TextStyle.Bold), keyword: "part def")), "«part def»");

        Assert.Contains("font-style=\"italic\"", legacy, StringComparison.Ordinal);
        Assert.DoesNotContain("font-style", regular, StringComparison.Ordinal);
        Assert.DoesNotContain("font-weight", regular, StringComparison.Ordinal);
        Assert.Contains("font-weight=\"bold\"", bold, StringComparison.Ordinal);
    }

    // ── Compartment titles ─────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     A centered compartment title is anchored in the middle at the centre of the content area
    ///     (between the left and right content insets) and is italic and not bold when asked.
    /// </summary>
    [Fact]
    public void CompartmentTitle_Centered_UsesMiddleAnchorAtContentCentre()
    {
        var compartment = new LayoutCompartment("attributes", ["x : Real"], TextAlign.Center, TextStyle.Italic);
        var box = Box(BoxShape.Rectangle, null, compartments: [compartment]) with { ContentInsetLeft = 10, ContentInsetRight = 30 };

        var title = TextElementFor(Render(box), "attributes");

        var expectedX = box.X + 10 + ((box.Width - 10 - 30) / 2.0);
        Assert.Contains("text-anchor=\"middle\"", title, StringComparison.Ordinal);
        Assert.Contains($"x=\"{F(expectedX)}\"", title, StringComparison.Ordinal);
        Assert.Contains("font-style=\"italic\"", title, StringComparison.Ordinal);
        Assert.DoesNotContain("font-weight", title, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A compartment without an explicit alignment or style keeps the legacy left-aligned bold-italic
    ///     title.
    /// </summary>
    [Fact]
    public void CompartmentTitle_Default_IsLegacyBoldItalicStart()
    {
        var box = Box(BoxShape.Rectangle, null, compartments: [new LayoutCompartment("attributes", [])]);

        var title = TextElementFor(Render(box), "attributes");

        Assert.Contains("text-anchor=\"start\"", title, StringComparison.Ordinal);
        Assert.Contains("font-weight=\"bold\" font-style=\"italic\"", title, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The separator above a compartment spans the full box width, whatever the title alignment.
    /// </summary>
    [Fact]
    public void CompartmentSeparator_WithCenteredTitle_SpansFullBoxWidth()
    {
        var compartment = new LayoutCompartment("attributes", [], TextAlign.Center, TextStyle.Italic);
        var box = Box(BoxShape.Rectangle, null, compartments: [compartment]);

        var svg = Render(box);

        Assert.Contains($"<line x1=\"{F(box.X)}\"", svg, StringComparison.Ordinal);
        Assert.Contains($"x2=\"{F(box.X + box.Width)}\"", svg, StringComparison.Ordinal);
    }

    // ── Folder name in tab ─────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     With the tab placement the folder's name is a <c>&lt;text&gt;</c> element positioned inside the
    ///     tab rectangle, and no name is drawn in the body.
    /// </summary>
    [Fact]
    public void FolderNameInTab_DrawsNameInsideTab()
    {
        var theme = Themes.Light;
        var box = Box(BoxShape.Folder, new BoxAppearance(NameStyle: TextStyle.Regular, FolderTitle: FolderTitlePlacement.Tab), label: "Package");

        var svg = Render(box);

        var title = TextElementFor(svg, "Package");
        Assert.Single(Regex.Matches(svg, ">Package</text>"));
        var x = double.Parse(Regex.Match(title, "x=\"([^\"]+)\"").Groups[1].Value, CultureInfo.InvariantCulture);
        var y = double.Parse(Regex.Match(title, "y=\"([^\"]+)\"").Groups[1].Value, CultureInfo.InvariantCulture);
        var tabWidth = BoxMetrics.ResolveFolderTabWidth(box, theme);
        var tabHeight = BoxMetrics.ResolveFolderTabHeight(box, theme);
        Assert.InRange(x, box.X, box.X + tabWidth);
        Assert.InRange(y, box.Y, box.Y + tabHeight);
        Assert.Contains("text-anchor=\"start\"", title, StringComparison.Ordinal);
        Assert.DoesNotContain("font-weight", title, StringComparison.Ordinal);
    }

    /// <summary>
    ///     With the tab placement compartments start directly under the tab (no body title band), so the
    ///     first compartment's row sits higher than under a body-titled folder.
    /// </summary>
    [Fact]
    public void FolderNameInTab_CompartmentsStartUnderTab()
    {
        var rows = new LayoutCompartment(null, ["row"]);
        var tabbed = Box(BoxShape.Folder, new BoxAppearance(FolderTitle: FolderTitlePlacement.Tab), compartments: [rows]);
        var bodied = Box(BoxShape.Folder, null, compartments: [rows]);

        var tabbedY = double.Parse(Regex.Match(TextElementFor(Render(tabbed), "row"), "y=\"([^\"]+)\"").Groups[1].Value, CultureInfo.InvariantCulture);
        var bodiedY = double.Parse(Regex.Match(TextElementFor(Render(bodied), "row"), "y=\"([^\"]+)\"").Groups[1].Value, CultureInfo.InvariantCulture);

        Assert.True(tabbedY < bodiedY);
    }

    /// <summary>
    ///     By default a folder's name is drawn in the body below the tab, centered and bold as before.
    /// </summary>
    [Fact]
    public void FolderBodyTitle_Default_DrawsNameBelowTab()
    {
        var theme = Themes.Light;
        var box = Box(BoxShape.Folder, null, label: "Package");

        var title = TextElementFor(Render(box), "Package");

        var y = double.Parse(Regex.Match(title, "y=\"([^\"]+)\"").Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.True(y > box.Y + BoxMetrics.FolderTabHeight(theme));
        Assert.Contains("text-anchor=\"middle\"", title, StringComparison.Ordinal);
        Assert.Contains("font-weight=\"bold\"", title, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The tab placement is ignored for a non-folder shape: its name stays centered in the body.
    /// </summary>
    [Fact]
    public void FolderNameInTab_NonFolderShape_IsIgnored()
    {
        var box = Box(BoxShape.Rectangle, new BoxAppearance(FolderTitle: FolderTitlePlacement.Tab), label: "Rect");

        var title = TextElementFor(Render(box), "Rect");

        Assert.Contains("text-anchor=\"middle\"", title, StringComparison.Ordinal);
    }

    // ── Corner icon ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     The send icon is a five-vertex polygon at the box's top-left inset position; the accept icon
    ///     has a sixth vertex for its notch; no icon emits no polygon.
    /// </summary>
    [Fact]
    public void CornerIcon_SendAndAccept_EmitPolygonsWithExpectedVertexCounts()
    {
        var send = Render(Box(BoxShape.Rectangle, new BoxAppearance(CornerIcon: BoxCornerIcon.Send), label: null));
        var accept = Render(Box(BoxShape.Rectangle, new BoxAppearance(CornerIcon: BoxCornerIcon.Accept), label: null));
        var none = Render(Box(BoxShape.Rectangle, null, label: null));

        Assert.Equal(5, IconPoints(send).Count);
        Assert.Equal(6, IconPoints(accept).Count);
        Assert.DoesNotContain("<polygon", none[none.IndexOf("</defs>", StringComparison.Ordinal)..], StringComparison.Ordinal);

        var first = IconPoints(send)[0];
        Assert.Equal(20 + NotationMetrics.CornerIconInset, first.X, 2);
        Assert.Equal(20 + NotationMetrics.CornerIconInset, first.Y, 2);
    }

    /// <summary>
    ///     On a folder the corner icon starts below the tab rather than over it.
    /// </summary>
    [Fact]
    public void CornerIcon_OnFolder_StartsBelowTab()
    {
        var theme = Themes.Light;
        var svg = Render(Box(BoxShape.Folder, new BoxAppearance(CornerIcon: BoxCornerIcon.Send), label: null));

        var first = IconPoints(svg)[0];

        Assert.Equal(20 + BoxMetrics.FolderTabHeight(theme) + NotationMetrics.CornerIconInset, first.Y, 2);
    }

    private static List<(double X, double Y)> IconPoints(string svg)
    {
        // The corner icon is the only <polygon> drawn outside <defs> (marker definitions live inside it).
        var body = svg[svg.IndexOf("</defs>", StringComparison.Ordinal)..];
        var icon = Regex.Match(body, "<polygon points=\"([^\"]+)\"");
        Assert.True(icon.Success, "Expected a corner-icon polygon.");
        return icon.Groups[1].Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split(','))
            .Select(parts => (double.Parse(parts[0], CultureInfo.InvariantCulture), double.Parse(parts[1], CultureInfo.InvariantCulture)))
            .ToList();
    }

    // ── Badges ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     A thick horizontal bar is a filled rectangle whose width is the badge size and whose height is
    ///     the default thickness, centered on the badge position.
    /// </summary>
    [Fact]
    public void BadgeThickHorizontalBar_EmitsFilledRectOfSizeAndDefaultThickness()
    {
        var svg = Render(new LayoutBadge(100, 50, 60, BadgeShape.ThickHorizontalBar, null));

        Assert.Contains(
            $"<rect x=\"{F(70)}\" y=\"{F(50 - (NotationMetrics.ThickBarThickness / 2.0))}\" width=\"{F(60)}\" height=\"{F(NotationMetrics.ThickBarThickness)}\" fill=\"{Themes.Light.StrokeColor}\"/>",
            svg,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     A thick vertical bar is a filled rectangle whose height is the badge size; an explicit
    ///     thickness overrides the default.
    /// </summary>
    [Fact]
    public void BadgeThickVerticalBar_EmitsFilledRectWithCustomThickness()
    {
        var svg = Render(new LayoutBadge(100, 50, 60, BadgeShape.ThickVerticalBar, null, Thickness: 8.0));

        Assert.Contains(
            $"<rect x=\"{F(96)}\" y=\"{F(20)}\" width=\"{F(8)}\" height=\"{F(60)}\" fill=\"{Themes.Light.StrokeColor}\"/>",
            svg,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     The crossed circle is a background-filled circle plus two diagonal arms that end exactly on the
    ///     circle.
    /// </summary>
    [Fact]
    public void BadgeCrossedCircle_EmitsCircleAndTwoDiagonalsEndingOnCircle()
    {
        var svg = Render(new LayoutBadge(100, 50, 24, BadgeShape.CrossedCircle, null));

        var arm = 12 * NotationMetrics.CrossedCircleArmFraction;
        Assert.Contains($"<circle cx=\"{F(100)}\" cy=\"{F(50)}\" r=\"{F(12)}\" fill=\"{Themes.Light.BackgroundColor}\"", svg, StringComparison.Ordinal);
        Assert.Contains($"<line x1=\"{F(100 - arm)}\" y1=\"{F(50 - arm)}\" x2=\"{F(100 + arm)}\" y2=\"{F(50 + arm)}\"", svg, StringComparison.Ordinal);
        Assert.Contains($"<line x1=\"{F(100 - arm)}\" y1=\"{F(50 + arm)}\" x2=\"{F(100 + arm)}\" y2=\"{F(50 - arm)}\"", svg, StringComparison.Ordinal);
    }

    // ── Band ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     A dashed band dashes its outline rectangle; a solid band does not.
    /// </summary>
    [Fact]
    public void BandOutline_DashedAndSolid()
    {
        var dashed = Render(new LayoutBand(10, 10, 100, 60, BandOrientation.Horizontal, null, [], LineStyle.Dashed));
        var solid = Render(new LayoutBand(10, 10, 100, 60, BandOrientation.Horizontal, null, []));

        Assert.Contains("stroke-dasharray=\"6 3\"", dashed, StringComparison.Ordinal);
        Assert.DoesNotContain("stroke-dasharray", solid, StringComparison.Ordinal);
    }

    // ── Lifeline ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     A rounded lifeline header has a corner radius (the caller's, else the theme's rounded-rectangle
    ///     radius); the stem stays dashed.
    /// </summary>
    [Fact]
    public void LifelineRoundedHeader_EmitsCornerRadiusAndKeepsDashedStem()
    {
        var theme = Themes.Light;
        var themed = Render(new LayoutLifeline(100, 10, 150, "a : A", 80, 30, null, BoxShape.RoundedRectangle));
        var explicitRadius = Render(new LayoutLifeline(100, 10, 150, "a : A", 80, 30, null, BoxShape.RoundedRectangle, null, 7.0));

        var radius = NotationMetrics.RoundedRectRadius(theme);
        Assert.Contains($"rx=\"{F(radius)}\" ry=\"{F(radius)}\"", themed, StringComparison.Ordinal);
        Assert.Contains($"rx=\"{F(7)}\" ry=\"{F(7)}\"", explicitRadius, StringComparison.Ordinal);
        Assert.Contains("stroke-dasharray=\"6 3\"", themed, StringComparison.Ordinal);
    }

    /// <summary>
    ///     A lifeline keyword is drawn as a guillemet-wrapped line above the label, in the keyword style.
    /// </summary>
    [Fact]
    public void LifelineKeyword_DrawsGuillemetLineAboveLabel()
    {
        var theme = Themes.Light;
        var height = BoxMetrics.LifelineHeaderHeight(theme, hasKeyword: true);
        var svg = Render(new LayoutLifeline(100, 10, 150, "a : A", 80, height, "part"));

        var keyword = TextElementFor(svg, "«part»");
        var label = TextElementFor(svg, "a : A");
        var keywordY = double.Parse(Regex.Match(keyword, "y=\"([^\"]+)\"").Groups[1].Value, CultureInfo.InvariantCulture);
        var labelY = double.Parse(Regex.Match(label, "y=\"([^\"]+)\"").Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.True(keywordY < labelY);
        Assert.Contains("font-style=\"italic\"", keyword, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The header appearance sets a dashed header outline and a regular (not bold) label.
    /// </summary>
    [Fact]
    public void LifelineHeaderAppearance_DashedOutlineAndRegularLabel()
    {
        var appearance = new BoxAppearance(LineStyle.Dashed, NameStyle: TextStyle.Regular);
        var svg = Render(new LayoutLifeline(100, 10, 150, "a : A", 80, 30, null, BoxShape.Rectangle, appearance));

        Assert.Equal(2, Regex.Matches(svg, "stroke-dasharray=\"6 3\"").Count);
        Assert.DoesNotContain("font-weight", TextElementFor(svg, "a : A"), StringComparison.Ordinal);
    }

    /// <summary>
    ///     A lifeline built with only its legacy members renders exactly as before: a sharp header and a
    ///     bold label.
    /// </summary>
    [Fact]
    public void LifelineDefault_MatchesLegacyMarkup()
    {
        var svg = Render(new LayoutLifeline(100, 10, 150, "a : A", 80, 30));

        Assert.DoesNotContain("rx=", svg, StringComparison.Ordinal);
        Assert.Contains("font-weight=\"bold\"", TextElementFor(svg, "a : A"), StringComparison.Ordinal);
        Assert.Single(Regex.Matches(svg, "stroke-dasharray=\"6 3\""));
    }

    // ── Connector labels avoid boxes ───────────────────────────────────────────────────────────

    /// <summary>
    ///     A connector label whose midpoint would land on a leaf box is placed clear of it, so the label's
    ///     opaque background does not cut the box border.
    /// </summary>
    [Fact]
    public void LineLabel_MidpointOnLeafBox_IsPlacedClear()
    {
        var line = new LayoutLine(
            [new Point2D(0, 100), new Point2D(300, 100)],
            EndMarkerStyle.None,
            EndMarkerStyle.None,
            LineStyle.Solid,
            "[guard]");
        var box = new LayoutBox(140, 80, 20, 40, null, 0, BoxShape.Rectangle, [], []);

        var svg = Render(Themes.Light, 320, 200, box, line);

        var label = TextElementFor(svg, "[guard]");
        var x = double.Parse(Regex.Match(label, "x=\"([^\"]+)\"").Groups[1].Value, CultureInfo.InvariantCulture);
        var halfWidth = ("[guard]".Length * Themes.Light.FontSizeBody * 0.6 / 2.0) + 2.0;
        Assert.True(x + halfWidth <= box.X || x - halfWidth >= box.X + box.Width);
    }
}
