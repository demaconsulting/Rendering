// <copyright file="HierarchyHandling.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// Selects how a hierarchical layout engine treats a container node's nested children when placing a
/// compound graph. This is the ELK <c>elk.hierarchyHandling</c> analogue: it decides whether each
/// container is laid out in isolation or its children participate in the parent's layout.
/// </summary>
/// <remarks>
///     <para>
///     <see cref="SeparateChildren"/> is the only shipped mode and the default. Under it each
///     container's children are laid out in their own coordinate space by the container's selected
///     algorithm, and the container is then sized to fit the resulting sub-layout (plus padding and an
///     optional title band). The container <em>interiors</em> therefore stay independent and
///     deterministic, mirroring how ELK lays out compound nodes when hierarchy handling is left separate.
///     </para>
///     <para>
///     Edges are not confined to one container, though. An edge declared on an ancestor graph (the
///     lowest common ancestor of its endpoints, or any graph above it) that references nodes nested
///     inside containers is routed in the coordinate space of the graph that declares it, from the
///     actual nested source node to the actual nested target node: the connector crosses only the walls
///     of the containers enclosing its own two endpoints and steers around every other box. The
///     declaring graph's placement also accounts for such an edge, treating it as joining the top-level
///     containers that hold its endpoints, so related containers are placed near each other. An edge
///     whose endpoint is a container itself still anchors on that container. This is the default
///     behavior; it needs no separate mode.
///     </para>
///     <para>
///     Placing every container's children in one shared layout pass (ELK's <c>INCLUDE_CHILDREN</c>)
///     is not implemented and is tracked in the project roadmap.
///     </para>
/// </remarks>
public enum HierarchyHandling
{
    /// <summary>
    /// Each container's children are laid out in their own coordinate space and the container is sized
    /// to enclose them. This is the only shipped mode and the default; edges declared on an ancestor
    /// graph are routed between the actual nested endpoint nodes rather than between their containers.
    /// </summary>
    SeparateChildren = 0,
}
