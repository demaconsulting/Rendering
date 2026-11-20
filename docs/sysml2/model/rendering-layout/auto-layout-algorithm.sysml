package RenderingLayout_AutoLayoutAlgorithm {
    private import SoftwareItemMetadata::*;

    part def AutoLayoutAlgorithm {
        doc /* The auto-routing meta-algorithm. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Layout/AutoLayoutAlgorithm.cs");
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/AutoLayoutAlgorithmTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/NotationPrimitivePassThroughTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/EndLabelPassThroughTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/PortAppearancePassThroughTests.cs", 
"test/DemaConsulting.Rendering.Layout.Tests/OrderRankPlumbingTests.cs", 
"test/DemaConsulting.Rendering.Layout.Tests/LayoutCompactnessTests.cs", 
"test/DemaConsulting.Rendering.Layout.Tests/CanvasMarginTests.cs", 
"test/DemaConsulting.Rendering.Layout.Tests/LayoutQualityBaseline.cs", 
"test/DemaConsulting.Rendering.Layout.Tests/LayoutQualityMetrics.cs", 
"test/DemaConsulting.Rendering.Layout.Tests/LayoutQualityScenes.cs");
            designFiles = ("docs/design/rendering-layout/auto-layout-algorithm.md");
            verificationFiles = ("docs/verification/rendering-layout/auto-layout-algorithm.md");
            requirementFiles = ("docs/reqstream/rendering-layout/auto-layout-algorithm.yaml");
        }
    }
}
