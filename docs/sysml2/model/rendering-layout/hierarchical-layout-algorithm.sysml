package RenderingLayout {
    private import SoftwareItemMetadata::*;

    part def HierarchicalLayoutAlgorithm {
        doc /* The recursive hierarchical layout engine. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Layout/HierarchicalLayoutAlgorithm.cs");
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/HierarchicalLayoutAlgorithmTests.cs");
            designFiles = ("docs/design/rendering-layout/hierarchical-layout-algorithm.md");
            verificationFiles = ("docs/verification/rendering-layout/hierarchical-layout-algorithm.md");
            requirementFiles = ("docs/reqstream/rendering-layout/hierarchical-layout-algorithm.yaml");
        }
    }
}
