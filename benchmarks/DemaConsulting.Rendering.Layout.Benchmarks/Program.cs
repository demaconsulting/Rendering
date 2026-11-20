// <copyright file="Program.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Globalization;
using System.Text;
using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Layout;
using DemaConsulting.Rendering.Layout.Tests;

// The opt-in routing benchmark: lays out the synthetic and nested scenes of the layout tests through the public
// LayoutEngine.Layout and prints a Markdown table of times, the growth exponent between doublings and the
// determinism check. It never judges a time; the exit code is 1 only when a scene draws fewer connectors than it
// has or two runs of one scene differ (a correctness failure, not a performance one).
return BenchmarkProgram.Run(args);

/// <summary>
/// One benchmark scene: a name, how to build its graph, and what a valid layout of it looks like.
/// </summary>
/// <param name="Name">The scene's name, as given to <c>--scene</c>.</param>
/// <param name="Description">A short description for the table.</param>
/// <param name="Build">Builds a fresh graph (a layout does not change the graph, but a new one keeps runs independent).</param>
/// <param name="ExpectedLines">The number of connector lines a complete layout draws.</param>
/// <param name="Series">The series the scene belongs to; the growth exponent is computed between consecutive scenes of a series.</param>
/// <param name="Slow">Whether one layout takes seconds: such a scene runs once by default and is not warmed up.</param>
internal sealed record BenchmarkScene(
    string Name,
    string Description,
    Func<LayoutGraph> Build,
    int ExpectedLines,
    string Series,
    bool Slow = false);

/// <summary>
/// The measurements of one scene.
/// </summary>
/// <param name="Scene">The scene.</param>
/// <param name="Seconds">The time of each measured layout, in seconds.</param>
/// <param name="Lines">The number of connector lines the layout drew.</param>
/// <param name="Warnings">The number of warnings of the layout tree.</param>
/// <param name="Fingerprint">The layout fingerprint of the first run.</param>
/// <param name="Deterministic">Whether every run of the scene gave the same fingerprint.</param>
internal sealed record SceneResult(
    BenchmarkScene Scene,
    double[] Seconds,
    int Lines,
    int Warnings,
    string Fingerprint,
    bool Deterministic)
{
    /// <summary>Gets the shortest measured time.</summary>
    public double Min => Seconds.Min();

    /// <summary>Gets the median measured time.</summary>
    public double Median
    {
        get
        {
            var sorted = Seconds.Order().ToArray();
            var middle = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2.0;
        }
    }

    /// <summary>Gets the longest measured time.</summary>
    public double Max => Seconds.Max();

    /// <summary>Gets a value indicating whether the layout drew every connector of the scene.</summary>
    public bool Complete => Lines == Scene.ExpectedLines;
}

/// <summary>
/// The benchmark driver: parses the arguments, measures the scenes and prints the report.
/// </summary>
internal static class BenchmarkProgram
{
    /// <summary>The number of measured layouts of a scene when <c>--runs</c> is not given.</summary>
    private const int DefaultRuns = 5;

    /// <summary>The median time, in seconds, below which a growth exponent is not shown: the time is within timer and JIT noise.</summary>
    private const double GrowthNoiseFloorSeconds = 0.05;

    /// <summary>The numbers of cross-container connectors of the synthetic series (each twice the one before).</summary>
    private static readonly int[] SyntheticSizes = [5, 10, 20, 40, 80, 160, 320, 640];

    /// <summary>
    /// Runs the benchmark.
    /// </summary>
    /// <param name="args">The command line: <c>--scene name</c> (repeatable), <c>--runs n</c>, <c>--list</c>, <c>--help</c>.</param>
    /// <returns>0 on success, 1 when a scene is incomplete or not deterministic, 2 on a usage error.</returns>
    public static int Run(string[] args)
    {
        // Parse the command line
        var wanted = new List<string>();
        int? runs = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--scene" when i + 1 < args.Length:
                    wanted.AddRange(args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                case "--runs" when i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0:
                    runs = n;
                    i++;
                    break;
                case "--list":
                    foreach (var scene in Scenes())
                    {
                        Console.WriteLine($"{scene.Name,-18} {scene.Description}");
                    }

                    return 0;
                default:
                    Console.WriteLine("usage: benchmark [--scene name[,name...]] [--runs n] [--list]");
                    return args[i] == "--help" ? 0 : 2;
            }
        }

        // Select the scenes, in the order of the suite
        var all = Scenes();
        var unknown = wanted.Where(name => all.All(scene => scene.Name != name)).ToList();
        if (unknown.Count > 0)
        {
            Console.WriteLine($"unknown scene(s): {string.Join(", ", unknown)} (see --list)");
            return 2;
        }

        var selected = wanted.Count == 0 ? all : all.Where(scene => wanted.Contains(scene.Name)).ToList();

        // Measure every scene
        var results = new List<SceneResult>();
        foreach (var scene in selected)
        {
            Console.Error.WriteLine($"measuring {scene.Name} ...");
            results.Add(Measure(scene, runs ?? (scene.Slow ? 1 : DefaultRuns)));
        }

        Report(results);
        return results.All(result => result.Complete && result.Deterministic) ? 0 : 1;
    }

    /// <summary>
    /// Returns the scenes of the suite: the synthetic package at 5 to 640 cross-container connectors, and the
    /// nested packages (small, oversize, and the everyday package of four sub-packages of three folders).
    /// </summary>
    /// <returns>The scenes, in the order they are measured and reported.</returns>
    private static List<BenchmarkScene> Scenes()
    {
        var scenes = SyntheticSizes
            .Select(edges => new BenchmarkScene(
                $"syn-{edges}",
                $"Package of 6 folders of 5 leaves, {edges} cross-container connectors",
                () => RoutingScenes.SyntheticPackage(edges),
                RoutingScenes.SyntheticLineCount(edges),
                "syn"))
            .ToList();
        scenes.Add(new BenchmarkScene(
            "nested-small",
            "2 sub-packages of 2 folders of 3 leaves, 3 inner and 2 package connectors",
            () => RoutingScenes.NestedPackage(2, 2, 3, 3, 2),
            RoutingScenes.NestedLineCount(2, 2, 3, 3, 2),
            "nested"));
        scenes.Add(new BenchmarkScene(
            "nested-oversize",
            "4 sub-packages of 3 folders of 4 leaves, 6 inner and 80 package connectors",
            () => RoutingScenes.NestedPackage(4, 3, 4, 6, 80),
            RoutingScenes.NestedLineCount(4, 3, 4, 6, 80),
            "nested"));
        scenes.Add(new BenchmarkScene(
            "nested-everyday",
            "4 sub-packages of 3 folders of 4 leaves, 6 inner and 6 package connectors (65 nodes, 66 connectors)",
            () => RoutingScenes.NestedPackage(4, 3, 4, 6, 6),
            RoutingScenes.NestedLineCount(4, 3, 4, 6, 6),
            "nested",
            Slow: true));
        return scenes;
    }

    /// <summary>
    /// Lays the scene out once to warm up the runtime (unless it is slow), then the given number of times,
    /// timing each layout and fingerprinting its result.
    /// </summary>
    /// <param name="scene">The scene to measure.</param>
    /// <param name="runs">The number of measured layouts.</param>
    /// <returns>The measurements.</returns>
    private static SceneResult Measure(BenchmarkScene scene, int runs)
    {
        // Warm up: the first layout pays for the JIT of the whole layout pipeline
        var fingerprints = new HashSet<string>();
        if (!scene.Slow)
        {
            fingerprints.Add(RoutingScenes.Fingerprint(LayoutEngine.Layout(scene.Build())));
        }

        // Measure: graph construction is outside the timing
        var seconds = new double[runs];
        LayoutTree? last = null;
        for (var run = 0; run < runs; run++)
        {
            var graph = scene.Build();
            GC.Collect();
            var stopwatch = Stopwatch.StartNew();
            last = LayoutEngine.Layout(graph);
            seconds[run] = stopwatch.Elapsed.TotalSeconds;
            fingerprints.Add(RoutingScenes.Fingerprint(last));
        }

        return new SceneResult(
            scene,
            seconds,
            RoutingScenes.CountLines(last!.Nodes),
            last.Warnings.Count,
            RoutingScenes.Fingerprint(last),
            fingerprints.Count == 1);
    }

    /// <summary>
    /// Prints the results as a Markdown table, with the growth exponent log2(t(2E) / t(E)) of the median time
    /// between consecutive scenes of the synthetic series (each has twice the connectors of the one before).
    /// </summary>
    /// <param name="results">The measurements, in the order of the suite.</param>
    private static void Report(List<SceneResult> results)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"Runtime: {Environment.Version}, {RuntimeInformation()}, processors: {Environment.ProcessorCount}");
        text.AppendLine();
        text.AppendLine("| Scene | Connectors drawn | Warnings | Runs | Min (s) | Median (s) | Max (s) | Growth exponent | Fingerprint | Check |");
        text.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        SceneResult? previous = null;
        foreach (var result in results)
        {
            // The growth exponent compares a synthetic scene with the scene that has half its connectors. It is
            // shown only where it means something: both scenes at the same effort (a scope too large for the
            // full effort drops to the reduced one, which is a different algorithm) and a time above the timer
            // and JIT noise
            var exponent = "-";
            if (previous is not null
                && previous.Scene.Series == "syn"
                && result.Scene.Series == "syn"
                && previous.Median >= GrowthNoiseFloorSeconds
                && (previous.Warnings > 0) == (result.Warnings > 0))
            {
                exponent = Math.Log2(result.Median / previous.Median).ToString("F2", CultureInfo.InvariantCulture);
            }

            var check = !result.Complete ? "INCOMPLETE" : !result.Deterministic ? "NOT DETERMINISTIC" : "ok";
            text.AppendLine(CultureInfo.InvariantCulture, $"| {result.Scene.Name} | {result.Lines} / {result.Scene.ExpectedLines} | {result.Warnings} | {result.Seconds.Length} | {result.Min:F2} | {result.Median:F2} | {result.Max:F2} | {exponent} | {result.Fingerprint} | {check} |");
            previous = result;
        }

        Console.Write(text);
    }

    /// <summary>Describes the runtime and operating system for the report header.</summary>
    /// <returns>A short description.</returns>
    private static string RuntimeInformation() =>
        $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription.Trim()} {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}";
}
