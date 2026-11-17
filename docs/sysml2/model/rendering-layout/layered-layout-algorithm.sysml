package RenderingLayout {
    private import SoftwareItemMetadata::*;

    part def LayeredLayoutAlgorithm {
        doc /* The public layered layout algorithm. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Layout/LayeredLayoutAlgorithm.cs");
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/LayeredLayoutAlgorithmTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/NotationPrimitivePassThroughTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/BackEdgeEntryApproachTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/FaceAlignmentTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/DiagramQuality.cs", "test/DemaConsulting.Rendering.Layout.Tests/DiamondShapeAnchorTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/EndLabelPassThroughTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/EndLabelLayoutSpaceTests.cs");
            designFiles = ("docs/design/rendering-layout/layered-layout-algorithm.md");
            verificationFiles = ("docs/verification/rendering-layout/layered-layout-algorithm.md");
            requirementFiles = ("docs/reqstream/rendering-layout/layered-layout-algorithm.yaml");
        }
    }
}
