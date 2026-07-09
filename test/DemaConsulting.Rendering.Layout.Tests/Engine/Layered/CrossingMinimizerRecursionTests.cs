// <copyright file="CrossingMinimizerRecursionTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Tests.Engine.Layered;

/// <summary>
///     Tests for the hierarchy-aware <see cref="CrossingMinimizer.MinimizeCrossingsRecursive"/> entry
///     point: genuine ELK-style recursive-descent crossing minimization over an assembled merge region,
///     where a child level's resolved boundary order propagates up into its parent's sweep, and the
///     parent's resolved boundary order (decided under outer-scope crossing pressure) propagates back
///     down so an ordinary interior node reorders. These tests operate on small hand-built two-level
///     hierarchies against the isolation seam, never through production wiring.
/// </summary>
public sealed class CrossingMinimizerRecursionTests
{
    /// <summary>
    ///     A two-level hierarchy in which the parent's own external crossing pressure is neutral (both
    ///     boundary ports of the container are fed by the same single external node) resolves its
    ///     boundary-crossing order by adopting the child level's resolved incoming order — proving the
    ///     child's order propagates upward and constrains the parent's barycenter sweep rather than the
    ///     parent falling back to boundary-discovery (index) order.
    /// </summary>
    [Fact]
    public void CrossingMinimizer_MinimizeCrossingsRecursive_TwoLevelHierarchy_ChildOrderPropagatesToParent()
    {
        // Arrange: parent scope { X, B(ports p1, p2) }; X fans out to both p1 and p2 (neutral external
        // pressure). B.Children { Ta, Tb, Pa, Pb } with delegations p1->Tb and p2->Ta, and ordinary
        // interior pins Pa->Ta (Pa above Pb by insertion) and Pb->Tb that fix Ta above Tb. The child's
        // own barycenter sweep therefore resolves its incoming crossing order to [p2, p1] — the reverse
        // of the boundary-discovery order [p1, p2].
        var scope = new LayoutGraph();
        var x = scope.AddNode("X", 80, 40);
        var b = scope.AddNode("B", 10, 10);
        var p1 = b.Ports.AddPort("p1");
        var p2 = b.Ports.AddPort("p2");
        var ta = b.Children.AddNode("Ta", 80, 40);
        var tb = b.Children.AddNode("Tb", 80, 40);
        var pa = b.Children.AddNode("Pa", 80, 40);
        var pb = b.Children.AddNode("Pb", 80, 40);
        scope.AddEdge("x-p1", x, p1);
        scope.AddEdge("x-p2", x, p2);
        b.Children.AddEdge("p1-tb", p1, tb);
        b.Children.AddEdge("p2-ta", p2, ta);
        b.Children.AddEdge("pa-ta", pa, ta);
        b.Children.AddEdge("pb-tb", pb, tb);
        var boundaries = HierarchyMergeRegionBuilder.Collect(scope);
        var region = MergeRegionGraphAssembler.Assemble(scope, boundaries, EmptySize());
        var childLevel = region.Root.Children[0].Child;
        var levels = MergeRegionGraphAssembler.BuildAllLevelGraphs(region, LayoutDirection.Right);
        PrepareLevels(region.Root, levels);

        // Act
        new CrossingMinimizer().MinimizeCrossingsRecursive(region.Root, levels);

        // Assert: the child resolved its incoming order to [p2, p1], and the parent adopted exactly that
        // order for its outgoing crossings — the child order propagated upward.
        var childOrder = CrossingOrder(levels[childLevel], HierarchyCrossingFace.Internal);
        var parentOrder = CrossingOrder(levels[region.Root], HierarchyCrossingFace.External);
        Assert.Equal(new[] { p2, p1 }, childOrder);
        Assert.Equal(new[] { p2, p1 }, parentOrder);
        Assert.Equal(childOrder, parentOrder);

        // Assert: the resolved parent order genuinely differs from the boundary-discovery (index) order,
        // so the assertion is not tautological — the propagation actually changed the outcome.
        Assert.NotEqual(new[] { p1, p2 }, parentOrder);
    }

    /// <summary>
    ///     A two-level hierarchy in which the parent's external crossing pressure is decisive (two
    ///     distinct external nodes feed the container's two boundary ports in an order opposite to the
    ///     child's own preference) forces the parent's boundary order, and that order propagates back
    ///     <em>down</em> into the child. The result reorders an ordinary (non-boundary) interior node:
    ///     its position under outer-scope pressure differs from the position the child's own isolated
    ///     crossing minimization would give it. This is a genuine demonstration of cross-boundary
    ///     optimization, asserted against the child's naive (no outer influence) baseline.
    /// </summary>
    [Fact]
    public void CrossingMinimizer_InteriorNodeReordering_OrdinaryNodeParticipatesInRealCrossingMinimization()
    {
        // Arrange: parent scope { N, M, B(ports p1, p2) }; N->p2 and M->p1 give the parent a decisive,
        // distinct external order [p2, p1] (opposite to the child's own preference [p1, p2]). B.Children
        // { T1, T2, Ord } with delegations p1->T1, p2->T2, and p2->Ord, so Ord is an ordinary interior
        // node reached only by the p2 crossing. In isolation the child orders Ord LAST; under the
        // parent's decisive [p2, p1] order propagated down, Ord moves to the MIDDLE.
        var scope = new LayoutGraph();
        var n = scope.AddNode("N", 80, 40);
        var m = scope.AddNode("M", 80, 40);
        var b = scope.AddNode("B", 10, 10);
        var p1 = b.Ports.AddPort("p1");
        var p2 = b.Ports.AddPort("p2");
        var t1 = b.Children.AddNode("T1", 80, 40);
        var t2 = b.Children.AddNode("T2", 80, 40);
        var ord = b.Children.AddNode("Ord", 80, 40);
        scope.AddEdge("n-p2", n, p2);
        scope.AddEdge("m-p1", m, p1);
        b.Children.AddEdge("p1-t1", p1, t1);
        b.Children.AddEdge("p2-t2", p2, t2);
        b.Children.AddEdge("p2-ord", p2, ord);
        var boundaries = HierarchyMergeRegionBuilder.Collect(scope);
        var region = MergeRegionGraphAssembler.Assemble(scope, boundaries, EmptySize());
        var childLevel = region.Root.Children[0].Child;
        var ordIndex = childLevel.NodeIndex[ord];

        // Act 1 (baseline): minimize the child level in ISOLATION with the flat entry point, so it sees
        // no outer-scope pressure. Ord settles into its child-alone position.
        var baselineLevels = MergeRegionGraphAssembler.BuildAllLevelGraphs(region, LayoutDirection.Right);
        PrepareLevels(region.Root, baselineLevels);
        new CrossingMinimizer().Apply(baselineLevels[childLevel].Graph);
        var naiveOrdPosition = PositionInLayer(baselineLevels[childLevel], ordIndex);

        // Act 2 (recursive): minimize the whole hierarchy, so the parent's decisive external order
        // propagates down and re-minimizes the child under outer-scope pressure.
        var recursiveLevels = MergeRegionGraphAssembler.BuildAllLevelGraphs(region, LayoutDirection.Right);
        PrepareLevels(region.Root, recursiveLevels);
        new CrossingMinimizer().MinimizeCrossingsRecursive(region.Root, recursiveLevels);
        var recursiveOrdPosition = PositionInLayer(recursiveLevels[childLevel], ordIndex);

        // Assert: in isolation the ordinary node Ord is ordered last (position 2); under outer-scope
        // pressure it moves to the middle (position 1). Its order genuinely changed because of the outer
        // scope, not merely its edge routing.
        Assert.Equal(2, naiveOrdPosition);
        Assert.Equal(1, recursiveOrdPosition);
        Assert.NotEqual(naiveOrdPosition, recursiveOrdPosition);
    }

    /// <summary>
    ///     Runs the per-level preparation stages that must precede crossing minimization — cycle
    ///     breaking, level-relative layer assignment, and long-edge splitting — mirroring the sequence
    ///     <see cref="LayeredLayoutPipeline.RunRecursive"/> applies before its crossing-minimization
    ///     stage, so a test can drive <see cref="CrossingMinimizer.MinimizeCrossingsRecursive"/> in
    ///     isolation.
    /// </summary>
    /// <param name="root">The root nesting level of the assembled region.</param>
    /// <param name="levels">The per-level layered graphs to prepare in place.</param>
    private static void PrepareLevels(
        MergeRegionLevel root,
        IReadOnlyDictionary<MergeRegionLevel, LevelLayeredGraph> levels)
    {
        foreach (var levelGraph in levels.Values)
        {
            new CycleBreaker().Apply(levelGraph.Graph);
        }

        new LayerAssigner().AssignLayersRecursive(root, levels);

        foreach (var levelGraph in levels.Values)
        {
            new LongEdgeSplitter().Apply(levelGraph.Graph);
        }
    }

    /// <summary>
    ///     Returns the boundary ports of the crossings on <paramref name="face"/> in their resolved
    ///     cross-axis order — their position within their layer group in <paramref name="levelGraph"/>.
    /// </summary>
    /// <param name="levelGraph">The already-minimized level graph.</param>
    /// <param name="face">The crossing face to read.</param>
    /// <returns>The boundary ports in resolved cross-axis order.</returns>
    private static List<LayoutGraphPort> CrossingOrder(LevelLayeredGraph levelGraph, HierarchyCrossingFace face)
    {
        var positions = Positions(levelGraph.Graph.Groups);
        return levelGraph.Crossings
            .Where(crossing => crossing.Face == face)
            .OrderBy(crossing => positions[crossing.NodeIndex])
            .Select(crossing => crossing.Boundary.Port)
            .ToList();
    }

    /// <summary>Returns the position of augmented node <paramref name="nodeIndex"/> within its layer group.</summary>
    /// <param name="levelGraph">The minimized level graph.</param>
    /// <param name="nodeIndex">The augmented-node index to locate.</param>
    /// <returns>The zero-based position within the node's layer, or <c>-1</c> when absent.</returns>
    private static int PositionInLayer(LevelLayeredGraph levelGraph, int nodeIndex)
    {
        foreach (var layer in levelGraph.Graph.Groups)
        {
            var position = layer.IndexOf(nodeIndex);
            if (position >= 0)
            {
                return position;
            }
        }

        return -1;
    }

    /// <summary>Returns a lookup from augmented-node index to its position within its layer group.</summary>
    /// <param name="groups">The per-layer ordered index lists.</param>
    /// <returns>A lookup from node index to cross-axis position.</returns>
    private static Dictionary<int, int> Positions(List<List<int>> groups)
    {
        var positions = new Dictionary<int, int>();
        foreach (var layer in groups)
        {
            for (var i = 0; i < layer.Count; i++)
            {
                positions[layer[i]] = i;
            }
        }

        return positions;
    }

    /// <summary>Creates an empty effective-size lookup, so every node uses its own declared dimensions.</summary>
    /// <returns>An empty node-to-size lookup.</returns>
    private static IReadOnlyDictionary<LayoutGraphNode, (double Width, double Height)> EmptySize()
    {
        return new Dictionary<LayoutGraphNode, (double Width, double Height)>();
    }
}
