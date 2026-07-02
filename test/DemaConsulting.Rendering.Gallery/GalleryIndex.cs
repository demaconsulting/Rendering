// <copyright file="GalleryIndex.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using System.Text;

namespace DemaConsulting.Rendering.Gallery;

/// <summary>
///     Generates the browsable <c>gallery.md</c> index from the <see cref="GalleryCatalog"/>. The output
///     is kept markdownlint-clean (blank-line-separated headings and paragraphs, lines wrapped to 120
///     columns, alt text on every image) so the committed index passes the repository's lint gate.
/// </summary>
internal static class GalleryIndex
{
    /// <summary>Maximum line length permitted by the repository markdownlint configuration (MD013).</summary>
    private const int MaxLineLength = 120;

    /// <summary>Builds the full Markdown text of the gallery index from the catalog.</summary>
    /// <returns>The complete <c>gallery.md</c> document, ending with a single newline.</returns>
    public static string Build()
    {
        var builder = new StringBuilder();

        AppendParagraph(builder, "# Rendering Gallery");
        AppendParagraph(
            builder,
            "This gallery showcases what the Rendering library can produce. Every image is generated "
            + "by the gallery test project directly from the public API, so this page doubles as an "
            + "end-to-end rendering smoke test.");
        AppendParagraph(
            builder,
            "Regenerate this page and its images by running `./gallery.ps1` from the repository root.");

        foreach (var section in GalleryCatalog.Sections)
        {
            AppendParagraph(builder, $"## {section.Title}");
            AppendParagraph(builder, section.Intro);

            foreach (var image in section.Images)
            {
                AppendParagraph(builder, $"![{image.Alt}]({image.FileName})");
                AppendParagraph(builder, image.Caption);
            }
        }

        // Collapse the trailing paragraph separator to a single terminating newline (MD047).
        return builder.ToString().TrimEnd('\n') + "\n";
    }

    /// <summary>
    ///     Writes the generated index to <c>gallery.md</c> in the given directory.
    /// </summary>
    /// <param name="directory">The directory that receives <c>gallery.md</c>.</param>
    /// <returns>The absolute path of the written index file.</returns>
    public static string Write(string directory)
    {
        var path = Path.Combine(directory, "gallery.md");
        File.WriteAllText(path, Build(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    /// <summary>
    ///     Appends a paragraph — its text word-wrapped to <see cref="MaxLineLength"/> — followed by a
    ///     blank separator line, so headings and paragraphs are always blank-line separated.
    /// </summary>
    private static void AppendParagraph(StringBuilder builder, string text)
    {
        foreach (var line in WrapToWidth(text, MaxLineLength))
        {
            builder.Append(line).Append('\n');
        }

        builder.Append('\n');
    }

    /// <summary>
    ///     Greedily wraps <paramref name="text"/> on word boundaries so no line exceeds
    ///     <paramref name="width"/> columns. A single word longer than the width is emitted on its own
    ///     line rather than broken.
    /// </summary>
    private static IEnumerable<string> WrapToWidth(string text, int width)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();

        foreach (var word in words)
        {
            if (line.Length == 0)
            {
                line.Append(word);
            }
            else if (line.Length + 1 + word.Length <= width)
            {
                line.Append(' ').Append(word);
            }
            else
            {
                yield return line.ToString();
                line.Clear();
                line.Append(word);
            }
        }

        if (line.Length > 0)
        {
            yield return line.ToString();
        }
    }
}
