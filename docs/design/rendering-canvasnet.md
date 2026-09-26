# Rendering.CanvasNet Design

## Architecture

The `DemaConsulting.Rendering.CanvasNet` system renders a placed `LayoutTree` to raster images
using DemaConsulting.CanvasNet. A shared `CanvasNetRasterRenderer` base performs all drawing on a
managed `Surface`; thin concrete renderers select the encoded output format. The public
`PngRenderer` and `JpegRenderer` wrappers each delegate to a singleton private core subclass of the
shared base, so the diagram is drawn once and then encoded as either PNG or JPEG.

The system is composed of one shared base unit and two concrete format units:

```text
Rendering.CanvasNet (System)
├── CanvasNetRasterRenderer (Unit)  — abstract CanvasNet rasterizer shared by all formats
├── PngRenderer (Unit)              — lossless PNG output
└── JpegRenderer (Unit)             — JPEG output
```

- **CanvasNetRasterRenderer** — the shared CanvasNet rasterizer that allocates the `Surface`,
  initializes it with the render theme's background color, draws layout-tree nodes (boxes, labels,
  connectors with end markers, ports, badges, bands, lifelines, activations, and grids), and lets
  the derived core encode the surface. Detailed in CanvasNetRasterRenderer Unit Design.
- **PngRenderer** — the concrete renderer that emits lossless PNG output. The public wrapper
  delegates to a private `PngRendererCore` subclass of `CanvasNetRasterRenderer`. Detailed in
  PngRenderer Unit Design.
- **JpegRenderer** — the concrete renderer that emits lossy JPEG output. The public wrapper
  delegates to a private `JpegRendererCore` subclass of `CanvasNetRasterRenderer`. Detailed in
  JpegRenderer Unit Design.

All drawing logic lives in the abstract `CanvasNetRasterRenderer`; each concrete renderer supplies
only format metadata and the final save step (`PngCodec.Save` or `JpegCodec.Save`). This keeps the
multi-format surface a few lines per format while guaranteeing that PNG and JPEG output share the
same raster drawing path apart from the final encode step. The two concrete renderers do not
interact with each other; each is a leaf that inherits its drawing behavior from
`CanvasNetRasterRenderer` through its private core subclass.

## External Interfaces

- **`PngRenderer` / `JpegRenderer` (`: IRenderer`)** — inbound; each realizes the `IRenderer`
  contract, accepting a `LayoutTree`, `RenderOptions`, and output `Stream`, and advertising its
  media type (`image/png`, `image/jpeg`) and file extension for registry resolution.
- **Output stream** — outbound; encoded raster image bytes written to the caller-owned `Stream`.

`CanvasNetRasterRenderer` is an internal abstract base, not a directly instantiated public entry
point.

## Dependencies

The system references the *Rendering Model* package (`DemaConsulting.Rendering`) for `LayoutTree`
and node records, and the *Rendering Abstractions* package
(`DemaConsulting.Rendering.Abstractions`) for the `IRenderer` contract, `RenderOptions`, `Theme`,
`NotationMetrics`, `BoxMetrics`, and `ConnectorLabelPlacer`.

Unlike the build-time and documentation OTS items, this system carries a runtime dependency:

- **CanvasNet** — the pure managed raster-graphics library (MIT license) that provides the
  `Surface`, drawing canvas, geometry paths, text renderer, typeface loader, and PNG/JPEG codecs
  used for raster drawing and encoding. CanvasNet has no P/Invoke layer, no native binary
  dependency, and no platform-specific initialization. It is documented as an OTS software item —
  see *CanvasNet Integration Design* under `docs/design/ots/` for its integration pattern.

The system also embeds a Noto Sans font (SIL Open Font License 1.1) as a resource so text is drawn
from a bundled font rather than an installed system font. The build-time-only NuGet references
(SBOM, SourceLink, API documentation, and `Polyfill`) are private assets and not part of the
runtime surface.

## Risk Control Measures

N/A - general-purpose rendering libraries carry no safety-related risk controls requiring
architectural segregation (IEC 62304 §5.3.3).

## Data Flow

```text
LayoutTree + RenderOptions (Theme)
        │
        ▼  concrete renderer (Png / Jpeg)
   CanvasNetRasterRenderer: allocate Surface → fill background → draw nodes → encode
        │
        ▼
   encoded PNG / JPEG bytes ──► caller Stream
```

A caller passes a placed `LayoutTree` and `RenderOptions` to a concrete renderer. The shared
`CanvasNetRasterRenderer` base draws the diagram onto a managed `Surface` using the embedded font
and the shared geometry helpers, then the concrete renderer encodes the surface in its format and
writes the bytes to the caller-owned stream.

## Design Constraints

- **Target frameworks**: `net8.0`, `net9.0`, and `net10.0`. Unlike the model, abstractions,
  layout, and SVG systems this one does not offer a `netstandard2.0` target: the CanvasNet package
  does not ship `netstandard2.0` assets.
- **Pure managed runtime**: raster output shall not require native asset packages, platform-specific
  loader hooks, or P/Invoke-based initialization.
- **Byte-reproducible output**: an embedded Noto Sans font is used for all text so raster output is
  byte-reproducible across platforms regardless of installed fonts.
- **Shared drawing path**: all formats share the single `CanvasNetRasterRenderer` drawing path and
  differ only in the final encode step, so PNG and JPEG output stay visually consistent.
