# Roadmap

This file tracks well-known options and behaviors that are accepted by the API today but not
yet honored by any bundled algorithm or renderer, so they are not forgotten between sessions.
It is not part of the reference template's structure — it is an additive, repo-local tracking
aid — and is not itself a compliance artifact (no reqstream/design/verification obligations
attach to entries here until they are actually implemented).

## Unimplemented `CoreOptions` behavior

All six `CoreOptions` properties now cascade correctly through the layout hierarchy (nearest-ancestor
inheritance via `PropertyHolder.OverlayOnto`, landed alongside this file). The following properties are
accepted and cascade correctly, but are not yet *read* by any bundled algorithm's layout logic:

- **`CoreOptions.HierarchyHandling`** — only `HierarchyHandling.SeparateChildren` exists and is
  implicitly what the bundled `HierarchicalLayoutAlgorithm` does; no algorithm branches on this
  property's value. Additional modes (e.g. an ELK-style "include children in the parent's own
  layout space" mode) are not implemented.
- **`CoreOptions.NodeSpacing`** — advisory; the bundled `layered` algorithm uses fixed engine
  metrics instead of this value.
- **`CoreOptions.LayerSpacing`** — advisory; same as `NodeSpacing`, fixed engine metrics are used
  instead.

Because these properties are ordinary `LayoutProperty<T>` values stored via `PropertyHolder`, the
generalized cascading mechanism already applies to them automatically — implementing any of the
above only requires an algorithm to start reading the (already-cascaded) effective value; no new
option-resolution plumbing is required.

## Process note

When a future session identifies further deferred/advisory work, add it here rather than letting
it live only in conversation history or scattered XML doc comments, so it survives across sessions.
