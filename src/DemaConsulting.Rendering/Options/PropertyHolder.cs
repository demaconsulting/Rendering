// <copyright file="PropertyHolder.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// Dictionary-backed base implementation of <see cref="IPropertyHolder"/>. Values are stored keyed
/// by <see cref="LayoutProperty{T}.Id"/>, so unknown or not-yet-honored properties are carried
/// harmlessly and simply ignored by algorithms that do not read them.
/// </summary>
public class PropertyHolder : IPropertyHolder
{
    private readonly Dictionary<string, object?> _values = [];

    /// <inheritdoc/>
    public TValue Get<TValue>(LayoutProperty<TValue> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return _values.TryGetValue(property.Id, out var stored) && stored is TValue typed
            ? typed
            : property.DefaultValue;
    }

    /// <inheritdoc/>
    public bool TryGet<TValue>(LayoutProperty<TValue> property, out TValue value)
    {
        ArgumentNullException.ThrowIfNull(property);
        if (_values.TryGetValue(property.Id, out var stored) && stored is TValue typed)
        {
            value = typed;
            return true;
        }

        value = property.DefaultValue;
        return false;
    }

    /// <inheritdoc/>
    public IPropertyHolder Set<TValue>(LayoutProperty<TValue> property, TValue value)
    {
        ArgumentNullException.ThrowIfNull(property);
        _values[property.Id] = value;
        return this;
    }

    /// <inheritdoc/>
    public bool Contains<TValue>(LayoutProperty<TValue> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return _values.ContainsKey(property.Id);
    }
}
