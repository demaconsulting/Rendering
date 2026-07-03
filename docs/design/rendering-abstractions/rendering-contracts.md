## Rendering Contracts Unit Design

Part of the Rendering Abstractions system.

### Contracts Purpose

The Rendering Contracts unit defines the two extension-point interfaces and the value types that flow
across them. `ILayoutAlgorithm` is the high-level extension point that turns an unplaced graph into a
placed tree; `IRenderer` is the low-level extension point that turns a placed tree into an output
stream. `RenderOptions` carries the theme and sizing for a render, and `RenderOutput` bundles one
rendered stream with its metadata.

### Contracts Data Model

- `ILayoutAlgorithm` (interface) — `Id` and `Apply(LayoutGraph, LayoutOptions)`.
- `IRenderer` (interface) — `MediaType`, `DefaultExtension`, `FileExtensions`, and
  `Render(LayoutTree, RenderOptions, Stream)`.
- `RenderOptions` (sealed record) — `Theme`, `Scale`, `Dpi`, `DepthLimit`.
- `RenderOutput` (sealed record) — `SuggestedFileName`, `MediaType`, `Data`, `Warnings`.

### Contracts Key Methods

`LayoutTree ILayoutAlgorithm.Apply(LayoutGraph graph, LayoutOptions options)` — turns an unplaced
graph plus caller-supplied options into a placed layout tree. Preconditions: `graph` and `options`
are non-null; the implementation is free to interpret any subset of `options` it understands and to
ignore the remainder. Postcondition: the returned `LayoutTree` describes the placed graph and is
independent of the input `graph` reference.

`void IRenderer.Render(LayoutTree layout, RenderOptions options, Stream output)` — writes the
rendered artefact for `layout` to `output` using the theme, scale, DPI, and depth limit supplied by
`options`. Preconditions: `layout`, `options`, and `output` are non-null and `output` is writable.
Postcondition: all rendered bytes have been written to `output`; the renderer performs no
filesystem access itself.

The identity members — `ILayoutAlgorithm.Id`, `IRenderer.MediaType`, `IRenderer.DefaultExtension`,
and `IRenderer.FileExtensions` — expose the stable keys used by the Registries unit to resolve
implementations at run time.

### Contracts Error Handling

The contracts do not define custom exception types. Implementers are expected to validate reference
parameters (`ArgumentNullException`) and to write only to the caller-supplied `Stream`; the
registries unit adds `KeyNotFoundException` when an id, media type, or file extension is not
registered. `RenderOptions` and `RenderOutput` are `sealed record` types whose properties are
initialised by their primary constructors and require no additional validation. No logging is
performed at the contract level; renderers surface non-fatal issues by populating
`RenderOutput.Warnings` rather than by throwing.

### Contracts Dependencies

- **Rendering Model system (`DemaConsulting.Rendering`)** — `LayoutGraph`, `LayoutOptions`, and
  `LayoutTree` are the data types that flow across `ILayoutAlgorithm.Apply` and `IRenderer.Render`.
- **Theme Unit** (same system) — `RenderOptions.Theme` references `Theme`.
- **.NET base class library** — `System.IO.Stream`, `System.Collections.Generic.IReadOnlyList<T>`.
  No third-party runtime packages are consumed.

### Contracts Callers

- **Registries Unit** (same system) — `LayoutAlgorithmRegistry` and `RendererRegistry` store,
  index, and resolve implementations of `ILayoutAlgorithm` and `IRenderer` by their identity
  members.
- **Rendering.Layout system** — every layout algorithm (`LayeredLayoutAlgorithm`,
  `ContainmentLayoutAlgorithm`, `HierarchicalLayoutAlgorithm`, and the `DefaultLayout` facade)
  implements `ILayoutAlgorithm`.
- **Rendering.Svg and Rendering.Skia systems** — `SvgRenderer`, `PngRenderer`, `JpegRenderer`, and
  `WebpRenderer` implement `IRenderer` and produce `RenderOutput` records.
- **Host applications** — construct `RenderOptions`, pass a `Stream` to `IRenderer.Render`, and
  consume the produced `RenderOutput` metadata.

### Contracts Design Constraints

- An `ILayoutAlgorithm` shall expose a stable `Id` that matches the value read from
  `CoreOptions.Algorithm`, and shall ignore options it does not understand so callers may pass options
  intended for other algorithms without error.
- An `IRenderer` shall expose its media type, default extension, and every file extension it produces,
  and shall write only to the caller-supplied `Stream` without filesystem access.
- Adding a new algorithm or renderer shall be an additive change: a new implementation of these
  interfaces requires no change to the existing contracts.

### Contracts Interactions

`ILayoutAlgorithm.Apply` consumes a `LayoutGraph` and `LayoutOptions` from the rendering model and
produces a `LayoutTree`. `IRenderer.Render` consumes that `LayoutTree` and a `RenderOptions` (whose
`Theme` comes from the Theme unit). Instances are registered in and resolved from the Registries unit.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Abstractions-Contracts-Algorithm | `ILayoutAlgorithm.Id` and `ILayoutAlgorithm.Apply` |
| Rendering-Abstractions-Contracts-Renderer | `IRenderer` output contract members |
