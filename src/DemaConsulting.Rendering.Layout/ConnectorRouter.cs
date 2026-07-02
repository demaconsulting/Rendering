// <copyright file="ConnectorRouter.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Layout.Engine;

namespace DemaConsulting.Rendering.Layout;

/// <summary>
/// Describes one connector to be routed between two already-placed boxes, together with the styling
/// carried onto the resulting <see cref="LayoutLine"/>.
/// </summary>
/// <param name="From">
/// The source box. Its instance identity is used to exclude it from the obstacle set while routing,
/// so pass the exact <see cref="LayoutBox"/> reference that appears in the box list.
/// </param>
/// <param name="To">
/// The target box. Its instance identity is used to exclude it from the obstacle set while routing;
/// the connector's target end marker is drawn where the route meets this box.
/// </param>
/// <param name="TargetEnd">End-marker style drawn at the target (arrival) end of the connector.</param>
/// <param name="LineStyle">Stroke style (solid, dashed, dotted) applied to the routed connector.</param>
/// <param name="Label">Optional midpoint label carried onto the routed line; <see langword="null"/> for an unlabelled connector.</param>
/// <remarks>
/// A <see cref="Connection"/> carries no geometry of its own: source and target anchors are chosen by
/// <see cref="ConnectorRouter"/> from the boxes' current placement. The record intentionally models
/// only general diagram concepts (two endpoints plus line styling) and holds no domain-specific data.
/// </remarks>
public sealed record Connection(
    LayoutBox From,
    LayoutBox To,
    EndMarkerStyle TargetEnd = EndMarkerStyle.None,
    LineStyle LineStyle = LineStyle.Solid,
    string? Label = null);

/// <summary>
/// Options controlling how <see cref="ConnectorRouter"/> routes connectors among placed boxes.
/// </summary>
/// <param name="EdgeRouting">
/// Routing style to apply, mirroring ELK's <c>elk.edgeRouting</c>. Defaults to
/// <see cref="Rendering.EdgeRouting.Orthogonal"/>, the only shipped style; the switch grows additively
/// as new routers are implemented.
/// </param>
/// <param name="Clearance">
/// Minimum gap, in logical pixels, kept between routed segments and the boxes they steer around.
/// Defaults to <c>12.0</c>. Callers can override this to reproduce a specific spacing (for example a
/// downstream adapter matching its historical output).
/// </param>
/// <remarks>
/// The default value mirrors the property default on <see cref="CoreOptions.EdgeRouting"/>, so a
/// caller that has not selected a routing style gets orthogonal routing either way.
/// </remarks>
public sealed record ConnectorRouteOptions(
    EdgeRouting EdgeRouting = EdgeRouting.Orthogonal,
    double Clearance = 12.0);

/// <summary>
/// Routes connectors among a set of already-placed boxes, producing one <see cref="LayoutLine"/> per
/// connection. This is the public routing-orchestration entry point: it picks boundary anchors facing
/// the other endpoint, builds the obstacle set from the remaining boxes, and defers the per-connector
/// path to the router selected by <see cref="ConnectorRouteOptions.EdgeRouting"/>.
/// </summary>
/// <remarks>
/// <para>
/// The orchestration is deliberately model-agnostic: boxes are matched to their connections by
/// instance identity, and no domain concept (names, kinds, qualified references) enters the routing.
/// For each connection the two endpoint boxes are excluded from the obstacle set — a connector must
/// be free to leave and enter the boxes it joins — while every other box becomes a <see cref="Rect"/>
/// obstacle the route steers around by <see cref="ConnectorRouteOptions.Clearance"/>.
/// </para>
/// <para>
/// Source and target anchors are chosen on the box faces that front each other, based on the boxes'
/// relative placement rather than the direction to a possibly far-off centre, and are aligned to the
/// overlap of the boxes on the shared edge. Connectors therefore leave and arrive on the sides the two
/// boxes actually present to each other, without wrapping back across a wide endpoint box. The chosen
/// side is passed to the underlying router so the connector exits and enters perpendicular to the edge.
/// </para>
/// <para>
/// Today the only supported style is <see cref="Rendering.EdgeRouting.Orthogonal"/>, realized by the
/// library's internal orthogonal edge router. The dispatch is a single-arm switch structured so new
/// routing styles slot in additively without changing this contract.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Two already-placed boxes and an intervening one that the connector must avoid.
/// var a = new LayoutBox(0, 0, 80, 40, "A", 0, BoxShape.Rectangle, [], []);
/// var mid = new LayoutBox(140, -10, 60, 80, "M", 0, BoxShape.Rectangle, [], []);
/// var b = new LayoutBox(260, 0, 80, 40, "B", 0, BoxShape.Rectangle, [], []);
/// var boxes = new[] { a, mid, b };
///
/// // Route A -> B (avoiding M) and A -> M, carrying the requested styling.
/// var connections = new[]
/// {
///     new Connection(a, b, EndMarkerStyle.FilledArrow),
///     new Connection(a, mid, EndMarkerStyle.HollowDiamond, LineStyle.Dashed, "owns"),
/// };
///
/// var lines = ConnectorRouter.Route(boxes, connections, new ConnectorRouteOptions());
///
/// // Drop the placed boxes and the routed connectors into a LayoutTree for a renderer.
/// var nodes = new List&lt;LayoutNode&gt;();
/// nodes.AddRange(boxes);
/// nodes.AddRange(lines);
/// var tree = new LayoutTree(360, 80, nodes);
/// </code>
/// </example>
public static class ConnectorRouter
{
    /// <summary>
    /// Routes every connection in <paramref name="connections"/> among the placed
    /// <paramref name="boxes"/>, returning one routed <see cref="LayoutLine"/> per connection in the
    /// same order.
    /// </summary>
    /// <param name="boxes">
    /// All placed boxes on the canvas. Boxes other than a connection's two endpoints act as obstacles
    /// for that connection's route.
    /// </param>
    /// <param name="connections">The connectors to route, each naming a source and target box.</param>
    /// <param name="options">Routing options, including the routing style and obstacle clearance.</param>
    /// <returns>One routed <see cref="LayoutLine"/> per connection, in input order.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="boxes"/>, <paramref name="connections"/>, <paramref name="options"/>,
    /// or any connection (or its <see cref="Connection.From"/> / <see cref="Connection.To"/>) is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when <see cref="ConnectorRouteOptions.EdgeRouting"/> names a style that has no shipped
    /// router.
    /// </exception>
    public static IReadOnlyList<LayoutLine> Route(
        IReadOnlyList<LayoutBox> boxes,
        IReadOnlyList<Connection> connections,
        ConnectorRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(options);

        var lines = new List<LayoutLine>(connections.Count);
        foreach (var connection in connections)
        {
            lines.Add(Route(boxes, connection, options));
        }

        return lines;
    }

    /// <summary>
    /// Routes a single <paramref name="connection"/> among the placed <paramref name="boxes"/>.
    /// </summary>
    /// <param name="boxes">
    /// All placed boxes on the canvas. Every box except the connection's two endpoints acts as an
    /// obstacle for this route.
    /// </param>
    /// <param name="connection">The connector to route.</param>
    /// <param name="options">Routing options, including the routing style and obstacle clearance.</param>
    /// <returns>The routed connector as a <see cref="LayoutLine"/> carrying the connection's styling.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="boxes"/>, <paramref name="connection"/>, <paramref name="options"/>,
    /// or the connection's <see cref="Connection.From"/> / <see cref="Connection.To"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when <see cref="ConnectorRouteOptions.EdgeRouting"/> names a style that has no shipped
    /// router.
    /// </exception>
    public static LayoutLine Route(
        IReadOnlyList<LayoutBox> boxes,
        Connection connection,
        ConnectorRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(connection.From);
        ArgumentNullException.ThrowIfNull(connection.To);

        var from = connection.From;
        var to = connection.To;

        // Pick anchors on the faces the two boxes actually present to each other, based on their
        // relative placement. Using the direction to the other box's centre misfires for wide boxes
        // (whose centre can sit far past the near endpoint), forcing the connector to wrap back across a
        // box; deriving the facing sides from the box rectangles avoids that.
        var (source, sourceSide, target, targetSide) = FacingAnchors(from, to);

        // The obstacle set is every box except this connection's own endpoints, matched by instance
        // identity. The connector must be free to leave and enter the boxes it joins.
        var obstacles = new List<Rect>(boxes.Count);
        foreach (var box in boxes)
        {
            if (ReferenceEquals(box, from) || ReferenceEquals(box, to))
            {
                continue;
            }

            obstacles.Add(new Rect(box.X, box.Y, box.Width, box.Height));
        }

        var waypoints = RouteWaypoints(options.EdgeRouting, source, target, obstacles, options.Clearance, sourceSide, targetSide);

        return new LayoutLine(
            Waypoints: waypoints,
            SourceEnd: EndMarkerStyle.None,
            TargetEnd: connection.TargetEnd,
            LineStyle: connection.LineStyle,
            MidpointLabel: connection.Label);
    }

    /// <summary>
    /// Dispatches to the router realizing the requested <paramref name="edgeRouting"/> style.
    /// </summary>
    private static IReadOnlyList<Point2D> RouteWaypoints(
        EdgeRouting edgeRouting,
        Point2D source,
        Point2D target,
        IReadOnlyList<Rect> obstacles,
        double clearance,
        PortSide sourceSide,
        PortSide targetSide) => edgeRouting switch
        {
            EdgeRouting.Orthogonal =>
                OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, clearance, sourceSide, targetSide).Waypoints,
            _ => throw new NotSupportedException($"Edge routing style '{edgeRouting}' has no shipped router."),
        };

    /// <summary>
    /// Chooses boundary anchors on the two box faces that front each other, based on the boxes'
    /// relative placement. The connector leaves and enters on the sides actually facing the other box —
    /// the axis along which the boxes are more separated — and each anchor is aligned to the overlap of
    /// the boxes on the shared edge, so the route stays short and never wraps back across an endpoint.
    /// </summary>
    /// <param name="from">The source box.</param>
    /// <param name="to">The target box.</param>
    /// <returns>The source anchor and side, and the target anchor and side.</returns>
    private static (Point2D Source, PortSide SourceSide, Point2D Target, PortSide TargetSide) FacingAnchors(
        LayoutBox from, LayoutBox to)
    {
        // Signed separation on each axis: positive when the boxes clear each other on that axis,
        // negative when they overlap. Anchor on the axis with the greater separation so the connector
        // spans the real gap between the boxes.
        var horizontalGap = Math.Max(to.X - (from.X + from.Width), from.X - (to.X + to.Width));
        var verticalGap = Math.Max(to.Y - (from.Y + from.Height), from.Y - (to.Y + to.Height));

        if (horizontalGap >= verticalGap)
        {
            // Left/right relationship: the left box anchors its right face, the right box its left face.
            var fromIsLeft = from.X + (from.Width / 2.0) <= to.X + (to.Width / 2.0);
            var y = AlignedCoordinate(from.Y, from.Y + from.Height, to.Y, to.Y + to.Height);
            return fromIsLeft
                ? (new Point2D(from.X + from.Width, Clamp(y, from.Y, from.Y + from.Height)), PortSide.Right,
                   new Point2D(to.X, Clamp(y, to.Y, to.Y + to.Height)), PortSide.Left)
                : (new Point2D(from.X, Clamp(y, from.Y, from.Y + from.Height)), PortSide.Left,
                   new Point2D(to.X + to.Width, Clamp(y, to.Y, to.Y + to.Height)), PortSide.Right);
        }

        // Top/bottom relationship: the upper box anchors its bottom face, the lower box its top face.
        var fromIsAbove = from.Y + (from.Height / 2.0) <= to.Y + (to.Height / 2.0);
        var x = AlignedCoordinate(from.X, from.X + from.Width, to.X, to.X + to.Width);
        return fromIsAbove
            ? (new Point2D(Clamp(x, from.X, from.X + from.Width), from.Y + from.Height), PortSide.Bottom,
               new Point2D(Clamp(x, to.X, to.X + to.Width), to.Y), PortSide.Top)
            : (new Point2D(Clamp(x, from.X, from.X + from.Width), from.Y), PortSide.Top,
               new Point2D(Clamp(x, to.X, to.X + to.Width), to.Y + to.Height), PortSide.Bottom);
    }

    /// <summary>
    /// Returns a coordinate shared by the spans [<paramref name="aLo"/>, <paramref name="aHi"/>] and
    /// [<paramref name="bLo"/>, <paramref name="bHi"/>]: the centre of their overlap when they overlap,
    /// otherwise the midpoint of the gap between them. Anchors are clamped into each box afterwards, so
    /// a shared coordinate keeps both ends aligned when the boxes overlap on the perpendicular axis.
    /// </summary>
    private static double AlignedCoordinate(double aLo, double aHi, double bLo, double bHi) =>
        (Math.Max(aLo, bLo) + Math.Min(aHi, bHi)) / 2.0;

    /// <summary>Clamps <paramref name="value"/> into the inclusive range [<paramref name="min"/>, <paramref name="max"/>].</summary>
    private static double Clamp(double value, double min, double max) =>
        Math.Clamp(value, min, max);
}
