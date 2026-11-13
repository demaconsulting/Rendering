package RenderingLayout {
    private import SoftwareItemMetadata::*;

    part def LayoutTreePacker {
        doc /* Shelf packer merging multiple placed LayoutTrees. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Layout/Engine/LayoutTreePacker.cs");
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/Engine/LayoutTreePackerTests.cs");
            designFiles = ("docs/design/rendering-layout/engine/layout-tree-packer.md");
            verificationFiles = ("docs/verification/rendering-layout/engine/layout-tree-packer.md");
            requirementFiles = ("docs/reqstream/rendering-layout/engine/layout-tree-packer.yaml");
        }
    }
}
