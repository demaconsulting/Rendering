package RenderingModel {
    private import SoftwareItemMetadata::*;

    part def LayoutTree {
        doc /* Immutable placed intermediate representation records (LayoutTree and its node records). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering/LayoutTree/LayoutBadge.cs", "src/DemaConsulting.Rendering/LayoutTree/LayoutBand.cs", "src/DemaConsulting.Rendering/LayoutTree/LayoutBox.cs", "src/DemaConsulting.Rendering/LayoutTree/LayoutGrid.cs", "src/DemaConsulting.Rendering/LayoutTree/LayoutLabel.cs", "src/DemaConsulting.Rendering/LayoutTree/LayoutLifeline.cs", "src/DemaConsulting.Rendering/LayoutTree/LayoutLine.cs", "src/DemaConsulting.Rendering/LayoutTree/LayoutNode.cs", "src/DemaConsulting.Rendering/LayoutTree/LayoutPort.cs", "src/DemaConsulting.Rendering/LayoutTree/LayoutTree.cs", "src/DemaConsulting.Rendering/Rect.cs");
            testFiles = ("test/DemaConsulting.Rendering.Tests/LayoutTests.cs");
            designFiles = ("docs/design/rendering/layout-tree.md");
            verificationFiles = ("docs/verification/rendering/layout-tree.md");
            requirementFiles = ("docs/reqstream/rendering/layout-tree.yaml");
        }
    }
}
