// <copyright file="GalleryWriter.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Text;

using DemaConsulting.CanvasNet.Codecs;

using DemaConsulting.Rendering.Abstractions;
using DemaConsulting.Rendering.CanvasNet;
using DemaConsulting.Rendering.Layout;
using DemaConsulting.Rendering.Layout.Tests;
using DemaConsulting.Rendering.Svg;

namespace DemaConsulting.Rendering.Gallery;

/// <summary>
///     Lays out a <see cref="LayoutGraph"/>, renders it to the gallery output directory, and asserts the
///     produced file is a valid, non-empty image. This is the shared engine behind every showcase fact,
///     so each fact reduces to "describe a graph, name a file, pick a theme".
/// </summary>
internal static class GalleryWriter
{
    private static readonly SvgRenderer SvgRenderer = new();
    private static readonly PngRenderer PngRenderer = new();
    private static readonly JpegRenderer JpegRenderer = new();

    /// <summary>PNG file signature (the eight leading bytes of every PNG stream).</summary>
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>JPEG file signature (the leading Start Of Image marker bytes of every JPEG stream).</summary>
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    /// <summary>
    ///     Lays out <paramref name="graph"/> with whatever algorithm and options it declares (see
    ///     <see cref="LayoutEngine"/>), renders it to <paramref name="fileName"/> as SVG, and asserts the
    ///     result is a well-formed SVG document.
    /// </summary>
    /// <param name="fileName">Stable output filename (for example <c>layered-pipeline.svg</c>).</param>
    /// <param name="graph">
    /// The graph to lay out. Configure it directly (for example
    /// <c>graph.Set(CoreOptions.Algorithm, "layered")</c>) before calling this method.
    /// </param>
    /// <param name="theme">The theme to render with.</param>
    public static void Svg(string fileName, LayoutGraph graph, Theme theme)
    {
        var tree = LayoutEngine.Layout(graph);
        AssertMarkedEndsOnBoundaryOrPort(tree, fileName);
        LayoutGeometryAssert.AssertCanvasMarginsWithin(tree, name: fileName);
        LayoutGeometryAssert.AssertMarkersClearContainerBorders(tree, fileName);
        var path = Path.Combine(GalleryOutput.ResolveDirectory(), fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using (var stream = File.Create(path))
        {
            SvgRenderer.Render(tree, new RenderOptions(theme), stream);
        }

        AssertValidSvg(path);
    }

    /// <summary>
    ///     Renders a pre-built <paramref name="tree"/> directly to <paramref name="fileName"/> as SVG,
    ///     bypassing <see cref="LayoutEngine.Layout(LayoutGraph)"/> entirely, and asserts the result is a
    ///     well-formed SVG document. Every other gallery group exists to show what the layout engine
    ///     itself produces, so this overload must be used only by diagrams belonging to the
    ///     <c>custom-rendering</c> gallery group — the one group whose whole point is a hand-built box
    ///     arrangement, honestly labeled as bypassing the engine, rather than algorithm output.
    /// </summary>
    /// <param name="fileName">Stable output filename (for example <c>custom-rendering/my-diagram.svg</c>).</param>
    /// <param name="tree">The already-laid-out tree to render.</param>
    /// <param name="theme">The theme to render with.</param>
    public static void Svg(string fileName, LayoutTree tree, Theme theme)
    {
        var path = Path.Combine(GalleryOutput.ResolveDirectory(), fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using (var stream = File.Create(path))
        {
            SvgRenderer.Render(tree, new RenderOptions(theme), stream);
        }

        AssertValidSvg(path);
    }

    /// <summary>
    ///     Lays out <paramref name="graph"/> with whatever algorithm and options it declares (see
    ///     <see cref="LayoutEngine"/>), renders it to <paramref name="fileName"/> as PNG, and asserts the
    ///     result decodes as a valid raster image.
    /// </summary>
    /// <param name="fileName">Stable output filename (for example <c>layered-pipeline.png</c>).</param>
    /// <param name="graph">
    /// The graph to lay out. Configure it directly (for example
    /// <c>graph.Set(CoreOptions.Algorithm, "layered")</c>) before calling this method.
    /// </param>
    /// <param name="theme">The theme to render with.</param>
    public static void Png(string fileName, LayoutGraph graph, Theme theme)
    {
        var tree = LayoutEngine.Layout(graph);
        AssertMarkedEndsOnBoundaryOrPort(tree, fileName);
        LayoutGeometryAssert.AssertCanvasMarginsWithin(tree, name: fileName);
        LayoutGeometryAssert.AssertMarkersClearContainerBorders(tree, fileName);
        var path = Path.Combine(GalleryOutput.ResolveDirectory(), fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using (var stream = File.Create(path))
        {
            PngRenderer.Render(tree, new RenderOptions(theme), stream);
        }

        AssertValidPng(path);
    }

    /// <summary>
    ///     Renders a pre-built <paramref name="tree"/> directly to <paramref name="fileName"/> as PNG,
    ///     bypassing <see cref="LayoutEngine.Layout(LayoutGraph)"/> entirely, and asserts the result
    ///     decodes as a valid raster image. Like the matching <see cref="Svg(string, LayoutTree, Theme)"/>
    ///     overload, it must be used only by diagrams belonging to the <c>custom-rendering</c> gallery
    ///     group.
    /// </summary>
    /// <param name="fileName">Stable output filename (for example <c>custom-rendering/my-diagram.png</c>).</param>
    /// <param name="tree">The already-laid-out tree to render.</param>
    /// <param name="theme">The theme to render with.</param>
    public static void Png(string fileName, LayoutTree tree, Theme theme)
    {
        var path = Path.Combine(GalleryOutput.ResolveDirectory(), fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using (var stream = File.Create(path))
        {
            PngRenderer.Render(tree, new RenderOptions(theme), stream);
        }

        AssertValidPng(path);
    }

    /// <summary>
    ///     Lays out <paramref name="graph"/> with whatever algorithm and options it declares (see
    ///     <see cref="LayoutEngine"/>), renders it to <paramref name="fileName"/> as JPEG, and asserts
    ///     the result is a valid raster image.
    /// </summary>
    /// <param name="fileName">Stable output filename (for example <c>layered-pipeline.jpeg</c>).</param>
    /// <param name="graph">
    /// The graph to lay out. Configure it directly (for example
    /// <c>graph.Set(CoreOptions.Algorithm, "layered")</c>) before calling this method.
    /// </param>
    /// <param name="theme">The theme to render with.</param>
    public static void Jpeg(string fileName, LayoutGraph graph, Theme theme)
    {
        var tree = LayoutEngine.Layout(graph);
        var path = Path.Combine(GalleryOutput.ResolveDirectory(), fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using (var stream = File.Create(path))
        {
            JpegRenderer.Render(tree, new RenderOptions(theme), stream);
        }

        AssertValidJpeg(path);
    }

    /// <summary>
    ///     Asserts the layout invariant behind demaconsulting/Rendering#49: every connector end that carries an
    ///     end marker lies on a box boundary (the box face the tip must sit on) or on a port. A marker end
    ///     floating off a face, or inside a box, would make the painted tip overlap or miss its target.
    /// </summary>
    /// <param name="tree">The laid-out tree to inspect.</param>
    /// <param name="fileName">The gallery file the tree is rendered to (for failure messages).</param>
    private static void AssertMarkedEndsOnBoundaryOrPort(LayoutTree tree, string fileName)
    {
        const double tolerance = 0.01;
        var boxes = new List<LayoutBox>();
        var lines = new List<LayoutLine>();
        var ports = new List<LayoutPort>();

        void Collect(IEnumerable<LayoutNode> nodes)
        {
            foreach (var node in nodes)
            {
                switch (node)
                {
                    case LayoutBox box:
                        boxes.Add(box);
                        Collect(box.Children);
                        break;
                    case LayoutLine line:
                        lines.Add(line);
                        break;
                    case LayoutPort port:
                        ports.Add(port);
                        break;
                }
            }
        }

        Collect(tree.Nodes);

        // A connector that leaves the canvas is clipped by every renderer.
        foreach (var line in lines)
        {
            foreach (var point in line.Waypoints)
            {
                Assert.True(
                    point.X >= -tolerance && point.X <= tree.Width + tolerance && point.Y >= -tolerance && point.Y <= tree.Height + tolerance,
                    $"{fileName}: connector waypoint {point} lies outside the {tree.Width}x{tree.Height} canvas.");
            }
        }

        bool OnBoundary(Point2D point) =>
            ports.Any(port => Math.Abs(port.CentreX - point.X) <= tolerance && Math.Abs(port.CentreY - point.Y) <= tolerance)
            || boxes.Any(box => OnBoxSurface(box, point, tolerance));

        foreach (var line in lines.Where(line => line.Waypoints.Count >= 2))
        {
            if (line.SourceEnd != EndMarkerStyle.None)
            {
                Assert.True(OnBoundary(line.Waypoints[0]), $"{fileName}: the {line.SourceEnd} source end {line.Waypoints[0]} is not on a box boundary or port.");
            }

            if (line.TargetEnd != EndMarkerStyle.None)
            {
                Assert.True(OnBoundary(line.Waypoints[^1]), $"{fileName}: the {line.TargetEnd} target end {line.Waypoints[^1]} is not on a box boundary or port.");
            }
        }
    }

    /// <summary>
    ///     Returns whether <paramref name="point"/> lies on the outline of <paramref name="box"/>. A folder
    ///     also has its body top (below the tab) as an outline edge, which is where connectors anchor on a
    ///     folder's top face, and a diamond's outline is the rhombus through the box edge midpoints.
    /// </summary>
    private static bool OnBoxSurface(LayoutBox box, Point2D point, double tolerance)
    {
        var withinX = point.X >= box.X - tolerance && point.X <= box.X + box.Width + tolerance;
        var withinY = point.Y >= box.Y - tolerance && point.Y <= box.Y + box.Height + tolerance;
        var onVerticalEdge = (Math.Abs(point.X - box.X) <= tolerance || Math.Abs(point.X - (box.X + box.Width)) <= tolerance) && withinY;
        var onHorizontalEdge = (Math.Abs(point.Y - box.Y) <= tolerance || Math.Abs(point.Y - (box.Y + box.Height)) <= tolerance) && withinX;
        var onFolderBodyTop = box.Shape == BoxShape.Folder
            && Math.Abs(point.Y - (box.Y + BoxMetrics.ResolveFolderTabHeight(box, Themes.Light))) <= tolerance
            && withinX;
        var onDiamond = box.Shape == BoxShape.Diamond && withinX && withinY
            && Math.Abs((Math.Abs(point.X - (box.X + (box.Width / 2.0))) / (box.Width / 2.0)) + (Math.Abs(point.Y - (box.Y + (box.Height / 2.0))) / (box.Height / 2.0)) - 1.0)
                * (Math.Min(box.Width, box.Height) / 2.0) <= 0.6;
        return onVerticalEdge || onHorizontalEdge || onFolderBodyTop || onDiamond;
    }

    /// <summary>Asserts that the file exists, is non-empty, and contains a well-formed SVG document.</summary>
    /// <param name="path">Absolute path of the generated SVG file.</param>
    public static void AssertValidSvg(string path)
    {
        Assert.True(File.Exists(path), $"Expected SVG file to exist: {path}");
        var content = File.ReadAllText(path, Encoding.UTF8);
        Assert.False(string.IsNullOrWhiteSpace(content), $"SVG file is empty: {path}");
        Assert.Contains("<svg", content, StringComparison.Ordinal);
        Assert.Contains("</svg>", content, StringComparison.Ordinal);
    }

    /// <summary>Asserts that the file exists, starts with the PNG signature, and decodes to a bitmap.</summary>
    /// <param name="path">Absolute path of the generated PNG file.</param>
    public static void AssertValidPng(string path)
    {
        Assert.True(File.Exists(path), $"Expected PNG file to exist: {path}");
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > PngSignature.Length, $"PNG file is too small: {path}");
        Assert.True(
            bytes.Take(PngSignature.Length).SequenceEqual(PngSignature),
            $"File does not start with the PNG signature: {path}");

        using var surface = PngCodec.Load(path);
        Assert.True(surface.Width > 0 && surface.Height > 0, $"Decoded PNG has no pixels: {path}");
    }

    /// <summary>Asserts that the file exists, starts with the JPEG signature, and decodes to a bitmap.</summary>
    /// <param name="path">Absolute path of the generated JPEG file.</param>
    public static void AssertValidJpeg(string path)
    {
        Assert.True(File.Exists(path), $"Expected JPEG file to exist: {path}");
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > JpegSignature.Length, $"JPEG file is too small: {path}");
        Assert.True(
            bytes.Take(JpegSignature.Length).SequenceEqual(JpegSignature),
            $"File does not start with the JPEG signature: {path}");

        using var surface = JpegCodec.Load(path);
        Assert.True(surface.Width > 0 && surface.Height > 0, $"Decoded JPEG has no pixels: {path}");
    }
}
