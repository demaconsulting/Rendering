// <copyright file="NotationPrimitiveModelTests.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Tests;

/// <summary>
///     Tests for the notation-primitive additions to the layout-tree and layout-graph models: the
///     appended end-marker and badge enum members, the <see cref="TextStyle"/> presets, and the optional
///     members whose defaults must reproduce the legacy appearance.
/// </summary>
public sealed class NotationPrimitiveModelTests
{
    /// <summary>
    ///     The pre-existing <see cref="EndMarkerStyle"/> members keep their ordinals and the new members
    ///     are appended after them, so persisted or interop values never shift.
    /// </summary>
    [Fact]
    public void EndMarkerStyle_LegacyOrdinals_ArePinnedAndNewMembersAppended()
    {
        Assert.Equal(0, (int)EndMarkerStyle.None);
        Assert.Equal(1, (int)EndMarkerStyle.OpenChevron);
        Assert.Equal(2, (int)EndMarkerStyle.HollowTriangle);
        Assert.Equal(3, (int)EndMarkerStyle.HollowTriangleCrossbar);
        Assert.Equal(4, (int)EndMarkerStyle.FilledArrow);
        Assert.Equal(5, (int)EndMarkerStyle.HollowDiamond);
        Assert.Equal(6, (int)EndMarkerStyle.FilledDiamond);
        Assert.Equal(7, (int)EndMarkerStyle.Circle);
        Assert.Equal(8, (int)EndMarkerStyle.Bar);
        Assert.Equal(9, (int)EndMarkerStyle.HollowTriangleTwoDots);
        Assert.Equal(10, (int)EndMarkerStyle.HollowTriangleFourDots);
        Assert.Equal(11, (int)EndMarkerStyle.CircledPlus);
    }

    /// <summary>
    ///     The pre-existing <see cref="BadgeShape"/> members keep their ordinals and the new members are
    ///     appended after them.
    /// </summary>
    [Fact]
    public void BadgeShape_LegacyOrdinals_ArePinnedAndNewMembersAppended()
    {
        Assert.Equal(0, (int)BadgeShape.FilledCircle);
        Assert.Equal(1, (int)BadgeShape.Bullseye);
        Assert.Equal(2, (int)BadgeShape.Diamond);
        Assert.Equal(3, (int)BadgeShape.HorizontalBar);
        Assert.Equal(4, (int)BadgeShape.VerticalBar);
        Assert.Equal(5, (int)BadgeShape.ThickHorizontalBar);
        Assert.Equal(6, (int)BadgeShape.ThickVerticalBar);
        Assert.Equal(7, (int)BadgeShape.CrossedCircle);
    }

    /// <summary>
    ///     The <see cref="TextStyle"/> presets pair the weight and style they are named for.
    /// </summary>
    [Fact]
    public void TextStyle_Presets_PairNamedWeightAndStyle()
    {
        Assert.Equal(new TextStyle(FontWeight.Regular, FontStyle.Normal), TextStyle.Regular);
        Assert.Equal(new TextStyle(FontWeight.Bold, FontStyle.Normal), TextStyle.Bold);
        Assert.Equal(new TextStyle(FontWeight.Regular, FontStyle.Italic), TextStyle.Italic);
        Assert.Equal(new TextStyle(FontWeight.Bold, FontStyle.Italic), TextStyle.BoldItalic);
    }

    /// <summary>
    ///     A default <see cref="BoxAppearance"/> is the legacy look: a solid outline, a bold upright name,
    ///     a regular italic keyword, a body-drawn folder title and no corner icon.
    /// </summary>
    [Fact]
    public void BoxAppearance_Defaults_ResolveLegacyLook()
    {
        var appearance = new BoxAppearance();

        Assert.Equal(LineStyle.Solid, appearance.OutlineStyle);
        Assert.Equal(TextStyle.Bold, appearance.ResolvedNameStyle);
        Assert.Equal(TextStyle.Italic, appearance.ResolvedKeywordStyle);
        Assert.Equal(FolderTitlePlacement.Body, appearance.FolderTitle);
        Assert.Equal(BoxCornerIcon.None, appearance.CornerIcon);
    }

    /// <summary>
    ///     Explicit name and keyword styles override the legacy defaults.
    /// </summary>
    [Fact]
    public void BoxAppearance_ExplicitStyles_OverrideLegacyDefaults()
    {
        var appearance = new BoxAppearance(NameStyle: TextStyle.Regular, KeywordStyle: TextStyle.Bold);

        Assert.Equal(TextStyle.Regular, appearance.ResolvedNameStyle);
        Assert.Equal(TextStyle.Bold, appearance.ResolvedKeywordStyle);
    }

    /// <summary>
    ///     A default <see cref="LayoutCompartment"/> keeps the legacy left-aligned bold-italic title.
    /// </summary>
    [Fact]
    public void LayoutCompartment_Defaults_KeepLegacyTitle()
    {
        var compartment = new LayoutCompartment("attributes", ["a"]);

        Assert.Equal(TextAlign.Left, compartment.TitleAlign);
        Assert.Null(compartment.TitleStyle);
        Assert.Equal(TextStyle.BoldItalic, compartment.ResolvedTitleStyle);
    }

    /// <summary>
    ///     A compartment's explicit title alignment and style are carried on the record.
    /// </summary>
    [Fact]
    public void LayoutCompartment_ExplicitTitleAlignAndStyle_AreCarried()
    {
        var compartment = new LayoutCompartment("parts", [], TextAlign.Center, TextStyle.Italic);

        Assert.Equal(TextAlign.Center, compartment.TitleAlign);
        Assert.Equal(TextStyle.Italic, compartment.ResolvedTitleStyle);
    }

    /// <summary>
    ///     A <see cref="LayoutBox"/> built without an appearance has none, which is the legacy look.
    /// </summary>
    [Fact]
    public void LayoutBox_Appearance_DefaultsToNull()
    {
        var box = new LayoutBox(0, 0, 10, 10, "A", 0, BoxShape.Rectangle, [], []);

        Assert.Null(box.Appearance);
    }

    /// <summary>
    ///     A <see cref="LayoutBadge"/> built without a thickness has none (the default bar thickness).
    /// </summary>
    [Fact]
    public void LayoutBadge_Thickness_DefaultsToNull()
    {
        var badge = new LayoutBadge(0, 0, 10, BadgeShape.FilledCircle, null);

        Assert.Null(badge.Thickness);
    }

    /// <summary>
    ///     A <see cref="LayoutBand"/> built without an outline style is solid.
    /// </summary>
    [Fact]
    public void LayoutBand_OutlineStyle_DefaultsToSolid()
    {
        var band = new LayoutBand(0, 0, 10, 10, BandOrientation.Horizontal, null, []);

        Assert.Equal(LineStyle.Solid, band.OutlineStyle);
    }

    /// <summary>
    ///     A <see cref="LayoutLifeline"/> built with only its legacy members has no keyword, a rectangular
    ///     header, no appearance and no corner radius.
    /// </summary>
    [Fact]
    public void LayoutLifeline_NewMembers_DefaultToLegacyLook()
    {
        var lifeline = new LayoutLifeline(10, 0, 100, "a", 40, 20);

        Assert.Null(lifeline.Keyword);
        Assert.Equal(BoxShape.Rectangle, lifeline.HeaderShape);
        Assert.Null(lifeline.HeaderAppearance);
        Assert.Null(lifeline.HeaderCornerRadius);
    }

    /// <summary>
    ///     A <see cref="LayoutGraphNode"/> has no appearance until one is assigned.
    /// </summary>
    [Fact]
    public void LayoutGraphNode_Appearance_DefaultsToNullAndRoundTrips()
    {
        var node = new LayoutGraphNode("n", 10, 10);
        Assert.Null(node.Appearance);

        var appearance = new BoxAppearance(LineStyle.Dashed);
        node.Appearance = appearance;

        Assert.Same(appearance, node.Appearance);
    }
}
