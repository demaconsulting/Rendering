# Layout quality (#48, #60)

The benchmark scenes behind the layout-quality work: cross-hierarchy connector crossing minimization (Rendering#48) and
compact, honestly spaced layouts (Rendering#60). Every diagram below is laid out from the same graph the regression
tests measure, and the before/after numbers (connector crossings, empty space, block area ratio, canvas size) against
the baseline (main@219650c) are in the [metrics table](metrics.md).

[Back to the gallery index](../README.md)

## Nested packages and cross-hierarchy edges (#48)

Package folders holding definitions and nested folders, joined by connectors that cross the package borders. The order
of the members inside a folder used to decide whether those connectors crossed, and every container reserved a wide
empty margin around its content. The layered engine now orders the members of each container by where their
cross-hierarchy partners sit, and containers hug their content.

![Two package folders and a root definition joined by cross-hierarchy connectors](repro2-right.svg)

The smallest reproduction: folder B holds C, S (specializing C) and H, folder P holds E and the root holds K. The
connectors H to E, E to K and C to K leave their folders without crossing, whatever order the members were added in.

![The same reproduction with an extra connector inside the second folder](repro3-right.svg)

Adds a connector M to E inside folder P, which the connector H to E entering the folder must not cross.

![Connectors reaching leaves three containers deep](nested-depth-right.svg)

H reaches X three containers deep, and X and C reach the root definition K; the unconnected E sits beside the nested
folders.

![Four package folders with nested package and keyword-labeled connectors, flowing right](nested-packages-right.svg)

The reproduction scaled up: design, requirements, verification and library packages, a nested power package,
keyword-labeled connectors between leaves and between packages, and unconnected definitions.

![The nested packages rendered through the raster path](nested-packages-right.png)

The same diagram through the CanvasNet raster renderer.

![The same four packages flowing up](nested-packages-up.svg)

The same graph with the SysML General View direction: generals above their specifics.

![The upward nested packages rendered through the raster path](nested-packages-up.png)

The same diagram through the CanvasNet raster renderer.

## SysML general view and usage tree

Two larger diagrams in the shape consumers produce: a General View with package folders, definitions and usages as
nodes, composite and typing connectors and scattered insertion order, and a dense usage tree with one owner, a dozen
usages and shared and unused definitions.

![A SysML general view flowing up](general-view-up.svg)

Folders, definitions and usages joined by composite (filled diamond) and typing (hollow triangle with two dots)
connectors, many of them across package borders, with orphan definitions tucked into free space.

![The general view rendered through the raster path](general-view-up.png)

The same diagram through the CanvasNet raster renderer.

![A drone usage tree with shared and isolated definitions](dense-usage-tree.svg)

One owner with a dozen usages typed by shared definitions, plus unused definitions packed beside the tree.

![The usage tree rendered through the raster path](dense-usage-tree.png)

The same diagram through the CanvasNet raster renderer.

## Interconnection corridors and LayerSpacing (#60)

Parts and ports joined by labeled connectors with end labels, laid out to the right. The corridors between layers hold
the labels as well as the routing lanes, and the LayerSpacing option sets the least gap between adjacent layers: a
floor, since the connector routing may still need more. An unset option keeps the historical 70 px corridor.

![An interconnection with labeled connectors, LayerSpacing unset](interconnection-labeled-corridors.svg)

The default: corridors sized by the labels and lanes they hold.

![The default interconnection rendered through the raster path](interconnection-labeled-corridors.png)

The same diagram through the CanvasNet raster renderer.

![The same interconnection with LayerSpacing 40](interconnection-layer-spacing-40.svg)

A spacing below what the labels and lanes need changes nothing: the option is a floor, not a ceiling.

![A source feeding two targets with LayerSpacing unset](layer-spacing-default.svg)

The Rendering#60 reproduction: three unlabeled boxes with the default 70 px corridor between the layers.

![The same fan-out with LayerSpacing 150](layer-spacing-150.svg)

The same graph with LayerSpacing set to 150: the corridor between the layers is 150 px wide, leaving room for connector
labels and bends at the cost of a wider canvas.

![The LayerSpacing 150 fan-out rendered through the raster path](layer-spacing-150.png)

The same diagram through the CanvasNet raster renderer.

## Flows, state machines and isolated definitions

Small diagrams that must stay exactly as good as before: a downward action flow, a state machine with back edges, and a
tall connected chain beside isolated definitions that the auto algorithm now packs into the free space.

![An action flow with decisions, a fork and join, and a back edge](action-flow.svg)

A downward flow with a guarded decision, a fork and join pair, a merge and a retry edge back to an earlier action.

![A state machine with two back edges](state-machine.svg)

A downward state machine whose reversed edges end in wrap-around corridors.

![A tall connected chain beside isolated definitions](auto-tall-plus-isolated.svg)

The auto algorithm lays out the chain with the layered algorithm and tucks the isolated definitions into the free space
beside it.

![The tall chain with isolated definitions rendered through the raster path](auto-tall-plus-isolated.png)

The same diagram through the CanvasNet raster renderer.

## Measurements

The [metrics table](metrics.md) lists, for every scene above, the connector crossings, the largest empty square relative
to the block height (B4), the block area ratio (B5) and the canvas size before (main@219650c) and after the
layout-quality work. The gallery test that writes it fails if any scene gets worse than the baseline.
