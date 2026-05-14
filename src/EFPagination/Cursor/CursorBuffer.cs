using System.Buffers;
using System.Runtime.CompilerServices;

namespace EFPagination.Cursor;

/// <summary>
/// A growable write-only byte buffer backed by <see cref="ArrayPool{T}"/>. Used by
/// <see cref="CursorWriter"/> as the underlying byte sink and by <see cref="CursorBufferPool"/>
/// for instance reuse across cursor encodings.
/// </summary>
internal sealed class CursorBuffer
{
    private const int DefaultCapacity = 256;

    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(DefaultCapacity);

    /// <summary>
    /// The number of bytes written into the buffer so far.
    /// </summary>
    public int Written;

    /// <summary>
    /// Gets a read-only view over the bytes written so far.
    /// </summary>
    /// <value>A <see cref="ReadOnlySpan{T}"/> of length <see cref="Written"/>.</value>
    public ReadOnlySpan<byte> WrittenSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _buffer.AsSpan(0, Written);
    }

    /// <summary>
    /// Sets the bit at <paramref name="bitIndex"/> inside the null bitmap located at
    /// <paramref name="byteOffset"/>.
    /// </summary>
    /// <param name="byteOffset">The offset of the bitmap region in the buffer.</param>
    /// <param name="bitIndex">The bit index within the bitmap (column ordinal).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBit(int byteOffset, int bitIndex)
        => _buffer[byteOffset + (bitIndex >>> 3)] |= (byte)(1 << (bitIndex & 7));

    /// <summary>
    /// Resets <see cref="Written"/> to zero without releasing the backing array.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => Written = 0;

    /// <summary>
    /// Returns a span at the current write position, growing the buffer if <paramref name="sizeHint"/>
    /// bytes would not fit.
    /// </summary>
    /// <param name="sizeHint">The minimum number of writable bytes the caller requires.</param>
    /// <returns>A span starting at the current write position.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan(int sizeHint)
    {
        if ((uint)Written + (uint)sizeHint > (uint)_buffer.Length)
            Grow(sizeHint);
        return _buffer.AsSpan(Written);
    }

    /// <summary>
    /// Advances the write cursor by <paramref name="n"/> bytes.
    /// </summary>
    /// <param name="n">The number of bytes just written.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int n) => Written += n;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int sizeHint)
    {
        var newSize = Math.Max(_buffer.Length * 2, Written + sizeHint);
        var next = ArrayPool<byte>.Shared.Rent(newSize);
        _buffer.AsSpan(0, Written).CopyTo(next);
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = next;
    }
}

/// <summary>
/// A bounded lock-free pool of <see cref="CursorBuffer"/> instances reused across cursor
/// encoding calls to avoid per-call allocation.
/// </summary>
internal static class CursorBufferPool
{
    private const int MaxRetained = 16;
    private static readonly CursorBuffer?[] s_slots = new CursorBuffer?[MaxRetained];

    /// <summary>
    /// Rents a reset buffer from the pool, allocating a fresh instance on miss.
    /// </summary>
    /// <returns>A <see cref="CursorBuffer"/> with <see cref="CursorBuffer.Written"/> at zero.</returns>
    public static CursorBuffer Rent()
    {
        for (var i = 0; i < s_slots.Length; i++)
        {
            var slot = Interlocked.Exchange(ref s_slots[i], null);
            if (slot is not null)
            {
                slot.Reset();
                return slot;
            }
        }
        return new CursorBuffer();
    }

    /// <summary>
    /// Returns <paramref name="buffer"/> to the pool. Resets the write cursor; surplus buffers
    /// past the retention cap are discarded.
    /// </summary>
    /// <param name="buffer">The buffer to return.</param>
    public static void Return(CursorBuffer buffer)
    {
        buffer.Reset();
        for (var i = 0; i < s_slots.Length; i++)
        {
            if (Interlocked.CompareExchange(ref s_slots[i], buffer, null) is null)
                return;
        }
    }
}
