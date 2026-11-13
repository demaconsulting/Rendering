## SysML2Tools Integration Design

### Purpose

SysML2Tools (`DemaConsulting.SysML2Tools.Tool`) lints and renders the repository's SysML2
architecture model under `docs/sysml2/`. It was chosen because it is the reference implementation
for the SysML v2 subset used by the Continuous Compliance methodology, and it integrates directly
with the same `dotnet tool restore` and CI pipeline used by the other pipeline tools.

### Features Used

- **Lint** (`lint 'docs/sysml2/**/*.sysml'`) — reports syntax errors and unresolved references in
  the model without producing output files; run from `lint.ps1`.
- **Render** (`render --format svg`) — renders every view declared in
  `docs/sysml2/views/design-views.sysml` to an SVG file named after the view (for example
  `SoftwareStructureView.svg`).
- **Self-validation** (`--validate --results`) — exercises `lint` and `render` against known
  fixtures and writes a TRX result.

### Integration Pattern

SysML2Tools is installed as a .NET local tool via `.config/dotnet-tools.json` and invoked as
`dotnet sysml2tools` from two places: `lint.ps1` runs
`dotnet sysml2tools lint 'docs/sysml2/**/*.sysml'` as an early-detection lint gate, and the
design-document section of `.github/workflows/build.yaml` runs the following command

```pwsh
dotnet sysml2tools render `
  --output docs/design/generated --format svg `
  'docs/sysml2/common/**/*.sysml' `
  'docs/sysml2/model/**/*.sysml' `
  'docs/sysml2/views/design-views.sysml'
```

immediately before Pandoc compiles the Design document. FileAssert then asserts that each declared
view's rendered SVG file exists in `docs/design/generated/` and is well-formed XML, before Pandoc
embeds it in the compiled document. Each invocation is a stateless CLI run with no configuration
object beyond its command-line arguments. SysML2Tools is not referenced by the delivered Rendering
packages.
