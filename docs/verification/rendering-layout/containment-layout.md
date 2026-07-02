# ContainmentLayout Unit Verification

Part of the [Rendering Layout Verification](rendering-layout.md).

This document maps the containment-layout unit requirements to named test scenarios.

## ContainmentLayout Scenarios

- **Order preserved** (`Rendering-Layout-ContainmentLayout-Order`):
  `Pack_ItemsFitInRow_PreservesOrderLeftToRight` confirms the packed children keep their input order,
  positioned left to right along a shared row.
- **No overlap** (`Rendering-Layout-ContainmentLayout-NoOverlap`): `Pack_MixedSizes_ProducesNoOverlaps`
  asserts no two packed children overlap for a multi-row mix of sizes.
- **Within region** (`Rendering-Layout-ContainmentLayout-WithinRegion`):
  `Pack_MixedSizes_AllChildrenWithinRegion` asserts every child lies inside the reported region.
- **Wrapping** (`Rendering-Layout-ContainmentLayout-Wrapping`):
  `Pack_ChildExceedsWidth_WrapsToNewRow` confirms an overflowing child starts a new row beneath the
  current one at the left origin.
- **Oversized child** (`Rendering-Layout-ContainmentLayout-OversizedChild`):
  `Pack_OversizedChild_PlacedAloneAndRegionWidens` confirms a child wider than the content width is
  placed alone and the region widens to contain it.
- **Empty input** (`Rendering-Layout-ContainmentLayout-EmptyInput`):
  `Pack_EmptyInput_ReturnsPaddingOnlyRegion` confirms an empty input yields no children and a
  padding-only region.
- **Fields preserved** (`Rendering-Layout-ContainmentLayout-PreservesFields`):
  `Pack_PreservesNonPositionFields` confirms label, depth, shape, compartments, nested children, and
  keyword survive unchanged while only X and Y are updated.
- **Option defaults** (`Rendering-Layout-ContainmentLayout-Defaults`):
  `ContainmentOptions_Defaults_AreSensibleGapsAndPadding` confirms the default gaps are eight pixels and
  the default padding is twelve pixels.
- **Validation** (`Rendering-Layout-ContainmentLayout-Validation`): `Pack_NullChildren_Throws`,
  `Pack_NullOptions_Throws`, and `Pack_NullChildElement_Throws` confirm null arguments are rejected with
  an argument-null error.

## Requirements Coverage

- **`Rendering-Layout-ContainmentLayout-Order`**:
  Pack_ItemsFitInRow_PreservesOrderLeftToRight
- **`Rendering-Layout-ContainmentLayout-NoOverlap`**:
  Pack_MixedSizes_ProducesNoOverlaps
- **`Rendering-Layout-ContainmentLayout-WithinRegion`**:
  Pack_MixedSizes_AllChildrenWithinRegion
- **`Rendering-Layout-ContainmentLayout-Wrapping`**:
  Pack_ChildExceedsWidth_WrapsToNewRow
- **`Rendering-Layout-ContainmentLayout-OversizedChild`**:
  Pack_OversizedChild_PlacedAloneAndRegionWidens
- **`Rendering-Layout-ContainmentLayout-EmptyInput`**:
  Pack_EmptyInput_ReturnsPaddingOnlyRegion
- **`Rendering-Layout-ContainmentLayout-PreservesFields`**:
  Pack_PreservesNonPositionFields
- **`Rendering-Layout-ContainmentLayout-Defaults`**:
  ContainmentOptions_Defaults_AreSensibleGapsAndPadding
- **`Rendering-Layout-ContainmentLayout-Validation`**:
  Pack_NullChildren_Throws, Pack_NullOptions_Throws, Pack_NullChildElement_Throws
