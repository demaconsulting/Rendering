// <copyright file="CanvasFitter.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout.Engine;

/// <summary>
/// Grows a placed <see cref="LayoutTree"/>'s canvas so that every connector waypoint and every connector
/// label (midpoint and end labels) lies fully inside it, translating the whole tree when content reaches
/// above or left of the origin.
/// </summary>
/// <remarks>
///     <para>
///     The canvas a layout reports is computed from the placed boxes (plus the engine's own padding).
///     Two kinds of content can still fall outside it: a connector that the router detours around the
///     outer edge of a container wall (its keep-out lane lies beyond the outermost box, so for a box near
///     the origin it lies at a negative coordinate), and a connector label that the renderers place beside
///     its line (a label at the left or top edge of the diagram extends past the origin, and one at the
///     right or bottom edge past the declared width or height). Renderers can only grow a canvas to the
///     right and down, so anything at a negative coordinate was clipped.
///     </para>
///     <para>
///     <see cref="Fit"/> resolves the labels with <see cref="ConnectorLabelPlacer.ResolveLabels"/>, the one
///     resolver the SVG and raster renderers also call (same obstacle boxes, port-glyph and outward port label
///     rectangles, and ordering; the light theme with the caller's assumed font size), so the label positions
///     it measures are the positions the renderers draw. It measures the extent of every waypoint, label
///     rectangle, port glyph and outward port label, and — only when something lies
///     outside <c>[0, Width] x [0, Height]</c> — translates the tree so the content starts a
///     <see cref="FitMargin"/> inside the origin and widens the canvas to a <see cref="FitMargin"/> beyond the
///     far content. A tree whose content is already inside its canvas is returned unchanged (the same
///     instance), so every well-behaved layout is byte-identical to its previous output.
///     </para>
///     <para>
///     Given a <c>padding</c>, <see cref="Fit"/> goes one step further: the canvas becomes exactly the drawn
///     content (boxes included) plus that padding on every side. The layered engine reflects a Left or Up flow
///     about the far edge of its content, which leaves the content one padding off-center (against the top or
///     left edge, with a double margin at the opposite edge), and a connector detour or a label can reach into
///     the padding band without leaving the canvas; the padded fit puts all of these back to one padding, so
///     the canvas never carries an empty ring around the diagram nor lets content touch an edge. A tree whose
///     margins already equal the padding is returned unchanged (the same instance), so a layout that already
///     hugs its content is byte-identical. <see cref="MeasureMargin"/> reports the smallest margin of a tree:
///     the padding a leaf algorithm kept around its own output, which the hierarchical algorithm keeps around
///     the composed result.
///     </para>
///     <para>
///     A tree containing a node type outside the closed set <see cref="LayoutTreePacker"/> can translate
///     (<see cref="LayoutBox"/>, <see cref="LayoutLine"/>, <see cref="LayoutPort"/>) is returned unchanged:
///     the bundled algorithms never emit one, and leaving such a tree alone is safer than mis-placing it.
///     </para>
/// </remarks>
internal static class CanvasFitter
{
    /// <summary>
    /// Clearance, in logical pixels, kept between content that had to be brought back inside the canvas
    /// and the canvas edge: half of the engine's uniform padding, enough that a connector stroke or an
    /// arrowhead on the edge is never clipped.
    /// </summary>
    internal const double FitMargin = 10.0;

    /// <summary>
    /// Tolerance, in logical pixels, under which a margin counts as already equal to the requested padding
    /// (so a tree that hugs its content is returned as the same instance despite floating point noise).
    /// </summary>
    private const double HugTolerance = 0.005;

    /// <summary>
    /// Returns <paramref name="tree"/> with its canvas grown (and its content translated when needed) so that
    /// every connector waypoint and connector label lies inside it; or, when <paramref name="padding"/> is
    /// given, with its canvas exactly the drawn content plus that padding on every side.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    /// <param name="fontSize">The body font size, in logical pixels, used to size connector labels.</param>
    /// <param name="padding">
    /// When not <see langword="null"/>, the distance, in logical pixels, to keep between every edge of the
    /// canvas and the drawn content (boxes, connector waypoints and resolved labels, port glyphs and outward
    /// port labels): the tree is translated until its content starts <paramref name="padding"/> inside the
    /// origin and the canvas is sized to end <paramref name="padding"/> beyond the far content, so a canvas
    /// that was too small is grown, one with a surplus ring is shrunk, and content that sits against one edge
    /// (the reflection of a Left or Up flow leaves the content one padding off-center) is centered. A tree
    /// whose margins already equal <paramref name="padding"/> is returned unchanged. When
    /// <see langword="null"/>, only content outside the canvas is brought inside, with <see cref="FitMargin"/>.
    /// </param>
    /// <returns>
    /// <paramref name="tree"/> itself when nothing needs to change; otherwise a fitted copy.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="tree"/> is <see langword="null"/>.</exception>
    internal static LayoutTree Fit(LayoutTree tree, double fontSize, double? padding = null)
    {
        ArgumentNullException.ThrowIfNull(tree);

        if (!TryMeasure(tree, fontSize, includeBoxes: padding.HasValue, bendInset: 0.0, out var minX, out var minY, out var maxX, out var maxY))
        {
            return tree;
        }

        if (padding is { } hug)
        {
            return Hug(tree, hug, minX, minY, maxX, maxY);
        }

        if (minX >= 0.0 && minY >= 0.0 && maxX <= tree.Width && maxY <= tree.Height)
        {
            return tree;
        }

        // Shift only on an axis whose content starts before the origin, and widen only on an axis whose
        // content ends beyond the declared size (after the shift).
        var dx = minX < 0.0 ? FitMargin - minX : 0.0;
        var dy = minY < 0.0 ? FitMargin - minY : 0.0;
        var width = tree.Width + dx;
        var height = tree.Height + dy;
        if (maxX + dx > width)
        {
            width = maxX + dx + FitMargin;
        }

        if (maxY + dy > height)
        {
            height = maxY + dy + FitMargin;
        }

        var nodes = dx == 0.0 && dy == 0.0
            ? tree.Nodes
            : [.. tree.Nodes.Select(node => LayoutTreePacker.TranslateNode(node, dx, dy))];
        return tree with { Width = width, Height = height, Nodes = nodes };
    }

    /// <summary>
    /// Returns the smallest distance between the drawn content of <paramref name="tree"/> (boxes, connector
    /// waypoints and resolved labels, port glyphs and outward port labels) and any edge of its canvas: the
    /// padding a leaf algorithm kept around its own output.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    /// <param name="fontSize">The body font size, in logical pixels, used to size connector labels.</param>
    /// <returns>The smallest margin, or <see langword="null"/> when the tree has no measurable content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tree"/> is <see langword="null"/>.</exception>
    internal static double? MeasureMargin(LayoutTree tree, double fontSize)
    {
        ArgumentNullException.ThrowIfNull(tree);

        return TryMeasure(tree, fontSize, includeBoxes: true, bendInset: 0.0, out var minX, out var minY, out var maxX, out var maxY)
            ? Math.Min(Math.Min(minX, minY), Math.Min(tree.Width - maxX, tree.Height - maxY))
            : null;
    }

    /// <summary>
    /// Translates and resizes <paramref name="tree"/> so its measured content has exactly
    /// <paramref name="padding"/> to every canvas edge, returning the tree itself when it already does.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    /// <param name="padding">The margin to keep on every side.</param>
    /// <param name="minX">The leftmost content coordinate.</param>
    /// <param name="minY">The topmost content coordinate.</param>
    /// <param name="maxX">The rightmost content coordinate.</param>
    /// <param name="maxY">The lowest content coordinate.</param>
    /// <returns>The tree, or a translated and resized copy.</returns>
    private static LayoutTree Hug(LayoutTree tree, double padding, double minX, double minY, double maxX, double maxY)
    {
        var dx = Math.Abs(padding - minX) <= HugTolerance ? 0.0 : padding - minX;
        var dy = Math.Abs(padding - minY) <= HugTolerance ? 0.0 : padding - minY;
        var width = maxX + dx + padding;
        var height = maxY + dy + padding;
        if (dx == 0.0 && dy == 0.0 && Math.Abs(width - tree.Width) <= HugTolerance && Math.Abs(height - tree.Height) <= HugTolerance)
        {
            return tree;
        }

        var nodes = dx == 0.0 && dy == 0.0
            ? tree.Nodes
            : [.. tree.Nodes.Select(node => LayoutTreePacker.TranslateNode(node, dx, dy))];
        return tree with { Width = width, Height = height, Nodes = nodes };
    }

    /// <summary>
    /// Returns <paramref name="tree"/> translated so its drawn content (boxes, connector waypoints and
    /// resolved labels, port glyphs and outward port labels) starts at the origin, with the canvas sized to
    /// exactly that content: the layout's own canvas padding is trimmed so the caller can add exactly one
    /// padding of its own choosing.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    /// <param name="fontSize">The body font size, in logical pixels, used to size connector labels.</param>
    /// <param name="bendInset">
    /// How far a connector bend counts as extending beyond its position (see <c>TryMeasure</c>): the margin a
    /// border must keep from a bend beyond the margin it keeps from a box.
    /// </param>
    /// <returns>
    /// The trimmed copy, or <paramref name="tree"/> itself when it has no measurable content or contains a
    /// node type the packer cannot translate.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="tree"/> is <see langword="null"/>.</exception>
    internal static LayoutTree TrimToContent(LayoutTree tree, double fontSize, double bendInset = 0.0)
    {
        ArgumentNullException.ThrowIfNull(tree);

        if (!TryMeasure(tree, fontSize, includeBoxes: true, bendInset, out var minX, out var minY, out var maxX, out var maxY))
        {
            return tree;
        }

        var width = maxX - minX;
        var height = maxY - minY;
        if (minX == 0.0 && minY == 0.0 && Math.Abs(width - tree.Width) < 1e-9 && Math.Abs(height - tree.Height) < 1e-9)
        {
            return tree;
        }

        var nodes = minX == 0.0 && minY == 0.0
            ? tree.Nodes
            : [.. tree.Nodes.Select(node => LayoutTreePacker.TranslateNode(node, -minX, -minY))];
        return tree with { Width = width, Height = height, Nodes = nodes };
    }

    /// <summary>
    /// Measures the extent of the drawn content of <paramref name="tree"/>: connector waypoints, resolved
    /// connector labels (through the one resolver the renderers use), port glyphs and outward port labels,
    /// and optionally every box.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    /// <param name="fontSize">The body font size, in logical pixels, used to size connector labels.</param>
    /// <param name="includeBoxes">Whether box rectangles count as content.</param>
    /// <param name="bendInset">
    /// How far, in logical pixels, a connector bend (any waypoint that is not one of the two ends) counts as
    /// extending beyond its position, so a caller can keep a wider margin from bends than from boxes.
    /// </param>
    /// <param name="minX">Receives the leftmost content coordinate.</param>
    /// <param name="minY">Receives the topmost content coordinate.</param>
    /// <param name="maxX">Receives the rightmost content coordinate.</param>
    /// <param name="maxY">Receives the lowest content coordinate.</param>
    /// <returns>
    /// <see langword="false"/> when the tree has an unrecognized node type or nothing to measure (without
    /// <paramref name="includeBoxes"/>, a tree with neither lines nor ports).
    /// </returns>
    private static bool TryMeasure(
        LayoutTree tree,
        double fontSize,
        bool includeBoxes,
        double bendInset,
        out double minX,
        out double minY,
        out double maxX,
        out double maxY)
    {
        minX = double.PositiveInfinity;
        minY = double.PositiveInfinity;
        maxX = double.NegativeInfinity;
        maxY = double.NegativeInfinity;

        var boxes = new List<LayoutBox>();
        var lines = new List<LayoutLine>();
        var ports = new List<LayoutPort>();
        if (!Collect(tree.Nodes, boxes, lines, ports) ||
            (lines.Count == 0 && ports.Count == 0 && !(includeBoxes && boxes.Count > 0)))
        {
            return false;
        }

        // Locals cannot be captured from out parameters, so the extents accumulate in plain locals.
        var left = double.PositiveInfinity;
        var top = double.PositiveInfinity;
        var right = double.NegativeInfinity;
        var bottom = double.NegativeInfinity;

        void Include(double x1, double y1, double x2, double y2)
        {
            left = Math.Min(left, x1);
            top = Math.Min(top, y1);
            right = Math.Max(right, x2);
            bottom = Math.Max(bottom, y2);
        }

        void IncludePlacement(double x, double y, double halfWidth, double halfHeight) =>
            Include(x - halfWidth, y - halfHeight, x + halfWidth, y + halfHeight);

        if (includeBoxes)
        {
            foreach (var box in boxes)
            {
                Include(box.X, box.Y, box.X + box.Width, box.Y + box.Height);
            }
        }

        foreach (var points in lines.Select(line => line.Waypoints))
        {
            for (var i = 0; i < points.Count; i++)
            {
                // A bend (not an end on a box face) may be required to keep a wider margin from the
                // enclosing border than a box does.
                var inset = i > 0 && i < points.Count - 1 ? bendInset : 0.0;
                Include(points[i].X - inset, points[i].Y - inset, points[i].X + inset, points[i].Y + inset);
            }
        }

        // Resolve every label through the one shared resolver the renderers use (same obstacle boxes, port
        // glyph and outward port label rectangles, and ordering), so the positions measured here are the
        // positions the renderers draw.
        var theme = Themes.Light with { FontSizeBody = fontSize };
        if (lines.Exists(line => line.MidpointLabel is not null || line.SourceEndLabel is not null || line.TargetEndLabel is not null))
        {
            var labelLayout = ConnectorLabelPlacer.ResolveLabels(lines, boxes, ports, theme);
            foreach (var placement in labelLayout.Midpoints.Values)
            {
                IncludePlacement(placement.X, placement.Y, placement.HalfWidth, placement.HalfHeight);
            }

            foreach (var placement in labelLayout.EndLabels)
            {
                IncludePlacement(placement.X, placement.Y, placement.HalfWidth, placement.HalfHeight);
            }
        }

        // Port glyphs and outward port labels are drawn content too.
        foreach (var port in ports)
        {
            var glyph = NotationMetrics.PortGlyphBounds(port);
            IncludePlacement(glyph.X, glyph.Y, glyph.HalfWidth, glyph.HalfHeight);
            if (NotationMetrics.IsOutwardLabel(port))
            {
                var label = NotationMetrics.PortOutwardLabelBounds(port, port.ExternalLabel!, theme);
                IncludePlacement(label.X, label.Y, label.HalfWidth, label.HalfHeight);
            }
        }

        minX = left;
        minY = top;
        maxX = right;
        maxY = bottom;
        return !double.IsInfinity(left);
    }

    /// <summary>
    /// Collects every box, line, and port of <paramref name="nodes"/> (recursing through box children).
    /// </summary>
    /// <param name="nodes">The nodes to walk.</param>
    /// <param name="boxes">Receives every box.</param>
    /// <param name="lines">Receives every line.</param>
    /// <param name="ports">Receives every port.</param>
    /// <returns><see langword="false"/> when a node of an unrecognized type was found.</returns>
    private static bool Collect(
        IReadOnlyList<LayoutNode> nodes,
        List<LayoutBox> boxes,
        List<LayoutLine> lines,
        List<LayoutPort> ports)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case LayoutBox box:
                    boxes.Add(box);
                    if (!Collect(box.Children, boxes, lines, ports))
                    {
                        return false;
                    }

                    break;
                case LayoutLine line:
                    lines.Add(line);
                    break;
                case LayoutPort port:
                    ports.Add(port);
                    break;
                default:
                    return false;
            }
        }

        return true;
    }
}
