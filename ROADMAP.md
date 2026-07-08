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

**Phase 1 (flat graphs) is implemented** — `CoreOptions.MergeParallelEdges`, the port model
(`ILayoutConnectable`/`LayoutGraphPort`/`LayoutGraphNode.Ports`), a shared per-character
advance-width heuristic (`PortLabelWidthEstimator`, promoted to `Rendering.Abstractions` and made
public so both `Rendering.Layout`'s `LayeredLayoutAlgorithm` and `Rendering.Svg`'s `SvgRenderer`
measure port-label width identically), and
the `LayoutBox.ContentInset*` reserved-margin mechanism described below all exist and are exercised by
the `test/DemaConsulting.Rendering.Gallery` "Parallel edges and named ports" section. Phase 2
(`HierarchyHandling.Recursive` and boundary/delegation ports) remains unimplemented and is tracked
separately below. One deliberate deviation from the phase-1 acceptance criteria as originally
written: a *single* gallery diagram cannot exercise all four port sides at once, because (as
documented under "Named ports" below) a port's rendered side is a purely geometric consequence of
where the layered algorithm's own routing anchors that connector — there is intentionally no
caller-settable `Side` — and under a single flow direction every inter-layer connector anchors on
only one pair of opposite faces (left/right for a rightward/leftward flow, top/bottom for a
downward/upward flow). The gallery therefore ships two companion diagrams,
`ports-showcase-horizontal` (rightward flow; left/right ports, including the long-label
`ContentInsetLeft` case) and `ports-showcase-vertical` (downward flow; top/bottom ports), which
together exercise all four sides.

**`HierarchicalLayoutAlgorithm` bug fix (same-scope port edges, distinct from Phase 2 below).**
`HierarchicalLayoutAlgorithm` (the compound/recursive engine, not the flat `layered` algorithm
itself) previously dropped *every* edge touching a named `LayoutGraphPort` the instant its scope
contained any container node at all, even when the port edge's own endpoints were both literal,
non-nested members of that scope (for example two root-level siblings, with some unrelated
container elsewhere in the same scope) — silently, with no diagnostic. This has been fixed: a
same-scope port edge now reaches the selected leaf algorithm and is routed exactly as it would be in
a flat (container-free) graph. A port edge that genuinely crosses a container boundary — one
endpoint's owning node nested inside a container relative to the scope while the other is not, or an
edge otherwise sharing a box with such an edge — still has no anchoring/routing design and now fails
loudly with `NotSupportedException` instead of being silently dropped, pending the
`HierarchyHandling.Recursive`/boundary-port work described below and in "Phase 2 (hierarchy, once
`HierarchyHandling.Recursive` exists)" further down this file. Implementing actual
boundary-crossing port routing/anchoring remains out of scope for this fix.

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
once per-end names exist, exactly one pair of `LayoutPort`s) per distinct pair, **omitting the
midpoint label (and every duplicate edge's per-end port names) entirely whenever 2+ raw edges
collapse into that one line** — rather than the current behavior of emitting one `LayoutLine` per
original input edge regardless of duplication. A reader cannot tell which of several collapsed
connectors a kept label would have belonged to, so "first surviving edge's label wins" is not an
acceptable substitute for that missing information; only a pair whose `(source, target)` matches
exactly one raw edge keeps its own label/port names. When `CoreOptions.MergeParallelEdges` is
`false`, every input edge keeps its own line and its own two port names, because each now resolves
to its own distinct routed polyline.

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

### Text measurement: `PortLabelWidthEstimator` + `CoreOptions.AssumedFontSize` (superseded — see note)

**Note (post-implementation):** the `ITextMeasurer` interface and Skia-backed measurer described
below were removed after implementation — a materially improved dependency-free heuristic (a
per-character Noto-Sans advance-width table) closed most of the accuracy gap without an
abstraction, interface, or extension point. The narrative below is retained as historical design
rationale for the font-family/measurement-timing reasoning, which still applies.

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

Proposed shape (implemented; `ITextMeasurer` lives in `DemaConsulting.Rendering`, not
`Abstractions` — see note below): **(Superseded — see note above.)**

- Add `ITextMeasurer` to `DemaConsulting.Rendering`:
  `double MeasureWidth(string text, double fontSize, bool bold, bool italic)`. The originally
  proposed home, `Abstractions`, turned out to be backwards: the real project reference graph is
  `Abstractions -> Rendering` (`Abstractions` references `Rendering`, not the reverse), so
  `CoreOptions` (declared in `Rendering`) cannot reference a type declared in `Abstractions`.
  `DemaConsulting.Rendering.Layout` already has a `ProjectReference` on `Rendering`, so placing the
  interface there satisfies the same intent (a dependency-light, Skia-free interface `Layout` can
  reference) without inverting the reference graph.
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
  band (and everything below it) down to make room. When the node *also* has its own title, that
  flat height is widened further (a generous multiple of `AssumedFontSize`/`PortLabelClearance`) so
  the title's own start position — which depends only on `ContentInsetTop`, never on the box's
  total height — clears the top port's rendered label; see "Auto-grow to fit title + port insets"
  below for why growing the box taller alone cannot create that clearance.
- **Bottom ports.** Mirrors top: label sits centered directly *above* the port glyph, inside the
  box, below the last compartment row (or below the lowest child, for a container).
  `ContentInsetBottom` is the same flat, fixed height (with the same widening when the node also
  has a title), and whatever computes the box's lowest occupied content row (or child placement,
  for a container) must stop that far short of the box's bottom edge.
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

No renderer wraps or truncates text by adding line breaks or an ellipsis; `box`/keyword titles and
port labels are instead **squeezed to fit** (SVG `textLength`/`lengthAdjust`, or the Skia
font-size-shrink equivalent) once they exceed their reserved width, rather than drawn at
uncontrolled natural size. For port labels, each side's `LayoutPort.MaxLabelWidth` bounds the
label to roughly half its owning box's inner width, so an excessively long port name can no longer
visually overlap the *opposite* port's label region on the same axis (this squeeze is computed by
`LayeredLayoutAlgorithm` at emission time, since a flat `LayoutPort` has no reference to its owning
box to compute this itself). The auto-grow floor also reconciles this squeeze bound with the
reserved margin above: since `ContentInsetLeft`/`Right` already equal the widest same-side label
width plus `PortLabelClearance`, the floor additionally requires the box's `Width` to be at least
double `ContentInsetLeft` and at least double `ContentInsetRight`, so `MaxLabelWidth` (half the
placed width minus clearance) can never end up smaller than the very label width the inset already
reserved full room for — closing a gap this section originally left latent, where a box that grew
only enough to satisfy the inset (not double it) could still needlessly squeeze a label it had
already made physical space for. A second, independent gap of the same shape existed at
*render* time: `SvgRenderer`'s `FitTextLength` originally decided whether to squeeze a port label
using its own coarse `text.Length * fontSize * 0.6` estimate, which could disagree with the layout
engine's `PortLabelWidthEstimator`-based sizing of `MaxLabelWidth` — so a label the layout engine
had already sized to fit exactly could still be squeezed unnecessarily at render time. Promoting
`PortLabelWidthEstimator` to `Rendering.Abstractions` (shared by both `LayeredLayoutAlgorithm` and
`SvgRenderer`'s port-label call site) closed that gap too, so a port label whose `MaxLabelWidth`
already covers its measured natural width now renders with no `textLength` attribute at all. This
does **not** solve same-face crowding: multiple long labels *on
the same face* can still visually overlap each other, exactly as an excessively long box title or
keyword already could before squeezing existed. Options for that remaining case, roughly in order
of preference:

- **Do nothing beyond what other labels already do (recommended starting point).** Accept that
  same-face crowding can still overflow, consistent with every other bounded-but-not-wrapped label
  in the codebase today; the reserved-margin mechanism above already solves the collision that
  actually motivated this entry (a port label colliding with the box's own title/compartment text),
  and the opposite-port squeeze above solves the cross-face collision. This keeps the feature
  focused and defers a general same-face "text fitting" concern to a separate, dedicated roadmap
  entry.
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

### Auto-grow to fit title + port insets

`LayeredLayoutAlgorithm` never shrinks a caller-supplied box size, but does grow it: after
computing a node's `ContentInset*` values from its aggregated port labels (per "Reserved title/
compartment margins for ports on the top/bottom face" above), it also computes the minimum
width/height actually needed to fit the node's title plus its reserved insets simultaneously
(using `CoreOptions.AssumedFontSize`/`PortLabelClearance`, since `LayeredLayoutAlgorithm` has no
`Theme` dependency to draw exact font metrics from), and emits `max(caller-supplied, computed
minimum)` for both `Width` and `Height`. This runs as a second placement pass — after Pass 1
reveals which nodes are undersized, engine nodes are cloned with the grown sizes and the full
layer packing/spacing pass re-runs, so a grown node never silently overlaps a sibling that was
positioned relative to its smaller Pass-1 footprint. Growing only the box's overall height is not
by itself sufficient to prevent a title/port-label collision — a rendered port label's own
position is independent of box height, so the fix widens the underlying `ContentInsetTop`/
`ContentInsetBottom` values themselves (not just the box height) whenever the node has both a
title and a top/bottom port. Compartment/child content sizing remains entirely caller/child-driven
(unaffected by this floor).

### Auto-grow to fit parallel labeled connectors on the same face

Straight, evenly-spaced parallel connectors between the same two boxes (for example 3 independent
labeled connectors between the same pair, preserved via `CoreOptions.MergeParallelEdges = false`)
need more than just even lane spacing: `ConnectorLabelPlacer`'s midpoint-label placement estimates
each label's own bounding-box extent from `CoreOptions.AssumedFontSize` (and, for width, the label
text itself), and if `PortDistributor`'s lane spacing between adjacent parallel lines is smaller
than that label extent, every label after the first collides with an already-placed label and gets
nudged perpendicular to its own line — visually detaching the label from the connector it names.
`LayeredLayoutAlgorithm`'s auto-grow floor (above) additionally aggregates, per node and per face,
the total connector-anchor count, whether any anchored edge carries a label (unconditionally, not
only for named `LayoutGraphPort` endpoints), and — since it varies by text — the widest labeled
anchor's estimated label width, then widens the minimum-size floor along whichever axis
`PortDistributor` actually spreads that face's anchors along: a `Left`/`Right` face spreads anchors
vertically, so the minimum-**height** floor is widened to match
`ConnectorLabelPlacer.EstimateLabelHeight`; a `Top`/`Bottom` face (a downward- or upward-flowing
diagram) spreads anchors horizontally instead, so the minimum-**width** floor is widened to match
`ConnectorLabelPlacer.EstimateLabelWidth` for the widest labeled anchor's text — both cases matching
`PortDistributor`'s own even-spacing formula on the relevant axis exactly, so `ConnectorLabelPlacer`'s
first-pass (no-nudge) placement succeeds for every label instead, regardless of flow direction. The
gallery's "Parallel edges and named ports" section ships a `parallel-edges-preserved-vertical`
example (a downward-flowing companion to `parallel-edges-preserved`) proving the width-growth path
end to end.

### Port glyph outline for arrowhead contrast

A port glyph (an 8x8 square, filled with `Theme.StrokeColor`) and a connector arrowhead marker
(also solid-filled, no border of its own) can land within a few pixels of each other at a box edge
— for example a connector arriving directly at a named port — and, sharing the same fill color with
no outline of their own, visually merge into a single indistinguishable shape. Both `SvgRenderer`
and `SkiaRasterRenderer` now draw a thin (1.0 logical px, pre-scale) `Theme.BackgroundColor` outline
around the port glyph, keeping it visually distinct from an adjacent arrowhead without touching the
arrowhead's own rendering at all.

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
  self-contained function. **(Superseded — see note above.)**
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

**Phase 1 (flat graphs) — implemented:**

- A new `test/DemaConsulting.Rendering.Gallery` example showing two boxes joined by **three or
  more** distinct parallel connectors (mirroring the SysML2Tools wiring-diagram scenario that
  motivated this entry), rendered with `CoreOptions.MergeParallelEdges` set to `false` to preserve
  them — each connector must appear as its own visually distinct routed line, not
  stacked/overlapping copies of the same route. A companion case with `MergeParallelEdges` left at
  its default (`true`) must show exactly one line per duplicate pair, with the midpoint label
  omitted entirely (never a "first surviving edge's label" substitute) whenever 2+ raw edges
  collapse into that one line. Shipped as `parallel-edges-preserved`/`parallel-edges-merged`.
- Each connector's port must carry its own rendered `ExternalLabel`, distinct from the connector's
  own separate midpoint label — both must be able to be set and rendered simultaneously.
- The gallery entry must exercise ports on **all four sides**, each labeled inside the box next to
  its port glyph per the "Port label placement per side" decision above, with at least one long
  left/right port label demonstrating that the box's own title/compartment content is pushed in by
  the auto-computed `ContentInset*` margin rather than colliding with it. Shipped as the companion
  pair `ports-showcase-horizontal` (left/right, including the long-label case) and
  `ports-showcase-vertical` (top/bottom) — see the status note at the top of this section for why a
  single diagram cannot exercise all four sides at once.
- The gallery entry must include at least one box with a long port label and confirm the SVG output
  of the same `LayoutTree` still reads correctly, verifying the single-layout/multi-renderer flow
  (`LayoutTree tree = LayoutEngine.Layout(graph);` reused across both renderers) is honored. Both
  `ports-showcase-*` diagrams render to SVG using the built-in estimator with no configuration.

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
