## BuildMark Integration Design

### Purpose

BuildMark (`DemaConsulting.BuildMark`) generates a build-notes document for each release of the
Rendering libraries. It queries the GitHub API to capture workflow run details, version tags, commit
history, and issue/pull-request activity, and renders them as a Markdown build-notes document that is
compiled into the release artifacts. It provides the repository's automated release-notes evidence
without hand-maintained changelogs.

### Features Used

- **Self-validation** (`--validate --results`) — runs BuildMark's built-in self-validation suite and
  writes a TRX result consumed by ReqStream for OTS traceability.
- **Build-notes report generation** (`--build-version`, `--report`, `--report-depth`) — produces
  `docs/build_notes/generated/build_notes.md` from the current build version and GitHub Actions
  metadata, including Git tag/commit integration, GitHub issue and pull-request tracking, known-issue
  reporting, and label/type-based routing of items into report sections.

### Integration Pattern

BuildMark is installed as a .NET local tool via `.config/dotnet-tools.json` and restored with
`dotnet tool restore`. It is invoked as `dotnet buildmark` from the documentation-build job of
`.github/workflows/build.yaml`. Self-validation runs first (writing
`artifacts/buildmark-self-validation.trx`), then the report step generates the build-notes Markdown,
which Pandoc converts to HTML and WeasyPrint renders to a PDF/A artifact. It reads GitHub Actions
environment metadata and the repository's Git history as input and writes into the
`docs/build_notes/generated/` folder. BuildMark is not referenced by the delivered Rendering packages.
