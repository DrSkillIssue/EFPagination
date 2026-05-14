using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using EFPagination.Internal;

namespace EFPagination.Cursor;

internal static class CursorEncoder
{
    public static string EncodeSchemaBound<T>(
        PaginationQueryDefinition<T> definition,
        T reference,
        PaginationCursorOptions options) where T : notnull
        => EncodeCore(options, new SchemaBoundFromReferenceBody<T>(definition, reference, options));

    public static string EncodeSchemaBoundFromBindings<T>(
        PaginationQueryDefinition<T> definition,
        ColumnBinding[] bindings,
        PaginationCursorOptions options)
        => EncodeCore(options, new SchemaBoundFromBindingsBody<T>(definition, bindings, options));

    public static string EncodeTagged(ReadOnlySpan<ColumnValue> values, PaginationCursorOptions options)
        => EncodeCore(options, new TaggedBody(values, options));

    /// <summary>
    /// Shared encode envelope: pool rent → body write → HMAC trailer → base64url → pool return.
    /// </summary>
    [SkipLocalsInit]
    private static string EncodeCore<TBody>(PaginationCursorOptions options, scoped in TBody body)
        where TBody : struct, IEncodeBody, allows ref struct
    {
        var buffer = CursorBufferPool.Rent();
        try
        {
            var writer = new CursorWriter(buffer);
            body.Write(ref writer);
            AppendHmac(buffer, options.SigningKey);
            return Base64UrlEncode(buffer);
        }
        finally
        {
            CursorBufferPool.Return(buffer);
        }
    }

    private interface IEncodeBody
    {
        void Write(scoped ref CursorWriter writer);
    }

    private readonly ref struct SchemaBoundFromReferenceBody<T> : IEncodeBody where T : notnull
    {
        private readonly PaginationQueryDefinition<T> _definition;
        private readonly T _reference;
        private readonly PaginationCursorOptions _options;

        public SchemaBoundFromReferenceBody(PaginationQueryDefinition<T> definition, T reference, PaginationCursorOptions options)
        { _definition = definition; _reference = reference; _options = options; }

        public void Write(ref CursorWriter writer)
        {
            var columns = _definition.Columns;
            WriteHeader(ref writer, _options, _definition.SchemaFingerprint, columns.Length, schemaBound: true);
            var nullMaskStart = writer.ReserveZeros((columns.Length + 7) >>> 3);
            for (var i = 0; i < columns.Length; i++)
            {
                if (!columns[i].TryWriteCursorValueFromReference(_reference, ref writer))
                    writer.SetBit(nullMaskStart, i);
            }
        }
    }

    private readonly ref struct SchemaBoundFromBindingsBody<T> : IEncodeBody
    {
        private readonly PaginationQueryDefinition<T> _definition;
        private readonly ColumnBinding[] _bindings;
        private readonly PaginationCursorOptions _options;

        public SchemaBoundFromBindingsBody(PaginationQueryDefinition<T> definition, ColumnBinding[] bindings, PaginationCursorOptions options)
        { _definition = definition; _bindings = bindings; _options = options; }

        public void Write(ref CursorWriter writer)
        {
            var columns = _definition.Columns;
            var count = Math.Min(columns.Length, _bindings.Length);
            WriteHeader(ref writer, _options, _definition.SchemaFingerprint, count, schemaBound: true);
            var nullMaskStart = writer.ReserveZeros((count + 7) >>> 3);
            for (var i = 0; i < count; i++)
            {
                if (!columns[i].TryWriteCursorValueFromBinding(_bindings[i], ref writer))
                    writer.SetBit(nullMaskStart, i);
            }
        }
    }

    private readonly ref struct TaggedBody : IEncodeBody
    {
        private readonly ReadOnlySpan<ColumnValue> _values;
        private readonly PaginationCursorOptions _options;

        public TaggedBody(ReadOnlySpan<ColumnValue> values, PaginationCursorOptions options)
        { _values = values; _options = options; }

        public void Write(ref CursorWriter writer)
        {
            WriteHeader(ref writer, _options, _options.SchemaFingerprint ?? 0, _values.Length, schemaBound: false);
            for (var i = 0; i < _values.Length; i++)
                WriteTaggedValue(ref writer, _values[i].Value);
        }
    }

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
