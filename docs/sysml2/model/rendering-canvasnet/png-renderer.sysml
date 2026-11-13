package RenderingCanvasNet {
    private import SoftwareItemMetadata::*;

    part def PngRenderer {
        doc /* Lossless PNG output. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.CanvasNet/PngRenderer.cs");
            testFiles = ("test/DemaConsulting.Rendering.CanvasNet.Tests/PngRendererTests.cs", "test/DemaConsulting.Rendering.CanvasNet.Tests/CanvasNetFormatRendererTests.cs");
            designFiles = ("docs/design/rendering-canvasnet/png-renderer.md");
            verificationFiles = ("docs/verification/rendering-canvasnet/png-renderer.md");
            requirementFiles = ("docs/reqstream/rendering-canvasnet/png-renderer.yaml");
        }
    }
}
