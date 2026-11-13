package RenderingAbstractions {
    private import SoftwareItemMetadata::*;

    part def BoxMetrics {
        doc /* Box title-area and folder-tab geometry. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Abstractions/BoxMetrics.cs");
            testFiles = ("test/DemaConsulting.Rendering.Abstractions.Tests/BoxMetricsTests.cs");
            designFiles = ("docs/design/rendering-abstractions/box-metrics.md");
            verificationFiles = ("docs/verification/rendering-abstractions/box-metrics.md");
            requirementFiles = ("docs/reqstream/rendering-abstractions/box-metrics.yaml");
        }
    }
}
