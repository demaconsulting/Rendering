package RenderingCanvasNet {
    private import SoftwareItemMetadata::*;

    part def CanvasNetRasterRenderer {
        doc /* Abstract CanvasNet rasterizer shared by all raster formats. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.CanvasNet/CanvasNetRasterRenderer.cs", "src/DemaConsulting.Rendering.CanvasNet/CanvasNetTypefaces.cs");
            testFiles = ("test/DemaConsulting.Rendering.CanvasNet.Tests/PngRendererPortedTests.cs", "test/DemaConsulting.Rendering.CanvasNet.Tests/PngEndMarkerTests.cs", "test/DemaConsulting.Rendering.CanvasNet.Tests/CanvasNetPortAndContentInsetTests.cs", "test/DemaConsulting.Rendering.CanvasNet.Tests/CanvasNetTypefacesTests.cs");
            designFiles = ("docs/design/rendering-canvasnet/canvasnet-raster-renderer.md");
            verificationFiles = ("docs/verification/rendering-canvasnet/canvasnet-raster-renderer.md");
            requirementFiles = ("docs/reqstream/rendering-canvasnet/canvasnet-raster-renderer.yaml");
        }
    }
}
