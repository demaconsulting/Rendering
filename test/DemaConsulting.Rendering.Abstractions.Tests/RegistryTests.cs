// <copyright file="RegistryTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;

namespace DemaConsulting.Rendering.Abstractions.Tests;

/// <summary>
///     Tests for the pluggable <see cref="LayoutAlgorithmRegistry"/> and
///     <see cref="RendererRegistry"/> service-provider lookups.
/// </summary>
public class RegistryTests
{
    /// <summary>
    ///     Proves that a registered algorithm can be found by identifier and that resolving it returns
    ///     an instance whose <see cref="LayoutAlgorithmBase.Apply(LayoutGraph)"/> can be invoked through
    ///     the contract to produce a placed layout tree.
    /// </summary>
    [Fact]
    public void LayoutAlgorithmRegistry_RegisterThenResolve_ReturnsAlgorithm()
    {
        // Arrange
        var registry = new LayoutAlgorithmRegistry();
        registry.Register(new FakeAlgorithm());

        // Act
        var algorithm = registry.Resolve("fake");
        var tree = algorithm.Apply(new LayoutGraph());

        // Assert: the algorithm is resolved by identifier and its Apply contract method can be invoked,
        // producing the placed layout tree it was implemented to return.
        Assert.True(registry.Contains("fake"));
        Assert.Equal("fake", algorithm.Id);
        Assert.Equal(FakeAlgorithm.PlacedWidth, tree.Width);
        Assert.Equal(FakeAlgorithm.PlacedHeight, tree.Height);
    }

    /// <summary>
    ///     Proves that resolving an unregistered algorithm throws.
    /// </summary>
    [Fact]
    public void LayoutAlgorithmRegistry_ResolveMissing_Throws()
    {
        // Arrange
        var registry = new LayoutAlgorithmRegistry();

        // Act / Assert
        Assert.Throws<KeyNotFoundException>(() => registry.Resolve("missing"));
    }

    /// <summary>
    ///     Proves that a registered renderer can be found by media type and that resolving it returns an
    ///     instance whose <see cref="IRenderer.Render"/> can be invoked through the contract to write
    ///     rendered output for a layout tree to a supplied stream.
    /// </summary>
    [Fact]
    public void RendererRegistry_RegisterThenResolve_ReturnsRenderer()
    {
        // Arrange
        var registry = new RendererRegistry();
        registry.Register(new FakeRenderer());
        using var output = new MemoryStream();

        // Act
        var renderer = registry.Resolve("text/plain");
        renderer.Render(new LayoutTree(0, 0, []), new RenderOptions(Themes.Light), output);

        // Assert: the renderer is resolved by media type and its Render contract method can be invoked,
        // writing the output it was implemented to produce to the caller's stream.
        Assert.True(registry.Contains("text/plain"));
        Assert.Equal("text/plain", renderer.MediaType);
        Assert.Equal(FakeRenderer.RenderedText, System.Text.Encoding.UTF8.GetString(output.ToArray()));
    }

    /// <summary>
    ///     Proves that a registered renderer can be found by any file extension it advertises,
    ///     with or without a leading dot and regardless of case.
    /// </summary>
    [Fact]
    public void RendererRegistry_ResolveByExtension_MatchesAdvertisedExtensions()
    {
        // Arrange
        var registry = new RendererRegistry();

        // Act
        registry.Register(new FakeRenderer());

        // Assert
        Assert.True(registry.ContainsExtension(".txt"));
        Assert.Equal("text/plain", registry.ResolveByExtension(".txt").MediaType);
        // Leading dot optional and matching is case-insensitive.
        Assert.Equal("text/plain", registry.ResolveByExtension("TEXT").MediaType);
    }

    private sealed class FakeAlgorithm : LayoutAlgorithmBase
    {
        public const double PlacedWidth = 42.0;
        public const double PlacedHeight = 24.0;

        public override string Id => "fake";

        protected internal override LayoutTree ApplyCore(LayoutGraph graph, LayoutOptions options) => new(PlacedWidth, PlacedHeight, []);
    }

    private sealed class FakeRenderer : IRenderer
    {
        public const string RenderedText = "fake-render-output";

        public string MediaType => "text/plain";

        public string DefaultExtension => ".txt";

        public IReadOnlyList<string> FileExtensions => [".txt", ".text"];

        public void Render(LayoutTree layout, RenderOptions options, Stream output)
        {
            // Write recognizable, deterministic output so tests can prove Render was actually invoked
            // through the contract rather than only resolved by media type.
            var bytes = System.Text.Encoding.UTF8.GetBytes(RenderedText);
            output.Write(bytes, 0, bytes.Length);
        }
    }
}
