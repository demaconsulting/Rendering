## DefaultLayout Unit Design

Part of the Rendering Layout system.

### DefaultLayout Purpose

`LayoutAlgorithms` and `LayoutEngine` form the batteries-included happy path: the smallest possible way
to lay out a graph with the algorithm it declares. `LayoutAlgorithms` is a factory for a
`LayoutAlgorithmRegistry` pre-populated with the three bundled algorithms; `LayoutEngine` is a thin
facade that resolves the declared algorithm and applies it. Together they turn "lay out my graph with
whatever algorithm it declares" into one call that correctly handles both flat and nested graphs, with
no registry assembly or engine choice required of the caller. Both units are additive: they compose the
existing algorithms and change no existing behavior.

### DefaultLayout Data Model

Both units are static and hold no per-call state. `LayoutAlgorithms.CreateDefaultRegistry()` builds a
fresh `LayoutAlgorithmRegistry` and registers `LayeredLayoutAlgorithm` (`"layered"`),
`ContainmentLayoutAlgorithm` (`"containment"`), and `HierarchicalLayoutAlgorithm` (`"hierarchical"`),
returning a new, independently mutable instance on each call. `LayoutEngine` exposes the
`DefaultAlgorithmId` constant (`"hierarchical"`) and holds one private static `LayoutAlgorithmRegistry`
built once from `CreateDefaultRegistry()`; because the bundled algorithms are stateless, that shared
registry is safe to read (resolve) concurrently.

### DefaultLayout Methods

`LayoutEngine.Layout(graph)` resolves against the shared default registry;
`LayoutEngine.Layout(graph, registry)` resolves against a caller-supplied registry. Both reject
null arguments with `ArgumentNullException`, then:

1. **Resolve the algorithm identifier.** The identifier is read from an explicit `CoreOptions.Algorithm`
   set directly on the graph, else `DefaultAlgorithmId` (`"hierarchical"`). Resolution consults an
   *explicit* graph setting only (via `TryGet`), so an unset graph falls through to the hierarchical
   default rather than the `CoreOptions.Algorithm` property default of `"layered"`. The graph is the
   single place to configure a layout — since `LayoutGraph` is itself an `IPropertyHolder` — so there is
   no second, free-standing options object at this entry point that could disagree with it.
2. **Resolve and apply.** The identifier is resolved from the registry and the resolved algorithm's
   `Apply(graph, options)` produces the placed `LayoutTree`, where `options` is an empty `LayoutOptions`
   used only to seed the algorithm's internal option-cascading contract (see the respective algorithm's
   Unit Design document); the graph itself already carries every explicit setting.

Defaulting to the hierarchical engine is what lets the single facade serve both flat and nested graphs.
It is safe because of the hierarchical engine's flat-graph equivalence guarantee: for a graph with no
container nodes the engine returns output byte-for-byte identical to the selected leaf algorithm
(default `"layered"`) applied directly. A flat graph therefore lays out exactly as the layered algorithm
would, while a nested graph is composed correctly — with no decision required from the caller.

### DefaultLayout Design Constraints

- The factory shall live in the Layout package, not in Abstractions, because it references the concrete
  bundled algorithms; the `LayoutAlgorithmRegistry` it populates remains in Abstractions. This keeps the
  dependency direction intact (model &lt;- Abstractions &lt;- Layout).
- The facade shall default to the hierarchical engine, not the layered algorithm, so one entry point
  handles both flat and nested graphs; the flat-graph equivalence guarantee makes this behavior-
  preserving.
- The facade shall consult only an explicit algorithm declaration on the graph when resolving, so an
  unset graph reaches the hierarchical default rather than the layered property default.

### DefaultLayout Error Handling

Null `graph` or (two-argument overload) `registry` throw `ArgumentNullException`. A
declared algorithm identifier absent from the resolving registry surfaces the registry's
`KeyNotFoundException`.

### DefaultLayout Dependencies

`LayoutAlgorithms` and `LayoutEngine` depend on the following items:

- **Rendering.Abstractions** (`LayoutAlgorithmRegistry`, `ILayoutAlgorithm`) — the registry type
  populated by `CreateDefaultRegistry` and the algorithm contract resolved and invoked by
  `LayoutEngine.Layout`.
- **Rendering model** (`DemaConsulting.Rendering`) — the `LayoutGraph` and
  `LayoutTree` types on the public `Layout` signature, plus `CoreOptions.Algorithm` used for
  algorithm-identifier resolution.
- **Layout units** (`LayeredLayoutAlgorithm`, `ContainmentLayoutAlgorithm`,
  `HierarchicalLayoutAlgorithm`) — the three bundled algorithms registered by the default registry.
  See the respective Unit Design documents.

No OTS runtime component or shared package is consumed.

### DefaultLayout Callers

`LayoutAlgorithms` and `LayoutEngine` are consumed by:

- **External application code** — the primary caller. Applications invoke `LayoutEngine.Layout(graph)`
  (or the two-argument overload with a custom registry) as the batteries-included happy
  path for going from `LayoutGraph` to placed `LayoutTree` with a single call.
- **Renderer host code** (for example downstream of `SvgRenderer` / `PngRenderer`) — callers that
  pair `LayoutEngine.Layout(...)` with an `IRenderer` to go from graph to rendered output in two
  calls.
- **Test host code** — the `LayoutAlgorithms.CreateDefaultRegistry()` factory is also consumed
  directly by tests that need a pre-populated, independently mutable registry.

No other Rendering.Layout unit calls into DefaultLayout; the dependency direction is always
application → `LayoutEngine` → bundled algorithms.

### DefaultLayout Interactions

`LayoutAlgorithms` depends on `LayoutAlgorithmRegistry` and the three bundled algorithm units.
`LayoutEngine` depends on `LayoutAlgorithms`, `LayoutAlgorithmRegistry`, `LayoutGraph`, `LayoutOptions`,
`LayoutTree`, and `CoreOptions`. Callers typically pair `LayoutEngine.Layout(...)` with an `IRenderer`
(for example `SvgRenderer`) to go from graph to rendered output in two calls.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-DefaultRegistry-BundledAlgorithms | DefaultLayout behavior described above |
| Rendering-Layout-LayoutEngine-DefaultAlgorithm | DefaultLayout behavior described above |
| Rendering-Layout-LayoutEngine-Resolution | DefaultLayout behavior described above |
| Rendering-Layout-LayoutEngine-FlatEquivalence | DefaultLayout behavior described above |
| Rendering-Layout-LayoutEngine-NestedComposition | DefaultLayout behavior described above |
| Rendering-Layout-LayoutEngine-CustomRegistry | DefaultLayout behavior described above |
| Rendering-Layout-LayoutEngine-Validation | DefaultLayout behavior described above |
