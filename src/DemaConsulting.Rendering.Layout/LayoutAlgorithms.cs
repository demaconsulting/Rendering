// <copyright file="LayoutAlgorithms.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

using DemaConsulting.Rendering.Abstractions;

namespace DemaConsulting.Rendering.Layout;

/// <summary>
/// Convenience factory for a <see cref="LayoutAlgorithmRegistry"/> pre-populated with the bundled
/// layout algorithms. This is the "batteries-included" entry point that saves callers from wiring the
/// registry by hand: a single call returns a registry from which every bundled algorithm resolves by
/// its identifier.
/// </summary>
/// <remarks>
///     <para>
///     The factory lives in the <c>DemaConsulting.Rendering.Layout</c> package rather than in
///     <c>Rendering.Abstractions</c> because it references the concrete bundled algorithms. The
///     <see cref="LayoutAlgorithmRegistry"/> type itself belongs to Abstractions (which knows nothing of
///     any particular algorithm); this factory populates that registry from the Layout package, keeping
///     the dependency direction intact (model &lt;- Abstractions &lt;- Layout).
///     </para>
///     <para>
///     The returned registry contains the three bundled algorithms:
///     <see cref="LayeredLayoutAlgorithm"/> (<c>layered</c>), <see cref="ContainmentLayoutAlgorithm"/>
///     (<c>containment</c>), and <see cref="HierarchicalLayoutAlgorithm"/> (<c>hierarchical</c>). Each
///     call returns a fresh, independently mutable registry, so a caller may register additional
///     algorithms or replace a bundled one without affecting any other caller.
///     </para>
/// </remarks>
/// <example>
///     <code>
///     // Assemble the bundled algorithms once, then resolve by identifier at run time.
///     var registry = LayoutAlgorithms.CreateDefaultRegistry();
///     var algorithm = registry.Resolve("containment");
///     var tree = algorithm.Apply(graph);
///
///     // The registry is a normal registry: extend it with a custom algorithm if needed.
///     registry.Register(new MyTreeLayoutAlgorithm());
///     </code>
/// </example>
public static class LayoutAlgorithms
{
    /// <summary>
    /// Creates a <see cref="LayoutAlgorithmRegistry"/> populated with the three bundled layout
    /// algorithms — <see cref="LayeredLayoutAlgorithm"/> (<c>layered</c>),
    /// <see cref="ContainmentLayoutAlgorithm"/> (<c>containment</c>), and
    /// <see cref="HierarchicalLayoutAlgorithm"/> (<c>hierarchical</c>).
    /// </summary>
    /// <returns>
    /// A new registry from which each bundled algorithm resolves by its <see cref="LayoutAlgorithmBase.Id"/>.
    /// The instance is freshly allocated on every call, so callers may safely add or replace algorithms.
    /// </returns>
    public static LayoutAlgorithmRegistry CreateDefaultRegistry() =>
        new LayoutAlgorithmRegistry()
            .Register(new LayeredLayoutAlgorithm())
            .Register(new ContainmentLayoutAlgorithm())
            .Register(new HierarchicalLayoutAlgorithm());
}
