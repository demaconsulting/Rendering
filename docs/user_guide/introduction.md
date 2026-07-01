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
| `DemaConsulting.Rendering.Layout` | To run the bundled `layered` layout algorithm |
| `DemaConsulting.Rendering.Svg` | To render diagrams to SVG (no native dependencies) |
| `DemaConsulting.Rendering.Skia` | To render diagrams to PNG, JPEG, or WEBP (uses SkiaSharp) |

```bash
dotnet add package DemaConsulting.Rendering.Layout
dotnet add package DemaConsulting.Rendering.Svg
```

Installing a renderer or the layout package transitively brings in the model and abstractions.

# Core Concepts

- **`LayoutGraph`** — the *unplaced* input: sized `LayoutGraphNode` boxes and directed
  `LayoutGraphEdge` connections.
- **`LayoutOptions`** — an open, property-keyed configuration bag. Options are declared as typed
  `LayoutProperty<T>` keys (see `CoreOptions`) and can be attached to the whole graph, a single
  element, or a free-standing options object.
- **`ILayoutAlgorithm`** — consumes a `LayoutGraph` plus `LayoutOptions` and produces a placed
  `LayoutTree`. The bundled implementation is `LayeredLayoutAlgorithm` (id `"layered"`).
- **`LayoutTree`** — the *placed* result: boxes with absolute coordinates and orthogonally routed
  connectors.
- **`IRenderer`** — draws a `LayoutTree` to a stream. Bundled implementations are `SvgRenderer`
  and the SkiaSharp `PngRenderer`, `JpegRenderer`, and `WebpRenderer`.

# Usage

## Laying out and rendering a graph

```csharp
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

// 2. Lay it out with the bundled layered algorithm.
var options = new LayoutOptions();
var tree = new LayeredLayoutAlgorithm().Apply(graph, options);

// 3. Render the placed tree to SVG.
var renderer = new SvgRenderer();
using var output = File.Create("diagram.svg");
renderer.Render(tree, new RenderOptions(Themes.Light), output);
```

Swap `SvgRenderer` for `PngRenderer`, `JpegRenderer`, or `WebpRenderer` (from
`DemaConsulting.Rendering.Skia`) to produce a raster image instead; the rest of the flow is
identical.

## Configuring layout with options

Options are set with typed keys from `CoreOptions`. Unknown or not-yet-honored options default
harmlessly, so it is always safe to set an option even if the chosen algorithm does not yet act
on it:

```csharp
var options = new LayoutOptions();
options.Set(CoreOptions.Algorithm, "layered");
options.Set(CoreOptions.Direction, LayoutFlowDirection.Right);

// Per-element overrides: any graph element is also a property holder.
a.Set(CoreOptions.NodeSpacing, 32.0);
```

## Selecting algorithms and renderers by registry

For applications that choose an algorithm or output format at run time, register the
implementations once and resolve them by id, media type, or output file extension:

```csharp
var algorithms = new LayoutAlgorithmRegistry();
algorithms.Register(new LayeredLayoutAlgorithm());

var renderers = new RendererRegistry();
renderers.Register(new SvgRenderer());
renderers.Register(new PngRenderer());
renderers.Register(new JpegRenderer());
renderers.Register(new WebpRenderer());

var algorithm = algorithms.Resolve(options.Get(CoreOptions.Algorithm)); // "layered"
var tree = algorithm.Apply(graph, options);

// Resolve the renderer directly from the desired output filename's extension.
var outputPath = "diagram.webp";
var renderer = renderers.ResolveByExtension(Path.GetExtension(outputPath));
using var output = File.Create(outputPath);
renderer.Render(tree, new RenderOptions(Themes.Light), output);
```

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
