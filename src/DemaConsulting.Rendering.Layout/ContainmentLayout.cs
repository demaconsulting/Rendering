// <copyright file="ContainmentLayout.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Layout.Engine;

namespace DemaConsulting.Rendering.Layout;

/// <summary>
/// Options controlling how <see cref="ContainmentLayout"/> packs child boxes into a container region.
/// </summary>
/// <param name="MaxContentWidth">
/// Maximum width of the content area (excluding outer padding), in logical pixels. Rows wrap to a new
/// line once the next child would push the current row past this budget. Must be positive. Mirrors the
/// wrap width a container gives its contents, analogous to ELK's node-size / content-area constraint.
/// </param>
/// <param name="HorizontalGap">
/// Gap, in logical pixels, kept between adjacent children in the same row. Defaults to <c>8.0</c>.
/// Corresponds to ELK's <c>spacing.nodeNode</c> along the row axis.
/// </param>
/// <param name="VerticalGap">
/// Gap, in logical pixels, kept between successive rows. Defaults to <c>8.0</c>. Corresponds to ELK's
/// <c>spacing.nodeNode</c> across rows.
/// </param>
/// <param name="Padding">
/// Uniform padding, in logical pixels, added around the entire packed region on every side. Defaults to
/// <c>12.0</c>. Corresponds to ELK's <c>padding</c> inset between a container's border and its contents.
/// </param>
/// <remarks>
/// The gap and padding defaults are deliberately modest, sensible values that read well for typical box
/// sizes; supply explicit values to reproduce a specific container spacing (for example a downstream
/// adapter matching its historical output).
/// </remarks>
public sealed record ContainmentOptions(
    double MaxContentWidth,
    double HorizontalGap = 8.0,
    double VerticalGap = 8.0,
    double Padding = 12.0);

/// <summary>
/// The result of a containment-packing operation: the packed region size together with the input boxes
/// repositioned to their packed coordinates.
/// </summary>
/// <param name="Width">Total width of the packed region (including outer padding) in logical pixels.</param>
/// <param name="Height">Total height of the packed region (including outer padding) in logical pixels.</param>
/// <param name="Children">
/// The input boxes, in their original order, each repositioned to its packed <c>X</c>/<c>Y</c>. Every
/// coordinate is relative to the region origin <c>(0, 0)</c>; all non-position fields (label, depth,
/// shape, compartments, children, keyword) are carried through unchanged.
/// </param>
public sealed record ContainmentResult(
    double Width,
    double Height,
    IReadOnlyList<LayoutBox> Children);

/// <summary>
/// Packs a set of already-sized model boxes into a single container region, arranging them into rows
/// within a width budget. This is the public, model-speaking containment building block: it maps each
/// child's size onto the internal row bin-packer, then returns the same boxes repositioned to their
/// packed coordinates plus the size of the region that encloses them.
/// </summary>
/// <remarks>
/// <para>
/// The operation is deterministic and preserves input order: given the same children and options it
/// always produces the same geometry, and the returned children appear in the same order they were
/// supplied. Children are laid out left to right along each row and wrap to a new row beneath the current
/// one when the next child would exceed <see cref="ContainmentOptions.MaxContentWidth"/>. A child wider
/// than the content width is placed alone on its own row, and the region widens to contain it.
/// </para>
/// <para>
/// No two packed boxes overlap, and every packed box lies fully within the returned
/// <see cref="ContainmentResult.Width"/> by <see cref="ContainmentResult.Height"/> region. That region
/// includes the uniform <see cref="ContainmentOptions.Padding"/> on every side, so an empty input yields
/// a padding-only region. All coordinates are relative to the region origin <c>(0, 0)</c>; a caller that
/// nests the region inside a parent box offsets the children by the region's placement.
/// </para>
/// <para>
/// The operation is deliberately model-agnostic: only each child's <see cref="LayoutBox.Width"/> and
/// <see cref="LayoutBox.Height"/> influence placement, and every other field is carried onto the
/// repositioned box unchanged. It composes with, rather than replaces, the layered algorithm — use it to
/// arrange peers inside a container when their relative order, not their connectivity, drives the layout.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Three already-sized boxes to arrange inside a container.
/// var boxes = new[]
/// {
///     new LayoutBox(0, 0, 80, 40, "A", 0, BoxShape.Rectangle, [], []),
///     new LayoutBox(0, 0, 120, 40, "B", 0, BoxShape.Rectangle, [], []),
///     new LayoutBox(0, 0, 60, 40, "C", 0, BoxShape.Rectangle, [], []),
/// };
///
/// // Pack them into rows no wider than 200px of content, with the default gaps and padding.
/// var result = ContainmentLayout.Pack(boxes, new ContainmentOptions(MaxContentWidth: 200));
///
/// // result.Children are the same boxes repositioned to their packed X/Y (region-relative);
/// // result.Width x result.Height is the container size that fits them plus padding.
/// var container = new LayoutBox(0, 0, result.Width, result.Height, "Group", 0, BoxShape.Folder, [], result.Children);
/// </code>
/// </example>
public static class ContainmentLayout
{
    /// <summary>
    /// Packs the given <paramref name="children"/> into a container region according to
    /// <paramref name="options"/>.
    /// </summary>
    /// <param name="children">
    /// The boxes to pack, in the desired visual order. Only each box's width and height affect placement;
    /// all other fields are preserved on the repositioned box.
    /// </param>
    /// <param name="options">
    /// Packing options: the row-wrap content width plus the horizontal gap, vertical gap, and outer
    /// padding.
    /// </param>
    /// <returns>
    /// A <see cref="ContainmentResult"/> carrying the region size and the input boxes repositioned to
    /// their packed, region-relative coordinates, in input order.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="children"/>, <paramref name="options"/>, or any element of
    /// <paramref name="children"/> is <see langword="null"/>.
    /// </exception>
    public static ContainmentResult Pack(
        IReadOnlyList<LayoutBox> children,
        ContainmentOptions options)
    {
        ArgumentNullException.ThrowIfNull(children);
        ArgumentNullException.ThrowIfNull(options);

        // Map each child onto a size-only pack item, matching the packer back to the child by index.
        var items = new PackItem[children.Count];
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            ArgumentNullException.ThrowIfNull(child);
            items[i] = new PackItem(child.Width, child.Height);
        }

        var packed = ContainmentPacker.Pack(
            items,
            options.MaxContentWidth,
            options.HorizontalGap,
            options.VerticalGap,
            options.Padding);

        // Reposition each child to its packed rectangle, preserving every non-position field.
        var placed = new LayoutBox[children.Count];
        for (var i = 0; i < children.Count; i++)
        {
            var rect = packed.Rects[i];
            placed[i] = children[i] with { X = rect.X, Y = rect.Y };
        }

        return new ContainmentResult(packed.Width, packed.Height, placed);
    }
}
