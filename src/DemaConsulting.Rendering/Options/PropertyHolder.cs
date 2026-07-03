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
        return TryGet(property, out var value) ? value : property.DefaultValue;
    }

    /// <inheritdoc/>
    public bool TryGet<TValue>(LayoutProperty<TValue> property, out TValue value)
    {
        ArgumentNullException.ThrowIfNull(property);
        if (_values.TryGetValue(property.Id, out var stored))
        {
            // A present key counts as explicitly set, even when the stored value is null
            // (for reference or nullable value types). This keeps Contains, TryGet, and Get
            // consistent: setting a property to null is honored rather than folded into the default.
            if (stored is TValue typed)
            {
                value = typed;
                return true;
            }

            if (stored is null)
            {
                value = default!;
                return true;
            }
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

    /// <summary>
    /// Builds a new <see cref="LayoutOptions"/> snapshot that merges <paramref name="parent"/>'s
    /// explicitly-set values with this holder's own, with this holder's own values taking precedence.
    /// This is the generic cascading primitive behind option inheritance: a caller resolving a nested
    /// scope's effective options overlays each level's own overrides onto its parent's already-resolved
    /// snapshot, nearest-ancestor-wins, without needing to know which properties exist. Because the
    /// merge copies raw, boxed values by key rather than reading through <see cref="LayoutProperty{T}"/>
    /// accessors, it works for any current or future property — including ones this holder or its
    /// caller has never heard of — with no per-property code.
    /// </summary>
    /// <param name="parent">
    /// The base holder whose explicitly-set values are used wherever this holder has not set its own.
    /// </param>
    /// <returns>
    /// A new <see cref="LayoutOptions"/> containing <paramref name="parent"/>'s values overlaid by this
    /// holder's own values.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parent"/> is <see langword="null"/>.</exception>
    public LayoutOptions OverlayOnto(PropertyHolder parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var effective = new LayoutOptions();
        foreach (var kvp in parent._values)
        {
            effective._values[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in _values)
        {
            effective._values[kvp.Key] = kvp.Value;
        }

        return effective;
    }
}
