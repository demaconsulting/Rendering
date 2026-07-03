# Rendering.Svg Design

## Architecture

`DemaConsulting.Rendering.Svg` is the SVG renderer system. It translates a placed `LayoutTree` from the
Rendering model into a self-contained SVG 1.1 document written to a caller-supplied stream. The system
has a single product unit:

```text
DemaConsulting.Rendering.Svg (System)
└── SvgRenderer (Unit)
```

- **SvgRenderer** — writes SVG markup for placed layout nodes, connector paths, labels, and end-marker
  definitions. Detailed in SvgRenderer Unit Design.

`SvgRenderer` realizes the `IRenderer` contract from Rendering.Abstractions. It is responsible for
vector output only: layout placement, the `LayoutTree` data model, renderer contracts, themes, and
shared notation geometry are defined by upstream systems and consumed here through their public APIs.
The renderer does not choose a layout algorithm and does not mutate the model.

## External Interfaces

- **`SvgRenderer : IRenderer`** — inbound; callers provide a `LayoutTree`, `RenderOptions`, and output
  `Stream` through the shared `IRenderer` contract. The renderer advertises the `image/svg+xml` media
  type and the `.svg` file extension for registry resolution.
- **Output stream** — outbound; UTF-8 SVG 1.1 document bytes written to the caller-owned `Stream`.

All interaction is through the in-process `IRenderer` API; there is no other external surface.

## Dependencies

The system references the *Rendering Model* package (`DemaConsulting.Rendering`) for `LayoutTree` and
node records, and the *Rendering Abstractions* package (`DemaConsulting.Rendering.Abstractions`) for the
`IRenderer` contract, `RenderOptions`, `Theme`, and the shared notation, box, and label geometry
helpers. It has **zero external runtime dependencies** beyond the .NET base class library; all NuGet
references are build-time-only private assets (SBOM, SourceLink, API documentation, and `Polyfill`). No
OTS runtime component or Shared Package is consumed — SVG markup is emitted directly as text.

## Risk Control Measures

N/A - general-purpose rendering libraries carry no safety-related risk controls requiring
architectural segregation (IEC 62304 §5.3.3).

## Data Flow

```text
LayoutTree + RenderOptions (Theme)  ──►  SvgRenderer  ──►  UTF-8 SVG bytes ──► caller Stream
```

Callers first run a layout algorithm (typically from Rendering.Layout) to place a graph, then pass the
resulting `LayoutTree` to this renderer. `SvgRenderer` reads model nodes from Rendering, applies the
supplied `Theme` and shared geometry helpers from Rendering.Abstractions, and writes SVG bytes to the
caller-owned stream. A typical pipeline runs layout first, then this renderer.

## Design Constraints

- **Target frameworks**: `net8.0`, `net9.0`, and `net10.0`.
- **Zero external dependencies**: the SVG renderer must remain dependency-free (base class library
  only) so it can be consumed in the most constrained environments.
- **Self-contained output**: the emitted document is a self-contained SVG 1.1 file with no external
  references, so it renders standalone.
- **Determinism**: identical inputs produce identical SVG text, enabling byte-level verification.
