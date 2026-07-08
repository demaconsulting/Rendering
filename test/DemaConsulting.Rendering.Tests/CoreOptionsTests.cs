// <copyright file="CoreOptionsTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Tests;

/// <summary>
///     Tests for the new Phase 1 parallel-edge/port <see cref="CoreOptions"/> keys: default values and
///     independent settability at both graph and free-standing <see cref="LayoutOptions"/> scope.
/// </summary>
public sealed class CoreOptionsTests
{
    /// <summary>
    ///     Proves that <see cref="CoreOptions.MergeParallelEdges"/> defaults to <see langword="true"/>,
    ///     preserving the pre-existing (pre-option) collapse-to-one-line behavior for any caller that
    ///     never sets it.
    /// </summary>
    [Fact]
    public void MergeParallelEdges_DefaultValue_IsTrue()
    {
        Assert.True(CoreOptions.MergeParallelEdges.DefaultValue);
    }

    /// <summary>
    ///     Proves that <see cref="CoreOptions.MergeParallelEdges"/> is settable on a graph and reads
    ///     back the explicit value.
    /// </summary>
    [Fact]
    public void MergeParallelEdges_SetOnGraph_ReadsBackExplicitValue()
    {
        var graph = new LayoutGraph();
        graph.Set(CoreOptions.MergeParallelEdges, false);

        Assert.False(graph.Get(CoreOptions.MergeParallelEdges));
    }

    /// <summary>
    ///     Proves that <see cref="CoreOptions.AssumedFontSize"/> defaults to <c>12.0</c>, matching
    ///     the renderer theme's own default body font size.
    /// </summary>
    [Fact]
    public void AssumedFontSize_DefaultValue_Is12()
    {
        Assert.Equal(12.0, CoreOptions.AssumedFontSize.DefaultValue);
    }

    /// <summary>
    ///     Proves that <see cref="CoreOptions.AssumedFontSize"/> is settable on a free-standing
    ///     <see cref="LayoutOptions"/> and reads back the explicit value.
    /// </summary>
    [Fact]
    public void AssumedFontSize_SetOnOptions_ReadsBackExplicitValue()
    {
        var options = new LayoutOptions();
        options.Set(CoreOptions.AssumedFontSize, 16.0);

        Assert.Equal(16.0, options.Get(CoreOptions.AssumedFontSize));
    }
}
