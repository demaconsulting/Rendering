// <copyright file="GalleryLayoutQualityTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;

using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.Layout;
using DemaConsulting.Rendering.Layout.Tests;

namespace DemaConsulting.Rendering.Gallery;

/// <summary>
///     The <c>layout-quality</c> gallery group (Rendering#48 and Rendering#60): every benchmark scene of
///     <see cref="LayoutQualityScenes"/> is rendered to SVG (and, for the main ones, PNG) from the very
///     graph the layout regression tests measure, and the metrics table comparing the layout with the
///     baseline of <see cref="LayoutQualityBaseline"/> (main@219650c) is generated from the same
///     <see cref="LayoutQualityMetrics"/>, so the picture and the numbers can never drift apart.
/// </summary>
public sealed class GalleryLayoutQualityTests
{
    /// <summary>Slack, in measurement units, for comparing a floating point metric with its baseline.</summary>
    private const double Slack = 1e-6;

    /// <summary>The LayerSpacing, in logical pixels, of the wide-corridor demonstration.</summary>
    private const double WideLayerSpacing = 150.0;

    /// <summary>The engine's historical corridor width between layers, in logical pixels.</summary>
    private const double HistoricalCorridor = 70.0;

    /// <summary>The output files of each benchmark scene: the SVG and, for the main scenes, the PNG.</summary>
    private static readonly IReadOnlyDictionary<string, (string Svg, string? Png)> SceneFiles =
        new Dictionary<string, (string Svg, string? Png)>
        {
            ["Repro2Right"] = (GalleryCatalog.QualityRepro2RightSvg, null),
            ["Repro3Right"] = (GalleryCatalog.QualityRepro3RightSvg, null),
            ["NestedDepthRight"] = (GalleryCatalog.QualityNestedDepthRightSvg, null),
            ["NestedPackagesRight"] = (GalleryCatalog.QualityNestedPackagesRightSvg, GalleryCatalog.QualityNestedPackagesRightPng),
            ["NestedPackagesUp"] = (GalleryCatalog.QualityNestedPackagesUpSvg, GalleryCatalog.QualityNestedPackagesUpPng),
            ["GeneralViewUp"] = (GalleryCatalog.QualityGeneralViewUpSvg, GalleryCatalog.QualityGeneralViewUpPng),
            ["DenseUsageTree"] = (GalleryCatalog.QualityDenseUsageTreeSvg, GalleryCatalog.QualityDenseUsageTreePng),
            ["InterconnectionLabeledCorridors"] = (GalleryCatalog.QualityInterconnectionSvg, GalleryCatalog.QualityInterconnectionPng),
            ["InterconnectionLayerSpacing40"] = (GalleryCatalog.QualityInterconnectionLayerSpacing40Svg, null),
            ["ActionFlow"] = (GalleryCatalog.QualityActionFlowSvg, null),
            ["StateMachine"] = (GalleryCatalog.QualityStateMachineSvg, null),
            ["AutoTallPlusIsolated"] = (GalleryCatalog.QualityAutoTallPlusIsolatedSvg, GalleryCatalog.QualityAutoTallPlusIsolatedPng),
        };

    /// <summary>Every benchmark scene name.</summary>
    public static TheoryData<string> BenchmarkSceneNames()
    {
        var names = new TheoryData<string>();
        foreach (var scene in LayoutQualityScenes.Benchmarks)
        {
            names.Add(scene.Name);
        }

        return names;
    }

    /// <summary>
    ///     Renders one benchmark scene to its gallery SVG (and PNG where the scene has one), proving every
    ///     scene of the benchmark set has a gallery image.
    /// </summary>
    /// <param name="scene">The benchmark scene name.</param>
    [Theory]
    [MemberData(nameof(BenchmarkSceneNames))]
    public void Gallery_LayoutQuality_BenchmarkScene_RendersSvgAndPng(string scene)
    {
        // Arrange
        var files = SceneFiles[scene];
        var benchmark = LayoutQualityScenes.Benchmarks.Single(candidate => candidate.Name == scene);

        // Act / Assert: the writer asserts the output is a valid image
        GalleryWriter.Svg(files.Svg, benchmark.Build(), Themes.Light);
        if (files.Png is { } png)
        {
            GalleryWriter.Png(png, benchmark.Build(), Themes.Light);
        }
    }

    /// <summary>
    ///     Renders the Rendering#60 fan-out with the default and with a LayerSpacing of 150 px, proving the
    ///     option widens the corridor between the layers: the canvas grows by the difference between the
    ///     corridors, from the historical 70 px to 150 px.
    /// </summary>
    [Fact]
    public void Gallery_LayoutQuality_LayerSpacing150_RendersWiderCorridorThanDefault()
    {
        // Act
        GalleryWriter.Svg(GalleryCatalog.QualityLayerSpacingDefaultSvg, LayoutQualityScenes.FanOutPair("layered", LayoutFlowDirection.Right, null), Themes.Light);
        GalleryWriter.Svg(GalleryCatalog.QualityLayerSpacing150Svg, LayoutQualityScenes.FanOutPair("layered", LayoutFlowDirection.Right, WideLayerSpacing), Themes.Light);
        GalleryWriter.Png(GalleryCatalog.QualityLayerSpacing150Png, LayoutQualityScenes.FanOutPair("layered", LayoutFlowDirection.Right, WideLayerSpacing), Themes.Light);

        // Assert
        var wide = LayoutEngine.Layout(LayoutQualityScenes.FanOutPair("layered", LayoutFlowDirection.Right, WideLayerSpacing));
        var unset = LayoutEngine.Layout(LayoutQualityScenes.FanOutPair("layered", LayoutFlowDirection.Right, null));
        Assert.Equal(unset.Width + (WideLayerSpacing - HistoricalCorridor), wide.Width, 0.5);
        Assert.Equal(unset.Height, wide.Height, 0.5);
    }

    /// <summary>
    ///     Measures every benchmark scene, asserts none got worse than the baseline of main@219650c (crossings,
    ///     largest empty square relative to the block height, block area ratio, canvas area) and that the
    ///     scenes a fix targets got strictly better, and, in gallery mode, writes the before/after table to
    ///     <c>layout-quality/metrics.md</c> next to the images. The table holds no timings, so the committed
    ///     file is identical between runs.
    /// </summary>
    [Fact]
    public void Gallery_LayoutQuality_WritesMetricsTable()
    {
        // Act: measure the layout of every benchmark scene
        var measured = new List<(QualityScene Scene, BaselineMetrics Before, LayoutQualityReport After)>();
        foreach (var scene in LayoutQualityScenes.Benchmarks)
        {
            var report = LayoutQualityMetrics.Measure(LayoutEngine.Layout(scene.Build()));
            measured.Add((scene, LayoutQualityBaseline.Scenes[scene.Name], report));
        }

        var spacing = new List<(string Label, LayoutTree Tree)>
        {
            ("unset (historical 70 px corridor)", LayoutEngine.Layout(FanOut(null))),
            ("100", LayoutEngine.Layout(FanOut(100.0))),
            ("150", LayoutEngine.Layout(FanOut(WideLayerSpacing))),
        };

        // Assert: never worse than the baseline on any metric
        foreach (var (scene, before, after) in measured)
        {
            Assert.True(after.Crossings <= before.Crossings, $"{scene.Name}: crossings {after.Crossings} > {before.Crossings}");
            Assert.True(after.B4Ratio <= before.B4Ratio + Slack, $"{scene.Name}: B4 {after.B4Ratio} > {before.B4Ratio}");
            Assert.True(after.BlockAreaRatio >= before.BlockAreaRatio - Slack, $"{scene.Name}: B5 {after.BlockAreaRatio} < {before.BlockAreaRatio}");
            Assert.True(after.CanvasArea <= before.CanvasArea + Slack, $"{scene.Name}: area {after.CanvasArea} > {before.CanvasArea}");

            // Assert: the connectors are no visibly worse (length and bends within 10%, hairpins and close parallel runs)
            var limit = LayoutQualityBaseline.RouteQualityLimits(scene.Name);
            Assert.True(after.TotalConnectorLength <= limit.TotalConnectorLength + Slack, $"{scene.Name}: connector length {after.TotalConnectorLength} > {limit.TotalConnectorLength}");
            Assert.True(after.TotalBends <= limit.TotalBends, $"{scene.Name}: bends {after.TotalBends} > {limit.TotalBends}");
            Assert.True(after.UTurns <= limit.UTurns, $"{scene.Name}: hairpins {after.UTurns} > {limit.UTurns}");
            Assert.True(after.ParallelRunLength <= limit.ParallelRunLength + Slack, $"{scene.Name}: close parallel runs {after.ParallelRunLength} > {limit.ParallelRunLength}");
        }

        // Assert: the scenes a fix targets are strictly better: a smaller canvas with a better B4 or B5
        foreach (var name in LayoutQualityBaseline.MustImprove)
        {
            var (_, before, after) = measured.Single(entry => entry.Scene.Name == name);
            Assert.True(after.CanvasArea < before.CanvasArea - Slack, $"{name}: area {after.CanvasArea} is not below {before.CanvasArea}");
            Assert.True(
                after.B4Ratio < before.B4Ratio - Slack || after.BlockAreaRatio > before.BlockAreaRatio + Slack,
                $"{name}: neither B4 ({after.B4Ratio} vs {before.B4Ratio}) nor B5 ({after.BlockAreaRatio} vs {before.BlockAreaRatio}) improved");
        }

        // Assert: the canvas hugs the content: one padding of 20 px on every side
        foreach (var (scene, _, after) in measured)
        {
            Assert.True(
                Math.Abs(after.Margins.Left - 20.0) <= 1.0 && Math.Abs(after.Margins.Top - 20.0) <= 1.0
                && Math.Abs(after.Margins.Right - 20.0) <= 1.0 && Math.Abs(after.Margins.Bottom - 20.0) <= 1.0,
                $"{scene.Name}: canvas margins {after.Margins}");
        }

        // Assert: LayerSpacing is honored as a floor for the corridor between the layers
        Assert.True(spacing[1].Tree.Width > spacing[0].Tree.Width, "LayerSpacing 100 must widen the canvas.");
        Assert.True(spacing[2].Tree.Width > spacing[1].Tree.Width, "LayerSpacing 150 must widen the canvas further.");

        // The table is only written to disk in "regenerate the committed showcase" mode: ordinary test runs
        // never write a Markdown file into the build output where the lint gate would pick it up.
        var markdown = BuildMetricsTable(measured, spacing);
        Assert.Contains("| `NestedPackagesRight` |", markdown, StringComparison.Ordinal);
        if (!GalleryOutput.IsGalleryMode)
        {
            return;
        }

        var path = Path.Combine(GalleryOutput.ResolveDirectory(), GalleryCatalog.QualityMetricsMarkdown);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, markdown, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Assert.True(new FileInfo(path).Length > 0, $"Expected the metrics table to be non-empty: {path}");
    }

    /// <summary>Builds the Markdown metrics document comparing the baseline with the measured layout.</summary>
    private static string BuildMetricsTable(
        IReadOnlyList<(QualityScene Scene, BaselineMetrics Before, LayoutQualityReport After)> measured,
        IReadOnlyList<(string Label, LayoutTree Tree)> spacing)
    {
        var builder = new StringBuilder();
        builder.Append("# Layout quality metrics (#48, #60)\n\n");
        builder.Append(GalleryIndex.WrapParagraph(
            "Each cell reads `before → after`. *Before* is the baseline of the previous layout engine "
            + "(main@219650c), recorded as constants in the test project; *after* is measured when this "
            + "page is generated, on the very graphs rendered in the [layout-quality group](README.md). "
            + "The generating gallery test fails if any scene is worse than its baseline.")).Append("\n\n");
        builder.Append("- **Crossings**: proper crossings between distinct connectors (touches at a shared end excluded).\n");
        builder.Append("- **B4**: side of the largest empty square divided by the mean block height (pass: at most 3).\n");
        builder.Append("- **B5**: total block area divided by the canvas area (pass: at least 0.20).\n");
        builder.Append("- **Canvas**: width x height and area in logical pixels.\n\n");

        builder.Append("## Crossings and canvas\n\n");
        builder.Append("| Scene | Crossings | Canvas | Area |\n| --- | --- | --- | --- |\n");
        foreach (var (scene, before, after) in measured)
        {
            builder.Append(CultureInfo.InvariantCulture, $"| `{scene.Name}` | {before.Crossings} → {after.Crossings} ")
                .Append(CultureInfo.InvariantCulture, $"| {Size(before.CanvasWidth, before.CanvasHeight)} → {Size(after.CanvasWidth, after.CanvasHeight)} ")
                .Append(CultureInfo.InvariantCulture, $"| {before.CanvasArea:F0} → {after.CanvasArea:F0} ({Change(before.CanvasArea, after.CanvasArea)}) |\n");
        }

        builder.Append("\n## Empty space and block area\n\n");
        builder.Append("| Scene | Largest empty square (px) | B4 | B5 |\n| --- | --- | --- | --- |\n");
        foreach (var (scene, before, after) in measured)
        {
            builder.Append(CultureInfo.InvariantCulture, $"| `{scene.Name}` | {before.LargestEmptySquare:F0} → {after.LargestEmptySquare:F0} ")
                .Append(CultureInfo.InvariantCulture, $"| {before.B4Ratio:F2} → {after.B4Ratio:F2} ")
                .Append(CultureInfo.InvariantCulture, $"| {before.BlockAreaRatio:F3} → {after.BlockAreaRatio:F3} |\n");
        }

        builder.Append("\n## Connector routes\n\n");
        builder.Append(GalleryIndex.WrapParagraph(
            "How the connectors themselves read. *Length* is the total connector length in logical pixels, "
            + "*bends* the number of direction changes, *hairpins* the legs that run back against the direction "
            + "of the leg two before them, and *close parallel* the length, in logical pixels, of runs of "
            + "different connectors that lie closer than 16 px for more than 20 px. The generating test holds "
            + "length and bends to within 10% of the baseline and hairpins and close parallel runs to the "
            + "baseline, except for the scenes listed under *Exceptions*.")).Append("\n\n");
        builder.Append("| Scene | Length | Bends | Hairpins | Close parallel |\n| --- | --- | --- | --- | --- |\n");
        foreach (var (scene, before, after) in measured)
        {
            builder.Append(CultureInfo.InvariantCulture, $"| `{scene.Name}` | {before.TotalConnectorLength:F0} → {after.TotalConnectorLength:F0} ({Change(before.TotalConnectorLength, after.TotalConnectorLength)}) ")
                .Append(CultureInfo.InvariantCulture, $"| {before.TotalBends} → {after.TotalBends} ")
                .Append(CultureInfo.InvariantCulture, $"| {before.UTurns} → {after.UTurns} ")
                .Append(CultureInfo.InvariantCulture, $"| {before.ParallelRunLength:F0} → {after.ParallelRunLength:F0} |\n");
        }

        builder.Append("\n### Exceptions\n\n");
        foreach (var (scene, allowance) in LayoutQualityBaseline.RouteQualityExceptions)
        {
            var wrapped = GalleryIndex.WrapParagraph($"- `{scene}`: {allowance.Reason}.", 118).Split('\n');
            builder.Append(string.Join("\n  ", wrapped)).Append('\n');
        }

        builder.Append("\n## Canvas margin\n\n");
        builder.Append(GalleryIndex.WrapParagraph(
            "The canvas of every scene is its content (boxes, connectors, labels, ports) plus one padding of "
            + "20 px on all four sides; the generating test fails when a margin is not 20 px. The previous engine "
            + "left the content of a Left or Up flow against the top or left edge with a double margin at the "
            + "opposite edge, and put a connector that detoured beyond the origin 10 px from it.")).Append("\n\n");
        builder.Append("| Scene | Margin (left, top, right, bottom) |\n| --- | --- |\n");
        foreach (var (scene, _, after) in measured)
        {
            builder.Append(CultureInfo.InvariantCulture, $"| `{scene.Name}` | {after.Margins.Left:0.#}, {after.Margins.Top:0.#}, {after.Margins.Right:0.#}, {after.Margins.Bottom:0.#} |\n");
        }

        builder.Append("\n## LayerSpacing\n\n");
        builder.Append(GalleryIndex.WrapParagraph(
            "The source feeding two targets of the [layout-quality group](README.md) laid out with different "
            + "`CoreOptions.LayerSpacing` values. The option is a floor for the corridor between adjacent "
            + "layers; the previous engine ignored it, so all three canvases were the unset one.")).Append("\n\n");
        builder.Append("| LayerSpacing | Canvas |\n| --- | --- |\n");
        foreach (var (label, tree) in spacing)
        {
            builder.Append(CultureInfo.InvariantCulture, $"| {label} | {Size(tree.Width, tree.Height)} |\n");
        }

        return builder.ToString();
    }

    /// <summary>Builds the Rendering#60 fan-out laid out to the right with the given LayerSpacing.</summary>
    private static LayoutGraph FanOut(double? layerSpacing) =>
        LayoutQualityScenes.FanOutPair("layered", LayoutFlowDirection.Right, layerSpacing);

    /// <summary>Formats a canvas size as <c>width x height</c> with at most one decimal.</summary>
    private static string Size(double width, double height) =>
        string.Create(CultureInfo.InvariantCulture, $"{width:0.#}x{height:0.#}");

    /// <summary>Formats the relative change of a value as a signed percentage.</summary>
    private static string Change(double before, double after) =>
        string.Create(CultureInfo.InvariantCulture, $"{((after / before) - 1.0) * 100.0:+0.0;-0.0;0.0}%");
}
