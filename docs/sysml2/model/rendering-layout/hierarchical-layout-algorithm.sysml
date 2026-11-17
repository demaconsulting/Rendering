package RenderingLayout {
    private import SoftwareItemMetadata::*;

    part def HierarchicalLayoutAlgorithm {
        doc /* The recursive hierarchical layout engine. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Layout/HierarchicalLayoutAlgorithm.cs");
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/HierarchicalLayoutAlgorithmTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/NotationPrimitivePassThroughTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/FaceAlignmentTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/DiagramQuality.cs", "test/DemaConsulting.Rendering.Layout.Tests/DiamondShapeAnchorTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/EndLabelPassThroughTests.cs");
            designFiles = ("docs/design/rendering-layout/hierarchical-layout-algorithm.md");
            verificationFiles = ("docs/verification/rendering-layout/hierarchical-layout-algorithm.md");
            requirementFiles = ("docs/reqstream/rendering-layout/hierarchical-layout-algorithm.yaml");
        }
    }
}
