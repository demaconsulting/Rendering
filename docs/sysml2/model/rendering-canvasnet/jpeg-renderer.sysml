package RenderingCanvasNet {
    private import SoftwareItemMetadata::*;

    part def JpegRenderer {
        doc /* JPEG output. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.CanvasNet/JpegRenderer.cs");
            testFiles = ("test/DemaConsulting.Rendering.CanvasNet.Tests/CanvasNetFormatRendererTests.cs");
            designFiles = ("docs/design/rendering-canvasnet/jpeg-renderer.md");
            verificationFiles = ("docs/verification/rendering-canvasnet/jpeg-renderer.md");
            requirementFiles = ("docs/reqstream/rendering-canvasnet/jpeg-renderer.yaml");
        }
    }
}
