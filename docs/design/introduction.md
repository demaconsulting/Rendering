# Introduction

This document provides the detailed design for the Rendering libraries, a set of
general-purpose .NET packages that lay out and render node-and-edge diagrams. The design
is inspired by the [Eclipse Layout Kernel (ELK)](https://eclipse.dev/elk/): diagrams are
described as a graph, laid out by a pluggable algorithm, and drawn by a pluggable renderer,
all configured through an open, extensible property system.

## Purpose

The purpose of this document is to serve as the design entry point and to provide detailed
design specifications for each software item across the five Rendering systems. It enables
formal code review by providing implementation specifications, supports compliance auditing
by maintaining clear traceability from requirements through design to code, and aids
maintenance by documenting system structure and interactions.

This document is intended for:

- Software developers implementing and maintaining the libraries
- Code reviewers validating implementation against design
- Compliance auditors tracing requirements through design to implementation
- Quality assurance teams validating library behavior

## Scope

This document covers the detailed design of the five Rendering systems and their constituent
software items:

- **Rendering (System)** — the layout model: the `LayoutTree` intermediate representation,
  the open property system, and the input `LayoutGraph`.
- **Rendering.Abstractions (System)** — the service-provider interfaces: the `ILayoutAlgorithm`
  and `IRenderer` contracts, their registries, the `Theme`, render options, and the shared
  notation-geometry helpers.
- **Rendering.Layout (System)** — the pluggable layout algorithms and the reusable geometric
  engines they are built from: the ELK-style layered pipeline plus the bundled `layered`,
  `containment`, and `hierarchical` algorithms, orthogonal connector routing, and the default
  layout facade.
- **Rendering.Svg (System)** — the SVG renderer.
- **Rendering.Skia (System)** — the SkiaSharp raster renderers (PNG, JPEG, and WEBP).

The integration design for the following OTS build, compliance, and documentation tools is also
covered (their internal design is out of scope):

- **BuildMark** — build-notes documentation tool
- **FileAssert** — document assertion tool
- **Pandoc** — Markdown-to-HTML conversion tool
- **ReqStream** — requirements traceability tool
- **ReviewMark** — file review enforcement tool
- **SarifMark** — SARIF report conversion tool
- **SonarMark** — SonarCloud quality report tool
- **VersionMark** — tool-version documentation tool
- **WeasyPrint** — HTML-to-PDF conversion tool
- **xUnit** — unit-testing framework

The following topics are explicitly excluded:

- Internal design or source of third-party OTS components (only their integration/usage design is covered)
- Build pipeline configuration and CI/CD processes
- Deployment, packaging, and distribution mechanisms
- Test projects and test infrastructure

## Architectural Overview

The libraries separate *what a diagram contains* from *how it is placed* and *how it is drawn*:

```text
LayoutGraph + LayoutOptions   (Rendering: unplaced input + open property configuration)
        │
        ▼  ILayoutAlgorithm            (Rendering.Abstractions: the layout SPI)
   LayeredLayoutAlgorithm              (Rendering.Layout: the bundled "layered" algorithm)
        │
        ▼
    LayoutTree                         (Rendering: placed boxes and routed connectors)
        │
        ▼  IRenderer                   (Rendering.Abstractions: the render SPI)
 SvgRenderer / PngRenderer / JpegRenderer / WebpRenderer   (Rendering.Svg / Rendering.Skia)
        │
        ▼
   SVG, PNG, JPEG, or WEBP output
```

Configuration is **open** and **property-based**: options are declared as typed
`LayoutProperty<T>` keys and carried on any `IPropertyHolder` (the graph, a graph element, or a
free-standing `LayoutOptions`). Algorithms and renderers read only the properties they
understand, so unknown or not-yet-honored properties default harmlessly. New diagram families
and output formats are introduced additively by implementing `ILayoutAlgorithm` or `IRenderer`
and registering them — no existing contract changes.

The delivered subset implements the `layered`, `containment`, and `hierarchical` algorithms and the
SVG and SkiaSharp (PNG, JPEG, WEBP) renderers.

## Software Structure

The following tree shows how the Rendering software items are organized across System,
Subsystem, and Unit levels according to the software-items classification standard:

```text
Rendering (System)
├── LayoutTree (Unit)      — immutable placed intermediate representation records
├── Options (Unit)         — open property system (LayoutProperty, IPropertyHolder, LayoutOptions, CoreOptions)
└── LayoutGraph (Unit)     — unplaced input graph model

Rendering.Abstractions (System)
├── RenderingContracts (Unit)   — ILayoutAlgorithm, IRenderer, RenderOptions, RenderOutput
├── Registries (Unit)           — LayoutAlgorithmRegistry, RendererRegistry
├── Theme (Unit)                — Theme record and built-in Themes
├── NotationMetrics (Unit)      — intrinsic notation geometry shared by renderers
├── BoxMetrics (Unit)           — box title-area and folder-tab geometry
└── ConnectorLabelPlacer (Unit) — collision-aware connector-label placement

Rendering.Layout (System)
├── Engine (Subsystem)
│   ├── OrthogonalEdgeRouter (Unit)       — orthogonal (channel) edge router
│   ├── ContainmentPacker (Unit)          — shelf packer for grouped/containment layout
│   ├── InterconnectionLayoutEngine (Unit)— cross-edge routing among placed boxes
│   └── LayeredPipeline (Unit)            — the ELK-style layered Sugiyama stage pipeline
├── LayeredLayoutAlgorithm (Unit)        — the public layered ILayoutAlgorithm
├── ContainmentLayoutAlgorithm (Unit)    — the public containment ILayoutAlgorithm
├── HierarchicalLayoutAlgorithm (Unit)   — the recursive hierarchical engine
├── ContainmentLayout (Unit)             — public containment packing entry point
├── ConnectorRouter (Unit)              — public edge-routing orchestration
├── EdgeRoutingOption (Unit)            — the EdgeRouting option realization
└── DefaultLayout (Unit)               — LayoutEngine facade + default algorithm registry

Rendering.Svg (System)
└── SvgRenderer (Unit)

Rendering.Skia (System)
├── SkiaRasterRenderer (Unit)   — abstract SkiaSharp rasterizer shared by all formats
├── PngRenderer (Unit)          — lossless PNG output
├── JpegRenderer (Unit)         — JPEG output
├── WebpRenderer (Unit)          — WEBP output

OTS Software Items
├── BuildMark    — build-notes documentation from GitHub Actions metadata
├── FileAssert   — generated-document assertion tool
├── Pandoc       — Markdown-to-HTML conversion
├── ReqStream    — requirements traceability and enforcement
├── ReviewMark   — file-review plan, report, and enforcement
├── SarifMark    — CodeQL SARIF-to-Markdown conversion
├── SkiaSharp    — raster graphics library (bitmap drawing and PNG/JPEG/WEBP encoding)
├── SonarMark    — SonarCloud quality-report generation
├── VersionMark  — tool-version capture and publishing
├── WeasyPrint   — HTML-to-PDF (PDF/A) conversion
└── xUnit        — unit-testing framework
```

Most OTS software items are compliance, build, and documentation tooling consumed while building,
verifying, and documenting the Rendering libraries, and are not linked into the delivered packages.
SkiaSharp is the one exception: it is a runtime library linked into the delivered
`DemaConsulting.Rendering.Skia` package. Their integration design is described in the OTS
Integration Design section and the per-item documents under `docs/design/ots/`.

Package dependencies form an acyclic graph: `Abstractions` and `Layout` depend on the
`Rendering` model; `Svg` and `Skia` depend on the model and `Abstractions`; the model depends
on nothing.

## Companion Artifact Structure

Each software item has corresponding artifacts in parallel directory trees. Each system is
decomposed into a slim system-level file plus one file per unit (and per subsystem where one
exists, such as the Layout `engine/` subsystem), so a system-level review excludes unit detail and
each unit review carries only its own slice:

- Requirements: `docs/reqstream/{system}.yaml` (system-level) plus
  `docs/reqstream/{system}/[{subsystem}/]{unit}.yaml` per unit (kebab-case)
- Design docs: `docs/design/{system}.md` plus `docs/design/{system}/[{subsystem}/]{unit}.md`
- Verification design: `docs/verification/{system}.md` plus
  `docs/verification/{system}/[{subsystem}/]{unit}.md`
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)
- Review-sets: defined in `.reviewmark.yaml`

OTS items sit parallel to the system folders and have no source code:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml` (kebab-case)
- Design docs: `docs/design/ots.md` (integration index) plus `docs/design/ots/{ots-name}.md`
- Verification: `docs/verification/ots/{ots-name}.md` (kebab-case)

The five systems map to these kebab-case folders:

| NuGet Package | kebab-case system folder |
| --- | --- |
| `DemaConsulting.Rendering` | `rendering` |
| `DemaConsulting.Rendering.Abstractions` | `rendering-abstractions` |
| `DemaConsulting.Rendering.Layout` | `rendering-layout` |
| `DemaConsulting.Rendering.Svg` | `rendering-svg` |
| `DemaConsulting.Rendering.Skia` | `rendering-skia` |

## Folder Layout

```text
src/
├── DemaConsulting.Rendering/            — model: IR, property system, input graph
│   ├── LayoutTree/                      — LayoutTree and node records
│   ├── Options/                         — property system and CoreOptions
│   └── Graph/                           — LayoutGraph input model
├── DemaConsulting.Rendering.Abstractions/ — SPI contracts, registries, theme, metrics
├── DemaConsulting.Rendering.Layout/     — layout algorithms
│   └── Engine/Layered/                  — ELK-style layered pipeline stages
├── DemaConsulting.Rendering.Svg/        — SVG renderer
└── DemaConsulting.Rendering.Skia/       — SkiaSharp raster renderers (PNG, JPEG, WEBP; Noto Sans)
```

## Document Conventions

Throughout this document:

- Class names, method names, property names, and file names appear in `monospace` font.
- The word **shall** denotes a design constraint that the implementation must satisfy.
- Section headings within each unit chapter follow a consistent structure: overview, data model,
  methods/algorithms, and interactions with other units.
- Text tables are used in preference to diagrams, which may not render in all PDF viewers.

## References

- [REF-1] Rendering User Guide (<https://github.com/demaconsulting/Rendering/blob/main/docs/user_guide/introduction.md>)
- [REF-2] Rendering Repository (<https://github.com/demaconsulting/Rendering>)
- [REF-3] Eclipse Layout Kernel (<https://eclipse.dev/elk/>)
