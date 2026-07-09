// <copyright file="MergeRegionDecomposerTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Layout.Engine.Layered;

namespace DemaConsulting.Rendering.Layout.Tests.Engine.Layered;

/// <summary>
///     Full-waypoint orthogonality tests for the boundary-port decomposition
///     (<see cref="MergeRegionDecomposer"/>) exercised end-to-end through the public
///     <see cref="HierarchicalLayoutAlgorithm.Apply"/> surface. These are the genuine regression tests
///     for the shipped fan-in defect: every connector that terminates at a shared boundary anchor must
///     be strictly orthogonal along its <em>whole</em> polyline — every consecutive waypoint pair sharing
///     exactly one coordinate — so no edge is ever patched onto the anchor with a raw diagonal segment.
///     The old reconciliation code produced exactly such a diagonal for every fan-in approach past the
///     first; these tests fail against that code and pass against the corridor-router-derived decomposition.
/// </summary>
public sealed class MergeRegionDecomposerTests
{
    private const double OrthogonalTolerance = 1e-4;

    /// <summary>
    ///     Reproduces the downward fan-in showcase (<c>Monitor</c> and <c>Operator</c> both approach one
    ///     top-face boundary port that delegates inward to <c>Controller</c>'s child). Every converging
    ///     external approach — and the internal delegation — that reaches the shared anchor must be
    ///     strictly orthogonal end to end, with no diagonal segment anywhere. The second converging
    ///     approach is precisely where the old code emitted a diagonal.
    /// </summary>
    [Fact]
    public void MergeRegionDecomposer_FanIn_EveryConvergingEdge_IsStrictlyOrthogonalWithNoDirectDiagonal()
    {
        // Arrange: the vertical (downward) external fan-in showcase.
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.Direction, LayoutFlowDirection.Down);

        var monitor = graph.AddNode("monitor", 120, 50);
        monitor.Label = "Monitor";
        var operatorConsole = graph.AddNode("operator", 120, 50);
        operatorConsole.Label = "Operator";

        var controller = graph.AddNode("controller", 10, 10);
        controller.Label = "Controller";
        var command = controller.Ports.AddPort("command");
        command.ExternalLabel = "command";
        command.InternalLabel = "dispatch";

        var driver = controller.Children.AddNode("driver", 120, 50);
        driver.Label = "Driver";

        graph.AddEdge("monitor-command", monitor, command);
        graph.AddEdge("operator-command", operatorConsole, command);
        controller.Children.AddEdge("command-driver", command, driver);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: exactly one shared anchor, and every edge reaching it is orthogonal along its whole path.
        AssertEveryEdgeAtSharedAnchorIsOrthogonal(tree, expectedReachingAnchor: 3);
    }

    /// <summary>
    ///     The fan-out mirror of the fan-in showcase: two siblings approach one left-face boundary port
    ///     that also delegates inward to two children (a full bidirectional fan). Every edge that meets
    ///     the shared anchor — the two external approaches and the two internal delegations — must be
    ///     strictly orthogonal end to end. The second converging approach is exactly where the old code
    ///     emitted a diagonal, so this genuinely captures the defect on the delegated (fan-out) side too.
    /// </summary>
    [Fact]
    public void MergeRegionDecomposer_FanOut_EveryDelegatedEdge_IsStrictlyOrthogonalWithNoDirectDiagonal()
    {
        // Arrange: the horizontal (rightward) full-fan showcase (two approaches, two delegations).
        var graph = new LayoutGraph();

        var sensor = graph.AddNode("sensor", 120, 50);
        sensor.Label = "Sensor";
        var gauge = graph.AddNode("gauge", 120, 50);
        gauge.Label = "Gauge";

        var controller = graph.AddNode("controller", 10, 10);
        controller.Label = "Controller";
        var command = controller.Ports.AddPort("command");
        command.ExternalLabel = "command";
        command.InternalLabel = "dispatch";

        var driver = controller.Children.AddNode("driver", 120, 50);
        driver.Label = "Driver";
        var logger = controller.Children.AddNode("logger", 120, 50);
        logger.Label = "Logger";

        graph.AddEdge("sensor-command", sensor, command);
        graph.AddEdge("gauge-command", gauge, command);
        controller.Children.AddEdge("command-driver", command, driver);
        controller.Children.AddEdge("command-logger", command, logger);

        // Act
        var tree = new HierarchicalLayoutAlgorithm().Apply(graph, LayoutOptions.ForAlgorithm("layered"));

        // Assert: exactly one shared anchor, and every edge reaching it is orthogonal along its whole path.
        AssertEveryEdgeAtSharedAnchorIsOrthogonal(tree, expectedReachingAnchor: 4);
    }

    /// <summary>
    ///     Asserts the layout emits exactly one boundary anchor, that the expected number of connectors
    ///     touch it, and that every such connector is strictly orthogonal along its entire polyline.
    /// </summary>
    /// <param name="tree">The laid-out tree.</param>
    /// <param name="expectedReachingAnchor">The number of connectors expected to touch the shared anchor.</param>
    private static void AssertEveryEdgeAtSharedAnchorIsOrthogonal(LayoutTree tree, int expectedReachingAnchor)
    {
        var anchorPort = Assert.Single(tree.Nodes.OfType<LayoutPort>());
        var anchor = new Point2D(anchorPort.CentreX, anchorPort.CentreY);

        var reaching = tree.Nodes.OfType<LayoutLine>()
            .Where(line => line.Waypoints.Count > 0 &&
                (SamePoint(line.Waypoints[0], anchor) || SamePoint(line.Waypoints[^1], anchor)))
            .ToList();

        Assert.Equal(expectedReachingAnchor, reaching.Count);

        foreach (var line in reaching)
        {
            AssertPolylineIsStrictlyOrthogonal(line.Waypoints);
        }
    }

    /// <summary>
    ///     Asserts every consecutive, non-degenerate waypoint pair of the polyline shares exactly one
    ///     coordinate — i.e. it is axis-aligned (dx≈0 XOR dy≈0) — so no segment runs diagonally.
    /// </summary>
    /// <param name="waypoints">The connector polyline.</param>
    private static void AssertPolylineIsStrictlyOrthogonal(IReadOnlyList<Point2D> waypoints)
    {
        for (var i = 1; i < waypoints.Count; i++)
        {
            var dx = Math.Abs(waypoints[i].X - waypoints[i - 1].X);
            var dy = Math.Abs(waypoints[i].Y - waypoints[i - 1].Y);

            // Skip degenerate (coincident) points; they are not diagonal segments.
            if (dx < OrthogonalTolerance && dy < OrthogonalTolerance)
            {
                continue;
            }

            var horizontal = dy < OrthogonalTolerance;
            var vertical = dx < OrthogonalTolerance;
            Assert.True(
                horizontal ^ vertical,
                $"Segment [{waypoints[i - 1].X},{waypoints[i - 1].Y}]->[{waypoints[i].X},{waypoints[i].Y}] " +
                "is diagonal (dx and dy both non-zero) — a boundary connector was patched onto the anchor.");
        }
    }

    /// <summary>Returns true when two points coincide within a small tolerance.</summary>
    private static bool SamePoint(Point2D a, Point2D b)
    {
        const double tolerance = 1e-6;
        return Math.Abs(a.X - b.X) < tolerance && Math.Abs(a.Y - b.Y) < tolerance;
    }
}
