// <copyright file="LayoutLabel.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// Horizontal text alignment.
/// </summary>
public enum TextAlign
{
    /// <summary>Align text to the left.</summary>
    Left,

    /// <summary>Align text to the center.</summary>
    Center,

    /// <summary>Align text to the right.</summary>
    Right,
}

/// <summary>Weight of a font.</summary>
public enum FontWeight
{
    /// <summary>Regular (normal) weight.</summary>
    Regular,

    /// <summary>Bold weight.</summary>
    Bold,
}

/// <summary>Style of a font.</summary>
public enum FontStyle
{
    /// <summary>Normal (upright) style.</summary>
    Normal,

    /// <summary>Italic style.</summary>
    Italic,
}

/// <summary>
/// A weight and style pair describing how a run of text is drawn.
/// </summary>
/// <param name="Weight">Font weight of the text.</param>
/// <param name="Style">Font style of the text.</param>
public readonly record struct TextStyle(FontWeight Weight, FontStyle Style)
{
    /// <summary>Gets regular weight, normal (upright) style.</summary>
    public static TextStyle Regular => new(FontWeight.Regular, FontStyle.Normal);

    /// <summary>Gets bold weight, normal (upright) style.</summary>
    public static TextStyle Bold => new(FontWeight.Bold, FontStyle.Normal);

    /// <summary>Gets regular weight, italic style.</summary>
    public static TextStyle Italic => new(FontWeight.Regular, FontStyle.Italic);

    /// <summary>Gets bold weight, italic style.</summary>
    public static TextStyle BoldItalic => new(FontWeight.Bold, FontStyle.Italic);
}

/// <summary>
/// A standalone text label at an absolute position. Width is capped to <see cref="MaxWidth"/>.
/// </summary>
/// <param name="X">Absolute X coordinate of the label origin in logical pixels.</param>
/// <param name="Y">Absolute Y coordinate of the label baseline in logical pixels.</param>
/// <param name="MaxWidth">Maximum width before text wraps or truncates.</param>
/// <param name="Text">Text content of the label.</param>
/// <param name="Align">Horizontal alignment of the text within the label bounds.</param>
/// <param name="Weight">Font weight applied to the label text.</param>
/// <param name="Style">Font style applied to the label text.</param>
/// <param name="FontSize">Font size in logical pixels.</param>
public sealed record LayoutLabel(
    double X,
    double Y,
    double MaxWidth,
    string Text,
    TextAlign Align,
    FontWeight Weight,
    FontStyle Style,
    double FontSize) : LayoutNode;
