package RenderingAbstractions {
    private import SoftwareItemMetadata::*;

    part def Registries {
        doc /* LayoutAlgorithmRegistry and RendererRegistry for looking up algorithms and renderers. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Abstractions/LayoutAlgorithmRegistry.cs", "src/DemaConsulting.Rendering.Abstractions/RendererRegistry.cs");
            testFiles = ("test/DemaConsulting.Rendering.Abstractions.Tests/RegistryTests.cs");
            designFiles = ("docs/design/rendering-abstractions/registries.md");
            verificationFiles = ("docs/verification/rendering-abstractions/registries.md");
            requirementFiles = ("docs/reqstream/rendering-abstractions/registries.yaml");
        }
    }
}
