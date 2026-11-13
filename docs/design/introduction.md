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
- **Rendering.Abstractions (System)** — the service-provider interfaces: the `LayoutAlgorithmBase`
  and `IRenderer` contracts, their registries, the `Theme`, render options, and the shared
  notation-geometry helpers.
- **Rendering.Layout (System)** — the pluggable layout algorithms and the reusable geometric
  engines they are built from: the ELK-style layered pipeline plus the bundled `layered`,
  `containment`, `hierarchical`, and `auto` algorithms, orthogonal connector routing, and the default
  layout facade.
- **Rendering.Svg (System)** — the SVG renderer.
- **Rendering.CanvasNet (System)** — the CanvasNet raster renderers (PNG and JPEG).

The integration design for the following OTS build, compliance, and documentation tools is also
covered (their internal design is out of scope):

- **BuildMark** — build-notes documentation tool
- **FileAssert** — document assertion tool
- **Pandoc** — Markdown-to-HTML conversion tool
- **ReqStream** — requirements traceability tool
- **ReviewMark** — file review enforcement tool
- **SarifMark** — SARIF report conversion tool
- **SonarMark** — SonarCloud quality report tool
- **SysML2Tools** — SysML2 architecture-model linting and view-rendering tool
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
        ▼  LayoutAlgorithmBase         (Rendering.Abstractions: the layout SPI)
   LayeredLayoutAlgorithm              (Rendering.Layout: the bundled "layered" algorithm)
        │
        ▼
    LayoutTree                         (Rendering: placed boxes and routed connectors)
        │
        ▼  IRenderer                   (Rendering.Abstractions: the render SPI)
 SvgRenderer / PngRenderer / JpegRenderer   (Rendering.Svg / Rendering.CanvasNet)
        │
        ▼
   SVG, PNG, or JPEG output
```

Configuration is **open** and **property-based**: options are declared as typed
`LayoutProperty<T>` keys and carried on any `IPropertyHolder` (the graph, a graph element, or a
free-standing `LayoutOptions`). Algorithms and renderers read only the properties they
understand, so unknown or not-yet-honored properties default harmlessly. New diagram families
and output formats are introduced additively by deriving from `LayoutAlgorithmBase` or implementing
`IRenderer`
and registering them — no existing contract changes.

The delivered subset implements the `layered`, `containment`, `hierarchical`, and `auto` algorithms and
the SVG and CanvasNet (PNG, JPEG) renderers.

## Software Structure

The software structure is modeled in SysML2 under `docs/sysml2/` (see
`docs/sysml2/model/**/*.sysml`) and rendered to the diagram below by SysML2Tools as part
of the build pipeline. Each of the five systems is its own SysML2 package (the
`DemaConsulting.Rendering` model system is the `RenderingModel` package) containing a system
part definition and its subsystems and units; the OTS items are in the `OtsDependencies`
package. The SysML2 model — not this diagram or prose — is the authoritative,
machine-queryable source of structure. AI agents should query the SysML2 model directly
(see the `sysml2tools-query` skill) rather than parsing this diagram before deep-diving into
source code.

![Software Structure](SoftwareStructureView.svg)

The diagram below complements the structure view by showing the libraries in their run-time
context: the calling application describes a diagram as a `LayoutGraph` in the core model, a
layout algorithm from `.Layout` places it into a `LayoutTree`, and a renderer from `.Svg` or
`.CanvasNet` draws it as SVG, PNG, or JPEG, with the layout algorithms and renderers built on
the `.Abstractions` contracts.

![Rendering architecture: your application describes a diagram as a graph, the core model,
layout algorithms, and renderers turn it into SVG, PNG, and JPEG output, and the layout
algorithms and renderers implement the Abstractions contracts](architecture.svg)

Most OTS software items are compliance, build, and documentation tooling consumed while building,
verifying, and documenting the Rendering libraries, and are not linked into the delivered packages.
CanvasNet is the one exception: it is a pure managed runtime library linked into the delivered
`DemaConsulting.Rendering.CanvasNet` package, with no P/Invoke layer or platform-specific native
asset packages. Their integration design is described in the OTS Integration Design section and the
per-item documents under `docs/design/ots/`.

Package dependencies form an acyclic graph: `Abstractions` and `Layout` depend on the
`Rendering` model; `Svg` and `CanvasNet` depend on the model and `Abstractions`; the model
depends on nothing.

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
| `DemaConsulting.Rendering.CanvasNet` | `rendering-canvasnet` |

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
└── DemaConsulting.Rendering.CanvasNet/   — CanvasNet raster renderers (PNG, JPEG; Noto Sans)
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
