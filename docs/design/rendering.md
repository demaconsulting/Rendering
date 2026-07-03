# Rendering Model Design

## Architecture

`DemaConsulting.Rendering` is the SysML-agnostic rendering model. It defines the data types that flow
through the rendering pipeline but contains no layout algorithm and no renderer. It provides three
things: the placed `LayoutTree` intermediate representation that a renderer draws, the open
(ELK-inspired) property-based option system that carries configuration, and the unplaced `LayoutGraph`
input that a layout algorithm consumes.

The system is composed of three units:

```text
DemaConsulting.Rendering (System)
├── LayoutTree (Unit)
├── Options (Unit)
└── LayoutGraph (Unit)
```

- **Layout Tree** — the placed intermediate representation: `LayoutTree` and the `LayoutNode`
  discriminated-union hierarchy of concrete node records, plus the shared `Point2D` / `Rect` geometry
  value types. Detailed in Layout Tree Unit Design.
- **Options** — the open configuration system: `LayoutProperty<T>`, `IPropertyHolder`,
  `PropertyHolder`, `LayoutOptions`, `CoreOptions`, `LayoutFlowDirection`, and `HierarchyHandling`.
  Detailed in Options Unit Design.
- **Layout Graph** — the unplaced input model: `LayoutGraph`, `LayoutGraphNode`, `LayoutGraphEdge`.
  Detailed in Layout Graph Unit Design.

The units collaborate through the Options unit's `PropertyHolder`, which is the base type for the
Layout Graph elements, so configuration flows from the input model into the algorithms uniformly. The
model performs no layout and no rendering; it defines only the shared vocabulary at both ends of the
pipeline. A layout algorithm (defined in the *Rendering Abstractions* system) consumes a `LayoutGraph`
plus a `LayoutOptions` and produces a placed `LayoutTree`; a renderer then draws that tree.

## External Interfaces

The model exposes its contract as public .NET types rather than service interfaces:

- **`LayoutGraph` (with `LayoutGraphNode`, `LayoutGraphEdge`)** — outbound to layout algorithms; the
  unplaced input a caller builds and an `ILayoutAlgorithm` consumes. In-memory object model.
- **`LayoutOptions` / `IPropertyHolder` / `LayoutProperty<T>` / `CoreOptions`** — outbound to both
  algorithms and renderers; typed property keys carried on any property holder. Unknown properties
  are ignored, so callers may set options that a given algorithm or renderer does not honor.
- **`LayoutTree` (with the `LayoutNode` hierarchy, `Point2D`, `Rect`)** — outbound to renderers; the
  placed representation an `IRenderer` draws. Immutable records with absolute coordinates.

All interfaces are in-process .NET APIs; there are no network, file, or wire-format contracts.

## Dependencies

The model has no project references and no runtime NuGet package dependencies beyond the .NET base
class library. Build-time-only packages (SBOM generation, SourceLink, API documentation, and
`Polyfill` for newer BCL surface on older target frameworks) are private build assets and are not part
of the delivered runtime surface. No OTS runtime component or Shared Package is consumed.

## Risk Control Measures

N/A - general-purpose rendering libraries carry no safety-related risk controls requiring
architectural segregation (IEC 62304 §5.3.3).

## Data Flow

The model sits at both ends of the rendering pipeline:

```text
LayoutGraph + LayoutOptions        (Rendering: unplaced input + open configuration)
        │
        ▼  ILayoutAlgorithm        (Rendering.Abstractions: layout SPI)
    LayoutTree                     (Rendering: placed boxes and routed connectors)
        │
        ▼  IRenderer               (Rendering.Abstractions: render SPI)
    SVG / PNG / JPEG / WEBP output
```

Input flows in as a `LayoutGraph` and a `LayoutOptions`; a layout algorithm reads the graph and its
properties and emits a placed `LayoutTree`; a renderer reads that tree and writes a concrete output
format. The model itself only defines and carries these values; it neither transforms nor emits them.

## Design Constraints

- **Target frameworks**: `net8.0`, `net9.0`, and `net10.0`.
- **Determinism and immutability**: the placed types are immutable records or small mutable holders;
  stored values are returned unchanged so layout and rendering are reproducible.
- **Absolute coordinates**: `LayoutTree` node geometry uses absolute coordinates, not color- or
  depth-relative encodings, so any renderer can draw the tree without re-deriving placement.
- **Zero runtime dependencies**: the model depends only on the .NET base class library, keeping it a
  neutral shared vocabulary that every upstream and downstream package can reference without pulling
  in additional runtime components.
