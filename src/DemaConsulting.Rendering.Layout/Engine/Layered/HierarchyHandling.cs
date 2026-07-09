// <copyright file="HierarchyHandling.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Layout.Engine.Layered;

/// <summary>
/// How a layered layout treats nested (compound) nodes.
/// </summary>
/// <remarks>
///     <para>
///     <see cref="Flat"/> runs the pipeline once over a single flat graph and is the mode every
///     container-free layout uses.
///     </para>
///     <para>
///     <see cref="Recursive"/> selects the ELK-style compound-graph handling used when an edge with a
///     named boundary port crosses a container boundary. The touched hierarchy is flattened into a
///     single hierarchy-aware graph spanning every nesting level (<see cref="MergeRegionGraphAssembler"/>),
///     every boundary crossing becomes a hierarchy-crossing dummy that flows through exactly the same
///     layer-assignment, crossing-minimization, placement, and orthogonal corridor routing as an
///     ordinary node (<see cref="LayeredLayoutPipeline.RunRecursive"/>), and the fully-placed result is
///     projected back into per-scope boxes, lines, and ports (<see cref="MergeRegionDecomposer"/>). A
///     boundary port therefore resolves to a single shared dual-label anchor whose external approaches
///     and internal delegations are all routed by the corridor router — no post-hoc reconciliation and
///     no endpoint patching. The merge region is <em>general and transitive</em>: it is the minimal
///     footprint touched by boundary-crossing edges — not fixed at two levels and not one boundary port
///     at a time — so it follows a chain of delegation ports to arbitrary depth and merges any number of
///     independent boundary ports on one container together. Each container is sized to fit its placed
///     interior, and that growth cascades outward through every enclosing level.
///     </para>
/// </remarks>
internal enum HierarchyHandling
{
    /// <summary>Run the pipeline once over a single flat graph.</summary>
    Flat,

    /// <summary>
    /// Handle boundary-crossing port edges over the general/transitive merge region touched by the
    /// crossings, in a single combined pass that flattens the region, lays every crossing out as a
    /// hierarchy-crossing dummy, and projects the placement back into per-scope geometry with one shared
    /// dual-label anchor per port whose connectors are all corridor-router-derived.
    /// </summary>
    Recursive,
}
