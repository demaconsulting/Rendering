# Connector Label Placer Unit Design

Part of the [Rendering Abstractions](rendering-abstractions.md) system.

## Connector Label Placer Overview

The Connector Label Placer unit computes non-overlapping screen positions for connector midpoint
labels. Each labelled line prefers the midpoint of its longest segment; when two labels would collide,
the placer falls back to a shorter segment or nudges the label perpendicular to its segment until it no
longer overlaps an already-placed label. Lines are processed in the supplied order so the result is
deterministic, and both renderers share this logic so their label layouts match.

## Connector Label Placer Data Model

- `ConnectorLabelPlacer` (static class) — `Place(IEnumerable<LayoutLine>, double)`.

## Connector Label Placer Key Methods

`IReadOnlyDictionary<LayoutLine, (double X, double Y)> Place(IEnumerable<LayoutLine> lines, double
fontSize)` — returns a chosen label centre for every line that has a `MidpointLabel`. Lines without a
label, and lines with no waypoints, are omitted. The method estimates each label box from `fontSize`,
places the label at the longest clear segment midpoint, and nudges perpendicular to avoid overlap.

## Connector Label Placer Design Constraints

- A line without a `MidpointLabel` shall be omitted from the result.
- A label shall be placed at the midpoint of its line's longest segment unless doing so would overlap
  an already-placed label, in which case it shall be moved to a shorter segment or nudged aside.
- Placement shall be deterministic for a given input order so the SVG and PNG renderers agree.

## Connector Label Placer Interactions

`ConnectorLabelPlacer` reads `LayoutLine.MidpointLabel` and `Waypoints` from the rendering model and
is called by the renderers (SVG and PNG systems) before drawing connector labels.

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Abstractions-ConnectorLabelPlacer-OmitUnlabelled | `ConnectorLabelPlacer.Place` filtering unlabelled lines |
| Rendering-Abstractions-ConnectorLabelPlacer-LongestSegment | `ConnectorLabelPlacer.Place` segment choice |
| Rendering-Abstractions-ConnectorLabelPlacer-AvoidOverlap | `ConnectorLabelPlacer.Place` overlap avoidance |
