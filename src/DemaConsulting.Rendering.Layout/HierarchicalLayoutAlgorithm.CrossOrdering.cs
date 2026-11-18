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
///     predicts to be better, and is left alone when it already has no crossing. Interior layouts are
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
        /// Gets the loose containers (not tightened to their content) of the scopes laid out so far, so a
        /// refinement pass starts with what the first pass of the scope found out.
        /// </summary>
        private Dictionary<LayoutGraph, HashSet<LayoutGraphNode>> LooseContainers { get; } = [];

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

        var key = (node, RankSignature(node, childEffective.Get(OrderRanks)) + (tighten ? "|t" : "|u"));
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

        var proposals = BuildProposals(graph, effective, first);
        if (proposals.Count == 0)
        {
            return first.Tree;
        }

        var best = first;
        var bestEffective = effective;
        var bestCost = CountCrossings(best.Tree);
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
                .Where(p => !heavy || p.Score.EmptySquare <= Math.Max(current.EmptySquare, passingSquare) + AreaTolerance)
                .OrderBy(p => p.Variant == PreferredVariant ? 0 : 1)
                .ThenBy(p => p.Score.Crossings)
                .ThenBy(p => p.Score.Area)
                .ThenBy(p => p.Variant))
            {
                if (routed >= RoutedProposalsInRound(round, heavy))
                {
                    break;
                }

                // An expensive scope only pays for a proposal its placement-only score predicts to win.
                if (heavy
                    && (proposal.Score.Crossings > current.Crossings
                        || (proposal.Score.Crossings == current.Crossings
                            && proposal.Score.Area >= current.Area - AreaTolerance)))
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
                    if (cost == 0 || emptySquare <= Math.Max(bestEmptySquare, passingSquare) + AreaTolerance)
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
        var childEffectives = new Dictionary<LayoutGraphNode, LayoutOptions>();
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
                var childEffective = ChildEffective(parent, effective, pass, childEffectives);
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
