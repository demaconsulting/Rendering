// <copyright file="LayoutProperty.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// A strongly-typed, uniquely-identified configuration key used to read and write values on an
/// <see cref="IPropertyHolder"/>. This is the ELK-inspired open-configuration primitive: new options
/// are introduced by declaring new <see cref="LayoutProperty{T}"/> constants, so the layout and
/// rendering contracts never change signature as configuration coverage grows.
/// </summary>
/// <typeparam name="T">Type of the value carried by this property.</typeparam>
public sealed class LayoutProperty<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutProperty{T}"/> class.
    /// </summary>
    /// <param name="id">
    /// Globally-unique identifier for the property, conventionally a dotted name such as
    /// <c>rendering.direction</c>. Used as the storage key and to compare properties for equality.
    /// </param>
    /// <param name="defaultValue">
    /// Value returned by <see cref="IPropertyHolder.Get{TValue}"/> when the property has not been set.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is empty.</exception>
    public LayoutProperty(string id, T defaultValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        Id = id;
        DefaultValue = defaultValue;
    }

    /// <summary>Gets the globally-unique identifier for this property.</summary>
    public string Id { get; }

    /// <summary>Gets the value returned when this property has not been explicitly set on a holder.</summary>
    public T DefaultValue { get; }
}
