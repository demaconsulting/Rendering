// <copyright file="BoxMetrics.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Abstractions;

/// <summary>
/// Shared geometry helpers that compute box title-area and folder-tab heights from a
/// <see cref="Theme"/>. Both the layout strategies and the renderers use these formulas so
/// that reserved space and drawn space stay consistent.
/// </summary>
public static class BoxMetrics
{
    /// <summary>
    /// Computes the height of the folder tab drawn at the top-left of a
    /// <see cref="BoxShape.Folder"/> box.
    /// </summary>
    /// <param name="theme">Theme providing font and padding metrics.</param>
    /// <returns>The tab height in logical pixels.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme"/> is <see langword="null"/>.</exception>
    public static double FolderTabHeight(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return theme.FontSizeBody + 2.0 * theme.LabelPadding;
    }

    /// <summary>
    /// Computes the height of the title area of a box: the vertical space reserved at the top
    /// for the optional keyword line and the bold name line.
    /// </summary>
    /// <param name="theme">Theme providing font and padding metrics.</param>
    /// <param name="hasLabel">Whether the box has a name label.</param>
    /// <param name="hasKeyword">Whether the box has a keyword line above the name.</param>
    /// <returns>The title-area height in logical pixels.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme"/> is <see langword="null"/>.</exception>
    public static double TitleAreaHeight(Theme theme, bool hasLabel, bool hasKeyword)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (!hasLabel && !hasKeyword)
        {
            return 0.0;
        }

        var height = theme.LabelPadding;
        if (hasKeyword)
        {
            height += theme.FontSizeBody + theme.LabelPadding;
        }

        if (hasLabel)
        {
            height += theme.FontSizeTitle + theme.LabelPadding;
        }

        return height;
    }

    /// <summary>
    /// Computes the Y coordinate at which a box's title block (keyword line, if any, then the bold
    /// name line) should begin, i.e. the position immediately before the leading
    /// <see cref="Theme.LabelPadding"/> gap that precedes the first line. Both renderers must call
    /// this so title placement never silently diverges between them.
    /// </summary>
    /// <remarks>
    /// A box with children or compartments always has its title pinned directly under the top
    /// (folder-tab-adjusted) edge, since the title acts as a header above that content. A leaf box
    /// — no children and no compartments — instead has its title block centered vertically within
    /// the box's own content height (excluding any port-reserved <c>ContentInset*</c> margins), so a
    /// plain name-only box doesn't read as top-anchored with dead space below. This centering is
    /// skipped when the box has a left- or right-side port (<see cref="LayoutBox.ContentInsetLeft"/>
    /// or <see cref="LayoutBox.ContentInsetRight"/> greater than zero), because such a port's
    /// inward-rendered label runs horizontally through the box's mid-height band and would otherwise
    /// visually collide with a centered title.
    /// </remarks>
    /// <param name="box">Box whose title position is being resolved.</param>
    /// <param name="theme">Theme providing font and padding metrics.</param>
    /// <returns>The Y coordinate, in logical pixels, at which the title block begins.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="box"/> or <paramref name="theme"/> is <see langword="null"/>.</exception>
    public static double TitleCursorTop(LayoutBox box, Theme theme)
    {
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(theme);

        var tabHeight = box.FolderTabHeight.HasValue
            ? Math.Max(0.0, box.FolderTabHeight.Value)
            : FolderTabHeight(theme);
        var top = box.Shape == BoxShape.Folder ? box.Y + tabHeight : box.Y;
        var availableTop = top + box.ContentInsetTop;

        // Leaf boxes (no children, no compartments) center the title block vertically within the
        // box's own content height instead of pinning it to the top. Skip centering when the box
        // has a left- or right-side port (ContentInsetLeft/Right > 0): those ports' inward-rendered
        // labels run horizontally through the box's mid-height band, and a centered title would
        // land in and visually collide with that same band.
        var hasSidePort = box.ContentInsetLeft > 0.0 || box.ContentInsetRight > 0.0;
        if (box.Children.Count == 0 && box.Compartments.Count == 0 && !hasSidePort)
        {
            var availableBottom = box.Y + box.Height - box.ContentInsetBottom;
            var titleHeight = TitleAreaHeight(theme, box.Label != null, box.Keyword != null);
            var offset = Math.Max(0.0, (availableBottom - availableTop - titleHeight) / 2.0);
            return availableTop + offset + theme.LabelPadding;
        }

        return availableTop + theme.LabelPadding;
    }
}
