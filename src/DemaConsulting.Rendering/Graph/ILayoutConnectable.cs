// <copyright file="ILayoutConnectable.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// Marker interface implemented by anything a <see cref="LayoutGraphEdge"/> may connect: a
/// <see cref="LayoutGraphNode"/> or a <see cref="LayoutGraphPort"/> owned by one. Mirrors the
/// Eclipse Layout Kernel (ELK) <c>ElkConnectableShape</c> abstraction, letting
/// <see cref="LayoutGraphEdge.Source"/>/<see cref="LayoutGraphEdge.Target"/> and
/// <see cref="LayoutGraph.AddEdge"/> reference either a node or one of its named ports without a
/// separate edge type.
/// </summary>
public interface ILayoutConnectable
{
}
