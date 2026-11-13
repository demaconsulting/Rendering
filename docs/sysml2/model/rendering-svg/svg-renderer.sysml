package RenderingSvg {
    private import SoftwareItemMetadata::*;

    part def SvgRenderer {
        doc /* Renders a placed LayoutTree to an SVG document. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Svg/SvgRenderer.cs");
            testFiles = ("test/DemaConsulting.Rendering.Svg.Tests/SvgEndMarkerTests.cs", "test/DemaConsulting.Rendering.Svg.Tests/SvgRendererPortedTests.cs", "test/DemaConsulting.Rendering.Svg.Tests/SvgRendererTests.cs");
            designFiles = ("docs/design/rendering-svg/svg-renderer.md");
            verificationFiles = ("docs/verification/rendering-svg/svg-renderer.md");
            requirementFiles = ("docs/reqstream/rendering-svg/svg-renderer.yaml");
        }
    }
}
