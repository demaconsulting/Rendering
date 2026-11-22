package RenderingCanvasNet {
    public import RenderingCanvasNet_CanvasNetRasterRenderer::*;
    public import RenderingCanvasNet_JpegRenderer::*;
    public import RenderingCanvasNet_PngRenderer::*;
    private import SoftwareItemMetadata::*;
    private import OtsDependencies::*;

    doc /* DemaConsulting.Rendering.CanvasNet renders a placed LayoutTree to PNG and JPEG using the CanvasNet raster library. */

    part def RenderingCanvasNetSystem {
        doc /* The Rendering.CanvasNet system as a whole: the CanvasNet raster renderers. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/rendering-canvasnet.md");
            verificationFiles = ("docs/verification/rendering-canvasnet.md");
            requirementFiles = ("docs/reqstream/rendering-canvasnet.yaml");
        }

        part canvasNetRasterRenderer : CanvasNetRasterRenderer;
        part pngRenderer : PngRenderer;
        part jpegRenderer : JpegRenderer;

        part canvasNet : CanvasNet;
    }
}
