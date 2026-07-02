// <copyright file="Rect.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// An immutable, axis-aligned rectangle defined by the position of its top-left corner and its size,
/// expressed in logical pixels.
/// </summary>
/// <param name="X">Absolute X coordinate of the left edge, in logical pixels.</param>
/// <param name="Y">Absolute Y coordinate of the top edge, in logical pixels.</param>
/// <param name="Width">Width along the X axis, in logical pixels.</param>
/// <param name="Height">Height along the Y axis, in logical pixels.</param>
/// <remarks>
/// <para>
/// <see cref="Rect"/> is part of the core geometry vocabulary shared across the Rendering libraries,
/// alongside <see cref="Point2D"/> and <see cref="PortSide"/>. It carries no styling or semantic
/// information; it is purely a placed region.
/// </para>
/// <para>
/// Coordinates follow the same conventions as the rest of the model: values are in logical pixels,
/// the origin is at the top-left of the canvas, X increases to the right, and Y increases downward.
/// The rectangle spans the half-open ranges <c>[X, X + Width)</c> horizontally and
/// <c>[Y, Y + Height)</c> vertically; <see cref="Width"/> and <see cref="Height"/> are expected to be
/// non-negative.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // A 120 x 60 box whose top-left corner sits at (10, 20).
/// var box = new Rect(10, 20, 120, 60);
///
/// // Derive the far edges and centre from the position and size.
/// var right = box.X + box.Width;    // 130
/// var bottom = box.Y + box.Height;  // 80
/// var centre = new Point2D(box.X + (box.Width / 2), box.Y + (box.Height / 2)); // (70, 50)
/// </code>
/// </example>
public readonly record struct Rect(double X, double Y, double Width, double Height);
