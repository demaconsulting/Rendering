# Rendering Abstractions Design

## Overview

`DemaConsulting.Rendering.Abstractions` is the service provider interface (SPI) that sits between the
rendering model (*Rendering Model* system, `DemaConsulting.Rendering`) and the concrete layout and
renderer implementations. It defines the pluggable `ILayoutAlgorithm` and `IRenderer` contracts, the
registries that resolve an implementation by identifier, media type, or file extension, the visual
`Theme` model, and the single-source geometry helpers (`NotationMetrics`, `BoxMetrics`,
`ConnectorLabelPlacer`) that let every renderer draw identical decorations. The package depends only
on the rendering model and the .NET base class library.

The ELK-inspired flow is: a `LayoutGraph` plus `LayoutOptions` is passed to an `ILayoutAlgorithm`,
which produces a placed `LayoutTree`; an `IRenderer` then draws that tree to an output stream.
Algorithms and renderers are selected at run time through the registries, so additional diagram
families and output formats are introduced purely additively.

## Software Structure

```text
DemaConsulting.Rendering.Abstractions (System)
├── RenderingContracts (Unit)
├── Registries (Unit)
├── Theme (Unit)
├── NotationMetrics (Unit)
├── BoxMetrics (Unit)
└── ConnectorLabelPlacer (Unit)
```

- **Rendering Contracts** — `ILayoutAlgorithm`, `IRenderer`, `RenderOptions`, `RenderOutput`.
  Detailed in [Rendering Contracts Unit Design](rendering-contracts.md).
- **Registries** — `LayoutAlgorithmRegistry`, `RendererRegistry`. Detailed in
  [Registries Unit Design](registries.md).
- **Theme** — `Theme` and the built-in `Themes`. Detailed in [Theme Unit Design](theme.md).
- **Notation Metrics** — `NotationMetrics` and `MarkerVertex`. Detailed in
  [Notation Metrics Unit Design](notation-metrics.md).
- **Box Metrics** — `BoxMetrics`. Detailed in [Box Metrics Unit Design](box-metrics.md).
- **Connector Label Placer** — `ConnectorLabelPlacer`. Detailed in
  [Connector Label Placer Unit Design](connector-label-placer.md).

## System Interactions

A `LayoutGraph` plus `LayoutOptions` from the *Rendering Model* system is the input consumed by a
selected `ILayoutAlgorithm`; the algorithm produces a placed `LayoutTree`, which a selected
`IRenderer` draws to a concrete stream. The selected renderer receives `RenderOptions`, including a
`Theme`, and uses the shared notation, box, and connector-label geometry helpers to keep SVG and raster
outputs consistent.

Callers resolve algorithms from `LayoutAlgorithmRegistry` by the configured algorithm identifier.
Callers resolve renderers from `RendererRegistry` by media type or by output file extension. Concrete
algorithm and renderer implementations live in the downstream Rendering.Layout, Rendering.Svg, and
Rendering.Skia systems; this system defines only the contracts, registries, and shared geometry those
implementations use.

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Abstractions-Extensibility | The contracts and registries units |
| Rendering-Abstractions-Theming | The `Theme` and `Themes` types (see [Theme Unit Design](theme.md)) |
| Rendering-Abstractions-SharedGeometry | The notation, box, and label geometry units |
