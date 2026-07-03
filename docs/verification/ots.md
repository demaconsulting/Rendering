# OTS Verification Evidence

This document describes the overall verification strategy for the Off-The-Shelf (OTS) software
items that the Rendering repository depends on (see "OTS Integration Design" in
`docs/design/ots.md` for the corresponding design). Each item's detailed verification evidence is
documented under `docs/verification/ots/{ots-name}.md`.

## Verification Strategy

Because most of these OTS items are compliance/build tooling rather than code this repository
authors, they are verified through a combination of self-validation and observable effect rather
than through bespoke test suites:

- **Self-validation** — most DemaConsulting tools (BuildMark, ReqStream, ReviewMark, SarifMark,
  SonarMark, VersionMark) expose a `--validate` mode that runs a built-in self-validation suite and
  writes a TRX result (for example, `dotnet reqstream --validate --results
  artifacts/reqstream-self-validation.trx`). ReqStream then traces those TRX results against the OTS
  requirements in `requirements.yaml`.
- **Generated-output assertion** — tools without a self-validation suite in this pipeline (Pandoc,
  WeasyPrint) are verified indirectly: FileAssert asserts that their generated HTML and PDF outputs
  exist and contain expected content.
- **Repository test evidence** — xUnit and SkiaSharp are verified by the repository's own passing
  tests: xUnit as the framework that discovers, executes, and records them, and SkiaSharp as the
  raster library those renderer tests exercise directly (bitmap drawing, text rendering, and image
  encoding).
- **Fixed-behavior assertion** — FileAssert itself is verified by tests that assert its own
  documented pass/fail behavior on known-good and known-bad inputs.

Each OTS item's per-item verification document names the concrete evidence (self-validation TRX,
generated artifacts, or repository test methods) and the requirements it satisfies.
