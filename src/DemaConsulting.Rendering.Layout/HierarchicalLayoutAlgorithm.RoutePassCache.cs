// <copyright file="HierarchicalLayoutAlgorithm.RoutePassCache.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout.Engine;

namespace DemaConsulting.Rendering.Layout;

/// <summary>
/// The exact route-pass cache of <see cref="HierarchicalLayoutAlgorithm"/>.
/// </summary>
/// <remarks>
///     <para>
///     A scope is laid out several times in one run (the tight, loose and reference layouts and the ordering
///     proposals of its parent's refinement), and a nested scope again for every layout of the scopes that
///     hold it. Most of those layouts hand the router the very same composed boxes as an earlier one: a
///     different order or room hint of the parent frequently leaves the interior of a child unchanged.
///     Routing the same problem again is what makes a hierarchy of everyday size cost the product of its
///     levels' pass counts.
///     </para>
///     <para>
///     The cache replays the lines an earlier pass produced for the same scope, the same composed boxes (with
///     every line already inside them), the same edge-routing style and the same font size. The router is a
///     pure function of exactly these inputs while the scope's budget stays at the full effort, so a replay
///     returns what routing would have returned, coordinate for coordinate. A replay is only made when it is
///     exact: the budget is at the full effort and the work the original pass charged still leaves it there
///     (below the soft and the hard limit), and the replay charges that same work, so the budget, the tier
///     decisions and the hints that follow from them are the same with or without the cache.
///     </para>
///     <para>
///     The inputs are identified by a digest of everything the boxes carry (see
///     <see cref="RoutePassCache.StructuralSignature"/>), so a property added to the layout model later is
///     part of the key without anyone having to remember it.
///     </para>
/// </remarks>
public sealed partial class HierarchicalLayoutAlgorithm
{
    /// <summary>
    /// Routes the cross-container edges of a scope (see <see cref="RouteCrossContainerEdges"/>), replaying
    /// the lines of an earlier pass over the same routing problem instead of searching again whenever the
    /// replay is exact (see <see cref="RoutePassCache"/>).
    /// </summary>
    /// <param name="scope">The container scope the edges belong to.</param>
    /// <param name="routedEdges">The edges this scope must route, per <see cref="ClassifyEdges"/>.</param>
    /// <param name="composed">The composed top-level boxes of this scope, aligned with its nodes by index.</param>
    /// <param name="indexOf">Map from each direct-member node to its positional index in <paramref name="composed"/>.</param>
    /// <param name="descendantToDirect">Map from every descendant node to the direct member that owns it.</param>
    /// <param name="placement">Parent and index-in-parent of every node of this scope's subtree.</param>
    /// <param name="effective">This scope's cascaded effective options.</param>
    /// <param name="violators">Receives the direct-member containers whose own border a routed connector hugs.</param>
    /// <returns>One routed line per edge in <paramref name="routedEdges"/>; a new list on every call.</returns>
    private static List<LayoutLine> RouteCrossContainerEdgesCached(
        LayoutGraph scope,
        List<LayoutGraphEdge> routedEdges,
        LayoutBox[] composed,
        Dictionary<LayoutGraphNode, int> indexOf,
        Dictionary<LayoutGraphNode, LayoutGraphNode> descendantToDirect,
        Dictionary<LayoutGraphNode, (LayoutGraphNode? Parent, int Index)> placement,
        LayoutOptions effective,
        HashSet<LayoutGraphNode> violators)
    {
        // Only a pass that starts at the full effort can be replayed or stored: at any lower tier the
        // router searches less, so the lines depend on the work already spent and not on the boxes alone.
        var cache = effective.Get(RefinementContext)?.RoutePasses;
        var budget = effective.Get(RoutingBudget);
        if (cache is null || routedEdges.Count == 0 || budget is not { Tier: RoutingTier.Full })
        {
            return RouteCrossContainerEdges(routedEdges, composed, indexOf, descendantToDirect, placement, effective, violators);
        }

        // Replay an earlier pass over the same problem when that is exact
        var key = cache.KeyOf(effective.Get(CoreOptions.EdgeRouting), effective.Get(CoreOptions.AssumedFontSize), composed);
        if (cache.TryReplay(scope, key, budget, violators, out var replayed))
        {
            return replayed;
        }

        // Route for real, and keep the outcome when the whole pass ran at the full effort
        var found = new HashSet<LayoutGraphNode>();
        var spentBefore = budget.Spent;
        var routed = RouteCrossContainerEdges(routedEdges, composed, indexOf, descendantToDirect, placement, effective, found);
        violators.UnionWith(found);
        if (budget.Tier == RoutingTier.Full)
        {
            cache.Store(scope, key, routed, found, budget.Spent - spentBefore);
        }

        return routed;
    }

    /// <summary>
    /// The per-run memo of routed passes: what routing the composed boxes of a scope produced, and what it
    /// cost, keyed by a digest of everything the router was given.
    /// </summary>
    /// <remarks>
    /// Belongs to one layout run (it lives in the run's <see cref="OrderingRefinementContext"/>) and is not
    /// thread safe. The number of entries is capped; once it is reached routing simply runs, which is
    /// exact too.
    /// </remarks>
    internal sealed class RoutePassCache
    {
        /// <summary>The most routed passes the cache keeps; beyond it a pass is routed and not stored.</summary>
        internal const int MaxEntries = 2048;

        /// <summary>The routed passes, by scope and input digest.</summary>
        private readonly Dictionary<(LayoutGraph Scope, string Key), Entry> _entries = [];

        /// <summary>The identity numbers given to the objects a signature cannot look into.</summary>
        private readonly Dictionary<object, int> _identities = new(IdentityComparer.Instance);

        /// <summary>
        /// Computes the key of a routing problem: a digest of the edge-routing style, the font size and
        /// every property of every composed box, at any depth, down to the last waypoint of every line
        /// already inside the boxes.
        /// </summary>
        /// <param name="edgeRouting">The edge-routing style the router is given.</param>
        /// <param name="fontSize">The assumed font size the router is given.</param>
        /// <param name="composed">The composed boxes of the scope.</param>
        /// <returns>The digest, equal for equal routing problems.</returns>
        internal string KeyOf(object edgeRouting, double fontSize, LayoutBox[] composed)
        {
            var text = new StringBuilder();
            StructuralSignature.Append(text, edgeRouting, _identities);
            StructuralSignature.Append(text, fontSize, _identities);
            StructuralSignature.Append(text, composed, _identities);
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }

        /// <summary>
        /// Replays the lines of an earlier pass over the same routing problem when the replay is exact:
        /// the budget is at the full effort, and stays there after the work of that pass.
        /// </summary>
        /// <param name="scope">The container scope.</param>
        /// <param name="key">The digest of the routing problem (see <see cref="KeyOf"/>).</param>
        /// <param name="budget">The scope's work budget; charged the work of the replayed pass on a replay.</param>
        /// <param name="violators">Receives, on a replay, the containers whose border the lines hug.</param>
        /// <param name="lines">On a replay, a new list of the lines the earlier pass routed.</param>
        /// <returns><see langword="true"/> when the pass was replayed.</returns>
        internal bool TryReplay(
            LayoutGraph scope,
            string key,
            RoutingWorkBudget budget,
            HashSet<LayoutGraphNode> violators,
            out List<LayoutLine> lines)
        {
            // A replay below the full effort would hide a tier change the original pass did not have
            if (!_entries.TryGetValue((scope, key), out var entry) || !budget.WouldStayFull(entry.Work))
            {
                lines = [];
                return false;
            }

            // The replay stands for the pass: it spends what that pass spent, so the budget's tier and the
            // hints that follow from it are the same as if the router had searched
            budget.Charge(entry.Work);
            violators.UnionWith(entry.Violators);
            lines = [.. entry.Lines];
            return true;
        }

        /// <summary>
        /// Keeps the outcome of a pass that was routed at the full effort from start to end.
        /// </summary>
        /// <param name="scope">The container scope.</param>
        /// <param name="key">The digest of the routing problem (see <see cref="KeyOf"/>).</param>
        /// <param name="lines">The lines the pass routed.</param>
        /// <param name="violators">The containers whose border the lines hug.</param>
        /// <param name="work">The work units the pass charged to the budget.</param>
        internal void Store(
            LayoutGraph scope,
            string key,
            IReadOnlyList<LayoutLine> lines,
            HashSet<LayoutGraphNode> violators,
            long work)
        {
            if (_entries.Count < MaxEntries)
            {
                _entries[(scope, key)] = new Entry([.. lines], [.. violators], work);
            }
        }

        /// <summary>One routed pass: its lines, the containers they hug and the work it cost.</summary>
        /// <param name="Lines">The routed lines, one per connection.</param>
        /// <param name="Violators">The direct-member containers whose border a line hugs.</param>
        /// <param name="Work">The work units the pass charged to its budget.</param>
        private sealed record Entry(LayoutLine[] Lines, LayoutGraphNode[] Violators, long Work);

        /// <summary>
        /// Compares objects by reference, so the objects a signature cannot look into (the graph's own
        /// nodes and ports, which a layout node may refer to) are told apart by identity.
        /// </summary>
        private sealed class IdentityComparer : IEqualityComparer<object>
        {
            /// <summary>Gets the shared instance.</summary>
            internal static IdentityComparer Instance { get; } = new();

            /// <inheritdoc/>
            bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);

            /// <inheritdoc/>
            int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }

        /// <summary>
        /// Writes a canonical text of a value that is equal for equal values and differs for any difference
        /// in any public property of the layout model's records, so a property added to the model later is
        /// covered without a change here.
        /// </summary>
        /// <remarks>
        /// The records of the layout model (and the lists and structs they are made of) are described by
        /// walking their public properties; floating-point numbers by their exact bits; every other object
        /// (a graph node or port a layout node refers to, which a layout run never replaces) by an identity
        /// number. Stateless apart from the cache of reflected property lists, which is thread safe.
        /// </remarks>
        internal static class StructuralSignature
        {
            /// <summary>The public readable properties of each record type already walked.</summary>
            private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Properties = new();

            /// <summary>
            /// Appends the canonical text of <paramref name="value"/> to <paramref name="text"/>.
            /// </summary>
            /// <param name="text">The builder to append to.</param>
            /// <param name="value">The value to describe; <see langword="null"/> is allowed.</param>
            /// <param name="identities">The identity numbers of the objects that are not looked into.</param>
            internal static void Append(StringBuilder text, object? value, Dictionary<object, int> identities)
            {
                switch (value)
                {
                    case null:
                        text.Append('~');
                        return;

                    case string s:
                        text.Append('"').Append(s.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(s);
                        return;

                    case double d:
                        text.Append('d').Append(BitConverter.DoubleToInt64Bits(d).ToString("X", CultureInfo.InvariantCulture)).Append(',');
                        return;

                    case float f:
                        text.Append('f').Append(BitConverter.DoubleToInt64Bits(f).ToString("X", CultureInfo.InvariantCulture)).Append(',');
                        return;

                    case Enum e:
                        text.Append('e').Append(Convert.ToInt64(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)).Append(',');
                        return;

                    case IConvertible c when value.GetType().IsPrimitive || value is decimal:
                        text.Append('p').Append(c.ToString(CultureInfo.InvariantCulture)).Append(',');
                        return;

                    case IEnumerable items:
                        text.Append('[');
                        foreach (var item in items)
                        {
                            Append(text, item, identities);
                        }

                        text.Append(']');
                        return;
                }

                // A record of the layout model (or a struct of it) is its type and its properties; any
                // other object is told apart by identity only
                var type = value.GetType();
                if (!IsModelValue(type))
                {
                    if (!identities.TryGetValue(value, out var id))
                    {
                        id = identities.Count;
                        identities[value] = id;
                    }

                    text.Append('@').Append(id.ToString(CultureInfo.InvariantCulture)).Append(',');
                    return;
                }

                text.Append('{').Append(type.Name).Append(':');
                foreach (var property in PropertiesOf(type))
                {
                    Append(text, property.GetValue(value), identities);
                }

                text.Append('}');
            }

            /// <summary>
            /// Returns whether <paramref name="type"/> is a value the signature can look into: a struct, or
            /// a record (a class with the compiler's clone method) declared in the layout model's assembly.
            /// </summary>
            /// <param name="type">The runtime type of the value.</param>
            /// <returns><see langword="true"/> for the layout model's records and structs.</returns>
            private static bool IsModelValue(Type type) =>
                type.Assembly == typeof(LayoutNode).Assembly
                && (type.IsValueType || type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null);

            /// <summary>
            /// Returns the public readable instance properties of <paramref name="type"/>, in a fixed order.
            /// </summary>
            /// <param name="type">The record or struct type.</param>
            /// <returns>The properties, without indexers.</returns>
            private static PropertyInfo[] PropertiesOf(Type type) =>
                Properties.GetOrAdd(
                    type,
                    t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                        .OrderBy(p => p.Name, StringComparer.Ordinal)
                        .ToArray());
        }
    }
}
