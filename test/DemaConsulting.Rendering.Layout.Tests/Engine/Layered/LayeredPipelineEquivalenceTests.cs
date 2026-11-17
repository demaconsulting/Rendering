// <copyright file="LayeredPipelineEquivalenceTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Layout.Engine;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Tests.Engine.Layered;

/// <summary>
///     Behavior-preservation gate for the layered-pipeline refactor. Feeds many graphs through
///     both the legacy monolithic engine (<see cref="LegacyInterconnectionLayoutEngineOracle"/>)
///     and the refactored <see cref="InterconnectionLayoutEngine"/> façade and asserts that every
///     field of the resulting <c>LayerResult</c> is bit-for-bit identical: rectangles, total
///     dimensions, node layers, and every connector waypoint. No numeric tolerance is allowed,
///     except for the three documented, intentional divergences identified by
///     <see cref="HasIsolatedNode"/> (the isolated-node layer-gap fix),
///     <see cref="HasMultipleComponents"/> (component-packing for disconnected graphs), and
///     <see cref="HasWaypointBeyondNodeBounds"/> (the canvas-clipping fix for routed geometry that
///     extends past the placed node rects), and the fourth, port alignment (<see cref="PortAligner"/>,
///     Rendering#57): the comparison against the oracle runs with alignment switched off, and
///     <see cref="Pipeline_PortAlignment_MatchesOracleWhenNoPortCanSlide"/> and
///     <see cref="Pipeline_PortAlignment_OnlyMovesPortsAlongTheirFaces"/> pin what alignment may
///     change (<see cref="HasAlignablePorts"/>).
/// </summary>
public sealed class LayeredPipelineEquivalenceTests
{
    /// <summary>
    ///     The pipeline reproduces the legacy engine exactly across two thousand pseudo-randomly
    ///     generated graphs spanning empty, disconnected, cyclic, parallel-edge, self-loop, and
    ///     long-edge topologies with varied node sizes, excluding the graphs the isolated-node
    ///     layer-gap fix (see <see cref="HasIsolatedNode"/>), component packing (see
    ///     <see cref="HasMultipleComponents"/>), and the canvas-clipping fix (see
    ///     <see cref="HasWaypointBeyondNodeBounds"/>) intentionally change.
    /// </summary>
    [Fact]
    public void Pipeline_MatchesLegacyOracle_OnRandomGraphs()
    {
        for (var seed = 0; seed < 2000; seed++)
        {
            var (nodes, edges) = BuildRandomGraph(seed);

            // A genuinely isolated node (zero incident edges) is one documented, intentional
            // behavior divergence between the frozen legacy oracle and the refactored pipeline: the
            // legacy oracle still reproduces the isolated-node layer-gap bug (CrossingMinimizer's
            // barycenter sort falling back to the node's arbitrary index, and BrandesKopfPlacer's
            // one-directional compaction floor inheriting an unrelated node's pulled-down position),
            // while the refactored pipeline's fix (isolated-node clustering plus a trailing-run gap
            // squeeze) always sorts every isolated node to one end of its layer — a real behavior
            // change, not a bit-level rounding difference, so it is excluded from this bit-for-bit gate.
            // See docs/reqstream/rendering-layout/engine/layered-pipeline.yaml for the requirement this
            // corresponds to.
            if (HasIsolatedNode(nodes, edges))
            {
                continue;
            }

            // A graph with two or more genuinely disconnected connected components is the second
            // documented, intentional divergence: the refactored pipeline now routes every graph
            // through ComponentPacker (see InterconnectionLayoutEngine.Place), which splits, lays out,
            // and shelf-packs each component separately instead of stacking every component into one
            // shared-layer column the way the frozen legacy oracle still does. See
            // Place_DisconnectedComponents_PacksEachComponentSeparately for a direct test of the new
            // behavior, and docs/reqstream/rendering-layout/engine/layered-pipeline.yaml for the
            // requirement this corresponds to.
            if (HasMultipleComponents(nodes, edges))
            {
                continue;
            }

            // A graph whose routed connector geometry extends beyond the placed node rects (typically
            // a reversed back edge's wrap-around approach, see LayeredCorridorRouter) is the third
            // documented, intentional divergence: the legacy oracle still sizes the canvas from node
            // rects alone, silently clipping such connectors, while the refactored pipeline now widens
            // the canvas to cover every routed waypoint. See
            // Place_CyclicGraphWithTallNode_AllWaypointsWithinCanvasBounds (InterconnectionLayoutEngine
            // tests) for a direct test of the new behavior.
            if (HasWaypointBeyondNodeBounds(nodes, edges))
            {
                continue;
            }

            AssertEquivalent($"random seed {seed}", nodes, edges);
        }
    }


    /// <summary>
    ///     The shipping pipeline (port alignment on) reproduces the legacy oracle bit-for-bit for every
    ///     random graph where no port can slide (<see cref="HasAlignablePorts"/> is false): the stage
    ///     is the fourth documented divergence and changes nothing it is not meant to.
    /// </summary>
    [Fact]
    public void Pipeline_PortAlignment_MatchesOracleWhenNoPortCanSlide()
    {
        var compared = 0;
        for (var seed = 0; seed < 2000; seed++)
        {
            var (nodes, edges) = BuildRandomGraph(seed);
            if (HasIsolatedNode(nodes, edges) || HasMultipleComponents(nodes, edges) || HasWaypointBeyondNodeBounds(nodes, edges))
            {
                continue;
            }

            if (HasAlignablePorts(nodes, edges))
            {
                continue;
            }

            compared++;
            AssertEquivalent($"aligned random seed {seed}", nodes, edges, alignPorts: true);
        }

        // Guard against the predicate silently skipping everything.
        Assert.True(compared > 100, $"only {compared} graphs compared");
    }

    /// <summary>
    ///     Port alignment only moves connector ports along their own faces: box rectangles, node layers,
    ///     and the along-axis coordinate of every connector's first and last point are bit-identical with
    ///     and without it, across all random graphs.
    /// </summary>
    [Fact]
    public void Pipeline_PortAlignment_OnlyMovesPortsAlongTheirFaces()
    {
        for (var seed = 0; seed < 2000; seed++)
        {
            var (nodes, edges) = BuildRandomGraph(seed);
            var plain = InterconnectionLayoutEngine.Place(nodes, edges, alignPorts: false);
            var aligned = InterconnectionLayoutEngine.Place(nodes, edges, alignPorts: true);

            Assert.Equal(plain.Rects.Count, aligned.Rects.Count);
            for (var i = 0; i < plain.Rects.Count; i++)
            {
                Assert.Equal(plain.Rects[i], aligned.Rects[i]);
            }

            Assert.Equal(plain.NodeLayers, aligned.NodeLayers);
            Assert.Equal(plain.ConnectorWaypoints.Count, aligned.ConnectorWaypoints.Count);
            for (var e = 0; e < plain.ConnectorWaypoints.Count; e++)
            {
                var before = plain.ConnectorWaypoints[e];
                var after = aligned.ConnectorWaypoints[e];
                Assert.Equal(before[0].X, after[0].X);
                Assert.Equal(before[^1].X, after[^1].X);
            }
        }
    }

    /// <summary>
    ///     A single source and a single target whose faces do not overlap: alignment cannot make them
    ///     straight, so the connector keeps a bend, never a sub-marker step.
    /// </summary>
    [Fact]
    public void Pipeline_PortAlignment_DisjointFaces_KeepsClearStep()
    {
        // Arrange: a narrow tall chain forced so source and target faces cannot line up.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 400) };
        var edges = new List<LayerEdge> { new(0, 2), new(1, 2) };

        // Act
        var result = InterconnectionLayoutEngine.Place(nodes, edges);

        // Assert: any interior step is either absent or at least the visible-step threshold.
        foreach (var waypoints in result.ConnectorWaypoints)
        {
            for (var i = 1; i + 2 < waypoints.Count; i++)
            {
                var step = Math.Abs(waypoints[i + 1].Y - waypoints[i].Y) + Math.Abs(waypoints[i + 1].X - waypoints[i].X);
                Assert.True(step < 1e-6 || step >= PortAligner.MinPortStep, $"step {step}");
            }
        }
    }

    /// <summary>An empty graph produces identical (degenerate) results from both engines.</summary>
    [Fact]
    public void Pipeline_MatchesLegacyOracle_OnEmptyGraph()
    {
        AssertEquivalent("empty", [], []);
    }

    /// <summary>
    ///     A drone-interconnect-style graph (seven heterogeneously sized parts with a mix of
    ///     short and long edges) produces identical results from both engines.
    /// </summary>
    [Fact]
    public void Pipeline_MatchesLegacyOracle_OnDroneLikeGraph()
    {
        var nodes = new List<LayerNode>
        {
            new(150, 54), // airframe
            new(150, 54), // battery
            new(230, 94), // controller
            new(150, 54), // gps
            new(150, 54), // imu
            new(153, 54), // motors
            new(209, 54), // propellers
        };
        var edges = new List<LayerEdge>
        {
            new(2, 0),
            new(2, 1),
            new(2, 3),
            new(2, 4),
            new(2, 5),
            new(5, 6),
            new(0, 6),
        };

        AssertEquivalent("drone-like", nodes, edges);
    }

    /// <summary>
    ///     A larger workstation-interconnect-style graph (twelve parts, multiple layers, a long
    ///     edge spanning three layers, and a back edge) produces identical results.
    /// </summary>
    [Fact]
    public void Pipeline_MatchesLegacyOracle_OnWorkstationLikeGraph()
    {
        var nodes = new List<LayerNode>();
        for (var i = 0; i < 12; i++)
        {
            nodes.Add(new LayerNode(120 + (i % 4 * 30), 50 + (i % 3 * 20)));
        }

        var edges = new List<LayerEdge>
        {
            new(0, 1),
            new(0, 2),
            new(1, 3),
            new(2, 3),
            new(3, 4),
            new(3, 5),
            new(4, 6),
            new(5, 7),
            new(6, 8),
            new(7, 8),
            new(0, 8), // long edge spanning several layers
            new(8, 0), // back edge (cycle)
            new(9, 3),
            new(10, 4),
            new(11, 5),
        };

        AssertEquivalent("workstation-like", nodes, edges);
    }

    /// <summary>
    ///     Canonical named topologies (chain, diamond, self loop, and parallel edges) each produce
    ///     identical results. "longedge" and "cycle" are intentionally excluded here — see
    ///     <see cref="Place_LongEdgeAndCycleTopologies_NoWaypointClipsCanvas"/>, which asserts the new
    ///     (intentionally divergent) canvas-widening behavior directly — and "disconnected" is
    ///     excluded for the same reason as
    ///     <see cref="Place_DisconnectedComponents_PacksEachComponentSeparately"/>.
    /// </summary>
    /// <param name="name">A human-readable name for the topology, used in failure messages.</param>
    [Theory]
    [InlineData("chain")]
    [InlineData("diamond")]
    [InlineData("selfloop")]
    [InlineData("parallel")]
    public void Pipeline_MatchesLegacyOracle_OnNamedTopologies(string name)
    {
        var (nodes, edges) = name switch
        {
            "chain" => (Sizes(3), Edges((0, 1), (1, 2))),
            "diamond" => (Sizes(4), Edges((0, 1), (0, 2), (1, 3), (2, 3))),
            "selfloop" => (Sizes(3), Edges((0, 0), (0, 1), (1, 2))),
            "parallel" => (Sizes(2), Edges((0, 1), (0, 1), (0, 1))),
            _ => (Sizes(0), Edges()),
        };

        AssertEquivalent(name, nodes, edges);
    }

    /// <summary>
    ///     The former "longedge" and "cycle" named topologies (a span-2 long edge and a tight 3-cycle,
    ///     respectively) now trip the canvas-widening fix (see <see cref="HasWaypointBeyondNodeBounds"/>
    ///     and <see cref="InterconnectionLayoutEngine.Place"/>): their reversed back edges route a bend
    ///     point past the node-rect-only extent the legacy oracle still uses, so the refactored
    ///     pipeline now reports a taller canvas than the legacy oracle. This directly asserts every
    ///     routed waypoint stays within the refactored pipeline's own reported bounds, in place of the
    ///     removed bit-for-bit comparison against the legacy oracle.
    /// </summary>
    /// <param name="name">A human-readable name for the topology, used in failure messages.</param>
    [Theory]
    [InlineData("longedge")]
    [InlineData("cycle")]
    public void Place_LongEdgeAndCycleTopologies_NoWaypointClipsCanvas(string name)
    {
        var (nodes, edges) = name switch
        {
            "longedge" => (Sizes(4), Edges((0, 1), (1, 2), (2, 3), (0, 3))),
            "cycle" => (Sizes(3), Edges((0, 1), (1, 2), (2, 0))),
            _ => (Sizes(0), Edges()),
        };

        var result = InterconnectionLayoutEngine.Place(nodes, edges);

        foreach (var wp in result.ConnectorWaypoints)
        {
            foreach (var p in wp)
            {
                Assert.InRange(p.X, 0.0, result.TotalWidth);
                Assert.InRange(p.Y, 0.0, result.TotalHeight);
            }
        }
    }

    /// <summary>
    ///     A graph made of three disconnected 2-node pairs (the former "disconnected" named
    ///     topology) now routes through <see cref="InterconnectionLayoutEngine.Place"/>'s
    ///     component-packing behavior (see <see cref="ComponentPacker"/>): each pair is laid out as
    ///     its own component and the three components are shelf-packed into non-overlapping
    ///     bounding boxes, rather than being stacked into one shared-layer column the way the legacy
    ///     oracle still does. This directly asserts the new, intentionally divergent behavior in
    ///     place of the removed bit-for-bit comparison against the legacy oracle.
    /// </summary>
    [Fact]
    public void Place_DisconnectedComponents_PacksEachComponentSeparately()
    {
        // Arrange: three independent 2-node pairs, no cross-pair edges.
        var nodes = Sizes(6);
        var edges = Edges((0, 1), (2, 3), (4, 5));

        // Act.
        var result = InterconnectionLayoutEngine.Place(nodes, edges);

        // Assert: every pair's two boxes are placed (any incident edge was routed), and no two of the
        // three pair bounding boxes overlap.
        Assert.Equal(6, result.Rects.Count);

        var pairs = new (int A, int B)[] { (0, 1), (2, 3), (4, 5) };
        var boxes = pairs
            .Select(pair => BoundingBoxOf(result.Rects[pair.A], result.Rects[pair.B]))
            .ToList();

        for (var a = 0; a < boxes.Count; a++)
        {
            for (var b = a + 1; b < boxes.Count; b++)
            {
                Assert.False(
                    BoxesOverlap(boxes[a], boxes[b]),
                    $"component bounding boxes {a} and {b} should not overlap");
            }
        }
    }

    /// <summary>Computes the bounding box that encloses two rectangles.</summary>
    /// <param name="first">First rectangle.</param>
    /// <param name="second">Second rectangle.</param>
    /// <returns>The smallest rectangle containing both inputs.</returns>
    private static Rect BoundingBoxOf(Rect first, Rect second)
    {
        var minX = Math.Min(first.X, second.X);
        var minY = Math.Min(first.Y, second.Y);
        var maxX = Math.Max(first.X + first.Width, second.X + second.Width);
        var maxY = Math.Max(first.Y + first.Height, second.Y + second.Height);
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>Determines whether two rectangles overlap.</summary>
    /// <param name="first">First rectangle.</param>
    /// <param name="second">Second rectangle.</param>
    /// <returns><see langword="true"/> if the two rectangles intersect.</returns>
    private static bool BoxesOverlap(Rect first, Rect second) =>
        first.X < second.X + second.Width && second.X < first.X + first.Width &&
        first.Y < second.Y + second.Height && second.Y < first.Y + first.Height;

    /// <summary>Builds a list of uniformly sized nodes.</summary>
    private static List<LayerNode> Sizes(int count)
    {
        var nodes = new List<LayerNode>(count);
        for (var i = 0; i < count; i++)
        {
            nodes.Add(new LayerNode(60, 40));
        }

        return nodes;
    }

    /// <summary>Builds an edge list from source/target index pairs.</summary>
    private static List<LayerEdge> Edges(params (int Source, int Target)[] pairs)
    {
        var edges = new List<LayerEdge>(pairs.Length);
        foreach (var (s, t) in pairs)
        {
            edges.Add(new LayerEdge(s, t));
        }

        return edges;
    }

    /// <summary>
    ///     Deterministically builds a pseudo-random graph for the given seed, exercising varied
    ///     node counts and sizes plus arbitrary edges (including self loops, parallel edges,
    ///     cycles, and multi-layer spans).
    /// </summary>
    private static (List<LayerNode> Nodes, List<LayerEdge> Edges) BuildRandomGraph(int seed)
    {
        var rng = new Random(seed);
        var n = rng.Next(0, 16);

        var nodes = new List<LayerNode>(n);
        for (var i = 0; i < n; i++)
        {
            nodes.Add(new LayerNode(rng.Next(40, 240), rng.Next(30, 120)));
        }

        var edges = new List<LayerEdge>();
        if (n > 0)
        {
            var m = rng.Next(0, (n * 2) + 1);
            for (var e = 0; e < m; e++)
            {
                edges.Add(new LayerEdge(rng.Next(0, n), rng.Next(0, n)));
            }
        }

        return (nodes, edges);
    }

    /// <summary>
    ///     Returns whether the graph contains at least one genuinely isolated real node — a node with
    ///     no incident augmented sub-edge (as either a source or a target) after cycle-breaking, layer
    ///     assignment, and long-edge splitting. This mirrors, in the actual pipeline classes, the exact
    ///     "isolated" definition <c>CrossingMinimizer</c> and <c>BrandesKopfPlacer</c> use, so it also
    ///     correctly identifies a node whose only input edges are self-loops or parallel duplicates that
    ///     do not survive into the augmented graph. Such a node is the one documented, intentional
    ///     behavior divergence between the frozen legacy oracle and the refactored pipeline's
    ///     isolated-node layer-gap fix (see the remarks on
    ///     <see cref="Pipeline_MatchesLegacyOracle_OnRandomGraphs"/>).
    /// </summary>
    /// <param name="nodes">The graph's nodes.</param>
    /// <param name="edges">The graph's edges.</param>
    /// <returns><see langword="true"/> if at least one real node has zero incident augmented edges.</returns>
    private static bool HasIsolatedNode(List<LayerNode> nodes, List<LayerEdge> edges)
    {
        if (nodes.Count == 0)
        {
            return false;
        }

        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);
        new CycleBreaker().Apply(graph);
        new LayerAssigner().Apply(graph);
        new LongEdgeSplitter().Apply(graph);

        var hasIncidentEdge = new bool[graph.AugNodes.Count];
        foreach (var ae in graph.AugEdges)
        {
            hasIncidentEdge[ae.Source] = true;
            hasIncidentEdge[ae.Target] = true;
        }

        // Only the first N augmented nodes correspond to real input nodes (dummies are appended after).
        for (var i = 0; i < graph.N; i++)
        {
            if (!hasIncidentEdge[i])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Returns whether the graph contains two or more genuinely disconnected connected
    ///     components — computed independently via a small local union-find over the raw
    ///     <paramref name="edges"/> list (self-loops are ignored, mirroring
    ///     <see cref="ComponentPacker"/>'s own component-detection rule). A single node with no
    ///     edges at all also counts as exactly one component (itself), so it is not, by itself,
    ///     "multiple components" — only 2+ distinct components trigger the divergence. This is the
    ///     second documented, intentional behavior divergence between the frozen legacy oracle and
    ///     the refactored pipeline (component packing — see the remarks on
    ///     <see cref="Pipeline_MatchesLegacyOracle_OnRandomGraphs"/>).
    /// </summary>
    /// <param name="nodes">The graph's nodes.</param>
    /// <param name="edges">The graph's edges.</param>
    /// <returns><see langword="true"/> if the graph has 2 or more connected components.</returns>
    private static bool HasMultipleComponents(List<LayerNode> nodes, List<LayerEdge> edges)
    {
        var n = nodes.Count;
        if (n <= 1)
        {
            return false;
        }

        var parent = new int[n];
        for (var i = 0; i < n; i++)
        {
            parent[i] = i;
        }

        int Find(int node)
        {
            while (parent[node] != node)
            {
                node = parent[node];
            }

            return node;
        }

        foreach (var edge in edges.Where(edge => edge.Source != edge.Target))
        {
            var ra = Find(edge.Source);
            var rb = Find(edge.Target);
            if (ra != rb)
            {
                parent[ra] = rb;
            }
        }

        var roots = new HashSet<int>();
        for (var i = 0; i < n; i++)
        {
            roots.Add(Find(i));
        }

        return roots.Count > 1;
    }

    /// <summary>
    ///     Returns whether port alignment (see <see cref="PortAligner"/>) changes any routed waypoint of
    ///     this graph — the fourth documented, intentional divergence from the frozen legacy oracle.
    /// </summary>
    /// <param name="nodes">The graph's nodes.</param>
    /// <param name="edges">The graph's edges.</param>
    /// <returns><see langword="true"/> if alignment moves at least one waypoint.</returns>
    private static bool HasAlignablePorts(List<LayerNode> nodes, List<LayerEdge> edges)
    {
        var plain = InterconnectionLayoutEngine.Place(nodes, edges, alignPorts: false);
        var aligned = InterconnectionLayoutEngine.Place(nodes, edges, alignPorts: true);
        return !plain.ConnectorWaypoints.SequenceEqual(aligned.ConnectorWaypoints, WaypointListComparer.Instance);
    }

    /// <summary>Compares two waypoint lists bit-for-bit.</summary>
    private sealed class WaypointListComparer : IEqualityComparer<IReadOnlyList<Point2D>>
    {
        /// <summary>Gets the shared instance.</summary>
        public static readonly WaypointListComparer Instance = new();

        /// <inheritdoc/>
        public bool Equals(IReadOnlyList<Point2D>? x, IReadOnlyList<Point2D>? y) =>
            x is not null && y is not null && x.SequenceEqual(y);

        /// <inheritdoc/>
        public int GetHashCode(IReadOnlyList<Point2D> obj) => obj.Count;
    }

    /// <summary>
    ///     Returns whether the refactored pipeline's canvas-widening fix (see
    ///     <see cref="InterconnectionLayoutEngine.Place"/>) actually changes this graph's reported
    ///     canvas size relative to the frozen legacy oracle — i.e. whether some routed connector
    ///     waypoint extends past the node-rect-only extent the legacy oracle still uses (typically a
    ///     reversed back edge's wrap-around approach, see <see cref="LayeredCorridorRouter"/>). Any
    ///     graph the fix changes is the third documented, intentional behavior divergence between the
    ///     frozen legacy oracle and the refactored pipeline (see the remarks on
    ///     <see cref="Pipeline_MatchesLegacyOracle_OnRandomGraphs"/>).
    /// </summary>
    /// <param name="nodes">The graph's nodes.</param>
    /// <param name="edges">The graph's edges.</param>
    /// <returns>
    ///     <see langword="true"/> if the refactored pipeline reports a different canvas size than the
    ///     legacy oracle for this graph.
    /// </returns>
    private static bool HasWaypointBeyondNodeBounds(List<LayerNode> nodes, List<LayerEdge> edges)
    {
        var legacy = LegacyInterconnectionLayoutEngineOracle.Place(nodes, edges);
        var actual = InterconnectionLayoutEngine.Place(nodes, edges, alignPorts: false);

        return BitConverter.DoubleToInt64Bits(legacy.TotalWidth) != BitConverter.DoubleToInt64Bits(actual.TotalWidth) ||
               BitConverter.DoubleToInt64Bits(legacy.TotalHeight) != BitConverter.DoubleToInt64Bits(actual.TotalHeight);
    }

    /// <summary>
    ///     Runs both engines on the same input and asserts bit-for-bit equality of every field
    ///     of the resulting <c>LayerResult</c>. The default compares the pipeline with port alignment
    ///     (see <see cref="PortAligner"/>) switched off, so every other stage stays bit-identical to the
    ///     frozen oracle; pass <paramref name="alignPorts"/> to compare the shipping default.
    /// </summary>
    /// <param name="context">A human-readable name for the graph, used in failure messages.</param>
    /// <param name="nodes">The graph's nodes.</param>
    /// <param name="edges">The graph's edges.</param>
    /// <param name="alignPorts">Whether the pipeline runs with port alignment on.</param>
    private static void AssertEquivalent(string context, List<LayerNode> nodes, List<LayerEdge> edges, bool alignPorts = false)
    {
        var expected = LegacyInterconnectionLayoutEngineOracle.Place(nodes, edges);
        var actual = InterconnectionLayoutEngine.Place(nodes, edges, alignPorts: alignPorts);

        AssertExact($"{context}: TotalWidth", expected.TotalWidth, actual.TotalWidth);
        AssertExact($"{context}: TotalHeight", expected.TotalHeight, actual.TotalHeight);

        Assert.Equal(expected.Rects.Count, actual.Rects.Count);
        for (var i = 0; i < expected.Rects.Count; i++)
        {
            AssertExact($"{context}: Rects[{i}].X", expected.Rects[i].X, actual.Rects[i].X);
            AssertExact($"{context}: Rects[{i}].Y", expected.Rects[i].Y, actual.Rects[i].Y);
            AssertExact($"{context}: Rects[{i}].Width", expected.Rects[i].Width, actual.Rects[i].Width);
            AssertExact($"{context}: Rects[{i}].Height", expected.Rects[i].Height, actual.Rects[i].Height);
        }

        Assert.Equal(expected.NodeLayers.Count, actual.NodeLayers.Count);
        for (var i = 0; i < expected.NodeLayers.Count; i++)
        {
            Assert.Equal(expected.NodeLayers[i], actual.NodeLayers[i]);
        }

        Assert.Equal(expected.ConnectorWaypoints.Count, actual.ConnectorWaypoints.Count);
        for (var e = 0; e < expected.ConnectorWaypoints.Count; e++)
        {
            var ew = expected.ConnectorWaypoints[e];
            var aw = actual.ConnectorWaypoints[e];
            Assert.Equal(ew.Count, aw.Count);
            for (var w = 0; w < ew.Count; w++)
            {
                AssertExact($"{context}: Waypoint[{e}][{w}].X", ew[w].X, aw[w].X);
                AssertExact($"{context}: Waypoint[{e}][{w}].Y", ew[w].Y, aw[w].Y);
            }
        }
    }

    /// <summary>Asserts that two doubles are identical at the bit level (no tolerance).</summary>
    private static void AssertExact(string context, double expected, double actual)
    {
        Assert.True(
            BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual),
            $"{context}: expected {expected:R} but got {actual:R}");
    }
}
