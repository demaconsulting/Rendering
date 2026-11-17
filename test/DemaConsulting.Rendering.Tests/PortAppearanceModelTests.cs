// <copyright file="PortAppearanceModelTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Tests;

/// <summary>
///     Tests for the port appearance members of the layout model: <see cref="PortAppearance"/> and its
///     enumerations, the optional <see cref="LayoutPort.Appearance"/> member, and the matching
///     <see cref="LayoutGraphPort.Appearance"/> property. Every default is the legacy look.
/// </summary>
public sealed class PortAppearanceModelTests
{
    /// <summary>A default appearance has no arrow, an automatic glyph, a solid outline, and the legacy label rule.</summary>
    [Fact]
    public void PortAppearance_Defaults_AreTheLegacyLook()
    {
        // Act
        var appearance = new PortAppearance();

        // Assert
        Assert.Equal(PortDirection.None, appearance.Direction);
        Assert.Equal(PortGlyph.Auto, appearance.Glyph);
        Assert.Equal(LineStyle.Solid, appearance.OutlineStyle);
        Assert.Equal(PortLabelPlacement.Auto, appearance.LabelPlacement);
    }

    /// <summary>The automatic glyph resolves to the legacy filled square unless an arrow or a dashed outline asks for more.</summary>
    /// <param name="direction">The direction arrow.</param>
    /// <param name="style">The outline style.</param>
    /// <param name="expected">The expected resolved glyph.</param>
    [Theory]
    [InlineData(PortDirection.None, LineStyle.Solid, PortGlyph.Filled)]
    [InlineData(PortDirection.In, LineStyle.Solid, PortGlyph.Outlined)]
    [InlineData(PortDirection.Out, LineStyle.Solid, PortGlyph.Outlined)]
    [InlineData(PortDirection.InOut, LineStyle.Solid, PortGlyph.Outlined)]
    [InlineData(PortDirection.None, LineStyle.Dashed, PortGlyph.Outlined)]
    [InlineData(PortDirection.None, LineStyle.Dotted, PortGlyph.Outlined)]
    public void PortAppearance_ResolvedGlyph_Auto_PicksFilledOrOutlined(
        PortDirection direction,
        LineStyle style,
        PortGlyph expected)
    {
        // Act
        var appearance = new PortAppearance(direction, PortGlyph.Auto, style);

        // Assert
        Assert.Equal(expected, appearance.ResolvedGlyph);
    }

    /// <summary>An explicit glyph is never overridden by the automatic rule.</summary>
    [Fact]
    public void PortAppearance_ResolvedGlyph_Explicit_IsKept()
    {
        Assert.Equal(PortGlyph.Proxy, new PortAppearance(PortDirection.In, PortGlyph.Proxy).ResolvedGlyph);
        Assert.Equal(PortGlyph.Filled, new PortAppearance(PortDirection.InOut, PortGlyph.Filled).ResolvedGlyph);
        Assert.Equal(PortGlyph.Outlined, new PortAppearance(PortDirection.None, PortGlyph.Outlined).ResolvedGlyph);
    }

    /// <summary>The appearance is a value record that supports <c>with</c>.</summary>
    [Fact]
    public void PortAppearance_IsAValueRecord()
    {
        // Arrange
        var a = new PortAppearance(PortDirection.In, LabelPlacement: PortLabelPlacement.Outward);

        // Act
        var b = a with { OutlineStyle = LineStyle.Dashed };

        // Assert
        Assert.Equal(new PortAppearance(PortDirection.In, LabelPlacement: PortLabelPlacement.Outward), a);
        Assert.NotEqual(a, b);
        Assert.Equal(LineStyle.Dashed, b.OutlineStyle);
    }

    /// <summary>A port built with only its legacy members has no appearance.</summary>
    [Fact]
    public void LayoutPort_Appearance_DefaultsToNull()
    {
        // Act
        var port = new LayoutPort(10, 20, PortSide.Left, "p");

        // Assert
        Assert.Null(port.Appearance);
    }

    /// <summary>A port carries the appearance it was constructed with.</summary>
    [Fact]
    public void LayoutPort_Appearance_IsCarried()
    {
        // Arrange
        var appearance = new PortAppearance(PortDirection.Out);

        // Act
        var port = new LayoutPort(10, 20, PortSide.Right, "p", Appearance: appearance);

        // Assert
        Assert.Same(appearance, port.Appearance);
    }

    /// <summary>A graph port has no appearance until it is set.</summary>
    [Fact]
    public void LayoutGraphPort_Appearance_DefaultsToNullAndIsSettable()
    {
        // Arrange
        var graph = new LayoutGraph();
        var node = graph.AddNode("n", 100, 50);
        var port = node.Ports.AddPort("p");

        // Assert default
        Assert.Null(port.Appearance);

        // Act
        port.Appearance = new PortAppearance(PortDirection.InOut, PortGlyph.Outlined, LineStyle.Dashed);

        // Assert
        Assert.Equal(new PortAppearance(PortDirection.InOut, PortGlyph.Outlined, LineStyle.Dashed), port.Appearance);
    }
}
