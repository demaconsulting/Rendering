## Options Unit Design

Part of the Rendering Model system.

### Options Purpose

The Options unit's single responsibility is to define the open, ELK-inspired property system that
carries configuration end-to-end through the rendering pipeline: it declares typed property keys
(`LayoutProperty<T>`), the contract for any object that stores them (`IPropertyHolder`), a default
dictionary-backed store (`PropertyHolder`), a free-standing options bag (`LayoutOptions`), and the
well-known key catalogue (`CoreOptions`). It does not perform layout or rendering; it only defines and
holds configuration values.

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
  `EdgeRouting`, `NodeSpacing`, `LayerSpacing`, `MergeParallelEdges`,
  `AssumedFontSize`.
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

`LayoutOptions OverlayOnto(PropertyHolder parent)` — the generic option-cascading primitive: builds a
new `LayoutOptions` snapshot by copying `parent`'s explicitly-set values first, then this holder's own
explicitly-set values on top, so this holder's own values win. Because the merge copies boxed values
by key rather than going through each property's typed accessors, it works for any current or future
`LayoutProperty<T>` — including ones neither side knows about — with no per-property code. This is the
mechanism callers such as `HierarchicalLayoutAlgorithm` use to build each scope's cascaded effective
options: a nested scope's own overrides are overlaid onto its parent scope's already-resolved
snapshot, so a scope with no overrides of its own inherits the nearest ancestor's resolved value, and
a scope's own explicit override wins for itself and every unset descendant. Throws
`ArgumentNullException` when `parent` is null.

### Options Error Handling

`Get`, `TryGet`, `Set`, and `Contains` all reject a null `LayoutProperty<T>` by throwing
`ArgumentNullException`, so a caller cannot silently corrupt the store with a value that has no key. The
`LayoutProperty<T>` constructor rejects a null or empty `id` with `ArgumentException`, and
`LayoutOptions.ForAlgorithm(string)` rejects a null or empty `algorithmId` on the same terms. Reading
a property that has never been set is not an error: the store returns the property's declared
`DefaultValue` (through `Get`) or the default plus a `false` result (through `TryGet`). Unknown or
not-yet-honored keys carried on a holder are not detected here — they are ignored by whichever
algorithm or renderer chooses not to read them, which is a deliberate design property, not an error
path.

### Options Dependencies

The unit depends only on the .NET base class library (`System.Collections.Generic.Dictionary<TKey,
TValue>`). It has no project references, no OTS runtime component, and no Shared Package dependency;
the `Polyfill` build-time package is a private asset that only backfills newer BCL surface on older
target frameworks. Within the Rendering model, Options is a peer of the Layout Tree and Layout Graph
units and depends on neither.

### Options Callers

Within the Rendering model, the Layout Graph unit's `LayoutGraph`, `LayoutGraphNode`, and
`LayoutGraphEdge` all derive from `PropertyHolder`, so every graph element carries options through
this unit. Outside the model, layout algorithms in `DemaConsulting.Rendering.Layout` and renderers in
`DemaConsulting.Rendering.Svg` and `DemaConsulting.Rendering.Skia` read `LayoutOptions` and
`CoreOptions` keys to configure their behavior; the shared abstractions in
`DemaConsulting.Rendering.Abstractions` also consume `LayoutOptions` as the configuration parameter of
`ILayoutAlgorithm.Apply`. Application code builds a `LayoutOptions` (often via `ForAlgorithm`) and
passes it to a layout facade.

### Options Design Constraints

- `PropertyHolder` shall store values keyed by `LayoutProperty<T>.Id`, so an option that no algorithm
  yet honors is carried harmlessly and simply ignored by algorithms that do not read it.
- `CoreOptions` keys marked advisory shall default harmlessly until an algorithm implements them, so
  that adding a key is a purely additive change.
- `OverlayOnto` shall merge by raw key rather than by enumerating known `LayoutProperty<T>` constants,
  so the cascading primitive remains correct for options this unit has never heard of (including
  future, not-yet-declared properties).
- `CoreOptions.MergeParallelEdges` shall default to `true`, exactly reproducing the layered
  algorithm's pre-existing unconditional parallel-edge deduplication so an existing caller that never
  sets this key sees byte-identical output.
- `CoreOptions.AssumedFontSize` shall default to `12.0`, matching the bundled themes'
  `Theme.FontSizeBody`, so a text-aware layout decision made once at layout time (before any theme is
  available) uses a reasonable font size by default.

### Options Interactions

`LayoutGraph`, `LayoutGraphNode`, and `LayoutGraphEdge` (in the Layout Graph unit) all derive from
`PropertyHolder`, so configuration can be attached to the whole graph or to a single element.
`LayoutOptions` is passed to a layout algorithm alongside a `LayoutGraph`. `LayeredLayoutAlgorithm`
(in `DemaConsulting.Rendering.Layout`) computes `LayoutBox.ContentInset*` (see the Layout Tree unit)
using its own self-contained `PortLabelWidthEstimator` heuristic.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Model-Options-Default | `PropertyHolder.Get` default path |
| Rendering-Model-Options-StoreAndRetrieve | `PropertyHolder.Set` / `Get` |
| Rendering-Model-Options-Contains | `PropertyHolder.Contains` |
| Rendering-Model-Options-TryGet | `PropertyHolder.TryGet` |
| Rendering-Model-Options-Cascade | `PropertyHolder.OverlayOnto` |
| Rendering-Model-Options-MergeParallelEdges | `CoreOptions.MergeParallelEdges` |
| Rendering-Model-Options-AssumedFontSize | `CoreOptions.AssumedFontSize` |
