package RenderingModel {
    private import SoftwareItemMetadata::*;

    part def Options {
        doc /* Open property system: LayoutProperty, IPropertyHolder, LayoutOptions, and CoreOptions. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Rendering/Options/CoreOptions.cs", "src/DemaConsulting.Rendering/Options/EdgeRouting.cs", "src/DemaConsulting.Rendering/Options/HierarchyHandling.cs", "src/DemaConsulting.Rendering/Options/IPropertyHolder.cs", "src/DemaConsulting.Rendering/Options/LayoutFlowDirection.cs", "src/DemaConsulting.Rendering/Options/LayoutOptions.cs", "src/DemaConsulting.Rendering/Options/LayoutProperty.cs", "src/DemaConsulting.Rendering/Options/PropertyHolder.cs");
            testFiles = ("test/DemaConsulting.Rendering.Tests/PropertyHolderTests.cs", "test/DemaConsulting.Rendering.Tests/CoreOptionsTests.cs");
            designFiles = ("docs/design/rendering/options.md");
            verificationFiles = ("docs/verification/rendering/options.md");
            requirementFiles = ("docs/reqstream/rendering/options.yaml");
        }
    }
}
