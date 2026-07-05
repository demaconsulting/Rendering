## Layout Graph Unit Design

Part of the Rendering Model system.

### Layout Graph Purpose

The Layout Graph unit's single responsibility is to define the unplaced input model that a layout
algorithm consumes: a `LayoutGraph` container of `LayoutGraphNode` boxes and `LayoutGraphEdge`
connections, each derived from `PropertyHolder` so that graph-wide, node-level, and edge-level
options may be attached anywhere on the input. It supports hierarchical (compound) nesting through a
node's lazily-allocated `Children` graph. The unit performs no layout and no rendering; it is only
the input vocabulary passed to `ILayoutAlgorithm.Apply`.

### Layout Graph Overview

The Layout Graph unit is the unplaced input to a layout algorithm: a set of sized `LayoutGraphNode`
boxes and directed `LayoutGraphEdge` connections. `LayoutGraph` itself derives from `PropertyHolder`,
so graph-wide options may be attached directly to it, and each node and edge also carries its own
property overrides.

The input model is **hierarchical (recursive)**, mirroring the Eclipse Layout Kernel (ELK)
`ElkNode`: a node is either a *leaf* or a *container* (compound node) that owns a nested child
subgraph of further nodes and the edges contained at that level. A `LayoutGraphNode` becomes a
container by populating its `Children` graph, which is an ordinary `LayoutGraph`. Hierarchy is thus
expressed by recursion over a single container type rather than by a distinct nested-graph type: the
`LayoutGraph` a caller creates directly is the top-level (root) container, and each container node's
`Children` is a container nested one level deeper.

The nesting is **additive and behavior-preserving**. The child subgraph is created lazily, so a leaf
node allocates no child graph and a flat (non-nested) graph behaves exactly as it did before nesting
existed. A layout algorithm that reads only the top-level `Nodes` and `Edges` — such as the bundled
`LayeredLayoutAlgorithm` — is unaffected by the presence of the capability; consuming the nesting is
the responsibility of a hierarchical layout engine (a later delivery).

### Layout Graph Data Model

- `LayoutGraph` (sealed class extending `PropertyHolder`) — `Nodes`, `Edges`, `AddNode`, `AddEdge`;
  each instance is a container scope (the root graph, or a node's `Children`).
- `LayoutGraphNode` (sealed class extending `PropertyHolder`) — `Id`, `Width`, `Height`, `Label`,
  `Shape` (the `BoxShape` outline, defaulting to `Rectangle`), `Keyword` (an optional italicized
  keyword line shown above the title), `Compartments` (an ordered, read-only list of
  `LayoutCompartment` feature sections, defaulting to empty), `TitleHeight` (an optional override, in
  logical pixels, of the title band a hierarchical layout engine reserves above this node's children
  when it is a labelled container), `Children` (the lazily-created nested child subgraph), and
  `HasChildren` (whether the node holds at least one child).
- `LayoutGraphEdge` (sealed class extending `PropertyHolder`) — `Id`, `Source`, `Target`,
  `TargetEnd`, `LineStyle`, `Label`.

### Layout Graph Key Methods

`LayoutGraphNode AddNode(string id, double width, double height)` — creates a node with the requested
identity and size, appends it to `Nodes` in insertion order, and returns it for further
configuration.

`LayoutGraphEdge AddEdge(string id, LayoutGraphNode source, LayoutGraphNode target)` — creates a
directed edge referencing the two endpoint nodes, appends it to `Edges`, and returns it. The
`LayoutGraphEdge` constructor rejects a null identifier, source, or target. The endpoints need not be
direct members of the graph the edge is added to, which is what makes cross-container edges
expressible (see the design constraints below).

`LayoutGraph LayoutGraphNode.Children { get; }` — the nested child subgraph, created lazily on first
access, through which a caller adds nested nodes and contained edges (`node.Children.AddNode(...)`,
`node.Children.AddEdge(...)`), reusing the container's identifier-uniqueness and insertion-order
guarantees.

`bool LayoutGraphNode.HasChildren { get; }` — reports whether the node currently holds at least one
child without forcing the lazy allocation, so consumers can distinguish a container from a leaf and
skip empty containers.

`BoxShape LayoutGraphNode.Shape { get; set; }`, `string? LayoutGraphNode.Keyword { get; set; }`, and
`IReadOnlyList<LayoutCompartment> LayoutGraphNode.Compartments { get; set; }` — the node's box
outline, optional keyword line, and ordered feature-section list. Each defaults to a plain rectangle
with no keyword and no compartments, so setting none of them reproduces the pre-existing flat-graph
behavior exactly. Every bundled leaf algorithm (`LayeredLayoutAlgorithm`, `ContainmentLayoutAlgorithm`)
and `HierarchicalLayoutAlgorithm` copy these three properties, unchanged, onto the placed `LayoutBox`
(or view node), so a caller selects the full appearance of a box once, on the input graph, rather
than after layout.

`double? LayoutGraphNode.TitleHeight { get; set; }` — an optional override, in logical pixels, of the
title band `HierarchicalLayoutAlgorithm` reserves above this node's children when the node is a
labelled container. `null` (the default) selects the engine's own generic default band height;
setting it — typically to a theme's own computed title-area height when the container also carries a
`Keyword` — lets the reserved band match what the renderer will actually draw instead of being limited
to the engine's generic default. Ignored for a leaf node (one with no children) and for a container
with no `Label`.

### Layout Graph Error Handling

Invalid inputs are rejected at their entry point rather than deferred to layout time. The
`LayoutGraphNode` and `LayoutGraphEdge` constructors call `ArgumentException.ThrowIfNullOrEmpty(id)`
so that a null or empty identifier throws `ArgumentException` (or `ArgumentNullException` for null),
and `LayoutGraphEdge` additionally calls `ArgumentNullException.ThrowIfNull` on its `source` and
`target`. `LayoutGraph.AddNode` and `LayoutGraph.AddEdge` throw `ArgumentException` when the supplied
identifier is already used within that container scope (the same identifier may be reused in a
different `Children` scope). All exceptions propagate to the caller unchanged; the unit performs no
recovery and no logging. Cross-container endpoint references are not validated — the model
deliberately permits them so that a hierarchical layout engine can resolve cross-container edges in
the appropriate ancestor container.

### Layout Graph Design Constraints

- `AddNode` and `AddEdge` shall preserve caller insertion order in `Nodes` and `Edges`, so that a
  layout algorithm processes elements deterministically.
- `Nodes` and `Edges` shall be exposed as read-only views (`IReadOnlyList<T>`); all mutation shall go
  through `AddNode` and `AddEdge`, so a caller cannot bypass the per-container identifier-uniqueness
  guarantee by appending directly.
- A `LayoutGraphNode` identifier shall be unique within its owning graph, and an empty identifier
  shall be rejected at construction.
- Identifier uniqueness shall be scoped **per container**: each `LayoutGraph` (the root, or any node's
  `Children`) shall enforce its own node- and edge-identifier uniqueness, so an identifier may be
  reused across different scopes but not twice within one scope.
- A leaf node shall allocate no child subgraph, so that adding the hierarchy capability leaves a flat
  graph's structure and layout unchanged; `HasChildren` shall not force that allocation.
- `Shape`, `Keyword`, and `Compartments` shall default to a plain rectangle, no keyword, and no
  compartments respectively, so that adding box-appearance selection to the input graph leaves a
  node's placed appearance unchanged when none of the three are set; a layout algorithm shall copy
  each of the three properties unchanged onto the placed box (or, for `HierarchicalLayoutAlgorithm`,
  onto the corresponding view node) rather than substituting a default.
- `TitleHeight` shall default to `null`, so that a container node reserves `HierarchicalLayoutAlgorithm`'s
  generic default title-band height unless a caller explicitly overrides it; the override shall apply
  only while the container also carries a `Label`, matching the existing label-gated title-band
  behavior.
- An edge shall reside in the container at or above the *lowest common ancestor* (LCA) of its two
  endpoints. An edge whose endpoints live in different descendant containers (a *cross-container*
  edge) shall therefore be added to an ancestor container while its `Source` and `Target` reference
  the descendant nodes directly; the model shall not add membership validation that would forbid such
  cross-scope references. A hierarchical layout engine resolves the routing in the owning container's
  coordinate space.

### Layout Graph Dependencies

The unit depends on the Options unit within the Rendering model (`LayoutGraph`, `LayoutGraphNode`,
and `LayoutGraphEdge` all derive from `PropertyHolder`, and each element carries `LayoutProperty<T>`
overrides). It also consumes the shared notation enumerations `EndMarkerStyle` and `LineStyle`
declared by the Layout Tree unit, on `LayoutGraphEdge`, and `BoxShape` and `LayoutCompartment`,
also declared by the Layout Tree unit, on `LayoutGraphNode`. Outside the Rendering model, the unit
has no project references, no OTS runtime component, and no Shared Package dependency; it uses only
the .NET base class library.

### Layout Graph Callers

Application code constructs a `LayoutGraph` and populates it through `AddNode`, `AddEdge`, and (for
compound diagrams) the recursive `LayoutGraphNode.Children` graph. The populated graph is passed to
`ILayoutAlgorithm.Apply` in `DemaConsulting.Rendering.Abstractions`, which is implemented by the
`LayeredLayoutAlgorithm`, `ContainmentLayoutAlgorithm`, and `HierarchicalLayoutAlgorithm` in
`DemaConsulting.Rendering.Layout`. The bundled `LayeredLayoutAlgorithm` consumes only the top-level
`Nodes` and `Edges`; the recursive `Children` structure is consumed by the hierarchical layout
engine.

### Layout Graph Interactions

A `LayoutGraph` plus a `LayoutOptions` is the input to `ILayoutAlgorithm.Apply` (see the
*Rendering Abstractions* design), which produces a placed `LayoutTree`. Each `LayoutGraphEdge`
carries an `EndMarkerStyle` and `LineStyle` from the Layout Tree unit's enumerations. The bundled
`LayeredLayoutAlgorithm` (see the *Rendering Layout* design) consumes only the top-level `Nodes` and
`Edges`; the recursive `Children` structure is intended for a future hierarchical layout engine.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Model-LayoutGraph-AddNode | `LayoutGraph.AddNode` |
| Rendering-Model-LayoutGraph-AddEdge | `LayoutGraph.AddEdge` |
| Rendering-Model-LayoutGraph-PerElementProperties | `LayoutGraphNode` : `PropertyHolder` |
| Rendering-Model-LayoutGraph-ContainerNodes | `LayoutGraphNode.Children` / `HasChildren` |
| Rendering-Model-LayoutGraph-ScopedIdentifiers | Per-`LayoutGraph` id-uniqueness reused by `Children` |
| Rendering-Model-LayoutGraph-CrossContainerEdge | `LayoutGraphEdge` endpoints referencing descendant nodes |
| Rendering-Model-LayoutGraph-BoxAppearance | `LayoutGraphNode.Shape` / `.Keyword` / `.Compartments` |
| Rendering-Model-LayoutGraph-ContainerTitleHeight | `LayoutGraphNode.TitleHeight` |
