### CanvasFitter Unit Design

Part of the Rendering Layout system.

#### CanvasFitter Purpose

`CanvasFitter` is a post-placement correction that grows a placed `LayoutTree`'s canvas so every
connector waypoint and every connector label lies inside it. The canvas a layout algorithm reports is
computed from the placed boxes plus the engine's own padding. Two kinds of content can still fall outside
it: a connector that the router detours around the outer edge of a container wall (its keep-out lane lies
beyond the outermost box, so for a box near the origin it lies at a negative coordinate), and a connector
label that the renderers place beside its line (a label at the left or top edge of the diagram extends
past the origin, and one at the right or bottom edge past the declared width or height). Renderers can
only grow a canvas to the right and down, so anything at a negative coordinate is clipped; `CanvasFitter`
makes the canvas a true bound of the drawn content instead.

#### CanvasFitter Data Model

`CanvasFitter` is an internal static class with no instance state. Input is a placed `LayoutTree` and the
body `fontSize` (in logical pixels) used to size connector labels. The result is a `LayoutTree`. The
internal constant `FitMargin` (10 px, half of the engine's uniform padding) is the clearance kept between
content that had to be brought back inside and the canvas edge, enough that a connector stroke or an
arrowhead on the edge is never clipped.

#### CanvasFitter Methods

`Fit(tree, fontSize)` rejects a null `tree` with `ArgumentNullException`, then:

1. **Collect.** Every `LayoutBox` (recursing through `Children`), `LayoutLine`, and `LayoutPort` is
   collected. A tree with a node of any other type, or with no line, is returned unchanged.
2. **Measure.** The extent (minimum and maximum X and Y) of every connector waypoint is computed. Midpoint and end
   labels are resolved through the shared `ConnectorLabelPlacer.ResolveLabels` resolver that the SVG and raster
   renderers also call (same obstacle boxes, port glyph and outward port label rectangles, and ordering; the light theme
   with the caller's font size), so the measured positions are the positions the renderers draw. Every label rectangle,
   every port glyph, and every outward port label is added to the extent.
3. **Contained case.** When the whole extent lies inside `[0, Width] x [0, Height]`, the same instance is
   returned, so every layout that already fits is byte-identical to its previous output.
4. **Translate.** On an axis whose content starts before the origin, every node is translated by
   `FitMargin` minus that minimum, through `LayoutTreePacker.TranslateNode` (the same recursive translation
   used for packing), and the canvas grows by the same amount on that axis.
5. **Grow.** On an axis whose content (after any translation) ends beyond the canvas size, the size becomes
   that maximum plus `FitMargin`. An axis whose content already fits is left unchanged.

#### CanvasFitter Error Handling

A null `tree` throws `ArgumentNullException`. A tree containing a node type outside the closed set
(`LayoutBox`, `LayoutLine`, `LayoutPort`) is returned unchanged rather than failing: the bundled algorithms
never emit such a node, and because fitting is an optional correction (unlike packing, which cannot
partially succeed without corrupting coordinates), leaving the tree alone is safer than mis-placing part of
it.

#### CanvasFitter Interactions

`CanvasFitter` depends on the `LayoutTree`, `LayoutBox`, `LayoutLine`, and `LayoutPort` types, on
`ConnectorLabelPlacer` and the notation marker metrics from `DemaConsulting.Rendering.Abstractions`, and on
`LayoutTreePacker.TranslateNode` (internal to the Engine subsystem). It is called at the end of
`LayeredLayoutAlgorithm` and at both return sites of the hierarchical algorithm's per-scope layout
(with that scope's assumed font size), so that every placed tree the bundled algorithms return keeps its
connectors and labels inside the canvas.

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-CanvasFitter-LeavesContainedTreeUnchanged | CanvasFitter behavior described above (contained case) |
| Rendering-Layout-CanvasFitter-TranslatesWhenContentBeforeOrigin | CanvasFitter behavior described above (translate) |
| Rendering-Layout-CanvasFitter-GrowsCanvasForFarContent | CanvasFitter behavior described above (grow) |
| Rendering-Layout-CanvasFitter-IncludesConnectorLabels | CanvasFitter behavior described above (measure) |
| Rendering-Layout-CanvasFitter-UnrecognizedNodeUnchanged | CanvasFitter behavior described above (collect) |
| Rendering-Layout-CanvasFitter-Validation | CanvasFitter behavior described above |
