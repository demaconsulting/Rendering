package RenderingLayout {
    private import SoftwareItemMetadata::*;

    part def AutoLayoutAlgorithm {
        doc /* The auto-routing meta-algorithm. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Layout/AutoLayoutAlgorithm.cs");
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/AutoLayoutAlgorithmTests.cs");
            designFiles = ("docs/design/rendering-layout/auto-layout-algorithm.md");
            verificationFiles = ("docs/verification/rendering-layout/auto-layout-algorithm.md");
            requirementFiles = ("docs/reqstream/rendering-layout/auto-layout-algorithm.yaml");
        }
    }
}
