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
                    var reserve = TitleReserveFor(graph.Direction, nodes[ni]);
                    DistributePorts(sorted, augY[ni] + reserve, nodes[ni].Height - reserve, augPortYSrc);
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
                var node = nodes[ni];
                if (ShapeAnchorSupport.IsPlainRectangle(node))
                {
                    var reserve = TitleReserveFor(graph.Direction, node);
                    DistributePorts(sorted, augY[ni] + reserve, node.Height - reserve, augPortYTgt);
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
    /// Resolves the title band, in logical pixels, to exclude from left/right-face port placement for
    /// <paramref name="node"/>: <see cref="LayerNode.TitleReserveTop"/> when the requested flow
    /// direction leaves the abstract cross-axis band correlated with the box's real top edge, or 0
    /// otherwise (see <see cref="LayerNode.TitleReserveTop"/> remarks). Capped at the node's own
    /// height so a node too small to hold the reserve degrades to the plain full-span band instead of
    /// producing a negative usable span.
    /// </summary>
    /// <param name="direction">The layout's requested flow direction.</param>
    /// <param name="node">The real node whose title reserve is being resolved.</param>
    /// <returns>The title band height to exclude, in logical pixels.</returns>
    private static double TitleReserveFor(LayoutDirection direction, LayerNode node) =>
        direction is LayoutDirection.Right or LayoutDirection.Left
            ? Math.Min(node.TitleReserveTop, node.Height)
            : 0.0;

    /// <summary>
    /// Distributes port Y positions along a node face by dividing it into <c>count</c> equal-width
    /// areas and centering each port within its own area (ELK-style "equal spacing" convention).
    /// </summary>
    /// <remarks>
    /// For <c>count</c> ports this places port <c>k</c> at the centre of the <c>k</c>-th of
    /// <c>count</c> equal slices of the face: <c>(k + 0.5) / count</c> of the way along it. This
    /// gives every port (including the first and last) a margin from its neighbors and from the
    /// face's own edges that is proportional to the slice width, rather than a fixed absolute
    /// clearance — e.g. for two ports this naturally produces the 0.25 / 0.5 / 0.25 margin/gap/margin
    /// pattern (each port a quarter of the way in from its nearest edge, half the face apart from each
    /// other), so a label centred on either port has room on both sides before the box's own edge.
    /// The single-port case (<c>count == 1</c>) is simply the one-slice special case of the same
    /// formula (centred on the whole face) and needs no separate branch. Because every computed
    /// position is strictly between <paramref name="nodeTop"/> and
    /// <c>nodeTop + nodeHeight</c> by construction, no clamp is needed even for a face far shorter
    /// than any fixed clearance would tolerate (the small-face regression this stage must not throw
    /// for).
    /// </remarks>
    private static void DistributePorts(
        IReadOnlyList<int> sortedEdgeIndices,
        double nodeTop,
        double nodeHeight,
        double[] portY)
    {
        var count = sortedEdgeIndices.Count;
        for (var k = 0; k < count; k++)
        {
            portY[sortedEdgeIndices[k]] = nodeTop + ((k + 0.5) * nodeHeight / count);
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
