# Rendering Abstractions Design

## Architecture

`DemaConsulting.Rendering.Abstractions` is the service provider interface (SPI) that sits between the
rendering model (*Rendering Model* system, `DemaConsulting.Rendering`) and the concrete layout and
renderer implementations. It defines the pluggable `ILayoutAlgorithm` and `IRenderer` contracts, the
registries that resolve an implementation by identifier, media type, or file extension, the visual
`Theme` model, and the single-source geometry helpers that let every renderer draw identical
decorations.

The system is composed of seven units:

```text
DemaConsulting.Rendering.Abstractions (System)
├── RenderingContracts (Unit)
├── Registries (Unit)
├── Theme (Unit)
├── NotationMetrics (Unit)
├── BoxMetrics (Unit)
├── ConnectorLabelPlacer (Unit)
└── PortLabelWidthEstimator (Unit)
```

- **Rendering Contracts** — `ILayoutAlgorithm`, `IRenderer`, `RenderOptions`, `RenderOutput`. Detailed
  in Rendering Contracts Unit Design.
- **Registries** — `LayoutAlgorithmRegistry`, `RendererRegistry`. Detailed in Registries Unit Design.
- **Theme** — `Theme` and the built-in `Themes`. Detailed in Theme Unit Design.
- **Notation Metrics** — `NotationMetrics` and `MarkerVertex`. Detailed in Notation Metrics Unit
  Design.
- **Box Metrics** — `BoxMetrics`. Detailed in Box Metrics Unit Design.
- **Connector Label Placer** — `ConnectorLabelPlacer`. Detailed in Connector Label Placer Unit Design.
- **Port Label Width Estimator** — `PortLabelWidthEstimator`, shared by the Rendering.Layout layout
  engine and the Rendering.Svg renderer so a port label's natural-width estimate can never disagree
  between layout time and render time. Detailed in Port Label Width Estimator Unit Design.

The contracts and registries collaborate to make the pipeline extensible: callers resolve an
`ILayoutAlgorithm` from `LayoutAlgorithmRegistry` by its configured identifier and an `IRenderer` from
`RendererRegistry` by media type or output file extension, so additional diagram families and output
formats are introduced purely additively. The `Theme` and the four geometry helpers (`NotationMetrics`,
`BoxMetrics`, `ConnectorLabelPlacer`, `PortLabelWidthEstimator`) are the single source of truth that
keeps SVG and raster outputs visually consistent. Concrete algorithm and renderer implementations live
in the downstream Rendering.Layout, Rendering.Svg, and Rendering.Skia systems.

## External Interfaces

- **`ILayoutAlgorithm`** — inbound extension point implemented by layout systems; accepts a
  `LayoutGraph` plus `LayoutOptions` and returns a placed `LayoutTree`. Each implementation advertises
  an identifier used for registry lookup.
- **`IRenderer`** — inbound extension point implemented by renderer systems; accepts a `LayoutTree`,
  `RenderOptions`, and an output `Stream`, and advertises a media type and file extensions used for
  registry lookup.
- **`LayoutAlgorithmRegistry` / `RendererRegistry`** — outbound resolution APIs; register and resolve
  implementations by identifier, media type, or advertised file extension.
- **`RenderOptions` / `RenderOutput`** — the render invocation parameters and result descriptor,
  including the selected `Theme`.
- **`Theme` / `Themes`** — outbound value model consumed by renderers to color and size decorations.

All interfaces are in-process .NET APIs.

## Dependencies

The system references the *Rendering Model* package (`DemaConsulting.Rendering`) for the graph, option,
and tree types it names in its contracts. It has no runtime NuGet package dependencies beyond the .NET
base class library; all NuGet references are build-time-only private assets (SBOM, SourceLink, API
documentation, and `Polyfill`). No OTS runtime component or Shared Package is consumed.

## Risk Control Measures

N/A - general-purpose rendering libraries carry no safety-related risk controls requiring
architectural segregation (IEC 62304 §5.3.3).

## Data Flow

```text
LayoutGraph + LayoutOptions  ──►  ILayoutAlgorithm  ──►  LayoutTree
                                                            │
                                    RenderOptions (Theme)   ▼
                                          └────────►  IRenderer  ──►  output Stream
```

A `LayoutGraph` plus `LayoutOptions` from the *Rendering Model* system is the input consumed by a
selected `ILayoutAlgorithm`; the algorithm produces a placed `LayoutTree`, which a selected `IRenderer`
draws to a caller-supplied stream. The renderer receives `RenderOptions` (including a `Theme`) and
reads the shared notation, box, and connector-label geometry so every output format draws the same
decorations. This system defines the contracts and shared geometry; it moves no data itself.

## Design Constraints

- **Target frameworks**: `net8.0`, `net9.0`, and `net10.0`.
- **Additive extensibility**: new algorithms and renderers are added by implementing a contract and
  registering it; existing contracts do not change, so consumers remain source-compatible.
- **Single-source geometry**: notation, box, connector-label, and port-label-width geometry are
  defined once here and reused by every renderer to guarantee visual consistency across SVG and
  raster output.
- **Model-only runtime dependency**: the package depends only on the rendering model and the .NET base
  class library, keeping the SPI free of algorithm- or renderer-specific coupling.
