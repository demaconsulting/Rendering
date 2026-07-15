// <copyright file="LayoutTreePacker.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Layout.Engine;

/// <summary>
/// Reusable shelf-packer that merges several independently-placed <see cref="LayoutTree"/>s (each
/// already a complete, self-contained canvas, typically produced by routing one connected component of
/// a graph through a different bundled leaf algorithm) into a single combined <see cref="LayoutTree"/>.
/// </summary>
/// <remarks>
///     <para>
///     This is the <see cref="LayoutTree"/>-level counterpart to
///     <see cref="Layered.ComponentPacker"/>: both split a disconnected input into independently-laid-out
///     pieces and then pack the pieces into one non-overlapping arrangement using the same greedy shelf
///     heuristic (target row width = the wider of the widest piece and <c>sqrt(totalArea) * aspect</c>).
///     They are legitimately distinct, small implementations rather than true duplication, because they
///     operate on different types at different levels: <see cref="Layered.ComponentPacker"/> works on the
///     layered engine's internal <c>Rect</c>/<c>Point2D</c> representation for components that all share
///     one algorithm (the layered pipeline), while this type works on already-rendered
///     <see cref="LayoutTree"/>s that may have come from <em>different</em> algorithms entirely (for
///     example one component routed through <c>layered</c> and another through <c>hierarchical</c>),
///     which is exactly the shape <see cref="AutoLayoutAlgorithm"/> needs for its per-component routing.
///     </para>
///     <para>
///     <strong>Translation.</strong> Every node in a packed sub-tree is shifted by that sub-tree's
///     assigned shelf offset, recursively: a <see cref="LayoutBox"/>'s own <c>X</c>/<c>Y</c> and every
///     nested <see cref="LayoutBox.Children"/> node, a <see cref="LayoutLine"/>'s every
///     <see cref="LayoutLine.Waypoints"/> point, and a <see cref="LayoutPort"/>'s <c>CentreX</c>/
///     <c>CentreY</c>. <see cref="LayoutTree"/> coordinates are absolute (not parent-relative — see its
///     own remarks), so translating a placed sub-tree requires this recursive walk rather than a single
///     offset applied once at the root.
///     </para>
///     <para>
///     <strong>Unknown node types fail loudly.</strong> <see cref="TranslateNode"/> switches over the
///     closed set of <see cref="LayoutNode"/> subtypes the three bundled leaf algorithms
///     (<c>layered</c>, <c>hierarchical</c>, <c>containment</c>) are confirmed to emit —
///     <see cref="LayoutBox"/>, <see cref="LayoutLine"/>, and <see cref="LayoutPort"/> (verified by
///     grepping every <c>new LayoutBox(</c>/<c>new LayoutLine(</c>/<c>new LayoutPort(</c> construction
///     site across <c>LayeredLayoutAlgorithm.cs</c>, <c>HierarchicalLayoutAlgorithm.cs</c>, and
///     <c>ContainmentLayoutAlgorithm.cs</c>). An unrecognized node type throws
///     <see cref="NotSupportedException"/> instead of being silently skipped. This is a deliberate
///     divergence from the renderer convention documented on <see cref="LayoutNode"/> ("unknown
///     subtypes should be skipped for forward compatibility"): a renderer skipping an unknown node still
///     draws every other node correctly, but a packer silently leaving an unknown node's coordinates
///     untranslated would place it at the wrong (pre-pack) position with no visible sign of the error —
///     a worse failure mode than a hard, immediate exception naming the offending type.
///     </para>
///     <para>
///     <strong>Out of scope: force-directed placement for sparse components.</strong> The shelf packer
///     always wraps a component onto the next row once the running row width would exceed the target,
///     which suits the compact rectangular components every bundled leaf algorithm produces. A future
///     enhancement could add a force-directed/spring-relaxation fallback that packs many small, sparse
///     components more organically (for example a large field of unrelated singleton nodes) instead of a
///     strict grid of shelves, but that is a materially different, self-contained algorithm and is
///     explicitly out of scope for this type today.
///     </para>
/// </remarks>
internal static class LayoutTreePacker
{
    /// <summary>
    /// Packs the supplied sub-trees into one combined <see cref="LayoutTree"/> using a greedy shelf
    /// packer, translating every placed node (recursively) by its assigned shelf offset.
    /// </summary>
    /// <param name="trees">
    /// The independently-placed sub-trees to merge, each a complete, self-contained canvas. Must not be
    /// <see langword="null"/>; may be empty (yielding a degenerate empty tree) or contain a single tree
    /// (its coordinates require no translation, but its node types are still validated against the
    /// closed set <see cref="TranslateNode"/> recognizes).
    /// </param>
    /// <param name="spacing">Gap, in logical pixels, kept between adjacent packed sub-trees.</param>
    /// <param name="aspect">
    /// Target width-to-height multiplier controlling the packed row width (larger values produce wider,
    /// shorter arrangements).
    /// </param>
    /// <returns>The combined, packed <see cref="LayoutTree"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="trees"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when a sub-tree contains a <see cref="LayoutNode"/> subtype outside the closed set this
    /// packer knows how to translate (see the type's remarks).
    /// </exception>
    public static LayoutTree Pack(IReadOnlyList<LayoutTree> trees, double spacing, double aspect)
    {
        ArgumentNullException.ThrowIfNull(trees);

        if (trees.Count == 0)
        {
            return new LayoutTree(0.0, 0.0, []);
        }

        if (trees.Count == 1)
        {
            // A single tree needs no coordinate translation (offset (0, 0)), but its node types must
            // still be validated against the closed set TranslateNode recognizes — routing it through
            // TranslateNode here (rather than returning the tree unchanged) closes a gap where an
            // unrecognized LayoutNode subtype in singleton input would silently bypass the same
            // NotSupportedException every multi-tree pack enforces.
            var singleton = trees[0];
            return singleton with
            {
                Nodes = [.. singleton.Nodes.Select(node => TranslateNode(node, 0.0, 0.0))],
            };
        }

        var totalArea = 0.0;
        var widest = 0.0;
        foreach (var tree in trees)
        {
            totalArea += tree.Width * tree.Height;
            widest = Math.Max(widest, tree.Width);
        }

        var targetRowWidth = Math.Max(widest, Math.Sqrt(totalArea) * aspect);

        var offsetX = new double[trees.Count];
        var offsetY = new double[trees.Count];
        var cursorX = 0.0;
        var shelfTop = 0.0;
        var shelfHeight = 0.0;
        var totalWidth = 0.0;
        for (var i = 0; i < trees.Count; i++)
        {
            var tree = trees[i];

            // Wrap to a new shelf when the running row width would exceed the target (but never leave
            // a shelf empty — an over-wide tree sits alone on its own shelf).
            if (cursorX > 0.0 && cursorX + tree.Width > targetRowWidth)
            {
                shelfTop += shelfHeight + spacing;
                cursorX = 0.0;
                shelfHeight = 0.0;
            }

            offsetX[i] = cursorX;
            offsetY[i] = shelfTop;
            cursorX += tree.Width + spacing;
            shelfHeight = Math.Max(shelfHeight, tree.Height);
            totalWidth = Math.Max(totalWidth, offsetX[i] + tree.Width);
        }

        var totalHeight = shelfTop + shelfHeight;

        var mergedNodes = new List<LayoutNode>();
        var mergedWarnings = new List<string>();
        for (var i = 0; i < trees.Count; i++)
        {
            var tree = trees[i];
            var dx = offsetX[i];
            var dy = offsetY[i];

            foreach (var node in tree.Nodes)
            {
                mergedNodes.Add(TranslateNode(node, dx, dy));
            }

            mergedWarnings.AddRange(tree.Warnings);
        }

        return new LayoutTree(totalWidth, totalHeight, mergedNodes) { Warnings = mergedWarnings };
    }

    /// <summary>
    /// Recursively translates a single placed <see cref="LayoutNode"/> (and any nested nodes it
    /// carries) by <paramref name="dx"/>/<paramref name="dy"/>.
    /// </summary>
    /// <param name="node">The node to translate.</param>
    /// <param name="dx">Horizontal offset, in logical pixels.</param>
    /// <param name="dy">Vertical offset, in logical pixels.</param>
    /// <returns>A new node of the same kind, shifted by the given offset.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when <paramref name="node"/> is a <see cref="LayoutNode"/> subtype outside this packer's
    /// closed, verified set (see the type's remarks).
    /// </exception>
    private static LayoutNode TranslateNode(LayoutNode node, double dx, double dy) => node switch
    {
        LayoutBox box => box with
        {
            X = box.X + dx,
            Y = box.Y + dy,
            Children = [.. box.Children.Select(child => TranslateNode(child, dx, dy))],
        },
        LayoutLine line => line with
        {
            Waypoints = [.. line.Waypoints.Select(point => new Point2D(point.X + dx, point.Y + dy))],
        },
        LayoutPort port => port with
        {
            CentreX = port.CentreX + dx,
            CentreY = port.CentreY + dy,
        },
        _ => throw new NotSupportedException(
            $"LayoutTreePacker cannot translate unrecognized LayoutNode subtype '{node.GetType().Name}'. " +
            "The closed set of node types the bundled leaf algorithms emit is LayoutBox, LayoutLine, and " +
            "LayoutPort; if a new leaf algorithm or node type is introduced, TranslateNode must be " +
            "extended to cover it."),
    };
}
