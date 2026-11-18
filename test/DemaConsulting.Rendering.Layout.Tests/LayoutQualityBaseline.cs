// <copyright file="LayoutQualityBaseline.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     The layout-quality numbers of one benchmark scene as measured on the baseline.
/// </summary>
/// <param name="Crossings">Proper connector crossings.</param>
/// <param name="LargestEmptySquare">Side of the largest empty square, in logical pixels.</param>
/// <param name="MeanLeafBlockHeight">Mean leaf block height, in logical pixels.</param>
/// <param name="B4Ratio">Largest empty square side divided by the mean leaf block height.</param>
/// <param name="BlockAreaRatio">Leaf block area divided by canvas area.</param>
/// <param name="CanvasWidth">Canvas width.</param>
/// <param name="CanvasHeight">Canvas height.</param>
/// <param name="CanvasArea">Canvas area.</param>
internal sealed record BaselineMetrics(
    int Crossings,
    double LargestEmptySquare,
    double MeanLeafBlockHeight,
    double B4Ratio,
    double BlockAreaRatio,
    double CanvasWidth,
    double CanvasHeight,
    double CanvasArea);

/// <summary>
///     The layout-quality baseline: the numbers the benchmark scenes of <see cref="LayoutQualityScenes"/>
///     produced on main@219650c, before the layout-quality work (Rendering#48, Rendering#60). They are
///     measured with <see cref="LayoutQualityMetrics"/> on the unmodified main projects and are the
///     thresholds the regression tests compare against: never worse, and strictly better where a fix
///     targets the metric.
/// </summary>
internal static class LayoutQualityBaseline
{
    /// <summary>
    ///     The best crossing count main reaches for <see cref="LayoutQualityScenes.GeneralViewUp"/> (Up) over
    ///     seeds 0 to 39 of the shuffled insertion order (seed 28), i.e. what a caller that hand-picks the
    ///     insertion order can reach on main. main@219650c.
    /// </summary>
    public const int GeneralViewUpBestShuffledCrossings = 12;

    /// <summary>
    ///     The smallest canvas area main reaches for Repro 2 over every insertion order of the first folder,
    ///     by flow direction (Right and Left; Up and Down). main@219650c.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    /// <returns>The best canvas area.</returns>
    public static double Repro2BestCanvasArea(LayoutFlowDirection direction) =>
        direction is LayoutFlowDirection.Right or LayoutFlowDirection.Left ? 288904.0 : 243040.0;

    /// <summary>
    ///     The smallest canvas area main reaches for Repro 3 (interior connector) over every insertion order of
    ///     the first folder, by flow direction (Right and Left; Up and Down). main@219650c.
    /// </summary>
    /// <param name="direction">The flow direction.</param>
    /// <returns>The best canvas area.</returns>
    public static double Repro3BestCanvasArea(LayoutFlowDirection direction) =>
        direction is LayoutFlowDirection.Right or LayoutFlowDirection.Left ? 353584.0 : 294840.0;

    /// <summary>The per-scene baseline, keyed by <see cref="QualityScene.Name"/>. main@219650c.</summary>
    public static readonly IReadOnlyDictionary<string, BaselineMetrics> Scenes = new Dictionary<string, BaselineMetrics>
    {
        ["Repro2Right"] = new(1, 132, 56, 2.357142857142857, 0.13568521031207598, 938, 308, 288904),
        ["Repro3Right"] = new(2, 132, 56, 2.357142857142857, 0.13303769401330376, 1148, 308, 353584),
        ["NestedDepthRight"] = new(1, 344, 56, 6.142857142857143, 0.07848431457794659, 1276, 548, 699248),
        ["NestedPackagesRight"] = new(3, 268, 56, 4.785714285714286, 0.1383399209486166, 1564, 616, 963424),
        ["NestedPackagesUp"] = new(7, 496, 56, 8.857142857142858, 0.08382626126824202, 879.4, 1808, 1589955.2),
        ["GeneralViewUp"] = new(22, 440, 56, 7.857142857142857, 0.09381479795680979, 1310.4, 1690, 2214576),
        ["DenseUsageTree"] = new(43, 388, 56, 6.928571428571429, 0.099817613199547, 2474, 898, 2221652),
        ["InterconnectionLabeledCorridors"] = new(0, 128, 122.62857142857145, 1.0438024231127678, 0.2041584307662507, 2237.12, 319.6, 714983.552),
        ["InterconnectionLayerSpacing40"] = new(0, 128, 122.62857142857145, 1.0438024231127678, 0.2041584307662507, 2237.12, 319.6, 714983.552),
        ["ActionFlow"] = new(0, 232, 40, 5.8, 0.10892093630111571, 432.84137678665, 1086, 470065.7351903019),
        ["StateMachine"] = new(0, 96, 44, 2.1818181818181817, 0.20863829279416213, 318.20723405160044, 654, 208107.53106974668),
        ["AutoTallPlusIsolated"] = new(0, 332, 56, 5.928571428571429, 0.2101313320825516, 492, 910, 447720),
    };

    /// <summary>
    ///     The scenes a layout-quality fix must make strictly better: a smaller canvas together with a better
    ///     B4 ratio or B5 ratio. The remaining scenes only have to be no worse.
    /// </summary>
    public static readonly IReadOnlyList<string> MustImprove =
    [
        "Repro2Right",
        "Repro3Right",
        "NestedDepthRight",
        "NestedPackagesRight",
        "NestedPackagesUp",
        "GeneralViewUp",
        "DenseUsageTree",
        "InterconnectionLabeledCorridors",
        "AutoTallPlusIsolated",
    ];
}
