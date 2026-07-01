// <copyright file="PropertyHolderTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Tests;

/// <summary>
///     Tests for the ELK-style open property system (<see cref="PropertyHolder"/> and
///     <see cref="LayoutProperty{T}"/>).
/// </summary>
public class PropertyHolderTests
{
    private static readonly LayoutProperty<int> Count = new("test.count", 7);

    /// <summary>
    ///     Proves that reading an unset property returns the property's declared default.
    /// </summary>
    [Fact]
    public void Get_UnsetProperty_ReturnsDefault()
    {
        var holder = new PropertyHolder();

        Assert.Equal(7, holder.Get(Count));
    }

    /// <summary>
    ///     Proves that a value written with Set is returned by a subsequent Get.
    /// </summary>
    [Fact]
    public void Get_AfterSet_ReturnsStoredValue()
    {
        var holder = new PropertyHolder();

        holder.Set(Count, 42);

        Assert.Equal(42, holder.Get(Count));
    }

    /// <summary>
    ///     Proves that Contains reflects whether a property has been explicitly set.
    /// </summary>
    [Fact]
    public void Contains_ReflectsExplicitSet()
    {
        var holder = new PropertyHolder();

        Assert.False(holder.Contains(Count));
        holder.Set(Count, 1);
        Assert.True(holder.Contains(Count));
    }

    /// <summary>
    ///     Proves that TryGet reports false and yields the default for an unset property.
    /// </summary>
    [Fact]
    public void TryGet_UnsetProperty_ReturnsFalseAndDefault()
    {
        var holder = new PropertyHolder();

        var found = holder.TryGet(Count, out var value);

        Assert.False(found);
        Assert.Equal(7, value);
    }
}
