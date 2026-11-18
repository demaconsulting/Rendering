// <copyright file="EmptySpaceMeter.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout.Engine;

/// <summary>
/// Measures the largest empty axis-aligned square of a placed <see cref="LayoutTree"/> on a coarse
/// occupancy grid, the quantity behind the "no large empty areas" diagram-quality criterion (B4).
/// </summary>
/// <remarks>
///     <para>
///     A grid cell is occupied when it is touched by a leaf box (a box without nested boxes), by a
///     connector segment dilated by one cell, or by a resolved connector label. Container outlines are
///     transparent: a container's interior is empty space unless something inside it occupies it. The
///     largest all-empty square is found with the classic dynamic program over the grid.
///     </para>
///     <para>
///     The ordering refinement of <see cref="HierarchicalLayoutAlgorithm"/> uses the measure to refuse a
///     reordering that, while it removes crossings, opens a larger hole in the drawing than the layout it
///     would replace.
///     </para>
/// </remarks>
internal static class EmptySpaceMeter
{
    /// <summary>
    /// Side, in logical pixels, of one occupancy-grid cell.
    /// </summary>
    internal const double CellSize = 4.0;

    /// <summary>
    /// The diagram-quality limit of the largest empty square, as a multiple of the mean leaf block height.
    /// </summary>
    internal const double PassingSquareRatio = 3.0;

    /// <summary>
    /// Returns the side of the largest empty square that still meets the diagram-quality limit
    /// (<see cref="PassingSquareRatio"/> times the mean height of the leaf boxes of
    /// <paramref name="tree"/>), or zero when the tree has no leaf box.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The side, in logical pixels.</returns>
    internal static double PassingSquare(LayoutTree tree)
    {
        var boxes = new List<LayoutBox>();
        var lines = new List<LayoutLine>();
        var ports = new List<LayoutPort>();
        Collect(tree.Nodes, boxes, lines, ports);
        var leaves = boxes.Where(candidate => !candidate.Children.OfType<LayoutBox>().Any()).ToList();
        return leaves.Count == 0 ? 0.0 : PassingSquareRatio * leaves.Average(leaf => leaf.Height);
    }

    /// <summary>
    /// Returns the side of the largest empty axis-aligned square of <paramref name="tree"/>.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The side, in logical pixels, a multiple of <see cref="CellSize"/>.</returns>
    internal static double LargestEmptySquare(LayoutTree tree)
    {
        var columns = Math.Max(1, (int)Math.Ceiling(tree.Width / CellSize));
        var rows = Math.Max(1, (int)Math.Ceiling(tree.Height / CellSize));
        var grid = new bool[columns, rows];

        var boxes = new List<LayoutBox>();
        var lines = new List<LayoutLine>();
        var ports = new List<LayoutPort>();
        Collect(tree.Nodes, boxes, lines, ports);

        foreach (var box in boxes.Where(candidate => !candidate.Children.OfType<LayoutBox>().Any()))
        {
            Fill(grid, box.X, box.Y, box.X + box.Width, box.Y + box.Height);
        }

        foreach (var waypoints in lines.Select(line => line.Waypoints))
        {
            for (var i = 0; i + 1 < waypoints.Count; i++)
            {
                var p = waypoints[i];
                var q = waypoints[i + 1];
                Fill(
                    grid,
                    Math.Min(p.X, q.X) - CellSize,
                    Math.Min(p.Y, q.Y) - CellSize,
                    Math.Max(p.X, q.X) + CellSize,
                    Math.Max(p.Y, q.Y) + CellSize);
            }
        }

        var labels = ConnectorLabelPlacer.ResolveLabels(lines, boxes, ports, Themes.Light);
        foreach (var placement in labels.Midpoints.Values)
        {
            Fill(grid, placement.X - placement.HalfWidth, placement.Y - placement.HalfHeight, placement.X + placement.HalfWidth, placement.Y + placement.HalfHeight);
        }

        foreach (var placement in labels.EndLabels)
        {
            Fill(grid, placement.X - placement.HalfWidth, placement.Y - placement.HalfHeight, placement.X + placement.HalfWidth, placement.Y + placement.HalfHeight);
        }

        return LargestEmptySquare(grid) * CellSize;
    }

    /// <summary>
    /// Collects the boxes, lines, and ports of a node list, recursing through box children.
    /// </summary>
    /// <param name="nodes">The nodes to walk.</param>
    /// <param name="boxes">The accumulating boxes, outermost first.</param>
    /// <param name="lines">The accumulating lines.</param>
    /// <param name="ports">The accumulating ports.</param>
    private static void Collect(
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
                    Collect(box.Children, boxes, lines, ports);
                    break;
                case LayoutLine line:
                    lines.Add(line);
                    break;
                case LayoutPort port:
                    ports.Add(port);
                    break;
            }
        }
    }

    /// <summary>
    /// Marks every cell touched by the rectangle as occupied (clipped to the canvas).
    /// </summary>
    /// <param name="grid">The occupancy grid.</param>
    /// <param name="left">The left edge of the rectangle.</param>
    /// <param name="top">The top edge of the rectangle.</param>
    /// <param name="right">The right edge of the rectangle.</param>
    /// <param name="bottom">The bottom edge of the rectangle.</param>
    private static void Fill(bool[,] grid, double left, double top, double right, double bottom)
    {
        var columns = grid.GetLength(0);
        var rows = grid.GetLength(1);
        var firstColumn = Math.Max(0, (int)Math.Floor(left / CellSize));
        var lastColumn = Math.Min(columns - 1, (int)Math.Ceiling(right / CellSize) - 1);
        var firstRow = Math.Max(0, (int)Math.Floor(top / CellSize));
        var lastRow = Math.Min(rows - 1, (int)Math.Ceiling(bottom / CellSize) - 1);
        for (var column = firstColumn; column <= lastColumn; column++)
        {
            for (var row = firstRow; row <= lastRow; row++)
            {
                grid[column, row] = true;
            }
        }
    }

    /// <summary>
    /// The classic dynamic program for the side, in cells, of the largest all-empty square of a grid.
    /// </summary>
    /// <param name="grid">The occupancy grid.</param>
    /// <returns>The side in cells.</returns>
    private static int LargestEmptySquare(bool[,] grid)
    {
        var columns = grid.GetLength(0);
        var rows = grid.GetLength(1);
        var previous = new int[rows + 1];
        var best = 0;
        for (var column = 0; column < columns; column++)
        {
            var current = new int[rows + 1];
            for (var row = 0; row < rows; row++)
            {
                if (grid[column, row])
                {
                    continue;
                }

                // current[row + 1] is the square ending at (column, row); previous holds column - 1.
                var side = 1 + Math.Min(Math.Min(previous[row + 1], current[row]), previous[row]);
                current[row + 1] = side;
                best = Math.Max(best, side);
            }

            previous = current;
        }

        return best;
    }
}
