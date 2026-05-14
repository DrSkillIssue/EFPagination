using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using EFPagination.Internal;

namespace EFPagination.Cursor;

internal static class CursorDecoder
{
    public static bool TryDecodeWithDefinition<T>(
        ReadOnlySpan<char> encoded,
        PaginationQueryDefinition<T> definition,
        ColumnBinding[] bindings,
        byte[]? signingKey,
        out CursorMetadata metadata)
        => DecodeCore(encoded, signingKey, new DefinitionBody<T>(definition, bindings), out metadata);

    public static bool TryDecodeTagged(
        ReadOnlySpan<char> encoded,
        Span<ColumnValue> values,
        byte[]? signingKey,
        out CursorMetadata metadata)
        => DecodeCore(encoded, signingKey, new TaggedBody(values), out metadata);

    /// <summary>
    /// Shared decode envelope: buffer rental + base64url decode + header parse + body dispatch +
    /// remaining-bytes sanity + metadata construction + malformed-cursor exception trapping.
    /// </summary>
    [SkipLocalsInit]
    private static bool DecodeCore<TBody>(
        ReadOnlySpan<char> encoded,
        byte[]? signingKey,
        scoped TBody body,
        out CursorMetadata metadata)
        where TBody : struct, IDecodeBody, allows ref struct
    {
        metadata = default;
        if (encoded.IsEmpty) return false;

        var maxLen = Base64Url.GetMaxDecodedLength(encoded.Length);
        byte[]? rented = null;
        Span<byte> scratch = maxLen <= 256
            ? stackalloc byte[256]
            : (rented = ArrayPool<byte>.Shared.Rent(maxLen));

        try
        {
            if (!Base64Url.TryDecodeFromChars(encoded, scratch, out var bytesWritten))
                return false;

            var payload = scratch[..bytesWritten];
            if (!ParseHeader(payload, signingKey, out var header))
                return false;

            var reader = new CursorReader(payload.Slice(header.BodyStart, header.BodyEnd - header.BodyStart));

            if (!body.TryDecode(ref reader, in header, out var valueCount))
                return false;
            if (reader.Failed || reader.Remaining != 0)
                return false;

            metadata = new CursorMetadata(
                header.SortBy,
                header.TotalCount,
                header.HasFingerprint ? header.Fingerprint : null,
                valueCount);
            return true;
        }
        catch (FormatException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (InvalidCastException) { return false; }
        finally
        {
            if (rented is not null) ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private interface IDecodeBody
    {
        bool TryDecode(ref CursorReader reader, in ParsedHeader header, out int valueCount);
    }

    private readonly ref struct DefinitionBody<T>(PaginationQueryDefinition<T> definition, ColumnBinding[] bindings) : IDecodeBody
    {
        public bool TryDecode(ref CursorReader reader, in ParsedHeader header, out int valueCount)
        {
            if (header.SchemaBound)
            {
                if (!header.HasFingerprint || header.Fingerprint != definition.SchemaFingerprint)
                { valueCount = 0; return false; }
                return DecodeSchemaBoundBody(ref reader, definition, bindings, out valueCount);
            }

            if (header.HasFingerprint && header.Fingerprint != definition.SchemaFingerprint)
            { valueCount = 0; return false; }
            return DecodeTaggedBodyIntoBindings(ref reader, definition, bindings, out valueCount);
        }
    }

    private readonly ref struct TaggedBody : IDecodeBody
    {
        private readonly Span<ColumnValue> _values;
        public TaggedBody(Span<ColumnValue> values) { _values = values; }

        public bool TryDecode(ref CursorReader reader, in ParsedHeader header, out int valueCount)
        {
            if (header.SchemaBound) { valueCount = 0; return false; }
            return DecodeTaggedBodyIntoColumnValues(ref reader, _values, out valueCount);
        }
    }

    private readonly ref struct ParsedHeader
    {
        public bool HasFingerprint { get; init; }
        public bool SchemaBound { get; init; }
        public uint Fingerprint { get; init; }
        public string? SortBy { get; init; }
        public int? TotalCount { get; init; }
        public int BodyStart { get; init; }
        public int BodyEnd { get; init; }
    }

    [SkipLocalsInit]
    private static bool ParseHeader(ReadOnlySpan<byte> payload, byte[]? signingKey, out ParsedHeader header)
    {
        header = default;
        if (payload.Length < 2 || payload[0] != CursorFormat.Version)
            return false;

        var flags = payload[1];
        var signed = (flags & CursorFormat.FlagSigned) != 0;

        int bodyEnd;
        if (signed)
        {
            if (signingKey is null) return false;
            if (payload.Length < 2 + CursorFormat.HmacTruncatedLength) return false;
            bodyEnd = payload.Length - CursorFormat.HmacTruncatedLength;

            Span<byte> expected = stackalloc byte[HMACSHA256.HashSizeInBytes];
            HMACSHA256.HashData(signingKey, payload[..bodyEnd], expected);
            if (!CryptographicOperations.FixedTimeEquals(expected[..CursorFormat.HmacTruncatedLength], payload.Slice(bodyEnd, CursorFormat.HmacTruncatedLength)))
                return false;
        }
        else
        {
            if (signingKey is not null) return false;
            bodyEnd = payload.Length;
        }

        var reader = new CursorReader(payload[2..bodyEnd]);

        var hasFingerprint = (flags & CursorFormat.FlagFingerprint) != 0;
        uint fingerprint = 0;
        if (hasFingerprint)
        {
            fingerprint = reader.ReadUInt32();
            if (reader.Failed) return false;
        }

        string? sortBy = null;
        if ((flags & CursorFormat.FlagSortBy) != 0)
        {
            sortBy = reader.ReadString();
            if (reader.Failed) return false;
        }

        int? totalCount = null;
        if ((flags & CursorFormat.FlagTotalCount) != 0)
        {
            totalCount = reader.ReadVarInt32();
            if (reader.Failed) return false;
        }

        header = new ParsedHeader
        {
            HasFingerprint = hasFingerprint,
            SchemaBound = (flags & CursorFormat.FlagSchemaBound) != 0,
            Fingerprint = fingerprint,
            SortBy = sortBy,
            TotalCount = totalCount,
            BodyStart = 2 + reader.Position,
            BodyEnd = bodyEnd,
        };
        return true;
    }

    private static bool DecodeSchemaBoundBody<T>(ref CursorReader reader, PaginationQueryDefinition<T> definition, ColumnBinding[] bindings, out int valueCount)
    {
        valueCount = 0;
        var count = (int)reader.ReadVarUInt32();
        if (reader.Failed) return false;

        var columns = definition.Columns;
        if (count != columns.Length || bindings.Length < count) return false;

        var nullMaskLength = (count + 7) >>> 3;
        var nullMask = reader.ReadRawBytes(nullMaskLength);
        if (reader.Failed) return false;

        for (var i = 0; i < count; i++)
        {
            var col = columns[i];
            if (((nullMask[i >>> 3] >>> (i & 7)) & 1) != 0)
            {
                if (!col.IsNullable) return false;
                col.WriteBindingFromBoxed(null, bindings[i]);
            }
            else
            {
                col.DecodeCursorValueInto(ref reader, bindings[i]);
                if (reader.Failed) return false;
            }
        }

        valueCount = count;
        return true;
    }

    private static bool DecodeTaggedBodyIntoBindings<T>(ref CursorReader reader, PaginationQueryDefinition<T> definition, ColumnBinding[] bindings, out int valueCount)
    {
        valueCount = 0;
        var count = (int)reader.ReadVarUInt32();
        if (reader.Failed) return false;

        var columns = definition.Columns;
        if (count != columns.Length || bindings.Length < count) return false;

        for (var i = 0; i < count; i++)
        {
            var kind = reader.ReadByte();
            if (reader.Failed) return false;
            if (!columns[i].TryDecodeTaggedValueInto(ref reader, kind, bindings[i]))
                return false;
        }

        valueCount = count;
        return true;
    }

    private static bool DecodeTaggedBodyIntoColumnValues(ref CursorReader reader, Span<ColumnValue> destination, out int valueCount)
    {
        valueCount = 0;
        var count = (int)reader.ReadVarUInt32();
        if (reader.Failed || destination.Length < count) return false;

        for (var i = 0; i < count; i++)
        {
            if (!TryReadTaggedValue(ref reader, out var value))
                return false;
            destination[i] = new ColumnValue(destination[i].Name, value);
        }

        valueCount = count;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryReadTaggedValue(ref CursorReader reader, out object? value)
    {
        value = null;
        var kind = reader.ReadByte();
        if (reader.Failed) return false;

        if (kind == CursorFormat.KindNull) return true;

        if (kind == CursorFormat.KindEnum)
        {
            var typeName = reader.ReadString();
            if (reader.Failed) return false;
            if (!EnumTypeRegistry.TryResolve(typeName, out var enumType))
                return false;
            value = EnumCodecFactory.Create(enumType).ReadBoxed(ref reader);
            return !reader.Failed;
        }

        var codec = ColumnCodecRegistry.GetByKind(kind);
        if (codec is null) return false;
        value = codec.ReadBoxed(ref reader);
        return !reader.Failed;
    }
}
