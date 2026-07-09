## Layout Tree Unit Design

Part of the Rendering Model system.

### Layout Tree Purpose

The Layout Tree unit's single responsibility is to define the placed, renderer-agnostic intermediate
representation that a layout algorithm produces and a renderer draws: the `LayoutTree` container, the
`LayoutNode` discriminated-union hierarchy of concrete node records (`LayoutBox`, `LayoutPort`,
`LayoutLine`, `LayoutLabel`, `LayoutBadge`, `LayoutBand`, `LayoutLifeline`, `LayoutActivation`,
`LayoutGrid`, and their sub-records), the shared geometry value types (`Point2D`, `Rect`), and the
notation enumerations used across the tree. The unit performs no layout and no rendering; it is a
pure, immutable data vocabulary.

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
  `Compartments`, `Children`, optional `Keyword`, optional resolved shape-geometry hints
  `RoundedCornerRadius`, `FolderTabWidth`, and `FolderTabHeight`, and `ContentInsetLeft`,
  `ContentInsetRight`, `ContentInsetTop`, `ContentInsetBottom` (each defaulting to `0.0`) — a
  per-side reserved margin auto-computed by a layout algorithm from any port labels on that face, so
  a renderer knows where title/compartment content may start and must stop without colliding with a
  port label.
- `LayoutCompartment` (sealed record) — `Title` and text `Rows`.
- `LayoutPort` (node record) — `CentreX`, `CentreY`, attached `Side`, `ExternalLabel`,
  `InternalLabel` (defaulting to `null`), and `MaxLabelWidth` (defaulting to
  `double.PositiveInfinity`). A plain port carries only an `ExternalLabel`, rendered inward beside the
  glyph exactly as the single legacy label was (keeping every pre-existing call site byte-identical). A
  boundary (delegation) port additionally carries an `InternalLabel`: its presence marks the port as a
  boundary port and moves the `ExternalLabel` to the outward face while the `InternalLabel` reads
  inward, so one physical anchor names both the connection reaching it from outside the container and
  the delegation relaying it into the container's own child scope. `MaxLabelWidth` is an optional upper
  bound, computed by a layout algorithm from the owning box's placed width (a `LayoutPort` has no
  reference back to its owning box, so this bound must be precomputed and carried on the port itself),
  that a renderer uses to squeeze an excessively long port label rather than let it visually overlap
  the opposite port's label region.
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

### Layout Tree Key Methods

N/A - the Layout Tree types are immutable data records with no behavioral methods. Each record type
exposes only its declared fields through record-generated property getters and value-based equality;
there are no operations to document beyond construction (all fields are supplied through the
positional or init-only constructor and returned unchanged).

### Layout Tree Error Handling

N/A - the Layout Tree types are passive immutable records that do no I/O, allocate no unmanaged
resources, and expose no operational methods. There are no runtime error paths to detect or propagate;
argument validation (for example, non-null identifiers on graph elements) is the responsibility of the
Layout Graph unit, not this unit.

### Layout Tree Design Constraints

- All node coordinates shall be absolute in logical pixels with the origin at the top-left; the model
  shall not apply any coordinate transform.
- `LayoutBox` shall express nesting as an integer `Depth`, not a resolved color, so that the theme
  in effect at render time selects the fill color.
- `LayoutBox` may carry optional resolved shape-geometry hints when routing and rendering need to
  agree on the real outline of a non-rectangular shape; the hints remain geometric values rather than
  resolved drawing commands so the model stays renderer-agnostic.
- `LayoutBox`'s four `ContentInset*` values shall default to `0.0`, so an existing caller whose boxes
  have no ports on any face reads back byte-identical geometry; the four values are appended as new
  optional positional-record parameters after `FolderTabHeight` (the primary constructor's last
  existing parameter) to preserve source-compatibility of every pre-existing positional `new
  LayoutBox(...)` call site.
- `LayoutNode` shall remain an open discriminated union; adding a new node type shall be an additive
  change, and consumers shall skip node types they do not recognize.
- All node records shall be immutable, so a placed tree can be shared and rendered repeatedly without
  defensive copying.

### Layout Tree Interactions

The Layout Tree is produced by a layout algorithm (see the *Rendering Abstractions* design,
`ILayoutAlgorithm`) and consumed by a renderer (`IRenderer`). Within the tree, `LayoutBox` and
`LayoutBand` hold nested `Children`, so renderers walk the node hierarchy recursively.

### Layout Tree Dependencies

The unit depends only on the .NET base class library. It has no project references, no OTS runtime
component, and no Shared Package dependency; within the Rendering model, it is a peer of the Options
and Layout Graph units and depends on neither at the code level (though `LayoutGraphEdge` in the
Layout Graph unit consumes the `EndMarkerStyle` and `LineStyle` enumerations declared here as part of
the shared notation vocabulary).

### Layout Tree Callers

The unit is written by layout algorithms in `DemaConsulting.Rendering.Layout` — the `layered`,
`containment`, and `hierarchical` algorithms and the orthogonal edge router — and read by renderers
in `DemaConsulting.Rendering.Svg` and `DemaConsulting.Rendering.Skia` (`SvgRenderer`, `PngRenderer`,
`JpegRenderer`, `WebpRenderer`). The `ILayoutAlgorithm` and `IRenderer` contracts in
`DemaConsulting.Rendering.Abstractions` reference `LayoutTree` and its geometry types as their
produced and consumed values. Application code typically does not construct a `LayoutTree` directly;
it obtains one from a layout algorithm and passes it to a renderer.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Model-LayoutTree-Canvas | `LayoutTree.Width`, `Height`, `Nodes` |
| Rendering-Model-LayoutTree-AbsoluteCoordinates | Absolute coordinates on every node record |
| Rendering-Model-LayoutTree-Box | `LayoutBox` record |
| Rendering-Model-LayoutTree-DepthNotColor | `LayoutBox.Depth` integer |
| Rendering-Model-LayoutTree-ContentInset | `LayoutBox.ContentInsetLeft/Right/Top/Bottom` |
| Rendering-Model-LayoutTree-Port | `LayoutPort` record |
| Rendering-Model-LayoutTree-PortMaxLabelWidth | `LayoutPort.MaxLabelWidth` |
| Rendering-Model-LayoutTree-Line | `LayoutLine` record |
| Rendering-Model-LayoutTree-Label | `LayoutLabel` record |
| Rendering-Model-LayoutTree-Badge | `LayoutBadge` record |
| Rendering-Model-LayoutTree-Band | `LayoutBand` record |
| Rendering-Model-LayoutTree-Lifeline | `LayoutLifeline` record |
| Rendering-Model-LayoutTree-Activation | `LayoutActivation` record |
| Rendering-Model-LayoutTree-Grid | `LayoutGrid` / `LayoutGridRow` / `LayoutGridCell` |
| Rendering-Model-LayoutTree-Geometry | `Point2D` and `Rect` value types |
| Rendering-Model-LayoutTree-ShapeGeometryHints | `LayoutBox` shape-geometry hint properties |
