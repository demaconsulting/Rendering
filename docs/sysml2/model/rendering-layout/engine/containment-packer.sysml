package RenderingLayout {
    private import SoftwareItemMetadata::*;

    part def ContainmentPacker {
        doc /* Shelf packer for grouped/containment layout. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Layout/Engine/ContainmentPacker.cs");
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/Engine/ContainmentPackerTests.cs");
            designFiles = ("docs/design/rendering-layout/engine/containment-packer.md");
            verificationFiles = ("docs/verification/rendering-layout/engine/containment-packer.md");
            requirementFiles = ("docs/reqstream/rendering-layout/engine/containment-packer.yaml");
        }
    }
}
