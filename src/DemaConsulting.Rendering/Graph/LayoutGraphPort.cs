// <copyright file="LayoutGraphPort.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// A named connection point owned by a <see cref="LayoutGraphNode"/>, allowing an edge to attach to
/// a specific, labelled location on the node's boundary instead of the node as a whole. Modelled
/// after the Eclipse Layout Kernel (ELK) <c>ElkPort</c>.
/// </summary>
/// <remarks>
///     <para>
///     A port has no independent size or position: it always anchors to whichever placed edge of
///     its owning node the layout algorithm resolves for its connected edge(s). Add a port through
///     the owning node's <see cref="LayoutGraphNode.Ports"/> collection, then reference it as the
///     <see cref="LayoutGraphEdge.Source"/> or <see cref="LayoutGraphEdge.Target"/> of an edge added
///     via <see cref="LayoutGraph.AddEdge"/>.
///     </para>
///     <para>
///     This first-cut (Phase 1, flat-graph) model deliberately has no <c>Side</c> property: no
///     caller control over which edge of the node a port is placed on exists yet, and no
///     hierarchy-aware placement (internal vs. external edges through a container) is implemented —
///     <see cref="ExternalLabel"/> is the only label rendered in this phase; <see cref="InternalLabel"/>
///     is carried for forward compatibility but is not read anywhere yet. The type does not forbid
///     more than one internal or external edge per port, but the first-cut scope only exercises at
///     most one of each.
///     </para>
/// </remarks>
public sealed class LayoutGraphPort : PropertyHolder, ILayoutConnectable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutGraphPort"/> class.
    /// </summary>
    /// <param name="id">Identifier unique within the owning node's port collection.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is empty.</exception>
    public LayoutGraphPort(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        Id = id;
    }

    /// <summary>Gets the identifier, unique within the owning node's port collection.</summary>
    public string Id { get; }

    /// <summary>
    /// Gets or sets the text label rendered beside this port for an edge that crosses into or out of
    /// the node from outside its container (an <em>external</em> edge). <see langword="null"/> when
    /// no label should be shown. This is the only label rendered by the flat-graph (Phase 1) layout
    /// algorithm, since no ancestor scope exists yet to make an edge "internal" by comparison.
    /// </summary>
    public string? ExternalLabel { get; set; }

    /// <summary>
    /// Gets or sets the text label associated with this port for an edge contained entirely within
    /// the node's own child scope (an <em>internal</em> edge). <see langword="null"/> when no label
    /// should be shown. Reserved for a future hierarchy-aware layout phase; unused and never read by
    /// the Phase 1 flat-graph layout algorithm.
    /// </summary>
    public string? InternalLabel { get; set; }
}
