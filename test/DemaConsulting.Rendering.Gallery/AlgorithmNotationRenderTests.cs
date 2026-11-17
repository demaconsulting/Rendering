// <copyright file="AlgorithmNotationRenderTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Text;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout;
using DemaConsulting.Rendering.Svg;

namespace DemaConsulting.Rendering.Gallery;

/// <summary>
///     Proves the port appearance and the connector end labels of a graph reach the rendered SVG through the
///     graph-cloning layout paths (the auto algorithm's component split and the hierarchical algorithm's
///     sized view), not just the layout tree. This lives in the gallery project because it is the one test
///     project that references both the layout algorithms and the renderers.
/// </summary>
public sealed class AlgorithmNotationRenderTests
{
    private static readonly PortAppearance DashedPort =
        new(PortDirection.InOut, PortGlyph.Outlined, LineStyle.Dashed, PortLabelPlacement.Outward);

    /// <summary>Gets the cloning scenarios: the graph shapes that force a copy of the ported edge's graph.</summary>
    /// <returns>The matrix rows: algorithm id, whether to add an isolated node, whether to add a container.</returns>
    public static IEnumerable<object[]> CloneScenarios()
    {
        // auto + isolated node: the component split copies nodes, ports and edges.
        yield return ["auto", true, false];

        // hierarchical + container: the sized view copies nodes, ports and edges.
        yield return ["hierarchical", false, true];

        // auto + container + isolated node: the split, then the sized view of the hierarchical group.
        yield return ["auto", true, true];
    }

    /// <summary>
    ///     The dashed outline of the port glyph and both end labels appear in the rendered SVG, and a graph
    ///     whose ports carry no appearance and whose edge carries no end labels renders none of them, so the
    ///     markers can only come from the notation members that crossed the clone.
    /// </summary>
    /// <param name="algorithm">The layout algorithm id.</param>
    /// <param name="isolated">Whether an isolated node is added (forces the auto split).</param>
    /// <param name="container">Whether a container node is added (forces the hierarchical sized view).</param>
    [Theory]
    [MemberData(nameof(CloneScenarios))]
    public void ClonePath_PortAppearanceAndEndLabels_ReachRenderedSvg(string algorithm, bool isolated, bool container)
    {
        // Act
        var styled = RenderSvg(Build(algorithm, isolated, container, styled: true));
        var legacy = RenderSvg(Build(algorithm, isolated, container, styled: false));

        // Assert
        Assert.Contains("stroke-dasharray", styled);
        Assert.DoesNotContain("stroke-dasharray", legacy);
        Assert.Contains(">owner</text>", styled);
        Assert.Contains(">0..*</text>", styled);
        Assert.DoesNotContain(">owner</text>", legacy);
        Assert.DoesNotContain(">0..*</text>", legacy);
    }

    private static LayoutGraph Build(string algorithm, bool isolated, bool container, bool styled)
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Algorithm, algorithm);
        var a = graph.AddNode("a", 120, 60);
        a.Label = "A";
        var source = a.Ports.AddPort("out1");
        source.ExternalLabel = "out1";
        source.Appearance = styled ? DashedPort : null;
        var b = graph.AddNode("b", 120, 60);
        b.Label = "B";
        var target = b.Ports.AddPort("in1");
        target.ExternalLabel = "in1";
        target.Appearance = styled ? DashedPort : null;
        var edge = graph.AddEdge("e", source, target);
        edge.TargetEnd = EndMarkerStyle.OpenChevron;
        if (styled)
        {
            edge.SourceEndLabel = new LineEndLabel("owner", "1");
            edge.TargetEndLabel = new LineEndLabel("parts", "0..*");
        }

        if (container)
        {
            var group = graph.AddNode("g", 120, 60);
            group.Label = "G";
            group.Children.AddNode("x", 80, 40).Label = "X";
            graph.AddEdge("b-g", b, group);
        }

        if (isolated)
        {
            graph.AddNode("i", 120, 60).Label = "I";
        }

        return graph;
    }

    private static string RenderSvg(LayoutGraph graph)
    {
        var tree = LayoutEngine.Layout(graph);
        using var output = new MemoryStream();
        new SvgRenderer().Render(tree, new RenderOptions(Themes.Light), output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
