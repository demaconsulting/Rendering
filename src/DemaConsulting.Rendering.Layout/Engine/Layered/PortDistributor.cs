// <copyright file="PortDistributor.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>
using static DemaConsulting.Rendering.Layout.Engine.Layered.LayeredLayoutMetrics;

namespace DemaConsulting.Rendering.Layout.Engine.Layered;

/// <summary>
/// Pipeline stage that distributes connector ports evenly along each box face and records the
/// source-side and target-side port Y coordinate for every augmented sub-edge.
/// </summary>
internal sealed class PortDistributor : ILayoutStage
{
    /// <inheritdoc/>
    public void Apply(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var n = graph.N;
        var nodes = graph.Nodes;
        var augNodes = graph.AugNodes;
        var augEdges = graph.AugEdges;
        var augY = graph.AugY;
        var numAugEdges = augEdges.Count;

        // Port Y values: augPortYSrc[i] = source (right face) Y; augPortYTgt[i] = target (left face) Y.
        var augPortYSrc = new double[numAugEdges];
        var augPortYTgt = new double[numAugEdges];

        // Distribute outgoing (source-side) ports on each real node's right face.
        var outByNode = new Dictionary<int, List<int>>();
        for (var ei = 0; ei < numAugEdges; ei++)
        {
            var src = augEdges[ei].Source;
            if (!outByNode.TryGetValue(src, out var list))
            {
                list = [];
                outByNode[src] = list;
            }

            list.Add(ei);
        }

        foreach (var (ni, edgeList) in outByNode)
        {
            if (augNodes[ni].IsDummy)
            {
                // Dummies pass the wire straight through at their own Y.
                foreach (var ei in edgeList)
                {
                    augPortYSrc[ei] = augY[ni];
                }
            }
            else
            {
                // Sort by target Y center, then edge index for stability.
                var sorted = edgeList
                    .OrderBy(ei => augY[augEdges[ei].Target] + (augNodes[augEdges[ei].Target].Height / 2.0))
                    .ThenBy(ei => ei)
                    .ToList();
                if (ShapeAnchorSupport.IsPlainRectangle(nodes[ni]))
                {
                    DistributePorts(sorted, augY[ni], nodes[ni].Height, augPortYSrc);
                }
                else
                {
                    DistributeShapedPorts(sorted, graph.Direction, isSource: true, augY[ni], nodes[ni], augPortYSrc);
                }
            }
        }

        // Distribute incoming (target-side) ports on each real node's left face.
        var inByNode = new Dictionary<int, List<int>>();
        for (var ei = 0; ei < numAugEdges; ei++)
        {
            var tgt = augEdges[ei].Target;
            if (!inByNode.TryGetValue(tgt, out var list))
            {
                list = [];
                inByNode[tgt] = list;
            }

            list.Add(ei);
        }

        foreach (var (ni, edgeList) in inByNode)
        {
            if (augNodes[ni].IsDummy)
            {
                foreach (var ei in edgeList)
                {
                    augPortYTgt[ei] = augY[ni];
                }
            }
            else
            {
                var sorted = edgeList
                    .OrderBy(ei => augY[augEdges[ei].Source] + (augNodes[augEdges[ei].Source].Height / 2.0))
                    .ThenBy(ei => ei)
                    .ToList();
                var node = nodes[ni < n ? ni : 0];
                if (ShapeAnchorSupport.IsPlainRectangle(node))
                {
                    DistributePorts(sorted, augY[ni], node.Height, augPortYTgt);
                }
                else
                {
                    DistributeShapedPorts(sorted, graph.Direction, isSource: false, augY[ni], node, augPortYTgt);
                }
            }
        }

        graph.AugPortYSrc = augPortYSrc;
        graph.AugPortYTgt = augPortYTgt;
    }

    /// <summary>
    /// Evenly distributes port Y positions along a node face, with
    /// <see cref="ConnectorClearance"/> inset from the top and bottom edges.
    /// </summary>
    /// <remarks>
    /// The requested inset is capped at half the node's cross-extent so a node too small to hold the
    /// full <see cref="ConnectorClearance"/> on both faces degrades gracefully (ports collapse toward
    /// the centre) rather than producing an inverted clamp range. This mirrors ELK, which distributes
    /// ports within the available span and tolerates overlap for fixed-size nodes instead of failing
    /// the layout. Nodes at least <c>2 &#215; ConnectorClearance</c> tall are unaffected (the cap is a
    /// no-op), so realistic output is preserved exactly.
    /// </remarks>
    private static void DistributePorts(
        IReadOnlyList<int> sortedEdgeIndices,
        double nodeTop,
        double nodeHeight,
        double[] portY)
    {
        // Cap the inset so the [top + inset, top + height - inset] band never inverts for small nodes.
        var inset = Math.Min(ConnectorClearance, nodeHeight / 2.0);
        var count = sortedEdgeIndices.Count;
        for (var k = 0; k < count; k++)
        {
            double y;
            if (count == 1)
            {
                y = nodeTop + (nodeHeight / 2.0);
            }
            else
            {
                var usable = nodeHeight - (2.0 * inset);
                y = nodeTop + inset + (k * usable / (count - 1));
            }

            portY[sortedEdgeIndices[k]] = Math.Clamp(
                y,
                nodeTop + inset,
                nodeTop + nodeHeight - inset);
        }
    }

    /// <summary>
    /// Distributes port Y positions along a non-<see cref="BoxShape.Rectangle"/> real node's face,
    /// restricting the band to the shape's usable connectable extents on the resolved real face
    /// (proportionally across multiple disjoint extents, when the shape excludes a middle portion of
    /// the face) instead of the plain full-span band <see cref="DistributePorts"/> uses.
    /// </summary>
    /// <remarks>
    /// Reuses <see cref="ConnectorRouter"/>'s (internal) shape-geometry resolution so a shaped node
    /// routed through the layered pipeline gets the same connectable-extent restriction that
    /// <see cref="ConnectorRouter"/>-routed edges already apply; see
    /// <c>docs/design/rendering-layout/engine/layered-pipeline.md</c>. Per the layered pipeline's
    /// abstract axis convention (verified in the shape-aware-anchors planning report), the abstract
    /// cross-axis coordinate is never reflected, so the local face coordinate returned by
    /// <see cref="ConnectorRouter.CoordinateAtDistance"/> maps directly onto the abstract Y axis by
    /// adding <paramref name="nodeTop"/>, with no sign flip in any of the four flow directions.
    /// </remarks>
    private static void DistributeShapedPorts(
        IReadOnlyList<int> sortedEdgeIndices,
        LayoutDirection direction,
        bool isSource,
        double nodeTop,
        LayerNode node,
        double[] portY)
    {
        var side = ShapeAnchorSupport.ResolveRealFace(direction, isSource);
        var geometry = ConnectorRouter.ResolveShapeGeometry(ShapeAnchorSupport.BuildAdapterBox(node));
        var extents = geometry.GetConnectableExtents(side);
        var usable = ConnectorRouter.BuildUsableExtents(extents, ConnectorClearance);
        var total = ConnectorRouter.TotalExtentLength(usable);

        if (total <= 1e-9)
        {
            // The shape excludes the entire face; fall back to the plain full-span band rather than
            // producing an invalid (zero-length) port distribution. The abstract Height already equals
            // the tangential face length for whichever real face was resolved (see the verified
            // abstract-axis invariant on ShapeAnchorSupport.ResolveRealFace).
            DistributePorts(sortedEdgeIndices, nodeTop, node.Height, portY);
            return;
        }

        var count = sortedEdgeIndices.Count;
        for (var k = 0; k < count; k++)
        {
            var distance = count == 1 ? total / 2.0 : k * total / (count - 1);
            var local = ConnectorRouter.CoordinateAtDistance(usable, distance);
            portY[sortedEdgeIndices[k]] = nodeTop + local;
        }
    }
}
