// <copyright file="DiagramQuality.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Numeric diagram-quality checks shared by the layout tests. They turn the visual review
///     criteria (end of a connector on the shape outline, perpendicular first and last runs, no
///     sub-marker steps) into assertions over a placed <see cref="LayoutTree"/>, so a regression is
///     caught by a test rather than only by eye.
/// </summary>
internal static class DiagramQuality
{
    /// <summary>Tolerance, in logical pixels, for "the same coordinate".</summary>
    public const double Epsilon = 0.6;

    /// <summary>
    ///     The smallest perpendicular step that is allowed between two parallel runs of one
    ///     connector: anything between zero and this value reads as a rendering glitch next to an end
    ///     marker (it is longer than every end marker's drawn length).
    /// </summary>
    public const double MinVisibleStep = 12.0;

    /// <summary>Returns every line of the tree, in emission order.</summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<LayoutLine> Lines(LayoutTree tree) =>
        tree.Nodes.OfType<LayoutLine>().ToList();

    /// <summary>
    ///     Returns the lengths of the interior segments (every segment except the first and the last)
    ///     of an orthogonal polyline whose length is not zero: the "steps" between two parallel runs.
    /// </summary>
    /// <param name="waypoints">The polyline.</param>
    /// <returns>The interior segment lengths.</returns>
    public static IReadOnlyList<double> InteriorSteps(IReadOnlyList<Point2D> waypoints)
    {
        var steps = new List<double>();
        for (var i = 1; i + 2 < waypoints.Count; i++)
        {
            var length = Distance(waypoints[i], waypoints[i + 1]);
            if (length > 1e-6)
            {
                steps.Add(length);
            }
        }

        return steps;
    }

    /// <summary>Returns the length of the first segment.</summary>
    /// <param name="waypoints">The polyline.</param>
    /// <returns>The first segment length.</returns>
    public static double FirstRun(IReadOnlyList<Point2D> waypoints) =>
        Distance(waypoints[0], waypoints[1]);

    /// <summary>Returns the length of the last segment.</summary>
    /// <param name="waypoints">The polyline.</param>
    /// <returns>The last segment length.</returns>
    public static double LastRun(IReadOnlyList<Point2D> waypoints) =>
        Distance(waypoints[^2], waypoints[^1]);

    /// <summary>Returns whether the first segment is axis-aligned (horizontal or vertical).</summary>
    /// <param name="waypoints">The polyline.</param>
    /// <returns><see langword="true"/> when the first segment is axis-aligned.</returns>
    public static bool FirstSegmentAxisAligned(IReadOnlyList<Point2D> waypoints) =>
        AxisAligned(waypoints[0], waypoints[1]);

    /// <summary>Returns whether the last segment is axis-aligned (horizontal or vertical).</summary>
    /// <param name="waypoints">The polyline.</param>
    /// <returns><see langword="true"/> when the last segment is axis-aligned.</returns>
    public static bool LastSegmentAxisAligned(IReadOnlyList<Point2D> waypoints) =>
        AxisAligned(waypoints[^2], waypoints[^1]);

    /// <summary>
    ///     Returns the normalized distance of <paramref name="point"/> from the outline of the diamond
    ///     inscribed in <paramref name="box"/>: <c>|x - cx| / hw + |y - cy| / hh</c>, which is 1 on the
    ///     outline, below 1 inside, and above 1 outside.
    /// </summary>
    /// <param name="box">The box whose inscribed diamond is measured.</param>
    /// <param name="point">The point to measure.</param>
    /// <returns>The diamond norm of the point.</returns>
    public static double DiamondNorm(LayoutBox box, Point2D point)
    {
        var cx = box.X + (box.Width / 2.0);
        var cy = box.Y + (box.Height / 2.0);
        return (Math.Abs(point.X - cx) / (box.Width / 2.0)) + (Math.Abs(point.Y - cy) / (box.Height / 2.0));
    }

    /// <summary>
    ///     Returns the length of the marker-bearing last run that a target end of
    ///     <paramref name="end"/> needs: the rendered marker length.
    /// </summary>
    /// <param name="end">The target end marker.</param>
    /// <returns>The rendered marker length, in logical pixels.</returns>
    public static double MarkerLength(EndMarkerStyle end) =>
        NotationMetrics.RenderedAlongLineLength(end, Themes.Light);

    private static bool AxisAligned(Point2D a, Point2D b) =>
        Math.Abs(a.X - b.X) < 1e-6 || Math.Abs(a.Y - b.Y) < 1e-6;

    private static double Distance(Point2D a, Point2D b) =>
        Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));
}
