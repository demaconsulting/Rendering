## Rendering Contracts Unit Design

Part of the Rendering Abstractions system.

### Contracts Purpose

The Rendering Contracts unit defines the two extension points and the value types that flow
across them. `LayoutAlgorithmBase` is the high-level extension point that turns an unplaced graph into a
placed tree; `IRenderer` is the low-level extension point that turns a placed tree into an output
stream. `RenderOptions` carries the theme and sizing for a render, and `RenderOutput` bundles one
rendered stream with its metadata.

### Contracts Data Model

- `LayoutAlgorithmBase` (abstract class, `LayoutAlgorithmBase.cs`) — `Id`, the public
  `Apply(LayoutGraph)`, and the `protected internal` `ApplyCore(LayoutGraph, LayoutOptions)`
  extension point that derived algorithms override.
- `IRenderer` (interface) — `MediaType`, `DefaultExtension`, `FileExtensions`, and
  `Render(LayoutTree, RenderOptions, Stream)`.
- `RenderOptions` (sealed record) — `Theme`, `Scale`, `Dpi`, `DepthLimit`.
- `RenderOutput` (sealed record) — `SuggestedFileName`, `MediaType`, `Data`, `Warnings`.

### Contracts Key Methods

`LayoutTree LayoutAlgorithmBase.Apply(LayoutGraph graph)` — turns an unplaced graph into a placed
layout tree. Every setting the layout honors is read from `graph` itself (it is an
`IPropertyHolder`), so there is no separate options argument. Precondition: `graph` is non-null.
Postcondition: the returned `LayoutTree` describes the placed graph and is independent of the input
`graph` reference.

`LayoutTree LayoutAlgorithmBase.ApplyCore(LayoutGraph graph, LayoutOptions options)` — the
`protected internal abstract` extension point each algorithm implements. It receives the scope's
resolved options so a hierarchical or composite algorithm can recurse into a nested scope; it is
reachable from derived classes and, through `InternalsVisibleTo`, from the bundled algorithms in
`DemaConsulting.Rendering.Layout`, but never from an external caller. The implementation is free to
interpret any subset of `options` it understands and to ignore the remainder.

`void IRenderer.Render(LayoutTree layout, RenderOptions options, Stream output)` — writes the
rendered artifact for `layout` to `output` using the theme, scale, DPI, and depth limit supplied by
`options`. Preconditions: `layout`, `options`, and `output` are non-null and `output` is writable.
Postcondition: all rendered bytes have been written to `output`; the renderer performs no
filesystem access itself.

The identity members — `LayoutAlgorithmBase.Id`, `IRenderer.MediaType`, `IRenderer.DefaultExtension`,
and `IRenderer.FileExtensions` — expose the stable keys used by the Registries unit to resolve
implementations at run time.

### Contracts Error Handling

The contracts do not define custom exception types. Implementers are expected to validate reference
parameters (`ArgumentNullException`) and to write only to the caller-supplied `Stream`; the
registries unit adds `KeyNotFoundException` when an id, media type, or file extension is not
registered. `RenderOptions` and `RenderOutput` are `sealed record` types whose properties are
initialized by their primary constructors and require no additional validation. No logging is
performed at the contract level; renderers surface non-fatal issues by populating
`RenderOutput.Warnings` rather than by throwing.

### Contracts Dependencies

- **Rendering Model system (`DemaConsulting.Rendering`)** — `LayoutGraph`, `LayoutOptions`, and
  `LayoutTree` are the data types that flow across `LayoutAlgorithmBase.Apply` and `IRenderer.Render`.
- **Theme Unit** (same system) — `RenderOptions.Theme` references `Theme`.
- **.NET base class library** — `System.IO.Stream`, `System.Collections.Generic.IReadOnlyList<T>`.
  No third-party runtime packages are consumed.

### Contracts Callers

- **Registries Unit** (same system) — `LayoutAlgorithmRegistry` and `RendererRegistry` store,
  index, and resolve implementations of `LayoutAlgorithmBase` and `IRenderer` by their identity
  members.
- **Rendering.Layout system** — every layout algorithm (`LayeredLayoutAlgorithm`,
  `ContainmentLayoutAlgorithm`, `HierarchicalLayoutAlgorithm`, and the `DefaultLayout` facade)
  derives from `LayoutAlgorithmBase`.
- **Rendering.Svg and Rendering.CanvasNet systems** — `SvgRenderer`, `PngRenderer`, and
  `JpegRenderer` implement `IRenderer` and produce `RenderOutput` records.
- **Host applications** — construct `RenderOptions`, pass a `Stream` to `IRenderer.Render`, and
  consume the produced `RenderOutput` metadata.

### Contracts Design Constraints

- A `LayoutAlgorithmBase` shall expose a stable `Id` that matches the value read from
  `CoreOptions.Algorithm`, and shall ignore options it does not understand so callers may pass options
  intended for other algorithms without error.
- An `IRenderer` shall expose its media type, default extension, and every file extension it produces,
  and shall write only to the caller-supplied `Stream` without filesystem access.
- Adding a new algorithm or renderer shall be an additive change: a new implementation of these
  interfaces requires no change to the existing contracts.

### Contracts Interactions

`LayoutAlgorithmBase.Apply` consumes a `LayoutGraph` (carrying its `LayoutOptions` properties) from the
rendering model and
produces a `LayoutTree`. `IRenderer.Render` consumes that `LayoutTree` and a `RenderOptions` (whose
`Theme` comes from the Theme unit). Instances are registered in and resolved from the Registries unit.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Abstractions-Contracts-AlgorithmIdentifier | `LayoutAlgorithmBase.Id` |
| Rendering-Abstractions-Contracts-AlgorithmApply | `LayoutAlgorithmBase.Apply` |
| Rendering-Abstractions-Contracts-RendererIdentity | `IRenderer.MediaType`, `DefaultExtension`, `FileExtensions` |
| Rendering-Abstractions-Contracts-RendererWrite | `IRenderer.Render` |
