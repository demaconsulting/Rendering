// Views rendered into docs/design/generated/ for the Design document.
// The repository contains five systems (one per NuGet package), so the overview is a
// general view exposing each system part def and each unit part def by qualified name
// (every package is uniquely named, with one file per package), plus the shared OTS
// package, with documentation filtered out to keep the diagram readable. Part usages are also
// filtered out: without that filter every exposed part def draws its owned part usages as a
// diamond-linked tree, which makes the diagram over 3700 px wide; with it each system and unit
// lists its parts in a compartment and the diagram stays near 1250 px. Per SysML2Tools syntax,
// 'expose' is only valid inside a named 'view' usage (not a 'view def' definition), and
// it - not the view definition - is what scopes the rendered diagram content.
package RenderingDesignViews {
    private import StandardViewDefinitions::*;

    view SoftwareStructureView : GeneralView {
        expose RenderingModel::RenderingModelSystem;
        expose RenderingModel_LayoutGraph::LayoutGraph;
        expose RenderingModel_LayoutTree::LayoutTree;
        expose RenderingModel_Options::Options;
        expose RenderingAbstractions::RenderingAbstractionsSystem;
        expose RenderingAbstractions_BoxMetrics::BoxMetrics;
        expose RenderingAbstractions_ConnectorLabelPlacer::ConnectorLabelPlacer;
        expose RenderingAbstractions_NotationMetrics::NotationMetrics;
        expose RenderingAbstractions_PortLabelWidthEstimator::PortLabelWidthEstimator;
        expose RenderingAbstractions_Registries::Registries;
        expose RenderingAbstractions_RenderingContracts::RenderingContracts;
        expose RenderingAbstractions_Theme::Theme;
        expose RenderingLayout::RenderingLayoutSystem;
        expose RenderingLayout_AutoLayoutAlgorithm::AutoLayoutAlgorithm;
        expose RenderingLayout_ConnectorRouter::ConnectorRouter;
        expose RenderingLayout_ContainmentLayout::ContainmentLayout;
        expose RenderingLayout_ContainmentLayoutAlgorithm::ContainmentLayoutAlgorithm;
        expose RenderingLayout_DefaultLayout::DefaultLayout;
        expose RenderingLayout_EdgeRoutingOption::EdgeRoutingOption;
        expose RenderingLayout_Engine::Engine;
        expose RenderingLayout_CanvasFitter::CanvasFitter;
        expose RenderingLayout_ContainmentPacker::ContainmentPacker;
        expose RenderingLayout_EdgeCountGapWidener::EdgeCountGapWidener;
        expose RenderingLayout_InterconnectionLayoutEngine::InterconnectionLayoutEngine;
        expose RenderingLayout_LayeredPipeline::LayeredPipeline;
        expose RenderingLayout_LayoutTreePacker::LayoutTreePacker;
        expose RenderingLayout_OrthogonalEdgeRouter::OrthogonalEdgeRouter;
        expose RenderingLayout_HierarchicalLayoutAlgorithm::HierarchicalLayoutAlgorithm;
        expose RenderingLayout_LayeredLayoutAlgorithm::LayeredLayoutAlgorithm;
        expose RenderingSvg::RenderingSvgSystem;
        expose RenderingSvg_SvgRenderer::SvgRenderer;
        expose RenderingCanvasNet::RenderingCanvasNetSystem;
        expose RenderingCanvasNet_CanvasNetRasterRenderer::CanvasNetRasterRenderer;
        expose RenderingCanvasNet_JpegRenderer::JpegRenderer;
        expose RenderingCanvasNet_PngRenderer::PngRenderer;
        expose OtsDependencies::**;
        filter not (@SysML::Documentation or @SysML::PartUsage);
    }
}
