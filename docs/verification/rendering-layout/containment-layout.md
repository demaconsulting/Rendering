## ContainmentLayout Unit Verification

Part of the Rendering Layout Verification.

This document maps the containment-layout unit requirements to named test scenarios.

### Verification Approach

`ContainmentLayout` (the public containment packing entry point) is verified by direct xUnit unit
tests that call `Pack(children, options)` on synthetic child lists. No mocks are used; the tests
exercise the real packing algorithm and its underlying `ContainmentPacker` engine end-to-end so
ordering, wrapping, region sizing, and field preservation are all observed on production output.

### Test Environment

- **Framework**: xUnit v3 running on the .NET SDK.
- **Runner**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Project**: `test/DemaConsulting.Rendering.Layout.Tests/ContainmentLayoutTests.cs`, with the
  underlying engine also covered by
  `test/DemaConsulting.Rendering.Layout.Tests/Engine/ContainmentPackerTests.cs`.
- **Dependencies**: no external services, files, or network access; every test constructs its own
  in-memory child list and `ContainmentOptions` instance.
- **Isolation**: each test builds its own inputs; the unit is stateless between calls.

### Acceptance Criteria

A verification run passes when every named scenario below asserts without unexpected exception, and
the referenced tests cover each `Rendering-Layout-ContainmentLayout-*` requirement. Any overlap
between packed children, child positioned outside the reported region, wrong wrapping behavior for
overflowing or oversized children, lost non-position field (label, depth, shape, compartments,
nested children, keyword), drift in default gaps or padding, or non-argument-null exception for
invalid input constitutes a failure.

### Test Scenarios

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
- **Edge-count gap widening** (`Rendering-Layout-ContainmentLayout-EdgeCountGapWidening`):
  `Pack_EdgeCounts_WidensIndicatedRowGap` and `Pack_SameRowEdgeCount_WidensHorizontalGap` confirm the
  horizontal gap between two adjacent same-row children grows to the connector-corridor width for the
  supplied edge count.
- **Edge-count gap widening opted out** (`Rendering-Layout-ContainmentLayout-EdgeCountGapWideningOptedOut`):
  `Pack_WithoutEdgeCounts_UsesDefaultGap` and `Pack_NullEdgeCounts_ByteIdenticalToNoCounts` confirm
  placement is byte-identical when no EdgeCounts lookup (or an explicit null lookup) is supplied.
- **Edge-count gap widening is row-scoped**
  (`Rendering-Layout-ContainmentLayout-EdgeCountGapWideningRowScoped`):
  `Pack_DifferentRowPair_Unaffected` confirms a pair the wrap decision splits across two rows is never
  widened, regardless of its supplied edge count.
- **Validation** (`Rendering-Layout-ContainmentLayout-Validation`): `Pack_NullChildren_Throws`,
  `Pack_NullOptions_Throws`, and `Pack_NullChildElement_Throws` confirm null arguments are rejected with
  an argument-null error.

### Requirements Coverage

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
- **`Rendering-Layout-ContainmentLayout-EdgeCountGapWidening`**:
  Pack_EdgeCounts_WidensIndicatedRowGap, Pack_SameRowEdgeCount_WidensHorizontalGap
- **`Rendering-Layout-ContainmentLayout-EdgeCountGapWideningOptedOut`**:
  Pack_WithoutEdgeCounts_UsesDefaultGap, Pack_NullEdgeCounts_ByteIdenticalToNoCounts
- **`Rendering-Layout-ContainmentLayout-EdgeCountGapWideningRowScoped`**:
  Pack_DifferentRowPair_Unaffected
- **`Rendering-Layout-ContainmentLayout-Validation`**:
  Pack_NullChildren_Throws, Pack_NullOptions_Throws, Pack_NullChildElement_Throws
