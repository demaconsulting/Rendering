# Rendering.Svg Verification

This document describes the system-level verification design for the `DemaConsulting.Rendering.Svg`
system and links to the per-unit verification document for its single unit. Detailed per-requirement
scenarios live in SvgRenderer Unit Verification.

## Verification Approach

The SVG system is verified through in-process xUnit tests that render placed `LayoutTree` inputs and
assert on the emitted SVG markup. The system-level smoke scenario exercises the public `IRenderer.Render`
entry point, decodes the written UTF-8 bytes, and confirms that a representative placed tree produces a
valid SVG document. Detailed element, styling, escaping, and marker-geometry scenarios are covered by the
SvgRenderer unit verification.

No mocking is required because the renderer is pure and stateless. Tests construct concrete model inputs,
use `Themes.Light`, write to `MemoryStream`, and inspect the resulting SVG text.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK.
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline.
- **Dependencies**: no external services, databases, files, or network access.
- **Isolation**: each test constructs its own renderer, layout tree, render options, and stream.
- **Test project**: `DemaConsulting.Rendering.Svg.Tests`.

## Acceptance Criteria

A verification run passes when the system scenario below and every scenario in
SvgRenderer Unit Verification pass without unexpected exception. Any missing SVG root,
wrong renderer metadata, wrong emitted element or attribute, malformed XML escaping, or marker geometry
that does not match `NotationMetrics` constitutes a failure.

## Test Scenarios

The system requirement is satisfied through the SvgRenderer unit scenarios; the representative
system-level scenario is:

- **`Rendering-Svg-WriteSvgDocument`**: `Render_SingleBox_ProducesSvgDocument` (see
  SvgRenderer Unit Verification).
