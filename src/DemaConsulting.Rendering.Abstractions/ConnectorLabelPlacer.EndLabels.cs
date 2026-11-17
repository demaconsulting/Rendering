// <copyright file="ConnectorLabelPlacer.EndLabels.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Abstractions;

/// <summary>
/// Identifies one end of a connector.
/// </summary>
public enum LineEnd
{
    /// <summary>The source end, at the first waypoint of the line.</summary>
    Source,

    /// <summary>The target end, at the last waypoint of the line.</summary>
    Target,
}

/// <summary>
/// Identifies which text slot of a <see cref="LineEndLabel"/> a placement belongs to.
/// </summary>
public enum EndLabelSlot
{
    /// <summary>The <see cref="LineEndLabel.Primary"/> text (above a horizontal run, right of a vertical one).</summary>
    Primary,

    /// <summary>The <see cref="LineEndLabel.Secondary"/> text (below a horizontal run, left of a vertical one).</summary>
    Secondary,
}

/// <summary>
/// A resolved connection end-label placement: which line, end, and slot it belongs to, its text, the
/// label centre, and the half-width and half-height of its bounding box (including the clearance the
/// placer used to avoid collisions), in logical pixels. A renderer draws <see cref="Text"/> centered
/// on (<see cref="X"/>, <see cref="Y"/>) in the body font, and grows its canvas to include the box.
/// </summary>
/// <param name="Line">The line the label belongs to.</param>
/// <param name="End">The line end the label is attached to.</param>
/// <param name="Slot">The text slot of the end label.</param>
/// <param name="Text">The text to draw.</param>
/// <param name="X">Label centre X coordinate, in logical pixels.</param>
/// <param name="Y">Label centre Y coordinate, in logical pixels.</param>
/// <param name="HalfWidth">Half the label's bounding-box width (including clearance), in logical pixels.</param>
/// <param name="HalfHeight">Half the label's bounding-box height (including clearance), in logical pixels.</param>
public readonly record struct EndLabelPlacement(
    LayoutLine Line,
    LineEnd End,
    EndLabelSlot Slot,
    string Text,
    double X,
    double Y,
    double HalfWidth,
    double HalfHeight);

/// <content>
/// Placement of connection end labels (role name and multiplicity) near each end of a connector.
/// </content>
public static partial class ConnectorLabelPlacer
{
    /// <summary>Clearance, in logical pixels, kept between an end label and the end point or marker it follows.</summary>
    private const double EndLabelOffset = 4.0;

    /// <summary>Clearance, in logical pixels, kept between an end label's box and the line it sits beside.</summary>
    private const double EndLabelLineClearance = 0.5;

    /// <summary>
    /// Computes a position for every <see cref="LayoutLine.SourceEndLabel"/> and
    /// <see cref="LayoutLine.TargetEndLabel"/> slot of the supplied lines, following the SysML v2
    /// graphical notation: the primary text (role name) is above a horizontal end run and to the right
    /// of a vertical one, the secondary text (multiplicity and adornments) is on the opposite side, and
    /// both sit just beyond the end marker along the end segment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Labels are processed in a fixed order — lines in the supplied order, source end before target end,
    /// primary before secondary — so the result is deterministic. For each label the candidates are, in
    /// order: the nominal position (immediately beyond the end marker, on the notation side), sliding
    /// further along the end segment in half-label steps, then the same on the opposite side of the
    /// line. A candidate is rejected when it overlaps a leaf box, straddles a container border, overlaps
    /// the footprint of one of the line's own end decorations, overlaps a rectangle in
    /// <paramref name="occupied"/> (midpoint labels, port glyphs, port labels), overlaps an earlier end
    /// label, or extends past the end of the end segment. When no candidate clears, the nominal position
    /// is used — a label is never dropped and the method never throws for a crowded diagram.
    /// </para>
    /// <para>
    /// Midpoint-label placement (<c>Place</c>) is not affected: it runs on its own, and its result is
    /// passed in through <paramref name="occupied"/> so end labels yield to it.
    /// </para>
    /// </remarks>
    /// <param name="lines">The lines whose end labels are placed, in render order.</param>
    /// <param name="fontSize">Body font size, in logical pixels, used to estimate label box sizes.</param>
    /// <param name="obstacles">The boxes labels should avoid; an empty sequence disables box avoidance.</param>
    /// <param name="markerScale">
    /// Pixels per notation unit of end-marker geometry (<see cref="NotationMetrics.MarkerScale(Theme)"/>).
    /// </param>
    /// <param name="occupied">
    /// Rectangles already taken by other drawn content — typically the midpoint label placements and the
    /// port glyph and port label rectangles — that end labels must not overlap; <see langword="null"/>
    /// for none.
    /// </param>
    /// <returns>One placement per non-empty end-label slot, in the deterministic order described above.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> or <paramref name="obstacles"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<EndLabelPlacement> PlaceEndLabels(
        IEnumerable<LayoutLine> lines,
        double fontSize,
        IEnumerable<LayoutBox> obstacles,
        double markerScale,
        IEnumerable<LabelPlacement>? occupied = null)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(obstacles);

        var blocking = obstacles.Select(ToObstacle).ToList();
        var placed = new List<Bounds>();
        if (occupied is not null)
        {
            foreach (var rect in occupied)
            {
                placed.Add(new Bounds(rect.X - rect.HalfWidth, rect.Y - rect.HalfHeight, rect.X + rect.HalfWidth, rect.Y + rect.HalfHeight));
            }
        }

        var result = new List<EndLabelPlacement>();
        foreach (var line in lines)
        {
            if (line.Waypoints.Count < 2 || (line.SourceEndLabel is null && line.TargetEndLabel is null))
            {
                continue;
            }

            // The line's own end decorations are obstacles too, so a label never sits on an arrowhead.
            var obstaclesForLine = new List<Obstacle>(blocking);
            obstaclesForLine.AddRange(DecorationZones(line, markerScale));

            foreach (var end in new[] { LineEnd.Source, LineEnd.Target })
            {
                var label = end == LineEnd.Source ? line.SourceEndLabel : line.TargetEndLabel;
                if (label is null)
                {
                    continue;
                }

                foreach (var slot in new[] { EndLabelSlot.Primary, EndLabelSlot.Secondary })
                {
                    var text = slot == EndLabelSlot.Primary ? label.Primary : label.Secondary;
                    if (text is null || text.Length == 0)
                    {
                        continue;
                    }

                    var placement = PlaceEndLabel(line, end, slot, text, fontSize, markerScale, placed, obstaclesForLine);
                    placed.Add(new Bounds(
                        placement.X - placement.HalfWidth,
                        placement.Y - placement.HalfHeight,
                        placement.X + placement.HalfWidth,
                        placement.Y + placement.HalfHeight));
                    result.Add(placement);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Estimates the full width of an end label's bounding box, including the clearance the placer keeps
    /// around it, from the same glyph-width table the port labels use (<see cref="PortLabelWidthEstimator"/>),
    /// which tracks the rendered width of the body font far more closely than the character-count estimate
    /// of <see cref="EstimateLabelWidth"/>. An end label is drawn beside its line without a background, so
    /// the estimate decides whether the text stays clear of the line, the box, and its neighbors. Exposed so
    /// the layout can reserve room with the same formula the placer uses.
    /// </summary>
    /// <param name="text">The label text.</param>
    /// <param name="fontSize">Body font size, in logical pixels.</param>
    /// <returns>The estimated full label bounding-box width, in logical pixels.</returns>
    public static double EstimateEndLabelWidth(string text, double fontSize) =>
        PortLabelWidthEstimator.MeasureWidth(text, fontSize) + (2.0 * Gap);

    /// <summary>
    /// Chooses the position of one end-label slot: the nominal spot beyond the end marker on the
    /// notation side, else a slide along the end segment, else the opposite side, else the nominal spot.
    /// </summary>
    /// <param name="line">The line the label belongs to.</param>
    /// <param name="end">The line end the label is attached to.</param>
    /// <param name="slot">The text slot being placed.</param>
    /// <param name="text">The text of the slot.</param>
    /// <param name="fontSize">Body font size, in logical pixels.</param>
    /// <param name="markerScale">Pixels per notation unit of end-marker geometry.</param>
    /// <param name="placed">Rectangles already taken (occupied rectangles and earlier labels).</param>
    /// <param name="obstacles">The obstacles for this line (boxes plus the line's own end decorations).</param>
    /// <returns>The placement.</returns>
    private static EndLabelPlacement PlaceEndLabel(
        LayoutLine line,
        LineEnd end,
        EndLabelSlot slot,
        string text,
        double fontSize,
        double markerScale,
        List<Bounds> placed,
        List<Obstacle> obstacles)
    {
        var waypoints = line.Waypoints;
        var origin = end == LineEnd.Source ? waypoints[0] : waypoints[^1];
        var inward = end == LineEnd.Source ? waypoints[1] : waypoints[^2];
        var style = end == LineEnd.Source ? line.SourceEnd : line.TargetEnd;

        // Unit direction from the end point into the line, and the end segment's length.
        var dx = inward.X - origin.X;
        var dy = inward.Y - origin.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        var dirX = length > 1e-9 ? dx / length : 1.0;
        var dirY = length > 1e-9 ? dy / length : 0.0;
        var horizontal = Math.Abs(dirX) >= Math.Abs(dirY);

        var halfWidth = EstimateEndLabelWidth(text, fontSize) / 2.0;
        var halfHeight = (fontSize * HeightFactor / 2.0) + Gap;
        var alongHalf = horizontal ? halfWidth : halfHeight;
        var crossHalf = horizontal ? halfHeight : halfWidth;

        // First usable distance along the segment: beyond the end marker plus a small clearance.
        var startOffset = (NotationMetrics.AlongLineLength(style) * markerScale) + EndLabelOffset;
        var crossOffset = crossHalf + EndLabelLineClearance;

        // Notation side of the slot: primary above (horizontal) or right (vertical).
        var nominalSign = slot == EndLabelSlot.Primary ? -1.0 : 1.0;
        if (!horizontal)
        {
            nominalSign = -nominalSign;
        }

        EndLabelPlacement At(double along, double sign)
        {
            var centreAlong = startOffset + along + alongHalf;
            var x = origin.X + (dirX * centreAlong) + (horizontal ? 0.0 : sign * crossOffset);
            var y = origin.Y + (dirY * centreAlong) + (horizontal ? sign * crossOffset : 0.0);
            return new EndLabelPlacement(line, end, slot, text, x, y, halfWidth, halfHeight);
        }

        // Candidates: slide along the segment in half-label steps on the notation side, then the other.
        foreach (var sign in new[] { nominalSign, -nominalSign })
        {
            for (var along = 0.0; startOffset + along + (2.0 * alongHalf) <= length + 1e-6; along += alongHalf)
            {
                var candidate = At(along, sign);
                if (!Collides(candidate.X, candidate.Y, halfWidth, halfHeight, placed, obstacles))
                {
                    return candidate;
                }
            }
        }

        // Nothing clears: keep the nominal position rather than dropping the label.
        return At(0.0, nominalSign);
    }
}
