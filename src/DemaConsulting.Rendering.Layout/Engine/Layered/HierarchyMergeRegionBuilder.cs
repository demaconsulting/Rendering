// <copyright file="HierarchyMergeRegionBuilder.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Layout.Engine.Layered;

/// <summary>
/// A single boundary (delegation) port discovered in one container scope: a named
/// <see cref="LayoutGraphPort"/> owned by a container node whose own child scope contains at least one
/// edge that references the port, marking the port as a genuine boundary crossing rather than a plain
/// same-scope port.
/// </summary>
/// <remarks>
///     The port carries both the <see cref="ExternalEdges"/> that approach it from the container's own
///     scope (typically a sibling node, or — for a nested link in a delegation chain — the parent
///     container's own boundary port) and the <see cref="InternalEdges"/> that delegate it into the
///     container's child scope. A boundary port always has at least one internal edge; it has zero or
///     more external edges. Fan-out is expressed naturally as more than one edge in either list, all
///     sharing the port's single physical anchor.
/// </remarks>
/// <param name="Port">The boundary port itself.</param>
/// <param name="Container">The container node that owns <paramref name="Port"/> and delegates it inward.</param>
/// <param name="ExternalEdges">Edges in the container's own scope that reference the port (may be empty).</param>
/// <param name="InternalEdges">Edges in the container's child scope that reference the port (never empty).</param>
internal sealed record BoundaryPort(
    LayoutGraphPort Port,
    LayoutGraphNode Container,
    IReadOnlyList<LayoutGraphEdge> ExternalEdges,
    IReadOnlyList<LayoutGraphEdge> InternalEdges);

/// <summary>
/// A boundary port paired with the container scope it was discovered in, used when collecting an entire
/// hierarchy's boundary ports across every nesting level in one call.
/// </summary>
/// <param name="Scope">The container scope (the graph) whose direct-member container owns the port.</param>
/// <param name="Boundary">The boundary port discovered in <paramref name="Scope"/>.</param>
internal sealed record ScopedBoundaryPort(LayoutGraph Scope, BoundaryPort Boundary);

/// <summary>
/// Assembles the boundary-crossing merge region for the ELK-style recursive hierarchy handling: the set
/// of boundary (delegation) ports whose edges cross a container boundary and therefore cannot be routed
/// by an ordinary per-scope leaf pass.
/// </summary>
/// <remarks>
///     <para>
///     Detection is purely structural and local to a scope: a container node <c>B</c> that is a direct
///     member of the scope owns a boundary port <c>P</c> exactly when <c>B.Children</c> — the container's
///     own child scope — contains at least one edge referencing <c>P</c>. That inward (delegation) edge
///     is the definitive signal, because a plain, non-boundary port's edges always live in the port
///     owner's <em>own</em> scope, never one level down inside the owner's children.
///     </para>
///     <para>
///     The merge region is <em>general and transitive by construction</em>: the builder reports the
///     boundary ports of a single scope, and the recursive layout walk visits every nested scope, so a
///     delegation chain (a port whose internal edge targets another container's own boundary port) is
///     discovered one level at a time as the walk descends — no fixed two-level or single-port cap.
/// <see cref="CollectRecursive(LayoutGraph)"/> materializes the same union across every level of a hierarchy in
///     one call, primarily so the transitive, multi-port behavior can be asserted directly in a unit
///     test.
///     </para>
/// </remarks>
internal static class HierarchyMergeRegionBuilder
{
    /// <summary>
    /// Collects every boundary (delegation) port owned by a direct-member container of
    /// <paramref name="scope"/>.
    /// </summary>
    /// <param name="scope">The container scope whose direct members are examined.</param>
    /// <returns>
    /// The boundary ports discovered in this scope, in a deterministic order (container insertion
    /// order, then port insertion order). Empty when the scope has no boundary-crossing port edges, so
    /// a caller can gate all recursive-hierarchy behavior behind a non-empty result and keep every
    /// boundary-port-free scope on its existing, unchanged code path.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scope"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<BoundaryPort> Collect(LayoutGraph scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var result = new List<BoundaryPort>();
        foreach (var container in scope.Nodes)
        {
            // Only a container node can delegate a port inward; a leaf (no child scope) never owns a
            // boundary port, and a container with no ports obviously owns none.
            if (!container.HasChildren || !container.HasPorts)
            {
                continue;
            }

            foreach (var port in container.Ports.Ports)
            {
                // The inward (delegation) edges live in the container's own child scope. Their presence
                // is the structural signal that promotes this port from a plain port to a boundary port.
                var internalEdges = EdgesReferencing(container.Children, port);
                if (internalEdges.Count == 0)
                {
                    continue;
                }

                // The outward (approach) edges live in this scope. There may be none (a delegation
                // port fed only from a further-out crossing), one, or several (fan-out).
                var externalEdges = EdgesReferencing(scope, port);
                result.Add(new BoundaryPort(port, container, externalEdges, internalEdges));
            }
        }

        return result;
    }

    /// <summary>
    /// Collects every boundary (delegation) port across an entire hierarchy, descending into every
    /// nested child scope, so the transitive union-of-chains merge region can be inspected as a whole.
    /// </summary>
    /// <param name="root">The root container scope to walk.</param>
    /// <returns>
    /// Every boundary port found at any nesting level, each paired with the scope it belongs to, in a
    /// deterministic top-down, insertion-order traversal.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="root"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<ScopedBoundaryPort> CollectRecursive(LayoutGraph root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var result = new List<ScopedBoundaryPort>();
        CollectRecursive(root, result);
        return result;
    }

    /// <summary>
    /// Recursively appends the boundary ports of <paramref name="scope"/> and all of its descendant
    /// scopes to <paramref name="sink"/>, in a top-down, insertion-order traversal.
    /// </summary>
    /// <param name="scope">The scope currently being walked.</param>
    /// <param name="sink">The accumulating result list.</param>
    private static void CollectRecursive(LayoutGraph scope, List<ScopedBoundaryPort> sink)
    {
        foreach (var boundary in Collect(scope))
        {
            sink.Add(new ScopedBoundaryPort(scope, boundary));
        }

        foreach (var node in scope.Nodes)
        {
            if (node.HasChildren)
            {
                CollectRecursive(node.Children, sink);
            }
        }
    }

    /// <summary>
    /// Returns every edge of <paramref name="graph"/> whose source or target is exactly
    /// <paramref name="port"/>, preserving insertion order so fan-out edges are reported deterministically.
    /// </summary>
    /// <param name="graph">The scope whose edges are scanned.</param>
    /// <param name="port">The port both endpoints are compared against by reference.</param>
    /// <returns>The matching edges, in insertion order; empty when none reference the port.</returns>
    private static List<LayoutGraphEdge> EdgesReferencing(LayoutGraph graph, LayoutGraphPort port)
    {
        return graph.Edges
            .Where(edge => ReferenceEquals(edge.Source, port) || ReferenceEquals(edge.Target, port))
            .ToList();
    }
}
