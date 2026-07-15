### EdgeCountGapWidener Unit Design

Part of the Rendering Layout system.

#### EdgeCountGapWidener Purpose

`EdgeCountGapWidener` widens a single node-to-node gap so a fan of parallel connectors routed
through it has room to spread into distinct orthogonal lanes instead of being crushed into one
narrow channel. It gives the containment packer and the hierarchical algorithm's post-placement
sibling-gap-widening pass the *same* corridor-width math the layered pipeline's `BrandesKopfPlacer`
already uses between adjacent columns, so all three widening rules stay byte-identical instead of
drifting apart through copy-paste.

#### EdgeCountGapWidener Data Model

`EdgeCountGapWidener` is an internal static class with no instance state. It exposes a single pure
function of two inputs — a `baseGap` (`double`) and an `edgeCount` (`int`) — and returns the widened
gap as a `double`. It has no fields, no constructors, and no mutable state, so it is safe for
concurrent use.

#### EdgeCountGapWidener Methods

`Widen(baseGap, edgeCount)` computes the corridor width a fan of `edgeCount` parallel connectors
needs and returns the larger of that corridor width and `baseGap`:

1. **Corridor width.** The corridor width is `2 * LayeredLayoutMetrics.ConnectorClearance +
   (edgeCount - 1) * LayeredLayoutMetrics.EdgeSpacing` — a clearance on each side of the fan plus one
   slot-to-slot spacing for every gap between adjacent connectors.
2. **Floor at the base gap.** `Math.Max(baseGap, corridorWidth)` makes the widening strictly
   additive: the method never narrows `baseGap`, only ever grows it.
3. **Degenerate edge counts.** An `edgeCount` of one yields zero inter-connector gaps, so the
   corridor width collapses to `2 * ConnectorClearance`, which is at or below every caller's existing
   base gap; the base gap wins and is returned unchanged. An `edgeCount` of zero (or a negative
   count, which no caller produces but which the formula still tolerates) drives `(edgeCount - 1)`
   negative, pulling the corridor width below `2 * ConnectorClearance` and, in practice, below the
   base gap; `Math.Max` again floors the result at the base gap. Both degenerate cases therefore
   leave a caller's pre-existing spacing exactly unchanged rather than requiring a separate guard
   clause.

#### EdgeCountGapWidener Error Handling

`Widen` performs no argument validation and never throws: every `double` value of `baseGap` and
every `int` value of `edgeCount` (including zero and negative counts) is a valid input, handled by
the formula and the `Math.Max` floor described above rather than by a rejected-input error path.

#### EdgeCountGapWidener Interactions

`EdgeCountGapWidener` depends only on `LayeredLayoutMetrics.ConnectorClearance` and
`LayeredLayoutMetrics.EdgeSpacing` from `Engine.Layered`. It is consumed by two call sites that each
widen a gap in proportion to the number of parallel connectors sharing it:

- **ContainmentPacker** widens the horizontal gap between two items on the same row when parallel
  connectors are routed through that gap, so a fan of connectors between adjacent packed items gets
  distinct routing lanes. See *ContainmentPacker Unit Design*.
- **HierarchicalLayoutAlgorithm** widens the horizontal gap between two sibling containers placed
  side by side on its no-boundary-port sibling-gap-widening pass, in proportion to the number of
  cross-container edges between that pair. See *HierarchicalLayoutAlgorithm Unit Design*.

Both call sites size their gap from the number of edges their own connector-routing pass must fan
through it; extracting the formula here keeps the three widening rules (this helper and
`BrandesKopfPlacer`'s own corridor sizing) byte-identical and prevents the copy-paste drift that
would otherwise let one call site's spacing diverge from another's.

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-EdgeCountGapWidener-CorridorFormula | EdgeCountGapWidener behavior described above |
| Rendering-Layout-EdgeCountGapWidener-DegenerateCases | EdgeCountGapWidener behavior described above |
| Rendering-Layout-EdgeCountGapWidener-NeverNarrows | EdgeCountGapWidener behavior described above |
