// <copyright file="LayoutTreePacker.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout.Engine;

/// <summary>
/// Reusable shelf-packer that merges several independently-placed <see cref="LayoutTree"/>s (each
/// already a complete, self-contained canvas, typically produced by routing one connected component of
/// a graph through a different bundled leaf algorithm) into a single combined <see cref="LayoutTree"/>.
/// </summary>
/// <remarks>
///     <para>
///     This is the <see cref="LayoutTree"/>-level counterpart to
///     <see cref="Layered.ComponentPacker"/>: both split a disconnected input into independently-laid-out
///     pieces and then pack the pieces into one non-overlapping arrangement using the same greedy shelf
///     heuristic (target row width = the wider of the widest piece and <c>sqrt(totalArea) * aspect</c>).
///     They are legitimately distinct, small implementations rather than true duplication, because they
///     operate on different types at different levels: <see cref="Layered.ComponentPacker"/> works on the
///     layered engine's internal <c>Rect</c>/<c>Point2D</c> representation for components that all share
///     one algorithm (the layered pipeline), while this type works on already-rendered
///     <see cref="LayoutTree"/>s that may have come from <em>different</em> algorithms entirely (for
///     example one component routed through <c>layered</c> and another through <c>hierarchical</c>),
///     which is exactly the shape <see cref="AutoLayoutAlgorithm"/> needs for its per-component routing.
///     </para>
///     <para>
///     <strong>Translation.</strong> Every node in a packed sub-tree is shifted by that sub-tree's
///     assigned shelf offset, recursively: a <see cref="LayoutBox"/>'s own <c>X</c>/<c>Y</c> and every
///     nested <see cref="LayoutBox.Children"/> node, a <see cref="LayoutLine"/>'s every
///     <see cref="LayoutLine.Waypoints"/> point, and a <see cref="LayoutPort"/>'s <c>CentreX</c>/
///     <c>CentreY</c>. <see cref="LayoutTree"/> coordinates are absolute (not parent-relative — see its
///     own remarks), so translating a placed sub-tree requires this recursive walk rather than a single
///     offset applied once at the root.
///     </para>
///     <para>
///     <strong>Unknown node types fail loudly.</strong> <see cref="TranslateNode"/> switches over the
///     closed set of <see cref="LayoutNode"/> subtypes the three bundled leaf algorithms
///     (<c>layered</c>, <c>hierarchical</c>, <c>containment</c>) are confirmed to emit —
///     <see cref="LayoutBox"/>, <see cref="LayoutLine"/>, and <see cref="LayoutPort"/> (verified by
///     grepping every <c>new LayoutBox(</c>/<c>new LayoutLine(</c>/<c>new LayoutPort(</c> construction
///     site across <c>LayeredLayoutAlgorithm.cs</c>, <c>HierarchicalLayoutAlgorithm.cs</c>, and
///     <c>ContainmentLayoutAlgorithm.cs</c>). An unrecognized node type throws
///     <see cref="NotSupportedException"/> instead of being silently skipped. This is a deliberate
///     divergence from the renderer convention documented on <see cref="LayoutNode"/> ("unknown
///     subtypes should be skipped for forward compatibility"): a renderer skipping an unknown node still
///     draws every other node correctly, but a packer silently leaving an unknown node's coordinates
///     untranslated would place it at the wrong (pre-pack) position with no visible sign of the error —
///     a worse failure mode than a hard, immediate exception naming the offending type.
///     </para>
///     <para>
///     <strong>Out of scope: force-directed placement for sparse components.</strong> The shelf packer
///     always wraps a component onto the next row once the running row width would exceed the target,
///     which suits the compact rectangular components every bundled leaf algorithm produces. A future
///     enhancement could add a force-directed/spring-relaxation fallback that packs many small, sparse
///     components more organically (for example a large field of unrelated singleton nodes) instead of a
///     strict grid of shelves, but that is a materially different, self-contained algorithm and is
///     explicitly out of scope for this type today.
///     </para>
/// </remarks>
internal static class LayoutTreePacker
{
    /// <summary>
    /// Packs the supplied sub-trees into one combined <see cref="LayoutTree"/> using a greedy shelf
    /// packer, translating every placed node (recursively) by its assigned shelf offset.
    /// </summary>
    /// <param name="trees">
    /// The independently-placed sub-trees to merge, each a complete, self-contained canvas. Must not be
    /// <see langword="null"/>; may be empty (yielding a degenerate empty tree) or contain a single tree
    /// (its coordinates require no translation, but its node types are still validated against the
    /// closed set <see cref="TranslateNode"/> recognizes).
    /// </param>
    /// <param name="spacing">Gap, in logical pixels, kept between adjacent packed sub-trees.</param>
    /// <param name="aspect">
    /// Target width-to-height multiplier controlling the packed row width (larger values produce wider,
    /// shorter arrangements).
    /// </param>
    /// <param name="margin">
    /// Optional outer margin, in logical pixels, kept between the packed content and every canvas edge
    /// (default 0: the sub-trees' own canvases already carry their padding).
    /// </param>
    /// <returns>The combined, packed <see cref="LayoutTree"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="trees"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when a sub-tree contains a <see cref="LayoutNode"/> subtype outside the closed set this
    /// packer knows how to translate (see the type's remarks).
    /// </exception>
    public static LayoutTree Pack(IReadOnlyList<LayoutTree> trees, double spacing, double aspect, double margin = 0.0)
    {
        ArgumentNullException.ThrowIfNull(trees);

        if (trees.Count == 0)
        {
            return new LayoutTree(0.0, 0.0, []);
        }

        if (trees.Count == 1)
        {
            // A single tree needs no coordinate translation (offset (0, 0)), but its node types must
            // still be validated against the closed set TranslateNode recognizes — routing it through
            // TranslateNode here (rather than returning the tree unchanged) closes a gap where an
            // unrecognized LayoutNode subtype in singleton input would silently bypass the same
            // NotSupportedException every multi-tree pack enforces.
            var singleton = trees[0];
            return singleton with
            {
                Width = singleton.Width + (2.0 * margin),
                Height = singleton.Height + (2.0 * margin),
                Nodes = [.. singleton.Nodes.Select(node => TranslateNode(node, margin, margin))],
            };
        }

        var sizes = new (double Width, double Height)[trees.Count];
        for (var i = 0; i < trees.Count; i++)
        {
            sizes[i] = (trees[i].Width, trees[i].Height);
        }

        var arrangement = Arrange(sizes, spacing, aspect);
        var offsetX = arrangement.X;
        var offsetY = arrangement.Y;
        var totalWidth = arrangement.Width + (2.0 * margin);
        var totalHeight = arrangement.Height + (2.0 * margin);

        var mergedNodes = new List<LayoutNode>();
        var mergedWarnings = new List<string>();
        for (var i = 0; i < trees.Count; i++)
        {
            var tree = trees[i];
            var dx = offsetX[i] + margin;
            var dy = offsetY[i] + margin;

            foreach (var node in tree.Nodes)
            {
                mergedNodes.Add(TranslateNode(node, dx, dy));
            }

            mergedWarnings.AddRange(tree.Warnings);
        }

        return new LayoutTree(totalWidth, totalHeight, mergedNodes) { Warnings = mergedWarnings };
    }

    /// <summary>
    /// The result of <see cref="Arrange"/>: the top-left offset of every piece (by input index) and the
    /// size of the canvas that encloses them.
    /// </summary>
    /// <param name="X">The left offset of each piece, by input index.</param>
    /// <param name="Y">The top offset of each piece, by input index.</param>
    /// <param name="Width">The canvas width: the right edge of the rightmost piece.</param>
    /// <param name="Height">The canvas height: the bottom edge of the lowest piece.</param>
    internal readonly record struct Arrangement(double[] X, double[] Y, double Width, double Height)
    {
        /// <summary>Gets the canvas area.</summary>
        public double Area => Width * Height;
    }

    /// <summary>The most pieces the free-space search is run for; beyond it the shelf arrangement is returned.</summary>
    private const int MaxFreeSpacePieces = 256;

    /// <summary>How many candidate row widths, evenly spread from the widest piece to all pieces in one row, are tried.</summary>
    private const int CandidateRowWidths = 8;

    /// <summary>The widest and narrowest canvas aspect ratio (width over height, or its inverse) that carries no penalty.</summary>
    private const double BalancedAspect = 2.0;

    /// <summary>A tolerance for comparing sizes and areas.</summary>
    private const double Tolerance = 1e-6;

    /// <summary>
    /// Arranges pieces (rectangles of the given sizes) without overlap, keeping <paramref name="spacing"/>
    /// between neighbors, and returns the arrangement with the smaller canvas.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     The reference arrangement is the greedy shelf packing (rows of pieces in input order; target row
    ///     width the wider of the widest piece and <c>sqrt(totalArea) * aspect</c>). Shelves leave empty
    ///     space beside a tall piece when the next pieces wrap to a row below it, so a free-space packing is
    ///     also tried: each piece, in input order or tallest first, goes to the free spot of the bounded-width
    ///     sheet that keeps the packing lowest (a maximal-rectangles search), for several candidate row widths.
    ///     A free-space arrangement replaces the shelf arrangement only when its canvas area is strictly
    ///     smaller and its area weighted by an aspect penalty (none while the width-to-height ratio lies in
    ///     [0.5, 2]) is no worse (equal scores within the tolerance count), so the result is never a larger
    ///     canvas than the shelf one and equal canvases keep the shelf arrangement exactly. The algorithm is
    ///     deterministic.
    ///     </para>
    /// </remarks>
    /// <param name="sizes">The piece sizes, in input order.</param>
    /// <param name="spacing">The gap kept between adjacent pieces.</param>
    /// <param name="aspect">The target width-to-height multiplier of the shelf arrangement.</param>
    /// <returns>The arrangement with the smaller canvas.</returns>
    internal static Arrangement Arrange(IReadOnlyList<(double Width, double Height)> sizes, double spacing, double aspect)
    {
        var shelf = ArrangeShelf(sizes, spacing, aspect);
        if (sizes.Count < 2 || sizes.Count > MaxFreeSpacePieces)
        {
            return shelf;
        }

        var totalArea = 0.0;
        var widest = 0.0;
        var allInOneRow = 0.0;
        foreach (var (width, height) in sizes)
        {
            totalArea += width * height;
            widest = Math.Max(widest, width);
            allInOneRow += width + spacing;
        }

        allInOneRow -= spacing;
        var rowWidths = new List<double> { Math.Max(widest, Math.Sqrt(totalArea) * aspect) };
        for (var step = 0; step <= CandidateRowWidths; step++)
        {
            var width = widest + ((allInOneRow - widest) * step / CandidateRowWidths);
            if (!rowWidths.Exists(existing => Math.Abs(existing - width) < Tolerance))
            {
                rowWidths.Add(width);
            }
        }

        var inputOrder = Enumerable.Range(0, sizes.Count).ToArray();
        var tallestFirst = inputOrder.OrderByDescending(index => sizes[index].Height).ThenBy(index => index).ToArray();

        var shelfScore = Score(shelf);
        var best = shelf;
        var bestScore = shelfScore;
        var bestIsFreeSpace = false;
        foreach (var order in new[] { inputOrder, tallestFirst })
        {
            foreach (var rowWidth in rowWidths)
            {
                var candidate = ArrangeFreeSpace(sizes, spacing, rowWidth, order);
                if (candidate.Area >= shelf.Area - Tolerance)
                {
                    continue;
                }

                // A free-space canvas with a strictly smaller area replaces the shelf one when its score is
                // no worse (a tie within the tolerance counts); among free-space candidates only a strictly
                // better score replaces the best so far, so the first of equals is kept.
                var score = Score(candidate);
                if (bestIsFreeSpace ? score < bestScore - Tolerance : score <= shelfScore + Tolerance)
                {
                    best = candidate;
                    bestScore = score;
                    bestIsFreeSpace = true;
                }
            }
        }

        return best;
    }

    /// <summary>Weighs a canvas by its area times a penalty for a width-to-height ratio beyond 2:1 either way.</summary>
    private static double Score(Arrangement arrangement)
    {
        if (arrangement.Width <= 0.0 || arrangement.Height <= 0.0)
        {
            return arrangement.Area;
        }

        var ratio = Math.Max(arrangement.Width / arrangement.Height, arrangement.Height / arrangement.Width);
        return arrangement.Area * (ratio <= BalancedAspect ? 1.0 : ratio / BalancedAspect);
    }

    /// <summary>
    /// The greedy shelf arrangement: rows in input order, wrapping when the running row width would exceed
    /// the target (but never leaving a shelf empty — an over-wide piece sits alone on its own shelf).
    /// </summary>
    internal static Arrangement ArrangeShelf(IReadOnlyList<(double Width, double Height)> sizes, double spacing, double aspect)
    {
        var totalArea = 0.0;
        var widest = 0.0;
        foreach (var (width, height) in sizes)
        {
            totalArea += width * height;
            widest = Math.Max(widest, width);
        }

        var targetRowWidth = Math.Max(widest, Math.Sqrt(totalArea) * aspect);

        var offsetX = new double[sizes.Count];
        var offsetY = new double[sizes.Count];
        var cursorX = 0.0;
        var shelfTop = 0.0;
        var shelfHeight = 0.0;
        var totalWidth = 0.0;
        for (var i = 0; i < sizes.Count; i++)
        {
            var (width, height) = sizes[i];
            if (cursorX > 0.0 && cursorX + width > targetRowWidth)
            {
                shelfTop += shelfHeight + spacing;
                cursorX = 0.0;
                shelfHeight = 0.0;
            }

            offsetX[i] = cursorX;
            offsetY[i] = shelfTop;
            cursorX += width + spacing;
            shelfHeight = Math.Max(shelfHeight, height);
            totalWidth = Math.Max(totalWidth, offsetX[i] + width);
        }

        return new Arrangement(offsetX, offsetY, totalWidth, shelfTop + shelfHeight);
    }

    /// <summary>A free rectangle of the sheet the free-space search places pieces into.</summary>
    private readonly record struct FreeRect(double X, double Y, double Width, double Height);

    /// <summary>
    /// Places the pieces, in <paramref name="order"/>, into a sheet of width <paramref name="rowWidth"/>
    /// (widened to the widest piece) and unbounded height: each piece takes the free spot that keeps its
    /// bottom edge highest, then leftmost (a maximal-rectangles bottom-left search).
    /// </summary>
    private static Arrangement ArrangeFreeSpace(
        IReadOnlyList<(double Width, double Height)> sizes,
        double spacing,
        double rowWidth,
        int[] order)
    {
        // Every piece is inflated by the spacing, so neighbors keep it between them; the sheet is wider by
        // one spacing, so the last piece of a row keeps no trailing gap.
        const double unbounded = 1e12;
        var free = new List<FreeRect> { new(0.0, 0.0, rowWidth + spacing, unbounded) };
        var offsetX = new double[sizes.Count];
        var offsetY = new double[sizes.Count];
        var totalWidth = 0.0;
        var totalHeight = 0.0;
        foreach (var index in order)
        {
            var (width, height) = sizes[index];
            var padded = (Width: width + spacing, Height: height + spacing);
            var found = false;
            var bestX = 0.0;
            var bestY = 0.0;
            var bestBottom = double.PositiveInfinity;
            foreach (var rect in free)
            {
                if (rect.Width < padded.Width - Tolerance || rect.Height < padded.Height - Tolerance)
                {
                    continue;
                }

                var bottom = rect.Y + padded.Height;
                if (!found || bottom < bestBottom - Tolerance || (Math.Abs(bottom - bestBottom) <= Tolerance && rect.X < bestX - Tolerance))
                {
                    found = true;
                    bestX = rect.X;
                    bestY = rect.Y;
                    bestBottom = bottom;
                }
            }

            if (!found)
            {
                // The sheet is as wide as the widest piece, so a free rectangle always fits; this guards
                // against a degenerate (non-finite) size by falling back to a new row at the bottom.
                bestX = 0.0;
                bestY = totalHeight + spacing;
            }

            offsetX[index] = bestX;
            offsetY[index] = bestY;
            totalWidth = Math.Max(totalWidth, bestX + width);
            totalHeight = Math.Max(totalHeight, bestY + height);
            Occupy(free, new FreeRect(bestX, bestY, padded.Width, padded.Height));
        }

        return new Arrangement(offsetX, offsetY, totalWidth, totalHeight);
    }

    /// <summary>
    /// Removes <paramref name="used"/> from the free rectangles: every free rectangle it overlaps is split
    /// into the (up to four) maximal rectangles around it, and rectangles contained in another are dropped.
    /// </summary>
    private static void Occupy(List<FreeRect> free, FreeRect used)
    {
        var next = new List<FreeRect>(free.Count + 4);
        foreach (var rect in free)
        {
            if (used.X >= rect.X + rect.Width - Tolerance || used.X + used.Width <= rect.X + Tolerance
                || used.Y >= rect.Y + rect.Height - Tolerance || used.Y + used.Height <= rect.Y + Tolerance)
            {
                next.Add(rect);
                continue;
            }

            if (used.X > rect.X + Tolerance)
            {
                next.Add(rect with { Width = used.X - rect.X });
            }

            if (used.X + used.Width < rect.X + rect.Width - Tolerance)
            {
                next.Add(new FreeRect(used.X + used.Width, rect.Y, rect.X + rect.Width - (used.X + used.Width), rect.Height));
            }

            if (used.Y > rect.Y + Tolerance)
            {
                next.Add(rect with { Height = used.Y - rect.Y });
            }

            if (used.Y + used.Height < rect.Y + rect.Height - Tolerance)
            {
                next.Add(new FreeRect(rect.X, used.Y + used.Height, rect.Width, rect.Y + rect.Height - (used.Y + used.Height)));
            }
        }

        free.Clear();
        for (var i = 0; i < next.Count; i++)
        {
            var contained = false;
            for (var j = 0; j < next.Count && !contained; j++)
            {
                if (i == j)
                {
                    continue;
                }

                var a = next[i];
                var b = next[j];
                var inside = a.X >= b.X - Tolerance && a.Y >= b.Y - Tolerance
                    && a.X + a.Width <= b.X + b.Width + Tolerance && a.Y + a.Height <= b.Y + b.Height + Tolerance;

                // Identical rectangles keep only the first.
                var identical = inside && b.X >= a.X - Tolerance && b.Y >= a.Y - Tolerance
                    && b.X + b.Width <= a.X + a.Width + Tolerance && b.Y + b.Height <= a.Y + a.Height + Tolerance;
                contained = inside && (!identical || j < i);
            }

            if (!contained)
            {
                free.Add(next[i]);
            }
        }
    }

    /// <summary>The most candidate positions of a hole search that are scored against the empty-space measure.</summary>
    private const int MaxHoleCandidates = 240;

    /// <summary>
    /// Tucks <paramref name="pieces"/>, arranged as one block, into an empty region of
    /// <paramref name="host"/> (a hole in its drawing) instead of appending them beside or below it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     The host is rasterized to the grid of <see cref="EmptySpaceMeter"/>; every box (a container as a
    ///     whole), connector segment, resolved connector label and port glyph is a keep-out, grown by half
    ///     the <paramref name="spacing"/> (boxes by the full spacing), and so is the band of
    ///     <paramref name="edgeMargin"/> along the canvas edge. The block, in several aspect ratios, is tried at
    ///     every free position (sampled evenly when there are very many) and the position that leaves the
    ///     smallest largest-empty-square (diagram-quality criterion B4) wins; ties keep the topmost, then
    ///     leftmost. The host canvas is unchanged. When the block fits nowhere the result is
    ///     <see langword="null"/>.
    ///     </para>
    /// </remarks>
    /// <param name="host">The placed tree whose canvas has the hole.</param>
    /// <param name="pieces">The placed trees to tuck in, each a complete, self-contained canvas.</param>
    /// <param name="spacing">The gap kept between the block's pieces and from the host's content.</param>
    /// <param name="aspect">The target width-to-height multiplier of the block arrangement.</param>
    /// <param name="edgeMargin">The clearance kept from the canvas edge.</param>
    /// <param name="fontSize">The body font size, in logical pixels, used to size connector labels.</param>
    /// <returns>The host with the pieces placed in it, or <see langword="null"/> when they fit nowhere.</returns>
    internal static LayoutTree? TuckIntoHoles(
        LayoutTree host,
        IReadOnlyList<LayoutTree> pieces,
        double spacing,
        double aspect,
        double edgeMargin,
        double fontSize)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(pieces);
        if (pieces.Count == 0)
        {
            return null;
        }

        var sizes = pieces.Select(piece => (piece.Width, piece.Height)).ToArray();
        var blocks = new List<Arrangement>();
        foreach (var blockAspect in new[] { aspect, 1.0, 2.5, 0.5 })
        {
            var block = Arrange(sizes, spacing, blockAspect);
            if (!blocks.Exists(existing => Math.Abs(existing.Width - block.Width) < Tolerance && Math.Abs(existing.Height - block.Height) < Tolerance))
            {
                blocks.Add(block);
            }
        }

        var cell = EmptySpaceMeter.CellSize;
        var occupancy = EmptySpaceMeter.BuildOccupancy(host);
        var columns = occupancy.GetLength(0);
        var rows = occupancy.GetLength(1);
        var keepOut = BuildKeepOut(host, columns, rows, spacing, edgeMargin, fontSize);
        var blocked = new int[columns + 1, rows + 1];
        for (var column = 0; column < columns; column++)
        {
            for (var row = 0; row < rows; row++)
            {
                blocked[column + 1, row + 1] = blocked[column, row + 1] + blocked[column + 1, row] - blocked[column, row] + (keepOut[column, row] ? 1 : 0);
            }
        }

        var bestSquare = int.MaxValue;
        var squares = new EmptySpaceMeter.SquareIndex(occupancy);
        var bestColumn = 0;
        var bestRow = 0;
        Arrangement? bestBlock = null;
        foreach (var block in blocks)
        {
            var blockColumns = (int)Math.Ceiling((block.Width - Tolerance) / cell);
            var blockRows = (int)Math.Ceiling((block.Height - Tolerance) / cell);
            var positions = new List<(int Column, int Row)>();
            for (var row = 0; row + blockRows <= rows; row++)
            {
                for (var column = 0; column + blockColumns <= columns; column++)
                {
                    var count = blocked[column + blockColumns, row + blockRows] - blocked[column, row + blockRows]
                        - blocked[column + blockColumns, row] + blocked[column, row];
                    if (count == 0)
                    {
                        positions.Add((column, row));
                    }
                }
            }

            var stride = Math.Max(1, (positions.Count + MaxHoleCandidates - 1) / MaxHoleCandidates);
            for (var i = 0; i < positions.Count; i += stride)
            {
                var (column, row) = positions[i];

                // The largest empty square left once the block sits here, found from the table of the host's
                // own squares instead of by a pass over a copy of the whole grid
                var (firstColumn, lastColumn, firstRow, lastRow) = EmptySpaceMeter.FillBounds(
                    columns, rows, column * cell, row * cell, (column * cell) + block.Width, (row * cell) + block.Height);
                var square = squares.LargestWithFilled(firstColumn, lastColumn, firstRow, lastRow, bestSquare);
                if (square < bestSquare || (square == bestSquare && bestBlock is { } && (row, column).CompareTo((bestRow, bestColumn)) < 0))
                {
                    bestSquare = square;
                    bestColumn = column;
                    bestRow = row;
                    bestBlock = block;
                }
            }
        }

        if (bestBlock is not { } chosen)
        {
            return null;
        }

        var originX = bestColumn * cell;
        var originY = bestRow * cell;
        var nodes = new List<LayoutNode>(host.Nodes);
        var warnings = new List<string>(host.Warnings);
        for (var i = 0; i < pieces.Count; i++)
        {
            nodes.AddRange(pieces[i].Nodes.Select(node => TranslateNode(node, originX + chosen.X[i], originY + chosen.Y[i])));
            warnings.AddRange(pieces[i].Warnings);
        }

        return host with { Nodes = nodes, Warnings = warnings };
    }

    /// <summary>
    /// Rasterizes the keep-out area of <paramref name="host"/>: every box grown by <paramref name="spacing"/>,
    /// every connector segment, resolved label and port glyph grown by half of it, and the canvas edge band.
    /// </summary>
    private static bool[,] BuildKeepOut(LayoutTree host, int columns, int rows, double spacing, double edgeMargin, double fontSize)
    {
        var grid = new bool[columns, rows];
        var boxes = new List<LayoutBox>();
        var lines = new List<LayoutLine>();
        var ports = new List<LayoutPort>();
        EmptySpaceMeter.Collect(host.Nodes, boxes, lines, ports);

        var half = spacing / 2.0;
        foreach (var box in boxes)
        {
            EmptySpaceMeter.Fill(grid, box.X - spacing, box.Y - spacing, box.X + box.Width + spacing, box.Y + box.Height + spacing);
        }

        foreach (var waypoints in lines.Select(line => line.Waypoints))
        {
            for (var i = 0; i + 1 < waypoints.Count; i++)
            {
                var p = waypoints[i];
                var q = waypoints[i + 1];
                EmptySpaceMeter.Fill(
                    grid, Math.Min(p.X, q.X) - half, Math.Min(p.Y, q.Y) - half, Math.Max(p.X, q.X) + half, Math.Max(p.Y, q.Y) + half);
            }
        }

        var theme = Themes.Light with { FontSizeBody = fontSize };
        var labels = ConnectorLabelPlacer.ResolveLabels(lines, boxes, ports, theme);
        foreach (var placement in labels.Midpoints.Values)
        {
            EmptySpaceMeter.Fill(
                grid, placement.X - placement.HalfWidth - half, placement.Y - placement.HalfHeight - half,
                placement.X + placement.HalfWidth + half, placement.Y + placement.HalfHeight + half);
        }

        foreach (var placement in labels.EndLabels)
        {
            EmptySpaceMeter.Fill(
                grid, placement.X - placement.HalfWidth - half, placement.Y - placement.HalfHeight - half,
                placement.X + placement.HalfWidth + half, placement.Y + placement.HalfHeight + half);
        }

        foreach (var port in ports)
        {
            var glyph = NotationMetrics.PortGlyphBounds(port);
            EmptySpaceMeter.Fill(grid, glyph.X - glyph.HalfWidth - half, glyph.Y - glyph.HalfHeight - half, glyph.X + glyph.HalfWidth + half, glyph.Y + glyph.HalfHeight + half);
            if (NotationMetrics.IsOutwardLabel(port))
            {
                var label = NotationMetrics.PortOutwardLabelBounds(port, port.ExternalLabel!, theme);
                EmptySpaceMeter.Fill(grid, label.X - label.HalfWidth - half, label.Y - label.HalfHeight - half, label.X + label.HalfWidth + half, label.Y + label.HalfHeight + half);
            }
        }

        // The band along the canvas edge stays free, so the tucked pieces keep the outer margin.
        EmptySpaceMeter.Fill(grid, 0.0, 0.0, host.Width, edgeMargin);
        EmptySpaceMeter.Fill(grid, 0.0, host.Height - edgeMargin, host.Width, host.Height);
        EmptySpaceMeter.Fill(grid, 0.0, 0.0, edgeMargin, host.Height);
        EmptySpaceMeter.Fill(grid, host.Width - edgeMargin, 0.0, host.Width, host.Height);
        return grid;
    }

    /// <summary>
    /// Recursively translates a single placed <see cref="LayoutNode"/> (and any nested nodes it
    /// carries) by <paramref name="dx"/>/<paramref name="dy"/>.
    /// </summary>
    /// <param name="node">The node to translate.</param>
    /// <param name="dx">Horizontal offset, in logical pixels.</param>
    /// <param name="dy">Vertical offset, in logical pixels.</param>
    /// <returns>A new node of the same kind, shifted by the given offset.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when <paramref name="node"/> is a <see cref="LayoutNode"/> subtype outside this packer's
    /// closed, verified set (see the type's remarks).
    /// </exception>
    internal static LayoutNode TranslateNode(LayoutNode node, double dx, double dy) => node switch
    {
        LayoutBox box => box with
        {
            X = box.X + dx,
            Y = box.Y + dy,
            Children = [.. box.Children.Select(child => TranslateNode(child, dx, dy))],
        },
        LayoutLine line => line with
        {
            Waypoints = [.. line.Waypoints.Select(point => new Point2D(point.X + dx, point.Y + dy))],
        },
        LayoutPort port => port with
        {
            CentreX = port.CentreX + dx,
            CentreY = port.CentreY + dy,
        },
        _ => throw new NotSupportedException(
            $"LayoutTreePacker cannot translate unrecognized LayoutNode subtype '{node.GetType().Name}'. " +
            "The closed set of node types the bundled leaf algorithms emit is LayoutBox, LayoutLine, and " +
            "LayoutPort; if a new leaf algorithm or node type is introduced, TranslateNode must be " +
            "extended to cover it."),
    };
}
