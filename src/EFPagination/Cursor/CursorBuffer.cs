using System.Buffers;
using System.Runtime.CompilerServices;

namespace EFPagination.Cursor;

internal sealed class CursorBuffer
{
    private const int DefaultCapacity = 256;

    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(DefaultCapacity);

    public int Written;

    public ReadOnlySpan<byte> WrittenSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _buffer.AsSpan(0, Written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBit(int byteOffset, int bitIndex)
        => _buffer[byteOffset + (bitIndex >>> 3)] |= (byte)(1 << (bitIndex & 7));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => Written = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan(int sizeHint)
    {
        if ((uint)Written + (uint)sizeHint > (uint)_buffer.Length)
            Grow(sizeHint);
        return _buffer.AsSpan(Written);
    }

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

internal static class CursorBufferPool
{
    private const int MaxRetained = 16;
    private static readonly CursorBuffer?[] s_slots = new CursorBuffer?[MaxRetained];

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
