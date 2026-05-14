using System.Runtime.CompilerServices;

namespace EFPagination.Cursor;

/// <summary>
/// Per-closed-generic enum read/write. <see cref="Instance"/> resolves at static-init to one of:
/// <list type="bullet">
///   <item><description>A typed <see cref="EnumIoImpl{TEnum}"/> when <typeparamref name="TColumn"/> is an enum.</description></item>
///   <item><description>A typed <see cref="NullableEnumIoImpl{TEnum}"/> when <typeparamref name="TColumn"/> is <see cref="Nullable{T}"/> over an enum.</description></item>
///   <item><description><see langword="null"/> otherwise; callers then route through <see cref="TypedCursorIo"/>.</description></item>
/// </list>
/// </summary>
/// <typeparam name="TColumn">The column CLR type (an enum, a nullable enum, or unrelated).</typeparam>
internal abstract class EnumIo<TColumn>
{
    /// <summary>
    /// Writes the underlying integer representation of <paramref name="value"/> to <paramref name="writer"/>.
    /// </summary>
    /// <param name="writer">The cursor writer.</param>
    /// <param name="value">The enum (or nullable enum) value to encode.</param>
    public abstract void Write(ref CursorWriter writer, TColumn value);

    /// <summary>
    /// Reads the underlying integer representation back into the enum (or nullable enum) value.
    /// </summary>
    /// <param name="reader">The cursor reader positioned at the enum payload.</param>
    /// <returns>The decoded value.</returns>
    public abstract TColumn Read(ref CursorReader reader);

    /// <summary>
    /// The resolved codec for <typeparamref name="TColumn"/>, or <see langword="null"/> when
    /// the type is neither an enum nor a nullable enum.
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

/// <summary>
/// Typed enum codec that round-trips the underlying integer representation, picking the byte
/// width based on <c>sizeof(TEnum)</c>.
/// </summary>
/// <typeparam name="TEnum">The enum type.</typeparam>
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

/// <summary>
/// Typed codec for <see cref="Nullable{T}"/> over an enum. The null bitmap in the cursor header
/// covers the null case; the codec itself only handles the present-value path.
/// </summary>
/// <typeparam name="TEnum">The underlying enum type.</typeparam>
internal sealed class NullableEnumIoImpl<TEnum> : EnumIo<TEnum?> where TEnum : struct, Enum
{
    private readonly EnumIoImpl<TEnum> _inner = new();

    public override void Write(ref CursorWriter writer, TEnum? value)
        => _inner.Write(ref writer, value.GetValueOrDefault());

    public override TEnum? Read(ref CursorReader reader) => _inner.Read(ref reader);
}
