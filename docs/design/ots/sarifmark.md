## SarifMark Integration Design

### Purpose

SarifMark (`DemaConsulting.SarifMark`) converts the SARIF output produced by CodeQL code scanning into a
human-readable Markdown quality report that is compiled into the release artifacts. In enforcement mode
it also gates the build on detected issues, letting CI fail when static analysis reports code-quality
violations.

### Features Used

- **Self-validation** (`--validate --results`) — runs SarifMark's built-in self-validation suite and
  writes a TRX result.
- **SARIF-to-Markdown report generation** (`--sarif artifacts/csharp.sarif`, `--report`, `--heading`,
  `--report-depth`) — reads the CodeQL SARIF file and renders a Markdown report into
  `docs/code_quality/generated/`.
- **Enforcement** — returns a non-zero exit code when the SARIF input contains reported issues.

### Integration Pattern

SarifMark is installed as a .NET local tool via `.config/dotnet-tools.json` and invoked as
`dotnet sarifmark` from the code-quality steps of `.github/workflows/build.yaml`. Self-validation runs
first (writing `artifacts/sarifmark-self-validation.trx`), then the report step consumes the CodeQL
SARIF artifact and emits the CodeQL analysis Markdown, which Pandoc and WeasyPrint compile into the code
quality document. Its input is the SARIF file produced by CodeQL scanning; its output is written to the
code-quality `generated/` folder. SarifMark is not referenced by the delivered Rendering packages.
