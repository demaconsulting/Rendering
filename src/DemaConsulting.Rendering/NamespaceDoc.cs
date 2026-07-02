// <copyright file="NamespaceDoc.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// The diagram model and entry point for a general-purpose, ELK-inspired diagramming toolkit: the
/// unplaced input <see cref="LayoutGraph"/>, the placed <see cref="LayoutTree"/>, the core geometry
/// types, and the open property system that configures layout and rendering.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Start here.</strong> <c>DemaConsulting.Rendering</c> is the root package of a
/// general-purpose, ELK-inspired toolkit that lays out and draws node-and-edge diagrams. It holds
/// the <em>diagram model</em>; the layout algorithms and renderers live in sibling packages that
/// build on it.
/// </para>
/// <para>
/// <strong>The model at a glance.</strong> You describe a diagram as a <see cref="LayoutGraph"/> — a
/// (possibly nested) graph of sized boxes and directed edges. Laying it out produces a
/// <see cref="LayoutTree"/>: an immutable tree of placed nodes (<see cref="LayoutBox"/>,
/// <see cref="LayoutLine"/>, and the other <c>Layout*</c> record types) carrying absolute
/// coordinates. Geometry is expressed with <see cref="Point2D"/> and <see cref="Rect"/>. Layout and
/// render behaviour is configured through the open property system (<c>LayoutProperty&lt;T&gt;</c>
/// keys carried on any <c>IPropertyHolder</c>), with the well-known keys gathered on
/// <see cref="CoreOptions"/> (for example <see cref="CoreOptions.Algorithm"/>,
/// <see cref="CoreOptions.EdgeRouting"/>, and <see cref="CoreOptions.HierarchyHandling"/>).
/// </para>
/// <para><strong>The full path to a diagram — where to go next:</strong></para>
/// <list type="number">
///   <item>
///     <description>
///     Build a <see cref="LayoutGraph"/> here in <c>DemaConsulting.Rendering</c> — add sized nodes
///     and directed edges, nesting nodes for compound diagrams.
///     </description>
///   </item>
///   <item>
///     <description>
///     Lay it out with the <c>DemaConsulting.Rendering.Layout</c> package: the one-call
///     <c>LayoutEngine.Layout(graph, options)</c> facade resolves whatever algorithm the graph
///     declares, or select a specific <c>ILayoutAlgorithm</c> (<c>layered</c>, <c>containment</c>,
///     or the recursive <c>hierarchical</c> engine) yourself. This yields a placed
///     <see cref="LayoutTree"/>.
///     </description>
///   </item>
///   <item>
///     <description>
///     Render the <see cref="LayoutTree"/> with a renderer from <c>DemaConsulting.Rendering.Svg</c>
///     (<c>SvgRenderer</c>, zero dependencies) or <c>DemaConsulting.Rendering.Skia</c> (raster
///     PNG/JPEG/WEBP).
///     </description>
///   </item>
/// </list>
/// <para>
/// The <c>DemaConsulting.Rendering.Abstractions</c> package defines the <c>ILayoutAlgorithm</c> and
/// <c>IRenderer</c> service-provider contracts, their registries, and the <c>Theme</c> that styles
/// output. The dependency pipeline is <em>model &#8592; Abstractions &#8592; Layout &#8592;
/// Svg/Skia</em>, so this model package depends on nothing and is the natural place to begin reading.
/// </para>
/// <para>
/// Configuration is <strong>open</strong> and <strong>property-based</strong>: algorithms and
/// renderers read only the properties they understand, so unknown or not-yet-honoured properties
/// default harmlessly. New diagram families and output formats are added additively by implementing
/// <c>ILayoutAlgorithm</c> or <c>IRenderer</c> and registering them — no existing contract changes.
/// The public surface deliberately mirrors the
/// <see href="https://eclipse.dev/elk/">Eclipse Layout Kernel (ELK)</see> so ELK users are
/// immediately at home.
/// </para>
/// </remarks>
/// <example>
/// An end-to-end diagram: build a graph, lay it out, and render it to SVG.
/// <code>
/// using System.IO;
/// using DemaConsulting.Rendering;
/// using DemaConsulting.Rendering.Abstractions;
/// using DemaConsulting.Rendering.Layout;
/// using DemaConsulting.Rendering.Svg;
///
/// // 1. Describe the diagram as a graph of sized boxes and directed edges.
/// var graph = new LayoutGraph();
/// var a = graph.AddNode("a", 80, 40);
/// var b = graph.AddNode("b", 80, 40);
/// var c = graph.AddNode("c", 80, 40);
/// graph.AddEdge("a-b", a, b);
/// graph.AddEdge("b-c", b, c);
///
/// // 2. Lay it out with the one-call facade (default: hierarchical, byte-identical to
/// //    layered for this flat graph).
/// LayoutTree tree = LayoutEngine.Layout(graph, new LayoutOptions());
///
/// // 3. Render the placed tree to SVG.
/// using var output = File.Create("diagram.svg");
/// new SvgRenderer().Render(tree, new RenderOptions(Themes.Light), output);
/// </code>
/// </example>
internal static class NamespaceDoc
{
}
