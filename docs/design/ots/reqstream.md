## ReqStream Integration Design

### Purpose

ReqStream (`demaconsulting.reqstream`) is the requirements-traceability tool for the repository. It
processes `requirements.yaml` and its include chain together with the TRX test-result files to produce a
requirements report, a justifications document, and a traceability matrix, and — in enforcement mode —
fails the build when any requirement lacks passing test evidence. It is the tool that ties requirements,
tests, and OTS self-validation results together into audit evidence.

### Features Used

- **Lint** (`--lint --requirements requirements.yaml`) — checks the requirements YAML files for
  structural and semantic issues (duplicate IDs, missing fields, broken references); also run from
  `lint.ps1`.
- **Self-validation** (`--validate --results`) — runs ReqStream's built-in self-validation suite and
  writes a TRX result.
- **Report, justifications, and trace matrix generation with enforcement**
  (`--requirements`, `--tests "artifacts/**/*.trx"`, `--report`, `--justifications`, `--matrix`,
  `--enforce`) — consumes all TRX evidence, generates the compliance documents, and exits non-zero if
  any requirement is unproven.

### Integration Pattern

ReqStream is installed as a .NET local tool via `.config/dotnet-tools.json` and invoked as
`dotnet reqstream`. Lint runs in `lint.ps1` (and CI quality checks); self-validation and the
report/matrix/enforce step run in the documentation-build job of `.github/workflows/build.yaml` after
all other TRX evidence has been produced. Its inputs are `requirements.yaml` (with the
`docs/reqstream/**` include chain) and the collected `artifacts/**/*.trx` files; its outputs are written
to the requirements-document and requirements-report `generated/` folders. ReqStream is not referenced
by the delivered Rendering packages.
