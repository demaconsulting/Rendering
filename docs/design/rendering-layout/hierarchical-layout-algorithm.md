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
limited to the generic default. A folder whose name is drawn in the tab
(`BoxMetrics.IsNameInTab(shape, label, appearance)`: `Shape` is `Folder`, the node has a label, and
`Appearance.FolderTitle` is `FolderTitlePlacement.Tab`) holds no name in its body, so
`ResolveTitleHeight` returns `0.0` for it and no body title band is reserved above its children — unless
the caller set an explicit `TitleHeight`, which is honored as given (for example to reserve a keyword
line), or the node has a `Keyword`, whose line the renderers still draw in the body, so the default
reserve is kept for the children to clear it. The folder tab height is still added by
`ResolveContentOffsetHeight`, because the tab itself occupies the top of the folder either way.
The engine holds a single field, a `LayoutAlgorithmRegistry` used to
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
3. **Sized view.** The engine builds an internal, side-effect-free *view* graph with the same nodes in the same order
   (container nodes carrying their effective size, leaves their own size, labels copied, each node's `Appearance`
   reference copied so a styled container or leaf keeps its notation through every level, and each direct member's named
   ports copied onto its view counterpart together with the port's labels and `Appearance`), only the edges whose
   direct- member endpoints are both under this scope and neither nested inside a container relative to it — including
   an edge whose endpoint is a named `LayoutGraphPort` rather than the node itself, so a same-scope port edge is routed
   by the leaf algorithm exactly as it would be in a flat (container-free) graph, regardless of whether the scope also
   contains an unrelated container elsewhere. Every `CoreOptions` property this scope needs is already present in
   `effective`, so the view carries no options of its own — the leaf algorithm resolves them from `effective`, not from
   the view graph. The caller's input graph is never mutated. In addition, when the resolved leaf algorithm is one of
   the bundled ones that understands layout-only edges (`LayeredLayoutAlgorithm`, `ContainmentLayoutAlgorithm`, or
   `AutoLayoutAlgorithm`), the view receives one *layout-only proxy edge* per routed edge (step 6) whose endpoints live
   under two different direct members, joining those two direct members. The proxy edge is marked through
   `MarkLayoutOnly`, which records the edge object in an identity-keyed side table (a `ConditionalWeakTable`) rather
   than encoding anything in its identifier, so no caller-chosen identifier can collide with it; `AutoLayoutAlgorithm`
   re-marks each edge copy it makes when it splits a graph into components. The proxy's own identifier is a unique,
   non-reserved name (`layout-only:{edge id}`, with a numeric suffix if that already names a leaf edge in the view) that
   never surfaces because the edge is never emitted. The bundled leaf algorithms let such an edge drive layering,
   ordering, and gap sizing but emit no connector, port, or anchor for it. A custom leaf algorithm receives the view
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
identical existing code path, so for flat and ordinary port-edge graphs the hierarchical algorithm adds nothing
to the leaf layered result (that result itself now includes the default-on port alignment of Rendering#57). On
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

Edge end labels (`SourceEndLabel`, `TargetEndLabel`) are copied onto the placed lines for leaf-scope edges (through
the layered algorithm) and for cross-container edges (through the `Connection` handed to `ConnectorRouter`).
Cross-container connectors reserve no room for their labels.

A port's `Appearance` is copied onto the placed port for leaf-scope edges (through the layered algorithm) and for
a boundary port projected onto its container boundary (`MergeRegionDecomposer.ProjectPorts`).

**Canvas fit.** A cross-container connector is routed around the outside of a container wall, so for a
container near the origin its lane can lie at a negative coordinate, and a connector label (for example an
`«import»` keyword label) can extend past the canvas; renderers only grow a canvas to the right and down, so
both were clipped. Each scope's finished tree — at both return sites of `LayoutScope` (the leaf/no-boundary-port
path and the boundary-port combined path) — is therefore passed through `CanvasFitter.Fit` with that scope's
resolved `CoreOptions.AssumedFontSize`. Because every enclosing scope fits the nested result it receives, the
root tree's canvas contains every connector and label at every depth. A scope that already fits is returned as
the same instance, so existing output is byte-identical. The leaf layered algorithm packs isolated nodes by
their on-screen extents for every direction (see the layered pipeline design), so isolated nodes inside and
outside a container do not overlap.

**Layer spacing.** `CoreOptions.LayerSpacing` cascades with the other properties: each scope's leaf layered pass
receives the resolved value (a container's own value lives on its `Children` graph, like every non-algorithm
property), and the boundary-port combined pass (`MergeRegionGraphAssembler`) carries it per level, so an unset
value behaves exactly like an explicit `70.0` at every depth.

#### Cross-hierarchy ordering refinement

A container interior is laid out in isolation, blind to the connectors its declaring scope routes between nested
nodes of different containers, so the order of an interior's members was decided by insertion order alone and
could force those connectors to cross. After a scope's first routed pass, the refinement driver
(`HierarchicalLayoutAlgorithm.CrossOrdering.cs`, a second part of the sealed class) does the following.

1. It counts the crossings over the whole subtree of the scope (`CountCrossings`: the scope's own connectors and
   the interior connectors of every container; touching, collinear and shared-end-point lines do not count).
2. For every node on the chain of a cross-container connector's endpoint it derives the position of the partner the
   connector leads to (the median of the placed partner centers) and builds order variants that sort the keyed
   members of each container interior by that position (cross or flow axis, ascending or descending, partners
   taken from their own boxes or from the boxes that stand for them where the connector's two ends meet).
3. Each variant is applied through the internal `OrderRanks` option (`LayoutProperty<IReadOnlyDictionary<
   LayoutGraphNode, double>>`, a per-node ordering hint keyed by the caller's nodes). The sized view translates
   the keys onto its copies, the nested scopes inherit it, and the leaf algorithms use it only as a tie-break for
   choices they would otherwise make by input order (the order of components, of a component's members and of the
   initial order within a layer), so a rank never overrides a crossing reduction and a graph without ranks lays
   out exactly as before.
4. Variants are first scored on a placement-only pass (the scope placed, its cross-container connectors replaced by
   straight segments between the endpoint boxes); only the best-scored ones are routed for real.
5. A routed proposal is accepted only when it strictly lowers the crossing count, or keeps it and strictly
   shrinks the canvas, and does not open a larger empty square (`EmptySpaceMeter`) beyond the diagram-quality
   limit and one occupancy-grid cell, and keeps the route quality (`KeepsRouteQuality`: length and bends within
   1.25 times of the first pass and a visual cost no more than the first pass plus 250 per crossing removed).
   Removing five or more crossings may open an empty square up to 10% larger. The first pass, laid out in the
   caller's insertion order, is the baseline, so the result is never worse than it.

The work is bounded: at most three rounds after the first pass, the first laying out four proposals and the
others one each, so at most seven routed layouts of one scope (`MaxLayoutsPerRefinedScope`); a scope whose routing
is expensive (cross-container connectors times boxes of at least 150) is refined in one round with at most two
proposals that the placement-only score predicts to be no worse. Interior layouts are memoized per container and
rank signature, so a proposal that does not change a container's subtree never lays that container out again,
and a scope with no cross-container connector that has a nested endpoint inside a container of at least two
members is returned exactly as the first pass produced it. The refinement state (memo and counters) travels in an
internal option so a run never shares state with another run, and the result is deterministic.

The router receives the interior lines of the container ancestors of an edge's endpoints
(`RoutingProfile.ExistingLinesFor`), so a cross-hierarchy connector avoids crossing connectors drawn inside the
containers it enters when an alternative exists (see *ConnectorRouter Unit Design*).

#### Container tightening

A container used to carry its child scope's canvas plus an extra 20 pixels of padding on every side. Each
container interior is now trimmed to its content (`CanvasFitter.TrimToContent`, keeping 24 pixels between a
connector bend and the border) and the container is the trimmed interior plus `ContainerPadding` (12 pixels)
and the title band.

- **Route-quality gate (per scope).** `LayoutScopePass` lays a routed scope out tight (with a comfort fallback
  that restores the former room for a container a routed connector enters when a connector hugs its border), then
  once more with every container of the scope and of all scopes below it loose (the former layout, carried by the
  internal `LooseContainerNodes` option). The tight layout is kept only when `KeepsTightening` holds: crossings,
  hairpins, bends, close parallel runs (16 pixels) and connector length are no worse and no decorated end is
  crowded by a container border. Otherwise the layout with only the containers a routed connector enters (at any
  depth) loose is tried, and otherwise everything is loose. The decision is remembered per scope
  (`SettledLooseNodes`), so the placement-only passes of the ordering refinement see the same sizes.
  Compaction is therefore never bought with a worse route: where tightening worsens routes (for example
  deeply nested packages with parallel connectors) the former container room is kept.
- **Entry arrowhead room.** A tightened container that a decorated connector enters keeps
  `RenderedAlongLineLength(marker) + CleanLegMargin` (15.37 + 1 = 16.37 pixels for an arrowhead) between its
  border and the boxes inside instead of 12 pixels (`EntryPaddings`, the internal `ContainerPaddings` option),
  so an end marker never overlaps a container border.
- **Canvas padding.** The scope's final `CanvasFitter.Fit` is called with the padding its leaf output kept
  (`MeasureMargin`: the layered 20 pixels, or a containment leaf's own 12), so the canvas hugs the content with a
  uniform margin for every flow direction.

Tightening applies only to scopes laid out by the layered or containment leaf algorithms; boundary-port scopes
keep their combined-pass sizes.

#### Routing work budget (Rendering#75)

The cost of routing one scope grew faster than the cube of its cross-container connector count: every connector
is searched many times (face pairs, re-routing passes, the wide-lane re-run), every scope is laid out several times
(tight, loose, reference and middle layouts, ordering proposals), and the grid of each search grows with every
obstacle and routed line. A package of 40 connectors over six folders did not finish in minutes. The exact
speed-ups of the router (see *OrthogonalEdgeRouter Unit Design*) cut the cost of the same searches by a factor of
four to five without changing a coordinate; they cannot bound it, so each scope also carries a deterministic
work budget, and a scope too large for the full effort is routed at a reduced effort whose cost grows slowly
(measured below):

- **One budget per scope.** `LayoutScope` creates a `RoutingWorkBudget` for every scope that has a container and
  carries it to every pass over that scope in its effective options (`RoutingBudget`); a nested scope creates its
  own. All passes of the scope — tight, loose, reference, middle, and every ordering proposal — spend the one
  budget, so the passes made last are the first to be reduced and a heavy scope can neither starve nor be starved
  by another. The limits come from `DefaultRoutingWorkLimits`.
- **Weight.** A routed pass tells the budget the scope's *weight*, the number of connectors routed times the number
  of nodes under the scope; from the weight limit (600) the scope starts in the reduced tier, because spending part
  of the budget to find out that a 40-connector scope is too big only delays the same outcome. The weight of every
  benchmark scene is at most 364; a scope of ten connectors over six folders of five leaves weighs 420 and is still
  routed in full.
- **Work.** The router charges grid nodes built, states expanded and connectivity-sweep nodes visited. At the soft
  limit (80 million units, roughly 8 to 17 seconds on a development machine at the measured 95 to 210 nanoseconds per
  unit, and more on a loaded or instrumented one) the scope drops to the reduced tier; at the hard limit (160 million)
  to the minimal tier. The limits bound the work of one scope and one budget, not of a layout: each scope-pass of a
  nested hierarchy gets a fresh budget, so a hierarchy of many heavy scopes spends their sum (a hierarchy of four
  sub-packages of three folders spent 280 million units over 364 routed passes with no scope above 30 million, and took
  35 to 41 seconds; the route-pass cache and the lean nested scopes below remove most of that work, the cache without
  changing a coordinate).
  That is a known limitation of the per-scope budget, not changed here (the benchmark scenes and the
  gallery must stay byte-identical); a budget for the whole layout is a possible follow-up. The heaviest benchmark scene
  needs about 53 million units, so no benchmark scene is ever limited (`LargeScopeLayoutTests` asserts that none of
  them carries the reduced-effort warning, so a future change that makes routing dearer is noticed).
- **Effect of the tier on the scope.** In the reduced tier the scope is not refined (`RefineCrossOrdering` returns the
  first pass), the reference and middle passes of `LayoutScopePass` are not made, and `RouteBatch` sheds the searches
  listed in *ConnectorRouter Unit Design*; the router also limits the soft lanes of every search (see
  *OrthogonalEdgeRouter Unit Design*).
- **One routed pass for a size-forced scope.** A scope that the weight limit alone put in the reduced tier
  (`RoutingWorkBudget.ForcedReduced`) is laid out and routed once. Normally the first pass tightens every
  container to its content, and a second pass gives the containers whose border a connector hugged their room back;
  a size-forced scope keeps that room from the start for every container a routed connector enters
  (`LayoutScopePassOnce` adds them to the loose set right after `NoteScopeWeight`) and skips the second pass, which
  would only repeat the dearest routing of the scope. On the whole-model view of the OMG Annex A model this gives a
  drawing identical to the two-pass one at half the routed passes of the scope. The same holds for a scope whose
  *first* pass spent the soft limit (its budget is no longer at the full tier): the second routed pass for the
  connectors that hugged a border is made only while `RoutingBudget.Tier` is `Full`, the same test that skips the
  reference and middle passes, so a scope that is already running its work down does not start its dearest pass.
- **The scopes inside an oversize scope inherit the reduction.** The drawing of a size-forced scope is one
  oversize diagram, so the effort is a property of the whole drawing: `LayoutScope` calls
  `RoutingWorkBudget.InheritReduced()` on the budget of a scope whose enclosing scope's budget is `ForcedReduced`
  (the enclosing budget is still in the options the scope inherited), and the inheritance carries on to every
  scope below. Such a scope is reduced exactly like any reduced scope (no refinement, no reference or middle
  pass, one routed pass, the reduced router) but the reduction is *not its own limit*: the budget keeps the tier
  its own size and work allow (`OwnTier`, `OwnLowestTier`) apart from the inherited one. Without the
  inheritance the full-effort inner scopes of the whole-model view took 29 % of its layout time.
- **Hint.** `OrderingRefinementContext.RecordRoutingBudget` records every scope whose *own* tier left the full
  tier (and how many connectors took the elbow route); the root `ApplyCore` appends one line per such scope to
  `LayoutTree.Warnings`, which `AutoLayoutAlgorithm`, the tree packer and `CanvasFitter` preserve. A scope that
  only inherited the reduction records none; the hint of a size-forced scope says that it holds for the scopes
  inside it too. A caller that prints the warnings tells the user that part of a large diagram was routed more
  simply. (The SysML2Tools General view builds its own warnings from the placed tree and does not forward these
  hints today.)

Determinism: the budget never reads a clock, the work units are counts of grid nodes and states, and the tier is a
function of the counters, so the same graph produces the same routes and warnings on every run and platform.

*Performance design properties.* The properties below are design properties, not requirements: no test asserts a
time or an amount of work (a timing assertion fails on a loaded machine and proves nothing about the drawing). They
are measured by the opt-in benchmark `benchmarks/DemaConsulting.Rendering.Layout.Benchmarks` (`pwsh ./benchmark.ps1`;
see *Performance verification* in the *HierarchicalLayoutAlgorithm Verification* document), which prints the time per
scene and the growth exponent between doublings, and the numbers are recorded here. On the whole-model general view of
the OMG Annex A SimpleVehicleModel (one scope of 87 routed
connectors over about 370 descendants, laid out twice by the SysML2Tools general view) the layout fell from 9.8 s
to about 1.6 s, and `sysml2tools render --auto` of the whole model from 33.7 s to about 13 s (of which about 8 s
are loading the model). The benchmark's synthetic package of six folders of five leaves with E cross-container
connectors, and its nested packages, measured as the minimum of the measured layouts over three runs of the benchmark
(Release, net10.0, Windows x64, 28 logical processors that other jobs kept about half busy, so every time is
inflated and noisy):

| Scene | Connectors drawn | Time (s) |
| --- | --- | --- |
| E = 5 | 29 | 0.96 |
| E = 10 | 34 | 1.5 |
| E = 20 | 44 | 0.01 |
| E = 40 | 64 | 0.05 |
| E = 80 | 104 | 0.15 |
| E = 160 | 184 | 0.38 |
| E = 320 | 344 | 1.5 |
| E = 640 | 664 | 4.8 |
| Nested, small (2 sub-packages) | 16 | 0.21 |
| Nested, oversize (80 package connectors) | 140 | 0.18 |
| Nested, everyday (4 sub-packages of 3 folders of 4 leaves) | 66 | 4.4 (12 before the lean tier) |

The time of the full effort (E up to 10) is seconds and grows quickly with the connector count; from 16 connectors
the scope is routed at the reduced effort and the time drops to milliseconds, then grows by 2.5 to 4 times per
doubling of E: about E<sup>1.7</sup> from 80 to 640 connectors on this loaded machine (the exponent between two
doublings ranges from 1.3 to 2.0 with the load; on a quiet moment single runs gave about E<sup>1.5</sup>). That is well
below the cube it was (and below a quadratic), but it is **not linear**. The remaining terms that grow faster than
linearly are that each search weighs every routed line in its cost and that the window around a connector holds more
hard obstacles as the scope spreads over a larger canvas. (During development the work units the budget counts were
recorded once with a temporary counter: 0.45, 1.3, 3.2, 9.6 and 24.6 million units for E = 40 to 640. The product no
longer exposes them, so the benchmark reports time.) The times are for a development machine; on a CI agent that runs
the four runtimes of the test project at once with coverage the same layouts take more than ten times as long, which
is the reason no test asserts a time.

How the releases compare. The everyday nested package (four sub-packages of three folders of four leaves, six inner and
six package connectors; the shape of a package model of moderate size) is where the cost of a hierarchy shows. One
layout in a fresh process took, on the same machine and at similar load (indicative, one run each):

| Scene | beta.5 | Before the route-pass cache (20c6ab7) | With the route-pass cache | With the lean tier |
| --- | --- | --- | --- | --- |
| E = 5 | 5.6 s | 2.7 s | 1.7 s | 1.7 s |
| E = 10 | 9.4 s | 3.7 s | 2.3 s | 2.3 s |
| E = 20 | 189 s | 0.25 s | 0.17 s | 0.17 s |
| E = 40 | more than 180 s | 0.39 s | 0.27 s | 0.27 s |
| E = 640 | not finishing | 4.6 s | 2.8 s | 2.8 s |
| Nested, 2 sub-packages of 3 folders of 4 leaves, 3 package connectors | 64 s | 24 s | 3.9 s | 3.2 s |
| Nested, everyday | 176 s (115 to 369 s over several runs) | 35 s | 7.1 s | 3.9 s |

The geometry of E = 5 and 10 and of the nested scenes is identical in all four columns (the fingerprints are equal);
from E = 16 beta.5 draws without the reduced effort and takes minutes.

The everyday nested package is timed several times in this document, each at a different stage and machine load: 7.1 s
is the stage with the route-pass cache alone (the "7 s" of the *Route-pass cache* section); 3.9 s is the final stage,
the lean tier, in the release comparison above, taken as one run at the load of that comparison; and 4.4 s is the same
final stage as the minimum of three benchmark runs in the table before it, while other jobs kept the machine about half
busy (a later single benchmark run gave 4.25 s). They are the same code at different loads; compare times only within
one table.

#### Route-pass cache

A scope is laid out and routed many times in one run: its own tight, loose, reference and middle layouts and ordering
proposals, and again for every layout of the scopes that hold it, because each parent layout that hands a child a new
order or room hint makes the child's whole pipeline run again, with its own refinement. Most of those layouts reach the
router with composed boxes identical to an earlier one: of the 364 routed passes of a package of four sub-packages of
three folders of four leaves, only 54 had a distinct input. Routing the same problem again made the cost the product of
the pass counts of the levels. The cache (`HierarchicalLayoutAlgorithm.RoutePassCache.cs`, one instance per layout run
in `OrderingRefinementContext.RoutePasses`) replays the earlier answer:

- **Key.** The scope (by reference; its edges, endpoints and classification are a function of it), the
  `EdgeRouting` style, the assumed font size, and a SHA-256 digest of every public property of every composed box at
  any depth (the lines already inside the boxes included, down to each waypoint's exact bits). The digest walks the
  public properties of the layout model's records by reflection, so a property added to the model later is part of the
  key without anyone having to remember it; an object it cannot look into (a graph node or port a layout node refers to)
  is told apart by identity.
- **Exactness.** Routing is a pure function of those inputs while the scope's budget is at the `Full` tier, so a pass is
  stored only when it started and ended at `Full` (the work it charged left the budget below both limits), and is
  replayed only when `RoutingWorkBudget.WouldStayFull` holds for that same work. A replay charges the work the stored
  pass charged, so the budget's `Spent`, every tier decision and every hint that follows from it are the same with or
  without the cache. Nothing else of the pass has a side effect that later code reads (the lines are immutable records;
  the replay returns a new list of them; the wall-hugging containers the pass found are added to the caller's set).
  A pass at the `Reduced` or `Minimal` tier is neither stored nor replayed, because its lines depend on how much work
  was already spent.
- **Limits.** At most 2,048 passes are kept per run; beyond that routing simply runs, which is exact too. The key
  digest costs a few milliseconds per pass against seconds of routing.
- **Effect.** The package of four sub-packages of three folders of four leaves (65 nodes, 66 connectors) falls from
  35 s to 7 s in a Release build on the reference machine (280 million units of work to 50 million really searched);
  the 2 and 3 sub-package variants fall by a factor of four to five. The geometry is identical: the fingerprints of
  the synthetic and nested scenes, the 107 files of the gallery and the seven Annex A views are byte-for-byte what
  they were without the cache.

#### Lean nested scopes

The route-pass cache removes the repetition that is exact; the rest of the cost of a hierarchy is the product of the
pass counts of its levels, which is not exact repetition. A scope that routes connectors lays itself out up to seven
times (tight, loose, reference and middle layouts, ordering proposals), and every one of those layouts lays each of
its sub-scopes out again with all of *their* extra passes; a hierarchy of depth *d* costs about 7<sup>d</sup> routed
passes of its innermost scope. The everyday nested package spent 364 routed passes in 42 layouts of its sub-packages.
A scope that routes connectors itself and holds a hierarchy of inner scopes whose summed routing weight reaches
`RoutingWorkLimits.NestedLeanWeight` (100; the weight of a scope is its routed connectors times the nodes under it,
the measure of `ReducedWeight`) keeps the full effort for itself and has the scopes inside it laid out *lean*:

- **What lean means.** `RoutingWorkBudget.Lean`. A lean scope is routed with the full-effort router (its tier stays
  `Full`, so it raises no warning and no hint) in **one routed pass**: the containers a routed connector enters keep
  their room from the start (the `roomy` rule of a size-forced scope), there is no second pass for the connectors that
  hugged a border, no reference or middle layout (`LayoutScopePass`), and no ordering refinement
  (`RefineCrossOrdering`). The scopes inside a lean scope are lean too (`LeansInnerScopes` flows down through the
  options like `ForcedReduced` does), and a size-forced or inherited-reduced scope is not lean: the reduced tier already
  does less.
- **The decision depends on the input only.** `RoutingFootprintOf` derives the routed connectors, the weight and the
  nested weight of a scope from its graph by the same classification the layout pass uses, once per scope per run
  (`OrderingRefinementContext.Footprints`), before any inner scope is laid out. A scope whose edges cannot be
  classified (a named-port edge across a container boundary) counts as routing nothing; the layout reports it itself.
  The top scope of a model that routes nothing itself (a single root folder) does not make its children lean; the
  package scope that holds the sub-packages' connectors does.
- **The trade-off (a product decision).** Nested scopes of a heavy hierarchy no longer get the ordering refinement
  and the extra layouts, so their interior order and room are not tuned against the connectors the enclosing scope
  routes. Measured against the full effort on nested packages of one to four sub-packages (the crossings of the
  whole drawing, counted once with a temporary harness; *same* means the fingerprint is equal, byte for byte). A
  scene is written S x F x L / I / T: S sub-packages of F folders of L leaves, I connectors inside each sub-package,
  T connectors in the package scope:

  | Scene (S x F x L / I / T) | Crossings, full effort | Crossings, lean | Geometry |
  | --- | --- | --- | --- |
  | 2 x 2 x 3 / 3 / 3 | 7 | 7 | same |
  | 1 x 3 x 4 / 6 / 0 | 3 | 3 | same |
  | 2 x 3 x 4 / 3 / 3 | 10 | 10 | same |
  | 2 x 3 x 4 / 6 / 3 | 12 | 12 | same |
  | 3 x 3 x 4 / 6 / 4 | 15 | 15 | same |
  | 4 x 3 x 4 / 6 / 6 (the everyday package) | 29 | 29 | same |
  | 2 x 3 x 3 / 6 / 3 | 17 | 17 | canvas 630 x 2402, was 610 x 2482 |
  | 2 x 3 x 6 / 6 / 3 | 18 | 19 | different |
  | 2 x 4 x 4 / 6 / 3 | 16 | 18 | different |

  So the drawings of the shapes most models have are byte-identical, the others differ by at most two crossings and
  one by canvas size. The reward is the cost: the everyday package falls from 7.1 s with the cache alone to 3.9 s (35 s
  before the cache; the times of the release comparison above, taken at one load), and the two sub-packages of three
  folders of six leaves from 70 s before the cache to 3 s.
- **Where it never applies.** No scene of the gallery or of the layout-quality benchmark set has a scope that routes
  connectors nested inside another that does, so the 107 gallery files and `metrics.md` are byte-identical with the
  lean tier; the OMG Annex A views' package scopes are size-forced to the reduced tier (their inner scopes inherit
  it), and the seven views are byte-identical too. A hierarchy below the weight (the 2 sub-package scene with inner
  connectors of 2 folders of 3 leaves weighs 60) keeps the full effort everywhere.
- **Reverting it.** The behavior is one constant: `NestedLeanWeight` in `DefaultRoutingWorkLimits`. With the default
  `long.MaxValue` of `RoutingWorkLimits` (no inner scope is ever lean) the layout is the one the route-pass cache alone
  produces; the whole tier is one commit of the history and can be reverted alone.

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
  accounts for them through layout-only proxy edges. Sharing one layout pass across container boundaries (ELK's
  `INCLUDE_CHILDREN`) remains future work (see ROADMAP.md); the cross-hierarchy ordering refinement and the
  interior-line crossing cost described above deliver part of its benefit without a joint pass.

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
  no-boundary-port sibling-gap widening pass reserves, `CanvasFitter` (internal engine helper) to keep
  every connector and connector label inside each scope's canvas (and to trim container interiors and
  size the canvas with the padding the leaf output kept), `RouteQuality` (internal engine helper) to compare
  routed layouts by length, bends, hairpins and close parallel runs, `EmptySpaceMeter` (internal engine
  helper) to keep the ordering refinement from opening a larger empty block, and `HierarchyMergeRegionBuilder`,
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
| Rendering-Layout-HierarchicalLayout-AppearancePassThrough | Sized view (step 3), appearance copy |
| Rendering-Layout-HierarchicalLayout-FolderTabTitleReserve | Data model, `ResolveTitleHeight` in-tab rule |
| Rendering-Layout-HierarchicalLayout-IsolatedNodesNoOverlap | Per-scope leaf layout, on-screen component extents |
| Rendering-Layout-HierarchicalLayout-ContentInsideCanvas | Canvas fit of each scope result, `CanvasFitter.Fit` |
| Rendering-Layout-HierarchicalLayout-LayerSpacingCascades | Layer spacing paragraph |
| Rendering-Layout-HierarchicalLayout-OrderRanks | Ordering refinement, step 3 (`OrderRanks` hint) |
| Rendering-Layout-HierarchicalLayout-CrossHierarchyOrdering | Ordering refinement, steps 1 to 5 |
| Rendering-Layout-HierarchicalLayout-OrderingBounded | Ordering refinement, bounded work and determinism |
| Rendering-Layout-HierarchicalLayout-InteriorConnectorCrossingCost | `CountCrossings`, router interior lines |
| Rendering-Layout-HierarchicalLayout-ContainerTightening | Container tightening, trimmed interior |
| Rendering-Layout-HierarchicalLayout-TighteningRouteQualityGate | Container tightening, `KeepsTightening` gate |
| Rendering-Layout-HierarchicalLayout-EntryArrowheadRoom | Container tightening, entry arrowhead room |
| Rendering-Layout-HierarchicalLayout-CanvasPadding | Container tightening, canvas padding |
| Rendering-Layout-HierarchicalLayout-ReducedEffortHint | Routing work budget: weight, tiers, hint |
| Rendering-Layout-HierarchicalLayout-LargeScopeLayout | Routing work budget; route-pass cache |
