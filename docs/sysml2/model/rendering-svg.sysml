package RenderingSvg {
    private import SoftwareItemMetadata::*;

    doc /* DemaConsulting.Rendering.Svg renders a placed LayoutTree to SVG with no external dependencies. */

    part def RenderingSvgSystem {
        doc /* The Rendering.Svg system as a whole: the SVG renderer. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/rendering-svg.md");
            verificationFiles = ("docs/verification/rendering-svg.md");
            requirementFiles = ("docs/reqstream/rendering-svg.yaml");
        }

        part svgRenderer : SvgRenderer;
    }
}
