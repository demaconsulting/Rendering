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
///     named boundary port crosses a container boundary. Each container is still sized bottom-up
///     (children first) and the existing per-scope leaf pass anchors the external side of every
///     boundary crossing exactly as it always has; a reconciliation step then merges each boundary
///     port's external anchor and its interior delegation targets into a single shared boundary
///     anchor carrying both labels, and routes the internal delegation connectors from that anchor to
///     the children the port feeds. The merge region the reconciliation covers is <em>general and
///     transitive</em>: it is the minimal footprint touched by boundary-crossing edges — not fixed at
///     two levels and not one boundary port at a time — so it follows a chain of delegation ports to
///     arbitrary depth and merges any number of independent boundary ports on one container together.
///     </para>
///     <para>
///     The end state this mode is designed toward is a single fully-joint pass that flattens the
///     touched hierarchy and lays every crossing out as a hierarchy-crossing dummy alongside ordinary
///     long-edge dummies (see <see cref="HierarchyCrossing"/>). The current implementation reaches the
///     same <em>general/transitive</em> external result by reconciliation rather than that literal
///     flattened pass; the hierarchy-crossing dummy descriptor and its combined ordering primitive
///     (<c>BoundaryPortResolver.OrderCrossings</c>) are exercised by unit tests and reserved for that
///     joint pass, and do not yet govern production anchor placement.
///     </para>
/// </remarks>
internal enum HierarchyHandling
{
    /// <summary>Run the pipeline once over a single flat graph.</summary>
    Flat,

    /// <summary>
    /// Handle boundary-crossing port edges over the general/transitive merge region touched by the
    /// crossings, reconciling each into a single shared dual-label boundary anchor with internal
    /// delegation connectors (the reserved fully-joint flattened pass is approximated by reconciliation).
    /// </summary>
    Recursive,
}
