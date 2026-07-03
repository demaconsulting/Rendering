# OTS Integration Design

This document describes the overall strategy for integrating the Off-The-Shelf (OTS) software items
that the Rendering repository depends on. These OTS items are compliance, build, and documentation
tooling rather than runtime libraries linked into the delivered packages: they are consumed while
building, verifying, and documenting the Rendering libraries, and none of them ship inside the
`DemaConsulting.Rendering*` NuGet packages.

## OTS Items

The repository integrates ten OTS items:

```text
OTS Software Items
├── BuildMark    — build-notes documentation from GitHub Actions metadata
├── FileAssert   — generated-document assertion tool
├── Pandoc       — Markdown-to-HTML conversion
├── ReqStream    — requirements traceability and enforcement
├── ReviewMark   — file-review plan, report, and enforcement
├── SarifMark    — CodeQL SARIF-to-Markdown conversion
├── SonarMark    — SonarCloud quality-report generation
├── VersionMark  — tool-version capture and publishing
├── WeasyPrint   — HTML-to-PDF (PDF/A) conversion
└── xUnit        — unit-testing framework
```

Each item has its own integration design under `docs/design/ots/{ots-name}.md` describing its Purpose,
Features Used, and Integration Pattern. This document covers the shared integration strategy.

## Integration Strategy

The OTS items fall into two consumption models:

- **.NET local tools** — BuildMark, FileAssert, Pandoc, ReqStream, ReviewMark, SarifMark, SonarMark,
  VersionMark, and WeasyPrint are installed as local .NET tools through the `.config/dotnet-tools.json`
  manifest and restored with `dotnet tool restore`. They are invoked as `dotnet {tool}` commands from
  the CI workflows (`.github/workflows/build.yaml` and `release.yaml`) and, for the linting subset,
  from `lint.ps1`. The Pandoc and WeasyPrint tools are DemaConsulting distributions
  (`demaconsulting.pandoctool`, `demaconsulting.weasyprinttool`) that package the underlying converters
  as .NET tools.
- **Test framework** — xUnit is referenced as a NuGet test-framework dependency by the test projects
  and is exercised by `dotnet test`; it discovers and runs the repository's own test methods and
  records TRX results.

Configuration is file-driven: the tools read repository configuration such as `requirements.yaml`,
`.reviewmark.yaml`, `.versionmark.yaml`, and the per-collection Pandoc `definition.yaml` manifests, and
they write their output into `generated/` folders and `artifacts/` TRX files. Compliance tools that
support an `--enforce` mode (ReqStream, ReviewMark, SarifMark) can fail the build when a compliance
condition is not met.

## Verification Strategy

Because these OTS items are compliance/build tooling, each is verified through a combination of its own
self-validation suite and its observable effect in the CI document-generation chain. Most DemaConsulting
tools expose a `--validate` mode that runs a built-in self-validation suite and writes a TRX result
(for example, `dotnet reqstream --validate --results artifacts/reqstream-self-validation.trx`); ReqStream
then traces those TRX results against the OTS requirements in `requirements.yaml`. Tools without a
self-validation suite in this pipeline (Pandoc, WeasyPrint) are verified indirectly: FileAssert asserts
that their generated HTML and PDF outputs exist and contain expected content. xUnit is verified by the
repository's own passing tests, which it discovers, executes, and records. The per-item verification
evidence is documented under `docs/verification/ots/{ots-name}.md`.
