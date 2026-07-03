## Registries Unit Design

Part of the Rendering Abstractions system.

### Registries Purpose

The Registries unit provides two service-provider lookups. `LayoutAlgorithmRegistry` keys algorithms
by their `Id`; `RendererRegistry` keys renderers by their `MediaType` and by every advertised
`IRenderer.FileExtensions` value. Consumers register the implementations they wish to offer and
resolve one at run time by algorithm identifier, output media type, or output file extension. Neither
registry is thread-safe for concurrent registration.

### Registries Data Model

- `LayoutAlgorithmRegistry` (sealed class) — `Ids`, `Register`, `Contains`, `TryResolve`, `Resolve`.
- `RendererRegistry` (sealed class) — `MediaTypes`, `FileExtensions`, `Register`, `Contains`,
  `ContainsExtension`, `TryResolve`, `TryResolveByExtension`, `Resolve`, and `ResolveByExtension`.

### Registries Key Methods

`LayoutAlgorithmRegistry Register(ILayoutAlgorithm algorithm)` — stores the algorithm keyed by its
`Id`, replacing any previous algorithm with the same identifier, and returns the registry for fluent
chaining.

`ILayoutAlgorithm Resolve(string id)` — returns the algorithm registered under `id`, or throws
`KeyNotFoundException` when none is registered. `bool TryResolve(string, out ILayoutAlgorithm?)`
performs the same lookup without throwing.

`RendererRegistry.Register` stores the renderer by `MediaType` and by every extension in
`IRenderer.FileExtensions`, replacing any previous renderer registered for the same media type or
extension. Media types and extensions are compared case-insensitively.

`IRenderer ResolveByExtension(string extension)` — normalizes the supplied extension by trimming it,
adding an optional leading dot when needed, and lower-casing it for lookup. It returns the renderer
registered for that extension or throws `KeyNotFoundException`; `TryResolveByExtension` performs the
same lookup without throwing.

### Registries Error Handling

Both registries validate their string and reference parameters with `ArgumentNullException.ThrowIfNull`
and propagate the resulting `ArgumentNullException` to the caller. `Resolve(string)` and
`ResolveByExtension(string)` throw `KeyNotFoundException` when no algorithm or renderer is registered
under the requested id, media type, or (normalized) file extension; the message includes the
requested key so a configuration mistake is immediately diagnosable. The non-throwing variants
(`TryResolve`, `TryResolveByExtension`) return `false` and set the `out` parameter to `null` in the
same missing-entry cases. No exceptions are caught internally: any exception raised by the underlying
`Dictionary<string, T>` (for example from an invalid key type at registration time) surfaces to the
caller unchanged. The registries are documented as not thread-safe for concurrent registration; the
callers are responsible for building each registry on a single thread before publishing it.

### Registries Dependencies

- **Rendering Contracts Unit** (same system) — `ILayoutAlgorithm` and `IRenderer` are the value types
  stored and returned by the two registries; `IRenderer.MediaType` and `IRenderer.FileExtensions`
  supply the keys used by `RendererRegistry`.
- **.NET base class library** — `System.Collections.Generic.Dictionary<TKey, TValue>` for the backing
  storage and `System.Collections.ObjectModel.ReadOnlyCollection<string>` for the `Ids`, `MediaTypes`,
  and `FileExtensions` snapshots.

No OTS runtime component or Shared Package is consumed.

### Registries Callers

- **Rendering.Layout `DefaultLayout` unit** — builds a `LayoutAlgorithmRegistry` populated with the
  layered, containment, and hierarchical algorithms and resolves the algorithm identified by
  `CoreOptions.Algorithm` before invoking `ILayoutAlgorithm.Apply`.
- **Rendering.Svg and Rendering.Skia systems** — register their `IRenderer` implementations in a
  shared `RendererRegistry` so callers can resolve a renderer by media type (for example
  `image/svg+xml`, `image/png`) or by an output file extension (`.svg`, `.png`, `.jpg`, `.webp`).
- **End-user applications** that host the rendering pipeline resolve algorithms and renderers by
  identifier or extension when translating CLI arguments or filename hints into concrete
  implementations.

### Registries Design Constraints

- `Resolve` shall raise `KeyNotFoundException` when the requested identifier or media type is not
  registered, so a configuration mistake surfaces immediately rather than as a later null-reference
  failure.
- `Register` shall replace any existing entry with the same key, so a consumer can override a bundled
  implementation.
- `RendererRegistry` shall index every advertised renderer extension, and extension lookup shall ignore
  case and tolerate an omitted leading dot so file-driven callers can pass user-supplied suffixes.

### Registries Interactions

The registries hold `ILayoutAlgorithm` and `IRenderer` instances from the Rendering Contracts unit. A
caller resolves an algorithm using the identifier from `CoreOptions.Algorithm` and resolves a renderer
using either the desired output media type or an output filename extension such as `.svg`, `.png`, or
`webp`.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Abstractions-Registries-ResolveAlgorithm | `LayoutAlgorithmRegistry` identifier lookup |
| Rendering-Abstractions-Registries-ResolveRenderer | `RendererRegistry` media-type lookup |
| Rendering-Abstractions-Registries-ResolveRendererByExtension | `RendererRegistry` extension lookup |
| Rendering-Abstractions-Registries-MissingThrows | `Resolve` not-found exception |
