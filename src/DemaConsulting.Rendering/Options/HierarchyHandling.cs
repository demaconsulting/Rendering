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
///     optional title band). Cross-container edges are routed in the coordinate space of the container
///     that owns them — the lowest common ancestor of their endpoints — rather than being folded into a
///     single flattened layout. This keeps every container's placement independent and deterministic,
///     mirroring how ELK lays out compound nodes when hierarchy handling is left separate.
///     </para>
///     <para>
///     A future, additive <c>IncludeChildren</c> mode is planned to mirror ELK's cross-boundary
///     (inclusion) hierarchy handling, in which a container's children take part in the same layout pass
///     as their siblings so edges may cross container boundaries as first-class layout elements. That
///     value is intentionally omitted until an engine implements it, so the public surface never
///     advertises a capability the library cannot yet deliver; adding it later is a source-compatible,
///     purely additive change.
///     </para>
/// </remarks>
public enum HierarchyHandling
{
    /// <summary>
    /// Each container's children are laid out in their own coordinate space and the container is sized
    /// to enclose them. This is the only shipped mode and the default; cross-container edges are routed
    /// by the container that owns them rather than flattened across boundaries.
    /// </summary>
    SeparateChildren = 0,
}
