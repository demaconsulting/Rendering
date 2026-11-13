package RenderingModel {
    private import SoftwareItemMetadata::*;

    part def LayoutGraph {
        doc /* Unplaced input graph model of nodes, ports, and edges. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering/Graph/ILayoutConnectable.cs", "src/DemaConsulting.Rendering/Graph/LayoutGraph.cs", "src/DemaConsulting.Rendering/Graph/LayoutGraphEdge.cs", "src/DemaConsulting.Rendering/Graph/LayoutGraphNode.cs", "src/DemaConsulting.Rendering/Graph/LayoutGraphPort.cs", "src/DemaConsulting.Rendering/Graph/LayoutGraphPortCollection.cs");
            testFiles = ("test/DemaConsulting.Rendering.Tests/LayoutGraphTests.cs", "test/DemaConsulting.Rendering.Tests/LayoutGraphPortTests.cs");
            designFiles = ("docs/design/rendering/layout-graph.md");
            verificationFiles = ("docs/verification/rendering/layout-graph.md");
            requirementFiles = ("docs/reqstream/rendering/layout-graph.yaml");
        }
    }
}
