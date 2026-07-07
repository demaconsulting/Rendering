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

## Parallel-edge (multi-edge) support in the layered algorithm

The bundled `layered` algorithm's layout pipeline currently treats the input graph as simple:
when two or more edges share the same directed `(source, target)` node pair, only one is
retained and routed, and every input edge sharing that pair resolves to the same single routed
polyline. This is invisible for typical flow/dependency diagrams (where a duplicate edge between
the same two nodes is rare and usually redundant), but it silently drops information for
consumers modeling physical wiring/interconnection diagrams, where two nodes are legitimately
joined by several distinct connectors (e.g. a power line and a separate signal line between the
same two components) that must each be shown as its own labeled, individually routed line.

Confirmed collapse points (both must change together for a fix, since either alone still
collapses the result):

- **`Engine/Layered/CycleBreaker.BreakCycles`** — the `seen.Add((from, to))` `HashSet<(int,int)>`
  check discards any edge whose `(source, target)` pair (after back-edge reversal) was already
  seen, before layering, port distribution, or corridor routing ever run. Only the first edge of
  each duplicate pair survives into `graph.Acyclic`.
- **`LayeredLayoutAlgorithm`'s final line-emission step** — rebuilds a
  `Dictionary<(int Source, int Target), IReadOnlyList<Point2D>> routes` keyed by node pair from
  the acyclic edge set, then looks up each of the caller's original input edges by that same key.
  Even if `CycleBreaker` were changed to preserve duplicate pairs through to routing, this
  dictionary can hold only one polyline per key, so every input edge sharing a pair would still
  resolve to the same last-written route.

### Proposed shape of the fix

Add a new opt-in `CoreOptions` property (for example `CoreOptions.MergeParallelEdges`, defaulting
to `true` to preserve today's behavior for existing consumers such as
`ActionFlowViewLayoutStrategy`/`StateTransitionViewLayoutStrategy`) that a caller can set to
`false` on the graph (or a container scope) to request that parallel edges be preserved end to
end:

- `CycleBreaker` reads the option; when `false`, it must still classify/reverse back edges (cycle
  detection is unaffected by multiplicity) but must retain every edge instance rather than
  deduplicating by `(from, to)`.
- Downstream stages that already operate per-edge-index (`PortDistributor`, `LongEdgeJoiner`,
  `LayeredCorridorRouter`) should require little to no change — they already distribute a distinct
  port slot and corridor `RoutingSlot` per augmented edge; they simply never currently receive more
  than one augmented edge per node pair because `CycleBreaker` filters the rest out first.
- `LayeredLayoutAlgorithm`'s final line-emission must key its route lookup by edge identity (index
  into the acyclic edge list) rather than by `(source, target)` node pair when parallel edges are
  preserved, so each of the caller's input edges recovers its own distinct routed polyline instead
  of colliding on a shared dictionary key.

This is tracked here because a real consumer (SysML2Tools' `InterconnectionViewLayoutStrategy`,
rendering `connect`/`connection` usages between the same two parts, e.g. power/encoder/sensor
wiring between a motor and a controller) needs to always disable merging for its diagrams — every
physical connector in the source model must appear as its own line with its own labeled ports,
never silently coalesced with another connector between the same two parts.

### Merged-edge label suppression (and a pre-existing latent bug this surfaces)

When `CoreOptions.MergeParallelEdges` is `true` (the default), only one physical line is ever
drawn for a duplicate `(source, target)` pair — so it would be actively misleading to still emit
several connectors' distinct per-end port names (or several different midpoint labels) stacked on
top of that single line; a reader cannot tell which name belongs to which underlying model
connector when only one line is visible. **When merging is enabled, per-edge port names (and the
connector's own midpoint label) for every duplicate edge past the first must not be emitted at
all**, not merely hidden by the renderer — there is nothing meaningful to attach them to once the
edges have been collapsed to one drawn line.

Confirming this surfaced a **pre-existing latent bug independent of this feature**: today's final
line-emission loop in `LayeredLayoutAlgorithm` (`foreach (var edge in graph.Edges)`) iterates every
*original* input edge, not the deduplicated acyclic set — so if a caller already has duplicate
edges between the same two nodes today (before any of this work), all of them get their own
`LayoutLine` with identical waypoints (since they all resolve to the same routed polyline via the
`(source, target)` dictionary) but each keeps its own `edge.Label`, stacking multiple midpoint
labels on top of each other at the same spot. Only `CycleBreaker` dedupes the *route*; nothing
dedupes the *emitted line/label count* today.

The fix for both is the same change: when `CoreOptions.MergeParallelEdges` is `true`, the final
line-emission loop must also dedupe by `(source, target)` — emitting exactly one `LayoutLine` (and,
once per-end names exist, exactly one pair of `LayoutPort`s) per distinct pair, using the first
surviving edge's label/port names and discarding the rest — rather than the current behavior of
emitting one `LayoutLine` per original input edge regardless of duplication. When
`CoreOptions.MergeParallelEdges` is `false`, every input edge keeps its own line and its own two
port names, because each now resolves to its own distinct routed polyline.

### Named ports: a first-class port object, modeled after ELK's `ElkPort`

Each connector endpoint needs its own name, independent of the connector's existing single
midpoint label (`LayoutGraphEdge.Label` / `LayoutLine.MidpointLabel`), which stays as-is for a
connector-level name. Two real scenarios drove the final shape of this:

- A **fan-out port** (several edges from/to the same node sharing one named connection point)
  means the name cannot live on any one edge — if it did, every edge referencing that port would
  have to repeat the identical string, with nothing to stop them disagreeing.
- A **boundary/delegation port** on a container (a physical bulkhead-connector pattern: one
  connector runs from outside the box to the port, a different connector runs from the port to a
  child node *inside* the box) means the port can carry **two independent names**, one per face,
  not one shared name.

Both point at the same conclusion: **the name belongs to the port, not to the edge.** This
mirrors the Eclipse Layout Kernel (ELK), which was checked directly against this design:

- ELK has a first-class `ElkPort` type. A port is created once, added to its owning `ElkNode`'s
  own `ports` collection, and both `ElkEdge`s that touch it reference **that same object** as
  their source/target — via `ElkConnectableShape`, a shared interface implemented by both
  `ElkNode` and `ElkPort`. Edges attach to ports by object identity, not by a string id that
  happens to match across separately-declared edges.
- ELK's `ElkPort` (like every `ElkGraphElement`) carries a generic `labels` collection, with a
  separate `portLabels.placement` layout option (`INSIDE`/`OUTSIDE`/etc.) controlling where a
  label draws relative to the box. ELK does **not** have a first-class "different name per face"
  concept — a shared port normally carries one label, and hierarchy-crossing edges are typically
  modeled as two edges attached to the same port (an ELK "hierarchical port dummy" pattern) with
  no separate external/internal name. Our `ExternalLabel`/`InternalLabel` pair is therefore a
  **deliberate, bespoke extension beyond ELK's baseline**, purpose-built for the bulkhead-connector
  scenario — not something we get "for free" from ELK compatibility.

Proposed model surface (`DemaConsulting.Rendering`):

- `ILayoutConnectable` — a marker interface implemented by both `LayoutGraphNode` and the new
  port type, mirroring `ElkConnectableShape`. `LayoutGraphEdge.Source`/`Target` widen from
  `LayoutGraphNode` to `ILayoutConnectable` so an edge can terminate at either.
- `LayoutGraphPort`: `Id` (unique within its owning node's ports) plus `ExternalLabel` and
  `InternalLabel` (both optional independently). **No `Side` property** — placement is left
  entirely to the engine, the same way `PortSide` is already only ever a *computed* value on
  today's `LayoutPort` output, never a caller input. Explicitly deferred: this repo does not
  currently give a caller any control over layer assignment or left-right ordering
  (`CrossingMinimizer`/`BrandesKopfPlacer` decide that), so a caller-declared side would be a
  promise the engine cannot keep. Revisit only if/when block-placement control is added as its
  own separate feature.
- `LayoutGraphNode.Ports` — a lazily-allocated collection, mirroring the existing `Children`
  pattern (a leaf node with no ports allocates nothing).
- Any number of edges may reference the same `LayoutGraphPort` instance as an endpoint; those
  added within the port's owning node's own `Children` scope are classified "internal" (their
  label, if any, comes from `InternalLabel`), and those added in an ancestor scope are classified
  "external" (`ExternalLabel`) — the classification is structural (which scope declared the edge),
  not a flag the caller sets. **First cut scope: support at most one internal and one external
  edge per port;** true fan-out (multiple edges sharing one port on the same face) is deferred as
  a later generalization once the simple case is proven.
- `LayeredLayoutAlgorithm` needs new logic to: (a) recognize when two or more edges share a
  `LayoutGraphPort` reference and resolve them to one shared anchor coordinate, and (b) emit a
  `LayoutPort` (the existing `LayoutTree` record — never constructed by any algorithm today; only
  renderers and their tests build one directly) at that coordinate carrying the resolved label for
  each face present.

Example (illustrative only; `ILayoutConnectable`/`LayoutGraphPort`/`Ports` do not exist yet):

```csharp
var graph = new LayoutGraph();
var containerA = graph.AddNode("A", 160, 100);
var containerB = graph.AddNode("B", 200, 140);
var containerC = containerB.Children.AddNode("C", 80, 50);

// The port owns its own identity and both face labels — not the edges attached to it.
var portP1 = containerB.Ports.AddPort("p1");
portP1.ExternalLabel = "PWR_OUT";
portP1.InternalLabel = "PWR_IN";

// External edge: added at the root scope (A and B are peers there).
graph.AddEdge("a-to-b", containerA, portP1);

// Internal (delegation) edge: added inside B's own Children scope, referencing B's own port.
containerB.Children.AddEdge("b-to-c", portP1, containerC);
```

**This also surfaces a real prerequisite gap, independent of ports:** a boundary/delegation edge
like `b-to-c` above only makes sense if the layered pipeline can actually lay out and route across
containment levels — but `Engine/Layered/HierarchyHandling` today has only `Flat` exercised;
`Recursive` (laying out each container's children bottom-up, routing cross-container edges) is
unimplemented scaffolding. **`HierarchyHandling.Recursive` needs to actually be implemented before
boundary ports can be routed at all** — this is a real, separate, and likely larger prerequisite
uncovered by this design discussion, not a detail of the port model itself.

### Text measurement: `ITextMeasurer` + `CoreOptions.AssumedFontSize`

A single `LayoutTree` is computed once and reused across renderers (`LayoutTree tree =
LayoutEngine.Layout(graph);` then passed to both the SVG and Skia renderers), so any text-aware
spacing decision has to be made once, at layout time, inside `Apply` — not per renderer. Two facts
make this workable without breaking the Layout unit's current zero-rendering-dependency design:

- **Font family is already global, not per-render.** `Theme`'s own doc comment states it directly:
  "Font choice is not part of the theme; each renderer hardcodes its own typeface internally" —
  both `SvgRenderer` and `SkiaRasterRenderer` hardcode Noto Sans. There is exactly one font this
  library ever uses, so a measurement taken once at layout time cannot go stale from a renderer
  using a different font later.
- **`DemaConsulting.Rendering.Skia` already embeds the real Noto Sans font files**
  (`NotoSans-Regular/Bold/Italic/BoldItalic.ttf`) and already calls `SKFont.MeasureText` elsewhere
  (title auto-fit, badge label sizing) — so a measurer backed by it is exact for Skia's own raster
  output and a good-faith estimate for SVG output (which targets the same nominal font family).

Proposed shape:

- Add `ITextMeasurer` to `Abstractions` (which `Layout` already depends on):
  `double MeasureWidth(string text, double fontSize, bool bold, bool italic)`. `Layout` depends
  only on the interface, never on SkiaSharp, preserving today's dependency direction and keeping
  pure-SVG consumers free of the native Skia dependency.
- `DemaConsulting.Rendering.Skia` ships a ready-made implementation backed by its already-embedded
  Noto Sans typefaces and real `SKFont.MeasureText`.
- Add `CoreOptions.TextMeasurer` (optional; cascades like every other option) so a caller wires it
  in once. If unset, `LayeredLayoutAlgorithm` falls back to a small, dependency-free heuristic
  (an average-advance-width-per-character estimate) so every caller gets automatic behavior with
  zero required setup, even one that never references Skia at all.
- Add `CoreOptions.AssumedFontSize` (default `12.0`, matching the bundled themes' `FontSizeBody`)
  because `Theme.FontSizeBody` is chosen per render call, not at layout time, and the measurer
  needs a font size up front. **Caveat, not a new risk:** if a later `Render` call uses a theme
  whose `FontSizeBody` differs materially from the assumed size, the reserved margin could be too
  small or unnecessarily large — but this is the same pre-existing category of risk that already
  applies to every caller-chosen `Width`/`Height` today (nothing stops a mismatched theme from
  overflowing a manually-sized box now either); it is not a new failure mode this feature
  introduces.

### Port label placement per side

With a measurer available at layout time, all four sides place their label the same conceptual
way — **inside the box, immediately next to the port glyph, reading inward** — informed by a real
SysML2Tools 3-axis-gantry hierarchy-view diagram (showing every descendant part and connector)
provided during this session, which labels ports on both the top/bottom and left/right faces of
the same boxes. What differs per side is *what the reserved margin is measured from*, and
correspondingly how much it pushes the box's own title/compartment content inward:

- **Left ports.** Label reads rightward from the port glyph on the left edge. `LayeredLayoutAlgorithm`
  computes `ContentInsetLeft` as the widest left-side port label's measured width (via
  `ITextMeasurer`/`AssumedFontSize`) plus a small clearance, and the box's title/compartment
  rendering start shifts right by that amount — replacing today's fixed
  `box.X + Theme.LabelPadding` start with `box.X + Theme.LabelPadding + ContentInsetLeft`. Without
  this push-in, a left-port label would collide almost immediately with compartment text, which
  starts only `LabelPadding` (6–8 px) from the left edge today.
- **Right ports.** Mirrors left: label reads leftward from the port glyph on the right edge, and
  `ContentInsetRight` (measured the same way) narrows the box's available content width from the
  right. Compartment text is not wrapped or clipped today (confirmed: neither renderer wraps or
  truncates any text), so this reservation is a best-effort margin, not a hard guarantee against an
  already-overflowing compartment line — consistent with the existing overflow behavior of every
  other label in the codebase, not a regression introduced here.
- **Top ports.** Label sits centered directly *under* the port glyph, inside the box, above the
  title text. `ContentInsetTop` is a **flat, fixed height** (one text line at `AssumedFontSize`
  plus padding) — not a measured width — because a single line's height does not depend on the
  label's text content, so this needs no per-label measurement at all. `ResolveTitleAreaTop`'s
  result shifts down by `ContentInsetTop` when the node has any top-side port, pushing the title
  band (and everything below it) down to make room.
- **Bottom ports.** Mirrors top: label sits centered directly *above* the port glyph, inside the
  box, below the last compartment row (or below the lowest child, for a container).
  `ContentInsetBottom` is the same flat, fixed height, and whatever computes the box's lowest
  occupied content row (or child placement, for a container) must stop that far short of the box's
  bottom edge.
- Multiple ports on the same face are still arranged left to right in port order within whatever
  space is available; horizontal crowding among labels *on the same face* (many ports, or long
  names collectively wider than the box) is accepted as overflow — see "Long port names" below —
  but the box's own title/compartment content no longer collides with a port label on its own face,
  which is the actual goal of the reserved-margin mechanism.
- This is genuinely new model surface: `LayoutBox` needs the four `ContentInset*` values (or
  equivalent) so `SvgRenderer`/`SkiaRasterRenderer` know where title/compartment rendering may
  start and must stop, auto-computed by `LayeredLayoutAlgorithm` rather than caller-supplied —
  directly answering "let the rendering library have the smarts, not the caller."
- For a **boundary port** (a `LayoutGraphPort` with both an external and an internal edge — see
  "Named ports" above), the two labels are drawn on opposite faces of the same anchor point:
  `ExternalLabel` renders reading outward on whichever side faces the external edge, and
  `InternalLabel` renders reading inward, on the interior side of that same boundary line —
  contributing to `ContentInset*` from the *inside* face only, since the exterior face has no
  sibling content to push inward. A plain (non-container) node's port only ever has an external
  edge, so it degenerates to the simple single-label case described above automatically.

### Long port names

Even with measured reservations, no renderer wraps or truncates *any* text today — every label
(box title, keyword, midpoint label, port label) is drawn at its natural size. This means an
excessively long port name is protected from colliding with the *box's own* content (per the
reserved-margin mechanism above), but can still visually overlap an *adjacent port's* label on the
same face, or run past the box/connector, exactly as an excessively long box title or keyword
already can today. Options, roughly in order of preference:

- **Do nothing beyond what other labels already do (recommended starting point).** Accept that
  same-face crowding can still overflow, consistent with every other unbounded label in the
  codebase today; the reserved-margin mechanism above already solves the collision that actually
  motivated this entry (a port label colliding with the box's own title/compartment text). This
  keeps the initial feature focused and defers a general same-face "text fitting" concern to a
  separate, dedicated roadmap entry.
- **Widen port spacing to fit the widest label on a face.** `PortDistributor` would space ports
  apart using `ITextMeasurer`-measured widths rather than the fixed `EdgeSpacing` constant — a
  natural extension of the same measurer this entry already introduces, so no new architectural
  concern, just more surface area to change for a first cut of this feature.
- **Truncate or add an ellipsis beyond a configurable max length or pixel width.** Simplest
  renderer-only change (no layout impact) if `ITextMeasurer` is available, but loses information
  silently and needs a policy decision (character count vs. measured width).

Whichever is chosen, it is layered on top of the port-naming and reserved-margin work above, not a
blocker for it — the initial implementation can ship with the "do nothing beyond existing
behavior" option (same-face crowding only) and revisit if real SysML2Tools port names turn out to
crowd a single face in practice.

### Approximate complexity

Rough sizing, assuming the "do nothing beyond what other labels already do" choice above for
same-face crowding (no port-spacing-by-width work):

- **`CoreOptions.MergeParallelEdges` + `CycleBreaker` deduplication bypass + route-lookup-by-index fix**
  (the parallel-edge mechanics) — **small**. Isolated to two files, no new model surface, and the
  downstream stages were already verified to need no change. Comparable in size to the recently
  landed `CoreOptions.NodeSpacing` work.
- **Port objects** (`ILayoutConnectable`, `LayoutGraphPort` with `Id`/`ExternalLabel`/`InternalLabel`,
  `LayoutGraphNode.Ports`, widening `LayoutGraphEdge.Source`/`Target`) — **medium**. New public
  model surface (its own reqstream requirements and design-doc updates) and new algorithm-side
  logic to correlate multiple edges sharing one port object into a single anchor and emit
  `LayoutPort` per face, but no new placement math — it reuses port coordinates the pipeline
  already computes today.
- **`HierarchyHandling.Recursive` implementation** (actually laying out and routing across
  containment levels; today only `Flat` is exercised) — **large**, and a genuine prerequisite for
  boundary/delegation ports specifically, not an optional nice-to-have. This is scoped separately
  from the rest of this entry: a non-hierarchical (flat-graph) subset of parallel edges + named
  ports + measured margins can ship independently and is useful on its own; boundary ports cannot
  ship until this lands.
- **`ITextMeasurer` + `CoreOptions.TextMeasurer`/`AssumedFontSize`** (new interface in
  `Abstractions`, Skia-backed implementation reusing its already-embedded Noto Sans typefaces and
  `SKFont.MeasureText`, dependency-free heuristic fallback in `Layout`) — **small-to-medium**. No
  new native dependency for `Layout` (interface-only), and the Skia side reuses font resources and
  measurement calls that already exist for other purposes; the heuristic fallback is a small,
  self-contained function.
- **Reserved-margin computation** (`ContentInsetLeft/Right/Top/Bottom` on `LayoutBox`, auto-computed
  by `LayeredLayoutAlgorithm` from the measurer for left/right and a flat constant for top/bottom;
  `SvgRenderer`/`SkiaRasterRenderer` reading these insets instead of the current fixed
  `box.X + LabelPadding`/`ResolveTitleAreaTop` assumptions) — **medium**. Touches both renderers
  (title/compartment start position, and for a container, where its lowest child/compartment must
  stop), plus new `LayoutBox` model surface with its own reqstream/design-doc updates. Top/bottom
  is the simpler half (flat constant, no per-label measurement); left/right depends on the
  measurer above being in place first.
- **Gallery example + tests** — **small**, following the same pattern as every other bundled
  gallery/test addition in this repo.
- **Combined** — **large overall, and best split into two independently-shippable phases.**
  Phase 1 (flat graphs only: `MergeParallelEdges`, port objects with only `ExternalLabel` in play,
  the measurer, and reserved margins) is comparable in shape to the recently landed
  `CoreOptions.NodeSpacing` work plus one cross-cutting concern (new `Abstractions` interface,
  changes to both renderers, new `LayoutBox` fields) — doable as a multi-session but bounded
  effort. Phase 2 (`HierarchyHandling.Recursive`, unlocking boundary ports with both
  `ExternalLabel` and `InternalLabel`) is a substantially larger, separate effort and should not
  block phase 1 from shipping.

### Acceptance criteria

**Phase 1 (flat graphs):**

- A new `test/DemaConsulting.Rendering.Gallery` example showing two boxes joined by **three or
  more** distinct parallel connectors (mirroring the SysML2Tools wiring-diagram scenario that
  motivated this entry), rendered with `CoreOptions.MergeParallelEdges` set to `false` to preserve
  them — each connector must appear as its own visually distinct routed line, not
  stacked/overlapping copies of the same route. A companion case with `MergeParallelEdges` left at
  its default (`true`) must show exactly one line and one label surviving per duplicate pair.
- Each connector's port must carry its own rendered `ExternalLabel`, distinct from the connector's
  own separate midpoint label — both must be able to be set and rendered simultaneously.
- The gallery entry must exercise ports on **all four sides**, each labeled inside the box next to
  its port glyph per the "Port label placement per side" decision above, with at least one long
  left/right port label demonstrating that the box's own title/compartment content is pushed in by
  the auto-computed `ContentInset*` margin rather than colliding with it.
- The gallery entry must include at least one box using the Skia-backed `ITextMeasurer` (exact
  measurement) and confirm the SVG output of the same `LayoutTree` still reads correctly, verifying
  the single-layout/multi-renderer flow (`LayoutTree tree = LayoutEngine.Layout(graph);` reused
  across both renderers) is honored.

**Phase 2 (hierarchy, once `HierarchyHandling.Recursive` exists):**

- A gallery example with a container node holding a nested child, and a `LayoutGraphPort` on the
  container referenced by one external edge (from an outside sibling) and one internal/delegation
  edge (to the nested child) — both `ExternalLabel` and `InternalLabel` must render, on the
  outward and inward faces of the same boundary point, respectively.

- The gallery entry must be wired into `GalleryCatalog`/`GalleryIndex` like existing examples, so
  it renders via `gallery.ps1` and appears in `docs/gallery/README.md`.

## Process note

When a future session identifies further deferred/advisory work, add it here rather than letting
it live only in conversation history or scattered XML doc comments, so it survives across sessions.
