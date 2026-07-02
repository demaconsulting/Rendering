# ContainmentPacker Unit Verification

Part of the Rendering Layout Verification.

This document maps the ContainmentPacker unit requirements to named test scenarios.

## ContainmentPacker Unit Scenarios

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

## Requirements Coverage

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
