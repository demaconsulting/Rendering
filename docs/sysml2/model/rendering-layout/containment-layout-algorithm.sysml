package RenderingLayout_ContainmentLayoutAlgorithm {
    private import SoftwareItemMetadata::*;

    part def ContainmentLayoutAlgorithm {
        doc /* The public containment layout algorithm. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Layout/ContainmentLayoutAlgorithm.cs");
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/ContainmentLayoutAlgorithmTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/NotationPrimitivePassThroughTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/EndLabelPassThroughTests.cs", 
"test/DemaConsulting.Rendering.Layout.Tests/OrderRankPlumbingTests.cs", 
"test/DemaConsulting.Rendering.Layout.Tests/CanvasMarginTests.cs");
            designFiles = ("docs/design/rendering-layout/containment-layout-algorithm.md");
            verificationFiles = ("docs/verification/rendering-layout/containment-layout-algorithm.md");
            requirementFiles = ("docs/reqstream/rendering-layout/containment-layout-algorithm.yaml");
        }
    }
}
