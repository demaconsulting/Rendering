// <copyright file="GalleryIndexTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Gallery;

/// <summary>
///     Exercises the <c>gallery.md</c> index generator. The generated Markdown is validated on every
///     run (a smoke test of the generator), but only written to disk in "regenerate the committed
///     showcase" mode — selected by <c>RENDERING_GALLERY_DIR</c> — so ordinary test runs never write a
///     Markdown file into the build output where the lint gate would pick it up.
/// </summary>
public sealed class GalleryIndexTests
{
    /// <summary>
    ///     Proves the generated index is a valid, browsable document that references every catalogued
    ///     image and section, and — when in gallery mode — is written next to the images.
    /// </summary>
    [Fact]
    public void Gallery_Index_ReferencesEveryImageAndSection()
    {
        // Arrange / Act: generate the index from the catalog.
        var markdown = GalleryIndex.Build();

        // Assert: the document is well-formed and complete.
        Assert.StartsWith("# Rendering Gallery", markdown, StringComparison.Ordinal);
        Assert.EndsWith("\n", markdown, StringComparison.Ordinal);

        foreach (var section in GalleryCatalog.Sections)
        {
            Assert.Contains($"## {section.Title}", markdown, StringComparison.Ordinal);
            foreach (var image in section.Images)
            {
                Assert.Contains($"![{image.Alt}]({image.FileName})", markdown, StringComparison.Ordinal);
            }
        }

        // No line exceeds the 120-column markdownlint limit.
        foreach (var line in markdown.Split('\n'))
        {
            Assert.True(line.Length <= 120, $"Index line exceeds 120 columns: {line}");
        }

        // In gallery mode, persist the index alongside the generated images and confirm it exists.
        if (GalleryOutput.IsGalleryMode)
        {
            var path = GalleryIndex.Write(GalleryOutput.ResolveDirectory());
            Assert.True(File.Exists(path), $"Expected gallery index to be written: {path}");
        }
    }
}
