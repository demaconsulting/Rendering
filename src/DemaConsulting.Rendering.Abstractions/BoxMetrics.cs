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
    /// Computes the size of the folded-corner cutout drawn at the top-right of a
    /// <see cref="BoxShape.Note"/> box: a fraction of the box's shorter side
    /// (<see cref="NotationMetrics.NoteFoldFraction"/>), capped at
    /// <see cref="NotationMetrics.NoteFoldMaxSize"/>. The same value is the horizontal inset from
    /// the right edge and the vertical inset from the top edge, since the fold cutout is a
    /// right-angle triangle. Both renderers, and anything reasoning about how close a horizontal
    /// divider may safely approach a Note box's top edge, must call this so the fold's drawn
    /// geometry and any content clearance never disagree.
    /// </summary>
    /// <param name="box">The Note-shaped box whose fold size is resolved.</param>
    /// <returns>The fold size in logical pixels.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="box"/> is <see langword="null"/>.</exception>
    public static double NoteFoldSize(LayoutBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        return Math.Min(Math.Min(box.Width, box.Height) * NotationMetrics.NoteFoldFraction, NotationMetrics.NoteFoldMaxSize);
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
    /// Computes the recommended <see cref="LayoutBox.ContentInsetTop"/> for a box that has left
    /// and/or right ports and a title (name and/or keyword), so those ports never land in the
    /// same row as the title text.
    /// </summary>
    /// <remarks>
    /// This is the public counterpart of the title-vs-side-port reservation that
    /// <c>LayeredLayoutAlgorithm</c> and <c>HierarchicalLayoutAlgorithm</c> apply automatically
    /// when a <c>LayoutGraph</c> is laid out through this library's own algorithms. A caller that
    /// instead constructs <see cref="LayoutBox"/> / port geometry directly — bypassing
    /// <c>LayoutGraph</c> entirely, as an external layout strategy might — never runs through that
    /// algorithm code path, so its left/right port labels get no automatic protection from the
    /// box's own title. Call this helper while building such a box to opt into the same protection
    /// without reimplementing the formula.
    /// </remarks>
    /// <param name="theme">Theme providing font and padding metrics.</param>
    /// <param name="hasLeftOrRightPorts">Whether the box has any port anchored on its left or right edge.</param>
    /// <param name="hasLabel">Whether the box has a name label.</param>
    /// <param name="hasKeyword">Whether the box has a keyword line above the name.</param>
    /// <param name="existingInsetTop">
    /// Any <see cref="LayoutBox.ContentInsetTop"/> the caller already reserves for another reason
    /// (e.g. a compartment header). The result never reserves less than this.
    /// </param>
    /// <returns>
    /// The recommended <see cref="LayoutBox.ContentInsetTop"/>: <paramref name="existingInsetTop"/>
    /// unchanged when there are no left/right ports or no title to protect against, otherwise the
    /// larger of <paramref name="existingInsetTop"/> and the box's <see cref="TitleAreaHeight"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme"/> is <see langword="null"/>.</exception>
    public static double ResolveTitleVsSidePortContentInsetTop(
        Theme theme,
        bool hasLeftOrRightPorts,
        bool hasLabel,
        bool hasKeyword,
        double existingInsetTop = 0.0)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (!hasLeftOrRightPorts || (!hasLabel && !hasKeyword))
        {
            return existingInsetTop;
        }

        return Math.Max(existingInsetTop, TitleAreaHeight(theme, hasLabel, hasKeyword));
    }

    /// <summary>
    /// Computes the Y coordinate at which a box's title block (keyword line, if any, then the bold
    /// name line) should begin, i.e. the position immediately before the leading
    /// <see cref="Theme.LabelPadding"/> gap that precedes the first line. Both renderers must call
    /// this so title placement never silently diverges between them.
    /// </summary>
    /// <remarks>
    /// A box with <see cref="LayoutBox.CenterTitle"/> <see langword="false"/> (the default) always
    /// has its title pinned directly under the top (folder-tab-adjusted) edge, since a container's
    /// title acts as a header above its content. A box with <see cref="LayoutBox.CenterTitle"/>
    /// <see langword="true"/> — set only by a layout algorithm that knows the box is a genuine
    /// atomic leaf — instead has its title block centered vertically within the box's own content
    /// height (excluding any port-reserved <c>ContentInset*</c> margins), so a plain name-only box
    /// doesn't read as top-anchored with dead space below. Once a box's <c>ContentInsetTop</c>
    /// already reserves room for the title above any left/right port band (see the layout
    /// algorithm's title-vs-side-port reservation), a left/right port can never land in the title's
    /// row regardless of whether the title is centered or top-pinned, so no further special-casing
    /// is needed here.
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

        if (box.CenterTitle)
        {
            var availableBottom = box.Y + box.Height - box.ContentInsetBottom;
            var titleHeight = TitleAreaHeight(theme, box.Label != null, box.Keyword != null);
            var offset = Math.Max(0.0, (availableBottom - availableTop - titleHeight) / 2.0);
            return availableTop + offset + theme.LabelPadding;
        }

        return availableTop + theme.LabelPadding;
    }
}
