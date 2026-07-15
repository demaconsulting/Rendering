### LayoutTreePacker Unit Verification

Part of the Rendering Layout Verification.

This document maps the layout-tree-packer unit requirements to named test scenarios.

#### Verification Approach

`LayoutTreePacker` is a stateless static engine, so verification is by direct xUnit unit tests that
call `Pack` on synthetic `LayoutTree` lists. No mocks are used; the tests observe the real shelf-
packing and recursive-translation logic end-to-end so multi-tree packing, coordinate translation
(including nested children, waypoints, and port centres), degenerate cases, warning preservation, and
the unsupported-node-type guard are all measured on production output.

#### Test Environment

- **Framework**: xUnit v3 running on the .NET SDK.
- **Runner**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Project**: `test/DemaConsulting.Rendering.Layout.Tests/Engine/LayoutTreePackerTests.cs`.
- **Dependencies**: no external services, files, or network access; every test constructs its own
  in-memory `LayoutTree` instances.
- **Isolation**: each test builds its own inputs; the engine holds no state between calls.

#### Acceptance Criteria

A verification run passes when every named scenario below asserts without unexpected exception, and
the referenced tests cover each `Rendering-Layout-LayoutTreePacker-*` requirement. Any overlap between
packed sub-trees, an untranslated coordinate (box, nested child, waypoint, or port centre), a wrong
degenerate-case result, a dropped warning, a silently-skipped unrecognized node type, or a missing
null-argument guard constitutes a failure.

#### Test Scenarios

- **Packs multiple trees** (`Rendering-Layout-LayoutTreePacker-PacksMultipleTrees`):
  `Pack_TwoTrees_PlacesSecondBesideFirstWithoutOverlap` confirms two trees pack side by side without
  overlapping; `Pack_ManyTrees_WrapsOntoNewShelfWithoutOverlap` confirms enough trees to overflow one
  shelf wrap onto a new row without any pair overlapping.
- **Translates coordinates** (`Rendering-Layout-LayoutTreePacker-TranslatesCoordinates`):
  `Pack_TwoTrees_PlacesSecondBesideFirstWithoutOverlap` confirms the second tree's box coordinates are
  shifted by its assigned shelf offset.
- **Translates nested children** (`Rendering-Layout-LayoutTreePacker-TranslatesNestedChildren`):
  `Pack_BoxWithNestedChildren_TranslatesChildrenRecursively` confirms a packed container box's nested
  children are shifted by the same offset as their parent.
- **Translates waypoints** (`Rendering-Layout-LayoutTreePacker-TranslatesWaypoints`):
  `Pack_TreeWithLine_TranslatesEveryWaypoint` confirms every waypoint of a packed connector line is
  translated; `Pack_TreeWithPort_TranslatesCentre` confirms a packed port's centre coordinate is
  translated.
- **Degenerate cases** (`Rendering-Layout-LayoutTreePacker-DegenerateCases`):
  `Pack_EmptyList_ReturnsEmptyTree` confirms an empty list yields a zero-size, empty tree;
  `Pack_SingleTree_ReturnsItUnchanged` confirms a single-tree list is returned unchanged.
- **Unsupported node type** (`Rendering-Layout-LayoutTreePacker-UnsupportedNodeType`):
  `Pack_UnrecognizedNodeType_ThrowsNotSupportedException` confirms a sub-tree containing a `LayoutNode`
  subtype outside the closed set throws `NotSupportedException` rather than being silently skipped.
- **Preserves warnings** (`Rendering-Layout-LayoutTreePacker-PreservesWarnings`):
  `Pack_TreesWithWarnings_PreservesAllWarnings` confirms every packed sub-tree's `Warnings` entries
  appear in the combined tree.
- **Validation** (`Rendering-Layout-LayoutTreePacker-Validation`): `Pack_NullTrees_ThrowsArgumentNullException`
  confirms a null `trees` argument is rejected with an argument-null error.

#### Requirements Coverage

- **`Rendering-Layout-LayoutTreePacker-PacksMultipleTrees`**:
  Pack_TwoTrees_PlacesSecondBesideFirstWithoutOverlap, Pack_ManyTrees_WrapsOntoNewShelfWithoutOverlap
- **`Rendering-Layout-LayoutTreePacker-TranslatesCoordinates`**:
  Pack_TwoTrees_PlacesSecondBesideFirstWithoutOverlap
- **`Rendering-Layout-LayoutTreePacker-TranslatesNestedChildren`**:
  Pack_BoxWithNestedChildren_TranslatesChildrenRecursively
- **`Rendering-Layout-LayoutTreePacker-TranslatesWaypoints`**:
  Pack_TreeWithLine_TranslatesEveryWaypoint, Pack_TreeWithPort_TranslatesCentre
- **`Rendering-Layout-LayoutTreePacker-DegenerateCases`**:
  Pack_EmptyList_ReturnsEmptyTree, Pack_SingleTree_ReturnsItUnchanged
- **`Rendering-Layout-LayoutTreePacker-UnsupportedNodeType`**:
  Pack_UnrecognizedNodeType_ThrowsNotSupportedException
- **`Rendering-Layout-LayoutTreePacker-PreservesWarnings`**:
  Pack_TreesWithWarnings_PreservesAllWarnings
- **`Rendering-Layout-LayoutTreePacker-Validation`**:
  Pack_NullTrees_ThrowsArgumentNullException
