# Rendering

[![GitHub forks][badge-forks]][link-forks]
[![GitHub stars][badge-stars]][link-stars]
[![GitHub contributors][badge-contributors]][link-contributors]
[![License][badge-license]][link-license]
[![Build][badge-build]][link-build]
[![Quality Gate][badge-quality]][link-quality]
[![Security][badge-security]][link-security]
[![NuGet][badge-nuget]][link-nuget]

General-purpose diagram layout and rendering for .NET. Describe a diagram as a graph, lay it out
with a pluggable algorithm, and render it to SVG, PNG, JPEG, or WEBP. The design is inspired by the
[Eclipse Layout Kernel (ELK)][link-elk]: layout and rendering are configured through an open,
extensible property system, and new algorithms, renderers, and options are added additively.

## Packages

The library is split into focused packages so consumers take only what they need:

| Package | Purpose |
| --- | --- |
| `DemaConsulting.Rendering` | Layout model: the `LayoutTree` IR, the property system, and the input `LayoutGraph` |
| `DemaConsulting.Rendering.Abstractions` | SPI: `ILayoutAlgorithm`/`IRenderer`, registries, `Theme`, notation metrics |
| `DemaConsulting.Rendering.Layout` | Layout algorithms: the layered pipeline and `LayeredLayoutAlgorithm` |
| `DemaConsulting.Rendering.Svg` | SVG renderer with zero external dependencies |
| `DemaConsulting.Rendering.Skia` | SkiaSharp raster renderers (PNG, JPEG, WEBP) with an embedded Noto Sans font |

Package dependencies form an acyclic graph: `Abstractions` and `Layout` depend on the model;
`Svg` and `Skia` depend on the model and `Abstractions`; the model depends on nothing.

## Installation

```bash
dotnet add package DemaConsulting.Rendering.Layout
dotnet add package DemaConsulting.Rendering.Svg
```

## Usage

```csharp
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout;
using DemaConsulting.Rendering.Svg;

// Describe the diagram as a graph of sized boxes and directed edges.
var graph = new LayoutGraph();
var a = graph.AddNode("a", width: 80, height: 40);
var b = graph.AddNode("b", width: 80, height: 40);
graph.AddEdge("a-b", a, b);

// Lay it out, then render the placed tree to SVG.
var tree = new LayeredLayoutAlgorithm().Apply(graph, new LayoutOptions());
using var output = File.Create("diagram.svg");
new SvgRenderer().Render(tree, new RenderOptions(Themes.Light), output);
```

See the [user guide][link-user-guide] for configuration options and extension points.

## Extensibility

- **New layout algorithms** implement `ILayoutAlgorithm` and register under a new id.
- **New output formats** implement `IRenderer` with a distinct media type.
- **New configuration options** are declared as typed `LayoutProperty<T>` keys carried in an open
  property bag, so adding an option never changes an existing method signature.

## Documentation

Generated documentation includes build notes, a user guide, a code quality report, requirements,
requirement justifications, and a requirements-to-test trace matrix.

## Contributing

Contributions are welcome. See [CONTRIBUTING.md][link-contributing] for development setup, coding
standards, and the pull request process.

## License

Copyright (c) DEMA Consulting. Licensed under the MIT License. See [LICENSE][link-license] for details.

By contributing to this project, you agree that your contributions will be licensed under the MIT License.

<!-- Badge References -->
[badge-forks]: https://img.shields.io/github/forks/demaconsulting/Rendering?style=plastic
[badge-stars]: https://img.shields.io/github/stars/demaconsulting/Rendering?style=plastic
[badge-contributors]: https://img.shields.io/github/contributors/demaconsulting/Rendering?style=plastic
[badge-license]: https://img.shields.io/github/license/demaconsulting/Rendering?style=plastic
[badge-build]: https://img.shields.io/github/actions/workflow/status/demaconsulting/Rendering/build_on_push.yaml?style=plastic
[badge-quality]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_Rendering&metric=alert_status
[badge-security]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_Rendering&metric=security_rating
[badge-nuget]: https://img.shields.io/nuget/v/DemaConsulting.Rendering?style=plastic

<!-- Link References -->
[link-forks]: https://github.com/demaconsulting/Rendering/network/members
[link-stars]: https://github.com/demaconsulting/Rendering/stargazers
[link-contributors]: https://github.com/demaconsulting/Rendering/graphs/contributors
[link-license]: https://github.com/demaconsulting/Rendering/blob/main/LICENSE
[link-build]: https://github.com/demaconsulting/Rendering/actions/workflows/build_on_push.yaml
[link-quality]: https://sonarcloud.io/dashboard?id=demaconsulting_Rendering
[link-security]: https://sonarcloud.io/dashboard?id=demaconsulting_Rendering
[link-nuget]: https://www.nuget.org/packages/DemaConsulting.Rendering
[link-elk]: https://eclipse.dev/elk/
[link-user-guide]: https://github.com/demaconsulting/Rendering/blob/main/docs/user_guide/introduction.md
[link-contributing]: https://github.com/demaconsulting/Rendering/blob/main/CONTRIBUTING.md
