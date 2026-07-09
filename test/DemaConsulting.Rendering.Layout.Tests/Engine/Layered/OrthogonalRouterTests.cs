// <copyright file="OrthogonalRouterTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Layout.Engine;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Tests.Engine.Layered;

/// <summary>
///     Tests for <see cref="LayeredCorridorRouter"/> covering that an aligned edge produces no bend
///     points and that every bend list is empty or a two-point vertical segment.
/// </summary>
public sealed class OrthogonalRouterTests
{
    /// <summary>
    ///     A single edge between two equal-height nodes is routed straight, so it produces no bend
    ///     points (its ports are aligned by the placer).
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Apply_StraightEdge_ProducesNoBendPoints()
    {
        // Arrange / Act: route a single 0->1 edge.
        var graph = BuildRoutedGraph(
            [new(60, 40), new(60, 40)],
            [new(0, 1)]);

        // Assert: the single sub-edge has no bend points.
        Assert.Single(graph.AugBendPoints);
        Assert.Empty(graph.AugBendPoints[0]);
    }

    /// <summary>
    ///     Every sub-edge's bend list is either empty (a straight run) or exactly two points that
    ///     share an X coordinate (a vertical routing segment).
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Apply_EveryBendListIsEmptyOrVerticalSegment()
    {
        // Arrange / Act: route a four-node diamond.
        var graph = BuildRoutedGraph(
            [new(60, 40), new(60, 40), new(60, 40), new(60, 40)],
            [new(0, 1), new(0, 2), new(1, 3), new(2, 3)]);

        // Assert: each bend list is empty or a vertical two-point segment.
        Assert.All(graph.AugBendPoints, bend =>
            Assert.True(
                bend.Count == 0 || (bend.Count == 2 && Math.Abs(bend[0].X - bend[1].X) < 1e-9),
                $"Unexpected bend geometry with {bend.Count} points."));
    }

    /// <summary>
    ///     Two mirror-symmetric approach edges converging on one shared target (the boundary-port
    ///     anchor scenario: <c>Monitor</c> and <c>Operator</c> both approaching the same port from
    ///     opposite sides) must receive the same routing slot, so their first-bend offsets from the
    ///     shared target are identical. Both sub-edges share the exact same <c>TargetY</c> (the same
    ///     target node/port), so <see cref="LayeredCorridorRouter"/>'s crossing-count tie-break used to
    ///     force them into different slots purely by insertion order, producing an asymmetric jog for
    ///     whichever edge happened to be considered second. With the fix, converging segments whose
    ///     target Y values coincide are not forced apart, so both bends land at the same corridor X.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Apply_MirrorSymmetricConvergingEdges_ProduceIdenticalFirstBendOffsets()
    {
        // Arrange / Act: two layer-0 sources (one above, one below the shared target's Y) both
        // converging on the single layer-1 target node - the exact "Monitor"/"Operator" shape. The
        // target has zero height (mirroring the real hierarchy-crossing dummy anchor both approaches
        // actually converge on), so both incoming ports collapse onto the same target Y rather than
        // being spread across a real node's face by the port distributor.
        var graph = BuildRoutedGraph(
            [new(60, 40), new(60, 40), new(0, 0)],
            [new(0, 2), new(1, 2)]);

        // Assert: both sub-edges bend (neither is a degenerate straight run - the two sources sit on
        // opposite sides of the shared target by construction), and the bend X (routing slot) - the
        // first-bend offset from the shared anchor - is identical for both.
        Assert.Equal(2, graph.AugBendPoints[0].Count);
        Assert.Equal(2, graph.AugBendPoints[1].Count);
        Assert.Equal(graph.AugBendPoints[0][0].X, graph.AugBendPoints[1][0].X, precision: 9);
    }

    /// <summary>
    ///     Two mirror-symmetric diverging edges fanning out from one shared source (the boundary-port
    ///     anchor scenario: <c>dispatch</c> branching to both <c>Driver</c> and <c>Logger</c>) must
    ///     receive the same routing slot, so their first-bend offsets from the shared source are
    ///     identical. Both sub-edges share the exact same <c>SourceY</c> (the same source node/port),
    ///     so <see cref="LayeredCorridorRouter"/>'s crossing-count tie-break used to force them into
    ///     different slots purely by insertion order, producing an asymmetric fork for whichever edge
    ///     happened to be considered second. With the fix, diverging segments whose source Y values
    ///     coincide are not forced apart, so both bends land at the same corridor X.
    /// </summary>
    [Fact]
    public void OrthogonalRouter_Apply_MirrorSymmetricDivergingEdges_ProduceIdenticalFirstBendOffsets()
    {
        // Arrange / Act: a single zero-height layer-0 source (mirroring the real boundary-port anchor
        // both branches actually diverge from) fanning out to two equal-size layer-1 targets - the
        // exact "dispatch"/"Driver"/"Logger" shape - one above, one below the shared source's Y.
        var graph = BuildRoutedGraph(
            [new(0, 0), new(60, 40), new(60, 40)],
            [new(0, 1), new(0, 2)]);

        // Assert: both sub-edges bend (neither is a degenerate straight run - the two targets sit on
        // opposite sides of the shared source by construction), and the bend X (routing slot) - the
        // first-bend offset from the shared anchor - is identical for both.
        Assert.Equal(2, graph.AugBendPoints[0].Count);
        Assert.Equal(2, graph.AugBendPoints[1].Count);
        Assert.Equal(graph.AugBendPoints[0][0].X, graph.AugBendPoints[1][0].X, precision: 9);
    }

    /// <summary>Runs the stages up to and including orthogonal routing and returns the graph.</summary>
    /// <param name="nodes">Input nodes.</param>
    /// <param name="edges">Input edges.</param>
    /// <returns>The graph after the orthogonal-routing stage.</returns>
    private static LayeredGraph BuildRoutedGraph(List<LayerNode> nodes, List<LayerEdge> edges)
    {
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        new LongEdgeSplitter().Apply(graph);
        new CrossingMinimizer().Apply(graph);
        new BrandesKopfPlacer().Apply(graph);
        new PortDistributor().Apply(graph);
        new LayeredCorridorRouter().Apply(graph);
        return graph;
    }
}
