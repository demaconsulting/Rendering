# Rendering Model Design

## Overview

`DemaConsulting.Rendering` is the SysML-agnostic rendering model. It defines the data types that
flow through the rendering pipeline but contains no layout algorithm and no renderer. It provides
three things: the placed `LayoutTree` intermediate representation that a renderer draws, the open
(ELK-inspired) property-based option system that carries configuration, and the unplaced
`LayoutGraph` input that a layout algorithm consumes. The package has no runtime dependencies beyond
the .NET base class library, and its types are immutable records or small mutable holders.

A layout algorithm consumes a `LayoutGraph` plus a `LayoutOptions` and produces a placed
`LayoutTree`; a renderer then draws that tree. This document describes the model that sits at both
ends of that flow. The algorithm and renderer contracts themselves live in the
*Rendering Abstractions* system.

## Software Structure

```text
DemaConsulting.Rendering (System)
├── LayoutTree (Unit)
├── Options (Unit)
└── LayoutGraph (Unit)
```

- **Layout Tree** — the placed intermediate representation: `LayoutTree` and the `LayoutNode`
  discriminated-union hierarchy of concrete node records.
- **Options** — the open configuration system: `LayoutProperty<T>`, `IPropertyHolder`,
  `PropertyHolder`, `LayoutOptions`, `CoreOptions`, and `LayoutFlowDirection`.
- **Layout Graph** — the unplaced input model: `LayoutGraph`, `LayoutGraphNode`, `LayoutGraphEdge`.

## Layout Tree Unit

### Layout Tree Overview

The Layout Tree unit is the placed, renderer-agnostic representation of one view. A `LayoutTree`
carries the canvas dimensions and a flat list of top-level `LayoutNode` instances. `LayoutNode` is an
abstract record acting as the root of a discriminated union; renderers switch on the concrete type
and skip unknown subtypes for forward compatibility. All coordinates are absolute, with the origin at
the top-left, so a renderer can draw each element directly without resolving nested offsets.

### Layout Tree Data Model

- `LayoutTree` (sealed record) — `Width`, `Height`, the top-level `Nodes` list, and layout-quality
  `Warnings`.
- `LayoutNode` (abstract record) — the discriminated-union root; carries no members.
- `LayoutBox` (node record) — `X`, `Y`, `Width`, `Height`, `Label`, integer `Depth`, `Shape`,
  `Compartments`, `Children`, and optional `Keyword`.
- `LayoutCompartment` (sealed record) — `Title` and text `Rows`.
- `LayoutPort` (node record) — `CentreX`, `CentreY`, attached `Side`, and `Label`.
- `LayoutLine` (node record) — `Waypoints`, `SourceEnd`, `TargetEnd`, `LineStyle`, and
  `MidpointLabel`.
- `Point2D` (sealed record) — `X` and `Y`.
- `LayoutLabel` (node record) — `X`, `Y`, `MaxWidth`, `Text`, `Align`, `Weight`, `Style`, `FontSize`.
- `LayoutBadge` (node record) — `CentreX`, `CentreY`, `Size`, `Shape`, and `Label`.
- `LayoutBand` (node record) — `X`, `Y`, `Width`, `Height`, `Orientation`, `Label`, and `Children`.
- `LayoutLifeline` (node record) — `CentreX`, `TopY`, `BottomY`, `Label`, `HeaderWidth`,
  `HeaderHeight`.
- `LayoutActivation` (node record) — `CentreX`, `TopY`, `BottomY`.
- `LayoutGrid` (node record) — `X`, `Y`, and `Rows`.
- `LayoutGridRow` (sealed record) — `IsHeader` and `Cells`.
- `LayoutGridCell` (sealed record) — `Width`, `Height`, `Text`, `Align`, `ColSpan`.

The unit also defines the notation enumerations `BoxShape`, `PortSide`, `EndMarkerStyle`,
`LineStyle`, `TextAlign`, `FontWeight`, `FontStyle`, `BadgeShape`, and `BandOrientation`.

### Layout Tree Design Constraints

- All node coordinates shall be absolute in logical pixels with the origin at the top-left; the model
  shall not apply any coordinate transform.
- `LayoutBox` shall express nesting as an integer `Depth`, not a resolved color, so that the theme
  in effect at render time selects the fill color.
- `LayoutNode` shall remain an open discriminated union; adding a new node type shall be an additive
  change, and consumers shall skip node types they do not recognize.
- All node records shall be immutable, so a placed tree can be shared and rendered repeatedly without
  defensive copying.

### Layout Tree Interactions

The Layout Tree is produced by a layout algorithm (see the *Rendering Abstractions* design,
`ILayoutAlgorithm`) and consumed by a renderer (`IRenderer`). Within the tree, `LayoutBox` and
`LayoutBand` hold nested `Children`, so renderers walk the node hierarchy recursively.

## Options Unit

### Options Overview

The Options unit is the open, ELK-inspired configuration system. Configuration values are keyed by
`LayoutProperty<T>` constants rather than by fixed fields, so new options are introduced by declaring
new keys without changing any method signature. Any object that needs to carry configuration
implements `IPropertyHolder`; the concrete `PropertyHolder` provides a dictionary-backed store keyed
by each property's identifier.

### Options Data Model

- `LayoutProperty<T>` (sealed class) — `Id` and `DefaultValue`.
- `IPropertyHolder` (interface) — `Get`, `TryGet`, `Set`, `Contains`.
- `PropertyHolder` (class implementing `IPropertyHolder`) — dictionary-backed store.
- `LayoutOptions` (sealed class extending `PropertyHolder`) — with a `ForAlgorithm(string)` factory.
- `CoreOptions` (static class) — well-known keys `Algorithm`, `Direction`, `NodeSpacing`,
  `LayerSpacing`.
- `LayoutFlowDirection` (enum) — `Right`, `Left`, `Down`, `Up`.

### Options Key Methods

`TValue Get<TValue>(LayoutProperty<TValue> property)` — returns the stored value for a property, or
the property's `DefaultValue` when it has not been set. Throws `ArgumentNullException` when the
property is null.

`bool TryGet<TValue>(LayoutProperty<TValue> property, out TValue value)` — reports whether the
property was explicitly set; when it was not, yields the declared default through `value`.

`IPropertyHolder Set<TValue>(LayoutProperty<TValue> property, TValue value)` — stores a value keyed by
the property identifier, replacing any previous value, and returns the holder for fluent chaining.

`bool Contains<TValue>(LayoutProperty<TValue> property)` — reports whether the property has been
explicitly set on this holder.

`LayoutOptions.ForAlgorithm(string algorithmId)` — creates a `LayoutOptions` pre-set with
`CoreOptions.Algorithm`.

### Options Design Constraints

- `PropertyHolder` shall store values keyed by `LayoutProperty<T>.Id`, so an option that no algorithm
  yet honors is carried harmlessly and simply ignored by algorithms that do not read it.
- `CoreOptions` keys marked advisory shall default harmlessly until an algorithm implements them, so
  that adding a key is a purely additive change.

### Options Interactions

`LayoutGraph`, `LayoutGraphNode`, and `LayoutGraphEdge` (in the Layout Graph unit) all derive from
`PropertyHolder`, so configuration can be attached to the whole graph or to a single element.
`LayoutOptions` is passed to a layout algorithm alongside a `LayoutGraph`.

## Layout Graph Unit

### Layout Graph Overview

The Layout Graph unit is the unplaced input to a layout algorithm: a set of sized `LayoutGraphNode`
boxes and directed `LayoutGraphEdge` connections. `LayoutGraph` itself derives from `PropertyHolder`,
so graph-wide options may be attached directly to it, and each node and edge also carries its own
property overrides.

### Layout Graph Data Model

- `LayoutGraph` (sealed class extending `PropertyHolder`) — `Nodes`, `Edges`, `AddNode`, `AddEdge`.
- `LayoutGraphNode` (sealed class extending `PropertyHolder`) — `Id`, `Width`, `Height`, `Label`.
- `LayoutGraphEdge` (sealed class extending `PropertyHolder`) — `Id`, `Source`, `Target`,
  `TargetEnd`, `LineStyle`, `Label`.

### Layout Graph Key Methods

`LayoutGraphNode AddNode(string id, double width, double height)` — creates a node with the requested
identity and size, appends it to `Nodes` in insertion order, and returns it for further
configuration.

`LayoutGraphEdge AddEdge(string id, LayoutGraphNode source, LayoutGraphNode target)` — creates a
directed edge referencing the two endpoint nodes, appends it to `Edges`, and returns it. The
`LayoutGraphEdge` constructor rejects a null identifier, source, or target.

### Layout Graph Design Constraints

- `AddNode` and `AddEdge` shall preserve caller insertion order in `Nodes` and `Edges`, so that a
  layout algorithm processes elements deterministically.
- A `LayoutGraphNode` identifier shall be unique within its owning graph, and an empty identifier
  shall be rejected at construction.

### Layout Graph Interactions

A `LayoutGraph` plus a `LayoutOptions` is the input to `ILayoutAlgorithm.Apply` (see the
*Rendering Abstractions* design), which produces a placed `LayoutTree`. Each `LayoutGraphEdge`
carries an `EndMarkerStyle` and `LineStyle` from the Layout Tree unit's enumerations.

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Model-LayoutTree | The `LayoutTree` and `LayoutNode` hierarchy |
| Rendering-Model-Configuration | The `IPropertyHolder` / `LayoutProperty<T>` option system |
| Rendering-Model-InputGraph | The `LayoutGraph` input model |
| Rendering-Model-LayoutTree-Canvas | `LayoutTree.Width`, `Height`, `Nodes` |
| Rendering-Model-LayoutTree-AbsoluteCoordinates | Absolute coordinates on every node record |
| Rendering-Model-LayoutTree-Box | `LayoutBox` record |
| Rendering-Model-LayoutTree-DepthNotColor | `LayoutBox.Depth` integer |
| Rendering-Model-LayoutTree-Port | `LayoutPort` record |
| Rendering-Model-LayoutTree-Line | `LayoutLine` record |
| Rendering-Model-LayoutTree-Label | `LayoutLabel` record |
| Rendering-Model-LayoutTree-Badge | `LayoutBadge` record |
| Rendering-Model-LayoutTree-Band | `LayoutBand` record |
| Rendering-Model-LayoutTree-Lifeline | `LayoutLifeline` record |
| Rendering-Model-LayoutTree-Activation | `LayoutActivation` record |
| Rendering-Model-LayoutTree-Grid | `LayoutGrid` / `LayoutGridRow` / `LayoutGridCell` |
| Rendering-Model-Options-Default | `PropertyHolder.Get` default path |
| Rendering-Model-Options-StoreAndRetrieve | `PropertyHolder.Set` / `Get` |
| Rendering-Model-Options-Contains | `PropertyHolder.Contains` |
| Rendering-Model-Options-TryGet | `PropertyHolder.TryGet` |
| Rendering-Model-LayoutGraph-AddNode | `LayoutGraph.AddNode` |
| Rendering-Model-LayoutGraph-AddEdge | `LayoutGraph.AddEdge` |
| Rendering-Model-LayoutGraph-PerElementProperties | `LayoutGraphNode` : `PropertyHolder` |
