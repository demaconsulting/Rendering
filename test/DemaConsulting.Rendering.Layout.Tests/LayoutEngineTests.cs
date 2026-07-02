// <copyright file="LayoutEngineTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests for the <see cref="LayoutEngine"/> batteries-included facade, covering algorithm
///     resolution (graph precedence over options, default to hierarchical), the flat-graph equivalence
///     to the corresponding leaf algorithm, nested-graph composition, custom-registry support, and
///     null-argument validation.
/// </summary>
public sealed class LayoutEngineTests
{
    /// <summary>
    ///     Proves the facade's declared default algorithm identifier is "hierarchical".
    /// </summary>
    [Fact]
    public void DefaultAlgorithmId_IsHierarchical()
    {
        // Act / Assert
        Assert.Equal("hierarchical", LayoutEngine.DefaultAlgorithmId);
    }

    /// <summary>
    ///     Proves that laying out a flat graph with no declared algorithm produces output identical to
    ///     the layered leaf algorithm, because the default hierarchical engine is byte-for-byte
    ///     equivalent to its leaf algorithm on a flat graph.
    /// </summary>
    [Fact]
    public void Layout_FlatGraphNoAlgorithmDeclared_MatchesLayeredLeafExactly()
    {
        // Arrange
        var graph = BuildFlatGraph();
        var options = new LayoutOptions();

        // Act
        var expected = new LayeredLayoutAlgorithm().Apply(graph, options);
        var actual = LayoutEngine.Layout(graph, options);

        // Assert
        AssertTreesIdentical(expected, actual);
    }

    /// <summary>
    ///     Proves that an explicit "layered" declaration on the options resolves the layered algorithm,
    ///     producing output identical to invoking that algorithm directly.
    /// </summary>
    [Fact]
    public void Layout_OptionsDeclareLayered_MatchesLayeredAlgorithmExactly()
    {
        // Arrange
        var graph = BuildFlatGraph();
        var options = LayoutOptions.ForAlgorithm("layered");

        // Act
        var expected = new LayeredLayoutAlgorithm().Apply(graph, options);
        var actual = LayoutEngine.Layout(graph, options);

        // Assert
        AssertTreesIdentical(expected, actual);
    }

    /// <summary>
    ///     Proves that an explicit "containment" declaration on the options resolves the containment
    ///     algorithm, producing output identical to invoking that algorithm directly.
    /// </summary>
    [Fact]
    public void Layout_OptionsDeclareContainment_MatchesContainmentAlgorithmExactly()
    {
        // Arrange
        var graph = BuildFlatGraph();
        var options = LayoutOptions.ForAlgorithm("containment");

        // Act
        var expected = new ContainmentLayoutAlgorithm().Apply(graph, options);
        var actual = LayoutEngine.Layout(graph, options);

        // Assert
        AssertTreesIdentical(expected, actual);
    }

    /// <summary>
    ///     Proves the graph's explicit algorithm declaration takes precedence over the options'
    ///     declaration: a graph declaring "containment" is packed even when the options declare "layered".
    /// </summary>
    [Fact]
    public void Layout_GraphDeclarationOverridesOptions()
    {
        // Arrange: the graph declares containment, the options declare layered
        var graph = BuildFlatGraph();
        graph.Set(CoreOptions.Algorithm, "containment");
        var options = LayoutOptions.ForAlgorithm("layered");

        // Act
        var expected = new ContainmentLayoutAlgorithm().Apply(graph, options);
        var actual = LayoutEngine.Layout(graph, options);

        // Assert: the containment result wins
        AssertTreesIdentical(expected, actual);
    }

    /// <summary>
    ///     Proves that a nested (compound) graph laid out with the default hierarchical engine produces a
    ///     composed tree in which the container box carries its recursively laid-out children.
    /// </summary>
    [Fact]
    public void Layout_NestedGraphNoAlgorithmDeclared_ProducesComposedTree()
    {
        // Arrange: a container holding two nested leaves, plus a sibling leaf at the root
        var graph = new LayoutGraph();
        var group = graph.AddNode("group", 10, 10);
        group.Label = "Group";
        var c1 = group.Children.AddNode("c1", 80, 40);
        var c2 = group.Children.AddNode("c2", 80, 40);
        group.Children.AddEdge("c1-c2", c1, c2);
        graph.AddNode("outside", 80, 40);

        // Act
        var tree = LayoutEngine.Layout(graph, new LayoutOptions());

        // Assert: the container box was composed with its nested children
        var containerBox = Assert.Single(tree.Nodes.OfType<LayoutBox>(), box => box.Children.Count > 0);
        Assert.Equal(2, containerBox.Children.OfType<LayoutBox>().Count());
    }

    /// <summary>
    ///     Proves a caller-supplied registry is honored: an algorithm registered only in a custom
    ///     registry is resolved and applied when the graph declares its identifier.
    /// </summary>
    [Fact]
    public void Layout_CustomRegistry_ResolvesRegisteredAlgorithm()
    {
        // Arrange: a registry carrying a stub algorithm selected by the graph
        var registry = new LayoutAlgorithmRegistry().Register(new StubAlgorithm());
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "stub");

        // Act
        var tree = LayoutEngine.Layout(graph, new LayoutOptions(), registry);

        // Assert: the stub's sentinel canvas proves it was the algorithm applied
        Assert.Equal(StubAlgorithm.SentinelWidth, tree.Width);
        Assert.Equal(StubAlgorithm.SentinelHeight, tree.Height);
    }

    /// <summary>
    ///     Proves an unresolvable algorithm identifier surfaces the registry's key-not-found error.
    /// </summary>
    [Fact]
    public void Layout_UnregisteredAlgorithm_Throws()
    {
        // Arrange: a graph declaring an algorithm the default registry does not contain
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, "nope");

        // Act / Assert
        Assert.Throws<KeyNotFoundException>(() => LayoutEngine.Layout(graph, new LayoutOptions()));
    }

    /// <summary>
    ///     Proves a null graph argument is rejected.
    /// </summary>
    [Fact]
    public void Layout_NullGraph_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => LayoutEngine.Layout(null!, new LayoutOptions()));
    }

    /// <summary>
    ///     Proves a null options argument is rejected.
    /// </summary>
    [Fact]
    public void Layout_NullOptions_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => LayoutEngine.Layout(new LayoutGraph(), null!));
    }

    /// <summary>
    ///     Proves a null registry argument is rejected by the custom-registry overload.
    /// </summary>
    [Fact]
    public void Layout_NullRegistry_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(
            () => LayoutEngine.Layout(new LayoutGraph(), new LayoutOptions(), null!));
    }

    /// <summary>Builds a small deterministic flat graph of three labelled leaves and two edges.</summary>
    private static LayoutGraph BuildFlatGraph()
    {
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 80, 40);
        var b = graph.AddNode("b", 80, 40);
        var c = graph.AddNode("c", 80, 40);
        a.Label = "A";
        b.Label = "B";
        c.Label = "C";
        graph.AddEdge("a-b", a, b);
        graph.AddEdge("b-c", b, c);
        return graph;
    }

    /// <summary>
    ///     Deep-compares two layout trees for exact (bit-level) equality of canvas size, node kinds, box
    ///     geometry and attributes, and every line waypoint and style.
    /// </summary>
    private static void AssertTreesIdentical(LayoutTree expected, LayoutTree actual)
    {
        AssertExact(expected.Width, actual.Width);
        AssertExact(expected.Height, actual.Height);
        Assert.Equal(expected.Nodes.Count, actual.Nodes.Count);

        for (var i = 0; i < expected.Nodes.Count; i++)
        {
            var expectedNode = expected.Nodes[i];
            var actualNode = actual.Nodes[i];
            Assert.Equal(expectedNode.GetType(), actualNode.GetType());

            switch (expectedNode)
            {
                case LayoutBox expectedBox:
                    var actualBox = (LayoutBox)actualNode;
                    AssertExact(expectedBox.X, actualBox.X);
                    AssertExact(expectedBox.Y, actualBox.Y);
                    AssertExact(expectedBox.Width, actualBox.Width);
                    AssertExact(expectedBox.Height, actualBox.Height);
                    Assert.Equal(expectedBox.Label, actualBox.Label);
                    Assert.Equal(expectedBox.Depth, actualBox.Depth);
                    Assert.Equal(expectedBox.Shape, actualBox.Shape);
                    Assert.Equal(expectedBox.Children.Count, actualBox.Children.Count);
                    break;
                case LayoutLine expectedLine:
                    var actualLine = (LayoutLine)actualNode;
                    Assert.Equal(expectedLine.Waypoints.Count, actualLine.Waypoints.Count);
                    for (var w = 0; w < expectedLine.Waypoints.Count; w++)
                    {
                        AssertExact(expectedLine.Waypoints[w].X, actualLine.Waypoints[w].X);
                        AssertExact(expectedLine.Waypoints[w].Y, actualLine.Waypoints[w].Y);
                    }

                    Assert.Equal(expectedLine.SourceEnd, actualLine.SourceEnd);
                    Assert.Equal(expectedLine.TargetEnd, actualLine.TargetEnd);
                    Assert.Equal(expectedLine.LineStyle, actualLine.LineStyle);
                    Assert.Equal(expectedLine.MidpointLabel, actualLine.MidpointLabel);
                    break;
                default:
                    Assert.Fail($"Unexpected node type {expectedNode.GetType()}.");
                    break;
            }
        }
    }

    /// <summary>Asserts that two doubles are identical at the bit level (no tolerance).</summary>
    private static void AssertExact(double expected, double actual) =>
        Assert.True(
            BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual),
            $"expected {expected:R} but got {actual:R}");

    /// <summary>
    ///     A minimal <see cref="ILayoutAlgorithm"/> stub returning a recognizable sentinel canvas so a
    ///     test can prove it — and not a bundled algorithm — was the one applied.
    /// </summary>
    private sealed class StubAlgorithm : ILayoutAlgorithm
    {
        public const double SentinelWidth = 4242.0;

        public const double SentinelHeight = 2424.0;

        public string Id => "stub";

        public LayoutTree Apply(LayoutGraph graph, LayoutOptions options) =>
            new(SentinelWidth, SentinelHeight, []);
    }
}
