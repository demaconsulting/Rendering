// <copyright file="MinHeap.cs" company="DemaConsulting">
// Copyright (c) DemaConsulting. All rights reserved.
// </copyright>

namespace DemaConsulting.Rendering.Layout.Engine;

/// <summary>
/// A minimum-priority queue over <typeparamref name="TElement"/> keyed by
/// <typeparamref name="TPriority"/>, exposing only the operations the routing search needs.
/// </summary>
/// <typeparam name="TElement">The type of the queued elements.</typeparam>
/// <typeparam name="TPriority">The type of the priorities, ordered by its default comparer.</typeparam>
/// <remarks>
/// <para>
/// The layout engine owns this heap rather than using <c>System.Collections.Generic.PriorityQueue</c>
/// so that every target framework runs the same code. That type is unavailable on
/// <c>netstandard2.0</c>, and a framework-conditional substitute would let equal-priority elements
/// come out in a different order depending on the runtime, which would in turn make the same diagram
/// lay out differently.
/// </para>
/// <para>
/// The implementation is a four-ary min-heap. Elements whose priorities compare equal are surrendered
/// in an unspecified order, but that order is a pure function of the sequence of operations, so a
/// given diagram always produces the same result.
/// </para>
/// </remarks>
internal sealed class MinHeap<TElement, TPriority>
{
    /// <summary>Number of children per node. A wider heap trades deeper sift-down scans for a shallower tree.</summary>
    private const int Arity = 4;

    /// <summary>Log base two of <see cref="Arity"/>, used to turn index division into a shift.</summary>
    private const int Log2Arity = 2;

    /// <summary>Comparer establishing the priority ordering.</summary>
    private static readonly IComparer<TPriority> Comparer = System.Collections.Generic.Comparer<TPriority>.Default;

    /// <summary>Backing array holding the heap in level order; only the first <see cref="_size"/> entries are live.</summary>
    private (TElement Element, TPriority Priority)[] _nodes = [];

    /// <summary>Number of live entries in <see cref="_nodes"/>.</summary>
    private int _size;

    /// <summary>Gets the number of elements currently queued.</summary>
    public int Count => _size;

    /// <summary>Adds an element with the given priority.</summary>
    /// <param name="element">The element to queue.</param>
    /// <param name="priority">The priority to order it by; lower priorities are dequeued first.</param>
    public void Enqueue(TElement element, TPriority priority)
    {
        if (_size == _nodes.Length)
        {
            Grow(_size + 1);
        }

        var index = _size++;
        MoveUp((element, priority), index);
    }

    /// <summary>Removes and returns the element with the lowest priority.</summary>
    /// <returns>The element with the lowest priority.</returns>
    /// <exception cref="InvalidOperationException">The queue is empty.</exception>
    public TElement Dequeue()
    {
        if (_size == 0)
        {
            throw new InvalidOperationException("The queue is empty.");
        }

        var element = _nodes[0].Element;
        RemoveRootNode();
        return element;
    }

    /// <summary>Expands the backing array to hold at least <paramref name="minCapacity"/> entries.</summary>
    /// <param name="minCapacity">The smallest capacity that must fit.</param>
    private void Grow(int minCapacity)
    {
        // Double the array, starting at four, and fall back to the exact requirement on overflow
        var capacity = _nodes.Length == 0 ? 4 : 2 * _nodes.Length;
        if (capacity < minCapacity)
        {
            capacity = minCapacity;
        }

        Array.Resize(ref _nodes, capacity);
    }

    /// <summary>Drops the root and restores the heap property.</summary>
    private void RemoveRootNode()
    {
        var lastNodeIndex = --_size;

        if (lastNodeIndex > 0)
        {
            // Sift the former last node down from the root to close the gap
            MoveDown(_nodes[lastNodeIndex], 0);
        }

        // Release the vacated slot so it does not pin the element or priority
        _nodes[lastNodeIndex] = default;
    }

    /// <summary>Sifts <paramref name="node"/> up from <paramref name="nodeIndex"/> until its parent is no greater.</summary>
    /// <param name="node">The node being placed.</param>
    /// <param name="nodeIndex">The index the node currently occupies.</param>
    private void MoveUp((TElement Element, TPriority Priority) node, int nodeIndex)
    {
        var nodes = _nodes;

        while (nodeIndex > 0)
        {
            var parentIndex = (nodeIndex - 1) >> Log2Arity;
            var parent = nodes[parentIndex];

            if (Comparer.Compare(node.Priority, parent.Priority) >= 0)
            {
                break;
            }

            nodes[nodeIndex] = parent;
            nodeIndex = parentIndex;
        }

        nodes[nodeIndex] = node;
    }

    /// <summary>Sifts <paramref name="node"/> down from <paramref name="nodeIndex"/> until no child is smaller.</summary>
    /// <param name="node">The node being placed.</param>
    /// <param name="nodeIndex">The index the node currently occupies.</param>
    private void MoveDown((TElement Element, TPriority Priority) node, int nodeIndex)
    {
        var nodes = _nodes;
        var size = _size;

        int firstChildIndex;
        while ((firstChildIndex = (nodeIndex << Log2Arity) + 1) < size)
        {
            // Find the smallest child, preferring the earliest on ties so the order stays reproducible
            var minChild = nodes[firstChildIndex];
            var minChildIndex = firstChildIndex;

            var childIndexUpperBound = Math.Min(firstChildIndex + Arity, size);
            for (var childIndex = firstChildIndex + 1; childIndex < childIndexUpperBound; childIndex++)
            {
                var child = nodes[childIndex];
                if (Comparer.Compare(child.Priority, minChild.Priority) < 0)
                {
                    minChild = child;
                    minChildIndex = childIndex;
                }
            }

            if (Comparer.Compare(node.Priority, minChild.Priority) <= 0)
            {
                break;
            }

            nodes[nodeIndex] = minChild;
            nodeIndex = minChildIndex;
        }

        nodes[nodeIndex] = node;
    }
}
