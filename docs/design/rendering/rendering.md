# Rendering Model Design

## Overview

`DemaConsulting.Rendering` is the SysML-agnostic rendering model. It defines the data types that
flow through the rendering pipeline but contains no layout algorithm and no renderer. It provides
three things: the placed `LayoutTree` intermediate representation that a renderer draws, the open
(ELK-inspired) property-based option system that carries configuration, and the unplaced
`LayoutGraph` input that a layout algorithm consumes. The package has no runtime dependencies beyond
the .NET base class library, and its types are immutable records or small mutable holders.

A layout algorithm consumes a `LayoutGraph` plus a `LayoutOptions` and produces a placed
`LayoutTree`; a renderer then draws that tree. This document describes the model that sits at both
ends of that flow. The algorithm and renderer contracts themselves live in the
*Rendering Abstractions* system.

## Software Structure

```text
DemaConsulting.Rendering (System)
├── LayoutTree (Unit)
├── Options (Unit)
└── LayoutGraph (Unit)
```

- **Layout Tree** — the placed intermediate representation: `LayoutTree` and the `LayoutNode`
  discriminated-union hierarchy of concrete node records, plus the shared `Point2D` / `Rect`
  geometry value types. Detailed in Layout Tree Unit Design.
- **Options** — the open configuration system: `LayoutProperty<T>`, `IPropertyHolder`,
  `PropertyHolder`, `LayoutOptions`, `CoreOptions`, `LayoutFlowDirection`, and `HierarchyHandling`.
  Detailed in Options Unit Design.
- **Layout Graph** — the unplaced input model: `LayoutGraph`, `LayoutGraphNode`, `LayoutGraphEdge`.
  Detailed in Layout Graph Unit Design.

## System Interactions

A `LayoutGraph` plus a `LayoutOptions` is the input consumed by an `ILayoutAlgorithm` (defined in the
*Rendering Abstractions* system); the algorithm produces a placed `LayoutTree`, which an `IRenderer`
then draws to a concrete output format. The Options unit's `PropertyHolder` is the base type for the
Layout Graph elements, so configuration flows from the input model into the algorithms uniformly. The
model itself performs no layout and no rendering; it defines only the shared vocabulary at both ends
of the pipeline.

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Model-LayoutTree | The `LayoutTree` and `LayoutNode` hierarchy (see Layout Tree Unit Design) |
| Rendering-Model-Configuration | The `IPropertyHolder` / `LayoutProperty<T>` option system (see Options Unit Design) |
| Rendering-Model-InputGraph | The `LayoutGraph` input model (see Layout Graph Unit Design) |

## Model Scope Exclusions

The following public members are optional presentation or diagnostic metadata that the model carries
through unchanged. The
model's only obligation is to store and return them unchanged; they carry no algorithmic behavior of
their own, and the rendered behavior they influence is specified and verified in the renderer systems
(`DemaConsulting.Rendering.Svg` / `DemaConsulting.Rendering.Skia`), not in the model. They are
therefore intentionally excluded from the Rendering-Model requirement set and are not given
individual functional requirements or verification scenarios:

| Member | Kind | Rationale |
| --- | --- | --- |
| `LayoutBox.Keyword` | presentation | Optional SysML keyword drawn above the box label; renderer pass-through. |
| `LayoutTree.Warnings` | diagnostic | Non-fatal layout-quality diagnostics; advisory only. |
| `LayoutGraphNode.Label` | input-graph presentation | Optional display text carried to the renderer. |
| `LayoutGraphEdge.Label` | input-graph presentation | Optional midpoint display text carried to the renderer. |
| `LayoutGraphEdge.TargetEnd` | input-graph presentation | Optional end-marker style hint carried to the renderer. |
| `LayoutGraphEdge.LineStyle` | input-graph presentation | Optional stroke-style hint carried to the renderer. |
