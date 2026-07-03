## SonarMark Integration Design

### Purpose

SonarMark (`DemaConsulting.SonarMark`) retrieves quality-gate status, metrics, issues, and security
hotspots from SonarCloud for the repository's project and renders them as a Markdown quality report that
is compiled into the release artifacts. It turns the SonarCloud analysis into an offline, reviewable
compliance document.

### Features Used

- **Self-validation** (`--validate --results`) — runs SonarMark's built-in self-validation suite and
  writes a TRX result.
- **SonarCloud quality-report generation** (`--server https://sonarcloud.io`, `--project-key`,
  `--branch`, `--token`, `--report`, `--report-depth`) — queries the SonarCloud quality gate, issues,
  and hotspots and renders `docs/code_quality/generated/sonar-quality.md`.

### Integration Pattern

SonarMark is installed as a .NET local tool via `.config/dotnet-tools.json` and invoked as
`dotnet sonarmark` from the code-quality steps of `.github/workflows/build.yaml`. It runs after the
SonarScanner analysis has published results to SonarCloud; self-validation runs first (writing
`artifacts/sonarmark-self-validation.trx`), then the report step retrieves the analysis for the current
branch using the `SONAR_TOKEN` secret and emits the Sonar quality Markdown, which Pandoc and WeasyPrint
compile into the code quality document. Its inputs are the SonarCloud project key, branch, and API
token; its output is written to the code-quality `generated/` folder. SonarMark is not referenced by the
delivered Rendering packages.
