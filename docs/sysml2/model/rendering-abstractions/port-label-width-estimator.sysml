package RenderingAbstractions {
    private import SoftwareItemMetadata::*;

    part def PortLabelWidthEstimator {
        doc /* Shared port-label natural-width estimator used by layout and SVG rendering. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Abstractions/PortLabelWidthEstimator.cs");
            testFiles = ("test/DemaConsulting.Rendering.Abstractions.Tests/PortLabelWidthEstimatorTests.cs");
            designFiles = ("docs/design/rendering-abstractions/port-label-width-estimator.md");
            verificationFiles = ("docs/verification/rendering-abstractions/port-label-width-estimator.md");
            requirementFiles = ("docs/reqstream/rendering-abstractions/port-label-width-estimator.yaml");
        }
    }
}
