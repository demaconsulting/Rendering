// <copyright file="BoundaryPortResolverTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Tests.Engine.Layered;

/// <summary>
///     Tests for <see cref="BoundaryPortResolver"/>: the combined-pass ordering primitive
///     (<see cref="BoundaryPortResolver.OrderCrossings"/>) and the per-scope
///     <see cref="BoundaryPortResolver.Resolve"/> reconciliation that enriches a leaf anchor with both
///     labels and wires internal delegation connectors to one shared anchor.
/// </summary>
public sealed class BoundaryPortResolverTests
{
    /// <summary>
    ///     Ordering several hierarchy crossings that all delegate to interior targets returns a
    ///     deterministic permutation of the crossing indices — proof the crossings participate in the
    ///     combined layered pass rather than colliding at one point.
    /// </summary>
    [Fact]
    public void OrderCrossings_MultipleCrossings_ReturnsPermutationOfIndices()
    {
        // Arrange: three zero-size hierarchy crossings each delegating to a sized interior target.
        var crossings = new List<HierarchyCrossing>
        {
            new(new LayoutGraphPort("x0"), HierarchyCrossingFace.Internal),
            new(new LayoutGraphPort("x1"), HierarchyCrossingFace.Internal),
            new(new LayoutGraphPort("x2"), HierarchyCrossingFace.Internal),
        };
        var targets = new List<(double Width, double Height)> { (80, 40), (80, 40), (80, 40) };

        // Act
        var order = BoundaryPortResolver.OrderCrossings(crossings, targets, LayoutDirection.Right);

        // Assert: a permutation of {0,1,2}.
        Assert.Equal(3, order.Count);
        Assert.Equal([0, 1, 2], order.OrderBy(i => i).ToList());
    }

    /// <summary>
    ///     Ordering crossings with no interior targets falls back to input order (nothing to order
    ///     against), so an empty region degrades gracefully.
    /// </summary>
    [Fact]
    public void OrderCrossings_NoTargets_ReturnsInputOrder()
    {
        // Arrange: two crossings, no targets.
        var crossings = new List<HierarchyCrossing>
        {
            new(new LayoutGraphPort("x0"), HierarchyCrossingFace.Internal),
            new(new LayoutGraphPort("x1"), HierarchyCrossingFace.Internal),
        };

        // Act
        var order = BoundaryPortResolver.OrderCrossings(crossings, [], LayoutDirection.Right);

        // Assert
        Assert.Equal([0, 1], order);
    }

    /// <summary>
    ///     Ordering an empty crossing set returns an empty result.
    /// </summary>
    [Fact]
    public void OrderCrossings_Empty_ReturnsEmpty()
    {
        // Act
        var order = BoundaryPortResolver.OrderCrossings([], [], LayoutDirection.Right);

        // Assert
        Assert.Empty(order);
    }

    /// <summary>
    ///     Resolving a boundary port whose external edge the leaf pass already anchored enriches that
    ///     single leaf anchor with the internal label (keeping the external label and the anchor
    ///     position) and adds one internal delegation connector starting at the anchor and reaching
    ///     into the interior child, so the external and internal approaches share one physical point.
    /// </summary>
    [Fact]
    public void Resolve_LeafAnchoredPort_EnrichesAnchorAndAddsInternalConnector()
    {
        // Arrange: scope with sibling A and container B (port P delegates to child C).
        var scope = new LayoutGraph();
        var a = scope.AddNode("A", 80, 40);
        var b = scope.AddNode("B", 120, 80);
        var port = b.Ports.AddPort("p");
        port.ExternalLabel = "EXT";
        port.InternalLabel = "INT";
        var c = b.Children.AddNode("C", 80, 40);
        scope.AddEdge("a-p", a, port);
        b.Children.AddEdge("p-c", port, c);

        var boundaryPorts = HierarchyMergeRegionBuilder.Collect(scope);
        Assert.Single(boundaryPorts);

        // Composed geometry: A at the origin; B to its right; C inside B; the leaf's port anchor on
        // B's left face at (200, 40); the external line ending on that anchor.
        var aBox = Box(0, 0, 80, 40, "A", []);
        var cBox = Box(220, 20, 80, 40, "C", []);
        var bBox = Box(200, 0, 120, 80, "B", [cBox]);
        var composed = new[] { aBox, bBox };
        var indexOf = new Dictionary<LayoutGraphNode, int> { [a] = 0, [b] = 1 };

        var anchor = new LayoutPort(200, 40, PortSide.Left, "EXT");
        var placedPorts = new List<LayoutPort> { anchor };
        var externalLine = new LayoutLine(
            [new Point2D(80, 20), new Point2D(200, 40)],
            EndMarkerStyle.None,
            EndMarkerStyle.FilledArrow,
            LineStyle.Solid,
            null);
        var placedLines = new List<LayoutLine> { externalLine };

        // Act
        var resolution = BoundaryPortResolver.Resolve(
            scope,
            LayoutDirection.Right,
            boundaryPorts,
            composed,
            indexOf,
            placedPorts,
            placedLines);

        // Assert: one port carrying BOTH labels at the original anchor position.
        var emitted = Assert.Single(resolution.Ports);
        Assert.Equal("EXT", emitted.ExternalLabel);
        Assert.Equal("INT", emitted.InternalLabel);
        Assert.Equal(200, emitted.CentreX, 3);
        Assert.Equal(40, emitted.CentreY, 3);
        Assert.Equal(PortSide.Left, emitted.Side);

        // The external line still ends at the anchor, and a new internal connector starts at it.
        var anchorPoint = new Point2D(200, 40);
        Assert.Contains(
            resolution.Lines,
            line => line.Waypoints.Count > 0 && Same(line.Waypoints[^1], anchorPoint));
        Assert.Contains(
            resolution.Lines,
            line => line.Waypoints.Count > 0 && Same(line.Waypoints[0], anchorPoint) &&
                line.Waypoints.Any(wp => wp.X >= cBox.X - 1 && wp.X <= cBox.X + cBox.Width + 1));
    }

    /// <summary>
    ///     Builds a <see cref="LayoutBox"/> with the minimum shape metadata for a resolver test.
    /// </summary>
    /// <param name="x">The box left coordinate.</param>
    /// <param name="y">The box top coordinate.</param>
    /// <param name="width">The box width.</param>
    /// <param name="height">The box height.</param>
    /// <param name="label">The box label.</param>
    /// <param name="children">The box children.</param>
    /// <returns>The constructed box.</returns>
    private static LayoutBox Box(
        double x,
        double y,
        double width,
        double height,
        string label,
        IReadOnlyList<LayoutNode> children) =>
        new(x, y, width, height, label, 0, BoxShape.Rectangle, [], children);

    /// <summary>Returns whether two points coincide within a small tolerance.</summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns><see langword="true"/> when the points coincide.</returns>
    private static bool Same(Point2D a, Point2D b) =>
        Math.Abs(a.X - b.X) < 1e-6 && Math.Abs(a.Y - b.Y) < 1e-6;
}
