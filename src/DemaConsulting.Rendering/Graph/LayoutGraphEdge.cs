// <copyright file="LayoutGraphEdge.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// A directed edge in an input <see cref="LayoutGraph"/>, connecting a source node to a target node.
/// The edge carries its own configuration via <see cref="PropertyHolder"/>.
/// </summary>
public sealed class LayoutGraphEdge : PropertyHolder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutGraphEdge"/> class.
    /// </summary>
    /// <param name="id">Identifier unique within the owning graph.</param>
    /// <param name="source">The node the edge originates from.</param>
    /// <param name="target">The node the edge terminates at.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="id"/>, <paramref name="source"/>, or <paramref name="target"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is empty.</exception>
    public LayoutGraphEdge(string id, LayoutGraphNode source, LayoutGraphNode target)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        Id = id;
        Source = source;
        Target = target;
    }

    /// <summary>Gets the identifier, unique within the owning graph.</summary>
    public string Id { get; }

    /// <summary>Gets the node the edge originates from.</summary>
    public LayoutGraphNode Source { get; }

    /// <summary>Gets the node the edge terminates at.</summary>
    public LayoutGraphNode Target { get; }

    /// <summary>Gets or sets the end-marker style drawn at the target end of the edge.</summary>
    public EndMarkerStyle TargetEnd { get; set; } = EndMarkerStyle.None;

    /// <summary>Gets or sets the stroke style used to draw the edge.</summary>
    public LineStyle LineStyle { get; set; } = LineStyle.Solid;

    /// <summary>Gets or sets an optional text label rendered at the midpoint of the edge.</summary>
    public string? Label { get; set; }
}
