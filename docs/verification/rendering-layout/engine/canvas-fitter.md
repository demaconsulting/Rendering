### CanvasFitter Unit Verification

Part of the Rendering Layout Verification.

This document maps the canvas-fitter unit requirements to named test scenarios.

#### Verification Approach

`CanvasFitter` is a stateless static engine, so verification is by direct xUnit unit tests that call `Fit`
on small synthetic `LayoutTree` inputs. No mocks are used; the tests observe the real label placement,
extent measurement, and translation logic, so the contained case (same instance), the translate case, the
grow case, label inclusion, the unrecognized-node guard, and argument validation are all measured on
production output. The integration with the layout algorithms is verified by their own unit tests (see the
`ContentInsideCanvas` scenarios of the layered and hierarchical layout algorithm verification documents).

#### Test Environment

- **Framework**: xUnit v4 running on the .NET SDK.
- **Runner**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Project**: `test/DemaConsulting.Rendering.Layout.Tests/Engine/CanvasFitterTests.cs`.
- **Dependencies**: no external services, files, or network access; every test constructs its own
  in-memory `LayoutTree` instances.
- **Isolation**: each test builds its own inputs; the engine holds no state between calls.

#### Acceptance Criteria

A verification run passes when every named scenario below asserts without unexpected exception, and the
referenced tests cover each `Rendering-Layout-CanvasFitter-*` requirement. Any change to a tree whose
content already fits, a waypoint or label left outside the canvas after fitting, an unexpected translation
when content only exceeds the far edges, a changed result for a tree with an unrecognized node type, or a
missing null-argument guard constitutes a failure.

#### Test Scenarios

- **Leaves contained tree unchanged** (`Rendering-Layout-CanvasFitter-LeavesContainedTreeUnchanged`):
  `CanvasFitter_Fit_ContentInsideCanvas_ReturnsSameTree` confirms a tree whose waypoints and labels are
  inside the canvas is returned as the same instance.
- **Translates when content before origin** (`Rendering-Layout-CanvasFitter-TranslatesWhenContentBeforeOrigin`):
  `CanvasFitter_Fit_WaypointLeftOfOrigin_TranslatesTreeAndWidensCanvas` confirms a waypoint at a negative
  coordinate moves the whole tree inside the origin by the fit margin and widens the canvas accordingly.
- **Grows canvas for far content** (`Rendering-Layout-CanvasFitter-GrowsCanvasForFarContent`):
  `CanvasFitter_Fit_WaypointBeyondFarEdges_GrowsCanvasWithoutTranslating` confirms content past the width
  and height enlarges the canvas to the far content plus the margin while every node keeps its coordinates.
- **Includes connector labels** (`Rendering-Layout-CanvasFitter-IncludesConnectorLabels`):
  `CanvasFitter_Fit_LabelLeftOfOrigin_BringsLabelInsideCanvas` confirms a connector label extending left of
  the origin (while every waypoint is inside) is brought fully inside the canvas.
- **Unrecognized node unchanged** (`Rendering-Layout-CanvasFitter-UnrecognizedNodeUnchanged`):
  `CanvasFitter_Fit_UnrecognizedNode_ReturnsSameTree` confirms a tree containing a node subtype outside the
  closed set is returned unchanged.
- **Validation** (`Rendering-Layout-CanvasFitter-Validation`): `CanvasFitter_Fit_NullTree_Throws` confirms
  a null tree argument is rejected with an argument-null error.

#### Requirements Coverage

- **`Rendering-Layout-CanvasFitter-LeavesContainedTreeUnchanged`**:
  CanvasFitter_Fit_ContentInsideCanvas_ReturnsSameTree
- **`Rendering-Layout-CanvasFitter-TranslatesWhenContentBeforeOrigin`**:
  CanvasFitter_Fit_WaypointLeftOfOrigin_TranslatesTreeAndWidensCanvas
- **`Rendering-Layout-CanvasFitter-GrowsCanvasForFarContent`**:
  CanvasFitter_Fit_WaypointBeyondFarEdges_GrowsCanvasWithoutTranslating
- **`Rendering-Layout-CanvasFitter-IncludesConnectorLabels`**:
  CanvasFitter_Fit_LabelLeftOfOrigin_BringsLabelInsideCanvas
- **`Rendering-Layout-CanvasFitter-UnrecognizedNodeUnchanged`**:
  CanvasFitter_Fit_UnrecognizedNode_ReturnsSameTree
- **`Rendering-Layout-CanvasFitter-Validation`**:
  CanvasFitter_Fit_NullTree_Throws
