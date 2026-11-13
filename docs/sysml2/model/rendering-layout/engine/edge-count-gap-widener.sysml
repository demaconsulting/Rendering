package RenderingLayout {
    private import SoftwareItemMetadata::*;

    part def EdgeCountGapWidener {
        doc /* Shared edge-count-to-gap corridor-width formula. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Layout/Engine/EdgeCountGapWidener.cs");
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/Engine/EdgeCountGapWidenerTests.cs");
            designFiles = ("docs/design/rendering-layout/engine/edge-count-gap-widener.md");
            verificationFiles = ("docs/verification/rendering-layout/engine/edge-count-gap-widener.md");
            requirementFiles = ("docs/reqstream/rendering-layout/engine/edge-count-gap-widener.yaml");
        }
    }
}
