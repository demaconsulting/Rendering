package RenderingLayout {
    private import SoftwareItemMetadata::*;

    part def HierarchicalLayoutAlgorithm {
        doc /* The recursive hierarchical layout engine. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Layout/HierarchicalLayoutAlgorithm.cs", "src/DemaConsulting.Rendering.Layout/HierarchicalLayoutAlgorithm.CrossOrdering.cs", "src/DemaConsulting.Rendering.Layout/HierarchicalLayoutAlgorithm.RoutePassCache.cs");
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/HierarchicalLayoutAlgorithmTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/HierarchicalCrossOrderingTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/CrossHierarchyOrderingTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/OrderRankPlumbingTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/LayerSpacingTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/LayoutCompactnessTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/CanvasMarginTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/LayoutQualityBaseline.cs", "test/DemaConsulting.Rendering.Layout.Tests/LayoutQualityMetrics.cs", "test/DemaConsulting.Rendering.Layout.Tests/LayoutQualityScenes.cs", "test/DemaConsulting.Rendering.Layout.Tests/NotationPrimitivePassThroughTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/FaceAlignmentTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/DiagramQuality.cs", "test/DemaConsulting.Rendering.Layout.Tests/DiamondShapeAnchorTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/EndLabelPassThroughTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/PortAppearancePassThroughTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/DirectionLayoutGeometryTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/EdgeKeywordLabelLayoutTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/DirectionScenes.cs", "test/DemaConsulting.Rendering.Layout.Tests/LayoutGeometryAssert.cs", "test/DemaConsulting.Rendering.Layout.Tests/LargeScopeLayoutTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/RoutingScenes.cs");
            designFiles = ("docs/design/rendering-layout/hierarchical-layout-algorithm.md");
            verificationFiles = ("docs/verification/rendering-layout/hierarchical-layout-algorithm.md");
            requirementFiles = ("docs/reqstream/rendering-layout/hierarchical-layout-algorithm.yaml");
        }
    }
}
