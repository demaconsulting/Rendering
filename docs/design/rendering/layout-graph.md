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
- `LayoutGraphNode` (sealed class extending `PropertyHolder`, implementing `ILayoutConnectable`) —
  `Id`, `Width`, `Height`, `Label`,
  `Shape` (the `BoxShape` outline, defaulting to `Rectangle`), `Keyword` (an optional italicized
  keyword line shown above the title), `Compartments` (an ordered, read-only list of
  `LayoutCompartment` feature sections, defaulting to empty), `TitleHeight` (an optional override, in
  logical pixels, of the title band a hierarchical layout engine reserves above this node's children
  when it is a labelled container), `RoundedCornerRadius`, `FolderTabWidth`, and `FolderTabHeight`
  (optional resolved shape-geometry hints copied onto placed boxes so routing and rendering can agree
  on the real outline of rounded rectangles and folders), `Children` (the lazily-created nested child
  subgraph), `HasChildren` (whether the node holds at least one child), `Ports` (the lazily-created
  `LayoutGraphPortCollection` of named ports on the node's boundary), and `HasPorts` (whether the
  node holds at least one port).
- `ILayoutConnectable` (marker interface) — implemented by both `LayoutGraphNode` and
  `LayoutGraphPort`, mirroring ELK's `ElkConnectableShape`, so an edge can terminate at either a
  whole node or a specific named port on a node's boundary.
- `LayoutGraphPort` (sealed class, implementing `ILayoutConnectable`) — `Id` (unique within its
  owning node's `Ports` collection), `ExternalLabel` (string?, settable), and `InternalLabel`
  (string?, settable; read by a future hierarchy-aware phase, unused by any bundled algorithm today).
  Deliberately carries no `Side` property: placement is always a computed layout-engine output
  (`LayoutPort.Side`), never a caller input.
- `LayoutGraphEdge` (sealed class extending `PropertyHolder`) — `Id`, `Source`, `Target`
  (both typed `ILayoutConnectable`, so either may be a `LayoutGraphNode` or a `LayoutGraphPort`),
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

`LayoutGraphPortCollection LayoutGraphNode.Ports { get; }` — the node's named-port collection,
created lazily on first access, mirroring `Children`/`HasChildren` exactly.
`LayoutGraphPort AddPort(string id)` appends a port with the requested identifier (unique within the
owning node's own `Ports`, though the same identifier may be reused on a different node) and returns
it for further configuration (`ExternalLabel`, `InternalLabel`).

`bool LayoutGraphNode.HasPorts { get; }` — reports whether the node currently holds at least one port
without forcing the lazy allocation.

`LayoutGraphEdge AddEdge(string id, ILayoutConnectable source, ILayoutConnectable target)` — the
same `AddEdge` widened to accept either a `LayoutGraphNode` or a `LayoutGraphPort` (via the shared
`ILayoutConnectable` interface) as either endpoint, so a port can be connected exactly like a plain
node.

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

`double? LayoutGraphNode.RoundedCornerRadius { get; set; }`, `double? LayoutGraphNode.FolderTabWidth
{ get; set; }`, and `double? LayoutGraphNode.FolderTabHeight { get; set; }` — optional resolved
shape-geometry hints, in logical pixels, for the two shipped non-rectangular box families whose real
outline differs materially from the bounding rectangle used during placement. A caller sets
`RoundedCornerRadius` to the radius the renderer will actually draw for a rounded rectangle and sets
the two folder-tab values to the exact top-left folder tab geometry the renderer will draw. All three
default to `null`, preserving the existing fallback behavior for callers that do not need exact
shape-aware routing. The bundled leaf algorithms and the hierarchical engine's sized view propagate
the hints unchanged onto the placed `LayoutBox`, where `ConnectorRouter`, `SvgRenderer`, and
`SkiaRasterRenderer` can all consume the same resolved values without the layout APIs taking a
`Theme` dependency.

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
- A node with no ports shall allocate no port collection, mirroring the `Children`/`HasChildren`
  pattern exactly; `HasPorts` shall not force that allocation. A port identifier shall be unique
  within its owning node's own `Ports` (mirroring per-container node/edge identifier scoping), but the
  same identifier may be reused on a different node.
- `LayoutGraphPort` shall carry no `Side` property; a caller cannot request a particular face for a
  port, because no bundled algorithm today gives a caller control over layer assignment or
  within-layer ordering — a caller-declared side would be a promise the engine cannot keep.
- `Shape`, `Keyword`, and `Compartments` shall default to a plain rectangle, no keyword, and no
  compartments respectively, so that adding box-appearance selection to the input graph leaves a
  node's placed appearance unchanged when none of the three are set; a layout algorithm shall copy
  each of the three properties unchanged onto the placed box (or, for `HierarchicalLayoutAlgorithm`,
  onto the corresponding view node) rather than substituting a default.
- `TitleHeight` shall default to `null`, so that a container node reserves `HierarchicalLayoutAlgorithm`'s
  generic default title-band height unless a caller explicitly overrides it; the override shall apply
  only while the container also carries a `Label`, matching the existing label-gated title-band
  behavior.
- `RoundedCornerRadius`, `FolderTabWidth`, and `FolderTabHeight` shall default to `null`, so callers
  that do not need exact shape-aware routing or rendering retain the pre-existing generic fallback
  behavior; when set, a layout algorithm shall propagate each hint unchanged onto the placed box.
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
| Rendering-Model-LayoutGraph-ShapeGeometryHints | `LayoutGraphNode` shape-geometry hint properties |
| Rendering-Model-LayoutGraph-Ports | `LayoutGraphNode.Ports` / `HasPorts` / `ILayoutConnectable` |
| Rendering-Model-LayoutGraph-PortLabels | `LayoutGraphPort.ExternalLabel` / `.InternalLabel` |
