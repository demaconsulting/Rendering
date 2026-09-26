// <copyright file="CanvasNetTypefacesTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.CanvasNet;

namespace DemaConsulting.Rendering.CanvasNet.Tests;

/// <summary>
///     Tests for the shared <see cref="CanvasNetTypefaces"/> helper that
///     <see cref="CanvasNetRasterRenderer"/> resolves typefaces from.
/// </summary>
public sealed class CanvasNetTypefacesTests
{
    /// <summary>
    ///     Proves that <see cref="CanvasNetTypefaces.Resolve"/> returns a distinct typeface instance for
    ///     each of the four bold/italic combinations, and is stable (returns the same cached instance)
    ///     across repeated calls with the same arguments.
    /// </summary>
    [Fact]
    public void CanvasNetTypefaces_Resolve_ReturnsStableDistinctTypefacesPerVariant()
    {
        // Arrange / Act
        var regular1 = CanvasNetTypefaces.Resolve(false, false);
        var regular2 = CanvasNetTypefaces.Resolve(false, false);
        var bold = CanvasNetTypefaces.Resolve(true, false);
        var italic = CanvasNetTypefaces.Resolve(false, true);
        var boldItalic = CanvasNetTypefaces.Resolve(true, true);

        // Assert
        Assert.Same(regular1, regular2);
        Assert.NotSame(regular1, bold);
        Assert.NotSame(regular1, italic);
        Assert.NotSame(regular1, boldItalic);
        Assert.NotSame(bold, boldItalic);
    }
}
