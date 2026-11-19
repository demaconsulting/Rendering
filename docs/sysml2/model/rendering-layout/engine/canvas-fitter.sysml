package RenderingLayout {
    private import SoftwareItemMetadata::*;

    part def CanvasFitter {
        doc /* Grows a placed LayoutTree's canvas so connectors and connector labels stay inside it. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Layout/Engine/CanvasFitter.cs");
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/Engine/CanvasFitterTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/CanvasMarginTests.cs");
            designFiles = ("docs/design/rendering-layout/engine/canvas-fitter.md");
            verificationFiles = ("docs/verification/rendering-layout/engine/canvas-fitter.md");
            requirementFiles = ("docs/reqstream/rendering-layout/engine/canvas-fitter.yaml");
        }
    }
}
