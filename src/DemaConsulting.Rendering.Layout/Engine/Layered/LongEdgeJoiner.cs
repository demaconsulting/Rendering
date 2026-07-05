// <copyright file="LongEdgeJoiner.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>
namespace DemaConsulting.Rendering.Layout.Engine.Layered;

/// <summary>
/// Pipeline stage that assembles per-original-edge orthogonal waypoints by concatenating the bend
/// points of each sub-edge in source-to-target layer order (ELK's <c>LongEdgeJoiner</c>).
/// </summary>
internal sealed class LongEdgeJoiner : ILayoutStage
{
    /// <inheritdoc/>
    public void Apply(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var augNodes = graph.AugNodes;
        var augEdges = graph.AugEdges;
        var augX = graph.AugX;
        var augY = graph.AugY;
        var nodes = graph.Nodes;
        var direction = graph.Direction;
        var augPortYSrc = graph.AugPortYSrc;
        var augPortYTgt = graph.AugPortYTgt;
        var augBendPoints = graph.AugBendPoints;
        var numAugEdges = augEdges.Count;
        var numOrigEdges = graph.Acyclic.Count;

        // Assemble per-original-edge waypoints from sub-edge bend points (ELK LongEdgeJoiner).
        var subEdgesByOrig = new List<int>[numOrigEdges];
        for (var ei = 0; ei < numOrigEdges; ei++)
        {
            subEdgesByOrig[ei] = [];
        }

        for (var ei = 0; ei < numAugEdges; ei++)
        {
            subEdgesByOrig[augEdges[ei].OrigEdgeIndex].Add(ei);
        }

        // Sub-edges must be in source-to-target layer order for concatenation.
        for (var ei = 0; ei < numOrigEdges; ei++)
        {
            subEdgesByOrig[ei].Sort((a, b) =>
                augNodes[augEdges[a].Source].Layer.CompareTo(augNodes[augEdges[b].Source].Layer));
        }

        var result = new IReadOnlyList<Point2D>[numOrigEdges];
        for (var origIdx = 0; origIdx < numOrigEdges; origIdx++)
        {
            var subEdges = subEdgesByOrig[origIdx];
            if (subEdges.Count == 0)
            {
                result[origIdx] = [];
                continue;
            }

            var firstSubEdge = augEdges[subEdges[0]];
            var lastSubEdge = augEdges[subEdges[^1]];

            var srcNodeIdx = firstSubEdge.Source;
            var tgtNodeIdx = lastSubEdge.Target;

            var srcRight = augX[srcNodeIdx] + augNodes[srcNodeIdx].Width;
            var tgtLeft = augX[tgtNodeIdx];
            var srcPortY = augPortYSrc[subEdges[0]];
            var tgtPortY = augPortYTgt[subEdges[^1]];

            // Project a shaped real endpoint inward to the shape's real outline, matching
            // ConnectorRouter's own shape-geometry rules. Dummy nodes never reach here (long-edge
            // splitting only inserts dummies at intermediate layers, so the first sub-edge's source
            // and the last sub-edge's target are always real), and the plain-Rectangle fast path skips
            // geometry resolution entirely so its arithmetic stays byte-identical.
            if (!ShapeAnchorSupport.IsPlainRectangle(nodes[srcNodeIdx]))
            {
                var srcSide = ShapeAnchorSupport.ResolveRealFace(direction, isSource: true);
                var srcGeometry = ConnectorRouter.ResolveShapeGeometry(ShapeAnchorSupport.BuildAdapterBox(nodes[srcNodeIdx]));
                var srcLocal = srcPortY - augY[srcNodeIdx];
                var srcOffset = Math.Max(0.0, srcGeometry.ProjectToSurface(srcSide, srcLocal));
                srcRight -= srcOffset;
            }

            if (!ShapeAnchorSupport.IsPlainRectangle(nodes[tgtNodeIdx]))
            {
                var tgtSide = ShapeAnchorSupport.ResolveRealFace(direction, isSource: false);
                var tgtGeometry = ConnectorRouter.ResolveShapeGeometry(ShapeAnchorSupport.BuildAdapterBox(nodes[tgtNodeIdx]));
                var tgtLocal = tgtPortY - augY[tgtNodeIdx];
                var tgtOffset = Math.Max(0.0, tgtGeometry.ProjectToSurface(tgtSide, tgtLocal));
                tgtLeft += tgtOffset;
            }

            var wps = new List<Point2D>
            {
                new(srcRight, srcPortY),
            };

            foreach (var subEdgeIdx in subEdges)
            {
                wps.AddRange(augBendPoints[subEdgeIdx]);
            }

            wps.Add(new Point2D(tgtLeft, tgtPortY));
            result[origIdx] = wps;
        }

        graph.Waypoints = result;
    }
}
