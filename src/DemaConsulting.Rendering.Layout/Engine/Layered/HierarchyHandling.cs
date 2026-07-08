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
///     named boundary port crosses a container boundary: the portion of the node hierarchy the
///     crossing actually touches is flattened into one graph, each boundary crossing is represented as
///     a hierarchy-crossing dummy node that participates in the same single layer-assignment and
///     crossing-minimization pass as ordinary long-edge dummies, and each container's own size is still
///     reconciled bottom-up (children sized first) before the joint pass positions the crossing point
///     consistently with both the container's placement among its siblings and its children's placement
///     inside it. The merge region is <em>general and transitive</em>: it is the minimal footprint
///     touched by boundary-crossing edges — not fixed at two levels and not one boundary port at a
///     time — so it follows a chain of delegation ports to arbitrary depth and merges any number of
///     independent boundary ports on one container together in a single combined pass.
///     </para>
/// </remarks>
internal enum HierarchyHandling
{
    /// <summary>Run the pipeline once over a single flat graph.</summary>
    Flat,

    /// <summary>
    /// Flatten the portion of the hierarchy touched by boundary-crossing port edges into one graph and
    /// resolve each crossing as a hierarchy-crossing dummy in a single combined layered pass.
    /// </summary>
    Recursive,
}
