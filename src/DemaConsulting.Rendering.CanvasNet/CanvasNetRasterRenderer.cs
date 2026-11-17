// <copyright file="CanvasNetRasterRenderer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Numerics;

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Rendering;
using DemaConsulting.Rendering.Abstractions;

using CanvasRenderer = DemaConsulting.CanvasNet.Rendering.Canvas;
using CanvasTextAlign = DemaConsulting.CanvasNet.Rendering.TextAlign;
using GeometryPath = DemaConsulting.CanvasNet.Geometry.Path;
using ModelTextAlign = DemaConsulting.Rendering.TextAlign;

namespace DemaConsulting.Rendering.CanvasNet;

/// <summary>
/// Abstract base for CanvasNet raster renderers: draws a <see cref="LayoutTree"/> onto a managed
/// <see cref="Surface"/> and lets a derived renderer encode it in a concrete image format supplied
/// by that renderer (PNG or JPEG).
/// </summary>
/// <remarks>
/// The renderer is pure and stateless: each call to <see cref="Render"/> allocates a new
/// <see cref="Surface"/>, draws all nodes, and encodes the result to the output stream before
/// disposing the surface. The output stream is not closed or flushed by this renderer; the caller
/// is responsible for its lifetime.
///
/// Node rendering:
/// - <see cref="LayoutBox"/> → filled rectangle (plain or rounded) + optional centered label
///   + compartment dividers and text rows; children rendered recursively.
/// - <see cref="LayoutLine"/> → corner-radius-aware polyline built as a single
///   <see cref="DemaConsulting.CanvasNet.Geometry.Path"/>
///   with optional dashing; end markers at both ends; optional midpoint label with a background
///   plate in the theme background color.
/// - <see cref="LayoutLabel"/> → text element with <see cref="ModelTextAlign"/>-derived alignment.
/// - <see cref="LayoutPort"/> → small filled square centered at the port position with optional
///   label offset away from the attached edge.
/// - <see cref="LayoutBadge"/> → icon shape (filled circle, bullseye, diamond, or bar) centered at
///   the badge position with optional label to the right.
/// - <see cref="LayoutBand"/> → swim-lane rectangle; label rendered vertically on the left edge for
///   Horizontal orientation or horizontally at the top for Vertical; children rendered recursively.
/// - <see cref="LayoutLifeline"/> → header box at the top with a dashed vertical stem below.
/// - <see cref="LayoutActivation"/> → narrow white-filled rectangle with stroke border centered at
///   <c>CentreX</c>.
/// - <see cref="LayoutGrid"/> → bordered table; header rows use depth-1 fill color, body rows use
///   depth-0 fill color; per-cell text alignment respected.
/// - All other node types are silently skipped for forward compatibility.
/// </remarks>
internal abstract class CanvasNetRasterRenderer : IRenderer
{
    /// <summary>
    /// Stroke width, in logical pixels (before scale), of the contrasting outline drawn around a
    /// port glyph square. Distinguishes the port glyph from an arrowhead marker that may land on or
    /// near the same box edge, so the two remain visually distinct instead of merging into a single
    /// blob.
    /// </summary>
    private const float PortGlyphStrokeWidth = 1.0f;

    /// <summary>
    /// Opaque white fill used by shapes whose semantics are intentionally theme-independent in the
    /// CanvasNet renderer: activation bars and the inner bullseye circle.
    /// </summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <inheritdoc/>
    public abstract string MediaType { get; }

    /// <inheritdoc/>
    public abstract string DefaultExtension { get; }

    /// <inheritdoc/>
    public abstract IReadOnlyList<string> FileExtensions { get; }

    /// <summary>
    /// Draws the layout tree to a fresh <see cref="Surface"/> sized to include both the declared
    /// layout bounds and every placed connector midpoint label.
    /// </summary>
    /// <param name="layout">The layout tree describing all nodes to render.</param>
    /// <param name="options">Render options including theme and scale.</param>
    /// <returns>
    /// A newly allocated <see cref="Surface"/> containing the fully rendered diagram. The caller
    /// owns the returned surface and must dispose it.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layout"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    internal static Surface RenderToSurface(LayoutTree layout, RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(options);

        var lines = CollectLines(layout.Nodes).ToList();
        var labelPositions = ConnectorLabelPlacer.Place(lines, options.Theme.FontSizeBody, CollectBoxes(layout.Nodes), NotationMetrics.MarkerScale(options.Theme));

        // Connection end labels are placed after the midpoint labels, which they yield to.
        // Port glyphs and outward port labels are drawn content too, so end labels keep clear of them.
        var allPorts = new List<LayoutPort>();
        CollectPorts(layout.Nodes, allPorts);
        var portRects = PortOccupiedRects(allPorts, options.Theme);
        var endLabelPlacements = ConnectorLabelPlacer.PlaceEndLabels(
            lines,
            options.Theme.FontSizeBody,
            CollectBoxes(layout.Nodes),
            NotationMetrics.MarkerScale(options.Theme),
            labelPositions.Values.Concat(portRects));

        var width = Math.Max(1, (int)Math.Ceiling(layout.Width * options.Scale));
        var height = Math.Max(1, (int)Math.Ceiling(layout.Height * options.Scale));
        foreach (var placement in labelPositions.Values)
        {
            width = Math.Max(width, (int)Math.Ceiling((placement.X + placement.HalfWidth) * options.Scale));
            height = Math.Max(height, (int)Math.Ceiling((placement.Y + placement.HalfHeight) * options.Scale));
        }

        foreach (var placement in endLabelPlacements)
        {
            width = Math.Max(width, (int)Math.Ceiling((placement.X + placement.HalfWidth) * options.Scale));
            height = Math.Max(height, (int)Math.Ceiling((placement.Y + placement.HalfHeight) * options.Scale));
        }

        // An outward port label hangs outside its box and can extend past the declared surface.
        foreach (var port in allPorts.Where(NotationMetrics.IsOutwardLabel))
        {
            var bounds = NotationMetrics.PortOutwardLabelBounds(port, port.ExternalLabel!, options.Theme);
            width = Math.Max(width, (int)Math.Ceiling((bounds.X + bounds.HalfWidth) * options.Scale));
            height = Math.Max(height, (int)Math.Ceiling((bounds.Y + bounds.HalfHeight) * options.Scale));
        }

        var surface = new Surface(width, height);
        surface.Clear(ParseColor(options.Theme.BackgroundColor));

        var canvas = new CanvasRenderer(surface);

        // A marker end that sits on a port glyph is shortened to the glyph's outer edge first, so the
        // arrowhead is not hidden under the (later-drawn) port square.
        foreach (var node in ShortenPortEnds(layout.Nodes))
        {
            RenderNode(canvas, node, options);
        }

        foreach (var line in lines)
        {
            if (line.MidpointLabel is not null && labelPositions.TryGetValue(line, out var position))
            {
                RenderLineLabel(canvas, line, options, position.X, position.Y);
            }
        }

        foreach (var placement in endLabelPlacements)
        {
            RenderEndLabel(canvas, placement, options);
        }

        return surface;
    }

    /// <inheritdoc/>
    public void Render(LayoutTree layout, RenderOptions options, Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);

        using var surface = RenderToSurface(layout, options);
        Save(surface, output);
    }

    /// <summary>
    /// Encodes a rendered <see cref="Surface"/> to <paramref name="output"/> in the derived
    /// renderer's concrete image format.
    /// </summary>
    /// <param name="surface">Rendered surface to encode.</param>
    /// <param name="output">Destination stream that receives the encoded bytes.</param>
    protected abstract void Save(Surface surface, Stream output);

    /// <summary>
    /// Resolves the Noto Sans font matching the requested weight and style.
    /// </summary>
    /// <param name="bold">When <see langword="true"/>, selects the bold typeface variant.</param>
    /// <param name="italic">When <see langword="true"/>, selects the italic typeface variant.</param>
    /// <returns>The shared <see cref="TrueTypeFont"/> instance for the requested style.</returns>
    private static TrueTypeFont CreateFont(bool bold, bool italic) => CanvasNetTypefaces.Resolve(bold, italic);

    /// <summary>
    /// Measures a text run at the requested size using CanvasNet's font metrics API.
    /// </summary>
    /// <param name="text">Text to measure.</param>
    /// <param name="font">Typeface used for the measurement.</param>
    /// <param name="fontSize">Font size in scaled pixels.</param>
    /// <returns>The measured text metrics.</returns>
    private static TextMetrics MeasureText(string text, TrueTypeFont font, float fontSize) =>
        TextRenderer.MeasureText(text, font, fontSize);

    /// <summary>
    /// Computes a reduced font size that fits <paramref name="text"/> within
    /// <paramref name="availableWidth"/> scaled pixels by scaling down proportionally. Returns
    /// <paramref name="maxFontSize"/> unchanged when the text already fits or when there is no
    /// meaningful width constraint.
    /// </summary>
    /// <param name="font">Typeface used to measure the text width.</param>
    /// <param name="text">Text whose rendered width is measured.</param>
    /// <param name="availableWidth">Maximum allowed width in scaled pixels. 0 or negative disables shrinking.</param>
    /// <param name="maxFontSize">Preferred (maximum) font size in scaled pixels.</param>
    /// <returns>Font size in scaled pixels, guaranteed to be greater than zero.</returns>
    private static float FitFontSize(TrueTypeFont font, string text, float availableWidth, float maxFontSize)
    {
        if (availableWidth <= 0 || string.IsNullOrEmpty(text))
        {
            return maxFontSize;
        }

        var measuredWidth = MeasureText(text, font, maxFontSize).Width;
        if (measuredWidth <= availableWidth)
        {
            return maxFontSize;
        }

        return maxFontSize * (availableWidth / measuredWidth);
    }

    /// <summary>
    /// Parses a theme color expressed as a hexadecimal string into CanvasNet's RGBA color value.
    /// </summary>
    /// <param name="hex">Hexadecimal color string in <c>#RRGGBB</c> or <c>#AARRGGBB</c> form.</param>
    /// <returns>The parsed <see cref="Rgba32"/> value.</returns>
    private static Rgba32 ParseColor(string hex) => Rgba32.Parse(hex);

    /// <summary>
    /// Converts the rendering model's horizontal text alignment to the CanvasNet equivalent.
    /// </summary>
    /// <param name="align">Model alignment value.</param>
    /// <returns>The corresponding CanvasNet alignment.</returns>
    private static CanvasTextAlign ToCanvasTextAlign(ModelTextAlign align) => align switch
    {
        ModelTextAlign.Center => CanvasTextAlign.Center,
        ModelTextAlign.Right => CanvasTextAlign.Right,
        _ => CanvasTextAlign.Left,
    };

    /// <summary>
    /// Strokes a single line segment by building a two-point path and drawing it with the supplied
    /// style. Centralizes the low-level open-path plumbing used throughout the raster renderer.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="startX">Scaled X coordinate of the segment start.</param>
    /// <param name="startY">Scaled Y coordinate of the segment start.</param>
    /// <param name="endX">Scaled X coordinate of the segment end.</param>
    /// <param name="endY">Scaled Y coordinate of the segment end.</param>
    /// <param name="style">Stroke style to apply.</param>
    /// <param name="color">Stroke color.</param>
    private static void StrokeLine(
        CanvasRenderer canvas,
        float startX,
        float startY,
        float endX,
        float endY,
        StrokeStyle style,
        Rgba32 color)
    {
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(startX, startY));
        builder.LineTo(new Vector2(endX, endY));
        canvas.StrokePath(builder.Build(), style, color);
    }

    /// <summary>
    /// Dispatches a single <see cref="LayoutNode"/> to the appropriate typed render method.
    /// Unknown concrete types are silently skipped so that future node types do not break existing
    /// callers.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="node">Node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderNode(CanvasRenderer canvas, LayoutNode node, RenderOptions options)
    {
        switch (node)
        {
            case LayoutBox box:
                RenderBox(canvas, box, options);
                break;

            case LayoutLine line:
                RenderLine(canvas, line, options);
                break;

            case LayoutLabel label:
                RenderLabel(canvas, label, options);
                break;

            case LayoutPort port:
                RenderPort(canvas, port, options);
                break;

            case LayoutBadge badge:
                RenderBadge(canvas, badge, options);
                break;

            case LayoutBand band:
                RenderBand(canvas, band, options);
                break;

            case LayoutLifeline lifeline:
                RenderLifeline(canvas, lifeline, options);
                break;

            case LayoutActivation activation:
                RenderActivation(canvas, activation, options);
                break;

            case LayoutGrid grid:
                RenderGrid(canvas, grid, options);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutBox"/> as a filled and stroked rectangle — plain or rounded
    /// depending on <see cref="BoxShape"/> — with an optional centered label, compartment dividers,
    /// and rows, then recursively renders its children.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="box">Box node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderBox(CanvasRenderer canvas, LayoutBox box, RenderOptions options)
    {
        var theme = options.Theme;
        var strokeColor = ParseColor(theme.StrokeColor);
        var fillColor = ParseColor(theme.DepthFillColors[box.Depth % theme.DepthFillColors.Count]);

        RenderBoxOutline(canvas, box, options, fillColor, strokeColor);
        RenderBoxCornerIcon(canvas, box, options, strokeColor);
        RenderBoxTitle(canvas, box, options, strokeColor);

        if (box.Compartments.Count > 0)
        {
            RenderBoxCompartments(canvas, box, options, strokeColor);
        }

        foreach (var child in box.Children)
        {
            RenderNode(canvas, child, options);
        }
    }

    /// <summary>
    /// Draws the fill and border of a <see cref="LayoutBox"/>, selecting geometry based on
    /// <see cref="LayoutBox.Shape"/>.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="box">Box whose outline is drawn.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    /// <param name="fillColor">Fill color for the interior.</param>
    /// <param name="strokeColor">Stroke color for the border.</param>
    private static void RenderBoxOutline(
        CanvasRenderer canvas,
        LayoutBox box,
        RenderOptions options,
        Rgba32 fillColor,
        Rgba32 strokeColor)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var dashArray = DashArrayFor(box.Appearance?.OutlineStyle ?? LineStyle.Solid, scale);
        var strokeStyle = new StrokeStyle((float)theme.StrokeWidth * scale, dashArray: dashArray);

        var x = (float)(box.X * scale);
        var y = (float)(box.Y * scale);
        var width = (float)(box.Width * scale);
        var height = (float)(box.Height * scale);

        switch (box.Shape)
        {
            case BoxShape.Folder:
                var folderPath = BuildFolderPath(box, theme, scale);
                canvas.FillPath(folderPath, fillColor);
                canvas.StrokePath(folderPath, strokeStyle, strokeColor);
                break;

            case BoxShape.Note:
                RenderNoteOutline(canvas, box, scale, strokeStyle, fillColor, strokeColor);
                break;

            case BoxShape.Diamond:
                var diamondPath = BuildDiamondPath(x, y, width, height);
                canvas.FillPath(diamondPath, fillColor);
                canvas.StrokePath(diamondPath, strokeStyle, strokeColor);
                break;

            case BoxShape.RoundedRectangle when ResolveRoundedCornerRadius(box, theme) > 0:
                var cornerRadius = (float)(ResolveRoundedCornerRadius(box, theme) * scale);
                canvas.FillRoundRect(x, y, width, height, cornerRadius, fillColor);
                if (dashArray is null)
                {
                    canvas.StrokeRoundRect(x, y, width, height, cornerRadius, strokeStyle, strokeColor);
                }
                else
                {
                    // Dashed outlines are stroked as a path so the dash pattern is honored
                    canvas.StrokePath(BuildRoundRectPath(x, y, width, height, cornerRadius), strokeStyle, strokeColor);
                }

                break;

            default:
                canvas.FillRect(x, y, width, height, fillColor);
                if (dashArray is null)
                {
                    canvas.StrokeRect(x, y, width, height, strokeStyle, strokeColor);
                }
                else
                {
                    canvas.StrokePath(BuildRoundRectPath(x, y, width, height, 0f), strokeStyle, strokeColor);
                }

                break;
        }
    }

    /// <summary>
    /// Returns the stroke dash array, in scaled pixels, for a line style: <c>6 3</c> for dashed,
    /// <c>2 2</c> for dotted, and <see langword="null"/> (a solid stroke) otherwise.
    /// </summary>
    /// <param name="style">The stroke style.</param>
    /// <param name="scale">Uniform scale factor.</param>
    /// <returns>The dash array, or <see langword="null"/> for a solid stroke.</returns>
    private static float[]? DashArrayFor(LineStyle style, float scale) => style switch
    {
        LineStyle.Dashed => [6f * scale, 3f * scale],
        LineStyle.Dotted => [2f * scale, 2f * scale],
        _ => null,
    };

    /// <summary>
    /// Builds a closed rectangle path, with rounded corners when <paramref name="radius"/> is positive,
    /// starting at the top edge so a dash pattern begins with ink along the top. Used to stroke dashed
    /// outlines, which the dedicated rectangle strokers do not dash.
    /// </summary>
    /// <param name="x">Scaled left edge.</param>
    /// <param name="y">Scaled top edge.</param>
    /// <param name="width">Scaled width.</param>
    /// <param name="height">Scaled height.</param>
    /// <param name="radius">Scaled corner radius; zero or negative for square corners.</param>
    /// <returns>The closed outline path.</returns>
    private static GeometryPath BuildRoundRectPath(float x, float y, float width, float height, float radius)
    {
        var r = Math.Min(Math.Max(radius, 0f), Math.Min(width, height) / 2f);
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(x + r, y));
        builder.LineTo(new Vector2(x + width - r, y));
        if (r > 0f)
        {
            builder.TangentArcTo(new Vector2(x + width, y), new Vector2(x + width, y + r), r);
        }

        builder.LineTo(new Vector2(x + width, y + height - r));
        if (r > 0f)
        {
            builder.TangentArcTo(new Vector2(x + width, y + height), new Vector2(x + width - r, y + height), r);
        }

        builder.LineTo(new Vector2(x + r, y + height));
        if (r > 0f)
        {
            builder.TangentArcTo(new Vector2(x, y + height), new Vector2(x, y + height - r), r);
        }

        builder.LineTo(new Vector2(x, y + r));
        if (r > 0f)
        {
            builder.TangentArcTo(new Vector2(x, y), new Vector2(x + r, y), r);
        }

        builder.Close();
        return builder.Build();
    }

    /// <summary>
    /// Builds the closed diamond outline path through <see cref="NotationMetrics.DiamondBoxVertices"/>,
    /// starting at the top vertex so a dash pattern begins there. Sharing the vertex helper with the SVG
    /// renderer keeps the two outlines identical.
    /// </summary>
    /// <param name="x">Scaled left edge of the bounding box.</param>
    /// <param name="y">Scaled top edge of the bounding box.</param>
    /// <param name="width">Scaled width of the bounding box.</param>
    /// <param name="height">Scaled height of the bounding box.</param>
    /// <returns>The closed diamond path.</returns>
    private static GeometryPath BuildDiamondPath(float x, float y, float width, float height)
    {
        var vertices = NotationMetrics.DiamondBoxVertices(x, y, width, height);
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2((float)vertices[0].X, (float)vertices[0].Y));
        for (var i = 1; i < vertices.Count; i++)
        {
            builder.LineTo(new Vector2((float)vertices[i].X, (float)vertices[i].Y));
        }

        builder.Close();
        return builder.Build();
    }

    /// <summary>
    /// Builds the folder outline path (a tab at the top-left above a full-width body).
    /// </summary>
    /// <param name="box">Folder-shaped box whose outline is built.</param>
    /// <param name="theme">Theme providing fallback folder-tab metrics.</param>
    /// <param name="scale">Uniform scale factor.</param>
    /// <returns>The folder outline path.</returns>
    private static GeometryPath BuildFolderPath(LayoutBox box, Theme theme, float scale)
    {
        var tabHeight = ResolveFolderTabHeight(box, theme);
        var tabWidth = ResolveFolderTabWidth(box, theme);

        var x = (float)(box.X * scale);
        var yTab = (float)(box.Y * scale);
        var yBody = (float)((box.Y + tabHeight) * scale);
        var xTabRight = (float)((box.X + tabWidth) * scale);
        var xRight = (float)((box.X + box.Width) * scale);
        var yBottom = (float)((box.Y + box.Height) * scale);

        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(x, yBody));
        builder.LineTo(new Vector2(x, yTab));
        builder.LineTo(new Vector2(xTabRight, yTab));
        builder.LineTo(new Vector2(xTabRight, yBody));
        builder.LineTo(new Vector2(xRight, yBody));
        builder.LineTo(new Vector2(xRight, yBottom));
        builder.LineTo(new Vector2(x, yBottom));
        builder.Close();
        return builder.Build();
    }

    /// <summary>
    /// Resolves the rounded-corner radius for a box, preferring a caller-supplied placed-box value
    /// so routing and rendering can agree on the exact outline geometry. A negative caller-supplied
    /// value is clamped to zero.
    /// </summary>
    /// <param name="box">Box whose rounded-corner radius is resolved.</param>
    /// <param name="theme">Theme providing the fallback line-corner radius.</param>
    /// <returns>The rounded-corner radius in logical pixels.</returns>
    private static double ResolveRoundedCornerRadius(LayoutBox box, Theme theme) =>
        box.RoundedCornerRadius.HasValue
            ? Math.Max(0.0, box.RoundedCornerRadius.Value)
            : NotationMetrics.RoundedRectRadius(theme);

    /// <summary>
    /// Resolves the folder-tab width for a box, preferring a caller-supplied placed-box value so
    /// routing and rendering can agree on the exact top-face geometry. A negative caller-supplied
    /// value is clamped to zero.
    /// </summary>
    /// <param name="box">Folder-shaped box whose tab width is resolved.</param>
    /// <param name="theme">Theme providing fallback font and padding metrics.</param>
    /// <returns>The folder-tab width in logical pixels.</returns>
    private static double ResolveFolderTabWidth(LayoutBox box, Theme theme) =>
        BoxMetrics.ResolveFolderTabWidth(box, theme);

    /// <summary>
    /// Resolves the folder-tab height for a box, preferring a caller-supplied placed-box value so
    /// routing and rendering can agree on the exact top-face projection offset. A negative
    /// caller-supplied value is clamped to zero.
    /// </summary>
    /// <param name="box">Folder-shaped box whose tab height is resolved.</param>
    /// <param name="theme">Theme providing fallback tab metrics.</param>
    /// <returns>The folder-tab height in logical pixels.</returns>
    private static double ResolveFolderTabHeight(LayoutBox box, Theme theme) =>
        BoxMetrics.ResolveFolderTabHeight(box, theme);

    /// <summary>
    /// Resolves the top Y coordinate (unscaled) of the title and label area for a box. For a
    /// <see cref="BoxShape.Folder"/> outline, the title area is recessed below the tab so that
    /// keyword, label text, and compartments never overlap the otherwise empty tab notch.
    /// </summary>
    /// <param name="box">Box whose title-area origin is resolved.</param>
    /// <param name="theme">Theme providing fallback folder-tab metrics.</param>
    /// <returns>The top Y coordinate of the title area in logical pixels.</returns>
    private static double ResolveTitleAreaTop(LayoutBox box, Theme theme) =>
        box.Shape == BoxShape.Folder
            ? box.Y + ResolveFolderTabHeight(box, theme)
            : box.Y;

    /// <summary>
    /// Draws a note-shaped box (a rectangle with a folded-down top-right corner).
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="box">Note-shaped box to render.</param>
    /// <param name="scale">Uniform scale factor.</param>
    /// <param name="strokeStyle">Stroke style used for the outline.</param>
    /// <param name="fillColor">Interior fill color.</param>
    /// <param name="strokeColor">Outline color.</param>
    private static void RenderNoteOutline(
        CanvasRenderer canvas,
        LayoutBox box,
        float scale,
        StrokeStyle strokeStyle,
        Rgba32 fillColor,
        Rgba32 strokeColor)
    {
        var fold = BoxMetrics.NoteFoldSize(box);

        var x = (float)(box.X * scale);
        var y = (float)(box.Y * scale);
        var xRight = (float)((box.X + box.Width) * scale);
        var xFold = (float)((box.X + box.Width - fold) * scale);
        var yFold = (float)((box.Y + fold) * scale);
        var yBottom = (float)((box.Y + box.Height) * scale);

        var bodyBuilder = new PathBuilder();
        bodyBuilder.MoveTo(new Vector2(x, y));
        bodyBuilder.LineTo(new Vector2(xFold, y));
        bodyBuilder.LineTo(new Vector2(xRight, yFold));
        bodyBuilder.LineTo(new Vector2(xRight, yBottom));
        bodyBuilder.LineTo(new Vector2(x, yBottom));
        bodyBuilder.Close();
        var body = bodyBuilder.Build();
        canvas.FillPath(body, fillColor);
        canvas.StrokePath(body, strokeStyle, strokeColor);

        var cornerBuilder = new PathBuilder();
        cornerBuilder.MoveTo(new Vector2(xFold, y));
        cornerBuilder.LineTo(new Vector2(xFold, yFold));
        cornerBuilder.LineTo(new Vector2(xRight, yFold));
        canvas.StrokePath(cornerBuilder.Build(), strokeStyle, strokeColor);
    }

    /// <summary>
    /// Draws the optional keyword line and name label in the title area of a box, using the box's
    /// <see cref="BoxAppearance"/> text styles (the legacy italic keyword and bold name by default).
    /// A folder whose appearance requests the name in the tab draws the name inside the tab instead
    /// of the body.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="box">Box whose title is drawn.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    /// <param name="strokeColor">Text color.</param>
    private static void RenderBoxTitle(CanvasRenderer canvas, LayoutBox box, RenderOptions options, Rgba32 strokeColor)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var centerX = (float)((box.X + (box.Width / 2.0)) * scale);
        var cursorY = BoxMetrics.TitleCursorTop(box, theme);
        var appearance = box.Appearance ?? new BoxAppearance();

        if (box.Keyword != null)
        {
            var keywordStyle = appearance.ResolvedKeywordStyle;
            var keywordFont = CreateFont(keywordStyle.Weight == FontWeight.Bold, keywordStyle.Style == FontStyle.Italic);
            var keywordSize = (float)theme.FontSizeBody * scale;
            var keywordY = (float)((cursorY + theme.FontSizeBody) * scale);
            canvas.DrawText("\u00AB" + box.Keyword + "\u00BB", centerX, keywordY, CanvasTextAlign.Center, keywordFont, keywordSize, strokeColor);
            cursorY += theme.FontSizeBody + theme.LabelPadding;
        }

        var nameStyle = appearance.ResolvedNameStyle;
        var labelFont = CreateFont(nameStyle.Weight == FontWeight.Bold, nameStyle.Style == FontStyle.Italic);

        if (BoxMetrics.IsNameInTab(box))
        {
            var tabWidth = ResolveFolderTabWidth(box, theme);
            var tabHeight = ResolveFolderTabHeight(box, theme);
            var tabAvailable = (float)((tabWidth - (2 * theme.LabelPadding)) * scale);
            var tabSize = FitFontSize(labelFont, box.Label!, tabAvailable, (float)theme.FontSizeBody * scale);
            var tabBaseline = (float)((box.Y + ((tabHeight - theme.FontSizeBody) / 2.0) + theme.FontSizeBody) * scale);
            canvas.DrawText(box.Label!, (float)((box.X + theme.LabelPadding) * scale), tabBaseline, CanvasTextAlign.Left, labelFont, tabSize, strokeColor);
            return;
        }

        if (box.Label != null)
        {
            var maxFontSize = (float)theme.FontSizeTitle * scale;
            var availableWidth = (float)((box.Width - (2 * theme.LabelPadding)) * scale);
            var labelSize = FitFontSize(labelFont, box.Label, availableWidth, maxFontSize);
            var labelY = (float)((cursorY + theme.FontSizeTitle) * scale);
            canvas.DrawText(box.Label, centerX, labelY, CanvasTextAlign.Center, labelFont, labelSize, strokeColor);
        }
    }

    /// <summary>
    /// Draws the send/accept corner icon of a box as a polygon in the top-left corner of its body
    /// (below the tab for a folder), filled with the theme background and stroked in the theme stroke.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="box">The box whose icon is drawn.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    /// <param name="strokeColor">Outline color.</param>
    private static void RenderBoxCornerIcon(CanvasRenderer canvas, LayoutBox box, RenderOptions options, Rgba32 strokeColor)
    {
        var vertices = NotationMetrics.CornerIconVertices(box.Appearance?.CornerIcon ?? BoxCornerIcon.None);
        if (vertices.Count == 0)
        {
            return;
        }

        var theme = options.Theme;
        var scale = (float)options.Scale;
        var originX = box.X + NotationMetrics.CornerIconInset;
        var originY = ResolveTitleAreaTop(box, theme) + NotationMetrics.CornerIconInset;
        var builder = new PathBuilder();
        for (var index = 0; index < vertices.Count; index++)
        {
            var point = new Vector2(
                (float)((originX + vertices[index].Along) * scale),
                (float)((originY + vertices[index].Across) * scale));
            if (index == 0)
            {
                builder.MoveTo(point);
            }
            else
            {
                builder.LineTo(point);
            }
        }

        builder.Close();
        var path = builder.Build();
        canvas.FillPath(path, ParseColor(theme.BackgroundColor));
        canvas.StrokePath(path, new StrokeStyle((float)theme.StrokeWidth * scale), strokeColor);
    }

    /// <summary>
    /// Renders the compartments of a <see cref="LayoutBox"/> below the title area. Each
    /// compartment begins with a full-width horizontal divider, followed by an optional bold title
    /// row and then zero or more body-font text rows, each indented by <see cref="Theme.LabelPadding"/>.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="box">Box whose compartments are rendered.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    /// <param name="strokeColor">Pre-parsed stroke color reused across all compartment draws.</param>
    private static void RenderBoxCompartments(
        CanvasRenderer canvas,
        LayoutBox box,
        RenderOptions options,
        Rgba32 strokeColor)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeStyle = new StrokeStyle((float)theme.StrokeWidth * scale);

        var labelAreaHeight = BoxMetrics.TitleAreaHeight(box, theme);
        var titleAreaOccupiesSpace = labelAreaHeight > 0;
        var compartmentY = ResolveTitleAreaTop(box, theme) + box.ContentInsetTop + labelAreaHeight;

        var isFirstCompartment = true;
        foreach (var compartment in box.Compartments)
        {
            if (!isFirstCompartment || titleAreaOccupiesSpace)
            {
                var dividerY = box.Shape == BoxShape.Note
                    ? Math.Max(compartmentY, box.Y + BoxMetrics.NoteFoldSize(box))
                    : compartmentY;

                StrokeLine(
                    canvas,
                    (float)(box.X * scale),
                    (float)(dividerY * scale),
                    (float)((box.X + box.Width) * scale),
                    (float)(dividerY * scale),
                    strokeStyle,
                    strokeColor);
            }

            isFirstCompartment = false;

            if (compartment.Title != null)
            {
                var titleStyle = compartment.ResolvedTitleStyle;
                var titleFont = CreateFont(titleStyle.Weight == FontWeight.Bold, titleStyle.Style == FontStyle.Italic);
                var titleSize = (float)theme.FontSizeBody * scale;
                var centered = compartment.TitleAlign == ModelTextAlign.Center;
                var titleX = centered
                    ? (float)((box.X + box.ContentInsetLeft + ((box.Width - box.ContentInsetLeft - box.ContentInsetRight) / 2.0)) * scale)
                    : (float)((box.X + theme.LabelPadding + box.ContentInsetLeft) * scale);
                var titleY = (float)((compartmentY + theme.LabelPadding + theme.FontSizeBody) * scale);
                canvas.DrawText(compartment.Title, titleX, titleY, centered ? CanvasTextAlign.Center : CanvasTextAlign.Left, titleFont, titleSize, strokeColor);
                compartmentY += theme.LabelPadding + theme.FontSizeBody + theme.LabelPadding;
            }

            foreach (var row in compartment.Rows)
            {
                var rowFont = CreateFont(bold: false, italic: false);
                var rowSize = (float)theme.FontSizeBody * scale;
                var rowX = (float)((box.X + theme.LabelPadding + box.ContentInsetLeft) * scale);
                var rowY = (float)((compartmentY + theme.LabelPadding + theme.FontSizeBody) * scale);
                canvas.DrawText(row, rowX, rowY, CanvasTextAlign.Left, rowFont, rowSize, strokeColor);
                compartmentY += theme.LabelPadding + theme.FontSizeBody;
            }

            compartmentY += theme.LabelPadding;
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutLine"/> as a corner-radius-aware polyline. Arrowheads are drawn
    /// on top of the finished path. An optional midpoint label is centered over the line with a
    /// background plate in the theme background color.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="line">Line node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderLine(CanvasRenderer canvas, LayoutLine line, RenderOptions options)
    {
        if (line.Waypoints.Count < 2)
        {
            return;
        }

        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = ParseColor(theme.StrokeColor);

        // Every marker is drawn at the shared marker scale (the SVG marker-unit convention), so the
        // corner clamp reserves the marker's rendered length and the glyph has the SVG marker's size.
        var sourceMarkerLength = NotationMetrics.RenderedAlongLineLength(line.SourceEnd, theme);
        var targetMarkerLength = NotationMetrics.RenderedAlongLineLength(line.TargetEnd, theme);
        var markerPaint = new EndMarkerPaint(
            strokeColor,
            ParseColor(theme.BackgroundColor),
            (float)NotationMetrics.MarkerStrokeWidth(theme) * scale,
            (float)NotationMetrics.MarkerScale(theme) * scale);

        var clampCorners = (line.SourceEnd != EndMarkerStyle.None || line.TargetEnd != EndMarkerStyle.None)
            && theme.LineCornerRadius > 0
            && NeedsEndCornerClamp(line.Waypoints, theme.LineCornerRadius, sourceMarkerLength, targetMarkerLength);

        var path = clampCorners
            ? BuildClampedLinePath(line.Waypoints, theme.LineCornerRadius, scale, sourceMarkerLength, targetMarkerLength)
            : BuildSimpleLinePath(line.Waypoints, scale);

        if (!clampCorners && theme.LineCornerRadius > 0)
        {
            path = CornerRoundEffect.Apply(path, (float)(theme.LineCornerRadius * scale));
        }

        float[]? dashArray = line.LineStyle switch
        {
            LineStyle.Dashed => [6f * scale, 3f * scale],
            LineStyle.Dotted => [2f * scale, 2f * scale],
            _ => null,
        };
        var strokeStyle = new StrokeStyle((float)theme.StrokeWidth * scale, dashArray: dashArray);
        canvas.StrokePath(path, strokeStyle, strokeColor);

        if (line.SourceEnd != EndMarkerStyle.None)
        {
            var tip = line.Waypoints[0];
            var next = line.Waypoints[1];
            var (dx, dy) = ComputeDirection(next.X, next.Y, tip.X, tip.Y);

            // Move the glyph back along the line so its STROKED tip, not its geometric vertex, lands on the endpoint.
            var retraction = NotationMetrics.TipRetraction(line.SourceEnd, theme) * markerPaint.Scale;
            DrawEndMarker(
                canvas,
                (float)(tip.X * scale) - ((float)dx * (float)retraction),
                (float)(tip.Y * scale) - ((float)dy * (float)retraction),
                (float)dx,
                (float)dy,
                line.SourceEnd,
                markerPaint);
        }

        if (line.TargetEnd != EndMarkerStyle.None)
        {
            var count = line.Waypoints.Count;
            var tip = line.Waypoints[count - 1];
            var previous = line.Waypoints[count - 2];
            var (dx, dy) = ComputeDirection(previous.X, previous.Y, tip.X, tip.Y);
            var retraction = NotationMetrics.TipRetraction(line.TargetEnd, theme) * markerPaint.Scale;
            DrawEndMarker(
                canvas,
                (float)(tip.X * scale) - ((float)dx * (float)retraction),
                (float)(tip.Y * scale) - ((float)dy * (float)retraction),
                (float)dx,
                (float)dy,
                line.TargetEnd,
                markerPaint);
        }
    }

    /// <summary>
    /// Builds a plain scaled polyline path through all waypoints, used together with uniform corner
    /// rounding for lines that do not need decoration-aware corner clamping.
    /// </summary>
    /// <param name="waypoints">Ordered waypoints.</param>
    /// <param name="scale">Uniform scale factor.</param>
    /// <returns>The polyline path.</returns>
    private static GeometryPath BuildSimpleLinePath(IReadOnlyList<Point2D> waypoints, float scale)
    {
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2((float)(waypoints[0].X * scale), (float)(waypoints[0].Y * scale)));
        for (var index = 1; index < waypoints.Count; index++)
        {
            builder.LineTo(new Vector2((float)(waypoints[index].X * scale), (float)(waypoints[index].Y * scale)));
        }

        return builder.Build();
    }

    /// <summary>
    /// Returns whether a uniform rounded corner of <paramref name="cornerRadius"/> would intrude
    /// into an end-marker decoration, meaning the straight approach at a decorated end is shorter
    /// than the marker's along-line length plus the corner radius.
    /// </summary>
    /// <param name="waypoints">Ordered waypoints (unscaled).</param>
    /// <param name="cornerRadius">Unscaled line corner radius.</param>
    /// <param name="sourceMarkerLength">Rendered along-line length of the source end marker (zero without one).</param>
    /// <param name="targetMarkerLength">Rendered along-line length of the target end marker (zero without one).</param>
    /// <returns><see langword="true"/> if explicit clamped corners are required; otherwise <see langword="false"/>.</returns>
    private static bool NeedsEndCornerClamp(
        IReadOnlyList<Point2D> waypoints,
        double cornerRadius,
        double sourceMarkerLength,
        double targetMarkerLength)
    {
        if (waypoints.Count < 3)
        {
            return false;
        }

        if (sourceMarkerLength > 0.0)
        {
            var inLength = Distance(waypoints[0], waypoints[1]);
            if (inLength - sourceMarkerLength < cornerRadius)
            {
                return true;
            }
        }

        if (targetMarkerLength > 0.0)
        {
            var count = waypoints.Count;
            var outLength = Distance(waypoints[count - 2], waypoints[count - 1]);
            if (outLength - targetMarkerLength < cornerRadius)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds a scaled connector path with arc-rounded interior corners, mirroring the SVG
    /// renderer's corner-clamped geometry: the first interior corner additionally clamps its radius
    /// to <c>inLen − RenderedAlongLineLength(sourceEnd)</c> and the last to
    /// <c>outLen − RenderedAlongLineLength(targetEnd)</c>, so the rounded corner never intrudes into an end
    /// decoration.
    /// </summary>
    /// <param name="waypoints">Ordered waypoints (unscaled); at least two entries.</param>
    /// <param name="cornerRadius">Unscaled line corner radius.</param>
    /// <param name="scale">Uniform scale factor.</param>
    /// <param name="sourceMarkerLength">Rendered along-line length of the source end marker (zero without one).</param>
    /// <param name="targetMarkerLength">Rendered along-line length of the target end marker (zero without one).</param>
    /// <returns>The corner-baked connector path.</returns>
    private static GeometryPath BuildClampedLinePath(
        IReadOnlyList<Point2D> waypoints,
        double cornerRadius,
        float scale,
        double sourceMarkerLength,
        double targetMarkerLength)
    {
        var builder = new PathBuilder();
        var first = waypoints[0];
        builder.MoveTo(new Vector2((float)(first.X * scale), (float)(first.Y * scale)));

        for (var index = 1; index < waypoints.Count; index++)
        {
            var current = waypoints[index];
            var isInterior = index < waypoints.Count - 1;
            if (!isInterior)
            {
                builder.LineTo(new Vector2((float)(current.X * scale), (float)(current.Y * scale)));
                continue;
            }

            var previous = waypoints[index - 1];
            var next = waypoints[index + 1];

            var inDx = current.X - previous.X;
            var inDy = current.Y - previous.Y;
            var inLength = Math.Sqrt((inDx * inDx) + (inDy * inDy));
            var outDx = next.X - current.X;
            var outDy = next.Y - current.Y;
            var outLength = Math.Sqrt((outDx * outDx) + (outDy * outDy));

            if (inLength < 0.001 || outLength < 0.001)
            {
                builder.LineTo(new Vector2((float)(current.X * scale), (float)(current.Y * scale)));
                continue;
            }

            var inNx = inDx / inLength;
            var inNy = inDy / inLength;
            var outNx = outDx / outLength;
            var outNy = outDy / outLength;

            var radius = Math.Min(cornerRadius, Math.Min(inLength / 2.0, outLength / 2.0));
            if (index == 1)
            {
                radius = Math.Min(radius, inLength - sourceMarkerLength);
            }

            if (index == waypoints.Count - 2)
            {
                radius = Math.Min(radius, outLength - targetMarkerLength);
            }

            if (radius <= 0.0)
            {
                builder.LineTo(new Vector2((float)(current.X * scale), (float)(current.Y * scale)));
                continue;
            }

            var shortEndX = current.X - (inNx * radius);
            var shortEndY = current.Y - (inNy * radius);
            var shortStartX = current.X + (outNx * radius);
            var shortStartY = current.Y + (outNy * radius);

            builder.LineTo(new Vector2((float)(shortEndX * scale), (float)(shortEndY * scale)));
            builder.TangentArcTo(
                new Vector2((float)(current.X * scale), (float)(current.Y * scale)),
                new Vector2((float)(shortStartX * scale), (float)(shortStartY * scale)),
                (float)(radius * scale));
        }

        return builder.Build();
    }

    /// <summary>
    /// Returns the Euclidean distance between two points.
    /// </summary>
    /// <param name="a">First point.</param>
    /// <param name="b">Second point.</param>
    /// <returns>The distance.</returns>
    private static double Distance(Point2D a, Point2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    /// Draws a line's optional midpoint label in a final pass after all wires and boxes are drawn so
    /// labels are never drawn over by another wire.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="line">The line whose label is rendered.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    /// <param name="midX">Pre-computed label center X in logical pixels.</param>
    /// <param name="midY">Pre-computed label center Y in logical pixels.</param>
    private static void RenderLineLabel(CanvasRenderer canvas, LayoutLine line, RenderOptions options, double midX, double midY)
    {
        if (line.MidpointLabel is null)
        {
            return;
        }

        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = ParseColor(theme.StrokeColor);
        RenderLineMidpointLabel(canvas, midX, midY, line.MidpointLabel, theme, scale, strokeColor);
    }

    /// <summary>
    /// Draws one connection end label (a role name or multiplicity slot) centered on the position
    /// <see cref="ConnectorLabelPlacer.PlaceEndLabels"/> chose, on a background-colored rectangle like a
    /// midpoint label. The text is centered vertically on the placement, which is where the SVG renderer
    /// anchors it (<c>dominant-baseline="middle"</c>), so the two formats agree.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="placement">The resolved end-label placement.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderEndLabel(CanvasRenderer canvas, EndLabelPlacement placement, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var centreX = (float)(placement.X * scale);
        var centreY = (float)(placement.Y * scale);

        var font = CreateFont(bold: false, italic: false);
        var textSize = (float)theme.FontSizeBody * scale;
        var metrics = MeasureText(placement.Text, font, textSize);
        var padding = (float)theme.LabelPadding * scale * 0.5f;
        var backgroundWidth = metrics.Width + (2f * padding);
        var backgroundHeight = textSize + (2f * padding);
        canvas.FillRect(
            centreX - (backgroundWidth / 2f),
            centreY - (backgroundHeight / 2f),
            backgroundWidth,
            backgroundHeight,
            ParseColor(theme.BackgroundColor));

        // The baseline sits about 0.35 em below the vertical centre of the text.
        canvas.DrawText(
            placement.Text,
            centreX,
            centreY + (0.35f * textSize),
            CanvasTextAlign.Center,
            font,
            textSize,
            ParseColor(theme.StrokeColor));
    }

    /// <summary>
    /// Recursively collects all <see cref="LayoutBox"/> nodes from a node tree.
    /// </summary>
    /// <param name="nodes">Top-level nodes to walk.</param>
    /// <returns>Every box node, including those nested inside boxes or bands, parents before children.</returns>
    private static IEnumerable<LayoutBox> CollectBoxes(IReadOnlyList<LayoutNode> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case LayoutBox box:
                    yield return box;
                    foreach (var inner in CollectBoxes(box.Children))
                    {
                        yield return inner;
                    }

                    break;

                case LayoutBand band:
                    foreach (var inner in CollectBoxes(band.Children))
                    {
                        yield return inner;
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Recursively collects all <see cref="LayoutLine"/> nodes from a node tree.
    /// </summary>
    /// <param name="nodes">Top-level nodes to walk.</param>
    /// <returns>Every line node, including those nested inside boxes or bands.</returns>
    private static IEnumerable<LayoutLine> CollectLines(IReadOnlyList<LayoutNode> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case LayoutLine line:
                    yield return line;
                    break;

                case LayoutBox box:
                    foreach (var inner in CollectLines(box.Children))
                    {
                        yield return inner;
                    }

                    break;

                case LayoutBand band:
                    foreach (var inner in CollectLines(band.Children))
                    {
                        yield return inner;
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Recursively collects all <see cref="LayoutPort"/> nodes from a node tree.
    /// </summary>
    /// <param name="nodes">Top-level nodes to walk.</param>
    /// <param name="ports">Receives every port found, including those nested inside boxes or bands.</param>
    private static void CollectPorts(IReadOnlyList<LayoutNode> nodes, List<LayoutPort> ports)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case LayoutPort port:
                    ports.Add(port);
                    break;

                case LayoutBox box:
                    CollectPorts(box.Children, ports);
                    break;

                case LayoutBand band:
                    CollectPorts(band.Children, ports);
                    break;
            }
        }
    }

    /// <summary>
    /// Returns the node list with every line whose marker end sits on a port centre shortened by
    /// <see cref="NotationMetrics.PortHalfSize"/> along its end segment, so the painted marker tip lands on
    /// the port square's outer edge instead of being hidden under the glyph. The layout tree itself (and the
    /// connector label placement computed from it) is not modified. The SVG renderer applies the identical rule.
    /// </summary>
    /// <param name="nodes">The tree's top-level nodes.</param>
    /// <returns>The original list when the tree has no ports; otherwise a rewritten copy.</returns>
    private static IReadOnlyList<LayoutNode> ShortenPortEnds(IReadOnlyList<LayoutNode> nodes)
    {
        var ports = new List<LayoutPort>();
        CollectPorts(nodes, ports);
        return ports.Count == 0 ? nodes : RewriteForPorts(nodes, ports);
    }

    /// <summary>
    /// Rewrites the lines of a node list (recursively) for <see cref="ShortenPortEnds"/>.
    /// </summary>
    /// <param name="nodes">The nodes to rewrite.</param>
    /// <param name="ports">Every port in the tree.</param>
    /// <returns>The rewritten nodes.</returns>
    private static List<LayoutNode> RewriteForPorts(IReadOnlyList<LayoutNode> nodes, List<LayoutPort> ports)
    {
        var result = new List<LayoutNode>(nodes.Count);
        foreach (var node in nodes)
        {
            result.Add(node switch
            {
                LayoutLine line => ShortenLineForPorts(line, ports),
                LayoutBox box => box with { Children = RewriteForPorts(box.Children, ports) },
                LayoutBand band => band with { Children = RewriteForPorts(band.Children, ports) },
                _ => node,
            });
        }

        return result;
    }

    /// <summary>
    /// Shortens a line's marker ends that sit on a port centre (see <see cref="ShortenPortEnds"/>).
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="ports">Every port in the tree.</param>
    /// <returns>The line, or a copy with shortened end waypoints.</returns>
    private static LayoutLine ShortenLineForPorts(LayoutLine line, List<LayoutPort> ports)
    {
        var waypoints = line.Waypoints;
        if (waypoints.Count < 2)
        {
            return line;
        }

        var first = line.SourceEnd == EndMarkerStyle.None ? null : RetreatFromPort(waypoints[0], waypoints[1], ports);
        var last = line.TargetEnd == EndMarkerStyle.None ? null : RetreatFromPort(waypoints[^1], waypoints[^2], ports);
        if (first is null && last is null)
        {
            return line;
        }

        var shortened = new List<Point2D>(waypoints);
        if (first is not null)
        {
            shortened[0] = first;
        }

        if (last is not null)
        {
            shortened[^1] = last;
        }

        return line with { Waypoints = shortened };
    }

    /// <summary>
    /// Moves <paramref name="end"/> toward <paramref name="adjacent"/> by <see cref="NotationMetrics.PortHalfSize"/>
    /// when it coincides with a port centre and the end segment is longer than that.
    /// </summary>
    /// <param name="end">The line end point.</param>
    /// <param name="adjacent">The adjacent waypoint.</param>
    /// <param name="ports">Every port in the tree.</param>
    /// <returns>The retreated point, or <see langword="null"/> when the end is not on a port.</returns>
    private static Point2D? RetreatFromPort(Point2D end, Point2D adjacent, List<LayoutPort> ports)
    {
        const double portTolerance = 0.5;
        var onPort = ports.Find(port => Math.Abs(port.CentreX - end.X) <= portTolerance && Math.Abs(port.CentreY - end.Y) <= portTolerance);
        if (onPort is null)
        {
            return null;
        }

        // The retreat is the glyph's own half size, so the painted tip lands on the glyph's outer edge.
        var halfSize = NotationMetrics.PortGlyphHalfSizeFor(onPort.Appearance);

        var dx = adjacent.X - end.X;
        var dy = adjacent.Y - end.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length <= halfSize)
        {
            return null;
        }

        var fraction = halfSize / length;
        return new Point2D(end.X + (dx * fraction), end.Y + (dy * fraction));
    }

    /// <summary>
    /// Groups the visual paint parameters for line-end marker rendering, reducing the parameter
    /// count on <see cref="DrawEndMarker"/> to within the allowed limit.
    /// </summary>
    /// <param name="Color">Stroke and fill color for the end marker.</param>
    /// <param name="BackgroundColor">Background fill for hollow enclosing markers so the connector line does not show through.</param>
    /// <param name="StrokeWidth">Marker outline width in pixels (<see cref="NotationMetrics.MarkerStrokeWidth(Theme)"/> times the render scale).</param>
    /// <param name="Scale">Pixels per notation unit of marker geometry (<see cref="NotationMetrics.MarkerScale(Theme)"/> times the render scale), so PNG markers have the size of the SVG markers.</param>
    private readonly record struct EndMarkerPaint(Rgba32 Color, Rgba32 BackgroundColor, float StrokeWidth, float Scale);

    /// <summary>
    /// Maps a tip-relative <see cref="MarkerVertex"/> to a scaled canvas point, shared by all
    /// marker shapes so the raster renderer draws the identical geometry as the SVG marker
    /// definitions.
    /// </summary>
    /// <param name="tipX">Scaled X coordinate of the line endpoint.</param>
    /// <param name="tipY">Scaled Y coordinate of the line endpoint.</param>
    /// <param name="dx">X component of the outward unit direction.</param>
    /// <param name="dy">Y component of the outward unit direction.</param>
    /// <param name="px">X component of the perpendicular unit direction.</param>
    /// <param name="py">Y component of the perpendicular unit direction.</param>
    /// <param name="vertex">The tip-relative marker vertex in notation units.</param>
    /// <param name="scale">Uniform scale factor.</param>
    /// <returns>The mapped canvas point.</returns>
    private static Vector2 MarkerPoint(
        float tipX,
        float tipY,
        float dx,
        float dy,
        float px,
        float py,
        MarkerVertex vertex,
        float scale)
    {
        var along = (float)vertex.Along;
        var across = (float)vertex.Across;
        return new Vector2(
            tipX - (dx * along * scale) + (px * across * scale),
            tipY - (dy * along * scale) + (py * across * scale));
    }

    /// <summary>
    /// Draws a line-end marker of the specified style at a line endpoint, with every coordinate
    /// derived from <see cref="NotationMetrics"/> so the raster marker matches the SVG marker
    /// exactly.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="tipX">Scaled X coordinate of the line endpoint.</param>
    /// <param name="tipY">Scaled Y coordinate of the line endpoint.</param>
    /// <param name="dx">X component of the normalized outward direction vector.</param>
    /// <param name="dy">Y component of the normalized outward direction vector.</param>
    /// <param name="style">End-marker style to draw; <see cref="EndMarkerStyle.None"/> is a no-op.</param>
    /// <param name="paint">Color, stroke width, and scale parameters for the end marker.</param>
    private static void DrawEndMarker(
        CanvasRenderer canvas,
        float tipX,
        float tipY,
        float dx,
        float dy,
        EndMarkerStyle style,
        EndMarkerPaint paint)
    {
        if (style == EndMarkerStyle.None)
        {
            return;
        }

        var px = -dy;
        var py = dx;
        var scale = paint.Scale;
        var strokeStyle = new StrokeStyle(paint.StrokeWidth);

        // Polygonal markers stroke with a bevel join: their tips lie on the marker-box edge, where an SVG
        // marker's clip blunts the miter, so a bevel gives the PNG the same blunt tip instead of a spike
        // that would poke through the target box border.
        var triangleStroke = new StrokeStyle(paint.StrokeWidth, LineCap.Butt, LineJoin.Bevel);

        switch (style)
        {
            case EndMarkerStyle.OpenChevron:
                var chevron = TrianglePath(tipX, tipY, dx, dy, px, py, scale, close: false);
                canvas.StrokePath(chevron, triangleStroke, paint.Color);
                break;

            case EndMarkerStyle.HollowTriangle:
                var hollowTriangle = TrianglePath(tipX, tipY, dx, dy, px, py, scale, close: true);
                canvas.FillPath(hollowTriangle, paint.BackgroundColor);
                canvas.StrokePath(hollowTriangle, triangleStroke, paint.Color);
                break;

            case EndMarkerStyle.HollowTriangleCrossbar:
                var crossbarTriangle = TrianglePath(tipX, tipY, dx, dy, px, py, scale, close: true);
                canvas.FillPath(crossbarTriangle, paint.BackgroundColor);
                canvas.StrokePath(crossbarTriangle, triangleStroke, paint.Color);

                // The redefinition bar sits behind the triangle base, spanning the base height
                var crossStart = MarkerPoint(tipX, tipY, dx, dy, px, py, new MarkerVertex(NotationMetrics.CrossbarAlong, -NotationMetrics.CrossbarHalfLength), scale);
                var crossEnd = MarkerPoint(tipX, tipY, dx, dy, px, py, new MarkerVertex(NotationMetrics.CrossbarAlong, NotationMetrics.CrossbarHalfLength), scale);
                StrokeLine(canvas, crossStart.X, crossStart.Y, crossEnd.X, crossEnd.Y, strokeStyle, paint.Color);
                break;

            case EndMarkerStyle.FilledArrow:
                var filledTriangle = TrianglePath(tipX, tipY, dx, dy, px, py, scale, close: true);
                canvas.FillPath(filledTriangle, paint.Color);

                // The SVG marker also outlines the filled shape in the same color, so it is a stroke wider
                canvas.StrokePath(filledTriangle, triangleStroke, paint.Color);
                break;

            case EndMarkerStyle.HollowDiamond:
                var hollowDiamond = DiamondPath(tipX, tipY, dx, dy, px, py, scale);
                canvas.FillPath(hollowDiamond, paint.BackgroundColor);
                canvas.StrokePath(hollowDiamond, triangleStroke, paint.Color);
                break;

            case EndMarkerStyle.FilledDiamond:
                var filledDiamond = DiamondPath(tipX, tipY, dx, dy, px, py, scale);
                canvas.FillPath(filledDiamond, paint.Color);

                // The SVG marker also outlines the filled shape in the same color, so it is a stroke wider
                canvas.StrokePath(filledDiamond, triangleStroke, paint.Color);
                break;

            case EndMarkerStyle.Circle:
                var center = MarkerPoint(tipX, tipY, dx, dy, px, py, new MarkerVertex(NotationMetrics.CircleRadius, 0.0), scale);
                var radius = (float)NotationMetrics.CircleRadius * scale;
                canvas.FillCircle(center.X, center.Y, radius, paint.BackgroundColor);
                canvas.StrokeCircle(center.X, center.Y, radius, strokeStyle, paint.Color);
                break;

            case EndMarkerStyle.Bar:
                var barStart = MarkerPoint(tipX, tipY, dx, dy, px, py, new MarkerVertex(0.0, NotationMetrics.BarHalf), scale);
                var barEnd = MarkerPoint(tipX, tipY, dx, dy, px, py, new MarkerVertex(0.0, -NotationMetrics.BarHalf), scale);
                StrokeLine(canvas, barStart.X, barStart.Y, barEnd.X, barEnd.Y, strokeStyle, paint.Color);
                break;

            case EndMarkerStyle.HollowTriangleTwoDots:
            case EndMarkerStyle.HollowTriangleFourDots:
                // The triangle lands exactly where a plain hollow triangle does; the dots trail its base
                var dottedTriangle = TrianglePath(tipX, tipY, dx, dy, px, py, scale, close: true);
                canvas.FillPath(dottedTriangle, paint.BackgroundColor);
                canvas.StrokePath(dottedTriangle, triangleStroke, paint.Color);
                var dotRadius = (float)NotationMetrics.DotRadius * scale;
                foreach (var dot in NotationMetrics.DotCenters(style))
                {
                    var dotCentre = MarkerPoint(tipX, tipY, dx, dy, px, py, dot, scale);
                    canvas.FillCircle(dotCentre.X, dotCentre.Y, dotRadius, paint.Color);
                }

                break;

            case EndMarkerStyle.CircledPlus:
                var plusRadius = NotationMetrics.CircledPlusRadius;
                var plusCentre = MarkerPoint(tipX, tipY, dx, dy, px, py, new MarkerVertex(plusRadius, 0.0), scale);
                canvas.FillCircle(plusCentre.X, plusCentre.Y, (float)plusRadius * scale, paint.BackgroundColor);
                canvas.StrokeCircle(plusCentre.X, plusCentre.Y, (float)plusRadius * scale, strokeStyle, paint.Color);
                var armHalf = NotationMetrics.CircledPlusArmHalfLength;
                var alongA = MarkerPoint(tipX, tipY, dx, dy, px, py, new MarkerVertex(plusRadius - armHalf, 0.0), scale);
                var alongB = MarkerPoint(tipX, tipY, dx, dy, px, py, new MarkerVertex(plusRadius + armHalf, 0.0), scale);
                StrokeLine(canvas, alongA.X, alongA.Y, alongB.X, alongB.Y, strokeStyle, paint.Color);
                var acrossA = MarkerPoint(tipX, tipY, dx, dy, px, py, new MarkerVertex(plusRadius, -armHalf), scale);
                var acrossB = MarkerPoint(tipX, tipY, dx, dy, px, py, new MarkerVertex(plusRadius, armHalf), scale);
                StrokeLine(canvas, acrossA.X, acrossA.Y, acrossB.X, acrossB.Y, strokeStyle, paint.Color);
                break;
        }
    }

    /// <summary>
    /// Builds the triangle marker path from <see cref="NotationMetrics.TriangleVertices"/>,
    /// optionally closing the base edge (closed for the hollow and filled triangle, open for the
    /// chevron).
    /// </summary>
    /// <param name="tipX">Scaled X coordinate of the line endpoint.</param>
    /// <param name="tipY">Scaled Y coordinate of the line endpoint.</param>
    /// <param name="dx">X component of the normalized outward direction vector.</param>
    /// <param name="dy">Y component of the normalized outward direction vector.</param>
    /// <param name="px">X component of the perpendicular unit direction.</param>
    /// <param name="py">Y component of the perpendicular unit direction.</param>
    /// <param name="scale">Uniform scale factor.</param>
    /// <param name="close">Whether to close the path back to the first vertex.</param>
    /// <returns>The triangle marker path.</returns>
    private static GeometryPath TrianglePath(
        float tipX,
        float tipY,
        float dx,
        float dy,
        float px,
        float py,
        float scale,
        bool close)
    {
        var vertices = NotationMetrics.TriangleVertices();
        var builder = new PathBuilder();
        var first = MarkerPoint(tipX, tipY, dx, dy, px, py, vertices[0], scale);
        builder.MoveTo(first);
        for (var index = 1; index < vertices.Count; index++)
        {
            builder.LineTo(MarkerPoint(tipX, tipY, dx, dy, px, py, vertices[index], scale));
        }

        if (close)
        {
            builder.Close();
        }

        return builder.Build();
    }

    /// <summary>
    /// Builds the closed diamond marker path from <see cref="NotationMetrics.DiamondVertices"/>.
    /// </summary>
    /// <param name="tipX">Scaled X coordinate of the line endpoint.</param>
    /// <param name="tipY">Scaled Y coordinate of the line endpoint.</param>
    /// <param name="dx">X component of the normalized outward direction vector.</param>
    /// <param name="dy">Y component of the normalized outward direction vector.</param>
    /// <param name="px">X component of the perpendicular unit direction.</param>
    /// <param name="py">Y component of the perpendicular unit direction.</param>
    /// <param name="scale">Uniform scale factor.</param>
    /// <returns>The diamond marker path.</returns>
    private static GeometryPath DiamondPath(
        float tipX,
        float tipY,
        float dx,
        float dy,
        float px,
        float py,
        float scale)
    {
        var vertices = NotationMetrics.DiamondVertices();
        var builder = new PathBuilder();
        var first = MarkerPoint(tipX, tipY, dx, dy, px, py, vertices[0], scale);
        builder.MoveTo(first);
        for (var index = 1; index < vertices.Count; index++)
        {
            builder.LineTo(MarkerPoint(tipX, tipY, dx, dy, px, py, vertices[index], scale));
        }

        builder.Close();
        return builder.Build();
    }

    /// <summary>
    /// Computes a normalized direction unit vector from (<paramref name="fromX"/>,
    /// <paramref name="fromY"/>) toward (<paramref name="toX"/>, <paramref name="toY"/>).
    /// Returns (1, 0) as a safe fallback when the two points coincide.
    /// </summary>
    /// <param name="fromX">X coordinate of the source point.</param>
    /// <param name="fromY">Y coordinate of the source point.</param>
    /// <param name="toX">X coordinate of the target point.</param>
    /// <param name="toY">Y coordinate of the target point.</param>
    /// <returns>Normalized (Dx, Dy) direction tuple.</returns>
    private static (double Dx, double Dy) ComputeDirection(double fromX, double fromY, double toX, double toY)
    {
        var dx = toX - fromX;
        var dy = toY - fromY;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        return length < 0.001 ? (1.0, 0.0) : (dx / length, dy / length);
    }

    /// <summary>
    /// Renders a text label centered at the midpoint of a polyline, with a background rectangle drawn
    /// first to ensure readability over the line stroke.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="midX">Label center X in logical pixels.</param>
    /// <param name="midY">Label center Y in logical pixels.</param>
    /// <param name="label">Label text to render.</param>
    /// <param name="theme">Theme providing font size and padding.</param>
    /// <param name="scale">Uniform scale factor.</param>
    /// <param name="strokeColor">Color used for the label text.</param>
    private static void RenderLineMidpointLabel(
        CanvasRenderer canvas,
        double midX,
        double midY,
        string label,
        Theme theme,
        float scale,
        Rgba32 strokeColor)
    {
        var scaledX = (float)(midX * scale);
        var scaledY = (float)(midY * scale);

        var textFont = CreateFont(bold: false, italic: false);
        var textSize = (float)theme.FontSizeBody * scale;
        var textMetrics = MeasureText(label, textFont, textSize);
        var textHeight = (float)theme.FontSizeBody * scale;
        var padding = (float)theme.LabelPadding * scale * 0.5f;
        var backgroundLeft = scaledX - (textMetrics.Width / 2f) - padding;
        var backgroundTop = scaledY - textHeight - padding;
        var backgroundWidth = textMetrics.Width + (2f * padding);
        var backgroundHeight = textHeight + (2f * padding);
        canvas.FillRect(backgroundLeft, backgroundTop, backgroundWidth, backgroundHeight, ParseColor(theme.BackgroundColor));

        canvas.DrawText(label, scaledX, scaledY, CanvasTextAlign.Center, textFont, textSize, strokeColor);
    }

    /// <summary>
    /// Renders a <see cref="LayoutLabel"/> as a text element at its absolute position.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="label">Label node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderLabel(CanvasRenderer canvas, LayoutLabel label, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = ParseColor(theme.StrokeColor);
        var font = CreateFont(label.Weight == FontWeight.Bold, label.Style == FontStyle.Italic);
        var maxFontSize = (float)label.FontSize * scale;
        var availableWidth = (float)(label.MaxWidth * scale);
        var fontSize = FitFontSize(font, label.Text, availableWidth, maxFontSize);

        canvas.DrawText(
            label.Text,
            (float)(label.X * scale),
            (float)(label.Y * scale),
            ToCanvasTextAlign(label.Align),
            font,
            fontSize,
            strokeColor);
    }

    /// <summary>
    /// Renders a <see cref="LayoutPort"/> as a small (8×8 logical pixels) filled square centered at
    /// the port position. When a label is present it is offset away from the edge the port is
    /// attached to, ensuring it does not overlap with the host box.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="port">Port node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderPort(CanvasRenderer canvas, LayoutPort port, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = ParseColor(theme.StrokeColor);
        var backgroundColor = ParseColor(theme.BackgroundColor);

        // Glyph: the legacy filled 8x8 square (unchanged pixels) unless the appearance asks for an outlined
        // glyph or a proxy circle; the geometry comes from NotationMetrics, shared with the SVG renderer.
        switch (port.Appearance?.ResolvedGlyph)
        {
            case PortGlyph.Outlined:
                RenderOutlinedPortGlyph(canvas, port, options);
                break;

            case PortGlyph.Proxy:
                var proxyX = (float)(port.CentreX * scale);
                var proxyY = (float)(port.CentreY * scale);
                var proxyRadius = (float)(NotationMetrics.PortProxyRadius * scale);
                canvas.FillCircle(proxyX, proxyY, proxyRadius, strokeColor);
                canvas.StrokeCircle(proxyX, proxyY, proxyRadius, new StrokeStyle(PortGlyphStrokeWidth * scale), backgroundColor);
                break;

            default:
                var x = (float)((port.CentreX - NotationMetrics.PortHalfSize) * scale);
                var y = (float)((port.CentreY - NotationMetrics.PortHalfSize) * scale);
                var size = (float)(NotationMetrics.PortSize * scale);
                canvas.FillRect(x, y, size, size, strokeColor);
                canvas.StrokeRect(x, y, size, size, new StrokeStyle(PortGlyphStrokeWidth * scale), backgroundColor);
                break;
        }

        if (port.InternalLabel != null)
        {
            DrawPortLabel(canvas, port, port.InternalLabel, port.Side, options);
        }

        if (NotationMetrics.IsOutwardLabel(port))
        {
            DrawOutwardPortLabel(canvas, port, options);
        }
        else if (port.ExternalLabel != null)
        {
            var side = port.InternalLabel != null ? OppositeSide(port.Side) : port.Side;
            DrawPortLabel(canvas, port, port.ExternalLabel, side, options);
        }
    }

    /// <summary>
    /// Draws an outlined port glyph: a background-filled rounded square with the theme stroke (dashed for a
    /// reference port), then the direction arrow as one stroked polyline per segment of
    /// <see cref="NotationMetrics.PortArrowSegments"/> around the port centre.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="port">The port whose glyph is drawn.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderOutlinedPortGlyph(CanvasRenderer canvas, LayoutPort port, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = ParseColor(theme.StrokeColor);
        var half = (float)NotationMetrics.PortGlyphHalfSize;
        var x = (float)(port.CentreX * scale) - (half * scale);
        var y = (float)(port.CentreY * scale) - (half * scale);
        var size = 2f * half * scale;
        var corner = (float)NotationMetrics.PortGlyphCornerRadius * scale;
        var dashArray = DashArrayFor(port.Appearance?.OutlineStyle ?? LineStyle.Solid, scale);
        var outline = new StrokeStyle((float)theme.StrokeWidth * scale, dashArray: dashArray);

        canvas.FillRoundRect(x, y, size, size, corner, ParseColor(theme.BackgroundColor));
        canvas.StrokePath(BuildRoundRectPath(x, y, size, size, corner), outline, strokeColor);

        var arrowStyle = new StrokeStyle(PortGlyphStrokeWidth * scale);
        foreach (var segment in NotationMetrics.PortArrowSegments(port.Appearance?.Direction ?? PortDirection.None, port.Side))
        {
            var builder = new PathBuilder();
            for (var i = 0; i < segment.Count; i++)
            {
                var point = new Vector2((float)((port.CentreX + segment[i].X) * scale), (float)((port.CentreY + segment[i].Y) * scale));
                if (i == 0)
                {
                    builder.MoveTo(point);
                }
                else
                {
                    builder.LineTo(point);
                }
            }

            canvas.StrokePath(builder.Build(), arrowStyle, strokeColor);
        }
    }

    /// <summary>
    /// Draws a port's external label outside the owning box, beside the connector, at the anchor
    /// <see cref="NotationMetrics.PortOutwardLabelAnchor"/> returns, vertically centered on it (the same
    /// anchor the SVG renderer's <c>dominant-baseline="middle"</c> text uses) and never squeezed.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="port">The port whose outward label is drawn.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void DrawOutwardPortLabel(CanvasRenderer canvas, LayoutPort port, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var anchor = NotationMetrics.PortOutwardLabelAnchor(port, theme);
        var font = CreateFont(bold: false, italic: false);
        var fontSize = (float)theme.FontSizeBody * scale;
        var metrics = MeasureText(port.ExternalLabel!, font, fontSize);
        var baselineY = (float)(anchor.Y * scale) + ((metrics.Ascent - metrics.Descent) / 2.0f);
        var align = anchor.Align == PortLabelAlign.End ? CanvasTextAlign.Right : CanvasTextAlign.Left;
        canvas.DrawText(port.ExternalLabel!, (float)(anchor.X * scale), baselineY, align, font, fontSize, ParseColor(theme.StrokeColor));
    }

    /// <summary>
    /// Returns the rectangles the ports occupy — each glyph plus each outward label — that end labels must
    /// keep clear of.
    /// </summary>
    /// <param name="ports">Every port in the tree.</param>
    /// <param name="theme">Theme providing font settings.</param>
    /// <returns>The occupied rectangles.</returns>
    private static List<LabelPlacement> PortOccupiedRects(List<LayoutPort> ports, Theme theme)
    {
        var rects = new List<LabelPlacement>(ports.Count);
        foreach (var port in ports)
        {
            rects.Add(NotationMetrics.PortGlyphBounds(port));
            if (NotationMetrics.IsOutwardLabel(port))
            {
                rects.Add(NotationMetrics.PortOutwardLabelBounds(port, port.ExternalLabel!, theme));
            }
        }

        return rects;
    }

    /// <summary>
    /// Returns the box edge opposite <paramref name="side"/>, used to place an outward label.
    /// </summary>
    /// <param name="side">Input box edge.</param>
    /// <returns>The opposite box edge.</returns>
    private static PortSide OppositeSide(PortSide side) => side switch
    {
        PortSide.Top => PortSide.Bottom,
        PortSide.Bottom => PortSide.Top,
        PortSide.Left => PortSide.Right,
        _ => PortSide.Left,
    };

    /// <summary>
    /// Draws one port label offset from the port center using the interior-side formula for
    /// <paramref name="offsetSide"/> (the port's own side for an inward label, the opposite side
    /// for an outward one), so an inward and an outward label on one boundary port sit symmetrically
    /// about the port center.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="port">Port whose label is drawn.</param>
    /// <param name="text">Label text.</param>
    /// <param name="offsetSide">Side controlling the label offset direction.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void DrawPortLabel(CanvasRenderer canvas, LayoutPort port, string text, PortSide offsetSide, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = ParseColor(theme.StrokeColor);

        var offset = NotationMetrics.PortGlyphHalfSizeFor(port.Appearance) + theme.LabelPadding
            + (port.InternalLabel != null && port.ExternalLabel != null ? NotationMetrics.EndMarkerLength : 0.0);
        var (labelX, labelY, align) = offsetSide switch
        {
            PortSide.Top => (port.CentreX, port.CentreY + offset + theme.FontSizeBody, CanvasTextAlign.Center),
            PortSide.Bottom => (port.CentreX, port.CentreY - offset, CanvasTextAlign.Center),
            PortSide.Left => (port.CentreX + offset, port.CentreY + theme.FontSizeBody, CanvasTextAlign.Left),
            _ => (port.CentreX - offset, port.CentreY + theme.FontSizeBody, CanvasTextAlign.Right),
        };

        var font = CreateFont(bold: false, italic: false);
        var maxFontSize = (float)theme.FontSizeBody * scale;
        var maxLabelWidth = (float)(port.MaxLabelWidth * scale);
        var fontSize = FitFontSize(font, text, maxLabelWidth, maxFontSize);
        var metrics = MeasureText(text, font, fontSize);
        var baselineY = (float)(labelY * scale) + ((metrics.Ascent - metrics.Descent) / 2.0f);
        canvas.DrawText(text, (float)(labelX * scale), baselineY, align, font, fontSize, strokeColor);
    }

    /// <summary>
    /// Renders a <see cref="LayoutBadge"/> as the specified icon shape centered at the badge
    /// position. An optional label is drawn to the right of the bounding circle.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="badge">Badge node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderBadge(CanvasRenderer canvas, LayoutBadge badge, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = ParseColor(theme.StrokeColor);
        var strokeStyle = new StrokeStyle((float)theme.StrokeWidth * scale);

        var centerX = (float)(badge.CentreX * scale);
        var centerY = (float)(badge.CentreY * scale);
        var radius = (float)((badge.Size / 2.0) * scale);

        switch (badge.Shape)
        {
            case BadgeShape.FilledCircle:
                canvas.FillCircle(centerX, centerY, radius, strokeColor);
                break;

            case BadgeShape.Bullseye:
                var innerRadius = radius * (float)NotationMetrics.BadgeBullseyeInnerFraction;
                canvas.FillCircle(centerX, centerY, radius, strokeColor);
                canvas.FillCircle(centerX, centerY, innerRadius, White);
                canvas.StrokeCircle(centerX, centerY, innerRadius, strokeStyle, strokeColor);
                break;

            case BadgeShape.Diamond:
                var diamondBuilder = new PathBuilder();
                diamondBuilder.MoveTo(new Vector2(centerX, centerY - radius));
                diamondBuilder.LineTo(new Vector2(centerX + radius, centerY));
                diamondBuilder.LineTo(new Vector2(centerX, centerY + radius));
                diamondBuilder.LineTo(new Vector2(centerX - radius, centerY));
                diamondBuilder.Close();
                canvas.StrokePath(diamondBuilder.Build(), strokeStyle, strokeColor);
                break;

            case BadgeShape.HorizontalBar:
                StrokeLine(
                    canvas,
                    centerX - (radius * (float)NotationMetrics.BadgeBarLengthFraction),
                    centerY,
                    centerX + (radius * (float)NotationMetrics.BadgeBarLengthFraction),
                    centerY,
                    strokeStyle,
                    strokeColor);
                break;

            case BadgeShape.VerticalBar:
                StrokeLine(
                    canvas,
                    centerX,
                    centerY - (radius * (float)NotationMetrics.BadgeBarLengthFraction),
                    centerX,
                    centerY + (radius * (float)NotationMetrics.BadgeBarLengthFraction),
                    strokeStyle,
                    strokeColor);
                break;
            case BadgeShape.ThickHorizontalBar:
                var hBarThickness = (float)((badge.Thickness ?? NotationMetrics.ThickBarThickness) * scale);
                canvas.FillRect(centerX - radius, centerY - (hBarThickness / 2f), radius * 2f, hBarThickness, strokeColor);
                break;

            case BadgeShape.ThickVerticalBar:
                var vBarThickness = (float)((badge.Thickness ?? NotationMetrics.ThickBarThickness) * scale);
                canvas.FillRect(centerX - (vBarThickness / 2f), centerY - radius, vBarThickness, radius * 2f, strokeColor);
                break;

            case BadgeShape.CrossedCircle:
                var arm = radius * (float)NotationMetrics.CrossedCircleArmFraction;
                canvas.FillCircle(centerX, centerY, radius, ParseColor(theme.BackgroundColor));
                canvas.StrokeCircle(centerX, centerY, radius, strokeStyle, strokeColor);
                StrokeLine(canvas, centerX - arm, centerY - arm, centerX + arm, centerY + arm, strokeStyle, strokeColor);
                StrokeLine(canvas, centerX - arm, centerY + arm, centerX + arm, centerY - arm, strokeStyle, strokeColor);
                break;
        }

        if (badge.Label != null)
        {
            var font = CreateFont(bold: false, italic: false);
            var fontSize = (float)theme.FontSizeBody * scale;
            var labelHalfExtent = badge.Shape == BadgeShape.ThickVerticalBar
                ? (badge.Thickness ?? NotationMetrics.ThickBarThickness) / 2.0
                : badge.Size / 2.0;
            var labelX = (float)((badge.CentreX + labelHalfExtent + theme.LabelPadding) * scale);
            var labelY = (float)((badge.CentreY + (theme.FontSizeBody / 2.0)) * scale);
            canvas.DrawText(badge.Label, labelX, labelY, CanvasTextAlign.Left, font, fontSize, strokeColor);
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutBand"/> as a swim-lane rectangle with an optional label. For
    /// Horizontal bands the label is rendered vertically (rotated 90° counterclockwise) along the
    /// left edge; for Vertical bands it is rendered horizontally at the top. Children are rendered
    /// recursively.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="band">Band node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderBand(CanvasRenderer canvas, LayoutBand band, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = ParseColor(theme.StrokeColor);
        var strokeStyle = new StrokeStyle((float)theme.StrokeWidth * scale, dashArray: DashArrayFor(band.OutlineStyle, scale));

        var x = (float)(band.X * scale);
        var y = (float)(band.Y * scale);
        var width = (float)(band.Width * scale);
        var height = (float)(band.Height * scale);

        canvas.FillRect(x, y, width, height, ParseColor(theme.DepthFillColors[0]));
        if (band.OutlineStyle == LineStyle.Solid)
        {
            canvas.StrokeRect(x, y, width, height, strokeStyle, strokeColor);
        }
        else
        {
            canvas.StrokePath(BuildRoundRectPath(x, y, width, height, 0f), strokeStyle, strokeColor);
        }

        if (band.Label != null)
        {
            var font = CreateFont(bold: false, italic: false);
            var fontSize = (float)theme.FontSizeBody * scale;

            if (band.Orientation == BandOrientation.Horizontal)
            {
                var labelCenterX = (float)((band.X + theme.LabelPadding + (theme.FontSizeBody / 2.0)) * scale);
                var labelCenterY = (float)((band.Y + (band.Height / 2.0)) * scale);
                canvas.Save();
                canvas.Translate(labelCenterX, labelCenterY);
                canvas.RotateDegrees(-90);
                canvas.DrawText(band.Label, 0f, 0f, CanvasTextAlign.Center, font, fontSize, strokeColor);
                canvas.Restore();
            }
            else
            {
                var textX = (float)((band.X + (band.Width / 2.0)) * scale);
                var textY = (float)((band.Y + theme.LabelPadding + theme.FontSizeBody) * scale);
                canvas.DrawText(band.Label, textX, textY, CanvasTextAlign.Center, font, fontSize, strokeColor);
            }
        }

        foreach (var child in band.Children)
        {
            RenderNode(canvas, child, options);
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutLifeline"/> as a header box centered at
    /// <see cref="LayoutLifeline.CentreX"/> containing the lifeline label, followed by a dashed
    /// vertical stem running from the bottom of the header to <see cref="LayoutLifeline.BottomY"/>.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="lifeline">Lifeline node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderLifeline(CanvasRenderer canvas, LayoutLifeline lifeline, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = ParseColor(theme.StrokeColor);

        var headerLeft = lifeline.CentreX - (lifeline.HeaderWidth / 2.0);
        var x = (float)(headerLeft * scale);
        var y = (float)(lifeline.TopY * scale);
        var width = (float)(lifeline.HeaderWidth * scale);
        var height = (float)(lifeline.HeaderHeight * scale);
        var appearance = lifeline.HeaderAppearance ?? new BoxAppearance();
        var headerDash = DashArrayFor(appearance.OutlineStyle, scale);
        var headerStroke = new StrokeStyle((float)theme.StrokeWidth * scale, dashArray: headerDash);
        var headerRadius = 0f;
        if (lifeline.HeaderShape == BoxShape.RoundedRectangle)
        {
            headerRadius = (float)((lifeline.HeaderCornerRadius.HasValue
                ? Math.Max(0.0, lifeline.HeaderCornerRadius.Value)
                : NotationMetrics.RoundedRectRadius(theme)) * scale);
        }

        var headerFill = ParseColor(theme.DepthFillColors[0]);
        if (headerRadius > 0f)
        {
            canvas.FillRoundRect(x, y, width, height, headerRadius, headerFill);
        }
        else
        {
            canvas.FillRect(x, y, width, height, headerFill);
        }

        if (headerDash is not null)
        {
            canvas.StrokePath(BuildRoundRectPath(x, y, width, height, headerRadius), headerStroke, strokeColor);
        }
        else if (headerRadius > 0f)
        {
            canvas.StrokeRoundRect(x, y, width, height, headerRadius, headerStroke, strokeColor);
        }
        else
        {
            canvas.StrokeRect(x, y, width, height, headerStroke, strokeColor);
        }

        var nameStyle = appearance.ResolvedNameStyle;
        var font = CreateFont(nameStyle.Weight == FontWeight.Bold, nameStyle.Style == FontStyle.Italic);
        var fontSize = (float)theme.FontSizeBody * scale;
        var textX = (float)(lifeline.CentreX * scale);
        var textY = (float)((lifeline.TopY + ((lifeline.HeaderHeight + theme.FontSizeBody) / 2.0)) * scale);
        if (lifeline.Keyword != null)
        {
            var blockHeight = BoxMetrics.LifelineHeaderHeight(theme, hasKeyword: true);
            var blockTop = lifeline.TopY + Math.Max(0.0, (lifeline.HeaderHeight - blockHeight) / 2.0);
            var keywordStyle = appearance.ResolvedKeywordStyle;
            var keywordFont = CreateFont(keywordStyle.Weight == FontWeight.Bold, keywordStyle.Style == FontStyle.Italic);
            var keywordY = (float)((blockTop + theme.LabelPadding + theme.FontSizeBody) * scale);
            textY = (float)((blockTop + (2.0 * theme.LabelPadding) + (2.0 * theme.FontSizeBody)) * scale);
            canvas.DrawText("\u00AB" + lifeline.Keyword + "\u00BB", textX, keywordY, CanvasTextAlign.Center, keywordFont, fontSize, strokeColor);
        }

        canvas.DrawText(lifeline.Label, textX, textY, CanvasTextAlign.Center, font, fontSize, strokeColor);

        var dashedStyle = new StrokeStyle((float)theme.StrokeWidth * scale, dashArray: [6f * scale, 3f * scale]);
        var stemX = (float)(lifeline.CentreX * scale);
        StrokeLine(
            canvas,
            stemX,
            (float)((lifeline.TopY + lifeline.HeaderHeight) * scale),
            stemX,
            (float)(lifeline.BottomY * scale),
            dashedStyle,
            strokeColor);
    }

    /// <summary>
    /// Renders a <see cref="LayoutActivation"/> as a narrow white-filled rectangle with a stroke
    /// border, centered horizontally at <see cref="LayoutActivation.CentreX"/>.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="activation">Activation node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderActivation(CanvasRenderer canvas, LayoutActivation activation, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = ParseColor(theme.StrokeColor);
        var strokeStyle = new StrokeStyle((float)theme.StrokeWidth * scale);

        var halfWidth = theme.LabelPadding;
        var x = (float)((activation.CentreX - halfWidth) * scale);
        var y = (float)(activation.TopY * scale);
        var width = (float)((halfWidth * 2.0) * scale);
        var height = (float)((activation.BottomY - activation.TopY) * scale);

        canvas.FillRect(x, y, width, height, White);
        canvas.StrokeRect(x, y, width, height, strokeStyle, strokeColor);
    }

    /// <summary>
    /// Renders a <see cref="LayoutGrid"/> as a bordered table. Header rows are filled with the
    /// depth-1 theme color; body rows use the depth-0 color. Each cell's text is aligned according
    /// to <see cref="LayoutGridCell.Align"/> and vertically centered within the row height.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="grid">Grid node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderGrid(CanvasRenderer canvas, LayoutGrid grid, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = ParseColor(theme.StrokeColor);
        var strokeStyle = new StrokeStyle((float)theme.StrokeWidth * scale);
        var headerFill = ParseColor(theme.DepthFillColors[1 % theme.DepthFillColors.Count]);
        var bodyFill = ParseColor(theme.DepthFillColors[0]);

        var currentY = grid.Y;
        foreach (var row in grid.Rows)
        {
            var rowHeight = 0.0;
            foreach (var cell in row.Cells)
            {
                rowHeight = Math.Max(rowHeight, cell.Height);
            }

            var currentX = grid.X;
            foreach (var cell in row.Cells)
            {
                var x = (float)(currentX * scale);
                var y = (float)(currentY * scale);
                var width = (float)(cell.Width * scale);
                var height = (float)(rowHeight * scale);

                canvas.FillRect(x, y, width, height, row.IsHeader ? headerFill : bodyFill);
                canvas.StrokeRect(x, y, width, height, strokeStyle, strokeColor);

                var font = CreateFont(bold: row.IsHeader, italic: false);
                var fontSize = (float)theme.FontSizeBody * scale;
                var textX = cell.Align switch
                {
                    ModelTextAlign.Center => currentX + (cell.Width / 2.0),
                    ModelTextAlign.Right => currentX + cell.Width - theme.LabelPadding,
                    _ => currentX + theme.LabelPadding,
                };
                var textY = currentY + ((rowHeight + theme.FontSizeBody) / 2.0);
                canvas.DrawText(cell.Text, (float)(textX * scale), (float)(textY * scale), ToCanvasTextAlign(cell.Align), font, fontSize, strokeColor);

                currentX += cell.Width;
            }

            currentY += rowHeight;
        }
    }
}
