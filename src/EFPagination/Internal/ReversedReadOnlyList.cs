using System.Collections;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

/// <summary>
/// Zero-copy reverse-indexed view over an <see cref="IReadOnlyList{T}"/>. The indexer and
/// enumerator both access <paramref name="inner"/> in reverse without allocating, beyond the
/// stack-allocated <see cref="Enumerator"/> struct returned by duck-typed <c>foreach</c>.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="inner">The underlying list to expose in reverse.</param>
internal sealed class ReversedReadOnlyList<T>(IReadOnlyList<T> inner) : IReadOnlyList<T>
{
    /// <inheritdoc/>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => inner.Count;
    }

    /// <inheritdoc/>
    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => inner[inner.Count - 1 - index];
    }

    /// <summary>
    /// Returns a stack-allocatable enumerator that walks the underlying list in reverse.
    /// </summary>
    /// <returns>A value-type <see cref="Enumerator"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(inner);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => new EnumeratorObject(inner);

    IEnumerator IEnumerable.GetEnumerator() => new EnumeratorObject(inner);

    /// <summary>
    /// Value-type enumerator that walks the underlying list in reverse. Stack-allocated when
    /// consumed via duck-typed <c>foreach</c>.
    /// </summary>
    public struct Enumerator
    {
        private readonly IReadOnlyList<T> _inner;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(IReadOnlyList<T> inner)
        {
            _inner = inner;
            _index = inner.Count;
        }

        /// <summary>
        /// Gets the element at the current enumerator position.
        /// </summary>
        public T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _inner[_index];
        }

        /// <summary>
        /// Advances the enumerator one element in reverse order.
        /// </summary>
        /// <returns><see langword="true"/> if another element is available; otherwise <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() => --_index >= 0;
    }

    /// <summary>
    /// Reference-type enumerator for interface dispatch (LINQ, non-duck-typed consumers).
    /// </summary>
    private sealed class EnumeratorObject(IReadOnlyList<T> inner) : IEnumerator<T>
    {
        private int _index = inner.Count;

        public T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => inner[_index];
        }

        object? IEnumerator.Current => Current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() => --_index >= 0;

        public void Reset() => _index = inner.Count;

        public void Dispose() { }
    }
}
