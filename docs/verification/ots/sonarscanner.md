## SonarScanner for .NET Verification

This document provides the verification evidence for the SonarScanner for .NET OTS software item.
Requirements for this OTS item are defined in the SonarScanner for .NET OTS Software Requirements
document.

### Required Functionality

The dotnet-sonarscanner local tool wraps the build with begin/end analysis steps that upload
source metrics, coverage, and issues to SonarCloud. It runs in the same CI pipeline that produces
the TRX test results consumed by SonarMark, so a successful pipeline run is evidence that
SonarScanner for .NET executed without error.

### Verification Approach

SonarScanner for .NET is verified by the CI pipeline itself: the pipeline runs
`dotnet sonarscanner begin` before the build and `dotnet sonarscanner end` after tests complete,
uploading the analysis to SonarCloud. A CI build failure at either step is evidence that
SonarScanner for .NET did not analyze and publish the build correctly; a successful pipeline run,
combined with SonarMark's downstream retrieval of the published quality-gate data, is evidence
that the analysis was uploaded correctly.

### Test Scenarios

#### SonarScanner_BeginAnalysis

**Scenario**: The CI pipeline runs `dotnet sonarscanner begin` before the build.

**Expected**: Exits 0 and prepares the workspace for analysis.

**Requirement coverage**: `Rendering-OTS-SonarScanner`.

#### SonarScanner_EndAnalysisPublishesResults

**Scenario**: The CI pipeline runs `dotnet sonarscanner end` after the build and tests complete.

**Expected**: Exits 0 and publishes the analysis results to SonarCloud, where SonarMark
subsequently retrieves them.

**Requirement coverage**: `Rendering-OTS-SonarScanner`.

### Requirements Coverage

- **`Rendering-OTS-SonarScanner`**: SonarScanner_BeginAnalysis, SonarScanner_EndAnalysisPublishesResults
