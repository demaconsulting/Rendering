## ContainmentLayoutAlgorithm Unit Design

Part of the Rendering Layout system.

### ContainmentLayoutAlgorithm Purpose

`ContainmentLayoutAlgorithm` is a second public `ILayoutAlgorithm` implementation alongside
`LayeredLayoutAlgorithm`. Where the layered algorithm arranges nodes by their connectivity into
Sugiyama layers, the containment algorithm arranges them by their reading order: it packs the graph's
top-level nodes into rows within a heuristic width budget and then routes each edge around the packed
boxes. It composes the two public building blocks of the system — the `ContainmentLayout` packer and
the `ConnectorRouter` orchestration — rather than the layered pipeline, and suits views whose elements
group as peers inside a container rather than flowing along a directed spine. It is additive: adding it
changes no existing output and leaves the layered algorithm untouched.

### ContainmentLayoutAlgorithm Data Model

The class is stateless and sealed. It exposes the `AlgorithmId` constant (`"containment"`) and returns
it from the `Id` property, the stable identifier under which the algorithm is selected and registered.
Private constants govern the arrangement: `CanvasAspectRatio` (`4/3`) biases the derived content width
toward a landscape block; `NodeSpacing` (`24.0` logical pixels) is the inter-box gap, sized wider than
the router's approach stub so a connector can pass cleanly between two packed boxes; `MinBoxesForColumnEstimate`
(`6`) gates the column-count-based content-width candidate to sets large enough for multi-column packing
to plausibly make sense; and `ColumnEstimateFullWeightSizeRatio` (`2.0`) / `ColumnEstimateZeroWeightSizeRatio`
(`6.0`) bound the graduated falloff that scales that candidate's contribution by how uniform the boxes
are in size (see Methods, below). Its single behavior is `Apply(LayoutGraph graph, LayoutOptions options)`,
which returns a `LayoutTree` carrying the packed region size and a flat list of `LayoutNode` items
(`LayoutBox` per top-level node followed by `LayoutLine` per routed edge).

### ContainmentLayoutAlgorithm Methods

`Apply(graph, options)` rejects null `graph` or `options` with `ArgumentNullException`, then:

1. **Leaf-box conversion.** Converts each top-level `LayoutGraphNode` into a leaf `LayoutBox` at the
   origin (carrying the node's width, height, and label), recording each node's positional index. A
   node's nested `Children` are treated as opaque and are not laid out at this level (that is the
   recursive hierarchical engine's responsibility, not this flat algorithm's).
2. **Content-width heuristic.** Derives the packer's `MaxContentWidth` from the boxes via
   `ComputeContentWidth`: the square root of their total area scaled by `CanvasAspectRatio`, widened to
   at least the widest box, widened further to a column-count-based estimate (`columns = ceil(sqrt(n))`
   sized from each box's own average width) once there are at least `MinBoxesForColumnEstimate` boxes,
   and floored to a small positive value so the packer always receives a usable width — even for an
   empty graph. The column-count-based candidate's contribution is scaled by
   `ComputeColumnEstimateWeight`, a graduated falloff on the largest-to-smallest box-size ratio (the
   greater of the width ratio and the height ratio): full weight at or below
   `ColumnEstimateFullWeightSizeRatio` (`2.0`), zero weight at or above
   `ColumnEstimateZeroWeightSizeRatio` (`6.0`), and a linear interpolation between those bounds for a
   ratio in between — rather than the hard on/off cutoff the candidate originally used. Because the
   candidate is combined via `Math.Max` with the other candidates, it can only ever widen the final
   budget, never narrow it; the graduated falloff exists because that asymmetry means over-applying the
   estimate costs at most some extra whitespace, while under-applying it (the original hard cutoff's
   failure mode) has no bound on how degenerate the resulting single-column layout gets — an ordinary
   box set whose width variance only marginally exceeds the old cutoff (for example differently-labelled
   peer boxes) still receives most of the estimate's benefit instead of losing it outright.
3. **Packing.** Calls `ContainmentLayout.Pack` with the leaf boxes and a `ContainmentOptions` using the
   derived width, the connector-aware `NodeSpacing` on both axes, and an `EdgeCounts` map (see below),
   obtaining the packed boxes and the enclosing region size.
4. **Connection building.** Builds one `Connection` per edge whose source and target are both top-level
   nodes — carrying the edge's `TargetEnd`, `LineStyle`, and `Label` — using the packed box that
   represents each endpoint. Edges referencing a node outside the graph's top-level nodes are skipped,
   mirroring the layered algorithm's handling of out-of-graph endpoints.
5. **Routing.** Routes the connections around the packed boxes via `ConnectorRouter.Route`, selecting
   the routing style with `ResolveEdgeRouting`: the graph's own explicit `CoreOptions.EdgeRouting`
   override takes precedence, then the supplied options' value, falling back to the property's default
   (`Orthogonal`) only when neither declares one — mirroring `LayeredLayoutAlgorithm.ResolveDirection`'s
   graph-then-options-then-default resolution of `CoreOptions.Direction`. This lets a graph-level
   override be honored whether the algorithm is invoked directly or as a scope's leaf algorithm inside
   `HierarchicalLayoutAlgorithm` (which passes each scope's already-cascaded effective options).
6. **Assembly.** Returns a `LayoutTree` with the region `Width`/`Height` and the packed boxes followed
   by the routed lines.

An empty graph yields an empty `LayoutTree` with a positive-size canvas, because the packer returns a
padding-only region for no children and no connections are routed.

**Edge-count-aware horizontal gap widening.** Before packing, the algorithm builds the `EdgeCounts`
map by reusing the same positional-index map recorded during leaf-box conversion: for every edge whose
source and target are both top-level nodes (the same endpoints the connection-building step keeps), it
increments the count for the unordered index pair `(min, max)`. Passing that map to the packer widens
the horizontal gap between two peer boxes packed side by side on the same row to the connector-corridor
width for their edge count — via the shared `EdgeCountGapWidener` helper, whose formula
(`max(baseGap, 2·ConnectorClearance + (n − 1)·EdgeSpacing)`) mirrors the layered pipeline's
`BrandesKopfPlacer` corridor sizing — so a fan of parallel connectors gets distinct lanes instead of
crowding one channel. The widening is horizontal-only by deliberate design decision: the vertical gap
between rows is left untouched so a `Source`-over-`Target` vertical stack (the arrangement of the
existing `parallel-edges-into-compartment-box` gallery diagram) keeps byte-identical box positions,
and the row-wrap decision itself uses the un-widened gap so no wrap point moves. The
`containment-parallel-edges-side-by-side` gallery diagram (two tall, narrow peer boxes joined by eight
parallel edges, sized so the packer places them on one row) is the reproduction scenario that exercises
and visually demonstrates this widening.

### ContainmentLayoutAlgorithm Error Handling

Null `graph` or `options` throw `ArgumentNullException`. Edges with an out-of-graph endpoint are
skipped rather than treated as errors. All other behavior is inherited from the composed
`ContainmentLayout` and `ConnectorRouter` units.

### ContainmentLayoutAlgorithm Dependencies

- `DemaConsulting.Rendering.Abstractions` — `ILayoutAlgorithm` defines the service-provider contract
  this public algorithm implements.
- `DemaConsulting.Rendering` — `LayoutGraph`, `LayoutTree`, `LayoutBox`, `LayoutLine`, and the graph
  node and edge model types carry the input graph and returned placed layout.
- `CoreOptions` and `LayoutOptions` (Rendering model system) — supply the selected edge-routing style
  and the algorithm-selection option that chooses this unit.
- `ContainmentLayout` (same system) — packs the top-level boxes into rows within the derived width
  budget.
- `ConnectorRouter` (same system) — routes the top-level connections around the packed boxes.
- `EdgeCountGapWidener` (same system, internal engine helper) — supplies the shared connector-corridor
  width formula the algorithm uses (through `ContainmentLayout`'s `EdgeCounts` map) to widen the
  horizontal gap between same-row peer boxes; the same helper is reused by
  `HierarchicalLayoutAlgorithm`'s sibling-container widening pass, keeping the three call sites'
  spacing byte-identical.

### ContainmentLayoutAlgorithm Callers

Renderers and other consumers do not construct routing geometry directly; they resolve this unit
through the layout registry under the `"containment"` identifier and select it through
`CoreOptions.Algorithm` when a reading-order containment layout is requested.

### ContainmentLayoutAlgorithm Interactions

`ContainmentLayoutAlgorithm` composes `ContainmentLayout` for packing and `ConnectorRouter` for edge
routing, adapting their combined output to the public `ILayoutAlgorithm` contract.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-ContainmentAlgorithm-Identity | ContainmentLayoutAlgorithm behavior described above |
| Rendering-Layout-ContainmentAlgorithm-PacksNodes | ContainmentLayoutAlgorithm behavior described above |
| Rendering-Layout-ContainmentAlgorithm-RoutesEdges | ContainmentLayoutAlgorithm behavior described above |
| Rendering-Layout-ContainmentAlgorithm-RoutesAroundObstacle | ContainmentLayoutAlgorithm behavior described above |
| Rendering-Layout-ContainmentAlgorithm-EmptyGraph | ContainmentLayoutAlgorithm behavior described above |
| Rendering-Layout-ContainmentAlgorithm-SkipsOutOfGraphEdges | ContainmentLayoutAlgorithm behavior described above |
| Rendering-Layout-ContainmentAlgorithm-Validation | ContainmentLayoutAlgorithm behavior described above |
| Rendering-Layout-ContainmentAlgorithm-HonorsScopeEdgeRouting | ContainmentLayoutAlgorithm behavior described above |
| Rendering-Layout-ContainmentAlgorithm-EdgeCountGapWidening | ContainmentLayoutAlgorithm behavior described above |
| Rendering-Layout-ContainmentAlgorithm-VerticalStackUnaffected | ContainmentLayoutAlgorithm behavior described above |
| Rendering-Layout-ContainmentAlgorithm-CorridorWidthFormula | EdgeCountGapWidener helper described above |
