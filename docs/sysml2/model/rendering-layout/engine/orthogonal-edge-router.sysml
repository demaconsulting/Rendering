package RenderingLayout {
    private import SoftwareItemMetadata::*;

    part def OrthogonalEdgeRouter {
        doc /* Orthogonal (channel) edge router. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Layout/Engine/OrthogonalEdgeRouter.cs", "src/DemaConsulting.Rendering.Layout/Engine/MinHeap.cs");
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/Engine/OrthogonalEdgeRouterTests.cs");
            designFiles = ("docs/design/rendering-layout/engine/orthogonal-edge-router.md");
            verificationFiles = ("docs/verification/rendering-layout/engine/orthogonal-edge-router.md");
            requirementFiles = ("docs/reqstream/rendering-layout/engine/orthogonal-edge-router.yaml");
        }
    }
}
