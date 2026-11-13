package RenderingLayout {
    private import SoftwareItemMetadata::*;

    part def EdgeRoutingOption {
        doc /* Realization of the EdgeRouting option (no source file of its own; the option is declared in the Rendering model and consumed by the layout algorithms). */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/EdgeRoutingOptionTests.cs");
            designFiles = ("docs/design/rendering-layout/edge-routing-option.md");
            verificationFiles = ("docs/verification/rendering-layout/edge-routing-option.md");
            requirementFiles = ("docs/reqstream/rendering-layout/edge-routing-option.yaml");
        }
    }
}
