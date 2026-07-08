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
5. **Placement.** Calls `InterconnectionLayoutEngine.Place` with the resolved direction and node
   spacing to obtain the `LayerResult`. For a downward or upward flow the engine transposes the layout
   so the layers progress top-to-bottom (or bottom-to-top); `Right` is the default and is
   byte-identical to the original left-to-right placement.
6. **Box emission.** Emits one `LayoutBox` per input node, in input order, at the placed rectangle,
   carrying the node label and its auto-computed `ContentInset*` margins (step 9 below).
7. **Parallel-edge resolution and route lookup.** Resolves `CoreOptions.MergeParallelEdges` (graph
   in preference to options, default `true`) and builds a route lookup keyed by *engine edge
   index* — the acyclic edge's own position in the pipeline's edge list — rather than by
   `(source, target)` node pair, so parallel edges between the same two nodes each recover their own
   distinct routed polyline instead of colliding on a shared dictionary key. `ResolveRoute` returns
   the forward polyline when present, reverses the polyline of a reversed back edge, and otherwise
   falls back to a straight segment between the two node centers (for a self-loop the engine
   dropped) so the connector is still drawn.
8. **Line emission.** When `MergeParallelEdges` is `true`, a first pass groups the caller's original
   input edges by `(source, target)` and emits exactly one `LayoutLine` per group, using the first
   surviving edge's label and route and discarding the rest — fixing a pre-existing latent bug where
   every original input edge emitted its own stacked `LayoutLine` regardless of duplication. When
   `false`, every input edge is emitted as its own independently-routed `LayoutLine`.
9. **Port emission and content-inset computation.** For each emitted edge whose source or target is
   a `LayoutGraphPort` (not a plain `LayoutGraphNode`), emits a `LayoutPort` at the routed
   connector's resolved anchor waypoint, carrying the port's `ExternalLabel` as its label
   unconditionally (this phase does not yet read `InternalLabel` or distinguish an internal/external
   edge — that is deferred to the hierarchy-aware phase 2). `ResolveSide` classifies the anchor
   against the owning node's placed rectangle (within a small tolerance) to determine which of the
   four faces the port glyph occupies. For each box, resolves an `ITextMeasurer` (an explicit
   `CoreOptions.TextMeasurer` on the graph, then on the options, else a shared
   `HeuristicTextMeasurer` instance) and `CoreOptions.AssumedFontSize`, then computes
   `ContentInsetLeft`/`Right` as the widest same-side port label's measured width plus a small
   clearance, and `ContentInsetTop`/`Bottom` as a flat fixed height (one text line at
   `AssumedFontSize` plus padding) — zero on any side with no ports.

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
| Rendering-Layout-LayeredAlgorithm-ShapeAwareRouting | LayeredLayoutAlgorithm behavior described above |
