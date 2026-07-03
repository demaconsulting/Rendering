## ContainmentLayout Unit Design

Part of the Rendering Layout system.

### ContainmentLayout Purpose

`ContainmentLayout` is the public, model-speaking containment building block: it packs a set of
already-sized `LayoutBox` children into a single container region, arranging them into rows within a
width budget. It complements `ConnectorRouter` and `LayeredLayoutAlgorithm`: where the algorithm places
and routes a whole connected graph and `ConnectorRouter` joins already-placed boxes, `ContainmentLayout`
arranges peer boxes inside a container when their reading order, not their connectivity, drives the
layout (for example the members of a package or the contents of a folder). It is the single-level packing
primitive; multi-level folder/canvas assembly is composed from it by higher layers rather than provided
here.

### ContainmentLayout Data Model

The unit comprises the public static class plus two records:

- `ContainmentOptions(MaxContentWidth, HorizontalGap, VerticalGap, Padding)` — an immutable record
  selecting the row-wrap content width and the spacing. `MaxContentWidth` is required; `HorizontalGap`
  and `VerticalGap` default to `8.0` and `Padding` defaults to `12.0` logical pixels. The names and
  defaults mirror ELK's content-area, `spacing.nodeNode`, and `padding` vocabulary.
- `ContainmentResult(Width, Height, Children)` — an immutable record carrying the enclosing region size
  (including outer padding) and the input boxes repositioned to their packed, region-relative
  coordinates, in input order.
- `ContainmentLayout` — a stateless static class exposing `Pack(children, options)`.

### ContainmentLayout Methods

`Pack(children, options)` rejects null arguments — a null `children` list, null `options`, or any null
child element — with `ArgumentNullException`, then:

1. **Size mapping.** Maps each child onto a size-only `PackItem` built from its `Width` and `Height`,
   correlating the packer's output back to each child by index.
2. **Packing.** Calls the internal `ContainmentPacker.Pack` with the mapped items and the options'
   content width, gaps, and padding to obtain the packed rectangles and region size.
3. **Repositioning.** Produces one new `LayoutBox` per child via `with { X = ..., Y = ... }`, updating
   only the coordinates from the corresponding packed rectangle and carrying every other field (label,
   depth, shape, compartments, nested children, keyword) through unchanged.
4. **Assembly.** Returns a `ContainmentResult` with the region `Width`/`Height` and the repositioned
   children in input order.

The operation is deterministic and order-preserving, never overlaps two children, keeps every child
within the reported region (which includes the outer padding on every side), places a child wider than
the content width alone on its own row while widening the region to contain it, and returns a
padding-only region for an empty input.

### ContainmentLayout Error Handling

Null `children`, `options`, or a null child element throw `ArgumentNullException`. Packing behavior for
degenerate sizes (zero or negative dimensions) follows the underlying `ContainmentPacker`; the public
operation adds no further validation beyond null rejection.

### ContainmentLayout Interactions

`ContainmentLayout` consumes the `LayoutBox` model type and the internal `ContainmentPacker`,
`PackItem`, `PackedRect`, and `PackResult` engine types, which remain internal to the Layout system —
the public API speaks only `LayoutBox`. It produces `LayoutBox` children that a caller nests inside a
container box (offsetting by the container's placement) and drops into a `LayoutTree` for a renderer to
draw. It is independent of the layered pipeline and can be used on any set of sized boxes.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-ContainmentLayout-Order | ContainmentLayout behavior described above |
| Rendering-Layout-ContainmentLayout-NoOverlap | ContainmentLayout behavior described above |
| Rendering-Layout-ContainmentLayout-WithinRegion | ContainmentLayout behavior described above |
| Rendering-Layout-ContainmentLayout-Wrapping | ContainmentLayout behavior described above |
| Rendering-Layout-ContainmentLayout-OversizedChild | ContainmentLayout behavior described above |
| Rendering-Layout-ContainmentLayout-EmptyInput | ContainmentLayout behavior described above |
| Rendering-Layout-ContainmentLayout-PreservesFields | ContainmentLayout behavior described above |
| Rendering-Layout-ContainmentLayout-Defaults | ContainmentLayout behavior described above |
| Rendering-Layout-ContainmentLayout-Validation | ContainmentLayout behavior described above |
