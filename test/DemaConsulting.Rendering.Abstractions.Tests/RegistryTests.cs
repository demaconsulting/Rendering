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
    ///     Proves that a registered algorithm can be found by identifier.
    /// </summary>
    [Fact]
    public void LayoutAlgorithmRegistry_RegisterThenResolve_ReturnsAlgorithm()
    {
        // Arrange
        var registry = new LayoutAlgorithmRegistry();

        // Act
        registry.Register(new FakeAlgorithm());

        // Assert
        Assert.True(registry.Contains("fake"));
        Assert.Equal("fake", registry.Resolve("fake").Id);
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
    ///     Proves that a registered renderer can be found by media type.
    /// </summary>
    [Fact]
    public void RendererRegistry_RegisterThenResolve_ReturnsRenderer()
    {
        // Arrange
        var registry = new RendererRegistry();

        // Act
        registry.Register(new FakeRenderer());

        // Assert
        Assert.True(registry.Contains("text/plain"));
        Assert.Equal("text/plain", registry.Resolve("text/plain").MediaType);
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

    private sealed class FakeAlgorithm : ILayoutAlgorithm
    {
        public string Id => "fake";

        public LayoutTree Apply(LayoutGraph graph, LayoutOptions options) => new(0, 0, []);
    }

    private sealed class FakeRenderer : IRenderer
    {
        public string MediaType => "text/plain";

        public string DefaultExtension => ".txt";

        public IReadOnlyList<string> FileExtensions => [".txt", ".text"];

        public void Render(LayoutTree layout, RenderOptions options, Stream output)
        {
            // No-op fake used only to exercise registry lookup.
        }
    }
}
