## LayeredLayoutAlgorithm Unit Design

Part of the Rendering Layout system.

### LayeredLayoutAlgorithm Purpose

`LayeredLayoutAlgorithm` is the public `ILayoutAlgorithm` implementation and the system's product
boundary. It arranges an input `LayoutGraph` into Sugiyama layers, routes edges orthogonally, and
produces a placed `LayoutTree` of boxes and connectors. It wraps the reusable layered pipeline via
`InterconnectionLayoutEngine`.

### LayeredLayoutAlgorithm Data Model

The class is stateless and sealed. It exposes the `AlgorithmId` constant (`"layered"`) and returns it
from the `Id` property, the stable identifier under which the algorithm is selected and registered.
Its single behavior is `Apply(LayoutGraph graph, LayoutOptions options)`, which returns a
`LayoutTree` carrying the total width and height and a flat list of `LayoutNode` items (`LayoutBox`
per node followed by `LayoutLine` per edge).

### LayeredLayoutAlgorithm Methods

`Apply(graph, options)` rejects null `graph` or `options` with `ArgumentNullException`, then:

1. **Index mapping.** Assigns each `LayoutGraphNode` a positional index and builds the
   `LayerNode` array from node sizes and shape metadata (`Shape`, `RoundedCornerRadius`,
   `FolderTabWidth`, `FolderTabHeight`, `Label`, and the real, never-swapped `RealWidth`/`RealHeight`).
   The pipeline's `PortDistributor` and `LongEdgeJoiner` stages read this metadata for a
   non-`BoxShape.Rectangle` node, so `Shape` now influences same-scope connector port placement and
   endpoint surface projection, not only the placed box's rendered outline: a `BoxShape.Folder`
   package's tab is excluded from its connectable extent and the routed connector's endpoint is
   projected inward to the recessed body outline, and a `BoxShape.Note`'s folded corner is similarly
   excluded, matching `ConnectorRouter`'s existing shape-geometry rules for cross-container edges.
2. **Edge mapping.** Maps each edge to an index pair, dropping any edge that references a node
   outside this graph.
3. **Direction resolution.** Resolves the requested flow direction from `CoreOptions.Direction`,
   taking an explicit value on the graph in preference to one on the options and falling back to the
   property default (`Right`), then maps the public `LayoutFlowDirection` to the engine's internal
   direction. This mirrors how the algorithm resolves its other well-known options.
4. **Node-spacing resolution.** Resolves the requested minimum same-layer node gap from
   `CoreOptions.NodeSpacing` using the same graph-then-options-then-default precedence as direction
   resolution, and passes the resolved value to `InterconnectionLayoutEngine.Place`. The property's
   default matches the engine's original fixed constant, so an unset option reproduces the algorithm's
   prior behavior exactly.
5. **Placement (pass 1).** Calls `InterconnectionLayoutEngine.Place` with the resolved direction and
   node spacing to obtain the `LayerResult`. For a downward or upward flow the engine transposes the
   layout so the layers progress top-to-bottom (or bottom-to-top); `Right` is the default and is
   byte-identical to the original left-to-right placement.
6. **Box emission (provisional).** Emits one `LayoutBox` per input node, in input order, at the
   placed rectangle, carrying the node label and its auto-computed `ContentInset*` margins (step 9
   below). If step 10's auto-grow floor determines any node's caller-supplied size is insufficient,
   this emission is discarded and placement/emission re-runs (pass 2) against the grown sizes.
7. **Parallel-edge resolution and route lookup.** Resolves `CoreOptions.MergeParallelEdges` (graph
   in preference to options, default `true`) and builds a route lookup keyed by *engine edge
   index* — the acyclic edge's own position in the pipeline's edge list — rather than by
   `(source, target)` node pair, so parallel edges between the same two nodes each recover their own
   distinct routed polyline instead of colliding on a shared dictionary key. `ResolveRoute` returns
   the forward polyline when present, reverses the polyline of a reversed back edge, and otherwise
   falls back to a straight segment between the two node centers (for a self-loop the engine
   dropped) so the connector is still drawn.
8. **Line emission.** When `MergeParallelEdges` is `true`, a first pass groups the caller's original
   input edges by `(source, target)` and emits exactly one `LayoutLine` per group. Its midpoint label
   (and, once per-end names exist, each duplicate edge's own port names) is emitted **only when the
   group contains exactly one raw input edge**; whenever 2+ raw edges collapse into that one line,
   the label is omitted entirely rather than keeping any single surviving edge's label — a reader
   cannot tell which of several collapsed connectors a kept label would have belonged to, so there is
   nothing meaningful to attach it to once the edges are drawn as one line. This also fixes a
   pre-existing latent bug where every original input edge emitted its own stacked `LayoutLine`
   regardless of duplication. When `MergeParallelEdges` is `false`, every input edge is emitted as
   its own independently-routed `LayoutLine` and keeps its own label.
9. **Port emission and content-inset computation.** For each emitted edge whose source or target is
   a `LayoutGraphPort` (not a plain `LayoutGraphNode`), emits a `LayoutPort` at the routed
   connector's resolved anchor waypoint, carrying the port's `ExternalLabel` as its label
   unconditionally (this phase does not yet read `InternalLabel` or distinguish an internal/external
   edge — that is deferred to the hierarchy-aware phase 2), and its `MaxLabelWidth` — computed as
   roughly half the owning box's placed width minus a small clearance — so a renderer can squeeze an
   excessively long port label rather than let it visually overlap the opposite port's label (a flat
   `LayoutPort` has no reference to its owning box, so this bound must be computed here, where the
   box's placed width is known). `ResolveSide` classifies the anchor against the owning node's placed
   rectangle (within a small tolerance) to determine which of the four faces the port glyph occupies.
   For each box, measures port labels via the self-contained `PortLabelWidthEstimator` heuristic and
   `CoreOptions.AssumedFontSize`, then computes `ContentInsetLeft`/`Right` as the widest same-side
   port label's measured width plus a small clearance, and `ContentInsetTop`/`Bottom` as a flat fixed
   height (one text line at `AssumedFontSize` plus padding) — zero on any side with no ports. When the
   node also carries its own title, the top/bottom flat height is widened further (a generous multiple
   of `AssumedFontSize`/`PortLabelClearance`) so the title — whose rendered start position depends
   only on the inset, never on the box's overall height — cannot visually overlap the top/bottom
   port's own rendered label.
10. **Auto-grow floor (never shrinks).** After pass 1 has revealed every node's `ContentInset*`
    values, computes the minimum width/height each node actually needs to fit its title plus its
    reserved insets simultaneously (from `CoreOptions.AssumedFontSize`/`PortLabelClearance`, since
    this algorithm has no `Theme` dependency to draw exact font metrics from) and compares it against
    that node's caller-supplied size. It additionally aggregates, per node and per resolved
    `PortSide`, the total number of connector anchors on that face and whether any of those anchored
    edges carries a midpoint label — unconditionally for every emitted edge endpoint, not only named
    `LayoutGraphPort`s, since the parallel-label-spacing defect also occurs on plain unnamed edges.
    Whenever a face has 2+ anchors and at least one is labeled, the minimum height candidate is
    widened to `ConnectorLabelPlacer.EstimateLabelHeight(assumedFontSize) * (anchorCount - 1) + 2 *
    ConnectorClearance` — the exact inverse of `PortDistributor.DistributePorts`'s own even-spacing
    formula — so that face's anchors end up spaced at least a full label-height apart. A node whose
    caller-supplied size is already large enough (on every computed floor, including this one) is
    left completely unchanged. Otherwise, engine nodes are cloned with `max(caller-supplied, computed
    minimum)` for the undersized dimension(s) and the full placement/packing/spacing pass re-runs
    (pass 2) against the grown sizes, so a grown node never silently overlaps a sibling that was
    positioned relative to its smaller pass-1 footprint. When no node needs growth, pass 2 is skipped
    entirely and the pass-1 result is emitted as-is.

    Straight, evenly-spaced parallel connectors between the same two boxes (for example 3
    independent labeled connectors preserved via `MergeParallelEdges = false`) previously spaced
    each line only `PortDistributor`'s default lane spacing apart, which for a typical box height was
    smaller than a label's own bounding-box height (`ConnectorLabelPlacer.EstimateLabelHeight`);
    every label after the first then collided with an already-placed label and was nudged
    perpendicular to its own line by `ConnectorLabelPlacer`'s fallback pass, visually detaching the
    label from the line it names. This floor grows the node just enough that `PortDistributor`'s own
    even-spacing formula matches `ConnectorLabelPlacer`'s label-height formula, so the placer's
    first-pass (no-nudge) placement succeeds for every label instead, and each label lands directly
    on its own line.

An empty graph yields an empty `LayoutTree` because `InterconnectionLayoutEngine.Place` returns a
minimal-size empty result, which produces an empty canvas.

### LayeredLayoutAlgorithm Interactions

`LayeredLayoutAlgorithm` depends on the `ILayoutAlgorithm`, `LayoutGraph`, `LayoutTree`, and related
model types from `DemaConsulting.Rendering.Abstractions` and on `InterconnectionLayoutEngine` from
the Engine subsystem. It is the entry point resolved by renderers through the layout registry.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-LayeredAlgorithm-Identity | LayeredLayoutAlgorithm behavior described above |
| Rendering-Layout-LayeredAlgorithm-PlacesAndRoutes | LayeredLayoutAlgorithm behavior described above |
| Rendering-Layout-LayeredAlgorithm-EmptyGraph | LayeredLayoutAlgorithm behavior described above |
| Rendering-Layout-LayeredAlgorithm-Direction | LayeredLayoutAlgorithm behavior described above |
| Rendering-Layout-LayeredAlgorithm-NodeSpacing | LayeredLayoutAlgorithm behavior described above |
| Rendering-Layout-LayeredAlgorithm-Validation | LayeredLayoutAlgorithm behavior described above |
| Rendering-Layout-LayeredAlgorithm-MergeParallelEdges | LayeredLayoutAlgorithm behavior described above |
| Rendering-Layout-LayeredAlgorithm-PortEmission | LayeredLayoutAlgorithm behavior described above |
| Rendering-Layout-LayeredAlgorithm-ContentInset | LayeredLayoutAlgorithm behavior described above |
| Rendering-Layout-LayeredAlgorithm-AutoGrowMinimumSize | LayeredLayoutAlgorithm behavior described above |
| Rendering-Layout-LayeredAlgorithm-ParallelLabelSpacing | LayeredLayoutAlgorithm behavior described above |
| Rendering-Layout-LayeredAlgorithm-ShapeAwareRouting | LayeredLayoutAlgorithm behavior described above |
