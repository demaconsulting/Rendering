// <copyright file="LayoutGraphNode.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// A node in an input <see cref="LayoutGraph"/>: a sized box that a layout algorithm places. The
/// node carries its own configuration via <see cref="PropertyHolder"/>, allowing per-node overrides
/// of algorithm options. A node may additionally act as a <em>container</em> of a nested child
/// subgraph, making the input model hierarchical.
/// </summary>
/// <remarks>
///     <para>
///     Modelled after the Eclipse Layout Kernel (ELK) <c>ElkNode</c>: a node is either a
///     <em>leaf</em> (no children) or a <em>container</em> (compound node) that owns a child
///     subgraph of nested nodes and the edges contained at that level. The nesting is recursive, so
///     a container's children may themselves be containers, and the root <see cref="LayoutGraph"/>
///     is the top-level container.
///     </para>
///     <para>
///     The child subgraph is exposed through <see cref="Children"/> and is created lazily: a leaf
///     node allocates no child graph and costs nothing beyond a null reference, so a flat (non-nested)
///     graph behaves exactly as it did before nesting existed. Use <see cref="HasChildren"/> to test
///     cheaply whether a node is a container without forcing that allocation.
///     </para>
///     <para>
///     Identifiers are unique <em>per scope</em>: each container (including the root graph) enforces
///     its own node- and edge-identifier uniqueness, so the same identifier may be reused in
///     different scopes but not twice within one scope — exactly as ELK scopes identifiers to a
///     compound node's children.
///     </para>
///     <para>
///     An edge whose endpoints live in different containers (a <em>cross-container</em> edge) is not
///     a new type: it is an ordinary <see cref="LayoutGraphEdge"/> added to an ancestor container —
///     the lowest common ancestor (LCA) of its endpoints, or any container above it — whose
///     <see cref="LayoutGraphEdge.Source"/> and <see cref="LayoutGraphEdge.Target"/> reference the
///     descendant nodes. The model deliberately does not forbid such cross-scope references; a
///     hierarchical layout engine resolves the routing in the owning container's coordinate space.
///     </para>
/// </remarks>
/// <example>
///     Building a two-level graph — a container node whose <see cref="Children"/> hold two nested
///     nodes joined by an intra-container edge, plus a cross-container edge added at the root that
///     references a descendant node:
///     <code>
///     var graph = new LayoutGraph();
///
///     // A container node and a peer leaf node at the root scope.
///     var group = graph.AddNode("group", 200, 120);
///     var outside = graph.AddNode("outside", 80, 40);
///
///     // Nested children live in the container's own scope.
///     var inner1 = group.Children.AddNode("child1", 80, 40);
///     var inner2 = group.Children.AddNode("child2", 80, 40);
///     group.Children.AddEdge("inner-edge", inner1, inner2);   // intra-container edge
///
///     // A cross-container edge lives in the lowest common ancestor (here the root) and
///     // references a descendant node inside the container.
///     graph.AddEdge("cross-edge", outside, inner1);
///
///     bool groupIsContainer = group.HasChildren;     // true
///     bool outsideIsContainer = outside.HasChildren; // false (leaf; no child graph allocated)
///     </code>
/// </example>
public sealed class LayoutGraphNode : PropertyHolder
{
    /// <summary>
    /// Backing store for the lazily-created child subgraph. Remains <see langword="null"/> for a leaf
    /// node so that a non-container node allocates nothing and a flat graph is unchanged.
    /// </summary>
    private LayoutGraph? _children;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutGraphNode"/> class.
    /// </summary>
    /// <param name="id">Identifier unique within the owning graph.</param>
    /// <param name="width">Width of the node's bounding box in logical pixels.</param>
    /// <param name="height">Height of the node's bounding box in logical pixels.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is empty.</exception>
    public LayoutGraphNode(string id, double width, double height)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        Id = id;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the identifier, unique within the owning graph.</summary>
    public string Id { get; }

    /// <summary>Gets or sets the width of the node's bounding box in logical pixels.</summary>
    public double Width { get; set; }

    /// <summary>Gets or sets the height of the node's bounding box in logical pixels.</summary>
    public double Height { get; set; }

    /// <summary>Gets or sets an optional text label rendered inside the node.</summary>
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets the visual shape of the box a leaf algorithm places for this node. Defaults to
    /// <see cref="BoxShape.Rectangle"/>; set to <see cref="BoxShape.Folder"/> for a package-style
    /// container, for example.
    /// </summary>
    public BoxShape Shape { get; set; } = BoxShape.Rectangle;

    /// <summary>
    /// Gets or sets an optional keyword (for example <c>"part def"</c>) rendered on a smaller line
    /// above the node's <see cref="Label"/>, following the SysML v2 graphical convention.
    /// <see langword="null"/> when no keyword line should be shown.
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// Gets or sets the ordered list of compartments (for example an attributes or ports section)
    /// displayed below the node's label when it is placed as a <see cref="LayoutBox"/>. Empty by
    /// default.
    /// </summary>
    public IReadOnlyList<LayoutCompartment> Compartments { get; set; } = [];

    /// <summary>
    /// Gets or sets the height, in logical pixels, of the title band a hierarchical layout engine
    /// reserves above this node's children when it acts as a labelled container. <see langword="null"/>
    /// (the default) selects the engine's own generic default band height; set this explicitly — for
    /// example to a theme's computed title-area height — when the container also carries a
    /// <see cref="Keyword"/> or a larger title font than the generic default assumes, so the reserved
    /// band matches what the renderer will actually draw. Ignored for a leaf node (one with no
    /// children).
    /// </summary>
    public double? TitleHeight { get; set; }

    /// <summary>
    /// Gets the child subgraph nested inside this node, turning the node into a container of nodes and
    /// contained edges.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Mirrors ELK's compound-node model: accessing this property makes the node a container whose
    ///     children occupy their own identifier scope. Add nested elements through the returned graph's
    ///     own factory methods, for example <c>node.Children.AddNode(...)</c> and
    ///     <c>node.Children.AddEdge(...)</c>; identifier-uniqueness, insertion order, and per-element
    ///     property overrides all behave exactly as they do on the root <see cref="LayoutGraph"/>.
    ///     </para>
    ///     <para>
    ///     The child graph is created lazily on first access, so merely reading this property once
    ///     promotes the node to a (possibly empty) container. To test whether a node already holds
    ///     children without triggering that allocation, use <see cref="HasChildren"/>.
    ///     </para>
    /// </remarks>
    public LayoutGraph Children => _children ??= new LayoutGraph();

    /// <summary>
    /// Gets a value indicating whether this node is a container that currently holds at least one child
    /// node.
    /// </summary>
    /// <remarks>
    ///     Lets consumers — and a future hierarchical layout engine — distinguish a container from a
    ///     leaf and skip empty containers cheaply. The check does not allocate the child subgraph: it
    ///     returns <see langword="false"/> for a leaf node whose <see cref="Children"/> has never been
    ///     accessed, and also for a node whose child graph was materialized but never populated.
    /// </remarks>
    public bool HasChildren => _children is { Nodes.Count: > 0 };
}
