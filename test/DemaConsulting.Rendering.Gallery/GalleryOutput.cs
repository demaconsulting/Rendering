// <copyright file="GalleryOutput.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Gallery;

/// <summary>
///     Resolves where the gallery showcase writes its generated images and index.
/// </summary>
/// <remarks>
///     <para>
///     The gallery facts double as a rendering smoke test that runs in the normal test suite. To keep
///     ordinary <c>build.ps1</c>/CI runs from dirtying the repository, the output directory is read from
///     the <c>RENDERING_GALLERY_DIR</c> environment variable and falls back to a throwaway folder under
///     the test's own output directory when the variable is unset.
///     </para>
///     <para>
///     Setting <c>RENDERING_GALLERY_DIR</c> to the committed <c>docs/gallery</c> folder (as the
///     <c>-Gallery</c> invocation does) switches the same facts into "regenerate the committed showcase"
///     mode, in which the browsable <c>gallery.md</c> index is (re)written alongside the images.
///     </para>
/// </remarks>
internal static class GalleryOutput
{
    /// <summary>Name of the environment variable that selects the gallery output directory.</summary>
    public const string DirectoryEnvironmentVariable = "RENDERING_GALLERY_DIR";

    /// <summary>
    ///     Gets a value indicating whether the gallery is running in "regenerate the committed showcase"
    ///     mode, selected by setting <see cref="DirectoryEnvironmentVariable"/>.
    /// </summary>
    public static bool IsGalleryMode =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(DirectoryEnvironmentVariable));

    /// <summary>
    ///     Gets the directory that receives the generated images and index, creating it if necessary.
    /// </summary>
    /// <returns>The absolute path of the (now-existing) output directory.</returns>
    public static string ResolveDirectory()
    {
        var configured = Environment.GetEnvironmentVariable(DirectoryEnvironmentVariable);
        var directory = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "gallery-output")
            : configured;

        Directory.CreateDirectory(directory);
        return directory;
    }
}
