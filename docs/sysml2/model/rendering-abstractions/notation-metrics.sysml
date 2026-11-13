package RenderingAbstractions {
    private import SoftwareItemMetadata::*;

    part def NotationMetrics {
        doc /* Intrinsic notation geometry shared by renderers. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Abstractions/NotationMetrics.cs");
            testFiles = ("test/DemaConsulting.Rendering.Abstractions.Tests/NotationMetricsTests.cs");
            designFiles = ("docs/design/rendering-abstractions/notation-metrics.md");
            verificationFiles = ("docs/verification/rendering-abstractions/notation-metrics.md");
            requirementFiles = ("docs/reqstream/rendering-abstractions/notation-metrics.yaml");
        }
    }
}
