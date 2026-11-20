### LayoutTreePacker Unit Design

Part of the Rendering Layout system.

#### LayoutTreePacker Purpose

`LayoutTreePacker` is a reusable shelf-packer that merges several independently-placed `LayoutTree`s
(each already a complete, self-contained canvas, typically produced by routing one connected component
of a graph through a different bundled leaf algorithm) into a single combined `LayoutTree`. It is the
`LayoutTree`-level counterpart to `Engine.Layered.ComponentPacker`: both split a disconnected input
into independently-laid-out pieces and pack the pieces into one non-overlapping arrangement using the
same greedy shelf heuristic. They are legitimately distinct, small implementations rather than true
duplication, because they operate on different types at different levels: `ComponentPacker` works on
the layered engine's internal `Rect`/`Point2D` representation for components that all share one
algorithm (the layered pipeline), while `LayoutTreePacker` works on already-rendered `LayoutTree`s that
may have come from *different* algorithms entirely (for example one component routed through
`layered` and another through `hierarchical`) — exactly the shape `AutoLayoutAlgorithm` needs for its
per-component routing.

#### LayoutTreePacker Data Model

`LayoutTreePacker` is an internal static class with no instance state. Input is an
`IReadOnlyList<LayoutTree>` (the sub-trees to merge), a `spacing` (gap kept between adjacent packed
sub-trees), and an `aspect` (target width-to-height multiplier for the packed arrangement). The result
is a single combined `LayoutTree`.

#### LayoutTreePacker Methods

`Pack(trees, spacing, aspect, margin)` rejects a null `trees` argument with `ArgumentNullException`, then:

1. **Degenerate cases.** An empty list returns a zero-size, empty `LayoutTree`. A single-tree list is
   returned unchanged, since a lone tree needs no translation or merging.
2. **Arrangement.** Each sub-tree's overall `Width`/`Height` is treated as one packable item and placed by
   `Arrange(sizes, spacing, aspect)`. It first computes the greedy shelf arrangement (`ArrangeShelf`; target
   row width = the wider of the widest single tree and `sqrt(totalArea) * aspect`), the heuristic
   `Engine.Layered.ComponentPacker` historically used. It then searches a free-space arrangement: a maximal-
   rectangles search over the space left beside already-placed pieces, for nine candidate row widths and for
   the input order and the tallest-first order, so a small piece may fill the hole beside a tall one. The
   free-space result replaces the shelf arrangement only when its canvas is strictly smaller and its
   aspect-penalized score (no penalty for an aspect within 0.5 to 2) is not worse (a tie within the tolerance
   counts as not worse); otherwise (including a tie in both area and score, an empty input and a single piece) the
   shelf arrangement is returned, so the search can never produce
   a larger, overlapping or non-deterministic result. `ComponentPacker` uses the same `Arrange` for the
   components of a layered graph.
3. **Recursive translation.** Every node in a packed sub-tree is shifted by that sub-tree's assigned
   offset, recursively: a `LayoutBox`'s own `X`/`Y` and every nested `LayoutBox.Children` node, a
   `LayoutLine`'s every `Waypoints` point, and a `LayoutPort`'s `CentreX`/`CentreY`. `LayoutTree`
   coordinates are absolute (not parent-relative), so translating a placed sub-tree requires this
   recursive walk rather than a single offset applied once at the root.
4. **Combined result.** The packed region's overall width/height (from the arrangement, plus an optional outer
   `margin` on every side) and the concatenated, translated node lists from every sub-tree (in input order) are
   assembled into one combined `LayoutTree`.

`TuckIntoHoles(host, pieces, spacing)` places small pieces (the isolated nodes of an auto-routed graph) into
holes of an already packed host tree: a keep-out grid marks the host's content (and the required spacing), the
position of each piece is the one that minimizes the largest remaining empty square, and the result is the
host with the pieces inside its own canvas, or `null` when a piece fits nowhere or no piece is supplied. The
caller (`AutoLayoutAlgorithm`) accepts the result only when the empty square is not larger.

`EmptySpaceMeter` (internal, same engine folder) measures the largest empty axis-aligned square of a placed
tree on a 4 pixel occupancy grid: a leaf box, a connector segment dilated by one cell and a resolved connector
label occupy cells, a container outline is transparent (its interior is empty unless something inside it
occupies it), and the largest all-empty square is found by the classic dynamic program over the grid (an empty
tree measures its smaller canvas side). `PassingSquare` is the diagram-quality limit for that square, three times
the mean leaf-box height (zero without a leaf box). The tuck step and the hierarchical ordering refinement use
the measure to refuse a change that opens a larger empty block.

The hole search scores up to 240 positions of each of up to four block shapes, so the cost of one position matters
on a large host canvas (Rendering#75: the whole Annex A model is 2 million cells and spent more time here than in
routing). `EmptySpaceMeter.SquareIndex` holds the dynamic-program table of the host once and answers "the largest
empty square once this rectangle is filled in" from it exactly: a square ending at a cell reaches only the cells up
and to the left of it, so the cells left of or above the rectangle keep their value (two prefix maxima) and only the
cells right of and below its first column and row are recomputed, starting from the unchanged values beside them.
The search also stops a position as soon as it has found a square larger than the best position so far, since such a
position cannot win. Both are exact: `EmptySpaceMeterTests` compares the index with a full pass over a filled copy of
the grid on random grids and rectangles, and the gallery is unchanged.

#### LayoutTreePacker Error Handling

A null `trees` argument throws `ArgumentNullException`. `TranslateNode` switches over the closed set
of `LayoutNode` subtypes the three bundled leaf algorithms (`layered`, `hierarchical`, `containment`)
are confirmed to emit — `LayoutBox`, `LayoutLine`, and `LayoutPort` (verified by grepping every
`new LayoutBox(`/`new LayoutLine(`/`new LayoutPort(` construction site across `LayeredLayoutAlgorithm.cs`,
`HierarchicalLayoutAlgorithm.cs`, and `ContainmentLayoutAlgorithm.cs`). An unrecognized node type throws
`NotSupportedException` instead of being silently skipped — a deliberate divergence from the renderer
convention documented on `LayoutNode` ("unknown subtypes should be skipped for forward compatibility"):
a renderer skipping an unknown node still draws every other node correctly, but a packer silently
leaving an unknown node's coordinates untranslated would place it at the wrong (pre-pack) position with
no visible sign of the error — a worse failure mode than a hard, immediate exception naming the
offending type.

#### LayoutTreePacker Design Notes: Out of Scope

The arrangement is a shelf or maximal-rectangles heuristic, which suits the compact rectangular components every
bundled leaf algorithm produces. A
future enhancement could add a force-directed/spring-relaxation fallback that packs many small, sparse
components more organically (for example a large field of unrelated singleton nodes) instead of a
strict grid of shelves, but that is a materially different, self-contained algorithm and is explicitly
out of scope for this type today.

#### LayoutTreePacker Interactions

`LayoutTreePacker` depends only on the `LayoutTree`, `LayoutNode`, `LayoutBox`, `LayoutLine`, and
`LayoutPort` types from `DemaConsulting.Rendering.Abstractions` / `DemaConsulting.Rendering`. It is
consumed by `AutoLayoutAlgorithm`, which packs one placed sub-tree per routed component into a
single combined tree, and (through `Arrange`) by `Engine.Layered.ComponentPacker`. `EmptySpaceMeter` is also
consumed by `HierarchicalLayoutAlgorithm`'s ordering refinement.

#### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-LayoutTreePacker-PacksMultipleTrees | LayoutTreePacker behavior described above |
| Rendering-Layout-LayoutTreePacker-TranslatesCoordinates | LayoutTreePacker behavior described above |
| Rendering-Layout-LayoutTreePacker-TranslatesNestedChildren | LayoutTreePacker behavior described above |
| Rendering-Layout-LayoutTreePacker-TranslatesWaypoints | LayoutTreePacker behavior described above |
| Rendering-Layout-LayoutTreePacker-DegenerateCases | LayoutTreePacker behavior described above |
| Rendering-Layout-LayoutTreePacker-UnsupportedNodeType | LayoutTreePacker behavior described above |
| Rendering-Layout-LayoutTreePacker-PreservesWarnings | LayoutTreePacker behavior described above |
| Rendering-Layout-LayoutTreePacker-Validation | LayoutTreePacker behavior described above |
| Rendering-Layout-LayoutTreePacker-FreeSpacePacking | `Arrange` free-space search, `TuckIntoHoles` |
| Rendering-Layout-LayoutTreePacker-NeverWorseThanShelf | `Arrange` acceptance rule |
| Rendering-Layout-LayoutTreePacker-EmptySpaceMeasure | `EmptySpaceMeter` |
