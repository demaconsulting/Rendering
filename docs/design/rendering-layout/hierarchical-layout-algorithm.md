# HierarchicalLayoutAlgorithm Unit Design

Part of the [Rendering Layout](rendering-layout.md) system.

## HierarchicalLayoutAlgorithm Purpose

`HierarchicalLayoutAlgorithm` is a third public `ILayoutAlgorithm` implementation: the recursive
hierarchical layout engine, analogous to ELK's `RecursiveGraphLayoutEngine`. Where the layered and
containment algorithms place a single flat scope, this engine lays out a *compound* graph — a graph
whose nodes may be containers of nested subgraphs — by recursively placing each container's children
and composing the sub-layouts into one absolute `LayoutTree`. It does not place boxes itself; it
selects a bundled *leaf* algorithm per scope and delegates the actual placement to it, then sizes each
container and composes the results. It is additive: it changes no existing output and is honored only
when a caller selects it by name.

## HierarchicalLayoutAlgorithm Data Model

The class is sealed and stateless with respect to any single layout. It exposes the `AlgorithmId`
constant (`"hierarchical"`) and returns it from `Id`. Two private constants govern container framing:
`ContainerPadding` (`12.0` logical pixels) is the inset kept on every side between a container border
and its children's sub-layout, and `ContainerTitleHeight` (`24.0` logical pixels) is the title band
reserved above the children of a container that carries a `Label` (a container with no label reserves
no band). The engine holds a single field, a `LayoutAlgorithmRegistry` used to resolve the per-scope
leaf algorithm by identifier. A default constructor builds a default registry containing the
bundled `LayeredLayoutAlgorithm` and `ContainmentLayoutAlgorithm`; an injecting constructor accepts a
caller-supplied registry (rejecting null). The engine treats a scope that explicitly selects its own
`"hierarchical"` identifier as selecting the default leaf algorithm, so recursion always terminates in a
bundled leaf algorithm.

## HierarchicalLayoutAlgorithm Methods

`Apply(graph, options)` rejects null `graph` or `options` with `ArgumentNullException`, resolves the
root scope's algorithm (the graph's explicit `CoreOptions.Algorithm` override if present, otherwise the
options' algorithm — default `"layered"`), and calls the recursive `LayoutScope`. `LayoutScope(graph,
algoId, options)` performs:

1. **Flat fast path (equivalence guarantee).** If no direct node of the scope is a container
   (`HasChildren` is false for all), the engine delegates straight to the resolved leaf algorithm and
   returns its `LayoutTree` unchanged — no cloning, no post-processing, no mutation. This guarantees a
   flat graph is placed byte-for-byte identically to invoking the leaf algorithm directly.
2. **Post-order recursion.** For each container child, the engine resolves the child's algorithm (the
   node's `CoreOptions.Algorithm` override, else the inherited scope algorithm), recursively lays out
   the child's subgraph, and records both the sub-layout and the container's effective size — the
   sub-layout size grown by `ContainerPadding` on every side plus a title band when the container is
   labelled.
3. **Sized view.** The engine builds an internal, side-effect-free *view* graph with the same nodes in
   the same order (container nodes carrying their effective size, leaves their own size, labels copied)
   and only the edges whose endpoints are both direct members of this scope. The caller's input graph
   is never mutated.
4. **Placement.** The resolved leaf algorithm places the sized view, emitting one box per node in input
   order followed by routed lines for the in-scope edges.
5. **Composition.** Each container's placed box receives its recursively laid-out children, translated
   from their local origin to the box's padded (and title-offset) interior via a recursive `Translate`
   that shifts nested boxes and line waypoints (local-to-absolute translation, following the
   `ComponentPacker` precedent).
6. **Cross-container (LCA) routing.** Edges whose endpoints resolve to different direct-member
   containers of this scope — mapped from any descendant endpoint up to its owning top-level box — are
   routed at this level with `ConnectorRouter.Route`, steering around the sibling boxes; the
   `EdgeRouting` style is read from the options. Edges already routed by the leaf algorithm (both
   endpoints direct) or belonging to a lower scope (both endpoints under one container) are skipped.
7. **Assembly.** The engine returns a `LayoutTree` with the leaf algorithm's canvas size for this level
   and the composed boxes followed by the leaf-routed lines and the cross-container lines.

## HierarchicalLayoutAlgorithm Design Constraints

- The flat-graph equivalence guarantee is load-bearing: the fast path must delegate directly to the
  leaf algorithm and must not clone the graph or transform the tree, so selecting the engine never
  changes existing output.
- The engine shall not mutate the caller's graph; re-sizing is expressed through the internal sized
  view rather than by altering node sizes in place.
- Hierarchy handling is `HierarchyHandling.SeparateChildren` (see the *Rendering Model* design): each
  container is laid out in isolation and sized to fit its children. The `CoreOptions.HierarchyHandling`
  option records this selection; only the separate-children mode is honored today.

## HierarchicalLayoutAlgorithm Error Handling

Null `graph`, `options`, or (injecting constructor) `registry` throw `ArgumentNullException`. A scope
that selects an algorithm identifier absent from the registry surfaces the registry's
`KeyNotFoundException`. Edges whose endpoints are not under the current scope are skipped rather than
treated as errors.

## HierarchicalLayoutAlgorithm Interactions

`HierarchicalLayoutAlgorithm` depends on the `ILayoutAlgorithm`, `LayoutAlgorithmRegistry`,
`LayoutGraph`, `LayoutGraphNode`, `LayoutTree`, `CoreOptions`, and related model types, and composes
the public `ConnectorRouter` unit for cross-container routing and the bundled leaf algorithms
(`LayeredLayoutAlgorithm`, `ContainmentLayoutAlgorithm`) for per-scope placement. It is resolvable by
renderers and callers through the layout registry under the `"hierarchical"` identifier, selected via
`CoreOptions.Algorithm`.

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-HierarchicalLayout-Identity | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-FlatEquivalence | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-NestsChildren | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-PerNodeAlgorithm | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-HierarchyHandling | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-CrossContainerEdge | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-Validation | HierarchicalLayoutAlgorithm behavior described above |
