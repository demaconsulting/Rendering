# Rendering Contracts Unit Design

Part of the [Rendering Abstractions](rendering-abstractions.md) system.

## Contracts Overview

The Rendering Contracts unit defines the two extension-point interfaces and the value types that flow
across them. `ILayoutAlgorithm` is the high-level extension point that turns an unplaced graph into a
placed tree; `IRenderer` is the low-level extension point that turns a placed tree into an output
stream. `RenderOptions` carries the theme and sizing for a render, and `RenderOutput` bundles one
rendered stream with its metadata.

## Contracts Data Model

- `ILayoutAlgorithm` (interface) — `Id` and `Apply(LayoutGraph, LayoutOptions)`.
- `IRenderer` (interface) — `MediaType`, `DefaultExtension`, `FileExtensions`, and
  `Render(LayoutTree, RenderOptions, Stream)`.
- `RenderOptions` (sealed record) — `Theme`, `Scale`, `Dpi`, `DepthLimit`.
- `RenderOutput` (sealed record) — `SuggestedFileName`, `MediaType`, `Data`, `Warnings`.

## Contracts Design Constraints

- An `ILayoutAlgorithm` shall expose a stable `Id` that matches the value read from
  `CoreOptions.Algorithm`, and shall ignore options it does not understand so callers may pass options
  intended for other algorithms without error.
- An `IRenderer` shall expose its media type, default extension, and every file extension it produces,
  and shall write only to the caller-supplied `Stream` without filesystem access.
- Adding a new algorithm or renderer shall be an additive change: a new implementation of these
  interfaces requires no change to the existing contracts.

## Contracts Interactions

`ILayoutAlgorithm.Apply` consumes a `LayoutGraph` and `LayoutOptions` from the rendering model and
produces a `LayoutTree`. `IRenderer.Render` consumes that `LayoutTree` and a `RenderOptions` (whose
`Theme` comes from the Theme unit). Instances are registered in and resolved from the Registries unit.

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Abstractions-Contracts-Algorithm | `ILayoutAlgorithm.Id` and `ILayoutAlgorithm.Apply` |
| Rendering-Abstractions-Contracts-Renderer | `IRenderer` output contract members |
