## VersionMark Integration Design

### Purpose

VersionMark (`demaconsulting.versionmark`) captures and publishes the versions of the tools used across
the build pipeline. It records the version of each dotnet tool (and supporting programs such as `git`,
`node`, and `npm`) per CI job and publishes a consolidated tool-versions Markdown document included in
the release artifacts, providing a reproducible record of the toolchain that produced each build.

### Features Used

- **Version capture** (`--capture --job-id {id} --output {json} -- {tools...}`) — records the versions
  of the listed tools for a CI job into a JSON file.
- **Self-validation** (`--validate --results`) — runs VersionMark's built-in self-validation suite and
  writes a TRX result.
- **Publish** (`--publish --report docs/build_notes/generated/versions.md --report-depth 1 --
  "artifacts/**/versionmark-*.json"`) — merges the captured per-job JSON files into a single versions
  Markdown report.
- **Lint** — validates the `.versionmark.yaml` configuration for structural and semantic errors.

### Integration Pattern

VersionMark is installed as a .NET local tool via `.config/dotnet-tools.json` and invoked as
`dotnet versionmark`. Each CI job in `.github/workflows/build.yaml` runs a capture step that writes an
`artifacts/versionmark-{job}.json` file and a self-validation step that writes a TRX result; the
documentation-build job then runs the publish step to merge all captured JSON files into the versions
report. Its inputs are the per-job capture JSON files; its output is written to the build-notes
`generated/` folder and compiled by Pandoc and WeasyPrint. VersionMark is not referenced by the
delivered Rendering packages.
