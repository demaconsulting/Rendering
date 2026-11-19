package RenderingLayout {
    private import SoftwareItemMetadata::*;

    part def LayeredLayoutAlgorithm {
        doc /* The public layered layout algorithm. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Layout/LayeredLayoutAlgorithm.cs");
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/LayeredLayoutAlgorithmTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/NotationPrimitivePassThroughTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/BackEdgeEntryApproachTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/FaceAlignmentTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/DiagramQuality.cs", "test/DemaConsulting.Rendering.Layout.Tests/DiamondShapeAnchorTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/EndLabelPassThroughTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/EndLabelLayoutSpaceTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/PortAppearancePassThroughTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/OutwardPortLabelLayoutTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/DirectionLayoutGeometryTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/EdgeKeywordLabelLayoutTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/DirectionScenes.cs", "test/DemaConsulting.Rendering.Layout.Tests/LayoutGeometryAssert.cs", 
"test/DemaConsulting.Rendering.Layout.Tests/LayerSpacingTests.cs", 
"test/DemaConsulting.Rendering.Layout.Tests/OrderRankPlumbingTests.cs", 
"test/DemaConsulting.Rendering.Layout.Tests/CanvasMarginTests.cs", 
"test/DemaConsulting.Rendering.Layout.Tests/LayoutQualityBaseline.cs", 
"test/DemaConsulting.Rendering.Layout.Tests/LayoutQualityMetrics.cs", 
"test/DemaConsulting.Rendering.Layout.Tests/LayoutQualityScenes.cs");
            designFiles = ("docs/design/rendering-layout/layered-layout-algorithm.md");
            verificationFiles = ("docs/verification/rendering-layout/layered-layout-algorithm.md");
            requirementFiles = ("docs/reqstream/rendering-layout/layered-layout-algorithm.yaml");
        }
    }
}
