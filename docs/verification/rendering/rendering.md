# Rendering Model Verification

This document describes the verification design for the `DemaConsulting.Rendering` (rendering model)
system and its three units: layout-tree, options, and layout-graph. It maps every requirement to at
least one named test scenario so a reviewer can confirm coverage without reading the test code.

## Verification Strategy

The rendering model is a pure data-and-configuration library with no I/O, so it is verified entirely
through in-process unit tests that construct the model types and assert on their observable state.
Each test constructs the type directly with known inputs and asserts that every field is stored and
retrieved unchanged, confirming the model's core invariants (absolute coordinates, depth-not-color,
default-then-override configuration). No mocking or stubbing is required because the model has no
dependencies to isolate.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK.
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Dependencies**: none; no external services, network, or filesystem access.
- **Isolation**: each test constructs its own model instances; there is no shared state.
- **Test project**: `DemaConsulting.Rendering.Tests` (`LayoutTests.cs`, `PropertyHolderTests.cs`,
  `LayoutGraphTests.cs`).

## Layout Tree Unit Scenarios

### Layout tree carries canvas and nodes

Test `LayoutTree_Construction_StoresWidthHeightNodes` constructs a `LayoutTree` with explicit width,
height, and a single top-level node, then asserts that `Width`, `Height`, and `Nodes` return the
supplied values and that the single node is the same instance supplied.

**Covers**: `Rendering-Model-LayoutTree-Canvas`.

### Node coordinates are absolute

Tests `LayoutBox_Coordinates_AreAbsolute`, `LayoutPort_Coordinates_AreAbsolute`, and
`LayoutLine_Waypoints_AreAbsolute` construct a box, a port, and a line at explicit positions and
assert that the stored coordinates equal the supplied values with no offset or transform applied.

**Covers**: `Rendering-Model-LayoutTree-AbsoluteCoordinates`.

### Box carries all fields and children

Tests `LayoutBox_Construction_StoresAllFields` and `LayoutBox_Children_ContainsNestedNodes` construct
a box with all nine parameters non-default and a box with a port and a nested box as children, then
assert that every property is stored and that both heterogeneous children are retrievable in
insertion order.

**Covers**: `Rendering-Model-LayoutTree-Box`.

### Box depth is an integer

Test `LayoutBox_Depth_IsInteger` constructs a box with `Depth` set to 3 and asserts that `Depth` is
stored as an `int` with value 3, confirming the depth-not-color invariant.

**Covers**: `Rendering-Model-LayoutTree-DepthNotColor`.

### Port carries all fields

Test `LayoutPort_Construction_StoresAllFields` constructs a port with centre, side, and label set and
asserts that `CentreX`, `CentreY`, `Side`, and `Label` equal the supplied values.

**Covers**: `Rendering-Model-LayoutTree-Port`.

### Line carries all fields

Test `LayoutLine_Construction_StoresAllFields` constructs a line with two waypoints, both end-marker
styles, a line style, and a midpoint label, and asserts that `Waypoints`, `SourceEnd`, `TargetEnd`,
`LineStyle`, and `MidpointLabel` equal the supplied values.

**Covers**: `Rendering-Model-LayoutTree-Line`.

### Label carries all fields

Test `LayoutLabel_Construction_StoresAllFields` constructs a label with all eight parameters
non-default and asserts that `X`, `Y`, `MaxWidth`, `Text`, `Align`, `Weight`, `Style`, and `FontSize`
equal the supplied values.

**Covers**: `Rendering-Model-LayoutTree-Label`.

### Badge carries all fields

Test `LayoutBadge_Construction_StoresAllFields` constructs a badge with centre, size, shape, and label
and asserts that `CentreX`, `CentreY`, `Size`, `Shape`, and `Label` equal the supplied values.

**Covers**: `Rendering-Model-LayoutTree-Badge`.

### Band carries all fields

Test `LayoutBand_Construction_StoresAllFields` constructs a band with bounds, orientation, label, and
one child and asserts that `X`, `Y`, `Width`, `Height`, `Orientation`, `Label`, and `Children` equal
the supplied values.

**Covers**: `Rendering-Model-LayoutTree-Band`.

### Lifeline carries all fields

Test `LayoutLifeline_Construction_StoresAllFields` constructs a lifeline with centre, extent, label,
and header dimensions and asserts that `CentreX`, `TopY`, `BottomY`, `Label`, `HeaderWidth`, and
`HeaderHeight` equal the supplied values.

**Covers**: `Rendering-Model-LayoutTree-Lifeline`.

### Activation carries all fields

Test `LayoutActivation_Construction_StoresAllFields` constructs an activation with centre and vertical
extent and asserts that `CentreX`, `TopY`, and `BottomY` equal the supplied values.

**Covers**: `Rendering-Model-LayoutTree-Activation`.

### Grid carries rows and cells

Test `LayoutGrid_Construction_StoresAllFields` constructs a grid with one header row containing one
cell and asserts the grid position, the row's `IsHeader` flag, and the cell's `Width`, `Height`,
`Text`, `Align`, and `ColSpan`.

**Covers**: `Rendering-Model-LayoutTree-Grid`.

## Options Unit Scenarios

### Unset property returns default

Test `Get_UnsetProperty_ReturnsDefault` reads a property from a fresh `PropertyHolder` without setting
it and asserts that the read returns the property's declared default value.

**Covers**: `Rendering-Model-Options-Default`.

### Set value is retrieved

Test `Get_AfterSet_ReturnsStoredValue` sets a property to a value, then reads the same property and
asserts that the read returns the stored value rather than the default.

**Covers**: `Rendering-Model-Options-StoreAndRetrieve`.

### Contains reflects explicit set

Test `Contains_ReflectsExplicitSet` queries `Contains` before and after setting a property and asserts
that it returns false before the set and true afterwards.

**Covers**: `Rendering-Model-Options-Contains`.

### TryGet reports unset and yields default

Test `TryGet_UnsetProperty_ReturnsFalseAndDefault` calls `TryGet` for a property that has not been set
and asserts that it returns false and yields the declared default through its out parameter.

**Covers**: `Rendering-Model-Options-TryGet`.

## Layout Graph Unit Scenarios

### AddNode appends and returns the node

Test `AddNode_AppendsNodeAndReturnsIt` calls `AddNode` on a fresh graph and asserts that the graph
contains one node and that the returned node carries the requested id, width, and height.

**Covers**: `Rendering-Model-LayoutGraph-AddNode`.

### AddEdge appends an edge with endpoints

Test `AddEdge_AppendsEdgeWithEndpoints` adds two nodes and an edge referencing them, then asserts that
the graph contains one edge whose `Source` and `Target` are the same node instances supplied.

**Covers**: `Rendering-Model-LayoutGraph-AddEdge`.

### Node carries per-element properties

Test `Node_CarriesPerElementProperties` sets the `CoreOptions.Direction` property on a node and reads
it back, asserting that the read returns the value set on that node.

**Covers**: `Rendering-Model-LayoutGraph-PerElementProperties`.

## Acceptance Criteria

A verification run passes when every scenario above passes without error or unexpected exception. Any
wrong stored value, wrong type, or unexpected exception constitutes a failure.

## Requirements Coverage

- **`Rendering-Model-LayoutTree`**: LayoutTree_Construction_StoresWidthHeightNodes,
  LayoutBox_Construction_StoresAllFields
- **`Rendering-Model-Configuration`**: Get_UnsetProperty_ReturnsDefault, Get_AfterSet_ReturnsStoredValue
- **`Rendering-Model-InputGraph`**: AddNode_AppendsNodeAndReturnsIt, AddEdge_AppendsEdgeWithEndpoints
- **`Rendering-Model-LayoutTree-Canvas`**: LayoutTree_Construction_StoresWidthHeightNodes
- **`Rendering-Model-LayoutTree-AbsoluteCoordinates`**: LayoutBox_Coordinates_AreAbsolute,
  LayoutPort_Coordinates_AreAbsolute, LayoutLine_Waypoints_AreAbsolute
- **`Rendering-Model-LayoutTree-Box`**: LayoutBox_Construction_StoresAllFields,
  LayoutBox_Children_ContainsNestedNodes
- **`Rendering-Model-LayoutTree-DepthNotColor`**: LayoutBox_Depth_IsInteger
- **`Rendering-Model-LayoutTree-Port`**: LayoutPort_Construction_StoresAllFields
- **`Rendering-Model-LayoutTree-Line`**: LayoutLine_Construction_StoresAllFields
- **`Rendering-Model-LayoutTree-Label`**: LayoutLabel_Construction_StoresAllFields
- **`Rendering-Model-LayoutTree-Badge`**: LayoutBadge_Construction_StoresAllFields
- **`Rendering-Model-LayoutTree-Band`**: LayoutBand_Construction_StoresAllFields
- **`Rendering-Model-LayoutTree-Lifeline`**: LayoutLifeline_Construction_StoresAllFields
- **`Rendering-Model-LayoutTree-Activation`**: LayoutActivation_Construction_StoresAllFields
- **`Rendering-Model-LayoutTree-Grid`**: LayoutGrid_Construction_StoresAllFields
- **`Rendering-Model-Options-Default`**: Get_UnsetProperty_ReturnsDefault
- **`Rendering-Model-Options-StoreAndRetrieve`**: Get_AfterSet_ReturnsStoredValue
- **`Rendering-Model-Options-Contains`**: Contains_ReflectsExplicitSet
- **`Rendering-Model-Options-TryGet`**: TryGet_UnsetProperty_ReturnsFalseAndDefault
- **`Rendering-Model-LayoutGraph-AddNode`**: AddNode_AppendsNodeAndReturnsIt
- **`Rendering-Model-LayoutGraph-AddEdge`**: AddEdge_AppendsEdgeWithEndpoints
- **`Rendering-Model-LayoutGraph-PerElementProperties`**: Node_CarriesPerElementProperties
