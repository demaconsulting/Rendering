// <copyright file="AlgorithmNotationPassThroughTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Cross-algorithm tests that every notation member of the input graph reaches the placed layout tree
///     through every bundled layout algorithm: the port appearance, both connector end labels, the box
///     appearance and the diamond shape. Each scenario forces a different graph-cloning path so that a
///     member dropped by any clone (the auto algorithm's component split, the hierarchical algorithm's
///     sized view) fails here by name.
/// </summary>
public sealed class AlgorithmNotationPassThroughTests
{
    /// <summary>The graph shapes that each force a different copy path.</summary>
    public enum Scenario
    {
        /// <summary>One connected group: the zero-copy fast path of the auto and hierarchical algorithms.</summary>
        Flat,

        /// <summary>The flat graph plus a disconnected pair and an isolated node: the auto algorithm's split.</summary>
        FlatPlusIsolated,

        /// <summary>A container node beside the ported leaf pair: the hierarchical algorithm's sized view.</summary>
        Container,

        /// <summary>The container scenario plus a disconnected pair and an isolated node.</summary>
        ContainerPlusIsolated,

        /// <summary>A container with a boundary (delegation) port: the combined merge-region pass.</summary>
        BoundaryPort,

        /// <summary>The boundary-port scenario plus a disconnected pair and an isolated node.</summary>
        BoundaryPortPlusIsolated,
    }

    private static readonly PortAppearance PortStyle =
        new(PortDirection.InOut, PortGlyph.Outlined, LineStyle.Dashed, PortLabelPlacement.Outward);

    private static readonly LineEndLabel SourceLabel = new("owner", "1");
    private static readonly LineEndLabel TargetLabel = new("parts", "0..*");

    private static readonly BoxAppearance StyledBox = new(LineStyle.Dashed, NameStyle: TextStyle.Regular);

    private static readonly BoxAppearance DiamondBox = new(LineStyle.Dotted);

    /// <summary>
    ///     Gets the algorithm x scenario matrix. The containment algorithm lays boxes out and routes node-to-node
    ///     connectors only (a port endpoint is not supported there by design), so it is paired with the scenarios
    ///     that carry no container and its graph carries no ports; the layered algorithm does not lay out nested
    ///     children, so it is paired with the flat scenarios only.
    /// </summary>
    /// <returns>The matrix rows: algorithm id and scenario.</returns>
    public static IEnumerable<object[]> Matrix()
    {
        foreach (var scenario in Enum.GetValues<Scenario>())
        {
            yield return ["auto", scenario];
            yield return ["hierarchical", scenario];
        }

        foreach (var algorithm in new[] { "layered", "containment" })
        {
            yield return [algorithm, Scenario.Flat];
            yield return [algorithm, Scenario.FlatPlusIsolated];
        }
    }

    /// <summary>Every notation member of the input graph reaches the placed tree.</summary>
    /// <param name="algorithm">The layout algorithm id.</param>
    /// <param name="scenario">The graph shape.</param>
    [Theory]
    [MemberData(nameof(Matrix))]
    public void Algorithm_Notation_ReachesPlacedTree(string algorithm, Scenario scenario)
    {
        // Arrange
        var expected = Build(algorithm, scenario, styled: true, out var graph);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        var ports = Flatten(tree.Nodes).OfType<LayoutPort>().ToList();
        Assert.Equal(expected.Ports, ports.Count);
        Assert.All(ports, port => Assert.Equal(PortStyle, port.Appearance));

        var lines = Flatten(tree.Nodes).OfType<LayoutLine>().ToList();
        Assert.Equal(expected.Lines, lines.Count);
        Assert.All(lines, line =>
        {
            Assert.Equal(SourceLabel, line.SourceEndLabel);
            Assert.Equal(TargetLabel, line.TargetEndLabel);
        });

        var boxes = Flatten(tree.Nodes).OfType<LayoutBox>().ToList();
        var diamond = Assert.Single(boxes, box => box.Label == "D");
        Assert.Equal(BoxShape.Diamond, diamond.Shape);
        Assert.Equal(DiamondBox, diamond.Appearance);
        var styled = Assert.Single(boxes, box => box.Label == "S");
        Assert.Equal(StyledBox, styled.Appearance);
    }

    /// <summary>
    ///     Without any notation members the legacy output stays legacy: no port appearance, no end labels and
    ///     no box appearance appear out of thin air (the diamond shape is a shape, not an appearance, and stays).
    /// </summary>
    /// <param name="algorithm">The layout algorithm id.</param>
    /// <param name="scenario">The graph shape.</param>
    [Theory]
    [MemberData(nameof(Matrix))]
    public void Algorithm_WithoutNotation_StaysLegacy(string algorithm, Scenario scenario)
    {
        // Arrange
        var expected = Build(algorithm, scenario, styled: false, out var graph);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        var ports = Flatten(tree.Nodes).OfType<LayoutPort>().ToList();
        Assert.Equal(expected.Ports, ports.Count);
        Assert.All(ports, port => Assert.Null(port.Appearance));

        var lines = Flatten(tree.Nodes).OfType<LayoutLine>().ToList();
        Assert.Equal(expected.Lines, lines.Count);
        Assert.All(lines, line =>
        {
            Assert.Null(line.SourceEndLabel);
            Assert.Null(line.TargetEndLabel);
        });

        var boxes = Flatten(tree.Nodes).OfType<LayoutBox>().ToList();
        Assert.Equal(BoxShape.Diamond, Assert.Single(boxes, box => box.Label == "D").Shape);
        Assert.All(boxes, box => Assert.Null(box.Appearance));
    }

    private static IEnumerable<LayoutNode> Flatten(IEnumerable<LayoutNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            if (node is LayoutBox box)
            {
                foreach (var inner in Flatten(box.Children))
                {
                    yield return inner;
                }
            }
        }
    }

    /// <summary>
    ///     Builds the graph for a cell of the matrix and returns the number of ports and lines expected in the
    ///     placed tree.
    /// </summary>
    private static (int Ports, int Lines) Build(string algorithm, Scenario scenario, bool styled, out LayoutGraph graph)
    {
        graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, algorithm);

        var withPorts = algorithm != "containment";
        var ports = 0;
        var lines = 0;

        void Connect(ILayoutConnectable source, ILayoutConnectable target, LayoutGraph scope)
        {
            var edge = scope.AddEdge($"e{lines}", source, target);
            edge.TargetEnd = EndMarkerStyle.OpenChevron;
            if (styled)
            {
                edge.SourceEndLabel = SourceLabel;
                edge.TargetEndLabel = TargetLabel;
            }

            lines++;
        }

        LayoutGraphNode Box(string id, string label, LayoutGraph scope)
        {
            var node = scope.AddNode(id, 100, 60);
            node.Label = label;
            return node;
        }

        // The diamond and the styled box; both sit in the root scope in every scenario.
        var diamond = Box("d", "D", graph);
        diamond.Shape = BoxShape.Diamond;
        diamond.Width = 60;
        diamond.Height = 60;
        var box = Box("s", "S", graph);
        if (styled)
        {
            diamond.Appearance = DiamondBox;
            box.Appearance = StyledBox;
        }

        var isBoundary = scenario is Scenario.BoundaryPort or Scenario.BoundaryPortPlusIsolated;
        var isContainer = scenario is Scenario.Container or Scenario.ContainerPlusIsolated;
        LayoutGraphNode last;
        LayoutGraphNode? containerNode = null;
        if (isBoundary)
        {
            // Container B owns boundary port P: A -> P outside, P -> C inside.
            var a = Box("a", "A", graph);
            var b = Box("b", "B", graph);
            var port = b.Ports.AddPort("p1");
            port.ExternalLabel = "PWR_OUT";
            port.InternalLabel = "PWR_IN";
            port.Appearance = styled ? PortStyle : null;
            ports++;
            var c = Box("c", "C", b.Children);
            Connect(a, port, graph);
            Connect(port, c, b.Children);
            last = a;
        }
        else
        {
            var a = Box("a", "A", graph);
            var b = Box("b", "B", graph);
            if (withPorts)
            {
                var source = a.Ports.AddPort("out1");
                source.ExternalLabel = "out1 : Real";
                source.Appearance = styled ? PortStyle : null;
                var target = b.Ports.AddPort("in1");
                target.ExternalLabel = "in1 : Real";
                target.Appearance = styled ? PortStyle : null;
                ports += 2;
                Connect(source, target, graph);
            }
            else
            {
                Connect(a, b, graph);
            }

            last = b;
            if (isContainer)
            {
                containerNode = Box("g", "G", graph);
                var x = Box("x", "X", containerNode.Children);
                var y = Box("y", "Y", containerNode.Children);
                Connect(x, y, containerNode.Children);
            }
        }

        Connect(last, box, graph);
        Connect(box, diamond, graph);
        if (containerNode is not null)
        {
            // Keep the container in the same connected group as the ported pair so the auto algorithm routes
            // the whole group to the hierarchical algorithm (a sized view with a container and a ported edge).
            Connect(diamond, containerNode, graph);
        }

        if (scenario is Scenario.FlatPlusIsolated or Scenario.ContainerPlusIsolated or Scenario.BoundaryPortPlusIsolated)
        {
            var p = Box("p", "P", graph);
            var q = Box("q", "Q", graph);
            Connect(p, q, graph);
            Box("i", "I", graph);
        }

        return (ports, lines);
    }
}
