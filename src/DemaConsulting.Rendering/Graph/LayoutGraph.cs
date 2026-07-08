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
/// <remarks>
///     <para>
///     A <see cref="LayoutGraph"/> is a <em>container</em> scope. The instance created directly by a
///     caller is the top-level (root) container, mirroring the Eclipse Layout Kernel (ELK) root
///     <c>ElkNode</c>. The same container type is reused at every level of nesting: a
///     <see cref="LayoutGraphNode"/> becomes a compound/container node by populating its
///     <see cref="LayoutGraphNode.Children"/> graph, so hierarchy is expressed by recursion rather
///     than by a distinct type.
///     </para>
///     <para>
///     Identifiers are unique <em>per container</em>. Each graph enforces its own node- and
///     edge-identifier uniqueness, so an identifier may be reused freely in different scopes but not
///     twice within one scope.
///     </para>
///     <para>
///     <strong>Contained-edge / lowest-common-ancestor (LCA) convention.</strong> An edge belongs in
///     the container at or above the lowest common ancestor of its two endpoints. An edge whose
///     endpoints share this graph as their nearest common container is added here; a
///     <em>cross-container</em> edge — one connecting nodes that live in different descendant
///     containers — is likewise added to an ancestor container (this root, or the LCA) while its
///     <see cref="LayoutGraphEdge.Source"/> and <see cref="LayoutGraphEdge.Target"/> reference the
///     descendant nodes directly. No separate cross-container edge type is required, and no
///     membership validation forbids these references; a hierarchical layout engine resolves the
///     routing in the owning container's coordinate space.
///     </para>
/// </remarks>
public sealed class LayoutGraph : PropertyHolder
{
    private readonly List<LayoutGraphNode> _nodes = [];
    private readonly List<LayoutGraphEdge> _edges = [];
    private readonly ReadOnlyCollection<LayoutGraphNode> _nodesView;
    private readonly ReadOnlyCollection<LayoutGraphEdge> _edgesView;
    private readonly HashSet<string> _nodeIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _edgeIds = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new, empty <see cref="LayoutGraph"/> container scope.
    /// </summary>
    public LayoutGraph()
    {
        _nodesView = _nodes.AsReadOnly();
        _edgesView = _edges.AsReadOnly();
    }

    /// <summary>Gets the nodes to be placed, in insertion order. The returned view is genuinely
    /// read-only (a <see cref="ReadOnlyCollection{T}"/>); add nodes through <see cref="AddNode"/>.</summary>
    public IReadOnlyList<LayoutGraphNode> Nodes => _nodesView;

    /// <summary>Gets the directed edges connecting the nodes. The returned view is genuinely read-only
    /// (a <see cref="ReadOnlyCollection{T}"/>); add edges through <see cref="AddEdge"/>.</summary>
    public IReadOnlyList<LayoutGraphEdge> Edges => _edgesView;

    /// <summary>
    /// Creates a node, adds it to <see cref="Nodes"/>, and returns it for further configuration.
    /// </summary>
    /// <param name="id">Identifier unique within this graph.</param>
    /// <param name="width">Width of the node's bounding box in logical pixels.</param>
    /// <param name="height">Height of the node's bounding box in logical pixels.</param>
    /// <returns>The newly-created node.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="id"/> is empty, or when a node with the same <paramref name="id"/>
    /// already exists in this graph.
    /// </exception>
    public LayoutGraphNode AddNode(string id, double width, double height)
    {
        var node = new LayoutGraphNode(id, width, height);
        if (!_nodeIds.Add(id))
        {
            throw new ArgumentException($"A node with id '{id}' already exists in this graph.", nameof(id));
        }

        _nodes.Add(node);
        return node;
    }

    /// <summary>
    /// Creates an edge between two nodes, adds it to <see cref="Edges"/>, and returns it.
    /// </summary>
    /// <remarks>
    ///     The endpoints need not be direct members of this graph: to express a cross-container edge,
    ///     add it to the container at or above the lowest common ancestor of its endpoints (per the
    ///     type-level contained-edge/LCA convention) and pass descendant nodes as
    ///     <paramref name="source"/> and/or <paramref name="target"/>.
    /// </remarks>
    /// <param name="id">Identifier unique within this graph.</param>
    /// <param name="source">The node or port the edge originates from.</param>
    /// <param name="target">The node or port the edge terminates at.</param>
    /// <returns>The newly-created edge.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="id"/>, <paramref name="source"/>, or <paramref name="target"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="id"/> is empty, or when an edge with the same <paramref name="id"/>
    /// already exists in this graph.
    /// </exception>
    public LayoutGraphEdge AddEdge(string id, ILayoutConnectable source, ILayoutConnectable target)
    {
        var edge = new LayoutGraphEdge(id, source, target);
        if (!_edgeIds.Add(id))
        {
            throw new ArgumentException($"An edge with id '{id}' already exists in this graph.", nameof(id));
        }

        _edges.Add(edge);
        return edge;
    }
}
