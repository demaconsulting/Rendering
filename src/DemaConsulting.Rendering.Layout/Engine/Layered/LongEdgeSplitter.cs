// <copyright file="LongEdgeSplitter.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>
namespace DemaConsulting.Rendering.Layout.Engine.Layered;

/// <summary>
/// Pipeline stage that splits every multi-layer edge into a chain of unit-span sub-edges by
/// inserting one zero-size dummy node per intermediate layer (ELK's <c>LongEdgeSplitter</c>).
/// </summary>
internal sealed class LongEdgeSplitter : ILayoutStage
{
    /// <inheritdoc/>
    public void Apply(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var (augNodes, augEdges) = InsertLongEdgeDummies(graph.N, graph.Nodes, graph.NodeLayers, graph.Acyclic, graph.AugNodes);
        graph.AugNodes = augNodes;
        graph.AugEdges = augEdges;
    }

    /// <summary>
    /// Splits every edge spanning more than one layer into a chain of unit-span sub-edges by
    /// inserting one zero-size dummy node at each intermediate layer, following
    /// ELK's <c>LongEdgeSplitter</c> phase.
    /// </summary>
    /// <remarks>
    /// A hierarchy-crossing dummy (a real node carrying a non-<see langword="null"/>
    /// <see cref="AugNode.Crossing"/>, pre-seeded into <paramref name="priorAug"/> by the recursive
    /// pipeline) is a zero-size terminal hop across a container boundary, never an intermediate long-edge
    /// relay: its <see cref="AugNode.Crossing"/> tag is carried forward unchanged so it is never further
    /// split. For the ordinary flat path <paramref name="priorAug"/> is empty, so every rebuilt node
    /// keeps the default <see langword="null"/> tag and the output is byte-identical.
    /// </remarks>
    private static (List<AugNode> AugNodes, List<AugEdge> AugEdges) InsertLongEdgeDummies(
        int n,
        IReadOnlyList<LayerNode> nodes,
        int[] nodeLayers,
        List<LayerEdge> acyclic,
        IReadOnlyList<AugNode> priorAug)
    {
        var augNodes = new List<AugNode>(n + acyclic.Count);
        for (var i = 0; i < n; i++)
        {
            // Carry forward any hierarchy-crossing tag a caller pre-seeded for this real node, so a
            // crossing dummy stays a tagged terminal hop rather than being treated as an ordinary node.
            var crossing = i < priorAug.Count ? priorAug[i].Crossing : null;
            augNodes.Add(new AugNode(nodes[i].Width, nodes[i].Height, nodeLayers[i], Crossing: crossing));
        }

        var augEdges = new List<AugEdge>(acyclic.Count * 2);
        for (var e = 0; e < acyclic.Count; e++)
        {
            var edge = acyclic[e];
            var span = nodeLayers[edge.Target] - nodeLayers[edge.Source];

            if (span <= 0)
            {
                continue;
            }

            if (span == 1)
            {
                augEdges.Add(new AugEdge(edge.Source, edge.Target, e));
            }
            else
            {
                // Chain: src → d1 → d2 → … → tgt with one dummy per intermediate layer.
                var prev = edge.Source;
                for (var l = nodeLayers[edge.Source] + 1; l < nodeLayers[edge.Target]; l++)
                {
                    var dIdx = augNodes.Count;
                    augNodes.Add(new AugNode(0.0, 0.0, l, IsDummy: true));
                    augEdges.Add(new AugEdge(prev, dIdx, e));
                    prev = dIdx;
                }

                augEdges.Add(new AugEdge(prev, edge.Target, e));
            }
        }

        return (augNodes, augEdges);
    }
}
