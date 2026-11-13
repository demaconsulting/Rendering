package RenderingLayout {
    private import SoftwareItemMetadata::*;

    part def LayeredLayoutAlgorithm {
        doc /* The public layered layout algorithm. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Layout/LayeredLayoutAlgorithm.cs");
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/LayeredLayoutAlgorithmTests.cs");
            designFiles = ("docs/design/rendering-layout/layered-layout-algorithm.md");
            verificationFiles = ("docs/verification/rendering-layout/layered-layout-algorithm.md");
            requirementFiles = ("docs/reqstream/rendering-layout/layered-layout-algorithm.yaml");
        }
    }
}
