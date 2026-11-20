// <copyright file="RoutingScenes.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Graph builders for the large-scope layout tests and the opt-in routing benchmark: a synthetic package of
///     folder containers joined by seeded random cross-container connectors (the shape of the OMG Annex A
///     "General" views), and a package of sub-packages (the shape of a nested package model). They use the
///     public layout API only, so the tests and the benchmark (which compiles this file) lay out exactly the
///     same graphs, and the layout of a scene can be fingerprinted from its public tree.
/// </summary>
internal static class RoutingScenes
{
    /// <summary>The number of folder containers of the synthetic package.</summary>
    public const int Containers = 6;

    /// <summary>The number of leaf boxes inside each folder of the synthetic package.</summary>
    public const int LeavesPerContainer = 5;

    /// <summary>The number of connector lines the folders of the synthetic package draw on their own (a spanning tree in each).</summary>
    public const int SyntheticInteriorLines = Containers * (LeavesPerContainer - 1);

    /// <summary>
    ///     Builds the synthetic package graph: <see cref="Containers"/> folders of
    ///     <see cref="LeavesPerContainer"/> leaves each (a spanning tree of interior connectors inside every
    ///     folder) plus <paramref name="crossEdges"/> seeded random connectors between leaves of different
    ///     folders, declared in the package scope.
    /// </summary>
    /// <param name="crossEdges">The number of cross-container connectors.</param>
    /// <returns>The graph to lay out.</returns>
    public static LayoutGraph SyntheticPackage(int crossEdges)
    {
        // Arrange the root graph and the enclosing package folder
        var rng = new Random(1);
        var graph = Root();
        var package = AddFolder(graph, "folder:Pkg", "Pkg");

        // Add the folders with their leaves and the interior spanning trees
        var leaves = new List<List<LayoutGraphNode>>();
        for (var c = 0; c < Containers; c++)
        {
            var folder = AddFolder(package.Children, $"folder:Pkg::F{c}", $"F{c}");
            var list = new List<LayoutGraphNode>();
            for (var l = 0; l < LeavesPerContainer; l++)
            {
                var leaf = folder.Children.AddNode($"F{c}::L{l}", 120, 60);
                leaf.Label = $"L{l}";
                list.Add(leaf);
                if (l > 0)
                {
                    folder.Children.AddEdge($"in{c}_{l}", list[(l - 1) / 2], leaf);
                }
            }

            leaves.Add(list);
        }

        // Add the distinct cross-container connectors in the package scope
        var seen = new HashSet<(int, int, int, int)>();
        var added = 0;
        while (added < crossEdges)
        {
            var a = rng.Next(Containers);
            var b = rng.Next(Containers);
            if (a == b)
            {
                continue;
            }

            var i = rng.Next(LeavesPerContainer);
            var j = rng.Next(LeavesPerContainer);
            if (!seen.Add((a, i, b, j)))
            {
                continue;
            }

            package.Children.AddEdge($"x{added}", leaves[a][i], leaves[b][j]);
            added++;
        }

        return graph;
    }

    /// <summary>Returns the number of connector lines the layout of <see cref="SyntheticPackage"/> draws.</summary>
    /// <param name="crossEdges">The number of cross-container connectors.</param>
    /// <returns>The number of lines.</returns>
    public static int SyntheticLineCount(int crossEdges) => crossEdges + SyntheticInteriorLines;

    /// <summary>
    ///     Builds a package of <paramref name="subPackages"/> sub-packages, each holding
    ///     <paramref name="foldersPerSub"/> folders of <paramref name="leavesPerFolder"/> leaves (a spanning
    ///     tree inside each folder) joined by <paramref name="innerEdges"/> connectors declared in the
    ///     sub-package scope, plus <paramref name="packageEdges"/> seeded random connectors between leaves of
    ///     different sub-packages, declared in the package scope. With enough package connectors the package
    ///     scope alone is too large for the full routing effort while every sub-package scope is small.
    /// </summary>
    /// <param name="subPackages">The number of sub-packages.</param>
    /// <param name="foldersPerSub">The number of folders of each sub-package.</param>
    /// <param name="leavesPerFolder">The number of leaves of each folder.</param>
    /// <param name="innerEdges">The number of connectors between the folders of one sub-package.</param>
    /// <param name="packageEdges">The number of connectors in the package scope.</param>
    /// <returns>The graph to lay out.</returns>
    public static LayoutGraph NestedPackage(int subPackages, int foldersPerSub, int leavesPerFolder, int innerEdges, int packageEdges)
    {
        var rng = new Random(7);
        var graph = Root();
        var package = AddFolder(graph, "folder:Top", "Top");

        // The sub-packages, their folders, the leaves, and the connectors inside each sub-package
        var leaves = new List<List<LayoutGraphNode>>();
        for (var s = 0; s < subPackages; s++)
        {
            var sub = AddFolder(package.Children, $"folder:Top::S{s}", $"S{s}");
            var subLeaves = new List<LayoutGraphNode>();
            var folderLeaves = new List<List<LayoutGraphNode>>();
            for (var f = 0; f < foldersPerSub; f++)
            {
                var folder = AddFolder(sub.Children, $"folder:Top::S{s}::F{f}", $"F{f}");
                var list = new List<LayoutGraphNode>();
                for (var l = 0; l < leavesPerFolder; l++)
                {
                    var leaf = folder.Children.AddNode($"S{s}::F{f}::L{l}", 120, 60);
                    leaf.Label = $"L{l}";
                    list.Add(leaf);
                    if (l > 0)
                    {
                        folder.Children.AddEdge($"in{s}_{f}_{l}", list[(l - 1) / 2], leaf);
                    }
                }

                folderLeaves.Add(list);
                subLeaves.AddRange(list);
            }

            // Connectors between the folders of the sub-package, declared in the sub-package scope
            for (var e = 0; e < innerEdges; e++)
            {
                var a = e % foldersPerSub;
                var b = (a + 1 + (e / foldersPerSub)) % foldersPerSub;
                sub.Children.AddEdge($"sub{s}_{e}", folderLeaves[a][e % leavesPerFolder], folderLeaves[b][(e + 1) % leavesPerFolder]);
            }

            leaves.Add(subLeaves);
        }

        // The connectors between sub-packages, declared in the package scope
        var seen = new HashSet<(int, int, int, int)>();
        var added = 0;
        while (added < packageEdges)
        {
            var a = rng.Next(subPackages);
            var b = rng.Next(subPackages);
            if (a == b)
            {
                continue;
            }

            var i = rng.Next(leaves[a].Count);
            var j = rng.Next(leaves[b].Count);
            if (!seen.Add((a, i, b, j)))
            {
                continue;
            }

            package.Children.AddEdge($"top{added}", leaves[a][i], leaves[b][j]);
            added++;
        }

        return graph;
    }

    /// <summary>Returns the number of connector lines the layout of <see cref="NestedPackage"/> draws.</summary>
    /// <param name="subPackages">The number of sub-packages.</param>
    /// <param name="foldersPerSub">The number of folders of each sub-package.</param>
    /// <param name="leavesPerFolder">The number of leaves of each folder.</param>
    /// <param name="innerEdges">The number of connectors between the folders of one sub-package.</param>
    /// <param name="packageEdges">The number of connectors in the package scope.</param>
    /// <returns>The number of lines.</returns>
    public static int NestedLineCount(int subPackages, int foldersPerSub, int leavesPerFolder, int innerEdges, int packageEdges) =>
        packageEdges + (subPackages * (innerEdges + (foldersPerSub * (leavesPerFolder - 1))));

    /// <summary>
    ///     Returns a fingerprint of every box and connector coordinate of a laid-out tree, rounded to three
    ///     decimals so it is independent of floating-point printing differences between runtimes.
    /// </summary>
    /// <param name="tree">The laid-out tree.</param>
    /// <returns>A twelve-character hexadecimal digest.</returns>
    public static string Fingerprint(LayoutTree tree)
    {
        var text = new StringBuilder();
        Append(text, tree.Nodes);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
        var hex = new StringBuilder();
        for (var i = 0; i < 6; i++)
        {
            hex.Append(hash[i].ToString("X2", CultureInfo.InvariantCulture));
        }

        return hex.ToString();
    }

    /// <summary>Counts the connector lines in <paramref name="nodes"/> and their descendants.</summary>
    /// <param name="nodes">The nodes to count in.</param>
    /// <returns>The number of lines.</returns>
    public static int CountLines(IReadOnlyList<LayoutNode> nodes)
    {
        var count = 0;
        foreach (var node in nodes)
        {
            if (node is LayoutLine)
            {
                count++;
            }
            else if (node is LayoutBox box)
            {
                count += CountLines(box.Children);
            }
        }

        return count;
    }

    /// <summary>Creates the root graph every scene shares: automatic layout, downward flow, parallel edges kept apart.</summary>
    /// <returns>The empty root graph.</returns>
    private static LayoutGraph Root()
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.MergeParallelEdges, false);
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
        graph.Set(CoreOptions.Algorithm, AutoLayoutAlgorithm.AlgorithmId);
        return graph;
    }

    /// <summary>Adds a folder container laid out by the automatic algorithm to <paramref name="scope"/>.</summary>
    /// <param name="scope">The scope to add to.</param>
    /// <param name="id">The folder's identifier.</param>
    /// <param name="label">The folder's label.</param>
    /// <returns>The folder node.</returns>
    private static LayoutGraphNode AddFolder(LayoutGraph scope, string id, string label)
    {
        var folder = scope.AddNode(id, 0, 0);
        folder.Label = label;
        folder.Shape = BoxShape.Folder;
        folder.Set(CoreOptions.Algorithm, AutoLayoutAlgorithm.AlgorithmId);
        return folder;
    }
    /// <summary>Appends the coordinates of <paramref name="nodes"/> and their descendants to <paramref name="text"/>.</summary>
    /// <param name="text">The builder to append to.</param>
    /// <param name="nodes">The nodes to describe.</param>
    private static void Append(StringBuilder text, IReadOnlyList<LayoutNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is LayoutBox box)
            {
                text.Append(string.Format(CultureInfo.InvariantCulture, "B{0:F3},{1:F3},{2:F3},{3:F3};", box.X, box.Y, box.Width, box.Height));
                Append(text, box.Children);
            }
            else if (node is LayoutLine line)
            {
                text.Append('L');
                foreach (var waypoint in line.Waypoints)
                {
                    text.Append(string.Format(CultureInfo.InvariantCulture, "{0:F3},{1:F3} ", waypoint.X, waypoint.Y));
                }

                text.Append(';');
            }
        }
    }
}
