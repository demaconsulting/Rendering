package RenderingLayout {
    private import SoftwareItemMetadata::*;

    part def ContainmentLayout {
        doc /* Public containment packing entry point. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Layout/ContainmentLayout.cs");
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/ContainmentLayoutTests.cs");
            designFiles = ("docs/design/rendering-layout/containment-layout.md");
            verificationFiles = ("docs/verification/rendering-layout/containment-layout.md");
            requirementFiles = ("docs/reqstream/rendering-layout/containment-layout.yaml");
        }
    }
}
