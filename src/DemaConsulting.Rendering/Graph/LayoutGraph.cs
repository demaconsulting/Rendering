// <copyright file="LayoutGraph.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Collections.ObjectModel;

namespace DemaConsulting.Rendering;

/// <summary>
/// The unplaced input to a layout algorithm: a set of sized <see cref="LayoutGraphNode"/> boxes and
/// directed <see cref="LayoutGraphEdge"/> connections. A layout algorithm consumes a
/// <see cref="LayoutGraph"/> plus <see cref="LayoutOptions"/> and produces a placed
/// <see cref="LayoutTree"/>. The graph itself is an <see cref="IPropertyHolder"/>, so graph-wide
/// options may be attached directly to it.
/// </summary>
public sealed class LayoutGraph : PropertyHolder
{
    private readonly Collection<LayoutGraphNode> _nodes = [];
    private readonly Collection<LayoutGraphEdge> _edges = [];

    /// <summary>Gets the nodes to be placed, in insertion order.</summary>
    public Collection<LayoutGraphNode> Nodes => _nodes;

    /// <summary>Gets the directed edges connecting the nodes.</summary>
    public Collection<LayoutGraphEdge> Edges => _edges;

    /// <summary>
    /// Creates a node, adds it to <see cref="Nodes"/>, and returns it for further configuration.
    /// </summary>
    /// <param name="id">Identifier unique within this graph.</param>
    /// <param name="width">Width of the node's bounding box in logical pixels.</param>
    /// <param name="height">Height of the node's bounding box in logical pixels.</param>
    /// <returns>The newly-created node.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is empty.</exception>
    public LayoutGraphNode AddNode(string id, double width, double height)
    {
        var node = new LayoutGraphNode(id, width, height);
        _nodes.Add(node);
        return node;
    }

    /// <summary>
    /// Creates an edge between two nodes, adds it to <see cref="Edges"/>, and returns it.
    /// </summary>
    /// <param name="id">Identifier unique within this graph.</param>
    /// <param name="source">The node the edge originates from.</param>
    /// <param name="target">The node the edge terminates at.</param>
    /// <returns>The newly-created edge.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="id"/>, <paramref name="source"/>, or <paramref name="target"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is empty.</exception>
    public LayoutGraphEdge AddEdge(string id, LayoutGraphNode source, LayoutGraphNode target)
    {
        var edge = new LayoutGraphEdge(id, source, target);
        _edges.Add(edge);
        return edge;
    }
}
