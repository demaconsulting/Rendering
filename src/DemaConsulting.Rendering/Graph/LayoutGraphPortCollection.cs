// <copyright file="LayoutGraphPortCollection.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Collections.ObjectModel;

namespace DemaConsulting.Rendering;

/// <summary>
/// The lazily-created collection of <see cref="LayoutGraphPort"/>s owned by a single
/// <see cref="LayoutGraphNode"/>, exposed through <see cref="LayoutGraphNode.Ports"/>.
/// </summary>
/// <remarks>
/// Port identifiers are unique <em>per owning node</em>, mirroring the node/edge identifier
/// uniqueness enforced per container by <see cref="LayoutGraph"/> — the same identifier may be
/// reused on a different node's ports but not twice on the same node.
/// </remarks>
public sealed class LayoutGraphPortCollection
{
    private readonly List<LayoutGraphPort> _ports = [];
    private readonly ReadOnlyCollection<LayoutGraphPort> _portsView;
    private readonly HashSet<string> _portIds = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new, empty <see cref="LayoutGraphPortCollection"/>.
    /// </summary>
    public LayoutGraphPortCollection()
    {
        _portsView = _ports.AsReadOnly();
    }

    /// <summary>Gets the ports owned by the node, in insertion order. The returned view is genuinely
    /// read-only (a <see cref="ReadOnlyCollection{T}"/>); add ports through <see cref="AddPort"/>.</summary>
    public IReadOnlyList<LayoutGraphPort> Ports => _portsView;

    /// <summary>
    /// Creates a port, adds it to <see cref="Ports"/>, and returns it for further configuration.
    /// </summary>
    /// <param name="id">Identifier unique within this node's port collection.</param>
    /// <returns>The newly-created port.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="id"/> is empty, or when a port with the same <paramref name="id"/>
    /// already exists on this node.
    /// </exception>
    public LayoutGraphPort AddPort(string id)
    {
        var port = new LayoutGraphPort(id);
        if (!_portIds.Add(id))
        {
            throw new ArgumentException($"A port with id '{id}' already exists on this node.", nameof(id));
        }

        _ports.Add(port);
        return port;
    }
}
