## SonarScanner for .NET Integration Design

### Purpose

SonarScanner for .NET (the `dotnet-sonarscanner` local tool) wraps the MSBuild-based build with
begin/end analysis steps that collect source metrics, test coverage, and static-analysis issues and
upload them to SonarCloud for the repository's project. It is the analysis producer that SonarMark's
quality report subsequently reads back from SonarCloud.

### Features Used

- **Begin analysis** (`dotnet sonarscanner begin --key --organization --token`) — starts an
  analysis session before the build, instrumenting subsequent compiler and test runs.
- **End analysis** (`dotnet sonarscanner end --token`) — stops the analysis session after the build
  and tests complete, uploading the collected metrics, coverage, and issues to SonarCloud.

### Integration Pattern

SonarScanner for .NET is installed as a .NET local tool via `.config/dotnet-tools.json` and invoked
as `dotnet sonarscanner` from the code-quality steps of `.github/workflows/build.yaml`. The begin
step runs before `dotnet build`/`dotnet test`, and the end step runs immediately after, using the
`SONAR_TOKEN` secret for authentication. Its inputs are the SonarCloud project key, organization,
and API token; its output is the analysis published to SonarCloud, which SonarMark then retrieves
to render the offline quality report. SonarScanner for .NET is not referenced by the delivered
Rendering packages.
