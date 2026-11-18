// <copyright file="RouteQuality.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout.Engine;

/// <summary>
/// The visual quality of routed orthogonal connectors: how long they are, how often they bend, whether
/// they double back on themselves (a hairpin), and how much of them runs squeezed beside another
/// connector. These are what makes a route that merely avoids a crossing nevertheless look worse than the
/// route it replaces, so the re-routing and ordering passes weigh them against the crossings they remove.
/// </summary>
internal static class RouteQuality
{
    /// <summary>Two parallel runs of different connectors closer than this, in logical pixels, read as one thick line.</summary>
    internal const double CloseParallelGap = 16.0;

    /// <summary>Parallel runs with an overlap of at most this many logical pixels are not counted as running side by side.</summary>
    internal const double CloseParallelOverlap = 20.0;

    /// <summary>Runs closer than this, in logical pixels, are the same line to the eye and count as one connector on top of another.</summary>
    private const double MinimumGap = 0.5;

    /// <summary>Tolerance, in logical pixels, when classifying a segment as horizontal or vertical.</summary>
    private const double Epsilon = 1e-9;

    /// <summary>
    /// The quality numbers of a set of connectors.
    /// </summary>
    /// <param name="Length">Total connector length, in logical pixels.</param>
    /// <param name="Bends">Number of connector bends.</param>
    /// <param name="Hairpins">Number of hairpins.</param>
    /// <param name="ParallelLength">Total length of close parallel runs of different connectors.</param>
    internal readonly record struct Summary(double Length, int Bends, int Hairpins, double ParallelLength);

    /// <summary>
    /// Measures a set of connectors: the sum of their lengths, bends and hairpins, and the close parallel
    /// runs between every pair of different connectors.
    /// </summary>
    /// <param name="lines">The waypoints of every connector.</param>
    /// <returns>The summary.</returns>
    internal static Summary Measure(IReadOnlyList<IReadOnlyList<Point2D>> lines)
    {
        var length = 0.0;
        var bends = 0;
        var hairpins = 0;
        var parallel = 0.0;
        for (var i = 0; i < lines.Count; i++)
        {
            length += PathLength(lines[i]);
            bends += BendCount(lines[i]);
            hairpins += HairpinCount(lines[i]);
            for (var j = i + 1; j < lines.Count; j++)
            {
                parallel += CloseParallelLength(lines[i], lines[j]);
            }
        }

        return new Summary(length, bends, hairpins, parallel);
    }

    /// <summary>Returns the total length of the orthogonal path <paramref name="w"/>.</summary>
    /// <param name="w">The path.</param>
    /// <returns>The length, in logical pixels.</returns>
    internal static double PathLength(IReadOnlyList<Point2D> w)
    {
        var length = 0.0;
        for (var i = 0; i < w.Count - 1; i++)
        {
            length += Math.Abs(w[i + 1].X - w[i].X) + Math.Abs(w[i + 1].Y - w[i].Y);
        }

        return length;
    }

    /// <summary>Returns the number of waypoints of <paramref name="w"/> at which the path changes direction.</summary>
    /// <param name="w">The path.</param>
    /// <returns>The number of bends.</returns>
    internal static int BendCount(IReadOnlyList<Point2D> w)
    {
        var bends = 0;
        for (var i = 1; i < w.Count - 1; i++)
        {
            var sameX = Math.Abs(w[i - 1].X - w[i].X) < Epsilon && Math.Abs(w[i].X - w[i + 1].X) < Epsilon;
            var sameY = Math.Abs(w[i - 1].Y - w[i].Y) < Epsilon && Math.Abs(w[i].Y - w[i + 1].Y) < Epsilon;
            if (!sameX && !sameY)
            {
                bends++;
            }
        }

        return bends;
    }

    /// <summary>
    /// Returns the number of hairpins of <paramref name="w"/>: three consecutive legs of which the first and
    /// the third run in opposite directions along the same axis (east, south, west).
    /// </summary>
    /// <param name="w">The path.</param>
    /// <returns>The number of hairpins.</returns>
    internal static int HairpinCount(IReadOnlyList<Point2D> w)
    {
        var count = 0;
        for (var i = 0; i + 3 < w.Count; i++)
        {
            var firstX = w[i + 1].X - w[i].X;
            var firstY = w[i + 1].Y - w[i].Y;
            var thirdX = w[i + 3].X - w[i + 2].X;
            var thirdY = w[i + 3].Y - w[i + 2].Y;
            var horizontal = Math.Abs(firstY) < Epsilon && Math.Abs(thirdY) < Epsilon && firstX * thirdX < 0.0;
            var vertical = Math.Abs(firstX) < Epsilon && Math.Abs(thirdX) < Epsilon && firstY * thirdY < 0.0;
            if (horizontal || vertical)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Returns the total overlap length of the runs of <paramref name="w"/> that lie parallel to, and
    /// closer than <see cref="CloseParallelGap"/> to (but not on top of), a run of <paramref name="other"/>,
    /// counting only overlaps longer than <see cref="CloseParallelOverlap"/>, each weighted by how close
    /// the two runs lie: a run 2 px from another is almost one line, one 12 px away is merely tight.
    /// </summary>
    /// <param name="w">The path.</param>
    /// <param name="other">The path of a different connector.</param>
    /// <returns>The closeness-weighted overlap length, in logical pixels.</returns>
    internal static double WeightedCloseParallelLength(IReadOnlyList<Point2D> w, IReadOnlyList<Point2D> other) =>
        CloseParallelLength(w, other, CloseParallelGap, weighted: true);

    /// <summary>
    /// Returns the total overlap length of the runs of <paramref name="w"/> that lie parallel to, and closer
    /// than <paramref name="maxGap"/> to (but not on top of), a run of <paramref name="other"/>, counting
    /// only overlaps longer than <see cref="CloseParallelOverlap"/>.
    /// </summary>
    /// <param name="w">The path.</param>
    /// <param name="other">The path of a different connector.</param>
    /// <param name="maxGap">The largest gap, in logical pixels, that counts as close.</param>
    /// <param name="weighted">Whether each overlap is weighted by how close the runs lie (one at no gap, zero at <see cref="CloseParallelGap"/>).</param>
    /// <returns>The total overlap length, in logical pixels.</returns>
    internal static double CloseParallelLength(IReadOnlyList<Point2D> w, IReadOnlyList<Point2D> other, double maxGap = CloseParallelGap, bool weighted = false)
    {
        var total = 0.0;
        for (var j = 0; j < other.Count - 1; j++)
        {
            for (var i = 0; i < w.Count - 1; i++)
            {
                double gap;
                double overlap;
                if (Math.Abs(w[i].Y - w[i + 1].Y) < Epsilon && Math.Abs(other[j].Y - other[j + 1].Y) < Epsilon)
                {
                    gap = Math.Abs(w[i].Y - other[j].Y);
                    overlap = Math.Min(Math.Max(w[i].X, w[i + 1].X), Math.Max(other[j].X, other[j + 1].X))
                        - Math.Max(Math.Min(w[i].X, w[i + 1].X), Math.Min(other[j].X, other[j + 1].X));
                }
                else if (Math.Abs(w[i].X - w[i + 1].X) < Epsilon && Math.Abs(other[j].X - other[j + 1].X) < Epsilon)
                {
                    gap = Math.Abs(w[i].X - other[j].X);
                    overlap = Math.Min(Math.Max(w[i].Y, w[i + 1].Y), Math.Max(other[j].Y, other[j + 1].Y))
                        - Math.Max(Math.Min(w[i].Y, w[i + 1].Y), Math.Min(other[j].Y, other[j + 1].Y));
                }
                else
                {
                    continue;
                }

                // A gap of exactly the lane separation (the router places anchors 16 px apart) is not close:
                // allow for the floating-point error of coordinates that were summed.
                if (gap >= MinimumGap && gap < maxGap - Epsilon && overlap > CloseParallelOverlap)
                {
                    total += weighted ? overlap * (1.0 - (gap / CloseParallelGap)) : overlap;
                }
            }
        }

        return total;
    }
}
