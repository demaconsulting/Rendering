package RenderingLayout_LayoutTreePacker {
    private import SoftwareItemMetadata::*;

    part def LayoutTreePacker {
        doc /* Packer merging multiple placed LayoutTrees (shelf or free-space arrangement) and the empty-space measure. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Layout/Engine/LayoutTreePacker.cs", "src/DemaConsulting.Rendering.Layout/Engine/EmptySpaceMeter.cs");
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/Engine/LayoutTreePackerTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/Engine/EmptySpaceMeterTests.cs");
            designFiles = ("docs/design/rendering-layout/engine/layout-tree-packer.md");
            verificationFiles = ("docs/verification/rendering-layout/engine/layout-tree-packer.md");
            requirementFiles = ("docs/reqstream/rendering-layout/engine/layout-tree-packer.yaml");
        }
    }
}
