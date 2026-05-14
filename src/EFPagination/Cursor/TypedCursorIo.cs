using System.Runtime.CompilerServices;

namespace EFPagination.Cursor;

/// <summary>
/// Direct <c>typeof(T) ==</c> chain dispatch for primitive cursor read/write. Each branch is
/// JIT-folded per closed generic, so only the matching arm survives in the specialized code.
/// Enum types are routed via <see cref="EnumIo{TColumn}"/> from the call site.
/// </summary>
internal static class TypedCursorIo
{
    /// <summary>
    /// Writes a typed primitive value to <paramref name="writer"/>.
    /// </summary>
    /// <typeparam name="T">The CLR type to encode (must be one of the supported primitives or its nullable equivalent).</typeparam>
    /// <param name="writer">The cursor writer.</param>
    /// <param name="value">The value to encode.</param>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> is not a supported primitive type.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write<T>(ref CursorWriter writer, T value)
    {
        if (typeof(T) == typeof(string)) { writer.WriteString(Unsafe.As<T, string>(ref value)); return; }

        if (typeof(T) == typeof(bool)) { writer.WriteBool(Unsafe.As<T, bool>(ref value)); return; }
        if (typeof(T) == typeof(char)) { writer.WriteChar(Unsafe.As<T, char>(ref value)); return; }
        if (typeof(T) == typeof(byte)) { writer.WriteByte(Unsafe.As<T, byte>(ref value)); return; }
        if (typeof(T) == typeof(sbyte)) { writer.WriteByte((byte)Unsafe.As<T, sbyte>(ref value)); return; }
        if (typeof(T) == typeof(short)) { writer.WriteInt16(Unsafe.As<T, short>(ref value)); return; }
        if (typeof(T) == typeof(ushort)) { writer.WriteUInt16(Unsafe.As<T, ushort>(ref value)); return; }
        if (typeof(T) == typeof(int)) { writer.WriteInt32(Unsafe.As<T, int>(ref value)); return; }
        if (typeof(T) == typeof(uint)) { writer.WriteUInt32(Unsafe.As<T, uint>(ref value)); return; }
        if (typeof(T) == typeof(long)) { writer.WriteInt64(Unsafe.As<T, long>(ref value)); return; }
        if (typeof(T) == typeof(ulong)) { writer.WriteUInt64(Unsafe.As<T, ulong>(ref value)); return; }
        if (typeof(T) == typeof(float)) { writer.WriteSingle(Unsafe.As<T, float>(ref value)); return; }
        if (typeof(T) == typeof(double)) { writer.WriteDouble(Unsafe.As<T, double>(ref value)); return; }
        if (typeof(T) == typeof(decimal)) { writer.WriteDecimal(Unsafe.As<T, decimal>(ref value)); return; }
        if (typeof(T) == typeof(Guid)) { writer.WriteGuid(Unsafe.As<T, Guid>(ref value)); return; }
        if (typeof(T) == typeof(DateTime)) { writer.WriteDateTime(Unsafe.As<T, DateTime>(ref value)); return; }
        if (typeof(T) == typeof(DateTimeOffset)) { writer.WriteDateTimeOffset(Unsafe.As<T, DateTimeOffset>(ref value)); return; }
        if (typeof(T) == typeof(DateOnly)) { writer.WriteDateOnly(Unsafe.As<T, DateOnly>(ref value)); return; }
        if (typeof(T) == typeof(TimeOnly)) { writer.WriteTimeOnly(Unsafe.As<T, TimeOnly>(ref value)); return; }
        if (typeof(T) == typeof(TimeSpan)) { writer.WriteTimeSpan(Unsafe.As<T, TimeSpan>(ref value)); return; }

        // Nullable<T>: caller pre-checked HasValue=true; GetValueOrDefault returns the bound value.
        if (typeof(T) == typeof(bool?)) { writer.WriteBool(Unsafe.As<T, bool?>(ref value).GetValueOrDefault()); return; }
        if (typeof(T) == typeof(char?)) { writer.WriteChar(Unsafe.As<T, char?>(ref value).GetValueOrDefault()); return; }
        if (typeof(T) == typeof(byte?)) { writer.WriteByte(Unsafe.As<T, byte?>(ref value).GetValueOrDefault()); return; }
        if (typeof(T) == typeof(sbyte?)) { writer.WriteByte((byte)Unsafe.As<T, sbyte?>(ref value).GetValueOrDefault()); return; }
        if (typeof(T) == typeof(short?)) { writer.WriteInt16(Unsafe.As<T, short?>(ref value).GetValueOrDefault()); return; }
        if (typeof(T) == typeof(ushort?)) { writer.WriteUInt16(Unsafe.As<T, ushort?>(ref value).GetValueOrDefault()); return; }
        if (typeof(T) == typeof(int?)) { writer.WriteInt32(Unsafe.As<T, int?>(ref value).GetValueOrDefault()); return; }
        if (typeof(T) == typeof(uint?)) { writer.WriteUInt32(Unsafe.As<T, uint?>(ref value).GetValueOrDefault()); return; }
        if (typeof(T) == typeof(long?)) { writer.WriteInt64(Unsafe.As<T, long?>(ref value).GetValueOrDefault()); return; }
        if (typeof(T) == typeof(ulong?)) { writer.WriteUInt64(Unsafe.As<T, ulong?>(ref value).GetValueOrDefault()); return; }
        if (typeof(T) == typeof(float?)) { writer.WriteSingle(Unsafe.As<T, float?>(ref value).GetValueOrDefault()); return; }
        if (typeof(T) == typeof(double?)) { writer.WriteDouble(Unsafe.As<T, double?>(ref value).GetValueOrDefault()); return; }
        if (typeof(T) == typeof(decimal?)) { writer.WriteDecimal(Unsafe.As<T, decimal?>(ref value).GetValueOrDefault()); return; }
        if (typeof(T) == typeof(Guid?)) { writer.WriteGuid(Unsafe.As<T, Guid?>(ref value).GetValueOrDefault()); return; }
        if (typeof(T) == typeof(DateTime?)) { writer.WriteDateTime(Unsafe.As<T, DateTime?>(ref value).GetValueOrDefault()); return; }
        if (typeof(T) == typeof(DateTimeOffset?)) { writer.WriteDateTimeOffset(Unsafe.As<T, DateTimeOffset?>(ref value).GetValueOrDefault()); return; }
        if (typeof(T) == typeof(DateOnly?)) { writer.WriteDateOnly(Unsafe.As<T, DateOnly?>(ref value).GetValueOrDefault()); return; }
        if (typeof(T) == typeof(TimeOnly?)) { writer.WriteTimeOnly(Unsafe.As<T, TimeOnly?>(ref value).GetValueOrDefault()); return; }
        if (typeof(T) == typeof(TimeSpan?)) { writer.WriteTimeSpan(Unsafe.As<T, TimeSpan?>(ref value).GetValueOrDefault()); return; }

        ThrowUnsupported<T>();
    }

    /// <summary>
    /// Reads a typed primitive value from <paramref name="reader"/>.
    /// </summary>
    /// <typeparam name="T">The CLR type to decode (must be one of the supported primitives or its nullable equivalent).</typeparam>
    /// <param name="reader">The cursor reader.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> is not a supported primitive type.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Read<T>(ref CursorReader reader)
    {
        if (typeof(T) == typeof(string)) { var v = reader.ReadString(); return Unsafe.As<string, T>(ref v); }

        if (typeof(T) == typeof(bool)) { var v = reader.ReadBool(); return Unsafe.As<bool, T>(ref v); }
        if (typeof(T) == typeof(char)) { var v = reader.ReadChar(); return Unsafe.As<char, T>(ref v); }
        if (typeof(T) == typeof(byte)) { var v = reader.ReadByte(); return Unsafe.As<byte, T>(ref v); }
        if (typeof(T) == typeof(sbyte)) { var v = (sbyte)reader.ReadByte(); return Unsafe.As<sbyte, T>(ref v); }
        if (typeof(T) == typeof(short)) { var v = reader.ReadInt16(); return Unsafe.As<short, T>(ref v); }
        if (typeof(T) == typeof(ushort)) { var v = reader.ReadUInt16(); return Unsafe.As<ushort, T>(ref v); }
        if (typeof(T) == typeof(int)) { var v = reader.ReadInt32(); return Unsafe.As<int, T>(ref v); }
        if (typeof(T) == typeof(uint)) { var v = reader.ReadUInt32(); return Unsafe.As<uint, T>(ref v); }
        if (typeof(T) == typeof(long)) { var v = reader.ReadInt64(); return Unsafe.As<long, T>(ref v); }
        if (typeof(T) == typeof(ulong)) { var v = reader.ReadUInt64(); return Unsafe.As<ulong, T>(ref v); }
        if (typeof(T) == typeof(float)) { var v = reader.ReadSingle(); return Unsafe.As<float, T>(ref v); }
        if (typeof(T) == typeof(double)) { var v = reader.ReadDouble(); return Unsafe.As<double, T>(ref v); }
        if (typeof(T) == typeof(decimal)) { var v = reader.ReadDecimal(); return Unsafe.As<decimal, T>(ref v); }
        if (typeof(T) == typeof(Guid)) { var v = reader.ReadGuid(); return Unsafe.As<Guid, T>(ref v); }
        if (typeof(T) == typeof(DateTime)) { var v = reader.ReadDateTime(); return Unsafe.As<DateTime, T>(ref v); }
        if (typeof(T) == typeof(DateTimeOffset)) { var v = reader.ReadDateTimeOffset(); return Unsafe.As<DateTimeOffset, T>(ref v); }
        if (typeof(T) == typeof(DateOnly)) { var v = reader.ReadDateOnly(); return Unsafe.As<DateOnly, T>(ref v); }
        if (typeof(T) == typeof(TimeOnly)) { var v = reader.ReadTimeOnly(); return Unsafe.As<TimeOnly, T>(ref v); }
        if (typeof(T) == typeof(TimeSpan)) { var v = reader.ReadTimeSpan(); return Unsafe.As<TimeSpan, T>(ref v); }

        if (typeof(T) == typeof(bool?)) { bool? v = reader.ReadBool(); return Unsafe.As<bool?, T>(ref v); }
        if (typeof(T) == typeof(char?)) { char? v = reader.ReadChar(); return Unsafe.As<char?, T>(ref v); }
        if (typeof(T) == typeof(byte?)) { byte? v = reader.ReadByte(); return Unsafe.As<byte?, T>(ref v); }
        if (typeof(T) == typeof(sbyte?)) { sbyte? v = (sbyte)reader.ReadByte(); return Unsafe.As<sbyte?, T>(ref v); }
        if (typeof(T) == typeof(short?)) { short? v = reader.ReadInt16(); return Unsafe.As<short?, T>(ref v); }
        if (typeof(T) == typeof(ushort?)) { ushort? v = reader.ReadUInt16(); return Unsafe.As<ushort?, T>(ref v); }
        if (typeof(T) == typeof(int?)) { int? v = reader.ReadInt32(); return Unsafe.As<int?, T>(ref v); }
        if (typeof(T) == typeof(uint?)) { uint? v = reader.ReadUInt32(); return Unsafe.As<uint?, T>(ref v); }
        if (typeof(T) == typeof(long?)) { long? v = reader.ReadInt64(); return Unsafe.As<long?, T>(ref v); }
        if (typeof(T) == typeof(ulong?)) { ulong? v = reader.ReadUInt64(); return Unsafe.As<ulong?, T>(ref v); }
        if (typeof(T) == typeof(float?)) { float? v = reader.ReadSingle(); return Unsafe.As<float?, T>(ref v); }
        if (typeof(T) == typeof(double?)) { double? v = reader.ReadDouble(); return Unsafe.As<double?, T>(ref v); }
        if (typeof(T) == typeof(decimal?)) { decimal? v = reader.ReadDecimal(); return Unsafe.As<decimal?, T>(ref v); }
        if (typeof(T) == typeof(Guid?)) { Guid? v = reader.ReadGuid(); return Unsafe.As<Guid?, T>(ref v); }
        if (typeof(T) == typeof(DateTime?)) { DateTime? v = reader.ReadDateTime(); return Unsafe.As<DateTime?, T>(ref v); }
        if (typeof(T) == typeof(DateTimeOffset?)) { DateTimeOffset? v = reader.ReadDateTimeOffset(); return Unsafe.As<DateTimeOffset?, T>(ref v); }
        if (typeof(T) == typeof(DateOnly?)) { DateOnly? v = reader.ReadDateOnly(); return Unsafe.As<DateOnly?, T>(ref v); }
        if (typeof(T) == typeof(TimeOnly?)) { TimeOnly? v = reader.ReadTimeOnly(); return Unsafe.As<TimeOnly?, T>(ref v); }
        if (typeof(T) == typeof(TimeSpan?)) { TimeSpan? v = reader.ReadTimeSpan(); return Unsafe.As<TimeSpan?, T>(ref v); }

        return ThrowUnsupported<T>();
    }

    private static T ThrowUnsupported<T>()
        => throw new NotSupportedException($"Cursor codec for type '{typeof(T)}' is not supported.");
}
