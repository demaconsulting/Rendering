// Views rendered into docs/design/generated/ for the Design document.
// The repository contains five systems (one per NuGet package), so the overview is a
// general view exposing each system package's members, plus the shared OTS package,
// with documentation filtered out to keep the diagram readable. Per SysML2Tools syntax,
// 'expose' is only valid inside a named 'view' usage (not a 'view def' definition), and
// it - not the view definition - is what scopes the rendered diagram content.
package RenderingDesignViews {
    private import StandardViewDefinitions::*;

    view SoftwareStructureView : GeneralView {
        expose RenderingModel::**;
        expose RenderingAbstractions::**;
        expose RenderingLayout::**;
        expose RenderingSvg::**;
        expose RenderingCanvasNet::**;
        expose OtsDependencies::**;
        filter not @SysML::Documentation;
    }
}
