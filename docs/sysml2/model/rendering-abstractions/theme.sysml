package RenderingAbstractions {
    private import SoftwareItemMetadata::*;

    part def Theme {
        doc /* Theme record and the built-in themes. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Abstractions/Theme.cs");
            testFiles = ("test/DemaConsulting.Rendering.Abstractions.Tests/ThemeTests.cs");
            designFiles = ("docs/design/rendering-abstractions/theme.md");
            verificationFiles = ("docs/verification/rendering-abstractions/theme.md");
            requirementFiles = ("docs/reqstream/rendering-abstractions/theme.yaml");
        }
    }
}
