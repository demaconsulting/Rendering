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
    /// Returns <paramref name="tree"/> with its canvas grown (and its content translated when needed) so that
    /// every connector waypoint and connector label lies inside it.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    /// <param name="fontSize">The body font size, in logical pixels, used to size connector labels.</param>
    /// <returns>
    /// <paramref name="tree"/> itself when its content is already inside the canvas; otherwise a fitted copy.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="tree"/> is <see langword="null"/>.</exception>
    internal static LayoutTree Fit(LayoutTree tree, double fontSize)
    {
        ArgumentNullException.ThrowIfNull(tree);

        var boxes = new List<LayoutBox>();
        var lines = new List<LayoutLine>();
        var ports = new List<LayoutPort>();
        if (!Collect(tree.Nodes, boxes, lines, ports) || (lines.Count == 0 && ports.Count == 0))
        {
            return tree;
        }

        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;

        void Include(double left, double top, double right, double bottom)
        {
            minX = Math.Min(minX, left);
            minY = Math.Min(minY, top);
            maxX = Math.Max(maxX, right);
            maxY = Math.Max(maxY, bottom);
        }

        void IncludePlacement(double x, double y, double halfWidth, double halfHeight) =>
            Include(x - halfWidth, y - halfHeight, x + halfWidth, y + halfHeight);

        foreach (var line in lines)
        {
            foreach (var point in line.Waypoints)
            {
                Include(point.X, point.Y, point.X, point.Y);
            }
        }

        // Resolve every label through the one shared resolver the renderers use (same obstacle boxes, port
        // glyph and outward port label rectangles, and ordering), so the positions measured here are the
        // positions the renderers draw.
        var theme = Themes.Light with { FontSizeBody = fontSize };
        var labelLayout = ConnectorLabelPlacer.ResolveLabels(lines, boxes, ports, theme);
        foreach (var placement in labelLayout.Midpoints.Values)
        {
            IncludePlacement(placement.X, placement.Y, placement.HalfWidth, placement.HalfHeight);
        }

        foreach (var placement in labelLayout.EndLabels)
        {
            IncludePlacement(placement.X, placement.Y, placement.HalfWidth, placement.HalfHeight);
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
