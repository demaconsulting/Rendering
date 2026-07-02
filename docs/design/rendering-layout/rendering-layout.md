# Rendering.Layout Design

## Overview

`DemaConsulting.Rendering.Layout` is the placement system for the Rendering stack. A caller supplies a
`LayoutGraph` plus `LayoutOptions`; the system returns a placed `LayoutTree` of boxes and orthogonally
routed connectors that downstream renderers can draw without layout knowledge.

The system exposes bundled algorithms for layered, containment, and hierarchical layout. Reusable
geometric engines provide orthogonal routing, containment packing, and layered interconnection placement,
while the default facade resolves the requested algorithm from the bundled registry.

## Software Structure

```text
DemaConsulting.Rendering.Layout (System)
├── Engine (Subsystem)
│   ├── OrthogonalEdgeRouter (Unit)
│   ├── ContainmentPacker (Unit)
│   ├── InterconnectionLayoutEngine (Unit)
│   └── LayeredPipeline (Unit)
├── EdgeRoutingOption (Unit)
├── ConnectorRouter (Unit)
├── ContainmentLayout (Unit)
├── ContainmentLayoutAlgorithm (Unit)
├── HierarchicalLayoutAlgorithm (Unit)
├── DefaultLayout (Unit)
└── LayeredLayoutAlgorithm (Unit)
```

- **Engine** - reusable model-agnostic geometric engines. Detailed in
  [Engine Subsystem Design](engine/engine.md), which links to its unit designs.
- **EdgeRoutingOption** - routing-style configuration keys. Detailed in
  [EdgeRoutingOption Unit Design](edge-routing-option.md).
- **ConnectorRouter** - routes connectors among already placed boxes. Detailed in
  [ConnectorRouter Unit Design](connector-router.md).
- **ContainmentLayout** - packs already sized model boxes into a container region. Detailed in
  [ContainmentLayout Unit Design](containment-layout.md).
- **ContainmentLayoutAlgorithm** - public containment algorithm. Detailed in
  [ContainmentLayoutAlgorithm Unit Design](containment-layout-algorithm.md).
- **HierarchicalLayoutAlgorithm** - recursive compound-graph algorithm. Detailed in
  [HierarchicalLayoutAlgorithm Unit Design](hierarchical-layout-algorithm.md).
- **DefaultLayout** - bundled registry factory and layout facade. Detailed in
  [DefaultLayout Unit Design](default-layout.md).
- **LayeredLayoutAlgorithm** - public layered algorithm. Detailed in
  [LayeredLayoutAlgorithm Unit Design](layered-layout-algorithm.md).

## System Interactions

The system consumes `LayoutGraph`, `LayoutOptions`, `CoreOptions`, and `LayoutTree` from the Rendering
model and implements `ILayoutAlgorithm` from Rendering.Abstractions. Callers may use the bundled registry
and `LayoutEngine` facade, or resolve a specific bundled algorithm directly. Renderers consume only the
placed tree produced by this system; they do not call the geometric engines directly.

The Engine subsystem operates on model-agnostic geometry and is composed by the public algorithms and
routing helpers. The hierarchical algorithm composes leaf algorithms per container and routes
cross-container edges at the owning scope.

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| `Rendering-Layout-Algorithm` | [LayeredLayoutAlgorithm](layered-layout-algorithm.md) |
| `Rendering-Layout-GeometricEngines` | [Engine Subsystem](engine/engine.md) |
| `Rendering-Layout-StagedPipeline` | [LayeredPipeline](engine/layered-pipeline.md) |
| `Rendering-Layout-ConnectorRouting` | [EdgeRoutingOption](edge-routing-option.md), [ConnectorRouter](connector-router.md) |
| `Rendering-Layout-ContainmentPlacement` | [ContainmentLayout](containment-layout.md) |
| `Rendering-Layout-ContainmentAlgorithm` | [ContainmentLayoutAlgorithm](containment-layout-algorithm.md) |
| `Rendering-Layout-HierarchicalLayout` | [HierarchicalLayoutAlgorithm](hierarchical-layout-algorithm.md) |
| `Rendering-Layout-DefaultLayout` | [DefaultLayout](default-layout.md) |

## Scope Exclusions

Per-unit data models, algorithms, error handling, and design constraints are intentionally excluded from
this system document and live in the unit and subsystem documents linked above. Test projects and test
infrastructure are verification artifacts, not product design scope.
