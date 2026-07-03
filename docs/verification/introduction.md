# Introduction

This document provides the verification design for the Rendering libraries, a set of
general-purpose .NET packages for laying out and rendering node-and-edge diagrams.

## Purpose

The purpose of this document is to serve as the verification design entry point and to document
how requirements will be tested across all software items in the five Rendering systems. It
enables formal review by mapping every requirement to named test scenarios, supports compliance
auditing by providing traceability from requirements through verification design to tests, and
lets test completeness be assessed without reading implementation code.

This document is intended for:

- Software developers implementing and maintaining tests
- Code reviewers validating test completeness against requirements
- Compliance auditors tracing requirements through verification design to tests
- Quality assurance teams validating test coverage and scenario adequacy

## Scope

This document covers the verification design for the five Rendering systems and their
constituent software items:

- **Rendering (System)** — layout model: `LayoutTree` IR, property system, `LayoutGraph`
- **Rendering.Abstractions (System)** — SPI contracts, registries, theme, notation metrics
- **Rendering.Layout (System)** — layered pipeline engines and `LayeredLayoutAlgorithm`
- **Rendering.Svg (System)** — SVG renderer
- **Rendering.Skia (System)** — SkiaSharp raster renderers (PNG, JPEG, WEBP)

The following OTS items are also covered:

- **BuildMark** — build-notes documentation tool
- **FileAssert** — document assertion tool
- **Pandoc** — Markdown-to-HTML conversion tool
- **ReqStream** — requirements traceability tool
- **ReviewMark** — file review enforcement tool
- **SarifMark** — SARIF report conversion tool
- **SkiaSharp** — raster graphics library (bitmap drawing and PNG/JPEG/WEBP encoding)
- **SonarMark** — SonarCloud quality report tool
- **VersionMark** — tool-version documentation tool
- **WeasyPrint** — HTML-to-PDF conversion tool
- **xUnit** — unit-testing framework

This verification documentation covers the same in-house software items as the design
documentation. The following topics are explicitly excluded:

- Build pipeline and CI/CD process testing
- Infrastructure and hosting environment testing
- Test projects and test infrastructure

## Verification Approach

Each software item is verified by xUnit v3 tests executed by `dotnet test` (invoked by
`build.ps1` and CI) across the supported target frameworks (.NET 8, 9, and 10). Because the
libraries are pure and deterministic, no mocking is required: tests supply controlled inputs
(graphs, layout trees, options) and assert on returned values, produced geometry, or rendered
output. The layered pipeline additionally carries byte-for-byte equivalence and legacy-oracle
tests that pin its numeric output. Requirement coverage is proven by an auto-generated trace
matrix linking each requirement to the passing tests named in its verification scenarios.

## Companion Artifact Structure

In-house items have artifacts in these parallel locations:

- Requirements: `docs/reqstream/{system}/{system}.yaml` (kebab-case)
- Design docs: `docs/design/{system}/{system}.md` (kebab-case)
- Verification design: `docs/verification/{system}/{system}.md` (kebab-case)
- Source code: `src/{System}/.../{Item}.cs` (PascalCase for C#)
- Tests: `test/{System}.Tests/.../{Item}Tests.cs` (PascalCase for C#)

OTS items have parallel artifacts in:

- Requirements: `docs/reqstream/ots/{ots-name}.yaml` (kebab-case)
- Verification: `docs/verification/ots/{ots-name}.md` (kebab-case)

Review-sets: defined in `.reviewmark.yaml`

## References

- [REF-1] Rendering User Guide (<https://github.com/demaconsulting/Rendering/blob/main/docs/user_guide/introduction.md>)
- [REF-2] Rendering Repository (<https://github.com/demaconsulting/Rendering>)
