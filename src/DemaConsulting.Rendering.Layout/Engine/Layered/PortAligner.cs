// <copyright file="PortAligner.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>
using static DemaConsulting.Rendering.Layout.Engine.Layered.LayeredLayoutMetrics;

namespace DemaConsulting.Rendering.Layout.Engine.Layered;

/// <summary>
/// Pipeline stage that slides connector ports along their faces so the two ends of an edge sit at the
/// same cross-axis coordinate whenever both faces allow it, giving a straight final run into the end
/// marker instead of a small step next to it.
/// </summary>
/// <remarks>
///     <para>
///     <see cref="PortDistributor"/> spreads the ports of a face evenly (the k-th of n at
///     <c>(k + 0.5) / n</c>) without looking at where the opposite end of each edge sits, and
///     <see cref="BrandesKopfPlacer"/> aligns node centers rather than ports. Two sources feeding one
///     wide target therefore routinely end up a few pixels apart, and the corridor router turns that
///     residual into a short jog right before the marker (Rendering#57). This stage runs between
///     <see cref="PortDistributor"/> and <see cref="LayeredCorridorRouter"/> and treats the even
///     spacing only as a starting point.
///     </para>
///     <para>
///     For every sub-edge it computes, for each end, the window the port may occupy: the face's usable
///     extent (the shape's connectable extents, kept <see cref="ConnectorClearance"/> from the corners,
///     and below the title reserve on left/right faces) intersected with the span between its
///     neighbors on the same face kept <see cref="EdgeSpacing"/> away. A long-edge or
///     hierarchy-crossing dummy end is pinned to its own coordinate. When the two windows intersect both
///     ports move to the point of the intersection nearest their mean, so the edge is straight; port
///     order, minimum separation, and the face extents are preserved by construction. When they cannot
///     meet, the residual step is pushed out to at least <see cref="MinPortStep"/> where the windows
///     allow, so a sub-marker-length step never survives next to a marker. Passes repeat, in edge-index
///     order, until nothing moves (bounded by <see cref="MaxPasses"/>), which keeps the stage
///     deterministic. The stage works in the pipeline's abstract axes, so it is direction-agnostic;
///     <see cref="AxisTransform"/> later maps the result to the requested flow direction.
///     </para>
/// </remarks>
internal sealed class PortAligner : ILayoutStage
{
    /// <summary>
    /// The smallest perpendicular step kept between two parallel runs of one connector when the
    /// two ports cannot be aligned: longer than every end marker's drawn length, so a step is never
    /// mistaken for a rendering glitch next to the marker.
    /// </summary>
    internal const double MinPortStep = 12.0;

    /// <summary>The fixed upper bound on alignment passes (a fix-point is normally reached in two).</summary>
    private const int MaxPasses = 4;

    /// <inheritdoc/>
    public void Apply(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (!graph.AlignPorts || graph.AugEdges.Count == 0)
        {
            return;
        }

        var augEdges = graph.AugEdges;
        var count = augEdges.Count;
        var srcPorts = graph.AugPortYSrc;
        var tgtPorts = graph.AugPortYTgt;

        // Group the edges of every node face, ordered by their current port coordinate (which is the
        // order PortDistributor assigned), so a port's neighbors on the same face are known.
        var srcFaces = BuildFaces(count, augEdges, srcPorts, isSource: true);
        var tgtFaces = BuildFaces(count, augEdges, tgtPorts, isSource: false);

        var srcAllowed = new Dictionary<int, IReadOnlyList<(double Lo, double Hi)>>();
        var tgtAllowed = new Dictionary<int, IReadOnlyList<(double Lo, double Hi)>>();

        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var moved = false;
            for (var ei = 0; ei < count; ei++)
            {
                var source = augEdges[ei].Source;
                var target = augEdges[ei].Target;
                var cs = srcPorts[ei];
                var ct = tgtPorts[ei];
                if (Math.Abs(cs - ct) < StraightTolerance)
                {
                    continue;
                }

                var srcWindow = Window(graph, srcFaces[source], srcAllowed, source, isSource: true, srcPorts, ei);
                var tgtWindow = Window(graph, tgtFaces[target], tgtAllowed, target, isSource: false, tgtPorts, ei);

                if (TryAlign(srcWindow, tgtWindow, cs, ct, out var meet))
                {
                    srcPorts[ei] = meet;
                    tgtPorts[ei] = meet;
                    moved = true;
                }
                else if (TrySeparate(srcWindow, tgtWindow, cs, ct, out var newSrc, out var newTgt))
                {
                    srcPorts[ei] = newSrc;
                    tgtPorts[ei] = newTgt;
                    moved = true;
                }
            }

            if (!moved)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Groups the sub-edges by the node face they leave (<paramref name="isSource"/>) or enter, each
    /// group ordered by the current port coordinate then edge index.
    /// </summary>
    private static Dictionary<int, List<int>> BuildFaces(
        int count,
        IReadOnlyList<AugEdge> augEdges,
        double[] ports,
        bool isSource)
    {
        var faces = new Dictionary<int, List<int>>();
        for (var ei = 0; ei < count; ei++)
        {
            var node = isSource ? augEdges[ei].Source : augEdges[ei].Target;
            if (!faces.TryGetValue(node, out var list))
            {
                list = [];
                faces[node] = list;
            }

            list.Add(ei);
        }

        foreach (var list in faces.Values)
        {
            list.Sort((a, b) =>
            {
                var byPort = ports[a].CompareTo(ports[b]);
                return byPort != 0 ? byPort : a.CompareTo(b);
            });
        }

        return faces;
    }

    /// <summary>
    /// Returns the intervals, in absolute abstract cross-axis coordinates, that the port of edge
    /// <paramref name="edgeIndex"/> may occupy: the node's allowed extents intersected with the span
    /// between its neighbors on the same face (kept <see cref="EdgeSpacing"/> away). The port's
    /// current position is always included, so a port that already violates a bound is never forced
    /// to move.
    /// </summary>
    private static List<(double Lo, double Hi)> Window(
        LayeredGraph graph,
        List<int> face,
        Dictionary<int, IReadOnlyList<(double Lo, double Hi)>> allowedCache,
        int node,
        bool isSource,
        double[] ports,
        int edgeIndex)
    {
        if (!allowedCache.TryGetValue(node, out var allowed))
        {
            allowed = AllowedExtents(graph, node, isSource);
            allowedCache[node] = allowed;
        }

        var current = ports[edgeIndex];
        var position = face.IndexOf(edgeIndex);
        var lowBound = position > 0 ? ports[face[position - 1]] + EdgeSpacing : double.NegativeInfinity;
        var highBound = position < face.Count - 1 ? ports[face[position + 1]] - EdgeSpacing : double.PositiveInfinity;

        var window = new List<(double Lo, double Hi)>();
        var containsCurrent = false;
        foreach (var (lo, hi) in allowed)
        {
            var clippedLo = Math.Max(lo, lowBound);
            var clippedHi = Math.Min(hi, highBound);
            if (clippedLo > clippedHi)
            {
                continue;
            }

            window.Add((clippedLo, clippedHi));
            containsCurrent |= current >= clippedLo - StraightTolerance && current <= clippedHi + StraightTolerance;
        }

        if (!containsCurrent)
        {
            window.Add((current, current));
        }

        return window;
    }

    /// <summary>
    /// Resolves the absolute cross-axis extents a port on the abstract source or target face of
    /// <paramref name="node"/> may use: the pinned coordinate for a dummy, the title-reserve-aware
    /// clearance-inset span for a plain rectangle, or the shape's usable connectable extents.
    /// </summary>
    private static IReadOnlyList<(double Lo, double Hi)> AllowedExtents(LayeredGraph graph, int node, bool isSource)
    {
        var top = graph.AugY[node];
        if (graph.AugNodes[node].IsDummy)
        {
            return [(top, top)];
        }

        var layerNode = graph.Nodes[node];
        if (ShapeAnchorSupport.IsPlainRectangle(layerNode))
        {
            var reserve = PortDistributor.TitleReserveFor(graph.Direction, layerNode);
            var lo = top + reserve + ConnectorClearance;
            var hi = top + layerNode.Height - ConnectorClearance;
            return hi < lo ? [] : [(lo, hi)];
        }

        var side = ShapeAnchorSupport.ResolveRealFace(graph.Direction, isSource);
        var geometry = ConnectorRouter.ResolveShapeGeometry(ShapeAnchorSupport.BuildAdapterBox(layerNode));
        var usable = ConnectorRouter.BuildUsableExtents(geometry.GetConnectableExtents(side), ConnectorClearance);
        var shifted = new List<(double Lo, double Hi)>(usable.Count);
        foreach (var (lo, hi) in usable)
        {
            shifted.Add((top + lo, top + hi));
        }

        return shifted;
    }

    /// <summary>
    /// Finds the point of the two windows' intersection nearest the mean of the current port
    /// coordinates, so both ends of the edge can sit on it.
    /// </summary>
    private static bool TryAlign(
        List<(double Lo, double Hi)> srcWindow,
        List<(double Lo, double Hi)> tgtWindow,
        double currentSrc,
        double currentTgt,
        out double meet)
    {
        var mean = (currentSrc + currentTgt) / 2.0;
        var found = false;
        var best = 0.0;
        var bestDistance = double.PositiveInfinity;
        foreach (var (sLo, sHi) in srcWindow)
        {
            foreach (var (tLo, tHi) in tgtWindow)
            {
                var lo = Math.Max(sLo, tLo);
                var hi = Math.Min(sHi, tHi);
                if (lo > hi)
                {
                    continue;
                }

                var candidate = Math.Clamp(mean, lo, hi);
                var distance = Math.Abs(candidate - mean);
                if (distance < bestDistance - StraightTolerance)
                {
                    found = true;
                    best = candidate;
                    bestDistance = distance;
                }
            }
        }

        meet = best;
        return found;
    }

    /// <summary>
    /// For a pair of ports that cannot be aligned, pushes the residual step out to at least
    /// <see cref="MinPortStep"/> where the windows allow (never creating a step shorter than the
    /// current one unless the current one is itself shorter than the minimum).
    /// </summary>
    private static bool TrySeparate(
        List<(double Lo, double Hi)> srcWindow,
        List<(double Lo, double Hi)> tgtWindow,
        double currentSrc,
        double currentTgt,
        out double newSrc,
        out double newTgt)
    {
        newSrc = currentSrc;
        newTgt = currentTgt;
        var step = currentTgt - currentSrc;
        if (Math.Abs(step) >= MinPortStep)
        {
            return false;
        }

        var sign = Math.Sign(step);
        var wanted = MinPortStep;
        var bestShortfall = double.PositiveInfinity;
        var found = false;
        foreach (var (sLo, sHi) in srcWindow)
        {
            foreach (var (tLo, tHi) in tgtWindow)
            {
                // Range of achievable signed steps (target minus source) for this interval pair.
                var minStep = tLo - sHi;
                var maxStep = tHi - sLo;
                var lowest = sign > 0 ? Math.Max(minStep, StraightTolerance) : Math.Max(-maxStep, StraightTolerance);
                var highest = sign > 0 ? maxStep : -minStep;
                if (lowest > highest)
                {
                    continue;
                }

                var magnitude = Math.Clamp(wanted, lowest, highest);
                var shortfall = wanted - magnitude;
                if (shortfall < bestShortfall - StraightTolerance)
                {
                    bestShortfall = shortfall;
                    found = true;
                    var (s, t) = Realize(sLo, sHi, tLo, tHi, currentSrc, currentTgt, sign * magnitude);
                    newSrc = s;
                    newTgt = t;
                }
            }
        }

        return found && Math.Abs(newTgt - newSrc) > Math.Abs(step) + StraightTolerance;
    }

    /// <summary>
    /// Moves the two ports, source first away from the target and then the target, so that
    /// <c>target - source</c> equals <paramref name="desiredStep"/> as nearly as the intervals allow.
    /// </summary>
    private static (double Source, double Target) Realize(
        double sLo,
        double sHi,
        double tLo,
        double tHi,
        double currentSrc,
        double currentTgt,
        double desiredStep)
    {
        var src = Math.Clamp(currentSrc, sLo, sHi);
        var tgt = Math.Clamp(currentTgt, tLo, tHi);
        var needed = desiredStep - (tgt - src);

        // Split the change evenly, then give whatever one end cannot absorb to the other.
        var tgtMoved = Math.Clamp(tgt + (needed / 2.0), tLo, tHi);
        var remaining = needed - (tgtMoved - tgt);
        var srcMoved = Math.Clamp(src - remaining, sLo, sHi);
        remaining -= src - srcMoved;
        tgtMoved = Math.Clamp(tgtMoved + remaining, tLo, tHi);
        return (srcMoved, tgtMoved);
    }
}
