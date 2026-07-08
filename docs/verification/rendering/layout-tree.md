## Layout Tree Unit Verification

Part of the Rendering Model Verification.

This document describes the verification design for the layout-tree unit of the
`DemaConsulting.Rendering` system. It maps every layout-tree unit requirement to at least one named
test scenario so a reviewer can confirm coverage without reading the test code.

### Layout Tree Unit Verification Approach

The Layout Tree unit is verified by direct in-process xUnit tests that construct each concrete
`LayoutNode` record and the `LayoutTree`, `Point2D`, and `Rect` value types with explicit inputs and
assert on the observable state. No mocking or stubbing is required: the types are immutable data
records with no dependencies and no I/O, so the tests exercise the real records exactly as consumers
do. Each test focuses on a single record type or geometry type and verifies that supplied
constructor arguments are stored and returned unchanged, that heterogeneous children are retained in
insertion order, and that the depth-not-color and absolute-coordinate invariants hold.

### Layout Tree Unit Test Environment

- **Framework**: xUnit v3, run through the standard `dotnet test` runner.
- **Test project**: `DemaConsulting.Rendering.Tests`, source file `LayoutTests.cs`.
- **Runtime**: any target framework built by the solution (`net8.0`, `net9.0`, or `net10.0`).
- **Dependencies**: none beyond the standard test runner; no external services, network, filesystem,
  or configuration is required.
- **Isolation**: each test constructs its own record instances, so there is no shared mutable state
  between tests and no ordering dependency.

### Layout Tree Unit Acceptance Criteria

Every named scenario listed below passes without error or unexpected exception (IEC 62304 §5.5.2). A
failure is any stored value that does not match the constructor input, any nested child that is
missing or reordered, any non-integer `Depth`, any coordinate that has been transformed away from the
supplied absolute value, or any unexpected exception. The verification run is considered complete
when every requirement listed in the Requirements Coverage section is mapped to at least one passing
test.

### Layout Tree Unit Scenarios

#### Layout tree carries canvas and nodes

Test `LayoutTree_Construction_StoresWidthHeightNodes` constructs a `LayoutTree` with explicit width,
height, and a single top-level node, then asserts that `Width`, `Height`, and `Nodes` return the
supplied values and that the single node is the same instance supplied.

**Covers**: `Rendering-Model-LayoutTree-Canvas`.

#### Node coordinates are absolute

Tests `LayoutBox_Coordinates_AreAbsolute`, `LayoutPort_Coordinates_AreAbsolute`, and
`LayoutLine_Waypoints_AreAbsolute` construct a box, a port, and a line at explicit positions and
assert that the stored coordinates equal the supplied values with no offset or transform applied.

**Covers**: `Rendering-Model-LayoutTree-AbsoluteCoordinates`.

#### Box carries all fields and children

Tests `LayoutBox_Construction_StoresAllFields` and `LayoutBox_Children_ContainsNestedNodes` construct
a box with all nine parameters non-default and a box with a port and a nested box as children, then
assert that every property is stored and that both heterogeneous children are retrievable in
insertion order.

**Covers**: `Rendering-Model-LayoutTree-Box`.

#### Box depth is an integer

Test `LayoutBox_Depth_IsInteger` constructs a box with `Depth` set to 3 and asserts that `Depth` is
stored as an `int` with value 3, confirming the depth-not-color invariant.

**Covers**: `Rendering-Model-LayoutTree-DepthNotColor`.

#### Content insets default zero and are independently settable

Tests `LayoutBox_ContentInsets_DefaultZero` and `LayoutBox_ContentInsets_IndependentlySettable`
construct a box without specifying the four `ContentInset*` values and assert they all default to
`0.0`, then construct a box with distinct explicit values for each side and assert every side reads
back its own value independently of the others.

**Covers**: `Rendering-Model-LayoutTree-ContentInset`.

#### Port carries all fields

Test `LayoutPort_Construction_StoresAllFields` constructs a port with centre, side, and label set and
asserts that `CentreX`, `CentreY`, `Side`, and `Label` equal the supplied values.

**Covers**: `Rendering-Model-LayoutTree-Port`.

#### Line carries all fields

Test `LayoutLine_Construction_StoresAllFields` constructs a line with two waypoints, both end-marker
styles, a line style, and a midpoint label, and asserts that `Waypoints`, `SourceEnd`, `TargetEnd`,
`LineStyle`, and `MidpointLabel` equal the supplied values.

**Covers**: `Rendering-Model-LayoutTree-Line`.

#### Label carries all fields

Test `LayoutLabel_Construction_StoresAllFields` constructs a label with all eight parameters
non-default and asserts that `X`, `Y`, `MaxWidth`, `Text`, `Align`, `Weight`, `Style`, and `FontSize`
equal the supplied values.

**Covers**: `Rendering-Model-LayoutTree-Label`.

#### Badge carries all fields

Test `LayoutBadge_Construction_StoresAllFields` constructs a badge with centre, size, shape, and label
and asserts that `CentreX`, `CentreY`, `Size`, `Shape`, and `Label` equal the supplied values.

**Covers**: `Rendering-Model-LayoutTree-Badge`.

#### Band carries all fields

Test `LayoutBand_Construction_StoresAllFields` constructs a band with bounds, orientation, label, and
one child and asserts that `X`, `Y`, `Width`, `Height`, `Orientation`, `Label`, and `Children` equal
the supplied values.

**Covers**: `Rendering-Model-LayoutTree-Band`.

#### Lifeline carries all fields

Test `LayoutLifeline_Construction_StoresAllFields` constructs a lifeline with centre, extent, label,
and header dimensions and asserts that `CentreX`, `TopY`, `BottomY`, `Label`, `HeaderWidth`, and
`HeaderHeight` equal the supplied values.

**Covers**: `Rendering-Model-LayoutTree-Lifeline`.

#### Activation carries all fields

Test `LayoutActivation_Construction_StoresAllFields` constructs an activation with centre and vertical
extent and asserts that `CentreX`, `TopY`, and `BottomY` equal the supplied values.

**Covers**: `Rendering-Model-LayoutTree-Activation`.

#### Grid carries rows and cells

Test `LayoutGrid_Construction_StoresAllFields` constructs a grid with one header row containing one
cell and asserts the grid position, the row's `IsHeader` flag, and the cell's `Width`, `Height`,
`Text`, `Align`, and `ColSpan`.

**Covers**: `Rendering-Model-LayoutTree-Grid`.

#### Geometry value types carry their fields

Tests `Point2D_Construction_StoresXY` and `Rect_Construction_StoresAllFields` construct a point at a
known location and a rectangle with all four fields non-default, then assert each value type stores
and returns its supplied coordinates and bounds unchanged.

**Covers**: `Rendering-Model-LayoutTree-Geometry`.

#### Box carries optional shape-geometry hints

Test `LayoutBox_Construction_StoresAllFields` confirms that a placed box preserves the optional
rounded-corner and folder-tab geometry hints alongside its existing position, shape, compartments, and
children, so downstream routing and rendering stages can observe the resolved shape outline geometry.

**Covers**: `Rendering-Model-LayoutTree-ShapeGeometryHints`.

### Requirements Coverage

- **`Rendering-Model-LayoutTree-Canvas`**: LayoutTree_Construction_StoresWidthHeightNodes
- **`Rendering-Model-LayoutTree-AbsoluteCoordinates`**: LayoutBox_Coordinates_AreAbsolute,
  LayoutPort_Coordinates_AreAbsolute, LayoutLine_Waypoints_AreAbsolute
- **`Rendering-Model-LayoutTree-Box`**: LayoutBox_Construction_StoresAllFields,
  LayoutBox_Children_ContainsNestedNodes
- **`Rendering-Model-LayoutTree-DepthNotColor`**: LayoutBox_Depth_IsInteger
- **`Rendering-Model-LayoutTree-ContentInset`**: LayoutBox_ContentInsets_DefaultZero,
  LayoutBox_ContentInsets_IndependentlySettable
- **`Rendering-Model-LayoutTree-Port`**: LayoutPort_Construction_StoresAllFields
- **`Rendering-Model-LayoutTree-Line`**: LayoutLine_Construction_StoresAllFields
- **`Rendering-Model-LayoutTree-Label`**: LayoutLabel_Construction_StoresAllFields
- **`Rendering-Model-LayoutTree-Badge`**: LayoutBadge_Construction_StoresAllFields
- **`Rendering-Model-LayoutTree-Band`**: LayoutBand_Construction_StoresAllFields
- **`Rendering-Model-LayoutTree-Lifeline`**: LayoutLifeline_Construction_StoresAllFields
- **`Rendering-Model-LayoutTree-Activation`**: LayoutActivation_Construction_StoresAllFields
- **`Rendering-Model-LayoutTree-Grid`**: LayoutGrid_Construction_StoresAllFields
- **`Rendering-Model-LayoutTree-Geometry`**: Point2D_Construction_StoresXY,
  Rect_Construction_StoresAllFields
- **`Rendering-Model-LayoutTree-ShapeGeometryHints`**:
  LayoutBox_Construction_StoresAllFields
