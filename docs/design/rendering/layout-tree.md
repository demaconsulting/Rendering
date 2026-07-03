## Layout Tree Unit Design

Part of the Rendering Model system.

### Layout Tree Overview

The Layout Tree unit is the placed, renderer-agnostic representation of one view. A `LayoutTree`
carries the canvas dimensions and a flat list of top-level `LayoutNode` instances. `LayoutNode` is an
abstract record acting as the root of a discriminated union; renderers switch on the concrete type
and skip unknown subtypes for forward compatibility. All coordinates are absolute, with the origin at
the top-left, so a renderer can draw each element directly without resolving nested offsets.

### Layout Tree Data Model

- `LayoutTree` (sealed record) — `Width`, `Height`, the top-level `Nodes` list, and layout-quality
  `Warnings`.
- `LayoutNode` (abstract record) — the discriminated-union root; carries no members.
- `LayoutBox` (node record) — `X`, `Y`, `Width`, `Height`, `Label`, integer `Depth`, `Shape`,
  `Compartments`, `Children`, and optional `Keyword`.
- `LayoutCompartment` (sealed record) — `Title` and text `Rows`.
- `LayoutPort` (node record) — `CentreX`, `CentreY`, attached `Side`, and `Label`.
- `LayoutLine` (node record) — `Waypoints`, `SourceEnd`, `TargetEnd`, `LineStyle`, and
  `MidpointLabel`.
- `Point2D` (sealed record) — `X` and `Y`.
- `Rect` (readonly record struct) — `X`, `Y`, `Width`, `Height`; the shared public axis-aligned
  rectangle geometry value type in logical pixels, used alongside `Point2D` and `PortSide`.
- `LayoutLabel` (node record) — `X`, `Y`, `MaxWidth`, `Text`, `Align`, `Weight`, `Style`, `FontSize`.
- `LayoutBadge` (node record) — `CentreX`, `CentreY`, `Size`, `Shape`, and `Label`.
- `LayoutBand` (node record) — `X`, `Y`, `Width`, `Height`, `Orientation`, `Label`, and `Children`.
- `LayoutLifeline` (node record) — `CentreX`, `TopY`, `BottomY`, `Label`, `HeaderWidth`,
  `HeaderHeight`.
- `LayoutActivation` (node record) — `CentreX`, `TopY`, `BottomY`.
- `LayoutGrid` (node record) — `X`, `Y`, and `Rows`.
- `LayoutGridRow` (sealed record) — `IsHeader` and `Cells`.
- `LayoutGridCell` (sealed record) — `Width`, `Height`, `Text`, `Align`, `ColSpan`.

The unit also defines the notation enumerations `BoxShape`, `PortSide`, `EndMarkerStyle`,
`LineStyle`, `TextAlign`, `FontWeight`, `FontStyle`, `BadgeShape`, and `BandOrientation`.

`Point2D` and `Rect` are the unit's shared geometry value types. They carry no behavior of their own;
they exist so every node record and every layout algorithm expresses coordinates and bounds with one
consistent, allocation-light vocabulary.

### Layout Tree Design Constraints

- All node coordinates shall be absolute in logical pixels with the origin at the top-left; the model
  shall not apply any coordinate transform.
- `LayoutBox` shall express nesting as an integer `Depth`, not a resolved color, so that the theme
  in effect at render time selects the fill color.
- `LayoutNode` shall remain an open discriminated union; adding a new node type shall be an additive
  change, and consumers shall skip node types they do not recognize.
- All node records shall be immutable, so a placed tree can be shared and rendered repeatedly without
  defensive copying.

### Layout Tree Interactions

The Layout Tree is produced by a layout algorithm (see the *Rendering Abstractions* design,
`ILayoutAlgorithm`) and consumed by a renderer (`IRenderer`). Within the tree, `LayoutBox` and
`LayoutBand` hold nested `Children`, so renderers walk the node hierarchy recursively.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Model-LayoutTree-Canvas | `LayoutTree.Width`, `Height`, `Nodes` |
| Rendering-Model-LayoutTree-AbsoluteCoordinates | Absolute coordinates on every node record |
| Rendering-Model-LayoutTree-Box | `LayoutBox` record |
| Rendering-Model-LayoutTree-DepthNotColor | `LayoutBox.Depth` integer |
| Rendering-Model-LayoutTree-Port | `LayoutPort` record |
| Rendering-Model-LayoutTree-Line | `LayoutLine` record |
| Rendering-Model-LayoutTree-Label | `LayoutLabel` record |
| Rendering-Model-LayoutTree-Badge | `LayoutBadge` record |
| Rendering-Model-LayoutTree-Band | `LayoutBand` record |
| Rendering-Model-LayoutTree-Lifeline | `LayoutLifeline` record |
| Rendering-Model-LayoutTree-Activation | `LayoutActivation` record |
| Rendering-Model-LayoutTree-Grid | `LayoutGrid` / `LayoutGridRow` / `LayoutGridCell` |
| Rendering-Model-LayoutTree-Geometry | `Point2D` and `Rect` value types |
