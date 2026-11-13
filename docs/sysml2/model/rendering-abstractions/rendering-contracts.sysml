package RenderingAbstractions {
    private import SoftwareItemMetadata::*;

    part def RenderingContracts {
        doc /* Layout-algorithm and renderer extension-point contracts plus RenderOptions and RenderOutput. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Abstractions/LayoutAlgorithmBase.cs", "src/DemaConsulting.Rendering.Abstractions/IRenderer.cs", "src/DemaConsulting.Rendering.Abstractions/RenderOptions.cs", "src/DemaConsulting.Rendering.Abstractions/RenderOutput.cs");
            testFiles = ("test/DemaConsulting.Rendering.Abstractions.Tests/RegistryTests.cs");
            designFiles = ("docs/design/rendering-abstractions/rendering-contracts.md");
            verificationFiles = ("docs/verification/rendering-abstractions/rendering-contracts.md");
            requirementFiles = ("docs/reqstream/rendering-abstractions/rendering-contracts.yaml");
        }
    }
}
