## HierarchicalLayoutAlgorithm Unit Design

Part of the Rendering Layout system.

### HierarchicalLayoutAlgorithm Purpose

`HierarchicalLayoutAlgorithm` is a third public `LayoutAlgorithmBase` implementation: the recursive
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

`ApplyCore(graph, options)` (the `LayoutAlgorithmBase` override reached through the public
`Apply(graph)`) rejects null `graph` or `options` with `ArgumentNullException`, builds the
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
   `ContainerPadding` on every side plus a title band when the container is labeled.
3. **Sized view.** The engine builds an internal, side-effect-free *view* graph with the same nodes in
   the same order (container nodes carrying their effective size, leaves their own size, labels copied,
   and each direct member's named ports copied onto its view counterpart), only the edges whose direct-
   member endpoints are both under this scope and neither nested inside a container relative to it —
   including an edge whose endpoint is a named `LayoutGraphPort` rather than the node itself, so a
   same-scope port edge is routed by the leaf algorithm exactly as it would be in a flat (container-free)
   graph, regardless of whether the scope also contains an unrelated container elsewhere. Every
   `CoreOptions` property this scope needs is already present in `effective`, so the view carries no
   options of its own — the leaf algorithm resolves them from `effective`, not from the view graph. The
   caller's input graph is never mutated. In addition, when the resolved leaf algorithm is one of the
   bundled ones that understands layout-only edges (`LayeredLayoutAlgorithm`, `ContainmentLayoutAlgorithm`,
   or `AutoLayoutAlgorithm`), the view receives one *layout-only proxy edge* per routed edge (step 6)
   whose endpoints live under two different direct members, joining those two direct members. The proxy
   edge is marked through `MarkLayoutOnly`, which records the edge object in an identity-keyed
   side table (a `ConditionalWeakTable`) rather than encoding anything in its identifier, so no
   caller-chosen identifier can collide with it; `AutoLayoutAlgorithm` re-marks each edge copy it
   makes when it splits a graph into components. The proxy's own identifier is a unique, non-reserved
   name (`layout-only:{edge id}`, with a numeric suffix if that already names a leaf edge in the
   view) that never surfaces because the edge is never emitted. The bundled leaf algorithms let such an
   edge drive layering, ordering, and gap
   sizing but emit no connector, port, or anchor for it. A custom leaf algorithm receives the view
   unchanged (no proxies), so it is never handed an edge it does not understand.
4. **Placement.** The resolved leaf algorithm places the sized view against `effective`, emitting one box
   per node in input order, followed by routed lines and any named-port anchors (`LayoutPort`) for the
   in-scope edges. The one-box-per-node, input-order contract is what lets step 5 and the nested-endpoint
   lookup in step 6 index the placed boxes by node index; `AutoLayoutAlgorithm` restores input order after
   packing a multi-group result to honor it.
5. **Composition.** Each container's placed box receives its recursively laid-out children, translated
   from their local origin to the box's padded (and title-offset) interior via a recursive `Translate`
   that shifts nested boxes, line waypoints, and port anchors (local-to-absolute translation, following
   the `ComponentPacker` precedent).
6. **Cross-hierarchy routing.** `ClassifyEdges` first records, for every node of the scope's subtree,
   its parent and its index in that parent's child list (`MapDescendants`), and then sorts the scope's
   edges. An edge whose endpoints resolve to different direct members of this scope, with at least one
   endpoint nested, is a *cross-hierarchy edge*; an edge between two direct members that shares a box
   with such an edge is promoted into the same batch so one coordinated pass anchors every connector
   on that box. An edge declared on this scope whose two endpoints are distinct, unrelated nodes
   nested under the *same* direct member (an ancestor graph referencing nested nodes, for example a
   root graph naming two nodes of one package) is routed here too, between the nested nodes; it cannot
   influence the container's interior placement, which was laid out in isolation first. An edge between
   a container and one of its own descendants, a self-edge, and any same-direct-member port edge are
   skipped (documented limitation). Each routed edge is routed with the internal
   `ConnectorRouter.RouteBatch`: both endpoints are resolved to their actual placed boxes
   (`ResolveEndpointBox` walks from the direct member's composed box down through each level's child
   boxes, which are index-aligned with that level's graph nodes; if a level cannot be resolved the
   deepest resolvable box is used, degrading to the old container-anchored behavior instead of failing),
   and each connector gets its own hard-obstacle set: every box of the scope, with the containers
   enclosing that connector's two endpoints *expanded* — replaced by an obstacle for the title text (a
   thin box as wide as the estimated title or keyword text, centered in the title band), the folder tab
   when the container is a folder, and their children, recursively — so the connector may cross those
   containers' walls beside the title while every unrelated box and container stays solid. Face
   anchors already occupied by the internal connectors of the endpoint's own container are passed as
   fixed anchors so a new connector does not land on top of one, and the long runs of already-routed
   end segments are soft obstacles for later connectors so connectors do not run collinearly along one
   another's approach. The `EdgeRouting` style is read from this scope's own cascaded `effective`
   snapshot. The router is also given a `ConnectorRouter.RoutingProfile` carrying, per connector, the
   borders of the expanded containers *and of every other container of the scope* as wall keep-out
   strips (a folder's top border follows the tab and the body, `AddWalls`; the unrelated containers are
   reached by `AddUnrelatedWalls`, so a run beside an unrelated container's border keeps twice the
   clearance as well, instead of skirting it at the bare obstacle clearance), so a connector crosses
   those borders at right angles, keeps straight legs
   (at least twice the clearance) off both ends and no interior segment shorter than that, slides its
   ports along their faces — never nearer a face end than the arrowhead's reach, `PortCornerMargin` — onto
   a free lane (a straight connector wherever a lane fits both faces and clears every border; otherwise a
   clean Z or U), may enter a nested box through an adjacent
   face when the natural face lies under a container's title band, keeps clear of the end faces of the
   other connectors and avoids crossing them. See *ConnectorRouter Unit Design*. An edge whose endpoint
   is a container itself still anchors on that container. A genuine
   cross-container edge whose endpoint is a named `LayoutGraphPort` in a shape the boundary-port
   delegation mechanism does not cover — for example a port owned by a plain (non-container) node
   with an edge straight into a different container — throws `NotSupportedException` instead of being
   routed or silently dropped; that port's owner has no child scope to delegate into, so this scope's
   box-only router (which has no port concept) cannot anchor it. Broader boundary-crossing port support
   is a separate future effort (see ROADMAP.md). Known limitations: placement accounts for an edge only
   at the declaring scope (an edge cannot reshuffle the interior of the container that holds both of
   its endpoints), the boundary-port combined-pass path routes the connector between the nested nodes
   but does not yet let it influence that path's placement, and a dense corridor can still yield
   closely spaced parallel connector lanes.
7. **Assembly.** The engine returns a `LayoutTree` with the leaf algorithm's canvas size for this level
   and the composed boxes followed by the leaf-routed lines, any leaf-emitted port anchors, and the
   cross-container lines.

**No-boundary-port sibling-container gap widening.** On the no-boundary-port path (step 7 above),
before routing the cross-container edges, `LayoutScope` runs a post-placement widening pass
(`WidenSiblingContainerGaps`) over the composed boxes. It counts the cross-container edges between each
unordered pair of direct-member containers (resolving each endpoint to the direct member that owns it,
exactly as the router does) and, for a pair placed genuinely side by side — vertically overlapping,
with no third box in the channel between them — widens the horizontal gap between the two to the
connector-corridor width for that edge count. The width comes from the shared `EdgeCountGapWidener`
helper, the same formula the containment packer and the layered pipeline's `BrandesKopfPlacer` use, so
a fan of parallel cross-container connectors gets distinct routing lanes — spacing the per-scope leaf
algorithm alone cannot reserve, because those edges never appear in the sized view it lays out. The
pass applies each widening as a rigid half-plane shift (every box, line, and port at or right of the
cut moves right by the summed extra), returning the inputs unchanged when no pair qualifies, so a pair
joined by zero or one cross-container edge stays byte-identical. The widening is horizontal-only and is
confined to this no-boundary-port path; extending it to the boundary-port combined pass below is
explicitly deferred (see ROADMAP.md) because that path composes geometry through the recursive
merge-region pipeline rather than the `ComposeBoxes` path this post-placement pass operates on. The
`hierarchical-parallel-edges-side-by-side` gallery diagram (two peer containers, each with a
compartment-bearing child, joined by eight cross-container edges) is the reproduction scenario that
exercises and visually demonstrates this widening.

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
  container's interior is laid out in isolation and the container sized to fit its children. The
  `CoreOptions.HierarchyHandling` option records this selection; only the separate-children mode is
  honored today. Edges declared on an ancestor graph are nevertheless routed between the actual nested
  endpoint nodes (the default behavior, with no separate mode), and the declaring scope's placement
  accounts for them through layout-only proxy edges; sharing one layout pass across container
  boundaries remains future work (see ROADMAP.md).

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

- **Rendering.Abstractions** (`LayoutAlgorithmBase`, `LayoutAlgorithmRegistry`) — implements the layout
  contract and resolves the per-scope leaf algorithm from the injected registry.
- **Rendering model** (`DemaConsulting.Rendering`) — the `LayoutGraph`, `LayoutGraphNode`,
  `LayoutTree`, `LayoutBox`, `LayoutLine`, and `Point2D` types on the layout contract, plus
  `CoreOptions.Algorithm`, `CoreOptions.Direction`, `CoreOptions.EdgeRouting`, and
  `CoreOptions.HierarchyHandling` for configuration, and `PropertyHolder.OverlayOnto` for building each
  scope's cascaded effective options snapshot.
- **Layout units** — `LayeredLayoutAlgorithm` and `ContainmentLayoutAlgorithm` as bundled leaf
  algorithms registered in the default registry, `ConnectorRouter` for LCA cross-container edge
  routing, `EdgeCountGapWidener` (internal engine helper) for the shared connector-corridor width the
  no-boundary-port sibling-gap widening pass reserves, and `HierarchyMergeRegionBuilder`,
  `MergeRegionGraphAssembler`, the recursive
  `LayeredLayoutPipeline`, `MergeRegionDecomposer`, and `BoundaryPortResolver.FaceForDirection`
  (layered-pipeline unit, `Engine/Layered`) for boundary-port detection, the combined recursive pass,
  and its projection back to per-scope geometry. See *ConnectorRouter Unit Design*
  and *Layered Pipeline Unit Design*.

No OTS runtime component or shared package is consumed.

### HierarchicalLayoutAlgorithm Callers

`HierarchicalLayoutAlgorithm` is invoked through the `LayoutAlgorithmBase` contract, so callers reach
it by algorithm identifier rather than by direct type reference:

- **DefaultLayout** (`LayoutEngine.Layout`) — resolves this algorithm from the bundled default
  registry under the `"hierarchical"` identifier and defaults to it when no algorithm is declared on
  the graph. See *DefaultLayout Unit Design*.
- **External application code** — any caller that registers `HierarchicalLayoutAlgorithm` in its own
  `LayoutAlgorithmRegistry` (or uses `LayoutAlgorithms.CreateDefaultRegistry()`) and selects it via
  `CoreOptions.Algorithm = "hierarchical"` on the graph.
- **Renderer host code** — indirectly via `LayoutEngine.Layout` when driving nested/compound
  diagrams from graph to rendered output.

### HierarchicalLayoutAlgorithm Interactions

`HierarchicalLayoutAlgorithm` depends on the `LayoutAlgorithmBase`, `LayoutAlgorithmRegistry`,
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
| Rendering-Layout-HierarchicalLayout-CrossHierarchyEdge-NestedAnchors | Cross-hierarchy routing (step 6) |
| Rendering-Layout-HierarchicalLayout-CrossHierarchyEdge-AncestorPlacement | Layout-only proxy edges (step 3) |
| Rendering-Layout-HierarchicalLayout-CrossHierarchyEdge-SameContainer | Cross-hierarchy routing (step 6) |
| Rendering-Layout-HierarchicalLayout-CrossHierarchyEdge-CleanRouting | Cross-hierarchy routing (step 6), profile |
| Rendering-Layout-HierarchicalLayout-LayoutOnlyMarkerIsCollisionProof | Layout-only proxy edges (step 3) |
| Rendering-Layout-HierarchicalLayout-SiblingContainerGapWidening | Widening pass described above |
| Rendering-Layout-HierarchicalLayout-SiblingContainerGapWideningSingleEdgeUnaffected | Widening pass described above |
| Rendering-Layout-HierarchicalLayout-SiblingGapWideningPortPathUnaffected | Widening pass described above |
| Rendering-Layout-HierarchicalLayout-BoundaryPortDelegation | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-BoundaryPortFanMerge | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-IndependentBoundaryPorts | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-BoundaryPortOrthogonal | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-BoundaryPortEdgeThrows | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-CascadesOptions | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-CascadesAllProperties | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-HonorsScopeEdgeRouting | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-ValidatesGraph | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-ValidatesOptions | HierarchicalLayoutAlgorithm behavior described above |
| Rendering-Layout-HierarchicalLayout-ValidatesRegistry | HierarchicalLayoutAlgorithm behavior described above |
