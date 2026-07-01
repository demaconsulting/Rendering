// <copyright file="IPropertyHolder.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// An open, extensible bag of configuration values keyed by <see cref="LayoutProperty{T}"/>.
/// Layout graphs, their elements, and <see cref="LayoutOptions"/> all implement this contract so
/// that configuration can be attached at any granularity (whole graph, single node, or a shared
/// options object) without changing any method signature as new properties are introduced.
/// </summary>
public interface IPropertyHolder
{
    /// <summary>
    /// Gets the value stored for <paramref name="property"/>, or the property's
    /// <see cref="LayoutProperty{T}.DefaultValue"/> when it has not been set.
    /// </summary>
    /// <typeparam name="TValue">Type of the property value.</typeparam>
    /// <param name="property">The property to read.</param>
    /// <returns>The stored value, or the property default when unset.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="property"/> is <see langword="null"/>.</exception>
    TValue Get<TValue>(LayoutProperty<TValue> property);

    /// <summary>
    /// Attempts to read a previously-set value for <paramref name="property"/>.
    /// </summary>
    /// <typeparam name="TValue">Type of the property value.</typeparam>
    /// <param name="property">The property to read.</param>
    /// <param name="value">When this method returns <see langword="true"/>, the stored value.</param>
    /// <returns><see langword="true"/> when the property was explicitly set; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="property"/> is <see langword="null"/>.</exception>
    bool TryGet<TValue>(LayoutProperty<TValue> property, out TValue value);

    /// <summary>
    /// Sets the value for <paramref name="property"/>, replacing any previously-stored value.
    /// </summary>
    /// <typeparam name="TValue">Type of the property value.</typeparam>
    /// <param name="property">The property to write.</param>
    /// <param name="value">The value to store.</param>
    /// <returns>This holder, to support fluent chaining of multiple <c>Set</c> calls.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="property"/> is <see langword="null"/>.</exception>
    IPropertyHolder Set<TValue>(LayoutProperty<TValue> property, TValue value);

    /// <summary>
    /// Determines whether <paramref name="property"/> has been explicitly set on this holder.
    /// </summary>
    /// <typeparam name="TValue">Type of the property value.</typeparam>
    /// <param name="property">The property to test.</param>
    /// <returns><see langword="true"/> when the property was explicitly set; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="property"/> is <see langword="null"/>.</exception>
    bool Contains<TValue>(LayoutProperty<TValue> property);
}
