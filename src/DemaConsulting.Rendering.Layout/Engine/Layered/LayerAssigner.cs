// <copyright file="LayerAssigner.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>
namespace DemaConsulting.Rendering.Layout.Engine.Layered;

/// <summary>
/// Pipeline stage that assigns each node to a layer using longest-path layering.
/// </summary>
internal sealed class LayerAssigner : ILayoutStage
{
    /// <inheritdoc/>
    public void Apply(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        graph.NodeLayers = AssignLayers(graph.N, graph.Acyclic);
    }

    /// <summary>
    /// Assigns level-relative layers to every nesting level of an assembled merge region: each level's
    /// interior nodes are laid out by that level's own longest-path computation, starting fresh at
    /// layer 0, and never appended to the outer scope's global layer sequence.
    /// </summary>
    /// <remarks>
    ///     A merge-region container node is an ordinary node in its <em>parent</em> level's own
    ///     longest-path sequence; its child level's layers are computed independently over the child
    ///     level's own graph, so a container's interior stays spatially nested inside the container's own
    ///     box rather than progressing sequentially with the outer scope. Each level's
    ///     <see cref="LayeredGraph"/> must already have had <see cref="CycleBreaker"/> run (its
    ///     <see cref="LayeredGraph.Acyclic"/> populated). The flat <see cref="Apply"/> entry point is
    ///     unchanged and still governs every single-level (non-hierarchical) graph.
    /// </remarks>
    /// <param name="level">The nesting level whose subtree is assigned layers.</param>
    /// <param name="levels">The lookup from each level to its assembled layered graph.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="level"/> or <paramref name="levels"/> is <see langword="null"/>.</exception>
    internal void AssignLayersRecursive(
        MergeRegionLevel level,
        IReadOnlyDictionary<MergeRegionLevel, LevelLayeredGraph> levels)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(levels);

        var graph = levels[level].Graph;
        graph.NodeLayers = AssignLayers(graph.N, graph.Acyclic);

        foreach (var (_, _, child) in level.Children)
        {
            AssignLayersRecursive(child, levels);
        }
    }

    /// <summary>
    /// Assigns each node to a layer equal to the length of its longest incoming path
    /// (sources at layer 0, sinks at the maximum layer).
    /// </summary>
    private static int[] AssignLayers(int n, List<LayerEdge> edges)
    {
        var outgoing = new List<int>[n];
        var inDegree = new int[n];
        for (var i = 0; i < n; i++)
        {
            outgoing[i] = [];
        }

        foreach (var e in edges)
        {
            outgoing[e.Source].Add(e.Target);
            inDegree[e.Target]++;
        }

        var layer = new int[n];
        var queue = new Queue<int>();
        for (var i = 0; i < n; i++)
        {
            if (inDegree[i] == 0)
            {
                queue.Enqueue(i);
            }
        }

        var remaining = (int[])inDegree.Clone();
        while (queue.Count > 0)
        {
            var u = queue.Dequeue();
            foreach (var v in outgoing[u])
            {
                layer[v] = Math.Max(layer[v], layer[u] + 1);
                if (--remaining[v] == 0)
                {
                    queue.Enqueue(v);
                }
            }
        }

        return layer;
    }
}
