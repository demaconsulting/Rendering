### Engine Subsystem Verification

Part of the Rendering Layout Verification.

This document describes subsystem-level verification coverage for the Rendering.Layout Engine subsystem.
Unit scenarios live in the Engine unit verification documents:

- OrthogonalEdgeRouter Unit Verification
- ContainmentPacker Unit Verification
- InterconnectionLayoutEngine Unit Verification
- Layered Pipeline Unit Verification
- LayoutTreePacker Unit Verification
- EdgeCountGapWidener Unit Verification

#### Verification Approach

The Engine subsystem is verified through its unit-level xUnit tests: each engine
(`OrthogonalEdgeRouter`, `ContainmentPacker`, `InterconnectionLayoutEngine`, `LayeredPipeline`,
`LayoutTreePacker`, `EdgeCountGapWidener`) is exercised directly on real inputs, and the assembled
layered pipeline is byte-compared to a legacy oracle. Because the engines have no shared runtime
state, the subsystem does not require additional integration mocks at its boundary — the unit
tests together constitute the subsystem verification. See each linked Unit Verification for
engine-specific approach and mocking notes.

#### Test Environment

- **Framework**: xUnit v3 running on the .NET SDK.
- **Runner**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Projects**: `test/DemaConsulting.Rendering.Layout.Tests/Engine/` (per-engine tests and their
  `Layered/` subfolder for pipeline stages).
- **Dependencies**: no external services, files, or network access; every test constructs its own
  in-memory geometric inputs. Deterministic seeds are used where the underlying engines run over
  pseudo-random graphs.
- **Isolation**: each engine is stateless; tests are order-independent.

#### Acceptance Criteria

A verification run passes when every subsystem-requirement scenario below is covered by passing
unit tests in the linked documents, and no engine regression is observed against the layered
pipeline's legacy-oracle byte-identity suite. Any regression in orthogonal routing,
containment packing, interconnection placement, or the assembled staged pipeline constitutes a
failure.

#### Test Scenarios

- **`Rendering-Layout-OrthogonalRouting`**:
  Route_NoObstacles_ProducesOrthogonalPath,
  Route_ObstacleBetween_RoutesAround. Detailed by the OrthogonalEdgeRouter unit verification.
- **`Rendering-Layout-Containment`**:
  Pack_MixedSizes_ProducesNoOverlaps,
  Pack_ItemsFitInRow_ShareSameRow. Detailed by the ContainmentPacker unit verification.
- **`Rendering-Layout-Interconnection`**:
  Place_LinearChain_MonotonicLayerAssignment,
  Place_WorkstationTopology_CorrectLayersAndNoOverlap. Detailed by the InterconnectionLayoutEngine and
  Layered Pipeline unit verification documents.
