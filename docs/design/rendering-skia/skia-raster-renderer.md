## SkiaRasterRenderer Unit Design

Part of the Rendering.Skia system.

### SkiaRasterRenderer Purpose

`SkiaRasterRenderer` has a single responsibility: rasterize a placed `LayoutTree` onto a SkiaSharp
bitmap and encode that bitmap to a caller-supplied `Stream` in whatever image format the derived
renderer selects. It centralizes every drawing decision (background fill, node drawing, connector
end-markers, label backplates, typography) so that PNG, JPEG, and WEBP output share identical
pixel-level behaviour and differ only in the final encode step.

### SkiaRasterRenderer Overview

`SkiaRasterRenderer` is the abstract `IRenderer` implementation that provides the common SkiaSharp
rasterization path for every raster format. Concrete renderers supply the encoded image format, quality,
media type, and file extensions; the base class owns argument validation, bitmap allocation, canvas
initialization, node drawing, connector-label finalization, and stream encoding. The internal
`SkiaTypefaces` helper resolves the shared, lazily-loaded embedded Noto Sans typeface instances that
every drawing call site in this unit measures and draws against.

### SkiaRasterRenderer Data Model

| Member | Type | Description |
| --- | --- | --- |
| `EncodedFormat` | `abstract SKEncodedImageFormat` | The SkiaSharp encode format supplied by the concrete renderer. |
| `EncodingQuality` | `virtual int` (default 100) | Quality passed to the encoder; used by lossy formats. |
| `MediaType` | `abstract string` | The MIME media type reported to callers and registries. |
| `DefaultExtension` | `abstract string` | The primary output file extension. |
| `FileExtensions` | `abstract IReadOnlyList<string>` | Every file extension the concrete renderer produces. |

### SkiaRasterRenderer Methods

- **`Render(LayoutTree, RenderOptions, Stream)`** - validates its arguments, resolves every connector
  label's placement (position and estimated half-width/half-height) via `ConnectorLabelPlacer.Place`
  *before* allocating the bitmap, then allocates an `SKBitmap` sized from `LayoutTree.Width`/
  `LayoutTree.Height` scaled by `RenderOptions.Scale` and grown further to include the full
  bounding-box extent of every placed connector label (minimum one by one pixel either way), clears
  the bitmap to `RenderOptions.Theme.BackgroundColor`, draws every node, draws connector labels in a
  final pass at the positions already computed, and writes the encoded bytes to the stream without
  closing or flushing it. Sizing the bitmap only after label placement is known prevents a label
  nudged during collision avoidance from landing outside the bitmap and being invisibly clipped.
- **Node drawing helpers** - draw boxes, labels, lines, ports, badges, bands, lifelines, activations,
  and grids using the render theme, the embedded Noto Sans typefaces, shared notation metrics, and
  shared box metrics. Port drawing mirrors `SvgRenderer`'s inward-reading placement exactly: each
  port's label is drawn immediately next to its glyph, reading rightward for a left-side port,
  leftward for a right-side port, below for a top-side port, and above for a bottom-side port, with
  its rendered width bounded to `LayoutPort.MaxLabelWidth` via the same font-size-shrink squeeze
  mechanism `RenderBoxTitle` uses, so an excessively long port label compresses instead of visually
  overlapping the opposite port's label region. The port glyph square is filled with
  `Theme.StrokeColor` and outlined with a second stroke-only `SKPaint` pass in
  `Theme.BackgroundColor` (1.0 logical px, pre-scale) so the glyph remains visually distinct from a
  solid-filled connector arrowhead marker that may land on/near the same box edge. Box title and
  compartment content start at `box.X + Theme.LabelPadding + box.ContentInsetLeft` (and reduce
  available width by `box.ContentInsetRight`) instead of the fixed pre-port offset, so a non-zero
  reserved port-label margin never overlaps rendered content; the title's horizontal center,
  however, is always the box's full geometric center (`box.X + box.Width / 2.0`), regardless of any
  asymmetric `ContentInsetLeft`/`ContentInsetRight` — the title occupies its own row above the
  title area, while left/right port labels are drawn at the box's vertical center (a different
  row), so the title never needs to dodge sideways around a side-port label's inset.
- **Connector helpers** - build connector end markers from `NotationMetrics` so raster markers match
  the SVG renderer. Hollow marker interiors and midpoint-label backplates use `Theme.BackgroundColor`
  so they occlude connector strokes with the same background used for the bitmap.

### SkiaRasterRenderer Design Constraints

- The rasterizer shall enforce a minimum bitmap size of one by one pixels before allocating the
  `SKBitmap`.
- The rasterizer shall use the render theme as the single source of truth for the canvas background,
  hollow-marker occlusion fill, and midpoint-label backplate fill.
- Box and grid fills shall be selected from `Theme.DepthFillColors` by layout depth.
- Connector end-marker geometry shall derive from `NotationMetrics`, matching the SVG renderer's
  marker dimensions.
- The output stream remains owned by the caller; the renderer writes encoded bytes but does not close
  or flush the stream.

### SkiaRasterRenderer Interactions

`SkiaRasterRenderer` consumes model nodes from the Rendering system and `RenderOptions`, `Theme`,
`NotationMetrics`, `BoxMetrics`, and `ConnectorLabelPlacer` from the Rendering.Abstractions system. It
is inherited by `PngRenderer`, `JpegRenderer`, and `WebpRenderer`, which provide only format-selection
members.

### SkiaRasterRenderer Error Handling

- **Null arguments** — `Render(layout, options, output)` calls `ArgumentNullException.ThrowIfNull`
  on each of its three arguments before doing any work, so a null `LayoutTree`, `RenderOptions`, or
  output `Stream` results in a fast `ArgumentNullException` rather than a later `NullReferenceException`
  or a partially written stream.
- **Degenerate layout sizes** — non-positive layout widths or heights are clamped to a minimum of
  one by one pixel before `SKBitmap` allocation so SkiaSharp does not raise an allocation error and
  callers always receive a valid encoded image (including for an empty tree).
- **SkiaSharp exceptions** — errors surfaced by the underlying SkiaSharp APIs (for example bitmap
  allocation failure, encoder errors) are not caught. They propagate unchanged to the caller so
  they are visible in test output and diagnostics; the renderer records no logs of its own.
- **Output stream errors** — exceptions from `SKData.SaveTo(output)` (for example a disposed or
  read-only stream) propagate to the caller. The renderer never closes, flushes, or otherwise
  mutates the caller-owned `Stream`, so failures leave the stream in whatever state SkiaSharp's
  partial write produced; disposal remains the caller's responsibility.
- **Resource cleanup on failure** — every SkiaSharp object (`SKBitmap`, `SKCanvas`, `SKImage`,
  `SKData`, `SKPaint`, `SKPath`, `SKPathEffect`) is created with a `using` declaration so its
  unmanaged handle is released even when an exception is thrown mid-render.

### SkiaRasterRenderer Dependencies

- **`DemaConsulting.Rendering`** — consumes `LayoutTree`, `LayoutNode`, `LayoutBox`, `LayoutLine`,
  `LayoutLabel`, `LayoutPort`, and the other layout-node records.
- **`DemaConsulting.Rendering.Abstractions`** — consumes the `IRenderer` contract it implements as
  well as `RenderOptions`, `Theme`, `NotationMetrics`, `BoxMetrics`, and `ConnectorLabelPlacer`
  for drawing geometry and typography.
- **SkiaSharp (OTS)** — uses `SKBitmap`, `SKCanvas`, `SKImage`, `SKData`, `SKPaint`, `SKPath`,
  `SKPathEffect`, `SKTypeface`, `SKColor`, and `SKEncodedImageFormat` for allocation, drawing, and
  encoding; see "SkiaSharp Integration Design" under `docs/design/ots/` for lifecycle and
  disposal details.
- **Embedded Noto Sans typefaces** — the regular, bold, italic, and bold-italic Noto Sans font
  resources embedded in the assembly are loaded once as `SKTypeface` instances at startup.

### SkiaRasterRenderer Callers

- **`PngRenderer`, `JpegRenderer`, `WebpRenderer`** — the three concrete raster renderers each
  derive from `SkiaRasterRenderer` and rely on its `Render` implementation; they contribute only
  format metadata overrides.
- **`RendererRegistry` / consumers of `IRenderer`** — external callers do not use
  `SkiaRasterRenderer` directly (it is `abstract`); they resolve one of the concrete renderers by
  media type or file extension and invoke `Render` through the `IRenderer` contract.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Skia-SkiaRasterRenderer-DrawsLayoutTree | `Render`, node drawing, background, markers, and labels |
| Rendering-Skia-SkiaRasterRenderer-ThemeColours | Box and grid fill selection from `Theme.DepthFillColors` |
| Rendering-Skia-SkiaRasterRenderer-EndMarkers | End-marker drawing helpers that use `NotationMetrics` |
| Rendering-Skia-SkiaRasterRenderer-EmptyTree | Minimum bitmap width and height enforcement in `Render` |
| Rendering-Skia-SkiaRasterRenderer-PortAndContentInset | Port label placement, `ContentInsetLeft`-aware start |
| Rendering-Skia-SkiaRasterRenderer-CanvasGrowsForLabels | `Render` grows the bitmap to fit every placed label |
| Rendering-Skia-SkiaRasterRenderer-TitleCentersOnBoxWidth | `RenderBoxTitle` centers on full box width |
| Rendering-Skia-SkiaRasterRenderer-PortGlyphOutline | `RenderPort` outlines the port glyph in `theme.BackgroundColor` |
| Rendering-Skia-SkiaRasterRenderer-PortLabelSqueeze | `RenderPort` bounds label width to `port.MaxLabelWidth` |
| Rendering-Skia-SkiaRasterRenderer-SharedTypefaces | `SkiaTypefaces.Resolve` and its lazily-loaded typeface fields |
