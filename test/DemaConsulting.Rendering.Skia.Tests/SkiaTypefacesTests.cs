// <copyright file="SkiaTypefacesTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Skia.Tests;

/// <summary>
///     Tests for the shared <see cref="SkiaTypefaces"/> helper that <see cref="SkiaRasterRenderer"/>
///     resolves typefaces from.
/// </summary>
public sealed class SkiaTypefacesTests
{
    /// <summary>
    ///     Proves that <see cref="SkiaTypefaces.Resolve"/> returns a distinct typeface instance for
    ///     each of the four bold/italic combinations, and is stable (returns the same cached instance)
    ///     across repeated calls with the same arguments.
    /// </summary>
    [Fact]
    public void SkiaTypefaces_Resolve_ReturnsStableDistinctTypefacesPerVariant()
    {
        var regular1 = SkiaTypefaces.Resolve(false, false);
        var regular2 = SkiaTypefaces.Resolve(false, false);
        var bold = SkiaTypefaces.Resolve(true, false);
        var italic = SkiaTypefaces.Resolve(false, true);
        var boldItalic = SkiaTypefaces.Resolve(true, true);

        Assert.Same(regular1, regular2);
        Assert.NotSame(regular1, bold);
        Assert.NotSame(regular1, italic);
        Assert.NotSame(regular1, boldItalic);
        Assert.NotSame(bold, boldItalic);
    }
}
