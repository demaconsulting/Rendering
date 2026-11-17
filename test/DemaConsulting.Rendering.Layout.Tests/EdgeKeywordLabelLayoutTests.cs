// <copyright file="EdgeKeywordLabelLayoutTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Regression tests for issue #61: edge keyword labels such as <c>«import»*</c> were clipped at the
///     canvas edge, and parallel keyword-labeled edges into one node crowded their arrowheads. Every
///     placed label must lie fully inside the canvas and clear of boxes and other labels, and parallel
///     keyword-labeled arrowheads into one node keep a minimum separation.
/// </summary>
public sealed class EdgeKeywordLabelLayoutTests
{
    /// <summary>Minimum distance, in logical pixels, between two arrowhead anchors on one node.</summary>
    private const double MinAnchorSeparation = 16.0;

    /// <summary>Gets every flow direction as theory data.</summary>
    public static TheoryData<LayoutFlowDirection> Directions =>
        new(DirectionScenes.AllDirections);

    /// <summary>
    ///     The <c>«import»*</c> labels of a General View connector between root folders are fully inside
    ///     the canvas, clear of boxes, and clear of each other in every direction.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [MemberData(nameof(Directions))]
    public void HierarchicalLayout_ImportLabels_AreInsideCanvasAndClear(LayoutFlowDirection direction)
    {
        // Arrange
        var graph = DirectionScenes.GeneralView(direction);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        var labels = LayoutGeometryAssert.LabelRects(tree);
        Assert.Contains(labels, l => l.Text == "«import»*");
        LayoutGeometryAssert.AssertWithinCanvas(tree);
        LayoutGeometryAssert.AssertLabelsClear(tree);
    }

    /// <summary>
    ///     Several <c>«import»*</c> connectors between root folders keep every label inside the canvas
    ///     and distinct from each other in every direction.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [MemberData(nameof(Directions))]
    public void HierarchicalLayout_ManyImportLabelsBetweenRootFolders_AreInsideCanvasAndClear(LayoutFlowDirection direction)
    {
        // Arrange
        var graph = DirectionScenes.RootFolders(direction);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        Assert.Equal(5, LayoutGeometryAssert.LabelRects(tree).Count(l => l.Text.StartsWith("«import»", StringComparison.Ordinal)));
        LayoutGeometryAssert.AssertWithinCanvas(tree);
        LayoutGeometryAssert.AssertLabelsClear(tree);
    }

    /// <summary>
    ///     The keyword-labeled requirements-and-design view keeps every <c>«import»*</c>, <c>«satisfy»</c>,
    ///     and <c>«verify»</c> label fully inside the canvas and clear of boxes and other labels, and keeps
    ///     the arrowheads converging on one node at least <see cref="MinAnchorSeparation"/> apart, in every
    ///     direction.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [MemberData(nameof(Directions))]
    public void HierarchicalLayout_KeywordLabeledView_LabelsInsideCanvasAndAnchorsApart(LayoutFlowDirection direction)
    {
        // Arrange
        var graph = DirectionScenes.KeywordLabels(direction);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert
        var labels = LayoutGeometryAssert.LabelRects(tree);
        Assert.Equal(6, labels.Count);
        LayoutGeometryAssert.AssertClean(tree);

        LayoutGeometryAssert.AssertArrowheadsApart(tree, MinAnchorSeparation);
    }

    /// <summary>
    ///     Four parallel keyword-labeled edges into one node keep their arrowhead anchors at least
    ///     <see cref="MinAnchorSeparation"/> apart, and their labels are inside the canvas, distinct, and
    ///     clear of every box.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    [Theory]
    [MemberData(nameof(Directions))]
    public void LayeredLayout_ParallelKeywordLabeledEdgesIntoOneNode_KeepAnchorsApartAndLabelsClear(LayoutFlowDirection direction)
    {
        // Arrange
        var graph = DirectionScenes.SatisfyVerify(direction);

        // Act
        var tree = LayoutEngine.Layout(graph);

        // Assert: every arrowhead tip is well separated from the others.
        Assert.Equal(4, LayoutGeometryAssert.AllLines(tree).Count);
        LayoutGeometryAssert.AssertArrowheadsApart(tree, MinAnchorSeparation);

        // Assert: the four labels are all present, distinct, and clear.
        Assert.Equal(4, LayoutGeometryAssert.LabelRects(tree).Count);
        LayoutGeometryAssert.AssertClean(tree);
    }
}
