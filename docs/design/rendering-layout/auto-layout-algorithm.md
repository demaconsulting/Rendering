## AutoLayoutAlgorithm Unit Design

Part of the Rendering Layout system.

### AutoLayoutAlgorithm Purpose

`AutoLayoutAlgorithm` is the bundled auto-routing meta-algorithm. It splits an input graph into its
connected top-level components, routes each component to whichever bundled leaf algorithm best suits
its shape, lays each component out independently, and packs the resulting placed sub-trees into one
combined `LayoutTree` with `LayoutTreePacker`. It lets a caller select `"auto"` once and get a
reasonable layout for a graph whose shape is not known in advance — a mixture of connected clusters,
nested containers, and unrelated singleton nodes — without having to inspect the graph and choose an
algorithm by hand.

### AutoLayoutAlgorithm Data Model

The class is a sealed `LayoutAlgorithmBase` subclass with no per-call state beyond three private,
reused leaf-algorithm instances (`HierarchicalLayoutAlgorithm`, `LayeredLayoutAlgorithm`,
`ContainmentLayoutAlgorithm`, all themselves stateless). It exposes the `AlgorithmId` constant
(`"auto"`) and returns it from the `Id` property. Its single behavior is
`ApplyCore(LayoutGraph graph, LayoutOptions options)`, which returns a `LayoutTree`.

### AutoLayoutAlgorithm Methods

`ApplyCore(graph, options)` rejects a null `options` with `ArgumentNullException` (the base class
rejects a null `graph`), then:

1. **Component detection.** Every top-level node is assigned to a connected component using the
   graph's top-level edges, resolved through a private `TryResolveOwner`-shaped helper that mirrors
   `LayeredLayoutAlgorithm`'s and `HierarchicalLayoutAlgorithm`'s own node/port endpoint resolution.
   The helper is replicated locally rather than extracted into a shared type, since each caller's
   resolution serves a slightly different purpose and all three implementations are independently
   small.
2. **Routing rule.** Each detected component is routed by shape:
   - A component containing any node with `HasChildren` is routed to `HierarchicalLayoutAlgorithm`
     (which recurses further into any nesting on its own), regardless of the component's size — a
     single isolated container node still needs the hierarchical engine to lay out its children.
   - A component with two or more nodes and no children anywhere in it (including a single node
     carrying only a self-loop edge) is routed to `LayeredLayoutAlgorithm`, since it has genuine
     connectivity for the layered engine's Sugiyama layering to exploit.
   - A truly childless, edgeless singleton node carries no connectivity or nesting information a
     layered or hierarchical layout could use, so every such singleton across the whole graph is
     instead gathered into one shared bucket routed through `ContainmentLayoutAlgorithm`, which packs
     unrelated peer boxes into a balanced block.
3. **Fast path: nothing to split.** When the routing rule produces exactly one group overall (either
   a single non-singleton component, or every top-level node is a childless, edgeless singleton), the
   graph is not split at all: it is delegated directly, unchanged, to that one leaf algorithm's
   `ApplyCore`, so the result is byte-for-byte identical to invoking that algorithm directly. This
   mirrors `HierarchicalLayoutAlgorithm`'s own flat-graph equivalence guarantee, and is the common
   case: a single fully (or mostly) connected diagram never pays any splitting or copying cost.
4. **Genuine multi-group split.** When 2+ groups are produced, the algorithm captures the graph's own
   cascaded options once (`graph.OverlayOnto(options)`), then resets the captured `CoreOptions.Algorithm`
   value to `LayeredLayoutAlgorithm.AlgorithmId` before passing it down to each group's leaf algorithm
   call. This reset is required because the captured options otherwise still carry this graph's own
   `Algorithm` value (typically `"auto"` itself, however the caller selected this algorithm), and
   `HierarchicalLayoutAlgorithm` re-reads `CoreOptions.Algorithm` from its own effective options to
   resolve its own top scope's leaf algorithm — `"auto"` is never a registered leaf identifier there, so
   without the reset a hierarchical-routed group throws a lookup error. Resetting it to the layered
   default (the same default `HierarchicalLayoutAlgorithm` itself falls back to when nothing declares
   an override) restores the cascade to exactly what an ordinary caller not using `"auto"` would see.
   Each group is then built into its own freshly-constructed `LayoutGraph` (see Sub-graph Construction
   below), laid out through its routed algorithm, and every resulting `LayoutTree` is packed into one
   combined tree via `LayoutTreePacker.Pack` (see its own Unit Design document).

### AutoLayoutAlgorithm Sub-Graph Construction

When more than one group is produced, each group is laid out on its own freshly-built `LayoutGraph`
rather than a shared view of the original graph: the public `LayoutGraph`/`LayoutGraphNode` API offers
no way to insert an existing node instance into a different graph's node list, and
`LayoutGraphNode.Children` has no setter, so an original node cannot simply be attached, by reference,
to a new parent graph. Every node in a split-off component is therefore copied field-by-field — label,
shape, keyword, compartments, title height, rounded-corner radius, folder-tab dimensions, its named
ports, and (recursively) its entire nested `Children` subgraph — with edges re-added afterward once
every node and port in the component has a copy, so both direct and cross-container edge endpoints
resolve correctly regardless of nesting depth.

**Known, disclosed limitation.** A node's or edge's own arbitrary `PropertyHolder` option overrides
(set with `node.Set(property, value)`, for example a per-node `CoreOptions.Algorithm` override on a
container) are not copied onto a split component's nodes: `PropertyHolder` exposes no generic API to
enumerate or copy an arbitrary set of overrides onto a different instance, and adding one purely to
support this rarely-hit multi-component path was judged out of scope. The graph-level overrides that
matter most in practice are unaffected, since the original graph's own cascaded options are captured
once, before splitting, and passed as the fallback options to every split component's leaf algorithm,
so a graph-level override still applies to every piece exactly as it would have applied to the whole.
Only a node- or edge-level override, specifically on a node that ends up copied into a split
component, would be silently dropped — an edge case a caller can avoid today by preferring graph-level
(or per-scope `Children`-level) overrides over per-node ones when also relying on `"auto"` routing.

An empty graph yields an empty `LayoutTree` because the containment fast path handles the (vacuously
single-group) empty case and `ContainmentLayoutAlgorithm.ApplyCore` returns an empty `LayoutTree` for
an empty node list.

### AutoLayoutAlgorithm Interactions

`AutoLayoutAlgorithm` depends on `LayoutGraph`, `LayoutTree`, and related model types from
`DemaConsulting.Rendering.Abstractions`, and on `LayeredLayoutAlgorithm`, `ContainmentLayoutAlgorithm`,
`HierarchicalLayoutAlgorithm`, and `LayoutTreePacker` (Engine subsystem) from within the Layout
package. It is registered by `LayoutAlgorithms.CreateDefaultRegistry()` alongside the other three
bundled algorithms and is resolved by renderers through the layout registry like any other algorithm.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-AutoAlgorithm-Identity | AutoLayoutAlgorithm behavior described above |
| Rendering-Layout-AutoAlgorithm-RoutesConnectedCluster | AutoLayoutAlgorithm behavior described above |
| Rendering-Layout-AutoAlgorithm-RoutesContainer | AutoLayoutAlgorithm behavior described above |
| Rendering-Layout-AutoAlgorithm-RoutesSingletonBucket | AutoLayoutAlgorithm behavior described above |
| Rendering-Layout-AutoAlgorithm-FastPathEquivalence | AutoLayoutAlgorithm behavior described above |
| Rendering-Layout-AutoAlgorithm-MultiGroupPacking | AutoLayoutAlgorithm behavior described above |
| Rendering-Layout-AutoAlgorithm-CascadesOptions | AutoLayoutAlgorithm behavior described above |
| Rendering-Layout-AutoAlgorithm-EmptyGraph | AutoLayoutAlgorithm behavior described above |
| Rendering-Layout-AutoAlgorithm-Validation | AutoLayoutAlgorithm behavior described above |
