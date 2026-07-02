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
    private static readonly LayoutProperty<string?> Name = new("test.name", "default");

    /// <summary>
    ///     Proves that reading an unset property returns the property's declared default.
    /// </summary>
    [Fact]
    public void Get_UnsetProperty_ReturnsDefault()
    {
        // Arrange: create an empty holder
        var holder = new PropertyHolder();

        // Act / Assert: an unset property yields its declared default
        Assert.Equal(7, holder.Get(Count));
    }

    /// <summary>
    ///     Proves that a value written with Set is returned by a subsequent Get.
    /// </summary>
    [Fact]
    public void Get_AfterSet_ReturnsStoredValue()
    {
        // Arrange: create an empty holder
        var holder = new PropertyHolder();

        // Act: store a value
        holder.Set(Count, 42);

        // Assert: the stored value is returned
        Assert.Equal(42, holder.Get(Count));
    }

    /// <summary>
    ///     Proves that Contains reflects whether a property has been explicitly set.
    /// </summary>
    [Fact]
    public void Contains_ReflectsExplicitSet()
    {
        // Arrange: create an empty holder
        var holder = new PropertyHolder();

        // Act / Assert: Contains flips from false to true once the property is set
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
        // Arrange: create an empty holder
        var holder = new PropertyHolder();

        // Act: attempt to read an unset property
        var found = holder.TryGet(Count, out var value);

        // Assert: the read reports failure and returns the default
        Assert.False(found);
        Assert.Equal(7, value);
    }

    /// <summary>
    ///     Proves that explicitly setting a reference-type property to null is honored by Get,
    ///     returning null rather than the property's non-null default.
    /// </summary>
    [Fact]
    public void Get_AfterSetNull_ReturnsNull()
    {
        // Arrange: create a holder and explicitly clear a property to null
        var holder = new PropertyHolder();
        holder.Set(Name, null);

        // Act / Assert: the explicit null is returned, not the "default" fallback
        Assert.Null(holder.Get(Name));
    }

    /// <summary>
    ///     Proves that TryGet treats an explicitly-stored null as present, reporting true with a
    ///     null value, so it stays consistent with Contains.
    /// </summary>
    [Fact]
    public void TryGet_AfterSetNull_ReturnsTrueAndNull()
    {
        // Arrange: create a holder and explicitly clear a property to null
        var holder = new PropertyHolder();
        holder.Set(Name, null);

        // Act: read the property back
        var found = holder.TryGet(Name, out var value);

        // Assert: the null is reported as present
        Assert.True(found);
        Assert.Null(value);
    }

    /// <summary>
    ///     Proves that Contains reports true for a property explicitly set to null, matching TryGet.
    /// </summary>
    [Fact]
    public void Contains_AfterSetNull_ReturnsTrue()
    {
        // Arrange: create a holder and explicitly clear a property to null
        var holder = new PropertyHolder();
        holder.Set(Name, null);

        // Act / Assert: the key is considered present
        Assert.True(holder.Contains(Name));
    }

    /// <summary>
    ///     Proves that Get rejects a null property argument.
    /// </summary>
    [Fact]
    public void Get_NullProperty_ThrowsArgumentNullException()
    {
        // Arrange: create an empty holder
        var holder = new PropertyHolder();

        // Act / Assert: a null property argument is rejected
        Assert.Throws<ArgumentNullException>(() => holder.Get<int>(null!));
    }

    /// <summary>
    ///     Proves that TryGet rejects a null property argument.
    /// </summary>
    [Fact]
    public void TryGet_NullProperty_ThrowsArgumentNullException()
    {
        // Arrange: create an empty holder
        var holder = new PropertyHolder();

        // Act / Assert: a null property argument is rejected
        Assert.Throws<ArgumentNullException>(() => holder.TryGet<int>(null!, out _));
    }

    /// <summary>
    ///     Proves that Set rejects a null property argument.
    /// </summary>
    [Fact]
    public void Set_NullProperty_ThrowsArgumentNullException()
    {
        // Arrange: create an empty holder
        var holder = new PropertyHolder();

        // Act / Assert: a null property argument is rejected
        Assert.Throws<ArgumentNullException>(() => holder.Set<int>(null!, 1));
    }

    /// <summary>
    ///     Proves that Contains rejects a null property argument.
    /// </summary>
    [Fact]
    public void Contains_NullProperty_ThrowsArgumentNullException()
    {
        // Arrange: create an empty holder
        var holder = new PropertyHolder();

        // Act / Assert: a null property argument is rejected
        Assert.Throws<ArgumentNullException>(() => holder.Contains<int>(null!));
    }
}
