package RenderingModel {
    private import SoftwareItemMetadata::*;

    doc /* DemaConsulting.Rendering is the layout model: the LayoutTree intermediate representation, the open property system, and the unplaced input LayoutGraph. */

    part def RenderingModelSystem {
        doc /* The Rendering model system as a whole: the LayoutTree IR, the property system, and the input LayoutGraph. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/rendering.md");
            verificationFiles = ("docs/verification/rendering.md");
            requirementFiles = ("docs/reqstream/rendering.yaml");
        }

        part layoutTree : LayoutTree;
        part options : Options;
        part layoutGraph : LayoutGraph;
    }
}
