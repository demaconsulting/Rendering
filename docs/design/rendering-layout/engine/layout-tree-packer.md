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

`Pack(trees, spacing, aspect)` rejects a null `trees` argument with `ArgumentNullException`, then:

1. **Degenerate cases.** An empty list returns a zero-size, empty `LayoutTree`. A single-tree list is
   returned unchanged, since a lone tree needs no translation or merging.
2. **Shelf packing.** Each sub-tree's overall `Width`/`Height` is treated as one packable item and
   placed by a greedy shelf heuristic (target row width = the wider of the widest single tree and
   `sqrt(totalArea) * aspect`), matching the heuristic `Engine.Layered.ComponentPacker` uses for its own
   packing.
3. **Recursive translation.** Every node in a packed sub-tree is shifted by that sub-tree's assigned
   shelf offset, recursively: a `LayoutBox`'s own `X`/`Y` and every nested `LayoutBox.Children` node, a
   `LayoutLine`'s every `Waypoints` point, and a `LayoutPort`'s `CentreX`/`CentreY`. `LayoutTree`
   coordinates are absolute (not parent-relative), so translating a placed sub-tree requires this
   recursive walk rather than a single offset applied once at the root.
4. **Combined result.** The packed region's overall width/height (from the shelf layout) and the
   concatenated, translated node lists from every sub-tree (in input order) are assembled into one
   combined `LayoutTree`.

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

The shelf packer always wraps a component onto the next row once the running row width would exceed
the target, which suits the compact rectangular components every bundled leaf algorithm produces. A
future enhancement could add a force-directed/spring-relaxation fallback that packs many small, sparse
components more organically (for example a large field of unrelated singleton nodes) instead of a
strict grid of shelves, but that is a materially different, self-contained algorithm and is explicitly
out of scope for this type today.

#### LayoutTreePacker Interactions

`LayoutTreePacker` depends only on the `LayoutTree`, `LayoutNode`, `LayoutBox`, `LayoutLine`, and
`LayoutPort` types from `DemaConsulting.Rendering.Abstractions` / `DemaConsulting.Rendering`. It is
consumed solely by `AutoLayoutAlgorithm`, which packs one placed sub-tree per routed component into a
single combined tree.

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
