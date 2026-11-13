package RenderingAbstractions {
    private import SoftwareItemMetadata::*;

    part def ConnectorLabelPlacer {
        doc /* Collision-aware connector-label placement. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Abstractions/ConnectorLabelPlacer.cs");
            testFiles = ("test/DemaConsulting.Rendering.Abstractions.Tests/ConnectorLabelPlacerTests.cs");
            designFiles = ("docs/design/rendering-abstractions/connector-label-placer.md");
            verificationFiles = ("docs/verification/rendering-abstractions/connector-label-placer.md");
            requirementFiles = ("docs/reqstream/rendering-abstractions/connector-label-placer.yaml");
        }
    }
}
