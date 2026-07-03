### Engine Subsystem Design

Part of the Rendering Layout system.

#### Engine Subsystem Overview

The Engine subsystem holds the reusable geometric components. Each is independent of any semantic model
and operates purely on sizes, edges, anchors, and rectangles. The `Rect` value type they consume is the
public axis-aligned rectangle in logical pixels defined by the `DemaConsulting.Rendering` model and
returned by the placement engines.

#### Engine Units

```text
Engine (Subsystem)
├── OrthogonalEdgeRouter (Unit)
├── ContainmentPacker (Unit)
├── InterconnectionLayoutEngine (Unit)
└── LayeredPipeline (Unit)
```

- **OrthogonalEdgeRouter** — routes individual orthogonal connectors. Detailed in
  OrthogonalEdgeRouter Unit Design.
- **ContainmentPacker** — packs variable-size items into rows. Detailed in
  ContainmentPacker Unit Design.
- **InterconnectionLayoutEngine** — adapts the layered pipeline to the geometric placement result.
  Detailed in InterconnectionLayoutEngine Unit Design.
- **LayeredPipeline** — the whole `Engine/Layered` staged layout pipeline. Detailed in
  Layered Pipeline Unit Design.

This subsystem design intentionally does not restate unit internals; those details live in the unit
design documents listed above.

#### Engine Interfaces

The Engine subsystem is a Layout-internal collection of geometric services; it exposes no public
`ILayoutAlgorithm` or `IRenderer` interface of its own. Its interfaces are:

- **Inbound (composed by the public Layout algorithms and by `ConnectorRouter`):**
  - `OrthogonalEdgeRouter.RouteWithStatus(source, target, obstacles, clearance, sourceSide?,
    targetSide?, costBands?)` and its thin `Route` wrapper — single-connector orthogonal routing
    returning ordered `Point2D` waypoints and a `Crossed` flag.
  - `ContainmentPacker.Pack(items, options)` — shelf-packs sized items into rows within a content
    width and returns their placed rectangles plus the enclosing region.
  - `InterconnectionLayoutEngine.Place(graph, options)` — placement adapter that runs the
    `LayeredPipeline` and returns the placed rectangles and routed connectors for a graph.
  - `LayeredLayoutPipeline.RunDefaultStages(graph)` (and the composable `AddStage` / `Run` pair) —
    the ELK-style staged pipeline over a `LayeredGraph`.
- **Consumed (from other software items):**
  - The Rendering-model geometric value types (`Point2D`, `Rect`, `PortSide`, `CostBand`) and the
    `LayeredGraph` internal representation used by the pipeline stages.

The subsystem exposes no external SPI: renderers and application code never call these engines
directly; they reach them only through the public Layout algorithms (`LayeredLayoutAlgorithm`,
`ContainmentLayoutAlgorithm`, `HierarchicalLayoutAlgorithm`) and `ConnectorRouter`.

#### Engine Design

The subsystem is organized as four independent geometric leaves that the public algorithms
compose:

- **OrthogonalEdgeRouter** is a stateless static class that performs A\*-style search over a grid
  derived from endpoint and obstacle coordinates, with a clearance-retry ladder and a turn penalty;
  every orthogonal single-connector route in the system passes through it.
- **ContainmentPacker** shelf-packs variable-size items into rows for a content-width budget; it
  underpins `ContainmentLayout` and the layered-pipeline `ComponentPacker` stage.
- **InterconnectionLayoutEngine** adapts a `LayoutGraph` into the pipeline's `LayeredGraph`, runs
  the default pipeline, and translates the pipeline output back to placed rectangles and routed
  connectors that the public algorithms wrap in `LayoutBox` / `LayoutLine` nodes.
- **LayeredPipeline** owns the ELK-style Sugiyama sequence — cycle breaking, layer assignment,
  long-edge splitting, crossing minimization, port distribution, Brandes-Köpf coordinate
  assignment, orthogonal routing, long-edge joining, component packing, and the axis transform.

Each engine is deterministic and stateless between calls: no engine mutates its inputs. Data flows
one direction only — the public algorithms drive the engines; engines never call back into the
public algorithms. This keeps the subsystem replaceable and independently testable, and lets the
same `OrthogonalEdgeRouter` and `ContainmentPacker` implementations back both the layered pipeline
and the free-form `ConnectorRouter` / `ContainmentLayout` entry points.

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-OrthogonalRouting | OrthogonalEdgeRouter unit design |
| Rendering-Layout-Containment | ContainmentPacker unit design |
| Rendering-Layout-Interconnection | InterconnectionLayoutEngine and LayeredPipeline unit designs |
