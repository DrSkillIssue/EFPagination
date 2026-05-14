using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace EFPagination.Cursor;

[SkipLocalsInit]
internal ref struct CursorWriter
{
    private readonly CursorBuffer _buffer;

    public CursorWriter(CursorBuffer buffer)
    {
        _buffer = buffer;
    }

    public readonly int Position => _buffer.Written;

    public readonly ReadOnlySpan<byte> WrittenSpan => _buffer.WrittenSpan;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void SetBit(int byteOffset, int bitIndex) => _buffer.SetBit(byteOffset, bitIndex);

    /// <summary>
    /// Reserves <paramref name="byteCount"/> zeroed bytes and returns the buffer offset where
    /// the region starts (used as the null-bitmap region in schema-bound encoding).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReserveZeros(int byteCount)
    {
        var start = Position;
        _buffer.GetSpan(byteCount)[..byteCount].Clear();
        _buffer.Advance(byteCount);
        return start;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteByte(byte value)
    {
        var span = _buffer.GetSpan(1);
        span[0] = value;
        _buffer.Advance(1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt16(ushort value)
    {
        var span = _buffer.GetSpan(2);
        BinaryPrimitives.WriteUInt16LittleEndian(span, value);
        _buffer.Advance(2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt16(short value) => WriteUInt16((ushort)value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt32(uint value)
    {
        var span = _buffer.GetSpan(4);
        BinaryPrimitives.WriteUInt32LittleEndian(span, value);
        _buffer.Advance(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt32(int value) => WriteUInt32((uint)value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt64(ulong value)
    {
        var span = _buffer.GetSpan(8);
        BinaryPrimitives.WriteUInt64LittleEndian(span, value);
        _buffer.Advance(8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt64(long value) => WriteUInt64((ulong)value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSingle(float value)
    {
        var span = _buffer.GetSpan(4);
        BinaryPrimitives.WriteSingleLittleEndian(span, value);
        _buffer.Advance(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDouble(double value)
    {
        var span = _buffer.GetSpan(8);
        BinaryPrimitives.WriteDoubleLittleEndian(span, value);
        _buffer.Advance(8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDecimal(decimal value)
    {
        var span = _buffer.GetSpan(16);
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
        BinaryPrimitives.WriteInt32LittleEndian(span, bits[0]);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], bits[1]);
        BinaryPrimitives.WriteInt32LittleEndian(span[8..], bits[2]);
        BinaryPrimitives.WriteInt32LittleEndian(span[12..], bits[3]);
        _buffer.Advance(16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteGuid(Guid value)
    {
        var span = _buffer.GetSpan(16);
        value.TryWriteBytes(span, bigEndian: false, out _);
        _buffer.Advance(16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDateTime(DateTime value)
    {
        WriteInt64(value.Ticks);
        WriteByte((byte)value.Kind);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDateTimeOffset(DateTimeOffset value)
    {
        WriteInt64(value.Ticks);
        WriteInt16((short)(value.Offset.Ticks / TimeSpan.TicksPerMinute));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDateOnly(DateOnly value) => WriteInt32(value.DayNumber);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteTimeOnly(TimeOnly value) => WriteInt64(value.Ticks);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteTimeSpan(TimeSpan value) => WriteInt64(value.Ticks);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteChar(char value) => WriteUInt16(value);

    public void WriteVarUInt32(uint value)
    {
        var span = _buffer.GetSpan(5);
        var i = 0;
        while (value >= 0x80)
        {
            span[i++] = (byte)(value | 0x80);
            value >>= 7;
        }
        span[i++] = (byte)value;
        _buffer.Advance(i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteVarInt32(int value) => WriteVarUInt32((uint)((value << 1) ^ (value >> 31)));

    public void WriteString(string value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        WriteVarUInt32((uint)byteCount);
        var dest = _buffer.GetSpan(byteCount);
        Encoding.UTF8.GetBytes(value, dest);
        _buffer.Advance(byteCount);
    }
}
