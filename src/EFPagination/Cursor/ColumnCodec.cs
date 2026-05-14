using System.Collections.Frozen;
using System.Runtime.CompilerServices;

namespace EFPagination.Cursor;

internal abstract class ColumnCodec
{
    public abstract byte Kind { get; }

    public abstract void WriteBoxed(ref CursorWriter writer, object value);

    public abstract object ReadBoxed(ref CursorReader reader);
}

internal abstract class ColumnCodec<TKey> : ColumnCodec
{
    public abstract void Write(ref CursorWriter writer, TKey value);

    public abstract TKey Read(ref CursorReader reader);

    public sealed override void WriteBoxed(ref CursorWriter writer, object value)
        => Write(ref writer, (TKey)value);

    public sealed override object ReadBoxed(ref CursorReader reader)
        => Read(ref reader)!;
}

internal static class ColumnCodecRegistry
{
    private const int KindTableSize = CursorFormat.KindEnum + 1;

    private static readonly (FrozenDictionary<Type, ColumnCodec> ByType, ColumnCodec?[] ByKind) s_tables = BuildTables();

    private static (FrozenDictionary<Type, ColumnCodec>, ColumnCodec?[]) BuildTables()
    {
        var byType = new Dictionary<Type, ColumnCodec>();
        var byKind = new ColumnCodec?[KindTableSize];

        RegisterRef(byType, byKind, new StringCodec());
        RegisterPair(byType, byKind, new BoolCodec());
        RegisterPair(byType, byKind, new CharCodec());
        RegisterPair(byType, byKind, new ByteCodec());
        RegisterPair(byType, byKind, new SByteCodec());
        RegisterPair(byType, byKind, new Int16Codec());
        RegisterPair(byType, byKind, new UInt16Codec());
        RegisterPair(byType, byKind, new Int32Codec());
        RegisterPair(byType, byKind, new UInt32Codec());
        RegisterPair(byType, byKind, new Int64Codec());
        RegisterPair(byType, byKind, new UInt64Codec());
        RegisterPair(byType, byKind, new SingleCodec());
        RegisterPair(byType, byKind, new DoubleCodec());
        RegisterPair(byType, byKind, new DecimalCodec());
        RegisterPair(byType, byKind, new GuidCodec());
        RegisterPair(byType, byKind, new DateTimeCodec());
        RegisterPair(byType, byKind, new DateTimeOffsetCodec());
        RegisterPair(byType, byKind, new DateOnlyCodec());
        RegisterPair(byType, byKind, new TimeOnlyCodec());
        RegisterPair(byType, byKind, new TimeSpanCodec());

        return (byType.ToFrozenDictionary(), byKind);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ColumnCodec<TKey> Resolve<TKey>()
    {
        var underlying = Nullable.GetUnderlyingType(typeof(TKey)) ?? typeof(TKey);

        if (underlying.IsEnum)
        {
            return typeof(TKey) == underlying
                ? (ColumnCodec<TKey>)EnumCodecFactory.Create(underlying)
                : (ColumnCodec<TKey>)NullableEnumCodecFactory.Create(underlying);
        }

        if (s_tables.ByType.TryGetValue(typeof(TKey), out var direct))
            return (ColumnCodec<TKey>)direct;

        throw new NotSupportedException($"Cursor codec for type '{typeof(TKey)}' is not supported.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ColumnCodec? GetByKind(byte kind)
    {
        var byKind = s_tables.ByKind;
        return kind < (uint)byKind.Length ? byKind[kind] : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryResolveByType(Type type, out ColumnCodec codec)
        => s_tables.ByType.TryGetValue(type, out codec!);

    private static void RegisterRef<T>(
        Dictionary<Type, ColumnCodec> byType,
        ColumnCodec?[] byKind,
        ColumnCodec<T> codec) where T : class
    {
        byType[typeof(T)] = codec;
        byKind[codec.Kind] = codec;
    }

    private static void RegisterPair<T>(
        Dictionary<Type, ColumnCodec> byType,
        ColumnCodec?[] byKind,
        ColumnCodec<T> codec) where T : struct
    {
        byType[typeof(T)] = codec;
        byKind[codec.Kind] = codec;
        byType[typeof(T?)] = new NullableValueCodec<T>(codec);
    }
}
