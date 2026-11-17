// <copyright file="ConnectorLabelPlacer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Abstractions;

/// <summary>
/// A resolved connector midpoint-label placement: the chosen label centre plus the half-width and
/// half-height (including the internal clearance <see cref="ConnectorLabelPlacer"/> already used to
/// avoid collisions) of its bounding box, in logical pixels. Exposing the size alongside the
/// position lets a renderer grow its canvas/bitmap to fully include every placed label, even one
/// nudged away from its preferred midpoint to avoid a collision.
/// </summary>
/// <param name="X">Label centre X coordinate, in logical pixels.</param>
/// <param name="Y">Label centre Y coordinate, in logical pixels.</param>
/// <param name="HalfWidth">Half the label's bounding-box width (including clearance), in logical pixels.</param>
/// <param name="HalfHeight">Half the label's bounding-box height (including clearance), in logical pixels.</param>
public readonly record struct LabelPlacement(double X, double Y, double HalfWidth, double HalfHeight)
{
    /// <summary>
    /// Deconstructs into just the label centre coordinates, preserving source compatibility with
    /// call sites written against the previous <c>(double X, double Y)</c> tuple return type (C#
    /// resolves <see cref="Deconstruct(out double, out double)"/> overloads by arity, so a 2-variable
    /// deconstruction picks this overload instead of the record's synthesized 4-arg one).
    /// </summary>
    /// <param name="x">Receives <see cref="X"/>.</param>
    /// <param name="y">Receives <see cref="Y"/>.</param>
    public void Deconstruct(out double x, out double y)
    {
        x = X;
        y = Y;
    }
}

/// <summary>
/// Computes non-overlapping screen positions for connector (midpoint) labels.
/// </summary>
/// <remarks>
/// Each labeled line prefers the midpoint of its longest segment (an open run between boxes), but
/// when two labels would land on top of each other — for example where two connectors cross — the
/// placer falls back to a shorter segment or nudges the label perpendicular to its segment until it
/// no longer collides with an already-placed label. Lines are processed in the supplied order so the
/// result is deterministic. Both the SVG and PNG renderers share this logic so their label layouts
/// match.
/// </remarks>
public static partial class ConnectorLabelPlacer
{
    /// <summary>Approximate width of one character as a fraction of the font size.</summary>
    private const double CharWidthFactor = 0.6;

    /// <summary>Label box height as a multiple of the font size (cap height plus padding).</summary>
    private const double HeightFactor = 1.3;

    /// <summary>Extra clearance, in logical pixels, added around each label box when testing overlap.</summary>
    private const double Gap = 2.0;

    /// <summary>
    /// Computes a label position for every line that has a <see cref="LayoutLine.MidpointLabel"/>.
    /// </summary>
    /// <param name="lines">The lines to place labels for, in render order.</param>
    /// <param name="fontSize">Body font size, in logical pixels, used to estimate label box sizes.</param>
    /// <returns>
    /// A dictionary mapping each labeled line to its chosen <see cref="LabelPlacement"/> (label
    /// centre plus half-width/half-height, in logical pixels). Lines without a label are omitted.
    /// </returns>
    public static IReadOnlyDictionary<LayoutLine, LabelPlacement> Place(
        IEnumerable<LayoutLine> lines,
        double fontSize) =>
        Place(lines, fontSize, []);

    /// <summary>
    /// Computes a label position for every line that has a <see cref="LayoutLine.MidpointLabel"/>,
    /// additionally keeping each label clear of the supplied obstacle boxes.
    /// </summary>
    /// <remarks>
    /// A candidate label rectangle is rejected when it crosses an obstacle. For a leaf box (one whose
    /// children, if any, are only ports) any overlap with the box rectangle rejects the candidate,
    /// because a label there would paint its opaque background over the box. For a container box (one
    /// with nested nodes) only straddling its border rejects the candidate — overlapping the rectangle
    /// without lying fully inside it — because a label legitimately sits inside a container. Candidate
    /// order is: segment midpoints (longest first), then sliding along each segment in half-label
    /// steps, then perpendicular nudges of the longest segment. When no candidate clears every
    /// obstacle, the obstacle-free placement of the two-argument overload is used, so the no-overlap
    /// guarantee between labels is preserved. With no obstacles the result is identical to the
    /// two-argument overload.
    /// </remarks>
    /// <param name="lines">The lines to place labels for, in render order.</param>
    /// <param name="fontSize">Body font size, in logical pixels, used to estimate label box sizes.</param>
    /// <param name="obstacles">The boxes labels should avoid; an empty sequence disables avoidance.</param>
    /// <returns>
    /// A dictionary mapping each labeled line to its chosen <see cref="LabelPlacement"/>. Lines without
    /// a label are omitted.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> or <paramref name="obstacles"/> is <see langword="null"/>.</exception>
    public static IReadOnlyDictionary<LayoutLine, LabelPlacement> Place(
        IEnumerable<LayoutLine> lines,
        double fontSize,
        IEnumerable<LayoutBox> obstacles) =>
        Place(lines, fontSize, obstacles, NotationMetrics.MarkerScale(Themes.Light));

    /// <summary>
    /// Computes a label position for every line that has a <see cref="LayoutLine.MidpointLabel"/>,
    /// keeping each label clear of the supplied obstacle boxes and of the end decorations it could
    /// otherwise hide, sizing each decoration with the supplied marker scale.
    /// </summary>
    /// <remarks>
    /// Behaves exactly like <see cref="Place(IEnumerable{LayoutLine}, double, IEnumerable{LayoutBox})"/>,
    /// which uses the marker scale of the default light theme. Renderers pass
    /// <see cref="NotationMetrics.MarkerScale(Theme)"/> of the theme they draw with, so the clearance
    /// tracks the size both renderers actually draw each marker at.
    /// </remarks>
    /// <param name="lines">The lines to place labels for, in render order.</param>
    /// <param name="fontSize">Body font size, in logical pixels, used to estimate label box sizes.</param>
    /// <param name="obstacles">The boxes labels should avoid; an empty sequence disables avoidance.</param>
    /// <param name="markerScale">
    /// Pixels per notation unit of end-marker geometry (<see cref="NotationMetrics.MarkerScale(Theme)"/>).
    /// </param>
    /// <returns>
    /// A dictionary mapping each labeled line to its chosen <see cref="LabelPlacement"/>. Lines without
    /// a label are omitted.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> or <paramref name="obstacles"/> is <see langword="null"/>.</exception>
    public static IReadOnlyDictionary<LayoutLine, LabelPlacement> Place(
        IEnumerable<LayoutLine> lines,
        double fontSize,
        IEnumerable<LayoutBox> obstacles,
        double markerScale)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(obstacles);

        var blocking = obstacles.Select(ToObstacle).ToList();
        var placed = new List<Bounds>();
        var result = new Dictionary<LayoutLine, LabelPlacement>();

        foreach (var line in lines)
        {
            if (line.MidpointLabel is null || line.Waypoints.Count == 0)
            {
                continue;
            }

            var halfWidth = (EstimateTextWidth(line.MidpointLabel, fontSize) / 2.0) + Gap;
            var halfHeight = (fontSize * HeightFactor / 2.0) + Gap;

            // The line's own end decorations are kept clear too, so a label is never placed over an
            // arrowhead (a short run's midpoint can fall inside the decoration's footprint).
            var position = ChoosePosition(
                line.Waypoints,
                halfWidth,
                halfHeight,
                placed,
                blocking,
                DecorationZones(line, markerScale),
                (NotationMetrics.AlongLineLength(line.SourceEnd) * markerScale, NotationMetrics.AlongLineLength(line.TargetEnd) * markerScale));
            placed.Add(new Bounds(position.X - halfWidth, position.Y - halfHeight, position.X + halfWidth, position.Y + halfHeight));
            result[line] = new LabelPlacement(position.X, position.Y, halfWidth, halfHeight);
        }

        return result;
    }

    /// <summary>
    /// Returns the footprint of each end decoration of a line as a leaf obstacle: the box spanned from
    /// the line endpoint back along its first or last segment by the decoration's rendered along-line
    /// length, widened across the line by the decoration's rendered half width.
    /// </summary>
    /// <param name="line">The line whose end decorations are measured.</param>
    /// <param name="markerScale">Pixels per notation unit of end-marker geometry.</param>
    /// <returns>The decoration footprints; empty when the line has no decoration.</returns>
    private static List<Obstacle> DecorationZones(LayoutLine line, double markerScale)
    {
        var zones = new List<Obstacle>();
        if (line.Waypoints.Count < 2)
        {
            return zones;
        }

        AddZone(zones, line.Waypoints[0], line.Waypoints[1], line.SourceEnd, markerScale);
        var last = line.Waypoints.Count - 1;
        AddZone(zones, line.Waypoints[last], line.Waypoints[last - 1], line.TargetEnd, markerScale);
        return zones;
    }

    /// <summary>Adds the footprint of one end decoration, anchored at <paramref name="end"/>, to <paramref name="zones"/>.</summary>
    /// <param name="zones">The list receiving the footprint.</param>
    /// <param name="end">The line endpoint carrying the decoration.</param>
    /// <param name="inward">The adjacent waypoint, which gives the direction back along the line.</param>
    /// <param name="style">The decoration style; <see cref="EndMarkerStyle.None"/> adds nothing.</param>
    /// <param name="markerScale">Pixels per notation unit of end-marker geometry.</param>
    private static void AddZone(List<Obstacle> zones, Point2D end, Point2D inward, EndMarkerStyle style, double markerScale)
    {
        if (style == EndMarkerStyle.None)
        {
            return;
        }

        var dx = inward.X - end.X;
        var dy = inward.Y - end.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        var dirX = length > 1e-9 ? dx / length : 0.0;
        var dirY = length > 1e-9 ? dy / length : 0.0;
        var along = NotationMetrics.AlongLineLength(style) * markerScale;
        var across = NotationMetrics.AcrossHalfWidth(style) * markerScale;

        // Bounding box of the oriented footprint rectangle
        var tipX = end.X + (dirX * along);
        var tipY = end.Y + (dirY * along);
        var perpX = Math.Abs(dirY) * across;
        var perpY = Math.Abs(dirX) * across;
        zones.Add(new Obstacle(
            Math.Min(end.X, tipX) - perpX,
            Math.Min(end.Y, tipY) - perpY,
            Math.Max(end.X, tipX) + perpX,
            Math.Max(end.Y, tipY) + perpY,
            IsContainer: false));
    }

    /// <summary>Converts a layout box to an obstacle rectangle, classifying it as leaf or container.</summary>
    /// <param name="box">The box to convert.</param>
    /// <returns>The obstacle.</returns>
    private static Obstacle ToObstacle(LayoutBox box) =>
        new(box.X, box.Y, box.X + box.Width, box.Y + box.Height, box.Children.Any(child => child is not LayoutPort));

    /// <summary>Estimates the rendered width of a label string in logical pixels.</summary>
    /// <param name="text">The label text.</param>
    /// <param name="fontSize">Font size in logical pixels.</param>
    /// <returns>The approximate text width.</returns>
    private static double EstimateTextWidth(string text, double fontSize) =>
        text.Length * fontSize * CharWidthFactor;

    /// <summary>
    /// Estimates the full (not half) height of a connector midpoint label's bounding box, including
    /// the clearance <see cref="Place(IEnumerable{LayoutLine}, double)"/> already adds around every label when testing for overlap.
    /// Exposed so other layout stages (for example <c>LayeredLayoutAlgorithm</c>'s parallel-edge lane
    /// spacing) can size themselves against the same label-height formula this placer uses internally,
    /// without duplicating the <see cref="HeightFactor"/>/<see cref="Gap"/> constants.
    /// </summary>
    /// <param name="fontSize">Body font size, in logical pixels.</param>
    /// <returns>The estimated full label bounding-box height, in logical pixels.</returns>
    public static double EstimateLabelHeight(double fontSize) =>
        (fontSize * HeightFactor) + (2.0 * Gap);

    /// <summary>
    /// Estimates the full (not half) width of a connector midpoint label's bounding box, including
    /// the clearance <see cref="Place(IEnumerable{LayoutLine}, double)"/> already adds around every label when testing for overlap.
    /// Exposed so other layout stages (for example <c>LayeredLayoutAlgorithm</c>'s parallel-edge lane
    /// spacing on a <c>Top</c>/<c>Bottom</c> box face, where anchors are spread horizontally) can size
    /// themselves against the same label-width formula this placer uses internally, without
    /// duplicating the <see cref="CharWidthFactor"/>/<see cref="Gap"/> constants. Unlike
    /// <see cref="EstimateLabelHeight"/>, this varies with the supplied label text.
    /// </summary>
    /// <param name="text">The label text.</param>
    /// <param name="fontSize">Body font size, in logical pixels.</param>
    /// <returns>The estimated full label bounding-box width, in logical pixels.</returns>
    public static double EstimateLabelWidth(string text, double fontSize) =>
        EstimateTextWidth(text, fontSize) + (2.0 * Gap);

    /// <summary>
    /// Selects a label position for a single line, preferring the midpoint of the longest segment and
    /// falling back to shorter segments or perpendicular nudges to avoid overlapping placed labels and
    /// obstacle boxes.
    /// </summary>
    /// <param name="waypoints">The line's waypoints.</param>
    /// <param name="halfWidth">Half the label box width (including gap).</param>
    /// <param name="halfHeight">Half the label box height (including gap).</param>
    /// <param name="placed">Boxes of labels already placed.</param>
    /// <param name="obstacles">Boxes the label must stay clear of; empty disables avoidance.</param>
    /// <param name="zones">
    /// The footprints of the line's own end decorations. A midpoint or slide never lands in one, but
    /// they never trigger a perpendicular nudge (a label moved off its line is worse than one that
    /// cannot fully clear a decoration on a run too short for both).
    /// </param>
    /// <param name="endClearances">
    /// The along-line space, in logical pixels, the line's source and target end decorations occupy at
    /// the first and last waypoint; an obstacle-avoiding slide never moves a label into it.
    /// </param>
    /// <returns>The chosen label centre.</returns>
    private static (double X, double Y) ChoosePosition(
        IReadOnlyList<Point2D> waypoints,
        double halfWidth,
        double halfHeight,
        List<Bounds> placed,
        List<Obstacle> obstacles,
        List<Obstacle> zones,
        (double Source, double Target) endClearances)
    {
        if (waypoints.Count == 1)
        {
            return (waypoints[0].X, waypoints[0].Y);
        }

        // Segment midpoints ordered by descending length (longest, most-open run first). Each segment
        // also records how much of its start and end is reserved for an end decoration.
        var segments = new List<(double Length, double X, double Y, double DirX, double DirY, double StartReserve, double EndReserve)>();
        for (var i = 0; i < waypoints.Count - 1; i++)
        {
            var a = waypoints[i];
            var b = waypoints[i + 1];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var length = Math.Sqrt((dx * dx) + (dy * dy));
            var dirX = length > 1e-9 ? dx / length : 0.0;
            var dirY = length > 1e-9 ? dy / length : 0.0;
            segments.Add((
                length,
                (a.X + b.X) / 2.0,
                (a.Y + b.Y) / 2.0,
                dirX,
                dirY,
                i == 0 ? endClearances.Source : 0.0,
                i == waypoints.Count - 2 ? endClearances.Target : 0.0));
        }

        // Stable ordering keeps ties in waypoint order on every target framework, because an
        // unstable sort resolves ties differently on .NET Framework and .NET Core.
        segments = [.. segments.OrderByDescending(seg => seg.Length)];

        // Obstacle-aware pass: only when a box could be crossed. Its first step takes the first
        // segment midpoint clear of both labels and boxes, which is the same midpoint the
        // obstacle-free pass below would take whenever that midpoint is itself clear of every box.
        if (obstacles.Count > 0)
        {
            var avoided = ChooseObstacleClearPosition(segments, halfWidth, halfHeight, placed, obstacles, zones);
            if (avoided is not null)
            {
                return avoided.Value;
            }
        }
        else if (zones.Count > 0)
        {
            var avoided = ChooseZoneClearPosition(segments, halfWidth, halfHeight, placed, zones);
            if (avoided is not null)
            {
                return avoided.Value;
            }
        }

        // First pass: take the first segment midpoint that does not collide.
        var firstClear = segments
            .Where(seg => !Collides(seg.X, seg.Y, halfWidth, halfHeight, placed))
            .Select(seg => ((double X, double Y)?)(seg.X, seg.Y))
            .FirstOrDefault();
        if (firstClear is not null)
        {
            return firstClear.Value;
        }

        // Second pass: nudge along the longest segment's perpendicular until clear.
        var best = segments[0];
        var perpX = -best.DirY;
        var perpY = best.DirX;
        var step = (halfHeight * 2.0) + Gap;
        for (var k = 1; k <= 4; k++)
        {
            var offset = step * k;
            if (!Collides(best.X + (perpX * offset), best.Y + (perpY * offset), halfWidth, halfHeight, placed))
            {
                return (best.X + (perpX * offset), best.Y + (perpY * offset));
            }

            if (!Collides(best.X - (perpX * offset), best.Y - (perpY * offset), halfWidth, halfHeight, placed))
            {
                return (best.X - (perpX * offset), best.Y - (perpY * offset));
            }
        }

        // Final fallback: drop the label just beneath every placed label so it is guaranteed not to
        // overlap, preserving the documented no-overlap guarantee even when the perpendicular nudges
        // above are exhausted (for example where many connectors cross at a single point). Because a
        // clear segment was not found in the first pass, `placed` is guaranteed to be non-empty here.
        var clearBottom = placed.Max(r => r.Bottom);
        return (best.X, clearBottom + halfHeight + Gap);
    }

    /// <summary>
    /// Searches for a label position that clears both the already-placed labels and every obstacle
    /// box: segment midpoints, then sliding along each segment in half-label steps (staying within the
    /// segment), then perpendicular nudges of the longest segment.
    /// </summary>
    /// <param name="segments">Segments ordered by descending length.</param>
    /// <param name="halfWidth">Half the label box width (including gap).</param>
    /// <param name="halfHeight">Half the label box height (including gap).</param>
    /// <param name="placed">Boxes of labels already placed.</param>
    /// <param name="boxes">Boxes the label must stay clear of.</param>
    /// <param name="zones">End-decoration footprints the midpoint and slides must stay clear of.</param>
    /// <returns>The clear position, or <see langword="null"/> when no candidate clears every obstacle.</returns>
    private static (double X, double Y)? ChooseObstacleClearPosition(
        List<(double Length, double X, double Y, double DirX, double DirY, double StartReserve, double EndReserve)> segments,
        double halfWidth,
        double halfHeight,
        List<Bounds> placed,
        List<Obstacle> boxes,
        List<Obstacle> zones)
    {
        // Midpoints and slides must clear the boxes and the decoration footprints; the perpendicular
        // nudges below only the boxes.
        List<Obstacle> obstacles = zones.Count == 0 ? boxes : [.. boxes, .. zones];

        // Segment midpoints, longest first.
        foreach (var seg in segments)
        {
            if (!Collides(seg.X, seg.Y, halfWidth, halfHeight, placed, obstacles))
            {
                return (seg.X, seg.Y);
            }
        }

        // Slide along each segment, alternating either side of its midpoint. A slide keeps the whole
        // label inside the segment and clear of any end decoration at either end, so a label never
        // ends up hiding an arrowhead or running into the box the segment attaches to.
        foreach (var seg in segments)
        {
            var slid = SlideAlongSegment(seg, halfWidth, halfHeight, placed, obstacles);
            if (slid is not null)
            {
                return slid;
            }
        }

        // When only a decoration footprint blocked the midpoint (the label is clear of every box and
        // label there), a perpendicular nudge would drop the label off its own line, so give up and let
        // the caller keep the midpoint.
        var best = segments[0];
        if (zones.Count > 0 && !Collides(best.X, best.Y, halfWidth, halfHeight, placed, boxes))
        {
            return null;
        }

        // Perpendicular nudges of the longest segment.
        var perpX = -best.DirY;
        var perpY = best.DirX;
        var step = (halfHeight * 2.0) + Gap;
        for (var k = 1; k <= 4; k++)
        {
            var offset = step * k;
            if (!Collides(best.X + (perpX * offset), best.Y + (perpY * offset), halfWidth, halfHeight, placed, boxes))
            {
                return (best.X + (perpX * offset), best.Y + (perpY * offset));
            }

            if (!Collides(best.X - (perpX * offset), best.Y - (perpY * offset), halfWidth, halfHeight, placed, boxes))
            {
                return (best.X - (perpX * offset), best.Y - (perpY * offset));
            }
        }

        return null;
    }

    /// <summary>
    /// Slides a label along one segment, alternating either side of its midpoint in half-label steps
    /// and then trying the exact position nearest the midpoint, and returns the first position that
    /// clears every obstacle, or <see langword="null"/>. A slide keeps the whole label inside the
    /// segment and clear of any end decoration at either end.
    /// </summary>
    /// <param name="seg">The segment, with its reserved start and end lengths.</param>
    /// <param name="halfWidth">Half the label box width (including gap).</param>
    /// <param name="halfHeight">Half the label box height (including gap).</param>
    /// <param name="placed">Boxes of labels already placed.</param>
    /// <param name="obstacles">The obstacles the position must clear.</param>
    /// <returns>The clear position, or <see langword="null"/> when no slide clears every obstacle.</returns>
    private static (double X, double Y)? SlideAlongSegment(
        (double Length, double X, double Y, double DirX, double DirY, double StartReserve, double EndReserve) seg,
        double halfWidth,
        double halfHeight,
        List<Bounds> placed,
        List<Obstacle> obstacles)
    {
        var labelExtent = (Math.Abs(seg.DirX) * halfWidth) + (Math.Abs(seg.DirY) * halfHeight);
        var towardEnd = (seg.Length / 2.0) - labelExtent - seg.EndReserve;
        var towardStart = (seg.Length / 2.0) - labelExtent - seg.StartReserve;
        for (var k = 1; (k * halfHeight) <= Math.Max(towardEnd, towardStart); k++)
        {
            var offset = k * halfHeight;
            if (offset <= towardEnd
                && !Collides(seg.X + (seg.DirX * offset), seg.Y + (seg.DirY * offset), halfWidth, halfHeight, placed, obstacles))
            {
                return (seg.X + (seg.DirX * offset), seg.Y + (seg.DirY * offset));
            }

            if (offset <= towardStart
                && !Collides(seg.X - (seg.DirX * offset), seg.Y - (seg.DirY * offset), halfWidth, halfHeight, placed, obstacles))
            {
                return (seg.X - (seg.DirX * offset), seg.Y - (seg.DirY * offset));
            }
        }

        // The half-label steps can all miss a narrow clear window (a short run just long enough for
        // the label and the decoration), so also try the exact position nearest the midpoint that
        // keeps the whole label clear of the reserved ends.
        var exact = NearestFeasibleOffset(towardStart, towardEnd);
        if (exact is not null
            && !Collides(seg.X + (seg.DirX * exact.Value), seg.Y + (seg.DirY * exact.Value), halfWidth, halfHeight, placed, obstacles))
        {
            return (seg.X + (seg.DirX * exact.Value), seg.Y + (seg.DirY * exact.Value));
        }

        return null;
    }

    /// <summary>
    /// The pass used when a line has end decorations but there are no obstacle boxes: it only moves a
    /// label that would sit on its own line's decoration, and leaves label-versus-label collisions to
    /// the original placement (so decorated lines keep exactly the positions they had unless a label
    /// would cover an arrowhead).
    /// </summary>
    /// <param name="segments">Segments ordered by descending length.</param>
    /// <param name="halfWidth">Half the label box width (including gap).</param>
    /// <param name="halfHeight">Half the label box height (including gap).</param>
    /// <param name="placed">Boxes of labels already placed.</param>
    /// <param name="zones">The line's end-decoration footprints.</param>
    /// <returns>The position, or <see langword="null"/> to fall back to the original placement.</returns>
    private static (double X, double Y)? ChooseZoneClearPosition(
        List<(double Length, double X, double Y, double DirX, double DirY, double StartReserve, double EndReserve)> segments,
        double halfWidth,
        double halfHeight,
        List<Bounds> placed,
        List<Obstacle> zones)
    {
        foreach (var seg in segments)
        {
            // A midpoint that collides with a placed label is the original placement's business
            if (Collides(seg.X, seg.Y, halfWidth, halfHeight, placed))
            {
                continue;
            }

            if (!Collides(seg.X, seg.Y, halfWidth, halfHeight, placed, zones))
            {
                return (seg.X, seg.Y);
            }

            var slid = SlideAlongSegment(seg, halfWidth, halfHeight, placed, zones);
            if (slid is not null)
            {
                return slid;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the slide offset nearest the segment midpoint (along the segment direction) that keeps
    /// the whole label inside the room left by the reserved ends, or <see langword="null"/> when the
    /// midpoint is already feasible (nothing to slide) or no offset is feasible.
    /// </summary>
    /// <param name="towardStart">The largest feasible slide toward the segment start (negative when none).</param>
    /// <param name="towardEnd">The largest feasible slide toward the segment end (negative when none).</param>
    /// <returns>The signed offset along the segment direction, or <see langword="null"/>.</returns>
    private static double? NearestFeasibleOffset(double towardStart, double towardEnd)
    {
        const double Epsilon = 1e-6;
        var high = towardEnd - Epsilon;
        var low = -(towardStart - Epsilon);
        if (low > high)
        {
            return null;
        }

        // Clamp the midpoint (offset zero) into the feasible window
        var offset = Math.Clamp(0.0, low, high);
        return Math.Abs(offset) < Epsilon ? null : offset;
    }

    /// <summary>Tests whether a candidate label box overlaps any already-placed box.</summary>
    /// <param name="centreX">Candidate box centre X.</param>
    /// <param name="centreY">Candidate box centre Y.</param>
    /// <param name="halfWidth">Half the candidate box width.</param>
    /// <param name="halfHeight">Half the candidate box height.</param>
    /// <param name="placed">Boxes already placed.</param>
    /// <returns><see langword="true"/> if the candidate overlaps a placed box.</returns>
    private static bool Collides(double centreX, double centreY, double halfWidth, double halfHeight, List<Bounds> placed)
    {
        var left = centreX - halfWidth;
        var top = centreY - halfHeight;
        var right = centreX + halfWidth;
        var bottom = centreY + halfHeight;
        foreach (var r in placed)
        {
            if (left < r.Right && right > r.Left && top < r.Bottom && bottom > r.Top)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tests whether a candidate label box overlaps an already-placed label or crosses an obstacle
    /// box (any overlap of a leaf box; straddling the border of a container box).
    /// </summary>
    /// <param name="centreX">Candidate box centre X.</param>
    /// <param name="centreY">Candidate box centre Y.</param>
    /// <param name="halfWidth">Half the candidate box width.</param>
    /// <param name="halfHeight">Half the candidate box height.</param>
    /// <param name="placed">Boxes already placed.</param>
    /// <param name="obstacles">Obstacle boxes.</param>
    /// <returns><see langword="true"/> if the candidate is rejected.</returns>
    private static bool Collides(
        double centreX,
        double centreY,
        double halfWidth,
        double halfHeight,
        List<Bounds> placed,
        List<Obstacle> obstacles)
    {
        if (Collides(centreX, centreY, halfWidth, halfHeight, placed))
        {
            return true;
        }

        var left = centreX - halfWidth;
        var top = centreY - halfHeight;
        var right = centreX + halfWidth;
        var bottom = centreY + halfHeight;
        foreach (var box in obstacles)
        {
            var overlaps = left < box.Right && right > box.Left && top < box.Bottom && bottom > box.Top;
            if (!overlaps)
            {
                continue;
            }

            var inside = left >= box.Left && right <= box.Right && top >= box.Top && bottom <= box.Bottom;
            if (!box.IsContainer || !inside)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// An axis-aligned obstacle rectangle used only for label avoidance.
    /// </summary>
    /// <param name="Left">Left edge.</param>
    /// <param name="Top">Top edge.</param>
    /// <param name="Right">Right edge.</param>
    /// <param name="Bottom">Bottom edge.</param>
    /// <param name="IsContainer">Whether the box nests other nodes, so a label may sit wholly inside it.</param>
    private readonly record struct Obstacle(double Left, double Top, double Right, double Bottom, bool IsContainer);

    /// <summary>
    /// An axis-aligned bounding box used only for label overlap tests. Distinct from the public
    /// geometry <see cref="Rect"/>: this type is expressed as edge coordinates
    /// (left/top/right/bottom) rather than position-and-size, which keeps the overlap arithmetic
    /// direct.
    /// </summary>
    /// <param name="Left">Left edge.</param>
    /// <param name="Top">Top edge.</param>
    /// <param name="Right">Right edge.</param>
    /// <param name="Bottom">Bottom edge.</param>
    private readonly record struct Bounds(double Left, double Top, double Right, double Bottom);
}
