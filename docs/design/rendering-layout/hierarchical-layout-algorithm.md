## HierarchicalLayoutAlgorithm Unit Design

Part of the Rendering Layout system.

### HierarchicalLayoutAlgorithm Purpose

`HierarchicalLayoutAlgorithm` is a third public `ILayoutAlgorithm` implementation: the recursive
hierarchical layout engine, analogous to ELK's `RecursiveGraphLayoutEngine`. Where the layered and
containment algorithms place a single flat scope, this engine lays out a *compound* graph — a graph
whose nodes may be containers of nested subgraphs — by recursively placing each container's children
and composing the sub-layouts into one absolute `LayoutTree`. It does not place boxes itself; it
selects a bundled *leaf* algorithm per scope and delegates the actual placement to it, then sizes each
container and composes the results. It additionally resolves *boundary (delegation) ports* — a
container port that both receives an external approach edge and is referenced by an edge inside the
container's own child scope — laying the container together with its nested children in a single
combined recursive layered pass so each such port resolves to one shared anchor on the container
boundary carrying both an external and an internal label, with every converging edge routed
orthogonally onto it. It is additive: it changes no existing output and is honored only
when a caller selects it by name.

### HierarchicalLayoutAlgorithm Data Model

The class is sealed and stateless with respect to any single layout. It exposes the `AlgorithmId`
constant (`"hierarchical"`) and returns it from `Id`. A private constant and a per-node override
together govern container framing: `ContainerPadding` (`12.0` logical pixels) is the inset kept on
every side between a container border and its children's sub-layout, and `DefaultContainerTitleHeight`
(`24.0` logical pixels) is the *default* title band reserved above the children of a container that
carries a `Label` (a container with no label reserves no band regardless of any override). A container
node's own `LayoutGraphNode.TitleHeight` — when set — replaces `DefaultContainerTitleHeight` for that
node, resolved by the private `ResolveTitleHeight` helper; this lets a caller reserve a title band that
matches a specific theme's actual title-area height (including a keyword line) instead of being
limited to the generic default. The engine holds a single field, a `LayoutAlgorithmRegistry` used to
resolve the per-scope leaf algorithm by identifier. A default constructor builds a default registry
containing the bundled `LayeredLayoutAlgorithm` and `ContainmentLayoutAlgorithm`; an injecting
constructor accepts a caller-supplied registry (rejecting null). The engine treats a scope that
explicitly selects its own `"hierarchical"` identifier as selecting the default leaf algorithm, so
recursion always terminates in a bundled leaf algorithm.

### HierarchicalLayoutAlgorithm Methods

`Apply(graph, options)` rejects null `graph` or `options` with `ArgumentNullException`, builds the
root scope's cascaded effective options (`graph.OverlayOnto(options)` — the graph's own explicit
overrides win over the supplied options, exactly like every nested scope), and calls the recursive
`LayoutScope`. `LayoutScope(graph, effective)` performs:

1. **Flat fast path (equivalence guarantee).** If no direct node of the scope is a container
   (`HasChildren` is false for all), the engine delegates straight to the leaf algorithm resolved from
   `effective` and returns its `LayoutTree` unchanged — no cloning, no post-processing, no mutation.
   This guarantees a flat graph is placed byte-for-byte identically to invoking the leaf algorithm
   directly.
2. **Post-order recursion.** For each container child, the engine builds that child's own cascaded
   effective options by overlaying, in order, the container node's own overrides (`CoreOptions.Algorithm`
   lives here by convention) and then its `Children` graph's own overrides (every other `CoreOptions`
   property lives here by convention) onto the parent scope's already-resolved `effective` snapshot, via
   `PropertyHolder.OverlayOnto`. It then recursively lays out the child's subgraph with that snapshot and
   records both the sub-layout and the container's effective size — the sub-layout size grown by
   `ContainerPadding` on every side plus a title band when the container is labelled.
3. **Sized view.** The engine builds an internal, side-effect-free *view* graph with the same nodes in
   the same order (container nodes carrying their effective size, leaves their own size, labels copied,
   and each direct member's named ports copied onto its view counterpart), only the edges whose direct-
   member endpoints are both under this scope and neither nested inside a container relative to it —
   including an edge whose endpoint is a named `LayoutGraphPort` rather than the node itself, so a
   same-scope port edge is routed by the leaf algorithm exactly as it would be in a flat (container-free)
   graph, regardless of whether the scope also contains an unrelated container elsewhere. Every
   `CoreOptions` property this scope needs is already present in `effective`, so the view carries no
   options of its own — the leaf algorithm resolves them from `effective`, not from the view graph. The
   caller's input graph is never mutated.
4. **Placement.** The resolved leaf algorithm places the sized view against `effective`, emitting one box
   per node in input order, followed by routed lines and any named-port anchors (`LayoutPort`) for the
   in-scope edges.
5. **Composition.** Each container's placed box receives its recursively laid-out children, translated
   from their local origin to the box's padded (and title-offset) interior via a recursive `Translate`
   that shifts nested boxes, line waypoints, and port anchors (local-to-absolute translation, following
   the `ComponentPacker` precedent).
6. **Cross-container (LCA) routing.** Edges whose endpoints resolve to different direct-member
   containers of this scope — mapped from any descendant endpoint up to its owning top-level box — are
   routed at this level with `ConnectorRouter.Route`, steering around the sibling boxes; the
   `EdgeRouting` style is read from this scope's own cascaded `effective` snapshot, so an override set
   on the owning scope's graph is honored rather than falling back to the root options. Edges already
   routed by the leaf algorithm (both endpoints direct) or belonging to a lower scope (both endpoints
   under one container) are skipped. A genuine cross-container edge whose endpoint is a named
   `LayoutGraphPort` in a shape the boundary-port delegation mechanism does not cover — for example a
   port owned by a plain (non-container) node with an edge straight into a different container — throws
   `NotSupportedException` instead of being routed or silently dropped; that port's owner has no child
   scope to delegate into, so this scope's box-only router (which has no port concept) cannot anchor it.
   Broader boundary-crossing port support is a separate future effort (see ROADMAP.md).
7. **Assembly.** The engine returns a `LayoutTree` with the leaf algorithm's canvas size for this level
   and the composed boxes followed by the leaf-routed lines, any leaf-emitted port anchors, and the
   cross-container lines.

**Boundary (delegation) port combined pass.** After the leaf pass places a scope, `LayoutScope`
collects that scope's boundary ports with `HierarchyMergeRegionBuilder` (part of the layered-pipeline
unit, in `Engine/Layered`): a container's port is a boundary port when an edge inside that container's
own `Children` references it — the inward delegation edge is the structural signal, detected
transitively and to unbounded depth by a recursive collect. *Only when at least one boundary port is
detected* does the engine take the combined-pass path; a scope with no boundary port takes the
identical existing code path, so flat and ordinary port-edge graphs are byte-for-byte unchanged. On
the combined path the engine assembles the container and all of its nested descendants into one
`AssembledMergeRegion` (`MergeRegionGraphAssembler`), runs it through the recursive layered pipeline
(`LayeredLayoutPipeline.RunRecursive` — recursive layer assignment and crossing minimization across
every level, one crossing dummy seeded per boundary face, each crossing tagged on its `AugNode`), and
projects the placed result back into per-scope geometry with `MergeRegionDecomposer`. The decomposer
resolves each boundary port to one shared physical anchor on the container face — the face given by
`BoundaryPortResolver.FaceForDirection`, keyed on the boundary port's reference identity so the
external and internal faces collapse onto one point — carrying both the external label (rendered
outward) and the internal label (rendered inward). Crucially, every converging edge takes its
waypoints directly from the orthogonal corridor router's routed polylines: the external approach is the
approach polyline concatenated with the container-link polyline, and each internal delegation prepends
the shared anchor to its delegation polyline with at most one orthogonal corner. No endpoint is patched
onto the anchor with a hand-built diagonal, which is what keeps external fan-in and multi-level
delegation chains free of the diagonal shortcut a post-hoc endpoint reconciliation produced.

**Two-pass cascading container sizing.** Because a container's placed interior size is only known
after its combined pass runs, the engine sizes containers in two passes, mirroring the established
`Fix-5` growth precedent: it assembles the region once, then re-runs `RunRecursive` while any
container's placed interior footprint (`MergeRegionDecomposer.LevelFootprint` plus padding and title
height) exceeds its current effective size, growing the shared mutable effective-size map in place so
inner growth cascades outward through every enclosing level (`RunRecursiveWithCascadingSizing`,
bounded by `MaxSizingIterations` and `SizingTolerance`). The common no-growth case settles in a single
pass. `ContainerPadding` and the resolved title height are the same offsets the flat composition path
uses, reused rather than re-derived.

Every `CoreOptions` property (`Algorithm`, `Direction`, `EdgeRouting`, `HierarchyHandling`,
`NodeSpacing`, `LayerSpacing`) cascades through this same generalized mechanism, built once on
`PropertyHolder.OverlayOnto` rather than threaded per property: a scope with no override of its own
inherits the nearest ancestor's resolved value, and any scope's own override wins for itself and every
unset descendant. Two established conventions decide where a container's own override lives —
`Algorithm` on the container node itself, every other property on the node's `Children` graph — and
`LayoutContainerChildren` overlays both, in that order, onto the parent's snapshot so either (or both)
take effect at their own layer. It is each leaf algorithm's own responsibility to resolve a property
from the `effective` options it is invoked with (optionally preferring its own graph's override first,
as `LayeredLayoutAlgorithm` and `ContainmentLayoutAlgorithm` do); a leaf algorithm that reads only its
input graph and ignores the supplied options would silently break cascading for that algorithm.

### HierarchicalLayoutAlgorithm Design Constraints

- The flat-graph equivalence guarantee is load-bearing: the fast path must delegate directly to the
  leaf algorithm and must not clone the graph or transform the tree, so selecting the engine never
  changes existing output.
- The engine shall not mutate the caller's graph; re-sizing is expressed through the internal sized
  view rather than by altering node sizes in place.
- Hierarchy handling is `HierarchyHandling.SeparateChildren` (see the *Rendering Model* design): each
  container is laid out in isolation and sized to fit its children. The `CoreOptions.HierarchyHandling`
  option records this selection; only the separate-children mode is honored today.

### HierarchicalLayoutAlgorithm Error Handling

Null `graph`, `options`, or (injecting constructor) `registry` throw `ArgumentNullException`. A scope
that selects an algorithm identifier absent from the registry surfaces the registry's
`KeyNotFoundException`. An edge whose endpoint resolves to no node at all under the current scope (an
out-of-scope reference) is skipped rather than treated as an error. A port edge that genuinely crosses
a container boundary in a shape the boundary-port delegation mechanism does not cover — for example a
port owned by a plain (non-container) node with an edge into a different container's nested child —
throws `NotSupportedException` with a message identifying named ports crossing a container boundary as
not supported; that port's owner has no child scope to delegate into, so this scope's router has no port
concept for it, and broader boundary-crossing port support remains a separate future effort. A
container's own boundary (delegation) port is not this error case: it is resolved to one shared anchor
by the combined recursive pass and its `MergeRegionDecomposer` as described above. A same-scope port
edge (neither endpoint nested relative
to this scope) is likewise not an error case: it is routed locally by the leaf algorithm exactly as it
would be in a flat graph.

### HierarchicalLayoutAlgorithm Dependencies

`HierarchicalLayoutAlgorithm` depends on the following items:

- **Rendering.Abstractions** (`ILayoutAlgorithm`, `LayoutAlgorithmRegistry`) — implements the layout
  contract and resolves the per-scope leaf algorithm from the injected registry.
- **Rendering model** (`DemaConsulting.Rendering`) — the `LayoutGraph`, `LayoutGraphNode`,
  `LayoutTree`, `LayoutBox`, `LayoutLine`, and `Point2D` types on the layout contract, plus
  `CoreOptions.Algorithm`, `CoreOptions.Direction`, `CoreOptions.EdgeRouting`, and
  `CoreOptions.HierarchyHandling` for configuration, and `PropertyHolder.OverlayOnto` for building each
  scope's cascaded effective options snapshot.
- **Layout units** — `LayeredLayoutAlgorithm` and `ContainmentLayoutAlgorithm` as bundled leaf
  algorithms registered in the default registry, `ConnectorRouter` for LCA cross-container edge
  routing, and `HierarchyMergeRegionBuilder`, `MergeRegionGraphAssembler`, the recursive
  `LayeredLayoutPipeline`, `MergeRegionDecomposer`, and `BoundaryPortResolver.FaceForDirection`
  (layered-pipeline unit, `Engine/Layered`) for boundary-port detection, the combined recursive pass,
  and its projection back to per-scope geometry. See *ConnectorRouter Unit Design*
  and *Layered Pipeline Unit Design*.

No OTS runtime component or shared package is consumed.

### HierarchicalLayoutAlgorithm Callers

`HierarchicalLayoutAlgorithm` is invoked through the `ILayoutAlgorithm` contract, so callers reach
it by algorithm identifier rather than by direct type reference:

- **DefaultLayout** (`LayoutEngine.Layout`) — resolves this algorithm from the bundled default
  registry under the `"hierarchical"` identifier and defaults to it when no algorithm is declared on
  the graph or options. See *DefaultLayout Unit Design*.
- **External application code** — any caller that registers `HierarchicalLayoutAlgorithm` in its own
  `LayoutAlgorithmRegistry` (or uses `LayoutAlgorithms.CreateDefaultRegistry()`) and selects it via
  `CoreOptions.Algorithm = "hierarchical"` on the graph or standalone `LayoutOptions`.
- **Renderer host code** — indirectly via `LayoutEngine.Layout` when driving nested/compound
  diagrams from graph to rendered output.

### HierarchicalLayoutAlgorithm Interactions

`HierarchicalLayoutAlgorithm` depends on the `ILayoutAlgorithm`, `LayoutAlgorithmRegistry`,
`LayoutGraph`, `LayoutGraphNode`, `LayoutTree`, `CoreOptions`, and related model types, and composes
the public `ConnectorRouter` unit for cross-container routing and the bundled leaf algorithms
(`LayeredLayoutAlgorithm`, `ContainmentLayoutAlgorithm`) for per-scope placement. It is resolvable by
renderers and callers through the layout registry under the `"hierarchical"` identifier, selected via
`CoreOptions.Algorithm`.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-HierarchicalLayout-Identity | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-FlatEquivalence | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-NestsChildren | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-PerNodeAlgorithm | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-HierarchyHandling | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-CrossContainerEdge | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-BoundaryPortDelegation | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-BoundaryPortEdgeThrows | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-CascadesOptions | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-HonorsScopeEdgeRouting | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-ValidatesGraph | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-ValidatesOptions | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-ValidatesRegistry | HierarchicalLayoutAlgorithm behavior described above |
