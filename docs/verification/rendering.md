# Rendering Model Verification

This document describes the system-level verification design for the `DemaConsulting.Rendering`
(rendering model) system and links to the per-unit verification documents for its three units. It
records the verification strategy, test environment, and acceptance criteria shared by every unit, and
maps each system-level requirement to at least one named test scenario. The detailed per-requirement
scenarios live in the unit documents:

- Layout Tree Unit Verification
- Options Unit Verification
- Layout Graph Unit Verification

## Verification Approach

The rendering model is a pure data-and-configuration library with no I/O, so it is verified entirely
through in-process unit tests that construct the model types and assert on their observable state.
Each test constructs the type directly with known inputs and asserts that every field is stored and
retrieved unchanged, confirming the model's core invariants (absolute coordinates, depth-not-color,
default-then-override configuration). No mocking or stubbing is required because the model has no
dependencies to isolate.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK.
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Dependencies**: none; no external services, network, or filesystem access.
- **Isolation**: each test constructs its own model instances; there is no shared state.
- **Test project**: `DemaConsulting.Rendering.Tests` (`LayoutTests.cs`, `PropertyHolderTests.cs`,
  `LayoutGraphTests.cs`).

## Acceptance Criteria

A verification run passes when every scenario in this system document and in the three unit documents
passes without error or unexpected exception. Any wrong stored value, wrong type, or unexpected
exception constitutes a failure.

## Test Scenarios

The system requirements are satisfied through the unit scenarios documented in the per-unit
verification files; the representative system-level scenarios are:

- **`Rendering-Model-LayoutTree`**: LayoutTree_Construction_StoresWidthHeightNodes,
  LayoutBox_Construction_StoresAllFields (see Layout Tree Unit Verification)
- **`Rendering-Model-Configuration`**: Get_UnsetProperty_ReturnsDefault, Get_AfterSet_ReturnsStoredValue
  (see Options Unit Verification)
- **`Rendering-Model-InputGraph`**: AddNode_AppendsNodeAndReturnsIt, AddEdge_AppendsEdgeWithEndpoints
  (see Layout Graph Unit Verification)
