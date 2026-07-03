// <copyright file="EdgeRoutingOptionTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering;
using DemaConsulting.Rendering.Layout;

namespace DemaConsulting.Rendering.Layout.Tests;

/// <summary>
///     Tests for the <see cref="EdgeRouting"/> routing-style option: the <see cref="CoreOptions.EdgeRouting"/>
///     property key, its participation in the open property system, and the
///     <see cref="ConnectorRouteOptions"/> defaults.
/// </summary>
public sealed class EdgeRoutingOptionTests
{
    /// <summary>
    ///     The <see cref="CoreOptions.EdgeRouting"/> key defaults to
    ///     <see cref="EdgeRouting.Orthogonal"/>, the only shipped routing style.
    /// </summary>
    [Fact]
    public void CoreOptions_EdgeRouting_DefaultValue_IsOrthogonal()
    {
        // Assert: the well-known key advertises the orthogonal default
        Assert.Equal(EdgeRouting.Orthogonal, CoreOptions.EdgeRouting.DefaultValue);
    }

    /// <summary>
    ///     The <see cref="CoreOptions.EdgeRouting"/> key carries the ELK-flavored identifier
    ///     <c>rendering.edgerouting</c>.
    /// </summary>
    [Fact]
    public void CoreOptions_EdgeRouting_Id_IsStableDottedIdentifier()
    {
        // Assert: the stable dotted id mirrors elk.edgeRouting
        Assert.Equal("rendering.edgerouting", CoreOptions.EdgeRouting.Id);
    }

    /// <summary>
    ///     The routing style rides the open property system: it can be selected per scope by setting
    ///     the key on any <see cref="IPropertyHolder"/> and read back, exactly like the algorithm key.
    /// </summary>
    [Fact]
    public void CoreOptions_EdgeRouting_SetThenGet_RoundTripsValue()
    {
        // Arrange: a free-standing options holder
        var options = new LayoutOptions();

        // Act: select the routing style on the holder, then read it back
        options.Set(CoreOptions.EdgeRouting, EdgeRouting.Orthogonal);

        // Assert: the value round-trips through the property system
        Assert.True(options.Contains(CoreOptions.EdgeRouting));
        Assert.Equal(EdgeRouting.Orthogonal, options.Get(CoreOptions.EdgeRouting));
    }

    /// <summary>
    ///     An unset holder returns the orthogonal default for the routing key, so callers that never
    ///     select a style still route orthogonally.
    /// </summary>
    [Fact]
    public void CoreOptions_EdgeRouting_UnsetHolder_ReturnsOrthogonalDefault()
    {
        // Arrange: an options holder with no routing selection
        var options = new LayoutOptions();

        // Assert: the key falls back to its orthogonal default
        Assert.False(options.Contains(CoreOptions.EdgeRouting));
        Assert.Equal(EdgeRouting.Orthogonal, options.Get(CoreOptions.EdgeRouting));
    }

    /// <summary>
    ///     <see cref="ConnectorRouteOptions"/> defaults to orthogonal routing with a 12-pixel
    ///     clearance, and callers can override the clearance.
    /// </summary>
    [Fact]
    public void ConnectorRouteOptions_Constructor_Defaults_AreOrthogonalWithTwelvePixelClearance()
    {
        // Act: construct with defaults, then with an explicit clearance override
        var defaults = new ConnectorRouteOptions();
        var overridden = new ConnectorRouteOptions(Clearance: 20.0);

        // Assert: orthogonal default style, 12px default clearance, and honored override
        Assert.Equal(EdgeRouting.Orthogonal, defaults.EdgeRouting);
        Assert.Equal(12.0, defaults.Clearance);
        Assert.Equal(20.0, overridden.Clearance);
    }
}
