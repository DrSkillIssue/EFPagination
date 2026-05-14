using System.Runtime.CompilerServices;

namespace EFPagination.Cursor;

/// <summary>
/// Per-closed-generic enum read/write. <see cref="Instance"/> resolves at static-init to:
/// • a typed <see cref="EnumIoImpl{TEnum}"/> when <typeparamref name="TColumn"/> is an enum,
/// • a typed <see cref="NullableEnumIoImpl{TEnum}"/> when <typeparamref name="TColumn"/> is <see cref="Nullable{T}"/> over an enum,
/// • <see langword="null"/> otherwise (caller routes through <see cref="TypedCursorIo"/>).
/// </summary>
internal abstract class EnumIo<TColumn>
{
    public abstract void Write(ref CursorWriter writer, TColumn value);
    public abstract TColumn Read(ref CursorReader reader);

    /// <summary>
    /// Resolved once per closed generic at static-init time. <see langword="null"/> when
    /// <typeparamref name="TColumn"/> is neither an enum nor <see cref="Nullable{T}"/> over an enum.
    /// </summary>
    public static readonly EnumIo<TColumn>? Instance = CreateInstance();

    private static EnumIo<TColumn>? CreateInstance()
    {
        var underlying = Nullable.GetUnderlyingType(typeof(TColumn)) ?? typeof(TColumn);
        if (!underlying.IsEnum) return null;

        var openType = typeof(TColumn) == underlying
            ? typeof(EnumIoImpl<>).MakeGenericType(underlying)
            : typeof(NullableEnumIoImpl<>).MakeGenericType(underlying);
        return (EnumIo<TColumn>)Activator.CreateInstance(openType)!;
    }
}

internal sealed class EnumIoImpl<TEnum> : EnumIo<TEnum> where TEnum : struct, Enum
{
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

internal sealed class NullableEnumIoImpl<TEnum> : EnumIo<TEnum?> where TEnum : struct, Enum
{
    private readonly EnumIoImpl<TEnum> _inner = new();

    public override void Write(ref CursorWriter writer, TEnum? value)
        => _inner.Write(ref writer, value.GetValueOrDefault());

    public override TEnum? Read(ref CursorReader reader) => _inner.Read(ref reader);
}
