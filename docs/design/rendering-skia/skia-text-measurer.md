## SkiaTextMeasurer Unit Design

Part of the Rendering.Skia system.

### SkiaTextMeasurer Purpose

`SkiaTextMeasurer` has a single responsibility: measure the rendered advance width of a string
using real font metrics, so `LayeredLayoutAlgorithm` can size a node's `ContentInsetLeft` and
`ContentInsetRight` reserved port-label margins (see `docs/design/rendering-layout/layered-layout-algorithm.md`)
to match what a Skia-backed renderer will actually draw, instead of relying on the dependency-free
`HeuristicTextMeasurer` estimate. `SkiaTypefaces` is a companion internal helper, extracted from
`SkiaRasterRenderer`, that resolves the shared embedded Noto Sans typeface instances both types
measure and draw against.

### SkiaTextMeasurer Overview

`SkiaTextMeasurer` implements `ITextMeasurer` (declared in `DemaConsulting.Rendering`) by resolving
the requested bold/italic typeface variant through `SkiaTypefaces.Resolve` and constructing a
short-lived `SKFont` at the requested font size to call `SKFont.MeasureText(string)`. Set an instance
on `CoreOptions.TextMeasurer` to opt a layout run into font-accurate reserved margins; a caller that
never sets this property continues to get the heuristic estimator's approximation.

### SkiaTextMeasurer Data Model

`SkiaTextMeasurer` has no instance state; it is a stateless, thread-safe `ITextMeasurer`
implementation. `SkiaTypefaces` holds the shared state:

| Member | Type | Description |
| --- | --- | --- |
| `RegularTypeface` | `internal static Lazy<SKTypeface>` | Regular, upright typeface from `NotoSans-Regular.ttf`. |
| `BoldTypeface` | `internal static Lazy<SKTypeface>` | Bold, upright typeface, loaded from `NotoSans-Bold.ttf`. |
| `ItalicTypeface` | `internal static Lazy<SKTypeface>` | Regular, italic typeface, loaded from `NotoSans-Italic.ttf`. |
| `BoldItalicTypeface` | `internal static Lazy<SKTypeface>` | Bold, italic typeface, from `NotoSans-BoldItalic.ttf`. |

### SkiaTextMeasurer Methods

- **`MeasureWidth(string text, double fontSize, bool bold, bool italic)`** - validates `text` is
  non-null, resolves the matching typeface via `SkiaTypefaces.Resolve(bold, italic)`, constructs a
  disposable `SKFont` at the requested size, and returns `SKFont.MeasureText(text)`.
- **`SkiaTypefaces.Resolve(bool bold, bool italic)`** - a pattern-matching switch over the
  `(bold, italic)` tuple that selects one of the four lazily-loaded typeface fields, so a given
  combination always resolves to the exact same `SKTypeface` instance for the lifetime of the
  process.
- **`SkiaTypefaces.LoadTypeface(string fileName)`** (private) - locates the embedded assembly
  manifest resource whose name ends with the requested file name (case-insensitive), loads it into
  an `SKTypeface` via `SKData.Create`/`SKTypeface.FromData`, and falls back to `SKTypeface.Default`
  when the resource is missing (for example, in a development environment without the downloaded
  font files) so callers remain functional rather than throwing.

### SkiaTextMeasurer Design Constraints

- `MeasureWidth` shall throw `ArgumentNullException` for a null `text` argument, matching the
  `ITextMeasurer` contract's documented behavior.
- Typeface resolution shall be shared between `SkiaRasterRenderer` and `SkiaTextMeasurer` through
  `SkiaTypefaces`, so a layout run configured with `SkiaTextMeasurer` reserves margins that match
  pixel-for-pixel what `SkiaRasterRenderer` subsequently draws using the same typeface objects.
- `SkiaTypefaces` is `internal`; it is an implementation-sharing detail between the two units in
  this assembly, not a public surface.
- Typeface loading remains lazy and per-process-lifetime cached (`Lazy<SKTypeface>`), preserving the
  behavior of the four private fields this refactor extracted from `SkiaRasterRenderer` — this is a
  behavior-preserving refactor, not a functional change to `SkiaRasterRenderer`'s own drawing output.

### SkiaTextMeasurer Interactions

`SkiaTextMeasurer` is constructed by a caller and assigned to `CoreOptions.TextMeasurer`, which
`LayeredLayoutAlgorithm` (in `DemaConsulting.Rendering.Layout`) reads when computing
`LayoutBox.ContentInsetLeft`/`ContentInsetRight`. It depends on `SkiaTypefaces`, which is also used
directly by `SkiaRasterRenderer` for drawing, so the two units never load or measure against
divergent font data.

### SkiaTextMeasurer Error Handling

- **Null text** — `MeasureWidth` calls `ArgumentNullException.ThrowIfNull(text)` before constructing
  any SkiaSharp object.
- **Missing embedded font resource** — `SkiaTypefaces.LoadTypeface` falls back to
  `SKTypeface.Default` rather than throwing, so measurement (and drawing) remain functional, at
  reduced metric fidelity, if the embedded font resources are ever unavailable.
- **SkiaSharp exceptions** — errors surfaced by the underlying SkiaSharp APIs are not caught; they
  propagate unchanged to the caller.

### SkiaTextMeasurer Dependencies

- **`DemaConsulting.Rendering`** — implements the `ITextMeasurer` interface and is consumed via
  `CoreOptions.TextMeasurer`.
- **SkiaSharp (OTS)** — uses `SKFont`, `SKTypeface`, and `SKData` for typeface loading and text
  measurement.
- **Embedded Noto Sans typefaces** — the same regular, bold, italic, and bold-italic Noto Sans
  font resources `SkiaRasterRenderer` embeds and draws with.

### SkiaTextMeasurer Callers

- **Layout callers** — any caller that wants font-accurate reserved port-label margins constructs a
  `SkiaTextMeasurer` and assigns it to `CoreOptions.TextMeasurer` before running the `layered`
  algorithm.
- **`SkiaRasterRenderer`** — depends on the sibling `SkiaTypefaces` helper (not on
  `SkiaTextMeasurer` itself) for its own drawing typefaces.

### Requirements Traceability

| Requirement ID | Satisfied by |
| --- | --- |
| Rendering-Skia-SkiaTextMeasurer-MeasuresRealFontMetrics | `MeasureWidth` via `SkiaTypefaces.Resolve` / `MeasureText` |
| Rendering-Skia-SkiaTypefaces-SharedResolution | `SkiaTypefaces.Resolve` and its lazily-loaded typeface fields |
