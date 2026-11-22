package RenderingLayout {
    public import RenderingLayout_AutoLayoutAlgorithm::*;
    public import RenderingLayout_ConnectorRouter::*;
    public import RenderingLayout_ContainmentLayout::*;
    public import RenderingLayout_ContainmentLayoutAlgorithm::*;
    public import RenderingLayout_DefaultLayout::*;
    public import RenderingLayout_EdgeRoutingOption::*;
    public import RenderingLayout_Engine::*;
    public import RenderingLayout_HierarchicalLayoutAlgorithm::*;
    public import RenderingLayout_LayeredLayoutAlgorithm::*;
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
