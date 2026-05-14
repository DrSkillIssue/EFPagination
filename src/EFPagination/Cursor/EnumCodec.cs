using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace EFPagination.Cursor;

internal sealed class EnumCodec<TEnum> : ColumnCodec<TEnum>
    where TEnum : struct, Enum
{
    public override byte Kind => CursorFormat.KindEnum;

    public override void Write(ref CursorWriter writer, TEnum value)
    {
        switch (Unsafe.SizeOf<TEnum>())
        {
            case 1: writer.WriteByte(Unsafe.As<TEnum, byte>(ref value)); break;
            case 2: writer.WriteUInt16(Unsafe.As<TEnum, ushort>(ref value)); break;
            case 4: writer.WriteUInt32(Unsafe.As<TEnum, uint>(ref value)); break;
            case 8: writer.WriteUInt64(Unsafe.As<TEnum, ulong>(ref value)); break;
            default: throw new NotSupportedException($"Enum '{typeof(TEnum)}' has unsupported size {Unsafe.SizeOf<TEnum>()}.");
        }
    }

    public override TEnum Read(ref CursorReader reader)
    {
        TEnum result = default;
        switch (Unsafe.SizeOf<TEnum>())
        {
            case 1: Unsafe.As<TEnum, byte>(ref result) = reader.ReadByte(); break;
            case 2: Unsafe.As<TEnum, ushort>(ref result) = reader.ReadUInt16(); break;
            case 4: Unsafe.As<TEnum, uint>(ref result) = reader.ReadUInt32(); break;
            case 8: Unsafe.As<TEnum, ulong>(ref result) = reader.ReadUInt64(); break;
            default: reader.Failed = true; break;
        }
        return result;
    }
}

internal sealed class NullableEnumCodec<TEnum> : ColumnCodec<TEnum?>
    where TEnum : struct, Enum
{
    private readonly EnumCodec<TEnum> _inner = new();
    public override byte Kind => CursorFormat.KindEnum;

    public override void Write(ref CursorWriter writer, TEnum? value)
    {
        if (!value.HasValue)
            throw new InvalidOperationException("Null nullable enum should be handled by the null bitmap.");
        _inner.Write(ref writer, value.GetValueOrDefault());
    }

    public override TEnum? Read(ref CursorReader reader) => _inner.Read(ref reader);
}

/// <summary>
/// Lazy factory for closed-generic enum codecs (with optional <see cref="Nullable{T}"/> wrapping).
/// </summary>
internal static class EnumCodecFactory
{
    private static readonly ConcurrentDictionary<(Type Underlying, bool Nullable), ColumnCodec> s_cache = new();

    public static ColumnCodec Create(Type enumType, bool nullable = false)
        => s_cache.GetOrAdd((enumType, nullable), static key =>
        {
            var open = key.Nullable ? typeof(NullableEnumCodec<>) : typeof(EnumCodec<>);
            return (ColumnCodec)Activator.CreateInstance(open.MakeGenericType(key.Underlying))!;
        });
}

internal static class EnumTypeRegistry
{
    private static readonly ConcurrentDictionary<string, Type> s_byStableName = new();
    private static readonly ConcurrentDictionary<Type, string> s_byType = new();

    public static string Register(Type enumType)
        => s_byType.GetOrAdd(enumType, static t =>
        {
            var fullName = t.FullName ?? throw new NotSupportedException($"Enum type '{t}' does not have a full name.");
            var assemblyName = t.Assembly.GetName().Name ?? throw new NotSupportedException($"Enum type '{t}' assembly does not have a name.");
            var stableName = string.Concat(fullName, ", ", assemblyName);
            s_byStableName.TryAdd(stableName, t);
            return stableName;
        });

    public static bool TryResolve(string stableName, out Type enumType)
        => s_byStableName.TryGetValue(stableName, out enumType!);
}
