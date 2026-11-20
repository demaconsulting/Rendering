package RenderingLayout {
    private import RenderingLayout_AutoLayoutAlgorithm::*;
    private import RenderingLayout_ConnectorRouter::*;
    private import RenderingLayout_ContainmentLayout::*;
    private import RenderingLayout_ContainmentLayoutAlgorithm::*;
    private import RenderingLayout_DefaultLayout::*;
    private import RenderingLayout_EdgeRoutingOption::*;
    private import RenderingLayout_Engine::*;
    private import RenderingLayout_HierarchicalLayoutAlgorithm::*;
    private import RenderingLayout_LayeredLayoutAlgorithm::*;
    private import SoftwareItemMetadata::*;

    doc /* DemaConsulting.Rendering.Layout provides the pluggable layout algorithms and the reusable geometric engines they are built from. */

    part def RenderingLayoutSystem {
        doc /* The Rendering.Layout system as a whole: the layered, containment, hierarchical, and auto algorithms, connector routing, and the default layout facade. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/rendering-layout.md");
            verificationFiles = ("docs/verification/rendering-layout.md");
            requirementFiles = ("docs/reqstream/rendering-layout.yaml");
        }

        part engine : Engine;
        part layeredLayoutAlgorithm : LayeredLayoutAlgorithm;
        part containmentLayoutAlgorithm : ContainmentLayoutAlgorithm;
        part hierarchicalLayoutAlgorithm : HierarchicalLayoutAlgorithm;
        part autoLayoutAlgorithm : AutoLayoutAlgorithm;
        part containmentLayout : ContainmentLayout;
        part connectorRouter : ConnectorRouter;
        part edgeRoutingOption : EdgeRoutingOption;
        part defaultLayout : DefaultLayout;
    }
}
