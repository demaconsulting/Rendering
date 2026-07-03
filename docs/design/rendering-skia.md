# Rendering.Skia Design

## Architecture

The `DemaConsulting.Rendering.Skia` system renders a placed `LayoutTree` to raster images using
SkiaSharp. A shared `SkiaRasterRenderer` base performs all drawing; thin concrete renderers select the
encoded output format. The system draws a diagram once onto a SkiaSharp bitmap and then encodes it in a
concrete image format.

The system is composed of one shared base unit and three concrete format units:

```text
Rendering.Skia (System)
├── SkiaRasterRenderer (Unit)  — abstract base: rasterizes a LayoutTree to a bitmap and encodes it
├── PngRenderer (Unit)         — lossless PNG output
├── JpegRenderer (Unit)        — lossy JPEG output
└── WebpRenderer (Unit)        — WEBP output
```

- **SkiaRasterRenderer** — the shared SkiaSharp rasterizer that allocates the bitmap, initializes it
  with the render theme's background color, draws layout-tree nodes (boxes, labels, connectors with end
  markers, ports, badges, bands, lifelines, activations, and grids), and encodes the bitmap using the
  derived renderer's format. Detailed in SkiaRasterRenderer Unit Design.
- **PngRenderer** — the concrete renderer that emits lossless PNG output. Detailed in PngRenderer Unit
  Design.
- **JpegRenderer** — the concrete renderer that emits lossy JPEG output. Detailed in JpegRenderer Unit
  Design.
- **WebpRenderer** — the concrete renderer that emits WEBP output. Detailed in WebpRenderer Unit
  Design.

All drawing logic lives in the abstract `SkiaRasterRenderer`; each concrete renderer supplies only the
SkiaSharp encoded-image format, encoding quality, media type, and file extensions. This keeps the
multi-format surface a few lines per format while guaranteeing that PNG, JPEG, and WEBP output share
the same raster drawing path apart from the final encode step. The three concrete renderers do not
interact with each other; each is a leaf that inherits its drawing behavior from `SkiaRasterRenderer`.

## External Interfaces

- **`PngRenderer` / `JpegRenderer` / `WebpRenderer` (`: IRenderer`)** — inbound; each realizes the
  `IRenderer` contract, accepting a `LayoutTree`, `RenderOptions`, and output `Stream`, and advertising
  its media type (`image/png`, `image/jpeg`, `image/webp`) and file extension for registry resolution.
- **Output stream** — outbound; encoded raster image bytes written to the caller-owned `Stream`.

`SkiaRasterRenderer` is an internal abstract base, not a directly instantiated public entry point.

## Dependencies

The system references the *Rendering Model* package (`DemaConsulting.Rendering`) for `LayoutTree` and
node records, and the *Rendering Abstractions* package (`DemaConsulting.Rendering.Abstractions`) for the
`IRenderer` contract, `RenderOptions`, `Theme`, `NotationMetrics`, `BoxMetrics`, and
`ConnectorLabelPlacer`.

Unlike the other Rendering systems, this system carries a genuine third-party runtime dependency:

- **SkiaSharp** — the 2D graphics library (MIT license) that provides the bitmap, canvas, and image
  encoders used for all raster drawing and encoding. The matching platform-specific SkiaSharp native
  asset packages supply the native Skia binaries for Windows, Linux, and macOS. (SkiaSharp is not among
  the repository's documented OTS build tools, so no OTS integration design document cross-reference is
  available for it.)

The system also embeds a Noto Sans font (SIL Open Font License 1.1) as a resource so text is drawn from
a bundled font rather than an installed system font. The build-time-only NuGet references (SBOM,
SourceLink, API documentation, and `Polyfill`) are private assets and not part of the runtime surface.

## Risk Control Measures

N/A - general-purpose rendering libraries carry no safety-related risk controls requiring
architectural segregation (IEC 62304 §5.3.3).

## Data Flow

```text
LayoutTree + RenderOptions (Theme)
        │
        ▼  concrete renderer (Png / Jpeg / Webp)
   SkiaRasterRenderer: allocate bitmap → fill background → draw nodes → encode
        │
        ▼
   encoded PNG / JPEG / WEBP bytes ──► caller Stream
```

A caller passes a placed `LayoutTree` and `RenderOptions` to a concrete renderer. The shared
`SkiaRasterRenderer` base draws the diagram onto a SkiaSharp bitmap using the embedded font and the
shared geometry helpers, then the concrete renderer encodes the bitmap in its format and writes the
bytes to the caller-owned stream.

## Design Constraints

- **Target frameworks**: `net8.0`, `net9.0`, and `net10.0`.
- **SkiaSharp runtime requirement**: raster output requires the SkiaSharp library and its
  platform-specific native assets, so this system cannot run in an environment where those native
  binaries are unavailable.
- **Byte-reproducible output**: an embedded Noto Sans font is used for all text so raster output is
  byte-reproducible across platforms regardless of installed fonts.
- **Shared drawing path**: all formats share the single `SkiaRasterRenderer` drawing path and differ
  only in the final encode step, so PNG, JPEG, and WEBP output stay visually consistent.
