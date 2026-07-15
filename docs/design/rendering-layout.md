# Rendering.Layout Design

## Architecture

`DemaConsulting.Rendering.Layout` is the placement system for the Rendering stack. A caller supplies a
`LayoutGraph` plus `LayoutOptions`; the system returns a placed `LayoutTree` of boxes and orthogonally
routed connectors that downstream renderers can draw without layout knowledge. The system exposes
bundled algorithms for layered, containment, hierarchical, and auto (meta-algorithm) layout, built on
reusable geometric engines.

The system is composed of one subsystem and a set of public algorithm and facade units:

```text
DemaConsulting.Rendering.Layout (System)
├── Engine (Subsystem)
│   ├── OrthogonalEdgeRouter (Unit)
│   ├── ContainmentPacker (Unit)
│   ├── InterconnectionLayoutEngine (Unit)
│   ├── LayeredPipeline (Unit)
│   └── LayoutTreePacker (Unit)
├── EdgeRoutingOption (Unit)
├── ConnectorRouter (Unit)
├── ContainmentLayout (Unit)
├── ContainmentLayoutAlgorithm (Unit)
├── HierarchicalLayoutAlgorithm (Unit)
├── DefaultLayout (Unit)
├── LayeredLayoutAlgorithm (Unit)
└── AutoLayoutAlgorithm (Unit)
```

- **Engine** — reusable, model-agnostic geometric engines (orthogonal routing, containment packing,
  interconnection placement, the ELK-style layered stage pipeline, and layout-tree packing). Detailed
  in Engine Subsystem Design.
- **EdgeRoutingOption** — routing-style configuration keys. Detailed in EdgeRoutingOption Unit Design.
- **ConnectorRouter** — routes connectors among already placed boxes. Detailed in ConnectorRouter Unit
  Design.
- **ContainmentLayout** — packs already sized model boxes into a container region. Detailed in
  ContainmentLayout Unit Design.
- **ContainmentLayoutAlgorithm** — public containment algorithm. Detailed in ContainmentLayoutAlgorithm
  Unit Design.
- **HierarchicalLayoutAlgorithm** — recursive compound-graph algorithm. Detailed in
  HierarchicalLayoutAlgorithm Unit Design.
- **DefaultLayout** — bundled registry factory and `LayoutEngine` facade. Detailed in DefaultLayout Unit
  Design.
- **LayeredLayoutAlgorithm** — public layered algorithm. Detailed in LayeredLayoutAlgorithm Unit
  Design.
- **AutoLayoutAlgorithm** — public auto-routing meta-algorithm that splits a graph into connected
  components, routes each to whichever bundled leaf algorithm best suits its shape, and packs the
  results into one combined tree. Detailed in AutoLayoutAlgorithm Unit Design.

The public algorithms implement `ILayoutAlgorithm` from Rendering.Abstractions and compose the Engine
subsystem's model-agnostic geometry. The `DefaultLayout` facade resolves the requested algorithm from
the bundled registry. The hierarchical algorithm composes leaf algorithms per container and routes
cross-container edges at the owning scope. The auto algorithm composes the other three bundled
algorithms per connected component and packs their independently-placed results with `LayoutTreePacker`.

## External Interfaces

- **`ILayoutAlgorithm` implementations** — outbound; `LayeredLayoutAlgorithm`,
  `ContainmentLayoutAlgorithm`, `HierarchicalLayoutAlgorithm`, and `AutoLayoutAlgorithm` each realize
  the Abstractions layout contract, accepting a `LayoutGraph` plus `LayoutOptions` and returning a
  placed `LayoutTree`. Each advertises an identifier (`layered`, `containment`, `hierarchical`,
  `auto`) for registry resolution.
- **`LayoutEngine` facade / bundled registry (DefaultLayout)** — outbound entry points; resolve and run
  the requested (or default) algorithm.
- **`EdgeRoutingOption`** — outbound `LayoutProperty` keys that select connector routing style.
- **Engine APIs (`OrthogonalEdgeRouter`, `ContainmentPacker`, `InterconnectionLayoutEngine`,
  `LayeredPipeline`, `LayoutTreePacker`)** — internal geometric services composed by the public
  algorithms; not intended for direct renderer use.

## Dependencies

The system references the *Rendering Model* package (`DemaConsulting.Rendering`) for `LayoutGraph`,
`LayoutOptions`, `CoreOptions`, and `LayoutTree`, and the *Rendering Abstractions* package
(`DemaConsulting.Rendering.Abstractions`) for the `ILayoutAlgorithm` contract and registries. It has no
runtime NuGet package dependencies beyond the .NET base class library; all NuGet references are
build-time-only private assets (SBOM, SourceLink, API documentation, and `Polyfill`). No OTS runtime
component or Shared Package is consumed.

## Risk Control Measures

N/A - general-purpose rendering libraries carry no safety-related risk controls requiring
architectural segregation (IEC 62304 §5.3.3).

## Data Flow

```text
LayoutGraph + LayoutOptions
        │
        ▼  selected ILayoutAlgorithm (layered / containment / hierarchical / auto)
   Engine geometry (layered pipeline, packing, routing)
        │
        ▼
    LayoutTree  (placed boxes + orthogonally routed connectors)  ──►  renderer
```

A caller passes an input graph and options; the selected algorithm drives the Engine subsystem to
assign positions, pack containers, and route connectors, then returns a placed `LayoutTree`. Renderers
consume only that placed tree and never call the geometric engines directly.

## Design Constraints

- **Target frameworks**: `net8.0`, `net9.0`, and `net10.0`.
- **Determinism**: algorithms and engines are stateless between calls and produce reproducible
  geometry for the same input, which the byte-identity and legacy-oracle tests pin.
- **Orthogonal routing**: connectors are routed orthogonally through channels so downstream renderers
  draw axis-aligned paths without further computation.
- **Model and Abstractions dependency only**: the package depends on the rendering model and the SPI,
  with no other runtime component, keeping layout replaceable and independent of any renderer.
