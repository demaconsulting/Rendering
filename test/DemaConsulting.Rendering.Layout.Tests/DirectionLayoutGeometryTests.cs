// <copyright file="DirectionLayoutGeometryTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Regression tests for issue #59: with <see cref="LayoutFlowDirection.Up"/> (and
///     <see cref="LayoutFlowDirection.Down"/>) unconnected nodes overlapped each other and cross-hierarchy
///     connectors between root-level folders were routed outside the canvas. Every scene is checked in
///     all four flow directions: no sibling boxes overlap, and every box, connector waypoint, and label
///     lies inside the canvas.
/// </summary>
public sealed class DirectionLayoutGeometryTests
{
    /// <summary>Gets every flow direction as theory data.</summary>
    public static TheoryData<LayoutFlowDirection> Directions =>
        new(DirectionScenes.AllDirections);

    /// <summary>
    ///     Isolated nodes in a flat <c>layered</c> graph are packed with their true on-screen extents, so
    ///     they never overlap a connected node or each other.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [MemberData(nameof(Directions))]
    public void LayeredLayout_IsolatedNodesBesideConnectedTree_DoNotOverlap(LayoutFlowDirection direction)
    {
        // Arrange
        var graph = DirectionScenes.FlatIsolated(direction);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        Assert.Equal(8, LayoutGeometryAssert.AllBoxes(tree).Count);
        LayoutGeometryAssert.AssertClean(tree);
    }

    /// <summary>
    ///     Isolated nodes inside a package folder and at the root of a <c>hierarchical</c> graph never
    ///     overlap in any direction.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [MemberData(nameof(Directions))]
    public void HierarchicalLayout_IsolatedNodesInsideAndOutsideFolder_DoNotOverlap(LayoutFlowDirection direction)
    {
        // Arrange
        var graph = DirectionScenes.ContainerIsolated(direction);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        LayoutGeometryAssert.AssertClean(tree);
    }

    /// <summary>
    ///     The SysML General View shape (folders, cross-hierarchy edges, keyword labels, isolated
    ///     definitions) is overlap-free and entirely inside its canvas in every direction.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [MemberData(nameof(Directions))]
    public void HierarchicalLayout_GeneralView_IsOverlapFreeAndInsideCanvas(LayoutFlowDirection direction)
    {
        // Arrange
        var graph = DirectionScenes.GeneralView(direction);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        LayoutGeometryAssert.AssertClean(tree);
    }

    /// <summary>Connectors between root-level folders stay inside the canvas in every direction.</summary>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [MemberData(nameof(Directions))]
    public void HierarchicalLayout_RootFolderConnectors_StayInsideCanvas(LayoutFlowDirection direction)
    {
        // Arrange
        var graph = DirectionScenes.RootFolders(direction);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        LayoutGeometryAssert.AssertClean(tree);
    }

    /// <summary>
    ///     With <see cref="LayoutFlowDirection.Up"/> the general (target of a specialization) is placed
    ///     above its specific definitions, and the isolated nodes are still packed without overlap.
    /// </summary>
    [Fact]
    public void LayeredLayout_UpDirection_PlacesGeneralAboveSpecificsAndKeepsIsolatedNodesApart()
    {
        // Arrange
        var graph = DirectionScenes.FlatIsolated(LayoutFlowDirection.Up);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        var boxes = LayoutGeometryAssert.AllBoxes(tree);
        var vehicle = boxes.Single(b => b.Label == "Vehicle");
        foreach (var label in new[] { "Engine", "Wheel" })
        {
            Assert.True(vehicle.Y + vehicle.Height <= boxes.Single(b => b.Label == label).Y, $"{label} must be below Vehicle");
        }

        LayoutGeometryAssert.AssertClean(tree);
    }
}
