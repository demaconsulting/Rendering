# Rendering.Svg Design

## Overview

`DemaConsulting.Rendering.Svg` is the SVG renderer system. It translates a placed
`LayoutTree` from the Rendering model into a self-contained SVG 1.1 document written to a
caller-supplied stream. The system has one product unit, `SvgRenderer`, and no runtime dependencies
beyond the .NET base class library and the in-house Rendering model and abstractions packages.

The renderer system is responsible for vector output only. Layout placement, the `LayoutTree` data
model, renderer contracts, themes, and shared notation geometry are defined by upstream systems and are
consumed here through their public APIs.

## Software Structure

```text
DemaConsulting.Rendering.Svg (System)
└── SvgRenderer (Unit)
```

- **SvgRenderer** - writes SVG markup for placed layout nodes, connector paths, labels, and end-marker
  definitions. Detailed in [SvgRenderer Unit Design](svg-renderer.md).

## System Interactions

Callers provide a `LayoutTree`, `RenderOptions`, and output `Stream` through the shared `IRenderer`
contract from Rendering.Abstractions. `SvgRenderer` reads model nodes from Rendering, uses the supplied
`Theme` and shared notation/box/label geometry helpers from Rendering.Abstractions, and writes UTF-8 SVG
bytes to the caller-owned stream.

The SVG system does not choose a layout algorithm and does not mutate the model. A typical pipeline runs
Rendering.Layout first to place a graph, then passes the resulting tree to this renderer.

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| `Rendering-Svg-WriteSvgDocument` | [SvgRenderer Unit Design](svg-renderer.md) |

## Scope Exclusions

Detailed SVG element generation, font styling, text fitting, XML escaping, and marker geometry are unit
scope and live in [SvgRenderer Unit Design](svg-renderer.md).
