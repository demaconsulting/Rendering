// <copyright file="LayoutAlgorithmsTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests for the <see cref="LayoutAlgorithms"/> default-registry factory, proving it registers each
///     bundled algorithm under its stable identifier and returns an independently mutable instance on
///     every call.
/// </summary>
public sealed class LayoutAlgorithmsTests
{
    /// <summary>
    ///     Proves the default registry resolves the bundled layered algorithm by its identifier.
    /// </summary>
    [Fact]
    public void CreateDefaultRegistry_ResolvesLayeredAlgorithm()
    {
        // Arrange
        var registry = LayoutAlgorithms.CreateDefaultRegistry();

        // Act
        var algorithm = registry.Resolve("layered");

        // Assert
        Assert.IsType<LayeredLayoutAlgorithm>(algorithm);
    }

    /// <summary>
    ///     Proves the default registry resolves the bundled containment algorithm by its identifier.
    /// </summary>
    [Fact]
    public void CreateDefaultRegistry_ResolvesContainmentAlgorithm()
    {
        // Arrange
        var registry = LayoutAlgorithms.CreateDefaultRegistry();

        // Act
        var algorithm = registry.Resolve("containment");

        // Assert
        Assert.IsType<ContainmentLayoutAlgorithm>(algorithm);
    }

    /// <summary>
    ///     Proves the default registry resolves the bundled hierarchical algorithm by its identifier.
    /// </summary>
    [Fact]
    public void CreateDefaultRegistry_ResolvesHierarchicalAlgorithm()
    {
        // Arrange
        var registry = LayoutAlgorithms.CreateDefaultRegistry();

        // Act
        var algorithm = registry.Resolve("hierarchical");

        // Assert
        Assert.IsType<HierarchicalLayoutAlgorithm>(algorithm);
    }

    /// <summary>
    ///     Proves the default registry resolves the bundled auto meta-algorithm by its identifier.
    /// </summary>
    [Fact]
    public void CreateDefaultRegistry_ResolvesAutoAlgorithm()
    {
        // Arrange
        var registry = LayoutAlgorithms.CreateDefaultRegistry();

        // Act
        var algorithm = registry.Resolve("auto");

        // Assert
        Assert.IsType<AutoLayoutAlgorithm>(algorithm);
    }

    /// <summary>
    ///     Proves the default registry contains exactly the four bundled algorithm identifiers.
    /// </summary>
    [Fact]
    public void CreateDefaultRegistry_RegistersOnlyTheFourBundledAlgorithms()
    {
        // Arrange
        var registry = LayoutAlgorithms.CreateDefaultRegistry();

        // Act
        var ids = registry.Ids.OrderBy(id => id, StringComparer.Ordinal).ToArray();

        // Assert
        Assert.Equal(["auto", "containment", "hierarchical", "layered"], ids);
    }

    /// <summary>
    ///     Proves each call returns an independent registry, so registering into one does not affect a
    ///     registry from a separate call.
    /// </summary>
    [Fact]
    public void CreateDefaultRegistry_ReturnsIndependentInstances()
    {
        // Arrange
        var first = LayoutAlgorithms.CreateDefaultRegistry();
        var second = LayoutAlgorithms.CreateDefaultRegistry();

        // Act: register a stub only in the first registry
        first.Register(new StubAlgorithm());

        // Assert: the second registry is unaffected
        Assert.True(first.Contains("stub"));
        Assert.False(second.Contains("stub"));
    }

    /// <summary>A minimal <see cref="LayoutAlgorithmBase"/> stub used to prove registry independence.</summary>
    private sealed class StubAlgorithm : LayoutAlgorithmBase
    {
        public override string Id => "stub";

        protected internal override LayoutTree ApplyCore(LayoutGraph graph, LayoutOptions options) =>
            new(1, 1, []);
    }
}
