// <copyright file="GalleryIndexTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Gallery;

/// <summary>
///     Exercises the <c>README.md</c> page generators. The generated Markdown is validated on every
///     run (a smoke test of the generator), but only written to disk in "regenerate the committed
///     showcase" mode — selected by <c>RENDERING_GALLERY_DIR</c> — so ordinary test runs never write a
///     Markdown file into the build output where the lint gate would pick it up.
/// </summary>
public sealed class GalleryIndexTests
{
    /// <summary>
    ///     Proves the generated top-level index is a valid, browsable document that references every
    ///     group's title, folder link, and short table summary.
    /// </summary>
    [Fact]
    public void Gallery_TopIndex_ReferencesEveryGroup()
    {
        // Arrange / Act: generate the top-level index from the catalog.
        var markdown = GalleryIndex.BuildTopIndex();

        // Assert: the document is well-formed and complete.
        Assert.StartsWith("# Rendering Gallery", markdown, StringComparison.Ordinal);
        Assert.EndsWith("\n", markdown, StringComparison.Ordinal);
        Assert.Contains("| Group | What it's about |", markdown, StringComparison.Ordinal);

        foreach (var group in GalleryCatalog.Groups)
        {
            Assert.Contains(
                $"| [{group.Title}]({group.Folder}/README.md) | {group.ShortSummary} |",
                markdown,
                StringComparison.Ordinal);
        }

        AssertNoLineExceedsLintWidth(markdown);
    }

    /// <summary>
    ///     Proves only images explicitly flagged <see cref="GalleryImage.IsTopIndexHighlight"/> appear
    ///     on the top-level index — opt-in by design, so a deliberate contrast/baseline image can never
    ///     surface there bare and out of context by accident, the way it could when the index used to
    ///     auto-pick the first non-excluded image per group.
    /// </summary>
    [Fact]
    public void Gallery_TopIndex_ShowsOnlyExplicitlyHighlightedImages()
    {
        // Arrange / Act: generate the top-level index and the connectivity-and-clusters group page.
        var topIndex = GalleryIndex.BuildTopIndex();
        var group = GalleryCatalog.Groups.First(g => g.Folder == "connectivity-and-clusters");
        var groupPage = GalleryIndex.BuildGroupPage(group);

        // Assert: a highlighted image appears on the index, using its own real caption.
        var highlighted = group.Sections
            .SelectMany(section => section.Images)
            .First(image => image.IsTopIndexHighlight);
        Assert.Contains(Path.GetFileName(highlighted.FileName), topIndex, StringComparison.Ordinal);

        // Assert: a non-highlighted image (the deliberate connected-graph contrast baseline) is never a
        // top-index thumbnail, even though it still appears on the group's own full page.
        var baselineFileName = Path.GetFileName(GalleryCatalog.LayeredRegressionBaselineSvg);
        Assert.DoesNotContain(baselineFileName, topIndex, StringComparison.Ordinal);
        Assert.Contains(baselineFileName, groupPage, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves each group's generated page is a valid, browsable document that references every one
    ///     of its sections' titles and images.
    /// </summary>
    [Fact]
    public void Gallery_GroupPages_ReferenceEveryImageAndSection()
    {
        foreach (var group in GalleryCatalog.Groups)
        {
            // Arrange / Act: generate the group page from the catalog.
            var markdown = GalleryIndex.BuildGroupPage(group);

            // Assert: the document is well-formed and complete.
            Assert.StartsWith($"# {group.Title}", markdown, StringComparison.Ordinal);
            Assert.EndsWith("\n", markdown, StringComparison.Ordinal);

            foreach (var section in group.Sections)
            {
                Assert.Contains($"## {section.Title}", markdown, StringComparison.Ordinal);
                foreach (var image in section.Images)
                {
                    var fileName = Path.GetFileName(image.FileName);
                    Assert.Contains($"![{image.Alt}]({fileName})", markdown, StringComparison.Ordinal);
                }
            }

            AssertNoLineExceedsLintWidth(markdown);
        }
    }

    /// <summary>
    ///     In gallery mode, proves <see cref="GalleryIndex.WriteAll"/> writes the top-level index and
    ///     every group's <c>README.md</c> alongside the images, each a non-empty file.
    /// </summary>
    [Fact]
    public void Gallery_WriteAll_WritesTopIndexAndEveryGroupPage()
    {
        if (!GalleryOutput.IsGalleryMode)
        {
            return;
        }

        var written = GalleryIndex.WriteAll(GalleryOutput.ResolveDirectory());

        // One top-level index plus one page per group.
        Assert.Equal(GalleryCatalog.Groups.Count + 1, written.Count);

        foreach (var path in written)
        {
            Assert.True(File.Exists(path), $"Expected gallery page to be written: {path}");
            Assert.True(new FileInfo(path).Length > 0, $"Expected gallery page to be non-empty: {path}");
        }
    }

    /// <summary>Asserts that no line in <paramref name="markdown"/> exceeds the 120-column markdownlint limit.</summary>
    private static void AssertNoLineExceedsLintWidth(string markdown)
    {
        foreach (var line in markdown.Split('\n'))
        {
            Assert.True(line.Length <= 120, $"Index line exceeds 120 columns: {line}");
        }
    }
}
