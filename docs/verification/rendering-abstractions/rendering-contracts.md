## Rendering Contracts Unit Verification

Part of the Rendering Abstractions Verification.

This document describes the verification design for the rendering-contracts unit of the
`DemaConsulting.Rendering.Abstractions` system. It maps every rendering-contracts unit requirement to
at least one named test scenario so a reviewer can confirm coverage without reading the test code. The
verification strategy, test environment, and acceptance criteria are described in the
system verification document; the primary test project is
`DemaConsulting.Rendering.Abstractions.Tests` (`RegistryTests.cs`). Concrete renderer extension
coverage also appears in `DemaConsulting.Rendering.Skia.Tests` (`SkiaFormatRendererTests.cs`).

### Verification Approach

The rendering-contracts unit is verified indirectly through the registry tests: because
`ILayoutAlgorithm` and `IRenderer` are pure abstractions with no behaviour, their identity members
(`Id`, `MediaType`, `DefaultExtension`, `FileExtensions`) are exercised by round-tripping them
through the two registries. The tests use test-local `FakeAlgorithm` and `FakeRenderer`
implementations for that round-trip, and additionally exercise the concrete `PngRenderer` to prove
that a real renderer honours the extension-advertising portion of the contract. `RenderOptions`
and `RenderOutput` are `sealed record` types whose behaviour is limited to their compiler-generated
members and needs no dedicated test scenarios beyond compilation.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK on `net8.0`, `net9.0`, and `net10.0`.
- **Execution**: `dotnet test` invoked by `build.ps1` and by the CI pipeline.
- **Test projects**: `DemaConsulting.Rendering.Abstractions.Tests` (`RegistryTests.cs`) and
  `DemaConsulting.Rendering.Skia.Tests` (`SkiaFormatRendererTests.cs`) for concrete-renderer
  contract coverage.
- **External dependencies**: none.

### Acceptance Criteria

The unit is considered verified when every scenario listed below passes. Each test must return the
documented identity member value; any missing member on a resolved instance or any mismatch between
`DefaultExtension` and `FileExtensions` constitutes a failure.

### Test Scenarios

The contract interfaces carry no behavior of their own; their identity members are verified through
the fake implementations registered in the registry tests. `FakeAlgorithm` implements
`ILayoutAlgorithm` (returning `Id` "fake") and `FakeRenderer` implements `IRenderer` (returning
`MediaType` "text/plain", `DefaultExtension` ".txt", and `FileExtensions` ".txt" and ".text").

#### Algorithm contract identity is exercised

Test `LayoutAlgorithmRegistry_RegisterThenResolve_ReturnsAlgorithm` registers a `FakeAlgorithm`,
resolves it, and reads the resolved algorithm's `Id`, asserting it is "fake" and thereby exercising
`ILayoutAlgorithm.Id`.

**Covers**: `Rendering-Abstractions-Contracts-Algorithm`.

#### Renderer contract identity is exercised

Test `RendererRegistry_RegisterThenResolve_ReturnsRenderer` registers a `FakeRenderer`, resolves it,
and reads the resolved renderer's `MediaType`, asserting it is "text/plain" and thereby exercising
`IRenderer.MediaType`.

Test `PngRenderer_FileExtensions_ContainsDefault` constructs the concrete PNG renderer and asserts
that its advertised `FileExtensions` contains its `DefaultExtension`, proving concrete renderers honor
the extension-advertising part of the `IRenderer` contract.

**Covers**: `Rendering-Abstractions-Contracts-Renderer`.

### Requirements Coverage

- **`Rendering-Abstractions-Contracts-Algorithm`**: LayoutAlgorithmRegistry_RegisterThenResolve_ReturnsAlgorithm
- **`Rendering-Abstractions-Contracts-Renderer`**: RendererRegistry_RegisterThenResolve_ReturnsRenderer,
  PngRenderer_FileExtensions_ContainsDefault
