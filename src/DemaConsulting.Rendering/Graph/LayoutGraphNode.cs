// <copyright file="LayoutGraphNode.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// A node in an input <see cref="LayoutGraph"/>: a sized box that a layout algorithm places. The
/// node carries its own configuration via <see cref="PropertyHolder"/>, allowing per-node overrides
/// of algorithm options.
/// </summary>
public sealed class LayoutGraphNode : PropertyHolder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutGraphNode"/> class.
    /// </summary>
    /// <param name="id">Identifier unique within the owning graph.</param>
    /// <param name="width">Width of the node's bounding box in logical pixels.</param>
    /// <param name="height">Height of the node's bounding box in logical pixels.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is empty.</exception>
    public LayoutGraphNode(string id, double width, double height)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        Id = id;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the identifier, unique within the owning graph.</summary>
    public string Id { get; }

    /// <summary>Gets or sets the width of the node's bounding box in logical pixels.</summary>
    public double Width { get; set; }

    /// <summary>Gets or sets the height of the node's bounding box in logical pixels.</summary>
    public double Height { get; set; }

    /// <summary>Gets or sets an optional text label rendered inside the node.</summary>
    public string? Label { get; set; }
}
