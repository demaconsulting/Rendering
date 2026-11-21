// Views rendered into docs/design/generated/ for the Design document.
//
// The repository contains five systems (one per NuGet package) and a shared OTS package. An
// interconnection view draws a part def's interior (SysML2Tools 0.3.0-beta.4+), so each system
// is a frame with its subsystem and unit part usages nested inside and the unit defs themselves
// are not exposed. Interconnection views place the exposed systems side by side in one row, and
// all five in one row measure about 2470 pixels, so the systems are split over two views to stay
// readable at document width (each about 1600 pixels or less):
//   - SoftwareStructureView: the Abstractions and Layout systems, the contracts and the
//     algorithms built on them.
//   - ModelAndRenderersView: the Model system and the SVG and CanvasNet renderer systems.
//   - OtsView: the shared OTS package as a package frame of part defs, kept out of the others.
// Per SysML2Tools syntax, 'expose' is only valid inside a named 'view' usage (not a 'view def'
// definition), and it - not the view definition - is what scopes the rendered diagram content.
// Documentation and metadata defs (SoftwareInfo, which every package imports) are filtered out of
// every view so doc compartments and metadata boxes cannot appear; the 'not (...)' parentheses are
// required by the filter grammar.
package RenderingDesignViews {
    private import StandardViewDefinitions::*;

    view SoftwareStructureView : InterconnectionView {
        expose RenderingAbstractions::RenderingAbstractionsSystem;
        expose RenderingLayout::RenderingLayoutSystem;
        filter not (@SysML::Documentation or @SysML::MetadataDefinition);
    }

    view ModelAndRenderersView : InterconnectionView {
        expose RenderingModel::RenderingModelSystem;
        expose RenderingSvg::RenderingSvgSystem;
        expose RenderingCanvasNet::RenderingCanvasNetSystem;
        filter not (@SysML::Documentation or @SysML::MetadataDefinition);
    }

    view OtsView : GeneralView {
        expose OtsDependencies::*;
        filter not (@SysML::Documentation or @SysML::MetadataDefinition);
    }
}