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
    internal static double LargestEmptySquare(LayoutTree tree) => LargestEmptySquareCells(BuildOccupancy(tree)) * CellSize;

    /// <summary>
    /// Rasterizes <paramref name="tree"/> onto the occupancy grid of the measure: leaf boxes, connector
    /// segments dilated by one cell, and resolved connector labels are occupied.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The grid, indexed <c>[column, row]</c>, <see langword="true"/> where occupied.</returns>
    internal static bool[,] BuildOccupancy(LayoutTree tree)
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

        return grid;
    }

    /// <summary>
    /// Collects the boxes, lines, and ports of a node list, recursing through box children.
    /// </summary>
    /// <param name="nodes">The nodes to walk.</param>
    /// <param name="boxes">The accumulating boxes, outermost first.</param>
    /// <param name="lines">The accumulating lines.</param>
    /// <param name="ports">The accumulating ports.</param>
    internal static void Collect(
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
    internal static void Fill(bool[,] grid, double left, double top, double right, double bottom)
    {
        var (firstColumn, lastColumn, firstRow, lastRow) = FillBounds(grid.GetLength(0), grid.GetLength(1), left, top, right, bottom);
        for (var column = firstColumn; column <= lastColumn; column++)
        {
            for (var row = firstRow; row <= lastRow; row++)
            {
                grid[column, row] = true;
            }
        }
    }

    /// <summary>
    /// Returns the first and last column and row <see cref="Fill"/> marks for a rectangle on a grid of the
    /// given size (empty when the first exceeds the last).
    /// </summary>
    /// <param name="columns">The number of grid columns.</param>
    /// <param name="rows">The number of grid rows.</param>
    /// <param name="left">The left edge of the rectangle.</param>
    /// <param name="top">The top edge of the rectangle.</param>
    /// <param name="right">The right edge of the rectangle.</param>
    /// <param name="bottom">The bottom edge of the rectangle.</param>
    /// <returns>The inclusive column and row ranges, clipped to the canvas.</returns>
    internal static (int FirstColumn, int LastColumn, int FirstRow, int LastRow) FillBounds(
        int columns,
        int rows,
        double left,
        double top,
        double right,
        double bottom) =>
        (
            Math.Max(0, (int)Math.Floor(left / CellSize)),
            Math.Min(columns - 1, (int)Math.Ceiling(right / CellSize) - 1),
            Math.Max(0, (int)Math.Floor(top / CellSize)),
            Math.Min(rows - 1, (int)Math.Ceiling(bottom / CellSize) - 1));

    /// <summary>
    /// The largest-empty-square table of an occupancy grid, built once so the largest empty square of the
    /// grid with one more rectangle filled in can be found without a pass over the whole grid.
    /// </summary>
    /// <remarks>
    /// Filling a rectangle changes the side of a square ending at a cell only when the square reaches the
    /// rectangle, and a square ending at (column, row) reaches only columns up to <c>column</c> and rows up to
    /// <c>row</c>. The cells left of or above the rectangle therefore keep their table value, and the answer
    /// for the filled grid is the larger of the best of those cells (two prefix maxima) and the best of the
    /// cells right of and below the rectangle's first column and row, found by the same dynamic program over
    /// only that part of the grid, started from the unchanged values beside it. The result is exactly the one
    /// <see cref="LargestEmptySquareCells(bool[,], int)"/> gives for the filled grid.
    /// </remarks>
    internal sealed class SquareIndex
    {
        /// <summary>The occupancy grid the index describes.</summary>
        private readonly bool[,] _grid;

        /// <summary>The number of grid columns.</summary>
        private readonly int _columns;

        /// <summary>The number of grid rows.</summary>
        private readonly int _rows;

        /// <summary>The side of the largest empty square ending at each cell, indexed [column, row + 1] (row 0 is zero).</summary>
        private readonly int[,] _table;

        /// <summary>For each column c, the largest table value in any column before c.</summary>
        private readonly int[] _columnPrefixMax;

        /// <summary>For each row r, the largest table value in any row before r.</summary>
        private readonly int[] _rowPrefixMax;

        /// <summary>Initializes the index for <paramref name="grid"/>, which must not change while the index is used.</summary>
        /// <param name="grid">The occupancy grid.</param>
        public SquareIndex(bool[,] grid)
        {
            ArgumentNullException.ThrowIfNull(grid);
            _grid = grid;
            _columns = grid.GetLength(0);
            _rows = grid.GetLength(1);
            _table = new int[_columns, _rows + 1];
            _columnPrefixMax = new int[_columns + 1];
            _rowPrefixMax = new int[_rows + 1];

            // The table, and the best per column and per row
            var rowBest = new int[_rows];
            for (var column = 0; column < _columns; column++)
            {
                var columnBest = 0;
                for (var row = 0; row < _rows; row++)
                {
                    if (grid[column, row])
                    {
                        continue;
                    }

                    var up = _table[column, row];
                    var left = column > 0 ? _table[column - 1, row + 1] : 0;
                    var diagonal = column > 0 ? _table[column - 1, row] : 0;
                    var side = 1 + Math.Min(Math.Min(left, up), diagonal);
                    _table[column, row + 1] = side;
                    columnBest = Math.Max(columnBest, side);
                    rowBest[row] = Math.Max(rowBest[row], side);
                }

                _columnPrefixMax[column + 1] = Math.Max(_columnPrefixMax[column], columnBest);
            }

            for (var row = 0; row < _rows; row++)
            {
                _rowPrefixMax[row + 1] = Math.Max(_rowPrefixMax[row], rowBest[row]);
            }
        }

        /// <summary>Gets the side, in cells, of the largest empty square of the grid itself.</summary>
        public int Largest => _columnPrefixMax[_columns];

        /// <summary>
        /// Returns the side, in cells, of the largest empty square of the grid with the given cells filled in.
        /// </summary>
        /// <param name="firstColumn">The first column filled.</param>
        /// <param name="lastColumn">The last column filled.</param>
        /// <param name="firstRow">The first row filled.</param>
        /// <param name="lastRow">The last row filled.</param>
        /// <param name="stopAbove">
        /// A side above which the exact answer is no longer wanted: the search stops once it has found a
        /// square larger than this and returns that (smaller than the true) size.
        /// </param>
        /// <returns>The side in cells, or a side above <paramref name="stopAbove"/> when it was exceeded.</returns>
        public int LargestWithFilled(int firstColumn, int lastColumn, int firstRow, int lastRow, int stopAbove = int.MaxValue)
        {
            // Nothing filled: the grid's own answer
            if (firstColumn > lastColumn || firstRow > lastRow)
            {
                return Largest;
            }

            // Cells left of or above the filled rectangle keep their value
            var best = Math.Max(_columnPrefixMax[firstColumn], _rowPrefixMax[firstRow]);
            if (best > stopAbove)
            {
                return best;
            }

            // The rest is the dynamic program over the columns from the first filled one, the rows from the
            // first filled one, started from the unchanged values of the column and row beside the part
            var previous = new int[_rows + 1];
            var current = new int[_rows + 1];
            if (firstColumn > 0)
            {
                for (var row = firstRow; row <= _rows; row++)
                {
                    previous[row] = _table[firstColumn - 1, row];
                }
            }

            for (var column = firstColumn; column < _columns; column++)
            {
                var filledColumn = column <= lastColumn;
                current[firstRow] = _table[column, firstRow];
                for (var row = firstRow; row < _rows; row++)
                {
                    if (_grid[column, row] || (filledColumn && row <= lastRow))
                    {
                        current[row + 1] = 0;
                        continue;
                    }

                    var side = 1 + Math.Min(Math.Min(previous[row + 1], current[row]), previous[row]);
                    current[row + 1] = side;
                    if (side > best)
                    {
                        best = side;
                    }
                }

                if (best > stopAbove)
                {
                    return best;
                }

                (previous, current) = (current, previous);
            }

            return best;
        }
    }

    /// <summary>
    /// The classic dynamic program for the side, in cells, of the largest all-empty square of a grid.
    /// </summary>
    /// <param name="grid">The occupancy grid.</param>
    /// <param name="stopAbove">
    /// A side above which the exact answer is no longer wanted: the program stops as soon as it has found
    /// a square larger than this and returns that (smaller than the true) size, which is still above the
    /// limit. A caller that only compares the result with a limit passes it here to skip the rest of the
    /// grid; the default computes the exact size.
    /// </param>
    /// <returns>The side in cells, or a side above <paramref name="stopAbove"/> when it was exceeded.</returns>
    internal static int LargestEmptySquareCells(bool[,] grid, int stopAbove = int.MaxValue)
    {
        var columns = grid.GetLength(0);
        var rows = grid.GetLength(1);

        // Two rows of the table swap roles every column instead of one being allocated per column
        var previous = new int[rows + 1];
        var current = new int[rows + 1];
        var best = 0;
        for (var column = 0; column < columns; column++)
        {
            for (var row = 0; row < rows; row++)
            {
                if (grid[column, row])
                {
                    current[row + 1] = 0;
                    continue;
                }

                // current[row + 1] is the square ending at (column, row); previous holds column - 1.
                var side = 1 + Math.Min(Math.Min(previous[row + 1], current[row]), previous[row]);
                current[row + 1] = side;
                if (side > best)
                {
                    best = side;
                }
            }

            if (best > stopAbove)
            {
                return best;
            }

            (previous, current) = (current, previous);
        }

        return best;
    }
}
