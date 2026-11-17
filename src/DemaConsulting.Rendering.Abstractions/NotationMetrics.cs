// <copyright file="NotationMetrics.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Abstractions;

/// <summary>
/// A single vertex of an end-marker decoration, expressed in tip-relative notation units (before the
/// renderer's uniform scale is applied).
/// </summary>
/// <remarks>
/// The coordinate frame is shared by both renderers so that the SVG marker and the PNG path describe
/// the identical shape: <see cref="Along"/> is the distance measured back from the line endpoint (the
/// tip) into the line, and <see cref="Across"/> is the perpendicular offset. A negative
/// <see cref="Along"/> would place the vertex ahead of the tip anchor; no bundled marker does, so a
/// glyph never reaches past the box face it is anchored on.
/// </remarks>
/// <param name="Along">Distance back from the tip along the line, in notation units.</param>
/// <param name="Across">Perpendicular offset from the line, in notation units.</param>
public readonly record struct MarkerVertex(double Along, double Across);

/// <summary>
/// The single home for all intrinsic, theme-independent notation geometry shared by the SVG and PNG
/// renderers: end-marker (arrowhead) shapes and sizes, port squares, folder-tab proportions, note
/// dog-ear folds, rounded-rectangle corner scaling, badge fractions, and the label-background inset.
/// </summary>
/// <remarks>
/// This class is the notation-geometry peer of <see cref="BoxMetrics"/>:
/// every value is either a documented primitive notation constant or a documented derivation of those
/// primitives, so a geometry literal never appears more than once in the rendering path. The canonical
/// values are the historical SVG marker values (open/triangle 10x7 with the apex on the tip anchor, diamond
/// 14x8 with refX 13, circle 10x10 r4, bar 4x12), shifted back by a stroke-aware
/// <see cref="NotationMetrics.TipRetraction(EndMarkerStyle, Theme)"/> so the painted tip lands on the line
/// endpoint; the PNG renderer derives its sizes from the same constants so
/// the two renderers draw the identical shape.
/// </remarks>
public static class NotationMetrics
{
    // ── End-marker primitives (triangle family: open chevron, hollow/filled triangle) ──────────

    /// <summary>Along-line length of a triangular end marker (chevron, hollow/filled triangle).</summary>
    public const double EndMarkerLength = 10.0;

    /// <summary>Across-line width (base span) of a triangular end marker.</summary>
    public const double EndMarkerWidth = 7.0;

    /// <summary>
    /// Along-line position, measured from the marker box origin, of the glyph's far point: the vertex
    /// (triangle apex) that lands on the tip anchor (the SVG <c>refX</c> before the stroke-aware
    /// <see cref="TipRetraction(EndMarkerStyle, Theme)"/> is added). Equal to
    /// <see cref="EndMarkerLength"/>, so the apex sits exactly on the tip anchor instead of overshooting
    /// it into the target box.
    /// </summary>
    public const double EndMarkerRefX = EndMarkerLength;

    /// <summary>Half of <see cref="EndMarkerWidth"/>: the perpendicular offset of each base corner.</summary>
    public const double EndMarkerHalfWidth = EndMarkerWidth / 2.0;

    /// <summary>
    /// Distance the triangle apex overshoots the tip anchor, equal to the marker box length minus its
    /// tip anchor (<see cref="EndMarkerLength"/> - <see cref="EndMarkerRefX"/>). It is zero: the apex
    /// lands on the anchor. The constant is retained so the notation frame stays self-describing.
    /// </summary>
    public const double EndMarkerTipOvershoot = EndMarkerLength - EndMarkerRefX;

    // ── Diamond primitives (hollow/filled diamond) ─────────────────────────────────────────────

    /// <summary>Along-line length of the diamond marker box.</summary>
    public const double DiamondLength = 14.0;

    /// <summary>Across-line width of the diamond marker box.</summary>
    public const double DiamondWidth = 8.0;

    /// <summary>
    /// Along-line position, from the diamond box origin, of the far point that lands on the line
    /// endpoint (the SVG <c>refX</c>).
    /// </summary>
    public const double DiamondRefX = 13.0;

    /// <summary>Half of <see cref="DiamondWidth"/>: the perpendicular offset of each side point.</summary>
    public const double DiamondHalfWidth = DiamondWidth / 2.0;

    /// <summary>Mid-length along-line position of the diamond side points (<see cref="DiamondLength"/> / 2).</summary>
    public const double DiamondMidX = DiamondLength / 2.0;

    /// <summary>
    /// Along-line position, from the diamond box origin, of the near point, mirrored from the far
    /// point about the mid-length (<see cref="DiamondLength"/> - <see cref="DiamondRefX"/>).
    /// </summary>
    public const double DiamondNearX = DiamondLength - DiamondRefX;

    // ── Circle primitives ──────────────────────────────────────────────────────────────────────

    /// <summary>Radius of the circle end marker.</summary>
    public const double CircleRadius = 4.0;

    /// <summary>
    /// Square box that contains the circle marker; equal to the triangular marker length so circle and
    /// triangle share one marker viewport.
    /// </summary>
    public const double CircleMarkerBox = EndMarkerLength;

    /// <summary>Centre of the circle marker box (half of <see cref="CircleMarkerBox"/>).</summary>
    public const double CircleCenter = CircleMarkerBox / 2.0;

    /// <summary>
    /// Along-line tip anchor of the circle marker: the far edge of the circle
    /// (<see cref="CircleCenter"/> + <see cref="CircleRadius"/>) sits on the line endpoint.
    /// </summary>
    public const double CircleRefX = CircleCenter + CircleRadius;

    // ── Bar primitives ─────────────────────────────────────────────────────────────────────────

    /// <summary>Along-line thickness of the bar end marker (marker box length).</summary>
    public const double BarAlong = 4.0;

    /// <summary>Across-line extent of the bar end marker (marker box height).</summary>
    public const double BarAcross = 12.0;

    /// <summary>Half of <see cref="BarAlong"/>: the bar's tip anchor along the line.</summary>
    public const double BarHalfAlong = BarAlong / 2.0;

    /// <summary>Half of <see cref="BarAcross"/>: the bar's perpendicular half-length.</summary>
    public const double BarHalf = BarAcross / 2.0;

    // ── Crossbar (redefinition: hollow triangle with a bar behind the base) ────────────────────

    /// <summary>
    /// Distance, along the line, from the triangle base to the redefinition crossbar. Per SysML v2.0
    /// section 8.2.3.6 the bar is perpendicular to the line BEHIND the triangle base (on the line side),
    /// separated from it by a small clear gap, like the dots of the typing marker.
    /// </summary>
    public const double CrossbarBaseGap = 3.5;

    /// <summary>
    /// Room reserved behind the crossbar, inside the widened marker box, for half of its stroke width
    /// (a marker is clipped to its box, so the bar must not sit on the box edge).
    /// </summary>
    public const double CrossbarStrokeRoom = 1.0;

    /// <summary>
    /// Along-line extension the crossbar adds behind the triangle base
    /// (<see cref="CrossbarBaseGap"/> + <see cref="CrossbarStrokeRoom"/>).
    /// </summary>
    public const double CrossbarExtension = CrossbarBaseGap + CrossbarStrokeRoom;

    /// <summary>Along-line length of the redefinition (hollow-triangle-with-crossbar) marker box.</summary>
    public const double CrossbarMarkerLength = EndMarkerLength + CrossbarExtension;

    /// <summary>
    /// Along-line tip anchor of the redefinition marker box: the triangle anchor
    /// (<see cref="EndMarkerRefX"/>) shifted by <see cref="CrossbarExtension"/> so the triangle lands
    /// exactly where a plain hollow triangle would and its tip stays on the target box border.
    /// </summary>
    public const double CrossbarRefX = EndMarkerRefX + CrossbarExtension;

    /// <summary>
    /// Distance back from the line endpoint (the tip) to the crossbar
    /// (<see cref="EndMarkerRefX"/> + <see cref="CrossbarBaseGap"/>).
    /// </summary>
    public const double CrossbarAlong = EndMarkerRefX + CrossbarBaseGap;

    /// <summary>
    /// Half the crossbar length, perpendicular to the line: the bar spans the triangle base
    /// (<see cref="EndMarkerHalfWidth"/>).
    /// </summary>
    public const double CrossbarHalfLength = EndMarkerHalfWidth;

    // ── Port square ────────────────────────────────────────────────────────────────────────────

    /// <summary>Half the side length of the square drawn for a port.</summary>
    public const double PortHalfSize = 4.0;

    /// <summary>Full side length of the square drawn for a port (2 x <see cref="PortHalfSize"/>).</summary>
    public const double PortSize = PortHalfSize * 2.0;

    // ── Folder tab ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Maximum folder-tab width as a fraction of the box width.</summary>
    public const double FolderTabMaxWidthFraction = 0.45;

    /// <summary>Minimum folder-tab width in logical pixels.</summary>
    public const double FolderTabMinWidth = 60.0;

    /// <summary>Approximate character-width factor (relative to body font size) used to size the folder-tab label.</summary>
    public const double FolderLabelCharWidthFactor = 0.55;

    // ── Note dog-ear fold ──────────────────────────────────────────────────────────────────────

    /// <summary>Note dog-ear fold size as a fraction of the box's shorter side.</summary>
    public const double NoteFoldFraction = 0.25;

    /// <summary>Maximum note dog-ear fold size in logical pixels.</summary>
    public const double NoteFoldMaxSize = 16.0;

    // ── Rounded rectangle ──────────────────────────────────────────────────────────────────────

    /// <summary>Factor applied to the theme corner radius for a rounded-rectangle box corner.</summary>
    public const double RoundedRectCornerFactor = 2.0;

    // ── Badge fractions ────────────────────────────────────────────────────────────────────────

    /// <summary>Inner (white) radius of a bullseye badge as a fraction of the badge radius.</summary>
    public const double BadgeBullseyeInnerFraction = 1.0 / 3.0;

    /// <summary>Bar-badge half-length as a fraction of the badge radius.</summary>
    public const double BadgeBarLengthFraction = 0.8;

    // ── Dotted-triangle primitives (feature typing and reference subsetting) ───────────────────

    /// <summary>Radius of each dot trailing a dotted hollow triangle marker.</summary>
    public const double DotRadius = 1.25;

    /// <summary>Gap between the triangle base and the first column of dots.</summary>
    public const double DotBaseGap = 2.5;

    /// <summary>Distance between the two dot columns of the reference-subsetting (2x2) marker.</summary>
    public const double DotColumnSpacing = 4.0;

    /// <summary>Perpendicular offset of each dot row from the line (each dot sits this far to either side of the line).</summary>
    public const double DotRowOffset = 2.25;

    /// <summary>
    /// Along-line extension a single column of dots adds behind the triangle base
    /// (<see cref="DotBaseGap"/> + 2 x <see cref="DotRadius"/>).
    /// </summary>
    public const double TwoDotsExtension = DotBaseGap + (2.0 * DotRadius);

    /// <summary>
    /// Along-line extension two columns of dots add behind the triangle base
    /// (<see cref="TwoDotsExtension"/> + <see cref="DotColumnSpacing"/>).
    /// </summary>
    public const double FourDotsExtension = TwoDotsExtension + DotColumnSpacing;

    /// <summary>Along-line length of the hollow-triangle-with-two-dots marker box.</summary>
    public const double TwoDotsMarkerLength = EndMarkerLength + TwoDotsExtension;

    /// <summary>Along-line length of the hollow-triangle-with-four-dots marker box.</summary>
    public const double FourDotsMarkerLength = EndMarkerLength + FourDotsExtension;

    /// <summary>
    /// Along-line tip anchor of the two-dots marker box: the triangle anchor
    /// (<see cref="EndMarkerRefX"/>) shifted by the dot extension so the triangle lands exactly where
    /// a plain hollow triangle would.
    /// </summary>
    public const double TwoDotsRefX = EndMarkerRefX + TwoDotsExtension;

    /// <summary>
    /// Along-line tip anchor of the four-dots marker box (<see cref="EndMarkerRefX"/> +
    /// <see cref="FourDotsExtension"/>).
    /// </summary>
    public const double FourDotsRefX = EndMarkerRefX + FourDotsExtension;

    // ── Circled-plus primitives (owned membership) ─────────────────────────────────────────────

    /// <summary>Radius of the circle of the circled-plus end marker.</summary>
    public const double CircledPlusRadius = 5.0;

    /// <summary>Square box that contains the circled-plus marker.</summary>
    public const double CircledPlusMarkerBox = 12.0;

    /// <summary>Centre of the circled-plus marker box (half of <see cref="CircledPlusMarkerBox"/>).</summary>
    public const double CircledPlusCenter = CircledPlusMarkerBox / 2.0;

    /// <summary>
    /// Along-line tip anchor of the circled-plus marker: the far edge of the circle
    /// (<see cref="CircledPlusCenter"/> + <see cref="CircledPlusRadius"/>) sits on the line endpoint.
    /// </summary>
    public const double CircledPlusRefX = CircledPlusCenter + CircledPlusRadius;

    /// <summary>Half the length of each plus arm; the arms span the circle diameter.</summary>
    public const double CircledPlusArmHalfLength = CircledPlusRadius;

    // ── Thick bar and crossed-circle badges (fork/join, terminate) ─────────────────────────────

    /// <summary>Default thickness, across its axis, of a thick fork/join bar badge.</summary>
    public const double ThickBarThickness = 4.0;

    /// <summary>
    /// Fraction of the circle radius, along each axis, at which an arm of the crossed-circle
    /// (terminate) badge ends, so the four arms end exactly on the circle (cos 45 degrees).
    /// </summary>
    public static readonly double CrossedCircleArmFraction = Math.Cos(Math.PI / 4.0);

    // ── Corner icon (send / accept action) ─────────────────────────────────────────────────────

    /// <summary>Width of the send/accept corner icon.</summary>
    public const double CornerIconWidth = 12.0;

    /// <summary>Height of the send/accept corner icon.</summary>
    public const double CornerIconHeight = 9.0;

    /// <summary>Inset of the corner icon from the box's left edge and from the top of its body.</summary>
    public const double CornerIconInset = 6.0;

    /// <summary>Depth of the triangular notch cut into the left edge of the accept icon.</summary>
    public const double CornerIconNotchDepth = 3.0;

    /// <summary>Width of the pointed (right) end of the send/accept icon.</summary>
    public const double CornerIconPointWidth = 4.0;

    // ── Label background filter ────────────────────────────────────────────────────────────────

    /// <summary>Negative inset (each side) of the SVG label-background filter region.</summary>
    public const double LabelBgInset = 0.05;

    /// <summary>Total label-background filter extent (1 + 2 x <see cref="LabelBgInset"/>).</summary>
    public const double LabelBgExtent = 1.0 + (2.0 * LabelBgInset);

    /// <summary>
    /// Returns the rounded-rectangle corner radius for a box: the theme corner radius scaled by
    /// <see cref="RoundedRectCornerFactor"/>.
    /// </summary>
    /// <param name="theme">Theme providing the base line corner radius.</param>
    /// <returns>The rounded-rectangle corner radius in logical pixels.</returns>
    public static double RoundedRectRadius(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return theme.LineCornerRadius * RoundedRectCornerFactor;
    }

    /// <summary>
    /// Returns the along-line length consumed by an end-marker decoration, used by the layout
    /// strategies (to reserve a clean approach) and the renderers (to clamp the final corner radius).
    /// </summary>
    /// <param name="style">The end-marker style.</param>
    /// <returns>The along-line length in notation units; zero for <see cref="EndMarkerStyle.None"/>.</returns>
    public static double AlongLineLength(EndMarkerStyle style) => style switch
    {
        EndMarkerStyle.OpenChevron => EndMarkerLength,
        EndMarkerStyle.HollowTriangle => EndMarkerLength,
        EndMarkerStyle.HollowTriangleCrossbar => CrossbarMarkerLength,
        EndMarkerStyle.FilledArrow => EndMarkerLength,
        EndMarkerStyle.HollowDiamond => DiamondLength,
        EndMarkerStyle.FilledDiamond => DiamondLength,
        EndMarkerStyle.Circle => CircleMarkerBox,
        EndMarkerStyle.Bar => BarAlong,
        EndMarkerStyle.HollowTriangleTwoDots => TwoDotsMarkerLength,
        EndMarkerStyle.HollowTriangleFourDots => FourDotsMarkerLength,
        EndMarkerStyle.CircledPlus => CircledPlusMarkerBox,
        _ => 0.0,
    };

    /// <summary>
    /// Returns the uniform scale, in pixels per notation unit, at which BOTH renderers draw every end
    /// marker: the theme stroke width. This is the SVG convention (a <c>&lt;marker&gt;</c> scales its
    /// content by the stroke width of the line that references it, the default
    /// <c>markerUnits="strokeWidth"</c>); the raster renderers apply the same factor so a PNG marker has
    /// exactly the size of the SVG marker. Layout stages reserve space with the same value.
    /// </summary>
    /// <param name="theme">Theme providing the stroke width.</param>
    /// <returns>The marker scale in pixels per notation unit.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme"/> is <see langword="null"/>.</exception>
    public static double MarkerScale(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return theme.StrokeWidth;
    }

    /// <summary>
    /// Returns the stroke width, in pixels, of a marker outline: the SVG marker content strokes at the
    /// theme stroke width in marker units, so the drawn width is the stroke width times
    /// <see cref="MarkerScale(Theme)"/>. The raster renderers use this width for marker outlines.
    /// </summary>
    /// <param name="theme">Theme providing the stroke width.</param>
    /// <returns>The marker outline width in pixels.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme"/> is <see langword="null"/>.</exception>
    public static double MarkerStrokeWidth(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return theme.StrokeWidth * MarkerScale(theme);
    }

    /// <summary>
    /// Returns the along-line length of an end-marker decoration as both renderers draw it: the marker
    /// box length (see <see cref="MarkerAnchor(EndMarkerStyle, Theme)"/>) times
    /// <see cref="MarkerScale(Theme)"/>. Layout stages use it to keep a decoration clear of the corner that
    /// precedes it, and the renderers' final-corner clamp uses it so a rounded bend never intrudes into the
    /// marker.
    /// </summary>
    /// <param name="style">The end-marker style.</param>
    /// <param name="theme">Theme providing the stroke width.</param>
    /// <returns>The rendered along-line length in logical pixels; zero for <see cref="EndMarkerStyle.None"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme"/> is <see langword="null"/>.</exception>
    public static double RenderedAlongLineLength(EndMarkerStyle style, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return MarkerAnchor(style, theme).Length * MarkerScale(theme);
    }

    /// <summary>
    /// Returns how far, in notation units, an end-marker glyph is moved BACK along the line (away from the
    /// endpoint) so that its STROKED tip - not its geometric far vertex - lands exactly on the endpoint.
    /// A stroked shape paints beyond its geometry: a bevel-joined polygon corner reaches half the stroke
    /// width times the sine of the corner's half angle past the vertex (about 0.25 notation units for the
    /// triangle family and 0.42 for the diamond at the default stroke width), and a circle, circled plus
    /// or bar reaches the full half stroke width. Without the retraction the painted tip would end past the
    /// endpoint, inside the target box.
    /// </summary>
    /// <param name="style">The end-marker style.</param>
    /// <param name="theme">Theme providing the marker stroke width (in marker units, the theme stroke width).</param>
    /// <returns>The retraction in notation units; zero for <see cref="EndMarkerStyle.None"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme"/> is <see langword="null"/>.</exception>
    public static double TipRetraction(EndMarkerStyle style, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var halfStroke = theme.StrokeWidth / 2.0;
        return style switch
        {
            EndMarkerStyle.OpenChevron or
            EndMarkerStyle.HollowTriangle or
            EndMarkerStyle.HollowTriangleCrossbar or
            EndMarkerStyle.FilledArrow or
            EndMarkerStyle.HollowTriangleTwoDots or
            EndMarkerStyle.HollowTriangleFourDots => halfStroke * BevelTipFactor(TriangleVertices()),
            EndMarkerStyle.HollowDiamond or
            EndMarkerStyle.FilledDiamond => halfStroke * BevelTipFactor(DiamondVertices()),
            EndMarkerStyle.Circle or
            EndMarkerStyle.CircledPlus or
            EndMarkerStyle.Bar => halfStroke,
            _ => 0.0,
        };
    }

    /// <summary>
    /// Returns the marker box length and the tip anchor (the SVG <c>refX</c>) of an end-marker glyph,
    /// both in notation units: the glyph's own anchor (<see cref="TriangleFamilyBox(EndMarkerStyle)"/>,
    /// <see cref="DiamondRefX"/>, <see cref="CircleRefX"/>, <see cref="BarHalfAlong"/>,
    /// <see cref="CircledPlusRefX"/>) plus <see cref="TipRetraction(EndMarkerStyle, Theme)"/>, and the
    /// box length widened if the retracted anchor would otherwise fall outside the box (so the stroked tip
    /// is never clipped). Both renderers derive the marker position from this single definition.
    /// </summary>
    /// <param name="style">The end-marker style.</param>
    /// <param name="theme">Theme providing the marker stroke width.</param>
    /// <returns>The marker box length and the retracted tip anchor; zero for <see cref="EndMarkerStyle.None"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme"/> is <see langword="null"/>.</exception>
    public static (double Length, double RefX) MarkerAnchor(EndMarkerStyle style, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var (length, refX) = style switch
        {
            EndMarkerStyle.None => (0.0, 0.0),
            EndMarkerStyle.HollowDiamond or EndMarkerStyle.FilledDiamond => (DiamondLength, DiamondRefX),
            EndMarkerStyle.Circle => (CircleMarkerBox, CircleRefX),
            EndMarkerStyle.Bar => (BarAlong, BarHalfAlong),
            EndMarkerStyle.CircledPlus => (CircledPlusMarkerBox, CircledPlusRefX),
            _ => TriangleFamilyBox(style),
        };

        var anchor = refX + TipRetraction(style, theme);
        return (Math.Max(length, anchor), anchor);
    }

    /// <summary>
    /// Returns the factor by which a bevel-joined corner paints beyond its vertex along the line, per unit
    /// of half stroke width: the sine of the half angle at the glyph's far (smallest <c>Along</c>) vertex,
    /// that is the larger across-to-length ratio of the two edges meeting there.
    /// </summary>
    /// <param name="vertices">The glyph's vertices in draw order (the cyclic adjacent vertices bound the edges).</param>
    /// <returns>The bevel tip factor in the range 0 to 1.</returns>
    private static double BevelTipFactor(IReadOnlyList<MarkerVertex> vertices)
    {
        var far = 0;
        for (var index = 1; index < vertices.Count; index++)
        {
            if (vertices[index].Along < vertices[far].Along)
            {
                far = index;
            }
        }

        var factor = 0.0;
        foreach (var adjacent in new[] { vertices[(far + 1) % vertices.Count], vertices[(far + vertices.Count - 1) % vertices.Count] })
        {
            var along = adjacent.Along - vertices[far].Along;
            var across = adjacent.Across - vertices[far].Across;
            factor = Math.Max(factor, Math.Abs(across) / Math.Sqrt((along * along) + (across * across)));
        }

        return factor;
    }

    /// <summary>
    /// Returns the half-extent, perpendicular to the line, of an end-marker decoration (the widest
    /// point of the glyph measured from the line centre).
    /// </summary>
    /// <param name="style">The end-marker style.</param>
    /// <returns>The across-line half width in notation units; zero for <see cref="EndMarkerStyle.None"/>.</returns>
    public static double AcrossHalfWidth(EndMarkerStyle style) => style switch
    {
        EndMarkerStyle.OpenChevron => EndMarkerHalfWidth,
        EndMarkerStyle.HollowTriangle => EndMarkerHalfWidth,
        EndMarkerStyle.HollowTriangleCrossbar => EndMarkerHalfWidth,
        EndMarkerStyle.FilledArrow => EndMarkerHalfWidth,
        EndMarkerStyle.HollowTriangleTwoDots => EndMarkerHalfWidth,
        EndMarkerStyle.HollowTriangleFourDots => EndMarkerHalfWidth,
        EndMarkerStyle.HollowDiamond => DiamondHalfWidth,
        EndMarkerStyle.FilledDiamond => DiamondHalfWidth,
        EndMarkerStyle.Circle => CircleRadius,
        EndMarkerStyle.Bar => BarHalf,
        EndMarkerStyle.CircledPlus => CircledPlusRadius,
        _ => 0.0,
    };

    /// <summary>
    /// Returns the tip-relative centers of the dots that trail the base of a dotted hollow triangle
    /// marker: two (one on each side of the line) for
    /// <see cref="EndMarkerStyle.HollowTriangleTwoDots"/>, four (a 2x2 grid) for
    /// <see cref="EndMarkerStyle.HollowTriangleFourDots"/>, and none for every other style.
    /// </summary>
    /// <param name="style">The end-marker style.</param>
    /// <returns>The dot centers in draw order; empty when the style carries no dots.</returns>
    public static IReadOnlyList<MarkerVertex> DotCenters(EndMarkerStyle style)
    {
        var firstColumn = EndMarkerRefX + DotBaseGap + DotRadius;
        switch (style)
        {
            case EndMarkerStyle.HollowTriangleTwoDots:
                return
                [
                    new MarkerVertex(firstColumn, -DotRowOffset),
                    new MarkerVertex(firstColumn, DotRowOffset),
                ];

            case EndMarkerStyle.HollowTriangleFourDots:
                var secondColumn = firstColumn + DotColumnSpacing;
                return
                [
                    new MarkerVertex(firstColumn, -DotRowOffset),
                    new MarkerVertex(firstColumn, DotRowOffset),
                    new MarkerVertex(secondColumn, -DotRowOffset),
                    new MarkerVertex(secondColumn, DotRowOffset),
                ];

            default:
                return [];
        }
    }

    /// <summary>
    /// Returns the marker-box length and the tip anchor (<c>refX</c>) of the hollow-triangle family,
    /// widened behind the base for the dotted variants.
    /// </summary>
    /// <param name="style">A hollow-triangle-family style.</param>
    /// <returns>The marker-box length and tip anchor along the line.</returns>
    public static (double Length, double RefX) TriangleFamilyBox(EndMarkerStyle style) => style switch
    {
        EndMarkerStyle.HollowTriangleTwoDots => (TwoDotsMarkerLength, TwoDotsRefX),
        EndMarkerStyle.HollowTriangleFourDots => (FourDotsMarkerLength, FourDotsRefX),
        EndMarkerStyle.HollowTriangleCrossbar => (CrossbarMarkerLength, CrossbarRefX),
        _ => (EndMarkerLength, EndMarkerRefX),
    };

    /// <summary>
    /// Returns the icon-local polygon of a send/accept corner icon: x increases to the right from the
    /// icon's left edge (0 to <see cref="CornerIconWidth"/>), y increases downward from its top
    /// (0 to <see cref="CornerIconHeight"/>). The send icon is a convex pentagon pointing right; the
    /// accept icon is the same pentagon with a triangular notch cut into its left edge.
    /// </summary>
    /// <param name="icon">The corner icon.</param>
    /// <returns>The polygon vertices in draw order; empty for <see cref="BoxCornerIcon.None"/>.</returns>
    public static IReadOnlyList<MarkerVertex> CornerIconVertices(BoxCornerIcon icon)
    {
        const double MidY = CornerIconHeight / 2.0;
        const double ShoulderX = CornerIconWidth - CornerIconPointWidth;
        switch (icon)
        {
            case BoxCornerIcon.Send:
                return
                [
                    new MarkerVertex(0.0, 0.0),
                    new MarkerVertex(ShoulderX, 0.0),
                    new MarkerVertex(CornerIconWidth, MidY),
                    new MarkerVertex(ShoulderX, CornerIconHeight),
                    new MarkerVertex(0.0, CornerIconHeight),
                ];

            case BoxCornerIcon.Accept:
                return
                [
                    new MarkerVertex(0.0, 0.0),
                    new MarkerVertex(ShoulderX, 0.0),
                    new MarkerVertex(CornerIconWidth, MidY),
                    new MarkerVertex(ShoulderX, CornerIconHeight),
                    new MarkerVertex(0.0, CornerIconHeight),
                    new MarkerVertex(CornerIconNotchDepth, MidY),
                ];

            default:
                return [];
        }
    }

    /// <summary>
    /// Returns the triangle vertices (base corner, apex, base corner) in tip-relative notation units,
    /// shared by the open chevron, hollow triangle, and filled arrow markers. The apex lands on the tip
    /// anchor (<c>Along == 0</c>); the renderers retract the whole glyph by
    /// <see cref="TipRetraction(EndMarkerStyle, Theme)"/> so the STROKED tip lands on the line endpoint.
    /// </summary>
    /// <returns>The three triangle vertices in draw order.</returns>
    public static IReadOnlyList<MarkerVertex> TriangleVertices() =>
    [
        new MarkerVertex(EndMarkerRefX, -EndMarkerHalfWidth),
        new MarkerVertex(EndMarkerRefX - EndMarkerLength, 0.0),
        new MarkerVertex(EndMarkerRefX, EndMarkerHalfWidth),
    ];

    /// <summary>
    /// Returns the diamond vertices (near, side, far, side) in tip-relative notation units, shared by
    /// the hollow and filled diamond markers. The far point lands on the line endpoint.
    /// </summary>
    /// <returns>The four diamond vertices in draw order.</returns>
    public static IReadOnlyList<MarkerVertex> DiamondVertices() =>
    [
        new MarkerVertex(DiamondRefX - DiamondNearX, 0.0),
        new MarkerVertex(DiamondRefX - DiamondMidX, -DiamondHalfWidth),
        new MarkerVertex(0.0, 0.0),
        new MarkerVertex(DiamondRefX - DiamondMidX, DiamondHalfWidth),
    ];

    /// <summary>
    /// Returns the outline vertices of a <see cref="BoxShape.Diamond"/> inscribed in the box
    /// <c>(x, y, width, height)</c>: the midpoints of the top, right, bottom, and left box edges, in
    /// that order. Both renderers draw their diamond outline through these points, so the SVG polygon
    /// and the raster path cannot drift apart, and the layout's anchor geometry (the sloped outline the
    /// connectors attach to) is the same shape.
    /// </summary>
    /// <param name="x">Left edge of the bounding box.</param>
    /// <param name="y">Top edge of the bounding box.</param>
    /// <param name="width">Width of the bounding box.</param>
    /// <param name="height">Height of the bounding box.</param>
    /// <returns>The four vertices in order top, right, bottom, left.</returns>
    public static IReadOnlyList<Point2D> DiamondBoxVertices(double x, double y, double width, double height)
    {
        var midX = x + (width / 2.0);
        var midY = y + (height / 2.0);
        return
        [
            new Point2D(midX, y),
            new Point2D(x + width, midY),
            new Point2D(midX, y + height),
            new Point2D(x, midY),
        ];
    }
}
