// <copyright file="CycleBreaker.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>
namespace DemaConsulting.Rendering.Layout.Engine.Layered;

/// <summary>
/// Pipeline stage that makes the input graph acyclic by reversing cycle-causing back edges,
/// following ELK's cycle-breaking phase.
/// </summary>
internal sealed class CycleBreaker : ILayoutStage
{
    /// <inheritdoc/>
    public void Apply(LayeredGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var (acyclic, reversed, originalIndex) = BreakCycles(graph.N, graph.Edges, graph.MergeParallelEdges);
        graph.Acyclic = acyclic;
        graph.AcyclicReversed = reversed;
        graph.AcyclicOriginalIndex = originalIndex;
    }

    /// <summary>
    /// Returns the edge set with cycle-causing back edges reversed, using DFS to classify any
    /// edge to a node still on the recursion stack as a back edge. The second tuple element is a
    /// parallel flag array marking which retained edges were produced by reversing a back edge. The
    /// third tuple element is parallel to the first, giving the 0-based index into
    /// <paramref name="edges"/> that each retained edge originated from.
    /// </summary>
    private static (List<LayerEdge> Acyclic, bool[] Reversed, List<int> OriginalIndex) BreakCycles(
        int n,
        IReadOnlyList<LayerEdge> edges,
        bool mergeParallelEdges)
    {
        var adjacency = new List<int>[n];
        for (var i = 0; i < n; i++)
        {
            adjacency[i] = [];
        }

        foreach (var e in edges.Where(e => e.Source != e.Target))
        {
            adjacency[e.Source].Add(e.Target);
        }

        var visited = new bool[n];
        var onStack = new bool[n];
        var backEdges = new HashSet<(int, int)>();

        void Dfs(int u)
        {
            visited[u] = true;
            onStack[u] = true;
            foreach (var v in adjacency[u])
            {
                if (onStack[v])
                {
                    backEdges.Add((u, v));
                }
                else if (!visited[v])
                {
                    Dfs(v);
                }
            }

            // S4143: standard DFS coloring — onStack[u] is read by recursive calls between the
            // true/false assignments; the analyzer cannot see across the recursion.
#pragma warning disable S4143
            onStack[u] = false;
#pragma warning restore S4143
        }

        for (var i = 0; i < n; i++)
        {
            if (!visited[i])
            {
                Dfs(i);
            }
        }

        var result = new List<LayerEdge>();
        var reversed = new List<bool>();
        var originalIndex = new List<int>();
        var seen = new HashSet<(int, int)>();
        for (var idx = 0; idx < edges.Count; idx++)
        {
            var e = edges[idx];
            if (e.Source == e.Target)
            {
                continue;
            }

            // S4158: backEdges is populated inside the Dfs local function above; the analyzer's
            // symbolic execution cannot track the mutation across the nested call and wrongly treats
            // the set as empty here (same limitation as the S4143 suppression earlier in this method).
#pragma warning disable S4158
            var isBack = backEdges.Contains((e.Source, e.Target));
#pragma warning restore S4158
            var (from, to) = isBack
                ? (e.Target, e.Source)
                : (e.Source, e.Target);

            if (from == to)
            {
                continue;
            }

            if (mergeParallelEdges && !seen.Add((from, to)))
            {
                continue;
            }

            result.Add(new LayerEdge(from, to));
            reversed.Add(isBack);
            originalIndex.Add(idx);
        }

        return (result, [.. reversed], originalIndex);
    }
}
