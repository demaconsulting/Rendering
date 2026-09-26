## xUnit Integration Design

### Purpose

xUnit is the unit-testing framework used by the Rendering test projects. Unlike the DemaConsulting
compliance tools, it is not a build-time .NET tool but the test framework that discovers, executes, and
records the repository's own tests. Those tests provide the passing evidence that ReqStream traces
against every requirement, so xUnit underpins the entire verification pipeline.

### Features Used

- **Test discovery and execution** — xUnit v4 (`xunit.v3`, version 4.x) discovers and runs all methods
  annotated with `[Fact]` or `[Theory]` across the model, layout, and renderer test projects. Each test
  project builds as a standalone Microsoft.Testing.Platform (MTP) executable
  (`UseMicrosoftTestingPlatformRunner`), which `dotnet test` invokes directly per the MTP `dotnet test`
  mode configured in `global.json`.
- **TRX result reporting** — the `Microsoft.Testing.Extensions.TrxReport` MTP extension writes TRX
  result files (via `dotnet test --report-trx`) that feed coverage reporting and requirements
  traceability through ReqStream.

### Integration Pattern

xUnit is referenced as a NuGet test-framework dependency (`xunit.v3`, plus the
`Microsoft.Testing.Extensions.TrxReport` and `Microsoft.Testing.Extensions.CodeCoverage` MTP extensions)
by each `test/DemaConsulting.Rendering*.Tests` project rather than installed as a .NET local tool. It is
invoked by `dotnet test` from `build.ps1` and from the test job of `.github/workflows/build.yaml` across
the supported target frameworks (.NET 8, 9, 10, and net481) using the Microsoft.Testing.Platform runner
rather than VSTest. Its inputs are the compiled test assemblies; its outputs are the TRX result files
under `artifacts/` that ReqStream consumes as compliance evidence. xUnit is a test-time dependency only
and is not referenced by the delivered Rendering packages.
