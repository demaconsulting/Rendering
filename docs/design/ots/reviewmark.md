## ReviewMark Integration Design

### Purpose

ReviewMark (`demaconsulting.reviewmark`) enforces formal file review across the repository. It reads the
`.reviewmark.yaml` configuration and the review-evidence store to determine which files require review,
group them into named review-sets, produce a review plan and review report, and — in enforcement mode —
fail the build when a reviewed file has changed since its last review. It provides the review-currency
evidence required by the compliance process.

### Features Used

- **Lint** (`--lint`) — validates the `.reviewmark.yaml` review definitions for structural and semantic
  issues; also run from `lint.ps1`.
- **Self-validation** (`--validate --results`) — runs ReviewMark's built-in self-validation suite and
  writes a TRX result.
- **Plan and report generation** (`--plan`, `--plan-depth`, `--report`, `--report-depth`) — produces the
  code review plan and report Markdown documents.
- **Additional capabilities available** — evidence index scanning (`--index`), enforcement
  (`--enforce`), review-set elaboration (`--elaborate`), and working-directory override (`--dir`).

### Integration Pattern

ReviewMark is installed as a .NET local tool via `.config/dotnet-tools.json` and invoked as
`dotnet reviewmark`. Lint runs in `lint.ps1` (and CI quality checks); self-validation and plan/report
generation run in the code-review job of `.github/workflows/build.yaml`. Its inputs are `.reviewmark.yaml`
(review-sets and `needs-review` patterns) and the review-evidence store referenced by
`evidence-source`; its outputs are the plan and report Markdown files in their `generated/` folders.
Enforcement (`--enforce`) is planned once the reviews branch is populated with evidence. ReviewMark is
not referenced by the delivered Rendering packages.
