// <copyright file="HierarchicalLayoutAlgorithm.CrossOrdering.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout.Engine;

namespace DemaConsulting.Rendering.Layout;

/// <summary>
/// Cross-hierarchy ordering refinement of <see cref="HierarchicalLayoutAlgorithm"/>.
/// </summary>
/// <remarks>
///     <para>
///     A container's interior is laid out in isolation, blind to the connectors its declaring scope routes
///     between nested nodes of different containers, so the order of the interior's members is decided
///     by insertion order alone and may force those connectors to cross. After a scope's first pass this
///     driver measures the crossings over the whole subtree of the scope (its own connectors and the
///     interior connectors of every container, shared end points excluded), derives for every node on
///     the chain of a cross-container connector's endpoint the position of the partner the connector
///     leads to (the median of the partners' placed centers), proposes orders that sort the keyed
///     members of each container interior by that position, applies them through the
///     <see cref="OrderRanks"/> hint, and lays the scope out again. A proposal is <em>accepted</em> only
///     when its routed layout strictly lowers the crossing count, or keeps it and strictly shrinks the
///     canvas, and — unless it removes every crossing — does not open a larger empty square than the
///     layout it replaces (see <see cref="EmptySpaceMeter"/>) beyond what the diagram-quality limit
///     allows, so the result is never worse than the first pass and a scope that gains nothing keeps its
///     first-pass geometry unchanged.
///     </para>
///     <para>
///     Routing dominates the cost of a pass, so the proposals are first <em>scored on a placement-only
///     pass</em> (the scope placed and composed, its cross-container connectors replaced by straight
///     segments between the endpoint boxes) and only the best-scored ones are laid out and routed for
///     real. The work is bounded: at most <see cref="MaxOrderingRounds"/> rounds on top of the first
///     pass, the first laying out and routing at most <see cref="FirstRoundProposals"/> proposals and the
///     others one each, i.e. at most <see cref="MaxLayoutsPerRefinedScope"/> routed layouts of one scope.
///     A scope whose routing is expensive (see <see cref="HeavyScopeWeight"/>) is refined in one round
///     with at most <see cref="HeavyScopeProposals"/> routed proposals that the placement-only score
///     predicts to be no worse than the layout in force (the prediction is a rough one: it counts straight
///     segments between the endpoint boxes, so a proposal that merely ties it can still route better), and
///     is left alone when it already has no crossing. A proposal is also accepted when its largest empty
///     square is larger than the limit it is held to by less than <see cref="EmptySquareTolerance"/>,
///     the resolution of that measure. Interior layouts are
///     memoized per container and order-rank signature, so a proposal that does not change a container's
///     subtree never lays that container out again. A scope without a cross-container connector that has
///     a nested endpoint inside a container of at least two members is returned as the first pass
///     produced it.
///     </para>/// </remarks>
public sealed partial class HierarchicalLayoutAlgorithm
{
    /// <summary>
    /// Maximum number of refinement rounds per scope.
    /// </summary>
    internal const int MaxOrderingRounds = 3;

    /// <summary>
    /// Maximum number of proposals the first refinement round lays out and routes.
    /// </summary>
    internal const int FirstRoundProposals = 4;

    /// <summary>
    /// Maximum number of times one scope is laid out and routed by its refinement: the first pass, up to
    /// <see cref="FirstRoundProposals"/> proposals in the first round and one in each later round.
    /// </summary>
    internal const int MaxLayoutsPerRefinedScope = 1 + FirstRoundProposals + (MaxOrderingRounds - 1);

    /// <summary>
    /// Maximum number of proposals a scope with expensive routing lays out and routes.
    /// </summary>
    internal const int HeavyScopeProposals = 2;

    /// <summary>
    /// Routing weight (cross-container connectors times boxes of the scope) from which a scope counts as
    /// expensive to route: it is refined in a single round with at most <see cref="HeavyScopeProposals"/>
    /// routed proposals.
    /// </summary>
    internal const int HeavyScopeWeight = 150;

    /// <summary>
    /// Number of order variants a round scores: sort by the partners' cross-axis or other-axis position,
    /// ascending or descending, from the partners' own boxes or from the boxes that stand for them at the
    /// level where the connector's two ends meet.
    /// </summary>
    private const int VariantCount = 8;

    /// <summary>
    /// The variant tried first: partners at the level where the connector's ends meet, sorted along the
    /// cross axis, ascending. It is the order a caller derives from a first layout by hand (members of a
    /// container sorted by the mean position of the boxes their outside relationships lead to).
    /// </summary>
    private const int PreferredVariant = 4;

    /// <summary>
    /// Smallest canvas-area reduction, in square logical pixels, that counts as "strictly smaller" when a
    /// proposal ties the best crossing count.
    /// </summary>
    private const double AreaTolerance = 1e-6;

    /// <summary>
    /// How much larger, in logical pixels, the largest empty square of a proposal may be than the limit it
    /// is held to: one occupancy-grid cell of <see cref="EmptySpaceMeter"/>, the resolution of that
    /// measurement. A difference of one cell is below what the measure can tell apart, so it must not veto
    /// a proposal that removes crossings.
    /// </summary>
    private const double EmptySquareTolerance = EmptySpaceMeter.CellSize;

    /// <summary>
    /// How many crossings a proposal must remove to be allowed an empty square larger than the limit it is
    /// held to (by up to <see cref="EmptySquareReliefFactor"/>): removing this many crossings is worth a
    /// little more empty space.
    /// </summary>
    private const int EmptySquareReliefCrossings = 5;

    /// <summary>The factor of the empty-square limit a proposal that removes <see cref="EmptySquareReliefCrossings"/> crossings may reach.</summary>
    private const double EmptySquareReliefFactor = 1.10;

    /// <summary>
    /// Tolerance, in logical pixels, when classifying a connector segment as horizontal or vertical.
    /// </summary>
    private const double SegmentEpsilon = 1e-6;

    /// <summary>
    /// Internal per-run refinement state: the memoized interior layouts and the layout counters. The root
    /// call creates one when the caller's options carry none, and it travels in every scope's effective
    /// <see cref="LayoutOptions"/>, so a run never shares state with another run.
    /// </summary>
    internal static readonly LayoutProperty<OrderingRefinementContext?> RefinementContext =
        new("rendering.layout.hierarchical.refinementContext", null);

    /// <summary>
    /// Internal per-scope state: the routing work budget of the scope being laid out. Every scope that
    /// routes connectors creates its own, replacing the one it inherits from the enclosing scope.
    /// </summary>
    internal static readonly LayoutProperty<RoutingWorkBudget?> RoutingBudget =
        new("rendering.layout.hierarchical.routingBudget", null);

    /// <summary>
    /// Internal hint: the containers, at any depth below the scope, that keep the room they had before
    /// containers were tightened to their content. A scope that finds tightening makes its connectors worse
    /// sets it for the scopes below.
    /// </summary>
    internal static readonly LayoutProperty<ISet<LayoutGraphNode>?> LooseContainerNodes =
        new("rendering.layout.hierarchical.looseContainerNodes", null);

    /// <summary>
    /// Internal hint: the padding, in logical pixels, a tightened container needs beside its content so the
    /// decoration (arrowhead, diamond) of a connector that enters it fits between the container's border and
    /// the node the connector ends on (see <see cref="ContainerPadding"/> for the default).
    /// </summary>
    internal static readonly LayoutProperty<IReadOnlyDictionary<LayoutGraphNode, double>?> ContainerPaddings =
        new("rendering.layout.hierarchical.containerPaddings", null);

    /// <summary>
    /// The first pass of a hierarchical scope together with the structural facts the refinement needs.
    /// </summary>
    /// <param name="Tree">The placed, canvas-fitted tree of the scope.</param>
    /// <param name="RoutedEdges">
    /// The edges the scope's router handled, or <see langword="null"/> when the scope was placed through
    /// the boundary-port pipeline (which is never refined).
    /// </param>
    /// <param name="DescendantToDirect">Map from every descendant node to the direct member that owns it.</param>
    /// <param name="Nesting">Parent and index-in-parent of every node of the scope's subtree.</param>
    private sealed record ScopePass(
        LayoutTree Tree,
        List<LayoutGraphEdge>? RoutedEdges,
        Dictionary<LayoutGraphNode, LayoutGraphNode> DescendantToDirect,
        Dictionary<LayoutGraphNode, (LayoutGraphNode? Parent, int Index)> Nesting);

    /// <summary>
    /// One order proposal: the complete rank dictionary to apply, the variant that produced it, and its
    /// placement-only score.
    /// </summary>
    /// <param name="Variant">The variant number (a bit set of axis, direction and key source).</param>
    /// <param name="Ranks">The complete order-rank hint of the proposal.</param>
    private sealed record Proposal(int Variant, Dictionary<LayoutGraphNode, double> Ranks)
    {
        /// <summary>
        /// Gets or sets the score of the placement-only pass of this proposal.
        /// </summary>
        public PlacementScore Score { get; set; }
    }

    /// <summary>
    /// The score of a placement-only pass.
    /// </summary>
    /// <param name="Crossings">The crossings among the interior connectors and the straight segments standing for the routed ones.</param>
    /// <param name="Area">The canvas area.</param>
    /// <param name="EmptySquare">The side of the largest empty square of the drawing, in logical pixels.</param>
    private readonly record struct PlacementScore(int Crossings, double Area, double EmptySquare);

    /// <summary>
    /// Mutable per-run state of the ordering refinement: the memoized container interior layouts, keyed
    /// by container node and order-rank signature, and the counters that bound and expose its work.
    /// </summary>
    internal sealed class OrderingRefinementContext
    {
        /// <summary>
        /// Gets the number of scopes whose refinement laid the scope out at least once more.
        /// </summary>
        public int RefinedScopes { get; private set; }

        /// <summary>
        /// Gets the largest number of routed layouts of one scope any single refinement performed (the
        /// first pass and every routed proposal); never above <see cref="MaxLayoutsPerRefinedScope"/>.
        /// </summary>
        public int MaxLayoutsPerScope { get; private set; }

        /// <summary>
        /// Gets the number of placement-only passes the refinement scored proposals with.
        /// </summary>
        public int PlacementPasses { get; private set; }

        /// <summary>
        /// Gets the number of interior layouts served from the memo instead of being laid out again.
        /// </summary>
        public int MemoHits { get; private set; }

        /// <summary>
        /// Gets the memoized interior layouts.
        /// </summary>
        internal Dictionary<(LayoutGraphNode Container, string Signature), LayoutTree> InteriorLayouts { get; } = [];

        /// <summary>
        /// Gets the routed passes of the run, so a pass over a routing problem an earlier pass already
        /// solved replays its lines instead of searching again (see <see cref="RoutePassCache"/>).
        /// </summary>
        internal RoutePassCache RoutePasses { get; } = new();

        /// <summary>
        /// Gets the loose containers (not tightened to their content) of the scopes laid out so far, so a
        /// refinement pass starts with what the first pass of the scope found out.
        /// </summary>
        private Dictionary<LayoutGraph, HashSet<LayoutGraphNode>> LooseContainers { get; } = [];

        /// <summary>
        /// Gets the containers, at any depth, each scope's last routed pass settled on keeping loose.
        /// </summary>
        private Dictionary<LayoutGraph, ISet<LayoutGraphNode>> SettledLoose { get; } = [];

        /// <summary>
        /// Returns the set of loose containers of <paramref name="scope"/>, created empty on first use.
        /// </summary>
        /// <param name="scope">The container scope.</param>
        /// <returns>The mutable set, shared by every pass over the scope in this run.</returns>
        internal HashSet<LayoutGraphNode> LooseContainersOf(LayoutGraph scope)
        {
            if (!LooseContainers.TryGetValue(scope, out var loose))
            {
                loose = [];
                LooseContainers[scope] = loose;
            }

            return loose;
        }

        /// <summary>
        /// Records the containers the last routed pass of <paramref name="scope"/> settled on keeping loose
        /// (at any depth), or <see langword="null"/> when tightening all of them was kept.
        /// </summary>
        /// <param name="scope">The container scope.</param>
        /// <param name="loose">The containers that stay loose, or <see langword="null"/> for none.</param>
        internal void SetSettledLooseNodes(LayoutGraph scope, ISet<LayoutGraphNode>? loose)
        {
            if (loose is null)
            {
                SettledLoose.Remove(scope);
            }
            else
            {
                SettledLoose[scope] = loose;
            }
        }

        /// <summary>
        /// Returns the containers the last routed pass of <paramref name="scope"/> settled on keeping loose
        /// (see <see cref="SetSettledLooseNodes"/>), or <see langword="null"/> when it kept them all tight.
        /// </summary>
        /// <param name="scope">The container scope.</param>
        /// <returns>The containers that stay loose, or <see langword="null"/>.</returns>
        internal ISet<LayoutGraphNode>? SettledLooseNodes(LayoutGraph scope) =>
            SettledLoose.TryGetValue(scope, out var loose) ? loose : null;

        /// <summary>
        /// Records one memo hit.
        /// </summary>
        internal void RecordMemoHit() => MemoHits++;

        /// <summary>
        /// Records one placement-only pass.
        /// </summary>
        internal void RecordPlacementPass() => PlacementPasses++;

        /// <summary>
        /// Records one finished refinement of a scope.
        /// </summary>
        /// <param name="layouts">How many routed layouts of the scope were made (first pass included).</param>
        internal void RecordRefinement(int layouts)
        {
            RefinedScopes++;
            MaxLayoutsPerScope = Math.Max(MaxLayoutsPerScope, layouts);
        }

        /// <summary>
        /// Gets one hint per scope whose connector routing reached its work budget during the run, in the
        /// order the scopes were first limited.
        /// </summary>
        internal IReadOnlyList<string> RoutingLimitWarnings => _routingLimitWarnings;

        /// <summary>The hints returned by <see cref="RoutingLimitWarnings"/>.</summary>
        private readonly List<string> _routingLimitWarnings = [];

        /// <summary>For every limited scope: where its hint sits, and the reduction, elbow count and cause it describes.</summary>
        private readonly Dictionary<LayoutGraph, (int Index, RoutingTier Tier, int Elbows, bool SizeForced)> _limitedScopes = [];

        /// <summary>
        /// Records, when a scope's own budget ran down, the hint that says so.
        /// A scope laid out several times (the passes of its parent's refinement) keeps the worst outcome.
        /// A scope only reduced because the oversize scope that holds it is (see
        /// <see cref="RoutingWorkBudget.InheritReduced"/>) has no limit of its own and so no hint: the hint
        /// of the oversize scope speaks for it.
        /// </summary>
        /// <param name="scope">The container scope.</param>
        /// <param name="budget">The budget its passes spent.</param>
        internal void RecordRoutingBudget(LayoutGraph scope, RoutingWorkBudget budget)
        {
            if (budget.OwnLowestTier == RoutingTier.Full)
            {
                return;
            }

            var found = _limitedScopes.TryGetValue(scope, out var known);
            var sizeForced = budget.SizeForced || (found && known.SizeForced);
            if (found && known.Tier >= budget.OwnLowestTier && known.Elbows >= budget.ElbowRoutes && known.SizeForced == sizeForced)
            {
                return;
            }

            var tier = found && known.Tier > budget.OwnLowestTier ? known.Tier : budget.OwnLowestTier;
            var elbows = Math.Max(found ? known.Elbows : 0, budget.ElbowRoutes);
            var first = scope.Nodes.Count > 0 ? scope.Nodes[0].Id : "?";
            var effect = elbows > 0
                ? $"{elbows} connector(s) were routed with the simplified elbow route"
                : "its connectors were routed without the extra quality searches";
            var inside = sizeForced ? " (and those of the scopes inside it)" : string.Empty;
            var text = $"Edge routing of the scope of {scope.Nodes.Count} node(s) starting at '{first}' reached its work limit: {effect}{inside}.";
            if (found)
            {
                _routingLimitWarnings[known.Index] = text;
                _limitedScopes[scope] = (known.Index, tier, elbows, sizeForced);
            }
            else
            {
                _limitedScopes[scope] = (_routingLimitWarnings.Count, tier, elbows, sizeForced);
                _routingLimitWarnings.Add(text);
            }
        }
    }

    /// <summary>
    /// The routing work limits of a scope without an explicit override. A scope of at least
    /// <see cref="RoutingWorkLimits.ReducedWeight"/>, or one whose connector routing spends more than the
    /// soft limit, is routed at the reduced effort; one that spends more than the hard limit gets the
    /// simple elbow route for the connectors still to route.
    /// </summary>
    /// <remarks>
    /// The limits are calibrated against the measured work of the layout-quality benchmark scenes and the
    /// gallery, the heaviest of which spends about 40 million units in one scope and weighs 338: the
    /// soft limit sits at twice that (so no benchmark scene is ever reduced), the hard limit
    /// at twice the soft one, and the weight threshold well above the weight of every such scene and well
    /// below that of a scope (such as 40 connectors over 36 nodes, weight 1 440) whose full routing takes
    /// minutes. One work unit costs between about 100 nanoseconds (the routing of a large flat grid) and
    /// about 200 nanoseconds (the nested scenes) on a fast desktop core, and more on a shared or
    /// instrumented one, so the soft limit is a bound of the work a scope spends, not of the time it takes.
    /// The limits are bounds per scope and per pass over it: a layout of many heavy scopes spends their sum.
    /// </remarks>
    internal static readonly RoutingWorkLimits DefaultRoutingWorkLimits = new(
        Soft: 80_000_000L,
        Hard: 160_000_000L,
        ReducedWeight: 540L);

    /// <summary>
    /// Returns <paramref name="effective"/> carrying <paramref name="budget"/> as the routing budget of the
    /// scope being laid out.
    /// </summary>
    /// <param name="effective">The scope's effective options.</param>
    /// <param name="budget">The scope's routing budget.</param>
    /// <returns>Options that carry the budget.</returns>
    private static LayoutOptions WithRoutingBudget(LayoutOptions effective, RoutingWorkBudget budget)
    {
        var overlay = new LayoutOptions();
        overlay.Set<RoutingWorkBudget?>(RoutingBudget, budget);
        return overlay.OverlayOnto(effective);
    }

    /// <summary>
    /// Returns <paramref name="effective"/> carrying a refinement context: its own when the caller's
    /// options supplied one, otherwise a fresh one.
    /// </summary>
    /// <param name="effective">The root scope's effective options.</param>
    /// <returns>Options that carry a refinement context.</returns>
    private static LayoutOptions WithRefinementContext(LayoutOptions effective)
    {
        if (effective.Get(RefinementContext) != null)
        {
            return effective;
        }

        var overlay = new LayoutOptions();
        overlay.Set<OrderingRefinementContext?>(RefinementContext, new OrderingRefinementContext());
        return overlay.OverlayOnto(effective);
    }

    /// <summary>
    /// Lays out the interior of the container <paramref name="node"/>, serving the result from the
    /// refinement memo when the same container was already laid out with the same order ranks on its
    /// subtree (nothing else a refinement changes can influence the interior).
    /// </summary>
    /// <param name="node">The container whose children scope is laid out.</param>
    /// <param name="childEffective">The cascaded effective options of the container's children scope.</param>
    /// <param name="tighten">Whether the interior is trimmed to its content (see <c>LayoutContainerChildren</c>).</param>
    /// <returns>The placed interior in local coordinates.</returns>
    private LayoutTree LayoutContainerScope(LayoutGraphNode node, LayoutOptions childEffective, bool tighten)
    {
        var context = childEffective.Get(RefinementContext);
        if (context is null)
        {
            return TrimInterior(LayoutScope(node.Children, childEffective), childEffective, tighten);
        }

        var suffix = tighten ? "|t" : "|u";
        var looseNodes = childEffective.Get(LooseContainerNodes);
        var paddings = childEffective.Get(ContainerPaddings);
        if (looseNodes is { Count: > 0 } || paddings is { Count: > 0 })
        {
            suffix += "|" + ContainerSignature(node, looseNodes, paddings);
        }

        var key = (node, RankSignature(node, childEffective.Get(OrderRanks)) + suffix);
        if (context.InteriorLayouts.TryGetValue(key, out var cached))
        {
            context.RecordMemoHit();
            return cached;
        }

        var sub = TrimInterior(LayoutScope(node.Children, childEffective), childEffective, tighten);
        context.InteriorLayouts[key] = sub;
        return sub;
    }

    /// <summary>
    /// Trims a container interior to its drawn content (boxes, waypoints, resolved labels, ports) so the
    /// leaf algorithm's own canvas padding is not added on top of <see cref="ContainerPadding"/>.
    /// </summary>
    /// <param name="interior">The placed interior.</param>
    /// <param name="childEffective">The cascaded effective options of the container's children scope.</param>
    /// <param name="tighten">Whether to trim at all.</param>
    /// <returns>The interior, trimmed when requested.</returns>
    private static LayoutTree TrimInterior(LayoutTree interior, LayoutOptions childEffective, bool tighten) =>
        tighten
            ? CanvasFitter.TrimToContent(interior, childEffective.Get(CoreOptions.AssumedFontSize), ContainerPadding)
            : interior;

    /// <summary>
    /// Builds the signature of the order ranks that apply to the subtree of <paramref name="container"/>:
    /// the rank (or a dash when unranked) of every descendant in depth-first order.
    /// </summary>
    /// <param name="container">The container whose subtree is signed.</param>
    /// <param name="ranks">The effective order-rank hint, or <see langword="null"/>.</param>
    /// <returns>The signature; empty when there are no ranks.</returns>
    private static string RankSignature(LayoutGraphNode container, IReadOnlyDictionary<LayoutGraphNode, double>? ranks)
    {
        if (ranks is null || ranks.Count == 0)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        AppendSignature(container, ranks, text);
        return text.ToString();
    }

    /// <summary>
    /// Builds the signature of which containers below <paramref name="container"/> are kept loose and how much
    /// padding each needs, so an interior laid out with different ones is not served from the memo.
    /// </summary>
    /// <param name="container">The container whose subtree is signed.</param>
    /// <param name="loose">The loose containers, or <see langword="null"/>.</param>
    /// <param name="paddings">The container paddings, or <see langword="null"/>.</param>
    /// <returns>The signature.</returns>
    private static string ContainerSignature(
        LayoutGraphNode container,
        ISet<LayoutGraphNode>? loose,
        IReadOnlyDictionary<LayoutGraphNode, double>? paddings)
    {
        var text = new StringBuilder();
        AppendContainerSignature(container, loose, paddings, text);
        return text.ToString();
    }

    /// <summary>
    /// Appends, for every container below <paramref name="node"/>, whether it is loose and its padding.
    /// </summary>
    /// <param name="node">The node whose children are appended.</param>
    /// <param name="loose">The loose containers, or <see langword="null"/>.</param>
    /// <param name="paddings">The container paddings, or <see langword="null"/>.</param>
    /// <param name="text">The signature under construction.</param>
    private static void AppendContainerSignature(
        LayoutGraphNode node,
        ISet<LayoutGraphNode>? loose,
        IReadOnlyDictionary<LayoutGraphNode, double>? paddings,
        StringBuilder text)
    {
        foreach (var child in node.Children.Nodes)
        {
            if (child.HasChildren)
            {
                text.Append(loose?.Contains(child) ?? false ? 'L' : 't');
                if (paddings != null && paddings.TryGetValue(child, out var padding))
                {
                    text.Append(padding.ToString("R", CultureInfo.InvariantCulture));
                }

                text.Append('(');
                AppendContainerSignature(child, loose, paddings, text);
                text.Append(')');
            }
            else
            {
                text.Append('.');
            }
        }
    }

    /// <summary>
    /// Appends the ranks of the descendants of <paramref name="node"/> to <paramref name="text"/>.
    /// </summary>
    /// <param name="node">The node whose children are appended.</param>
    /// <param name="ranks">The order-rank hint.</param>
    /// <param name="text">The signature under construction.</param>
    private static void AppendSignature(
        LayoutGraphNode node,
        IReadOnlyDictionary<LayoutGraphNode, double> ranks,
        StringBuilder text)
    {
        if (!node.HasChildren)
        {
            return;
        }

        text.Append('(');
        foreach (var child in node.Children.Nodes)
        {
            if (ranks.TryGetValue(child, out var rank))
            {
                text.Append(rank.ToString("R", CultureInfo.InvariantCulture));
            }
            else
            {
                text.Append('-');
            }

            AppendSignature(child, ranks, text);
            text.Append(',');
        }

        text.Append(')');
    }

    /// <summary>
    /// Refines the order of the container interiors under <paramref name="graph"/> so the connectors this
    /// scope routes between nested nodes cross less, accepting a proposal only when its routed layout
    /// strictly lowers the crossing count (or keeps it and strictly shrinks the canvas).
    /// </summary>
    /// <param name="graph">The scope.</param>
    /// <param name="algo">The leaf algorithm the scope is placed with.</param>
    /// <param name="effective">The scope's cascaded effective options.</param>
    /// <param name="first">The scope's first pass.</param>
    /// <returns>The best tree found; <paramref name="first"/>'s tree when nothing improves on it.</returns>
    private LayoutTree RefineCrossOrdering(
        LayoutGraph graph,
        LayoutAlgorithmBase algo,
        LayoutOptions effective,
        ScopePass first)
    {
        var context = effective.Get(RefinementContext);
        if (context is null || first.RoutedEdges is null || first.RoutedEdges.Count == 0)
        {
            return first.Tree;
        }

        // A scope whose routing already ran down its work budget is not refined: every further routed
        // layout would only spend more of what is gone.
        var budget = effective.Get(RoutingBudget);
        if (budget is { Tier: not RoutingTier.Full })
        {
            return first.Tree;
        }

        var proposals = BuildProposals(graph, effective, first);
        if (proposals.Count == 0)
        {
            return first.Tree;
        }

        var best = first;
        var bestEffective = effective;
        var bestCost = CountCrossings(best.Tree);
        var firstCost = bestCost;
        var bestArea = best.Tree.Width * best.Tree.Height;
        var bestEmptySquare = EmptySpaceMeter.LargestEmptySquare(best.Tree);
        var passingSquare = EmptySpaceMeter.PassingSquare(best.Tree);
        var heavy = first.RoutedEdges.Count * CountBoxes(first.Tree) >= HeavyScopeWeight;
        if (heavy && bestCost == 0)
        {
            return first.Tree;
        }

        // The placement-only score of the geometry in force, to compare the proposals' scores with.
        context.RecordPlacementPass();
        var current = ScorePlacement(graph, LayoutScopePass(graph, algo, effective, route: false));
        var layouts = 1;
        RouteQuality.Summary? firstQuality = null;
        var rounds = heavy ? 1 : MaxOrderingRounds;
        for (var round = 0; round < rounds && proposals.Count > 0; round++)
        {
            foreach (var proposal in proposals)
            {
                context.RecordPlacementPass();
                var placement = LayoutScopePass(graph, algo, WithOrderRanks(effective, proposal.Ranks), route: false);
                proposal.Score = ScorePlacement(graph, placement);
            }

            // A scope with expensive routing never routes a proposal that opens a larger empty square than
            // the layout in force allows (the routed layout would be refused anyway). The caller-side barycenter
            // order comes first (it is the order that is right most often) and the rest follow by their
            // placement-only score.
            var improved = false;
            var routed = 0;
            foreach (var proposal in proposals
                .Where(p => !heavy || p.Score.EmptySquare <= Math.Max(current.EmptySquare, passingSquare) + EmptySquareTolerance)
                .OrderBy(p => p.Variant == PreferredVariant ? 0 : 1)
                .ThenBy(p => p.Score.Crossings)
                .ThenBy(p => p.Score.Area)
                .ThenBy(p => p.Variant))
            {
                if (routed >= RoutedProposalsInRound(round, heavy) || budget is { Tier: not RoutingTier.Full })
                {
                    break;
                }

                // An expensive scope only pays for a proposal its placement-only score predicts to be no worse
                // (the prediction ignores how the router really bends and shares lanes, so a tie is worth a try).
                if (heavy && proposal.Score.Crossings > current.Crossings)
                {
                    continue;
                }

                var candidateEffective = WithOrderRanks(effective, proposal.Ranks);
                var candidate = LayoutScopePass(graph, algo, candidateEffective);
                layouts++;
                routed++;

                var cost = CountCrossings(candidate.Tree);
                var area = candidate.Tree.Width * candidate.Tree.Height;
                if (cost < bestCost || (cost == bestCost && area < bestArea - AreaTolerance))
                {
                    var emptySquare = EmptySpaceMeter.LargestEmptySquare(candidate.Tree);
                    var squareLimit = Math.Max(bestEmptySquare, passingSquare);
                    var squareOk = emptySquare <= squareLimit + EmptySquareTolerance
                        || (bestCost - cost >= EmptySquareReliefCrossings && emptySquare <= squareLimit * EmptySquareReliefFactor);
                    if ((cost == 0 || squareOk)
                        && KeepsRouteQuality(MeasureRoutes(candidate.Tree), firstQuality ??= MeasureRoutes(first.Tree), Math.Max(0, firstCost - cost)))
                    {
                        best = candidate;
                        bestEffective = candidateEffective;
                        bestCost = cost;
                        bestArea = area;
                        bestEmptySquare = emptySquare;
                        current = proposal.Score;
                        improved = true;
                    }
                }
            }

            if (!improved || round == rounds - 1)
            {
                break;
            }

            proposals = BuildProposals(graph, bestEffective, best);
        }

        context.RecordRefinement(layouts);
        return best.Tree;
    }

    /// <summary>
    /// Returns how many proposals a refinement round may lay out and route: a scope with expensive routing
    /// pays for two in total; otherwise the first round tries the best <see cref="FirstRoundProposals"/>
    /// and each later round re-keys and tries one more.
    /// </summary>
    /// <param name="round">The zero-based round.</param>
    /// <param name="heavy">Whether the scope is expensive to route.</param>
    /// <returns>The number of routed proposals the round may make.</returns>
    private static int RoutedProposalsInRound(int round, bool heavy)
    {
        if (heavy)
        {
            return HeavyScopeProposals;
        }

        return round == 0 ? FirstRoundProposals : 1;
    }

    /// <summary>
    /// Counts the boxes of <paramref name="tree"/> at every nesting depth.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The number of boxes.</returns>
    private static int CountBoxes(LayoutTree tree)
    {
        var count = 0;
        var pending = new Stack<IReadOnlyList<LayoutNode>>();
        pending.Push(tree.Nodes);
        while (pending.Count > 0)
        {
            foreach (var node in pending.Pop())
            {
                if (node is LayoutBox box)
                {
                    count++;
                    pending.Push(box.Children);
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Scores a placement-only pass: the crossings among its interior connectors and the straight
    /// segments that stand for its cross-container connectors, its canvas area, and the largest empty
    /// square of the drawing with those segments in place.
    /// </summary>
    /// <param name="graph">The scope.</param>
    /// <param name="pass">The placement-only pass.</param>
    /// <returns>The score.</returns>
    private static PlacementScore ScorePlacement(LayoutGraph graph, ScopePass pass)
    {
        var lines = new List<IReadOnlyList<Point2D>>();
        CollectLines(pass.Tree.Nodes, lines);

        var nodes = new List<LayoutNode>(pass.Tree.Nodes);
        var topBoxes = pass.Tree.Nodes.OfType<LayoutBox>().ToArray();
        if (topBoxes.Length == graph.Nodes.Count)
        {
            var indexOf = new Dictionary<LayoutGraphNode, int>(graph.Nodes.Count);
            for (var i = 0; i < graph.Nodes.Count; i++)
            {
                indexOf[graph.Nodes[i]] = i;
            }

            foreach (var edge in pass.RoutedEdges!)
            {
                if (edge.Source is not LayoutGraphNode source || edge.Target is not LayoutGraphNode target)
                {
                    continue;
                }

                var (from, _) = ResolveEndpointBox(source, topBoxes, indexOf, pass.DescendantToDirect, pass.Nesting);
                var (to, _) = ResolveEndpointBox(target, topBoxes, indexOf, pass.DescendantToDirect, pass.Nesting);
                if (!ReferenceEquals(from, to))
                {
                    List<Point2D> segment =
                    [
                        new Point2D(from.X + (from.Width / 2.0), from.Y + (from.Height / 2.0)),
                        new Point2D(to.X + (to.Width / 2.0), to.Y + (to.Height / 2.0)),
                    ];
                    lines.Add(segment);
                    nodes.Add(new LayoutLine(segment, EndMarkerStyle.None, EndMarkerStyle.None, LineStyle.Solid, null));
                }
            }
        }

        return new PlacementScore(
            CountLineCrossings(lines),
            pass.Tree.Width * pass.Tree.Height,
            EmptySpaceMeter.LargestEmptySquare(new LayoutTree(pass.Tree.Width, pass.Tree.Height, nodes)));
    }
    /// <summary>
    /// Returns <paramref name="effective"/> with its order-rank hint replaced by <paramref name="ranks"/>.
    /// </summary>
    /// <param name="effective">The scope's effective options.</param>
    /// <param name="ranks">The complete rank dictionary to apply.</param>
    /// <returns>Options carrying the new hint.</returns>
    private static LayoutOptions WithOrderRanks(LayoutOptions effective, Dictionary<LayoutGraphNode, double> ranks)
    {
        var overlay = new LayoutOptions();
        overlay.Set<IReadOnlyDictionary<LayoutGraphNode, double>?>(OrderRanks, ranks);
        return overlay.OverlayOnto(effective);
    }

    /// <summary>
    /// Returns <paramref name="effective"/> with <paramref name="loose"/> as the containers that keep their
    /// former room (see <see cref="LooseContainerNodes"/>).
    /// </summary>
    /// <param name="effective">The scope's effective options.</param>
    /// <param name="loose">The containers, at any depth, that stay loose.</param>
    /// <returns>Options that keep those containers loose.</returns>
    private static LayoutOptions WithLooseNodes(LayoutOptions effective, ISet<LayoutGraphNode> loose)
    {
        var overlay = new LayoutOptions();
        overlay.Set<ISet<LayoutGraphNode>?>(LooseContainerNodes, loose);
        return overlay.OverlayOnto(effective);
    }

    /// <summary>
    /// Returns <paramref name="effective"/> with <paramref name="paddings"/> as the container paddings (see
    /// <see cref="ContainerPaddings"/>).
    /// </summary>
    /// <param name="effective">The scope's effective options.</param>
    /// <param name="paddings">The padding each container needs.</param>
    /// <returns>Options that carry the paddings.</returns>
    private static LayoutOptions WithContainerPaddings(LayoutOptions effective, IReadOnlyDictionary<LayoutGraphNode, double> paddings)
    {
        var overlay = new LayoutOptions();
        overlay.Set<IReadOnlyDictionary<LayoutGraphNode, double>?>(ContainerPaddings, paddings);
        return overlay.OverlayOnto(effective);
    }

    /// <summary>
    /// Returns the padding every container enclosing the target of a routed edge needs so the target's
    /// end decoration fits between the container's border and the node the connector ends on: the decoration's
    /// length plus the clean-leg margin. The paddings an enclosing scope already decided are kept.
    /// </summary>
    /// <param name="routedEdges">The edges this scope routes.</param>
    /// <param name="nesting">The parent of every descendant node of the scope.</param>
    /// <param name="portOwner">The node that owns every named port of the scope's descendants.</param>
    /// <param name="inherited">The paddings an enclosing scope decided, or <see langword="null"/>.</param>
    /// <returns>The paddings, or <see langword="null"/> when no container needs any.</returns>
    private static Dictionary<LayoutGraphNode, double>? EntryPaddings(
        IReadOnlyList<LayoutGraphEdge> routedEdges,
        Dictionary<LayoutGraphNode, (LayoutGraphNode? Parent, int Index)> nesting,
        Dictionary<LayoutGraphPort, LayoutGraphNode> portOwner,
        IReadOnlyDictionary<LayoutGraphNode, double>? inherited)
    {
        var paddings = new Dictionary<LayoutGraphNode, double>();
        if (inherited != null)
        {
            foreach (var pair in inherited)
            {
                paddings[pair.Key] = pair.Value;
            }
        }
        foreach (var edge in routedEdges)
        {
            var needed = NotationMetrics.RenderedAlongLineLength(edge.TargetEnd, Themes.Light);
            if (needed <= 0.0)
            {
                continue;
            }

            var padding = needed + Themes.Light.CleanLegMargin;
            var target = edge.Target switch
            {
                LayoutGraphNode node => node,
                LayoutGraphPort port when portOwner.TryGetValue(port, out var owner) => owner,
                _ => null,
            };
            if (target is null || !nesting.TryGetValue(target, out var link))
            {
                continue;
            }

            for (var container = link.Parent; container != null; container = nesting.TryGetValue(container, out var up) ? up.Parent : null)
            {
                paddings[container] = paddings.TryGetValue(container, out var existing) ? Math.Max(padding, existing) : padding;
            }
        }

        return paddings.Count == 0 ? null : paddings;
    }

    /// <summary>
    /// Builds the order-rank proposals of a refinement round: per variant, the complete rank dictionary
    /// (the current hint plus a rank for every member of each container interior whose keyed members
    /// change places). A variant sorts the keyed members by the cross-axis position (bit 0 clear) or the
    /// other-axis position (bit 0 set) of their partners, ascending (bit 1 clear) or descending (bit 1
    /// set), the partner being the box the connector's other end landed on (bit 2 clear) or the box that
    /// stands for it at the level where the two ends meet (bit 2 set). Variants that change nothing, or
    /// duplicate an earlier one, are dropped.
    /// </summary>
    /// <param name="graph">The scope.</param>
    /// <param name="effective">The scope's effective options of the current best pass (carrying its hint).</param>
    /// <param name="pass">The current best pass of the scope.</param>
    /// <returns>The distinct proposals.</returns>
    private static List<Proposal> BuildProposals(LayoutGraph graph, LayoutOptions effective, ScopePass pass)
    {
        var proposals = new List<Proposal>();
        var fineKeys = ComputePartnerKeys(graph, pass, coarse: false);
        if (fineKeys.Count == 0)
        {
            return proposals;
        }

        var coarseKeys = ComputePartnerKeys(graph, pass, coarse: true);
        var current = effective.Get(OrderRanks);
        var childScopeOptions = new Dictionary<LayoutGraphNode, LayoutOptions>();
        var parents = new List<LayoutGraphNode>();
        foreach (var node in fineKeys.Keys)
        {
            var parent = pass.Nesting[node].Parent!;
            if (!parents.Contains(parent))
            {
                parents.Add(parent);
            }
        }

        for (var variant = 0; variant < VariantCount; variant++)
        {
            var keys = (variant & 4) == 0 ? fineKeys : coarseKeys;
            var ranks = new Dictionary<LayoutGraphNode, double>();
            if (current != null)
            {
                foreach (var pair in current)
                {
                    ranks[pair.Key] = pair.Value;
                }
            }

            var changed = false;
            foreach (var parent in parents)
            {
                var childEffective = ChildEffective(parent, effective, pass, childScopeOptions);
                var crossIsY = childEffective.Get(CoreOptions.Direction) is LayoutFlowDirection.Right or LayoutFlowDirection.Left;
                var useY = (variant & 1) == 0 ? crossIsY : !crossIsY;
                changed |= AssignRanks(parent, keys, useY, (variant & 2) != 0, current, ranks, pass.Nesting);
            }

            if (changed && !proposals.Exists(existing => SameRanks(existing.Ranks, ranks)))
            {
                proposals.Add(new Proposal(variant, ranks));
            }
        }

        return proposals;
    }

    /// <summary>
    /// Sorts the keyed members of the interior of <paramref name="parent"/> by their partner position into
    /// the slots the keyed members occupy (members without a key keep their slot) and records the
    /// resulting position of every member as its order rank.
    /// </summary>
    /// <remarks>
    /// A member without a key of its own that is joined by an interior connector to keyed members is
    /// placed with them by the leaf algorithms (a connected group is one unit), so it takes the median key
    /// of its group: otherwise it would hold its slot and pin the whole group in place.
    /// </remarks>
    /// <param name="parent">The container whose interior is ordered.</param>
    /// <param name="keys">The partner positions per node.</param>
    /// <param name="useY">Whether the vertical positions are used (otherwise the horizontal ones).</param>
    /// <param name="descending">Whether the members are sorted by descending position.</param>
    /// <param name="current">The hint currently in effect, used to detect a proposal that changes nothing.</param>
    /// <param name="proposal">The rank dictionary being built.</param>
    /// <param name="nesting">Parent and index-in-parent of every node of the scope's subtree.</param>
    /// <returns><see langword="true"/> when some member's rank differs from the one in effect.</returns>
    private static bool AssignRanks(
        LayoutGraphNode parent,
        Dictionary<LayoutGraphNode, (double X, double Y)> keys,
        bool useY,
        bool descending,
        IReadOnlyDictionary<LayoutGraphNode, double>? current,
        Dictionary<LayoutGraphNode, double> proposal,
        Dictionary<LayoutGraphNode, (LayoutGraphNode? Parent, int Index)> nesting)
    {
        var members = parent.Children.Nodes;

        // Round before comparing so sub-pixel differences between boxes that are effectively level do not
        // reorder them (and thereby make the result depend on layout noise); ties keep insertion order.
        var keyOf = new double?[members.Count];
        for (var i = 0; i < members.Count; i++)
        {
            if (keys.TryGetValue(members[i], out var key))
            {
                keyOf[i] = Math.Round(useY ? key.Y : key.X);
            }
        }

        foreach (var group in ConnectedGroups(parent, nesting))
        {
            var keyed = group.Where(i => keyOf[i].HasValue).Select(i => keyOf[i]!.Value).ToList();
            if (keyed.Count == 0)
            {
                continue;
            }

            var inherited = Math.Round(Median(keyed));
            foreach (var i in group.Where(i => !keyOf[i].HasValue))
            {
                keyOf[i] = inherited;
            }
        }

        var slots = Enumerable.Range(0, members.Count).Where(i => keyOf[i].HasValue).ToList();
        if (slots.Count < 2)
        {
            return false;
        }

        var sorted = (descending
                ? slots.OrderByDescending(i => keyOf[i])
                : slots.OrderBy(i => keyOf[i]))
            .ThenBy(i => i)
            .ToList();
        var order = Enumerable.Range(0, members.Count).ToArray();
        for (var k = 0; k < slots.Count; k++)
        {
            order[slots[k]] = sorted[k];
        }

        var changed = false;
        for (var position = 0; position < order.Length; position++)
        {
            var member = members[order[position]];
            proposal[member] = position;
            var inEffect = current != null && current.TryGetValue(member, out var value) ? value : order[position];
            if (Math.Abs(inEffect - position) > 0.0)
            {
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Splits the direct members of the interior of <paramref name="parent"/> into groups joined by one of
    /// its connectors (an endpoint nested deeper counts as the member that holds it). Each group lists its
    /// member indices ascending.
    /// </summary>
    /// <param name="parent">The container whose interior is grouped.</param>
    /// <param name="nesting">Parent and index-in-parent of every node of the scope's subtree.</param>
    /// <returns>The groups of member indices.</returns>
    private static List<List<int>> ConnectedGroups(
        LayoutGraphNode parent,
        Dictionary<LayoutGraphNode, (LayoutGraphNode? Parent, int Index)> nesting)
    {
        var count = parent.Children.Nodes.Count;
        var root = new int[count];
        for (var i = 0; i < count; i++)
        {
            root[i] = i;
        }

        int Find(int i)
        {
            while (root[i] != i)
            {
                root[i] = root[root[i]];
                i = root[i];
            }

            return i;
        }

        foreach (var edge in parent.Children.Edges)
        {
            var from = MemberIndex(parent, edge.Source, nesting);
            var to = MemberIndex(parent, edge.Target, nesting);
            if (from >= 0 && to >= 0)
            {
                var a = Find(from);
                var b = Find(to);
                if (a != b)
                {
                    root[Math.Max(a, b)] = Math.Min(a, b);
                }
            }
        }

        var groups = new List<List<int>>();
        var groupOf = new Dictionary<int, List<int>>();
        for (var i = 0; i < count; i++)
        {
            var key = Find(i);
            if (!groupOf.TryGetValue(key, out var group))
            {
                group = [];
                groupOf[key] = group;
                groups.Add(group);
            }

            group.Add(i);
        }

        return groups;
    }

    /// <summary>
    /// Returns the index, among the direct members of the interior of <paramref name="parent"/>, of the
    /// member that holds the edge endpoint <paramref name="endpoint"/> (itself; a port is not resolved).
    /// </summary>
    /// <param name="parent">The container that owns the interior.</param>
    /// <param name="endpoint">The edge endpoint.</param>
    /// <param name="nesting">Parent and index-in-parent of every node of the scope's subtree.</param>
    /// <returns>The member index, or -1 when the endpoint is not inside the interior.</returns>
    private static int MemberIndex(
        LayoutGraphNode parent,
        ILayoutConnectable endpoint,
        Dictionary<LayoutGraphNode, (LayoutGraphNode? Parent, int Index)> nesting)
    {
        if (endpoint is not LayoutGraphNode node)
        {
            return -1;
        }

        for (LayoutGraphNode? current = node; current != null && nesting.TryGetValue(current, out var place); current = place.Parent)
        {
            if (ReferenceEquals(place.Parent, parent))
            {
                return place.Index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Determines whether two rank dictionaries hold the same ranks.
    /// </summary>
    /// <param name="left">The first dictionary.</param>
    /// <param name="right">The second dictionary.</param>
    /// <returns><see langword="true"/> when both carry the same keys and values.</returns>
    private static bool SameRanks(Dictionary<LayoutGraphNode, double> left, Dictionary<LayoutGraphNode, double> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var value) || Math.Abs(value - pair.Value) > 0.0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Resolves the cascaded effective options of the children scope of <paramref name="container"/>,
    /// the same way <see cref="LayoutContainerChildren"/> does, so the interior's flow direction is known.
    /// </summary>
    /// <param name="container">A container node of the scope's subtree.</param>
    /// <param name="scopeEffective">The declaring scope's effective options.</param>
    /// <param name="pass">The scope's pass (supplying the nesting map).</param>
    /// <param name="memo">Memo of the already resolved containers.</param>
    /// <returns>The effective options of the container's children scope.</returns>
    private static LayoutOptions ChildEffective(
        LayoutGraphNode container,
        LayoutOptions scopeEffective,
        ScopePass pass,
        Dictionary<LayoutGraphNode, LayoutOptions> memo)
    {
        if (memo.TryGetValue(container, out var known))
        {
            return known;
        }

        var parent = pass.Nesting[container].Parent;
        var parentEffective = parent is null ? scopeEffective : ChildEffective(parent, scopeEffective, pass, memo);
        var nodeEffective = container.OverlayOnto(parentEffective);
        var resolved = container.Children.OverlayOnto(nodeEffective);
        memo[container] = resolved;
        return resolved;
    }

    /// <summary>
    /// Computes the partner position of every node on the endpoint chain of a routed cross-container
    /// connector: for a connector end inside a container, each node from the direct member's child down to
    /// the endpoint itself is keyed by the center of the box the connector's other end landed on (or, for
    /// the coarse keys, of the box that stands for that end at the level where the two ends meet), taking
    /// the median over all of that node's connectors. A node that contains the partner is not keyed.
    /// </summary>
    /// <param name="graph">The scope.</param>
    /// <param name="pass">The scope's current best pass, supplying the placed boxes.</param>
    /// <param name="coarse">Whether partners are the boxes standing for them where the two ends meet.</param>
    /// <returns>The keyed nodes with their horizontal and vertical partner positions, in a deterministic order.</returns>
    private static Dictionary<LayoutGraphNode, (double X, double Y)> ComputePartnerKeys(
        LayoutGraph graph,
        ScopePass pass,
        bool coarse)
    {
        var keys = new Dictionary<LayoutGraphNode, (double X, double Y)>();
        var topBoxes = pass.Tree.Nodes.OfType<LayoutBox>().ToArray();
        if (topBoxes.Length != graph.Nodes.Count)
        {
            return keys;
        }

        var indexOf = new Dictionary<LayoutGraphNode, int>(graph.Nodes.Count);
        for (var i = 0; i < graph.Nodes.Count; i++)
        {
            indexOf[graph.Nodes[i]] = i;
        }

        LayoutBox BoxAt(LayoutGraphNode node) =>
            ResolveEndpointBox(node, topBoxes, indexOf, pass.DescendantToDirect, pass.Nesting).Box;

        var samples = new Dictionary<LayoutGraphNode, (List<double> X, List<double> Y)>();
        var order = new List<LayoutGraphNode>();
        foreach (var edge in pass.RoutedEdges!)
        {
            if (edge.Source is not LayoutGraphNode source || edge.Target is not LayoutGraphNode target)
            {
                continue;
            }

            if (ReferenceEquals(BoxAt(source), BoxAt(target)))
            {
                continue;
            }

            AddChainSamples(source, target, coarse, BoxAt, pass.Nesting, samples, order);
            AddChainSamples(target, source, coarse, BoxAt, pass.Nesting, samples, order);
        }

        foreach (var node in order)
        {
            var (xs, ys) = samples[node];
            keys[node] = (Median(xs), Median(ys));
        }

        return keys;
    }

    /// <summary>
    /// Records the partner box center as a sample for every node of the chain that starts at
    /// <paramref name="endpoint"/> and climbs to (but excludes) the direct member, skipping a node that
    /// contains the partner.
    /// </summary>
    /// <param name="endpoint">The connector end whose chain is keyed.</param>
    /// <param name="partner">The connector's other end.</param>
    /// <param name="coarse">Whether the partner is the box standing for the other end where the two ends meet.</param>
    /// <param name="boxAt">Resolves the placed box of a node.</param>
    /// <param name="nesting">Parent and index-in-parent of every node of the scope's subtree.</param>
    /// <param name="samples">The accumulating samples per node.</param>
    /// <param name="order">The nodes in first-seen order.</param>
    private static void AddChainSamples(
        LayoutGraphNode endpoint,
        LayoutGraphNode partner,
        bool coarse,
        Func<LayoutGraphNode, LayoutBox> boxAt,
        Dictionary<LayoutGraphNode, (LayoutGraphNode? Parent, int Index)> nesting,
        Dictionary<LayoutGraphNode, (List<double> X, List<double> Y)> samples,
        List<LayoutGraphNode> order)
    {
        for (var current = endpoint; nesting[current].Parent != null; current = nesting[current].Parent!)
        {
            if (IsAncestor(current, partner, nesting))
            {
                continue;
            }

            var anchor = partner;
            if (coarse)
            {
                // Climb the partner's chain until its parent also encloses the keyed node: that ancestor
                // is the box the partner's connector heads for at the level where the two ends meet.
                while (nesting[anchor].Parent is { } above && !IsAncestor(above, current, nesting))
                {
                    anchor = above;
                }
            }

            var box = boxAt(anchor);
            if (!samples.TryGetValue(current, out var list))
            {
                list = ([], []);
                samples[current] = list;
                order.Add(current);
            }

            list.X.Add(box.X + (box.Width / 2.0));
            list.Y.Add(box.Y + (box.Height / 2.0));
        }
    }

    /// <summary>
    /// Returns the median of <paramref name="values"/> (the mean of the two middle values when the count
    /// is even, which is the mean itself for one or two values).
    /// </summary>
    /// <param name="values">The non-empty sample.</param>
    /// <returns>The median.</returns>
    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(value => value).ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2.0;
    }

    /// <summary>
    /// How much worse than the first pass, as a factor, the length and the bends of a refined scope may be:
    /// a proposal that removes a crossing by wrapping connectors around containers is not worth it.
    /// </summary>
    private const double RouteQualityTolerance = 1.25;

    /// <summary>
    /// What one crossing a proposal removes is worth, in the units of <see cref="RouteQualityPenalty"/>: the
    /// proposal's connectors may cost that much more than the first pass's, per crossing removed.
    /// </summary>
    private const double CrossingAllowance = 250.0;

    /// <summary>The price, in logical pixels of connector length, of one bend.</summary>
    private const double BendPenalty = 40.0;

    /// <summary>The price, in logical pixels of connector length, of one hairpin.</summary>
    private const double HairpinPenalty = 80.0;

    /// <summary>The price, in logical pixels of connector length, of one logical pixel of connector running within <see cref="RouteQuality.CloseParallelGap"/> of another.</summary>
    private const double ParallelPenalty = 0.5;

    /// <summary>
    /// Returns whether a scope's tight layout (containers trimmed to their content) is kept over its loose
    /// layout (containers keep their former room): its connectors must cross no more, double back no more,
    /// run no more closely beside one another, bend no more and be no longer than the loose layout's.
    /// </summary>
    /// <param name="tight">The tight layout.</param>
    /// <param name="loose">The loose layout.</param>
    /// <returns><see langword="true"/> when tightening does not make the routes worse.</returns>
    private static bool KeepsTightening(LayoutTree tight, LayoutTree loose)
    {
        var tightQuality = MeasureRoutes(tight);
        var looseQuality = MeasureRoutes(loose);
        return tightQuality.Hairpins <= looseQuality.Hairpins
            && tightQuality.Bends <= looseQuality.Bends
            && tightQuality.ParallelLength <= looseQuality.ParallelLength + TighteningTolerance
            && tightQuality.Length <= looseQuality.Length + TighteningTolerance
            && CountCrossings(tight) <= CountCrossings(loose)
            && CountCrowdedMarkers(tight) <= CountCrowdedMarkers(loose);
    }

    /// <summary>
    /// Counts the connector ends whose decoration (arrowhead, diamond) is crowded by a container border: the
    /// border of a container the connector enters crosses the straight approach closer to the end than the
    /// decoration is long, so the decoration overlaps the border.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The number of crowded decorated ends.</returns>
    private static int CountCrowdedMarkers(LayoutTree tree)
    {
        var containers = new List<LayoutBox>();
        var lines = new List<LayoutLine>();
        var pending = new Stack<IReadOnlyList<LayoutNode>>();
        pending.Push(tree.Nodes);
        while (pending.Count > 0)
        {
            foreach (var node in pending.Pop())
            {
                if (node is LayoutBox { Children.Count: > 0 } box)
                {
                    containers.Add(box);
                    pending.Push(box.Children);
                }
                else if (node is LayoutLine line)
                {
                    lines.Add(line);
                }
            }
        }

        var crowded = 0;
        foreach (var line in lines)
        {
            var w = line.Waypoints;
            if (w.Count < 2)
            {
                continue;
            }

            if (IsMarkerCrowded(w[^2], w[^1], line.TargetEnd, containers))
            {
                crowded++;
            }

            if (IsMarkerCrowded(w[1], w[0], line.SourceEnd, containers))
            {
                crowded++;
            }
        }

        return crowded;
    }

    /// <summary>
    /// Returns whether a container border crosses the segment from <paramref name="from"/> to the decorated
    /// end <paramref name="end"/> closer to the end than the decoration is long.
    /// </summary>
    /// <param name="from">The other end of the final segment.</param>
    /// <param name="end">The decorated end point.</param>
    /// <param name="marker">The decoration at <paramref name="end"/>.</param>
    /// <param name="containers">The container boxes of the tree.</param>
    /// <returns><see langword="true"/> when the decoration would overlap a container border.</returns>
    private static bool IsMarkerCrowded(Point2D from, Point2D end, EndMarkerStyle marker, List<LayoutBox> containers)
    {
        var needed = NotationMetrics.RenderedAlongLineLength(marker, Themes.Light);
        if (needed <= 0.0)
        {
            return false;
        }

        var horizontal = Math.Abs(from.Y - end.Y) < SegmentEpsilon;
        var vertical = Math.Abs(from.X - end.X) < SegmentEpsilon;
        foreach (var box in containers)
        {
            double along;
            double[] borders;
            bool crosses;
            if (horizontal)
            {
                crosses = end.Y > box.Y + SegmentEpsilon && end.Y < box.Y + box.Height - SegmentEpsilon;
                along = end.X;
                borders = [box.X, box.X + box.Width];
            }
            else if (vertical)
            {
                crosses = end.X > box.X + SegmentEpsilon && end.X < box.X + box.Width - SegmentEpsilon;
                along = end.Y;
                borders = [box.Y, box.Y + box.Height];
            }
            else
            {
                return false;
            }

            if (!crosses)
            {
                continue;
            }

            var start = horizontal ? from.X : from.Y;
            foreach (var border in borders)
            {
                var distance = Math.Abs(along - border);
                if (distance > SegmentEpsilon && distance < needed
                    && border > Math.Min(start, along) + SegmentEpsilon && border < Math.Max(start, along) - SegmentEpsilon)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Tolerance, in logical pixels, when comparing the connector length of a tight and a loose layout.</summary>
    private const double TighteningTolerance = 1.0;

    /// <summary>
    /// Measures the visual quality (length, bends, hairpins, close parallel runs) of every connector of the
    /// tree, at every nesting depth.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The measured summary.</returns>
    private static RouteQuality.Summary MeasureRoutes(LayoutTree tree)
    {
        var lines = new List<IReadOnlyList<Point2D>>();
        CollectLines(tree.Nodes, lines);
        return RouteQuality.Measure(lines);
    }

    /// <summary>Returns the visual cost of a set of connectors: length plus the price of its bends, hairpins and close parallel runs.</summary>
    private static double RouteQualityPenalty(RouteQuality.Summary summary) =>
        summary.Length + (summary.Bends * BendPenalty) + (summary.Hairpins * HairpinPenalty) + (summary.ParallelLength * ParallelPenalty);

    /// <summary>
    /// Returns whether a proposal's connectors are no visibly worse than the first pass's: no more than
    /// <see cref="RouteQualityTolerance"/> times the length and bends (so crossings are not bought with a
    /// wrap around a container), and a visual cost (see <see cref="RouteQualityPenalty"/>) that exceeds the
    /// first pass's by no more than <see cref="CrossingAllowance"/> per crossing the proposal removes.
    /// </summary>
    /// <param name="candidate">The proposal's connector quality.</param>
    /// <param name="reference">The first pass's connector quality.</param>
    /// <param name="removedCrossings">How many crossings the proposal removes relative to the first pass.</param>
    /// <returns><see langword="true"/> when the proposal keeps the first pass's route quality.</returns>
    private static bool KeepsRouteQuality(RouteQuality.Summary candidate, RouteQuality.Summary reference, int removedCrossings) =>
        candidate.Length <= reference.Length * RouteQualityTolerance
        && candidate.Bends <= Math.Floor(reference.Bends * RouteQualityTolerance)
        && RouteQualityPenalty(candidate) <= RouteQualityPenalty(reference) + (removedCrossings * CrossingAllowance);

    /// <summary>
    /// Counts the proper crossings between distinct connectors over the whole tree, at every nesting depth:
    /// two segments of different lines that intersect at a point interior to both. Touches at a shared end
    /// point, a line ending on another line, and collinear overlaps are not crossings.
    /// </summary>
    /// <param name="tree">The placed tree.</param>
    /// <returns>The number of crossings.</returns>
    internal static int CountCrossings(LayoutTree tree)
    {
        var lines = new List<IReadOnlyList<Point2D>>();
        CollectLines(tree.Nodes, lines);
        return CountLineCrossings(lines);
    }

    /// <summary>
    /// Counts the proper crossings between distinct polylines.
    /// </summary>
    /// <param name="lines">The waypoint lists of the lines.</param>
    /// <returns>The number of crossings.</returns>
    private static int CountLineCrossings(List<IReadOnlyList<Point2D>> lines)
    {
        var bounds = new (double MinX, double MinY, double MaxX, double MaxY)[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            var points = lines[i];
            bounds[i] = (points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));
        }

        var count = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            for (var j = i + 1; j < lines.Count; j++)
            {
                if (bounds[i].MaxX < bounds[j].MinX || bounds[j].MaxX < bounds[i].MinX
                    || bounds[i].MaxY < bounds[j].MinY || bounds[j].MaxY < bounds[i].MinY)
                {
                    continue;
                }

                count += CountPolylineCrossings(lines[i], lines[j]);
            }
        }

        return count;
    }

    /// <summary>
    /// Collects the waypoint lists of every line of <paramref name="nodes"/> at every nesting depth.
    /// </summary>
    /// <param name="nodes">The nodes to walk.</param>
    /// <param name="lines">The accumulating waypoint lists.</param>
    private static void CollectLines(IReadOnlyList<LayoutNode> nodes, List<IReadOnlyList<Point2D>> lines)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case LayoutLine line when line.Waypoints.Count >= 2:
                    lines.Add(line.Waypoints);
                    break;
                case LayoutBox box:
                    CollectLines(box.Children, lines);
                    break;
            }
        }
    }

    /// <summary>
    /// Counts the segment pairs of two polylines that cross at a point interior to both.
    /// </summary>
    /// <param name="first">The waypoints of the first line.</param>
    /// <param name="second">The waypoints of the second line.</param>
    /// <returns>The number of crossing segment pairs.</returns>
    private static int CountPolylineCrossings(IReadOnlyList<Point2D> first, IReadOnlyList<Point2D> second)
    {
        var count = 0;
        for (var i = 0; i + 1 < first.Count; i++)
        {
            for (var j = 0; j + 1 < second.Count; j++)
            {
                if (SegmentsCross(first[i], first[i + 1], second[j], second[j + 1]))
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Determines whether two segments cross at a point strictly interior to both.
    /// </summary>
    /// <param name="a1">The first end of the first segment.</param>
    /// <param name="a2">The second end of the first segment.</param>
    /// <param name="b1">The first end of the second segment.</param>
    /// <param name="b2">The second end of the second segment.</param>
    /// <returns><see langword="true"/> when the segments cross properly.</returns>
    private static bool SegmentsCross(Point2D a1, Point2D a2, Point2D b1, Point2D b2)
    {
        var aHorizontal = Math.Abs(a1.Y - a2.Y) < SegmentEpsilon;
        var aVertical = Math.Abs(a1.X - a2.X) < SegmentEpsilon;
        var bHorizontal = Math.Abs(b1.Y - b2.Y) < SegmentEpsilon;
        var bVertical = Math.Abs(b1.X - b2.X) < SegmentEpsilon;
        if ((aHorizontal && bVertical) || (aVertical && bHorizontal))
        {
            var (h1, h2, v1, v2) = aHorizontal ? (a1, a2, b1, b2) : (b1, b2, a1, a2);
            return v1.X > Math.Min(h1.X, h2.X) + SegmentEpsilon
                && v1.X < Math.Max(h1.X, h2.X) - SegmentEpsilon
                && h1.Y > Math.Min(v1.Y, v2.Y) + SegmentEpsilon
                && h1.Y < Math.Max(v1.Y, v2.Y) - SegmentEpsilon;
        }

        if ((aHorizontal || aVertical) && (bHorizontal || bVertical))
        {
            // Parallel orthogonal segments never cross (a collinear overlap is not a crossing).
            return false;
        }

        // At least one diagonal segment: a proper intersection has strictly opposite sides on both.
        var d1 = Orientation(b1, b2, a1);
        var d2 = Orientation(b1, b2, a2);
        var d3 = Orientation(a1, a2, b1);
        var d4 = Orientation(a1, a2, b2);
        return d1 * d2 < 0.0 && d3 * d4 < 0.0;
    }

    /// <summary>
    /// Returns the signed area of the triangle (a, b, c); its sign tells which side c lies on.
    /// </summary>
    /// <param name="a">The first corner.</param>
    /// <param name="b">The second corner.</param>
    /// <param name="c">The third corner.</param>
    /// <returns>The signed (doubled) area.</returns>
    private static double Orientation(Point2D a, Point2D b, Point2D c) =>
        ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
}
