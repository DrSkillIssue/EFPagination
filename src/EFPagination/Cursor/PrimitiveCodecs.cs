namespace EFPagination.Cursor;

internal sealed class StringCodec : ColumnCodec<string>
{
    public override byte Kind => CursorFormat.KindString;
    public override void Write(ref CursorWriter writer, string value) => writer.WriteString(value);
    public override string Read(ref CursorReader reader) => reader.ReadString();
}

internal sealed class BoolCodec : ColumnCodec<bool>
{
    public override byte Kind => CursorFormat.KindBoolean;
    public override void Write(ref CursorWriter writer, bool value) => writer.WriteBool(value);
    public override bool Read(ref CursorReader reader) => reader.ReadBool();
}

internal sealed class CharCodec : ColumnCodec<char>
{
    public override byte Kind => CursorFormat.KindChar;
    public override void Write(ref CursorWriter writer, char value) => writer.WriteChar(value);
    public override char Read(ref CursorReader reader) => reader.ReadChar();
}

internal sealed class ByteCodec : ColumnCodec<byte>
{
    public override byte Kind => CursorFormat.KindByte;
    public override void Write(ref CursorWriter writer, byte value) => writer.WriteByte(value);
    public override byte Read(ref CursorReader reader) => reader.ReadByte();
}

internal sealed class SByteCodec : ColumnCodec<sbyte>
{
    public override byte Kind => CursorFormat.KindSByte;
    public override void Write(ref CursorWriter writer, sbyte value) => writer.WriteByte((byte)value);
    public override sbyte Read(ref CursorReader reader) => (sbyte)reader.ReadByte();
}

internal sealed class Int16Codec : ColumnCodec<short>
{
    public override byte Kind => CursorFormat.KindInt16;
    public override void Write(ref CursorWriter writer, short value) => writer.WriteInt16(value);
    public override short Read(ref CursorReader reader) => reader.ReadInt16();
}

internal sealed class UInt16Codec : ColumnCodec<ushort>
{
    public override byte Kind => CursorFormat.KindUInt16;
    public override void Write(ref CursorWriter writer, ushort value) => writer.WriteUInt16(value);
    public override ushort Read(ref CursorReader reader) => reader.ReadUInt16();
}

internal sealed class Int32Codec : ColumnCodec<int>
{
    public override byte Kind => CursorFormat.KindInt32;
    public override void Write(ref CursorWriter writer, int value) => writer.WriteInt32(value);
    public override int Read(ref CursorReader reader) => reader.ReadInt32();
}

internal sealed class UInt32Codec : ColumnCodec<uint>
{
    public override byte Kind => CursorFormat.KindUInt32;
    public override void Write(ref CursorWriter writer, uint value) => writer.WriteUInt32(value);
    public override uint Read(ref CursorReader reader) => reader.ReadUInt32();
}

internal sealed class Int64Codec : ColumnCodec<long>
{
    public override byte Kind => CursorFormat.KindInt64;
    public override void Write(ref CursorWriter writer, long value) => writer.WriteInt64(value);
    public override long Read(ref CursorReader reader) => reader.ReadInt64();
}

internal sealed class UInt64Codec : ColumnCodec<ulong>
{
    public override byte Kind => CursorFormat.KindUInt64;
    public override void Write(ref CursorWriter writer, ulong value) => writer.WriteUInt64(value);
    public override ulong Read(ref CursorReader reader) => reader.ReadUInt64();
}

internal sealed class SingleCodec : ColumnCodec<float>
{
    public override byte Kind => CursorFormat.KindSingle;
    public override void Write(ref CursorWriter writer, float value) => writer.WriteSingle(value);
    public override float Read(ref CursorReader reader) => reader.ReadSingle();
}

internal sealed class DoubleCodec : ColumnCodec<double>
{
    public override byte Kind => CursorFormat.KindDouble;
    public override void Write(ref CursorWriter writer, double value) => writer.WriteDouble(value);
    public override double Read(ref CursorReader reader) => reader.ReadDouble();
}

internal sealed class DecimalCodec : ColumnCodec<decimal>
{
    public override byte Kind => CursorFormat.KindDecimal;
    public override void Write(ref CursorWriter writer, decimal value) => writer.WriteDecimal(value);
    public override decimal Read(ref CursorReader reader) => reader.ReadDecimal();
}

internal sealed class GuidCodec : ColumnCodec<Guid>
{
    public override byte Kind => CursorFormat.KindGuid;
    public override void Write(ref CursorWriter writer, Guid value) => writer.WriteGuid(value);
    public override Guid Read(ref CursorReader reader) => reader.ReadGuid();
}

internal sealed class DateTimeCodec : ColumnCodec<DateTime>
{
    public override byte Kind => CursorFormat.KindDateTime;
    public override void Write(ref CursorWriter writer, DateTime value) => writer.WriteDateTime(value);
    public override DateTime Read(ref CursorReader reader) => reader.ReadDateTime();
}

internal sealed class DateTimeOffsetCodec : ColumnCodec<DateTimeOffset>
{
    public override byte Kind => CursorFormat.KindDateTimeOffset;
    public override void Write(ref CursorWriter writer, DateTimeOffset value) => writer.WriteDateTimeOffset(value);
    public override DateTimeOffset Read(ref CursorReader reader) => reader.ReadDateTimeOffset();
}

internal sealed class DateOnlyCodec : ColumnCodec<DateOnly>
{
    public override byte Kind => CursorFormat.KindDateOnly;
    public override void Write(ref CursorWriter writer, DateOnly value) => writer.WriteDateOnly(value);
    public override DateOnly Read(ref CursorReader reader) => reader.ReadDateOnly();
}

internal sealed class TimeOnlyCodec : ColumnCodec<TimeOnly>
{
    public override byte Kind => CursorFormat.KindTimeOnly;
    public override void Write(ref CursorWriter writer, TimeOnly value) => writer.WriteTimeOnly(value);
    public override TimeOnly Read(ref CursorReader reader) => reader.ReadTimeOnly();
}

internal sealed class TimeSpanCodec : ColumnCodec<TimeSpan>
{
    public override byte Kind => CursorFormat.KindTimeSpan;
    public override void Write(ref CursorWriter writer, TimeSpan value) => writer.WriteTimeSpan(value);
    public override TimeSpan Read(ref CursorReader reader) => reader.ReadTimeSpan();
}

internal sealed class NullableValueCodec<T> : ColumnCodec<T?> where T : struct
{
    private readonly ColumnCodec<T> _inner;
    public NullableValueCodec(ColumnCodec<T> inner) { _inner = inner; }
    public override byte Kind => _inner.Kind;
    public override void Write(ref CursorWriter writer, T? value)
    {
        if (!value.HasValue)
            throw new InvalidOperationException("Null nullable value should be handled by the null bitmap.");
        _inner.Write(ref writer, value.GetValueOrDefault());
    }
    public override T? Read(ref CursorReader reader) => _inner.Read(ref reader);
}
