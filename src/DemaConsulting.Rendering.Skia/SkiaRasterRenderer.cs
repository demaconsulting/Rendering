// <copyright file="PngRenderer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using SkiaSharp;

namespace DemaConsulting.Rendering.Skia;

/// <summary>
/// Abstract base for SkiaSharp raster renderers: draws a <see cref="LayoutTree"/> onto a bitmap and
/// encodes it in a concrete image format supplied by a derived renderer (PNG, JPEG, or WEBP).
/// </summary>
/// <remarks>
/// The renderer is pure and stateless: each call to <see cref="Render"/> allocates a new
/// <see cref="SKBitmap"/> and <see cref="SKCanvas"/>, draws all nodes, and encodes the result
/// to the output stream before disposing all SkiaSharp resources. The output stream is not
/// closed or flushed by this renderer; the caller is responsible for its lifetime.
///
/// Node rendering:
/// - <see cref="LayoutBox"/> → filled rectangle (plain or rounded) + optional centered label
///   + compartment dividers and text rows; children rendered recursively.
/// - <see cref="LayoutLine"/> → corner-radius-aware polyline built as a single
///   <see cref="SKPath"/> with optional dashing; arrowheads at both ends; optional midpoint
///   label with white background.
/// - <see cref="LayoutLabel"/> → text element with <see cref="TextAlign"/>-derived alignment.
/// - <see cref="LayoutPort"/> → small filled square centered at the port position with
///   optional label offset away from the attached edge.
/// - <see cref="LayoutBadge"/> → icon shape (filled circle, bullseye, diamond, or bar)
///   centered at the badge position with optional label to the right.
/// - <see cref="LayoutBand"/> → swim-lane rectangle; label rendered vertically on the left
///   edge for Horizontal orientation or horizontally at the top for Vertical; children
///   rendered recursively.
/// - <see cref="LayoutLifeline"/> → header box at the top with a dashed vertical stem below.
/// - <see cref="LayoutActivation"/> → narrow white-filled rectangle with stroke border
///   centered at <c>CentreX</c>.
/// - <see cref="LayoutGrid"/> → bordered table; header rows use depth-1 fill color, body
///   rows use depth-0 fill color; per-cell text alignment respected.
/// - All other node types are silently skipped for forward compatibility.
///
/// Fill colors are derived from <see cref="Theme.DepthFillColors"/> using modulo wrapping on
/// <see cref="LayoutBox.Depth"/>. Hex color strings (e.g., <c>#RRGGBB</c>) are parsed with
/// <see cref="SKColor.Parse"/>.
///
/// A minimum bitmap size of 1×1 pixels is enforced to prevent SkiaSharp allocation errors
/// when the layout tree is empty.
/// </remarks>
public abstract class SkiaRasterRenderer : IRenderer
{
    /// <summary>
    /// Stroke width, in logical pixels (before scale), of the contrasting outline drawn around a port
    /// glyph square. Distinguishes the port glyph from an arrowhead marker that may land on/near the
    /// same box edge (both are otherwise solid-filled shapes with no border of their own), so the two
    /// remain visually distinct instead of merging into a single blob.
    /// </summary>
    private const float PortGlyphStrokeWidth = 1.0f;

    /// <summary>
    /// Creates an <see cref="SKPaint"/> configured for text rendering (color and antialiasing
    /// only). Font identity (typeface and size) is carried separately by an <see cref="SKFont"/>
    /// created with <see cref="CreateFont"/>. The caller is responsible for disposing the
    /// returned paint.
    /// </summary>
    /// <param name="color">Fill color for the text glyphs.</param>
    /// <returns>A new <see cref="SKPaint"/> ready for use with <c>canvas.DrawText</c>.</returns>
    private static SKPaint CreateTextPaint(SKColor color) =>
        new()
        {
            Color = color,
            IsAntialias = true,
        };

    /// <summary>
    /// Creates an <see cref="SKFont"/> using the Noto Sans typeface matching the requested weight
    /// and style, at the requested size. The caller is responsible for disposing the returned font.
    /// </summary>
    /// <param name="fontSize">Font size in scaled pixels.</param>
    /// <param name="bold">When <see langword="true"/>, selects the bold typeface variant.</param>
    /// <param name="italic">When <see langword="true"/>, selects the italic typeface variant.</param>
    /// <returns>A new <see cref="SKFont"/> ready for use with <c>canvas.DrawText</c>.</returns>
    private static SKFont CreateFont(float fontSize, bool bold, bool italic) =>
        new(SkiaTypefaces.Resolve(bold, italic), fontSize);

    /// <summary>
    /// Computes a reduced font size that fits <paramref name="text"/> within
    /// <paramref name="availableWidth"/> scaled pixels by scaling down proportionally.
    /// Returns <paramref name="maxFontSize"/> unchanged when the text already fits or
    /// when there is no meaningful width constraint.
    /// </summary>
    /// <param name="font">Font whose <see cref="SKFont.Size"/> is temporarily set
    /// to <paramref name="maxFontSize"/> to measure the text width.</param>
    /// <param name="text">Text whose rendered width is measured.</param>
    /// <param name="availableWidth">Maximum allowed width in scaled pixels. 0 or negative disables shrinking.</param>
    /// <param name="maxFontSize">Preferred (maximum) font size in scaled pixels.</param>
    /// <returns>Font size in scaled pixels, guaranteed to be &gt; 0.</returns>
    private static float FitFontSize(SKFont font, string text, float availableWidth, float maxFontSize)
    {
        font.Size = maxFontSize;
        if (availableWidth <= 0 || string.IsNullOrEmpty(text))
        {
            return maxFontSize;
        }

        var measuredWidth = font.MeasureText(text);
        if (measuredWidth <= availableWidth)
        {
            return maxFontSize;
        }

        // Scale font size proportionally so the text fits within the available width
        return maxFontSize * (availableWidth / measuredWidth);
    }

    /// <summary>Gets the SkiaSharp encoded-image format this renderer emits.</summary>
    protected abstract SKEncodedImageFormat EncodedFormat { get; }

    /// <summary>
    /// Gets the encoding quality (0-100) passed to the SkiaSharp encoder. Lossless formats such as
    /// PNG ignore this value; lossy formats such as JPEG and WEBP use it.
    /// </summary>
    protected virtual int EncodingQuality => 100;

    /// <inheritdoc/>
    public abstract string MediaType { get; }

    /// <inheritdoc/>
    public abstract string DefaultExtension { get; }

    /// <inheritdoc/>
    public abstract IReadOnlyList<string> FileExtensions { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// The bitmap dimensions are derived from <see cref="LayoutTree.Width"/> and
    /// <see cref="LayoutTree.Height"/> scaled by <see cref="RenderOptions.Scale"/>,
    /// with a minimum of 1×1 pixels. The background is filled with the theme's
    /// <see cref="Theme.BackgroundColor"/> before any nodes are drawn.
    /// </remarks>
    public void Render(LayoutTree layout, RenderOptions options, Stream output)
    {
        // Validate inputs — null arguments would produce silent failures
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        // Resolve every connector label's placement (position and size) before sizing the bitmap: a
        // label nudged to avoid colliding with another can land outside the box/routing geometry's
        // extent, so the bitmap must be sized only after label placement is known — a raster bitmap's
        // dimensions cannot grow once allocated.
        var lines = CollectLines(layout.Nodes).ToList();
        var labelPositions = ConnectorLabelPlacer.Place(lines, options.Theme.FontSizeBody);

        // Compute bitmap size, enforcing minimum 1×1 to prevent SKBitmap allocation errors, then grow
        // to fully include every placed label's bounding box.
        var w = Math.Max(1, (int)Math.Ceiling(layout.Width * options.Scale));
        var h = Math.Max(1, (int)Math.Ceiling(layout.Height * options.Scale));
        foreach (var placement in labelPositions.Values)
        {
            w = Math.Max(w, (int)Math.Ceiling((placement.X + placement.HalfWidth) * options.Scale));
            h = Math.Max(h, (int)Math.Ceiling((placement.Y + placement.HalfHeight) * options.Scale));
        }

        // Allocate bitmap, canvas and render all nodes
        using var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);

        // Fill the background with the theme background color before drawing diagram elements, so the
        // canvas fill and the hollow-marker occlusion (which also paints in the theme background)
        // share a single source of truth and stay consistent under non-white themes.
        canvas.Clear(SKColor.Parse(options.Theme.BackgroundColor));

        foreach (var node in layout.Nodes)
        {
            RenderNode(canvas, node, options);
        }

        // Final pass: draw every connector label on top of all wires and boxes, so that no later
        // wire can draw over an earlier wire's label. Positions were computed up front (before
        // sizing the bitmap, above) so that labels that would collide (for example where two
        // connectors cross) are spread apart.
        foreach (var line in lines)
        {
            if (line.MidpointLabel is not null && labelPositions.TryGetValue(line, out var pos))
            {
                RenderLineLabel(canvas, line, options, pos.X, pos.Y);
            }
        }

        // Encode in this renderer's concrete format and write to the output stream
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(EncodedFormat, EncodingQuality);
        data.SaveTo(output);
    }

    /// <summary>
    /// Dispatches a single <see cref="LayoutNode"/> to the appropriate typed render method.
    /// Unknown concrete types are silently skipped so that future node types do not break
    /// existing callers.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="node">Node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderNode(SKCanvas canvas, LayoutNode node, RenderOptions options)
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
                // Skip unknown node types for forward compatibility
                break;
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutBox"/> as a filled and stroked rectangle — plain or
    /// rounded depending on <see cref="BoxShape"/> — with an optional centered label,
    /// compartment dividers and rows, then recursively renders its children.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="box">Box node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderBox(SKCanvas canvas, LayoutBox box, RenderOptions options)
    {
        var theme = options.Theme;

        var strokeColor = SKColor.Parse(theme.StrokeColor);
        var fillHex = theme.DepthFillColors[box.Depth % theme.DepthFillColors.Count];
        var fillColor = SKColor.Parse(fillHex);

        // Draw the shape-specific outline (fill + border)
        RenderBoxOutline(canvas, box, options, fillColor, strokeColor);

        // Draw the keyword line and bold name label in the title area
        RenderBoxTitle(canvas, box, options, strokeColor);

        // Render compartments below the label area with horizontal dividers
        if (box.Compartments.Count > 0)
        {
            RenderBoxCompartments(canvas, box, options, strokeColor);
        }

        // Render children recursively
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
        SKCanvas canvas,
        LayoutBox box,
        RenderOptions options,
        SKColor fillColor,
        SKColor strokeColor)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;

        var x = (float)(box.X * scale);
        var y = (float)(box.Y * scale);
        var rect = new SKRect(x, y, x + (float)(box.Width * scale), y + (float)(box.Height * scale));

        using var fillPaint = new SKPaint { Color = fillColor, Style = SKPaintStyle.Fill, IsAntialias = true };
        using var strokePaint = new SKPaint
        {
            Color = strokeColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)theme.StrokeWidth * scale,
            IsAntialias = true,
        };

        switch (box.Shape)
        {
            case BoxShape.Folder:
                using (var path = BuildFolderPath(box, theme, scale))
                {
                    canvas.DrawPath(path, fillPaint);
                    canvas.DrawPath(path, strokePaint);
                }

                break;

            case BoxShape.Note:
                RenderNotePng(canvas, box, scale, fillPaint, strokePaint);
                break;

            case BoxShape.RoundedRectangle when ResolveRoundedCornerRadius(box, theme) > 0:
                var cornerR = (float)(ResolveRoundedCornerRadius(box, theme) * scale);
                canvas.DrawRoundRect(rect, cornerR, cornerR, fillPaint);
                canvas.DrawRoundRect(rect, cornerR, cornerR, strokePaint);
                break;

            default:
                canvas.DrawRect(rect, fillPaint);
                canvas.DrawRect(rect, strokePaint);
                break;
        }
    }

    /// <summary>
    /// Builds the folder outline path (a tab at the top-left above a full-width body).
    /// </summary>
    private static SKPath BuildFolderPath(LayoutBox box, Theme theme, float scale)
    {
        var tabHeight = ResolveFolderTabHeight(box, theme);
        var tabWidth = ResolveFolderTabWidth(box, theme);

        var x = (float)(box.X * scale);
        var yTab = (float)(box.Y * scale);
        var yBody = (float)((box.Y + tabHeight) * scale);
        var xTabRight = (float)((box.X + tabWidth) * scale);
        var xRight = (float)((box.X + box.Width) * scale);
        var yBottom = (float)((box.Y + box.Height) * scale);

        var builder = new SKPathBuilder();
        builder.MoveTo(x, yBody);
        builder.LineTo(x, yTab);
        builder.LineTo(xTabRight, yTab);
        builder.LineTo(xTabRight, yBody);
        builder.LineTo(xRight, yBody);
        builder.LineTo(xRight, yBottom);
        builder.LineTo(x, yBottom);
        builder.Close();
        return builder.Detach();
    }

    /// <summary>
    /// Resolves the rounded-corner radius for a box, preferring a caller-supplied placed-box value so
    /// routing and rendering can agree on the exact outline geometry. A negative caller-supplied value
    /// is clamped to zero.
    /// </summary>
    private static double ResolveRoundedCornerRadius(LayoutBox box, Theme theme) =>
        box.RoundedCornerRadius.HasValue
            ? Math.Max(0.0, box.RoundedCornerRadius.Value)
            : NotationMetrics.RoundedRectRadius(theme);

    /// <summary>
    /// Resolves the folder tab width for a box, preferring a caller-supplied placed-box value so
    /// routing and rendering can agree on the exact top-face geometry. A negative caller-supplied value
    /// is clamped to zero.
    /// </summary>
    private static double ResolveFolderTabWidth(LayoutBox box, Theme theme) =>
        box.FolderTabWidth.HasValue
            ? Math.Max(0.0, box.FolderTabWidth.Value)
            : Math.Min(
                box.Width * NotationMetrics.FolderTabMaxWidthFraction,
                Math.Max(
                    NotationMetrics.FolderTabMinWidth,
                    (box.Label?.Length ?? 4) * theme.FontSizeBody * NotationMetrics.FolderLabelCharWidthFactor +
                    (2.0 * theme.LabelPadding)));

    /// <summary>
    /// Resolves the folder tab height for a box, preferring a caller-supplied placed-box value so
    /// routing and rendering can agree on the exact top-face projection offset. A negative
    /// caller-supplied value is clamped to zero.
    /// </summary>
    private static double ResolveFolderTabHeight(LayoutBox box, Theme theme) =>
        box.FolderTabHeight.HasValue
            ? Math.Max(0.0, box.FolderTabHeight.Value)
            : BoxMetrics.FolderTabHeight(theme);

    /// <summary>
    /// Resolves the top Y coordinate (unscaled) of the title/label area for a box. For a
    /// <see cref="BoxShape.Folder"/> outline, the title area is recessed below the tab so that
    /// keyword/label text and compartments never overlap the (otherwise empty) tab notch.
    /// </summary>
    private static double ResolveTitleAreaTop(LayoutBox box, Theme theme) =>
        box.Shape == BoxShape.Folder
            ? box.Y + ResolveFolderTabHeight(box, theme)
            : box.Y;

    /// <summary>
    /// Draws a note-shaped box (a rectangle with a folded-down top-right corner).
    /// </summary>
    private static void RenderNotePng(
        SKCanvas canvas,
        LayoutBox box,
        float scale,
        SKPaint fillPaint,
        SKPaint strokePaint)
    {
        var fold = Math.Min(Math.Min(box.Width, box.Height) * NotationMetrics.NoteFoldFraction, NotationMetrics.NoteFoldMaxSize);

        var x = (float)(box.X * scale);
        var y = (float)(box.Y * scale);
        var xRight = (float)((box.X + box.Width) * scale);
        var xFold = (float)((box.X + box.Width - fold) * scale);
        var yFold = (float)((box.Y + fold) * scale);
        var yBottom = (float)((box.Y + box.Height) * scale);

        var bodyBuilder = new SKPathBuilder();
        bodyBuilder.MoveTo(x, y);
        bodyBuilder.LineTo(xFold, y);
        bodyBuilder.LineTo(xRight, yFold);
        bodyBuilder.LineTo(xRight, yBottom);
        bodyBuilder.LineTo(x, yBottom);
        bodyBuilder.Close();
        using var body = bodyBuilder.Detach();
        canvas.DrawPath(body, fillPaint);
        canvas.DrawPath(body, strokePaint);

        var cornerBuilder = new SKPathBuilder();
        cornerBuilder.MoveTo(xFold, y);
        cornerBuilder.LineTo(xFold, yFold);
        cornerBuilder.LineTo(xRight, yFold);
        using var corner = cornerBuilder.Detach();
        canvas.DrawPath(corner, strokePaint);
    }

    /// <summary>
    /// Draws the optional keyword line and bold name label in the title area of a box.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="box">Box whose title is drawn.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    /// <param name="strokeColor">Text color.</param>
    private static void RenderBoxTitle(SKCanvas canvas, LayoutBox box, RenderOptions options, SKColor strokeColor)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var centerX = (float)((box.X + (box.Width / 2.0)) * scale);
        var cursorY = BoxMetrics.TitleCursorTop(box, theme);

        // Keyword line (smaller, italic, guillemet-wrapped) above the name
        if (box.Keyword != null)
        {
            using var kwPaint = CreateTextPaint(strokeColor);
            using var kwFont = CreateFont((float)theme.FontSizeBody * scale, bold: false, italic: true);
            var kwY = (float)((cursorY + theme.FontSizeBody) * scale);
            canvas.DrawText("\u00AB" + box.Keyword + "\u00BB", centerX, kwY, SKTextAlign.Center, kwFont, kwPaint);
            cursorY += theme.FontSizeBody + theme.LabelPadding;
        }

        // Bold name label, shrink-to-fit
        if (box.Label != null)
        {
            using var textPaint = CreateTextPaint(strokeColor);
            using var textFont = CreateFont((float)theme.FontSizeTitle * scale, bold: true, italic: false);
            var availableWidth = (float)((box.Width - (2 * theme.LabelPadding)) * scale);
            textFont.Size = FitFontSize(textFont, box.Label, availableWidth, textFont.Size);
            var textY = (float)((cursorY + theme.FontSizeTitle) * scale);
            canvas.DrawText(box.Label, centerX, textY, SKTextAlign.Center, textFont, textPaint);
        }
    }

    /// <summary>
    /// Renders the compartments of a <see cref="LayoutBox"/> below the title area.
    /// Each compartment begins with a full-width horizontal divider, followed by an optional
    /// bold title row and then zero or more body-font text rows, each indented by
    /// <see cref="Theme.LabelPadding"/>.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="box">Box whose compartments are rendered.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    /// <param name="strokeColor">Pre-parsed stroke color reused across all compartment draws.</param>
    private static void RenderBoxCompartments(
        SKCanvas canvas,
        LayoutBox box,
        RenderOptions options,
        SKColor strokeColor)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;

        // Compartments start below the title area (keyword + label), computed via shared metrics
        var labelAreaHeight = BoxMetrics.TitleAreaHeight(theme, box.Label != null, box.Keyword != null);

        // Deliberately based on the computed title-area height (labelAreaHeight > 0), not on
        // "is Label/Keyword set": a theme with zero title padding/font-size would give
        // labelAreaHeight == 0 even with a Label present, and the leading divider must still be
        // skipped in that case to avoid the stray-line bug this fix addresses (see below).
        var titleAreaOccupiesSpace = labelAreaHeight > 0;
        var compartmentY = ResolveTitleAreaTop(box, theme) + box.ContentInsetTop + labelAreaHeight;

        var isFirstCompartment = true;
        foreach (var compartment in box.Compartments)
        {
            // The divider above the first compartment only has something to separate from when the
            // title area above it occupies real space. With no such space, this divider would sit
            // in the un-reserved region near the box's own top edge — redundant for a plain
            // rectangle (any coinciding lines are invisible duplicates) but a genuine visible defect
            // for shapes whose top edge isn't a plain straight line across the full width (e.g.
            // Note's folded corner cutout), where the full-width divider ignores the cut and renders
            // as a stray line past the fold.
            if (!isFirstCompartment || titleAreaOccupiesSpace)
            {
                using var divPaint = new SKPaint();
                divPaint.Color = strokeColor;
                divPaint.Style = SKPaintStyle.Stroke;
                divPaint.StrokeWidth = (float)theme.StrokeWidth * scale;
                canvas.DrawLine(
                    (float)(box.X * scale),
                    (float)(compartmentY * scale),
                    (float)((box.X + box.Width) * scale),
                    (float)(compartmentY * scale),
                    divPaint);
            }

            isFirstCompartment = false;

            // Draw the optional bold compartment title
            if (compartment.Title != null)
            {
                using var titlePaint = CreateTextPaint(strokeColor);
                using var titleFont = CreateFont((float)theme.FontSizeBody * scale, bold: true, italic: true);
                var titleX = (float)((box.X + theme.LabelPadding + box.ContentInsetLeft) * scale);
                var titleY = (float)((compartmentY + theme.LabelPadding + theme.FontSizeBody) * scale);
                canvas.DrawText(compartment.Title, titleX, titleY, SKTextAlign.Left, titleFont, titlePaint);
                compartmentY += theme.LabelPadding + theme.FontSizeBody + theme.LabelPadding;
            }

            // Draw each body row at body font size, left-aligned with LabelPadding indent
            foreach (var row in compartment.Rows)
            {
                using var rowPaint = CreateTextPaint(strokeColor);
                using var rowFont = CreateFont((float)theme.FontSizeBody * scale, bold: false, italic: false);
                var rowX = (float)((box.X + theme.LabelPadding + box.ContentInsetLeft) * scale);
                var rowY = (float)((compartmentY + theme.LabelPadding + theme.FontSizeBody) * scale);
                canvas.DrawText(row, rowX, rowY, SKTextAlign.Left, rowFont, rowPaint);
                compartmentY += theme.LabelPadding + theme.FontSizeBody;
            }

            // Bottom gap so the last row clears the next compartment divider.
            compartmentY += theme.LabelPadding;
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutLine"/> as a corner-radius-aware polyline, built from a
    /// single <see cref="SKPath"/> so that <c>CornerPathEffect</c> can round every bend
    /// uniformly. Arrowheads are drawn on top of the finished path. An optional midpoint
    /// label is centered over the line with a white background rectangle.
    /// </summary>
    /// <remarks>
    /// When <see cref="Theme.LineCornerRadius"/> is zero (e.g., the Print theme) the path is
    /// drawn with sharp corners. When both corner rounding and dashing are active, the two
    /// effects are composed so that the dash pattern follows the rounded path.
    /// </remarks>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="line">Line node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderLine(SKCanvas canvas, LayoutLine line, RenderOptions options)
    {
        // Lines with fewer than 2 waypoints cannot be drawn
        if (line.Waypoints.Count < 2)
        {
            return;
        }

        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = SKColor.Parse(theme.StrokeColor);

        // Build the connector path. Lines that carry an end marker whose decoration would otherwise be
        // intruded by a rounded corner are built with explicit, per-end clamped corners; all other
        // lines keep the uniform CornerPathEffect so their geometry is byte-identical to before.
        var clampCorners = (line.SourceEnd != EndMarkerStyle.None || line.TargetEnd != EndMarkerStyle.None)
            && theme.LineCornerRadius > 0
            && NeedsEndCornerClamp(line.Waypoints, theme.LineCornerRadius, line.SourceEnd, line.TargetEnd);

        using var path = clampCorners
            ? BuildClampedLinePath(line.Waypoints, theme.LineCornerRadius, scale, line.SourceEnd, line.TargetEnd)
            : BuildSimpleLinePath(line.Waypoints, scale);

        using var paint = new SKPaint();
        paint.Color = strokeColor;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = (float)theme.StrokeWidth * scale;
        paint.IsAntialias = true;

        // Apply corner and/or dash path effects based on theme and line style
        var cornerRadius = (float)(theme.LineCornerRadius * scale);
        var hasDash = line.LineStyle != LineStyle.Solid;

        if (clampCorners)
        {
            // Corners are already baked into the path; apply only the dash effect when needed.
            if (hasDash)
            {
                float[] intervals = line.LineStyle == LineStyle.Dashed
                    ? [6f * scale, 3f * scale]
                    : [2f * scale, 2f * scale];
                paint.PathEffect = SKPathEffect.CreateDash(intervals, 0);
            }
        }
        else if (hasDash && cornerRadius > 0)
        {
            // Compose: apply corner rounding (inner effect) then dashing (outer effect)
            float[] intervals = line.LineStyle == LineStyle.Dashed
                ? [6f * scale, 3f * scale]
                : [2f * scale, 2f * scale];
            using var dash = SKPathEffect.CreateDash(intervals, 0);
            using var corner = SKPathEffect.CreateCorner(cornerRadius);
            paint.PathEffect = SKPathEffect.CreateCompose(dash, corner);
        }
        else if (hasDash)
        {
            float[] intervals = line.LineStyle == LineStyle.Dashed
                ? [6f * scale, 3f * scale]
                : [2f * scale, 2f * scale];
            paint.PathEffect = SKPathEffect.CreateDash(intervals, 0);
        }
        else if (cornerRadius > 0)
        {
            paint.PathEffect = SKPathEffect.CreateCorner(cornerRadius);
        }

        canvas.DrawPath(path, paint);

        // Draw source end marker at the first waypoint, direction pointing away from the line
        if (line.SourceEnd != EndMarkerStyle.None)
        {
            var tip = line.Waypoints[0];
            var next = line.Waypoints[1];
            var (dx, dy) = ComputeDirection(next.X, next.Y, tip.X, tip.Y);
            DrawEndMarker(
                canvas,
                (float)(tip.X * scale), (float)(tip.Y * scale),
                (float)dx, (float)dy,
                line.SourceEnd,
                new EndMarkerPaint(strokeColor, SKColor.Parse(theme.BackgroundColor), (float)theme.StrokeWidth * scale, scale));
        }

        // Draw target end marker at the last waypoint, direction pointing away from the line
        if (line.TargetEnd != EndMarkerStyle.None)
        {
            var n = line.Waypoints.Count;
            var tip = line.Waypoints[n - 1];
            var prev = line.Waypoints[n - 2];
            var (dx, dy) = ComputeDirection(prev.X, prev.Y, tip.X, tip.Y);
            DrawEndMarker(
                canvas,
                (float)(tip.X * scale), (float)(tip.Y * scale),
                (float)dx, (float)dy,
                line.TargetEnd,
                new EndMarkerPaint(strokeColor, SKColor.Parse(theme.BackgroundColor), (float)theme.StrokeWidth * scale, scale));
        }

        // Note: the midpoint label is intentionally NOT drawn here. It is drawn in a final pass
        // (see RenderLineLabel) so that no later wire can draw over an earlier wire's label.
    }

    /// <summary>
    /// Builds a plain scaled polyline path through all waypoints, used together with the uniform
    /// <c>CornerPathEffect</c> for lines that do not need decoration-aware corner clamping.
    /// </summary>
    /// <param name="waypoints">Ordered waypoints.</param>
    /// <param name="scale">Uniform scale factor.</param>
    /// <returns>The polyline path.</returns>
    private static SKPath BuildSimpleLinePath(IReadOnlyList<Point2D> waypoints, float scale)
    {
        var builder = new SKPathBuilder();
        builder.MoveTo((float)(waypoints[0].X * scale), (float)(waypoints[0].Y * scale));
        for (var i = 1; i < waypoints.Count; i++)
        {
            builder.LineTo((float)(waypoints[i].X * scale), (float)(waypoints[i].Y * scale));
        }

        return builder.Detach();
    }

    /// <summary>
    /// Returns whether a uniform rounded corner of <paramref name="cornerRadius"/> would intrude into
    /// an end-marker decoration, i.e. the straight approach at a decorated end is shorter than the
    /// marker's along-line length plus the corner radius. Computed in unscaled notation units.
    /// </summary>
    /// <param name="waypoints">Ordered waypoints (unscaled).</param>
    /// <param name="cornerRadius">Unscaled line corner radius.</param>
    /// <param name="sourceEnd">Source end-marker style.</param>
    /// <param name="targetEnd">Target end-marker style.</param>
    /// <returns><c>true</c> if explicit clamped corners are required; otherwise <c>false</c>.</returns>
    private static bool NeedsEndCornerClamp(
        IReadOnlyList<Point2D> waypoints,
        double cornerRadius,
        EndMarkerStyle sourceEnd,
        EndMarkerStyle targetEnd)
    {
        // Without an interior corner there is nothing to round, so nothing can intrude.
        if (waypoints.Count < 3)
        {
            return false;
        }

        if (sourceEnd != EndMarkerStyle.None)
        {
            var inLen = Distance(waypoints[0], waypoints[1]);
            if (inLen - NotationMetrics.AlongLineLength(sourceEnd) < cornerRadius)
            {
                return true;
            }
        }

        if (targetEnd != EndMarkerStyle.None)
        {
            var n = waypoints.Count;
            var outLen = Distance(waypoints[n - 2], waypoints[n - 1]);
            if (outLen - NotationMetrics.AlongLineLength(targetEnd) < cornerRadius)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds a scaled connector path with arc-rounded interior corners, mirroring the SVG renderer's
    /// <c>BuildLinePath</c>: the first interior corner additionally clamps its radius to
    /// <c>inLen − AlongLineLength(sourceEnd)</c> and the last to
    /// <c>outLen − AlongLineLength(targetEnd)</c>, so the rounded corner never intrudes into an end
    /// decoration. Corners are baked into the geometry, so no <c>CornerPathEffect</c> is applied.
    /// </summary>
    /// <param name="waypoints">Ordered waypoints (unscaled); at least two entries.</param>
    /// <param name="cornerRadius">Unscaled line corner radius.</param>
    /// <param name="scale">Uniform scale factor.</param>
    /// <param name="sourceEnd">Source end-marker style.</param>
    /// <param name="targetEnd">Target end-marker style.</param>
    /// <returns>The corner-baked connector path.</returns>
    private static SKPath BuildClampedLinePath(
        IReadOnlyList<Point2D> waypoints,
        double cornerRadius,
        float scale,
        EndMarkerStyle sourceEnd,
        EndMarkerStyle targetEnd)
    {
        var builder = new SKPathBuilder();
        var first = waypoints[0];
        builder.MoveTo((float)(first.X * scale), (float)(first.Y * scale));

        for (var i = 1; i < waypoints.Count; i++)
        {
            var cur = waypoints[i];
            var isInterior = i < waypoints.Count - 1;
            if (!isInterior)
            {
                builder.LineTo((float)(cur.X * scale), (float)(cur.Y * scale));
                continue;
            }

            var prev = waypoints[i - 1];
            var next = waypoints[i + 1];

            var inDx = cur.X - prev.X;
            var inDy = cur.Y - prev.Y;
            var inLen = Math.Sqrt(inDx * inDx + inDy * inDy);
            var outDx = next.X - cur.X;
            var outDy = next.Y - cur.Y;
            var outLen = Math.Sqrt(outDx * outDx + outDy * outDy);

            if (inLen < 0.001 || outLen < 0.001)
            {
                builder.LineTo((float)(cur.X * scale), (float)(cur.Y * scale));
                continue;
            }

            var inNx = inDx / inLen;
            var inNy = inDy / inLen;
            var outNx = outDx / outLen;
            var outNy = outDy / outLen;

            var r = Math.Min(cornerRadius, Math.Min(inLen / 2.0, outLen / 2.0));
            if (i == 1)
            {
                r = Math.Min(r, inLen - NotationMetrics.AlongLineLength(sourceEnd));
            }

            if (i == waypoints.Count - 2)
            {
                r = Math.Min(r, outLen - NotationMetrics.AlongLineLength(targetEnd));
            }

            if (r <= 0.0)
            {
                builder.LineTo((float)(cur.X * scale), (float)(cur.Y * scale));
                continue;
            }

            var shortEndX = cur.X - inNx * r;
            var shortEndY = cur.Y - inNy * r;
            var shortStartX = cur.X + outNx * r;
            var shortStartY = cur.Y + outNy * r;

            var cross = inNx * outNy - inNy * outNx;
            var dir = cross > 0 ? SKPathDirection.Clockwise : SKPathDirection.CounterClockwise;

            builder.LineTo((float)(shortEndX * scale), (float)(shortEndY * scale));
            builder.ArcTo(
                (float)(r * scale), (float)(r * scale), 0, SKPathArcSize.Small, dir,
                (float)(shortStartX * scale), (float)(shortStartY * scale));
        }

        return builder.Detach();
    }

    /// <summary>Returns the Euclidean distance between two points.</summary>
    /// <param name="a">First point.</param>
    /// <param name="b">Second point.</param>
    /// <returns>The distance.</returns>
    private static double Distance(Point2D a, Point2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Draws a line's optional midpoint label, called in a final pass after all wires and boxes are
    /// drawn so labels are never drawn over by another wire.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="line">The line whose label is rendered.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    /// <param name="midX">Pre-computed label centre X in logical pixels.</param>
    /// <param name="midY">Pre-computed label centre Y in logical pixels.</param>
    private static void RenderLineLabel(SKCanvas canvas, LayoutLine line, RenderOptions options, double midX, double midY)
    {
        if (line.MidpointLabel is null)
        {
            return;
        }

        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = SKColor.Parse(theme.StrokeColor);
        RenderLineMidpointLabel(canvas, midX, midY, line.MidpointLabel, theme, scale, strokeColor);
    }

    /// <summary>Recursively collects all <see cref="LayoutLine"/> nodes from a node tree.</summary>
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

                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Groups the visual paint parameters for line-end marker rendering, reducing the parameter
    /// count on <see cref="DrawEndMarker"/> to within the allowed limit.
    /// </summary>
    /// <param name="Color">Stroke and fill color for the end marker.</param>
    /// <param name="BackgroundColor">Background fill for hollow enclosing markers, so the connector line does not show through.</param>
    /// <param name="StrokeWidth">Stroke width applied to open (non-filled) end-marker styles.</param>
    /// <param name="Scale">Uniform scale factor used to size the end marker relative to the diagram.</param>
    private readonly record struct EndMarkerPaint(SKColor Color, SKColor BackgroundColor, float StrokeWidth, float Scale);

    /// <summary>
    /// Maps a tip-relative <see cref="MarkerVertex"/> to a scaled canvas point, shared by all marker
    /// shapes so the PNG draws the identical geometry as the SVG marker definitions.
    /// </summary>
    /// <remarks>
    /// The endpoint (tip) is the line end; <c>(dx, dy)</c> points outward along the line and
    /// <c>(px, py)</c> is its left perpendicular. A vertex at distance <c>Along</c> back from the tip
    /// and <c>Across</c> to the side maps to <c>tip − dir·Along·scale + perp·Across·scale</c>, so a
    /// negative <c>Along</c> (the triangle apex) overshoots the tip outward.
    /// </remarks>
    /// <param name="tipX">Scaled X coordinate of the line endpoint.</param>
    /// <param name="tipY">Scaled Y coordinate of the line endpoint.</param>
    /// <param name="dx">X component of the outward unit direction.</param>
    /// <param name="dy">Y component of the outward unit direction.</param>
    /// <param name="px">X component of the perpendicular unit direction.</param>
    /// <param name="py">Y component of the perpendicular unit direction.</param>
    /// <param name="vertex">The tip-relative marker vertex in notation units.</param>
    /// <param name="scale">Uniform scale factor.</param>
    /// <returns>The mapped canvas point.</returns>
    private static SKPoint MarkerPoint(
        float tipX, float tipY, float dx, float dy, float px, float py, MarkerVertex vertex, float scale)
    {
        var along = (float)vertex.Along;
        var across = (float)vertex.Across;
        return new SKPoint(
            tipX - dx * along * scale + px * across * scale,
            tipY - dy * along * scale + py * across * scale);
    }

    /// <summary>
    /// Draws a line-end marker of the specified style at a line endpoint, with every coordinate
    /// derived from <see cref="NotationMetrics"/> so the PNG marker matches the SVG marker exactly.
    /// </summary>
    /// <remarks>
    /// The direction vector (<paramref name="dx"/>, <paramref name="dy"/>) must be a unit vector
    /// pointing from the line body outward through the tip. The triangle, filled arrow, and both
    /// diamonds reuse <see cref="NotationMetrics.TriangleVertices"/> /
    /// <see cref="NotationMetrics.DiamondVertices"/>; <see cref="EndMarkerStyle.OpenChevron"/> draws
    /// those triangle vertices as an OPEN two-stroke polyline (no closing base edge), whereas
    /// <see cref="EndMarkerStyle.HollowTriangle"/> closes the path.
    /// </remarks>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="tipX">Scaled X coordinate of the line endpoint.</param>
    /// <param name="tipY">Scaled Y coordinate of the line endpoint.</param>
    /// <param name="dx">X component of the normalized outward direction vector.</param>
    /// <param name="dy">Y component of the normalized outward direction vector.</param>
    /// <param name="style">End-marker style to draw; <see cref="EndMarkerStyle.None"/> is a no-op.</param>
    /// <param name="paint">Color, stroke width, and scale parameters for the end marker.</param>
    private static void DrawEndMarker(
        SKCanvas canvas,
        float tipX, float tipY,
        float dx, float dy,
        EndMarkerStyle style,
        EndMarkerPaint paint)
    {
        if (style == EndMarkerStyle.None)
        {
            return;
        }

        // Perpendicular direction (90° CCW rotation of the outward vector)
        var px = -dy;
        var py = dx;
        var scale = paint.Scale;

        using var paintObj = new SKPaint();
        paintObj.Color = paint.Color;
        paintObj.IsAntialias = true;
        paintObj.StrokeWidth = paint.StrokeWidth;

        switch (style)
        {
            case EndMarkerStyle.OpenChevron:
                {
                    // OPEN chevron: two strokes meeting at the apex, no closing base edge.
                    paintObj.Style = SKPaintStyle.Stroke;
                    using var p = TrianglePath(tipX, tipY, dx, dy, px, py, scale, close: false);
                    canvas.DrawPath(p, paintObj);
                    break;
                }

            case EndMarkerStyle.HollowTriangle:
                {
                    // Closed hollow triangle, background-filled so the line does not show through.
                    using var p = TrianglePath(tipX, tipY, dx, dy, px, py, scale, close: true);
                    paintObj.Style = SKPaintStyle.Fill;
                    paintObj.Color = paint.BackgroundColor;
                    canvas.DrawPath(p, paintObj);
                    paintObj.Style = SKPaintStyle.Stroke;
                    paintObj.Color = paint.Color;
                    canvas.DrawPath(p, paintObj);
                    break;
                }

            case EndMarkerStyle.HollowTriangleCrossbar:
                {
                    // Closed hollow triangle (background-filled) + perpendicular crossbar on the shaft.
                    using var p = TrianglePath(tipX, tipY, dx, dy, px, py, scale, close: true);
                    paintObj.Style = SKPaintStyle.Fill;
                    paintObj.Color = paint.BackgroundColor;
                    canvas.DrawPath(p, paintObj);
                    paintObj.Style = SKPaintStyle.Stroke;
                    paintObj.Color = paint.Color;
                    canvas.DrawPath(p, paintObj);

                    var crossAlong = NotationMetrics.EndMarkerRefX - NotationMetrics.CrossbarX;
                    var a = MarkerPoint(tipX, tipY, dx, dy, px, py, new MarkerVertex(crossAlong, -NotationMetrics.EndMarkerHalfWidth), scale);
                    var b = MarkerPoint(tipX, tipY, dx, dy, px, py, new MarkerVertex(crossAlong, NotationMetrics.EndMarkerHalfWidth), scale);
                    canvas.DrawLine(a.X, a.Y, b.X, b.Y, paintObj);
                    break;
                }

            case EndMarkerStyle.FilledArrow:
                {
                    // Filled solid triangle.
                    paintObj.Style = SKPaintStyle.Fill;
                    using var p = TrianglePath(tipX, tipY, dx, dy, px, py, scale, close: true);
                    canvas.DrawPath(p, paintObj);
                    break;
                }

            case EndMarkerStyle.HollowDiamond:
                {
                    // Hollow four-point diamond, background-filled so the line does not show through.
                    using var p = DiamondPath(tipX, tipY, dx, dy, px, py, scale);
                    paintObj.Style = SKPaintStyle.Fill;
                    paintObj.Color = paint.BackgroundColor;
                    canvas.DrawPath(p, paintObj);
                    paintObj.Style = SKPaintStyle.Stroke;
                    paintObj.Color = paint.Color;
                    canvas.DrawPath(p, paintObj);
                    break;
                }

            case EndMarkerStyle.FilledDiamond:
                {
                    // Filled four-point diamond.
                    paintObj.Style = SKPaintStyle.Fill;
                    using var p = DiamondPath(tipX, tipY, dx, dy, px, py, scale);
                    canvas.DrawPath(p, paintObj);
                    break;
                }

            case EndMarkerStyle.Circle:
                {
                    // Hollow circle (background-filled) whose near edge touches the tip; center pulled back by one radius.
                    var center = MarkerPoint(
                        tipX, tipY, dx, dy, px, py, new MarkerVertex(NotationMetrics.CircleRadius, 0.0), scale);
                    var r = (float)NotationMetrics.CircleRadius * scale;
                    paintObj.Style = SKPaintStyle.Fill;
                    paintObj.Color = paint.BackgroundColor;
                    canvas.DrawCircle(center.X, center.Y, r, paintObj);
                    paintObj.Style = SKPaintStyle.Stroke;
                    paintObj.Color = paint.Color;
                    canvas.DrawCircle(center.X, center.Y, r, paintObj);
                    break;
                }

            case EndMarkerStyle.Bar:
                {
                    // Perpendicular bar centered on the tip.
                    paintObj.Style = SKPaintStyle.Stroke;
                    var a = MarkerPoint(tipX, tipY, dx, dy, px, py, new MarkerVertex(0.0, NotationMetrics.BarHalf), scale);
                    var b = MarkerPoint(tipX, tipY, dx, dy, px, py, new MarkerVertex(0.0, -NotationMetrics.BarHalf), scale);
                    canvas.DrawLine(a.X, a.Y, b.X, b.Y, paintObj);
                    break;
                }

            default:
                // Unknown styles are treated as None
                break;
        }
    }

    /// <summary>
    /// Builds the triangle marker path from <see cref="NotationMetrics.TriangleVertices"/>, optionally
    /// closing the base edge (closed for the hollow/filled triangle, open for the chevron).
    /// </summary>
    private static SKPath TrianglePath(
        float tipX, float tipY, float dx, float dy, float px, float py, float scale, bool close)
    {
        var vertices = NotationMetrics.TriangleVertices();
        var builder = new SKPathBuilder();
        var v0 = MarkerPoint(tipX, tipY, dx, dy, px, py, vertices[0], scale);
        builder.MoveTo(v0.X, v0.Y);
        for (var i = 1; i < vertices.Count; i++)
        {
            var v = MarkerPoint(tipX, tipY, dx, dy, px, py, vertices[i], scale);
            builder.LineTo(v.X, v.Y);
        }

        if (close)
        {
            builder.Close();
        }

        return builder.Detach();
    }

    /// <summary>
    /// Builds the closed diamond marker path from <see cref="NotationMetrics.DiamondVertices"/>.
    /// </summary>
    private static SKPath DiamondPath(
        float tipX, float tipY, float dx, float dy, float px, float py, float scale)
    {
        var vertices = NotationMetrics.DiamondVertices();
        var builder = new SKPathBuilder();
        var v0 = MarkerPoint(tipX, tipY, dx, dy, px, py, vertices[0], scale);
        builder.MoveTo(v0.X, v0.Y);
        for (var i = 1; i < vertices.Count; i++)
        {
            var v = MarkerPoint(tipX, tipY, dx, dy, px, py, vertices[i], scale);
            builder.LineTo(v.X, v.Y);
        }

        builder.Close();
        return builder.Detach();
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
    private static (double Dx, double Dy) ComputeDirection(
        double fromX, double fromY, double toX, double toY)
    {
        var dx = toX - fromX;
        var dy = toY - fromY;
        var len = Math.Sqrt(dx * dx + dy * dy);
        return len < 0.001 ? (1.0, 0.0) : (dx / len, dy / len);
    }

    /// <summary>
    /// Renders a text label centered at the midpoint of a polyline, with a white background
    /// rectangle drawn first to ensure readability over the line stroke.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="midX">Label centre X in logical pixels.</param>
    /// <param name="midY">Label centre Y in logical pixels.</param>
    /// <param name="label">Label text to render.</param>
    /// <param name="theme">Theme providing font size and padding.</param>
    /// <param name="scale">Uniform scale factor.</param>
    /// <param name="strokeColor">Color used for the label text.</param>
    private static void RenderLineMidpointLabel(
        SKCanvas canvas,
        double midX,
        double midY,
        string label,
        Theme theme,
        float scale,
        SKColor strokeColor)
    {
        var scaledX = (float)(midX * scale);
        var scaledY = (float)(midY * scale);

        using var textPaint = CreateTextPaint(strokeColor);
        using var textFont = CreateFont((float)theme.FontSizeBody * scale, bold: false, italic: false);

        // Measure the text so the background rectangle fits snugly around it
        var textWidth = textFont.MeasureText(label);
        var textHeight = (float)theme.FontSizeBody * scale;
        var padding = (float)theme.LabelPadding * scale * 0.5f;
        var bgRect = new SKRect(
            scaledX - textWidth / 2f - padding,
            scaledY - textHeight - padding,
            scaledX + textWidth / 2f + padding,
            scaledY + padding);

        using (var bgPaint = new SKPaint())
        {
            // Occlude the connector line behind the label using the theme background, matching the
            // canvas fill so the label reads cleanly under non-white themes.
            bgPaint.Color = SKColor.Parse(theme.BackgroundColor);
            bgPaint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(bgRect, bgPaint);
        }

        canvas.DrawText(label, scaledX, scaledY, SKTextAlign.Center, textFont, textPaint);
    }

    /// <summary>
    /// Renders a <see cref="LayoutLabel"/> as a text element at its absolute position.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="label">Label node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderLabel(SKCanvas canvas, LayoutLabel label, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;

        using var paint = CreateTextPaint(SKColor.Parse(theme.StrokeColor));
        using var font = CreateFont(
            (float)label.FontSize * scale,
            bold: label.Weight == FontWeight.Bold,
            italic: label.Style == FontStyle.Italic);
        var align = label.Align switch
        {
            TextAlign.Center => SKTextAlign.Center,
            TextAlign.Right => SKTextAlign.Right,
            _ => SKTextAlign.Left
        };

        var availableWidth = (float)(label.MaxWidth * scale);
        font.Size = FitFontSize(font, label.Text, availableWidth, font.Size);

        canvas.DrawText(label.Text, (float)(label.X * scale), (float)(label.Y * scale), align, font, paint);
    }

    /// <summary>
    /// Renders a <see cref="LayoutPort"/> as a small (8×8 logical pixels) filled square
    /// centered at the port position. When a label is present it is offset away from the
    /// edge the port is attached to, ensuring it does not overlap with the host box.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="port">Port node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderPort(SKCanvas canvas, LayoutPort port, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = SKColor.Parse(theme.StrokeColor);

        // Port square: filled, centered at (CentreX, CentreY), sized from NotationMetrics.
        var portRect = new SKRect(
            (float)((port.CentreX - NotationMetrics.PortHalfSize) * scale),
            (float)((port.CentreY - NotationMetrics.PortHalfSize) * scale),
            (float)((port.CentreX + NotationMetrics.PortHalfSize) * scale),
            (float)((port.CentreY + NotationMetrics.PortHalfSize) * scale));

        // Ports are conventionally drawn as filled squares using the stroke color
        using (var fillPaint = new SKPaint())
        {
            fillPaint.Color = strokeColor;
            fillPaint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(portRect, fillPaint);
        }

        // Draw a contrasting outline around the port glyph so it remains visually distinct from an
        // arrowhead marker that may land on/near the same box edge (both are otherwise solid-filled
        // shapes with no border of their own, which can otherwise merge into a single indistinguishable
        // blob). Drawn as a separate stroke-only pass since the fill and stroke use different colors.
        using (var outlinePaint = new SKPaint())
        {
            outlinePaint.Color = SKColor.Parse(theme.BackgroundColor);
            outlinePaint.Style = SKPaintStyle.Stroke;
            outlinePaint.StrokeWidth = PortGlyphStrokeWidth * scale;
            canvas.DrawRect(portRect, outlinePaint);
        }

        // Labels. InternalLabel (when present) always renders inward, toward the box interior. The
        // ExternalLabel renders inward too when there is no InternalLabel (a plain, non-boundary port —
        // byte-identical to the single-label behavior), and only on the outward face when an
        // InternalLabel is also present (a genuine boundary port), mirroring the inward offset across
        // the port centre.
        if (port.InternalLabel != null)
        {
            DrawPortLabel(canvas, port, port.InternalLabel, port.Side, options);
        }

        if (port.ExternalLabel != null)
        {
            var side = port.InternalLabel != null ? OppositeSide(port.Side) : port.Side;
            DrawPortLabel(canvas, port, port.ExternalLabel, side, options);
        }
    }

    /// <summary>Returns the box edge opposite <paramref name="side"/>, used to place an outward label.</summary>
    private static PortSide OppositeSide(PortSide side) => side switch
    {
        PortSide.Top => PortSide.Bottom,
        PortSide.Bottom => PortSide.Top,
        PortSide.Left => PortSide.Right,
        _ => PortSide.Left,
    };

    /// <summary>
    /// Draws one port label offset from the port center using the interior-side formula for
    /// <paramref name="offsetSide"/> (the port's own side for an inward label, the opposite side for an
    /// outward one), so an inward and an outward label on one boundary port sit symmetrically about the
    /// port center. A boundary port (one carrying both an <see cref="LayoutPort.InternalLabel"/> and an
    /// <see cref="LayoutPort.ExternalLabel"/>) reserves extra clearance beyond the plain port-glyph
    /// offset — see <c>SvgRenderer.EmitPortLabel</c>'s matching remarks for why: its external label is
    /// deliberately drawn on the same outward face its external approach edge may terminate with an
    /// end-marker glyph, which would otherwise visually collide with the label text.
    /// </summary>
    private static void DrawPortLabel(SKCanvas canvas, LayoutPort port, string text, PortSide offsetSide, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = SKColor.Parse(theme.StrokeColor);

        // Offset the label far enough from the port square so it does not overlap.
        var offset = NotationMetrics.PortHalfSize + theme.LabelPadding
            + (port.InternalLabel != null && port.ExternalLabel != null ? NotationMetrics.EndMarkerLength : 0.0);
        var (labelX, labelY, align) = offsetSide switch
        {
            PortSide.Top => (port.CentreX, port.CentreY + offset + theme.FontSizeBody, SKTextAlign.Center),
            PortSide.Bottom => (port.CentreX, port.CentreY - offset, SKTextAlign.Center),
            PortSide.Left => (port.CentreX + offset, port.CentreY + theme.FontSizeBody / 2.0, SKTextAlign.Left),
            _ => (port.CentreX - offset, port.CentreY + theme.FontSizeBody / 2.0, SKTextAlign.Right)
        };

        using var textPaint = CreateTextPaint(strokeColor);
        using var font = CreateFont((float)theme.FontSizeBody * scale, bold: false, italic: false);
        var maxLabelWidth = (float)(port.MaxLabelWidth * scale);
        font.Size = FitFontSize(font, text, maxLabelWidth, font.Size);

        // labelY is a *center* Y, matching SvgRenderer.EmitPortLabel's dominant-baseline="middle"
        // semantics (the same offset formula is shared by both renderers). SKCanvas.DrawText instead
        // takes a baseline Y, so drawing at labelY directly would float the glyph body above that
        // center (an ascent-heavy font renders mostly above its baseline) instead of straddling it —
        // exactly the PNG/SVG divergence visible when comparing a port label against its connecting
        // line. Recover the equivalent baseline from the font's own ascent/descent metrics so both
        // renderers place the glyph body identically relative to labelY.
        var metrics = font.Metrics;
        var baselineY = (float)(labelY * scale) - ((metrics.Ascent + metrics.Descent) / 2.0f);
        canvas.DrawText(text, (float)(labelX * scale), baselineY, align, font, textPaint);
    }

    /// <summary>
    /// Renders a <see cref="LayoutBadge"/> as the specified icon shape centered at the badge
    /// position. An optional label is drawn to the right of the bounding circle.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="badge">Badge node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderBadge(SKCanvas canvas, LayoutBadge badge, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = SKColor.Parse(theme.StrokeColor);

        var cx = (float)(badge.CentreX * scale);
        var cy = (float)(badge.CentreY * scale);
        var r = (float)(badge.Size / 2.0 * scale);

        using var strokePaint = new SKPaint();
        strokePaint.Color = strokeColor;
        strokePaint.Style = SKPaintStyle.Stroke;
        strokePaint.StrokeWidth = (float)theme.StrokeWidth * scale;
        strokePaint.IsAntialias = true;

        using var fillPaint = new SKPaint();
        fillPaint.Color = strokeColor;
        fillPaint.Style = SKPaintStyle.Fill;
        fillPaint.IsAntialias = true;

        // Draw the badge shape centered at (cx, cy) within a bounding circle of radius r
        switch (badge.Shape)
        {
            case BadgeShape.FilledCircle:
                canvas.DrawCircle(cx, cy, r, fillPaint);
                break;

            case BadgeShape.Bullseye:
                {
                    // Outer filled circle + white inner circle to create a visible ring
                    canvas.DrawCircle(cx, cy, r, fillPaint);
                    using var innerWhite = new SKPaint();
                    innerWhite.Color = SKColors.White;
                    innerWhite.Style = SKPaintStyle.Fill;
                    innerWhite.IsAntialias = true;
                    canvas.DrawCircle(cx, cy, r * (float)NotationMetrics.BadgeBullseyeInnerFraction, innerWhite);
                    canvas.DrawCircle(cx, cy, r * (float)NotationMetrics.BadgeBullseyeInnerFraction, strokePaint);
                    break;
                }

            case BadgeShape.Diamond:
                {
                    // Open rotated-square diamond with vertices at the compass cardinal points
                    var diamondBuilder = new SKPathBuilder();
                    diamondBuilder.MoveTo(cx, cy - r);       // top
                    diamondBuilder.LineTo(cx + r, cy);       // right
                    diamondBuilder.LineTo(cx, cy + r);       // bottom
                    diamondBuilder.LineTo(cx - r, cy);       // left
                    diamondBuilder.Close();
                    using var p = diamondBuilder.Detach();
                    canvas.DrawPath(p, strokePaint);
                    break;
                }

            case BadgeShape.HorizontalBar:
                canvas.DrawLine(cx - r * (float)NotationMetrics.BadgeBarLengthFraction, cy, cx + r * (float)NotationMetrics.BadgeBarLengthFraction, cy, strokePaint);
                break;

            case BadgeShape.VerticalBar:
                canvas.DrawLine(cx, cy - r * (float)NotationMetrics.BadgeBarLengthFraction, cx, cy + r * (float)NotationMetrics.BadgeBarLengthFraction, strokePaint);
                break;

            default:
                // Unknown badge shapes are skipped for forward compatibility
                break;
        }

        // Draw the optional label to the right of the bounding circle
        if (badge.Label != null)
        {
            using var textPaint = CreateTextPaint(strokeColor);
            using var font = CreateFont((float)theme.FontSizeBody * scale, bold: false, italic: false);
            var labelX = (float)((badge.CentreX + badge.Size / 2.0 + theme.LabelPadding) * scale);
            var labelY = (float)((badge.CentreY + theme.FontSizeBody / 2.0) * scale);
            canvas.DrawText(badge.Label, labelX, labelY, SKTextAlign.Left, font, textPaint);
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutBand"/> as a swim-lane rectangle with an optional label.
    /// For Horizontal bands the label is rendered vertically (rotated 90° CCW) along the
    /// left edge; for Vertical bands it is rendered horizontally at the top. Children are
    /// rendered recursively.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="band">Band node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderBand(SKCanvas canvas, LayoutBand band, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = SKColor.Parse(theme.StrokeColor);

        var rect = new SKRect(
            (float)(band.X * scale),
            (float)(band.Y * scale),
            (float)((band.X + band.Width) * scale),
            (float)((band.Y + band.Height) * scale));

        // Fill band with the primary (depth-0) background color
        using (var fillPaint = new SKPaint())
        {
            fillPaint.Color = SKColor.Parse(theme.DepthFillColors[0]);
            fillPaint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(rect, fillPaint);
        }

        // Draw the band border
        using (var strokePaint = new SKPaint())
        {
            strokePaint.Color = strokeColor;
            strokePaint.Style = SKPaintStyle.Stroke;
            strokePaint.StrokeWidth = (float)theme.StrokeWidth * scale;
            canvas.DrawRect(rect, strokePaint);
        }

        // Draw the optional label; position and rotation depends on band orientation
        if (band.Label != null)
        {
            using var textPaint = CreateTextPaint(strokeColor);
            using var font = CreateFont((float)theme.FontSizeBody * scale, bold: false, italic: false);

            if (band.Orientation == BandOrientation.Horizontal)
            {
                // Vertical text on the left edge: translate to label center, rotate CCW
                var labelCx = (float)((band.X + theme.LabelPadding + theme.FontSizeBody / 2.0) * scale);
                var labelCy = (float)((band.Y + band.Height / 2.0) * scale);
                canvas.Save();
                canvas.Translate(labelCx, labelCy);
                canvas.RotateDegrees(-90);
                canvas.DrawText(band.Label, 0, 0, SKTextAlign.Center, font, textPaint);
                canvas.Restore();
            }
            else
            {
                // Horizontal text at the top of the band
                var textX = (float)((band.X + band.Width / 2.0) * scale);
                var textY = (float)((band.Y + theme.LabelPadding + theme.FontSizeBody) * scale);
                canvas.DrawText(band.Label, textX, textY, SKTextAlign.Center, font, textPaint);
            }
        }

        // Render children recursively
        foreach (var child in band.Children)
        {
            RenderNode(canvas, child, options);
        }
    }

    /// <summary>
    /// Renders a <see cref="LayoutLifeline"/> as a header box centered at
    /// <see cref="LayoutLifeline.CentreX"/> containing the lifeline label, followed by a
    /// dashed vertical stem running from the bottom of the header to
    /// <see cref="LayoutLifeline.BottomY"/>.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="lifeline">Lifeline node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderLifeline(SKCanvas canvas, LayoutLifeline lifeline, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = SKColor.Parse(theme.StrokeColor);

        // Header box: centered at CentreX, top edge at TopY
        var headerLeft = lifeline.CentreX - lifeline.HeaderWidth / 2.0;
        var headerRect = new SKRect(
            (float)(headerLeft * scale),
            (float)(lifeline.TopY * scale),
            (float)((headerLeft + lifeline.HeaderWidth) * scale),
            (float)((lifeline.TopY + lifeline.HeaderHeight) * scale));

        // Fill header with the primary background color
        using (var fillPaint = new SKPaint())
        {
            fillPaint.Color = SKColor.Parse(theme.DepthFillColors[0]);
            fillPaint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(headerRect, fillPaint);
        }

        // Draw header border
        using (var strokePaint = new SKPaint())
        {
            strokePaint.Color = strokeColor;
            strokePaint.Style = SKPaintStyle.Stroke;
            strokePaint.StrokeWidth = (float)theme.StrokeWidth * scale;
            canvas.DrawRect(headerRect, strokePaint);
        }

        // Draw the header label centered within the header box
        using (var textPaint = CreateTextPaint(strokeColor))
        using (var font = CreateFont((float)theme.FontSizeBody * scale, bold: true, italic: false))
        {
            var textX = (float)(lifeline.CentreX * scale);
            var textY = (float)((lifeline.TopY + (lifeline.HeaderHeight + theme.FontSizeBody) / 2.0) * scale);
            canvas.DrawText(lifeline.Label, textX, textY, SKTextAlign.Center, font, textPaint);
        }

        // Dashed vertical stem from the bottom of the header box to BottomY
        using var stemPaint = new SKPaint();
        stemPaint.Color = strokeColor;
        stemPaint.Style = SKPaintStyle.Stroke;
        stemPaint.StrokeWidth = (float)theme.StrokeWidth * scale;
        stemPaint.IsAntialias = true;
        stemPaint.PathEffect = SKPathEffect.CreateDash([6f * scale, 3f * scale], 0);

        var stemX = (float)(lifeline.CentreX * scale);
        canvas.DrawLine(
            stemX, (float)((lifeline.TopY + lifeline.HeaderHeight) * scale),
            stemX, (float)(lifeline.BottomY * scale),
            stemPaint);
    }

    /// <summary>
    /// Renders a <see cref="LayoutActivation"/> as a narrow white-filled rectangle with a
    /// stroke border, centered horizontally at <see cref="LayoutActivation.CentreX"/>.
    /// </summary>
    /// <remarks>
    /// The activation bar width is <c>Theme.LabelPadding * 2</c>, giving it a size that
    /// scales proportionally with the diagram's text padding setting.
    /// </remarks>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="activation">Activation node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderActivation(SKCanvas canvas, LayoutActivation activation, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = SKColor.Parse(theme.StrokeColor);

        // Bar width = LabelPadding * 2, centered at CentreX
        var halfWidth = theme.LabelPadding;
        var rect = new SKRect(
            (float)((activation.CentreX - halfWidth) * scale),
            (float)(activation.TopY * scale),
            (float)((activation.CentreX + halfWidth) * scale),
            (float)(activation.BottomY * scale));

        // White fill indicates the lifeline is active during this time interval
        using (var fillPaint = new SKPaint())
        {
            fillPaint.Color = SKColors.White;
            fillPaint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(rect, fillPaint);
        }

        // Stroke border delineates the bar from surrounding elements
        using var strokePaint = new SKPaint();
        strokePaint.Color = strokeColor;
        strokePaint.Style = SKPaintStyle.Stroke;
        strokePaint.StrokeWidth = (float)theme.StrokeWidth * scale;
        canvas.DrawRect(rect, strokePaint);
    }

    /// <summary>
    /// Renders a <see cref="LayoutGrid"/> as a bordered table. Header rows are filled with
    /// the depth-1 theme color; body rows use the depth-0 color. Each cell's text is
    /// aligned according to <see cref="LayoutGridCell.Align"/> and vertically centered
    /// within the row height.
    /// </summary>
    /// <param name="canvas">Canvas to draw on.</param>
    /// <param name="grid">Grid node to render.</param>
    /// <param name="options">Render options providing theme and scale.</param>
    private static void RenderGrid(SKCanvas canvas, LayoutGrid grid, RenderOptions options)
    {
        var theme = options.Theme;
        var scale = (float)options.Scale;
        var strokeColor = SKColor.Parse(theme.StrokeColor);

        // Header rows use depth-1 color; body rows use depth-0 color
        var headerFill = SKColor.Parse(theme.DepthFillColors[1 % theme.DepthFillColors.Count]);
        var bodyFill = SKColor.Parse(theme.DepthFillColors[0]);

        // Accumulate Y position across rows; X resets at the start of each row
        var currentY = grid.Y;
        foreach (var row in grid.Rows)
        {
            // Row height = maximum cell height in this row
            var rowHeight = 0.0;
            foreach (var cell in row.Cells)
            {
                rowHeight = Math.Max(rowHeight, cell.Height);
            }

            var currentX = grid.X;
            foreach (var cell in row.Cells)
            {
                var cellRect = new SKRect(
                    (float)(currentX * scale),
                    (float)(currentY * scale),
                    (float)((currentX + cell.Width) * scale),
                    (float)((currentY + rowHeight) * scale));

                // Fill cell with header or body background
                using (var fillPaint = new SKPaint())
                {
                    fillPaint.Color = row.IsHeader ? headerFill : bodyFill;
                    fillPaint.Style = SKPaintStyle.Fill;
                    canvas.DrawRect(cellRect, fillPaint);
                }

                // Draw the cell border
                using (var borderPaint = new SKPaint())
                {
                    borderPaint.Color = strokeColor;
                    borderPaint.Style = SKPaintStyle.Stroke;
                    borderPaint.StrokeWidth = (float)theme.StrokeWidth * scale;
                    canvas.DrawRect(cellRect, borderPaint);
                }

                // Draw cell text, horizontally aligned per cell spec and vertically centered
                using (var textPaint = CreateTextPaint(strokeColor))
                using (var font = CreateFont((float)theme.FontSizeBody * scale, bold: row.IsHeader, italic: false))
                {
                    var align = cell.Align switch
                    {
                        TextAlign.Center => SKTextAlign.Center,
                        TextAlign.Right => SKTextAlign.Right,
                        _ => SKTextAlign.Left
                    };

                    var textX = cell.Align switch
                    {
                        TextAlign.Center => currentX + cell.Width / 2.0,
                        TextAlign.Right => currentX + cell.Width - theme.LabelPadding,
                        _ => currentX + theme.LabelPadding
                    };

                    // Vertically center the baseline within the row
                    var textY = currentY + (rowHeight + theme.FontSizeBody) / 2.0;
                    canvas.DrawText(cell.Text, (float)(textX * scale), (float)(textY * scale), align, font, textPaint);
                }

                currentX += cell.Width;
            }

            currentY += rowHeight;
        }
    }
}
