// <copyright file="LayoutGeometryAssert.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Whole-tree geometry assertions shared by the layout tests: no two sibling boxes overlap, every box,
///     connector point, and connector label lies inside the canvas, and no label overlaps a box or another
///     label. They turn the "no overlaps, nothing clipped" visual review criteria into assertions over a
///     placed <see cref="LayoutTree"/>, resolving connector labels the way the renderers do (without the port-glyph clearance rectangles, which only matter for graphs with ports).
/// </summary>
internal static class LayoutGeometryAssert
{
    /// <summary>Tolerance, in logical pixels, for "inside the canvas" and "not overlapping".</summary>
    private const double Tolerance = 0.01;

    /// <summary>A placed connector label rectangle.</summary>
    /// <param name="Text">The label text.</param>
    /// <param name="Left">Left edge.</param>
    /// <param name="Top">Top edge.</param>
    /// <param name="Right">Right edge.</param>
    /// <param name="Bottom">Bottom edge.</param>
    internal readonly record struct LabelRect(string Text, double Left, double Top, double Right, double Bottom);

    /// <summary>Returns every box of the tree, at every nesting depth.</summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The boxes, outermost first.</returns>
    public static IReadOnlyList<LayoutBox> AllBoxes(LayoutTree tree)
    {
        var boxes = new List<LayoutBox>();
        CollectBoxes(tree.Nodes, boxes);
        return boxes;
    }

    /// <summary>Returns every line of the tree, at every nesting depth.</summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<LayoutLine> AllLines(LayoutTree tree)
    {
        var lines = new List<LayoutLine>();
        CollectLines(tree.Nodes, lines);
        return lines;
    }

    /// <summary>
    ///     Resolves the rectangle of every midpoint and end label the renderers would draw for the tree,
    ///     using the light theme's body font and marker scale (the layout's default assumption).
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The label rectangles.</returns>
    public static IReadOnlyList<LabelRect> LabelRects(LayoutTree tree)
    {
        var lines = AllLines(tree);
        var boxes = AllBoxes(tree);
        var scale = NotationMetrics.MarkerScale(Themes.Light);
        var font = Themes.Light.FontSizeBody;

        var rects = new List<LabelRect>();
        var midpoints = ConnectorLabelPlacer.Place(lines, font, boxes, scale);
        foreach (var (line, placement) in midpoints)
        {
            rects.Add(new LabelRect(
                line.MidpointLabel!,
                placement.X - placement.HalfWidth,
                placement.Y - placement.HalfHeight,
                placement.X + placement.HalfWidth,
                placement.Y + placement.HalfHeight));
        }

        var ends = ConnectorLabelPlacer.PlaceEndLabels(lines, font, boxes, scale, midpoints.Values);
        foreach (var end in ends)
        {
            rects.Add(new LabelRect(
                end.Text,
                end.X - end.HalfWidth,
                end.Y - end.HalfHeight,
                end.X + end.HalfWidth,
                end.Y + end.HalfHeight));
        }

        return rects;
    }

    /// <summary>Asserts that no two sibling boxes (at any depth) of the tree overlap.</summary>
    /// <param name="tree">The placed tree.</param>
    public static void AssertNoSiblingOverlap(LayoutTree tree) =>
        AssertNoSiblingOverlap(tree.Nodes);

    /// <summary>
    ///     Asserts that every box, every connector waypoint, and every connector label rectangle lies inside
    ///     the canvas <c>[0, Width] x [0, Height]</c>.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    public static void AssertWithinCanvas(LayoutTree tree)
    {
        foreach (var box in AllBoxes(tree))
        {
            Assert.True(
                box.X >= -Tolerance && box.Y >= -Tolerance &&
                box.X + box.Width <= tree.Width + Tolerance && box.Y + box.Height <= tree.Height + Tolerance,
                $"box '{box.Label}' [{box.X:F1},{box.Y:F1},{box.Width:F1},{box.Height:F1}] is outside the {tree.Width:F1}x{tree.Height:F1} canvas");
        }

        foreach (var line in AllLines(tree))
        {
            foreach (var point in line.Waypoints)
            {
                Assert.True(
                    point.X >= -Tolerance && point.Y >= -Tolerance &&
                    point.X <= tree.Width + Tolerance && point.Y <= tree.Height + Tolerance,
                    $"connector point ({point.X:F1},{point.Y:F1}) '{line.MidpointLabel}' is outside the {tree.Width:F1}x{tree.Height:F1} canvas");
            }
        }

        foreach (var label in LabelRects(tree))
        {
            Assert.True(
                label.Left >= -Tolerance && label.Top >= -Tolerance &&
                label.Right <= tree.Width + Tolerance && label.Bottom <= tree.Height + Tolerance,
                $"label '{label.Text}' [{label.Left:F1},{label.Top:F1},{label.Right:F1},{label.Bottom:F1}] is outside the {tree.Width:F1}x{tree.Height:F1} canvas");
        }
    }

    /// <summary>
    ///     Asserts that no connector label overlaps a leaf box (a box with no nested boxes) or another label.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    public static void AssertLabelsClear(LayoutTree tree)
    {
        var leaves = AllBoxes(tree).Where(b => !b.Children.OfType<LayoutBox>().Any()).ToList();
        var labels = LabelRects(tree);
        for (var i = 0; i < labels.Count; i++)
        {
            var label = labels[i];
            foreach (var box in leaves)
            {
                Assert.False(
                    Intersects(label.Left, label.Top, label.Right, label.Bottom, box.X, box.Y, box.X + box.Width, box.Y + box.Height),
                    $"label '{label.Text}' overlaps box '{box.Label}'");
            }

            for (var j = i + 1; j < labels.Count; j++)
            {
                var other = labels[j];
                Assert.False(
                    Intersects(label.Left, label.Top, label.Right, label.Bottom, other.Left, other.Top, other.Right, other.Bottom),
                    $"label '{label.Text}' overlaps label '{other.Text}'");
            }
        }
    }

    /// <summary>Runs every geometry assertion: no sibling overlap, inside the canvas, labels clear.</summary>
    /// <param name="tree">The placed tree.</param>
    public static void AssertClean(LayoutTree tree)
    {
        AssertNoSiblingOverlap(tree);
        AssertWithinCanvas(tree);
        AssertLabelsClear(tree);
    }

    /// <summary>The engine's uniform outer canvas padding, in logical pixels.</summary>
    public const double CanvasPadding = 20.0;

    /// <summary>
    ///     Asserts that the canvas of a final layout hugs its content whatever algorithm placed it: no margin
    ///     between the drawn content and a canvas edge exceeds <paramref name="maxMargin"/> (no empty ring of
    ///     canvas, and no more room on one side than the layout's padding) and none is smaller than
    ///     <paramref name="minMargin"/> (no content touching an edge). Used for the gallery, whose diagrams are
    ///     placed by algorithms with different paddings: the layered engine keeps 20 px, the containment
    ///     algorithm 12 px, and 4 px beside a connector that wraps around its boxes.
    /// </summary>
    /// <param name="tree">The final placed tree.</param>
    /// <param name="minMargin">The smallest acceptable margin, in logical pixels.</param>
    /// <param name="maxMargin">The largest acceptable margin, in logical pixels.</param>
    /// <param name="tolerance">The permitted deviation, in logical pixels.</param>
    /// <param name="name">A name for failure messages.</param>
    public static void AssertCanvasMarginsWithin(LayoutTree tree, double minMargin = 4.0, double maxMargin = CanvasPadding, double tolerance = 1.0, string? name = null)
    {
        var margins = LayoutQualityMetrics.Margins(tree);
        var smallest = Math.Min(Math.Min(margins.Left, margins.Top), Math.Min(margins.Right, margins.Bottom));
        Assert.True(
            smallest >= minMargin - tolerance && margins.Largest <= maxMargin + tolerance,
            $"{name}: the {tree.Width:0.#}x{tree.Height:0.#} canvas does not hug its content (margins {margins}, expected {minMargin:0.#} to {maxMargin:0.#} px on every side)");
    }

    /// <summary>
    ///     Asserts that the canvas of a final layout hugs its content: the distance between the drawn content
    ///     (boxes, connector waypoints, resolved connector labels, ports and outward port labels, see
    ///     <see cref="LayoutQualityMetrics.Margins"/>) and every canvas edge equals
    ///     <paramref name="padding"/> within <paramref name="tolerance"/>, so no empty ring of canvas surrounds
    ///     the diagram and no content touches an edge.
    /// </summary>
    /// <param name="tree">The final placed tree.</param>
    /// <param name="padding">The expected margin on every side, in logical pixels.</param>
    /// <param name="tolerance">The permitted deviation, in logical pixels.</param>
    /// <param name="name">A name for failure messages.</param>
    public static void AssertCanvasHugsContent(LayoutTree tree, double padding = CanvasPadding, double tolerance = 1.0, string? name = null)
    {
        var margins = LayoutQualityMetrics.Margins(tree);
        Assert.True(
            Math.Abs(margins.Left - padding) <= tolerance
            && Math.Abs(margins.Top - padding) <= tolerance
            && Math.Abs(margins.Right - padding) <= tolerance
            && Math.Abs(margins.Bottom - padding) <= tolerance,
            $"{name}: the {tree.Width:0.#}x{tree.Height:0.#} canvas does not hug its content (expected {padding:0.#} px on every side): margins {margins}");
    }

    /// <summary>
    ///     Asserts that arrowheads (connector target ends carrying a marker) that land on the same face of the
    ///     same box are at least <paramref name="minimum"/> apart, so parallel connectors into one node never
    ///     crowd their markers.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    /// <param name="minimum">The minimum distance between two arrowhead tips on one face, in logical pixels.</param>
    public static void AssertArrowheadsApart(LayoutTree tree, double minimum)
    {
        var boxes = AllBoxes(tree);
        var tips = new List<(LayoutBox Box, char Face, Point2D Tip)>();
        foreach (var line in AllLines(tree).Where(l => l.TargetEnd != EndMarkerStyle.None))
        {
            var tip = line.Waypoints[^1];
            foreach (var box in boxes)
            {
                var face = FaceOf(box, tip);
                if (face != '\0')
                {
                    tips.Add((box, face, tip));
                    break;
                }
            }
        }

        for (var i = 0; i < tips.Count; i++)
        {
            for (var j = i + 1; j < tips.Count; j++)
            {
                if (!ReferenceEquals(tips[i].Box, tips[j].Box) || tips[i].Face != tips[j].Face)
                {
                    continue;
                }

                var distance = Math.Sqrt(Math.Pow(tips[i].Tip.X - tips[j].Tip.X, 2) + Math.Pow(tips[i].Tip.Y - tips[j].Tip.Y, 2));
                Assert.True(
                    distance >= minimum,
                    $"arrowheads on the {tips[i].Face} face of '{tips[i].Box.Label}' are only {distance:F1}px apart");
            }
        }
    }
    private static void AssertNoSiblingOverlap(IReadOnlyList<LayoutNode> nodes)
    {
        var siblings = nodes.OfType<LayoutBox>().ToList();
        for (var i = 0; i < siblings.Count; i++)
        {
            var a = siblings[i];
            for (var j = i + 1; j < siblings.Count; j++)
            {
                var b = siblings[j];
                Assert.False(
                    Intersects(a.X, a.Y, a.X + a.Width, a.Y + a.Height, b.X, b.Y, b.X + b.Width, b.Y + b.Height),
                    $"boxes '{a.Label}' [{a.X:F1},{a.Y:F1},{a.Width:F1},{a.Height:F1}] and '{b.Label}' [{b.X:F1},{b.Y:F1},{b.Width:F1},{b.Height:F1}] overlap");
            }

            AssertNoSiblingOverlap(a.Children);
        }
    }

    /// <summary>Returns the face ('L', 'R', 'T', 'B') of <paramref name="box"/> a point lies on, or NUL when none.</summary>
    private static char FaceOf(LayoutBox box, Point2D point)
    {
        const double OnEdge = 0.5;
        var withinX = point.X >= box.X - OnEdge && point.X <= box.X + box.Width + OnEdge;
        var withinY = point.Y >= box.Y - OnEdge && point.Y <= box.Y + box.Height + OnEdge;
        if (withinY && Math.Abs(point.X - box.X) <= OnEdge)
        {
            return 'L';
        }

        if (withinY && Math.Abs(point.X - (box.X + box.Width)) <= OnEdge)
        {
            return 'R';
        }

        if (withinX && Math.Abs(point.Y - box.Y) <= OnEdge)
        {
            return 'T';
        }

        return withinX && Math.Abs(point.Y - (box.Y + box.Height)) <= OnEdge ? 'B' : '\0';
    }
    private static bool Intersects(
        double aLeft,
        double aTop,
        double aRight,
        double aBottom,
        double bLeft,
        double bTop,
        double bRight,
        double bBottom) =>
        aLeft < bRight - Tolerance && aRight > bLeft + Tolerance &&
        aTop < bBottom - Tolerance && aBottom > bTop + Tolerance;

    private static void CollectBoxes(IReadOnlyList<LayoutNode> nodes, List<LayoutBox> boxes)
    {
        foreach (var node in nodes)
        {
            if (node is LayoutBox box)
            {
                boxes.Add(box);
                CollectBoxes(box.Children, boxes);
            }
        }
    }

    private static void CollectLines(IReadOnlyList<LayoutNode> nodes, List<LayoutLine> lines)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case LayoutLine line:
                    lines.Add(line);
                    break;
                case LayoutBox box:
                    CollectLines(box.Children, lines);
                    break;
            }
        }
    }
}
