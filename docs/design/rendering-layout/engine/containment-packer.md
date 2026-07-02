# ContainmentPacker Unit Design

Part of the [Rendering Layout](../rendering-layout.md) system.

## ContainmentPacker Purpose

`ContainmentPacker` arranges a sequence of variable-size items into rows within a width budget. It
places items left to right, wraps to a new row when the next item would exceed the maximum content
width, and sizes the enclosing region to fit all items plus uniform outer padding. It is used to
pack child elements inside a containing box in a compact, ordered grid.

## ContainmentPacker Data Model

`ContainmentPacker` is a static class with no instance state. Inputs are a list of `PackItem`
records (each a `Width` and `Height`), a `maxContentWidth`, a `horizontalGap`, a `verticalGap`, and
a `padding`. The result is a `PackResult` record carrying the region `Width`, `Height`, and the
ordered list of `PackedRect` rectangles, one per input item in input order, each positioned relative
to the region origin `(0, 0)`.

## ContainmentPacker Methods

`Pack(items, maxContentWidth, horizontalGap, verticalGap, padding)` computes the packing as a single
left-to-right shelf (row) pass:

1. **Degenerate case.** An empty item list returns a region of `2 * padding` on each axis with no
   rectangles.
2. **Row filling.** A horizontal cursor starts at the left padding offset. Each item is placed at
   the current cursor and the cursor advances past the item plus `horizontalGap`. The row height
   tracks the tallest item placed so far.
3. **Wrapping.** Before placing an item that is not first in its row, the packer checks whether its
   right edge would exceed `padding + maxContentWidth`. If so, it drops to a new row (advancing the
   row top by the row height plus `verticalGap`), resets the cursor, and places the item there.
   Because the first-in-row item is exempt from the check, an item wider than the content width is
   placed alone on its own row and the region width grows to contain it.
4. **Region sizing.** The total width is the widest row's right edge plus padding; the total height
   is the last row's bottom plus padding.

Input order is preserved, and the left-to-right, no-backtracking placement is what guarantees that
no two rectangles overlap and that every rectangle stays within the reported region.

## ContainmentPacker Error Handling

A null `items` argument throws `ArgumentNullException`. An empty item list returns a padding-only
region. No other input causes a throw; an oversized item is handled by the first-in-row exemption
rather than by an error.

## ContainmentPacker Interactions

`ContainmentPacker` depends only on the `PackItem`, `PackedRect`, and `PackResult` value types
declared alongside it. It is a leaf engine invoked by callers that pack child elements inside a
containing box, using the returned rectangles to position children and the region size to size the
container.

## Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Layout-ContainmentPacker-SingleRow | ContainmentPacker behavior described above |
| Rendering-Layout-ContainmentPacker-Wrapping | ContainmentPacker behavior described above |
| Rendering-Layout-ContainmentPacker-NoOverlap | ContainmentPacker behavior described above |
| Rendering-Layout-ContainmentPacker-WithinBounds | ContainmentPacker behavior described above |
| Rendering-Layout-ContainmentPacker-OversizedItem | ContainmentPacker behavior described above |
| Rendering-Layout-ContainmentPacker-EmptyInput | ContainmentPacker behavior described above |
| Rendering-Layout-ContainmentPacker-SingleItem | ContainmentPacker behavior described above |
