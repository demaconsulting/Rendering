package RenderingLayout {
    private import SoftwareItemMetadata::*;

    part def DefaultLayout {
        doc /* LayoutEngine facade and the default layout-algorithm registry. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Layout/LayoutEngine.cs", "src/DemaConsulting.Rendering.Layout/LayoutAlgorithms.cs");
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/LayoutEngineTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/LayoutAlgorithmsTests.cs");
            designFiles = ("docs/design/rendering-layout/default-layout.md");
            verificationFiles = ("docs/verification/rendering-layout/default-layout.md");
            requirementFiles = ("docs/reqstream/rendering-layout/default-layout.yaml");
        }
    }
}
