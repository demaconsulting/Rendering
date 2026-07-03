## Rendering Contracts Unit Verification

Part of the Rendering Abstractions Verification.

This document describes the verification design for the rendering-contracts unit of the
`DemaConsulting.Rendering.Abstractions` system. It maps every rendering-contracts unit requirement to
at least one named test scenario so a reviewer can confirm coverage without reading the test code. The
verification strategy, test environment, and acceptance criteria are described in the
system verification document; the primary test project is
`DemaConsulting.Rendering.Abstractions.Tests` (`RegistryTests.cs`). Concrete renderer extension
coverage also appears in `DemaConsulting.Rendering.Skia.Tests` (`SkiaFormatRendererTests.cs`).

### Rendering Contracts Unit Scenarios

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
