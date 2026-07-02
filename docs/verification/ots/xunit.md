# xUnit Verification

This document provides the verification evidence for the xUnit OTS software item. Requirements
for this OTS item are defined in the xUnit OTS Software Requirements document.

## Required Functionality

xUnit v3 (xunit.v3 and xunit.runner.visualstudio) is the unit-testing framework used by the
project. It discovers and runs all test methods and writes TRX result files that feed into coverage
reporting and requirements traceability. Passing tests confirm the framework is functioning
correctly.

## Verification Approach

Unlike the DemaConsulting tool OTS items, which are verified by their own self-validation evidence,
xUnit is the test framework itself. It is therefore verified by this repository's own test methods:
each scenario names a real test — drawn from across the model, layout, and renderer projects — that
xUnit must discover, execute, and record in a TRX result file. A passing pipeline run for all
scenarios constitutes evidence that both requirements are satisfied.

## Test Scenarios

### Get_AfterSet_ReturnsStoredValue

**Scenario**: xUnit discovers and runs this property-system test, which stores a value and reads it
back.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Rendering-OTS-xUnit-Execute`, `Rendering-OTS-xUnit-Report`.

### Contains_ReflectsExplicitSet

**Scenario**: xUnit discovers and runs this property-system test, which checks that `Contains`
reflects an explicit set.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Rendering-OTS-xUnit-Execute`, `Rendering-OTS-xUnit-Report`.

### AddNode_AppendsNodeAndReturnsIt

**Scenario**: xUnit discovers and runs this layout-graph test, which appends a node and returns it.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Rendering-OTS-xUnit-Execute`, `Rendering-OTS-xUnit-Report`.

### LayoutTree_Construction_StoresWidthHeightNodes

**Scenario**: xUnit discovers and runs this model test, which constructs a layout tree and asserts
its stored fields.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Rendering-OTS-xUnit-Execute`, `Rendering-OTS-xUnit-Report`.

### Apply_ChainGraph_PlacesLayeredBoxesAndRoutesEdges

**Scenario**: xUnit discovers and runs this layout test, which lays out a chain graph with the
layered algorithm.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Rendering-OTS-xUnit-Execute`, `Rendering-OTS-xUnit-Report`.

### Id_IsLayered

**Scenario**: xUnit discovers and runs this layout test, which asserts the layered algorithm's stable
identifier.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Rendering-OTS-xUnit-Execute`, `Rendering-OTS-xUnit-Report`.

### SvgRenderer_Render_SingleBox_ProducesSvgDocument

**Scenario**: xUnit discovers and runs this renderer test, which renders a single box to an SVG
document.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Rendering-OTS-xUnit-Execute`, `Rendering-OTS-xUnit-Report`.

### PngRenderer_Render_SingleBox_ProducesNonEmptyOutput

**Scenario**: xUnit discovers and runs this renderer test, which renders a single box to a non-empty
PNG.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Rendering-OTS-xUnit-Execute`, `Rendering-OTS-xUnit-Report`.

## Requirements Coverage

- **`Rendering-OTS-xUnit-Execute`**: Get_AfterSet_ReturnsStoredValue, Contains_ReflectsExplicitSet,
  AddNode_AppendsNodeAndReturnsIt, LayoutTree_Construction_StoresWidthHeightNodes,
  Apply_ChainGraph_PlacesLayeredBoxesAndRoutesEdges, Id_IsLayered,
  SvgRenderer_Render_SingleBox_ProducesSvgDocument,
  PngRenderer_Render_SingleBox_ProducesNonEmptyOutput
- **`Rendering-OTS-xUnit-Report`**: Get_AfterSet_ReturnsStoredValue, Contains_ReflectsExplicitSet,
  AddNode_AppendsNodeAndReturnsIt, LayoutTree_Construction_StoresWidthHeightNodes,
  Apply_ChainGraph_PlacesLayeredBoxesAndRoutesEdges, Id_IsLayered,
  SvgRenderer_Render_SingleBox_ProducesSvgDocument,
  PngRenderer_Render_SingleBox_ProducesNonEmptyOutput
