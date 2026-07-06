# Roadmap

This file tracks well-known options and behaviors that are accepted by the API today but not
yet honored by any bundled algorithm or renderer, so they are not forgotten between sessions.
It is not part of the reference template's structure — it is an additive, repo-local tracking
aid — and is not itself a compliance artifact (no reqstream/design/verification obligations
attach to entries here until they are actually implemented).

## Unimplemented `CoreOptions` behavior

All six `CoreOptions` properties now cascade correctly through the layout hierarchy (nearest-ancestor
inheritance via `PropertyHolder.OverlayOnto`, landed alongside this file). `CoreOptions.NodeSpacing` is
now honored by the bundled `layered` algorithm (see below). The following properties are still accepted
and cascade correctly, but are not yet *read* by any bundled algorithm's layout logic. Effort below is
ranked from easiest to hardest — it is not uniform across the two, despite both sharing the same
already-working cascading mechanism:

- **`CoreOptions.LayerSpacing`** (needs a design decision first) — advisory; the bundled `layered`
  algorithm uses a fixed engine constant (`LayeredLayoutMetrics.CorridorMinWidth`, 70.0) instead. Unlike
  `NodeSpacing`, there is no clean 1:1 replacement: `CorridorMinWidth` is *derived* from connector-routing
  slot math (`2×ConnectorClearance + (edgeCount−1)×EdgeSpacing`), not a free-standing spacing value. A
  user-supplied `LayerSpacing` smaller than what routing needs must not silently override the
  routing-derived minimum, or connectors will overlap — implementation must decide whether the option
  acts as an additional floor on top of the routing minimum, a validated hard override, or something
  else, before any code is written.
- **`CoreOptions.HierarchyHandling`** (hardest) — only `HierarchyHandling.SeparateChildren` exists and
  is implicitly what the bundled `HierarchicalLayoutAlgorithm` does; no algorithm branches on this
  property's value, and no second mode exists yet. Implementing an additional mode (e.g. an ELK-style
  "include children in the parent's own layout space" mode) is genuine new layout logic in
  `HierarchicalLayoutAlgorithm` — not a matter of reading an already-cascaded value — and introduces new
  user-visible layout behavior, so it belongs in a proper planning/implementation pass rather than an
  opportunistic fix.

## Process note

When a future session identifies further deferred/advisory work, add it here rather than letting
it live only in conversation history or scattered XML doc comments, so it survives across sessions.
