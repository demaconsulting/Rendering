# Rendering Abstractions Design

## Overview

`DemaConsulting.Rendering.Abstractions` is the service provider interface (SPI) that sits between the
rendering model (*Rendering Model* system, `DemaConsulting.Rendering`) and the concrete layout and
renderer implementations. It defines the pluggable `ILayoutAlgorithm` and `IRenderer` contracts, the
registries that resolve an implementation by identifier or media type, the visual `Theme` model, and
the single-source geometry helpers (`NotationMetrics`, `BoxMetrics`, `ConnectorLabelPlacer`) that let
every renderer draw identical decorations. The package depends only on the rendering model and the
.NET base class library.

The ELK-inspired flow is: a `LayoutGraph` plus `LayoutOptions` is passed to an `ILayoutAlgorithm`,
which produces a placed `LayoutTree`; an `IRenderer` then draws that tree to an output stream.
Algorithms and renderers are selected at run time through the registries, so additional diagram
families and output formats are introduced purely additively.

## Software Structure

```text
DemaConsulting.Rendering.Abstractions (System)
├── RenderingContracts (Unit)
├── Registries (Unit)
├── Theme (Unit)
├── NotationMetrics (Unit)
├── BoxMetrics (Unit)
└── ConnectorLabelPlacer (Unit)
```

- **Rendering Contracts** — `ILayoutAlgorithm`, `IRenderer`, `RenderOptions`, `RenderOutput`.
- **Registries** — `LayoutAlgorithmRegistry`, `RendererRegistry`.
- **Theme** — `Theme` and the built-in `Themes`.
- **Notation Metrics** — `NotationMetrics` and `MarkerVertex`.
- **Box Metrics** — `BoxMetrics`.
- **Connector Label Placer** — `ConnectorLabelPlacer`.

## Rendering Contracts Unit

### Contracts Overview

The Rendering Contracts unit defines the two extension-point interfaces and the value types that flow
across them. `ILayoutAlgorithm` is the high-level extension point that turns an unplaced graph into a
placed tree; `IRenderer` is the low-level extension point that turns a placed tree into an output
stream. `RenderOptions` carries the theme and sizing for a render, and `RenderOutput` bundles one
rendered stream with its metadata.

### Contracts Data Model

- `ILayoutAlgorithm` (interface) — `Id` and `Apply(LayoutGraph, LayoutOptions)`.
- `IRenderer` (interface) — `MediaType`, `DefaultExtension`, and
  `Render(LayoutTree, RenderOptions, Stream)`.
- `RenderOptions` (sealed record) — `Theme`, `Scale`, `Dpi`, `DepthLimit`.
- `RenderOutput` (sealed record) — `SuggestedFileName`, `MediaType`, `Data`, `Warnings`.

### Contracts Design Constraints

- An `ILayoutAlgorithm` shall expose a stable `Id` that matches the value read from
  `CoreOptions.Algorithm`, and shall ignore options it does not understand so callers may pass
  options intended for other algorithms without error.
- An `IRenderer` shall be pure and stateless and shall not perform filesystem access; it shall write
  only to the caller-supplied `Stream`.
- Adding a new algorithm or renderer shall be an additive change: a new implementation of these
  interfaces requires no change to the existing contracts.

### Contracts Interactions

`ILayoutAlgorithm.Apply` consumes a `LayoutGraph` and `LayoutOptions` from the rendering model and
produces a `LayoutTree`. `IRenderer.Render` consumes that `LayoutTree` and a `RenderOptions` (whose
`Theme` comes from the Theme unit). Instances are registered in and resolved from the Registries unit.

## Registries Unit

### Registries Overview

The Registries unit provides two service-provider lookups. `LayoutAlgorithmRegistry` keys algorithms
by their `Id`; `RendererRegistry` keys renderers by their `MediaType`. Consumers register the
implementations they wish to offer and resolve one at run time. Neither registry is thread-safe for
concurrent registration.

### Registries Data Model

- `LayoutAlgorithmRegistry` (sealed class) — `Ids`, `Register`, `Contains`, `TryResolve`, `Resolve`.
- `RendererRegistry` (sealed class) — `MediaTypes`, `Register`, `Contains`, `TryResolve`, `Resolve`.

### Registries Key Methods

`LayoutAlgorithmRegistry Register(ILayoutAlgorithm algorithm)` — stores the algorithm keyed by its
`Id`, replacing any previous algorithm with the same identifier, and returns the registry for fluent
chaining.

`ILayoutAlgorithm Resolve(string id)` — returns the algorithm registered under `id`, or throws
`KeyNotFoundException` when none is registered. `bool TryResolve(string, out ILayoutAlgorithm?)`
performs the same lookup without throwing.

`RendererRegistry.Register` and `Resolve` behave identically, keyed by `MediaType`; the renderer
registry compares media types case-insensitively.

### Registries Design Constraints

- `Resolve` shall raise `KeyNotFoundException` when the requested identifier or media type is not
  registered, so a configuration mistake surfaces immediately rather than as a later null-reference
  failure.
- `Register` shall replace any existing entry with the same key, so a consumer can override a bundled
  implementation.

### Registries Interactions

The registries hold `ILayoutAlgorithm` and `IRenderer` instances from the Rendering Contracts unit.
A caller resolves an algorithm using the identifier from `CoreOptions.Algorithm` and a renderer using
the desired output media type.

## Theme Unit

### Theme Overview

The Theme unit defines the visual parameters for rendering — depth-indexed fill colors, stroke
color and width, corner radius, font sizes, padding, and connector-approach geometry — and provides
three ready-made themes (`Light`, `Dark`, `Print`). Font choice is intentionally not part of the
theme; each renderer hardcodes its own typeface for consistent output.

### Theme Data Model

- `Theme` (sealed record) — `DepthFillColors`, `StrokeColor`, `StrokeWidth`, `LineCornerRadius`,
  `FontSizeTitle`, `FontSizeBody`, `LabelPadding`, `ConnectorStub`, `BendRadius`, `CleanLegMargin`.
- `Themes` (static class) — the built-in `Light`, `Dark`, and `Print` themes.

### Theme Key Methods

`double ConnectorApproachZone(double connectorClearance)` — returns the clear distance a connector
needs off a box face before it can bend, computed as `ConnectorStub + BendRadius + connectorClearance`.

`string BackgroundColor` — the depth-0 fill color, used to occlude a connector line behind a hollow
enclosing end marker.

### Theme Design Constraints

- `ConnectorApproachZone` shall equal the sum of the perpendicular stub, the corner bend radius, and
  the caller-supplied clearance, so that space reserved by layout matches geometry drawn by renderers.
- The `Light` and `Dark` themes shall carry identical connector geometry; the `Print` theme shall use
  a tighter stub and a zero bend radius suited to monochrome output.

### Theme Interactions

`Theme` is carried by `RenderOptions` (Rendering Contracts unit) and read by `NotationMetrics` and
`BoxMetrics` when deriving box and marker geometry.

## Notation Metrics Unit

### Notation Metrics Overview

The Notation Metrics unit is the single home for all intrinsic, theme-independent notation geometry
shared by the SVG and PNG renderers: end-marker (arrowhead) shapes and sizes, the port square,
folder-tab and note-fold proportions, rounded-rectangle corner scaling, badge fractions, and the
label-background inset. Every value is either a documented primitive constant or a documented
derivation of those primitives, so a geometry literal never appears more than once in the rendering
path. `MarkerVertex` expresses one vertex in tip-relative units.

### Notation Metrics Data Model

- `NotationMetrics` (static class) — the end-marker, port, folder, note, badge, and label constants
  plus the `AlongLineLength`, `TriangleVertices`, `DiamondVertices`, and `RoundedRectRadius` helpers.
- `MarkerVertex` (readonly record struct) — `Along` (distance back from the tip) and `Across`
  (perpendicular offset).

### Notation Metrics Key Methods

`double AlongLineLength(EndMarkerStyle style)` — returns the along-line length consumed by an
end-marker decoration; zero for `EndMarkerStyle.None`.

`IReadOnlyList<MarkerVertex> TriangleVertices()` — returns the three triangle vertices in tip-relative
units, shared by the open chevron, hollow triangle, and filled arrow markers. The apex overshoots the
line endpoint by `EndMarkerTipOvershoot`.

`IReadOnlyList<MarkerVertex> DiamondVertices()` — returns the four diamond vertices in tip-relative
units, shared by the hollow and filled diamonds. The far point lands exactly on the line endpoint.

`double RoundedRectRadius(Theme theme)` — returns the theme corner radius scaled by
`RoundedRectCornerFactor`.

### Notation Metrics Design Constraints

- The canonical marker values shall be the historical SVG marker dimensions (triangle 10x7 refX 9,
  diamond 14x8 refX 13, circle radius 4, bar 4x12); every renderer shall derive its markers from
  these constants so the two renderers draw the identical shape.
- Each derived constant shall be documented as a derivation of a named primitive so no geometry
  literal is duplicated.

### Notation Metrics Interactions

`NotationMetrics.RoundedRectRadius` reads `Theme.LineCornerRadius`. The end-marker helpers are called
by both renderers (SVG and PNG systems) and by the layout strategies (*Rendering Layout* system) that
reserve a clean approach using `AlongLineLength`.

## Box Metrics Unit

### Box Metrics Overview

The Box Metrics unit provides the shared formulas that compute a box's title-area height and
folder-tab height from a `Theme`, so that the space the layout strategies reserve equals the space
the renderers draw.

### Box Metrics Data Model

- `BoxMetrics` (static class) — `FolderTabHeight(Theme)` and `TitleAreaHeight(Theme, bool, bool)`.

### Box Metrics Key Methods

`double FolderTabHeight(Theme theme)` — returns `theme.FontSizeBody + 2 * theme.LabelPadding`.

`double TitleAreaHeight(Theme theme, bool hasLabel, bool hasKeyword)` — returns the vertical space
reserved at the top of a box: zero when the box has neither a name nor a keyword; otherwise a leading
padding plus, conditionally, a keyword line and a name line, each followed by a padding.

### Box Metrics Design Constraints

- `TitleAreaHeight` shall reserve no space when a box has neither a name label nor a keyword line.
- Both the layout strategies and the renderers shall compute box title and folder-tab heights from
  these formulas, so reserved space and drawn space always agree.

### Box Metrics Interactions

`BoxMetrics` reads `Theme.FontSizeBody`, `FontSizeTitle`, and `LabelPadding`. It is called by the
renderers (SVG and PNG systems) and by the box layout strategies (*Rendering Layout* system).

## Connector Label Placer Unit

### Connector Label Placer Overview

The Connector Label Placer unit computes non-overlapping screen positions for connector midpoint
labels. Each labelled line prefers the midpoint of its longest segment; when two labels would collide,
the placer falls back to a shorter segment or nudges the label perpendicular to its segment until it
no longer overlaps an already-placed label. Lines are processed in the supplied order so the result is
deterministic, and both renderers share this logic so their label layouts match.

### Connector Label Placer Data Model

- `ConnectorLabelPlacer` (static class) — `Place(IEnumerable<LayoutLine>, double)`.

### Connector Label Placer Key Methods

`IReadOnlyDictionary<LayoutLine, (double X, double Y)> Place(IEnumerable<LayoutLine> lines, double
fontSize)` — returns a chosen label centre for every line that has a `MidpointLabel`. Lines without a
label, and lines with no waypoints, are omitted. The method estimates each label box from `fontSize`,
places the label at the longest clear segment midpoint, and nudges perpendicular to avoid overlap.

### Connector Label Placer Design Constraints

- A line without a `MidpointLabel` shall be omitted from the result.
- A label shall be placed at the midpoint of its line's longest segment unless doing so would overlap
  an already-placed label, in which case it shall be moved to a shorter segment or nudged aside.
- Placement shall be deterministic for a given input order so the SVG and PNG renderers agree.

### Connector Label Placer Interactions

`ConnectorLabelPlacer` reads `LayoutLine.MidpointLabel` and `Waypoints` from the rendering model and
is called by the renderers (SVG and PNG systems) before drawing connector labels.

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Abstractions-Extensibility | The contracts and registries units |
| Rendering-Abstractions-Theming | The `Theme` and `Themes` types |
| Rendering-Abstractions-SharedGeometry | The notation, box, and label geometry units |
| Rendering-Abstractions-Contracts-Algorithm | `ILayoutAlgorithm` |
| Rendering-Abstractions-Contracts-Renderer | `IRenderer` |
| Rendering-Abstractions-Registries-ResolveAlgorithm | `LayoutAlgorithmRegistry.Register` / `Resolve` |
| Rendering-Abstractions-Registries-ResolveRenderer | `RendererRegistry.Register` / `Resolve` |
| Rendering-Abstractions-Registries-MissingThrows | `Resolve` throwing `KeyNotFoundException` |
| Rendering-Abstractions-Theme-ApproachZone | `Theme.ConnectorApproachZone` |
| Rendering-Abstractions-Theme-BuiltInGeometry | `Themes.Light` / `Dark` / `Print` |
| Rendering-Abstractions-NotationMetrics-TriangleGeometry | `NotationMetrics.TriangleVertices` |
| Rendering-Abstractions-NotationMetrics-DiamondGeometry | `NotationMetrics.DiamondVertices` |
| Rendering-Abstractions-NotationMetrics-CircleBarGeometry | `NotationMetrics` circle/bar constants |
| Rendering-Abstractions-NotationMetrics-Crossbar | `NotationMetrics.CrossbarX` |
| Rendering-Abstractions-NotationMetrics-AlongLineLength | `NotationMetrics.AlongLineLength` |
| Rendering-Abstractions-NotationMetrics-BoxDecorations | `NotationMetrics` box-decoration constants |
| Rendering-Abstractions-BoxMetrics-FolderTabHeight | `BoxMetrics.FolderTabHeight` |
| Rendering-Abstractions-BoxMetrics-TitleAreaHeight | `BoxMetrics.TitleAreaHeight` |
| Rendering-Abstractions-ConnectorLabelPlacer-OmitUnlabelled | `ConnectorLabelPlacer.Place` filter |
| Rendering-Abstractions-ConnectorLabelPlacer-LongestSegment | `ConnectorLabelPlacer` segment choice |
| Rendering-Abstractions-ConnectorLabelPlacer-AvoidOverlap | `ConnectorLabelPlacer` overlap avoidance |
