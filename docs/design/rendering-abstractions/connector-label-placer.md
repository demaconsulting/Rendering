## Connector Label Placer Unit Design

Part of the Rendering Abstractions system.

### Connector Label Placer Purpose

The Connector Label Placer unit computes non-overlapping screen positions for connector midpoint
labels. Each labelled line prefers the midpoint of its longest segment; when two labels would collide,
the placer falls back to a shorter segment or nudges the label perpendicular to its segment until it no
longer overlaps an already-placed label. Lines are processed in the supplied order so the result is
deterministic, and both renderers share this logic so their label layouts match.

### Connector Label Placer Data Model

- `ConnectorLabelPlacer` (static class) — `Place(IEnumerable<LayoutLine>, double)`.
- `LabelPlacement` (public `readonly record struct`) — `(double X, double Y, double HalfWidth,
  double HalfHeight)`. Carries the chosen label centre plus that label's estimated half-width/
  half-height, so a caller can compute the label's full rendered bounding box without
  re-estimating it. Exposes an explicit 2-argument `Deconstruct(out double x, out double y)`
  alongside its synthesized 4-argument member-wise `Deconstruct`, so existing
  `var (x, y) = result[line];` call sites keep compiling unchanged (C# resolves `Deconstruct`
  overloads by arity) while new callers can deconstruct or member-access all four values.

### Connector Label Placer Key Methods

`IReadOnlyDictionary<LayoutLine, LabelPlacement> Place(IEnumerable<LayoutLine> lines, double
fontSize)` — returns a chosen label placement (centre plus half-extent) for every line that has a
`MidpointLabel`. Lines without a label, and lines with no waypoints, are omitted. The method
estimates each label box from `fontSize`, places the label at the longest clear segment midpoint,
and nudges perpendicular to avoid overlap. Exposing each placement's half-width/half-height (not
just its centre) lets a caller compute the label's full bounding box after every nudge/fallback
has been applied, which is what a renderer needs to grow its canvas/bitmap bounds to include every
placed label — the final canvas size was previously computed from box+routing geometry before this
method ran, so a nudged label could land completely outside those bounds and render invisibly
clipped; a renderer must instead finalize its canvas size only after calling `Place`.

### Connector Label Placer Error Handling

`Place` validates its `lines` argument with `ArgumentNullException.ThrowIfNull` and propagates the
resulting `ArgumentNullException` to the caller. All other input shapes are treated as normal cases
rather than errors: a line whose `MidpointLabel` is `null` is omitted from the result, a line whose
`Waypoints` collection is empty is likewise omitted, and a line with a single waypoint yields the
degenerate midpoint of that waypoint. The overlap-avoidance search is bounded — after exhausting the
segment fallback and a fixed number of perpendicular nudges, the label is dropped just beneath every
already-placed label, guaranteeing no overlap even when the bounded nudges above are exhausted (for
example where many connectors cross at a single point). The method has no side effects on its inputs
and performs no logging.

### Connector Label Placer Dependencies

- **Rendering Model system (`DemaConsulting.Rendering`)** — reads `LayoutLine.MidpointLabel` and
  `LayoutLine.Waypoints` from the layout tree types.
- **.NET base class library** — `System.Collections.Generic.IEnumerable<T>`,
  `IReadOnlyDictionary<TKey, TValue>`, and standard geometry arithmetic. No third-party runtime
  packages are consumed.

### Connector Label Placer Callers

- **Rendering.Svg `SvgRenderer` unit** — calls `Place` once per render pass to compute the label
  positions for the connector labels it writes as `<text>` elements.
- **Rendering.Skia raster renderers (`SkiaRasterRenderer`, `PngRenderer`, `JpegRenderer`,
  `WebpRenderer`)** — call `Place` for the same purpose so that the SVG and raster outputs agree on
  label positions.

### Connector Label Placer Design Constraints

- A line without a `MidpointLabel` shall be omitted from the result.
- A label shall be placed at the midpoint of its line's longest segment unless doing so would overlap
  an already-placed label, in which case it shall be moved to a shorter segment or nudged aside.
- Placement shall be deterministic for a given input order so the SVG and PNG renderers agree.

### Connector Label Placer Interactions

`ConnectorLabelPlacer` reads `LayoutLine.MidpointLabel` and `Waypoints` from the rendering model and
is called by the renderers (SVG and PNG systems) before drawing connector labels.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Abstractions-ConnectorLabelPlacer-OmitUnlabelled | `ConnectorLabelPlacer.Place` filtering unlabelled lines |
| Rendering-Abstractions-ConnectorLabelPlacer-LongestSegment | `ConnectorLabelPlacer.Place` segment choice |
| Rendering-Abstractions-ConnectorLabelPlacer-AvoidOverlap | `ConnectorLabelPlacer.Place` overlap avoidance |
| Rendering-Abstractions-ConnectorLabelPlacer-ExposesLabelExtent | `LabelPlacement.HalfWidth`/`HalfHeight` in `Place` |
