using System.Collections;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

/// <summary>
/// Zero-copy reverse-indexed view over an <see cref="IReadOnlyList{T}"/>.
/// Indexer and enumerator both access the underlying list in reverse with no allocation
/// beyond the <see cref="Enumerator"/> struct itself (which is stack-allocated via duck-typing).
/// </summary>
internal sealed class ReversedReadOnlyList<T>(IReadOnlyList<T> inner) : IReadOnlyList<T>
{
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => inner.Count;
    }

    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => inner[inner.Count - 1 - index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(inner);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => new EnumeratorObject(inner);

    IEnumerator IEnumerable.GetEnumerator() => new EnumeratorObject(inner);

    /// <summary>
    /// Value-type enumerator — stack-allocated when consumed via duck-typed foreach.
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

        public T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _inner[_index];
        }

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
