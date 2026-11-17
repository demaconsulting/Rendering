// <copyright file="EndLabelModelTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Tests;

/// <summary>
///     Tests for the connection end-label members of the layout model: <see cref="LineEndLabel"/>, the
///     optional <see cref="LayoutLine.SourceEndLabel"/> and <see cref="LayoutLine.TargetEndLabel"/> members,
///     and the matching <see cref="LayoutGraphEdge"/> properties. Every member defaults to the legacy
///     look (no end labels), so a line built with only its original members is unchanged.
/// </summary>
public sealed class EndLabelModelTests
{
    private static LayoutLine Line() =>
        new([new Point2D(0, 0), new Point2D(100, 0)], EndMarkerStyle.None, EndMarkerStyle.None, LineStyle.Solid, null);

    /// <summary>A line built with its original members has no end labels.</summary>
    [Fact]
    public void LayoutLine_EndLabels_DefaultToNull()
    {
        // Act
        var line = Line();

        // Assert
        Assert.Null(line.SourceEndLabel);
        Assert.Null(line.TargetEndLabel);
    }

    /// <summary>A line carries the end labels it was constructed with.</summary>
    [Fact]
    public void LayoutLine_EndLabels_AreCarried()
    {
        // Arrange
        var source = new LineEndLabel("owner", "1");
        var target = new LineEndLabel("parts", "0..*");

        // Act
        var line = new LayoutLine(
            [new Point2D(0, 0), new Point2D(100, 0)],
            EndMarkerStyle.None,
            EndMarkerStyle.None,
            LineStyle.Solid,
            "mid",
            source,
            target);

        // Assert
        Assert.Equal(source, line.SourceEndLabel);
        Assert.Equal(target, line.TargetEndLabel);
        Assert.Equal("mid", line.MidpointLabel);
    }

    /// <summary>The secondary slot of an end label defaults to none.</summary>
    [Fact]
    public void LineEndLabel_Secondary_DefaultsToNull()
    {
        // Act
        var label = new LineEndLabel("role");

        // Assert
        Assert.Equal("role", label.Primary);
        Assert.Null(label.Secondary);
    }

    /// <summary>End labels compare by value and support <c>with</c>.</summary>
    [Fact]
    public void LineEndLabel_IsAValueRecord()
    {
        // Arrange
        var a = new LineEndLabel("role", "1..*");

        // Act
        var b = a with { Secondary = "0..1" };

        // Assert
        Assert.Equal(new LineEndLabel("role", "1..*"), a);
        Assert.NotEqual(a, b);
        Assert.Equal("0..1", b.Secondary);
    }

    /// <summary>A graph edge has no end labels until they are set, and stores each independently.</summary>
    [Fact]
    public void LayoutGraphEdge_EndLabels_DefaultToNullAndAreSettable()
    {
        // Arrange
        var graph = new LayoutGraph();
        var a = graph.AddNode("a", 10, 10);
        var b = graph.AddNode("b", 10, 10);
        var edge = graph.AddEdge("e", a, b);

        // Assert default
        Assert.Null(edge.SourceEndLabel);
        Assert.Null(edge.TargetEndLabel);

        // Act
        edge.SourceEndLabel = new LineEndLabel("src");
        edge.TargetEndLabel = new LineEndLabel(null, "0..*");

        // Assert
        Assert.Equal(new LineEndLabel("src"), edge.SourceEndLabel);
        Assert.Equal(new LineEndLabel(null, "0..*"), edge.TargetEndLabel);
    }
}
