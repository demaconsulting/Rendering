// <copyright file="HierarchicalLayoutAlgorithm.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Globalization;
using System.Runtime.CompilerServices;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout.Engine;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout;

/// <summary>
/// The bundled recursive hierarchical layout engine: it lays out a compound <see cref="LayoutGraph"/>
/// by placing each container node's children in their own coordinate space, sizing the container to
/// fit them, and composing the placed sub-layouts into a single absolute <see cref="LayoutTree"/>.
/// This is the ELK <c>RecursiveGraphLayoutEngine</c> analogue; it selects a bundled leaf algorithm
/// (for example <c>layered</c> or <c>containment</c>) per scope and delegates the actual box placement
/// to it.
/// </summary>
/// <remarks>
///     <para>
///     The engine walks the graph in <em>post-order</em>: each container's children are laid out first,
///     then the container is given an effective size that encloses that sub-layout (plus padding and an
///     optional title band), and finally the current level is placed by the selected leaf algorithm
///     using those effective sizes. The placed sub-layouts are translated from their local origin into
///     each container box's interior and attached as the box's <see cref="LayoutBox.Children"/>.
///     </para>
///     <para>
///     Every <see cref="CoreOptions"/> property — <see cref="CoreOptions.Algorithm"/>,
///     <see cref="CoreOptions.Direction"/>, <see cref="CoreOptions.EdgeRouting"/>,
///     <see cref="CoreOptions.HierarchyHandling"/>, <see cref="CoreOptions.NodeSpacing"/>, and
///     <see cref="CoreOptions.LayerSpacing"/> — cascades per scope through the same generalized
///     mechanism, built on <see cref="PropertyHolder.OverlayOnto"/>: each scope's own explicit
///     overrides are overlaid onto its parent scope's already-resolved effective options, so an
///     unset scope inherits its nearest ancestor's value and any scope may override it for itself and
///     its descendants. Two established, independently-tested conventions decide where a container's
///     own overrides live: <see cref="CoreOptions.Algorithm"/> is set on the container <em>node</em>
///     itself (for example <c>group.Set(CoreOptions.Algorithm, "layered")</c>), while every other
///     property is set on the container's <see cref="LayoutGraphNode.Children"/> graph (for example
///     <c>group.Children.Set(CoreOptions.Direction, LayoutFlowDirection.Down)</c>). This lets, for
///     example, a <c>containment</c>-packed root hold a <c>layered</c> container flowing in its own
///     direction without either level knowing about the other.
///     </para>
///     <para>
///     Hierarchy is handled in <see cref="HierarchyHandling.SeparateChildren"/> mode: each container is
///     laid out in isolation and sized to fit its children, so container interiors are independent and
///     deterministic. Cross-hierarchy edges — edges declared on an ancestor graph whose endpoints live
///     in different (or the same) descendant containers, added to their lowest common ancestor per
///     the model's LCA convention or any graph above it — are routed at the level that declares them,
///     from the actual nested source node to the actual nested target node, steering around every
///     unrelated box and crossing only the walls of the containers that enclose their own endpoints.
///     The declaring level's placement accounts for an edge joining two different direct members as
///     if it joined those members (a layout-only proxy edge understood by the bundled leaf
///     algorithms), so related containers end up adjacent.
///     </para>
///     <para>
///     <strong>Flat-graph equivalence guarantee.</strong> When no direct node of a scope is a container
///     (a flat graph), the engine returns the selected leaf algorithm's output <em>unchanged</em>: it
///     delegates directly to the algorithm without cloning the graph or post-processing the tree, so the
///     result is byte-for-byte identical to invoking that algorithm itself. Hierarchical composition
///     only engages once a scope actually contains a container node.
///     </para>
///     <para>
///     The engine never mutates the caller's input graph. When a level must be re-sized to account for
///     nested content, it builds an internal <em>sized view</em> graph (same node order, container nodes
///     carrying their effective size, in-scope edges only) and lays that out, leaving the caller's graph
///     untouched. The engine is stateless and its <see cref="LayoutAlgorithmBase.Apply(LayoutGraph)"/> is
///     safe to call concurrently on distinct graphs.
///     </para>
///     <para>
///     <strong>Leaf-algorithm contract.</strong> This engine builds each scope's cascaded effective
///     <see cref="LayoutOptions"/> snapshot once and passes it to the selected leaf algorithm; it is
///     the leaf algorithm's own responsibility to resolve a property from that snapshot as the
///     ultimate source of a cascaded value (optionally preferring its own graph's override first, as
///     <see cref="LayeredLayoutAlgorithm"/> and <see cref="ContainmentLayoutAlgorithm"/> do). A leaf
///     algorithm that reads only its input graph and ignores the supplied options would silently break
///     cascading for that algorithm.
///     </para>
/// </remarks>
/// <example>
///     <code>
///     // A two-level graph: a labeled container laid out "layered", packed by a "containment" root.
///     var graph = new LayoutGraph();
///     var group = graph.AddNode("group", 10, 10);
///     group.Label = "Group";
///     group.Set(CoreOptions.Algorithm, "layered");        // this container lays its children out layered
///     var c1 = group.Children.AddNode("c1", 80, 40);
///     var c2 = group.Children.AddNode("c2", 80, 40);
///     group.Children.AddEdge("c1-c2", c1, c2);            // intra-container edge
///     graph.AddNode("outside", 80, 40);                   // a sibling leaf at the root
///
///     // Pack the root with the containment algorithm; containers recurse with their own algorithm.
///     graph.Set(CoreOptions.Algorithm, "containment");
///     var tree = new HierarchicalLayoutAlgorithm().Apply(graph);
///
///     // Hand the composed tree to a renderer (for example the SVG renderer).
///     // new SvgRenderer().Render(tree, new RenderOptions(Themes.Light), stream);
///     </code>
/// </example>
public sealed partial class HierarchicalLayoutAlgorithm : LayoutAlgorithmBase
{
    /// <summary>
    /// The stable algorithm identifier <c>"hierarchical"</c> under which this algorithm is selected and
    /// registered. Pass it to <see cref="LayoutOptions.ForAlgorithm(string)"/> or
    /// <see cref="CoreOptions.Algorithm"/> instead of hardcoding the literal string.
    /// </summary>
    public const string AlgorithmId = "hierarchical";

    /// <summary>
    /// The leaf algorithm a scope is placed with when it resolves to this engine's own
    /// <see cref="AlgorithmId"/>. Because this engine is not a leaf and is never registered into the leaf
    /// registry, selecting <c>"hierarchical"</c> for a scope (including through the facade default) means
    /// "apply the hierarchical engine, placing this level with the default leaf algorithm" rather than a
    /// self-referential lookup that cannot resolve. That default leaf is <see cref="LayeredLayoutAlgorithm"/>.
    /// </summary>
    private const string DefaultLeafAlgorithmId = LayeredLayoutAlgorithm.AlgorithmId;

    /// <summary>
    /// Inset, in logical pixels, kept on every side between a container's border and the sub-layout of
    /// its children. Sizing each container to its children plus this padding keeps nested content from
    /// touching the container edge.
    /// </summary>
    internal const double ContainerPadding = 12.0;

    /// <summary>
    /// Identity side table recording which edge objects are <em>layout-only</em>: edges that participate
    /// in the bundled leaf algorithms' placement (layering, ordering, gap sizing) but are never emitted
    /// as a connector, port, or anchor. This engine adds one per cross-hierarchy routed edge to the
    /// per-scope sized view so a scope's placement accounts for edges that are really routed between
    /// nested endpoints afterwards. The marker is keyed by edge object identity (never by a public
    /// string such as the edge identifier), so no caller-chosen edge identifier can collide with it;
    /// <see cref="AutoLayoutAlgorithm"/> carries it across its component split by re-marking each edge
    /// copy. Entries are released together with their edge.
    /// </summary>
    private static readonly ConditionalWeakTable<LayoutGraphEdge, object> LayoutOnlyEdges = new();

    /// <summary>
    /// The shared value stored against every marked edge in <see cref="LayoutOnlyEdges"/>.
    /// </summary>
    private static readonly object LayoutOnlyMarker = new();

    /// <summary>
    /// Internal order-rank hint: a rank per node (keyed by node identity) that the bundled leaf
    /// algorithms use to decide the order of nodes they would otherwise place in input order — the order
    /// of disconnected components, of a component's members, and of unconstrained siblings. Smaller ranks
    /// are placed first; a node missing from the dictionary is unranked and falls back to input order, so
    /// a scope with no hint lays out exactly as it always has. The hint travels in the scopes' effective
    /// <see cref="LayoutOptions"/> (never on the caller's graph or nodes, which are not mutated) and is
    /// inherited by nested scopes; whenever an algorithm works on copies of the nodes (the sized view,
    /// <see cref="AutoLayoutAlgorithm"/>'s split-off components) the keys are translated onto the copies
    /// with <see cref="TranslateOrderRanks"/>.
    /// </summary>
    internal static readonly LayoutProperty<IReadOnlyDictionary<LayoutGraphNode, double>?> OrderRanks =
        new("rendering.layout.hierarchical.orderRanks", null);

    /// <summary>
    /// Returns <paramref name="effective"/> with its <see cref="OrderRanks"/> hint re-keyed from the
    /// original nodes onto their copies (<paramref name="copyOf"/>), leaving the rank values unchanged and
    /// every other option as it is. When the options carry no hint, <paramref name="effective"/> itself is
    /// returned. Neither <paramref name="effective"/> nor the original hint is modified.
    /// </summary>
    /// <param name="effective">The effective options of the scope being copied.</param>
    /// <param name="copyOf">A map from each original node to its copy.</param>
    /// <returns>Options whose hint is keyed by the copies.</returns>
    internal static LayoutOptions TranslateOrderRanks(
        LayoutOptions effective,
        IReadOnlyDictionary<LayoutGraphNode, LayoutGraphNode> copyOf)
    {
        var ranks = effective.Get(OrderRanks);
        if (ranks == null || ranks.Count == 0)
        {
            return effective;
        }

        var translated = new Dictionary<LayoutGraphNode, double>();
        foreach (var pair in copyOf)
        {
            if (ranks.TryGetValue(pair.Key, out var rank))
            {
                translated[pair.Value] = rank;
            }
        }

        var overlay = new LayoutOptions();
        overlay.Set<IReadOnlyDictionary<LayoutGraphNode, double>?>(OrderRanks, translated);
        return overlay.OverlayOnto(effective);
    }

    /// <summary>
    /// Marks <paramref name="edge"/> as layout-only: it influences placement but is never drawn.
    /// Marking an already-marked edge is a no-op.
    /// </summary>
    /// <param name="edge">The edge to mark.</param>
    internal static void MarkLayoutOnly(LayoutGraphEdge edge) =>
        LayoutOnlyEdges.GetValue(edge, static _ => LayoutOnlyMarker);

    /// <summary>
    /// Determines whether <paramref name="edge"/> is a layout-only edge (see
    /// <see cref="MarkLayoutOnly(LayoutGraphEdge)"/>) that influences placement but is never drawn.
    /// </summary>
    /// <param name="edge">The edge to test.</param>
    /// <returns><see langword="true"/> when the edge is layout-only.</returns>
    internal static bool IsLayoutOnlyEdge(LayoutGraphEdge edge) =>
        LayoutOnlyEdges.TryGetValue(edge, out _);

    /// <summary>
    /// Maximum number of cascading-sizing re-runs the combined pass performs before accepting the
    /// current sizes. Growth only ever propagates outward through the finite nesting chain, so a stable
    /// fixed point is reached in at most chain-depth iterations; this cap is a safety bound well above any
    /// realistic nesting depth.
    /// </summary>
    private const int MaxSizingIterations = 32;

    /// <summary>
    /// Slack, in logical pixels, a container's placed interior may exceed its current effective size by
    /// before the cascading-sizing pass grows it, so sub-pixel rounding never triggers a spurious re-run.
    /// </summary>
    private const double SizingTolerance = 0.5;


    /// <summary>
    /// Default height, in logical pixels, of the title band reserved at the top of a container that
    /// carries a <see cref="LayoutGraphNode.Label"/> and does not set its own
    /// <see cref="LayoutGraphNode.TitleHeight"/>. The children are offset below this band so the label
    /// and the nested content never overlap; a container with no label reserves no band.
    /// </summary>
    private const double DefaultContainerTitleHeight = 24.0;

    /// <summary>
    /// Extra width, in logical pixels, added to the estimated title/keyword text width when a
    /// container's title is treated as a routing obstacle, so a connector crossing the title band keeps
    /// a little clear space beside the text.
    /// </summary>
    private const double TitleTextMargin = 16.0;

    /// <summary>
    /// Resolves the title-band height reserved above a container's children: the node's own
    /// <see cref="LayoutGraphNode.TitleHeight"/> override when set (so a caller can match a specific
    /// theme's actual title-area height, including a keyword line), otherwise
    /// <see cref="DefaultContainerTitleHeight"/>; zero for a node with no label, and zero for a folder
    /// whose name is drawn in the tab (<see cref="FolderTitlePlacement.Tab"/>) unless the caller set an
    /// explicit <see cref="LayoutGraphNode.TitleHeight"/> (for example to reserve a keyword line) or the
    /// folder carries a <see cref="LayoutGraphNode.Keyword"/>, whose line the renderers still draw in the
    /// body so the default reserve is kept to clear it.
    /// </summary>
    /// <param name="node">The container node whose title-band height is resolved.</param>
    /// <returns>The title-band height in logical pixels.</returns>
    private static double ResolveTitleHeight(LayoutGraphNode node)
    {
        if (node.Label is null)
        {
            return 0.0;
        }

        if (node.TitleHeight is null
            && node.Keyword is null
            && BoxMetrics.IsNameInTab(node.Shape, node.Label, node.Appearance))
        {
            return 0.0;
        }

        return node.TitleHeight ?? DefaultContainerTitleHeight;
    }

    /// <summary>
    /// Resolves the total vertical band reserved above a container's children: the label/keyword
    /// title band from <see cref="ResolveTitleHeight"/>, plus — for a <see cref="BoxShape.Folder"/>
    /// container — the folder tab height, since the renderer recesses the title text below the tab
    /// and the container must reserve enough space so nested children never overlap it.
    /// </summary>
    /// <param name="node">The container node whose reserved content offset is resolved.</param>
    /// <returns>The total reserved band height in logical pixels.</returns>
    internal static double ResolveContentOffsetHeight(LayoutGraphNode node)
    {
        var titleHeight = ResolveTitleHeight(node);
        if (node.Shape != BoxShape.Folder)
        {
            return titleHeight;
        }

        var tabHeight = node.FolderTabHeight ?? BoxMetrics.FolderTabHeight(Themes.Light);
        return titleHeight + tabHeight;
    }

    /// <summary>
    /// The leaf-algorithm provider used to resolve the per-scope layout algorithm by identifier. It is
    /// deliberately limited to leaf algorithms (for example <c>layered</c> and <c>containment</c>); this
    /// engine is not registered into it, so recursion always terminates in a bundled leaf algorithm.
    /// </summary>
    private readonly LayoutAlgorithmRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="HierarchicalLayoutAlgorithm"/> class backed by a
    /// default registry of the bundled leaf algorithms (<see cref="LayeredLayoutAlgorithm"/> and
    /// <see cref="ContainmentLayoutAlgorithm"/>).
    /// </summary>
    /// <remarks>
    ///     This convenience constructor lets callers use the engine without assembling a registry. It
    ///     registers only the leaf algorithms, never this engine itself, so a scope that selects an
    ///     unregistered algorithm surfaces a resolution error rather than recursing indefinitely.
    /// </remarks>
    public HierarchicalLayoutAlgorithm()
        : this(new LayoutAlgorithmRegistry()
            .Register(new LayeredLayoutAlgorithm())
            .Register(new ContainmentLayoutAlgorithm()))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HierarchicalLayoutAlgorithm"/> class that resolves
    /// per-scope leaf algorithms from the supplied <paramref name="registry"/>.
    /// </summary>
    /// <param name="registry">
    /// Provider of the leaf algorithms this engine delegates to. It should contain only leaf algorithms
    /// (not this hierarchical engine) so recursion terminates; every algorithm identifier referenced by
    /// the graph or options must be resolvable from it.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="registry"/> is <see langword="null"/>.</exception>
    public HierarchicalLayoutAlgorithm(LayoutAlgorithmRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc/>
    public override string Id => AlgorithmId;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="graph"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when a scope selects a layout algorithm identifier that is not registered.
    /// </exception>
    protected internal override LayoutTree ApplyCore(LayoutGraph graph, LayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(options);

        // The root scope's own explicit overrides (set directly on the graph) win over the supplied
        // options, giving the root the same "own overrides win" treatment every nested scope receives.
        var effective = graph.OverlayOnto(options);
        var withContext = WithRefinementContext(effective);
        var context = withContext.Get(RefinementContext);
        var warningsBefore = context?.RoutingLimitWarnings.Count ?? 0;
        var tree = LayoutScope(graph, withContext);

        // Surface every scope whose routing reached its work budget, so the caller can tell that some
        // connectors were drawn with a reduced routing effort (see RoutingWorkBudget).
        var limited = context?.RoutingLimitWarnings.Skip(warningsBefore).ToList() ?? [];
        return limited.Count == 0 ? tree : tree with { Warnings = [.. tree.Warnings, .. limited] };
    }

    /// <summary>
    /// Lays out a single container scope with the resolved leaf algorithm, recursing into any nested
    /// containers first and composing their sub-layouts into this level's coordinate space.
    /// </summary>
    /// <param name="graph">The container scope to place (the root graph or a node's child subgraph).</param>
    /// <param name="effective">
    /// This scope's cascaded effective options: the parent scope's resolved snapshot overlaid by this
    /// scope's own explicit overrides, per <see cref="PropertyHolder.OverlayOnto"/>.
    /// </param>
    /// <returns>The placed sub-tree for this scope, in local coordinates rooted at the origin.</returns>
    private LayoutTree LayoutScope(LayoutGraph graph, LayoutOptions effective)
    {
        var algo = _registry.Resolve(ResolveLeafAlgorithmId(effective));

        // Flat fast path: when no direct node is a container, delegate straight to the leaf algorithm so
        // the result is byte-for-byte identical to invoking that algorithm directly. This preserves the
        // flat-graph equivalence guarantee: no cloning, no post-processing, no mutation.
        var anyContainer = graph.Nodes.Any(node => node.HasChildren);

        if (!anyContainer)
        {
            return algo.ApplyCore(graph, effective);
        }

        // Each scope that routes connectors spends its own work budget, shared by every pass over the scope
        // (the tight, loose and reference layouts and the ordering proposals) so the passes made last are
        // the first to be reduced, and a heavy scope can neither starve nor be starved by another.
        var budget = new RoutingWorkBudget(DefaultRoutingWorkLimits);

        // A scope inside a scope too large for the full routing effort is drawn in that same oversize
        // diagram, so it takes the reduced effort over from it (the enclosing scope's budget is still in
        // the options this scope inherited). Its own work is not what limited it: the reduction is not
        // reported as a limit of this scope, the hint of the oversize scope speaks for it.
        var enclosing = effective.Get(RoutingBudget);
        if (enclosing is { ForcedReduced: true })
        {
            budget.InheritReduced();
        }
        else if (enclosing is { LeansInnerScopes: true })
        {
            // A scope inside a heavy hierarchy is laid out lean (see RoutingWorkBudget.Lean)
            budget.MarkLean();
        }

        // A scope that routes connectors itself and holds a hierarchy of inner scopes heavy enough that
        // laying each of them out again for every layout of this scope would multiply their passes keeps
        // the full effort for itself and has its inner scopes laid out lean. The footprint is a structural
        // fact of the graph, so the decision depends on the input only.
        if (!budget.ForcedReduced && !budget.Lean)
        {
            var footprint = RoutingFootprintOf(graph, effective.Get(RefinementContext));
            if (footprint.RoutedEdges > 0 && footprint.NestedWeight >= budget.NestedLeanWeight)
            {
                budget.MarkInnerScopesLean();
            }
        }

        var scoped = WithRoutingBudget(effective, budget);

        // The first pass is the engine's ordinary result; the cross-hierarchy ordering refinement (see
        // HierarchicalLayoutAlgorithm.CrossOrdering.cs) only ever replaces it by a measurably better one,
        // and returns it untouched when the scope has nothing to refine.
        var first = LayoutScopePass(graph, algo, scoped);
        var result = RefineCrossOrdering(graph, algo, scoped, first);
        scoped.Get(RefinementContext)?.RecordRoutingBudget(graph, budget);
        return result;
    }

    /// <summary>
    /// Runs one complete hierarchical pass over a scope that has at least one container among its direct
    /// nodes: lays out every container child, places this level over a sized view, composes the
    /// sub-layouts, and routes the cross-container edges.
    /// </summary>
    /// <param name="graph">The container scope to place.</param>
    /// <param name="algo">The leaf algorithm the scope is placed with.</param>
    /// <param name="effective">The scope's cascaded effective options (including any order-rank hint).</param>
    /// <param name="route">
    /// Whether the cross-container edges are routed. A placement-only pass (<see langword="false"/>) is
    /// what the ordering refinement scores candidate orders with before it pays for a routed pass; it is
    /// only valid for a scope without boundary ports.
    /// </param>
    /// <returns>The placed tree and the structural facts the ordering refinement needs.</returns>
    private ScopePass LayoutScopePass(LayoutGraph graph, LayoutAlgorithmBase algo, LayoutOptions effective, bool route = true)
    {
        // Tightening a container to its content plus one ContainerPadding leaves less room beside the first
        // box than the engine's comfort rule asks of a connector that enters the container (a run along the
        // border keeps 2 x clearance from it). The scope is first laid out with every container tight; when
        // a routed connector hugs the border of a container, it is laid out once more with every container
        // a routed connector enters keeping the room it always had (the other containers stay tight), and
        // the scope remembers that choice for its later passes.
        //
        // Tightening is only worth keeping when it does not make the routes worse: a routed pass is also
        // laid out with every container loose (the room the containers always had) and the tight layout is
        // kept only when its connectors cross no more, bend no more, double back no more and run no closer
        // beside one another than the loose layout's. The decision is remembered so the placement-only
        // passes of the ordering refinement see the same container sizes.
        var context = effective.Get(RefinementContext);
        var loose = context?.LooseContainersOf(graph) ?? [];
        var containers = graph.Nodes.Where(node => node.HasChildren).ToHashSet();
        var inherited = effective.Get(LooseContainerNodes);

        // The direct-member containers kept loose: the scope's own comfort fallback plus those an ancestor
        // scope (or this scope's decision) keeps loose.
        HashSet<LayoutGraphNode> DirectLoose(ISet<LayoutGraphNode>? deep)
        {
            var direct = new HashSet<LayoutGraphNode>(loose);
            direct.UnionWith(containers.Where(node => (inherited?.Contains(node) ?? false) || (deep?.Contains(node) ?? false)));
            return direct;
        }

        // A placement-only pass uses the container sizes the last routed pass of the scope settled on.
        var settled = route ? null : context?.SettledLooseNodes(graph);
        if (settled != null)
        {
            return LayoutScopePassOnce(graph, algo, WithLooseNodes(effective, settled), route, DirectLoose(settled), [], []);
        }

        var violators = new HashSet<LayoutGraphNode>();
        var entered = new HashSet<LayoutGraphNode>();
        var enteredDeep = new HashSet<LayoutGraphNode>();
        var pass = LayoutScopePassOnce(graph, algo, effective, route, DirectLoose(null), violators, entered, enteredDeep);

        // A scope too large for the full routing effort, one whose first pass already ran its routing
        // work down, and a lean scope are routed with one pass: the containers a routed connector enters
        // already kept their room (see LayoutScopePassOnce), so a second pass for the connectors that
        // hugged a border would only repeat the most expensive routing of the scope.
        if (violators.Count > 0 && effective.Get(RoutingBudget) is not ({ Tier: not RoutingTier.Full } or { Lean: true }))
        {
            var before = loose.Count;
            loose.UnionWith(entered);
            if (loose.Count != before)
            {
                pass = LayoutScopePassOnce(graph, algo, effective, route, DirectLoose(null), [], []);
            }
        }

        if (!route || pass.RoutedEdges is null || pass.RoutedEdges.Count == 0 || containers.IsSubsetOf(DirectLoose(null)))
        {
            return pass;
        }

        // The tight layout stands without the reference and middle layouts when the scope's routing already
        // ran down its work budget, or the scope is lean: each is a full routed layout of the scope.
        if (effective.Get(RoutingBudget) is { Tier: not RoutingTier.Full } or { Lean: true })
        {
            return pass;
        }

        // The reference: every container of the scope and of every scope below it keeps its former room.
        var everything = new HashSet<LayoutGraphNode>();
        CollectContainers(graph, everything);
        if (inherited != null)
        {
            everything.UnionWith(inherited);
        }

        var reference = LayoutScopePassOnce(graph, algo, WithLooseNodes(effective, everything), route, containers, [], []);
        if (KeepsTightening(pass.Tree, reference.Tree))
        {
            context?.SetSettledLooseNodes(graph, null);
            return pass;
        }

        // Tightening made the routes worse. Before every container keeps its former room, try keeping it only
        // for the containers a routed connector enters (at any depth): the connectors cannot be worse beside
        // a container they do not enter. Keep that when it is no worse than the reference either.
        var middle = new HashSet<LayoutGraphNode>(enteredDeep);
        if (inherited != null)
        {
            middle.UnionWith(inherited);
        }

        if (!everything.IsSubsetOf(middle))
        {
            var middlePass = LayoutScopePassOnce(graph, algo, WithLooseNodes(effective, middle), route, DirectLoose(middle), [], []);
            if (KeepsTightening(middlePass.Tree, reference.Tree))
            {
                context?.SetSettledLooseNodes(graph, middle);
                return middlePass;
            }
        }

        context?.SetSettledLooseNodes(graph, everything);
        return reference;
    }

    /// <summary>
    /// Adds every container (node with children) of <paramref name="graph"/> and of every scope below it to
    /// <paramref name="containers"/>.
    /// </summary>
    /// <param name="graph">The scope.</param>
    /// <param name="containers">Receives the containers.</param>
    private static void CollectContainers(LayoutGraph graph, HashSet<LayoutGraphNode> containers)
    {
        foreach (var node in graph.Nodes)
        {
            if (node.HasChildren)
            {
                containers.Add(node);
                CollectContainers(node.Children, containers);
            }
        }
    }

    /// <summary>
    /// Runs one hierarchical pass of a scope (see <see cref="LayoutScopePass"/>) with the containers in
    /// <paramref name="loose"/> kept at their untrimmed size.
    /// </summary>
    /// <param name="graph">The container scope to place.</param>
    /// <param name="algo">The leaf algorithm the scope is placed with.</param>
    /// <param name="effective">The scope's cascaded effective options (including any order-rank hint).</param>
    /// <param name="route">Whether the cross-container edges are routed.</param>
    /// <param name="loose">The direct-member containers that are not tightened to their content.</param>
    /// <param name="violators">Receives the direct-member containers whose border a routed connector hugs.</param>
    /// <param name="entered">Receives the direct-member containers that hold an endpoint of a routed connector.</param>
    /// <param name="enteredDeep">Receives every container, at any depth, that holds an endpoint of a routed connector.</param>
    /// <returns>The placed tree and the structural facts the ordering refinement needs.</returns>
    private ScopePass LayoutScopePassOnce(
        LayoutGraph graph,
        LayoutAlgorithmBase algo,
        LayoutOptions effective,
        bool route,
        HashSet<LayoutGraphNode> loose,
        HashSet<LayoutGraphNode> violators,
        HashSet<LayoutGraphNode> entered,
        HashSet<LayoutGraphNode>? enteredDeep = null)
    {
        // Hierarchical path (SeparateChildren): recurse into each container child, size it to fit, place
        // this level over a sized view, compose the sub-layouts, and route cross-container edges.
        var subLayouts = new Dictionary<LayoutGraphNode, LayoutTree>();
        var effectiveSize = new Dictionary<LayoutGraphNode, (double Width, double Height)>();

        // A scope with boundary (delegation) ports takes the recursive merge-region path below, which
        // seeds and grows container sizes itself; it keeps the sub-layouts (and their canvas padding)
        // exactly as the leaf algorithm placed them. Every other scope trims each container's interior to
        // its content so the container is that content plus one ContainerPadding.
        var boundaryPorts = HierarchyMergeRegionBuilder.Collect(graph);

        // Map every descendant node to the direct member of this scope that contains it, and every
        // descendant's named port to the node that owns it (needed before placement so edge
        // classification below can decide, purely from graph structure, which edges the leaf algorithm
        // should route locally versus which the router must handle for this scope, resolving a port
        // endpoint to its owning node the same way as a plain node endpoint).
        var descendantToDirect = new Dictionary<LayoutGraphNode, LayoutGraphNode>();
        var portOwner = new Dictionary<LayoutGraphPort, LayoutGraphNode>();
        var nesting = new Dictionary<LayoutGraphNode, (LayoutGraphNode? Parent, int Index)>();
        for (var directIndex = 0; directIndex < graph.Nodes.Count; directIndex++)
        {
            var direct = graph.Nodes[directIndex];
            nesting[direct] = (null, directIndex);
            MapDescendants(direct, direct, descendantToDirect, portOwner, nesting);
        }

        var (leafEdges, routedEdges) = ClassifyEdges(graph, descendantToDirect, portOwner, nesting);
        if (route && routedEdges.Count > 0)
        {
            // A scope with this many connectors over this many nodes is known from its size alone to be
            // too costly for the full routing effort (see RoutingWorkLimits.ReducedWeight).
            effective.Get(RoutingBudget)?.NoteScopeWeight((long)routedEdges.Count * (descendantToDirect.Count + graph.Nodes.Count));
        }

        // Such a scope is laid out and routed once, so every container a routed connector enters keeps the
        // room it always had from the start, instead of being tightened first and given its room back only
        // after the connectors that hugged its border were found by routing the whole scope. A lean scope
        // does the same: it has one routed pass.
        var roomy = route && effective.Get(RoutingBudget) is { ForcedReduced: true } or { Lean: true };
        foreach (var edge in routedEdges)
        {
            foreach (var end in new[] { edge.Source, edge.Target })
            {
                if (end is LayoutGraphNode endNode && descendantToDirect.TryGetValue(endNode, out var owner) && owner.HasChildren)
                {
                    entered.Add(owner);
                    if (roomy)
                    {
                        loose.Add(owner);
                    }
                }

                if (enteredDeep != null && end is LayoutGraphNode deepNode)
                {
                    for (var current = deepNode; current != null; current = nesting.TryGetValue(current, out var link) ? link.Parent : null)
                    {
                        if (current.HasChildren)
                        {
                            enteredDeep.Add(current);
                        }
                    }
                }
            }
        }

        // A container a decorated connector enters needs room for the decoration between its border and
        // the node the connector ends on; the others keep the plain container padding.
        var paddings = boundaryPorts.Count == 0
            ? EntryPaddings(routedEdges, nesting, portOwner, effective.Get(ContainerPaddings))
            : null;
        var childrenEffective = paddings is null ? effective : WithContainerPaddings(effective, paddings);
        var usedPaddings = new Dictionary<LayoutGraphNode, double>();
        LayoutContainerChildren(graph, childrenEffective, subLayouts, effectiveSize, boundaryPorts.Count == 0 ? loose : null, paddings, usedPaddings);

        // An edge between two different direct members is invisible to a leaf algorithm that only sees
        // the sized view, so — when the leaf algorithm is one of the bundled ones that understands
        // layout-only edges — a proxy edge joining the two direct members is added to the view. It
        // steers placement (layering, ordering, gap sizing) but is never drawn: the real connector is
        // routed afterwards between the actual nested endpoints. A custom leaf algorithm gets the view
        // unchanged (backward compatible) and the real connector is still routed to the nested endpoints.
        var addProxies = algo is LayeredLayoutAlgorithm or ContainmentLayoutAlgorithm or AutoLayoutAlgorithm;
        var (view, viewOf, viewPortOf) = BuildSizedView(
            graph, effectiveSize, leafEdges, addProxies ? routedEdges : new List<LayoutGraphEdge>(), descendantToDirect);

        // Place this level with the resolved leaf algorithm over the sized view. The leaf algorithm
        // emits one box per node in Nodes order, so placed boxes align with graph.Nodes by index. The
        // order-rank hint is keyed by the original nodes, so it is translated onto the view's copies.
        var placed = algo.ApplyCore(view, TranslateOrderRanks(effective, viewOf));
        var placedBoxes = placed.Nodes.OfType<LayoutBox>().ToList();
        var placedLines = placed.Nodes.OfType<LayoutLine>().ToList();
        var placedPorts = placed.Nodes.OfType<LayoutPort>().ToList();

        // The leaf pass ran over the sized view graph, whose ports are distinct clone objects from this
        // scope's original graph ports (see BuildSizedView). Translate each placed port's SourcePort
        // back to the original scope's LayoutGraphPort so downstream boundary-port resolution reasons
        // about the same port identity as HierarchyMergeRegionBuilder/BoundaryPortResolver, never the
        // disposable view clone.
        if (viewPortOf.Count > 0)
        {
            var originalPortOf = viewPortOf.ToDictionary(kv => kv.Value, kv => kv.Key);
            placedPorts = placedPorts
                .Select(p => p.SourcePort != null && originalPortOf.TryGetValue(p.SourcePort, out var orig)
                    ? p with { SourcePort = orig }
                    : p)
                .ToList();
        }

        var (composed, indexOf) = ComposeBoxes(graph, placedBoxes, subLayouts, usedPaddings);

        // The composed scope keeps the padding its leaf algorithm gave its own content (the smallest margin
        // of the placed view): the final canvas fit below makes the canvas exactly the composed content plus
        // that padding on every side, whatever the cross-container connectors and labels add to it.
        var fontSize = effective.Get(CoreOptions.AssumedFontSize);
        var leafPadding = CanvasFitter.MeasureMargin(placed, fontSize);
        double? scopePadding = leafPadding is > 0.0 ? leafPadding : null;

        // Resolve any boundary (delegation) ports of this scope's direct-member containers: an edge
        // referencing such a port crosses the container boundary and cannot be routed by an ordinary
        // per-scope leaf pass. Gating on a non-empty discovery keeps every boundary-port-free scope on
        // its existing path.
        if (boundaryPorts.Count == 0)
        {
            // Hard invariant: a hierarchical scope with containers but zero boundary ports keeps its
            // existing leaf-pass + cross-container-router output, byte-for-byte unchanged — except that
            // a pair of horizontally-adjacent sibling containers joined by a fan of parallel
            // cross-container connectors first has the gap between them widened so those connectors get
            // distinct routing lanes. A pair joined by zero or one cross-container edge is never widened,
            // so every existing single-edge/no-edge scope stays byte-identical.
            var (widenedBoxes, widenedLines, widenedPorts, widenedWidth) = WidenSiblingContainerGaps(
                composed, indexOf, routedEdges, descendantToDirect, placedLines, placedPorts, placed.Width);

            var leafCrossLines = route
                ? RouteCrossContainerEdgesCached(graph, routedEdges, widenedBoxes, indexOf, descendantToDirect, nesting, effective, violators)
                : new List<LayoutLine>();
            var leafNodes = new List<LayoutNode>(
                widenedBoxes.Length + widenedLines.Count + widenedPorts.Count + leafCrossLines.Count);
            leafNodes.AddRange(widenedBoxes);
            leafNodes.AddRange(widenedLines);
            leafNodes.AddRange(widenedPorts);
            leafNodes.AddRange(leafCrossLines);
            return new ScopePass(
                CanvasFitter.Fit(
                    new LayoutTree(widenedWidth, placed.Height, leafNodes),
                    fontSize,
                    scopePadding),
                routedEdges,
                descendantToDirect,
                nesting);
        }

        // Deferred (see ROADMAP.md): extend the edge-count-aware sibling-gap widening above to the
        // boundary-port combined-pass path below. That path composes geometry through the recursive
        // merge-region pipeline (MergeRegionGraphAssembler / RunRecursiveWithCascadingSizing /
        // MergeRegionDecomposer), not the ComposeBoxes path the widening pass operates on, so it needs
        // its own separately-designed widening inside the corridor router rather than a post-placement
        // half-plane shift. This is an explicit, disclosed scope reduction, not an oversight.

        // Single combined pass (ELK-style recursive hierarchy handling): assemble the hierarchy-aware
        // graph spanning every nesting level, lay it all out in one coordinated placement (growing each
        // container to fit its placed interior and cascading that growth outward), then project the
        // placed result back into per-scope geometry. The boxes the leaf pass placed above are reused
        // only as styling/interior templates; every placed coordinate and every connector polyline comes
        // from the combined pass, so a fan-in of boundary approaches routes through the corridor router's
        // own orthogonal channels rather than being patched onto a shared anchor.
        var engineDirection = ToEngineDirection(effective.Get(CoreOptions.Direction));

        // The recursive merge-region pipeline's title-vs-side-port reserve is deliberately left at 0
        // (no exclusion) for now: unlike the flat engine, a boundary-port container's own left/right
        // anchor also has to satisfy the hierarchy-crossing dummy's already-delicate cross-axis pinning
        // (see PinIncomingCrossings), and reserving a title band there was found to reintroduce a
        // longer-bend detour in the internal-fan-out boundary-port scenario. See ROADMAP.md for the
        // follow-up to extend the reservation here once that interaction is worked out.
        var region = MergeRegionGraphAssembler.Assemble(graph, boundaryPorts, effectiveSize);
        var placement = RunRecursiveWithCascadingSizing(
            region, effectiveSize, engineDirection, assumedFontSize: 0.0, layerSpacing: effective.Get(CoreOptions.LayerSpacing));
        var decomposed = MergeRegionDecomposer.Decompose(placement, engineDirection, composed);

        var crossLines = RouteCrossContainerEdges(routedEdges, decomposed.Boxes, decomposed.IndexOf, descendantToDirect, nesting, effective);

        var nodes = new List<LayoutNode>(
            decomposed.Boxes.Length + decomposed.Lines.Count + decomposed.Ports.Count + crossLines.Count);
        nodes.AddRange(decomposed.Boxes);
        nodes.AddRange(decomposed.Lines);
        nodes.AddRange(decomposed.Ports);
        nodes.AddRange(crossLines);
        return new ScopePass(
            CanvasFitter.Fit(
                new LayoutTree(decomposed.Width, decomposed.Height, nodes),
                fontSize,
                scopePadding),
            null,
            descendantToDirect,
            nesting);
    }

    /// <summary>
    /// Runs the recursive combined pass over <paramref name="region"/>, then cascades container sizing:
    /// after each placement, any container whose placed interior needs more room than its current
    /// effective size is grown, and — because a grown container enlarges the interior of every level that
    /// encloses it — the pass is re-run until every container's size is stable. The common case where the
    /// first placement already fits every container skips the re-run entirely, mirroring
    /// <see cref="LayeredLayoutAlgorithm"/>'s Fix-5 two-pass growth detection generalized to cascade
    /// across the whole nesting chain.
    /// </summary>
    /// <param name="region">The assembled merge region to place; its levels share <paramref name="effectiveSize"/>.</param>
    /// <param name="effectiveSize">
    /// The mutable effective-size lookup threaded through every level of <paramref name="region"/>;
    /// grown in place as inner content demands more room so the next re-run reserves the larger box.
    /// </param>
    /// <param name="direction">The region's flow direction.</param>
    /// <param name="assumedFontSize">
    /// The <c>CoreOptions.AssumedFontSize</c>-derived font size used to reserve each node's title band
    /// from left/right port placement.
    /// </param>
    /// <param name="layerSpacing">The scope's effective <see cref="CoreOptions.LayerSpacing"/>, the corridor floor of every level.</param>
    /// <returns>The fully-placed, size-stable recursive layout result.</returns>
    private static RecursiveLayoutResult RunRecursiveWithCascadingSizing(
        AssembledMergeRegion region,
        Dictionary<LayoutGraphNode, (double Width, double Height)> effectiveSize,
        LayoutDirection direction,
        double assumedFontSize,
        double layerSpacing)
    {
        var pipeline = LayeredLayoutPipeline.Builder()
            .Direction(direction)
            .Hierarchy(Engine.Layered.HierarchyHandling.Recursive)
            .AddRecursiveStages()
            .Build();

        // Every (container, its placed child level) pair whose interior determines the container's size.
        var sizingPairs = new List<(LayoutGraphNode Container, MergeRegionLevel Child)>();
        CollectSizingPairs(region.Root, sizingPairs);

        var result = pipeline.RunRecursive(region, assumedFontSize, layerSpacing);
        for (var iteration = 0; iteration < MaxSizingIterations; iteration++)
        {
            var grew = false;
            foreach (var (container, child) in sizingPairs)
            {
                var (footprintWidth, footprintHeight) = MergeRegionDecomposer.LevelFootprint(
                    result.Levels[child].Graph, direction);
                var titleHeight = ResolveContentOffsetHeight(container);
                var requiredWidth = footprintWidth + (2 * ContainerPadding);
                var requiredHeight = footprintHeight + (2 * ContainerPadding) + titleHeight;
                var current = effectiveSize.TryGetValue(container, out var size) ? size : (Width: 0.0, Height: 0.0);
                if (requiredWidth > current.Width + SizingTolerance || requiredHeight > current.Height + SizingTolerance)
                {
                    effectiveSize[container] = (
                        Math.Max(current.Width, requiredWidth),
                        Math.Max(current.Height, requiredHeight));
                    grew = true;
                }
            }

            if (!grew)
            {
                break;
            }

            result = pipeline.RunRecursive(region, assumedFontSize, layerSpacing);
        }

        return result;
    }

    /// <summary>
    /// Collects every boundary-port container of <paramref name="level"/> and its descendants, paired
    /// with the child level whose placed footprint sizes it, so cascading sizing can measure each in a
    /// single sweep.
    /// </summary>
    /// <param name="level">The nesting level to walk.</param>
    /// <param name="pairs">The accumulating (container, child level) list, mutated in place.</param>
    private static void CollectSizingPairs(
        MergeRegionLevel level,
        List<(LayoutGraphNode Container, MergeRegionLevel Child)> pairs)
    {
        foreach (var (container, _, child) in level.Children)
        {
            pairs.Add((container, child));
            CollectSizingPairs(child, pairs);
        }
    }

    /// <summary>
    /// Recurses into every container child of <paramref name="graph"/>, laying out its subgraph and
    /// recording both the resulting sub-layout and the container's effective (child-fitting) size.
    /// </summary>
    /// <param name="graph">The scope whose container children are laid out.</param>
    /// <param name="parentEffective">This scope's own already-resolved cascaded effective options.</param>
    /// <param name="subLayouts">Receives each container's placed sub-tree in local coordinates.</param>
    /// <param name="effectiveSize">Receives each container's size enclosing its children plus padding/title.</param>
    /// <param name="loose">
    /// The containers that are not trimmed to their content (their interior keeps the leaf algorithm's own
    /// canvas padding), or <see langword="null"/> to trim none (boundary-port scopes). Every other
    /// container's interior is trimmed to its content before the container padding is added.
    /// </param>
    /// <param name="paddings">The padding each decorated-entry container needs, or <see langword="null"/>.</param>
    /// <param name="usedPaddings">Receives the padding of each container that has more than the plain container padding.</param>
    private void LayoutContainerChildren(
        LayoutGraph graph,
        LayoutOptions parentEffective,
        Dictionary<LayoutGraphNode, LayoutTree> subLayouts,
        Dictionary<LayoutGraphNode, (double Width, double Height)> effectiveSize,
        HashSet<LayoutGraphNode>? loose,
        IReadOnlyDictionary<LayoutGraphNode, double>? paddings,
        Dictionary<LayoutGraphNode, double> usedPaddings)
    {
        foreach (var node in graph.Nodes)
        {
            if (!node.HasChildren)
            {
                continue;
            }

            // Preserve both established, independently-tested conventions: CoreOptions.Algorithm
            // overrides live on the container node itself, while every other CoreOptions property
            // (Direction, EdgeRouting, and so on) lives on the node's Children graph. Overlaying in this
            // order lets either layer's own overrides win over the parent's resolved snapshot, and lets
            // the Children graph's overrides win over the node's own (though the two never collide, since
            // Algorithm is only ever set on the node and every other property only ever on Children).
            var nodeEffective = node.OverlayOnto(parentEffective);
            var childEffective = node.Children.OverlayOnto(nodeEffective);

            var tightened = loose != null && !loose.Contains(node);
            var sub = LayoutContainerScope(node, childEffective, tightened);
            subLayouts[node] = sub;

            // Size the container to enclose its sub-layout plus a padding inset on every side and, when
            // the container is labeled, a title band above the children. A tightened container a decorated
            // connector enters keeps the room that connector's decoration needs.
            var padding = ContainerPadding;
            if (tightened && paddings != null && paddings.TryGetValue(node, out var needed) && needed > ContainerPadding)
            {
                padding = needed;
                usedPaddings[node] = needed;
            }

            var titleHeight = ResolveContentOffsetHeight(node);
            effectiveSize[node] = (
                sub.Width + (2 * padding),
                sub.Height + (2 * padding) + titleHeight);
        }
    }

    /// <summary>
    /// Builds an internal, side-effect-free sized view of <paramref name="graph"/>: the same nodes in
    /// the same order (container nodes carrying their effective size, leaves their own size, and each
    /// direct member's named <see cref="LayoutGraphNode.Ports"/> copied onto its view counterpart),
    /// only the edges in <paramref name="leafEdges"/> — those between two distinct direct members that
    /// neither touch a box also involved in a cross-container edge, per <see cref="ClassifyEdges"/>,
    /// including any such edge whose endpoint is a named <see cref="LayoutGraphPort"/> rather than the
    /// node itself. Every other edge (genuine cross-container edges, and any direct-member edge that
    /// shares a box with one) is routed by this scope's router instead, so a box that receives both
    /// kinds of connector has every one of its anchors allocated by a single coordinated pass. The
    /// scope's own cascaded options are carried separately as the caller's <c>effective</c> snapshot
    /// rather than propagated onto this view, since every leaf algorithm resolves such properties from
    /// the options it is invoked with.
    /// </summary>
    /// <param name="graph">The caller's scope, which is never mutated.</param>
    /// <param name="effectiveSize">Effective sizes for the container nodes of this scope.</param>
    /// <param name="leafEdges">The edges this scope's leaf algorithm should route locally.</param>
    /// <param name="proxyEdges">
    /// The routed edges (see <see cref="ClassifyEdges"/>) for which a layout-only proxy edge between the
    /// two endpoints' direct members is added to the view, so the leaf algorithm's placement accounts
    /// for them; empty when the leaf algorithm does not understand layout-only edges.
    /// </param>
    /// <param name="descendantToDirect">Map from every descendant node to the direct member that owns it.</param>
    /// <returns>
    /// The sized view graph, a map from each original node to its view counterpart, and a map from each
    /// original port to its view counterpart (surfaced so callers can translate a placed
    /// <see cref="LayoutPort"/>'s <see cref="LayoutPort.SourcePort"/> back to the original scope's port
    /// identity once the leaf pass has run over the view).
    /// </returns>
    private static (
        LayoutGraph View,
        Dictionary<LayoutGraphNode, LayoutGraphNode> ViewOf,
        Dictionary<LayoutGraphPort, LayoutGraphPort> ViewPortOf) BuildSizedView(
        LayoutGraph graph,
        Dictionary<LayoutGraphNode, (double Width, double Height)> effectiveSize,
        HashSet<LayoutGraphEdge> leafEdges,
        List<LayoutGraphEdge> proxyEdges,
        Dictionary<LayoutGraphNode, LayoutGraphNode> descendantToDirect)
    {
        var view = new LayoutGraph();

        var viewOf = new Dictionary<LayoutGraphNode, LayoutGraphNode>(graph.Nodes.Count);
        var viewPortOf = new Dictionary<LayoutGraphPort, LayoutGraphPort>();
        foreach (var node in graph.Nodes)
        {
            var (width, height) = effectiveSize.TryGetValue(node, out var size)
                ? size
                : (node.Width, node.Height);
            var viewNode = view.AddNode(node.Id, width, height);
            viewNode.Label = node.Label;
            viewNode.Shape = node.Shape;
            viewNode.Keyword = node.Keyword;
            viewNode.Compartments = node.Compartments;
            viewNode.RoundedCornerRadius = node.RoundedCornerRadius;
            viewNode.FolderTabWidth = node.FolderTabWidth;
            viewNode.FolderTabHeight = node.FolderTabHeight;
            viewNode.Appearance = node.Appearance;
            viewOf[node] = viewNode;

            // Copy named ports too, so a same-scope port edge classified into leafEdges (per
            // ClassifyEdges — a port edge can only reach leafEdges when its owning node is itself a
            // direct member of this scope) can be faithfully reproduced on the view graph and reach
            // the leaf algorithm's own port-aware endpoint resolution unchanged.
            if (!node.HasPorts)
            {
                continue;
            }

            foreach (var port in node.Ports.Ports)
            {
                var viewPort = viewNode.Ports.AddPort(port.Id);
                viewPort.ExternalLabel = port.ExternalLabel;
                viewPort.InternalLabel = port.InternalLabel;
                viewPort.Appearance = port.Appearance;
                viewPortOf[port] = viewPort;
            }
        }

        var viewEdgeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in graph.Edges)
        {
            // Only edges classified for local routing are placed by the leaf algorithm here; every
            // other edge (cross-container, or a direct-member edge promoted alongside one) is routed
            // separately after composition, by this scope's router.
            if (!leafEdges.Contains(edge))
            {
                continue;
            }

            if (TryMapConnectable(edge.Source, viewOf, viewPortOf, out var viewSource) &&
                TryMapConnectable(edge.Target, viewOf, viewPortOf, out var viewTarget))
            {
                var viewEdge = view.AddEdge(edge.Id, viewSource, viewTarget);
                viewEdgeIds.Add(edge.Id);
                viewEdge.TargetEnd = edge.TargetEnd;
                viewEdge.LineStyle = edge.LineStyle;
                viewEdge.Label = edge.Label;
                viewEdge.SourceEndLabel = edge.SourceEndLabel;
                viewEdge.TargetEndLabel = edge.TargetEndLabel;
            }
        }

        // Layout-only proxy edges: one per routed edge whose endpoints live under two different direct
        // members, joining those direct members. Each is marked layout-only by object identity (see
        // MarkLayoutOnly), so it is recognized (and never drawn) by the bundled leaf algorithms and
        // cannot collide with any caller-chosen edge id; its own id is a unique, non-reserved name
        // that never surfaces because the edge is never emitted.
        foreach (var edge in proxyEdges)
        {
            if (!descendantToDirect.TryGetValue((LayoutGraphNode)edge.Source, out var sourceDirect) ||
                !descendantToDirect.TryGetValue((LayoutGraphNode)edge.Target, out var targetDirect) ||
                ReferenceEquals(sourceDirect, targetDirect) ||
                !viewOf.TryGetValue(sourceDirect, out var viewSourceDirect) ||
                !viewOf.TryGetValue(targetDirect, out var viewTargetDirect))
            {
                continue;
            }

            var proxyId = "layout-only:" + edge.Id;
            var suffix = 0;
            while (!viewEdgeIds.Add(proxyId))
            {
                suffix++;
                proxyId = "layout-only:" + edge.Id + "#" + suffix.ToString(CultureInfo.InvariantCulture);
            }

            MarkLayoutOnly(view.AddEdge(proxyId, viewSourceDirect, viewTargetDirect));
        }

        return (view, viewOf, viewPortOf);
    }

    /// <summary>
    /// Resolves an original-graph edge endpoint (a node or one of its named ports) to its counterpart
    /// on the sized view graph built by <see cref="BuildSizedView"/>.
    /// </summary>
    /// <param name="connectable">The original edge endpoint to resolve.</param>
    /// <param name="viewOf">Map from each original direct-member node to its view counterpart.</param>
    /// <param name="viewPortOf">Map from each original port to its copied view counterpart.</param>
    /// <param name="mapped">The resolved view endpoint, when resolution succeeds.</param>
    /// <returns><see langword="true"/> when the endpoint resolves to a view node or view port.</returns>
    private static bool TryMapConnectable(
        ILayoutConnectable connectable,
        Dictionary<LayoutGraphNode, LayoutGraphNode> viewOf,
        Dictionary<LayoutGraphPort, LayoutGraphPort> viewPortOf,
        out ILayoutConnectable mapped)
    {
        switch (connectable)
        {
            case LayoutGraphNode node when viewOf.TryGetValue(node, out var viewNode):
                mapped = viewNode;
                return true;

            case LayoutGraphPort port when viewPortOf.TryGetValue(port, out var viewPort):
                mapped = viewPort;
                return true;

            default:
                mapped = null!;
                return false;
        }
    }

    /// <summary>
    /// Composes the placed boxes for this level: each container box receives its recursively laid-out
    /// children, translated from their local origin into the box's padded (and, when labeled,
    /// title-offset) interior.
    /// </summary>
    /// <param name="graph">The scope whose nodes align with <paramref name="placedBoxes"/> by index.</param>
    /// <param name="placedBoxes">The leaf algorithm's placed boxes, one per node in input order.</param>
    /// <param name="subLayouts">The recursively laid-out sub-tree for each container node.</param>
    /// <param name="usedPaddings">The padding of each container that has more than the plain container padding.</param>
    /// <returns>The composed boxes and a map from each node to its positional index.</returns>
    private static (LayoutBox[] Composed, Dictionary<LayoutGraphNode, int> IndexOf) ComposeBoxes(
        LayoutGraph graph,
        List<LayoutBox> placedBoxes,
        Dictionary<LayoutGraphNode, LayoutTree> subLayouts,
        Dictionary<LayoutGraphNode, double> usedPaddings)
    {
        var count = graph.Nodes.Count;
        var indexOf = new Dictionary<LayoutGraphNode, int>(count);
        var composed = new LayoutBox[count];
        for (var i = 0; i < count; i++)
        {
            var node = graph.Nodes[i];
            indexOf[node] = i;
            var box = placedBoxes[i];

            if (subLayouts.TryGetValue(node, out var sub))
            {
                // Offset the nested content to the container's padded interior, below any title band.
                var titleHeight = ResolveContentOffsetHeight(node);
                var padding = usedPaddings.TryGetValue(node, out var used) ? used : ContainerPadding;
                var offsetX = box.X + padding;
                var offsetY = box.Y + padding + titleHeight;
                var children = new List<LayoutNode>(sub.Nodes.Count);
                foreach (var childNode in sub.Nodes)
                {
                    children.Add(Translate(childNode, offsetX, offsetY));
                }

                composed[i] = box with { Children = children, CenterTitle = false };
            }
            else
            {
                composed[i] = box;
            }
        }

        return (composed, indexOf);
    }

    /// <summary>
    /// Routes every edge this scope's router is responsible for — genuine cross-container edges, any
    /// direct-member edge promoted alongside one by <see cref="ClassifyEdges"/> because it shares a
    /// box with a cross-container edge, and ancestor-declared edges between two nodes nested under the
    /// same direct member — in one batch call. Each connector is anchored on the actual placed box of
    /// its source and target (the nested nodes themselves, at any depth, or the container itself when the
    /// edge names the container) and routed around every unrelated box; only the containers that enclose
    /// its own two endpoints are opened up (title band plus children) so the connector can cross their
    /// walls cleanly, while every other container's border keeps the same wall keep-out so a connector
    /// does not skirt it at the bare obstacle clearance.
    /// </summary>
    /// <param name="routedEdges">The edges this scope must route, per <see cref="ClassifyEdges"/>.</param>
    /// <param name="composed">The composed top-level boxes of this scope, aligned with its nodes by index.</param>
    /// <param name="indexOf">Map from each direct-member node to its positional index in <paramref name="composed"/>.</param>
    /// <param name="descendantToDirect">Map from every descendant node to the direct member that owns it.</param>
    /// <param name="placement">Parent and index-in-parent of every node of this scope's subtree.</param>
    /// <param name="effective">This scope's cascaded effective options, supplying <see cref="CoreOptions.EdgeRouting"/>.</param>
    /// <param name="violators">
    /// Optional. Receives the direct-member containers of this scope whose own border a routed connector
    /// hugs (see <see cref="CollectWallHuggingContainers"/>).
    /// </param>
    /// <returns>One routed line per edge in <paramref name="routedEdges"/>.</returns>
    private static List<LayoutLine> RouteCrossContainerEdges(
        List<LayoutGraphEdge> routedEdges,
        LayoutBox[] composed,
        Dictionary<LayoutGraphNode, int> indexOf,
        Dictionary<LayoutGraphNode, LayoutGraphNode> descendantToDirect,
        Dictionary<LayoutGraphNode, (LayoutGraphNode? Parent, int Index)> placement,
        LayoutOptions effective,
        HashSet<LayoutGraphNode>? violators = null)
    {
        var routeOptions = new ConnectorRouteOptions(effective.Get(CoreOptions.EdgeRouting));
        var fontSize = effective.Get(CoreOptions.AssumedFontSize);

        // Collect every edge this scope must route into one list of Connections, then route them all in
        // a single batch call. Routing them independently (one ConnectorRouter.Route call per edge)
        // would let separate edges that converge on the same box face pick colliding anchors, and
        // separate edges on similar paths collapse onto the same corridor — the batch routing spreads
        // shared-face anchors and steers later connectors around earlier ones.
        var connections = new List<Connection>(routedEdges.Count);
        var obstacleSets = new List<IReadOnlyList<LayoutBox>>(routedEdges.Count);
        var wallSets = new List<IReadOnlyList<RouterWall>>(routedEdges.Count);
        var enclosingSets = new List<List<(LayoutBox Box, LayoutGraphNode Node)>>(routedEdges.Count);
        var fixedAnchors = new List<ConnectorRouter.FixedAnchor>();
        var existingLineSets = new List<IReadOnlyList<LayoutLine>>(routedEdges.Count);
        foreach (var edge in routedEdges)
        {
            // ClassifyEdges only ever adds edges with LayoutGraphNode endpoints to routedEdges — a
            // genuine cross-container edge whose endpoint is a named LayoutGraphPort is rejected with
            // a NotSupportedException before it can reach this router, since Connection/ConnectorRouter
            // are box-only and have no port concept.
            var sourceNode = (LayoutGraphNode)edge.Source;
            var targetNode = (LayoutGraphNode)edge.Target;

            // Anchor on the actual placed box of each endpoint — the nested node itself, not the
            // top-level container that holds it.
            var (from, fromAncestors) = ResolveEndpointBox(sourceNode, composed, indexOf, descendantToDirect, placement);
            var (to, toAncestors) = ResolveEndpointBox(targetNode, composed, indexOf, descendantToDirect, placement);
            if (ReferenceEquals(from, to))
            {
                continue;
            }

            // The connector may cross the walls of the containers that hold its own endpoints, so those
            // ancestor containers are expanded (title band plus children) instead of being one solid
            // obstacle; every other box stays whole.
            var expanded = new List<(LayoutBox Box, LayoutGraphNode Node)>(fromAncestors);
            foreach (var ancestor in toAncestors.Where(a => !expanded.Exists(e => ReferenceEquals(e.Box, a.Box))))
            {
                expanded.Add(ancestor);
            }

            var obstacles = new List<LayoutBox>(composed.Length);
            foreach (var box in composed)
            {
                AddObstacle(box, expanded, fontSize, obstacles);
            }

            // The enclosing containers' borders may be crossed but must not be run along or bent against:
            // each becomes a keep-out strip the router honors (see OrthogonalEdgeRouter's RouterWall).
            var walls = new List<RouterWall>(expanded.Count * 4);
            foreach (var (container, _) in expanded)
            {
                AddWalls(container, walls);
            }

            // Every other container the route could pass gets the same keep-out: a connector that skirts
            // an unrelated container's border (it never crosses it) must stay clear of it as well, instead
            // of running a long way parallel to it at the bare obstacle clearance.
            foreach (var box in composed)
            {
                AddUnrelatedWalls(box, expanded, walls);
            }

            connections.Add(new Connection(from, to, edge.TargetEnd, edge.LineStyle, edge.Label, edge.SourceEndLabel, edge.TargetEndLabel));
            obstacleSets.Add(obstacles);
            wallSets.Add(walls);
            enclosingSets.Add(expanded);

            // The interior connectors that end on either endpoint box (the lines its own enclosing scope
            // already routed to it, which live in the parent container box's children) are crossing costs
            // for this connector: it should reach its endpoint without cutting across them where it can.
            var interiorLines = new List<LayoutLine>();
            AddIncidentInteriorLines(from, fromAncestors, interiorLines);
            AddIncidentInteriorLines(to, toAncestors, interiorLines);
            existingLineSets.Add(interiorLines);
            AddOccupiedAnchors(from, fromAncestors, fixedAnchors);
            AddOccupiedAnchors(to, toAncestors, fixedAnchors);
        }

        var routed = ConnectorRouter.RouteBatch(
            connections,
            routeOptions,
            index => obstacleSets[index],
            fixedAnchors,
            penalizeLongEndSegments: true,
            profile: new ConnectorRouter.RoutingProfile(
                index => wallSets[index],
                index => existingLineSets[index],
                effective.Get(RoutingBudget)));

        if (violators != null)
        {
            CollectWallHuggingContainers(routed, enclosingSets, indexOf, routeOptions.Clearance, violators);
        }

        return new List<LayoutLine>(routed);
    }

    /// <summary>
    /// Finds the direct-member containers of this scope whose own border a routed connector runs along or
    /// bends against closer than the comfort clearance (see <see cref="ConnectorRouter.HasWallViolation"/>).
    /// A container's border can only be hugged when it holds less room beside its first box than the
    /// connector needs, which is what tightening a container to its content may cause.
    /// </summary>
    /// <param name="routed">The routed lines, one per connection.</param>
    /// <param name="enclosingSets">For each connection, the containers (and nodes) enclosing its two endpoints.</param>
    /// <param name="indexOf">Map from each direct-member node to its positional index.</param>
    /// <param name="clearance">The router's obstacle clearance.</param>
    /// <param name="violators">Receives the direct-member containers whose border is hugged.</param>
    private static void CollectWallHuggingContainers(
        IReadOnlyList<LayoutLine> routed,
        List<List<(LayoutBox Box, LayoutGraphNode Node)>> enclosingSets,
        Dictionary<LayoutGraphNode, int> indexOf,
        double clearance,
        HashSet<LayoutGraphNode> violators)
    {
        var walls = new List<RouterWall>(6);
        for (var i = 0; i < routed.Count; i++)
        {
            foreach (var (container, node) in enclosingSets[i])
            {
                if (!indexOf.ContainsKey(node) || violators.Contains(node))
                {
                    continue;
                }

                walls.Clear();
                AddWalls(container, walls);
                if (ConnectorRouter.HasWallViolation(routed[i].Waypoints, walls, clearance))
                {
                    violators.Add(node);
                }
            }
        }
    }

    /// <summary>
    /// Adds the border walls of every container that is not one of the connector's own enclosing
    /// (expanded) containers, descending through the expanded ones to reach their children. An unrelated
    /// container is one solid obstacle (its content is unreachable), so only its own border needs a
    /// keep-out strip; leaf boxes keep their ordinary obstacle clearance.
    /// </summary>
    /// <param name="box">The box to examine.</param>
    /// <param name="expanded">The connector's enclosing containers, which are opened up and already walled.</param>
    /// <param name="walls">The accumulating wall list.</param>
    private static void AddUnrelatedWalls(
        LayoutBox box,
        List<(LayoutBox Box, LayoutGraphNode Node)> expanded,
        List<RouterWall> walls)
    {
        if (expanded.Exists(e => ReferenceEquals(e.Box, box)))
        {
            foreach (var child in box.Children.OfType<LayoutBox>())
            {
                AddUnrelatedWalls(child, expanded, walls);
            }

            return;
        }

        if (box.Children.OfType<LayoutBox>().Any())
        {
            AddWalls(box, walls);
        }
    }

    /// <summary>
    /// Adds the border walls of <paramref name="container"/> as keep-out strips. A folder's top border
    /// follows its real outline: the tab's top edge over the tab, the body's top edge beside it.
    /// </summary>
    /// <param name="container">An enclosing container box of the connector's endpoints.</param>
    /// <param name="walls">The accumulating wall list.</param>
    private static void AddWalls(LayoutBox container, List<RouterWall> walls)
    {
        var top = container.Y;
        var bottom = container.Y + container.Height;
        var left = container.X;
        var right = container.X + container.Width;
        var tabWidth = 0.0;
        var tabHeight = 0.0;
        if (container.Shape == BoxShape.Folder)
        {
            tabWidth = Math.Min(container.Width, ConnectorRouter.FolderGeometry.ResolveFolderTabWidth(container));
            tabHeight = Math.Min(container.Height, ConnectorRouter.FolderGeometry.ResolveFolderTabHeight(container));
        }

        walls.Add(new RouterWall(IsVertical: true, left, top, bottom, OutwardIsPositive: false));
        walls.Add(new RouterWall(IsVertical: true, right, top + tabHeight, bottom, OutwardIsPositive: true));
        walls.Add(new RouterWall(IsVertical: false, bottom, left, right, OutwardIsPositive: true));
        if (tabWidth > 0.0 && tabHeight > 0.0)
        {
            walls.Add(new RouterWall(IsVertical: false, top, left, left + tabWidth, OutwardIsPositive: false));
            walls.Add(new RouterWall(IsVertical: false, top + tabHeight, left + tabWidth, right, OutwardIsPositive: false));
        }
        else
        {
            walls.Add(new RouterWall(IsVertical: false, top, left, right, OutwardIsPositive: false));
        }
    }

    /// <summary>
    /// Collects, for a nested endpoint box, the interior connectors of its enclosing scope that end on it
    /// (those lines live in the parent container box's children — the same lines
    /// <see cref="AddOccupiedAnchors"/> reads the taken anchors from). They are what the cross-hierarchy
    /// connector landing on the same box has to avoid crossing, so the router receives them as crossing
    /// costs. Lines of the container that do not touch the endpoint are left out: reaching a box deep in a
    /// dense interior rarely has an uncrossed route, and including them would make the router's search
    /// far more expensive.
    /// </summary>
    /// <param name="endpoint">The endpoint's placed box.</param>
    /// <param name="ancestors">The container boxes enclosing the endpoint, outermost first.</param>
    /// <param name="lines">The accumulating list of interior lines.</param>
    private static void AddIncidentInteriorLines(
        LayoutBox endpoint,
        List<(LayoutBox Box, LayoutGraphNode Node)> ancestors,
        List<LayoutLine> lines)
    {
        if (ancestors.Count == 0)
        {
            return;
        }

        foreach (var line in ancestors[^1].Box.Children.OfType<LayoutLine>())
        {
            if (line.Waypoints.Count > 0
                && (ConnectorRouter.TryResolveFaceAnchor(endpoint, line.Waypoints[0], out _)
                    || ConnectorRouter.TryResolveFaceAnchor(endpoint, line.Waypoints[^1], out _))
                && !lines.Contains(line))
            {
                lines.Add(line);
            }
        }
    }

    /// <summary>
    /// Records, for a nested endpoint box, the face anchors already taken by the connectors its own
    /// enclosing scope routed to it (those lines live in the parent container box's children), so the
    /// cross-hierarchy connector landing on the same box does not sit on top of one of them.
    /// </summary>
    /// <param name="endpoint">The endpoint's placed box.</param>
    /// <param name="ancestors">The container boxes enclosing the endpoint, outermost first.</param>
    /// <param name="fixedAnchors">The accumulating occupied-anchor list.</param>
    private static void AddOccupiedAnchors(
        LayoutBox endpoint,
        List<(LayoutBox Box, LayoutGraphNode Node)> ancestors,
        List<ConnectorRouter.FixedAnchor> fixedAnchors)
    {
        if (ancestors.Count == 0)
        {
            return;
        }

        var endpoints = ancestors[^1].Box.Children.OfType<LayoutLine>()
            .Where(l => l.Waypoints.Count > 0)
            .SelectMany(l => new[] { l.Waypoints[0], l.Waypoints[^1] });
        foreach (var point in endpoints)
        {
            if (ConnectorRouter.TryResolveFaceAnchor(endpoint, point, out var anchor))
            {
                fixedAnchors.Add(anchor);
            }
        }
    }

    /// <summary>
    /// Resolves the placed box of <paramref name="node"/> — a direct member or a node nested at any
    /// depth — by walking down from its direct member's composed box through each level's child boxes,
    /// which are index-aligned with that level's graph nodes.
    /// </summary>
    /// <param name="node">The endpoint node to resolve.</param>
    /// <param name="composed">The composed top-level boxes of this scope, aligned with its nodes by index.</param>
    /// <param name="indexOf">Map from each direct-member node to its positional index in <paramref name="composed"/>.</param>
    /// <param name="descendantToDirect">Map from every descendant node to the direct member that owns it.</param>
    /// <param name="placement">Parent and index-in-parent of every node of this scope's subtree.</param>
    /// <returns>
    /// The endpoint's placed box and, from the direct member downward, the container boxes (with their
    /// nodes) that enclose it. If a level cannot be resolved the deepest resolvable box is returned, so
    /// the connector degrades to the old container-anchored behavior instead of failing.
    /// </returns>
    private static (LayoutBox Box, List<(LayoutBox Box, LayoutGraphNode Node)> Ancestors) ResolveEndpointBox(
        LayoutGraphNode node,
        LayoutBox[] composed,
        Dictionary<LayoutGraphNode, int> indexOf,
        Dictionary<LayoutGraphNode, LayoutGraphNode> descendantToDirect,
        Dictionary<LayoutGraphNode, (LayoutGraphNode? Parent, int Index)> placement)
    {
        // Path from the direct member (first) down to the endpoint (last).
        var path = new List<LayoutGraphNode>();
        for (LayoutGraphNode? current = node; current != null; current = placement[current].Parent)
        {
            path.Add(current);
        }

        path.Reverse();

        var box = composed[indexOf[descendantToDirect[node]]];
        var ancestors = new List<(LayoutBox Box, LayoutGraphNode Node)>();
        for (var level = 1; level < path.Count; level++)
        {
            var parent = path[level - 1];
            var childBoxes = box.Children.OfType<LayoutBox>().ToList();
            var childIndex = placement[path[level]].Index;
            if (childBoxes.Count != parent.Children.Nodes.Count || childIndex >= childBoxes.Count)
            {
                break;
            }

            ancestors.Add((box, parent));
            box = childBoxes[childIndex];
        }

        return (box, ancestors);
    }

    /// <summary>
    /// Adds <paramref name="box"/> to <paramref name="obstacles"/> as a solid obstacle, unless it is one
    /// of the <paramref name="expanded"/> endpoint-ancestor containers, in which case only its title
    /// text (a thin pseudo-box centered in the title band, as wide as the estimated title/keyword text)
    /// is added and its child boxes are visited recursively instead. Keeping the obstacle as narrow as
    /// the text lets a connector enter or leave the container through the title band beside the text
    /// rather than being forced around the whole container.
    /// </summary>
    /// <param name="box">The box to add or expand.</param>
    /// <param name="expanded">The containers (and their nodes) enclosing the connector's endpoints.</param>
    /// <param name="fontSize">The assumed font size used to estimate the title text width.</param>
    /// <param name="obstacles">The accumulating obstacle list.</param>
    private static void AddObstacle(
        LayoutBox box,
        List<(LayoutBox Box, LayoutGraphNode Node)> expanded,
        double fontSize,
        List<LayoutBox> obstacles)
    {
        var index = expanded.FindIndex(e => ReferenceEquals(e.Box, box));
        if (index < 0)
        {
            obstacles.Add(box);
            return;
        }

        var node = expanded[index].Node;
        var band = Math.Min(ResolveContentOffsetHeight(node), box.Height);
        if (band > 0.0)
        {
            var textWidth = Math.Max(
                node.Label is null ? 0.0 : ConnectorLabelPlacer.EstimateLabelWidth(node.Label, fontSize),
                node.Keyword is null ? 0.0 : ConnectorLabelPlacer.EstimateLabelWidth(node.Keyword, fontSize));
            var width = Math.Min(box.Width, textWidth + TitleTextMargin);
            obstacles.Add(new LayoutBox(
                box.X + ((box.Width - width) / 2.0), box.Y, width, band, null, box.Depth, BoxShape.Rectangle, [], []));
        }

        // A folder's tab protrudes above the body's top edge at the top-left; keep connectors from
        // slicing through it.
        if (box.Shape == BoxShape.Folder)
        {
            var tabWidth = Math.Min(box.Width, ConnectorRouter.FolderGeometry.ResolveFolderTabWidth(box));
            var tabHeight = Math.Min(box.Height, ConnectorRouter.FolderGeometry.ResolveFolderTabHeight(box));
            if (tabWidth > 0.0 && tabHeight > 0.0)
            {
                obstacles.Add(new LayoutBox(
                    box.X, box.Y, tabWidth, tabHeight, null, box.Depth, BoxShape.Rectangle, [], []));
            }
        }

        foreach (var child in box.Children.OfType<LayoutBox>())
        {
            AddObstacle(child, expanded, fontSize, obstacles);
        }
    }

    /// <summary>
    /// Widens the horizontal gap between horizontally-adjacent sibling container boxes that a fan of
    /// parallel cross-container connectors must route through, so those connectors get distinct
    /// orthogonal lanes instead of being crushed into one narrow channel.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     The leaf algorithm sizes the gap between two placed sibling boxes from its own internal
    ///     spacing, which is unaware of the cross-container edges this scope's router
    ///     (<see cref="RouteCrossContainerEdges"/>) is about to fan through that gap — those edges live
    ///     only in <paramref name="routedEdges"/>, never in the sized view the leaf pass laid out. This
    ///     pass closes that gap: it counts the cross-container edges between each pair of direct-member
    ///     containers, and for any pair placed side by side (their vertical extents overlap and no third
    ///     box lies between them) widens their separating gap to the same corridor width the layered
    ///     pipeline reserves for that many parallel edges, via the shared
    ///     <see cref="EdgeCountGapWidener"/>. A pair joined by zero or one cross-container edge is never
    ///     widened, so a scope without a genuine fan is returned byte-for-byte unchanged.
    ///     </para>
    ///     <para>
    ///     Widening is applied as a rigid half-plane shift: inserting extra space at a vertical cut line
    ///     translates every box, line, and port at or right of that cut by the same amount, which keeps
    ///     all already-placed geometry (container interiors, internal leaf connectors, ports) internally
    ///     consistent and non-overlapping. This is sound for the flat side-by-side sibling arrangement
    ///     this no-boundary-port path produces; a box straddling a cut line is not expected there and is
    ///     the reason the combined-pass boundary-port path is handled separately (see the caller's
    ///     deferral note referencing ROADMAP.md).
    ///     </para>
    /// </remarks>
    /// <param name="composed">The composed top-level boxes of this scope, aligned with its nodes by index.</param>
    /// <param name="indexOf">Map from each direct-member node to its positional index in <paramref name="composed"/>.</param>
    /// <param name="routedEdges">The cross-container edges this scope routes, per <see cref="ClassifyEdges"/>.</param>
    /// <param name="descendantToDirect">Map from every descendant node to the direct member that owns it.</param>
    /// <param name="placedLines">The leaf pass's placed connector lines for this scope.</param>
    /// <param name="placedPorts">The leaf pass's placed ports for this scope.</param>
    /// <param name="width">The scope's current total width, in logical pixels, before any widening.</param>
    /// <returns>
    /// The (possibly) widened boxes, lines, and ports, plus the scope's total width grown by the sum of
    /// every inserted gap. When no pair qualifies for widening, the inputs are returned unchanged.
    /// </returns>
    private static (LayoutBox[] Composed, List<LayoutLine> Lines, List<LayoutPort> Ports, double Width) WidenSiblingContainerGaps(
        LayoutBox[] composed,
        Dictionary<LayoutGraphNode, int> indexOf,
        List<LayoutGraphEdge> routedEdges,
        Dictionary<LayoutGraphNode, LayoutGraphNode> descendantToDirect,
        List<LayoutLine> placedLines,
        List<LayoutPort> placedPorts,
        double width)
    {
        // Count the cross-container edges between each unordered pair of direct-member containers,
        // resolving each endpoint to the direct member that owns it exactly as the router does.
        var pairCounts = new Dictionary<(int First, int Second), int>();
        foreach (var edge in routedEdges)
        {
            var sourceDirect = descendantToDirect[(LayoutGraphNode)edge.Source];
            var targetDirect = descendantToDirect[(LayoutGraphNode)edge.Target];
            var s = indexOf[sourceDirect];
            var t = indexOf[targetDirect];
            if (s == t)
            {
                continue;
            }

            var key = s < t ? (s, t) : (t, s);
            pairCounts.TryGetValue(key, out var existing);
            pairCounts[key] = existing + 1;
        }

        // Collect a horizontal cut for every side-by-side pair whose connector fan needs more room than
        // the leaf pass already left. Each cut records the original left edge of the right box and the
        // extra width to insert there.
        var cuts = new List<(double CutX, double Extra)>();
        foreach (var ((first, second), count) in pairCounts)
        {
            if (count <= 1)
            {
                continue;
            }

            var boxA = composed[first];
            var boxB = composed[second];

            // Require a genuine side-by-side placement: the two boxes' vertical extents overlap and one
            // sits entirely to the left of the other with a non-negative gap between them.
            var verticalOverlap = boxA.Y < boxB.Y + boxB.Height && boxB.Y < boxA.Y + boxA.Height;
            if (!verticalOverlap)
            {
                continue;
            }

            var left = boxA.X <= boxB.X ? boxA : boxB;
            var right = boxA.X <= boxB.X ? boxB : boxA;
            var gap = right.X - (left.X + left.Width);
            if (gap < 0.0)
            {
                continue;
            }

            // Skip the pair when a third box lies horizontally between them in the same vertical band:
            // the two are not immediate neighbors, so inserting space at the right box would not widen
            // the true channel the fan routes through.
            var hasIntervening = false;
            for (var k = 0; k < composed.Length; k++)
            {
                if (k == first || k == second)
                {
                    continue;
                }

                var other = composed[k];
                var otherVerticalOverlap = other.Y < right.Y + right.Height && right.Y < other.Y + other.Height;
                if (otherVerticalOverlap &&
                    other.X >= left.X + left.Width &&
                    other.X + other.Width <= right.X)
                {
                    hasIntervening = true;
                    break;
                }
            }

            if (hasIntervening)
            {
                continue;
            }

            var extra = EdgeCountGapWidener.Widen(gap, count) - gap;
            if (extra > 0.0)
            {
                cuts.Add((right.X, extra));
            }
        }

        // No qualifying pair: return the inputs unchanged so the scope stays byte-identical.
        if (cuts.Count == 0)
        {
            return (composed, placedLines, placedPorts, width);
        }

        // Apply every cut as a rigid half-plane shift: an element is pushed right by the summed extra of
        // all cuts at or left of its own left edge. Using each element's leftmost coordinate against the
        // original (pre-shift) cut positions makes overlapping cuts compose additively and deterministic.
        double ShiftFor(double leftX) => cuts.Where(cut => cut.CutX <= leftX).Sum(cut => cut.Extra);

        var shiftedBoxes = new LayoutBox[composed.Length];
        for (var i = 0; i < composed.Length; i++)
        {
            var shift = ShiftFor(composed[i].X);
            shiftedBoxes[i] = shift > 0.0 ? (LayoutBox)Translate(composed[i], shift, 0.0) : composed[i];
        }

        var shiftedLines = new List<LayoutLine>(placedLines.Count);
        foreach (var line in placedLines)
        {
            // Shift each waypoint by its own individual position against the cuts, not the line's
            // leftmost point against the cuts: an unrelated line between two other top-level siblings
            // can geometrically thread through the x-region of an inserted cut (a common outcome for
            // Sugiyama-style routing), and shifting such a straddling line as one rigid unit would move
            // its far end away from a box that never moved, detaching the connector from its endpoint.
            var changed = false;
            var waypoints = new List<Point2D>(line.Waypoints.Count);
            foreach (var point in line.Waypoints)
            {
                var pointShift = ShiftFor(point.X);
                if (pointShift > 0.0)
                {
                    changed = true;
                }

                waypoints.Add(pointShift > 0.0 ? new Point2D(point.X + pointShift, point.Y) : point);
            }

            shiftedLines.Add(changed ? line with { Waypoints = waypoints } : line);
        }

        var shiftedPorts = new List<LayoutPort>(placedPorts.Count);
        foreach (var port in placedPorts)
        {
            var shift = ShiftFor(port.CentreX);
            shiftedPorts.Add(shift > 0.0 ? (LayoutPort)Translate(port, shift, 0.0) : port);
        }

        return (shiftedBoxes, shiftedLines, shiftedPorts, width + cuts.Sum(cut => cut.Extra));
    }

    /// <summary>
    /// Classifies every edge of <paramref name="graph"/>, purely from graph structure, into the edges
    /// this scope's leaf algorithm should route locally and the edges this scope's router must handle
    /// instead.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     An edge whose endpoints are the same node, or a container and one of its own descendants,
    ///     cannot be drawn between two distinct boxes and is skipped. An edge whose direct-member
    ///     endpoints (per <paramref name="descendantToDirect"/>) resolve to the same node and whose
    ///     endpoints are two distinct, unrelated nodes (an ancestor graph referencing nodes nested under
    ///     one container) is routed by this scope's router between the nested boxes; it cannot influence
    ///     the container's interior placement, which is laid out in isolation, and is never promoted
    ///     alongside leaf edges. An edge between two distinct
    ///     direct members, with at least one endpoint actually nested inside a container, is a
    ///     genuine cross-container edge routed by this scope's router (between the actual nested
    ///     endpoint nodes, not their containers). An edge between two distinct
    ///     direct members that are <em>both</em> literally direct (neither endpoint nested) would
    ///     normally be routed locally by the leaf algorithm — but when either of those direct members is
    ///     also an endpoint of a genuine cross-container edge, the edge is promoted to this scope's
    ///     router too, so every connector converging on that box's face is anchored by one coordinated
    ///     pass instead of two independent, mutually unaware ones (the leaf algorithm's own internal
    ///     routing and this scope's router).
    ///     </para>
    ///     <para>
    ///     A named <see cref="LayoutGraphPort"/> endpoint is resolved to its owning node (per
    ///     <paramref name="portOwner"/>) for the purpose of this scope-membership classification, then
    ///     treated identically to a plain node endpoint: a same-scope port edge (both direct members
    ///     literally direct — neither endpoint nested relative to this scope) is routed locally by the
    ///     leaf algorithm exactly like a flat, container-free graph would route it. This scope's own
    ///     router (<see cref="RouteCrossContainerEdges"/>, built on the box-only
    ///     <see cref="ConnectorRouter"/>/<see cref="Connection"/>) has no port concept, so a port edge
    ///     that would otherwise be classified as a genuine cross-container edge instead throws
    ///     <see cref="NotSupportedException"/>: full boundary-crossing port support (anchoring, routing)
    ///     is a separate, not-yet-designed Phase 2 effort (see ROADMAP.md), so this deliberately fails
    ///     loudly rather than silently dropping the edge or routing it incorrectly as a plain box
    ///     connector. A same-scope port edge is never promoted into the router's batch even when one of
    ///     its direct members is also touched by an unrelated genuine cross-container edge: since the
    ///     edge itself never crosses a container boundary, the leaf algorithm can always route it
    ///     correctly, so promoting it would trade a working local route for an unsupported one the
    ///     box-only router cannot execute.
    ///     </para>
    /// </remarks>
    /// <param name="graph">The scope whose edges are classified.</param>
    /// <param name="descendantToDirect">Map from every descendant node to the direct member that owns it.</param>
    /// <param name="portOwner">Map from every descendant's named port to the node that owns it.</param>
    /// <param name="placement">Parent and index-in-parent of every node of this scope's subtree.</param>
    /// <returns>The edges to route locally, and the edges this scope's router must handle.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when a named-port edge is a genuine cross-container edge (at least one endpoint nested
    /// relative to this scope) — this hierarchical engine does not yet support routing or anchoring a
    /// port across a container boundary. A same-scope port edge is never promoted into the router's
    /// batch, so it never triggers this exception even when it shares a box with a genuine
    /// cross-container edge.
    /// </exception>
    private static (HashSet<LayoutGraphEdge> LeafEdges, List<LayoutGraphEdge> RoutedEdges) ClassifyEdges(
        LayoutGraph graph,
        Dictionary<LayoutGraphNode, LayoutGraphNode> descendantToDirect,
        Dictionary<LayoutGraphPort, LayoutGraphNode> portOwner,
        Dictionary<LayoutGraphNode, (LayoutGraphNode? Parent, int Index)> placement)
    {
        const string boundaryPortMessage =
            "Named ports on edges crossing a container boundary are not yet supported; see ROADMAP.md Phase 2.";

        var directDirect = new List<(LayoutGraphEdge Edge, LayoutGraphNode SourceDirect, LayoutGraphNode TargetDirect)>();
        var routedEdges = new List<LayoutGraphEdge>();
        var conflicted = new HashSet<LayoutGraphNode>();

        foreach (var edge in graph.Edges)
        {
            // Skip edges whose endpoints are not both under this scope (a reference to a node or port
            // outside this scope's descendant tree entirely).
            if (!TryResolveOwner(edge.Source, portOwner, out var sourceOwner) ||
                !TryResolveOwner(edge.Target, portOwner, out var targetOwner) ||
                !descendantToDirect.TryGetValue(sourceOwner, out var sourceDirect) ||
                !descendantToDirect.TryGetValue(targetOwner, out var targetDirect))
            {
                continue;
            }

            // Both endpoints live under one direct member. Such an edge normally belongs to that lower
            // scope. When it is nevertheless declared here (an ancestor graph referencing nested nodes
            // directly) and its endpoints are two distinct nodes neither of which contains the other,
            // it is routed here between the actual nested boxes. It cannot influence the container's
            // interior placement (that is laid out in isolation), and is never promoted alongside leaf
            // edges. An edge from a container to its own descendant, a self-edge, and any port edge
            // remain skipped.
            if (ReferenceEquals(sourceDirect, targetDirect))
            {
                if (!ReferenceEquals(sourceOwner, targetOwner) &&
                    !IsAncestor(sourceOwner, targetOwner, placement) &&
                    !IsAncestor(targetOwner, sourceOwner, placement) &&
                    edge.Source is LayoutGraphNode &&
                    edge.Target is LayoutGraphNode)
                {
                    routedEdges.Add(edge);
                }

                continue;
            }

            var bothDirect = ReferenceEquals(sourceDirect, sourceOwner) && ReferenceEquals(targetDirect, targetOwner);
            if (bothDirect)
            {
                // Provisionally a leaf-routed edge; may still be promoted below if either endpoint is
                // also touched by a genuine cross-container edge.
                directDirect.Add((edge, sourceDirect, targetDirect));
            }
            else
            {
                // A genuine cross-container edge (at least one endpoint is nested relative to this
                // scope). This scope's router is box-only, so a port endpoint here cannot be routed
                // and instead fails loudly rather than being silently dropped or routed incorrectly
                // as a plain box connector.
                if (edge.Source is LayoutGraphPort || edge.Target is LayoutGraphPort)
                {
                    throw new NotSupportedException(boundaryPortMessage);
                }

                routedEdges.Add(edge);
                conflicted.Add(sourceDirect);
                conflicted.Add(targetDirect);
            }
        }

        var leafEdges = new HashSet<LayoutGraphEdge>();
        foreach (var (edge, sourceDirect, targetDirect) in directDirect)
        {
            // A port edge never crosses a container boundary in this branch (both direct members are
            // literally direct), so it can always be routed locally by the leaf algorithm — it is never
            // promoted into the box-only router's batch, even when a sibling box it shares is also
            // touched by an unrelated genuine cross-container edge.
            var isPortEdge = edge.Source is LayoutGraphPort || edge.Target is LayoutGraphPort;
            if (!isPortEdge && (conflicted.Contains(sourceDirect) || conflicted.Contains(targetDirect)))
            {
                // Promoted into the router's batch because it shares a box with a genuine
                // cross-container edge, so one coordinated pass anchors every connector on that face.
                routedEdges.Add(edge);
            }
            else
            {
                leafEdges.Add(edge);
            }
        }

        return (leafEdges, routedEdges);
    }

    /// <summary>
    /// Resolves an edge endpoint (a node or one of its named ports) to the node that owns it: a plain
    /// node endpoint resolves to itself, while a <see cref="LayoutGraphPort"/> endpoint resolves to its
    /// owning node via <paramref name="portOwner"/>.
    /// </summary>
    /// <param name="connectable">The edge endpoint to resolve.</param>
    /// <param name="portOwner">Map from every descendant's named port to the node that owns it.</param>
    /// <param name="owner">The resolved owning node, when resolution succeeds.</param>
    /// <returns><see langword="true"/> when the endpoint resolves to a node in this scope's graph.</returns>
    private static bool TryResolveOwner(
        ILayoutConnectable connectable,
        Dictionary<LayoutGraphPort, LayoutGraphNode> portOwner,
        out LayoutGraphNode owner)
    {
        switch (connectable)
        {
            case LayoutGraphNode node:
                owner = node;
                return true;

            case LayoutGraphPort port when portOwner.TryGetValue(port, out var ownerNode):
                owner = ownerNode;
                return true;

            default:
                owner = null!;
                return false;
        }
    }

    /// <summary>
    /// Maps the public <see cref="LayoutFlowDirection"/> option to the internal engine
    /// <see cref="LayoutDirection"/> the boundary-port resolver reasons about, so a delegation port's
    /// boundary face is chosen consistently with the flow the leaf algorithm laid the scope out along.
    /// </summary>
    /// <param name="direction">The public flow direction resolved for this scope.</param>
    /// <returns>The equivalent internal engine direction.</returns>
    private static LayoutDirection ToEngineDirection(LayoutFlowDirection direction) => direction switch
    {
        LayoutFlowDirection.Down => LayoutDirection.Down,
        LayoutFlowDirection.Left => LayoutDirection.Left,
        LayoutFlowDirection.Up => LayoutDirection.Up,
        _ => LayoutDirection.Right,
    };

    /// <summary>
    /// Resolves the leaf-algorithm identifier a scope is placed with from its already-cascaded effective
    /// options.
    /// </summary>
    /// <param name="effective">
    /// This scope's cascaded effective options, produced by overlaying the scope's own explicit
    /// <see cref="CoreOptions.Algorithm"/> override (if any) onto its parent's resolved snapshot.
    /// </param>
    /// <returns>
    /// The leaf-algorithm identifier to place the scope with. When it resolves to this engine's own
    /// <see cref="AlgorithmId"/> (which is not a leaf), the <see cref="DefaultLeafAlgorithmId"/> is
    /// substituted so recursion terminates in a registered leaf instead of failing to resolve.
    /// </returns>
    private static string ResolveLeafAlgorithmId(LayoutOptions effective)
    {
        var resolved = effective.Get(CoreOptions.Algorithm);
        return resolved == AlgorithmId ? DefaultLeafAlgorithmId : resolved;
    }

    /// <summary>
    /// Records that <paramref name="node"/> and all of its descendants belong to the direct-member
    /// container <paramref name="direct"/>, so a cross-container edge referencing any descendant can be
    /// anchored to the top-level box representing that container. Also records, for every named port
    /// owned by <paramref name="node"/> or any of its descendants, the node that owns it, so an edge
    /// endpoint that is a <see cref="LayoutGraphPort"/> can be resolved to its owning node the same way
    /// a plain node endpoint resolves to itself.
    /// </summary>
    /// <param name="node">The node currently being mapped (initially the direct member itself).</param>
    /// <param name="direct">The direct member of the scope that owns this subtree.</param>
    /// <param name="map">The descendant-to-direct-member map being populated.</param>
    /// <param name="portOwner">The port-to-owning-node map being populated.</param>
    /// <param name="placement">
    /// The map being populated from each node to its parent node (<see langword="null"/> for a direct
    /// member) and its index within its parent's child list; the direct member's own entry is recorded
    /// by the caller.
    /// </param>
    private static void MapDescendants(
        LayoutGraphNode node,
        LayoutGraphNode direct,
        Dictionary<LayoutGraphNode, LayoutGraphNode> map,
        Dictionary<LayoutGraphPort, LayoutGraphNode> portOwner,
        Dictionary<LayoutGraphNode, (LayoutGraphNode? Parent, int Index)> placement)
    {
        map[node] = direct;
        if (node.HasPorts)
        {
            foreach (var port in node.Ports.Ports)
            {
                portOwner[port] = node;
            }
        }

        if (node.HasChildren)
        {
            for (var i = 0; i < node.Children.Nodes.Count; i++)
            {
                var child = node.Children.Nodes[i];
                placement[child] = (node, i);
                MapDescendants(child, direct, map, portOwner, placement);
            }
        }
    }

    /// <summary>
    /// Determines whether <paramref name="ancestor"/> strictly contains <paramref name="node"/> in the
    /// nesting hierarchy recorded by <paramref name="placement"/>.
    /// </summary>
    /// <param name="ancestor">The candidate containing node.</param>
    /// <param name="node">The node whose parent chain is walked.</param>
    /// <param name="placement">Parent and index-in-parent of every node of the scope's subtree.</param>
    /// <returns><see langword="true"/> when <paramref name="ancestor"/> is a proper ancestor of <paramref name="node"/>.</returns>
    private static bool IsAncestor(
        LayoutGraphNode ancestor,
        LayoutGraphNode node,
        Dictionary<LayoutGraphNode, (LayoutGraphNode? Parent, int Index)> placement)
    {
        for (var current = placement[node].Parent; current != null; current = placement[current].Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Translates a placed layout node by a fixed offset, recursively shifting a box's nested children,
    /// a line's waypoints, and a port's anchor centre, so a locally-placed sub-layout can be composed
    /// into an absolute position.
    /// </summary>
    /// <param name="node">The placed node to translate; boxes, lines, and ports are produced by the bundled leaf algorithms.</param>
    /// <param name="deltaX">Offset added to every X coordinate, in logical pixels.</param>
    /// <param name="deltaY">Offset added to every Y coordinate, in logical pixels.</param>
    /// <returns>A translated copy of <paramref name="node"/>, or the node unchanged for unknown subtypes.</returns>
    internal static LayoutNode Translate(LayoutNode node, double deltaX, double deltaY)
    {
        switch (node)
        {
            case LayoutBox box:
                var children = new List<LayoutNode>(box.Children.Count);
                foreach (var child in box.Children)
                {
                    children.Add(Translate(child, deltaX, deltaY));
                }

                return box with { X = box.X + deltaX, Y = box.Y + deltaY, Children = children };

            case LayoutLine line:
                var waypoints = new List<Point2D>(line.Waypoints.Count);
                foreach (var point in line.Waypoints)
                {
                    waypoints.Add(new Point2D(point.X + deltaX, point.Y + deltaY));
                }

                return line with { Waypoints = waypoints };

            case LayoutPort port:
                return port with { CentreX = port.CentreX + deltaX, CentreY = port.CentreY + deltaY };

            default:
                // Forward compatibility: an unknown node subtype is left untranslated rather than dropped.
                return node;
        }
    }
}
