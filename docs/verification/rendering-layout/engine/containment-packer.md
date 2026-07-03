### ContainmentPacker Unit Verification

Part of the Rendering Layout Verification.

This document maps the ContainmentPacker unit requirements to named test scenarios.

#### Verification Approach

`ContainmentPacker` is a stateless static engine, so verification is by direct xUnit unit tests
that call `Pack` on synthetic size lists. No mocks are used; the tests observe the real shelf-
packing algorithm end-to-end so single-row placement, wrapping, non-overlap, oversized-item
handling, and the reported region are all measured on production output.

#### Test Environment

- **Framework**: xUnit v3 running on the .NET SDK.
- **Runner**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Project**: `test/DemaConsulting.Rendering.Layout.Tests/Engine/ContainmentPackerTests.cs`.
- **Dependencies**: no external services, files, or network access; every test constructs its own
  in-memory item size list.
- **Isolation**: each test builds its own inputs; the engine holds no state between calls.

#### Acceptance Criteria

A verification run passes when every named scenario below asserts without unexpected exception, and
the referenced tests cover each `Rendering-Layout-ContainmentPacker-*` requirement. Any overlap
between packed rectangles, rectangle placed outside the reported region, wrong wrapping for
overflowing items, wrong solitary placement for oversized items, incorrect empty-input region, or
wrong single-item origin constitutes a failure.

#### Test Scenarios

- **Single row** (`Rendering-Layout-ContainmentPacker-SingleRow`): `Pack_ItemsFitInRow_ShareSameRow`
  asserts items that fit the width budget share one row, left to right.
- **Wrapping** (`Rendering-Layout-ContainmentPacker-Wrapping`):
  `Pack_ItemsExceedWidth_WrapToNewRow` confirms an overflowing item starts a new row beneath the
  current one.
- **No overlap** (`Rendering-Layout-ContainmentPacker-NoOverlap`):
  `Pack_MixedSizes_ProducesNoOverlaps` asserts no two packed rectangles overlap for a mix of sizes.
- **Within bounds** (`Rendering-Layout-ContainmentPacker-WithinBounds`):
  `Pack_MixedSizes_AllRectsWithinBounds` asserts every rectangle lies inside the reported region.
- **Oversized item** (`Rendering-Layout-ContainmentPacker-OversizedItem`):
  `Pack_ItemWiderThanContentWidth_PlacedAloneAndRegionWidens` confirms an oversized item is placed
  alone and the region widens to contain it.
- **Empty input** (`Rendering-Layout-ContainmentPacker-EmptyInput`):
  `Pack_EmptyList_ReturnsPaddingOnlyRegion` confirms an empty input yields a padding-only region.
- **Single item** (`Rendering-Layout-ContainmentPacker-SingleItem`):
  `Pack_SingleItem_PositionsAtPaddingOrigin` confirms a lone item lands at the padding origin with
  the region sized to wrap it.

#### Requirements Coverage

- **`Rendering-Layout-ContainmentPacker-SingleRow`**:
  Pack_ItemsFitInRow_ShareSameRow
- **`Rendering-Layout-ContainmentPacker-Wrapping`**:
  Pack_ItemsExceedWidth_WrapToNewRow
- **`Rendering-Layout-ContainmentPacker-NoOverlap`**:
  Pack_MixedSizes_ProducesNoOverlaps
- **`Rendering-Layout-ContainmentPacker-WithinBounds`**:
  Pack_MixedSizes_AllRectsWithinBounds
- **`Rendering-Layout-ContainmentPacker-OversizedItem`**:
  Pack_ItemWiderThanContentWidth_PlacedAloneAndRegionWidens
- **`Rendering-Layout-ContainmentPacker-EmptyInput`**:
  Pack_EmptyList_ReturnsPaddingOnlyRegion
- **`Rendering-Layout-ContainmentPacker-SingleItem`**:
  Pack_SingleItem_PositionsAtPaddingOrigin
