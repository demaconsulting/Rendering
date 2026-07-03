## Options Unit Design

Part of the Rendering Model system.

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
- `CoreOptions` (static class) — well-known keys `Algorithm`, `HierarchyHandling`, `Direction`,
  `EdgeRouting`, `NodeSpacing`, `LayerSpacing`.
- `LayoutFlowDirection` (enum) — `Right`, `Left`, `Down`, `Up`.
- `HierarchyHandling` (enum) — `SeparateChildren` (the only shipped mode). Mirrors ELK's
  `elk.hierarchyHandling`; selects how a hierarchical layout engine treats a container node's nested
  children. Under `SeparateChildren` each container is laid out in its own coordinate space and sized
  to fit its children. An `IncludeChildren` (cross-boundary) mode is a planned future additive value.

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

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Model-Options-Default | `PropertyHolder.Get` default path |
| Rendering-Model-Options-StoreAndRetrieve | `PropertyHolder.Set` / `Get` |
| Rendering-Model-Options-Contains | `PropertyHolder.Contains` |
| Rendering-Model-Options-TryGet | `PropertyHolder.TryGet` |
