// <copyright file="SkiaTypefaces.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using SkiaSharp;

namespace DemaConsulting.Rendering.Skia;

/// <summary>
/// Shared lazily-loaded Noto Sans typefaces, embedded as assembly resources, reused by both
/// <see cref="SkiaRasterRenderer"/> (for drawing) and <see cref="SkiaTextMeasurer"/> (for
/// layout-time measurement) so both consult exactly the same font regardless of which fonts are
/// installed on the host system.
/// </summary>
internal static class SkiaTypefaces
{
    /// <summary>
    /// Lazily-loaded typeface for regular-weight, upright text. Loaded once from the embedded
    /// NotoSans-Regular.ttf resource.
    /// </summary>
    internal static readonly Lazy<SKTypeface> RegularTypeface = new(() => LoadTypeface("NotoSans-Regular.ttf"));

    /// <summary>
    /// Lazily-loaded typeface for bold-weight, upright text. Loaded from NotoSans-Bold.ttf.
    /// </summary>
    internal static readonly Lazy<SKTypeface> BoldTypeface = new(() => LoadTypeface("NotoSans-Bold.ttf"));

    /// <summary>
    /// Lazily-loaded typeface for regular-weight, italic text. Loaded from NotoSans-Italic.ttf.
    /// </summary>
    internal static readonly Lazy<SKTypeface> ItalicTypeface = new(() => LoadTypeface("NotoSans-Italic.ttf"));

    /// <summary>
    /// Lazily-loaded typeface for bold-weight, italic text. Loaded from NotoSans-BoldItalic.ttf.
    /// </summary>
    internal static readonly Lazy<SKTypeface> BoldItalicTypeface = new(() => LoadTypeface("NotoSans-BoldItalic.ttf"));

    /// <summary>
    /// Resolves the typeface matching the requested weight and style.
    /// </summary>
    /// <param name="bold">When <see langword="true"/>, selects the bold typeface variant.</param>
    /// <param name="italic">When <see langword="true"/>, selects the italic typeface variant.</param>
    /// <returns>The matching lazily-loaded <see cref="SKTypeface"/>.</returns>
    internal static SKTypeface Resolve(bool bold, bool italic) => (bold, italic) switch
    {
        (true, true) => BoldItalicTypeface.Value,
        (true, false) => BoldTypeface.Value,
        (false, true) => ItalicTypeface.Value,
        _ => RegularTypeface.Value,
    };

    /// <summary>
    /// Loads a typeface from an embedded assembly resource. The resource is matched by its
    /// filename suffix (case-insensitive). Falls back to <see cref="SKTypeface.Default"/> if
    /// the resource is not found, so callers remain functional even when the font is not
    /// embedded (e.g., during development without the downloaded font files).
    /// </summary>
    /// <param name="fileName">File name suffix to match in the assembly manifest resource names.</param>
    /// <returns>
    /// An <see cref="SKTypeface"/> loaded from the embedded resource, or
    /// <see cref="SKTypeface.Default"/> if the resource is not found.
    /// </returns>
    private static SKTypeface LoadTypeface(string fileName)
    {
        var asm = typeof(SkiaTypefaces).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            return SKTypeface.Default;
        }

        using var stream = asm.GetManifestResourceStream(resourceName)!;
        using var data = SKData.Create(stream);
        return SKTypeface.FromData(data) ?? SKTypeface.Default;
    }
}
