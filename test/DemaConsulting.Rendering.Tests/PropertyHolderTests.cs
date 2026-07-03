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

    /// <summary>
    ///     Proves that overlaying an empty holder onto a populated parent returns the parent's values
    ///     unchanged, so a scope with no overrides of its own fully inherits its parent's snapshot.
    /// </summary>
    [Fact]
    public void OverlayOnto_EmptyHolderOntoPopulatedParent_ReturnsParentValuesUnchanged()
    {
        // Arrange: a populated parent and an empty overlaying holder
        var parent = new PropertyHolder();
        parent.Set(Count, 42);
        parent.Set(Name, "parent-name");
        var holder = new PropertyHolder();

        // Act: overlay the empty holder onto the populated parent
        var effective = holder.OverlayOnto(parent);

        // Assert: the parent's values pass through unchanged
        Assert.Equal(42, effective.Get(Count));
        Assert.Equal("parent-name", effective.Get(Name));
    }

    /// <summary>
    ///     Proves that the overlaying holder's own explicit value wins over the parent's value for the
    ///     same property, the core precedence rule behind nearest-ancestor-override cascading.
    /// </summary>
    [Fact]
    public void OverlayOnto_HolderOverridesProperty_HolderValueWins()
    {
        // Arrange: a parent with a value, and an overlaying holder that overrides it
        var parent = new PropertyHolder();
        parent.Set(Count, 1);
        var holder = new PropertyHolder();
        holder.Set(Count, 99);

        // Act: overlay the holder onto the parent
        var effective = holder.OverlayOnto(parent);

        // Assert: the overlaying holder's own value takes precedence
        Assert.Equal(99, effective.Get(Count));
    }

    /// <summary>
    ///     Proves that a value present only on the parent (and not overridden by the overlaying holder)
    ///     passes through into the effective snapshot.
    /// </summary>
    [Fact]
    public void OverlayOnto_ValueOnlyOnParent_PassesThrough()
    {
        // Arrange: a parent with a value the overlaying holder never sets
        var parent = new PropertyHolder();
        parent.Set(Name, "only-on-parent");
        var holder = new PropertyHolder();
        holder.Set(Count, 5);

        // Act: overlay the holder onto the parent
        var effective = holder.OverlayOnto(parent);

        // Assert: both the parent-only and holder-only values are present in the merged snapshot
        Assert.Equal("only-on-parent", effective.Get(Name));
        Assert.Equal(5, effective.Get(Count));
    }

    /// <summary>
    ///     Proves that OverlayOnto is fully generic: it merges an arbitrary custom property that is not
    ///     part of CoreOptions, without any per-property code.
    /// </summary>
    [Fact]
    public void OverlayOnto_CustomPropertyNotInCoreOptions_IsMerged()
    {
        // Arrange: a custom property unknown to CoreOptions, overridden by the overlaying holder
        var custom = new LayoutProperty<double>("custom.arbitrary.property", 0.0);
        var parent = new PropertyHolder();
        parent.Set(custom, 1.5);
        var holder = new PropertyHolder();
        holder.Set(custom, 2.5);

        // Act: overlay the holder onto the parent
        var effective = holder.OverlayOnto(parent);

        // Assert: the overlaying holder's value for the arbitrary property wins
        Assert.Equal(2.5, effective.Get(custom));
    }

    /// <summary>
    ///     Proves that OverlayOnto rejects a null parent.
    /// </summary>
    [Fact]
    public void OverlayOnto_NullParent_ThrowsArgumentNullException()
    {
        // Arrange: create an empty holder
        var holder = new PropertyHolder();

        // Act / Assert: a null parent argument is rejected
        Assert.Throws<ArgumentNullException>(() => holder.OverlayOnto(null!));
    }
}
