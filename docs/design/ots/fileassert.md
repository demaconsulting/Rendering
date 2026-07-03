## FileAssert Integration Design

### Purpose

FileAssert (`DemaConsulting.FileAssert`) validates the documents produced during the build. It asserts
that each generated HTML and PDF document exists, has a non-trivial size, and contains expected content.
It provides the OTS verification evidence for Pandoc and WeasyPrint (whose output correctness is proven
by FileAssert assertions) and independently confirms that document generation succeeded before ReqStream
consumes the results.

### Features Used

- **Self-validation** (`--validate --results`) — runs FileAssert's built-in self-validation suite
  (result, file, text, HTML, and PDF assertions) and writes a TRX result for ReqStream.
- **Document assertion** (`--results {trx} {assertion-set}`) — runs a named assertion set (for example
  `build-notes`, `code-quality`) that checks the corresponding generated documents exist, are
  non-empty, and contain expected text; results are written to a TRX file per document set.

### Integration Pattern

FileAssert is installed as a .NET local tool via `.config/dotnet-tools.json` and invoked as
`dotnet fileassert` from the documentation-build job of `.github/workflows/build.yaml`. After Pandoc and
WeasyPrint generate each document collection's HTML and PDF, FileAssert asserts those outputs and writes
`artifacts/fileassert-*.trx` files. ReqStream then traces those TRX results against the FileAssert,
Pandoc, and WeasyPrint OTS requirements. FileAssert is not referenced by the delivered Rendering
packages.
