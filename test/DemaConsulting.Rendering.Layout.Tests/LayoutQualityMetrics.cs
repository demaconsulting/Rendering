// <copyright file="LayoutQualityMetrics.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     The layout-quality numbers measured on one placed <see cref="LayoutTree"/>: connector crossings,
///     the largest empty square and rectangle (diagram-quality criterion B4), the leaf-block area ratio
///     (criterion B5), and the canvas size.
/// </summary>
/// <param name="Crossings">Proper crossings between distinct connectors (shared-endpoint touches excluded).</param>
/// <param name="LeafCount">Number of leaf boxes (boxes that contain no other box).</param>
/// <param name="LargestEmptySquare">Side, in logical pixels, of the largest empty axis-aligned square.</param>
/// <param name="LargestEmptyRectangleArea">Area of the largest empty axis-aligned rectangle.</param>
/// <param name="LargestEmptyRectangleWidth">Width of the largest empty axis-aligned rectangle.</param>
/// <param name="LargestEmptyRectangleHeight">Height of the largest empty axis-aligned rectangle.</param>
/// <param name="MeanLeafBlockHeight">Mean height of the leaf boxes.</param>
/// <param name="B4Ratio">The largest empty square side divided by the mean leaf block height (pass: at most 3).</param>
/// <param name="BlockAreaRatio">Total leaf box area divided by the canvas area (pass: at least 0.20).</param>
/// <param name="CanvasWidth">Canvas width.</param>
/// <param name="CanvasHeight">Canvas height.</param>
/// <param name="CanvasArea">Canvas area.</param>
internal sealed record LayoutQualityReport(
    int Crossings,
    int LeafCount,
    double LargestEmptySquare,
    double LargestEmptyRectangleArea,
    double LargestEmptyRectangleWidth,
    double LargestEmptyRectangleHeight,
    double MeanLeafBlockHeight,
    double B4Ratio,
    double BlockAreaRatio,
    double CanvasWidth,
    double CanvasHeight,
    double CanvasArea);

/// <summary>
///     Layout-quality metrics over a placed <see cref="LayoutTree"/>, written against the public API only
///     so the one definition measures the baseline (main) and the branch alike. The occupancy-based
///     metrics rasterize the tree to a 4 px grid in which leaf boxes, connector segments (dilated by one
///     cell), and resolved connector labels are occupied; container outlines are transparent, so a container
///     with free interior space is not "occupied" by its border.
/// </summary>
internal static class LayoutQualityMetrics
{
    /// <summary>Side, in logical pixels, of one occupancy-grid cell.</summary>
    public const double CellSize = 4.0;

    /// <summary>Tolerance, in logical pixels, when classifying a segment as horizontal or vertical.</summary>
    private const double Epsilon = 1e-6;

    /// <summary>Measures every metric of <paramref name="tree"/>.</summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The measured report.</returns>
    public static LayoutQualityReport Measure(LayoutTree tree)
    {
        var leaves = LeafBoxes(tree);
        var meanHeight = MeanLeafBlockHeight(tree);
        var occupancy = BuildOccupancy(tree);
        var square = LargestEmptySquare(occupancy);
        var (rectWidth, rectHeight) = LargestEmptyRectangle(occupancy);
        return new LayoutQualityReport(
            CountCrossings(tree),
            leaves.Count,
            square,
            rectWidth * rectHeight,
            rectWidth,
            rectHeight,
            meanHeight,
            meanHeight > 0.0 ? square / meanHeight : 0.0,
            BlockAreaRatio(tree),
            tree.Width,
            tree.Height,
            CanvasArea(tree));
    }

    /// <summary>
    ///     Counts proper crossings between distinct connectors of the whole flattened tree: two segments
    ///     of different lines that intersect at a point interior to both. Touches at a shared endpoint (two
    ///     connectors meeting at one anchor), a line ending on another line, and collinear overlaps are not
    ///     crossings.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The number of crossings.</returns>
    public static int CountCrossings(LayoutTree tree)
    {
        var lines = AllLines(tree);
        var count = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            for (var j = i + 1; j < lines.Count; j++)
            {
                count += CountCrossings(lines[i].Waypoints, lines[j].Waypoints);
            }
        }

        return count;
    }

    /// <summary>Returns the side of the largest empty axis-aligned square on the 4 px occupancy grid.</summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The side, in logical pixels.</returns>
    public static double LargestEmptySquare(LayoutTree tree) => LargestEmptySquare(BuildOccupancy(tree));

    /// <summary>Returns the width and height of the largest-area empty axis-aligned rectangle on the 4 px grid.</summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The width and height, in logical pixels (both zero when the canvas has no free cell).</returns>
    public static (double Width, double Height) LargestEmptyRectangle(LayoutTree tree) =>
        LargestEmptyRectangle(BuildOccupancy(tree));

    /// <summary>Returns the mean height of the leaf boxes (zero when there are none).</summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The mean leaf block height.</returns>
    public static double MeanLeafBlockHeight(LayoutTree tree)
    {
        var leaves = LeafBoxes(tree);
        return leaves.Count == 0 ? 0.0 : leaves.Sum(box => box.Height) / leaves.Count;
    }

    /// <summary>Returns the total leaf-box area divided by the canvas area (zero for an empty canvas).</summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The block area ratio.</returns>
    public static double BlockAreaRatio(LayoutTree tree)
    {
        var canvas = CanvasArea(tree);
        return canvas <= 0.0 ? 0.0 : LeafBoxes(tree).Sum(box => box.Width * box.Height) / canvas;
    }

    /// <summary>Returns the canvas area, <c>Width * Height</c>.</summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The canvas area.</returns>
    public static double CanvasArea(LayoutTree tree) => tree.Width * tree.Height;

    /// <summary>
    ///     Returns a stable fingerprint of the geometry of the tree: the canvas size, every box (position,
    ///     size, label) and every line (waypoints), rounded to two decimals, hashed with SHA-256. Two trees
    ///     with the same fingerprint are geometrically identical.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The upper-case hexadecimal SHA-256 of the canonical geometry text.</returns>
    public static string Fingerprint(LayoutTree tree)
    {
        var text = new StringBuilder();
        text.Append(Num(tree.Width)).Append('x').Append(Num(tree.Height));
        AppendGeometry(text, tree.Nodes);
        using var hash = SHA256.Create();
        var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
        return BitConverter.ToString(bytes).Replace("-", string.Empty);
    }

    /// <summary>Returns every line of the tree, at every nesting depth.</summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<LayoutLine> AllLines(LayoutTree tree)
    {
        var lines = new List<LayoutLine>();
        Collect(tree.Nodes, null, lines, null);
        return lines;
    }

    /// <summary>Returns every box of the tree, at every nesting depth, outermost first.</summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The boxes.</returns>
    public static IReadOnlyList<LayoutBox> AllBoxes(LayoutTree tree)
    {
        var boxes = new List<LayoutBox>();
        Collect(tree.Nodes, boxes, null, null);
        return boxes;
    }

    /// <summary>Returns the leaf boxes of the tree: boxes whose children contain no box.</summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The leaf boxes.</returns>
    public static IReadOnlyList<LayoutBox> LeafBoxes(LayoutTree tree) =>
        [.. AllBoxes(tree).Where(box => !box.Children.OfType<LayoutBox>().Any())];

    /// <summary>
    ///     Counts the proper crossings between two polylines.
    /// </summary>
    /// <param name="first">The waypoints of the first line.</param>
    /// <param name="second">The waypoints of the second line.</param>
    /// <returns>The number of segment pairs that cross at an interior point of both.</returns>
    private static int CountCrossings(IReadOnlyList<Point2D> first, IReadOnlyList<Point2D> second)
    {
        var count = 0;
        for (var i = 0; i + 1 < first.Count; i++)
        {
            for (var j = 0; j + 1 < second.Count; j++)
            {
                if (SegmentsCross(first[i], first[i + 1], second[j], second[j + 1]))
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>Returns whether two segments cross at a point strictly interior to both.</summary>
    private static bool SegmentsCross(Point2D a1, Point2D a2, Point2D b1, Point2D b2)
    {
        var aHorizontal = Math.Abs(a1.Y - a2.Y) < Epsilon;
        var aVertical = Math.Abs(a1.X - a2.X) < Epsilon;
        var bHorizontal = Math.Abs(b1.Y - b2.Y) < Epsilon;
        var bVertical = Math.Abs(b1.X - b2.X) < Epsilon;
        if ((aHorizontal && bVertical) || (aVertical && bHorizontal))
        {
            var (h1, h2, v1, v2) = aHorizontal ? (a1, a2, b1, b2) : (b1, b2, a1, a2);
            return v1.X > Math.Min(h1.X, h2.X) + Epsilon
                && v1.X < Math.Max(h1.X, h2.X) - Epsilon
                && h1.Y > Math.Min(v1.Y, v2.Y) + Epsilon
                && h1.Y < Math.Max(v1.Y, v2.Y) - Epsilon;
        }

        if ((aHorizontal || aVertical) && (bHorizontal || bVertical))
        {
            // Parallel orthogonal segments never cross (collinear overlap is not a crossing).
            return false;
        }

        // At least one diagonal segment: a general proper intersection (strictly opposite sides on both).
        var d1 = Orientation(b1, b2, a1);
        var d2 = Orientation(b1, b2, a2);
        var d3 = Orientation(a1, a2, b1);
        var d4 = Orientation(a1, a2, b2);
        return d1 * d2 < 0.0 && d3 * d4 < 0.0;
    }

    /// <summary>Returns the signed area of the triangle (a, b, c); its sign tells which side c lies on.</summary>
    private static double Orientation(Point2D a, Point2D b, Point2D c) =>
        ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));

    /// <summary>Builds the occupancy grid of the tree.</summary>
    private static bool[,] BuildOccupancy(LayoutTree tree)
    {
        var columns = Math.Max(1, (int)Math.Ceiling(tree.Width / CellSize));
        var rows = Math.Max(1, (int)Math.Ceiling(tree.Height / CellSize));
        var grid = new bool[columns, rows];

        foreach (var leaf in LeafBoxes(tree))
        {
            Fill(grid, leaf.X, leaf.Y, leaf.X + leaf.Width, leaf.Y + leaf.Height);
        }

        var lines = AllLines(tree);
        foreach (var line in lines)
        {
            for (var i = 0; i + 1 < line.Waypoints.Count; i++)
            {
                var p = line.Waypoints[i];
                var q = line.Waypoints[i + 1];
                Fill(
                    grid,
                    Math.Min(p.X, q.X) - CellSize,
                    Math.Min(p.Y, q.Y) - CellSize,
                    Math.Max(p.X, q.X) + CellSize,
                    Math.Max(p.Y, q.Y) + CellSize);
            }
        }

        var boxes = AllBoxes(tree);
        var ports = new List<LayoutPort>();
        Collect(tree.Nodes, null, null, ports);
        var labels = ConnectorLabelPlacer.ResolveLabels(lines, boxes, ports, Themes.Light);
        foreach (var placement in labels.Midpoints.Values)
        {
            Fill(grid, placement.X - placement.HalfWidth, placement.Y - placement.HalfHeight, placement.X + placement.HalfWidth, placement.Y + placement.HalfHeight);
        }

        foreach (var placement in labels.EndLabels)
        {
            Fill(grid, placement.X - placement.HalfWidth, placement.Y - placement.HalfHeight, placement.X + placement.HalfWidth, placement.Y + placement.HalfHeight);
        }

        return grid;
    }

    /// <summary>Marks every cell touched by the rectangle as occupied (clipped to the canvas).</summary>
    private static void Fill(bool[,] grid, double left, double top, double right, double bottom)
    {
        var columns = grid.GetLength(0);
        var rows = grid.GetLength(1);
        var c0 = Math.Max(0, (int)Math.Floor(left / CellSize));
        var c1 = Math.Min(columns - 1, (int)Math.Ceiling(right / CellSize) - 1);
        var r0 = Math.Max(0, (int)Math.Floor(top / CellSize));
        var r1 = Math.Min(rows - 1, (int)Math.Ceiling(bottom / CellSize) - 1);
        for (var c = c0; c <= c1; c++)
        {
            for (var r = r0; r <= r1; r++)
            {
                grid[c, r] = true;
            }
        }
    }

    /// <summary>The classic dynamic program for the largest all-empty square of a boolean grid.</summary>
    private static double LargestEmptySquare(bool[,] grid)
    {
        var columns = grid.GetLength(0);
        var rows = grid.GetLength(1);
        var previous = new int[rows + 1];
        var best = 0;
        for (var c = 0; c < columns; c++)
        {
            var current = new int[rows + 1];
            for (var r = 0; r < rows; r++)
            {
                if (grid[c, r])
                {
                    continue;
                }

                // current[r + 1] is the square ending at (c, r); previous holds column c - 1.
                var side = 1 + Math.Min(Math.Min(previous[r + 1], current[r]), previous[r]);
                current[r + 1] = side;
                best = Math.Max(best, side);
            }

            previous = current;
        }

        return best * CellSize;
    }

    /// <summary>The histogram method for the largest-area all-empty rectangle of a boolean grid.</summary>
    private static (double Width, double Height) LargestEmptyRectangle(bool[,] grid)
    {
        var columns = grid.GetLength(0);
        var rows = grid.GetLength(1);
        var heights = new int[columns];
        var bestArea = 0;
        var bestWidth = 0;
        var bestHeight = 0;
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < columns; c++)
            {
                heights[c] = grid[c, r] ? 0 : heights[c] + 1;
            }

            var stack = new Stack<int>();
            for (var c = 0; c <= columns; c++)
            {
                var height = c == columns ? 0 : heights[c];
                while (stack.Count > 0 && heights[stack.Peek()] >= height)
                {
                    var barHeight = heights[stack.Pop()];
                    var left = stack.Count == 0 ? 0 : stack.Peek() + 1;
                    var width = c - left;
                    if (barHeight * width > bestArea)
                    {
                        bestArea = barHeight * width;
                        bestWidth = width;
                        bestHeight = barHeight;
                    }
                }

                stack.Push(c);
            }
        }

        return (bestWidth * CellSize, bestHeight * CellSize);
    }

    /// <summary>Formats a coordinate with two decimals in the invariant culture.</summary>
    private static string Num(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Appends the canonical geometry text of a node list, recursing through box children.</summary>
    private static void AppendGeometry(StringBuilder text, IReadOnlyList<LayoutNode> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case LayoutBox box:
                    text.Append("|B").Append(Num(box.X)).Append(',').Append(Num(box.Y)).Append(',')
                        .Append(Num(box.Width)).Append(',').Append(Num(box.Height)).Append(',').Append(box.Label).Append('[');
                    AppendGeometry(text, box.Children);
                    text.Append(']');
                    break;
                case LayoutLine line:
                    text.Append("|L");
                    foreach (var point in line.Waypoints)
                    {
                        text.Append('(').Append(Num(point.X)).Append(',').Append(Num(point.Y)).Append(')');
                    }

                    break;
                case LayoutPort port:
                    text.Append("|P").Append(Num(port.CentreX)).Append(',').Append(Num(port.CentreY)).Append(',').Append(port.Side);
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>Collects the boxes, lines, and ports of a node list, recursing through box children.</summary>
    private static void Collect(
        IReadOnlyList<LayoutNode> nodes,
        List<LayoutBox>? boxes,
        List<LayoutLine>? lines,
        List<LayoutPort>? ports)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case LayoutBox box:
                    boxes?.Add(box);
                    Collect(box.Children, boxes, lines, ports);
                    break;
                case LayoutLine line:
                    lines?.Add(line);
                    break;
                case LayoutPort port:
                    ports?.Add(port);
                    break;
                default:
                    break;
            }
        }
    }
}
