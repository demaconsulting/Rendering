// <copyright file="CanvasNetTypefaces.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.Rendering.CanvasNet;

/// <summary>
/// Shared lazily-loaded Noto Sans <see cref="TrueTypeFont"/> instances, embedded as assembly
/// resources and reused by all drawing code in <see cref="CanvasNetRasterRenderer"/> so every call
/// site consults exactly the same font regardless of which fonts are installed on the host system.
/// </summary>
internal static class CanvasNetTypefaces
{
    /// <summary>
    /// Lazily-loaded typeface for regular-weight, upright text. Loaded once from the embedded
    /// NotoSans-Regular.ttf resource.
    /// </summary>
    internal static readonly Lazy<TrueTypeFont> Regular = new(() => LoadFont("NotoSans-Regular.ttf"));

    /// <summary>
    /// Lazily-loaded typeface for bold-weight, upright text. Loaded from NotoSans-Bold.ttf.
    /// </summary>
    internal static readonly Lazy<TrueTypeFont> Bold = new(() => LoadFont("NotoSans-Bold.ttf"));

    /// <summary>
    /// Lazily-loaded typeface for regular-weight, italic text. Loaded from NotoSans-Italic.ttf.
    /// </summary>
    internal static readonly Lazy<TrueTypeFont> Italic = new(() => LoadFont("NotoSans-Italic.ttf"));

    /// <summary>
    /// Lazily-loaded typeface for bold-weight, italic text. Loaded from NotoSans-BoldItalic.ttf.
    /// </summary>
    internal static readonly Lazy<TrueTypeFont> BoldItalic = new(() => LoadFont("NotoSans-BoldItalic.ttf"));

    /// <summary>
    /// Resolves the typeface matching the requested weight and style.
    /// </summary>
    /// <param name="bold">When <see langword="true"/>, selects the bold typeface variant.</param>
    /// <param name="italic">When <see langword="true"/>, selects the italic typeface variant.</param>
    /// <returns>The matching lazily-loaded <see cref="TrueTypeFont"/>.</returns>
    internal static TrueTypeFont Resolve(bool bold, bool italic) => (bold, italic) switch
    {
        (true, true) => BoldItalic.Value,
        (true, false) => Bold.Value,
        (false, true) => Italic.Value,
        _ => Regular.Value,
    };

    /// <summary>
    /// Loads a font from an embedded assembly resource matched by filename suffix.
    /// </summary>
    /// <param name="fileName">File name suffix to match in the assembly manifest resource names.</param>
    /// <returns>The loaded <see cref="TrueTypeFont"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the embedded resource is not found.</exception>
    private static TrueTypeFont LoadFont(string fileName)
    {
        var asm = typeof(CanvasNetTypefaces).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Embedded font resource not found: {fileName}");

        using var stream = asm.GetManifestResourceStream(resourceName)!;
        return TrueTypeFont.Load(stream);
    }
}
