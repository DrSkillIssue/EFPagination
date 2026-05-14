using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using EFPagination.Internal;

namespace EFPagination.Cursor;

internal static class CursorEncoder
{
    [SkipLocalsInit]
    public static string EncodeSchemaBound<T>(
        PaginationQueryDefinition<T> definition,
        T reference,
        PaginationCursorOptions options) where T : notnull
    {
        var buffer = CursorBufferPool.Rent();
        try
        {
            var writer = new CursorWriter(buffer);
            var columns = definition.Columns;

            WriteHeaderSchemaBound(ref writer, options, definition.SchemaFingerprint, columns.Length);

            var nullMaskLength = (columns.Length + 7) >>> 3;
            var nullMaskStart = writer.Position;
            for (var i = 0; i < nullMaskLength; i++)
                writer.WriteByte(0);

            for (var i = 0; i < columns.Length; i++)
            {
                if (!columns[i].TryWriteCursorValueFromReference(reference, ref writer))
                {
                    writer.SetBit(nullMaskStart, i);
                }
            }

            AppendHmac(buffer, options.SigningKey);
            return Base64UrlEncode(buffer);
        }
        finally
        {
            CursorBufferPool.Return(buffer);
        }
    }

    [SkipLocalsInit]
    public static string EncodeSchemaBoundFromBindings<T>(
        PaginationQueryDefinition<T> definition,
        ColumnBinding[] bindings,
        PaginationCursorOptions options)
    {
        var buffer = CursorBufferPool.Rent();
        try
        {
            var writer = new CursorWriter(buffer);
            var columns = definition.Columns;
            var count = Math.Min(columns.Length, bindings.Length);

            WriteHeaderSchemaBound(ref writer, options, definition.SchemaFingerprint, count);

            var nullMaskLength = (count + 7) >>> 3;
            var nullMaskStart = writer.Position;
            for (var i = 0; i < nullMaskLength; i++)
                writer.WriteByte(0);

            for (var i = 0; i < count; i++)
            {
                if (!columns[i].TryWriteCursorValueFromBinding(bindings[i], ref writer))
                {
                    writer.SetBit(nullMaskStart, i);
                }
            }

            AppendHmac(buffer, options.SigningKey);
            return Base64UrlEncode(buffer);
        }
        finally
        {
            CursorBufferPool.Return(buffer);
        }
    }

    [SkipLocalsInit]
    public static string EncodeTagged(ReadOnlySpan<ColumnValue> values, PaginationCursorOptions options)
    {
        var buffer = CursorBufferPool.Rent();
        try
        {
            var writer = new CursorWriter(buffer);
            WriteHeaderTagged(ref writer, options, valueCount: values.Length);

            for (var i = 0; i < values.Length; i++)
                WriteTaggedValue(ref writer, values[i].Value);

            AppendHmac(buffer, options.SigningKey);
            return Base64UrlEncode(buffer);
        }
        finally
        {
            CursorBufferPool.Return(buffer);
        }
    }

    private static void WriteHeaderSchemaBound(ref CursorWriter writer, PaginationCursorOptions options, uint fingerprint, int valueCount)
        => WriteHeader(ref writer, options, fingerprint, valueCount, schemaBound: true);

    private static void WriteHeaderTagged(ref CursorWriter writer, PaginationCursorOptions options, int valueCount)
        => WriteHeader(ref writer, options, options.SchemaFingerprint ?? 0, valueCount, schemaBound: false);

    private static void WriteHeader(ref CursorWriter writer, PaginationCursorOptions options, uint fingerprint, int valueCount, bool schemaBound)
    {
        var hasFingerprint = schemaBound || options.SchemaFingerprint.HasValue;

        byte flags = 0;
        if (schemaBound) flags |= CursorFormat.FlagSchemaBound;
        if (hasFingerprint) flags |= CursorFormat.FlagFingerprint;
        if (options.SortBy is not null) flags |= CursorFormat.FlagSortBy;
        if (options.TotalCount.HasValue) flags |= CursorFormat.FlagTotalCount;
        if (options.SigningKey is not null) flags |= CursorFormat.FlagSigned;

        writer.WriteByte(CursorFormat.Version);
        writer.WriteByte(flags);

        if (hasFingerprint)
            writer.WriteUInt32(fingerprint);
        if (options.SortBy is not null)
            writer.WriteString(options.SortBy);
        if (options.TotalCount.HasValue)
            writer.WriteVarInt32(options.TotalCount.GetValueOrDefault());

        writer.WriteVarUInt32((uint)valueCount);
    }

    private static void WriteTaggedValue(ref CursorWriter writer, object? value)
    {
        if (value is null)
        {
            writer.WriteByte(CursorFormat.KindNull);
            return;
        }

        var type = value.GetType();

        if (type.IsEnum)
        {
            writer.WriteByte(CursorFormat.KindEnum);
            writer.WriteString(EnumTypeRegistry.Register(type));
            EnumCodecFactory.Create(type).WriteBoxed(ref writer, value);
            return;
        }

        if (!ColumnCodecRegistry.TryResolveByType(type, out var codec))
            throw new NotSupportedException($"Cursor value type '{type}' is not supported.");

        writer.WriteByte(codec.Kind);
        codec.WriteBoxed(ref writer, value);
    }

    private static void AppendHmac(CursorBuffer buffer, byte[]? signingKey)
    {
        if (signingKey is null) return;
        Span<byte> hmacFull = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(signingKey, buffer.WrittenSpan, hmacFull);
        var dest = buffer.GetSpan(CursorFormat.HmacTruncatedLength);
        hmacFull[..CursorFormat.HmacTruncatedLength].CopyTo(dest);
        buffer.Advance(CursorFormat.HmacTruncatedLength);
    }

    private static string Base64UrlEncode(CursorBuffer buffer)
    {
        var len = Base64Url.GetEncodedLength(buffer.Written);
        return string.Create(len, buffer, static (span, state) =>
        {
            Base64Url.TryEncodeToChars(state.WrittenSpan, span, out _);
        });
    }
}
