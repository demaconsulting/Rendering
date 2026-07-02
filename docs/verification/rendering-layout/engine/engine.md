# Engine Subsystem Verification

Part of the Rendering Layout Verification.

This document describes subsystem-level verification coverage for the Rendering.Layout Engine subsystem.
Unit scenarios live in the Engine unit verification documents:

- OrthogonalEdgeRouter Unit Verification
- ContainmentPacker Unit Verification
- InterconnectionLayoutEngine Unit Verification
- Layered Pipeline Unit Verification

## Engine Subsystem Coverage

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
