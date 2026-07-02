# Engine Subsystem Design

Part of the [Rendering Layout](../rendering-layout.md) system.

## Engine Subsystem Overview

The Engine subsystem holds the reusable geometric components. Each is independent of any semantic model
and operates purely on sizes, edges, anchors, and rectangles. The `Rect` value type they consume is the
public axis-aligned rectangle in logical pixels defined by the `DemaConsulting.Rendering` model and
returned by the placement engines.

## Engine Units

```text
Engine (Subsystem)
├── OrthogonalEdgeRouter (Unit)
├── ContainmentPacker (Unit)
├── InterconnectionLayoutEngine (Unit)
└── LayeredPipeline (Unit)
```

- **OrthogonalEdgeRouter** — routes individual orthogonal connectors. Detailed in
  [OrthogonalEdgeRouter Unit Design](orthogonal-edge-router.md).
- **ContainmentPacker** — packs variable-size items into rows. Detailed in
  [ContainmentPacker Unit Design](containment-packer.md).
- **InterconnectionLayoutEngine** — adapts the layered pipeline to the geometric placement result.
  Detailed in [InterconnectionLayoutEngine Unit Design](interconnection-layout-engine.md).
- **LayeredPipeline** — the whole `Engine/Layered` staged layout pipeline. Detailed in
  [Layered Pipeline Unit Design](layered-pipeline.md).

This subsystem design intentionally does not restate unit internals; those details live in the unit
design documents listed above.

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-OrthogonalRouting | OrthogonalEdgeRouter unit design |
| Rendering-Layout-Containment | ContainmentPacker unit design |
| Rendering-Layout-Interconnection | InterconnectionLayoutEngine and LayeredPipeline unit designs |
