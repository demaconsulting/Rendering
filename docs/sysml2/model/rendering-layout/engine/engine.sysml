package RenderingLayout_Engine {
    public import RenderingLayout_CanvasFitter::*;
    public import RenderingLayout_ContainmentPacker::*;
    public import RenderingLayout_EdgeCountGapWidener::*;
    public import RenderingLayout_InterconnectionLayoutEngine::*;
    public import RenderingLayout_LayeredPipeline::*;
    public import RenderingLayout_LayoutTreePacker::*;
    public import RenderingLayout_OrthogonalEdgeRouter::*;
    private import SoftwareItemMetadata::*;

    part def Engine {
        doc /* Reusable geometric layout engines composed by the public layout algorithms. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/rendering-layout/engine/engine.md");
            verificationFiles = ("docs/verification/rendering-layout/engine/engine.md");
            requirementFiles = ("docs/reqstream/rendering-layout/engine/engine.yaml");
        }

        part orthogonalEdgeRouter : OrthogonalEdgeRouter;
        part containmentPacker : ContainmentPacker;
        part interconnectionLayoutEngine : InterconnectionLayoutEngine;
        part layeredPipeline : LayeredPipeline;
        part layoutTreePacker : LayoutTreePacker;
        part edgeCountGapWidener : EdgeCountGapWidener;
        part canvasFitter : CanvasFitter;
    }
}
