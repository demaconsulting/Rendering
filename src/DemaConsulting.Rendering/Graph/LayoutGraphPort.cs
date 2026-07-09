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
///     This model deliberately has no <c>Side</c> property: no caller control over which edge of the
///     node a port is placed on exists yet, so the layout algorithm resolves each port's side from the
///     placed geometry of its connected edge(s). <see cref="ExternalLabel"/> is rendered for an edge
///     that crosses into or out of the node from outside its container; <see cref="InternalLabel"/> is
///     rendered for a delegation edge into the node's own child scope when the port is a genuine
///     boundary port. Both labels may be present on one port (a boundary/delegation port), and a single
///     port may carry more than one external and more than one internal edge — fan-out is supported,
///     the several edges on a face sharing the port's single anchor.
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
    /// no label should be shown. On a plain (non-boundary) port this is the only label present and is
    /// rendered inward beside the port symbol; on a boundary port (one that also carries an
    /// <see cref="InternalLabel"/>) it is rendered on the outward face.
    /// </summary>
    public string? ExternalLabel { get; set; }

    /// <summary>
    /// Gets or sets the text label rendered beside this port for a delegation edge into the node's own
    /// child scope (an <em>internal</em> edge). <see langword="null"/> when no label should be shown.
    /// Its presence marks the port as a genuine boundary port and drives the boundary-crossing layout
    /// and outward placement of <see cref="ExternalLabel"/>.
    /// </summary>
    public string? InternalLabel { get; set; }
}
