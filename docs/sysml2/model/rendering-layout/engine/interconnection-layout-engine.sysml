package RenderingLayout {
    private import SoftwareItemMetadata::*;

    part def InterconnectionLayoutEngine {
        doc /* Cross-edge routing among placed boxes. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Layout/Engine/InterconnectionLayoutEngine.cs");
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/Engine/InterconnectionLayoutEngineTests.cs");
            designFiles = ("docs/design/rendering-layout/engine/interconnection-layout-engine.md");
            verificationFiles = ("docs/verification/rendering-layout/engine/interconnection-layout-engine.md");
            requirementFiles = ("docs/reqstream/rendering-layout/engine/interconnection-layout-engine.yaml");
        }
    }
}
