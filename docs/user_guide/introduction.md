# Introduction

## Purpose

This document is the user guide for the Rendering libraries, a set of general-purpose .NET
packages for laying out and rendering node-and-edge diagrams. The design is inspired by the
[Eclipse Layout Kernel (ELK)](https://eclipse.dev/elk/): you describe a diagram as a graph,
a pluggable algorithm places it, and a pluggable renderer draws it — all configured through an
open, extensible property system.

## Scope

This user guide covers:

- Installation of the packages
- The core concepts (graph, options, algorithm, layout tree, renderer)
- Basic usage: laying out a graph and rendering it to SVG, PNG, JPEG, or WEBP
- Configuring layout through the open property system
- Extending the library with custom algorithms and renderers

# Continuous Compliance

This project follows the
[Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance) methodology, so
compliance evidence — requirements, justifications, a trace matrix, and quality reports — is
generated automatically on every CI run, and every requirement is linked to passing tests.

# Installation

The library is split into focused packages so consumers take only what they need:

| Package | When to install |
| --- | --- |
| `DemaConsulting.Rendering` | Always — the layout model, property system, and input graph |
| `DemaConsulting.Rendering.Abstractions` | Always — the algorithm/renderer contracts and registries |
| `DemaConsulting.Rendering.Layout` | To run the bundled algorithms and the `LayoutEngine` facade |
| `DemaConsulting.Rendering.Svg` | To render diagrams to SVG (no native dependencies) |
| `DemaConsulting.Rendering.Skia` | To render diagrams to PNG, JPEG, or WEBP (uses SkiaSharp) |

```bash
dotnet add package DemaConsulting.Rendering.Layout
dotnet add package DemaConsulting.Rendering.Svg
```

Installing a renderer or the layout package transitively brings in the model and abstractions.

# Core Concepts

- **`LayoutGraph`** — the *unplaced* input: sized `LayoutGraphNode` boxes and directed
  `LayoutGraphEdge` connections. The graph is recursive — a node may contain a nested child subgraph
  through its `Children` — so hierarchical (grouped) diagrams are expressible (see below).
- **`LayoutOptions`** — an open, property-keyed configuration bag. Options are declared as typed
  `LayoutProperty<T>` keys (see `CoreOptions`) and can be attached to the whole graph, a single
  element, or a free-standing options object.
- **`ILayoutAlgorithm`** — consumes a `LayoutGraph` plus `LayoutOptions` and produces a placed
  `LayoutTree`. Bundled implementations are `LayeredLayoutAlgorithm` (id `"layered"`),
  `ContainmentLayoutAlgorithm` (id `"containment"`), and the recursive `HierarchicalLayoutAlgorithm`
  (id `"hierarchical"`).
- **`LayoutEngine`** — the batteries-included facade (in `DemaConsulting.Rendering.Layout`). One call,
  `LayoutEngine.Layout(graph)`, lays out a graph with the algorithm it declares, resolved from
  the bundled algorithms. It handles both flat and nested graphs (see *Quickstart* below).
- **`LayoutTree`** — the *placed* result: boxes with absolute coordinates and orthogonally routed
  connectors.
- **`IRenderer`** — draws a `LayoutTree` to a stream. Bundled implementations are `SvgRenderer`
  and the SkiaSharp `PngRenderer`, `JpegRenderer`, and `WebpRenderer`.

# Usage

## Quickstart

The fastest path from a graph to a rendered diagram is the `LayoutEngine` facade: describe the graph,
lay it out in one call, and render the placed tree. `LayoutEngine.Layout` lays out the graph with the
algorithm it declares and — when it declares none — defaults to the recursive `hierarchical` engine, so
the *same* call correctly handles both flat and nested graphs:

```csharp
using System.IO;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout;
using DemaConsulting.Rendering.Svg;

// 1. Describe the diagram as a graph of sized boxes and directed edges.
var graph = new LayoutGraph();
var a = graph.AddNode("a", width: 80, height: 40);
var b = graph.AddNode("b", width: 80, height: 40);
var c = graph.AddNode("c", width: 80, height: 40);
graph.AddEdge("a-b", a, b);
graph.AddEdge("b-c", b, c);

// 2. Lay it out with whatever algorithm the graph declares (default: hierarchical).
var tree = LayoutEngine.Layout(graph);

// 3. Render the placed tree to SVG.
var renderer = new SvgRenderer();
using var output = File.Create("diagram.svg");
renderer.Render(tree, new RenderOptions(Themes.Light), output);
```

Swap `SvgRenderer` for `PngRenderer`, `JpegRenderer`, or `WebpRenderer` (from
`DemaConsulting.Rendering.Skia`) to produce a raster image instead; the rest of the flow is identical.

For a flat graph the hierarchical default is **byte-for-byte identical** to the `layered` algorithm, so
the quickstart above lays the chain out exactly as the layered algorithm would — while a nested graph
(see *Describing nested (hierarchical) graphs* below) is composed automatically by the same call.

## Choosing a layout algorithm

Select an algorithm by setting `CoreOptions.Algorithm` directly on the graph; `LayoutEngine`
resolves it from the bundled algorithms. The two bundled *leaf* algorithms arrange a flat scope
differently:

- **`"layered"`** arranges nodes by their *connectivity* into ELK-style Sugiyama layers with orthogonal
  connectors — the convention for flow-like diagrams (block, state, activity).
- **`"containment"`** arranges nodes by their *reading order*, packing them into balanced rows and
  routing each edge around the packed boxes — suited to peers grouped inside a container.

```csharp
// Pick the layered algorithm explicitly.
graph.Set(CoreOptions.Algorithm, "layered");
var tree = LayoutEngine.Layout(graph);

// Or pack the same graph with the containment algorithm instead.
graph.Set(CoreOptions.Algorithm, "containment");
var packed = LayoutEngine.Layout(graph);
```

The graph is the single place to configure a layout — `LayoutGraph` is itself an `IPropertyHolder`, so
`graph.Set(CoreOptions.Algorithm, "containment")` is all it takes to select an algorithm; there is no
separate options object to keep in sync with it. If you prefer to skip the facade you can still
instantiate an algorithm directly and call `Apply` — `new LayeredLayoutAlgorithm().Apply(graph, new
LayoutOptions())` — but `LayoutEngine` is the recommended
entry point because it also handles nesting.

## Describing nested (hierarchical) graphs

A `LayoutGraph` is recursive: any `LayoutGraphNode` can become a *container* by populating its
`Children` graph, so a diagram can nest groups within groups — a package holding its members, a block
holding its parts. This mirrors the ELK `ElkNode` model, where a node with children is a compound
node. The child subgraph is an ordinary `LayoutGraph`, so it offers the same `AddNode`/`AddEdge` API,
the same insertion order, and the same identifier-uniqueness — scoped to that container. Identifiers
therefore need only be unique *within their own scope* and may be reused across scopes. The `Children`
graph is created lazily, so leaf nodes cost nothing; use `HasChildren` to tell a container from a leaf.

An edge that connects nodes in *different* containers needs no special type: add an ordinary edge to
the container at (or above) the **lowest common ancestor** of its endpoints — often the root graph —
and pass the descendant nodes as its source and target.

```csharp
var graph = new LayoutGraph();

// A container node and a peer leaf node at the root scope.
var group = graph.AddNode("group", width: 200, height: 120);
var outside = graph.AddNode("outside", width: 80, height: 40);

// Nested children live in the container's own identifier scope.
var inner1 = group.Children.AddNode("child1", width: 80, height: 40);
var inner2 = group.Children.AddNode("child2", width: 80, height: 40);
group.Children.AddEdge("inner-edge", inner1, inner2);   // intra-container edge

// A cross-container edge lives at the lowest common ancestor (here the root)
// and references a descendant node inside the container.
graph.AddEdge("cross-edge", outside, inner1);

bool groupIsContainer = group.HasChildren;     // true
bool outsideIsContainer = outside.HasChildren; // false
```

Pass a graph like this to `LayoutEngine.Layout` (or the `HierarchicalLayoutAlgorithm` directly) and the
nesting is laid out for you — each container's children are placed in their own coordinate space and the
container is sized to enclose them (see *Laying out nested diagrams with the hierarchical engine* below).
The flat *leaf* algorithms (`LayeredLayoutAlgorithm`, `ContainmentLayoutAlgorithm`) read only the
top-level nodes and edges, so a flat graph lays out exactly as before and any nesting is carried
harmlessly until a hierarchical engine consumes it.

## Selecting box appearance

A `LayoutGraphNode` also carries the appearance of the box it will be placed as: `Shape` (the
`BoxShape` outline — `Rectangle`, `RoundedRectangle`, `Folder`, or `Note`), `Keyword` (an optional
italicized keyword line shown above the title, e.g. `«part def»`), and `Compartments` (an ordered
list of `LayoutCompartment` feature sections, each with its own title and rows, rendered below the
title with a divider line). This is generic block-diagram notation — useful for any node-and-edge
diagram, not just SysML — and every bundled leaf algorithm plus `HierarchicalLayoutAlgorithm` copy
all three properties, unchanged, onto the placed box, so you select a node's full appearance once
on the input graph:

```csharp
var graph = new LayoutGraph();

// A folder-shaped container node.
var group = graph.AddNode("powertrain", width: 240, height: 220);
group.Label = "Powertrain";
group.Shape = BoxShape.Folder;

// A nested leaf node with a keyword and a compartment of feature rows.
var engine = group.Children.AddNode("engine", width: 160, height: 110);
engine.Label = "Engine";
engine.Keyword = "part def";
engine.Compartments = [new LayoutCompartment("ports", ["intake", "exhaust"])];
```

Only the node's `Width`/`Height` affect layout placement — a caller is responsible for sizing a node
large enough to fit its keyword line and compartment rows (see `BoxMetrics.TitleAreaHeight` and the
per-compartment row heights in `DemaConsulting.Rendering.Abstractions`); no leaf algorithm grows a
box to fit compartment/child content. The one exception is `LayeredLayoutAlgorithm`, which emits a
node's `Width`/`Height` as `max(caller-supplied, computed minimum)`, growing — never shrinking —
past a caller-supplied size only far enough to simultaneously fit the node's own title and its
reserved port-label content insets (`ContentInsetLeft/Right/Top/Bottom`); this floor exists purely
to prevent a title/port-label collision the caller has no visibility into (the insets are themselves
auto-computed from port labels), and does not extend to compartment or child sizing, which remains
entirely caller/child-driven as before. The
[gallery's "Box appearance" diagram](../gallery/README.md) shows a complete, rendered example.

When a container node (one with `Children`) also carries a `Label`, `HierarchicalLayoutAlgorithm`
reserves a title band above its children so the label never overlaps the nested content. By default
that band is a generic fixed height, sized for a plain label with no keyword; if the container also
sets `Keyword`, or is rendered with a theme whose fonts are larger than the default assumes, set
`TitleHeight` explicitly — typically to `BoxMetrics.TitleAreaHeight(theme, hasLabel: true, hasKeyword:
group.Keyword is not null)` — so the reserved band matches what the renderer will actually draw:

```csharp
group.Keyword = "package";
group.TitleHeight = BoxMetrics.TitleAreaHeight(Themes.Light, hasLabel: true, hasKeyword: true);
```

## Configuring layout with options

Options are set with typed keys from `CoreOptions`. Unknown or not-yet-honored options default
harmlessly, so it is always safe to set an option even if the chosen algorithm does not yet act
on it:

```csharp
var options = new LayoutOptions();
options.Set(CoreOptions.Algorithm, "layered");
options.Set(CoreOptions.Direction, LayoutFlowDirection.Down);   // flow the layers top-to-bottom

// Per-element overrides: any graph element is also a property holder.
a.Set(CoreOptions.NodeSpacing, 32.0);

// Routing style rides the same property system, mirroring ELK's elk.edgeRouting.
options.Set(CoreOptions.EdgeRouting, EdgeRouting.Orthogonal);
```

`CoreOptions.Direction` selects the flow direction the layered algorithm arranges its layers along:
`Right` (the default) and `Left` flow the layers left-to-right and right-to-left, while `Down` and
`Up` flow them top-to-bottom and bottom-to-top — the convention for action flows and state machines. A
downward flow swaps each node's width and height before layering so layer spacing follows node height.
As with the algorithm, a declaration on the graph takes precedence over one on the options.

## Parallel edges and named ports

A `LayoutGraphNode` can carry a set of `LayoutGraphPort`s — named attachment points on the node's
boundary, each implementing `ILayoutConnectable` alongside the node itself, so an edge can target a
specific port instead of the node as a whole. This lets several connectors land on different, clearly
labelled locations of the same box rather than all converging on one anchor:

```csharp
var graph = new LayoutGraph();

var upstream = graph.AddNode("upstream", width: 120, height: 60);
var hub = graph.AddNode("hub", width: 120, height: 60);

var inPort = hub.Ports.AddPort("in");
inPort.ExternalLabel = "a rather long incoming data label";

graph.AddEdge("upstream-hub", upstream, inPort);
```

A port's rendered side (left, right, top, or bottom) is derived from how the algorithm routes its
connector, not from a settable `Side` property on `LayoutGraphPort` itself.

Two or more edges between the same pair of boxes are parallel edges. By default they collapse to a
single rendered connector and suppress the collapsed edges' own labels; set `MergeParallelEdges` to
`false` to keep every parallel edge as its own independently-routed, independently-labelled connector:

```csharp
graph.Set(CoreOptions.MergeParallelEdges, false);
```

Each node's `ContentInsetLeft`/`ContentInsetRight` margins (reserved so port labels never overlap
the box's own content) are auto-computed by a built-in, dependency-free heuristic driven by
`CoreOptions.AssumedFontSize` — no configuration is required. The [gallery's "Parallel edges and
named ports" diagrams](../gallery/README.md) show complete, rendered examples of preserved vs.
merged parallel edges and horizontal vs. vertical port placement.

## Option cascading

Every well-known option cascades: it can be set at the free-standing `LayoutOptions`, at a
`LayoutGraph` (a node's or the whole graph's own scope), or at a container node's `Children` graph, and
each scope's own explicit value wins over its parent's, falling back to the option's declared default
only when no scope in the chain sets it. This is nearest-ancestor-wins resolution, not first-set-wins:
a deeper, more specific override always takes precedence over one set higher in the tree.

```csharp
var options = new LayoutOptions();
options.Set(CoreOptions.Direction, LayoutFlowDirection.Right);   // root default: flow rightward

var graph = new LayoutGraph();
var pipeline = graph.AddNode("pipeline", 10, 10);

// A container's own children graph may override an option for everything nested inside it...
pipeline.Children.Set(CoreOptions.Direction, LayoutFlowDirection.Down);
var validate = pipeline.Children.AddNode("validate", 80, 40);
var nested = pipeline.Children.AddNode("nested", 10, 10);   // sets nothing: inherits Down from pipeline

// ...and a deeper scope's own override still wins over an inherited ancestor value.
nested.Children.Set(CoreOptions.Direction, LayoutFlowDirection.Right);
```

Here `validate`'s siblings inherit `pipeline.Children`'s `Down` override (the nearest ancestor that set
one), while `nested`'s own children flow `Right` again, because `nested.Children`'s own explicit
override is nearer than `pipeline.Children`'s. `HierarchicalLayoutAlgorithm` builds this per-scope
resolution automatically for every nested container using `PropertyHolder.OverlayOnto`; algorithms
invoked directly on a single flat graph resolve the same way against whatever `LayoutOptions` they are
given.

## Routing connectors among placed boxes

When you have already positioned some boxes yourself — for example a free-form or containment layout
produced outside the layered algorithm — `ConnectorRouter` draws the connectors between them. Each
`Connection` names a source and target `LayoutBox`; the router picks anchors on the faces the two
boxes present to each other, steers around every other box, and returns one routed `LayoutLine` per
connection carrying your requested arrowhead, line style, and label. The routing style follows the
same `EdgeRouting` vocabulary as `CoreOptions.EdgeRouting` (today `Orthogonal`):

```csharp
// Three already-placed boxes; the connector from A to B must avoid M.
var a = new LayoutBox(0, 0, 80, 40, "A", 0, BoxShape.Rectangle, [], []);
var m = new LayoutBox(140, -10, 60, 80, "M", 0, BoxShape.Rectangle, [], []);
var b = new LayoutBox(260, 0, 80, 40, "B", 0, BoxShape.Rectangle, [], []);
var boxes = new[] { a, m, b };

var connections = new[]
{
    new Connection(a, b, EndMarkerStyle.FilledArrow),
    new Connection(a, m, EndMarkerStyle.HollowDiamond, LineStyle.Dashed, "owns"),
};

// Default options route orthogonally with a 12px clearance; override Clearance to widen the gap.
var lines = ConnectorRouter.Route(boxes, connections, new ConnectorRouteOptions());

// Drop the placed boxes and routed connectors into a LayoutTree for any renderer.
var nodes = new List<LayoutNode>();
nodes.AddRange(boxes);
nodes.AddRange(lines);
var tree = new LayoutTree(360, 80, nodes);
```

For folder-shaped boxes, `FolderTabWidth` and `FolderTabHeight` may be supplied together or
individually on each `LayoutBox`. If one value is omitted, `ConnectorRouter` falls back the
missing dimension before choosing anchors so routes still avoid the tab strip and land on the
recessed body top that the renderers draw.

## Packing boxes into a container

When you want to arrange peer boxes inside a container — the members of a package, the contents of a
folder — and their reading order rather than their connectivity drives the layout, `ContainmentLayout`
packs them into rows within a width budget. It returns the same boxes repositioned to their packed
coordinates (relative to the region origin) plus the size of the container that encloses them, so you
can size the parent box to fit. Only each child's width and height affect placement; every other field
is carried through unchanged:

```csharp
// Three already-sized boxes to arrange inside a container.
var boxes = new[]
{
    new LayoutBox(0, 0, 80, 40, "A", 0, BoxShape.Rectangle, [], []),
    new LayoutBox(0, 0, 120, 40, "B", 0, BoxShape.Rectangle, [], []),
    new LayoutBox(0, 0, 60, 40, "C", 0, BoxShape.Rectangle, [], []),
};

// Pack into rows no wider than 200px of content; gaps and padding use sensible defaults.
var result = ContainmentLayout.Pack(boxes, new ContainmentOptions(MaxContentWidth: 200));

// Wrap the packed children in a container sized to the returned region.
var container = new LayoutBox(
    0, 0, result.Width, result.Height, "Group", 0, BoxShape.Folder, [], result.Children);
```

## Laying out a graph with the containment algorithm

When your diagram's elements group as peers whose reading order — not their connectivity — drives the
layout, select the bundled `ContainmentLayoutAlgorithm` (id `"containment"`) instead of the layered
algorithm. It packs the graph's top-level nodes into rows within a balanced content width, then routes
each edge around the packed boxes with the selected `EdgeRouting` style. Like the layered algorithm it
is deterministic and order-preserving, produces one placed box per node and one routed connector per
edge, and is selected the same way — so it is a drop-in alternative that changes no other code:

```csharp
// Describe the diagram as a graph of peer boxes joined by a few edges.
var graph = new LayoutGraph();
var a = graph.AddNode("a", width: 80, height: 40);
var b = graph.AddNode("b", width: 80, height: 40);
var c = graph.AddNode("c", width: 80, height: 40);
a.Label = "A";
b.Label = "B";
c.Label = "C";
var ab = graph.AddEdge("a-b", a, b);
ab.TargetEnd = EndMarkerStyle.FilledArrow;
graph.AddEdge("a-c", a, c);

// Pack the nodes into rows and route the edges around them.
graph.Set(CoreOptions.Algorithm, "containment");
var tree = LayoutEngine.Layout(graph);

// Render the placed tree with any renderer.
var renderer = new SvgRenderer();
using var output = File.Create("containment.svg");
renderer.Render(tree, new RenderOptions(Themes.Light), output);
```

## Laying out nested diagrams with the hierarchical engine

When your diagram nests groups within groups — a package holding its members, a block holding its
parts — select the bundled `HierarchicalLayoutAlgorithm` (id `"hierarchical"`) — which is also what
`LayoutEngine` uses by default. It is the recursive layout engine: it lays out each container's
`Children` in their own coordinate space, sizes the container to enclose them, and composes every
sub-layout into one placed tree. It does not place boxes itself; it delegates each scope to a *leaf*
algorithm chosen with `CoreOptions.Algorithm` — the root inherits the graph's own declared algorithm
(default `"layered"`), and any container can override it. A graph with no containers is laid out exactly
as the selected leaf algorithm would lay it out, so the engine is a safe drop-in.

```csharp
// A container laid out "layered", packed by a "containment" root, plus a sibling leaf.
var graph = new LayoutGraph();
var group = graph.AddNode("group", width: 10, height: 10);
group.Label = "Group";
group.Set(CoreOptions.Algorithm, "layered");            // this container lays its children out layered
var c1 = group.Children.AddNode("c1", width: 80, height: 40);
var c2 = group.Children.AddNode("c2", width: 80, height: 40);
group.Children.AddEdge("c1-c2", c1, c2);                // intra-container edge
graph.AddNode("outside", width: 80, height: 40);        // a sibling leaf at the root
graph.Set(CoreOptions.Algorithm, "containment");        // the root packs with the containment algorithm

// Pack the root with the containment algorithm; the container recurses with its own algorithm.
var tree = LayoutEngine.Layout(graph);

// Render the composed tree with any renderer.
var renderer = new SvgRenderer();
using var output = File.Create("nested.svg");
renderer.Render(tree, new RenderOptions(Themes.Light), output);
```

## Selecting algorithms and renderers by registry

For applications that choose an algorithm or output format at run time, register the
implementations once and resolve them by id, media type, or output file extension. The bundled
algorithms already have a factory — `LayoutAlgorithms.CreateDefaultRegistry()` returns a registry with
`layered`, `containment`, and `hierarchical` pre-registered — so you only assemble a registry by hand
when you want to add custom algorithms:

```csharp
// Shortcut: a registry with the three bundled algorithms already registered.
var algorithms = LayoutAlgorithms.CreateDefaultRegistry();
algorithms.Register(new MyTreeLayoutAlgorithm()); // extend it with your own

var renderers = new RendererRegistry();
renderers.Register(new SvgRenderer());
renderers.Register(new PngRenderer());
renderers.Register(new JpegRenderer());
renderers.Register(new WebpRenderer());

// Resolve and apply an algorithm by id (or hand the registry to LayoutEngine.Layout).
var algorithm = algorithms.Resolve(options.Get(CoreOptions.Algorithm)); // e.g. "layered"
var tree = algorithm.Apply(graph, options);

// Resolve the renderer directly from the desired output filename's extension.
var outputPath = "diagram.webp";
var renderer = renderers.ResolveByExtension(Path.GetExtension(outputPath));
using var output = File.Create(outputPath);
renderer.Render(tree, new RenderOptions(Themes.Light), output);
```

`LayoutEngine.Layout(graph, registry)` accepts a custom registry directly, so you can keep the
one-call convenience while resolving against your own set of algorithms.

# Gallery

A browsable gallery (the `README.md` document under `docs/gallery/`) showcases what the library can
produce: a layered diagram, a containment-packed diagram, a hierarchical nested diagram with a
cross-container edge, orthogonal edge routing around an obstacle, the three built-in themes on one
diagram, and both the SVG and PNG output paths. Every image is generated by the
`DemaConsulting.Rendering.Gallery` test project directly from the public API, so the gallery doubles
as an end-to-end rendering smoke test that runs on every build.

To regenerate the committed gallery under `docs/gallery/` — the images and the `README.md` index —
run the following from the repository root:

```pwsh
pwsh ./gallery.ps1
```

The script points the `RENDERING_GALLERY_DIR` environment variable at `docs/gallery/` and runs only
the gallery project. On an ordinary build the same facts render to a throwaway directory and simply
assert that each image is valid, so the gallery never dirties the repository during normal testing.

# Extending the Library

The library is open for extension without modifying existing code:

- **New layout algorithms** (for example tree, force, or packing layouts) implement
  `ILayoutAlgorithm` and are registered under a new id. Consumers select them via
  `CoreOptions.Algorithm`.
- **New output formats** implement `IRenderer` with a distinct `MediaType` and file extensions and
  register alongside the bundled SVG, PNG, JPEG, and WEBP renderers.
- **New configuration options** are introduced by declaring additional `LayoutProperty<T>` keys.
  Because options travel in an open property bag, adding a key never changes an existing method
  signature.

# References

- [REF-1] Continuous Compliance Methodology (<https://github.com/demaconsulting/ContinuousCompliance>)
- [REF-2] Eclipse Layout Kernel (<https://eclipse.dev/elk/>)
