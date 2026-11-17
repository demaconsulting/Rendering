package RenderingLayout {
    private import SoftwareItemMetadata::*;

    part def ConnectorRouter {
        doc /* Public edge-routing orchestration. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering.Layout/ConnectorRouter.cs");
            testFiles = ("test/DemaConsulting.Rendering.Layout.Tests/ConnectorRouterTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/NotationPrimitivePassThroughTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/FaceAlignmentTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/DiagramQuality.cs", "test/DemaConsulting.Rendering.Layout.Tests/DiamondShapeAnchorTests.cs", "test/DemaConsulting.Rendering.Layout.Tests/EndLabelPassThroughTests.cs");
            designFiles = ("docs/design/rendering-layout/connector-router.md");
            verificationFiles = ("docs/verification/rendering-layout/connector-router.md");
            requirementFiles = ("docs/reqstream/rendering-layout/connector-router.yaml");
        }
    }
}
