// <copyright file="BrandesKopfPlacer.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>
using static DemaConsulting.Rendering.Layout.Engine.Layered.LayeredLayoutMetrics;

namespace DemaConsulting.Rendering.Layout.Engine.Layered;

/// <summary>
/// Pipeline stage that assigns absolute X and Y coordinates to all augmented nodes using ELK's
/// horizontal placement and the Brandes-Kopf balanced four-layout Y placement.
/// </summary>
internal sealed class BrandesKopfPlacer : ILayoutStage
{
    /// <inheritdoc/>
    public void Apply(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var (augX, augY, columnX, maxColWidth) = AssignCoordinatesAug(
            graph.AugNodes, graph.Groups, graph.AugEdges, graph.NodeSpacing);
        graph.AugX = augX;
        graph.AugY = augY;
        graph.ColumnX = columnX;
        graph.MaxColWidth = maxColWidth;
    }

    /// <summary>
    /// Assigns absolute X and Y coordinates to all augmented nodes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Y assignment follows the Brandes-Köpf (BK) four-layout balanced algorithm
    /// (<c>BKNodePlacer</c> + <c>BKAligner</c> + <c>BKCompactor</c>): four independent
    /// vertical alignments (DOWN/UP × RIGHT/LEFT) are compacted and their per-node medians
    /// averaged to produce port-aligned, crossing-minimized vertical positions.
    /// </para>
    /// <para>
    /// X assignment follows ELK's <c>LGraphUtil.placeNodesHorizontally</c>: dummies are placed at
    /// the horizontal center of their column; real nodes are left-aligned to their column start.
    /// Corridor widths are derived from sub-edge counts per corridor.
    /// </para>
    /// </remarks>
    private static (double[] AugX, double[] AugY, double[] ColumnX, double[] MaxColWidth) AssignCoordinatesAug(
        List<AugNode> augNodes,
        List<List<int>> groups,
        List<AugEdge> augEdges,
        double nodeSpacing)
    {
        var layerCount = groups.Count;
        var numAug = augNodes.Count;

        // Maximum real-node width per layer (dummies have width 0).
        var maxColWidth = new double[layerCount];
        for (var i = 0; i < numAug; i++)
        {
            if (!augNodes[i].IsDummy)
            {
                maxColWidth[augNodes[i].Layer] = Math.Max(maxColWidth[augNodes[i].Layer], augNodes[i].Width);
            }
        }

        // Sub-edges per corridor: one sub-edge per augEdge, keyed on source layer.
        var corridorEdgeCounts = new int[Math.Max(1, layerCount - 1)];
        foreach (var ae in augEdges)
        {
            var l = augNodes[ae.Source].Layer;
            if (l >= 0 && l < corridorEdgeCounts.Length)
            {
                corridorEdgeCounts[l]++;
            }
        }

        // Column X positions. Corridor width: ELK routingWidth = 2*edgeNodeSpacing + (n-1)*edgeEdgeSpacing.
        var columnX = new double[layerCount];
        columnX[0] = Padding;
        for (var l = 1; l < layerCount; l++)
        {
            var cnt = corridorEdgeCounts[l - 1];
            var corridorWidth = cnt > 0
                ? Math.Max(CorridorMinWidth, (2.0 * ConnectorClearance) + ((cnt - 1) * EdgeSpacing))
                : CorridorMinWidth;
            columnX[l] = columnX[l - 1] + maxColWidth[l - 1] + corridorWidth;
        }

        // Assign X coordinates: dummies are centered in their column; real nodes left-align.
        var augX = new double[numAug];
        for (var l = 0; l < layerCount; l++)
        {
            var colCenterX = columnX[l] + (maxColWidth[l] / 2.0);
            foreach (var ni in groups[l])
            {
                augX[ni] = augNodes[ni].IsDummy ? colCenterX : columnX[l];
            }
        }

        // Assign Y coordinates using the Brandes-Köpf balanced four-layout algorithm.
        var augY = BkAssignYCoordinates(augNodes, groups, augEdges, nodeSpacing);

        // Honor any pinned cross-axis coordinate: a hierarchy-crossing dummy tagged with
        // AugNode.PinnedCrossAxis (set by MergeRegionGraphAssembler.PinIncomingCrossings for the
        // recursive pipeline) must anchor to its parent scope's already-resolved boundary-port position
        // rather than the ordinary fork-centering value the alignment/compaction above just computed for
        // it. This bypasses ordinary placement for that node only; every other node's value is untouched.
        // AugNode.PinnedCrossAxis defaults to null and is never set by the flat (non-hierarchical)
        // pipeline, so this loop is a no-op there.
        for (var i = 0; i < numAug; i++)
        {
            if (augNodes[i].PinnedCrossAxis is { } pinnedCrossAxis)
            {
                augY[i] = pinnedCrossAxis;
            }
        }

        return (augX, augY, columnX, maxColWidth);
    }

    /// <summary>
    /// Assigns Y coordinates to all augmented nodes using the four-layout Brandes-Köpf
    /// balanced algorithm, producing port-aligned vertical positions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs four independent (vDown × hRight) alignment-and-compaction pipelines, one for
    /// each combination of vertical scan direction (DOWN = top-to-bottom, UP = bottom-to-top)
    /// and horizontal scan direction (RIGHT = layer 0 → max, LEFT = layer max → 0). The
    /// per-node average of the two middle values of the four results gives the final balanced
    /// position. Padding is added once at the end.
    /// </para>
    /// <para>
    /// Corresponds to ELK's BKNodePlacer orchestrating BKAligner and BKCompactor.
    /// </para>
    /// </remarks>
    private static double[] BkAssignYCoordinates(
        List<AugNode> augNodes,
        List<List<int>> groups,
        List<AugEdge> augEdges,
        double nodeSpacing)
    {
        var numAug = augNodes.Count;

        // Step 0: precompute port positions, layer positions, and neighbor-edge lists.
        BkPreprocess(
            augNodes, groups, augEdges,
            out var posInLayer,
            out var srcRelPortY,
            out var tgtRelPortY,
            out var leftNeighborEdges,
            out var rightNeighborEdges);

        // Step 1: mark type-1 conflicts (non-inner segments crossing inner segments).
        var markedEdges = BkMarkConflicts(augNodes, groups, augEdges, posInLayer, leftNeighborEdges);

        // Steps 2–4: compute four independent (vDown × hRight) layouts.
        var layouts = new double[4][];
        for (var d = 0; d < 4; d++)
        {
            // d=0: DOWN+RIGHT, d=1: UP+RIGHT, d=2: DOWN+LEFT, d=3: UP+LEFT.
            var vDown = d % 2 == 0;
            var hRight = d < 2;

            // Step 2: vertical alignment — builds block chains along the scan direction.
            BkVerticalAlignment(
                augNodes, groups, augEdges, posInLayer,
                leftNeighborEdges, rightNeighborEdges,
                markedEdges, vDown, hRight,
                out var root, out var align);

            // Step 3: inside-block shift — adjusts nodes within each block to align ports.
            var innerShift = BkInsideBlockShift(
                augNodes, augEdges, root, align,
                srcRelPortY, tgtRelPortY, hRight,
                rightNeighborEdges, leftNeighborEdges);

            // Step 4: horizontal compaction — assigns absolute Y to each block root.
            var blockY = BkHorizontalCompaction(
                augNodes, groups, root, align, innerShift, posInLayer, vDown, nodeSpacing);

            // Compute absolute Y for every node in this layout.
            var y = new double[numAug];
            for (var i = 0; i < numAug; i++)
            {
                y[i] = blockY[root[i]] + innerShift[i];
            }

            layouts[d] = y;
        }

        // Step 5: normalize each layout and return the balanced (median average) result.
        var balanced = BkBalancedLayout(layouts, numAug);

        // Step 6: relax residual port-centroid error for "hub" nodes — a real node with 2+ ports on
        // one face connecting to 2+ distinct sibling nodes in an adjacent layer. Classic BK's
        // per-node four-pass median only ever aligns a hub with ONE of its same-face neighbors per
        // pass (the low or high median), so averaging the four passes gives each such neighbor only a
        // partial (roughly half-magnitude) correction rather than the true least-squares-optimal
        // position. See RelaxPortCentroids for the iterative fix.
        return RelaxPortCentroids(augNodes, groups, augEdges, balanced, srcRelPortY, tgtRelPortY, nodeSpacing);
    }

    /// <summary>
    /// Iteratively nudges every node's cross-axis position toward the least-squares-optimal
    /// position implied by its neighbors' current positions and each edge's fixed port offsets,
    /// re-projecting each layer back onto its order-preserving minimum-spacing constraint after
    /// every sweep.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Classic Brandes-Köpf balancing (<see cref="BkBalancedLayout"/>) gives an excellent result for
    /// ordinary chains and single-sided forks — the existing four-pass median already reaches the
    /// least-squares optimum for those shapes, so this relaxation converges in its first sweep with
    /// zero change (verified: a chain or one-sided fork's per-edge desired position already equals its
    /// current position, since <see cref="BkBalancedLayout"/> already aligns it exactly). It only
    /// meaningfully moves nodes for "hub" shapes — a real node with 2+ differently-positioned ports on
    /// one face, each connecting to a different sibling node in an adjacent layer — where the classic
    /// algorithm's one-median-per-pass limitation leaves a systematic partial-correction residual.
    /// </para>
    /// <para>
    /// Each sweep computes, per node, the average of every incident edge's implied ideal position
    /// (the neighbor's current position offset by the difference in relative port offsets), then
    /// projects each layer's desired positions onto the closest order-preserving, minimum-spacing
    /// sequence via isotonic regression (<see cref="ProjectSingleLayerOrder"/>) — so relative node order and
    /// <paramref name="nodeSpacing"/> are always honored exactly as the single-pass algorithm would
    /// enforce them. Iteration stops early once no node's position changes by more than a small
    /// tolerance, or after a fixed sweep cap (bounded so pathological graphs cannot loop indefinitely);
    /// the last computed values are used either way, so a graph that has not fully converged by the cap
    /// still gets a partial improvement over the uncorrected balanced result, never a regression.
    /// </para>
    /// </remarks>
    private static double[] RelaxPortCentroids(
        List<AugNode> augNodes,
        List<List<int>> groups,
        List<AugEdge> augEdges,
        double[] initialY,
        double[] srcRelPortY,
        double[] tgtRelPortY,
        double nodeSpacing)
    {
        const int maxSweeps = 12;
        const double tolerance = 1e-9;

        var numAug = augNodes.Count;

        // incident[v]: for every edge touching v, the neighbor node index and the (myRel, otherRel)
        // port offsets needed to compute v's implied ideal position from that neighbor's position:
        // implied = y[neighbor] + otherRel - myRel.
        var incident = new List<(int Neighbor, double MyRel, double OtherRel)>[numAug];
        for (var i = 0; i < numAug; i++)
        {
            incident[i] = [];
        }

        for (var ei = 0; ei < augEdges.Count; ei++)
        {
            var e = augEdges[ei];
            incident[e.Source].Add((e.Target, srcRelPortY[ei], tgtRelPortY[ei]));
            incident[e.Target].Add((e.Source, tgtRelPortY[ei], srcRelPortY[ei]));
        }

        var y = (double[])initialY.Clone();
        var desiredForLayer = new double[augNodes.Count];
        var layerCount = groups.Count;

        for (var sweep = 0; sweep < maxSweeps; sweep++)
        {
            // Alternate sweep direction each iteration (forward then backward through the layer
            // sequence) so corrections propagate across long layer chains in far fewer sweeps than a
            // single fixed direction would need — mirroring why the classic four-pass algorithm itself
            // scans both DOWN/UP and RIGHT/LEFT.
            var layerStart = sweep % 2 == 0 ? 0 : layerCount - 1;
            var layerEnd = sweep % 2 == 0 ? layerCount - 1 : 0;
            var layerStep = sweep % 2 == 0 ? 1 : -1;

            var maxDelta = 0.0;
            for (var l = layerStart; sweep % 2 == 0 ? l <= layerEnd : l >= layerEnd; l += layerStep)
            {
                var layer = groups[l];
                foreach (var i in layer)
                {
                    var edges = incident[i];
                    if (edges.Count == 0)
                    {
                        desiredForLayer[i] = y[i];
                        continue;
                    }

                    // Gauss-Seidel: neighbors in already-processed layers this sweep contribute their
                    // freshly-updated position; neighbors not yet reached this sweep contribute their
                    // value from the previous sweep (still in y). Using only fresh values (Jacobi-style
                    // batching of a whole sweep before any update is visible) makes this bipartite-like
                    // system oscillate with period 2 instead of converging — verified by hand-tracing a
                    // hub example, where a whole-sweep batch update ping-pongs between two states forever.
                    var sum = 0.0;
                    foreach (var (neighbor, myRel, otherRel) in edges)
                    {
                        sum += y[neighbor] + otherRel - myRel;
                    }

                    desiredForLayer[i] = sum / edges.Count;
                }

                ProjectSingleLayerOrder(augNodes, layer, desiredForLayer, nodeSpacing, y, ref maxDelta);
            }

            if (maxDelta < tolerance)
            {
                break;
            }
        }

        return y;
    }

    /// <summary>
    /// Projects one layer's desired positions onto the closest (least-squares) sequence that
    /// preserves the layer's existing node order and every consecutive pair's minimum required gap,
    /// writing the result directly into <paramref name="y"/> and tracking the largest per-node change.
    /// </summary>
    /// <remarks>
    /// Subtracting the cumulative minimum gap up to each node reduces the "must be at least this much
    /// further than the previous node" constraint to a plain "must be no less than the previous node"
    /// (weakly non-decreasing) constraint — exactly the classic isotonic regression problem solved by
    /// <see cref="PoolAdjacentViolators"/>. Adding the same cumulative offset back afterward restores
    /// the minimum-gap semantics.
    /// </remarks>
    private static void ProjectSingleLayerOrder(
        List<AugNode> augNodes,
        List<int> layer,
        double[] desired,
        double nodeSpacing,
        double[] y,
        ref double maxDelta)
    {
        var n = layer.Count;
        if (n == 0)
        {
            return;
        }

        var offset = new double[n];
        var adjusted = new double[n];
        adjusted[0] = desired[layer[0]];
        for (var i = 1; i < n; i++)
        {
            offset[i] = offset[i - 1] + augNodes[layer[i - 1]].Height + nodeSpacing;
            adjusted[i] = desired[layer[i]] - offset[i];
        }

        var pooled = PoolAdjacentViolators(adjusted);

        for (var i = 0; i < n; i++)
        {
            var node = layer[i];
            var newValue = pooled[i] + offset[i];
            maxDelta = Math.Max(maxDelta, Math.Abs(newValue - y[node]));
            y[node] = newValue;
        }
    }

    /// <summary>
    /// Solves the classic (unweighted) isotonic regression problem: finds the weakly non-decreasing
    /// sequence closest (in least-squares distance) to <paramref name="values"/>, via the
    /// pool-adjacent-violators algorithm.
    /// </summary>
    private static double[] PoolAdjacentViolators(double[] values)
    {
        var n = values.Length;
        var result = new double[n];

        // Each stack entry is a pooled block: its mean and how many original elements it spans.
        var blockMean = new double[n];
        var blockCount = new int[n];
        var top = -1;

        for (var i = 0; i < n; i++)
        {
            top++;
            blockMean[top] = values[i];
            blockCount[top] = 1;

            // Merge back while the new block's mean would violate monotonicity with its predecessor.
            while (top > 0 && blockMean[top - 1] > blockMean[top])
            {
                var mergedCount = blockCount[top - 1] + blockCount[top];
                var mergedMean = ((blockMean[top - 1] * blockCount[top - 1]) + (blockMean[top] * blockCount[top])) / mergedCount;
                top--;
                blockMean[top] = mergedMean;
                blockCount[top] = mergedCount;
            }
        }

        var index = 0;
        for (var b = 0; b <= top; b++)
        {
            for (var c = 0; c < blockCount[b]; c++)
            {
                result[index++] = blockMean[b];
            }
        }

        return result;
    }

    /// <summary>
    /// Precomputes the lookup tables required by all four Brandes-Köpf layout passes:
    /// per-node layer position, relative port Y offsets, and sorted neighbor-edge lists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Port positions follow ELK's BKAligner preprocessing convention: a dummy node
    /// contributes relative port Y = 0 (the wire passes straight through); a real node
    /// centers each of its ports within its own equal-width slice of the face — see
    /// <see cref="PortDistributor.DistributePorts"/>, which this preview must mirror so the
    /// alignment heuristic optimizes against the same port positions the pipeline ultimately
    /// renders.
    /// </para>
    /// <para>
    /// <paramref name="leftNeighborEdges"/>[v] lists augmented-edge indices whose target is v,
    /// sorted ascending by the source node's position within its layer — used for RIGHT-direction
    /// alignment. <paramref name="rightNeighborEdges"/>[v] lists augmented-edge indices whose
    /// source is v, sorted ascending by the target node's position — used for LEFT-direction
    /// alignment. Both lists are stable-sorted by edge index as a tiebreaker.
    /// </para>
    /// </remarks>
    private static void BkPreprocess(
        List<AugNode> augNodes,
        List<List<int>> groups,
        List<AugEdge> augEdges,
        out int[] posInLayer,
        out double[] srcRelPortY,
        out double[] tgtRelPortY,
        out List<int>[] leftNeighborEdges,
        out List<int>[] rightNeighborEdges)
    {
        var numAug = augNodes.Count;
        var numEdges = augEdges.Count;

        // Position of each node within its layer group (0-based index in groups[layer]).
        posInLayer = new int[numAug];
        for (var l = 0; l < groups.Count; l++)
        {
            for (var k = 0; k < groups[l].Count; k++)
            {
                posInLayer[groups[l][k]] = k;
            }
        }

        // Collect outgoing/incoming edge indices per node for port computation.
        // Capture posInLayer in a local so it can be used inside lambda expressions
        // (C# prohibits capturing out parameters directly in lambdas — CS1628).
        var posLayer = posInLayer;
        var outEdges = new List<int>[numAug];
        var inEdges = new List<int>[numAug];
        for (var i = 0; i < numAug; i++)
        {
            outEdges[i] = [];
            inEdges[i] = [];
        }

        for (var ei = 0; ei < numEdges; ei++)
        {
            outEdges[augEdges[ei].Source].Add(ei);
            inEdges[augEdges[ei].Target].Add(ei);
        }

        // srcRelPortY[e]: Y of the source (EAST) port relative to source node's top-left.
        // Dummy nodes pass the wire through at Y = 0 relative to their own position.
        srcRelPortY = new double[numEdges];
        for (var ni = 0; ni < numAug; ni++)
        {
            var edges = outEdges[ni];
            if (edges.Count == 0)
            {
                continue;
            }

            if (augNodes[ni].IsDummy)
            {
                foreach (var ei in edges)
                {
                    srcRelPortY[ei] = 0.0;
                }
            }
            else
            {
                // Sort by target's position in its layer, then edge index for stability.
                var sorted = edges
                    .OrderBy(ei => posLayer[augEdges[ei].Target])
                    .ThenBy(ei => ei)
                    .ToList();
                var portCount = sorted.Count;
                for (var k = 0; k < portCount; k++)
                {
                    srcRelPortY[sorted[k]] = (k + 0.5) * augNodes[ni].Height / portCount;
                }
            }
        }

        // tgtRelPortY[e]: Y of the target (WEST) port relative to target node's top-left.
        tgtRelPortY = new double[numEdges];
        for (var ni = 0; ni < numAug; ni++)
        {
            var edges = inEdges[ni];
            if (edges.Count == 0)
            {
                continue;
            }

            if (augNodes[ni].IsDummy)
            {
                foreach (var ei in edges)
                {
                    tgtRelPortY[ei] = 0.0;
                }
            }
            else
            {
                // Sort by source's position in its layer, then edge index for stability.
                var sorted = edges
                    .OrderBy(ei => posLayer[augEdges[ei].Source])
                    .ThenBy(ei => ei)
                    .ToList();
                var portCount = sorted.Count;
                for (var k = 0; k < portCount; k++)
                {
                    tgtRelPortY[sorted[k]] = (k + 0.5) * augNodes[ni].Height / portCount;
                }
            }
        }

        // leftNeighborEdges[v]: edges whose Target == v, sorted by source posInLayer.
        leftNeighborEdges = new List<int>[numAug];
        for (var i = 0; i < numAug; i++)
        {
            leftNeighborEdges[i] = [];
        }

        for (var ei = 0; ei < numEdges; ei++)
        {
            leftNeighborEdges[augEdges[ei].Target].Add(ei);
        }

        for (var i = 0; i < numAug; i++)
        {
            leftNeighborEdges[i].Sort((a, b) =>
            {
                var c = posLayer[augEdges[a].Source].CompareTo(posLayer[augEdges[b].Source]);
                return c != 0 ? c : a.CompareTo(b);
            });
        }

        // rightNeighborEdges[v]: edges whose Source == v, sorted by target posInLayer.
        rightNeighborEdges = new List<int>[numAug];
        for (var i = 0; i < numAug; i++)
        {
            rightNeighborEdges[i] = [];
        }

        for (var ei = 0; ei < numEdges; ei++)
        {
            rightNeighborEdges[augEdges[ei].Source].Add(ei);
        }

        for (var i = 0; i < numAug; i++)
        {
            rightNeighborEdges[i].Sort((a, b) =>
            {
                var c = posLayer[augEdges[a].Target].CompareTo(posLayer[augEdges[b].Target]);
                return c != 0 ? c : a.CompareTo(b);
            });
        }
    }

    /// <summary>
    /// Returns true when <paramref name="v"/> is incident to an inner segment — a sub-edge
    /// where both endpoints are dummy nodes, forming part of a long-edge chain.
    /// </summary>
    /// <remarks>
    /// An inner segment has both endpoints as dummy nodes. The check inspects v's
    /// lowest-positioned left neighbor (leftNeighborEdges[v][0]). Used by type-1 conflict
    /// detection to identify nodes that anchor inner segments across layer boundaries.
    /// </remarks>
    private static bool IsIncidentToInnerSegment(
        int v,
        List<AugNode> augNodes,
        List<AugEdge> augEdges,
        List<int>[] leftNeighborEdges)
        => augNodes[v].IsDummy
            && leftNeighborEdges[v].Count > 0
            && augNodes[augEdges[leftNeighborEdges[v][0]].Source].IsDummy;

    /// <summary>
    /// Marks augmented edges that participate in type-1 conflicts: a non-inner segment
    /// (at least one real-node endpoint) that crosses an inner segment (both endpoints
    /// are dummy nodes from a long-edge chain).
    /// </summary>
    /// <remarks>
    /// Implements ELK's <c>markConflicts</c> procedure from BKNodePlacer. For each pair of
    /// adjacent middle layers (i, i+1), the algorithm tracks the permitted source-position
    /// range [k0, k1] established by each inner segment and marks any non-inner segment
    /// whose source falls outside that range. Marked edges are excluded from vertical
    /// alignment to preserve the topology of inner segments.
    /// </remarks>
    private static HashSet<int> BkMarkConflicts(
        List<AugNode> augNodes,
        List<List<int>> groups,
        List<AugEdge> augEdges,
        int[] posInLayer,
        List<int>[] leftNeighborEdges)
    {
        var markedEdges = new HashSet<int>();
        var maxLayer = groups.Count - 1;

        // Examine middle layers: i is the source side, i+1 is the target side.
        for (var i = 1; i < maxLayer; i++)
        {
            var leftLayerSize = groups[i].Count;
            var rightLayer = groups[i + 1];

            // k0: lower bound of the permitted source-position range for the current batch.
            var k0 = 0;

            // l: left cursor into the right layer (start of the current batch).
            var l = 0;

            for (var l1 = 0; l1 < rightLayer.Count; l1++)
            {
                var v = rightLayer[l1];
                var incident = IsIncidentToInnerSegment(v, augNodes, augEdges, leftNeighborEdges);

                // Flush the batch at the last node or at each inner-segment anchor.
                if (l1 != rightLayer.Count - 1 && !incident)
                {
                    continue;
                }

                // k1: upper bound of the permitted source-position range for this batch.
                var k1 = leftLayerSize - 1;
                if (incident)
                {
                    var innerEdge = leftNeighborEdges[v][0];
                    k1 = posInLayer[augEdges[innerEdge].Source];
                }

                // Mark non-inner segments in the batch whose sources are out of range.
                while (l <= l1)
                {
                    var vl = rightLayer[l];
                    if (!IsIncidentToInnerSegment(vl, augNodes, augEdges, leftNeighborEdges))
                    {
                        foreach (var edgeIdx in leftNeighborEdges[vl])
                        {
                            var k = posInLayer[augEdges[edgeIdx].Source];
                            if (k < k0 || k > k1)
                            {
                                markedEdges.Add(edgeIdx);
                            }
                        }
                    }

                    l++;
                }

                k0 = k1;
            }
        }

        return markedEdges;
    }

    /// <summary>
    /// Performs vertical alignment for one Brandes-Köpf layout direction, building the
    /// circular block-chain structure that groups co-aligned nodes into blocks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements ELK's BKAligner.verticalAlignment. Each node v is aligned with the
    /// median neighbor in the previous (RIGHT) or next (LEFT) layer that is still
    /// unaligned (align[v] == v) and whose layer-position satisfies the monotone
    /// constraint r. The monotone constraint prevents crossings between aligned pairs.
    /// </para>
    /// <para>
    /// On output, <paramref name="root"/>[i] identifies the block root for every node i,
    /// and <paramref name="align"/>[i] is the next node in the circular chain
    /// (root → n1 → n2 → … → root).
    /// </para>
    /// </remarks>
    private static void BkVerticalAlignment(
        List<AugNode> augNodes,
        List<List<int>> groups,
        List<AugEdge> augEdges,
        int[] posInLayer,
        List<int>[] leftNeighborEdges,
        List<int>[] rightNeighborEdges,
        HashSet<int> markedEdges,
        bool vDown,
        bool hRight,
        out int[] root,
        out int[] align)
    {
        var numAug = augNodes.Count;
        var maxLayer = groups.Count - 1;

        // Every node starts as its own singleton block.
        root = new int[numAug];
        align = new int[numAug];
        for (var i = 0; i < numAug; i++)
        {
            root[i] = i;
            align[i] = i;
        }

        // Layer iteration order: RIGHT scans forward (0..maxLayer); LEFT scans in reverse.
        var layerStart = hRight ? 0 : maxLayer;
        var layerEnd = hRight ? maxLayer : 0;
        var layerStep = hRight ? 1 : -1;

        for (var l = layerStart; hRight ? l <= layerEnd : l >= layerEnd; l += layerStep)
        {
            var layer = groups[l];

            // r: monotone position constraint; tracks the last aligned neighbor's position.
            var r = vDown ? -1 : int.MaxValue;

            // Node iteration order: DOWN scans forward (0..N-1); UP scans in reverse.
            var nodeStart = vDown ? 0 : layer.Count - 1;
            var nodeEnd = vDown ? layer.Count - 1 : 0;
            var nodeStep = vDown ? 1 : -1;

            for (var ni = nodeStart; vDown ? ni <= nodeEnd : ni >= nodeEnd; ni += nodeStep)
            {
                var v = layer[ni];
                var neighbors = hRight ? leftNeighborEdges[v] : rightNeighborEdges[v];
                var d = neighbors.Count;
                if (d == 0)
                {
                    continue;
                }

                // Median index range for this node's neighbor list.
                var low = (int)Math.Floor((d + 1) / 2.0) - 1;
                var high = (int)Math.Ceiling((d + 1) / 2.0) - 1;

                // Try median neighbors in vdir order; stop as soon as v is aligned.
                var mStart = vDown ? low : high;
                var mEnd = vDown ? high : low;
                var mStep = vDown ? 1 : -1;

                for (var m = mStart; vDown ? m <= mEnd : m >= mEnd; m += mStep)
                {
                    // Stop iterating once v has been aligned with a neighbor.
                    if (align[v] != v)
                    {
                        break;
                    }

                    var edgeIdx = neighbors[m];
                    var uIdx = hRight ? augEdges[edgeIdx].Source : augEdges[edgeIdx].Target;

                    if (markedEdges.Contains(edgeIdx))
                    {
                        continue;
                    }

                    var pos = posInLayer[uIdx];
                    if (vDown ? r < pos : r > pos)
                    {
                        // Extend the block chain: insert v between uIdx and the current root.
                        align[uIdx] = v;
                        root[v] = root[uIdx];
                        align[v] = root[v];
                        r = pos;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Computes the inside-block shift for each node: the vertical offset relative to its
    /// block root that makes the connecting ports co-linear within the block.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements ELK's BKAligner.insideBlockShift. For each block root, walks the circular
    /// align chain accumulating port-position differences between consecutive nodes. The
    /// accumulated shifts are then normalized so the topmost node in the block has
    /// innerShift = 0, making blockY the absolute Y of the block's highest point.
    /// </para>
    /// <para>
    /// For hdir=RIGHT the edge between consecutive chain nodes goes source → target (earlier
    /// layer to later layer), so portDiff = srcRelPortY − tgtRelPortY. For hdir=LEFT the
    /// direction reverses, so portDiff = tgtRelPortY − srcRelPortY.
    /// </para>
    /// </remarks>
    private static double[] BkInsideBlockShift(
        List<AugNode> augNodes,
        List<AugEdge> augEdges,
        int[] root,
        int[] align,
        double[] srcRelPortY,
        double[] tgtRelPortY,
        bool hRight,
        List<int>[] rightNeighborEdges,
        List<int>[] leftNeighborEdges)
    {
        var numAug = augNodes.Count;
        var innerShift = new double[numAug];

        // Process each block identified by its root node.
        for (var r = 0; r < numAug; r++)
        {
            if (root[r] != r)
            {
                continue;
            }

            // Walk the circular chain accumulating port-difference shifts.
            var spaceAbove = 0.0;
            var spaceBelow = augNodes[r].Height;

            var current = r;
            var next = align[r];
            while (next != r)
            {
                // Locate the augmented edge that links consecutive block-chain nodes.
                // For RIGHT: edge current → next (earlier → later layer).
                // For LEFT: edge next → current (earlier → later layer, chain walks backward).
                var edgeIdx = hRight
                    ? BkFindEdge(rightNeighborEdges[current], augEdges, next, findByTarget: true)
                    : BkFindEdge(leftNeighborEdges[current], augEdges, next, findByTarget: false);

                // Port alignment: accumulate the source-minus-target port offset.
                var portDiff = hRight
                    ? srcRelPortY[edgeIdx] - tgtRelPortY[edgeIdx]
                    : tgtRelPortY[edgeIdx] - srcRelPortY[edgeIdx];

                innerShift[next] = innerShift[current] + portDiff;
                spaceAbove = Math.Max(spaceAbove, -innerShift[next]);
                spaceBelow = Math.Max(spaceBelow, innerShift[next] + augNodes[next].Height);

                current = next;
                next = align[current];
            }

            // Normalize: add spaceAbove to all shifts so the topmost node is at offset 0.
            if (spaceAbove > 0.0)
            {
                var node = r;
                do
                {
                    innerShift[node] += spaceAbove;
                    node = align[node];
                }
                while (node != r);
            }
        }

        return innerShift;
    }

    /// <summary>
    /// Finds the augmented edge in <paramref name="edgeList"/> that connects to
    /// <paramref name="matchNode"/>, searching by target when
    /// <paramref name="findByTarget"/> is true, or by source otherwise.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="BkInsideBlockShift"/> to locate the edge that links consecutive
    /// nodes in a block chain. Because block chains are built strictly along real augmented
    /// edges, the edge is always present; returning −1 would indicate a logic error in the
    /// vertical-alignment phase.
    /// </remarks>
    private static int BkFindEdge(
        List<int> edgeList,
        List<AugEdge> augEdges,
        int matchNode,
        bool findByTarget)
    {
        foreach (var ei in edgeList)
        {
            if ((findByTarget ? augEdges[ei].Target : augEdges[ei].Source) == matchNode)
            {
                return ei;
            }
        }

        return -1; // Should not occur: block chains are always built along real edges.
    }

    /// <summary>
    /// Assigns an absolute Y coordinate (blockY) to each block root by compacting blocks
    /// in the vertical direction, respecting per-node heights and <paramref name="nodeSpacing"/> gaps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements ELK's BKCompactor.horizontalCompaction / placeBlock. For vdir=DOWN,
    /// blocks are packed top-to-bottom starting at Y = 0, constrained from above by the
    /// node immediately above each chain member in its layer. For vdir=UP, blocks are
    /// packed bottom-to-top starting at Y = 0, constrained from below (yielding negative
    /// values that are normalized in <see cref="BkBalancedLayout"/>).
    /// </para>
    /// <para>
    /// The local <c>PlaceBlock</c> function places a block root's Y coordinate by recursively
    /// ensuring every constraining block is placed first (memoized via a NaN sentinel).
    /// </para>
    /// </remarks>
    private static double[] BkHorizontalCompaction(
        List<AugNode> augNodes,
        List<List<int>> groups,
        int[] root,
        int[] align,
        double[] innerShift,
        int[] posInLayer,
        bool vDown,
        double nodeSpacing)
    {
        var maxLayer = groups.Count - 1;
        var blockY = new double[augNodes.Count];
        Array.Fill(blockY, double.NaN);

        // Recursively place a block root, memoized by the NaN-unplaced sentinel.
        void PlaceBlock(int v)
        {
            if (!double.IsNaN(blockY[v]))
            {
                return;
            }

            blockY[v] = 0.0;

            // Enforce separation constraints for every node in this block's chain.
            var current = v;
            do
            {
                var layer = augNodes[current].Layer;
                var idx = posInLayer[current];
                var layerNodes = groups[layer];

                if (vDown)
                {
                    // DOWN: constrain from above — current must be below the node at idx−1.
                    if (idx > 0)
                    {
                        var above = layerNodes[idx - 1];
                        var aboveRoot = root[above];
                        PlaceBlock(aboveRoot);

                        var requiredY = blockY[aboveRoot]
                            + innerShift[above]
                            + augNodes[above].Height
                            + nodeSpacing
                            - innerShift[current];
                        blockY[v] = Math.Max(blockY[v], requiredY);
                    }
                }
                else
                {
                    // UP: constrain from below — current must be above the node at idx+1.
                    if (idx < layerNodes.Count - 1)
                    {
                        var below = layerNodes[idx + 1];
                        var belowRoot = root[below];
                        PlaceBlock(belowRoot);

                        var requiredY = blockY[belowRoot]
                            + innerShift[below]
                            - nodeSpacing
                            - augNodes[current].Height
                            - innerShift[current];
                        blockY[v] = Math.Min(blockY[v], requiredY);
                    }
                }

                current = align[current];
            }
            while (current != v);
        }

        // Trigger placement for all block roots in vdir processing order.
        var layerStart = vDown ? 0 : maxLayer;
        var layerEnd = vDown ? maxLayer : 0;
        var layerStep = vDown ? 1 : -1;

        for (var l = layerStart; vDown ? l <= layerEnd : l >= layerEnd; l += layerStep)
        {
            var layer = groups[l];
            var nodeStart = vDown ? 0 : layer.Count - 1;
            var nodeEnd = vDown ? layer.Count - 1 : 0;
            var nodeStep = vDown ? 1 : -1;

            for (var ni = nodeStart; vDown ? ni <= nodeEnd : ni >= nodeEnd; ni += nodeStep)
            {
                var v = layer[ni];
                if (root[v] == v)
                {
                    PlaceBlock(v);
                }
            }
        }

        return blockY;
    }

    /// <summary>
    /// Normalizes four independent Brandes-Köpf layouts (each shifted so its minimum Y = 0)
    /// and returns the per-node average of the two middle values, giving the balanced result.
    /// </summary>
    /// <remarks>
    /// Implements ELK's BKNodePlacer balanced-layout combination. Each of the four layouts
    /// has a distinct direction bias (DOWN/UP × RIGHT/LEFT); the median average cancels
    /// those biases while preserving the port-alignment constraints of each individual
    /// layout. <see cref="Padding"/> is added once so all returned coordinates are absolute.
    /// </remarks>
    private static double[] BkBalancedLayout(double[][] layouts, int numAug)
    {
        // Normalize each layout: shift so the minimum absolute Y across all nodes is 0.
        foreach (var y in layouts)
        {
            var minY = y.Min();
            for (var i = 0; i < numAug; i++)
            {
                y[i] -= minY;
            }
        }

        // For each node, sort the four Y values and average the two middle ones.
        var finalY = new double[numAug];
        for (var i = 0; i < numAug; i++)
        {
            var ys = new[] { layouts[0][i], layouts[1][i], layouts[2][i], layouts[3][i] };
            Array.Sort(ys);
            finalY[i] = ((ys[1] + ys[2]) / 2.0) + Padding;
        }

        return finalY;
    }
}
