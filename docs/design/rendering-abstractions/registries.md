## Registries Unit Design

Part of the Rendering Abstractions system.

### Registries Overview

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
