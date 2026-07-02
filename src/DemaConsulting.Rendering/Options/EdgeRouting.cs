// <copyright file="EdgeRouting.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering;

/// <summary>
/// The routing style applied to connectors between placed boxes. This is the ELK-style
/// <c>elk.edgeRouting</c> analogue: it selects <em>how</em> a connector is shaped as it travels from
/// its source box to its target box, independently of which layout algorithm placed the boxes.
/// </summary>
/// <remarks>
/// <para>
/// Routing is a <em>closed</em> vocabulary: unlike the open, registry-based algorithm selection
/// (<see cref="CoreOptions.Algorithm"/>), the routing styles the library understands are enumerated
/// here. The set is expected to grow additively as new routers are implemented — adding a member is a
/// source-compatible change, so callers that switch exhaustively over the enum should keep a default
/// arm for forward compatibility.
/// </para>
/// <para>
/// Today the enum carries a single value, <see cref="Orthogonal"/>, because it is the only routing
/// style with a shipped implementation. Additional styles such as straight-line, polyline, or spline
/// routing will be introduced as their routers land; they are intentionally omitted until then so the
/// public surface never advertises a capability the library cannot deliver.
/// </para>
/// </remarks>
public enum EdgeRouting
{
    /// <summary>
    /// Axis-aligned, obstacle-avoiding routing: connectors are drawn as a sequence of horizontal and
    /// vertical segments that steer around intervening boxes. This is the convention for block,
    /// state, and activity diagrams and is realized by the library's internal orthogonal edge router.
    /// </summary>
    Orthogonal = 0,
}
