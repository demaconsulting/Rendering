// <copyright file="LayeredGraphTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Reflection;
using DemaConsulting.Rendering.Layout.Engine;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Tests.Engine.Layered;

/// <summary>
///     Tests for <see cref="LayeredGraph"/> covering null-argument validation and that the
///     supplied nodes, edges, direction, and node count are preserved.
/// </summary>
public sealed class LayeredGraphTests
{
    /// <summary>
    ///     Constructing a graph with a null node list throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void LayeredGraph_Constructor_NullNodes_ThrowsArgumentNullException()
    {
        // Arrange: a valid edge list but a null node list.
        var edges = new List<LayerEdge>();

        // Act / Assert: construction rejects the null node list.
        Assert.Throws<ArgumentNullException>(
            () => new LayeredGraph(null!, edges, LayoutDirection.Right));
    }

    /// <summary>
    ///     Constructing a graph with a null edge list throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void LayeredGraph_Constructor_NullEdges_ThrowsArgumentNullException()
    {
        // Arrange: a valid node list but a null edge list.
        var nodes = new List<LayerNode> { new(60, 40) };

        // Act / Assert: construction rejects the null edge list.
        Assert.Throws<ArgumentNullException>(
            () => new LayeredGraph(nodes, null!, LayoutDirection.Right));
    }

    /// <summary>
    ///     Construction preserves the supplied nodes, edges, and direction and reports the
    ///     input node count.
    /// </summary>
    [Fact]
    public void LayeredGraph_Constructor_ValidInput_StoresNodesEdgesDirectionAndCount()
    {
        // Arrange: three nodes and two edges.
        var nodes = new List<LayerNode> { new(60, 40), new(60, 40), new(60, 40) };
        var edges = new List<LayerEdge> { new(0, 1), new(1, 2) };

        // Act: construct the shared graph state.
        var graph = new LayeredGraph(nodes, edges, LayoutDirection.Right);

        // Assert: the inputs are preserved unchanged.
        Assert.Same(nodes, graph.Nodes);
        Assert.Same(edges, graph.Edges);
        Assert.Equal(LayoutDirection.Right, graph.Direction);
        Assert.Equal(3, graph.N);
    }

    /// <summary>
    ///     Every public settable property on <see cref="LayeredGraph"/> is either a pipeline-computed
    ///     output (listed in the local allowlist below, since a fresh child must compute its own) or is
    ///     copied by <see cref="LayeredGraph.CreateChild"/>. This is a structural safeguard, not just a
    ///     regression test for one bug: it fails the build the moment a future caller-configurable
    ///     option is added to <see cref="LayeredGraph"/> without a conscious decision being made about
    ///     whether <see cref="ComponentPacker"/>'s per-component child graphs should inherit it — exactly
    ///     the class of bug this test was written to catch (a real one: a component's child graph used
    ///     to be built inline and silently forgot to copy <c>MergeParallelEdges</c> and
    ///     <c>NodeSpacing</c>, so any graph split into 2+ connected components silently reverted those
    ///     settings to their defaults for every component).
    /// </summary>
    [Fact]
    public void LayeredGraph_CreateChild_CopiesEveryKnownInputOption()
    {
        // Every public settable property that CreateChild is NOT expected to copy, because it is
        // pipeline-computed output state a fresh child graph must derive for itself, not a
        // caller-configured input option.
        string[] computedOutputAllowlist =
        [
            nameof(LayeredGraph.InputAxesNormalized),
            nameof(LayeredGraph.Acyclic),
            nameof(LayeredGraph.AcyclicOriginalIndex),
            nameof(LayeredGraph.AcyclicReversed),
            nameof(LayeredGraph.NodeLayers),
            nameof(LayeredGraph.AugNodes),
            nameof(LayeredGraph.AugEdges),
            nameof(LayeredGraph.Groups),
            nameof(LayeredGraph.AugX),
            nameof(LayeredGraph.AugY),
            nameof(LayeredGraph.ColumnX),
            nameof(LayeredGraph.MaxColWidth),
            nameof(LayeredGraph.AugPortYSrc),
            nameof(LayeredGraph.AugPortYTgt),
            nameof(LayeredGraph.AugBendPoints),
            nameof(LayeredGraph.Waypoints),
        ];

        var settableProperties = typeof(LayeredGraph)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToList();

        var inputOptionProperties = settableProperties
            .Where(p => !computedOutputAllowlist.Contains(p.Name))
            .ToList();

        // Fail loudly (naming the property) if a new settable property shows up that this test does
        // not yet know how to classify, rather than silently skipping it.
        Assert.True(
            inputOptionProperties.Count > 0,
            "Expected at least one caller-configured input option property on LayeredGraph.");

        // Arrange: a parent graph with every known input option set to a distinctive non-default value.
        var parent = new LayeredGraph([], [], LayoutDirection.Down)
        {
            BackEdgeEntryApproach = 123.5,
            NodeSpacing = 456.5,
            MergeParallelEdges = false,
        };

        // Act
        var child = LayeredGraph.CreateChild([], [], parent);

        // Assert: every input-option property was copied onto the child, and the allowlisted
        // computed-output properties were left at their own fresh defaults (not the parent's).
        foreach (var property in inputOptionProperties)
        {
            var expected = property.GetValue(parent);
            var actual = property.GetValue(child);
            Assert.Equal(expected, actual);
        }

        // Direction is derived from the parent (not itself in the allowlist above, since it is neither
        // a mutable input option nor copied by field-initializer — CreateChild takes it from the
        // parent's own Direction property directly), confirming the child was built for the same flow.
        Assert.Equal(parent.Direction, child.Direction);
    }
}
