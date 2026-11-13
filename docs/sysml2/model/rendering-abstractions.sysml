package RenderingAbstractions {
    private import SoftwareItemMetadata::*;

    doc /* DemaConsulting.Rendering.Abstractions is the service-provider interface between the layout model, the layout algorithms, and the renderers. */

    part def RenderingAbstractionsSystem {
        doc /* The Rendering.Abstractions system as a whole: layout and render contracts, registries, theme, and shared notation geometry. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/rendering-abstractions.md");
            verificationFiles = ("docs/verification/rendering-abstractions.md");
            requirementFiles = ("docs/reqstream/rendering-abstractions.yaml");
        }

        part renderingContracts : RenderingContracts;
        part registries : Registries;
        part theme : Theme;
        part notationMetrics : NotationMetrics;
        part boxMetrics : BoxMetrics;
        part connectorLabelPlacer : ConnectorLabelPlacer;
        part portLabelWidthEstimator : PortLabelWidthEstimator;
    }
}
