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
  the origin (while every waypoint is inside) is brought fully inside the canvas;
  `CanvasFitter_Fit_OutwardPortLabelLeftOfOrigin_TranslatesTreeToHoldLabel` and
  `CanvasFitter_Fit_PortGlyphAboveOrigin_TranslatesTree` confirm an outward port label or port glyph past the
  origin translates the tree; `RenderedSvg_OutwardPortLabelsAndEndLabels_AreInsideCanvas` and
  `RenderedSvg_EndLabels_AreAtThePositionsTheLayoutPredicted` (gallery project) confirm that in a rendered SVG every
  port label, end label and port glyph is inside the canvas and each end label is where the layout predicted.
- **Unrecognized node unchanged** (`Rendering-Layout-CanvasFitter-UnrecognizedNodeUnchanged`):
  `CanvasFitter_Fit_UnrecognizedNode_ReturnsSameTree` confirms a tree containing a node subtype outside the
  closed set is returned unchanged.
- **Validation** (`Rendering-Layout-CanvasFitter-Validation`): `CanvasFitter_Fit_NullTree_Throws` confirms
  a null tree argument is rejected with an argument-null error.
- **Padded fit** (`Rendering-Layout-CanvasFitter-PaddedFit`):
  `CanvasFitter_FitWithPadding_ContentHuggedByPadding_ReturnsSameTree` confirms a tree whose content already hugs
  the canvas is returned as the same instance; `..._SurplusRing_ShrinksCanvasAndTranslatesContent` confirms a
  surplus margin shrinks the canvas and translates the content; `..._ContentOffCenter_CentersWithoutChangingCanvasSize`
  confirms an off-center content is centered with the canvas size unchanged;
  `..._LabelInsidePaddingBand_KeepsPaddingBeyondLabel` confirms the padding is kept beyond a resolved label;
  `CanvasFitter_MeasureMargin_Boxes_ReturnsSmallestMargin` confirms the reported smallest margin;
  `..._UnrecognizedNode_ReturnsSameTree` confirms an unknown node leaves the tree unchanged.
- **Trim to content** (`Rendering-Layout-CanvasFitter-TrimToContent`):
  `CanvasFitter_TrimToContent_Boxes_StartsAtOriginAndHugsContent` confirms the content starts at the origin and the
  canvas hugs it; `..._BendLeftOfBoxes_IsContentAndHonorsBendInset` confirms a connector bend counts as content
  with its inset; `..._EmptyTree_ReturnsSameTree` and `..._AlreadyTrimmed_ReturnsSameTree` confirm the identity
  cases; `..._NullTree_Throws` confirms the argument-null error.

#### Requirements Coverage

- **`Rendering-Layout-CanvasFitter-LeavesContainedTreeUnchanged`**:
  CanvasFitter_Fit_ContentInsideCanvas_ReturnsSameTree
- **`Rendering-Layout-CanvasFitter-TranslatesWhenContentBeforeOrigin`**:
  CanvasFitter_Fit_WaypointLeftOfOrigin_TranslatesTreeAndWidensCanvas
- **`Rendering-Layout-CanvasFitter-GrowsCanvasForFarContent`**:
  CanvasFitter_Fit_WaypointBeyondFarEdges_GrowsCanvasWithoutTranslating
- **`Rendering-Layout-CanvasFitter-IncludesConnectorLabels`**:
  CanvasFitter_Fit_LabelLeftOfOrigin_BringsLabelInsideCanvas,
  CanvasFitter_Fit_OutwardPortLabelLeftOfOrigin_TranslatesTreeToHoldLabel,
  CanvasFitter_Fit_PortGlyphAboveOrigin_TranslatesTree, RenderedSvg_OutwardPortLabelsAndEndLabels_AreInsideCanvas,
  RenderedSvg_EndLabels_AreAtThePositionsTheLayoutPredicted
- **`Rendering-Layout-CanvasFitter-UnrecognizedNodeUnchanged`**:
  CanvasFitter_Fit_UnrecognizedNode_ReturnsSameTree
- **`Rendering-Layout-CanvasFitter-Validation`**:
  CanvasFitter_Fit_NullTree_Throws
- **`Rendering-Layout-CanvasFitter-PaddedFit`**:
  CanvasFitter_FitWithPadding_ContentHuggedByPadding_ReturnsSameTree,
  CanvasFitter_FitWithPadding_SurplusRing_ShrinksCanvasAndTranslatesContent,
  CanvasFitter_FitWithPadding_ContentOffCenter_CentersWithoutChangingCanvasSize,
  CanvasFitter_FitWithPadding_LabelInsidePaddingBand_KeepsPaddingBeyondLabel,
  CanvasFitter_MeasureMargin_Boxes_ReturnsSmallestMargin, CanvasFitter_FitWithPadding_UnrecognizedNode_ReturnsSameTree
- **`Rendering-Layout-CanvasFitter-TrimToContent`**:
  CanvasFitter_TrimToContent_Boxes_StartsAtOriginAndHugsContent,
  CanvasFitter_TrimToContent_BendLeftOfBoxes_IsContentAndHonorsBendInset,
  CanvasFitter_TrimToContent_EmptyTree_ReturnsSameTree, CanvasFitter_TrimToContent_AlreadyTrimmed_ReturnsSameTree,
  CanvasFitter_TrimToContent_NullTree_Throws
