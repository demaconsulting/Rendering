# Registries Unit Verification

Part of the Rendering Abstractions Verification.

This document describes the verification design for the registries unit of the
`DemaConsulting.Rendering.Abstractions` system. It maps every registries unit requirement to at least
one named test scenario so a reviewer can confirm coverage without reading the test code. The
verification strategy, test environment, and acceptance criteria are described in the
system verification document; the test project is
`DemaConsulting.Rendering.Abstractions.Tests` (`RegistryTests.cs`).

## Registries Unit Scenarios

### Algorithm registers and resolves by id

Test `LayoutAlgorithmRegistry_RegisterThenResolve_ReturnsAlgorithm` registers an algorithm, then
asserts that `Contains("fake")` is true and `Resolve("fake")` returns the registered algorithm.

**Covers**: `Rendering-Abstractions-Registries-ResolveAlgorithm`.

### Renderer registers and resolves by media type

Test `RendererRegistry_RegisterThenResolve_ReturnsRenderer` registers a renderer, then asserts that
`Contains("text/plain")` is true and `Resolve("text/plain")` returns the registered renderer.

**Covers**: `Rendering-Abstractions-Registries-ResolveRenderer`.

### Renderer resolves by file extension

Test `RendererRegistry_ResolveByExtension_MatchesAdvertisedExtensions` registers a renderer that
advertises `.txt` and `.text`, asserts `ContainsExtension(".txt")`, resolves `.txt`, and resolves
`TEXT` without a leading dot. The assertions prove that every advertised extension is indexed and that
extension lookup ignores case and tolerates an omitted leading dot.

**Covers**: `Rendering-Abstractions-Registries-ResolveRendererByExtension`.

### Resolving a missing id throws

Test `LayoutAlgorithmRegistry_ResolveMissing_Throws` resolves an identifier that was never registered
and asserts that `Resolve` throws `KeyNotFoundException`.

**Covers**: `Rendering-Abstractions-Registries-MissingThrows`.

## Requirements Coverage

- **`Rendering-Abstractions-Registries-ResolveAlgorithm`**: LayoutAlgorithmRegistry_RegisterThenResolve_ReturnsAlgorithm
- **`Rendering-Abstractions-Registries-ResolveRenderer`**: RendererRegistry_RegisterThenResolve_ReturnsRenderer
- **`Rendering-Abstractions-Registries-ResolveRendererByExtension`**:
  RendererRegistry_ResolveByExtension_MatchesAdvertisedExtensions
- **`Rendering-Abstractions-Registries-MissingThrows`**: LayoutAlgorithmRegistry_ResolveMissing_Throws
