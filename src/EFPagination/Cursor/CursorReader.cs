using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace EFPagination.Cursor;

[SkipLocalsInit]
internal ref struct CursorReader
{
    private readonly ReadOnlySpan<byte> _buffer;

    public bool Failed { get; set; }

    public int Position { readonly get; private set; }

    public CursorReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
    }

    public readonly int Remaining => _buffer.Length - Position;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadByte()
    {
        if ((uint)Position >= (uint)_buffer.Length)
        {
            Failed = true;
            return 0;
        }
        return _buffer[Position++];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBool() => ReadByte() != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort ReadUInt16()
    {
        if (!TrySlice(2, out var s)) return 0;
        return BinaryPrimitives.ReadUInt16LittleEndian(s);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short ReadInt16() => (short)ReadUInt16();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadUInt32()
    {
        if (!TrySlice(4, out var s)) return 0;
        return BinaryPrimitives.ReadUInt32LittleEndian(s);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt32() => (int)ReadUInt32();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadUInt64()
    {
        if (!TrySlice(8, out var s)) return 0UL;
        return BinaryPrimitives.ReadUInt64LittleEndian(s);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadInt64() => (long)ReadUInt64();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadSingle()
    {
        if (!TrySlice(4, out var s)) return 0f;
        return BinaryPrimitives.ReadSingleLittleEndian(s);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDouble()
    {
        if (!TrySlice(8, out var s)) return 0d;
        return BinaryPrimitives.ReadDoubleLittleEndian(s);
    }

    public decimal ReadDecimal()
    {
        if (!TrySlice(16, out var s)) return 0m;
        Span<int> bits =
        [
            BinaryPrimitives.ReadInt32LittleEndian(s),
            BinaryPrimitives.ReadInt32LittleEndian(s[4..]),
            BinaryPrimitives.ReadInt32LittleEndian(s[8..]),
            BinaryPrimitives.ReadInt32LittleEndian(s[12..]),
        ];
        try
        {
            return new decimal(bits);
        }
        catch (ArgumentException)
        {
            Failed = true;
            return 0m;
        }
    }

    public Guid ReadGuid()
    {
        if (!TrySlice(16, out var s)) return Guid.Empty;
        return new Guid(s, bigEndian: false);
    }

    public DateTime ReadDateTime()
    {
        var ticks = ReadInt64();
        var kindByte = ReadByte();
        if (Failed) return default;
        if (kindByte > (byte)DateTimeKind.Local)
        {
            Failed = true;
            return default;
        }
        if ((ulong)ticks > (ulong)DateTime.MaxValue.Ticks)
        {
            Failed = true;
            return default;
        }
        return new DateTime(ticks, (DateTimeKind)kindByte);
    }

    public DateTimeOffset ReadDateTimeOffset()
    {
        var ticks = ReadInt64();
        var offsetMinutes = ReadInt16();
        if (Failed) return default;
        try
        {
            return new DateTimeOffset(ticks, TimeSpan.FromMinutes(offsetMinutes));
        }
        catch (ArgumentOutOfRangeException)
        {
            Failed = true;
            return default;
        }
    }

    public DateOnly ReadDateOnly()
    {
        var day = ReadInt32();
        if (Failed) return default;
        if ((uint)day > (uint)DateOnly.MaxValue.DayNumber)
        {
            Failed = true;
            return default;
        }
        return DateOnly.FromDayNumber(day);
    }

    public TimeOnly ReadTimeOnly()
    {
        var ticks = ReadInt64();
        if (Failed) return default;
        if ((ulong)ticks > (ulong)TimeOnly.MaxValue.Ticks)
        {
            Failed = true;
            return default;
        }
        return new TimeOnly(ticks);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeSpan ReadTimeSpan() => new(ReadInt64());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public char ReadChar() => (char)ReadUInt16();

    public uint ReadVarUInt32()
    {
        uint result = 0;
        var shift = 0;
        while (true)
        {
            if (shift > 28)
            {
                Failed = true;
                return 0;
            }
            var b = ReadByte();
            if (Failed) return 0;
            result |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return result;
            shift += 7;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadVarInt32()
    {
        var u = ReadVarUInt32();
        return (int)((u >> 1) ^ (uint)-(int)(u & 1));
    }

    public string ReadString()
    {
        var byteLen = ReadVarUInt32();
        if (Failed || byteLen > (uint)Remaining)
        {
            Failed = true;
            return string.Empty;
        }
        var slice = _buffer.Slice(Position, (int)byteLen);
        Position += (int)byteLen;
        return Encoding.UTF8.GetString(slice);
    }

    public ReadOnlySpan<byte> ReadRawBytes(int length)
    {
        if ((uint)length > (uint)Remaining)
        {
            Failed = true;
            return default;
        }
        var slice = _buffer.Slice(Position, length);
        Position += length;
        return slice;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TrySlice(int n, out ReadOnlySpan<byte> slice)
    {
        if (Position + n > _buffer.Length)
        {
            Failed = true;
            slice = default;
            return false;
        }
        slice = _buffer.Slice(Position, n);
        Position += n;
        return true;
    }
}
